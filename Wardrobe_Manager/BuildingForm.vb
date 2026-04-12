' Version Uploaded of Wardrobe 3.2.0

Imports System.Threading.Tasks
Imports Wardrobe_Manager.Wardrobe_Manager_Form

Public Class BuildingForm

    Private ReadOnly _Lista() As SliderSet_Class
    Private ReadOnly _Preset As SlidersPreset_Class
    Private ReadOnly _Pose As Poses_class

    Sub New(Que() As SliderSet_Class, Preset As SlidersPreset_Class, Pose As Poses_class)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        _Lista = Que
        _Preset = Preset
        _Pose = Pose
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    Private Sub BuildingForm_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        ProgressBar1.Value = 0
        ProgressBar2.Value = 0
        ProgressBar1.Maximum = 5
        ProgressBar2.Maximum = _Lista.Length * 2
        Dim DummyOSP As New OSP_Project_Class
        Dim Errores As String = ""
        Dim Nombre As String = "Unknown"
        ' Lee los sliders de looksmenu si se graba tri
        If WM_Config.Current.Settings_Build.SaveTri Then LooksMenuSliders.Read_Looksmenu_Sliders()
        OSP_Project_Class.Default_Memory_Pause = True
        ' Context unico y compartido para todo el batch de builds. Acumulamos los
        ' issues de load en effectiveContext.Issues y al final disparamos un solo
        ' ShowLoadIssuesDialog con la lista agregada, en vez de N popups individuales.
        Dim buildLoadContext = ProjectLoadContext.CreateCollectOnly(False)
        Dim has_pose = (WM_Config.Current.Settings_Build.BuildInPose AndAlso _Pose.Source <> Poses_class.Pose_Source_Enum.None)
        For Each sliderset_target In _Lista
            Try
                Dim NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                Dim builder As New SliderSet_Class(NodoClone, DummyOSP)
                ' Shapedata loaded on the builder clone below, not on sliderset_target
                Dim size As WM_Config.SliderSize = WM_Config.SliderSize.Default
                For Sizecount = 0 To CInt(IIf(sliderset_target.Multisize, 1, 0))
                    ProgressBar1.Value = 0
                    ProgressBar1.Maximum = (builder.Shapes.Count * 4 + 6)
                    If OSP_Project_Class.Load_and_CHeck_Project(builder, buildLoadContext) = False OrElse OSP_Project_Class.Load_and_Check_Shapedata(builder, buildLoadContext) = False Then Throw New InvalidOperationException("Could not load shape data for build.")
                    ProgressBar1.Value += 1
                    builder.HighHeelHeight = sliderset_target.HighHeelHeight
                    Skeleton_Class.PrepareSkeletonForShapes(builder.Shapes, If(has_pose, _Pose, Nothing))
                    ProgressBar1.Value += 1

                    Dim fil = builder.OutputFullPathBase + If(sliderset_target.Multisize, "_" + Sizecount.ToString, "") + ".nif"
                    Dim tri = builder.OutputFullPathBase + ".tri"
                    Dim Tridata = IO.Path.GetRelativePath(IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, "Meshes")), tri)
                    Dim dir = IO.Path.GetDirectoryName(fil)
                    Nombre = sliderset_target.Nombre
                    Label1.Text = "Building: " + Nombre + IIf(sliderset_target.Multisize(), "_" + Sizecount.ToString, "")
                    Application.DoEvents()
                    If Sizecount = 0 Then size = WM_Config.SliderSize.Small
                    If Sizecount = 1 Then size = WM_Config.SliderSize.Big
                    ' 0 - cargo morph
                    builder.SetPreset(_Preset, size)
                    ProgressBar1.Value += 1
                    ' --- O6.1: Parallel shape processing (compute-heavy part) ---
                    Dim shapeList = builder.Shapes.ToList
                    Dim shapeResults As New System.Collections.Concurrent.ConcurrentDictionary(Of Shape_class, SkinnedGeometry)
                    Dim localHasPose = has_pose
                    Dim localSingleBone = Config_App.Current.Setting_SingleBoneSkinning
                    Dim localRecalcNormals = Config_App.Current.Setting_RecalculateNormals

                    ' Phase 1: parallel compute (Extract + Morph + Bake/InjectToTrishape)
                    Parallel.ForEach(shapeList.Where(Function(s) s.RelatedNifShape IsNot Nothing),
                        Sub(shap)
                            ' 1- cargo geometria
                            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shap, ApplyPose:=localHasPose, singleboneskinning:=localSingleBone, RecalculateNormals:=False)
                            ' 3- aplico morph (y recalculo normales si esta elegido)
                            MorphingHelper.ApplyMorph_CPU(shap, geom, localRecalcNormals, AllowMask:=False)
                            ' 4- Borro zaps y revierto bakeo (includes InjectToTrishape per-shape)
                            SkinningHelper.BakeFromMemoryUsingOriginal(shap, geom, ApplyPose:=localHasPose, inverse:=False, ApplyMorph:=True, RemoveZaps:=True, singleBoneSkinning:=localSingleBone, geometryModifier:=New ZapGeometryModifier())
                            shapeResults(shap) = geom
                        End Sub)

                    ' Phase 2: sequential NIF structure updates + progress
                    For Each shap In shapeList
                        If shap.RelatedNifShape IsNot Nothing Then
                            Dim geom As SkinnedGeometry = Nothing
                            If shapeResults.TryGetValue(shap, geom) Then
                                ProgressBar1.Value += 3 ' account for extract+morph+bake steps
                                If builder.KeepZappedShapes = False AndAlso geom.Vertices.Length = 0 Then
                                    builder.RemoveShape(shap)
                                    ProgressBar1.Value += 1
                                Else
                                    builder.NIFContent.UpdateSkinPartitions(shap.RelatedNifShape)
                                    ProgressBar1.Value += 1
                                End If
                            Else
                                ' Shape was filtered or failed silently in the parallel phase
                                Debugger.Break()
                                ProgressBar1.Value += 4
                            End If
                        Else
                            ProgressBar1.Value += 4
                        End If
                    Next

                    If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

                    ' Grabo bloque tri si hace falta
                    If WM_Config.Current.Settings_Build.SaveTri AndAlso (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri) Then
                        builder.NIFContent.AddTriData("", Tridata, True)
                    Else
                        builder.NIFContent.RemoveTriData("", True)
                    End If

                    ' High Heels
                    Dim hhResult = builder.SaveHighHeelBuild(builder.NIFContent)
                    If hhResult.HasValue Then
                        Dim hhRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, builder.OutputFullPathBase & ".txt").Correct_Path_Separator
                        If hhResult.Value Then
                            FilesDictionary_class.AddOrUpdateDictionaryEntry(hhRelative, New FilesDictionary_class.File_Location With {
                                .BA2File = "", .Index = -1, .FullPath = hhRelative, .FileDate = Date.Now})
                        Else
                            FilesDictionary_class.RemoveDictionaryEntry(hhRelative)
                        End If
                    End If
                    ProgressBar1.Value += 1


                    ' Grabo nif
                    builder.NIFContent.Save_As_Manolo(fil, True)
                    Dim nifRelative As String = IO.Path.GetRelativePath(Directorios.Fallout4data, fil).Correct_Path_Separator
                    FilesDictionary_class.AddOrUpdateDictionaryEntry(nifRelative, New FilesDictionary_class.File_Location With {
                        .BA2File = "", .Index = -1, .FullPath = nifRelative, .FileDate = Date.Now})

                    ProgressBar1.Value += 1



                    If Sizecount = 0 Then
                        ' Grabo archivo tri
                        If WM_Config.Current.Settings_Build.SaveTri AndAlso (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri) Then
                            LooksMenuSliders.WriteMorphTRI(tri, builder)
                            Dim triRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, tri).Correct_Path_Separator
                            FilesDictionary_class.AddOrUpdateDictionaryEntry(triRelative, New FilesDictionary_class.File_Location With {
                                .BA2File = "", .Index = -1, .FullPath = triRelative, .FileDate = Date.Now})
                        End If
                        ' SSE: copia o borra XML de física HDT-SMP junto al NIF de salida (una sola vez, no depende del size)
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                            Dim outXml = builder.OutputFullPathBase + ".xml"
                            Dim xmlRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, outXml).Correct_Path_Separator
                            If Not String.IsNullOrEmpty(builder.PhysicsXmlContent) Then
                                IO.File.WriteAllText(outXml, builder.PhysicsXmlContent, System.Text.Encoding.UTF8)
                                FilesDictionary_class.AddOrUpdateDictionaryEntry(xmlRelative, New FilesDictionary_class.File_Location With {
                                    .BA2File = "", .Index = -1, .FullPath = xmlRelative, .FileDate = Date.Now})
                            ElseIf IO.File.Exists(outXml) Then
                                IO.File.Delete(outXml)
                                FilesDictionary_class.RemoveDictionaryEntry(xmlRelative)
                            End If
                        End If
                    End If
                    ProgressBar1.Value += 1

                    Nombre = "Unknown"
                    ProgressBar2.Value += IIf(sliderset_target.Multisize, 1, 2)
                Next
                'ComparadorTrip.CompararArchivos(tri, tri.Replace("_WM.tri", "_WM.tri2"))
            Catch ex As Exception
                If Errores <> "" Then Errores += ","
                Errores += Nombre + vbCrLf
            End Try

        Next
        OSP_Project_Class.Default_Memory_Pause = False
        ' Grabo archivo sliders.json
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 AndAlso WM_Config.Current.Settings_Build.AddAddintionalSliders AndAlso WM_Config.Current.Settings_Build.SaveTri Then LooksMenuSliders.Serialize_LooksmenuAdditionalSiliders()

        ' Mostrar los issues de load acumulados durante todo el batch en un solo dialog
        If buildLoadContext.Issues IsNot Nothing AndAlso buildLoadContext.Issues.Count > 0 Then
            Dim batchHandler = OSP_Project_Class.InteractiveIssueBatchDisplay
            If batchHandler IsNot Nothing Then
                Try
                    batchHandler.Invoke(buildLoadContext.Issues)
                Catch
                End Try
            End If
        End If

        If Errores <> "" Then
            MsgBox("Error building the following projects:" + Errores)
        End If
        Me.Close()
    End Sub


End Class
