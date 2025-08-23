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

        Dim has_pose = (Config_App.Current.Settings_Build.BuildInPose AndAlso _Pose.Source <> Poses_class.Pose_Source_Enum.None)
        If has_pose Then Skeleton_Class.AppplyPoseToSkeleton(_Pose)
        For Each sliderset_target In _Lista
            Try
                Dim NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                Dim builder As New SliderSet_Class(NodoClone, DummyOSP)
                OSP_Project_Class.Load_and_Check_Shapedata(sliderset_target)

                Dim size As Config_App.SliderSize = Config_App.SliderSize.Default
                For Sizecount = 0 To CInt(IIf(sliderset_target.Multisize, 1, 0))
                    ProgressBar1.Value = 0
                    ProgressBar1.Maximum = (builder.Shapes.Count * 5 + 5)
                    OSP_Project_Class.Load_and_CHeck_Project(builder, False)
                    ProgressBar1.Value += 1
                    OSP_Project_Class.Load_and_Check_Shapedata(builder)
                    builder.HighHeelHeight = sliderset_target.HighHeelHeight
                    ProgressBar1.Value += 1

                    Dim fil = IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, builder.OutputPathValue), builder.OutputFileValue) + IIf(sliderset_target.Multisize, "_" + Sizecount.ToString, "") + ".nif"
                    Dim tri = IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, builder.OutputPathValue), builder.OutputFileValue) + ".tri"
                    Dim Tridata = IO.Path.GetRelativePath(IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, "Meshes")), tri)
                    Dim dir = IO.Path.GetDirectoryName(fil)
                    Nombre = sliderset_target.Nombre
                    Label1.Text = "Building: " + Nombre + IIf(sliderset_target.Multisize(), "_" + Sizecount.ToString, "")
                    Application.DoEvents()
                    If Sizecount = 0 Then size = Config_App.SliderSize.Small
                    If Sizecount = 1 Then size = Config_App.SliderSize.Big
                    For Each shap In builder.Shapes.ToList
                        If Not IsNothing(shap.RelatedNifShape) Then
                            ' 1- cargo geometria 
                            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shap, ApplyPose:=has_pose, singleboneskinning:=Config_App.Current.Setting_SingleBoneSkinning, RecalculateNormals:=False) ' Los normales los calculo despues
                            ProgressBar1.Value += 1
                            ' 2- cargo morph
                            builder.SetPreset(_Preset, size)
                            ProgressBar1.Value += 1
                            ' 3- aplico morph (y recalculo normales si esta elegido)
                            MorphingHelper.ApplyMorph_CPU(shap, geom, Config_App.Current.Setting_RecalculateNormals, AllowMask:=False)
                            ProgressBar1.Value += 1
                            ' 4- Borro zaps y revierto bakeo
                            SkinningHelper.BakeFromMemoryUsingOriginal(shap, geom, ApplyPose:=has_pose, inverse:=False, ApplyMorph:=True, RemoveZaps:=True, singleBoneSkinning:=Config_App.Current.Setting_SingleBoneSkinning)
                            ProgressBar1.Value += 1

                            If builder.KeepZappedShapes = False AndAlso geom.Vertices.Length = 0 Then
                                builder.RemoveShape(shap)
                                ProgressBar1.Value += 1
                            Else
                                builder.NIFContent.UpdateSkinPartitions(shap.RelatedNifShape)
                                ProgressBar1.Value += 1
                            End If

                        Else
                            ProgressBar1.Value += 5
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

        ' Grabo archivo sliders.json 
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 AndAlso Config_App.Current.Settings_Build.AddAddintionalSliders AndAlso Config_App.Current.Settings_Build.SaveTri Then TriFile.Serialize_LooksmenuAdditionalSiliders()

        If Errores <> "" Then
            MsgBox("Error building the following projects:" + Errores)
        End If
        Me.Close()
    End Sub


End Class