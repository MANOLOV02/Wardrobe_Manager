' Version Uploaded of Wardrobe 3.2.0
Imports System.IO
Imports System.Net
Imports System.Xml
Imports K4os.Hash.xxHash
Imports NiflySharp
Imports NiflySharp.Blocks

Public Class Create_from_Nif_Form
    Private WithEvents EditPreviewControl As PreviewControl = Nothing
    Private Selected_OSP As New OSP_Project_Class
    Private selected_slider As New SliderSet_Class(Selected_OSP)
    Private HasSaved As Boolean = False

    Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub
    Public Sub New(keys As List(Of String), rootPrefix As String, allowedExts As HashSet(Of String), initialkey As String)
        InitializeComponent()
        ArgumentNullException.ThrowIfNull(keys)
        ArgumentNullException.ThrowIfNull(allowedExts)
        Me.DictionaryPicker_Control1.Initialize(keys, rootPrefix, allowedExts)
        Me.DictionaryPicker_Control1.Preselect(initialkey)
        Me.DictionaryPicker_Control1.AllowClone = False
        Me.DictionaryPicker_Control1.btnOk.Text = "Create"
        Me.DictionaryPicker_Control1.btnCancel.Text = "Exit"
    End Sub


    Private Sub Create_from_Nif_2_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        EditPreviewControl = New PreviewControl With {.Dock = DockStyle.Fill}
        Panel1.Controls.Add(EditPreviewControl)
        EditPreviewControl.Model.SingleBoneSkinning = False
        EditPreviewControl.Model.RecalculateNormals = False
        EditPreviewControl.AllowMask = False
    End Sub

    Private Sub DictionaryPicker_Control1_OkClicked() Handles DictionaryPicker_Control1.OkClicked
        Try
            If IsNothing(selected_slider) Then Exit Sub
            If selected_slider.Unreadable_NIF Then Throw New Exception("Unreadable NIF")
            If selected_slider.Unreadable_Project Then Throw New Exception("Unreadable Project")
            Dim OSPFIle = Path.Combine(Wardrobe_Manager_Form.Directorios.SliderSetsRoot, TextBox1.Text) + ".osp"
            If IO.File.Exists(OSPFIle) Then Throw New Exception("OSP File already exist")
            Dim New_Nif = Path.Combine(Wardrobe_Manager_Form.Directorios.ShapedataRoot, TextBox1.Text + "\" + TextBox1.Text + ".nif")
            Dim New_osd = Path.Combine(Wardrobe_Manager_Form.Directorios.ShapedataRoot, TextBox1.Text + "\" + TextBox1.Text + ".osd")

            If Directory.Exists(Path.GetDirectoryName(New_Nif)) = False Then
                Directory.CreateDirectory(Path.GetDirectoryName(New_Nif))
            End If

            For Each sli In selected_slider.Sliders
                For Each da In sli.Datas
                    da.TargetOsd = IO.Path.GetFileName(New_osd)
                Next
            Next
            selected_slider.NIFContent.Save_As_Manolo(New_Nif, False)
            selected_slider.OSDContent_Local.Save_As(New_osd, False)
            selected_slider.Nombre = TextBox1.Text
            selected_slider.DataFolderValue = TextBox1.Text
            selected_slider.SourceFileValue = TextBox1.Text + ".nif"
            Selected_OSP.Save_Pack_As(OSPFIle, False)
            selected_slider.BypassDiskShapeDataLoad = False
            selected_slider.ShapeDataLoaded = False
            selected_slider.LastShapeDataSignature = ""
            selected_slider.Unreadable_NIF = False
            selected_slider.Unreadable_Project = False
            Me.HasSaved = True
            MsgBox("Project created", vbInformation, "Success")

        Catch ex As Exception
            MsgBox("Error creating project:" + ex.ToString, vbCritical, "Error")
        End Try
    End Sub

    Private Sub DictionaryPicker_Control1_CancelClicked() Handles DictionaryPicker_Control1.CancelClicked
        Me.Close()

    End Sub

    Private Sub ChkDirSkeleton_CheckedChanged(sender As Object, e As EventArgs) Handles chkDirSkeleton.CheckedChanged
        If Last_key <> "" Then Read_selected(Last_key)
    End Sub

    Private Sub Create_from_Nif_2_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ' Restore global skeleton
        SkeletonInstance.Default.LoadFromConfig(True, True)
        EditPreviewControl.Clean()
        EditPreviewControl.Dispose()
        If Me.HasSaved = True Then
            Me.DialogResult = DialogResult.Yes
        Else
            Me.DialogResult = DialogResult.No
        End If
    End Sub




    Private _dirSkeletonKey As String = Nothing
    Private _loadedSkeletonKey As String = Nothing

    Private Sub Read_selected(key As String)
        Dim fil As String = key
        ' TRI lookup: first try the exact .nif → .tri replacement (cubre NIFs sin sufijo,
        ' y casos FO4 donde body_0.nif puede tener body_0.tri propio).  Si no existe y el
        ' NIF termina en _0.nif / _1.nif, fallback a la convención Outfit Studio: un solo
        ' body.tri compartido entre ambos tamaños.
        Dim tri = fil.Replace(".nif", ".tri", StringComparison.OrdinalIgnoreCase)
        If Not FilesDictionary_class.Dictionary.ContainsKey(tri) Then
            Dim stripped As String = Nothing
            If fil.EndsWith("_0.nif", StringComparison.OrdinalIgnoreCase) Then
                stripped = fil.Substring(0, fil.Length - "_0.nif".Length) & ".tri"
            ElseIf fil.EndsWith("_1.nif", StringComparison.OrdinalIgnoreCase) Then
                stripped = fil.Substring(0, fil.Length - "_1.nif".Length) & ".tri"
            End If
            If stripped IsNot Nothing AndAlso FilesDictionary_class.Dictionary.ContainsKey(stripped) Then
                tri = stripped
            End If
        End If
        selected_slider.ParentOSP.xml.DocumentElement.InnerText = ""
        selected_slider = New SliderSet_Class(selected_slider.ParentOSP) With {
            .BypassDiskShapeDataLoad = True
        }
        CheckBox1.Enabled = FilesDictionary_class.Dictionary.ContainsKey(tri)

        ' Check for skeleton.nif in the same directory
        Dim dirPath = IO.Path.GetDirectoryName(fil)
        Dim skelKey = If(String.IsNullOrEmpty(dirPath), "skeleton.nif", dirPath & "\skeleton.nif")
        _dirSkeletonKey = If(FilesDictionary_class.Dictionary.ContainsKey(skelKey), skelKey, Nothing)
        chkDirSkeleton.Enabled = _dirSkeletonKey IsNot Nothing

        Try
            Dim TriFileParese As FO4_Base_Library.TriFile = Nothing

            Dim value As FilesDictionary_class.File_Location = Nothing

            If FilesDictionary_class.Dictionary.TryGetValue(tri, value) AndAlso CheckBox1.Checked = True Then
                Try
                    TriFileParese = FO4_Base_Library.TriFileParser.ParseTriFromBytes(value.GetBytes)
                Catch ex As Exception
                End Try

            End If

            Dim filLoc As FilesDictionary_class.File_Location = Nothing
            If Not FilesDictionary_class.Dictionary.TryGetValue(fil, filLoc) Then Return
            selected_slider.NIFContent.Load_Manolo(filLoc.GetBytes)

            Dim OptResult As NifFileOptimizeResult = Nothing
            Dim ver = selected_slider.NIFContent.Header.Version

            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then

                ' Solo soportado: Skyrim LE -> Skyrim SE
                If ver.IsSK Then
                    If MsgBox("Current nif is Skyrim LE. Try to optimize it to Skyrim SE?", MsgBoxStyle.Information Or MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes Then
                        OptResult = selected_slider.NIFContent.Optimize(Config_App.Game_Enum.Skyrim)
                        If Not IsNothing(OptResult) AndAlso OptResult.VersionMismatch Then
                            MsgBox("Optimization failed, not supported for this file and game.", MsgBoxStyle.Critical, "Error")
                        End If
                    End If
                ElseIf Not ver.IsSSE Then
                    MsgBox("Current nif does not match Skyrim SE, and automatic optimization is only supported from Skyrim LE to Skyrim SE.", MsgBoxStyle.Critical, "Warning")
                End If

            ElseIf Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                If Not ver.IsFO4 Then
                    MsgBox("Current nif does not match Fallout 4, and automatic optimization to Fallout 4 is not supported.", MsgBoxStyle.Critical, "Warning")
                End If

            End If

            For Each shap In selected_slider.NIFContent.GetShapes

                If Nifcontent_Class_Manolo.SupportedShape(shap.GetType) Then
                    Dim shapec As New Shape_class(shap.Name.String, selected_slider)
                    selected_slider.Shapes.Add(shapec)
                End If
            Next

            selected_slider.OSDContent_Local = New OSD_Class(selected_slider)

            If Not IsNothing(TriFileParese) Then
                Try
                    For Each shapeMorph In TriFileParese.ShapeMorphs
                        For Each morp In shapeMorph.Value
                            If Not selected_slider.Sliders.Any(Function(pf) pf.Nombre.Equals(morp.Name, StringComparison.OrdinalIgnoreCase)) Then
                                selected_slider.Sliders.Add(New Slider_class(morp.Name, selected_slider, morp.MorphType))
                            End If
                            Dim slider = selected_slider.Sliders.First(Function(pf) pf.Nombre.Equals(morp.Name, StringComparison.OrdinalIgnoreCase))
                            Dim dat As Slider_Data_class
                            Dim datnombre = shapeMorph.Key.Replace(":", "_") + slider.Nombre
                            If Not slider.Datas.Any(Function(pf) pf.Nombre.Equals(datnombre, StringComparison.OrdinalIgnoreCase)) Then
                                slider.Datas.Add(New Slider_Data_class(datnombre, slider, shapeMorph.Key, "Tochange.osd"))
                            End If
                            dat = slider.Datas.First(Function(pf) pf.Nombre.Equals(datnombre, StringComparison.OrdinalIgnoreCase))

                            Dim block = New OSD_Block_Class(selected_slider.OSDContent_Local) With {.BlockName = dat.Nombre, .ParentOSDContent = selected_slider.OSDContent_Local, .DataDiff = New List(Of OSD_DataDiff_Class)}
                            selected_slider.OSDContent_Local.Blocks.Add(block)
                            For Each dif In morp.Offsets
                                Dim newdd = New OSD_DataDiff_Class With {.Index = dif.Key, .X = dif.Value.X, .Y = dif.Value.Y, .Z = dif.Value.Z}
                                block.DataDiff.Add(newdd)
                            Next
                        Next
                    Next
                Catch ex As Exception
                    Debugger.Break()
                End Try
            End If

            selected_slider.Unreadable_NIF = False
            selected_slider.ShapeDataLoaded = True
            selected_slider.InvalidateShapeDataLookupCache()
            selected_slider.RebuildShapeDataLookupCache()

            ' ── Shape type validator hook — DISABLED AFTER INITIAL VALIDATION PASS ──
            ' ShapeTypeValidator runs the A/B/C/D harness (round-trip, split, merge, zap)
            ' on shape types not yet marked "Validated" in shape_validator_cache.json.
            ' Needed only when refactoring the geometry adapter path or when adding support
            ' for a new shape type.  Re-enable by uncommenting this block; the validator's
            ' code lives in ShapeTypeValidator.vb and remains intact.
            '
            'Try
            '    ShapeTypeValidator.ValidateUntestedTypes(selected_slider, filLoc.FullPath)
            'Catch exValidator As Exception
            '    MsgBox("Shape Validator error (no bloquea el load): " & exValidator.Message,
            '           vbInformation, "Shape Validator")
            'End Try

        Catch ex As Exception
            Debugger.Break()
            selected_slider.Unreadable_NIF = True
        End Try


        TextBox1.Text = IO.Path.GetFileNameWithoutExtension(fil)
        selected_slider.OutputPathValue = IO.Path.GetDirectoryName(fil)
        selected_slider.OutputFileValue = IO.Path.GetFileNameWithoutExtension(fil)
        selected_slider.SourceFileValue = fil
        ' Load skeleton only if it changed (avoid reloading for NIFs in the same directory)
        Dim targetSkelKey = If(chkDirSkeleton.Checked AndAlso _dirSkeletonKey IsNot Nothing, _dirSkeletonKey, "")
        If Not String.Equals(_loadedSkeletonKey, targetSkelKey, StringComparison.OrdinalIgnoreCase) Then
            If targetSkelKey <> "" Then
                SkeletonInstance.Default.LoadFromKey(targetSkelKey)
            Else
                SkeletonInstance.Default.LoadFromConfig(True, True)
            End If
            _loadedSkeletonKey = targetSkelKey
        End If

        EditPreviewControl.WM_Set_Last_rendered(Nothing)
        EditPreviewControl.Model.FloorOffset = -selected_slider.HighHeelHeight
        EditPreviewControl.Update_Render(selected_slider, True, Nothing, Nothing, WM_Config.SliderSize.Default)
    End Sub
    Private Last_key As String = ""
    Private Sub DictionaryPicker_Control1_SelectionChanged(Key As String) Handles DictionaryPicker_Control1.SelectionChanged
        Last_key = Key
        If Key <> "" Then
            Read_selected(Key)
        Else
            EditPreviewControl.Model.Clean(False)
            CheckBox1.Enabled = False
        End If
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        If Last_key <> "" Then Read_selected(Last_key)
    End Sub
End Class