' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Xml
Imports K4os.Hash.xxHash
Imports NiflySharp
Imports NiflySharp.Blocks

Public Class Create_from_Nif_Form
    Private WithEvents EditPreviewControl As PreviewControl = Nothing
    Private _initializingUI As Boolean = False
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
        Me.DictionaryPicker_Control1.AllowClone = True
        Me.DictionaryPicker_Control1.btnOk.Text = "Create"
        Me.DictionaryPicker_Control1.btnOk.Font = New Font(Me.DictionaryPicker_Control1.btnOk.Font, FontStyle.Bold)
        Me.DictionaryPicker_Control1.btnCancel.Text = "Exit"
    End Sub


    Private Sub Create_from_Nif_2_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        ' El checkbox refleja el ajuste persistido; el flag evita que este seteo dispare el handler
        ' (que reescribiria el config y forzaria un re-read del nif antes de haber seleccionado nada).
        _initializingUI = True
        chkAutoConvert.Checked = WM_Config.Current.Setting_AutoConvertNif
        _initializingUI = False
        ' Solo aplica al camino LE -> SE; en Fallout 4 no hay conversion automatica que ofrecer.
        chkAutoConvert.Enabled = (Config_App.Current.Game = Config_App.Game_Enum.Skyrim)

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
            ' SSE: sincronizar el link in-NIF de física HDT-SMP con el sidecar del proyecto nuevo y escribir
            ' el sidecar (mismo modelo que Save_Shapedatas / HH_OFFSET-.hht). El path se ajusta ANTES de guardar.
            Dim smpXmlPath As String = Nothing
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso Not String.IsNullOrEmpty(selected_slider.PhysicsXmlContent) Then
                smpXmlPath = Path.ChangeExtension(New_Nif, ".xml")
                selected_slider.NIFContent.SetSmpPhysicsXmlPath(SliderSet_Class.BuildSmpInNifPath(smpXmlPath))
            End If
            selected_slider.NIFContent.Save_As_Manolo(New_Nif, False)
            If smpXmlPath IsNot Nothing Then
                File.WriteAllText(smpXmlPath, selected_slider.PhysicsXmlContent, System.Text.Encoding.UTF8)
            End If
            ' ⛔ Si el .osd NO se escribió (el usuario dijo "No" al reemplazar uno preexistente), NO se
            ' puede grabar el .osp: sus <Data> ya nombran New_osd (arriba, línea del TargetOsd), así que
            ' el proyecto nuevo quedaría heredando en silencio los morphs del proyecto VIEJO que dejó
            ' ese archivo ahí. Queda un .nif huérfano en ShapeData (se escribió unas líneas más arriba);
            ' es mucho menos malo que un proyecto con morphs ajenos, y el usuario ve el error.
            If Not selected_slider.OSDContent_Local.Save_As(New_osd, False) Then
                Throw New Exception("The osd file was not written, project not created: " & New_osd)
            End If
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
                stripped = String.Concat(fil.AsSpan(0, fil.Length - "_0.nif".Length), ".tri")
            ElseIf fil.EndsWith("_1.nif", StringComparison.OrdinalIgnoreCase) Then
                stripped = String.Concat(fil.AsSpan(0, fil.Length - "_1.nif".Length), ".tri")
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

                ' Solo soportado: Skyrim LE -> Skyrim SE. Con "Auto-convert" encendido no se pregunta:
                ' el prompt salia en CADA seleccion del picker, no una vez por sesion (MsgBox no tiene
                ' "Yes to all"), asi que la decision vive en el checkbox y se persiste en wm_config.json.
                If ver.IsSK Then
                    Dim doOptimize As Boolean = chkAutoConvert.Checked
                    If Not doOptimize Then
                        doOptimize = (MsgBox("Current nif is Skyrim LE. Try to optimize it to Skyrim SE?", MsgBoxStyle.Information Or MsgBoxStyle.YesNo, "Warning") = MsgBoxResult.Yes)
                    End If
                    If doOptimize Then
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

            ' SSE: detectar física HDT-SMP desde el link in-NIF autoritativo (mismo resolver que la carga
            ' normal). Sin esto, HasPhysics queda en False aunque el NIF traiga SMP.
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                selected_slider.PhysicsXmlContent = SmpPhysicsXml.ResolverXmlDeFisica(selected_slider.NIFContent, Nothing, Wardrobe_Manager_Form.Directorios.Fallout4data)
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
                            Dim esUv As Boolean = (morp.MorphType = FO4_Base_Library.TriMorphType.UV)
                            Dim existente = selected_slider.Sliders.FirstOrDefault(
                                Function(pf) pf.Nombre.Equals(morp.Name, StringComparison.OrdinalIgnoreCase))
                            If existente Is Nothing Then
                                selected_slider.Sliders.Add(New Slider_class(morp.Name, selected_slider, morp.MorphType))
                                selected_slider.NotifySlidersChanged()
                            ElseIf existente.IsUV <> esUv Then
                                ' El .tri trae el MISMO nombre en las dos secciones (posicion Y uv). El motor de
                                ' SSE si soporta el par — BodyMorphMap es unordered_map<nombre, pair<pos, uv>>
                                ' (skee64 BodyMorphInterface.h) — pero el modelo del .osp NO: un slider es
                                ' posicion O uv (Slider_class.IsUV, y MorphingHelper.ResolveSlider devuelve UN
                                ' SliderKind), y dos sliders con el mismo nombre revientan MorphDiffs, que se
                                ' indexa por nombre (MorphingHelper.LoadMorphTargets: shape.MorphDiffs.Add).
                                ' Si el match fuera SOLO por nombre, la segunda entrada se colgaría del slider
                                ' de la PRIMERA y sus deltas se aplicarían con el tipo equivocado — deltas de UV
                                ' sumados a POSICIONES, que es justo lo que deforma la malla.
                                ' Se conserva la primera y se descarta la segunda, avisando: perder un canal es
                                ' recuperable, aplicarlo como el otro tipo no.
                                Logger.LogLazy(Function() $"[TRI2OSD] shape='{shapeMorph.Key}' morph='{morp.Name}': el .tri lo declara como posicion Y como uv; el modelo del .osp no admite las dos. Se conserva {If(existente.IsUV, "UV", "POSICION")} y se descarta {If(esUv, "UV", "POSICION")}.")
                                Continue For
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
#If DEBUG Then
                    Debugger.Break()
#End If
                End Try
            End If

            selected_slider.Unreadable_NIF = False
            selected_slider.ShapeDataLoaded = True
            selected_slider.InvalidateShapeDataLookupCache()
            selected_slider.RebuildShapeDataLookupCache()

        Catch ex As Exception
#If DEBUG Then
            Debugger.Break()
#End If
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

    Private Sub ChkAutoConvert_CheckedChanged(sender As Object, e As EventArgs) Handles chkAutoConvert.CheckedChanged
        If _initializingUI Then Exit Sub
        WM_Config.Current.Setting_AutoConvertNif = chkAutoConvert.Checked
        ' Re-leer en los DOS sentidos, igual que los otros checks de la barra. Sin esto, destildar no
        ' preguntaba nada: el picker no vuelve a disparar SelectionChanged si se reclica el mismo item,
        ' asi que el prompt reactivado no aparecia hasta elegir OTRO nif.
        If Last_key <> "" Then Read_selected(Last_key)
    End Sub


End Class