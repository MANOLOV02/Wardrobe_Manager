Imports System.ComponentModel
Imports System.Diagnostics.Eventing.Reader
Imports System.Globalization
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports Material_Editor
Imports NiflySharp
Imports NiflySharp.Blocks
Imports OpenTK.Graphics.OpenGL.GL
Imports OpenTK.Mathematics
Public Class Editor_Form

    Private Last_BMP_Name As String = ""
    Private Last_BMP As Bitmap = Nothing
    Public Property Selected_Slider As SliderSet_Class = Nothing
    Public Selected_Shape As Shape_class = Nothing
    Public Selected_Material As FO4UnifiedMaterial_Class = Nothing
    Public Event Render_By_Edit(seleccionado As SliderSet_Class, force As Boolean)
    Public Event Edit_Begun()
    Public Event Edit_Ended()
    Public Grabable As Boolean = True
    Private _Editando As Boolean = False
    Private _LastMaterial As New FO4UnifiedMaterial_Class
    Private Sub Iniciado_Edit()
        If _Editando = False Then
            _Editando = True
            RaiseEvent Edit_Begun()
        End If
        Dim equalmaterial As Boolean = _LastMaterial.AreEqualTo(Selected_Shape.RelatedMaterial.material)
        ButtonMatCancel.Enabled = Not equalmaterial
        ButtonMatSaveAs.Enabled = Not equalmaterial
        ButtonMatSave.Enabled = Not equalmaterial
        ButtonMatLoad.Enabled = True
        ButtonSave.Enabled = Not ButtonMatSave.Enabled And _Editando
        ButtonCancel.Enabled = Not ButtonMatSave.Enabled And _Editando
        ComboBoxShapes.Enabled = equalmaterial
    End Sub
    Private Sub Finalizado_Edit()
        _Editando = False
        _LastMaterial = Nothing
        RaiseEvent Edit_Ended()
        ButtonCancel.Enabled = False
        ButtonSave.Enabled = False
    End Sub
    Public ReadOnly Property GrayscaleBMP_Rotated As Bitmap
        Get
            Dim fil As String = FO4UnifiedMaterial_Class.CorrectTexturePath(Selected_Material.GreyscaleTexture)
            If fil <> Last_BMP_Name Then
                Last_BMP = CreateBitmapFromDDS(FilesDictionary_class.GetBytes(fil))
                If Not IsNothing(Last_BMP) Then
                    Last_BMP.RotateFlip(RotateFlipType.Rotate270FlipNone)
                End If
                Last_BMP_Name = fil
            End If
            Return Last_BMP
        End Get
    End Property
    Private ReadOnly bones_list As New Dictionary(Of String, Integer)(StringComparison.OrdinalIgnoreCase)

    Private Sub Lee_Bones()
        bones_list.Clear()
        ListView1.Items.Clear()
        For Each shap In Selected_Slider.Shapes
            For Each bon In shap.RelatedBones
                Dim idx = shap.ParentSliderSet.NIFContent.Blocks.IndexOf(bon)
                Dim nam As String = bon.Name.String
                bones_list.TryAdd(nam, idx)
            Next
        Next

        TreeViewSkeleton.SuspendLayout()
        TreeViewSkeleton.BeginUpdate()
        TreeViewSkeleton.Nodes.Clear()
        If Skeleton_Class.HasSkeleton Then
            For Each it In Skeleton_Class.SkeletonStructure
                Recurse(Nothing, it)
            Next
        End If

        TreeViewSkeleton.Sort()
        TreeViewSkeleton.ExpandAll()

        TreeViewSkeleton.EndUpdate()
        TreeViewSkeleton.ResumeLayout()

        ListView1.Items.AddRange(bones_list.Keys.Order.Select(Function(pf) New ListViewItem({pf, "0"}.ToArray) With {.Name = pf}).ToArray)
        Lee_Zaps()
    End Sub

    Private Sub Recurse(Parentnode As TreeNode, sknode As Skeleton_Class.HierarchiBone_class)
        Dim node As TreeNode
        If IsNothing(Parentnode) Then
            node = TreeViewSkeleton.Nodes.Add(sknode.BoneName)
        Else
            node = Parentnode.Nodes.Add(sknode.BoneName)
        End If
        If bones_list.ContainsKey(sknode.BoneName) Then
            node.Tag = True
            node.ForeColor = Color.Blue
        Else
            node.Tag = False
            node.ForeColor = Color.DarkGray
        End If
        For Each ch In sknode.Childrens
            Recurse(node, ch)
        Next
    End Sub

    Private Selected_Preset As New SlidersPreset_Class
    Private Selected_Pose As Poses_class = Nothing
    Private ComboSelected_Pose As Poses_class = Nothing

    Private Selected_Pose_Transform As PoseTransformData = Nothing
    Private nif_cat = "(NIF)"
    Private Slid_cat = "(Preset)"
    Private Sub Actualiza_Preset()
        Selected_Preset.Sliders.Clear()

        ' De las categorias
        For Each Cat In FilesDictionary_class.SliderPresets.Categories
            For Each slid In Cat.Value
                For x = 0 To 2
                    Dim sli As New PresetSlider_Class With {.Name = slid(0), .DisplayName = slid(1), .Value = 0, .Category = Cat.Key, .Size = x}
                    Selected_Preset.Sliders.Add(sli)
                Next
            Next
        Next

        If Not IsNothing(Selected_Slider) Then
            ' De las formas
            For Each slid In Selected_Slider.Sliders
                If IsNothing(Selected_Preset.Sliders.Find(Function(pf) pf.Name = slid.Nombre)) = False Then
                    Dim sli0 As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Nombre And pf.Size = Config_App.SliderSize.Default).FirstOrDefault
                    If Not IsNothing(sli0) Then sli0.Value = slid.Default_Setting(Config_App.SliderSize.Default)
                    Dim sli As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Nombre And pf.Size = Config_App.SliderSize.Big).FirstOrDefault
                    If Not IsNothing(sli) Then sli.Value = slid.Default_Big_Value
                    Dim sli2 As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Nombre And pf.Size = Config_App.SliderSize.Small).FirstOrDefault
                    If Not IsNothing(sli2) Then sli2.Value = slid.Default_Small_Value
                Else
                    Dim sli As New PresetSlider_Class With {.Name = slid.Nombre, .DisplayName = slid.Nombre, .Value = slid.Default_Setting(Selected_size), .Category = nif_cat, .Size = Selected_size}
                    Selected_Preset.Sliders.Add(sli)
                End If
            Next
        End If

        ' Del Preset
        If ComboBoxPresets.SelectedIndex <> -1 Then
            Dim Selected_Combo_Preset = FilesDictionary_class.SliderPresets.Presets(ComboBoxPresets.Items(ComboBoxPresets.SelectedIndex))
            Selected_Preset.Name = Selected_Combo_Preset.Name
            Selected_Preset.GroupNames = Selected_Combo_Preset.GroupNames.ToList
            Selected_Preset.SetName = Selected_Combo_Preset.SetName
            For Each slid In Selected_Combo_Preset.Sliders
                If IsNothing(Selected_Preset.Sliders.Find(Function(pf) pf.Name = slid.Name)) = False Then
                    Dim sli0 As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Name And pf.Size = Config_App.SliderSize.Default).FirstOrDefault
                    If Not IsNothing(sli0) And slid.Size = Config_App.SliderSize.Default Then sli0.Value = slid.Value
                    Dim sli As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Name And pf.Size = Config_App.SliderSize.Big).FirstOrDefault
                    If Not IsNothing(sli) And slid.Size = Config_App.SliderSize.Big Then sli.Value = slid.Value
                    Dim sli2 As PresetSlider_Class = Selected_Preset.Sliders.Where(Function(pf) pf.Name = slid.Name And pf.Size = Config_App.SliderSize.Small).FirstOrDefault
                    If Not IsNothing(sli2) And slid.Size = Config_App.SliderSize.Small Then sli2.Value = slid.Value
                Else
                    Dim sli As New PresetSlider_Class With {.Name = slid.Name, .DisplayName = slid.Name, .Value = slid.Value, .Category = Slid_cat, .Size = slid.Size}
                    Selected_Preset.Sliders.Add(sli)
                End If
            Next
        End If


        Habilita_Preset_Botones(False)
        Pone_SLiders()
    End Sub

    Private Sub Habilita_Preset_Botones(Opcion As Boolean)
        ButtondelPreset.Enabled = Not Opcion
        ButtonSavePreset.Enabled = Opcion
        ButtonSaveAsPreset.Enabled = Opcion
        ButtonCancelPreset.Enabled = Opcion
    End Sub
    Private Sub Pone_SLiders()
        TableLayoutPanel4.SuspendLayout()
        TableLayoutPanel4.Parent.SuspendLayout()
        ' Configura TableLayoutPanel
        Dim tlp As TableLayoutPanel = Me.TableLayoutPanel4
        tlp.Controls.Clear()
        tlp.RowStyles.Clear()
        tlp.RowCount = 0
        tlp.ColumnCount = 2
        tlp.ColumnStyles.Clear()
        tlp.AutoSize = True
        tlp.AutoSizeMode = AutoSizeMode.GrowAndShrink
        tlp.Dock = DockStyle.Top
        tlp.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlp.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        ' Recorre cada categoría y cada slider definido en XML
        For Each kvp In Selected_Preset.Sliders.Select(Function(pf) pf.Category).Order.Distinct
            Dim catName As String = kvp
            Dim sliderNames As List(Of PresetSlider_Class) = Selected_Preset.Sliders.Where(Function(pf) pf.Category = catName AndAlso pf.Size = Selected_size).ToList

            ' Fila de encabezado (20px)
            Dim hdrRow As Integer = tlp.RowCount
            tlp.RowCount += 1
            tlp.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))
            Dim lblCat As New Label With {
            .Text = catName,
            .Font = New Font(tlp.Font, FontStyle.Bold),
            .AutoSize = False,
            .Height = 20,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }
            tlp.Controls.Add(lblCat, 0, hdrRow)
            tlp.SetColumnSpan(lblCat, 2)

            ' Filas de sliders (20px cada una)
            For Each slideName In sliderNames
                Dim presetItem As PresetSlider_Class = Selected_Preset.Sliders.Find(Function(s) s.Name = slideName.Name And s.Size = Selected_size)
                Dim initValue As Integer = If(presetItem IsNot Nothing, CInt(presetItem.Value), 0)

                Dim row As Integer = tlp.RowCount
                tlp.RowCount += 1
                tlp.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))

                ' Label con el nombre del slider
                Dim lbl As New Label With {
                .Text = slideName.DisplayName,
                .AutoSize = False,
                .Height = 20,
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Margin = New Padding(0)
            }
                tlp.Controls.Add(lbl, 0, row)

                ' TrackBar con valor inicial
                Dim tb As New TrackBar With {
                .Minimum = Math.Min(0, initValue),
                .Maximum = Math.Max(100, initValue),
                .TickStyle = TickStyle.None,
                .Value = initValue,
                .AutoSize = False,
                .Height = 20,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0)
            }
                If tb.Minimum <> 0 Or tb.Maximum <> 100 Then
                    tb.BackColor = Color.LightYellow
                End If
                tlp.Controls.Add(tb, 1, row)

                ' Solo sincroniza si el preset existe
                If presetItem IsNot Nothing Then
                    AddHandler tb.Scroll, Sub()
                                              presetItem.Value = tb.Value
                                              pendingValuePreset = True
                                              If Not PresetscrollTimer.Enabled Then PresetscrollTimer.Start()
                                          End Sub
                    AddHandler tb.MouseUp, Sub()
                                               If pendingValuePreset Then
                                                   Habilita_Preset_Botones(True)
                                                   Process_render_Changes(False)
                                                   pendingValuePreset = False
                                               End If
                                               PresetscrollTimer.Stop()
                                           End Sub
                End If
            Next
        Next
        TableLayoutPanel4.ResumeLayout()
        TableLayoutPanel4.Parent.ResumeLayout()
    End Sub
    Private WithEvents PresetscrollTimer As New Timer() With {.Interval = 500, .Enabled = False}
    Private pendingValuePreset As Boolean = False
    Private Sub PresetscrollTimer_Tick(sender As Object, e As EventArgs) Handles PresetscrollTimer.Tick
        If pendingValuePreset Then
            Habilita_Preset_Botones(True)
            Process_render_Changes(False)
            pendingValuePreset = False
        End If
    End Sub

    Private Sub Lee_Zaps()
        ListView2.Items.Clear()
        ' Lee Zap
        For Each sl In Selected_Slider.Sliders
            If sl.IsZap OrElse sl.IsManoloFix Then
                Dim tipo As String = "Zap"
                If sl.IsManoloFix Then tipo = "Fix"
                ListView2.Items.Add(New ListViewItem({sl.Nombre, tipo}) With {.Name = sl.Nombre, .Tag = sl})
            End If
        Next
    End Sub
    Private Sub MarcaBones()
        For Each it As ListViewItem In ListView1.Items
            it.ForeColor = Color.Gray
            it.SubItems(1).Text = "None"
        Next
        Dim j As Integer
        For i = 0 To Selected_Shape.RelatedBones.Count - 1
            Dim bonIt = ListView1.Items.Find(Selected_Shape.RelatedBones(i).Name.String, False).FirstOrDefault
            j = i
            Dim Nifversion = Selected_Shape.ParentSliderSet.NIFContent.Header.Version
            Dim vert As Integer
            If Nifversion.IsSSE Then
                vert = Selected_Shape.RelatedNifShape.VertexDataSSE.Where(Function(pf) pf.BoneIndices.Contains(j)).Count
            Else
                vert = Selected_Shape.RelatedNifShape.VertexData.Where(Function(pf) pf.BoneIndices.Contains(j)).Count
            End If

            bonIt.SubItems(1).Text = vert.ToString
            If Not IsNothing(bonIt) Then bonIt.ForeColor = Color.Blue
        Next
    End Sub

    Private Selected_size As Config_App.SliderSize
    Public Sub Lee_Edit(Seleccion As SliderSet_Class, Preset As String, Pose As String)
        OSP_Project_Class.Load_and_CHeck_Project(Seleccion, True)
        CheckBoxZappedShapes.Checked = Seleccion.KeepZappedShapes
        CheckBoxPreventMorph.Checked = Seleccion.PreventMorphFile
        CheckBoxGenweight.Checked = Seleccion.GenWeights
        Label34.Visible = CheckBox2.Checked AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim
        ComboBoxSize.SelectedIndex = Config_App.Current.Bodytipe
        Selected_size = Config_App.Current.Bodytipe
        Selected_Slider = Seleccion
        ComboBoxPresets.Items.AddRange(FilesDictionary_class.SliderPresets.Presets.Select(Function(pf) pf.Key).Order.ToArray)
        ComboBoxPoses.Items.AddRange(FilesDictionary_class.SliderPresets.Poses.Select(Function(pf) pf.Key).Order.ToArray)
        Dim idx = ComboBoxPresets.FindString(Preset)
        Dim idx2 = ComboBoxPoses.FindString(Pose)
        If idx <> -1 Then ComboBoxPresets.SelectedIndex = idx Else ComboBoxPresets.SelectedIndex = 0
        If idx2 <> -1 Then ComboBoxPoses.SelectedIndex = idx2 Else ComboBoxPoses.SelectedIndex = 0
        Actualiza_Preset()
        SingleBoneCheck.Checked = EditPreviewControl.Model.SingleBoneSkinning
        If IsNothing(Selected_Slider) Then
            OutDirTextbox.Text = ""
            OutFilTextbox.Text = ""
            MaterialPathTextbox.Text = ""
            TextBox2.Text = ""
            ComboBoxShapes.Items.Clear()
            ComboBoxMaterials.Items.Clear()
            HHNumericUpDown.Value = 0
            ButtonRemovePhysics.Enabled = False
            Button1.Enabled = False
            ButtonCancel.Enabled = False
            ButtonSave.Enabled = False
            ButtonMatSave.Enabled = False
            ButtonMatSaveAs.Enabled = False
            ButtonMatCancel.Enabled = False
            ButtonMatLoad.Enabled = False
            bones_list.Clear()
            Button7.Enabled = False
        Else
            Lee_Bones()
            OutDirTextbox.Text = Selected_Slider.OutputPathValue
            TextBox2.Text = Selected_Slider.DescriptionValue
            Button7.Enabled = OutDirTextbox.Text.Correct_Path_Separator.Contains("ManoloCloned\", StringComparison.OrdinalIgnoreCase)
            OutFilTextbox.Text = Selected_Slider.OutputFileValue
            ComboBoxShapes.Items.Clear()
            ComboBoxMaterials.Items.Clear()
            ComboBoxShapes.Items.AddRange(Selected_Slider.Shapes.Select(Function(pf) pf.Nombre).ToArray)
            If ComboBoxShapes.Items.Count > 0 Then Selected_Shape = Selected_Slider.Shapes(0)
            HHNumericUpDown.Value = Selected_Slider.HighHeelHeight
            ButtonRemovePhysics.Enabled = Selected_Slider.HasPhysics
            Button1.Enabled = ComboBoxShapes.Items.Count > 1
            ButtonCancel.Enabled = False
            ButtonSave.Enabled = False
            ButtonMatSave.Enabled = False
            ButtonMatSaveAs.Enabled = False
            ButtonMatCancel.Enabled = False
            ButtonMatLoad.Enabled = True
        End If
        Habiliza_Zap_Buttons()
        _Editando = False
    End Sub
    Private Sub ComboBoxShapes_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxShapes.SelectedIndexChanged
        Selected_Shape = Selected_Slider.Shapes.Where(Function(pf) pf.Nombre.Equals(ComboBoxShapes.SelectedItem.ToString, StringComparison.OrdinalIgnoreCase)).FirstOrDefault
        RenderCheckWeights.Checked = Selected_Shape.ShowWeight
        RenderCheckcolors.Checked = Selected_Shape.RelatedNifShape.HasVertexColors
        RenderCheckMasks.Checked = Selected_Shape.ShowMask
        RenderCheckTexture.Checked = Selected_Shape.ShowTexture
        RenderCheckWireframe.Checked = Selected_Shape.Wireframe
        RenderCheckZap.Checked = Selected_Shape.ApplyZaps
        RenderCheckHide.Checked = Selected_Shape.RenderHide
        RenderCheckVertexColors.Checked = Selected_Shape.ShowVertexColor
        ColorComboBox1.SelectedColor = Selected_Shape.Wirecolor
        TrackBar1.Value = Math.Max(0, Math.Min(100, CInt(Selected_Shape.WireAlpha * 100)))
        Button1.Enabled = ComboBoxShapes.Items.Count > 1
        Lee_Materials()
        MarcaBones()
    End Sub
    Private Sub Lee_Materials()
        Dim idx As Integer = -1
        Dim prefix = "materials\"
        ComboBoxMaterials.Items.Clear()
        If Not IsNothing(Selected_Shape) Then

            Dim path As String = IO.Path.GetDirectoryName(Selected_Shape.RelatedMaterial.path).Correct_Path_Separator
            If path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then path = path.Substring(prefix.Length)
            Dim fil As String = IO.Path.GetFileName(Selected_Shape.RelatedMaterial.path)
            Dim ext As String = IO.Path.GetExtension(fil)
            Dim pathtxt = prefix + path
            If pathtxt.EndsWith("\"c) Then pathtxt = pathtxt.Substring(0, pathtxt.Length - 1)
            MaterialPathTextbox.Text = pathtxt
            If fil <> "" Then ComboBoxMaterials.Items.AddRange(FilesDictionary_class.Dictionary.Keys.Where(Function(pf) IO.Path.GetDirectoryName(pf).Correct_Path_Separator.Equals(pathtxt, StringComparison.OrdinalIgnoreCase) AndAlso pf.EndsWith(ext, StringComparison.OrdinalIgnoreCase)).Select(Function(pf) IO.Path.GetFileName(pf)).Order.ToArray)
            idx = ComboBoxMaterials.FindStringExact(fil)
            If idx <> -1 Then ComboBoxMaterials.SelectedIndex = idx Else If ComboBoxMaterials.Items.Count > 0 Then ComboBoxMaterials.SelectedIndex = 0
            If Selected_Shape.RelatedMaterial.path = "" OrElse idx = -1 Then
                MaterialPathTextbox.Text = prefix
                ComboBoxMaterials.Items.Add("Embedded")
                ComboBoxMaterials.SelectedIndex = 0
                Exit Sub
            End If
        End If
    End Sub
    Private Sub PropertyGrid1_PropertyValueChanged(s As Object, e As PropertyValueChangedEventArgs) Handles PropertyGrid1.PropertyValueChanged
        Update_Grayscale()
        Iniciado_Edit()
        Render_Changes(False)
    End Sub
    Private Sub GrayScaleTrackbar1_ValueChanged(sender As Object, e As EventArgs) Handles GrayScaleTrackbar1.ValueChanged
        Selected_Material.GrayscaleToPaletteScale = GrayScaleTrackbar1.Setvalue
        Iniciado_Edit()
        Render_Changes(False)
    End Sub
    Private Sub Update_Grayscale()
        If Selected_Material.GreyscaleTexture <> "" AndAlso Not IsNothing(GrayscaleBMP_Rotated) Then
            If GrayScaleTrackbar1.Value <> GrayScaleTrackbar1.Getvalue(Selected_Material.GrayscaleToPaletteScale) Then Iniciado_Edit()
            GrayScaleTrackbar1.BackgroundImage = GrayscaleBMP_Rotated
            GrayScaleTrackbar1.Maximum = GrayscaleBMP_Rotated.Width
            GrayScaleTrackbar1.Value = GrayScaleTrackbar1.Getvalue(Selected_Material.GrayscaleToPaletteScale)
            GrayScaleTrackbar1.Enabled = Selected_Material.GrayscaleToPaletteColor
            ButtonMakeGradient.Enabled = False
        Else
            ButtonMakeGradient.Enabled = True
            GrayScaleTrackbar1.Maximum = 100
            GrayScaleTrackbar1.Value = 50
            GrayScaleTrackbar1.Enabled = False
            GrayScaleTrackbar1.BackgroundImage = Nothing
        End If
    End Sub

    Private Sub ComboBoxMaterials_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxMaterials.SelectedIndexChanged
        If IsNothing(ComboBoxMaterials.SelectedItem) Then Exit Sub
        ButonMatBackToOriginal.Enabled = MaterialPathTextbox.Text.Contains("ManoloCloned\", StringComparison.OrdinalIgnoreCase)
        Lee_Comboselected_Material(Path.Combine(MaterialPathTextbox.Text, ComboBoxMaterials.SelectedItem).Correct_Path_Separator)
        Iniciado_Edit()
    End Sub
    Private Sub Lee_Comboselected_Material(fullpath As String)
        Selected_Material = New FO4UnifiedMaterial_Class
        Dim prefix = "materials\"
        If IsNothing(Selected_Shape.RelatedNifShader) Then
            If Selected_Shape.RelatedNifShape.ShaderPropertyRef.Index <> -1 Then
                Debugger.Break()
                Throw New Exception
            End If
            Dim nif = Selected_Shape.ParentSliderSet.NIFContent
            Dim shad = New BSLightingShaderProperty
            Selected_Shape.RelatedNifShape.ShaderPropertyRef = New NiBlockRef(Of BSShaderProperty) With {.Index = nif.AddBlock(shad)}
            Dim texset1 = New BSShaderTextureSet
            shad._textureSet = New NiBlockRef(Of BSShaderTextureSet) With {.Index = nif.AddBlock(texset1)}
            texset1.Textures = New List(Of NiString4)
            While texset1.Textures.Count < 8
                texset1.Textures.Add(New NiString4 With {.Content = ""})
            End While
            Debugger.Break()
        End If
        Select Case Selected_Shape.RelatedNifShader.GetType
            Case GetType(BSLightingShaderProperty)
                If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
                If fullpath = "Embedded" Then
                    Selected_Material.Create_From_Shader(Selected_Slider.NIFContent, Selected_Shape.RelatedNifShape, CType(Selected_Shape.RelatedNifShader, BSLightingShaderProperty))
                    _LastMaterial.Create_From_Shader(Selected_Slider.NIFContent, Selected_Shape.RelatedNifShape, CType(Selected_Shape.RelatedNifShader, BSLightingShaderProperty))
                    Selected_Shape.RelatedMaterial.material = Selected_Material
                    Label6.Text = "None"
                    Label6.ForeColor = Color.FromKnownColor(KnownColor.DarkGray)
                    Exit Select
                End If
                Selected_Material.Deserialize(prefix + fullpath, GetType(BGSM))
                _LastMaterial.Deserialize(prefix + fullpath, GetType(BGSM))
                If FilesDictionary_class.Dictionary(prefix + fullpath).IsLosseFile Then
                    Label6.Text = "Loose"
                    If fullpath.Contains("ManoloCloned", StringComparison.OrdinalIgnoreCase) Or fullpath.Contains("ManoloMods", StringComparison.OrdinalIgnoreCase) Then
                        Label6.ForeColor = Color.FromKnownColor(KnownColor.Blue)
                    Else
                        Label6.ForeColor = Color.FromKnownColor(KnownColor.Red)
                    End If
                Else
                    Label6.Text = IO.Path.GetExtension(FilesDictionary_class.Dictionary(prefix + fullpath).BA2File)
                    Label6.ForeColor = Color.FromKnownColor(KnownColor.Brown)
                End If
                Selected_Shape.RelatedMaterial.path = fullpath
                Selected_Shape.RelatedMaterial.material = Selected_Material
            Case GetType(BSEffectShaderProperty)
                If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
                If fullpath = "Embedded" Then
                    Selected_Material.Create_From_Shader(Selected_Slider.NIFContent, Selected_Shape.RelatedNifShape, CType(Selected_Shape.RelatedNifShader, BSEffectShaderProperty))
                    _LastMaterial.Create_From_Shader(Selected_Slider.NIFContent, Selected_Shape.RelatedNifShape, CType(Selected_Shape.RelatedNifShader, BSEffectShaderProperty))
                    Selected_Shape.RelatedMaterial.material = Selected_Material
                    Label6.Text = "None"
                    Label6.ForeColor = Color.FromKnownColor(KnownColor.DarkGray)
                    Exit Select
                End If
                If FilesDictionary_class.Dictionary.ContainsKey(prefix + fullpath) Then
                    Selected_Material.Deserialize(prefix + fullpath, GetType(BGEM))
                    _LastMaterial.Deserialize(prefix + fullpath, GetType(BGEM))
                    If FilesDictionary_class.Dictionary(prefix + fullpath).IsLosseFile Then
                        Label6.Text = "Loose"
                    Else
                        Label6.Text = IO.Path.GetExtension(FilesDictionary_class.Dictionary(prefix + fullpath).BA2File)
                    End If
                    Selected_Shape.RelatedMaterial.path = fullpath
                    Selected_Shape.RelatedMaterial.material = Selected_Material
                End If
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select


        PropertyGrid1.SelectedObject = Selected_Material
        Update_Grayscale()
        Render_Changes(False)
        Iniciado_Edit()
    End Sub
    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged
        PropertyGrid1.Enabled = CheckBox2.Checked And CheckBox2.Enabled
        Label34.Visible = CheckBox2.Checked AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckTexture.CheckedChanged
        Selected_Shape.ShowTexture = RenderCheckTexture.Checked
        ColorComboBox1.Enabled = RenderCheckWireframe.Checked Or Not RenderCheckTexture.Checked
        Render_Changes(False)
    End Sub

    Private Sub RenderCheckWireframe_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckWireframe.CheckedChanged
        Selected_Shape.Wireframe = RenderCheckWireframe.Checked
        ColorComboBox1.Enabled = RenderCheckWireframe.Checked Or Not RenderCheckTexture.Checked
        TrackBar1.Enabled = RenderCheckWireframe.Checked
        Render_Changes(False)
    End Sub

    Private Sub RenderCheckMasks_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckMasks.CheckedChanged
        Selected_Shape.ShowMask = RenderCheckMasks.Checked
        'RenderCheckZap.Enabled = Not RenderCheckMasks.Checked
        Render_Changes(False)

    End Sub

    Private Sub RenderCheckZap_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckZap.CheckedChanged
        Selected_Shape.ApplyZaps = RenderCheckZap.Checked
        Render_Changes(False)

    End Sub

    Private Sub RenderCheckWeights_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckWeights.CheckedChanged
        Selected_Shape.ShowWeight = RenderCheckWeights.Checked
        Render_Changes(False)
    End Sub

    Private Sub CRenderCheckHide_CheckedChanged_1(sender As Object, e As EventArgs) Handles RenderCheckHide.CheckedChanged
        Selected_Shape.RenderHide = RenderCheckHide.Checked
        Render_Changes(False)
    End Sub

    Private Sub ButtonRemovePhysics_Click(sender As Object, e As EventArgs) Handles ButtonRemovePhysics.Click
        If MsgBox("This cant be undone, do you want to delete all physics", vbYesNo, "Remove Physics") = MsgBoxResult.Yes Then
            Selected_Slider.NIFContent.RemoveBlocksOfType(Of BSClothExtraData)()
            Render_Changes(True)
            ButtonRemovePhysics.Enabled = False
            Iniciado_Edit()
        End If
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Selected_Slider.RemoveShape(Selected_Shape)
        ComboBoxShapes.Items.RemoveAt(ComboBoxShapes.SelectedIndex)
        Render_Changes(True)
        ComboBoxShapes.SelectedIndex = 0
        Iniciado_Edit()
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles ButtonCancel.Click
        Finalizado_Edit()
        Me.Close()
    End Sub
    Private Sub Render_Changes(Force As Boolean)
        Process_render_Changes(Force)
        RaiseEvent Render_By_Edit(Selected_Slider, Force)
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles ButtonSave.Click
        If Revisa_Material() Then
            If Not Grabable AndAlso Selected_Slider.ParentOSP.IsManoloPack = False Then
                If MsgBox("This is not a Manolo Pack project, it is a source, are you sure you want to edit it", vbYesNo, "Warning - Source edit") = MsgBoxResult.No Then
                    Exit Sub
                End If
            End If
            Selected_Slider.Save_Shapedatas(True)
            Selected_Slider.ParentOSP.Save_Pack(True)
            Dim hhfile = IO.Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, IO.Path.Combine(Selected_Slider.OutputPathValue, Selected_Slider.OutputFileValue)).Replace(".nif", "hht", StringComparison.OrdinalIgnoreCase)
            If hhfile.EndsWith(".hht") = False Then hhfile += ".hht"
            Selected_Slider.SaveHighHeel(hhfile, True)
            Finalizado_Edit()
            Close()
        End If
    End Sub
    Private Function Revisa_Material() As Boolean
        ' Define material
        Dim prefix = "materials\"
        Dim TestChanges As New FO4UnifiedMaterial_Class
        For Each shap In Selected_Slider.Shapes
            Dim orig = shap.RelatedMaterial.path.Correct_Path_Separator
            If orig.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then orig = orig.Substring(prefix.Length)
            Select Case Path.GetExtension(orig).ToLower
                Case ".bgem"
                    TestChanges.Deserialize(prefix + orig, GetType(BGEM))
                Case ".bgsm"
                    TestChanges.Deserialize(prefix + orig, GetType(BGSM))
                Case ""
                    Select Case shap.RelatedNifShader.GetType
                        Case GetType(BSEffectShaderProperty)
                            TestChanges.Create_From_Shader(Selected_Slider.NIFContent, shap.RelatedNifShape, CType(shap.RelatedNifShader, BSEffectShaderProperty))
                        Case GetType(BSLightingShaderProperty)
                            TestChanges.Create_From_Shader(Selected_Slider.NIFContent, shap.RelatedNifShape, CType(shap.RelatedNifShader, BSLightingShaderProperty))
                        Case Else
                            Debugger.Break()
                            Throw New Exception
                    End Select
            End Select
            If shap.RelatedMaterial.material.AreEqualTo(TestChanges) Then
                'Simplemente cambie el material
            Else
                MsgBox("Must save materials modification in " + shap.Nombre + " first", vbOKOnly + vbCritical, "Error")
                Return False
            End If
            Selected_Slider.NIFContent.SetRelatedMaterial(shap.RelatedNifShape, shap.RelatedMaterial.path, shap.RelatedMaterial.material)
        Next
        Return True
    End Function

    Private Sub HHNumericUpDown_ValueChanged(sender As Object, e As EventArgs) Handles HHNumericUpDown.ValueChanged
        If IsNothing(Selected_Slider) Then Exit Sub
        Selected_Slider.HighHeelHeight = HHNumericUpDown.Value
        Iniciado_Edit()
    End Sub

    Private Sub OutDirTextbox_TextChanged(sender As Object, e As EventArgs) Handles OutDirTextbox.Leave
        If Selected_Slider.OutputPathValue <> OutDirTextbox.Text Then Iniciado_Edit()
        Selected_Slider.OutputPathValue = OutDirTextbox.Text
    End Sub

    Private Sub OutFilTextbox_TextChanged(sender As Object, e As EventArgs) Handles OutFilTextbox.Leave
        If Selected_Slider.OutputFileValue <> OutFilTextbox.Text Then Iniciado_Edit()
        Selected_Slider.OutputFileValue = OutFilTextbox.Text
    End Sub
    Private WithEvents EditPreviewControl As New PreviewControl
    Private Sub EditorControl_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        If ComboBoxShapes.Items.Count > 0 Then ComboBoxShapes.SelectedIndex = 0
        Application.DoEvents()
        EditPreviewControl = New PreviewControl With {.Dock = DockStyle.Fill}
        Panel1.Controls.Add(EditPreviewControl)
        EditPreviewControl.Model.SingleBoneSkinning = Config_App.Current.Setting_SingleBoneSkinning
        EditPreviewControl.AllowMask = True
        NumericMaskRadius.Value = EditPreviewControl.BrushRadiusPx
        Process_render_Changes(True)
    End Sub

    Private Sub EditorControl_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        For Each shap In Selected_Slider.Shapes
            shap.Wireframe = False
            shap.ShowTexture = True
            shap.ShowMask = False
            shap.ShowWeight = False
            shap.ApplyZaps = True
            shap.RenderHide = False
            shap.MaskedVertices.Clear()
        Next
        EditPreviewControl.Clean()
        EditPreviewControl.Dispose()
    End Sub


    Private Sub ButtonMatCancel_Click(sender As Object, e As EventArgs) Handles ButtonMatCancel.Click
        If ComboBoxMaterials.SelectedIndex = -1 Then Exit Sub
        Lee_Comboselected_Material(Path.Combine(MaterialPathTextbox.Text, ComboBoxMaterials.SelectedItem).Correct_Path_Separator)
    End Sub

    Private Sub ButtonMatSave_Click(sender As Object, e As EventArgs) Handles ButtonMatSave.Click
        Dim prefix = "materials\"
        Dim fil = Selected_Shape.RelatedMaterial.path.Correct_Path_Separator
        If fil.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) = False Then fil = prefix + fil
        If fil = "" OrElse FilesDictionary_class.Dictionary.ContainsKey(fil) = False Then
            ButtonMatSaveAs.PerformClick()
            Exit Sub
        End If
        Dim xx = FilesDictionary_class.Dictionary(fil)
        If xx.IsLosseFile = False Then
            MsgBox("Cant write Ba2/BSA file. Choose save as... instead", vbOKOnly + vbCritical, "Error")
            Exit Sub
        End If
        If MsgBox("Override material file?", vbYesNo + vbExclamation, "Warning") = MsgBoxResult.Yes Then
            Using Writer As New FileStream(Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, xx.FullPath), FileMode.Create)
                Selected_Shape.RelatedMaterial.material.Underlying_Material.Save(Writer)
                Writer.Close()
            End Using
        End If
        MaterialPathTextbox.Text = prefix + Path.GetDirectoryName(Selected_Shape.RelatedMaterial.path)
        ComboBoxMaterials.SelectedIndex = ComboBoxMaterials.Items.Add(Path.GetFileName(Selected_Shape.RelatedMaterial.path))
        Lee_Comboselected_Material(Path.Combine(MaterialPathTextbox.Text, ComboBoxMaterials.SelectedItem).Correct_Path_Separator)
    End Sub

    Private Sub ButtonMatSaveAs_Click(sender As Object, e As EventArgs) Handles ButtonMatSaveAs.Click
        Dim prefix = "materials\"
        Dim fil = Selected_Shape.RelatedMaterial.path.Correct_Path_Separator
        If fil.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) = False Then fil = prefix + fil
        If IO.Directory.Exists(Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, IO.Path.GetDirectoryName(fil))) = False Then
            IO.Directory.CreateDirectory(Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, IO.Path.GetDirectoryName(fil)))
        End If

        Dim ext = Path.GetExtension(fil)
        Dim filtro As String = " files (*" + ".bgsm" + ")|*.bgsm"
        If ext = "" AndAlso Selected_Shape.RelatedNifShader.GetType Is GetType(BSEffectShaderProperty) Then filtro = " files (*" + ".bgem" + ")|*.bgem"
        If ext <> "" Then filtro = ext.Remove(0, 1).ToUpper + " files (*" + ext + ")|*" + ext
        Dim sd As New SaveFileDialog With {.AddExtension = True, .OverwritePrompt = True, .AddToRecent = False, .DefaultExt = ext, .Filter = filtro, .InitialDirectory = Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, Path.GetDirectoryName(fil)), .Title = "Save material file"}
        If sd.ShowDialog = DialogResult.OK Then
            Using Writer As New FileStream(Path.Combine(Wardrobe_Manager_Form.Directorios.Fallout4data, sd.FileName), FileMode.Create)
                Selected_Shape.RelatedMaterial.material.Underlying_Material.Save(Writer)
                Writer.Close()
            End Using
            Dim fullpath = Path.GetRelativePath(Wardrobe_Manager_Form.Directorios.Fallout4data, sd.FileName).Correct_Path_Separator
            If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
            Selected_Shape.RelatedMaterial.path = fullpath
            Dim Location As New FilesDictionary_class.File_Location With {.BA2File = "", .Index = -1, .FullPath = prefix + Selected_Shape.RelatedMaterial.path}
            FilesDictionary_class.Dictionary.TryAdd((prefix + Selected_Shape.RelatedMaterial.path).Correct_Path_Separator, Location)
            MaterialPathTextbox.Text = prefix + Path.GetDirectoryName(Selected_Shape.RelatedMaterial.path)
            ComboBoxMaterials.SelectedIndex = ComboBoxMaterials.Items.Add(Path.GetFileName(Selected_Shape.RelatedMaterial.path))
        End If
        If ComboBoxMaterials.SelectedIndex <> -1 Then Lee_Comboselected_Material(Path.Combine(MaterialPathTextbox.Text, ComboBoxMaterials.SelectedItem).Correct_Path_Separator)
    End Sub

    Private Sub ButtonMatLoad_Click(sender As Object, e As EventArgs) Handles ButtonMatLoad.Click
        Dim prefix = "materials\"
        Dim fil = Selected_Shape.RelatedMaterial.path.Correct_Path_Separator
        If fil.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) = False Then fil = prefix + fil

        Dim dict_used As FilesDictionary_class.DictionaryFilePickerConfig = FilesDictionary_class.MaterialsDictionary_BGSM_Filter
        If Selected_Shape.RelatedNifShader.GetType Is GetType(BSEffectShaderProperty) Then
            dict_used = FilesDictionary_class.MaterialsDictionary_BGEM_Filter
        End If

        Dim dictProvider = dict_used.DictionaryProvider
        Dim dict = dictProvider.Invoke()
        Dim filtered = dict.Keys.Where(Function(k) FilesDictionary_class.DictionaryFilePickerConfig.PathStartsWithRoot(k, dict_used.RootPrefix) And dict_used.ExtensionAllowed(k)).Order.ToList()
        Dim initialKey As String = TryCast(fil, String)
        Using frm As New DictionaryFilePicker_Form(filtered, dict_used.RootPrefix, dict_used.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.OK Then
                Dim sel = frm.DictionaryPicker_Control1.SelectedKey
                Dim fullpath = sel.Correct_Path_Separator
                If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
                Selected_Shape.RelatedMaterial.path = fullpath
                Lee_Materials()
                MaterialPathTextbox.Text = prefix + Path.GetDirectoryName(Selected_Shape.RelatedMaterial.path)
                ComboBoxMaterials.SelectedIndex = ComboBoxMaterials.Items.Add(Path.GetFileName(Selected_Shape.RelatedMaterial.path))
            End If
        End Using
        If ComboBoxMaterials.SelectedItem <> "" Then Lee_Comboselected_Material(Path.Combine(MaterialPathTextbox.Text, ComboBoxMaterials.SelectedItem).Correct_Path_Separator)
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Selected_Shape.MaskedVertices.UnionWith(Procesa_Bones_Mask)
        Process_render_Changes(False)
    End Sub
    Private Function Procesa_Bones_Mask() As HashSet(Of Integer)
        Dim bonesNames = Selected_Shape.RelatedBones.Select(Function(pf) pf.Name.String).ToList
        Dim lista As New HashSet(Of Integer)()

        For Each it As ListViewItem In ListView1.SelectedItems
            Dim min = CType(NumericUpDown1.Value, System.Half)
            Dim max = CType(NumericUpDown2.Value, System.Half)
            Dim bidx = bonesNames.IndexOf(it.Name)
            Dim vert As Integer

            If bidx <> -1 Then
                Dim Nifversion = Selected_Shape.ParentSliderSet.NIFContent.Header.Version
                If Nifversion.IsSSE Then
                    Dim verst As IEnumerable(Of Integer) = Selected_Shape.RelatedNifShape.VertexDataSSE.Select(Function(pq, idx) New With {Key .pf = pq, Key .origIdx = idx}).Where(Function(item) item.pf.BoneIndices.Contains(bidx)).Select(Function(item) item.origIdx)
                    For Each vert In verst
                        Dim widx = Selected_Shape.RelatedNifShape.VertexDataSSE(vert).BoneIndices.ToList.IndexOf(bidx)
                        Dim weight = Selected_Shape.RelatedNifShape.VertexDataSSE(vert).BoneWeights(widx)
                        If weight >= min AndAlso weight <= max Then
                            lista.Add(vert)
                        End If
                    Next
                Else
                    Dim verst As IEnumerable(Of Integer) = Selected_Shape.RelatedNifShape.VertexData.Select(Function(pq, idx) New With {Key .pf = pq, Key .origIdx = idx}).Where(Function(item) item.pf.BoneIndices.Contains(bidx)).Select(Function(item) item.origIdx)
                    For Each vert In verst
                        Dim widx = Selected_Shape.RelatedNifShape.VertexData(vert).BoneIndices.ToList.IndexOf(bidx)
                        Dim weight = Selected_Shape.RelatedNifShape.VertexData(vert).BoneWeights(widx)
                        If weight >= min AndAlso weight <= max Then
                            lista.Add(vert)
                        End If
                    Next
                End If


            End If
        Next
        Return lista
    End Function

    Private Sub Button4_Click_1(sender As Object, e As EventArgs) Handles Button4.Click
        Dim Nifversion = Selected_Shape.ParentSliderSet.NIFContent.Header.Version
        If Nifversion.IsSSE Then
            Selected_Shape.MaskedVertices.UnionWith(Enumerable.Range(0, Selected_Shape.RelatedNifShape.VertexDataSSE.Count))
        Else
            Selected_Shape.MaskedVertices.UnionWith(Enumerable.Range(0, Selected_Shape.RelatedNifShape.VertexData.Count))
        End If

        Process_render_Changes(False)
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Selected_Shape.MaskedVertices.Clear()
        Process_render_Changes(False)
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        Dim lista As New HashSet(Of Integer)
        lista.UnionWith(Selected_Shape.MaskedVertices)
        Dim Nifversion = Selected_Shape.ParentSliderSet.NIFContent.Header.Version
        If Nifversion.IsSSE Then
            Selected_Shape.MaskedVertices.UnionWith(Enumerable.Range(0, Selected_Shape.RelatedNifShape.VertexDataSSE.Count))
        Else
            Selected_Shape.MaskedVertices.UnionWith(Enumerable.Range(0, Selected_Shape.RelatedNifShape.VertexData.Count))
        End If

        Selected_Shape.MaskedVertices.ExceptWith(lista)
        Process_render_Changes(False)
    End Sub

    Private Sub Button3_Click_1(sender As Object, e As EventArgs) Handles Button3.Click
        Selected_Shape.MaskedVertices.ExceptWith(Procesa_Bones_Mask)
        Process_render_Changes(False)
    End Sub
    Private SelectedZap As Slider_class = Nothing
    Private Sub Habiliza_Zap_Buttons()
        If ListView2.SelectedItems.Count > 0 Then SelectedZap = ListView2.SelectedItems(0).Tag Else SelectedZap = Nothing
        ZapLoad.Enabled = Not IsNothing(SelectedZap)
        ZapExclude.Enabled = Not IsNothing(SelectedZap)
        ZapInclude.Enabled = Not IsNothing(SelectedZap)
        ZapCreate.Enabled = Not IsNothing(Selected_Slider)
        ZapInverted.Enabled = Not IsNothing(SelectedZap)
        Zap_Zap.Enabled = Not IsNothing(SelectedZap)
        Zap_Fix.Enabled = Not IsNothing(SelectedZap)
        GroupBoxZaps.Enabled = Not IsNothing(SelectedZap) AndAlso SelectedZap.IsManoloFix = True
        DeleteZap.Enabled = Not IsNothing(SelectedZap)
        ButtonClearZap.Enabled = Not IsNothing(SelectedZap)
        If Not IsNothing(SelectedZap) Then
            ZapInverted.Checked = SelectedZap.Invert
            Zap_Zap.Checked = SelectedZap.IsZap
            Zap_Fix.Checked = SelectedZap.IsManoloFix And Not SelectedZap.IsZap
        End If
    End Sub

    Private Sub ListView2_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView2.SelectedIndexChanged
        Habiliza_Zap_Buttons()
    End Sub

    Private Sub ZapCreate_Click(sender As Object, e As EventArgs) Handles ZapCreate.Click
        Dim nombre = InputBox("Nombre del Zap", "Nuevo Zap", "")
        If nombre = "" Then Exit Sub
        If ListView2.Items.Find(nombre, False).Length > 0 Then
            MsgBox("El Zap ya existe", vbCritical, "Error")
            Exit Sub
        End If
        If nombre <> "" Then
            Dim el = Selected_Slider.Nodo.OwnerDocument.CreateElement("Slider")
            el.SetAttribute("name", nombre)
            el.SetAttribute("zap", "true")
            el.SetAttribute("invert", "false")
            el.SetAttribute("default", "100")
            Dim slid As New Slider_class(el, Selected_Slider)
            Selected_Slider.Nodo.AppendChild(el)
            Selected_Slider.Sliders.Add(slid)
            Lee_Zaps()
        End If

    End Sub

    Private Sub ZapLoad_Click(sender As Object, e As EventArgs) Handles ZapLoad.Click
        If ModifierKeys And Keys.Control Then
            Selected_Shape.MaskedVertices.ExceptWith(Get_Blocks_Verts)
        Else
            Selected_Shape.MaskedVertices.UnionWith(Get_Blocks_Verts)
        End If

        Process_render_Changes(False)
    End Sub

    Private Function Get_Blocks_Verts() As IEnumerable(Of Integer)
        Return SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape).SelectMany(Function(pf) pf.RelatedOSDBlocks).SelectMany(Function(pf) pf.DataDiff).Select(Function(pq) CType(pq.Index, Integer))
    End Function

    Private Sub ZapInclude_Click(sender As Object, e As EventArgs) Handles ZapInclude.Click
        Iniciado_Edit()
        Dim VertsToInclude As New HashSet(Of Integer)
        VertsToInclude.UnionWith(Selected_Shape.MaskedVertices)
        VertsToInclude.ExceptWith(Get_Blocks_Verts)
        Dim VertsToModify As New HashSet(Of Integer)
        VertsToModify.UnionWith(Selected_Shape.MaskedVertices)
        VertsToModify.ExceptWith(VertsToInclude)
        Dim dat As Slider_Data_class
        Dim block As OSD_Block_Class


        If Not SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Any Then
            Dim el = SelectedZap.Nodo.OwnerDocument.CreateElement("Data")
            el.SetAttribute("name", Selected_Shape.Target + SelectedZap.Nombre)
            el.SetAttribute("target", Selected_Shape.Target)
            el.SetAttribute("local", "true")
            el.InnerText = Path.GetFileName(Selected_Slider.OsdLocalFullPath(0)) + "\" + Selected_Shape.Target + SelectedZap.Nombre
            dat = New Slider_Data_class(el, SelectedZap)
            SelectedZap.Nodo.AppendChild(el)
            SelectedZap.Datas.Add(dat)
            Dim blockRoot = Selected_Slider.OSDContent_Local
            block = New OSD_Block_Class(blockRoot) With {.BlockName = Selected_Shape.Target + SelectedZap.Nombre}
            blockRoot.Blocks.Add(block)
        Else
            If SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Count > 1 Then Throw New Exception
            dat = SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).First
            block = SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Select(Function(pq) pq.RelatedLocalOSDBlocks).Select(Function(pf) pf).First.First
        End If

        If SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Select(Function(pq) pq.RelatedLocalOSDBlocks).Count > 1 Then Throw New Exception

        ' Inflate?
        Dim rawVerts = Array.Empty(Of Vector3)
        Dim Center As Vector3
        If CheckBoxInflate.Checked Then
            rawVerts = Selected_Shape.RelatedNifShape.VertexPositions.Select(Function(v) New Vector3(v.X, v.Y, v.Z)).ToArray
            Center = New Vector3(Selected_Shape.RelatedNifShape.BoundingVolume.Sphere.Center.X, Selected_Shape.RelatedNifShape.BoundingVolume.Sphere.Center.Y, Selected_Shape.RelatedNifShape.BoundingVolume.Sphere.Center.Z)
        End If


        ' Nuevos
        Dim Norm As Vector3
        Dim Diff As Vector3
        If SelectedZap.IsZap Then
            Diff = New Vector3(0, 1, 0)
        Else
            Diff = New Vector3(Fix_X.Value / 10, Fix_Y.Value / 10, Fix_Z.Value / 10)
        End If
        Dim vector = Diff
        For Each ver In VertsToInclude
            vector = Diff
            If SelectedZap.IsZap Then
                block.DataDiff.Add(New OSD_DataDiff_Class With {.Index = ver, .X = Diff.X, .Y = Diff.Y, .Z = Diff.Z})
            Else
                If CheckBoxInflate.Checked Then
                    Norm = Vector3.Normalize(rawVerts(ver) - Center)
                    vector = vector * Norm - vector * Norm / 2
                End If
                block.DataDiff.Add(New OSD_DataDiff_Class With {.Index = ver, .X = vector.X, .Y = vector.Y, .Z = vector.Z})
            End If
        Next

        ' Existentes
        For Each dd In block.DataDiff.ToList
            vector = Diff
            If VertsToModify.Contains(dd.Index) Then
                If SelectedZap.IsZap Then
                    dd.X = Diff.X
                    dd.Y = Diff.Y
                    dd.Z = Diff.Z
                Else
                    If IncrementalCheck.Checked Then
                        Dim old As New Vector3(dd.X, dd.Y, dd.Z)
                        If CheckBoxInflate.Checked Then
                            Norm = Vector3.Normalize(rawVerts(dd.Index) - Center)
                            vector = old + vector * Norm - vector * Norm / 2
                        Else
                            vector += old
                        End If
                    Else
                        If CheckBoxInflate.Checked Then
                            Norm = Vector3.Normalize(rawVerts(dd.Index) - Center)
                            vector = vector * Norm - vector * Norm / 2
                        End If
                    End If
                    dd.X = vector.X
                    dd.Y = vector.Y
                    dd.Z = vector.Z
                End If
            End If
        Next
        Process_render_Changes(False)
    End Sub

    Private Sub ZapExclude_Click(sender As Object, e As EventArgs) Handles ZapExclude.Click
        Iniciado_Edit()
        Dim VertsToExclude As New HashSet(Of Integer)
        VertsToExclude.UnionWith(Selected_Shape.MaskedVertices)
        VertsToExclude.IntersectWith(Get_Blocks_Verts)
        Select Case SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Select(Function(pq) pq.RelatedLocalOSDBlocks).Count
            Case 0
    ' Nada que limpiar
            Case 1
                Dim block = SelectedZap.Datas.Where(Function(pf) pf.RelatedShape Is Selected_Shape And pf.Islocal).Select(Function(pq) pq.RelatedLocalOSDBlocks).First
                For Each bl In block
                    For Each dd In bl.DataDiff.ToList
                        If VertsToExclude.Contains(dd.Index) Then
                            bl.DataDiff.Remove(dd)
                        End If
                    Next
                Next
            Case Else
                Throw New Exception
        End Select
        Process_render_Changes(False)
    End Sub

    Private Sub ZapInverted_CheckedChanged(sender As Object, e As EventArgs) Handles ZapInverted.CheckedChanged
        If IsNothing(SelectedZap) Then Exit Sub
        If SelectedZap.Invert <> ZapInverted.Checked Then Iniciado_Edit()
        SelectedZap.Invert = ZapInverted.Checked
        Process_render_Changes(False)
    End Sub
    Private Sub Cambia_Tipo_Zap()
        If IsNothing(SelectedZap) Then Exit Sub
        Dim tipo As String = "Zap"
        If SelectedZap.IsZap = True Then
            GroupBoxZaps.Enabled = False
        Else
            GroupBoxZaps.Enabled = True
            tipo = "Fix"
        End If
        ListView2.SelectedItems(0).SubItems(1).Text = tipo
        Process_render_Changes(False)
    End Sub
    Private Sub Zap_Fix_CheckedChanged(sender As Object, e As EventArgs) Handles Zap_Fix.CheckedChanged
        If IsNothing(SelectedZap) Then Exit Sub
        If SelectedZap.IsManoloFix <> Zap_Fix.Checked Then Iniciado_Edit()
        SelectedZap.IsManoloFix = Zap_Fix.Checked
        SelectedZap.IsZap = Not Zap_Fix.Checked
        Cambia_Tipo_Zap()
    End Sub

    Private Sub Zap_Zap_CheckedChanged(sender As Object, e As EventArgs) Handles Zap_Zap.CheckedChanged
        If IsNothing(SelectedZap) Then Exit Sub
        If SelectedZap.IsManoloFix <> Not Zap_Zap.Checked Then Iniciado_Edit()
        SelectedZap.IsManoloFix = Not Zap_Zap.Checked
        SelectedZap.IsZap = Zap_Zap.Checked
        Cambia_Tipo_Zap()
    End Sub

    Private Sub DeleteZap_Click(sender As Object, e As EventArgs) Handles DeleteZap.Click
        If MsgBox("Are you sure you want to delete this slider?", vbYesNo, "Warning") = MsgBoxResult.Yes Then
            Iniciado_Edit()

            For Each dat In SelectedZap.Datas.ToList
                For Each bl In dat.RelatedLocalOSDBlocks.ToList
                    bl.ParentOSDContent.Blocks.Remove(bl)
                Next
                SelectedZap.Nodo.RemoveChild(dat.Nodo)
                SelectedZap.Datas.Remove(dat)
            Next
            Selected_Slider.Nodo.RemoveChild(SelectedZap.Nodo)
            Selected_Slider.Sliders.Remove(SelectedZap)
            SelectedZap = Nothing
            Lee_Zaps()
        End If
        Habiliza_Zap_Buttons()
        Process_render_Changes(False)
    End Sub
    Private Function Get_Zap_Verts() As IEnumerable(Of Integer)
        Return SelectedZap.Datas.SelectMany(Function(pf) pf.RelatedOSDBlocks).SelectMany(Function(pf) pf.DataDiff).Select(Function(pq) CType(pq.Index, Integer))
    End Function
    Private Sub ButtonClearZap_Click(sender As Object, e As EventArgs) Handles ButtonClearZap.Click
        Iniciado_Edit()
        Dim VertsToExclude As New HashSet(Of Integer)
        VertsToExclude.UnionWith(Get_Zap_Verts)
        For Each block In SelectedZap.Datas.Where(Function(pf) pf.Islocal).Select(Function(pq) pq.RelatedLocalOSDBlocks)
            For Each bl In block
                For Each dd In bl.DataDiff.ToList
                    If VertsToExclude.Contains(dd.Index) Then
                        bl.DataDiff.Remove(dd)
                    End If
                Next
            Next
        Next
        Process_render_Changes(False)
    End Sub

    Private Sub ButtonCopyPath_Click(sender As Object, e As EventArgs) Handles ButtonCopyPath.Click
        If ComboBoxMaterials.SelectedIndex = -1 Then Exit Sub
        Dim path = (MaterialPathTextbox.Text + "\" + ComboBoxMaterials.SelectedItem.ToString).Correct_Path_Separator
        If path.StartsWith("Materials\", StringComparison.OrdinalIgnoreCase) Then path = path.Substring("Materials\".Length)
        Clipboard.SetText(path)
    End Sub

    Private Sub ComboBoxPresets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPresets.SelectedIndexChanged
        Actualiza_Preset()
        Process_render_Changes(False)
    End Sub

    Private Sub RadioButton2_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton2.CheckedChanged
        EditPreviewControl.InvertMasking = RadioButton2.Checked
    End Sub

    Private Sub RadioButton1_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton1.CheckedChanged
        EditPreviewControl.InvertMasking = Not RadioButton1.Checked
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles ButtonCancelPreset.Click
        Actualiza_Preset()
        Process_render_Changes(False)
    End Sub

    Private Sub Buttonsave_Click(sender As Object, e As EventArgs) Handles ButtonSavePreset.Click
        Dim nombre = ComboBoxPresets.SelectedItem.ToString
        Dim filename = FilesDictionary_class.SliderPresets.Presets(nombre).Filename
        If nombre <> "" Then
            SavePresetXml(filename, nombre, False)
        End If
    End Sub
    Private Function Size_to_str(Size As Config_App.SliderSize) As String
        Select Case Size
            Case Config_App.SliderSize.Big
                Return "big"
            Case Config_App.SliderSize.Default
                Return "small"
            Case Config_App.SliderSize.Small
                Return "default"
        End Select
        Return "big"
    End Function
    Public Function SavePresetXml(path As String, Nombre As String, delete As Boolean) As Boolean
        Try
            If delete = False Then
                If FilesDictionary_class.SliderPresets.Presets.ContainsKey(Nombre) Then
                    If FilesDictionary_class.SliderPresets.Presets(Nombre).Filename.Equals(path, StringComparison.OrdinalIgnoreCase) Then
                        If MsgBox("Preset " + Nombre + " already exist. Do you want to ovewrite?", vbYesNo, "Warning") = MsgBoxResult.No Then Return False
                    Else
                        MsgBox("Preset " + Nombre + " already exist in another file ", vbCritical, "Warning")
                        Return False
                    End If
                End If
            Else
                If MsgBox("Are you sure you want to delete Preset " + Nombre + "?", vbYesNo, "Warning") = MsgBoxResult.No Then Return False
            End If
            If IO.Directory.Exists(IO.Path.GetDirectoryName(path)) = False Then
                IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(path))
            End If
            If IO.File.Exists(path) = False Then
                Dim writer = IO.File.CreateText(path)
                writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")
                writer.WriteLine("<SliderPresets>")
                writer.WriteLine("</SliderPresets>")
                writer.Flush()
                writer.Close()
            End If

            Dim doc = XDocument.Load(path)
            Dim sel = doc.Root.Elements("Preset").Where(Function(pf) pf.Attribute("name").Value.Equals(Nombre, StringComparison.OrdinalIgnoreCase)).FirstOrDefault
            If IsNothing(sel) Then
                sel = New XElement("Preset", New XAttribute("name", Nombre), New XAttribute("set", "CBBE Body"))
                doc.Root.Add(sel)
            End If

            If delete Then
                sel.Remove()
            Else
                If CheckBoxIncGroups.Checked = False Then
                    For Each gr As XElement In sel.Elements("Group").ToList
                        gr.Remove()
                    Next
                End If

                For Each gr As XElement In sel.Elements("SetSlider").ToList
                    gr.Remove()
                Next
                For Each sli In Selected_Preset.Sliders
                    Dim nuevo As New XElement("SetSlider", New XAttribute("name", sli.Name), New XAttribute("size", Size_to_str(sli.Size)), New XAttribute("value", sli.Value.ToString(CultureInfo.InvariantCulture)))
                    Dim copi As Boolean = True
                    If sli.Category = nif_cat AndAlso CheckBoxIncNIF.Checked = False Then copi = False
                    If sli.Category = Slid_cat AndAlso CheckBoxIncSlid.Checked = False Then copi = False
                    If copi Then sel.Add(nuevo)
                Next
            End If

            Dim contar = doc.Root.Elements("Preset").Count
            doc.Save(path)


            If delete Then
                FilesDictionary_class.SliderPresets.Presets.Remove(Nombre)
                ComboBoxPresets.Items.Remove(Nombre)
                If ComboBoxPresets.Items.Count > 0 Then ComboBoxPresets.SelectedIndex = 0
            Else
                Dim cloned As SlidersPreset_Class = SliderPresetCollection.Clone(Selected_Preset, path, Nombre)
                If FilesDictionary_class.SliderPresets.Presets.TryAdd(Nombre, cloned) = False Then
                    FilesDictionary_class.SliderPresets.Presets(Nombre) = cloned
                Else
                    ComboBoxPresets.Items.Add(Nombre)
                End If
                ComboBoxPresets.SelectedIndex = ComboBoxPresets.FindString(Nombre)
            End If

            If delete AndAlso contar = 0 Then
                IO.File.Delete(path)
            End If

            Return True
        Catch ex As Exception
            MsgBox("Error writing Preset " + Nombre + " in file " + path, vbCritical, "Error")
            Return False
        End Try

    End Function
    Public Function SavePoseXML(path As String, Nombre As String, delete As Boolean, tipo As Poses_class.Pose_Source_Enum) As Boolean
        Try
            Dim Keyname = Poses_class.KeyName(Nombre, tipo)
            Dim KeynameBS = Poses_class.KeyName(Nombre, Poses_class.Pose_Source_Enum.BodySlide)
            Dim KeynameActual As String = Poses_class.KeyName(Nombre, tipo)

            If delete = False Then
                If FilesDictionary_class.SliderPresets.Poses.ContainsKey(Keyname) OrElse FilesDictionary_class.SliderPresets.Poses.ContainsKey(KeynameBS) Then
                    If FilesDictionary_class.SliderPresets.Poses(KeynameActual).Filename.Equals(path, StringComparison.OrdinalIgnoreCase) Then
                        If MsgBox("Pose " + Nombre + " already exist. Do you want to ovewrite?", vbYesNo, "Warning") = MsgBoxResult.No Then Return False
                    Else
                        MsgBox("Preset " + Nombre + " already exist in another file ", vbCritical, "Warning")
                        Return False
                    End If
                End If
            Else
                If MsgBox("Are you sure you want to delete pose " + Nombre + "?", vbYesNo, "Warning") = MsgBoxResult.No Then Return False
            End If

            If delete AndAlso tipo = Poses_class.Pose_Source_Enum.ScreenArcher Then
                If IO.File.Exists(path) Then
                    IO.File.Delete(path)
                End If
            End If
            Dim contar As Integer = 1
            If Not delete OrElse tipo <> Poses_class.Pose_Source_Enum.ScreenArcher Then
                If IO.Directory.Exists(IO.Path.GetDirectoryName(path)) = False Then
                    IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(path))
                End If

                If IO.File.Exists(path) = False Then
                    Dim writer = IO.File.CreateText(path)
                    writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")
                    writer.WriteLine("<PoseData>")
                    writer.WriteLine("</PoseData>")
                    writer.Flush()
                    writer.Close()
                End If

                Dim doc = XDocument.Load(path)
                Dim sel = doc.Root.Elements("Pose").Where(Function(pf) pf.Attribute("name").Value.Equals(Nombre, StringComparison.OrdinalIgnoreCase)).FirstOrDefault
                If IsNothing(sel) Then
                    sel = New XElement("Pose", New XAttribute("name", Nombre), New XAttribute("WMPose", "true"))
                    doc.Root.Add(sel)
                Else
                    If IsNothing(sel.Attribute("WMPose")) Then
                        Dim el As New XAttribute("WMPose", "true")
                        el = sel.Attributes.Append(el)
                    Else
                        sel.Attribute("WMPose").Value = "true"
                    End If
                End If

                If delete Then
                    sel.Remove()
                Else

                    For Each gr As XElement In sel.Elements("Bone").ToList
                        gr.Remove()
                    Next

                    For Each tr In Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False)
                        Dim nuevo As New XElement("Bone",
                                                  New XAttribute("name", tr.Key),
                                                  New XAttribute("rotX", tr.Value.Yaw.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("rotY", tr.Value.Pitch.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("rotZ", tr.Value.Roll.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("transX", tr.Value.X.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("transY", tr.Value.Y.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("transZ", tr.Value.Z.ToString(CultureInfo.InvariantCulture)),
                                                  New XAttribute("scale", tr.Value.Scale.ToString(CultureInfo.InvariantCulture))
                                                   )
                        sel.Add(nuevo)
                    Next
                End If
                contar = doc.Root.Elements("Pose").Count



                doc.Save(path)
            End If
            Dim SafExported As Boolean = False
            If Not delete AndAlso CheckBoxSaveSAF.Checked Then SafExported = ExportSaf(Nombre)

            If delete Or KeynameActual <> Keyname Then
                FilesDictionary_class.SliderPresets.Poses.Remove(KeynameActual)
                ComboBoxPoses.Items.Remove(KeynameActual)
                If delete Then If ComboBoxPoses.Items.Count > 0 Then ComboBoxPoses.SelectedIndex = 0
            End If

            If Not delete Then
                Dim cloned As Poses_class = Selected_Pose.Clone
                cloned.Filename = path
                cloned.Name = Nombre
                If FilesDictionary_class.SliderPresets.Poses.TryAdd(Keyname, cloned) = False Then
                    FilesDictionary_class.SliderPresets.Poses(Keyname) = cloned
                Else
                    ComboBoxPoses.Items.Add(Keyname)
                End If

                ComboBoxPoses.SelectedIndex = ComboBoxPoses.FindString(Keyname)
            End If
            If delete AndAlso contar = 0 Then
                IO.File.Delete(path)
            End If
            Return True
        Catch ex As Exception
            If Not delete Then
                MsgBox("Error writing pose " + Nombre + " in file " + path, vbCritical, "Error")
            Else
                MsgBox("Error deleting pose " + Nombre + " in file " + path, vbCritical, "Error")
            End If
            Return False
        End Try

    End Function

    Private opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True, .NumberHandling = JsonNumberHandling.AllowReadingFromString, .WriteIndented = True}

    Private Function ExportSaf(Nombre As String) As Boolean
        If Skeleton_Class.HasSkeleton = False Then Return False
        Try
            Dim Export As New Poses_class With {
                .Filename = IO.Path.Combine(IO.Path.Combine(Wardrobe_Manager_Form.Directorios.PosesSAMRoot), Nombre + ".json"),
                .Source = Poses_class.Pose_Source_Enum.ScreenArcher,
                .Version = 2,
                .Skeleton = "Vanilla",
                .Transforms = New Dictionary(Of String, PoseTransformData),
                .Name = Nombre
            }
            For Each sk In Skeleton_Class.SkeletonDictionary
                Dim tr = sk.Value.LocaLTransform
                Dim nuevo As New PoseTransformData With {
                    .X = tr.Translation.X,
                    .Y = tr.Translation.Y,
                    .Z = tr.Translation.Z,
                    .Scale = tr.Scale
                }
                Dim degs = Transform_Class.Matrix33ToEulerXYZ(tr.Rotation)
                nuevo.Yaw = degs.X
                nuevo.Pitch = degs.Y
                nuevo.Roll = degs.Z
                Export.Transforms.Add(sk.Key, nuevo)
            Next

            If IO.Directory.Exists(Wardrobe_Manager_Form.Directorios.PosesSAMRoot) = False Then
                IO.Directory.CreateDirectory(Wardrobe_Manager_Form.Directorios.PosesSAMRoot)
            End If
            Dim jsonOut As String = JsonSerializer.Serialize(Of Poses_class)(Export, opts)

            ' 2) Escribir el JSON en disco (sobrescribe o crea el archivo en xmlpath)
            IO.File.WriteAllText(Export.Filename, jsonOut)
            Dim Keyname = Poses_class.KeyName(Nombre, Poses_class.Pose_Source_Enum.ScreenArcher)
            If FilesDictionary_class.SliderPresets.Poses.TryAdd(Keyname, Export) = False Then
                FilesDictionary_class.SliderPresets.Poses(Keyname) = Export
            Else
                ComboBoxPoses.Items.Add(Keyname)
            End If
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles ButtonSaveAsPreset.Click
        Dim nombre = InputBox("Preset Name", "Preset", "")
        Dim filename = Path.Combine(Wardrobe_Manager_Form.Directorios.SliderPresetsRoot, "ManoloPresets.xml")
        If nombre <> "" Then
            SavePresetXml(filename, nombre, False)
        End If
    End Sub

    Private Sub Button10_Click(sender As Object, e As EventArgs) Handles ButtondelPreset.Click
        Dim nombre = ComboBoxPresets.SelectedItem.ToString
        Dim filename = FilesDictionary_class.SliderPresets.Presets(nombre).Filename
        If nombre <> "" Then
            SavePresetXml(filename, nombre, True)
        End If
    End Sub

    Private Sub CheckBoxShowVColors_CheckedChanged(sender As Object, e As EventArgs) Handles RenderCheckVertexColors.CheckedChanged
        Selected_Shape.ShowVertexColor = RenderCheckVertexColors.Checked
        Render_Changes(False)
    End Sub

    Private Sub OutFilTextbox_TextChanged_1(sender As Object, e As EventArgs) Handles OutFilTextbox.TextChanged

    End Sub

    Private Sub OutDirTextbox_TextChanged_1(sender As Object, e As EventArgs) Handles OutDirTextbox.TextChanged

    End Sub

    Private Sub SingleBoneCheck_CheckedChanged(sender As Object, e As EventArgs) Handles SingleBoneCheck.CheckedChanged
        EditPreviewControl.Model.SingleBoneSkinning = SingleBoneCheck.Checked
        ComboBoxPoses.Enabled = Not SingleBoneCheck.Checked
        Render_Changes(True)
    End Sub

    Private Sub ButtonMakeGradient_Click(sender As Object, e As EventArgs) Handles ButtonMakeGradient.Click
        If IsNothing(Selected_Material) Then Exit Sub
        Selected_Material.GrayscaleToPaletteColor = True
        Selected_Material.GreyscaleTexture = "ManoloCloned\ManoloShared\gradient_inverse.dds"
        Selected_Material.GrayscaleToPaletteScale = 0.5
        Update_Grayscale()
        Iniciado_Edit()
        Render_Changes(False)
    End Sub

    Private Sub ColorComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ColorComboBox1.SelectedIndexChanged
        If IsNothing(Selected_Shape) Then Exit Sub
        Selected_Shape.Wirecolor = ColorComboBox1.SelectedColor
        Render_Changes(False)
    End Sub

    Private Sub Button8_Click_1(sender As Object, e As EventArgs) Handles ButtonGrowMask.Click
        AddNeighborVerticesOnce(Selected_Shape.MaskedVertices, Selected_Shape.RelatedNifShape.Triangles)
        Render_Changes(False)
    End Sub

    Public Shared Sub AddNeighborVerticesOnce(marked As HashSet(Of Integer), triangles As List(Of NiflySharp.Structs.Triangle))
        If marked.Count = 0 Then Exit Sub
        Dim original = New HashSet(Of Integer)
        original.UnionWith(marked)
        For Each tri As NiflySharp.Structs.Triangle In triangles
            ' si alguno de los tres ya está marcado...
            If original.Contains(tri(0)) OrElse original.Contains(tri(1)) OrElse original.Contains(tri(2)) Then
                ' ...añadimos los tres al HashSet
                marked.Add(tri(0))
                marked.Add(tri(1))
                marked.Add(tri(2))
            End If
        Next

    End Sub
    Sub RemoveNeighborVerticesOnce(marked As HashSet(Of Integer), triangles As List(Of NiflySharp.Structs.Triangle))
        If marked.Count = Selected_Shape.RelatedNifShape.VertexPositions.Count Then Exit Sub
        Dim original = New HashSet(Of Integer)
        original.UnionWith(marked)
        For Each tri As NiflySharp.Structs.Triangle In triangles
            ' si alguno de los tres ya está marcado...
            If Not original.Contains(tri(0)) OrElse Not original.Contains(tri(1)) OrElse Not original.Contains(tri(2)) Then
                ' ...añadimos los tres al HashSet
                marked.Remove(tri(0))
                marked.Remove(tri(1))
                marked.Remove(tri(2))
            End If
        Next

    End Sub

    Private Sub NumericMaskRadius_ValueChanged(sender As Object, e As EventArgs) Handles NumericMaskRadius.ValueChanged
        EditPreviewControl.BrushRadiusPx = NumericMaskRadius.Value
    End Sub

    Private Sub Button8_Click_2(sender As Object, e As EventArgs) Handles Button8.Click
        RemoveNeighborVerticesOnce(Selected_Shape.MaskedVertices, Selected_Shape.RelatedNifShape.Triangles)
        Render_Changes(False)
    End Sub


    Private Sub TrackBar1_Scroll(sender As Object, e As EventArgs) Handles TrackBar1.Scroll
        If IsNothing(Selected_Shape) Then Exit Sub
        Selected_Shape.WireAlpha = TrackBar1.Value / 100
        Render_Changes(False)
    End Sub

    Private Sub Button9_Click(sender As Object, e As EventArgs) Handles ButonMatBackToOriginal.Click
        Dim prefix = "materials\"
        Dim combinado = Path.Combine(MaterialPathTextbox.Text.Replace("ManoloCloned\", "", StringComparison.OrdinalIgnoreCase), ComboBoxMaterials.SelectedItem)
        Dim fullpath = combinado.Correct_Path_Separator
        If FilesDictionary_class.Dictionary.ContainsKey(fullpath) = False Then
            MsgBox("Original material not found in files.", vbCritical, "Error")
            Exit Sub
        End If
        If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
        Selected_Shape.RelatedMaterial.path = fullpath
        Lee_Materials()

        Lee_Comboselected_Material(combinado.Correct_Path_Separator)
    End Sub

    Private Sub Editor_Form_Load(sender As Object, e As EventArgs) Handles Me.Load
        ColorComboBox1.Rellena()
        If Skeleton_Class.HasSkeleton = False Then GroupBox9.Enabled = False
    End Sub

    Private Const trackbarscale As Integer = 100000
    Private Const RotationConversion As Integer = (180 / Math.PI)
    Private Sub TreeViewSkeleton_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles TreeViewSkeleton.AfterSelect
        Update_Bone_Sliders()
    End Sub
    Dim pose_is_Edited As Boolean = False
    Private Sub Update_Bone_Sliders()
        _Prevent_Changes = True
        Dim cual = TreeViewSkeleton.SelectedNode
        If IsNothing(cual) Then GroupBox10.Enabled = False Else GroupBox10.Enabled = True
        Dim Bone As Skeleton_Class.HierarchiBone_class = Nothing
        If Not IsNothing(cual) Then
            Bone = Skeleton_Class.SkeletonDictionary(cual.Text)
        End If


        If IsNothing(Bone) Then
            Selected_Pose_Transform = Nothing
        Else
            If Selected_Pose.Transforms.TryGetValue(Bone.BoneName, Selected_Pose_Transform) = False Then
                Dim nuevo As New PoseTransformData
                Selected_Pose.Transforms.Add(Bone.BoneName, nuevo)
                Selected_Pose_Transform = nuevo
            End If
        End If

        If IsNothing(Bone) OrElse IsNothing(Bone.DeltaTransform) Then
            Dim MaxT As Double = 100
            Dim MaxRot As Double = 180
            Dim MaxScale As Double = 100
            Update_Trackbar_max(MaxT, MaxRot, MaxScale)
            UPdate_Labels_Poses()
        Else
            Dim degs = Transform_Class.Matrix33ToBSRotation(Bone.DeltaTransform.Rotation)
            Dim MaxT As Double = 100
            Dim MaxRot As Double = 180
            Dim MaxScale As Double = 100

            If Math.Abs(Bone.DeltaTransform.Translation.X) > MaxT Then MaxT = Math.Abs(Bone.DeltaTransform.Translation.X)
            If Math.Abs(Bone.DeltaTransform.Translation.Y) > MaxT Then MaxT = Math.Abs(Bone.DeltaTransform.Translation.Y)
            If Math.Abs(Bone.DeltaTransform.Translation.Z) > MaxT Then MaxT = Math.Abs(Bone.DeltaTransform.Translation.Z)
            If Math.Abs(degs.X * RotationConversion) > MaxRot Then MaxRot = Math.Abs(degs.X * RotationConversion)
            If Math.Abs(degs.Y * RotationConversion) > MaxRot Then MaxRot = Math.Abs(degs.Y * RotationConversion)
            If Math.Abs(degs.Z * RotationConversion) > MaxRot Then MaxRot = Math.Abs(degs.Z * RotationConversion)
            If Math.Abs(Bone.DeltaTransform.Scale * 10) > MaxScale Then MaxScale = Math.Abs(Bone.DeltaTransform.Scale * 10)

            Update_Trackbar_max(MaxT, MaxRot, MaxScale)
            TrackBar2.Value = Bone.DeltaTransform.Translation.X * trackbarscale
            TrackBar3.Value = Bone.DeltaTransform.Translation.Y * trackbarscale
            TrackBar4.Value = Bone.DeltaTransform.Translation.Z * trackbarscale
            TrackBar5.Value = degs.X * RotationConversion * trackbarscale
            TrackBar6.Value = degs.Y * RotationConversion * trackbarscale
            TrackBar7.Value = degs.Z * RotationConversion * trackbarscale
            TrackBar8.Value = Bone.DeltaTransform.Scale * 10 * trackbarscale
            UPdate_Labels_Poses()
        End If
        _Prevent_Changes = False
    End Sub


    Private Sub UPdate_Labels_Poses()
        If IsNothing(Selected_Pose_Transform) Then
            NumericUpDownRotX.Value = 0
            NumericUpDownRotY.Value = 0
            NumericUpDownRotZ.Value = 0
            NumericUpDownTrasX.Value = 0
            NumericUpDownTrasY.Value = 0
            NumericUpDownTrasZ.Value = 0
            NumericUpDownScale.Value = 1
        Else
            Dim converter = New Transform_Class(Selected_Pose_Transform, Poses_class.Pose_Source_Enum.WardrobeManager)
            Dim degrees = Transform_Class.Matrix33ToBSRotation(converter.Rotation)
            NumericUpDownRotX.Value = Math.Min(Math.Max(NumericUpDownRotX.Minimum, degrees.X * 180 / Math.PI), NumericUpDownRotX.Maximum)
            NumericUpDownRotY.Value = Math.Min(Math.Max(NumericUpDownRotY.Minimum, degrees.Y * 180 / Math.PI), NumericUpDownRotY.Maximum)
            NumericUpDownRotZ.Value = Math.Min(Math.Max(NumericUpDownRotZ.Minimum, degrees.Z * 180 / Math.PI), NumericUpDownRotZ.Maximum)
            NumericUpDownTrasX.Value = Math.Min(Math.Max(NumericUpDownTrasX.Minimum, converter.Translation.X), NumericUpDownTrasX.Maximum)
            NumericUpDownTrasY.Value = Math.Min(Math.Max(NumericUpDownTrasY.Minimum, converter.Translation.Y), NumericUpDownTrasY.Maximum)
            NumericUpDownTrasZ.Value = Math.Min(Math.Max(NumericUpDownTrasZ.Minimum, converter.Translation.Z), NumericUpDownTrasZ.Maximum)
            NumericUpDownScale.Value = Math.Min(Math.Max(NumericUpDownScale.Minimum, converter.Scale), NumericUpDownScale.Maximum)
        End If
        ButtonClearBoneTransform.Enabled = Not IsNothing(Selected_Pose_Transform) AndAlso Selected_Pose_Transform.Isidentity = False
        ButtonReloadBonePose.Enabled = Not IsNothing(ComboSelected_Pose) And pose_is_Edited
        ButtonClearPoseTransforms.Enabled = Not IsNothing(Selected_Pose) AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        ButtonReloadPose.Enabled = Not IsNothing(ComboSelected_Pose) And pose_is_Edited

        PoseSaveAsButton.Enabled = Not IsNothing(Selected_Pose)
        PoseSaveButton.Enabled = Not IsNothing(ComboSelected_Pose) AndAlso (ComboSelected_Pose.Source = Poses_class.Pose_Source_Enum.BodySlide Or ComboSelected_Pose.Source = Poses_class.Pose_Source_Enum.WardrobeManager) AndAlso Not IsNothing(Selected_Pose) AndAlso pose_is_Edited AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        PoseBakeButton.Enabled = Not SingleBoneCheck.Checked AndAlso Not IsNothing(Selected_Pose) AndAlso Not IsNothing(ComboSelected_Pose) AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        PoseUnBakeButton.Enabled = Not SingleBoneCheck.Checked AndAlso Not IsNothing(Selected_Pose) AndAlso Not IsNothing(ComboSelected_Pose) AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        PoseBakeShapeButton.Enabled = Not SingleBoneCheck.Checked AndAlso Not IsNothing(Selected_Pose) AndAlso Not IsNothing(ComboSelected_Pose) AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        PoseUnBakeShapeButton.Enabled = Not SingleBoneCheck.Checked AndAlso Not IsNothing(Selected_Pose) AndAlso Not IsNothing(ComboSelected_Pose) AndAlso Selected_Pose.Transforms.Where(Function(pf) pf.Value.Isidentity = False).Any
        PoseDeleteButton.Enabled = Not IsNothing(ComboSelected_Pose) AndAlso ComboSelected_Pose.Source <> Poses_class.Pose_Source_Enum.None

    End Sub





    Private Sub Update_Trackbar_max(MaxT As Double, MaxRot As Double, MaxScale As Double)
        If MaxRot * trackbarscale > Integer.MaxValue Then Debugger.Break()
        TrackBar2.Value = 0 * trackbarscale
        TrackBar3.Value = 0 * trackbarscale
        TrackBar4.Value = 0 * trackbarscale
        TrackBar5.Value = 0 * trackbarscale
        TrackBar6.Value = 0 * trackbarscale
        TrackBar7.Value = 0 * trackbarscale
        TrackBar8.Value = Math.Max(1, TrackBar8.Minimum)

        NumericUpDownRotX.Value = 0
        NumericUpDownRotY.Value = 0
        NumericUpDownRotZ.Value = 0
        NumericUpDownTrasX.Value = 0
        NumericUpDownTrasY.Value = 0
        NumericUpDownTrasZ.Value = 0
        NumericUpDownScale.Value = Math.Max(1, NumericUpDownScale.Minimum)



        TrackBar2.Maximum = MaxT * trackbarscale
        TrackBar3.Maximum = MaxT * trackbarscale
        TrackBar4.Maximum = MaxT * trackbarscale
        TrackBar5.Maximum = MaxRot * trackbarscale
        TrackBar6.Maximum = MaxRot * trackbarscale
        TrackBar7.Maximum = MaxRot * trackbarscale
        TrackBar8.Maximum = MaxScale * trackbarscale
        TrackBar2.Minimum = -MaxT * trackbarscale
        TrackBar3.Minimum = -MaxT * trackbarscale
        TrackBar4.Minimum = -MaxT * trackbarscale
        TrackBar5.Minimum = -MaxRot * trackbarscale
        TrackBar6.Minimum = -MaxRot * trackbarscale
        TrackBar7.Minimum = -MaxRot * trackbarscale

        TrackBar8.Minimum = 1 * trackbarscale

        TrackBar8.Value = 10 * trackbarscale


        NumericUpDownRotX.Maximum = MaxRot
        NumericUpDownRotY.Maximum = MaxRot
        NumericUpDownRotZ.Maximum = MaxRot
        NumericUpDownTrasX.Maximum = MaxT
        NumericUpDownTrasY.Maximum = MaxT
        NumericUpDownTrasZ.Maximum = MaxT
        NumericUpDownRotX.Minimum = -MaxRot
        NumericUpDownRotY.Minimum = -MaxRot
        NumericUpDownRotZ.Minimum = -MaxRot
        NumericUpDownTrasX.Minimum = -MaxT
        NumericUpDownTrasY.Minimum = -MaxT
        NumericUpDownTrasZ.Minimum = -MaxT
        NumericUpDownScale.Maximum = MaxScale
        NumericUpDownScale.Minimum = 0.1
        NumericUpDownScale.Value = 1


        TrackBar2.SmallChange = trackbarscale / 100
        TrackBar3.SmallChange = trackbarscale / 100
        TrackBar4.SmallChange = trackbarscale / 100
        TrackBar5.SmallChange = trackbarscale / 100
        TrackBar6.SmallChange = trackbarscale / 100
        TrackBar7.SmallChange = trackbarscale / 100
        TrackBar8.SmallChange = trackbarscale / 1000

        TrackBar2.LargeChange = trackbarscale
        TrackBar3.LargeChange = trackbarscale
        TrackBar4.LargeChange = trackbarscale
        TrackBar5.LargeChange = trackbarscale
        TrackBar6.LargeChange = trackbarscale
        TrackBar7.LargeChange = trackbarscale
        TrackBar8.LargeChange = trackbarscale / 10

        NumericUpDownRotX.Increment = 0.1
        NumericUpDownRotY.Increment = 0.1
        NumericUpDownRotZ.Increment = 0.1
        NumericUpDownTrasX.Increment = 1
        NumericUpDownTrasY.Increment = 1
        NumericUpDownTrasZ.Increment = 1
        NumericUpDownScale.Increment = 0.05

    End Sub

    Private Sub ComboBoxPoses_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPoses.SelectedIndexChanged
        If ComboBoxPoses.SelectedIndex = -1 Then
            Selected_Pose = Nothing
            ComboSelected_Pose = Nothing
        Else
            ComboSelected_Pose = FilesDictionary_class.SliderPresets.Poses(ComboBoxPoses.SelectedItem.ToString)
            Selected_Pose = ComboSelected_Pose.Clone
        End If
        Selected_Pose_Transform = Nothing
        Render_Pose_Change(False)
    End Sub

    Private Sub CheckBox1_CheckedChanged_1(sender As Object, e As EventArgs) Handles RecalculateNormalsCheck.CheckedChanged
        EditPreviewControl.Model.RecalculateNormals = RecalculateNormalsCheck.Checked
        Render_Changes(True)
    End Sub
    Private WithEvents ScrollTimer As New Timer() With {.Interval = 500, .Enabled = False}
    Private pendingValue As Boolean = False

    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        CheckBoxSaveSAF.Checked = Config_App.Current.Setting_ExportSam
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Private Sub ScrollTimer_Tick(sender As Object, e As EventArgs) Handles ScrollTimer.Tick
        If pendingValue Then
            Update_Pose_Changes_FromTrackbar()
            pendingValue = False
        End If
    End Sub
    Private Sub TrackBar2_Scroll(sender As Object, e As EventArgs) Handles TrackBar2.Scroll, TrackBar3.Scroll, TrackBar4.Scroll, TrackBar5.Scroll, TrackBar6.Scroll, TrackBar7.Scroll, TrackBar8.Scroll
        pendingValue = True
        If Not ScrollTimer.Enabled Then
            ScrollTimer.Start()
        End If
    End Sub

    Private Sub TrackBar2_Ended(sender As Object, e As EventArgs) Handles TrackBar2.MouseUp, TrackBar3.MouseUp, TrackBar4.MouseUp, TrackBar5.MouseUp, TrackBar6.MouseUp, TrackBar7.MouseUp, TrackBar8.MouseUp
        If pendingValue Then
            Update_Pose_Changes_FromTrackbar()
            pendingValue = False
        End If
        ScrollTimer.Stop()
    End Sub
    Private Sub Update_Pose_Changes_FromTrackbar()
        If IsNothing(Selected_Pose_Transform) Then Exit Sub
        Selected_Pose_Transform.X = TrackBar2.Value / trackbarscale
        Selected_Pose_Transform.Y = TrackBar3.Value / trackbarscale
        Selected_Pose_Transform.Z = TrackBar4.Value / trackbarscale
        Selected_Pose_Transform.Yaw = (TrackBar5.Value / trackbarscale) / RotationConversion
        Selected_Pose_Transform.Pitch = (TrackBar6.Value / trackbarscale) / RotationConversion
        Selected_Pose_Transform.Roll = (TrackBar7.Value / trackbarscale) / RotationConversion
        Selected_Pose_Transform.Scale = TrackBar8.Value / (10 * trackbarscale)
        Render_Pose_Change(True)
    End Sub



    Private Sub Button7_Click_1(sender As Object, e As EventArgs) Handles ButtonReloadPose.Click
        If IsNothing(ComboSelected_Pose) Then
            Selected_Pose = Nothing
        Else
            Selected_Pose = ComboSelected_Pose.Clone
        End If
        Selected_Pose_Transform = Nothing
        Render_Pose_Change(False)
    End Sub

    Private Sub Button15_Click(sender As Object, e As EventArgs) Handles ButtonClearPoseTransforms.Click
        If IsNothing(Selected_Pose) Then
            Selected_Pose = Nothing
        Else
            Selected_Pose.Transforms.Clear()
        End If
        Selected_Pose_Transform = Nothing
        Render_Pose_Change(Nothing)
    End Sub

    Private Sub Button14_Click(sender As Object, e As EventArgs) Handles ButtonClearBoneTransform.Click
        If Not IsNothing(Selected_Pose_Transform) Then
            Selected_Pose_Transform.Roll = 0
            Selected_Pose_Transform.Pitch = 0
            Selected_Pose_Transform.Yaw = 0
            Selected_Pose_Transform.X = 0
            Selected_Pose_Transform.Y = 0
            Selected_Pose_Transform.Z = 0
            Selected_Pose_Transform.Scale = 1
            Render_Pose_Change(True)
        End If

    End Sub

    Private Sub Button7_Click_2(sender As Object, e As EventArgs) Handles Button7.Click
        Dim combinado = OutDirTextbox.Text.Replace("ManoloCloned\", "", StringComparison.OrdinalIgnoreCase)
        Dim fullpath = combinado.Correct_Path_Separator
        OutDirTextbox.Text = fullpath
        If Selected_Slider.OutputPathValue <> OutDirTextbox.Text Then Iniciado_Edit()
        Selected_Slider.OutputPathValue = OutDirTextbox.Text
        Button7.Enabled = False
    End Sub
    Public Function ComputeInversePoseTransforms() As Dictionary(Of String, Transform_Class)

        Dim invLocals As New Dictionary(Of String, Transform_Class)
        If Not Skeleton_Class.HasSkeleton OrElse IsNothing(Selected_Pose) Then
            Return invLocals
        End If

        For Each kvp In Selected_Pose.Transforms
            Dim boneName As String = kvp.Key

            ' 1) Matriz GLOBAL de la pose actual
            Dim Gpose As Matrix4d =
            Skeleton_Class.
                SkeletonDictionary(boneName).
                GetGlobalTransform().ToMatrix4d()

            ' 2) Matriz GLOBAL del bind-pose original
            Dim Gbind As Matrix4d =
            Skeleton_Class.
                SkeletonDictionary(boneName).
                OriginalGetGlobalTransform().ToMatrix4d()

            ' 3) Matriz GLOBAL del padre en la pose actual
            Dim parentBone = Skeleton_Class.
                             SkeletonDictionary(boneName).Parent
            Dim GposeParent As Matrix4d = Matrix4d.Identity
            If parentBone IsNot Nothing Then
                GposeParent = Skeleton_Class.SkeletonDictionary(boneName).Parent.GetGlobalTransform().ToMatrix4d()
            End If

            ' 4) Pasa Gbind a coordenadas locales del hueso:
            '    inv(GposeParent) * Gbind
            Dim invGparent As Matrix4d = GposeParent
            invGparent.Invert()
            Dim Linv As Matrix4d = invGparent * Gbind

            ' 5) Crea el Transform_Class local inverso
            Dim tc As New Transform_Class(Linv)

            invLocals(boneName) = tc
        Next

        Return invLocals
    End Function
    Private Sub Button9_Click_1(sender As Object, e As EventArgs) Handles PoseUnBakeButton.Click
        If MsgBox("Are you sure you want to un-bake the pose in the mesh. This will modify the nif vertex positions", vbYesNo, "Warning") = MsgBoxResult.Yes Then
            EditPreviewControl.Model.BakeOrInvertPose(True)
            Process_render_Changes(True)
        End If

    End Sub
    Private Sub Render_Pose_Change(Edited As Boolean?)
        If Not IsNothing(Edited) Then pose_is_Edited = Edited
        Skeleton_Class.AppplyPoseToSkeleton(Selected_Pose)
        Update_Bone_Sliders()
        Process_render_Changes(True)
    End Sub
    Private Sub Process_render_Changes(Force As Boolean)
        EditPreviewControl.Update_Render(Selected_Slider, Force, Selected_Preset, Selected_Pose, ComboBoxSize.SelectedIndex)
    End Sub

    Private Sub PoseBakeButton_Click(sender As Object, e As EventArgs) Handles PoseBakeButton.Click
        If MsgBox("Are you sure you want to bake the pose in the mesh. This will modify the nif vertex positions", vbYesNo, "Warning") = MsgBoxResult.Yes Then
            EditPreviewControl.Model.BakeOrInvertPose(False)
            Process_render_Changes(True)
            Dim None_key = FilesDictionary_class.SliderPresets.Poses.Where(Function(pf) pf.Value.Source = Poses_class.Pose_Source_Enum.None).First.Key
            ComboBoxPoses.SelectedIndex = ComboBoxPoses.Items.IndexOf(None_key)
        End If
    End Sub

    Private Sub PoseBakeShapeButton_Click(sender As Object, e As EventArgs) Handles PoseBakeShapeButton.Click
        If MsgBox("Are you sure you want to bake the pose in the shape. This will modify the shape vertex positions", vbYesNo, "Warning") = MsgBoxResult.Yes Then
            EditPreviewControl.Model.BakeOrInvertPose(Selected_Shape, False)
            Process_render_Changes(True)
            Dim None_key = FilesDictionary_class.SliderPresets.Poses.Where(Function(pf) pf.Value.Source = Poses_class.Pose_Source_Enum.None).First.Key
            ComboBoxPoses.SelectedIndex = ComboBoxPoses.Items.IndexOf(None_key)
        End If
    End Sub

    Private Sub PoseUnBakeShapeButton_Click(sender As Object, e As EventArgs) Handles PoseUnBakeShapeButton.Click
        If MsgBox("Are you sure you want to un-bake the pose in the shape. This will modify the shape vertex positions", vbYesNo, "Warning") = MsgBoxResult.Yes Then
            EditPreviewControl.Model.BakeOrInvertPose(Selected_Shape, True)
            Process_render_Changes(True)
        End If

    End Sub

    Private Sub PoseDeleteButton_Click(sender As Object, e As EventArgs) Handles PoseDeleteButton.Click
        If IsNothing(ComboSelected_Pose) Then Exit Sub
        Dim nombre = ComboSelected_Pose.Name
        Dim filename = ComboSelected_Pose.Filename
        Dim Tipo = ComboSelected_Pose.Source
        If nombre <> "" Then
            SavePoseXML(filename, nombre, True, Tipo)
        End If
    End Sub

    Private Sub PoseSaveButton_Click(sender As Object, e As EventArgs) Handles PoseSaveButton.Click
        If IsNothing(ComboSelected_Pose) Then Exit Sub
        Dim nombre = ComboSelected_Pose.Name
        Dim filename = ComboSelected_Pose.Filename
        Dim Tipo = Poses_class.Pose_Source_Enum.WardrobeManager
        If nombre <> "" Then
            SavePoseXML(filename, nombre, False, Tipo)
        End If
    End Sub

    Private Sub PoseSaveAsButton_Click(sender As Object, e As EventArgs) Handles PoseSaveAsButton.Click
        Dim nombre = InputBox("Pose name ", "Pose_Name", "")
        Dim filename = Path.Combine(Wardrobe_Manager_Form.Directorios.PosesBSRoot, "WardrobeManagerPoses.xml")
        Dim Tipo = Poses_class.Pose_Source_Enum.WardrobeManager
        If nombre <> "" Then
            SavePoseXML(filename, nombre, False, Tipo)
        End If
    End Sub

    Private Sub ButtonReloadBonePose_Click(sender As Object, e As EventArgs) Handles ButtonReloadBonePose.Click
        If Not IsNothing(Selected_Pose_Transform) Then
            If Not IsNothing(ComboSelected_Pose) Then
                Dim cloned = ComboSelected_Pose.Clone
                Dim key As String
                key = TreeViewSkeleton.SelectedNode?.Text
                If key <> "" Then
                    Dim TR As New Transform_Class(cloned.Transforms(key), cloned.Source)
                    Dim degs = Transform_Class.Matrix33ToBSRotation(TR.Rotation)
                    Selected_Pose_Transform.Roll = degs.Z
                    Selected_Pose_Transform.Pitch = degs.Y
                    Selected_Pose_Transform.Yaw = degs.X
                    Selected_Pose_Transform.X = TR.Translation.X
                    Selected_Pose_Transform.Y = TR.Translation.Y
                    Selected_Pose_Transform.Z = TR.Translation.Z
                    Selected_Pose_Transform.Scale = TR.Scale
                    Render_Pose_Change(True)
                End If
            End If
        End If
    End Sub

    Private Sub CheckBoxSaveSAF_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxSaveSAF.CheckedChanged
        If IsNothing(Selected_Slider) Then Exit Sub
        Config_App.Current.Setting_ExportSam = CheckBoxSaveSAF.Checked

    End Sub

    Private Sub CheckBoxZappedShapes_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxZappedShapes.CheckedChanged
        If IsNothing(Selected_Slider) Then Exit Sub
        Selected_Slider.KeepZappedShapes = CheckBoxZappedShapes.Checked
        Iniciado_Edit()
    End Sub

    Private Sub Button9_Click_2(sender As Object, e As EventArgs) Handles Button9.Click
        EditPreviewControl.ResetCamera(True)
    End Sub

    Private Sub CheckBoxPreventMorph_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxPreventMorph.CheckedChanged
        If IsNothing(Selected_Slider) Then Exit Sub
        Selected_Slider.PreventMorphFile = CheckBoxPreventMorph.Checked
        Iniciado_Edit()
    End Sub

    Private Sub TextBox2_TextChanged(sender As Object, e As EventArgs) Handles TextBox2.TextChanged
        If Not IsNothing(Selected_Slider) Then
            Selected_Slider.DescriptionValue = TextBox2.Text
        End If
    End Sub

    Private Sub LabelOScale_Click(sender As Object, e As EventArgs)

    End Sub
    Private _Prevent_Changes As Boolean = True
    Private Sub NumericUpDownTrasX_ValueChanged(sender As Object, e As EventArgs) Handles NumericUpDownTrasX.ValueChanged, NumericUpDownTrasY.ValueChanged, NumericUpDownTrasZ.ValueChanged, NumericUpDownRotX.ValueChanged, NumericUpDownRotY.ValueChanged, NumericUpDownRotZ.ValueChanged, NumericUpDownScale.ValueChanged
        If IsNothing(Selected_Pose_Transform) Or _Prevent_Changes = True Then Exit Sub
        Dim Tr = New PoseTransformData With {
        .Scale = NumericUpDownScale.Value,
        .X = NumericUpDownTrasX.Value,
        .Y = NumericUpDownTrasY.Value,
        .Z = NumericUpDownTrasZ.Value,
        .Yaw = NumericUpDownRotX.Value,
        .Pitch = NumericUpDownRotY.Value,
        .Roll = NumericUpDownRotZ.Value
        }
        If Selected_Pose_Transform.X <> Tr.X OrElse Selected_Pose_Transform.Y <> Tr.Y OrElse Selected_Pose_Transform.Z <> Tr.Z OrElse Selected_Pose_Transform.Yaw <> Tr.Yaw OrElse Selected_Pose_Transform.Pitch <> Tr.Pitch OrElse Selected_Pose_Transform.Roll <> Tr.Roll OrElse Selected_Pose_Transform.Scale <> Tr.Scale Then
            TrackBar7.Value = Tr.Roll * trackbarscale
            TrackBar6.Value = Tr.Pitch * trackbarscale
            TrackBar5.Value = Tr.Yaw * trackbarscale
            TrackBar2.Value = Tr.X * trackbarscale
            TrackBar3.Value = Tr.Y * trackbarscale
            TrackBar4.Value = Tr.Z * trackbarscale
            TrackBar8.Value = Tr.Scale * (10 * trackbarscale)
            Update_Pose_Changes_FromTrackbar()
        Else
            Debugger.Break()
        End If
    End Sub

    Private Sub CheckBoxGenweight_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxGenweight.CheckedChanged
        If Not IsNothing(Selected_Slider) Then
            Selected_Slider.GenWeights = CheckBoxGenweight.Checked
        End If
    End Sub

    Private Sub ComboBoxSize_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxSize.SelectedIndexChanged
        Selected_size = ComboBoxSize.SelectedIndex
        Actualiza_Preset()
        Process_render_Changes(False)
    End Sub
End Class
