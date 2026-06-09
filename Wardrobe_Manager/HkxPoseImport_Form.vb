Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports FO4_Base_Library

Public Class HkxPoseImport_Form
    Private WithEvents EditPreviewControl As PreviewControl = Nothing
    Private WithEvents PreviewDebounceTimer As New Timer With {.Interval = 100}
    Private WithEvents PlaybackTimer As New Timer With {.Interval = 33}
    Private ReadOnly _selectedSliderSet As SliderSet_Class
    Private ReadOnly _selectedPreset As SlidersPreset_Class
    Private ReadOnly _selectedSize As WM_Config.SliderSize
    Private ReadOnly _skeletonSource As HkxByteSource
    Private _session As HkxPoseImportSession
    ' Playback compartido (reloj + selección de frame por tiempo + caché de poses). El PlaybackTimer
    ' de WinForms sigue siendo el driver; el player provee FrameForNow()/PoseForFrame().
    Private _player As HkxAnimationPlayer
    Private _lastResult As HkxPoseImportHelper.ImportResult
    Private _lastKey As String = ""
    Private _suppressFrameEvents As Boolean
    Private _suppressPlaybackIntervalEvents As Boolean
    Private HasSaved As Boolean = False

    Public ReadOnly Property ImportedResult As HkxPoseImportHelper.ImportResult
        Get
            Return _lastResult
        End Get
    End Property

    Public Sub New(selectedSliderSet As SliderSet_Class,
                   selectedPreset As SlidersPreset_Class,
                   selectedSize As WM_Config.SliderSize)
        InitializeComponent()
        _selectedSliderSet = selectedSliderSet
        _selectedPreset = selectedPreset
        _selectedSize = selectedSize
        _skeletonSource = ResolveConfiguredSkeletonHkxBytes()

        Dim allowedExts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".hkx"}
        Dim keys = FilesDictionary_class.GetFilteredKeys("Meshes\", allowedExts)
        Logger.LogLazy(Function() $"[HKX-POSE-UI] HKX import form dictionary keys root='Meshes\' count={keys.Count}.")

        DictionaryPicker_Control1.Initialize(keys, "Meshes\", allowedExts)
        DictionaryPicker_Control1.AllowClone = True
        DictionaryPicker_Control1.btnOk.Text = "Import"
        DictionaryPicker_Control1.btnOk.Font = New Font(DictionaryPicker_Control1.btnOk.Font, FontStyle.Bold)
        DictionaryPicker_Control1.btnCancel.Text = "Exit"
        DictionaryPicker_Control1.btnOk.Enabled = False
        FrameSlider.Enabled = False
        ButtonPlay.Enabled = False
    End Sub

    Private Sub HkxPoseImport_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        EditPreviewControl = New PreviewControl With {.Dock = DockStyle.Fill}
        PanelPreview.Controls.Add(EditPreviewControl)
        EditPreviewControl.Model.SingleBoneSkinning = False
        EditPreviewControl.Model.RecalculateNormals = False
        EditPreviewControl.AllowMask = False

        If _selectedSliderSet Is Nothing Then
            SetStatus("No selected slider set. Select a project in Wardrobe Manager before importing HKX poses.", True)
            DictionaryPicker_Control1.btnOk.Enabled = False
        End If
    End Sub

    Private Sub HkxPoseImport_Form_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ' Restore global skeleton
        PreviewDebounceTimer.Stop()
        PlaybackTimer.Stop()
        If EditPreviewControl IsNot Nothing Then EditPreviewControl.PlayingAnimation = False
        SkeletonInstance.Default.LoadFromConfig(True, True)
        If EditPreviewControl IsNot Nothing Then
            EditPreviewControl.Clean()
            EditPreviewControl.Dispose()
        End If
        If Me.HasSaved = True Then
            Me.DialogResult = DialogResult.Yes
        Else
            Me.DialogResult = DialogResult.No
        End If
    End Sub

    Private Sub DictionaryPicker_Control1_SelectionChanged(Key As String) Handles DictionaryPicker_Control1.SelectionChanged
        _lastKey = If(Key, "")
        PreviewDebounceTimer.Stop()
        StopPlayback()
        LoadSelectedHkx(_lastKey)
    End Sub

    Private Sub DictionaryPicker_Control1_OkClicked() Handles DictionaryPicker_Control1.OkClicked
        Try
            If _session Is Nothing Then Return
            Dim poseName = TextBoxPoseName.Text.Trim()
            If String.IsNullOrWhiteSpace(poseName) Then
                MsgBox("Pose name cannot be empty.", vbCritical Or vbOKOnly, "HKX Pose Import")
                Return
            End If

            _lastResult = _session.BuildPose(CurrentFrame(), poseName, collectDiagnostics:=True)
            If _lastResult Is Nothing OrElse _lastResult.ImportedBoneCount = 0 Then
                MsgBox("The HKX animation did not match any bones in the loaded preview skeleton.",
                       vbOKOnly Or vbCritical,
                       "HKX Pose Import")
                Return
            End If
            Me.HasSaved = True
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            Logger.LogLazy(Function() "[HKX-POSE-UI] Import form OK exception: " & ex.ToString())
            MsgBox("Error building HKX pose: " & ex.Message, vbOKOnly Or vbCritical, "HKX Pose Import")
        End Try
    End Sub

    Private Sub DictionaryPicker_Control1_CancelClicked() Handles DictionaryPicker_Control1.CancelClicked
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Private Sub FrameSlider_ValueChanged(sender As Object, e As EventArgs) Handles FrameSlider.ValueChanged
        If _suppressFrameEvents Then Return
        If _session Is Nothing Then Return
        PreviewDebounceTimer.Stop()
        PreviewDebounceTimer.Start()
    End Sub

    Private Sub PreviewDebounceTimer_Tick(sender As Object, e As EventArgs) Handles PreviewDebounceTimer.Tick
        PreviewDebounceTimer.Stop()
        UpdatePreview()
    End Sub

    Private Sub PlaybackTimer_Tick(sender As Object, e As EventArgs) Handles PlaybackTimer.Tick
        If _player Is Nothing OrElse FrameSlider.Maximum <= 0 Then
            StopPlayback()
            Return
        End If

        Dim nextFrame = _player.FrameForNow()
        If nextFrame < 0 OrElse nextFrame = CurrentFrame() Then Return

        _suppressFrameEvents = True
        FrameSlider.Value = nextFrame
        _suppressFrameEvents = False
        UpdatePreview()
    End Sub

    Private Sub ButtonPlay_Click(sender As Object, e As EventArgs) Handles ButtonPlay.Click
        If PlaybackTimer.Enabled Then
            StopPlayback()
        Else
            StartPlayback()
        End If
    End Sub

    Private Sub NumericFrameMs_ValueChanged(sender As Object, e As EventArgs) Handles NumericFrameMs.ValueChanged
        If _suppressPlaybackIntervalEvents Then Return
        Dim fps = Math.Max(1.0, CDbl(NumericFrameMs.Value))   ' el numeric ahora es FPS
        PlaybackTimer.Interval = FpsToCheckInterval(fps)
        If _player IsNot Nothing Then
            _player.TargetFps = fps
            ' Reanclar el reloj al frame actual para que el cambio de FPS no pegue un salto.
            If PlaybackTimer.Enabled Then _player.Rebase(CurrentFrame())
        End If
    End Sub

    ''' <summary>Intervalo del timer de chequeo (ms) a partir del FPS objetivo: ~la mitad del
    ''' tiempo por frame, tope 16ms. El player elige el frame real por reloj; esto es solo cada
    ''' cuánto se chequea.</summary>
    Private Shared Function FpsToCheckInterval(fps As Double) As Integer
        Dim frameMs = 1000.0 / Math.Max(1.0, fps)
        Return Math.Max(1, Math.Min(16, CInt(Math.Floor(frameMs / 2.0))))
    End Function

    Private Sub LoadSelectedHkx(key As String)
        _session = Nothing
        _player = Nothing
        _lastResult = Nothing
        If EditPreviewControl IsNot Nothing Then EditPreviewControl.PlayingAnimation = False
        DictionaryPicker_Control1.btnOk.Enabled = False
        FrameSlider.Enabled = False
        ButtonPlay.Enabled = False

        If _selectedSliderSet Is Nothing Then
            SetStatus("No selected slider set. Select a project in Wardrobe Manager before importing HKX poses.", True)
            Return
        End If

        If String.IsNullOrWhiteSpace(key) Then
            SetStatus("Select an HKX animation.", False)
            Return
        End If

        Try
            Dim animationSource = LoadHkxDictionaryEntryBytes(key)
            If animationSource Is Nothing OrElse animationSource.Bytes Is Nothing OrElse animationSource.Bytes.Length = 0 Then
                SetStatus("Selected HKX could not be read.", True)
                Return
            End If

            Logger.LogLazy(Function() $"[HKX-POSE-UI] Picker selected animation='{animationSource.DisplayPath}' skeleton='{If(_skeletonSource?.DisplayPath, "<none>")}'.")
            _session = HkxPoseImportSession.Create(If(_skeletonSource?.Bytes, Array.Empty(Of Byte)()),
                                                   animationSource.Bytes,
                                                   SkeletonInstance.Default,
                                                   animationSource.DisplayPath,
                                                   If(_skeletonSource?.DisplayPath, ""))

            _player = New HkxAnimationPlayer(_session)

            Dim maxFrame = Math.Max(0, _session.FrameCount - 1)
            _suppressFrameEvents = True
            FrameSlider.Minimum = 0
            FrameSlider.Maximum = maxFrame
            FrameSlider.Value = 0
            FrameSlider.Enabled = maxFrame > 0
            _suppressFrameEvents = False
            ButtonPlay.Enabled = maxFrame > 0
            ApplyHkxPlaybackInterval()
            If EditPreviewControl IsNot Nothing Then EditPreviewControl.WM_Set_Last_rendered(Nothing)

            TextBoxPoseName.Text = DefaultPoseName(animationSource.DisplayPath)
            DictionaryPicker_Control1.btnOk.Enabled = True
            UpdatePreview()
        Catch ex As Exception
            Logger.LogLazy(Function() "[HKX-POSE-UI] Picker load exception: " & ex.ToString())
            SetStatus("Error loading HKX: " & ex.Message, True)
        End Try
    End Sub

    Private _redcolor As Boolean = False
    Private Sub UpdatePreview()
        If _player Is Nothing OrElse EditPreviewControl Is Nothing Then Return

        Try
            Dim sw = Stopwatch.StartNew()
            Dim poseName = If(String.IsNullOrWhiteSpace(TextBoxPoseName.Text), "HKX Preview Pose", TextBoxPoseName.Text.Trim())
            Dim fram As Integer = CurrentFrame()
            _player.PoseName = poseName
            EditPreviewControl.Model.FloorOffset = -_selectedSliderSet.HighHeelHeight
            Dim pos As Poses_class = _player.PoseForFrame(fram)
            EditPreviewControl.Update_Render(_selectedSliderSet, False, _selectedPreset, pos, _selectedSize)
            sw.Stop()
            Dim budgetMs = Math.Max(1, CInt(Math.Round(1000.0 / Math.Max(1.0, CDbl(NumericFrameMs.Value)))))
            If PlaybackTimer.Enabled AndAlso sw.ElapsedMilliseconds > budgetMs Then
                If Not _redcolor Then NumericFrameMs.ForeColor = Color.Red : _redcolor = True
                Logger.LogLazy(Function() $"[HKX-POSE-UI] Playback frame over budget renderMs={sw.ElapsedMilliseconds} budgetMs={budgetMs}; real-time playback will skip frames.")
            Else
                If _redcolor Then NumericFrameMs.ForeColor = Color.FromKnownColor(KnownColor.ControlText) : _redcolor = False
            End If

        Catch ex As Exception
            StopPlayback()
            Logger.LogLazy(Function() "[HKX-POSE-UI] Preview exception: " & ex.ToString())
            SetStatus("Preview error: " & ex.Message, True)
        End Try
    End Sub

    Private Sub StartPlayback()
        If _player Is Nothing OrElse FrameSlider.Maximum <= 0 Then Return
        If EditPreviewControl IsNot Nothing Then
            EditPreviewControl.PlayingAnimation = True
        End If
        Dim fps = Math.Max(1.0, CDbl(NumericFrameMs.Value))
        _player.TargetFps = fps
        PlaybackTimer.Interval = FpsToCheckInterval(fps)
        _player.Start(CurrentFrame())
        PlaybackTimer.Start()
        ButtonPlay.Text = "Stop"
        Logger.LogLazy(Function() $"[HKX-POSE-UI] Playback started fps={fps} timerIntervalMs={PlaybackTimer.Interval} frames={_player.FrameCount}.")
    End Sub

    Private Sub StopPlayback()
        If PlaybackTimer.Enabled Then Logger.LogLazy(Function() "[HKX-POSE-UI] Playback stopped.")
        If EditPreviewControl IsNot Nothing Then
            EditPreviewControl.PlayingAnimation = False
        End If
        PlaybackTimer.Stop()
        _player?.Stop()
        If ButtonPlay IsNot Nothing Then ButtonPlay.Text = "Play"
    End Sub

    ''' <summary>Setea el FPS del numeric a partir del FPS nativo de la animación (1/frameDuration),
    ''' clampeado al rango del control. Roundtrip: el player trabaja en FPS.</summary>
    Private Sub ApplyHkxPlaybackInterval()
        Dim fps As Double = 30.0
        If _player IsNot Nothing AndAlso _player.NativeFps > 0.0 Then fps = _player.NativeFps
        fps = Math.Min(CDbl(NumericFrameMs.Maximum), Math.Max(CDbl(NumericFrameMs.Minimum), fps))
        _suppressPlaybackIntervalEvents = True
        NumericFrameMs.Value = CDec(Math.Round(fps, MidpointRounding.AwayFromZero))
        _suppressPlaybackIntervalEvents = False
        Dim appliedFps = Math.Max(1.0, CDbl(NumericFrameMs.Value))
        If _player IsNot Nothing Then _player.TargetFps = appliedFps
        PlaybackTimer.Interval = FpsToCheckInterval(appliedFps)
        Logger.LogLazy(Function() $"[HKX-POSE-UI] Playback FPS from HKX frameDuration={If(_session Is Nothing, 0.0F, _session.FrameDuration):0.######} fps={appliedFps}.")
    End Sub

    Private Function CurrentFrame() As Integer
        Return Math.Max(0, CInt(Math.Round(FrameSlider.Value, MidpointRounding.AwayFromZero)))
    End Function

    Private Shared Function DefaultPoseName(displayPath As String) As String
        Dim name = Path.GetFileNameWithoutExtension(displayPath)
        If String.IsNullOrWhiteSpace(name) Then name = "Imported HKX Pose"
        Return name
    End Function

    Private Sub SetStatus(text As String, isError As Boolean)
        If isError Then MsgBox(If(text, ""), vbOKOnly Or vbCritical, "HKX Pose Import")
        Logger.LogLazy(Function() $"[HKX-POSE-UI] Status error={isError}: {If(text, "")}")
    End Sub

    Private Function LoadHkxDictionaryEntryBytes(selectedKey As String) As HkxByteSource
        If String.IsNullOrWhiteSpace(selectedKey) Then Return Nothing
        Dim location As FilesDictionary_class.File_Location = Nothing
        If FilesDictionary_class.Dictionary.TryGetValue(selectedKey, location) = False OrElse location Is Nothing Then
            Logger.LogLazy(Function() $"[HKX-POSE-UI] Selected HKX key no longer exists in dictionary key='{selectedKey}'.")
            Return Nothing
        End If

        Logger.LogLazy(Function() $"[HKX-POSE-UI] Loading HKX dictionary entry path='{selectedKey}' source='{If(location.IsLosseFile, "loose", location.BA2File)}' index={location.Index}.")
        Dim bytes = location.GetBytes()
        Logger.LogLazy(Function() $"[HKX-POSE-UI] Loaded HKX dictionary entry path='{selectedKey}' bytes={If(bytes Is Nothing, 0, bytes.Length)}.")
        Return New HkxByteSource With {.Bytes = bytes, .DisplayPath = selectedKey}
    End Function

    Private Function ResolveConfiguredSkeletonHkxBytes() As HkxByteSource
        Dim skeletonNif = Wardrobe_Manager_Form.Directorios.SkeletonPath
        If String.IsNullOrWhiteSpace(skeletonNif) Then Return Nothing

        Logger.LogLazy(Function() $"[HKX-POSE-UI] Resolving configured skeleton HKX from skeletonNif='{skeletonNif}'.")
        Dim candidates As New List(Of String)
        candidates.Add(Path.ChangeExtension(skeletonNif, ".hkx"))
        Dim skeletonDir = Path.GetDirectoryName(skeletonNif)
        If String.IsNullOrWhiteSpace(skeletonDir) = False Then candidates.Add(Path.Combine(skeletonDir, "skeleton.hkx"))

        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each candidate In candidates
            If String.IsNullOrWhiteSpace(candidate) OrElse seen.Add(candidate) = False Then Continue For
            Logger.LogLazy(Function() $"[HKX-POSE-UI] Checking skeleton HKX candidate='{candidate}'.")

            If File.Exists(candidate) Then
                Dim bytes = File.ReadAllBytes(candidate)
                Logger.LogLazy(Function() $"[HKX-POSE-UI] Found loose skeleton HKX candidate='{candidate}' bytes={bytes.Length}.")
                Return New HkxByteSource With {.Bytes = bytes, .DisplayPath = candidate}
            End If

            Dim rel = TryMakeDataRelativePath(candidate)
            If rel = "" Then Continue For

            Dim location As FilesDictionary_class.File_Location = Nothing
            If FilesDictionary_class.Dictionary.TryGetValue(rel, location) Then
                Dim bytes = location.GetBytes()
                If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                    Logger.LogLazy(Function() $"[HKX-POSE-UI] Found dictionary skeleton HKX rel='{rel}' source='{If(location.IsLosseFile, "loose", location.BA2File)}' bytes={bytes.Length}.")
                    Return New HkxByteSource With {.Bytes = bytes, .DisplayPath = rel}
                End If
                Logger.LogLazy(Function() $"[HKX-POSE-UI] Dictionary skeleton HKX rel='{rel}' exists but returned no bytes.")
            End If
        Next

        Logger.LogLazy(Function() "[HKX-POSE-UI] No configured skeleton HKX found; importer will require embedded skeleton or full animation tracks.")
        Return Nothing
    End Function

    Private Function TryMakeDataRelativePath(path As String) As String
        Dim dataRoot = Config_App.Current.FO4EDataPath
        If String.IsNullOrWhiteSpace(path) OrElse String.IsNullOrWhiteSpace(dataRoot) Then Return ""
        Try
            Dim fullPath = IO.Path.GetFullPath(path)
            Dim fullRoot = IO.Path.GetFullPath(dataRoot)
            If fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) = False Then Return ""
            Return IO.Path.GetRelativePath(fullRoot, fullPath).Correct_Path_Separator
        Catch
            Return ""
        End Try
    End Function

    Private NotInheritable Class HkxByteSource
        Public Property Bytes As Byte()
        Public Property DisplayPath As String
    End Class
End Class
