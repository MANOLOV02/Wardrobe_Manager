' -----------------------------------------------------------------------------
'  Nombre del programa:  Wardrobe Manager – FO4 / SSE Wardrobe Manager
'  Copyright (C) 2025  ManoloV02 (https://github.com/MANOLOV02)
'
'  Créditos:
'   - Ousnius: NiflySharp – Licensed under the GPL-3.0 License (https://github.com/ousnius/NiflySharp)
'   - Ousnius: Material Editor – Licensed under the MIT License (https://github.com/ousnius/Material-Editor)
'   - OpenTK (GLControl) – Licensed under the MIT License (https://github.com/opentk/opentk)
'   - ICSharpCode.SharpZipLib.dll – Licensed under the GPL-3.0 with linking exception (https://github.com/icsharpcode/SharpZipLib)
'   - K4os.Compression.LZ4.Streams – Licensed under the MIT License (https://github.com/MiloszKrajewski/K4os.Compression.LZ4)
'
'  This program is free software: you can redistribute it and/or modify
'  it under the terms of the GNU General Public License as published by
'  the Free Software Foundation, either version 3 of the License, or
'  (at your option) any later version.
'
'  This program is distributed in the hope that it will be useful,
'  but WITHOUT ANY WARRANTY; without even the implied warranty of
'  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'  GNU General Public License for more details.
'
'  You should have received a copy of the GNU General Public License
'  along with this program.  If not, see <https://www.gnu.org/licenses/>.
' -----------------------------------------------------------------------------

Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Windows.Forms.VisualStyles.VisualStyleElement
Imports System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip

Public Class Wardrobe_Manager_Form

    Public Class Directorios

        Public Shared ReadOnly Property Fallout4data As String
            Get
                Return Config_App.Current.FO4EDataPath
            End Get
        End Property
        Public Shared ReadOnly Property Fallout4Textures As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "Textures")
            End Get
        End Property

        Public Shared ReadOnly Property Fallout4Materials As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "Materials")
            End Get
        End Property
        Public Shared ReadOnly Property Fallout4Meshes As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "Meshes")
            End Get
        End Property

        Public Shared ReadOnly Property SliderPresetsRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderPresets")
            End Get
        End Property
        Public Shared ReadOnly Property PosesSAMRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "F4SE\Plugins\SAF\Poses\Exports")
            End Get
        End Property
        Public Shared ReadOnly Property PosesBSRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "PoseData")
            End Get
        End Property
        Public Shared ReadOnly Property LooksMenuBSSliders As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "F4SE\Plugins\F4EE\Sliders\CBBE.esp\sliders.json")
            End Get
        End Property
        Public Shared ReadOnly Property LooksMenuWMSliders As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "F4SE\Plugins\F4EE\Sliders\FO4WM_Manolo.esp\sliders.json")
            End Get
        End Property

        Public Shared ReadOnly Property SliderSetsRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderSets")
            End Get
        End Property
        Public Shared ReadOnly Property CBBESliderCategories As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderCategories\CBBE.xml")
            End Get
        End Property
        Public Shared ReadOnly Property SkeletonPath As String
            Get
                If Config_App.Current.SkeletonPath = "" Then
                    Config_App.Current.SkeletonPath = IO.Path.Combine(Config_App.Current.BsPath, "res\skeleton_fo4.nif")
                End If
                Return Config_App.Current.SkeletonPath
            End Get
        End Property
        Public Shared ReadOnly Property SliderSets_Processed As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderSets\Procesados")
            End Get
        End Property
        Public Shared ReadOnly Property SliderSets_Discarded As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderSets\Descartados")
            End Get
        End Property

        Public Shared ReadOnly Property ShapedataRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "ShapeData\")
            End Get
        End Property
        Public Shared ReadOnly Property SharedTexturesPath As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "Textures\ManoloCloned\ManoloShared")
            End Get
        End Property

    End Class

    Public OSP_Files As New List(Of OSP_Project_Class)
    Private Last_List_focused As System.Windows.Forms.ListView = ListViewSources
    Private Sub Habilita_deshabilita()
        Dim fullpack As Boolean = Full_packs_Selected()
        MovetoDiscardedButton.Enabled = Me.Enabled AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso fullpack
        CopytoPackButton.Enabled = Me.Enabled AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso ComboboxPacks.SelectedIndex <> -1
        EditButton.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count = 1
        MoveToProcessedButton.Enabled = Me.Enabled AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso fullpack
        MergeButton.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count > 1 And ComboboxPacks.SelectedIndex <> -1
        MergeIntoTargetButton.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count > 0 And ComboboxPacks.SelectedIndex <> -1 And ListViewTargets.SelectedIndices.Count > 0
        MergeInSelectedButton.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count > 0 And ComboboxPacks.SelectedIndex <> -1 And ListViewTargets.SelectedIndices.Count > 0
        ExtractSingleButton.Enabled = ListViewTargets.SelectedIndices.Count > 0
        RenameButton.Enabled = ListViewTargets.SelectedIndices.Count = 1
        EditTargetButton.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count = 1
        ButtonEditInternally.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count = 1
        ButtonSourceInternalEdit.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count = 1
        ButtonDelete.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count > 0
        CloneButton.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count > 0
        'GroupBox2.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count > 0
        ButtonBuildSingles.Enabled = Me.Enabled And ListViewTargets.SelectedIndices.Count > 0
        ButtonDeleteSource.Enabled = Me.Enabled And ListViewSources.SelectedIndices.Count > 0
        ButtonBuildFullPack.Enabled = Me.Enabled And ComboboxPacks.SelectedIndex <> -1
        If Skeleton_Class.HasSkeleton = True Then
            ButtonSkeleton.ForeColor = Color.Black
        Else
            ButtonSkeleton.ForeColor = Color.Red
        End If
    End Sub

    Private Async Sub Lee_Listbox()
        If firstime = True Then Exit Sub
        Habilita_deshabilita()


        Try
            If CheckBoxReloadDict.Checked Then
                Application.DoEvents()
                Await Diccionario()
                Application.DoEvents()
                CheckBoxReloadDict.Checked = False
            End If

            ' 0) Deshabilitamos UI
            Empieza_Procesos(0)

            ' Guardar selecciones previas
            Dim oldList_Source = "None"
            Dim oldList_Target = "None"
            Dim olds = Determina_Seleccionado_y_CambiaNombres(0)
            Dim oldt = Determina_Seleccionado_y_CambiaNombres(1)
            If Not IsNothing(olds) Then
                oldList_Source = olds.Nombre + olds.ParentOSP.Nombre
            End If
            If Not IsNothing(oldt) Then
                oldList_Target = oldt.Nombre + oldt.ParentOSP.Nombre
            End If

            Dim oldCombo = ComboboxPacks.SelectedIndex
            ' Limpiar UI
            ListViewSources.Items.Clear()
            ComboboxPacks.Items.Clear()
            FilesDictionary_class.SliderPresets.Presets.Clear()
            FilesDictionary_class.SliderPresets.Poses.Clear()
            FilesDictionary_class.SliderPresets.LoadCategories(Directorios.CBBESliderCategories)
            FilesDictionary_class.SliderPresets.LoadDefaultPose()
            FilesDictionary_class.SliderPresets.LoadPosesSAM(Directorios.PosesSAMRoot)
            FilesDictionary_class.SliderPresets.LoadPosesBS(Directorios.PosesBSRoot)
            Skeleton_Class.LoadSkeleton(False, False)

            OSP_Files.Clear()

            ' 0) Presets
            Dim filesPresets = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Directorios.SliderPresetsRoot, "*.xml", False).ToList

            ' 1) Preparamos la lista de archivos y el ProgressBar
            Dim files = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Directorios.SliderSetsRoot, "*.osp", False).ToList()
            ProgressBar1.Value = 0
            ProgressBar1.Maximum = files.Count + filesPresets.Count
            ProgressBar1.Value = 0

            For Each xm In filesPresets
                FilesDictionary_class.SliderPresets.LoadFromXml(xm)
                ProgressBar1.Value += 1
            Next

            ' 2) Parsear en background SALVO DEEP CHECK
            Dim allOSPs = New ConcurrentBag(Of OSP_Project_Class)()

            OSD_Class.FileLocks.Clear()
            Await Task.Run(Sub()
                               Parallel.ForEach(files, Sub(fil)
                                                           Dim osp = New OSP_Project_Class(fil, DeepAnalize_check.Checked)
                                                           allOSPs.Add(osp)
                                                           ' Reportar progreso (invoke para UI thread)
                                                           If DeepAnalize_check.Checked Then
                                                               For Each slider In osp.SliderSets
                                                                   slider.NIFContent = New Nifcontent_Class_Manolo(slider)
                                                                   slider.OSDContent_External = New OSD_Class(slider)
                                                                   slider.OSDContent_Local = New OSD_Class(slider)
                                                               Next
                                                           End If
                                                           Me.Invoke(Sub() ProgressBar1.Value += 1)
                                                       End Sub)
                           End Sub)

            ' 3) Ya en UI thread: llenar ComboBox y OSP_Files
            For Each osp In allOSPs.OrderBy(Function(pf) pf.Nombre)
                OSP_Files.Add(osp)
                If osp.IsManoloPack Then
                    ComboboxPacks.Items.Add(osp)
                End If
            Next

            ' 4) Construir la lista de ListViewItem con el filtro original
            Dim tmp As New List(Of ListViewItem)()
            For Each osp In allOSPs.OrderBy(Function(pf) pf.Nombre)
                If ShowCBBECheck.Checked OrElse osp.Nombre.StartsWith("CBBE") = False Then
                    For Each sliderSet In osp.SliderSets.OrderBy(Function(pf) pf.Nombre)
                        If (Not osp.IsManoloPack OrElse CheckShowpacks.Checked) AndAlso
                   (String.IsNullOrEmpty(TextBox2.Text) OrElse
                    sliderSet.Nombre.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    sliderSet.DescriptionValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    osp.Filename_WithoutPath.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse sliderSet.OutputPathValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse sliderSet.OutputFileValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase)) Then
                            If osp.SliderSets.Count = 1 OrElse
                       (ShowCollectionsCheck.Checked AndAlso Not osp.IsManoloPack) OrElse
                       (CheckShowpacks.Checked AndAlso osp.IsManoloPack) Then
                                Dim lvi = New ListViewItem({sliderSet.Nombre, sliderSet.DescriptionValue, osp.Filename_WithoutPath}) With {
                            .Tag = sliderSet
                        }
                                tmp.Add(lvi)
                                lvi.Name = sliderSet.Nombre + sliderSet.ParentOSP.Nombre
                            End If
                        End If
                    Next
                End If
            Next
            ListViewSources.Items.AddRange(tmp.ToArray())

            ' 5) Restaurar selección
            If oldCombo >= 0 AndAlso oldCombo < ComboboxPacks.Items.Count Then
                ComboboxPacks.SelectedIndex = oldCombo
            ElseIf ComboboxPacks.Items.Count > 0 Then
                ComboboxPacks.SelectedIndex = 0
            End If

            Dim Choosed_Source As ListViewItem = ListViewSources.Items.Find(oldList_Source, False).FirstOrDefault
            Dim Choosed_Target As ListViewItem = ListViewTargets.Items.Find(oldList_Target, False).FirstOrDefault
            If Not IsNothing(Choosed_Source) Then ListViewSources.FocusedItem = Choosed_Source : Choosed_Source.Selected = True
            If Not IsNothing(Choosed_Target) Then ListViewTargets.FocusedItem = Choosed_Target : Choosed_Target.Selected = True

            Relee_Poses()
            Relee_Presets()
        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
        Termina_Procesos()
    End Sub

    Private Sub Lee_Listbox_Targets()
        Habilita_deshabilita()
        Dim sel_slider As SliderSet_Class = Nothing
        Dim item As ListViewItem
        Dim oldSel = ""
        If Not IsNothing(ListViewTargets.FocusedItem) Then
            oldSel = CType(ListViewTargets.FocusedItem.Tag, SliderSet_Class).Nombre
        End If

        ListViewTargets.Items.Clear()
        If ComboboxPacks.SelectedIndex = -1 Then Exit Sub
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        For Each SliderSet As SliderSet_Class In Selected_Pack.SliderSets.OrderBy(Function(pf) pf.Nombre)
            item = New ListViewItem({SliderSet.Nombre, SliderSet.DescriptionValue, SliderSet.ParentOSP.Filename_WithoutPath}) With {.Tag = SliderSet, .Name = SliderSet.Nombre + SliderSet.ParentOSP.Nombre}
            ListViewTargets.Items.Add(item)
            If sel_slider Is SliderSet Then item.Selected = True
        Next
        Habilita_deshabilita()
    End Sub

    Private Sub Lee_shapes()
        If _Procesando = True Then Exit Sub
        Dim Seleccionado As SliderSet_Class = Nothing
        If RadioButton1.Checked OrElse (RadioButton3.Checked AndAlso Last_List_focused Is ListViewSources) Then
            Seleccionado = Determina_Seleccionado_y_CambiaNombres(0)
        End If
        If RadioButton2.Checked OrElse (RadioButton3.Checked AndAlso Last_List_focused Is ListViewTargets) Then
            Seleccionado = Determina_Seleccionado_y_CambiaNombres(1)
        End If
        ListView2.Items.Clear()
        Dim Selected_Combo_Preset As SlidersPreset_Class = Nothing
        Dim Selected_Combo_Pose As Poses_class = Nothing
        If ComboBoxPresets.SelectedIndex <> -1 Then FilesDictionary_class.SliderPresets.Presets.TryGetValue(ComboBoxPresets.Items(ComboBoxPresets.SelectedIndex), Selected_Combo_Preset)
        If ComboBoxPoses.SelectedIndex <> -1 Then FilesDictionary_class.SliderPresets.Poses.TryGetValue(ComboBoxPoses.Items(ComboBoxPoses.SelectedIndex), Selected_Combo_Pose)

        If IsNothing(Seleccionado) Then Exit Sub
        If Seleccionado.Unreadable_Project Then
            preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)
            Exit Sub
        End If
        Label6.Visible = False
        Physics_Label.Visible = False
        Cursor.Current = Cursors.WaitCursor
        Dim it As ListViewItem
        If OSP_Project_Class.Load_and_Check_Shapedata(Seleccionado) = False Then
            preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)
            Exit Sub
        End If
        If Seleccionado.HasPhysics Then
            Physics_Label.Visible = True
        End If
        Seleccionado.ReadhighHeel()
        it = New ListViewItem({Seleccionado.OutputFileValue, "Output", Seleccionado.HighHeelHeight.ToString("F2"), Seleccionado.OutputPathValue}) With {
            .Tag = Nothing,
            .BackColor = Color.FromKnownColor(KnownColor.Control)
        }
        ListView2.Items.Add(it)

        For Each shap In Seleccionado.Shapes
            Dim hh As String = "No"
            If shap.ParentSliderSet.IsHighHeel Then hh = "Yes"
            Dim locals As String = "Yes"
            If shap.IsExternal Then
                If shap.HasExternalSliders Then locals = "No"
                If locals = "No" AndAlso shap.HasLocalSliders Then locals = "Mixed"
            End If

            it = New ListViewItem({shap.Nombre, locals, hh, String.Join(";", shap.Datafolder)}) With {.Tag = shap}
            If shap.IsExternal Then it.ForeColor = Color.Green
            If hh = "Yes" Then
                Label6.Visible = True
            Else
                Label6.Visible = False
            End If
            If shap.IsExternal AndAlso locals <> "No" Then it.BackColor = Color.LightYellow

            ListView2.Items.Add(it)
        Next


        preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)

    End Sub



    Private Function Determina_Seleccionado_y_CambiaNombres() As SliderSet_Class()
        Dim Selected_source As SliderSet_Class
        Dim Selected_target As SliderSet_Class

        If IsNothing(ListViewSources.FocusedItem) Then
            If ListViewSources.SelectedIndices.Count > 0 Then
                Selected_source = ListViewSources.SelectedItems(0).Tag
            Else
                Selected_source = Nothing
            End If
        Else
            If ListViewSources.FocusedItem.Selected Then
                Selected_source = ListViewSources.FocusedItem.Tag
            Else
                If ListViewSources.SelectedIndices.Count > 0 Then
                    Selected_source = ListViewSources.SelectedItems(0).Tag
                Else
                    Selected_source = Nothing
                End If
            End If
        End If

        If IsNothing(ListViewTargets.FocusedItem) Then
            If ListViewTargets.SelectedIndices.Count > 0 Then
                Selected_target = ListViewTargets.SelectedItems(0).Tag
            Else
                Selected_target = Nothing
            End If
        Else
            If ListViewTargets.FocusedItem.Selected Then
                Selected_target = ListViewTargets.FocusedItem.Tag
            Else
                If ListViewTargets.SelectedIndices.Count > 0 Then
                    Selected_target = ListViewTargets.SelectedItems(0).Tag
                Else
                    Selected_target = Nothing
                End If
            End If
        End If

        If IsNothing(Selected_source) Then
            TextBox_SourceName.Text = "(None)"
        Else
            TextBox_SourceName.Text = Calcula_nombre(Selected_source)
        End If
        If IsNothing(Selected_target) Then
            TextBox_TargetName.Text = "(None)"
        Else
            TextBox_TargetName.Text = Selected_target.Nombre
        End If
        Return {Selected_source, Selected_target}
    End Function
    Private Function Calcula_nombre(Sliderset_Source As SliderSet_Class) As String
        If IsNothing(Sliderset_Source) Or IsNothing(ComboboxPacks.SelectedItem) Then
            Return "ERROR"
        End If

        Dim nombre As String = Sliderset_Source.Nombre
        nombre = nombre.Replace("_DonEb14n", "")
        nombre = nombre.Replace("Wardrobe ", "")
        nombre = nombre.Replace("Closet 1_", "Jinga Closet 1 ")
        nombre = nombre.Replace("Closet 2_", "Jinga Closet 2 ")
        nombre = nombre.Replace("Closet 3_", "Jinga Closet 3 ")
        nombre = nombre.Replace("Tough Girl", "Vtaw Tough Girl")
        nombre = nombre.Replace("Jinga_", "Jinga ")
        nombre = nombre.Replace("Shino_", "Shino ")
        nombre = nombre.Replace("COCO-", "COCO ")
        nombre = nombre.Replace("COCO -", "COCO ")
        nombre = nombre.Replace("CBBE Automatron - ", "")
        nombre = nombre.Replace("CBBE Creation Club - ", "")
        nombre = nombre.Replace("CBBE Far Harbor - ", "")
        nombre = nombre.Replace("CBBE Nuka World - ", "")
        nombre = nombre.Replace("CBBE Vanilla - ", "")

        If Sliderset_Source.ParentOSP.IsManoloPack AndAlso nombre.StartsWith(Sliderset_Source.ParentOSP.Nombre + " ", StringComparison.OrdinalIgnoreCase) Then
            nombre = nombre.Remove(0, (Sliderset_Source.ParentOSP.Nombre + " ").Length)
        End If

        If ComboboxPacks.SelectedIndex <> -1 Then nombre = nombre
        Return nombre
    End Function

    Private Sub MovetoDiscardedButton_Click(sender As Object, e As EventArgs) Handles MovetoDiscardedButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count)
        If MsgBox("Esta Seguro de mover " + ListViewSources.SelectedIndices.Count.ToString + " elementos a la categoría de descartados", vbYesNo) = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim cur = ListViewSources.SelectedIndices(0)
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            Mueve_Singles(ind, Directorios.SliderSets_Discarded, True)
        Next
        If cur <= ListViewSources.Items.Count - 1 Then ListViewSources.Items(cur).Selected = True Else If ListViewSources.Items.Count > 0 Then ListViewSources.Items(0).Selected = True
        Termina_Procesos()
    End Sub
    Private Sub MoveToProcessedButton_Click(sender As Object, e As EventArgs) Handles MoveToProcessedButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count)
        If MsgBox("Esta Seguro de mover " + ListViewSources.SelectedIndices.Count.ToString + " elementos a la categoría de procesados", vbYesNo) = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim cur = ListViewSources.SelectedIndices(0)
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            Mueve_Singles(ind, Directorios.SliderSets_Processed, True)
        Next
        If cur <= ListViewSources.Items.Count - 1 Then ListViewSources.Items(cur).Selected = True Else If ListViewSources.Items.Count > 0 Then ListViewSources.Items(0).Selected = True
        Termina_Procesos()
    End Sub

    Private Sub CopytoPackButton_Click(sender As Object, e As EventArgs) Handles CopytoPackButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count)

        If MsgBox("Esta Seguro de agregar " + ListViewSources.SelectedIndices.Count.ToString + " elementos a la categoría " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString, vbYesNo) = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Procesa_Singles(Selected_Pack, Selected_Pack.Filename)
        Termina_Procesos()
    End Sub
    Private Sub MergeButton_Click(sender As Object, e As EventArgs) Handles MergeButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count)
        If MsgBox("Esta Seguro de fusionar " + ListViewSources.SelectedIndices.Count.ToString + " elementos en la categoría " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString, vbYesNo) = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Merge_Singles(Selected_Pack, Selected_Pack.Filename)
        Termina_Procesos()
    End Sub
    Private Sub ButtonDelete_Click_1(sender As Object, e As EventArgs) Handles ButtonDelete.Click
        Empieza_Procesos(ListViewTargets.SelectedItems.Count * 2)
        If MsgBox("Esta seguro que quieres borrar " + ListViewTargets.SelectedIndices.Count.ToString + " elementos", vbCritical + vbOKCancel, "Confirmacion") = MsgBoxResult.Ok Then
            Dim toremove(ListViewTargets.SelectedItems.Count - 1) As ListViewItem
            ListViewTargets.SelectedItems.CopyTo(toremove, 0)
            For Each it In toremove
                Dim sliderset_target As SliderSet_Class = it.Tag
                ProgressBar1.Value += 1
                sliderset_target.ParentOSP.RemoveProject(sliderset_target)
                ListViewTargets.Items.Remove(it)
            Next
        End If
        Termina_Procesos()
    End Sub

    Private Sub EditButton_Click(sender As Object, e As EventArgs) Handles EditButton.Click
        Empieza_Procesos(1)
        Dim Selected_Source As SliderSet_Class = ListViewSources.FocusedItem.Tag
        Dim lastsave = IO.File.GetLastWriteTime(Selected_Source.ParentOSP.Filename)
        Abre_Sliderset(Selected_Source, Selected_Source.ParentOSP.Filename)
        If lastsave.Equals(IO.File.GetLastWriteTime(Selected_Source.ParentOSP.Filename)) = False Then
            Selected_Source.Reload(DeepAnalize_check.Checked)
            If preview_Control.Model.Last_rendered Is Selected_Source Then
                preview_Control.Model.Clean(False)
            End If
            Lee_shapes()
        End If
        Termina_Procesos()
    End Sub

    Private Sub EditTargetButto_Click(sender As Object, e As EventArgs) Handles EditTargetButton.Click
        Empieza_Procesos(1)
        Dim sliderset_target As SliderSet_Class = ListViewTargets.SelectedItems(0).Tag
        Dim lastsave = IO.File.GetLastWriteTime(sliderset_target.ParentOSP.Filename)
        Abre_Sliderset(sliderset_target, sliderset_target.ParentOSP.Filename)
        If lastsave.Equals(IO.File.GetLastWriteTime(sliderset_target.ParentOSP.Filename)) = False Then
            sliderset_target.Reload(DeepAnalize_check.Checked)
            If preview_Control.Model.Last_rendered Is sliderset_target Then
                preview_Control.Model.Clean(False)
            End If
            Lee_shapes()
        End If
        Termina_Procesos()
    End Sub
    Private _Procesando As Boolean = False
    Private Sub Empieza_Procesos(cantidad)
        Try
            Cursor.Current = Cursors.WaitCursor
            Me.Enabled = False
            _Procesando = True
            ProgressBar1.Value = 0
            ProgressBar1.Maximum = cantidad
            ProgressBar1.Value = 0
            Habilita_deshabilita()
        Catch ex As Exception
            Debugger.Break()
        End Try

    End Sub
    Private Sub Termina_Procesos()
        Me.Enabled = True
        ProgressBar1.Value = 0
        ProgressBar1.Maximum = 100000
        ProgressBar1.Value = 0
        _Procesando = False
        Cursor.Current = Cursors.Default
        Lee_shapes()
        Habilita_deshabilita()
    End Sub
    Private Sub Procesa_Singles(Pack As OSP_Project_Class, Filename As String)
        Dim Nombre As String
        Dim resultado As SliderSet_Class
        Dim fullpack As Boolean = Full_packs_Selected()
        Dim varios As Boolean = (ListViewSources.SelectedIndices.Count > 1)
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            ProgressBar1.Value += 1
            If Not varios Then Nombre = TextBox_SourceName.Text Else Nombre = Calcula_nombre(ind.Tag)
            resultado = Pack.Agrega_Proyecto(ind.Tag, Nombre, Filename, Exclude_Reference_Checkbox.Checked, Ovewrite_DataFiles.Checked, CloneMaterialsCheck.Checked, PhysicsCheckbox.Checked, OutputDirChangeCheck.Checked)
            If Not IsNothing(resultado) AndAlso Auto_Move_Check.Checked Then Mueve_Singles(ind, Directorios.SliderSets_Processed, fullpack)
            Lee_Listbox_Targets()
        Next
    End Sub
    Private Sub Rename_Clone_Target(original As SliderSet_Class, pack As OSP_Project_Class, Nombre As String, DeleteAfter As Boolean)
        Dim resultado As SliderSet_Class
        ProgressBar1.Value += 1
        resultado = pack.Agrega_Proyecto(original, Nombre, pack.Filename, False, False, False, True, False)
        If Not IsNothing(resultado) Then
            If DeleteAfter Then pack.RemoveProject(original)
            Lee_Listbox_Targets()
        End If
    End Sub
    Private Sub Extract_Target(Source As SliderSet_Class, pack As OSP_Project_Class, Nombre As String)
        Dim resultado As SliderSet_Class
        Dim Origen As SliderSet_Class = Source
        resultado = pack.Agrega_Proyecto(Origen, Nombre, pack.Filename, False, False, False, True, False)
    End Sub

    Private Sub Merge_Singles(Pack As OSP_Project_Class, Filename As String)
        Dim Nombre As String = TextBox_SourceName.Text
        Dim fullpack As Boolean = Full_packs_Selected()
        ProgressBar1.Value += 1
        Dim mover As ListViewItem = ListViewSources.SelectedItems(0)
        Dim Proyecto_Madre As SliderSet_Class = Pack.Agrega_Proyecto(ListViewSources.SelectedItems(0).Tag, Nombre, Filename, Exclude_Reference_Checkbox.Checked, Ovewrite_DataFiles.Checked, CloneMaterialsCheck.Checked, PhysicsCheckbox.Checked, OutputDirChangeCheck.Checked)
        If Not IsNothing(Proyecto_Madre) Then
            Merge_Part(Proyecto_Madre, mover, Auto_Move_Check.Checked)
        End If
        If Not IsNothing(Proyecto_Madre) AndAlso Auto_Move_Check.Checked Then Mueve_Singles(mover, Directorios.SliderSets_Processed, fullpack)
        Lee_Listbox_Targets()
    End Sub
    Private Sub Merge_Part(Proyecto_Madre As SliderSet_Class, Movido As ListViewItem, Mueve As Boolean)
        Dim resultado As SliderSet_Class
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            If IsNothing(Movido) OrElse (ind Is Movido) = False Then
                ProgressBar1.Value += 1
                resultado = OSP_Project_Class.Merge_Proyecto(Proyecto_Madre, ind.Tag, Exclude_Reference_Checkbox.Checked, Ovewrite_DataFiles.Checked, CloneMaterialsCheck.Checked, PhysicsCheckbox.Checked)
                If Not IsNothing(resultado) AndAlso Mueve Then Mueve_Singles(ind, Directorios.SliderSets_Processed, False)
            End If
        Next
    End Sub

    Private Sub Mueve_Singles(ind As ListViewItem, Directorio As String, Mueve_Pack As Boolean)
        Dim slidertomove As SliderSet_Class = ind.Tag
        If Mueve_Pack = True OrElse (slidertomove.ParentOSP.IsManoloPack = False AndAlso slidertomove.ParentOSP.SliderSets.Count <= 1) Then
            Dim actual As String = slidertomove.ParentOSP.Filename
            Dim Nuevo As String = IO.Path.Combine(Directorio, slidertomove.ParentOSP.Filename_WithoutPath)
            If IO.Directory.Exists(IO.Path.GetDirectoryName(Nuevo)) = False Then IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(Nuevo))
            If IO.File.Exists(actual) Then
                If IO.File.Exists(Nuevo) Then If MsgBox("Desea reeplazar el archivo guardado", vbOKCancel) = MsgBoxResult.Ok Then IO.File.Delete(Nuevo)
                If slidertomove.ParentOSP.SliderSets.Count = 1 OrElse ListViewSources.SelectedItems.Cast(Of ListViewItem).Where(Function(pf) CType(pf.Tag, SliderSet_Class).ParentOSP Is slidertomove.ParentOSP).Count = 1 Then
                    IO.File.Move(actual, Nuevo)
                End If
            End If
            ListViewSources.Items.Remove(ind)
        End If
    End Sub


    Private Shared Sub Abre_Sliderset(sliderset As SliderSet_Class, OSP_Filename As String)
        Dim second As String = Chr(34) + OSP_Filename + Chr(34)
        Dim first As String = Chr(34) + sliderset.Nombre + Chr(34)
        Dim Strat As New ProcessStartInfo With {
            .Arguments = ""
        }
        Strat.Arguments += "-proj" + " " + first + " " + second + " "
        Strat.Arguments = Strat.Arguments.Trim
        Strat.FileName = Config_App.Current.OSExePath
        Process.Start(Strat).WaitForExit()
    End Sub


    Private Sub RefreshButtoClick(sender As Object, e As EventArgs) Handles RefreshButton.Click
        Lee_Listbox()
    End Sub
    Private Function Full_packs_Selected() As Boolean
        If ListViewSources.SelectedIndices.Count = 0 Then Return False
        Dim selecteds As Integer = ListViewSources.SelectedIndices.Count
        Dim packs As Integer = ListViewSources.SelectedItems.Cast(Of ListViewItem).Select(Function(pf) CType(pf.Tag, SliderSet_Class).ParentOSP).Distinct.Select(Function(pf) pf.SliderSets.Count).Sum
        Return selecteds = packs
    End Function

    Private Sub NewPackButton_Click(sender As Object, e As EventArgs) Handles NewPackButton.Click
        Dim nombre = InputBox("Pack name", "New pack", "")
        If nombre = "" Then Exit Sub
        If nombre.StartsWith("("c) = False Then nombre = "(" + nombre
        If nombre.EndsWith(")"c) = False Then nombre += ")"
        Dim selected_pàck = OSP_Project_Class.Create_New(Path.Combine(Directorios.SliderSetsRoot, nombre + ".osp"), False, True)
        If Not IsNothing(selected_pàck) Then
            ComboboxPacks.Items.Add(selected_pàck)
            ComboboxPacks.SelectedItem = selected_pàck
        End If
    End Sub

    Public preview_Control As PreviewControl = Nothing
    Private Diccionario_Leido As Boolean = False
    Private Sub OSPManager_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        preview_Control = New PreviewControl With {.Dock = DockStyle.Fill}
        Panel_Preview_Container.Controls.Add(preview_Control)
        'SingleBoneCheck.Checked = preview_Control.Model.SingleBoneSkinning
        'RecalculateNormalsCheck.Checked = preview_Control.Model.RecalculateNormals
        Pone_checks()
        Dim xx = InicializarAsync()
        preview_Control.ApplyResize(True)
    End Sub
    Private Async Function InicializarAsync() As Task
        If firstime Then
            Me.Select()
            Application.DoEvents()
            Await Diccionario()
            Application.DoEvents()
            Me.Enabled = True
            firstime = False
            Lee_Listbox()
        End If
    End Function
    Public Async Function Diccionario() As Task
        Me.Enabled = False
        ProgressBar1.Value = 0
        ProgressBar1.Minimum = 0
        ProgressBar1.Maximum = 1 ' Temporarily set; real value will be updated
        Await (Task.Delay(100)) ' Give time for the form to paint

        Dim progress = New Progress(Of (Stepn As String, Value As Integer, Max As Integer))(
        Sub(update)
            ProgressBar1.Maximum = update.Max
            ProgressBar1.Value = Math.Min(update.Value, update.Max)
        End Sub)
        Await FilesDictionary_class.Fill_DictionaryAsync(Directorios.Fallout4data, progress)
        ProgressBar1.Value = 0
    End Function

    Private Sub RadioButton1_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButton1.CheckedChanged
        Lee_shapes()
    End Sub


    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Button2.FlatStyle = FlatStyle.Flat
        Button3.FlatStyle = FlatStyle.Standard
        ListView2.Visible = False
        preview_Control.Visible = True
        Lee_shapes()
    End Sub
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Button2.FlatStyle = FlatStyle.Standard
        Button3.FlatStyle = FlatStyle.Flat
        ListView2.Visible = True
        preview_Control.Visible = False
    End Sub

    Private Sub ComboBoxPresets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPresets.SelectedIndexChanged
        If ComboBoxPresets.SelectedIndex <> -1 Then
            Config_App.Current.Default_Preset = ComboBoxPresets.SelectedItem.ToString
        End If
        Lee_shapes
    End Sub
    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboboxPacks.SelectedIndexChanged
        Lee_Listbox_Targets()
        Lee_shapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListViewTargets_GotFocus(sender As Object, e As EventArgs) Handles ListViewTargets.GotFocus
        Last_List_focused = ListViewTargets
        Lee_shapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListViewSources_GotFocus(sender As Object, e As EventArgs) Handles ListViewSources.GotFocus
        Last_List_focused = ListViewSources
        Lee_shapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListView3_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles ListViewTargets.MouseDoubleClick
        If ListViewTargets.SelectedIndices.Count > 0 Then
            EditTargetButton.PerformClick()
        End If
    End Sub
    Private Sub ListView1_DoubleClick(sender As Object, e As EventArgs) Handles ListViewSources.DoubleClick
        If ListViewSources.SelectedIndices.Count = 1 Then ButtonSourceInternalEdit.PerformClick()
    End Sub



    Private Sub ListViewSources_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListViewSources.SelectedIndexChanged
        Lee_shapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListViewTargets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListViewTargets.SelectedIndexChanged
        Lee_shapes()
        Habilita_deshabilita()
    End Sub



    Private Sub RenameButton_Click(sender As Object, e As EventArgs) Handles RenameButton.Click
        Empieza_Procesos(1)
        If MsgBox("Esta Seguro de renombrar " + ListViewTargets.SelectedIndices.Count.ToString + " elementos ", vbYesNo) = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Source As SliderSet_Class = ListViewTargets.SelectedItems(0).Tag
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Rename_Clone_Target(Source, Selected_Pack, TextBox_TargetName.Text, True)
        Termina_Procesos()
    End Sub

    Private Sub ExtractSingleButton_Click(sender As Object, e As EventArgs) Handles ExtractSingleButton.Click
        Empieza_Procesos(ListViewTargets.SelectedItems.Count)
        If MsgBox("Esta Seguro de extraer " + ListViewTargets.SelectedIndices.Count.ToString + " elementos ", vbYesNo) = MsgBoxResult.Yes Then
            Dim Origin_Pack As OSP_Project_Class = ComboboxPacks.Items(ComboboxPacks.SelectedIndex)
            For Each it In ListViewTargets.SelectedItems
                Dim Source As SliderSet_Class = it.Tag
                Dim nombre As String
                If ListViewTargets.SelectedIndices.Count = 1 Then
                    nombre = TextBox_TargetName.Text
                Else
                    nombre = Source.Nombre
                End If
                If nombre.StartsWith(Origin_Pack.Nombre) Then nombre = nombre.Substring(Origin_Pack.Nombre.Length + 1)
                Dim selected_pàck = OSP_Project_Class.Create_New(Path.Combine(Directorios.SliderSetsRoot, nombre + ".osp"), False, False)
                If Not IsNothing(selected_pàck) Then
                    Extract_Target(Source, selected_pàck, nombre)
                End If
                ProgressBar1.Value += 1
            Next
            Lee_Listbox()
        End If
        Termina_Procesos()
    End Sub

    Private Sub MergeIntoTargetButton_Click(sender As Object, e As EventArgs) Handles MergeIntoTargetButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count * ListViewTargets.SelectedItems.Count)
        If MsgBox("Esta Seguro de fusionar " + ListViewSources.SelectedIndices.Count.ToString + " elementos en " + ListViewTargets.SelectedIndices.Count.ToString + " elementos de la categoría " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString, vbYesNo) = MsgBoxResult.Yes Then
            Dim selected_target As SliderSet_Class = Determina_Seleccionado_y_CambiaNombres(1)
            For Each it In ListViewTargets.SelectedItems
                Dim target As SliderSet_Class = it.Tag
                OSP_Project_Class.Load_and_Check_Shapedata(target)
                Merge_Part(target, Nothing, False)
            Next
            selected_target.Reload(DeepAnalize_check.Checked)
            If preview_Control.Model.Last_rendered Is selected_target Then
                preview_Control.Model.Last_rendered = Nothing
                Lee_shapes()
            End If
        End If
        Termina_Procesos()
    End Sub

    Private Sub CloneButton_Click(sender As Object, e As EventArgs) Handles CloneButton.Click
        Empieza_Procesos(ListViewTargets.SelectedIndices.Count)
        If MsgBox("Esta Seguro de clonar " + ListViewTargets.SelectedIndices.Count.ToString + " elementos ", vbYesNo) = MsgBoxResult.Yes Then
            For Each it In ListViewTargets.SelectedItems
                Dim Source As SliderSet_Class = it.Tag
                Dim nombre As String
                Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem

                If ListViewTargets.SelectedIndices.Count = 1 Then
                    nombre = TextBox_TargetName.Text
                    If Selected_Pack.SliderSets.Where(Function(pf) pf.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)).Any Then
                        nombre += "_Clone"
                    End If
                Else
                    nombre = Source.Nombre + "_Clone"
                End If
                Rename_Clone_Target(Source, Selected_Pack, nombre, False)
            Next
        End If

        Termina_Procesos()
    End Sub

    Private Sub Wardrobe_Manager_Form_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Config_App.SaveConfig()
        preview_Control.Clean()
        preview_Control.Dispose()
    End Sub

    Private Sub Wardrobe_Manager_Form_Load(sender As Object, e As EventArgs) Handles Me.Load
        TypeDescriptor.AddProvider(New FO4UnifiedMaterialProvider(), GetType(FO4UnifiedMaterial_Class))
        Config_App.LoadConfig()
        If Config_App.Check_All_Folder = False Then
            Dim Config As New Config_Form()
            Config.ShowDialog(Me)
        End If
        Config_App.Current.theme = AppTheme.Dark
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)

        If Config_App.Check_All_Folder = False Then End
        Save_Shared()
        ColorComboBox1.Rellena()
        ColorComboBox1.SelectedColor = Config_App.Current.Setting_BackColor
        ComboBoxSize.SelectedIndex = CInt(Config_App.Current.Bodytipe)
    End Sub

    Private Sub Save_Shared()
        If IO.Directory.Exists(Directorios.SharedTexturesPath) = False Then
            IO.Directory.CreateDirectory(Directorios.SharedTexturesPath)
        End If
        Dim fil As String = IO.Path.Combine(Directorios.SharedTexturesPath, "gradient_Inverse.dds")
        If IO.File.Exists(fil) = False Then
            IO.File.WriteAllBytes(fil, My.Resources.gradient_Inverse)
        End If
        fil = IO.Path.Combine(Directorios.SharedTexturesPath, "gradient.dds")
        If IO.File.Exists(fil) = False Then
            IO.File.WriteAllBytes(fil, My.Resources.gradient)
        End If
    End Sub

    Private firstime As Boolean = True

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles MergeInSelectedButton.Click
        MergeIntoTargetButton.PerformClick()
    End Sub

    Private Sub Button1_Click_1(sender As Object, e As EventArgs) Handles ButtonBuildSingles.Click
        Empieza_Procesos(ListViewTargets.SelectedItems.Count)
        Dim lista As New List(Of SliderSet_Class)
        For Each it In ListViewTargets.SelectedItems
            Dim sliderset_target As SliderSet_Class = it.Tag
            ProgressBar1.Value += 1
            lista.Add(sliderset_target)
        Next
        Build(lista.ToArray)
        Termina_Procesos()
    End Sub
    Private Sub Build(que As SliderSet_Class())

        ' Borra primero 
        If Config_App.Current.Settings_Build.DeleteUnbuilt = True Then
            For Each projecto In que
                Dim fil = IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, projecto.OutputPathValue), projecto.OutputFileValue)
                If fil.EndsWith(".nif") = False Then fil += ".nif"
                Dim hhfile = fil.Replace(".nif", ".txt", StringComparison.OrdinalIgnoreCase)
                Dim Trifile = fil.Replace(".nif", ".tri", StringComparison.OrdinalIgnoreCase)
                If IO.File.Exists(fil) Then IO.File.Delete(fil)
                If IO.File.Exists(hhfile) Then IO.File.Delete(hhfile)
                If IO.File.Exists(Trifile) Then IO.File.Delete(Trifile)
            Next
        End If

        ' Constuye por motor
        If Config_App.Current.Settings_Build.OwnEngine Then
            BuildInternally(que)
        Else
            Build_WithBS(que)
            For Each sliderset_target In que
                sliderset_target.NIFContent.Load_Manolo(sliderset_target.SourceFileFullPath)
                sliderset_target.ReadhighHeel()
                sliderset_target.SaveHighHeelBuild()
            Next
        End If
        ' Graba HH (Fallout)

        Me.Activate()
    End Sub
    Private Sub BuildInternally(que As SliderSet_Class())
        Dim Selected_Combo_Preset As SlidersPreset_Class = Nothing
        Dim Selected_Combo_Pose As Poses_class = Nothing
        If ComboBoxPresets.SelectedIndex <> -1 Then FilesDictionary_class.SliderPresets.Presets.TryGetValue(ComboBoxPresets.Items(ComboBoxPresets.SelectedIndex), Selected_Combo_Preset)
        If ComboBoxPoses.SelectedIndex <> -1 Then FilesDictionary_class.SliderPresets.Poses.TryGetValue(ComboBoxPoses.Items(ComboBoxPoses.SelectedIndex), Selected_Combo_Pose)
        Dim Builder As New BuildingForm(que, Selected_Combo_Preset, Selected_Combo_Pose)
        Builder.ShowDialog()

    End Sub
    Private Sub Build_WithBS(que As SliderSet_Class())
        Dim results = Create_Group_Build(que)
        Dim temposdfile = results(0)
        Dim TempGroupfile = results(1)

        Dim first = Chr(34) + "Temp_WM_Builder" + Chr(34)
        Dim second = Chr(34) + ComboBoxPresets.SelectedItem.ToString + Chr(34)
        Dim third = Chr(34) + Directorios.Fallout4data + Chr(34)
        Dim Strat As New ProcessStartInfo With {
            .WindowStyle = ProcessWindowStyle.Normal,
            .Arguments = ""
        }
        Strat.Arguments += "/gbuild" + " " + first + " " + "/t" + " " + third + " " + " /p" + " " + second + IIf(Config_App.Current.Settings_Build.SaveTri, " /tri", "") + " "
        Strat.Arguments = Strat.Arguments.Trim
        Strat.FileName = Config_App.Current.BSExePath
        Process.Start(Strat).WaitForExit()

        If IO.File.Exists(temposdfile) Then IO.File.Delete(temposdfile)
        If IO.File.Exists(TempGroupfile) Then IO.File.Delete(TempGroupfile)

    End Sub
    Public Shared Function Create_Group_Build(que As SliderSet_Class()) As String()
        Dim Otufits As New List(Of String)
        Dim DummyOSP As New OSP_Project_Class
        Dim nombre As String
        Dim idx As Integer = 0
        Dim TempOSDFile = IO.Path.Combine(Directorios.SliderSetsRoot, "Temp_WM_Builder.osp")
        Dim TempGroupFile As String = IO.Path.Combine(Config_App.Current.BsPath, "SliderGroups\Temp_WM_Builder.xml")
        Try
            If IO.File.Exists(TempOSDFile) Then IO.File.Delete(TempOSDFile)
            If IO.File.Exists(TempGroupFile) Then IO.File.Delete(TempGroupFile)
            If IO.Directory.Exists(IO.Path.GetDirectoryName(TempGroupFile)) = False Then IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(TempGroupFile))
            For Each sliderset_target In que
                idx += 1
                Try
                    Dim NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                    Dim builder As New SliderSet_Class(NodoClone, DummyOSP)
                    nombre = "Temporary_WM_Builder" + idx.ToString + builder.Nombre
                    Otufits.Add(nombre)
                    builder.Nombre = nombre
                    DummyOSP.xml.DocumentElement.AppendChild(builder.Nodo)

                Catch ex As Exception
                    Debugger.Break()
                End Try
            Next
            DummyOSP.Save_Pack_As(TempOSDFile, False)

            Dim writer = IO.File.CreateText(TempGroupFile)
            writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")

            writer.WriteLine("<SliderGroups>")
            writer.WriteLine("<Group name =" + Chr(34) + "Temp_WM_Builder" + Chr(34) + ">")
            For Each out In Otufits
                writer.WriteLine("<Member name =" + Chr(34) + out + Chr(34) + "/>")
            Next
            writer.WriteLine("</Group>")
            writer.WriteLine("</SliderGroups>")
            writer.Flush()
            writer.Close()

        Catch ex As Exception
            Debugger.Break()
        End Try


        Return {TempOSDFile, TempGroupFile}

    End Function


    Private Sub Relee_Presets()
        Dim old_str
        If ComboBoxPresets.SelectedIndex <> -1 Then
            old_str = ComboBoxPresets.Items(ComboBoxPresets.SelectedIndex).ToString
        Else
            old_str = Config_App.Current.Default_Preset
        End If
        ComboBoxPresets.SuspendLayout()
        ComboBoxPresets.BeginUpdate()
        ComboBoxPresets.Items.Clear()
        ComboBoxPresets.Items.AddRange(FilesDictionary_class.SliderPresets.Presets.Keys.Order.ToArray)
        If FilesDictionary_class.SliderPresets.Presets.Keys.Order.ToList.IndexOf(old_str) <> -1 Then
            ComboBoxPresets.SelectedIndex = FilesDictionary_class.SliderPresets.Presets.Keys.Order.ToList.IndexOf(old_str)
        Else
            If ComboBoxPresets.Items.Count > 0 Then ComboBoxPresets.SelectedIndex = 0
        End If
        ComboBoxPresets.EndUpdate()
        ComboBoxPresets.ResumeLayout()
    End Sub

    Private Sub Relee_Poses()
        Dim old_str
        If ComboBoxPoses.SelectedIndex <> -1 Then
            old_str = ComboBoxPoses.Items(ComboBoxPoses.SelectedIndex).ToString
        Else
            old_str = "None (Wardrobe Manager pose)"
        End If
        ComboBoxPoses.SuspendLayout()
        ComboBoxPoses.BeginUpdate()
        ComboBoxPoses.Items.Clear()
        ComboBoxPoses.Items.AddRange(FilesDictionary_class.SliderPresets.Poses.Keys.Order.ToArray)
        If FilesDictionary_class.SliderPresets.Poses.Keys.Order.ToList.IndexOf(old_str) <> -1 Then
            ComboBoxPoses.SelectedIndex = FilesDictionary_class.SliderPresets.Poses.Keys.Order.ToList.IndexOf(old_str)
        Else
            If ComboBoxPoses.Items.Count > 0 Then ComboBoxPoses.SelectedIndex = 0
        End If
        ComboBoxPoses.EndUpdate()
        ComboBoxPoses.ResumeLayout()
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles ButtonSourceInternalEdit.Click
        Empieza_Procesos(1)
        Dim Editor As New Editor_Form
        Dim sliderset_Source = Determina_Seleccionado_y_CambiaNombres(0)
        If sliderset_Source.Unreadable_NIF Or sliderset_Source.Unreadable_Project Then Exit Sub
        Open_Editor(sliderset_Source, False)
    End Sub
    Private Sub ButtonEditInternally_Click(sender As Object, e As EventArgs) Handles ButtonEditInternally.Click
        Empieza_Procesos(1)
        Dim sliderset_target = Determina_Seleccionado_y_CambiaNombres(1)
        If sliderset_target.Unreadable_NIF Or sliderset_target.Unreadable_Project Then Exit Sub
        Open_Editor(sliderset_target, True)
    End Sub
    Private Sub Open_Editor(selected As SliderSet_Class, Grabable As Boolean)
        Dim Editor As New Editor_Form
        Editor.Lee_Edit(selected, ComboBoxPresets.SelectedItem.ToString, ComboBoxPoses.SelectedItem.ToString)
        Editor.Grabable = Grabable
        Editor.SingleBoneCheck.Checked = SingleBoneCheck.Checked
        Editor.RecalculateNormalsCheck.Checked = RecalculateNormalsCheck.Checked
        Editor.ShowDialog(Me)
        If preview_Control.Model.Last_rendered Is selected Then
            preview_Control.Model.Clean(False)
        End If
        selected.Reload(DeepAnalize_check.Checked)
        Relee_Presets()
        Relee_Poses()
        Lee_shapes()
        Termina_Procesos()
    End Sub
    Private Sub ListViewTargets_DoubleClick(sender As Object, e As EventArgs) Handles ListViewTargets.DoubleClick
        If ListViewTargets.SelectedIndices.Count = 1 Then ButtonEditInternally.PerformClick()
    End Sub

    Private Sub Button4_Click_1(sender As Object, e As EventArgs) Handles ButtonBuildFullPack.Click
        Dim ques(ListViewTargets.Items.Count - 1) As SliderSet_Class
        For i = 0 To ListViewTargets.Items.Count - 1
            ques(i) = ListViewTargets.Items(i).Tag
        Next
        Build(ques)
    End Sub

    Private Sub Wardrobe_Manager_Form_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing

    End Sub

    Private Sub Button4_Click_2(sender As Object, e As EventArgs) Handles Button4.Click
        Dim Config As New Config_Form
        Dim Oldtheme = Config_App.Current.theme
        Config.ShowDialog(Me)
        While Config_App.Check_All_Folder = False
            If MsgBox("Path error, Do you want to correct it or or exit?", vbYesNo, "Warning") = MsgBoxResult.No Then Me.Close() : Exit Sub
            Config.ShowDialog(Me)
        End While
        'If Config_App.Current.theme <> Oldtheme Then ThemeManager.SetTheme(Config_App.Current.theme, Me)
        SingleBoneCheck.Checked = Config_App.Current.Setting_SingleBoneSkinning
        RecalculateNormalsCheck.Checked = Config_App.Current.Setting_RecalculateNormals
        RefreshButton.PerformClick()
    End Sub

    Private Sub ButtonDeleteSource_Click(sender As Object, e As EventArgs) Handles ButtonDeleteSource.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count * 2)
        If MsgBox("Esta seguro que quieres borrar " + ListViewSources.SelectedIndices.Count.ToString + " elementos", vbCritical + vbYesNo, "Confirmacion") = MsgBoxResult.Yes Then
            Dim toremove(ListViewSources.SelectedItems.Count - 1) As ListViewItem
            ListViewSources.SelectedItems.CopyTo(toremove, 0)
            For Each it In toremove
                Dim sliderset_Source As SliderSet_Class = it.Tag
                ProgressBar1.Value += 1
                sliderset_Source.ParentOSP.RemoveProject(sliderset_Source)
                If sliderset_Source.ParentOSP.SliderSets.Count = 0 AndAlso sliderset_Source.ParentOSP.IsManoloPack = False Then
                    IO.File.Delete(sliderset_Source.ParentOSP.Filename)
                End If
                ListViewSources.Items.Remove(it)
            Next
        End If
        Termina_Procesos()
    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles SingleBoneCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        preview_Control.Model.SingleBoneSkinning = SingleBoneCheck.Checked
        Config_App.Current.Setting_SingleBoneSkinning = SingleBoneCheck.Checked
        ComboBoxPoses.Enabled = Not SingleBoneCheck.Checked
        preview_Control.Model.Clean(True)
        Lee_shapes()
    End Sub

    Private Sub Exclude_Reference_Checkbox_CheckedChanged(sender As Object, e As EventArgs) Handles Exclude_Reference_Checkbox.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_ExcludeReference = Exclude_Reference_Checkbox.Checked
    End Sub

    Private Sub PhysicsCheckbox_CheckedChanged(sender As Object, e As EventArgs) Handles PhysicsCheckbox.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_KeepPhysics = PhysicsCheckbox.Checked
    End Sub

    Private Sub CloneMaterialsCheck_CheckedChanged(sender As Object, e As EventArgs) Handles CloneMaterialsCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_Clone_Materials = CloneMaterialsCheck.Checked
    End Sub

    Private Sub Ovewrite_DataFiles_CheckedChanged(sender As Object, e As EventArgs) Handles Ovewrite_DataFiles.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_OverWrite = Ovewrite_DataFiles.Checked
    End Sub

    Private Sub Auto_Move_Check_CheckedChanged(sender As Object, e As EventArgs) Handles Auto_Move_Check.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_Automove = Auto_Move_Check.Checked

    End Sub

    Private Sub OutputDirChangeCheck_CheckedChanged(sender As Object, e As EventArgs) Handles OutputDirChangeCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_ChangeOutDir = OutputDirChangeCheck.Checked
    End Sub
    Private Sub Pone_checks()
        OutputDirChangeCheck.Checked = Config_App.Current.Setting_ChangeOutDir
        Auto_Move_Check.Checked = Config_App.Current.Setting_Automove
        Ovewrite_DataFiles.Checked = Config_App.Current.Setting_OverWrite
        CloneMaterialsCheck.Checked = Config_App.Current.Setting_Clone_Materials
        PhysicsCheckbox.Checked = Config_App.Current.Setting_KeepPhysics
        Exclude_Reference_Checkbox.Checked = Config_App.Current.Setting_ExcludeReference
        SingleBoneCheck.Checked = Config_App.Current.Setting_SingleBoneSkinning
        RecalculateNormalsCheck.Checked = Config_App.Current.Setting_RecalculateNormals
        ShowCollectionsCheck.Checked = Config_App.Current.Setting_ShowCollections
        ShowCBBECheck.Checked = Config_App.Current.Setting_ShowCBBE
        CheckShowpacks.Checked = Config_App.Current.Setting_Showpacks
    End Sub

    Private Sub CheckShowpacks_CheckedChanged(sender As Object, e As EventArgs) Handles CheckShowpacks.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_Showpacks = CheckShowpacks.Checked
    End Sub

    Private Sub ShowCBBECheck_CheckedChanged(sender As Object, e As EventArgs) Handles ShowCBBECheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_ShowCBBE = ShowCBBECheck.Checked
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles ShowCollectionsCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        Config_App.Current.Setting_ShowCollections = ShowCollectionsCheck.Checked
    End Sub

    Private Sub Button1_Click_2(sender As Object, e As EventArgs) Handles Button1.Click
        Dim dict_used As FilesDictionary_class.DictionaryFilePickerConfig = FilesDictionary_class.ALLMeshesDictionary_Filter
        Dim dictProvider = dict_used.DictionaryProvider
        Dim dict = dictProvider.Invoke()
        Dim filtered = dict.Keys.Where(Function(k) FilesDictionary_class.DictionaryFilePickerConfig.PathStartsWithRoot(k, dict_used.RootPrefix) And dict_used.ExtensionAllowed(k)).Order.ToList()
        Dim initialKey As String = ""

        Using frm As New Create_from_Nif_Form(filtered, dict_used.RootPrefix, dict_used.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.Yes Then
                RefreshButton.PerformClick()
            End If
        End Using
    End Sub

    Private Sub ComboBoxPoses_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPoses.SelectedIndexChanged
        Lee_shapes()
    End Sub

    Private Sub ButtonSkeleton_Click(sender As Object, e As EventArgs) Handles ButtonSkeleton.Click
        Dim dict_used As FilesDictionary_class.DictionaryFilePickerConfig = FilesDictionary_class.ALLMeshesDictionary_Filter
        Dim dictProvider = dict_used.DictionaryProvider
        Dim dict = dictProvider.Invoke()
        Dim filtered = dict.Keys.Where(Function(k) FilesDictionary_class.DictionaryFilePickerConfig.PathStartsWithRoot(k, dict_used.RootPrefix) And dict_used.ExtensionAllowed(k)).Order.ToList()
        Dim initialKey As String = IO.Path.GetRelativePath(Directorios.Fallout4data, Directorios.SkeletonPath)
        Using frm As New DictionaryFilePicker_Form(filtered, dict_used.RootPrefix, dict_used.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.OK Then
                Config_App.Current.SkeletonPath = IO.Path.Combine(Directorios.Fallout4data, frm.DictionaryPicker_Control1.SelectedKey)
                Skeleton_Class.LoadSkeleton(True, True)
                Habilita_deshabilita()
                Lee_shapes()
                RefreshButton.PerformClick()
            End If
        End Using
    End Sub

    Private Sub ButtonSkeleton_DpiChangedAfterParent(sender As Object, e As EventArgs) Handles ButtonSkeleton.DpiChangedAfterParent

    End Sub

    Private Sub CheckBoxRecalculate_CheckedChanged(sender As Object, e As EventArgs) Handles RecalculateNormalsCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        preview_Control.Model.RecalculateNormals = RecalculateNormalsCheck.Checked
        Config_App.Current.Setting_RecalculateNormals = RecalculateNormalsCheck.Checked
        preview_Control.Model.Clean(True)
        Lee_shapes()
    End Sub

    Private Sub Wardrobe_Manager_Form_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
#If DEBUG Then
        If e.KeyValue = Keys.F1 Then preview_Control.SharedActiveShader.Debugmode = 0
        If e.KeyValue = Keys.F2 Then preview_Control.SharedActiveShader.Debugmode = 1
        If e.KeyValue = Keys.F3 Then preview_Control.SharedActiveShader.Debugmode = 2
        If e.KeyValue = Keys.F4 Then preview_Control.SharedActiveShader.Debugmode = 3
        If e.KeyValue = Keys.F5 Then preview_Control.SharedActiveShader.Debugmode = 4
        preview_Control.updateRequired = True
#End If
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
    End Sub

    Private Sub Button5_Click_1(sender As Object, e As EventArgs) Handles Button5.Click
        Split_Principal2.Panel2Collapsed = Not Split_Principal2.Panel2Collapsed
        If Split_Principal2.Panel2Collapsed Then
            Button5.ImageIndex = 14
        Else
            Button5.ImageIndex = 15
        End If

    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click
        SplitPrincipal_1.Panel1Collapsed = Not SplitPrincipal_1.Panel1Collapsed
        If SplitPrincipal_1.Panel1Collapsed Then
            Button6.ImageIndex = 14
        Else
            Button6.ImageIndex = 15
        End If
    End Sub

    Private Sub ColorComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ColorComboBox1.SelectedIndexChanged
        Config_App.Current.Setting_BackColorName = ColorComboBox1.SelectedColor.Name
        If Not IsNothing(preview_Control) Then
            preview_Control.updateRequired = True
            preview_Control.Update()
        End If
    End Sub

    Private _Sourcesort As Integer = 0
    Private Sub ListViewSources_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles ListViewSources.ColumnClick
        If e.Column = _Sourcesort Then
            If ListViewSources.Sorting = SortOrder.Ascending Then
                ListViewSources.Sorting = SortOrder.Descending
            Else
                ListViewSources.Sorting = SortOrder.Ascending
            End If
        Else
            ' Nueva columna: ordenar ascendente por defecto
            _Sourcesort = e.Column
            ListViewSources.Sorting = SortOrder.Ascending
        End If
        ListViewSources.Columns(0).Text = "Name"
        ListViewSources.Columns(1).Text = "Description"
        ListViewSources.Columns(2).Text = "File"
        ListViewSources.Columns(_Sourcesort).Text += IIf(ListViewSources.Sorting = SortOrder.Ascending, " ↓", " ↑")
        ' Asignar el comparador y ordenar
        ListViewSources.ListViewItemSorter = New ListViewItemComparer(e.Column, ListViewSources.Sorting)
        ListViewSources.SuspendLayout()
        ListViewSources.Sort()
        ListViewSources.ResumeLayout()

    End Sub

    Private _TargetSort As Integer = 0
    Private Sub ListViewTargets_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles ListViewTargets.ColumnClick
        If e.Column = _TargetSort Then
            If ListViewTargets.Sorting = SortOrder.Ascending Then
                ListViewTargets.Sorting = SortOrder.Descending
            Else
                ListViewTargets.Sorting = SortOrder.Ascending
            End If
        Else
            ' Nueva columna: ordenar ascendente por defecto
            _TargetSort = e.Column
            ListViewTargets.Sorting = SortOrder.Ascending
        End If
        ListViewTargets.Columns(0).Text = "Name"
        ListViewTargets.Columns(1).Text = "Description"
        ListViewTargets.Columns(2).Text = "File"
        ListViewTargets.Columns(_TargetSort).Text += IIf(ListViewTargets.Sorting = SortOrder.Ascending, " ↓", " ↑")
        ' Asignar el comparador y ordenar
        ListViewTargets.ListViewItemSorter = New ListViewItemComparer(e.Column, ListViewTargets.Sorting)
        ListViewTargets.SuspendLayout()
        ListViewTargets.Sort()
        ListViewTargets.ResumeLayout()
    End Sub

    ' Comparador personalizado
    Public Class ListViewItemComparer
        Implements IComparer

        Private ReadOnly col As Integer
        Private ReadOnly order As SortOrder

        Public Sub New(column As Integer, order As SortOrder)
            Me.col = column
            Me.order = order
        End Sub

        Public Function ComparerForListview(x As Object, y As Object) As Integer Implements IComparer.Compare
            Dim itemX As String = DirectCast(x, ListViewItem).SubItems(col).Text
            Dim itemY As String = DirectCast(y, ListViewItem).SubItems(col).Text

            Dim compareResult As Integer

            ' Intentar comparar como número
            ' Comparar como texto
            compareResult = String.Compare(itemX, itemY)

            If order = SortOrder.Descending Then
                compareResult = -compareResult
            End If

            Return compareResult
        End Function
    End Class

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        Dim lightfirn As New LightRigForm
        lightfirn.ShowDialog(Me)
    End Sub

    Private Sub ComboBox1_SelectedIndexChanged_1(sender As Object, e As EventArgs) Handles ComboBoxSize.SelectedIndexChanged
        If ComboBoxPresets.SelectedIndex <> -1 Then
            Config_App.Current.Bodytipe = ComboBoxSize.SelectedIndex
        End If
        Lee_shapes()
    End Sub
End Class


