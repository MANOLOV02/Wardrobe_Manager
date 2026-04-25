' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.Threading

Public Class Config_Form
    Private initialgame As Config_App.Game_Enum
    ' Captured at form Load. Pack/Unpack discover archives via FilesDictionary, which was filled
    ' at startup against this path. If the user retargets to a different game/path mid-session the
    ' dictionary is stale, so we lock the buttons until WM is restarted (or the user reverts).
    Private initialDataPath As String = ""
    Public Sub New()
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Enable double-buffering on the two labels that get hammered by progress updates during
        ' Pack/Unpack — without this the Text-per-tick assignments cause visible flicker. Done
        ' via reflection so the Designer can keep these as plain Label (a custom subclass would
        ' be wiped out the next time the Designer round-trips this form).
        EnableDoubleBuffer(PackProgressLabel)
        EnableDoubleBuffer(PackLastActionLabel)
    End Sub

    ''' <summary>
    ''' Turns on double-buffering for an arbitrary Control. Equivalent to deriving a subclass
    ''' that calls SetStyle in its ctor, but applied to an existing instance so we don't have
    ''' to introduce a Designer-incompatible custom control.
    ''' </summary>
    Private Shared Sub EnableDoubleBuffer(c As Control)
        If c Is Nothing Then Return
        Dim prop = GetType(Control).GetProperty(
            "DoubleBuffered",
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        prop?.SetValue(c, True, Nothing)
    End Sub
    Private Sub Setea_Render_Options()
        Try
            RecalculateNormalsCheck.Checked = Config_App.Current.Setting_RecalculateNormals
            SingleBoneCheck.Checked = Config_App.Current.Setting_SingleBoneSkinning
            CheckBoxGPUSkinning.Checked = Config_App.Current.Setting_GPUSkinning

            NormalsForceOrthogonal.Checked = Config_App.Current.Setting_TBN.ForceOrthogonalBitangent
            NormalsRepairNan.Checked = Config_App.Current.Setting_TBN.RepairNaNs
            CheckBoxWelding.Checked = Config_App.Current.Setting_TBN.EnableWelding
            NumericUpDownPositionEps.Value = Config_App.Current.Setting_TBN.EpsilonPos
            NumericUpDownUVEps.Value = Config_App.Current.Setting_TBN.EpsilonUV
            NormalsNormalize.Checked = Config_App.Current.Setting_TBN.NormalizeOutputs
            RadioButtonWeldpsonly.Checked = Config_App.Current.Setting_TBN.WeldByPositionOnly
            RadioButtonWeldboth.Checked = Not Config_App.Current.Setting_TBN.WeldByPositionOnly
            NumericUpDownWeldEpspos.Value = Config_App.Current.Setting_TBN.WeldPosEpsilon
            NumericUpDownWeldEpsUv.Value = Config_App.Current.Setting_TBN.WeldUVEpsilon
            RadioButtonByArea.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AreaOnly)
            RadioButtonByangles.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AngleOnly)
            RadioButtoncombined.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AreaTimesAngle)
            CheckBoxanglereset.Checked = Config_App.Current.Settings_Camara.ResetAngles
            CheckBoxzoomreset.Checked = Config_App.Current.Settings_Camara.ResetZoom
            RadioButtonBSEngine.Checked = (WM_Config.Current.Settings_Build.OwnEngine = False)
            RadioButtonWMEngine.Checked = (WM_Config.Current.Settings_Build.OwnEngine = True)
            CheckBoxBuildHH.Checked = WM_Config.Current.Settings_Build.SaveHHS
            CheckBoxBuildTri.Checked = WM_Config.Current.Settings_Build.SaveTri
            CheckBoxDeletewithProject.Checked = WM_Config.Current.Settings_Build.DeleteWithProject
            CheckBoxDeleteBefore.Checked = WM_Config.Current.Settings_Build.DeleteUnbuilt
            CheckBoxLMReseteachBuild.Checked = WM_Config.Current.Settings_Build.ResetSlidersEachBuild
            CheckBoxLMASkipManoloFixes.Checked = WM_Config.Current.Settings_Build.SkipFixMorphs
            CheckBoxLMAddAditionals.Checked = WM_Config.Current.Settings_Build.AddAddintionalSliders
            CheckBoxIgnorePrevent.Checked = WM_Config.Current.Settings_Build.IgnorePreventri
            CheckBoxBuildInPose.Checked = WM_Config.Current.Settings_Build.BuildInPose
            CheckBoxFreeze.Checked = Config_App.Current.Settings_Camara.FreezeCamera
            CheckBoxweightignore.Checked = WM_Config.Current.Settings_Build.IgnoreWeightsFlags
            RadioButtonAllwaysWeight.Checked = WM_Config.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Checked = Not WM_Config.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Enabled = WM_Config.Current.Settings_Build.IgnoreWeightsFlags AndAlso WM_Config.Current.Settings_Build.OwnEngine
            RadioButtonAllwaysWeight.Enabled = WM_Config.Current.Settings_Build.IgnoreWeightsFlags AndAlso WM_Config.Current.Settings_Build.OwnEngine
            CheckBoxweightignore.Enabled = WM_Config.Current.Settings_Build.OwnEngine = True

            ' GRID
            CheckBoxRenderGrid.Checked = Config_App.Current.Settings_RenderGrid.Enabled
            NumericUpDownRenderGridSize.Value = CDec(Config_App.Current.Settings_RenderGrid.Size)
            NumericUpDownRenderGridStep.Value = CDec(Config_App.Current.Settings_RenderGrid.StepSize)
            GridColor.SelectedColor = Config_App.Current.RenderGridColor

        Catch ex As Exception
            'MsgBox("Render options error", vbCritical, "Reset render options")
        End Try

    End Sub
    Private Sub Graba_Render_Options()
        Dim opts = New RecalcTBN.TBNOptions With {
          .ForceOrthogonalBitangent = NormalsForceOrthogonal.Checked,
          .EnableWelding = CheckBoxWelding.Checked,
          .EpsilonUV = NumericUpDownUVEps.Value,
          .EpsilonPos = NumericUpDownPositionEps.Value,
          .NormalizeOutputs = NormalsNormalize.Checked,
          .RepairNaNs = NormalsRepairNan.Checked,
          .WeightMode = IIf(RadioButtoncombined.Checked, RecalcTBN.NormalWeightMode.AreaTimesAngle, IIf(RadioButtonByangles.Checked, RecalcTBN.NormalWeightMode.AngleOnly, RecalcTBN.NormalWeightMode.AreaOnly)),
          .WeldByPositionOnly = RadioButtonWeldpsonly.Checked,
          .WeldPosEpsilon = NumericUpDownWeldEpspos.Value,
          .WeldUVEpsilon = NumericUpDownWeldEpsUv.Value
          }

        Dim buildSet = New WM_Config.BuildSettings With {
            .DeleteUnbuilt = CheckBoxDeleteBefore.Checked,
            .DeleteWithProject = CheckBoxDeletewithProject.Checked,
            .OwnEngine = Not RadioButtonBSEngine.Checked,
            .SaveHHS = CheckBoxBuildHH.Checked,
            .SaveTri = CheckBoxBuildTri.Checked,
            .ResetSlidersEachBuild = CheckBoxLMReseteachBuild.Checked,
            .SkipFixMorphs = CheckBoxLMASkipManoloFixes.Checked,
            .AddAddintionalSliders = CheckBoxLMAddAditionals.Checked,
            .IgnorePreventri = CheckBoxIgnorePrevent.Checked,
            .BuildInPose = CheckBoxBuildInPose.Checked,
            .IgnoreWeightsFlags = CheckBoxweightignore.Checked,
         .ForceWeights = RadioButtonAllwaysWeight.Checked
                    }

        Config_App.Current.Settings_RenderGrid = New Config_App.RenderGridSettings With {
    .Enabled = CheckBoxRenderGrid.Checked,
    .Size = CDbl(NumericUpDownRenderGridSize.Value),
    .StepSize = CDbl(NumericUpDownRenderGridStep.Value)
    }

        Dim cam = New Config_App.CameraSettings With {.ResetAngles = CheckBoxanglereset.Checked, .ResetZoom = CheckBoxzoomreset.Checked, .FreezeCamera = CheckBoxFreeze.Checked}
        Config_App.Current.Settings_Camara = cam
        Config_App.Current.Setting_RenderGridColor = GridColor.SelectedColor.Name
        WM_Config.Current.Settings_Build = buildSet
        Config_App.Current.Setting_TBN = opts
        Config_App.Current.Setting_RecalculateNormals = RecalculateNormalsCheck.Checked
        Config_App.Current.Setting_SingleBoneSkinning = SingleBoneCheck.Checked
        Config_App.Current.Setting_GPUSkinning = CheckBoxGPUSkinning.Checked
    End Sub

    ''' <summary>
    ''' Block form closure while a Pack/Unpack is in flight. If the user really wants to close,
    ''' offer to stop the operation first; on confirmation we trigger the same safe Cancel that
    ''' the Stop button uses and let the worker reach its next checkpoint. Closing again after
    ''' the operation finishes proceeds normally.
    ''' </summary>
    Private Sub Config_Form_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Dim cts = _packCts
        If cts Is Nothing OrElse cts.IsCancellationRequested Then Return

        e.Cancel = True
        Dim ans = MessageBox.Show(
            "A Pack/Unpack operation is still running. Stop it safely and close after the current archive finishes?",
            "Operation in progress",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If ans = DialogResult.Yes Then
            StopButton.Enabled = False
            StopButton.Text = "Stopping…"
            PackProgressLabel.Text = "Stop requested — finishing current archive safely…"
            cts.Cancel()
        End If
    End Sub

    Private Sub Config_Form_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        TextBox1.Text = Config_App.Current.FO4ExePath
        TextBox2.Text = WM_Config.Current.BSExePath
        TextBox3.Text = WM_Config.Current.OSExePath
        TextBox4.Text = Config_App.Current.SkeletonPath
        ComboBoxGame.SelectedIndex = Config_App.Current.Game
        initialgame = Config_App.Current.Game
        initialDataPath = If(Config_App.Current.FO4EDataPath, "")
        Setea_Render_Options()
        Button8.Enabled = IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        GridColor.Rellena()
        GridColor.SelectedColor = Config_App.Current.RenderGridColor
        Check_Folders()
        Check_GameMismatch()
        InitClonedMaterialTab()
    End Sub

    ' ====== Cloned Material tab logic ======
    ' All UI controls for this tab live in Config_Form.Designer.vb (TabPagePack and children).
    ' Code-behind only owns the run-state fields (CTS, elapsed timer, started-at).
    Private _packStartedAt As DateTime
    Private _packElapsedTimer As System.Windows.Forms.Timer
    Private _packCts As CancellationTokenSource

    Private Sub InitClonedMaterialTab()
        _packElapsedTimer = New System.Windows.Forms.Timer() With {.Interval = 1000}
        AddHandler _packElapsedTimer.Tick, AddressOf PackElapsedTimer_Tick
        RefreshClonedMaterialStatus()
    End Sub

    Private Sub PackElapsedTimer_Tick(sender As Object, e As EventArgs)
        If Not PackElapsedLabel.Visible Then Return
        Dim elapsed = DateTime.UtcNow - _packStartedAt
        PackElapsedLabel.Text = $"Elapsed: {Math.Floor(elapsed.TotalMinutes):00}:{elapsed.Seconds:00}"
    End Sub

    Private Sub StopButton_Click(sender As Object, e As EventArgs) Handles StopButton.Click
        Dim cts = _packCts
        If cts Is Nothing OrElse cts.IsCancellationRequested Then Return
        StopButton.Enabled = False
        StopButton.Text = "Stopping…"
        PackProgressLabel.Text = "Stop requested — finishing current archive safely…"
        cts.Cancel()
    End Sub

    Private Sub RefreshClonedMaterialStatus()
        ' Defensive: this gets called from ComboBoxGame.SelectedIndexChanged, which fires DURING
        ' InitializeComponent() the moment the Designer assigns ComboBoxGame.SelectedIndex — before
        ' the rest of the Pack tab controls have been instantiated. Check the LAST-created Pack
        ' control (PackLastActionLabel, declared at the end of the InitializeComponent block) so
        ' the early reentrant call bails out cleanly. Once Form_Load runs the explicit
        ' RefreshClonedMaterialStatus invocation populates everything correctly.
        If PackLastActionLabel Is Nothing Then Return

        Try
            Dim s = WM_PackUnpack.GetStatus()
            Dim mb As Func(Of Long, String) = Function(b) (b / 1024.0 / 1024.0).ToString("N1") & " MB"

            Dim looseTotal = s.LooseMaterialCount + s.LooseTextureCount
            PackStatusLooseValue.Text =
                $"{looseTotal:N0}  ({s.LooseMaterialCount:N0} materials, {s.LooseTextureCount:N0} textures)"
            PackStatusLooseSizeValue.Text = mb(s.LooseTotalBytes)
            Dim packedTotal = s.PackedMaterialCount + s.PackedTextureCount
            PackStatusArchivesValue.Text =
                $"{s.Plugins.Count:N0} plugins ({s.PackedMaterialCount:N0} materials, {s.PackedTextureCount:N0} textures)"
            PackStatusArchiveSizeValue.Text = mb(s.ArchiveTotalBytes)

            Dim contextValid = IsClonedMaterialContextValid()
            Dim hasLoose = looseTotal > 0
            Dim hasArchives = s.Archives.Count > 0
            PackButton.Enabled = contextValid AndAlso hasLoose
            UnpackButton.Enabled = contextValid AndAlso hasArchives

            If Not contextValid Then
                PackLastActionLabel.ForeColor = Drawing.Color.DarkRed
                PackLastActionLabel.Text =
                    "Game / data path changed since startup. Pack/Unpack disabled until you " &
                    "revert to the original game and path, or close and reopen Wardrobe Manager " &
                    "(the file dictionary needs to be rebuilt against the new target)."
            ElseIf PackLastActionLabel.ForeColor.Equals(Drawing.Color.DarkRed) Then
                ' Clear the warning if context returned to valid mid-session.
                PackLastActionLabel.ForeColor = SystemColors.ControlText
                PackLastActionLabel.Text = ""
            End If
        Catch ex As Exception
            ' Surface the throwing call site so first-chance exceptions in the debugger point at
            ' the actual root cause, not at this catch line.
            Dim site = ex.TargetSite
            Dim where = If(site Is Nothing, "(unknown)", $"{site.DeclaringType?.Name}.{site.Name}")
            PackStatusLooseValue.Text = "—"
            PackStatusLooseSizeValue.Text = "—"
            PackStatusArchivesValue.Text = "—"
            PackStatusArchiveSizeValue.Text = "—"
            PackLastActionLabel.ForeColor = Drawing.Color.DarkRed
            PackLastActionLabel.Text = $"Error reading status [{where}]: {ex.Message}"
            PackButton.Enabled = False
            UnpackButton.Enabled = False
        End Try
    End Sub

    Private Function IsClonedMaterialContextValid() As Boolean
        If Config_App.Current.Game <> initialgame Then Return False
        Dim currentDataPath = If(Config_App.Current.FO4EDataPath, "")
        Return String.Equals(currentDataPath, initialDataPath, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Async Sub PackButton_Click(sender As Object, e As EventArgs) Handles PackButton.Click
        SetPackButtonsBusy(True)
        _packCts = New CancellationTokenSource()
        PackLastActionLabel.ForeColor = SystemColors.ControlText
        PackLastActionLabel.Text = "Starting pack…"
        Try
            Dim progress As New Progress(Of WM_PackUnpack.PackProgress)(AddressOf OnPackProgress)
            Dim result = Await WM_PackUnpack.PackAsync(progress, _packCts.Token)
            If _packCts.IsCancellationRequested Then
                PackLastActionLabel.ForeColor = Drawing.Color.DarkOrange
                PackLastActionLabel.Text = $"Pack stopped by user. Wrote {result.Archives.Count} archive(s), " &
                                            $"{result.Plugins.Count} new plugin(s) before stop. Remaining loose files left untouched."
            Else
                PackLastActionLabel.ForeColor = SystemColors.ControlText
                PackLastActionLabel.Text = $"Pack complete. Wrote {result.Archives.Count} archive(s), " &
                                            $"{result.Plugins.Count} new plugin(s); skipped {result.Skipped.Count} unchanged."
            End If
        Catch ex As Exception
            PackLastActionLabel.ForeColor = Drawing.Color.DarkRed
            PackLastActionLabel.Text = "Pack failed: " & ex.Message
            MessageBox.Show(ex.ToString(), "Pack failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _packCts?.Dispose()
            _packCts = Nothing
            SetPackButtonsBusy(False)
            RefreshClonedMaterialStatus()
        End Try
    End Sub

    Private Async Sub UnpackButton_Click(sender As Object, e As EventArgs) Handles UnpackButton.Click
        ' Confirm: Unpack permanently deletes all WM_ClonePack archives + plugins.
        Dim ok = MessageBox.Show(
            "Extract all WM_ClonePack archives back to loose files and remove the archives + plugins?",
            "Unpack", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If ok <> DialogResult.Yes Then Return

        SetPackButtonsBusy(True)
        _packCts = New CancellationTokenSource()
        PackLastActionLabel.ForeColor = SystemColors.ControlText
        PackLastActionLabel.Text = "Starting unpack…"
        Try
            Dim progress As New Progress(Of WM_PackUnpack.PackProgress)(AddressOf OnPackProgress)
            Dim result = Await WM_PackUnpack.UnpackAsync(progress, _packCts.Token)
            If _packCts.IsCancellationRequested Then
                PackLastActionLabel.ForeColor = Drawing.Color.DarkOrange
                PackLastActionLabel.Text = "Unpack stopped by user."
            Else
                PackLastActionLabel.ForeColor = SystemColors.ControlText
                PackLastActionLabel.Text = $"Unpack complete. Removed {result.ArchivesRemoved.Count} archive(s) and " &
                                            $"{result.PluginsRemoved.Count} plugin(s); wrote {result.LooseFilesWritten.Count} loose file(s)."
            End If
        Catch ex As Exception
            PackLastActionLabel.ForeColor = Drawing.Color.DarkRed
            PackLastActionLabel.Text = "Unpack failed: " & ex.Message
            MessageBox.Show(ex.ToString(), "Unpack failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _packCts?.Dispose()
            _packCts = Nothing
            SetPackButtonsBusy(False)
            RefreshClonedMaterialStatus()
        End Try
    End Sub

    ' Progress(Of T) marshals callbacks to the UI thread automatically — safe to touch controls.
    ' Three different controls get updated at different rates:
    '   - PackProgressLabel: per-tick, every Stage update. Double-buffered so it doesn't flicker.
    '   - PackProgressBar: only when Max >= 0 (Max < 0 = "leave the bar alone, this is a milestone
    '     report that doesn't change progress numbers").
    '   - PackLastActionLabel (bottom box): only when BoxText is non-empty. Low-frequency
    '     milestones (per archive started/finished); never on per-entry ticks.
    Private Sub OnPackProgress(p As WM_PackUnpack.PackProgress)
        If p Is Nothing Then Return
        PackProgressLabel.Text = p.Stage

        If p.Max >= 0 Then
            If p.Max > 0 Then
                PackProgressBar.Style = ProgressBarStyle.Continuous
                PackProgressBar.Maximum = p.Max
                ' Setting Value twice with the second one being the desired value is a known WinForms
                ' workaround: the animation lags badly without it on the Continuous style.
                Dim clamped = Math.Max(0, Math.Min(p.Current, p.Max))
                PackProgressBar.Value = clamped
            Else
                PackProgressBar.Style = ProgressBarStyle.Marquee
            End If
        End If

        If Not String.IsNullOrEmpty(p.BoxText) Then
            PackLastActionLabel.ForeColor = SystemColors.ControlText
            PackLastActionLabel.Text = p.BoxText
        End If
    End Sub

    Private Sub SetPackButtonsBusy(busy As Boolean)
        PackButton.Enabled = Not busy
        UnpackButton.Enabled = Not busy
        StopButton.Visible = busy
        StopButton.Enabled = busy
        StopButton.Text = "Stop"
        PackProgressBar.Visible = busy
        PackProgressLabel.Visible = busy
        PackElapsedLabel.Visible = busy

        If busy Then
            PackProgressBar.Style = ProgressBarStyle.Marquee
            PackProgressBar.Value = 0
            PackProgressLabel.Text = "Starting…"
            PackElapsedLabel.Text = "Elapsed: 00:00"
            _packStartedAt = DateTime.UtcNow
            _packElapsedTimer.Start()
        Else
            _packElapsedTimer.Stop()
        End If

        ' Lock other tabs while busy. We don't disable the TabPage controls (.Enabled = False on
        ' a TabPage greys everything but lets the user still click around inside it on some
        ' themes); instead we keep the user pinned to the current tab via SelectedIndexChanging,
        ' and we disable every sibling TabPage.Enabled so its controls visibly grey out.
        For Each tp As TabPage In TabControl1.TabPages
            If tp Is TabPagePack Then Continue For
            tp.Enabled = Not busy
        Next
        If busy Then
            ' Force selection back to the pack tab so the user sees the progress.
            TabControl1.SelectedTab = TabPagePack
            AddHandler TabControl1.Selecting, AddressOf TabControl1_LockSelection
        Else
            RemoveHandler TabControl1.Selecting, AddressOf TabControl1_LockSelection
        End If

        Cursor = If(busy, Cursors.WaitCursor, Cursors.Default)
    End Sub

    Private Sub TabControl1_LockSelection(sender As Object, e As TabControlCancelEventArgs)
        ' Reject any attempt to leave the pack tab while a Pack/Unpack is running.
        If e.TabPage IsNot TabPagePack Then e.Cancel = True
    End Sub

    Private Function Check_Folders() As Boolean
        If Config_App.Check_FOFolder = False Then Label1.ImageIndex = 1 Else Label1.ImageIndex = 0
        If WM_Config.Check_BSFolder = False Then Label2.ImageIndex = 1 Else Label2.ImageIndex = 0
        If WM_Config.Check_OsFolder = False Then Label3.ImageIndex = 1 Else Label3.ImageIndex = 0
        If Config_App.Check_Skeleton = False Then Label6.ImageIndex = 1 Else Label6.ImageIndex = 0

        Dim folderschek As Boolean = Config_App.Check_FOFolder And WM_Config.Check_BSFolder And WM_Config.Check_OsFolder
        If folderschek Then
            ListView1.Items.Clear()
            Dim oldbsa = WM_Config.Current.BSAFiles.ToList
            Dim oldchecks = WM_Config.Current.BSAFiles_Clonables.ToList
            WM_Config.Current.BSAFiles.Clear()
            WM_Config.Current.BSAFiles_Clonables.Clear()
            Dim idx2 As Integer = 0
            For Each fil In FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Config_App.Current.FO4EDataPath, "*.ba2;*.bsa", False).Order
                Dim it As New ListViewItem(IO.Path.GetFileName(fil))
                Dim idx = oldbsa.FindIndex(Function(s) String.Equals(s, IO.Path.GetFileName(fil), StringComparison.OrdinalIgnoreCase))
                WM_Config.Current.BSAFiles.Add(IO.Path.GetFileName(fil))
                If idx <> -1 Then
                    WM_Config.Current.BSAFiles_Clonables.Add(oldchecks(idx))
                Else
                    WM_Config.Current.BSAFiles_Clonables.Add(False)
                End If
                ListView1.Items.Add(it)
                it.Tag = idx2
                it.Checked = WM_Config.Current.BSAFiles_Clonables(idx2)
                idx2 += 1
            Next

        End If
        Return folderschek
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox1.Text))
        If String.IsNullOrEmpty(result) Then Return
        Config_App.Current.FO4ExePath = result
        TextBox1.Text = Config_App.Current.FO4ExePath
        Check_GameMismatch()
        Dim exe = IO.Path.GetFileName(Config_App.Current.FO4ExePath)
        If exe.ToLower.Contains("fallout4", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Fallout4
        If exe.ToLower.Contains("skyrimse", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Skyrim Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Skyrim

        Dim pathS As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "Tools", "CalienteTools")

        If Config_App.Check_FOFolder And (TextBox2.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or WM_Config.Check_BSFolder = False) Then
            If Environment.Is64BitOperatingSystem Then
                TextBox2.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\BodySlide x64.exe")
            Else
                TextBox2.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\BodySlide.exe")
            End If
            WM_Config.Current.BSExePath = TextBox2.Text
        End If
        If Config_App.Check_FOFolder And (TextBox3.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or WM_Config.Check_OsFolder = False) Then
            If Environment.Is64BitOperatingSystem Then
                TextBox3.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\OutfitStudio x64.exe")
            Else
                TextBox3.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\OutfitStudio.exe")
            End If
            WM_Config.Current.OSExePath = TextBox3.Text
        End If
        If Config_App.Check_FOFolder And (TextBox4.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or Config_App.Check_Skeleton = False) Then
            Dim skel As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "res\skeleton_fo4.nif", "res\skeleton_female_sse.nif")
            TextBox4.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\" + skel)
            Config_App.Current.SkeletonPath = TextBox4.Text
            SkeletonInstance.Default.Skeleton = Nothing
        End If
        Check_Folders()
        RefreshClonedMaterialStatus()
    End Sub

    Private Shared Function Search_exe(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an executable file"
            dlg.Filter = "EXE files (*.exe)|*.exe"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Shared Function Search_Nif(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an skeleton nif"
            dlg.Filter = "NIF files (*.nif)|*.nif"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox2.Text))
        If String.IsNullOrEmpty(result) Then Return
        WM_Config.Current.BSExePath = result
        TextBox2.Text = WM_Config.Current.BSExePath
        Check_Folders()
    End Sub
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox3.Text))
        If String.IsNullOrEmpty(result) Then Return
        WM_Config.Current.OSExePath = result
        TextBox3.Text = WM_Config.Current.OSExePath
        Check_Folders()
    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView1.SelectedIndexChanged

    End Sub

    Private Sub ListView1_ItemCheck(sender As Object, e As ItemCheckEventArgs) Handles ListView1.ItemCheck
        If CInt(ListView1.Items(e.Index).Tag) < -1 Or CInt(ListView1.Items(e.Index).Tag) > WM_Config.Current.BSAFiles_Clonables.Count - 1 Then
            Debugger.Break()
        End If
        WM_Config.Current.BSAFiles_Clonables(ListView1.Items(e.Index).Tag) = IIf(e.NewValue = CheckState.Checked, True, False)
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Dim result = Search_Nif(IO.Path.GetDirectoryName(TextBox4.Text))
        If String.IsNullOrEmpty(result) Then Return
        Config_App.Current.SkeletonPath = result
        TextBox4.Text = Config_App.Current.SkeletonPath
        SkeletonInstance.Default.Skeleton = Nothing
        Check_Folders()
    End Sub


    Private Sub Button5_Click(sender As Object, e As EventArgs)
        SingleBoneCheck.Checked = False
        RecalculateNormalsCheck.Checked = True
    End Sub
    Private actualizar = False
    Private Sub Config_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        actualizar = True
    End Sub

    Private Sub Config_Form_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If initialgame <> Config_App.Current.Game Then
            If Not IsNothing(Me.Owner) AndAlso Me.Owner.GetType Is GetType(Wardrobe_Manager_Form) Then
                If Not IsNothing(CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict) Then
                    CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict.Checked = True
                End If
            End If
        End If
        Graba_Render_Options()
    End Sub

    Private Sub Button5_Click_1(sender As Object, e As EventArgs) Handles Button5.Click
        Config_App.Current.Setting_SingleBoneSkinning = False
        Config_App.Current.Setting_GPUSkinning = True
        Config_App.Current.Setting_RecalculateNormals = True
        Config_App.Current.Setting_TBN = RecalcTBN.DefaultTBNOptions
        Config_App.Current.Settings_Camara = Config_App.Default_CameraSettings
        Config_App.Current.Settings_RenderGrid = Config_App.Default_RenderGrid_Settings
        Config_App.Current.Setting_RenderGridColor = Color.FromKnownColor(KnownColor.LightGray).Name
        Setea_Render_Options()
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click

        Graba_Render_Options()
        If Not IsNothing(Me.Owner) AndAlso Me.Owner.GetType Is GetType(Wardrobe_Manager_Form) Then
            If Not IsNothing(CType(Owner, Wardrobe_Manager_Form).preview_Control) Then
                Dim ctrl = CType(Owner, Wardrobe_Manager_Form).preview_Control
                ctrl.Model.RecalculateNormals = RecalculateNormalsCheck.Checked
                ctrl.Model.SingleBoneSkinning = SingleBoneCheck.Checked
                ctrl.Model.Floor.Enabled = CheckBoxRenderGrid.Checked
                ctrl.Model.Floor.Size = NumericUpDownRenderGridSize.Value
                ctrl.Model.Floor.StepSize = NumericUpDownRenderGridStep.Value
                ctrl.Model.Floor.Color = GridColor.SelectedColor
                ctrl.Model.Floor.Rebuild()
                ctrl.ForceRerender(RenderDirtyFlags.Shapes Or RenderDirtyFlags.Camera)

            End If
        End If
    End Sub
    Private Sub CheckBoxRenderGrid_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxRenderGrid.CheckedChanged
        Update_RenderGrid_Controls()
    End Sub
    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        WM_Config.Current.Settings_Build = WM_Config.Default_Build_Settings
        Setea_Render_Options()
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        Try
            If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
            Button8.Enabled = False
        Catch ex As Exception
            Debugger.Break()
        End Try

    End Sub

    Private Sub CheckBoxBuildTri_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxBuildTri.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonWMEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonWMEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonBSEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonBSEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub TabPage3_Click(sender As Object, e As EventArgs) Handles TabPage3.Click

    End Sub

    Private Sub CheckBoxzoomreset_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxzoomreset.CheckedChanged

    End Sub

    Private Sub CheckBoxFreeze_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxFreeze.CheckedChanged
        CheckBoxzoomreset.Enabled = Not CheckBoxFreeze.Checked
        CheckBoxanglereset.Enabled = Not CheckBoxFreeze.Checked
    End Sub

    Private Sub ComboBoxGame_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxGame.SelectedIndexChanged
        If ComboBoxGame.SelectedIndex <> -1 Then
            Config_App.Current.Game = ComboBoxGame.SelectedIndex
            GroupBoxweights.Enabled = ComboBoxGame.SelectedIndex <> 0
            GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
            Check_GameMismatch()
            RefreshClonedMaterialStatus()
        End If
    End Sub

    Private Sub Check_GameMismatch()
        Dim exe = Config_App.Current.FO4ExePath.ToLowerInvariant()
        Dim isFO4Exe = exe.Contains("fallout4", StringComparison.OrdinalIgnoreCase)
        Dim isSkyrimExe = exe.Contains("skyrim", StringComparison.OrdinalIgnoreCase) Or exe.Contains("sse", StringComparison.OrdinalIgnoreCase)
        Dim game = Config_App.Current.Game
        Dim mismatch = (game = Config_App.Game_Enum.Fallout4 AndAlso isSkyrimExe) OrElse
                       (game = Config_App.Game_Enum.Skyrim AndAlso isFO4Exe)
        LabelGameMismatch.Visible = mismatch
    End Sub

    Private Sub CheckBoxweightignore_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxweightignore.CheckedChanged
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked

    End Sub
    Private Sub Update_RenderGrid_Controls()
        Dim enabled As Boolean = CheckBoxRenderGrid.Checked
        NumericUpDownRenderGridSize.Enabled = enabled
        NumericUpDownRenderGridStep.Enabled = enabled
        GridColor.Enabled = enabled
    End Sub

End Class