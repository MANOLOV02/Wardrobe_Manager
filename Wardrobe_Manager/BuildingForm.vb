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

    ''' <summary>
    ''' True si el path existe y su header ES "PIRT" (body-tri de BodySlide). Equivale a
    ''' IsBodyTriFile de BSOS (TriFile.cpp:11-27), que compara los 4 bytes del header contra
    ''' "TRIP"_mci — un uint32 que escrito little-endian da los bytes P,I,R,T.
    ''' </summary>
    Private Shared Function ExistingTriIsBodySlide(triPath As String) As Boolean
        Return ReadTriHeader(triPath) = "PIRT"
    End Function

    ''' <summary>
    ''' True si el path existe y su header NO es "PIRT" — o sea hay un .tri que no es de BodySlide
    ''' (tipicamente un FRTRI003 de FaceGen). Un archivo inexistente NO es ajeno.
    ''' </summary>
    Private Shared Function ExistingTriIsForeign(triPath As String) As Boolean
        Dim hdr = ReadTriHeader(triPath)
        Return hdr IsNot Nothing AndAlso hdr <> "PIRT"
    End Function

    ''' <summary>Los 4 bytes de header como ASCII, o Nothing si el archivo no existe / no se puede leer.</summary>
    Private Shared Function ReadTriHeader(triPath As String) As String
        Try
            If String.IsNullOrWhiteSpace(triPath) OrElse Not IO.File.Exists(triPath) Then Return Nothing
            Using fs As New IO.FileStream(triPath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
                ' Un archivo de menos de 4 bytes es NUESTRO, no ajeno: WriteTriToFile abre con
                ' FileMode.Create (trunca primero), asi que un throw a mitad de escritura deja
                ' exactamente eso. Tratarlo como ajeno bloqueaba el proyecto PARA SIEMPRE.
                ' Se decide por fs.Length, NO por el retorno de Read: Read puede devolver menos bytes
                ' de los pedidos sin que el archivo este truncado (red, placeholder de OneDrive, AV),
                ' y con eso un FRTRI003 valido se habria reportado como nuestro y lo pisabamos.
                If fs.Length < 4 Then Return "PIRT"
                Dim buf(3) As Byte
                fs.ReadExactly(buf, 0, 4)
                Return System.Text.Encoding.ASCII.GetString(buf)
            End Using
        Catch
            ' Ilegible: se trata como ajeno para no pisarlo a ciegas.
            Return ""
        End Try
    End Function

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
                ' Force the cloned output dir (per-pack) when the build setting is on. Runs on the
                ' temporary clone only, so the pack name comes from the original sliderset's ParentOSP.
                If WM_Config.Current.Settings_Build.ForceClonedOnBuild Then
                    builder.ForceClonedOutputDir(If(sliderset_target.ParentOSP?.Nombre, ""))
                End If
                ' Shapedata loaded on the builder clone below, not on sliderset_target
                Dim size As WM_Config.SliderSize = WM_Config.SliderSize.Default
                ' Decididos en Sizecount=0 y reusados por el resto de los sizes: el .tri es uno solo
                ' para todo el proyecto, pero cada size necesita saber si estampar el BODYTRI.
                Dim triBlocked As Boolean = False
                Dim triWritten As Boolean = False
                For Sizecount = 0 To CInt(IIf(sliderset_target.Multisize, 1, 0))
                    ProgressBar1.Value = 0
                    ProgressBar1.Maximum = (builder.Shapes.Count * 4 + 6)
                    ' Cada peso debe partir de la geometría PRISTINE. Sin esto, la pasada _1 (Big)
                    ' hereda el NIFContent ya bakeado con Small: Load_and_Check_Shapedata skipea la
                    ' recarga (ShapeDataLoaded + misma signature) y BakeFromMemoryUsingOriginal de la
                    ' pasada anterior ya inyectó los vértices morphados al trishape. Resultado: sin
                    ' preset _1 sale byte-idéntico a _0 (deltas Big=0 sobre base ya morphada); con
                    ' preset, _1 = small+big APILADOS. OS (BodySlideApp::BuildBodies) aplica cada
                    ' peso desde cero sobre la base — replicamos eso recargando.
                    If Sizecount > 0 Then builder.UnloadShapeData(False)
                    If OSP_Project_Class.Load_and_CHeck_Project(builder, buildLoadContext) = False OrElse OSP_Project_Class.Load_and_Check_Shapedata(builder, buildLoadContext) = False Then Throw New InvalidOperationException("Could not load shape data for build.")
                    ProgressBar1.Value += 1
                    ' El clon NO puede resolver su propio HH: ForceClonedOutputDir ya le reescribió el
                    ' OutputPath (la detección FO4 va contra ese path) y su ParentOSP es un dummy sin
                    ' archivo. Y leer el campo crudo del original tampoco servía: la lista descarga cada
                    ' sliderset tras cargarlo y UnloadShapeData lo pone en 0, así que el build interno
                    ' horneaba 0 y BORRABA los tacones. Se resuelve sobre el ORIGINAL y se estampa como
                    ' autorizado en el clon.
                    builder.HighHeelHeight = sliderset_target.ResolveEffectiveHighHeel(buildLoadContext)
                    builder.HighHeelAuthored = True
                    SkeletonInstance.Default.PrepareForShapes(builder.Shapes)
                    SkeletonInstance.Default.ApplyPose(If(has_pose, _Pose, Nothing))
                    ProgressBar1.Value += 1

                    Dim fil = builder.OutputFullPathBase + If(sliderset_target.Multisize, "_" + Sizecount.ToString, "") + ".nif"
                    Dim tri = builder.OutputFullPathBase + ".tri"
                    Dim Tridata = IO.Path.GetRelativePath(IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, "Meshes")), tri)
                    Dim dir = IO.Path.GetDirectoryName(fil)
                    Nombre = sliderset_target.Nombre
                    Label1.Text = "Building: " + Nombre + IIf(sliderset_target.Multisize(), "_" + Sizecount.ToString, "")
                    Application.DoEvents()
                    ' Multisize() == GenWeights(). BodySlide SIN GenWeights emite UN solo mesh y lo hace
                    ' con `vbig` / `defBigValue` — `vsmall` sólo existe dentro de
                    ' `if (currentSet.GenWeights())` (BodySlideApp.cpp:4356-4364, :4394, :4409).
                    ' Mapear el pase único a Small hacía que un proyecto SSE no-multisize leyera
                    ' Default_Small_Value en vez de Default_Big_Value. FO4 no se ve afectado:
                    ' FO4 no cambia de artefactos: Multisize() esta hard-gateado a False para FO4, y
                    ' Default_Setting despacha por GenWeights — los 3.359 sliderSets de FO4 medidos
                    ' traen GenWeights="false", asi que caen en Default_Setting_FO. Un .osp de FO4 con
                    ' GenWeights ausente o true leeria big/small (inexistentes) y daria 0, pero
                    ' BodySlide hace exactamente lo mismo con ese archivo (SliderData.cpp:38-48).
                    If sliderset_target.Multisize Then
                        size = If(Sizecount = 0, WM_Config.SliderSize.Small, WM_Config.SliderSize.Big)
                    Else
                        size = WM_Config.SliderSize.Big
                    End If
                    ' 0 - cargo morph
                    builder.SetPreset(_Preset, size)
                    ProgressBar1.Value += 1
                    ' --- O6.1: Parallel shape processing (compute-heavy part) ---
                    Dim shapeList = builder.Shapes.ToList
                    Dim shapeResults As New System.Collections.Concurrent.ConcurrentDictionary(Of Shape_class, SkinnedGeometry)
                    ' El reindex de morphs post-zap NO puede correr dentro del Parallel.ForEach: escribe
                    ' en el XmlDocument del OSP y en OSDContent_Local.Blocks, ambos compartidos por todas
                    ' las shapes. Se recolecta aca el mapa old->new y se aplica en la fase 2 (serial).
                    Dim zapMods As New System.Collections.Concurrent.ConcurrentDictionary(Of Shape_class, ZapGeometryModifier)
                    ' Shapes 100 % zapeadas que se conservan ocultas: BodySlide tampoco emite sus morphs
                    ' en el .tri (con la geometria intacta su erase de rangos deja todos los offsets en
                    ' cero, BodySlideApp.cpp:1450-1460), asi que se las excluye explicitamente.
                    Dim hiddenZappedShapes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    ' Compartido por TODAS las shapes del size: un mismo bloque OSD alcanzado desde dos
                    ' shapes no se puede remapear dos veces (la segunda iria sobre indices ya movidos).
                    Dim remappedBlocks As New HashSet(Of OSD_Block_Class)(ReferenceEqualityComparer.Instance)
                    Dim localKeepZapped = builder.KeepZappedShapes
                    Dim localSize = size
                    Dim localSingleBone = Config_App.Current.Setting_SingleBoneSkinning
                    Dim localRecalcNormals = Config_App.Current.Setting_RecalculateNormals

                    ' Phase 1: parallel compute (Extract + Morph + Bake/InjectToTrishape).
                    ' Pose state was applied above via ApplyPose (or Reset when has_pose=False);
                    ' Extract/Bake read it from the SkeletonInstance's DeltaTransforms.
                    Parallel.ForEach(shapeList.Where(Function(s) s.RelatedNifShape IsNot Nothing),
                        Sub(shap)
                            ' 1- cargo geometria
                            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shap, singleboneskinning:=localSingleBone, RecalculateNormals:=False)
                            ' 3- aplico morph (y recalculo normales si esta elegido)
                            MorphingHelper.ApplyMorph_CPU(shap, geom, localRecalcNormals, AllowMask:=False, buildSize:=localSize)
                            ' 4- Borro zaps y revierto bakeo (includes InjectToTrishape per-shape)
                            Dim zapMod As New ZapGeometryModifier(localKeepZapped)
                            SkinningHelper.BakeFromMemoryUsingOriginal(shap, geom, inverse:=False, ApplyMorph:=True, RemoveZaps:=True, singleBoneSkinning:=localSingleBone, geometryModifier:=zapMod)
                            zapMods(shap) = zapMod
                            shapeResults(shap) = geom
                        End Sub)

                    ' Phase 2: sequential NIF structure updates + progress
                    For Each shap In shapeList
                        If shap.RelatedNifShape IsNot Nothing Then
                            Dim geom As SkinnedGeometry = Nothing
                            If shapeResults.TryGetValue(shap, geom) Then
                                ' Paso 4 del zap, fuera del paralelo. Antes de RemoveShape: si la shape se
                                ' va, RemoveShape ya borra sus Datas y bloques locales.
                                Dim zapMod As ZapGeometryModifier = Nothing
                                zapMods.TryGetValue(shap, zapMod)
                                If zapMod IsNot Nothing Then MorphingHelper.ReindexMorphsAfterZap(shap, zapMod.VertexRemap, remappedBlocks)
                                ProgressBar1.Value += 3 ' account for extract+morph+bake steps
                                If builder.KeepZappedShapes = False AndAlso geom.Vertices.Length = 0 Then
                                    builder.RemoveShape(shap)
                                    ProgressBar1.Value += 1
                                Else
                                    ' Shape 100 % zapeada que se conserva: oculta con geometria intacta,
                                    ' igual que BodySlideApp.cpp:3618-3620.
                                    If zapMod IsNot Nothing AndAlso zapMod.FullyZappedKept Then
                                        builder.NIFContent.SetShapeHidden(shap.RelatedNifShape)
                                        hiddenZappedShapes.Add(shap.RelatedNifShape.Name.String)
                                    End If
                                    builder.NIFContent.UpdateSkinPartitions(shap.RelatedNifShape)
                                    ProgressBar1.Value += 1
                                End If
                            Else
                                ' Shape was filtered or failed silently in the parallel phase
#If DEBUG Then
                                Debugger.Break()
#End If
                                ProgressBar1.Value += 4
                            End If
                        Else
                            ProgressBar1.Value += 4
                        End If
                    Next

                    If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

                    ' El engine resuelve el material leyendo el Name del shader como path relativo a
                    ' Data\, así que debe contener el ancla "Materials\". WM lo guarda pelado
                    ' (Clone_Materials setea "ManoloCloned\..." y SetRelatedMaterial strippea el prefijo),
                    ' lo que se ve bien in-app pero deja al engine sin encontrar el bgsm/bgem in-game.
                    ' Se lo devolvemos aquí, antes de grabar cualquier NIF de salida (incluido el variant
                    ' de high-heels, que opera sobre este mismo NIFContent). Independiente del flag ForceClone.
                    builder.NIFContent.EnsureMaterialPrefixForGame()

                    ' Grabo bloque tri si hace falta. GAME-AWARE, replicando OutfitStudio (BodySlideApp.cpp
                    ' AddTriData / BuildBodies :4589-4608):
                    '   • FO4/FO4VR/FO76 → BODYTRI en el NODO RAÍZ (AddTriData toRoot=true).
                    '   • Skyrim/SSE     → BODYTRI en un NiShape: el PRIMER shape (en orden del sliderset) que
                    '     existe en el NIF y tiene >0 vértices; solo UNO (triEnd se apaga tras el primero).
                    ' skee64 (RaceMenu) lo lee con VisitObjects → lo encuentra en el shape; escribirlo a la raíz
                    ' en SSE no es fiel a OutfitStudio (y rompe lectores shape-only). El .tri en sí es idéntico
                    ' (PIRT/TRIP) en ambos juegos — solo cambia DÓNDE se marca en el NIF.
                    ' `triKeep` de BodySlideApp.cpp:3653-3671 = PreventMorphFile (salvo IgnorePreventri,
                    ' extension de WM) O un .tri ajeno ya presente. El nombre del .tri sale del nombre del
                    ' NIF, asi que un proyecto que apunte a una malla de cabeza machacaria el FRTRI003 de
                    ' chargen del juego — un archivo ajeno que no se regenera.
                    Dim triAllowed As Boolean = (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri)
                    If Sizecount = 0 Then
                        triBlocked = False
                        triWritten = False
                        ' Solo se avisa cuando de verdad ibamos a escribir, como BSOS, que hace el
                        ' chequeo dentro de `if (tri && !triKeep)`.
                        If WM_Config.Current.Settings_Build.SaveTri AndAlso triAllowed Then
                            triBlocked = ExistingTriIsForeign(tri)
                            If triBlocked Then
                                If Errores <> "" Then Errores += vbCrLf
                                Errores += Nombre & ": kept the existing non-BodySlide .tri at " & tri & " (morphs skipped)"
                            Else
                                ' El .tri se escribe ANTES de grabar el NIF: si falla, el BODYTRI de abajo
                                ' no se estampa y el NIF sale coherente. Al reves (como estaba) el NIF ya
                                ' habia salido apuntando a un archivo inexistente, y cortar por excepcion
                                ' ademas se llevaba puesto el resto de los sizes del proyecto.
                                triWritten = LooksMenuSliders.WriteMorphTRI(tri, builder, hiddenZappedShapes)
                                If Not triWritten Then
                                    If Errores <> "" Then Errores += vbCrLf
                                    Errores += Nombre & ": failed to write the morph .tri at " & tri
                                End If
                            End If
                        End If
                    End If

                    If WM_Config.Current.Settings_Build.SaveTri AndAlso triWritten AndAlso triAllowed Then
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                            Dim triShapeName As String = Nothing
                            For Each shap In builder.Shapes
                                Dim ns = shap.RelatedNifShape
                                If ns IsNot Nothing AndAlso ns.VertexCount > 0 Then
                                    triShapeName = ns.Name.String
                                    Exit For
                                End If
                            Next
                            If triShapeName IsNot Nothing Then
                                builder.NIFContent.AddTriData(triShapeName, Tridata, False)
                            End If
                        Else
                            builder.NIFContent.AddTriData("", Tridata, True)
                        End If
                    Else
                        ' Limpieza game-aware: en SSE un BODYTRI heredado del NIF fuente puede colgar de un shape,
                        ' así que lo quitamos de la raíz y de cada shape; en FO4 solo puede estar en la raíz.
                        builder.NIFContent.RemoveTriData("", True)
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                            For Each shap In builder.Shapes
                                Dim ns = shap.RelatedNifShape
                                If ns IsNot Nothing Then builder.NIFContent.RemoveTriData(ns.Name.String, False)
                            Next
                        End If
                    End If

                    ' High Heels. El alta/baja en el diccionario ya la hace la emisión (antes vivía
                    ' acá, y por eso el build con el motor de BodySlide nunca lo actualizaba).
                    builder.SaveHighHeelBuild(builder.NIFContent)
                    ProgressBar1.Value += 1


                    ' SSE: ajustar el link in-NIF de física HDT-SMP ("HDT Skinned Mesh Physics Object") al
                    ' path del sidecar de SALIDA (paths de build), o removerlo si el proyecto no tiene física.
                    ' El motor lee ESE path; el sidecar se copia más abajo (una vez, en Sizecount=0). Corre
                    ' por cada size porque cada NIF de salida necesita el link ajustado. Mismo modelo que HH_OFFSET.
                    If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                        If Not String.IsNullOrEmpty(builder.PhysicsXmlContent) Then
                            builder.NIFContent.SetSmpPhysicsXmlPath(SliderSet_Class.BuildSmpInNifPath(builder.OutputFullPathBase + ".xml"))
                        Else
                            builder.NIFContent.RemoveSmpPhysicsExtraData()
                        End If
                    End If

                    ' Grabo nif
                    builder.NIFContent.Save_As_Manolo(fil, True)
                    Dim nifRelative As String = IO.Path.GetRelativePath(Directorios.Fallout4data, fil).Correct_Path_Separator
                    FilesDictionary_class.AddOrUpdateDictionaryEntry(nifRelative, New FilesDictionary_class.File_Location With {
                        .BA2File = "", .Index = -1, .FullPath = nifRelative, .FileDate = Date.Now})

                    ProgressBar1.Value += 1



                    If Sizecount = 0 Then
                        ' Grabo archivo tri
                        Dim triRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, tri).Correct_Path_Separator
                        If triWritten Then
                            FilesDictionary_class.AddOrUpdateDictionaryEntry(triRelative, New FilesDictionary_class.File_Location With {
                                .BA2File = "", .Index = -1, .FullPath = triRelative, .FileDate = Date.Now})
                        ElseIf Not WM_Config.Current.Settings_Build.SaveTri AndAlso Not triBlocked AndAlso (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri) Then
                            ' BodySlideApp.cpp:3716-3720: sin morphs, el .tri viejo queda huerfano — nadie lo
                            ' referencia ya (el BODYTRI se quito arriba) pero se empaqueta igual en el FOMOD/BA2
                            ' con morphs de una geometria que ya cambio. Solo se borra si es un body-tri PIRT;
                            ' un FRTRI003 ajeno nunca se toca (ese caso ya lo ataja triBlocked).
                            '
                            ' El guard de PreventMorphFile es obligatorio: en BSOS `triKeep` apaga TANTO la
                            ' escritura COMO el borrado, asi que un proyecto marcado "prevent morph file"
                            ' conserva su .tri intacto. Sin esta condicion lo borrabamos.
                            If ExistingTriIsBodySlide(tri) Then
                                IO.File.Delete(tri)
                                FilesDictionary_class.RemoveDictionaryEntry(triRelative)
                            End If
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
                ' Sin el mensaje de la excepcion el dialog final solo listaba nombres de proyecto,
                ' que no alcanza para distinguir un .tri no escrito de un load fallido.
                If Errores <> "" Then Errores += vbCrLf
                Errores += Nombre & ": " & ex.Message
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
            MsgBox("Error building the following projects:" & vbCrLf & Errores)
        End If
        Me.Close()
    End Sub


End Class
