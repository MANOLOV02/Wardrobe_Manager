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
' Version Uploaded of Wardrobe 2.1.3
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
                Return IO.Path.Combine(Config_App.Current.BsPath, "SliderCategories")
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
        Public Shared ReadOnly Property HighHeels_Plugin As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, "F4SE\Plugins\HHS")
            End Get
        End Property
        Public Shared ReadOnly Property ShapedataRoot As String
            Get
                Return IO.Path.Combine(Config_App.Current.BsPath, "ShapeData\")
            End Get
        End Property
        Public Shared ReadOnly Property SharedTexturesPath As String
            Get
                Return IO.Path.Combine(Config_App.Current.FO4EDataPath, TexturesPrefix & "ManoloCloned\ManoloShared")
            End Get
        End Property

    End Class

    Public OSP_Files As New List(Of OSP_Project_Class)
    Private Last_List_focused As System.Windows.Forms.ListView = ListViewSources
    Private _LastLeeShapesRequestKey As String = ""
    Private _LeeShapesPending As Boolean = False
    Private Default_Pack_Name As String = "WM Default Pack"
    Private _RefreshingTargetsFromDisk As Boolean = False
    Private Sub Habilita_deshabilita()
        Dim fullpack As Boolean = Full_packs_Selected()
        Dim normalUi As Boolean = Me.Enabled
        Dim externalLock As Boolean = _ExternalEditActive

        ' Navigation / project-changing controls
        ListViewSources.Enabled = normalUi AndAlso Not externalLock
        ListViewTargets.Enabled = normalUi AndAlso Not externalLock
        ComboboxPacks.Enabled = normalUi AndAlso Not externalLock
        RadioButton1.Enabled = normalUi AndAlso Not externalLock
        RadioButton2.Enabled = normalUi AndAlso Not externalLock
        RadioButton3.Enabled = normalUi AndAlso Not externalLock

        ' Filters and list rebuild controls
        TextBox_SourceName.Enabled = normalUi AndAlso Not externalLock
        TextBox2.Enabled = normalUi AndAlso Not externalLock
        TextBox_TargetName.Enabled = normalUi AndAlso Not externalLock
        ShowCollectionsCheck.Enabled = normalUi AndAlso Not externalLock
        ShowCBBECheck.Enabled = normalUi AndAlso Not externalLock
        CheckShowpacks.Enabled = normalUi AndAlso Not externalLock
        DeepAnalize_check.Enabled = normalUi AndAlso Not externalLock
        CheckBoxReloadDict.Enabled = normalUi AndAlso Not externalLock
        RefreshButton.Enabled = normalUi AndAlso Not externalLock
        NewPackButton.Enabled = normalUi AndAlso Not externalLock
        ButtonCreateFromNif.Enabled = normalUi AndAlso Not externalLock

        ' Source actions
        MovetoDiscardedButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso fullpack
        CopytoPackButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso ComboboxPacks.SelectedIndex <> -1
        EditButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count = 1
        MoveToProcessedButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso fullpack
        MergeButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 1 AndAlso ComboboxPacks.SelectedIndex <> -1
        MergeIntoTargetButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso ComboboxPacks.SelectedIndex <> -1 AndAlso ListViewTargets.SelectedIndices.Count > 0
        MergeInSelectedButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0 AndAlso ComboboxPacks.SelectedIndex <> -1 AndAlso ListViewTargets.SelectedIndices.Count > 0
        ButtonSourceInternalEdit.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count = 1
        ButtonDeleteSource.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewSources.SelectedIndices.Count > 0

        ' Target actions
        ExtractSingleButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count > 0
        RenameButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count = 1
        EditTargetButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count = 1
        ButtonEditInternally.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count = 1
        ButtonDelete.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count > 0
        CloneButton.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count > 0
        ButtonBuildSingles.Enabled = normalUi AndAlso Not externalLock AndAlso ListViewTargets.SelectedIndices.Count > 0
        ButtonBuildFullPack.Enabled = normalUi AndAlso Not externalLock AndAlso ComboboxPacks.SelectedIndex <> -1

        ' Keep preview controls active during external editing
        ComboBoxPresets.Enabled = normalUi
        ComboBoxPoses.Enabled = normalUi
        SingleBoneCheck.Enabled = normalUi
        RecalculateNormalsCheck.Enabled = normalUi
        ColorComboBox1.Enabled = normalUi
        ButtonLightRigSettings.Enabled = normalUi
        ButtonPreviewSelected.Enabled = normalUi
        ButtonDataSheetSelected.Enabled = normalUi
        ComboBoxSize.Enabled = normalUi
        ButtonSkeleton.Enabled = normalUi

        If Skeleton_Class.HasSkeleton = True Then
            ButtonSkeleton.ForeColor = Color.Black
        Else
            ButtonSkeleton.ForeColor = Color.Red
        End If
    End Sub
    Private Function GetCurrentSourceSelectionKey() As String
        If Not IsNothing(ListViewSources.FocusedItem) Then
            Return ListViewSources.FocusedItem.Name
        End If

        If ListViewSources.SelectedItems.Count > 0 Then
            Return ListViewSources.SelectedItems(0).Name
        End If

        Return ""
    End Function

    Private Function GetCurrentPackFilename() As String
        If ComboboxPacks.SelectedIndex <> -1 Then
            Dim selectedPack As OSP_Project_Class = TryCast(ComboboxPacks.SelectedItem, OSP_Project_Class)
            If Not IsNothing(selectedPack) Then
                Return selectedPack.Filename
            End If
        End If

        Return ""
    End Function

    Private Sub Rebuild_Packs_Combo(Optional selectedPackFilename As String = "")
        ComboboxPacks.Items.Clear()

        For Each osp In OSP_Files.OrderBy(Function(pf) pf.Nombre)
            If osp.IsManoloPack Then
                ComboboxPacks.Items.Add(osp)
            End If
        Next

        If ComboboxPacks.Items.Count = 0 Then
            Create_Default_Pack()
        End If

        If selectedPackFilename <> "" Then
            For i = 0 To ComboboxPacks.Items.Count - 1
                Dim pack = CType(ComboboxPacks.Items(i), OSP_Project_Class)
                If pack.Filename.Equals(selectedPackFilename, StringComparison.OrdinalIgnoreCase) Then
                    ComboboxPacks.SelectedIndex = i
                    Exit Sub
                End If
            Next
        End If

        If ComboboxPacks.Items.Count > 0 Then
            ComboboxPacks.SelectedIndex = 0
        End If
    End Sub

    Private Sub Rebuild_Source_List(Optional selectionKey As String = "")
        If String.IsNullOrEmpty(selectionKey) Then
            selectionKey = GetCurrentSourceSelectionKey()
        End If
        Dim totalSteps As Integer = OSP_Files.Count
        ProgressBar1.Minimum = 0
        ProgressBar1.Maximum = Math.Max(totalSteps, 1)
        ProgressBar1.Value = 0

        Dim tmp As New List(Of ListViewItem)

        For Each osp In OSP_Files.OrderBy(Function(pf) pf.Nombre)
            If ShowCBBECheck.Checked OrElse osp.Nombre.StartsWith("CBBE") = False Then
                For Each sliderSet In osp.SliderSets.OrderBy(Function(pf) pf.Nombre)
                    If (Not osp.IsManoloPack OrElse CheckShowpacks.Checked) AndAlso
                   (String.IsNullOrEmpty(TextBox2.Text) OrElse
                    sliderSet.Nombre.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    sliderSet.DescriptionValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    osp.Filename_WithoutPath.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    sliderSet.OutputPathValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase) OrElse
                    sliderSet.OutputFileValue.Contains(TextBox2.Text, StringComparison.OrdinalIgnoreCase)) Then

                        If osp.SliderSets.Count = 1 OrElse
                       (ShowCollectionsCheck.Checked AndAlso Not osp.IsManoloPack) OrElse
                       (CheckShowpacks.Checked AndAlso osp.IsManoloPack) Then

                            Dim lvi = New ListViewItem({sliderSet.Nombre, sliderSet.DescriptionValue, osp.Filename_WithoutPath}) With {
                                .Tag = sliderSet,
                                .Name = sliderSet.Nombre + sliderSet.ParentOSP.Nombre
                            }
                            tmp.Add(lvi)

                        End If
                    End If
                Next
            End If
            ProgressBar1.Value = Math.Min(ProgressBar1.Value + 1, ProgressBar1.Maximum)
        Next

        ListViewSources.BeginUpdate()
        ListViewSources.Items.Clear()

        If tmp.Count > 0 Then
            ListViewSources.Items.AddRange(tmp.ToArray())
        End If

        If selectionKey <> "" Then
            Dim choosedSource As ListViewItem = ListViewSources.Items.Find(selectionKey, False).FirstOrDefault
            If Not IsNothing(choosedSource) Then
                ListViewSources.FocusedItem = choosedSource
                choosedSource.Selected = True
            End If
        End If

        ListViewSources.EndUpdate()
    End Sub
    Private ReadOnly OSP_FileWriteTicks As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase)
    Private Async Function Refresh_OSP_Files_From_Disk() As Task
        If firstime Then Exit Function

        Habilita_deshabilita()
        _LastLeeShapesRequestKey = ""

        Try
            Empieza_Procesos(0)

            Dim oldSourceKey As String = GetCurrentSourceSelectionKey()
            Dim oldPackFilename As String = GetCurrentPackFilename()

            Dim diskFiles = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Directorios.SliderSetsRoot, "*.osp", False).ToList()

            Dim diskTicks As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase)
            For Each path In diskFiles
                diskTicks(path) = IO.File.GetLastWriteTimeUtc(path).Ticks
            Next

            Dim currentByPath As New Dictionary(Of String, OSP_Project_Class)(StringComparer.OrdinalIgnoreCase)


            For Each osp In OSP_Files
                currentByPath(osp.Filename) = osp
            Next

            Dim removedPaths = currentByPath.Keys.Except(diskTicks.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(Function(p) p).ToList()
            Dim addedPaths = diskTicks.Keys.Except(currentByPath.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(Function(p) p).ToList()
            Dim changedPaths = diskTicks.
            Where(Function(kvp) currentByPath.ContainsKey(kvp.Key) AndAlso
                                (Not OSP_FileWriteTicks.ContainsKey(kvp.Key) OrElse OSP_FileWriteTicks(kvp.Key) <> kvp.Value)).
            Select(Function(kvp) kvp.Key).
            OrderBy(Function(p) p).
            ToList()

            Dim totalSteps As Integer = removedPaths.Count + addedPaths.Count + changedPaths.Count
            ProgressBar1.Minimum = 0
            ProgressBar1.Maximum = Math.Max(totalSteps, 1)
            ProgressBar1.Value = 0

            For Each removedPath In removedPaths
                Dim ospToRemove = currentByPath(removedPath)
                OSP_Files.Remove(ospToRemove)
                OSP_FileWriteTicks.Remove(removedPath)
                ProgressBar1.Value = Math.Min(ProgressBar1.Value + 1, ProgressBar1.Maximum)
            Next

            For Each changedPath In changedPaths
                Dim oldOsp = currentByPath(changedPath)
                Dim newOsp = Await Task.Run(Function() New OSP_Project_Class(changedPath, DeepAnalize_check.Checked))

                If DeepAnalize_check.Checked Then
                    For Each slider In newOsp.SliderSets
                        slider.UnloadShapeData(False)
                    Next
                End If

                Dim idx = OSP_Files.IndexOf(oldOsp)
                If idx >= 0 Then
                    OSP_Files(idx) = newOsp
                Else
                    OSP_Files.Add(newOsp)
                End If

                OSP_FileWriteTicks(changedPath) = diskTicks(changedPath)
                ProgressBar1.Value = Math.Min(ProgressBar1.Value + 1, ProgressBar1.Maximum)
            Next

            For Each addedPath In addedPaths
                Dim newOsp = Await Task.Run(Function() New OSP_Project_Class(addedPath, DeepAnalize_check.Checked))

                If DeepAnalize_check.Checked Then
                    For Each slider In newOsp.SliderSets
                        slider.UnloadShapeData(False)
                    Next
                End If

                OSP_Files.Add(newOsp)
                OSP_FileWriteTicks(addedPath) = diskTicks(addedPath)
                ProgressBar1.Value = Math.Min(ProgressBar1.Value + 1, ProgressBar1.Maximum)
            Next

            Rebuild_Packs_Combo(oldPackFilename)
            Rebuild_Source_List(oldSourceKey)
            Lee_Listbox_Targets()
            Relee_Poses()
            Relee_Presets()

        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try

        Termina_Procesos()
    End Function
    Private Async Function Lee_Listbox() As Task
        If firstime = True Then Return
        Habilita_deshabilita()
        _LastLeeShapesRequestKey = ""

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

            Dim oldPackFilename As String = GetCurrentPackFilename()
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
            OSP_FileWriteTicks.Clear()

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

            FilesDictionary_class.HighHeels_Plugin_Value.LoadFromDirectory()

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
                                                                   slider.UnloadShapeData(False)
                                                               Next
                                                           End If
                                                           Me.Invoke(Sub() ProgressBar1.Value += 1)
                                                       End Sub)
                           End Sub)

            ' 3) Ya en UI thread: llenar ComboBox y OSP_Files
            For Each osp In allOSPs.OrderBy(Function(pf) pf.Nombre)
                OSP_Files.Add(osp)
                OSP_FileWriteTicks(osp.Filename) = IO.File.GetLastWriteTimeUtc(osp.Filename).Ticks
            Next

            ' 4) Construir la lista de ListViewItem con el filtro original
            Rebuild_Packs_Combo(oldPackFilename)
            Rebuild_Source_List(oldList_Source)

            ' 5) Restaurar selección
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
    End Function
    Private Sub Create_Default_Pack()
        Dim defaultPackPath = Path.Combine(Directorios.SliderSetsRoot, Default_Pack_Name + ".osp")
        If IO.File.Exists(defaultPackPath) Then Exit Sub
        Dim defaultPack = OSP_Project_Class.Create_New(defaultPackPath, False, True)
        OSP_FileWriteTicks(defaultPack.Filename) = IO.File.GetLastWriteTimeUtc(defaultPack.Filename).Ticks

        If Not IsNothing(defaultPack) Then
            OSP_Files.Add(defaultPack)
            ComboboxPacks.Items.Add(defaultPack)
        End If
    End Sub

    Private Sub Lee_Listbox_Targets()
        Habilita_deshabilita()

        Dim oldSel As String = GetCurrentTargetSelectionKey()

        Refresh_Selected_Pack_From_Disk()
        Rebuild_Target_List(oldSel)

        Habilita_deshabilita()
    End Sub
    Private Function GetCurrentTargetSelectionKey() As String
        If Not IsNothing(ListViewTargets.FocusedItem) Then
            Return ListViewTargets.FocusedItem.Name
        End If

        If ListViewTargets.SelectedItems.Count > 0 Then
            Return ListViewTargets.SelectedItems(0).Name
        End If

        Return ""
    End Function

    Private Sub Rebuild_Target_List(Optional selectionKey As String = "")
        If String.IsNullOrEmpty(selectionKey) Then
            selectionKey = GetCurrentTargetSelectionKey()
        End If

        ListViewTargets.BeginUpdate()
        ListViewTargets.Items.Clear()

        If ComboboxPacks.SelectedIndex = -1 Then
            ListViewTargets.EndUpdate()
            Exit Sub
        End If

        Dim selectedPack As OSP_Project_Class = TryCast(ComboboxPacks.SelectedItem, OSP_Project_Class)
        If IsNothing(selectedPack) Then
            ListViewTargets.EndUpdate()
            Exit Sub
        End If

        Dim tmp As New List(Of ListViewItem)

        For Each sliderSet As SliderSet_Class In selectedPack.SliderSets.OrderBy(Function(pf) pf.Nombre)
            Dim item = New ListViewItem({sliderSet.Nombre, sliderSet.DescriptionValue, sliderSet.ParentOSP.Filename_WithoutPath}) With {
            .Tag = sliderSet,
            .Name = sliderSet.Nombre + sliderSet.ParentOSP.Nombre
        }
            tmp.Add(item)
        Next

        If tmp.Count > 0 Then
            ListViewTargets.Items.AddRange(tmp.ToArray())
        End If

        If selectionKey <> "" Then
            Dim choosedTarget As ListViewItem = ListViewTargets.Items.Find(selectionKey, False).FirstOrDefault
            If Not IsNothing(choosedTarget) Then
                ListViewTargets.FocusedItem = choosedTarget
                choosedTarget.Selected = True
            End If
        End If

        ListViewTargets.EndUpdate()
    End Sub

    Private Sub Refresh_Selected_Pack_From_Disk()
        If _RefreshingTargetsFromDisk Then Exit Sub
        If ComboboxPacks.SelectedIndex = -1 Then Exit Sub

        Dim selectedPack As OSP_Project_Class = TryCast(ComboboxPacks.SelectedItem, OSP_Project_Class)
        If IsNothing(selectedPack) Then Exit Sub

        Dim selectedPackPath As String = selectedPack.Filename
        If String.IsNullOrWhiteSpace(selectedPackPath) Then Exit Sub

        _RefreshingTargetsFromDisk = True

        Try
            If IO.File.Exists(selectedPackPath) = False Then
                OSP_Files.Remove(selectedPack)
                OSP_FileWriteTicks.Remove(selectedPackPath)
                Rebuild_Packs_Combo("")
                Exit Sub
            End If

            Dim diskTick As Long = IO.File.GetLastWriteTimeUtc(selectedPackPath).Ticks
            Dim needsReload As Boolean =
            (Not OSP_FileWriteTicks.ContainsKey(selectedPackPath)) OrElse
            (OSP_FileWriteTicks(selectedPackPath) <> diskTick)

            If needsReload = False Then Exit Sub

            Dim newPack As New OSP_Project_Class(selectedPackPath, DeepAnalize_check.Checked)

            If DeepAnalize_check.Checked Then
                For Each slider In newPack.SliderSets
                    slider.UnloadShapeData(False)
                Next
            End If

            Dim idx = OSP_Files.IndexOf(selectedPack)
            If idx >= 0 Then
                OSP_Files(idx) = newPack
            Else
                OSP_Files.Add(newPack)
            End If

            OSP_FileWriteTicks(selectedPackPath) = diskTick
            Rebuild_Packs_Combo(selectedPackPath)

        Finally
            _RefreshingTargetsFromDisk = False
        End Try
    End Sub
    Private Function BuildLeeShapesRequestKey() As String
        Dim selectedSourceName As String = ""
        Dim selectedTargetName As String = ""
        Dim focusedListName As String = ""
        Dim presetName As String = ""
        Dim poseName As String = ""

        If ListViewSources.SelectedItems.Count > 0 Then
            Dim src = TryCast(ListViewSources.SelectedItems(0).Tag, SliderSet_Class)
            If Not IsNothing(src) Then selectedSourceName = src.ParentOSP.Filename & "|" & src.Nombre
        End If

        If ListViewTargets.SelectedItems.Count > 0 Then
            Dim trg = TryCast(ListViewTargets.SelectedItems(0).Tag, SliderSet_Class)
            If Not IsNothing(trg) Then selectedTargetName = trg.ParentOSP.Filename & "|" & trg.Nombre
        End If

        If Not IsNothing(Last_List_focused) Then focusedListName = Last_List_focused.Name
        If ComboBoxPresets.SelectedIndex <> -1 Then presetName = ComboBoxPresets.SelectedItem.ToString
        If ComboBoxPoses.SelectedIndex <> -1 Then poseName = ComboBoxPoses.SelectedItem.ToString

        Return String.Join("||", {
        RadioButton1.Checked.ToString,
        RadioButton2.Checked.ToString,
        RadioButton3.Checked.ToString,
        focusedListName,
        selectedSourceName,
        selectedTargetName,
        presetName,
        poseName,
        ComboBoxSize.SelectedIndex.ToString(Global.System.Globalization.CultureInfo.InvariantCulture),
        SingleBoneCheck.Checked.ToString,
        RecalculateNormalsCheck.Checked.ToString,
        preview_Control?.Visible.ToString,
        ListView2.Visible.ToString})
    End Function

    Private Sub RequestLeeShapes(Optional force As Boolean = False)
        If _Procesando Then Exit Sub
        If _LeeShapesPending Then Exit Sub

        Dim key = BuildLeeShapesRequestKey()
        If force = False AndAlso String.Equals(_LastLeeShapesRequestKey, key, StringComparison.Ordinal) Then Exit Sub

        _LeeShapesPending = True
        BeginInvoke(Sub()
                        Try
                            _LeeShapesPending = False
                            Dim currentKey = BuildLeeShapesRequestKey()
                            If force = False AndAlso String.Equals(_LastLeeShapesRequestKey, currentKey, StringComparison.Ordinal) Then Exit Sub
                            _LastLeeShapesRequestKey = currentKey
                            Lee_Shapes()
                        Catch
                            _LeeShapesPending = False
                        End Try
                    End Sub)
    End Sub


    Private Sub Lee_Shapes()
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
        preview_Control.Model.FloorOffset = -Seleccionado.HighHeelHeight
        If Seleccionado.Unreadable_Project Then
            preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)
            Exit Sub
        End If
        Label6.Visible = False
        Physics_Label.Visible = False
        Cursor.Current = Cursors.WaitCursor
        Dim it As ListViewItem
        If OSP_Project_Class.Load_and_Check_Shapedata(Seleccionado, False) = False Then
            preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)
            Exit Sub
        End If
        If Seleccionado.HasPhysics Then
            Physics_Label.Visible = True
        End If

        it = New ListViewItem({Seleccionado.OutputFileValue, "Output", Seleccionado.HighHeelHeight.ToString("F2"), Seleccionado.OutputPathValue}) With {
            .Tag = Nothing,
            .BackColor = Color.FromKnownColor(KnownColor.Control)
        }
        ListView2.Items.Add(it)

        Dim hh As String = "No"
        If Seleccionado.IsHighHeel Then
            hh = "Yes"
            Label6.Visible = True
        Else
            Label6.Visible = False
        End If
        For Each shap In Seleccionado.Shapes
            Dim locals As String = "Yes"
            If shap.IsExternal Then
                If shap.HasExternalSliders Then locals = "No"
                If locals = "No" AndAlso shap.HasLocalSliders Then locals = "Mixed"
            End If
            it = New ListViewItem({shap.Nombre, locals, hh, String.Join(";", shap.Datafolder)}) With {.Tag = shap}
            If shap.IsExternal Then it.ForeColor = Color.Green
            If shap.IsExternal AndAlso locals <> "No" Then it.BackColor = Color.LightYellow
            ListView2.Items.Add(it)
        Next
        preview_Control.Update_Render(Seleccionado, False, Selected_Combo_Preset, Selected_Combo_Pose, ComboBoxSize.SelectedIndex)
        _LastLeeShapesRequestKey = BuildLeeShapesRequestKey()
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
        If MsgBox("Are you sure you want to move " + ListViewSources.SelectedIndices.Count.ToString + " items to the discarded category?", vbYesNo, "Confirm") = MsgBoxResult.No Then
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
        If MsgBox("Are you sure you want to move " + ListViewSources.SelectedIndices.Count.ToString + " items to the processed category?", vbYesNo, "Confirm") = MsgBoxResult.No Then
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

        If MsgBox("Are you sure you want to add " + ListViewSources.SelectedIndices.Count.ToString + " items to category " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString + "?", vbYesNo, "Confirm") = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Procesa_Singles(Selected_Pack, Selected_Pack.Filename)
        Termina_Procesos()
    End Sub
    Private Sub MergeButton_Click(sender As Object, e As EventArgs) Handles MergeButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count)
        OSP_Project_Class.Default_Memory_Pause = True
        If MsgBox("Are you sure you want to merge " + ListViewSources.SelectedIndices.Count.ToString + " items into category " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString + "?", vbYesNo, "Confirm") = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Merge_Singles(Selected_Pack, Selected_Pack.Filename)
        Termina_Procesos()
        OSP_Project_Class.Default_Memory_Pause = False
    End Sub
    Private Sub ButtonDelete_Click_1(sender As Object, e As EventArgs) Handles ButtonDelete.Click
        Empieza_Procesos(ListViewTargets.SelectedItems.Count * 2)
        If MsgBox("Are you sure you want to delete " + ListViewTargets.SelectedIndices.Count.ToString + " items?", vbCritical + vbOKCancel, "Confirm") = MsgBoxResult.Ok Then
            Dim toremove(ListViewTargets.SelectedItems.Count - 1) As ListViewItem
            ListViewTargets.SelectedItems.CopyTo(toremove, 0)
            For Each it In toremove
                Dim sliderset_target As SliderSet_Class = it.Tag
                ProgressBar1.Value += 1
                sliderset_target.ParentOSP.RemoveProject(sliderset_target)
                ListViewTargets.Items.Remove(it)
                If sliderset_target.ParentOSP.SliderSets.Count = 0 Then Remove_Empty_Pack(sliderset_target)
            Next
            RequestLeeShapes(True)
        End If
        Termina_Procesos()

    End Sub
    Private Sub Remove_Empty_Pack(sliderset_target As SliderSet_Class)
        If MsgBox("The pack is empty, do you want to delete it?", vbYesNo, "Delete pack") = MsgBoxResult.Yes Then
            IO.File.Delete(sliderset_target.ParentOSP.Filename)
            Dim oldselected As OSP_Project_Class = Nothing
            If Not IsNothing(ComboboxPacks.SelectedItem) Then oldselected = ComboboxPacks.SelectedItem
            ComboboxPacks.Items.Remove(sliderset_target.ParentOSP)
            If ComboboxPacks.Items.Count = 0 Then Create_Default_Pack()
            If sliderset_target.ParentOSP Is oldselected Then ComboboxPacks.SelectedIndex = 0
        End If
    End Sub
    Private Sub EditButton_Click(sender As Object, e As EventArgs) Handles EditButton.Click
        If ListViewSources.FocusedItem Is Nothing Then Exit Sub
        Dim Selected_Source As SliderSet_Class = ListViewSources.FocusedItem.Tag
        StartExternalEditSession(Selected_Source, False)
    End Sub
    Private Sub EditTargetButto_Click(sender As Object, e As EventArgs) Handles EditTargetButton.Click
        If ListViewTargets.SelectedItems.Count = 0 Then Exit Sub
        Dim sliderset_target As SliderSet_Class = ListViewTargets.SelectedItems(0).Tag
        StartExternalEditSession(sliderset_target, True)
    End Sub

    Private _Procesando As Boolean = False
    Private _ExternalEditActive As Boolean = False
    Private _ExternalEditSlider As SliderSet_Class = Nothing
    Private _ExternalEditFromTarget As Boolean = False
    Private _ExternalEditProcess As Process = Nothing
    Private _ExternalEditLastOspWrite As Date = Date.MinValue
    Private _ExternalEditReloading As Boolean = False
    Private WithEvents ExternalEditTimer As New Timer With {.Interval = 700}

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
        RequestLeeShapes()
        Habilita_deshabilita()
        _LastLeeShapesRequestKey = ""
    End Sub
    Private Sub Procesa_Singles(Pack As OSP_Project_Class, Filename As String)
        Dim Nombre As String
        Dim resultado As SliderSet_Class
        Dim fullpack As Boolean = Full_packs_Selected()
        Dim varios As Boolean = (ListViewSources.SelectedIndices.Count > 1)
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            ProgressBar1.Value += 1
            If Not varios Then Nombre = TextBox_SourceName.Text Else Nombre = Calcula_nombre(ind.Tag)
            resultado = Pack.Agrega_Proyecto(ind.Tag, Nombre, Filename, Exclude_Reference_Checkbox.Checked, Ovewrite_DataFiles.Checked, PhysicsCheckbox.Checked, OutputDirChangeCheck.Checked)
            If Not IsNothing(resultado) Then
                If CloneMaterialsCheck.Checked Then Clone_Materials_class.Clone_Materials_For_Project(resultado, Ovewrite_DataFiles.Checked)
                If Auto_Move_Check.Checked Then Mueve_Singles(ind, Directorios.SliderSets_Processed, fullpack)
            End If
        Next
        Lee_Listbox_Targets()
    End Sub
    Private Sub Rename_Clone_Target(original As SliderSet_Class, pack As OSP_Project_Class, Nombre As String, DeleteAfter As Boolean)
        Dim resultado As SliderSet_Class
        ProgressBar1.Value += 1
        resultado = pack.Agrega_Proyecto(original, Nombre, pack.Filename, False, False, True, False)
        If Not IsNothing(resultado) Then
            If DeleteAfter Then pack.RemoveProject(original)
            Lee_Listbox_Targets()
        End If
    End Sub
    Private Sub Extract_Target(Source As SliderSet_Class, pack As OSP_Project_Class, Nombre As String)
        Dim resultado As SliderSet_Class
        Dim Origen As SliderSet_Class = Source
        resultado = pack.Agrega_Proyecto(Origen, Nombre, pack.Filename, False, False, True, False)
    End Sub

    Private Sub Merge_Singles(Pack As OSP_Project_Class, Filename As String)
        Dim Nombre As String = TextBox_SourceName.Text
        Dim fullpack As Boolean = Full_packs_Selected()
        ProgressBar1.Value += 1
        Dim mover As ListViewItem = ListViewSources.SelectedItems(0)
        Dim Proyecto_Madre As SliderSet_Class = Pack.Agrega_Proyecto(ListViewSources.SelectedItems(0).Tag, Nombre, Filename, Exclude_Reference_Checkbox.Checked, Ovewrite_DataFiles.Checked, PhysicsCheckbox.Checked, OutputDirChangeCheck.Checked)
        If Not IsNothing(Proyecto_Madre) Then
            Merge_Part(Proyecto_Madre, mover, Auto_Move_Check.Checked)
            If CloneMaterialsCheck.Checked Then Clone_Materials_class.Clone_Materials_For_Project(Proyecto_Madre, Ovewrite_DataFiles.Checked)
            If Auto_Move_Check.Checked Then Mueve_Singles(mover, Directorios.SliderSets_Processed, fullpack)
        End If
        Lee_Listbox_Targets()
    End Sub
    Private Sub Merge_Part(Proyecto_Madre As SliderSet_Class, Movido As ListViewItem, Mueve As Boolean)
        Dim resultado As SliderSet_Class
        For Each ind As ListViewItem In ListViewSources.SelectedItems
            If IsNothing(Movido) OrElse (ind Is Movido) = False Then
                ProgressBar1.Value += 1
                resultado = OSP_Project_Class.Merge_Proyecto(Proyecto_Madre, ind.Tag, Exclude_Reference_Checkbox.Checked, PhysicsCheckbox.Checked)
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
                If IO.File.Exists(Nuevo) Then
                    If MsgBox("Do you want to replace the saved file?", vbOKCancel, "Replace file") = MsgBoxResult.Ok Then
                        IO.File.Delete(Nuevo)
                    Else
                        Return
                    End If
                End If
                If slidertomove.ParentOSP.SliderSets.Count = 1 OrElse ListViewSources.SelectedItems.Cast(Of ListViewItem).Where(Function(pf) CType(pf.Tag, SliderSet_Class).ParentOSP Is slidertomove.ParentOSP).Count = 1 Then
                    IO.File.Move(actual, Nuevo)
                    IO.File.SetLastWriteTime(Nuevo, DateTime.Now)
                End If
            End If
            ListViewSources.Items.Remove(ind)
        End If
    End Sub
    Private Sub ReloadExternalEditedProject()
        If Not _ExternalEditActive Then Exit Sub
        If IsNothing(_ExternalEditSlider) Then Exit Sub
        If _ExternalEditReloading Then Exit Sub

        _ExternalEditReloading = True
        Try
            _ExternalEditSlider.Reload(DeepAnalize_check.Checked)
            If GetLatestExternalEditWriteTime(_ExternalEditSlider) > _ExternalEditLastOspWrite Then
                If preview_Control.Model.Last_rendered Is _ExternalEditSlider Then
                    preview_Control.Model.Clean(False)
                    preview_Control.Model.CleanTextures()
                End If
            End If
            RequestLeeShapes(True)
            _ExternalEditReloading = Not (Not _ExternalEditSlider.Unreadable_NIF And Not _ExternalEditSlider.Unreadable_Project)
        Catch ex As Exception
            MsgBox("External edit reload failed: " & ex.Message, MsgBoxStyle.Exclamation, "Error")
        Finally

        End Try
    End Sub
    Private Sub EndExternalEditSession(doFinalReload As Boolean)
        Dim lockedSlider = _ExternalEditSlider

        ExternalEditTimer.Stop()
        _ExternalEditActive = False
        _ExternalEditProcess = Nothing
        _ExternalEditReloading = False
        _ExternalEditSlider = Nothing
        _ExternalEditFromTarget = False

        If doFinalReload AndAlso Not IsNothing(lockedSlider) Then
            Try
                lockedSlider.Reload(DeepAnalize_check.Checked)
                If GetLatestExternalEditWriteTime(lockedSlider) > _ExternalEditLastOspWrite Then
                    If preview_Control.Model.Last_rendered Is lockedSlider Then
                        preview_Control.Model.Clean(False)
                        preview_Control.Model.CleanTextures()
                    End If
                End If
            Catch ex As Exception
                MsgBox("Final external edit reload failed: " & ex.Message, MsgBoxStyle.Exclamation, "Error")
            End Try
        End If
        _ExternalEditLastOspWrite = Date.MinValue

        Habilita_deshabilita()
        RequestLeeShapes(True)
    End Sub
    Private Function GetExistingExternalEditFiles(sliderset As SliderSet_Class) As List(Of String)
        Dim files As New List(Of String)

        If IsNothing(sliderset) Then Return files

        Try
            If Not IsNothing(sliderset.ParentOSP) AndAlso
           String.IsNullOrWhiteSpace(sliderset.ParentOSP.Filename) = False AndAlso
           IO.File.Exists(sliderset.ParentOSP.Filename) Then
                files.Add(sliderset.ParentOSP.Filename)
            End If
        Catch
        End Try

        Try
            If String.IsNullOrWhiteSpace(sliderset.SourceFileFullPath) = False AndAlso
           IO.File.Exists(sliderset.SourceFileFullPath) Then
                files.Add(sliderset.SourceFileFullPath)
            End If
        Catch
        End Try

        Try
            For Each osdPath In sliderset.OsdLocalFullPath
                If String.IsNullOrWhiteSpace(osdPath) = False AndAlso IO.File.Exists(osdPath) Then
                    files.Add(osdPath)
                End If
            Next
        Catch
        End Try

        Return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList
    End Function

    Private Function GetLatestExternalEditWriteTime(sliderset As SliderSet_Class) As Date
        Dim latest As Date = Date.MinValue
        If IsNothing(sliderset) Then Return latest
        For Each f In GetExistingExternalEditFiles(sliderset)
            Try
                Dim dt = IO.File.GetLastWriteTime(f)
                If dt > latest Then latest = dt
            Catch
            End Try
        Next

        Return latest
    End Function
    Private Sub StartExternalEditSession(sliderset As SliderSet_Class, fromTarget As Boolean)
        If IsNothing(sliderset) Then Exit Sub

        If _ExternalEditActive Then
            MsgBox("An Outfit Studio editing session is already active.")
            Exit Sub
        End If

        _ExternalEditSlider = sliderset
        _ExternalEditFromTarget = fromTarget
        _ExternalEditReloading = False
        _ExternalEditLastOspWrite = GetLatestExternalEditWriteTime(sliderset)

        Try
            _ExternalEditProcess = Abre_Sliderset(sliderset, sliderset.ParentOSP.Filename)
        Catch ex As Exception
            _ExternalEditProcess = Nothing
            MsgBox(ex.Message, MsgBoxStyle.Exclamation, "Error")
        End Try

        If IsNothing(_ExternalEditProcess) Then
            _ExternalEditSlider = Nothing
            _ExternalEditFromTarget = False
            _ExternalEditLastOspWrite = Date.MinValue
            _ExternalEditActive = False
            _ExternalEditReloading = False
            Exit Sub
        End If


        _ExternalEditActive = True
        ExternalEditTimer.Start()
        Habilita_deshabilita()
    End Sub

    Private Sub ExternalEditTimer_Tick(sender As Object, e As EventArgs) Handles ExternalEditTimer.Tick
        If Not _ExternalEditActive Then
            ExternalEditTimer.Stop()
            Exit Sub
        End If

        If IsNothing(_ExternalEditSlider) Then
            EndExternalEditSession(False)
            Exit Sub
        End If

        If IsNothing(_ExternalEditProcess) Then
            EndExternalEditSession(True)
            Exit Sub
        End If

        Try
            If _ExternalEditProcess.HasExited Then
                EndExternalEditSession(True)
                Exit Sub
            End If
        Catch
            EndExternalEditSession(True)
            Exit Sub
        End Try

        Dim ospPath As String = _ExternalEditSlider.ParentOSP.Filename
        If IO.File.Exists(ospPath) = False Then Exit Sub

        Dim currentWrite As Date
        Try
            currentWrite = GetLatestExternalEditWriteTime(_ExternalEditSlider)
        Catch
            Exit Sub
        End Try

        If currentWrite > _ExternalEditLastOspWrite Then
            ReloadExternalEditedProject()
            If _ExternalEditReloading = False Then _ExternalEditLastOspWrite = currentWrite Else _ExternalEditReloading = False
        End If
    End Sub
    Private Shared Function Abre_Sliderset(sliderset As SliderSet_Class, OSP_Filename As String) As Process
        Dim second As String = Chr(34) + OSP_Filename + Chr(34)
        Dim first As String = Chr(34) + sliderset.Nombre + Chr(34)

        Dim Strat As New ProcessStartInfo With {
        .Arguments = ""
    }

        Strat.Arguments += "-proj" + " " + first + " " + second + " "
        Strat.Arguments = Strat.Arguments.Trim
        Strat.FileName = Config_App.Current.OSExePath

        Dim pr As Process = Process.Start(Strat)
        If Not IsNothing(pr) Then
            pr.EnableRaisingEvents = True
        End If
        Return pr
    End Function

    Private Async Sub RefreshButtoClick(sender As Object, e As EventArgs) Handles RefreshButton.Click
        If CheckBoxReloadDict.Checked Then
            Await Lee_Listbox()
        Else
            Await Refresh_OSP_Files_From_Disk()
        End If
    End Sub
    Private Function Full_packs_Selected() As Boolean
        If ListViewSources.SelectedIndices.Count = 0 Then Return False
        Dim selecteds As Integer = ListViewSources.SelectedIndices.Count
        Dim packs As Integer = ListViewSources.SelectedItems.Cast(Of ListViewItem).Select(Function(pf) CType(pf.Tag, SliderSet_Class).ParentOSP).Distinct.Select(Function(pf) pf.SliderSets.Count).Sum
        Return selecteds = packs
    End Function

    Private Sub NewPackButton_Click(sender As Object, e As EventArgs) Handles NewPackButton.Click
        Dim nombre = InputBox("Pack name", "New pack", "")
        nombre = nombre.Trim
        If nombre = "" Then Exit Sub
        Dim selected_pack = OSP_Project_Class.Create_New(Path.Combine(Directorios.SliderSetsRoot, nombre + ".osp"), False, True)
        If Not IsNothing(selected_pack) Then
            ComboboxPacks.Items.Add(selected_pack)
            ComboboxPacks.SelectedItem = selected_pack
        End If
    End Sub

    Public preview_Control As PreviewControl = Nothing
    Private Diccionario_Leido As Boolean = False
    Private Async Sub OSPManager_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        preview_Control = New PreviewControl With {.Dock = DockStyle.Fill}
        Panel_Preview_Container.Controls.Add(preview_Control)
        'SingleBoneCheck.Checked = preview_Control.Model.SingleBoneSkinning
        'RecalculateNormalsCheck.Checked = preview_Control.Model.RecalculateNormals
        Pone_checks()
        Dim initTask = InicializarAsync()
        preview_Control.ApplyResize(True)
        preview_Control.Model.Floor.Enabled = Config_App.Current.Settings_RenderGrid.Enabled
        preview_Control.Model.Floor.Color = Config_App.Current.RenderGridColor
        preview_Control.Model.Floor.Size = Config_App.Current.Settings_RenderGrid.Size
        preview_Control.Model.Floor.StepSize = Config_App.Current.Settings_RenderGrid.StepSize
        preview_Control.Model.Floor.Rebuild()
        AddHandler preview_Control.FloorToggled, Sub(s, enabled)
                                                     Config_App.Current.Settings_RenderGrid = New Config_App.RenderGridSettings With {
                                                         .Enabled = enabled,
                                                         .Size = Config_App.Current.Settings_RenderGrid.Size,
                                                         .StepSize = Config_App.Current.Settings_RenderGrid.StepSize
                                                     }
                                                 End Sub
        Await initTask
    End Sub
    Private Async Function InicializarAsync() As Task
        If firstime Then
            Me.Select()
            Application.DoEvents()
            Await Diccionario()
            Application.DoEvents()
            Me.Enabled = True
            firstime = False
            Await Lee_Listbox()

            'Dim yy As New StikyNote
            'yy.Show()

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
        RequestLeeShapes()
    End Sub


    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles ButtonPreviewSelected.Click
        ButtonPreviewSelected.FlatStyle = FlatStyle.Flat
        ButtonDataSheetSelected.FlatStyle = FlatStyle.Standard
        ListView2.Visible = False
        preview_Control.Visible = True
        RequestLeeShapes()
    End Sub
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles ButtonDataSheetSelected.Click
        ButtonPreviewSelected.FlatStyle = FlatStyle.Standard
        ButtonDataSheetSelected.FlatStyle = FlatStyle.Flat
        ListView2.Visible = True
        preview_Control.Visible = False
        RequestLeeShapes()
    End Sub

    Private Sub ComboBoxPresets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPresets.SelectedIndexChanged
        If ComboBoxPresets.SelectedIndex <> -1 Then
            Config_App.Current.Default_Preset = ComboBoxPresets.SelectedItem.ToString
        End If
        RequestLeeShapes()
    End Sub
    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboboxPacks.SelectedIndexChanged
        Lee_Listbox_Targets()
        RequestLeeShapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListViewTargets_GotFocus(sender As Object, e As EventArgs) Handles ListViewTargets.GotFocus
        Last_List_focused = ListViewTargets
        RequestLeeShapes()
        Habilita_deshabilita()
    End Sub

    Private Sub ListViewSources_GotFocus(sender As Object, e As EventArgs) Handles ListViewSources.GotFocus
        Last_List_focused = ListViewSources
        RequestLeeShapes()
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



    ' Debounce timer: fires RequestLeeShapes only after the user has stopped
    ' changing selection for _selectionDebounceMs milliseconds.
    ' WinForms Timer runs on the UI thread — no synchronization needed.
    Private WithEvents TselectionDebounceTimer As New System.Windows.Forms.Timer With {.Interval = 180}

    Private Sub ListViewSources_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListViewSources.SelectedIndexChanged
        TselectionDebounceTimer.Stop()
        TselectionDebounceTimer.Start() ' restart debounce window
    End Sub

    Private Sub ListViewTargets_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListViewTargets.SelectedIndexChanged
        TselectionDebounceTimer.Stop()
        TselectionDebounceTimer.Start()
    End Sub

    Private Sub SelectionDebounceTimer_Tick(sender As Object, e As EventArgs) Handles TselectionDebounceTimer.Tick
        TselectionDebounceTimer.Stop() ' one-shot

        RequestLeeShapes()
        Habilita_deshabilita()
    End Sub

    Private Sub RenameButton_Click(sender As Object, e As EventArgs) Handles RenameButton.Click
        Empieza_Procesos(1)
        If MsgBox("Are you sure you want to rename " + ListViewTargets.SelectedIndices.Count.ToString + " items?", vbYesNo, "Confirm") = MsgBoxResult.No Then
            Termina_Procesos()
            Exit Sub
        End If
        Dim Source As SliderSet_Class = ListViewTargets.SelectedItems(0).Tag
        Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem
        Rename_Clone_Target(Source, Selected_Pack, TextBox_TargetName.Text, True)
        Termina_Procesos()
    End Sub

    Private Async Sub ExtractSingleButton_Click(sender As Object, e As EventArgs) Handles ExtractSingleButton.Click
        Empieza_Procesos(ListViewTargets.SelectedItems.Count)
        If MsgBox("Are you sure you want to extract " + ListViewTargets.SelectedIndices.Count.ToString + " items?", vbYesNo, "Confirm") = MsgBoxResult.Yes Then
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
                Dim selected_pack = OSP_Project_Class.Create_New(Path.Combine(Directorios.SliderSetsRoot, nombre + ".osp"), False, False)
                If Not IsNothing(selected_pack) Then
                    Extract_Target(Source, selected_pack, nombre)
                End If
                ProgressBar1.Value += 1
            Next
            Await Refresh_OSP_Files_From_Disk()
        End If
        Termina_Procesos()
    End Sub

    Private Sub MergeIntoTargetButton_Click(sender As Object, e As EventArgs) Handles MergeIntoTargetButton.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count * ListViewTargets.SelectedItems.Count)
        OSP_Project_Class.Default_Memory_Pause = True
        If MsgBox("Are you sure you want to merge " + ListViewSources.SelectedIndices.Count.ToString + " items into " + ListViewTargets.SelectedIndices.Count.ToString + " items in category " + ComboboxPacks.Items(ComboboxPacks.SelectedIndex).ToString + "?", vbYesNo, "Confirm") = MsgBoxResult.Yes Then
            Dim selected_target As SliderSet_Class = Determina_Seleccionado_y_CambiaNombres(1)
            If IsNothing(selected_target) Then
                OSP_Project_Class.Default_Memory_Pause = False
                Termina_Procesos()
                Exit Sub
            End If
            For Each it In ListViewTargets.SelectedItems
                Dim target As SliderSet_Class = it.Tag
                OSP_Project_Class.Load_and_Check_Shapedata(target, True)
                Merge_Part(target, Nothing, False)
            Next
            selected_target.Reload(DeepAnalize_check.Checked)
            If preview_Control.Model.Last_rendered Is selected_target Then
                preview_Control.Model.Last_rendered = Nothing
            End If
        End If
        OSP_Project_Class.Default_Memory_Pause = False
        Termina_Procesos()
        RequestLeeShapes(True)
    End Sub

    Private Sub CloneButton_Click(sender As Object, e As EventArgs) Handles CloneButton.Click
        Empieza_Procesos(ListViewTargets.SelectedIndices.Count)
        If MsgBox("Are you sure you want to clone " + ListViewTargets.SelectedIndices.Count.ToString + " items?", vbYesNo, "Confirm") = MsgBoxResult.Yes Then
            For Each it In ListViewTargets.SelectedItems
                Dim Source As SliderSet_Class = it.Tag
                Dim nombre As String
                Dim Selected_Pack As OSP_Project_Class = ComboboxPacks.SelectedItem

                If ListViewTargets.SelectedIndices.Count = 1 Then
                    nombre = TextBox_TargetName.Text
                    If Selected_Pack.SliderSets.Any(Function(pf) pf.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)) Then
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
        If preview_Control IsNot Nothing Then
            preview_Control.Clean()
            preview_Control.Dispose()
        End If
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
                Dim fil = projecto.OutputFullPathBase
                If fil.EndsWith(".nif", StringComparison.OrdinalIgnoreCase) = False Then fil += ".nif"
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

        If IsNothing(ComboBoxPresets.SelectedItem) Then
            MsgBox("Please select a preset before building.", vbOKOnly + vbExclamation, "No Preset Selected")
            Exit Sub
        End If
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
        Dim proc = Process.Start(Strat)
        If proc IsNot Nothing Then proc.WaitForExit()

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

            Using writer = IO.File.CreateText(TempGroupFile)
                writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")

                writer.WriteLine("<SliderGroups>")
                writer.WriteLine("<Group name =" + Chr(34) + "Temp_WM_Builder" + Chr(34) + ">")
                For Each out In Otufits
                    writer.WriteLine("<Member name =" + Chr(34) + out + Chr(34) + "/>")
                Next
                writer.WriteLine("</Group>")
                writer.WriteLine("</SliderGroups>")
                writer.Flush()
            End Using

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
        If IsNothing(sliderset_Source) Then
            Termina_Procesos()
            Exit Sub
        End If
        If sliderset_Source.Unreadable_NIF Or sliderset_Source.Unreadable_Project Then
            MsgBox("The project is unreadable.", vbOKOnly + vbCritical, "Error")
            Termina_Procesos()
            Exit Sub
        End If
        Open_Editor(sliderset_Source, False)
    End Sub
    Private Sub ButtonEditInternally_Click(sender As Object, e As EventArgs) Handles ButtonEditInternally.Click
        Empieza_Procesos(1)
        Dim sliderset_target = Determina_Seleccionado_y_CambiaNombres(1)
        If IsNothing(sliderset_target) Then
            Termina_Procesos()
            Exit Sub
        End If
        If sliderset_target.Unreadable_NIF Or sliderset_target.Unreadable_Project Then
            MsgBox("The project is unreadable.", vbOKOnly + vbCritical, "Error")
            Termina_Procesos()
            Exit Sub
        End If
        Open_Editor(sliderset_target, True)
    End Sub
    Private Sub Open_Editor(selected As SliderSet_Class, Grabable As Boolean)
        Dim Editor As New Editor_Form
        Dim presetName As String = If(ComboBoxPresets.SelectedItem?.ToString, "")
        Dim poseName As String = If(ComboBoxPoses.SelectedItem?.ToString, "")
        Editor.Lee_Edit(selected, presetName, poseName)
        Editor.Grabable = Grabable
        Editor.SingleBoneCheck.Checked = SingleBoneCheck.Checked
        Editor.RecalculateNormalsCheck.Checked = RecalculateNormalsCheck.Checked
        Dim resultado = Editor.ShowDialog(Me)
        If resultado <> DialogResult.Abort Then
            If preview_Control.Model.Last_rendered Is selected Then
                preview_Control.Model.Clean(False)
                preview_Control.Model.CleanTextures()
            End If
            selected.Reload(DeepAnalize_check.Checked)
            Relee_Presets()
            Relee_Poses()
            Termina_Procesos()
            RequestLeeShapes(True)
        Else
            Termina_Procesos()
            preview_Control.RefreshRender()
        End If
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

    Private Sub Button4_Click_2(sender As Object, e As EventArgs) Handles ButtonOpenConfig.Click
        Empieza_Procesos(0)
        Dim old_game = Config_App.Current.Game
        Dim old_Data = Config_App.Current.FO4EDataPath
        Dim old_OS = Config_App.Current.OSExePath
        Dim old_BS = Config_App.Current.BSExePath
        Dim old_SK = Config_App.Current.SkeletonPath

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
        If Not IsNothing(preview_Control) AndAlso Not IsNothing(preview_Control.Model) AndAlso Not IsNothing(preview_Control.Model.Floor) Then
            preview_Control.Model.Floor.Enabled = Config_App.Current.Settings_RenderGrid.Enabled
            preview_Control.Model.Floor.Color = Config_App.Current.RenderGridColor
            preview_Control.Model.Floor.Size = Config_App.Current.Settings_RenderGrid.Size
            preview_Control.Model.Floor.StepSize = Config_App.Current.Settings_RenderGrid.StepSize
            preview_Control.Model.Floor.Rebuild()
        End If
        Dim reproces_dic As Boolean = False
        If old_game <> Config_App.Current.Game Then reproces_dic = True
        If old_Data <> Config_App.Current.FO4EDataPath Then reproces_dic = True
        If old_OS <> Config_App.Current.OSExePath Then reproces_dic = True
        If old_BS <> Config_App.Current.BSExePath Then reproces_dic = True
        If old_SK <> Config_App.Current.SkeletonPath Then reproces_dic = True
        CheckBoxReloadDict.Checked = reproces_dic
        Termina_Procesos()
        RefreshButton.PerformClick()
    End Sub

    Private Sub ButtonDeleteSource_Click(sender As Object, e As EventArgs) Handles ButtonDeleteSource.Click
        Empieza_Procesos(ListViewSources.SelectedItems.Count * 2)
        If MsgBox("Are you sure you want to delete " + ListViewSources.SelectedIndices.Count.ToString + " items?", vbCritical + vbYesNo, "Confirm") = MsgBoxResult.Yes Then
            Dim toremove(ListViewSources.SelectedItems.Count - 1) As ListViewItem
            ListViewSources.SelectedItems.CopyTo(toremove, 0)
            For Each it In toremove
                Dim sliderset_Source As SliderSet_Class = it.Tag
                ProgressBar1.Value += 1
                sliderset_Source.ParentOSP.RemoveProject(sliderset_Source)
                If sliderset_Source.ParentOSP.SliderSets.Count = 0 Then
                    If sliderset_Source.ParentOSP.IsManoloPack = False Then
                        IO.File.Delete(sliderset_Source.ParentOSP.Filename)
                    Else
                        Remove_Empty_Pack(sliderset_Source)
                    End If
                End If
                ListViewSources.Items.Remove(it)
            Next
            RequestLeeShapes(True)
        End If
        Termina_Procesos()
    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles SingleBoneCheck.CheckedChanged
        If IsNothing(preview_Control) Then Exit Sub
        preview_Control.Model.SingleBoneSkinning = SingleBoneCheck.Checked
        Config_App.Current.Setting_SingleBoneSkinning = SingleBoneCheck.Checked
        ComboBoxPoses.Enabled = Not SingleBoneCheck.Checked
        preview_Control.Model.Clean(False)
        RequestLeeShapes()
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

    Private Sub Button1_Click_2(sender As Object, e As EventArgs) Handles ButtonCreateFromNif.Click
        Dim dict_used As FilesDictionary_class.DictionaryFilePickerConfig = FilesDictionary_class.ALLMeshesDictionary_Filter
        Dim filtered = FilesDictionary_class.GetFilteredKeys(dict_used)
        Dim initialKey As String = ""

        Using frm As New Create_from_Nif_Form(filtered, dict_used.RootPrefix, dict_used.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.Yes Then
                RefreshButton.PerformClick()
            Else
                preview_Control.RefreshRender()
            End If
        End Using
    End Sub

    Private Sub ComboBoxPoses_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxPoses.SelectedIndexChanged
        RequestLeeShapes()
    End Sub

    Private Sub ButtonSkeleton_Click(sender As Object, e As EventArgs) Handles ButtonSkeleton.Click
        Dim dict_used As FilesDictionary_class.DictionaryFilePickerConfig = FilesDictionary_class.ALLMeshesDictionary_Filter
        Dim filtered = FilesDictionary_class.GetFilteredKeys(dict_used)
        Dim initialKey As String = IO.Path.GetRelativePath(Directorios.Fallout4data, Directorios.SkeletonPath)
        Using frm As New DictionaryFilePicker_Form(filtered, dict_used.RootPrefix, dict_used.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.OK Then
                Config_App.Current.SkeletonPath = IO.Path.Combine(Directorios.Fallout4data, frm.DictionaryPicker_Control1.SelectedKey)
                Skeleton_Class.LoadSkeleton(True, True)
                Habilita_deshabilita()
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
        preview_Control.Model.Clean(False)
        RequestLeeShapes()
    End Sub

    Private Sub Wardrobe_Manager_Form_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
#If DEBUG Then
        If e.KeyValue = Keys.F1 Then preview_Control.CurrentShader.Debugmode = 0
        If e.KeyValue = Keys.F2 Then preview_Control.CurrentShader.Debugmode = 1
        If e.KeyValue = Keys.F3 Then preview_Control.CurrentShader.Debugmode = 2
        If e.KeyValue = Keys.F4 Then preview_Control.CurrentShader.Debugmode = 3
        If e.KeyValue = Keys.F5 Then preview_Control.CurrentShader.Debugmode = 4
        preview_Control.updateRequired = True
#End If
    End Sub


    Private Sub Button5_Click_1(sender As Object, e As EventArgs) Handles ButtonRightPanel.Click
        Split_Principal2.Panel2Collapsed = Not Split_Principal2.Panel2Collapsed
        If Split_Principal2.Panel2Collapsed Then
            ButtonRightPanel.ImageIndex = 14
        Else
            ButtonRightPanel.ImageIndex = 15
        End If

    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles ButtonLeftPanel.Click
        SplitPrincipal_1.Panel1Collapsed = Not SplitPrincipal_1.Panel1Collapsed
        If SplitPrincipal_1.Panel1Collapsed Then
            ButtonLeftPanel.ImageIndex = 14
        Else
            ButtonLeftPanel.ImageIndex = 15
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
        ListViewSources.Columns(_Sourcesort).Text += IIf(ListViewSources.Sorting = SortOrder.Ascending, " (↓)", " (↑)")
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
        ListViewTargets.Columns(_TargetSort).Text += IIf(ListViewTargets.Sorting = SortOrder.Ascending, " (↓)", " (↑)")
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

            ' Intentar comparar como n�mero
            ' Comparar como texto
            compareResult = String.Compare(itemX, itemY)

            If order = SortOrder.Descending Then
                compareResult = -compareResult
            End If

            Return compareResult
        End Function
    End Class

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles ButtonLightRigSettings.Click
        Dim lightfirn As New LightRigForm
        lightfirn.ShowDialog(Me)
    End Sub

    Private Sub ComboBox1_SelectedIndexChanged_1(sender As Object, e As EventArgs) Handles ComboBoxSize.SelectedIndexChanged
        If ComboBoxPresets.SelectedIndex <> -1 Then
            Config_App.Current.Bodytipe = ComboBoxSize.SelectedIndex
        End If
        RequestLeeShapes()
    End Sub


End Class



