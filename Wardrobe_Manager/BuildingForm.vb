' Version Uploaded of Wardrobe 2.1.3

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
        If Config_App.Current.Settings_Build.SaveTri Then TriFile.Read_Looksmenu_Sliders()
        OSP_Project_Class.Default_Memory_Pause = True
        Dim has_pose = (Config_App.Current.Settings_Build.BuildInPose AndAlso _Pose.Source <> Poses_class.Pose_Source_Enum.None)
        For Each sliderset_target In _Lista
            Try
                Dim NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                Dim builder As New SliderSet_Class(NodoClone, DummyOSP)
                ' Shapedata loaded on the builder clone below, not on sliderset_target
                Dim size As Config_App.SliderSize = Config_App.SliderSize.Default
                For Sizecount = 0 To CInt(IIf(sliderset_target.Multisize, 1, 0))
                    ProgressBar1.Value = 0
                    ProgressBar1.Maximum = (builder.Shapes.Count * 4 + 6)
                    OSP_Project_Class.Load_and_CHeck_Project(builder)
                    ProgressBar1.Value += 1
                    OSP_Project_Class.Load_and_Check_Shapedata(builder, True)
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
                    If Sizecount = 0 Then size = Config_App.SliderSize.Small
                    If Sizecount = 1 Then size = Config_App.SliderSize.Big
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
                            SkinningHelper.BakeFromMemoryUsingOriginal(shap, geom, ApplyPose:=localHasPose, inverse:=False, ApplyMorph:=True, RemoveZaps:=True, singleBoneSkinning:=localSingleBone)
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
                    If Config_App.Current.Settings_Build.SaveTri AndAlso (builder.PreventMorphFile = False OrElse Config_App.Current.Settings_Build.IgnorePreventri) Then
                        builder.NIFContent.AddTriData("", Tridata, True)
                    Else
                        builder.NIFContent.RemoveTriData("", True)
                    End If

                    ' High Heels
                    builder.SaveHighHeelBuild(builder.NIFContent)
                    ProgressBar1.Value += 1


                    ' Grabo nif 
                    builder.NIFContent.Save_As_Manolo(fil, True)
                    ProgressBar1.Value += 1



                    If Sizecount = 0 Then
                        ' Grabo archivo tri
                        If Config_App.Current.Settings_Build.SaveTri AndAlso (builder.PreventMorphFile = False OrElse Config_App.Current.Settings_Build.IgnorePreventri) Then TriFile.WriteMorphTRI(tri, builder)
                        ' SSE: copia o borra XML de física HDT-SMP junto al NIF de salida (una sola vez, no depende del size)
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                            Dim outXml = builder.OutputFullPathBase + ".xml"
                            If Not String.IsNullOrEmpty(builder.PhysicsXmlContent) Then
                                IO.File.WriteAllText(outXml, builder.PhysicsXmlContent, System.Text.Encoding.UTF8)
                            ElseIf IO.File.Exists(outXml) Then
                                IO.File.Delete(outXml)
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
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 AndAlso Config_App.Current.Settings_Build.AddAddintionalSliders AndAlso Config_App.Current.Settings_Build.SaveTri Then TriFile.Serialize_LooksmenuAdditionalSiliders()
        If Errores <> "" Then
            MsgBox("Error building the following projects:" + Errores)
        End If
        Me.Close()
    End Sub


End Class