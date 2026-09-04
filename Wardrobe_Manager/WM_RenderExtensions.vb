' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports FO4_Base_Library
Imports FO4_Base_Library.PreviewModel

''' <summary>
''' WM-specific rendering extensions for PreviewControl.
''' Adds Update_Render with OSP/SliderSet support, morphing, zaps, presets.
''' </summary>
Public Module WM_RenderExtensions

    ' Per-control WM state — ConditionalWeakTable auto-removes entries when the control is GC'd
    Private ReadOnly _state As New System.Runtime.CompilerServices.ConditionalWeakTable(Of PreviewControl, WM_RenderState)

    Private Class WM_RenderState
        Public Last_rendered As SliderSet_Class
        Public Last_Preset As SlidersPreset_Class
        Public Last_size As WM_Config.SliderSize = WM_Config.SliderSize.Default
        ' Version de la lista de sliders con la que se resolvio el ultimo SetPreset. Ver
        ' SliderSet_Class.SlidersVersion: un reload la sube y obliga a re-resolver.
        Public Last_SlidersVersion As Integer = -1
        ''' <summary>LA MISMA INSTANCIA de lista que se le paso a <c>LoadShapesParallel</c> en la ultima
        ''' carga de geometria. Los <c>RenderableMesh</c> guardan la referencia al <c>Shape_class</c> con
        ''' el que se construyeron (<c>MeshData.Shape</c>), asi que si el sliderSet reemplazo su lista
        ''' —<c>Lee_SlidersAndShapes</c> hace <c>Shapes = ...ToList</c> con objetos NUEVOS— los del
        ''' modelo quedaron huerfanos y hay que recargar la geometria. Ver el porque en
        ''' <c>Update_Render</c>.</summary>
        Public Last_Shapes As List(Of Shape_class) = Nothing
        ' SliderMorphResolver has no per-frame state — it rebuilds the same plan from each
        ' slider's persisted Current_Setting. Cache one instance per control and reuse it
        ' every frame instead of allocating on every Update_Render (incl. each animation tick).
        Public MorphResolver As SliderMorphResolver = New SliderMorphResolver()
    End Class

    Private Function GetState(ctrl As PreviewControl) As WM_RenderState
        Return _state.GetOrCreateValue(ctrl)
    End Function

    ''' <summary>WM slider presets. Stored via FilesDictionary.SetAppData for lifecycle management.</summary>
    Public Property WM_SliderPresets As SliderPresetCollection
        Get
            Dim presets = FilesDictionary_class.GetAppData(Of SliderPresetCollection)()
            If presets Is Nothing Then
                presets = New SliderPresetCollection()
                FilesDictionary_class.SetAppData(presets)
            End If
            Return presets
        End Get
        Set(value As SliderPresetCollection)
            FilesDictionary_class.SetAppData(value)
        End Set
    End Property

    ''' <summary>WM-specific high heels plugin data.</summary>
    Public Property WM_HighHeels As New HighHeels_Plugins_values

    ''' <summary>Initialize WM-specific setup. Call once at application startup.</summary>
    Public Sub InitializeWM()
        ' Register WM-specific file extensions for dictionary scanning
        FilesDictionary_class.RegisterExtensions(".osp")
        ' Initialize preset collection
        FilesDictionary_class.SetAppData(New SliderPresetCollection())
        ' Auto-detect BodySlide/OutfitStudio paths
        WM_Config.AutoDetectBSPaths()

    End Sub

    ''' <summary>Get/set the WM Last_rendered SliderSet for this control.</summary>
    <Extension()>
    Public Function WM_Last_rendered(ctrl As PreviewControl) As SliderSet_Class
        Return GetState(ctrl).Last_rendered
    End Function

    <Extension()>
    Public Sub WM_Set_Last_rendered(ctrl As PreviewControl, value As SliderSet_Class)
        GetState(ctrl).Last_rendered = value
    End Sub

    ''' <summary>
    ''' Re-render with specific dirty flags. Uses the intent already populated by the last Update_Render.
    ''' Callers specify exactly what changed — no nuclear "Force" unless truly needed.
    ''' </summary>
    <Extension()>
    Public Sub ForceRerender(ctrl As PreviewControl, Optional flags As RenderDirtyFlags = RenderDirtyFlags.Force Or RenderDirtyFlags.Camera)
        If ctrl.Disposing OrElse ctrl.IsDisposed OrElse Not ctrl.Visible Then Return
        ctrl.Intent.RecalculateNormals = ctrl.Model.RecalculateNormals
        ctrl.Intent.MarkDirty(flags)
        ctrl.InvalidateRender()
    End Sub

    ''' <summary>
    ''' Vacía el preview y deja el cartel <paramref name="statusText"/>. Además del clear del
    ''' render hay que soltar el estado de WM: <c>Last_rendered</c> apuntando al sliderSet viejo
    ''' haría que al re-seleccionarlo se tomara el atajo de "mismo set", y <c>PinnedForPreview</c>
    ''' lo mantiene a salvo del LRU aunque ya no se muestre.
    ''' </summary>
    Private Sub ClearUnreadable(ctrl As PreviewControl, s As WM_RenderState, statusText As String)
        ctrl.Model.FloorOffset = 0
        ctrl.ClearRender(statusText)
        s.Last_rendered = Nothing
        s.Last_SlidersVersion = -1
        s.Last_Shapes = Nothing
        OSP_Project_Class.PinnedForPreview = Nothing
    End Sub

    <Extension()>
    Public Sub Update_Render(ctrl As PreviewControl, seleccionado As SliderSet_Class, Force As Boolean,
                             Preset As SlidersPreset_Class, Pose As Poses_class, weight As WM_Config.SliderSize)
        Dim _sw As New System.Diagnostics.Stopwatch() : _sw.Start()
        If ctrl.Disposing OrElse ctrl.IsDisposed Then Exit Sub
        If Not ctrl.Visible Then Exit Sub

        Dim s = GetState(ctrl)

        ' Sin selección / proyecto ilegible: hay que DESCARGAR lo anterior, no sólo pintar el
        ' cartel. Con un Processing_Status suelto las mallas del sliderSet previo seguían cargadas
        ' con Can_Render=True, y el primer repaint que llegara (el heartbeat de ~1 s del
        ' RenderTimer, un resize, el mouse) las redibujaba encima: el cartel aparecía y al toque
        ' reaparecía el proyecto anterior.
        If IsNothing(seleccionado) Then
            ClearUnreadable(ctrl, s, "Select project")
            Exit Sub
        End If

        If seleccionado.Unreadable_Project Then
            ClearUnreadable(ctrl, s, "Unreadable...")
            Exit Sub
        End If
        If seleccionado.BypassDiskShapeDataLoad = False Then
            If OSP_Project_Class.Load_and_Check_Shapedata(seleccionado, False) = False Then
                ClearUnreadable(ctrl, s, "Unreadable...")
                Exit Sub
            End If
        End If

        If Not ctrl.PlayingAnimation Then Cursor.Current = Cursors.WaitCursor
        OSP_Project_Class.PinnedForPreview = seleccionado

        ' Snapshot previous state for change detection
        Dim prevPreset = s.Last_Preset
        Dim prevSize = s.Last_size

        ' Detect what changed
        ' ⛔⛔ EL CUARTO TERMINO CIERRA "al volver de Outfit Studio el cuerpo sale sin morfear y ya no
        ' aplica ningun preset" (reportado 2026-08-24, cerrado 2026-09-04). No alcanza con que el
        ' SliderSet_Class sea el MISMO OBJETO: `Lee_SlidersAndShapes` hace `Shapes = ...ToList` con
        ' `New Shape_class(...)`, o sea que tras cualquier relectura del .osp los Shape_class son OTROS
        ' aunque el sliderSet no se haya movido. Los `RenderableMesh` del modelo guardan el Shape_class
        ' con el que se cargaron (`MeshData.Shape`, en LoadShapesParallel) y el camino morph-only del
        ' pipeline resuelve el plan CONTRA ESOS. `Shape_class.Related_Sliders` filtra por
        ' `pf.RelatedShape Is Me` —identidad, y `RelatedShape` sale de `GetShapeByTargetCached`, que ya
        ' devuelve los shapes NUEVOS—, asi que para un shape huerfano da VACIO: plan sin canales, y
        ' `ApplyMorphPlan` con plan vacio devuelve la malla a NifLocalVertices (contrato "sin morphs").
        ' De ahi los dos sintomas: el cuerpo sin morfear y el preset que ya no hace nada — cambiarlo
        ' vuelve a entrar por el mismo morph-only con los mismos huerfanos. Recargar otro proyecto y
        ' volver lo arreglaba porque eso si rompia `Last_rendered Is seleccionado`.
        ' Los DOS caminos que relee el .osp sin tocar `Model.Cleaned` quedan cubiertos:
        '   · `EndExternalEditSession` llama `Reload` SIEMPRE, pero solo limpia el modelo si la marca de
        '     escritura avanzo — cerrar OS SIN GRABAR NADA es el caso 100 % reproducible;
        '   · `Load_and_Check_Shapedata` (que corre unas lineas mas arriba, en este mismo metodo) hace
        '     `ParentOSP.Reload` por su cuenta cuando la firma del .osp cambio en disco.
        Dim sameSet = (s.Last_rendered Is seleccionado) AndAlso ctrl.Model.Cleaned = False AndAlso Force = False AndAlso
                      ReferenceEquals(s.Last_Shapes, seleccionado.Shapes)
        ' ⛔ El tercer termino NO es de adorno: `Current_Setting`/`Zap_Setting_Big` viven en los
        ' Slider_class, y un reload los reconstruye con esos campos en 0. Sin mirar la version, el
        ' skipPresetApply de abajo (que compara el preset por REFERENCIA) se saltearia el SetPreset y
        ' el render aplicaria todos los zaps con peso 0.
        Dim prevSlidersVersion = s.Last_SlidersVersion
        Dim presetChanged = Not (prevPreset Is Preset) OrElse (prevSize <> weight) OrElse
                            (prevSlidersVersion <> seleccionado.SlidersVersion)
        Dim skipPresetApply = sameSet AndAlso Not presetChanged

        ' Apply slider weights from preset. During animation playback the pose changes every
        ' tick, but the slider preset usually does not, so avoid reapplying morph setup.
        If Not skipPresetApply Then
            seleccionado.SetPreset(Preset, weight)
            s.Last_size = weight
            ' El resolver necesita el peso para gatear el 2do pase de clamp por el DEFAULT de
            ' ese peso, igual que el bake (ApplyMorph_CPU recibe buildSize). Sin esto el preview
            ' gateaba siempre por Big y divergia del _0.nif.
            s.MorphResolver.BuildSize = weight
            s.Last_Preset = Preset
            s.Last_SlidersVersion = seleccionado.SlidersVersion
        End If

        ' Pose change detected against the SkeletonInstance's last applied pose (only used to
        ' pick the dirty flag below). Apply pose UNCONDITIONALLY here — idempotent and trivial
        ' (~200 bones × ~5µs), guarantees DeltaTransforms reflect the requested pose even if
        ' another flow (e.g. CreatefromNif with Pose=Nothing) reset them in between frames.
        Dim poseChanged = Not (SkeletonInstance.Default.Pose Is Pose)
        Dim _swApply = Stopwatch.StartNew()
        SkeletonInstance.Default.ApplyPose(Pose)
        _swApply.Stop()
        Dim _applyMs = _swApply.Elapsed.TotalMilliseconds
        Logger.LogLazy(Function() $"[POSE-APPLY] ApplyPose={_applyMs:F1}ms")

        ' Fill the intent — the pipeline decides HOW based on dirty flags.
        Dim intent = ctrl.Intent
        intent.Shapes = seleccionado.Shapes
        intent.FloorOffset = -seleccionado.HighHeelHeight
        intent.RecalculateNormals = ctrl.Model.RecalculateNormals
        intent.SkeletonResolver = Nothing  ' default skeleton resolver
        ' Always provide a real resolver. skipPresetApply only means "the slider weights did
        ' not change, so skip the expensive SetPreset" — it must NOT null the resolver.
        ' PipelineStep_Morphs ALWAYS calls ApplyMorphPlan when Morphs is dirty, and a null
        ' resolver yields a null plan, which by contract RESETS the mesh to NifLocalVertices
        ' (base, pre-morph). That wiped the active morph on any incidental refresh that flags
        ' Morphs without changing preset/pose — e.g. changing the selection/pack of the
        ' non-focused list. The resolver rebuilds the same plan from the persisted slider
        ' Current_Setting, so dirty stays empty and the morph is preserved. No extra animation
        ' cost: a pose-only change never flags Morphs, so this step does not run.
        intent.MorphResolver = s.MorphResolver
        intent.GeometryModifiers = Nothing

        If Not sameSet Then
            ' Full reload: new SliderSet, forced, or model was cleaned
            intent.MarkDirty(RenderDirtyFlags.Shapes Or RenderDirtyFlags.Camera)

            ' Texture prefetch (async before geometry load)
            Dim prefetchShapes = seleccionado.Shapes
            If prefetchShapes IsNot Nothing AndAlso prefetchShapes.Count > 0 Then
                intent.TexturePrefetchAction = Sub()
                                                   Dim texturePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                                                   For Each shape In prefetchShapes
                                                       If shape.RelatedMaterial?.material IsNot Nothing Then
                                                           Dim mat = shape.RelatedMaterial.material
                                                           Dim paths = {mat.Diffuse_or_Base_Texture, mat.NormalTexture, mat.SmoothSpecTexture,
                                                                        mat.GreyscaleTexture, mat.EnvmapTexture, mat.FlowTexture,
                                                                        mat.GlowTexture, mat.DisplacementTexture, mat.InnerLayerTexture,
                                                                        mat.LightingTexture, mat.SpecularTexture, mat.WrinklesTexture,
                                                                        mat.DistanceFieldAlphaTexture, mat.EnvmapMaskTexture,
                                                                        mat.DetailMaskTexture, mat.TintMaskTexture}
                                                           For Each p In paths
                                                               Dim corrected = FO4UnifiedMaterial_Class.CorrectTexturePath(p)
                                                               If corrected <> "" Then texturePaths.Add(corrected)
                                                           Next
                                                       End If
                                                   Next
                                                   If texturePaths.Count > 0 Then
                                                       Dim pathsArray = texturePaths.ToArray()
                                                       Task.Run(Sub() FilesDictionary_class.GetMultipleFilesBytes(pathsArray))
                                                   End If
                                               End Sub
            End If

            s.Last_rendered = seleccionado
            ' Se sella JUNTO con Last_rendered y con la MISMA instancia que va en intent.Shapes: es
            ' exactamente la lista con la que LoadShapesParallel va a construir los RenderableMesh.
            s.Last_Shapes = seleccionado.Shapes

        ElseIf poseChanged Then
            ' Pose change: skeleton + bone matrices, optional morphs
            Dim poseFlags = RenderDirtyFlags.Pose
            If Not ctrl.PlayingAnimation Then poseFlags = poseFlags Or RenderDirtyFlags.Camera
            intent.MarkDirty(poseFlags)
            If presetChanged Then intent.MarkDirty(RenderDirtyFlags.Morphs)

        ElseIf presetChanged Then
            ' Morph-only: same set, same pose, preset/size changed. A slider/preset change does
            ' not alter materials, so Textures stays clean (it would trigger Process_Textures_GL).
            intent.MarkDirty(RenderDirtyFlags.Morphs)

        Else
            ' Preserve the old refresh behavior outside playback, but keep same-frame timer
            ' ticks from doing needless morph work while the animation is running. Morph-only:
            ' no material change, so Textures stays clean (avoids redundant Process_Textures_GL).
            If Not ctrl.PlayingAnimation Then
                intent.MarkDirty(RenderDirtyFlags.Morphs)
            End If
        End If

        ' Signal the control — pipeline executes synchronously for now
        ctrl.InvalidateRender()

        _sw.Stop()
        ctrl.LastUpdateMs = _sw.Elapsed.TotalMilliseconds
        If Not ctrl.PlayingAnimation Then Cursor.Current = Cursors.Default
    End Sub

    ''' <summary>JSON options for SAM (ScreenArcher) pose export — mirrors Editor_Form.opts.</summary>
    Private ReadOnly _samExportOpts As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .NumberHandling = JsonNumberHandling.AllowReadingFromString,
        .WriteIndented = True}

    ''' <summary>Build+write the imported pose as a SAM (ScreenArcher) JSON file under
    ''' <see cref="Wardrobe_Manager_Form.Directorios.PosesSAMRoot"/>, reading the currently-posed
    ''' local transforms from <c>SkeletonInstance.Default</c>. Shared core extracted from
    ''' <c>Editor_Form.ExportSaf</c> so both the editor and the HKX import form write SAM identically.
    ''' Returns the built <see cref="Poses_class"/> (the caller registers it in its combos), or
    ''' <c>Nothing</c> if <paramref name="name"/> is blank, no skeleton is loaded, or writing fails
    ''' (swallow-and-return, matching ExportSaf).
    ''' <para>PRECONDITION: <c>SkeletonInstance.Default</c> must already be posed at the desired frame
    ''' (its <c>LocaLTransform</c> per bone is read verbatim).</para>
    ''' <param name="extraBones">Optional bones to append AFTER the live skeleton (e.g. HKX-defined bones
    ''' the live NIF skeleton lacks, for pose portability). Live skeleton wins on name collision.</param></summary>
    Public Function ExportSamPoseFile(name As String, Optional extraBones As Dictionary(Of String, PoseTransformData) = Nothing) As Poses_class
        If String.IsNullOrWhiteSpace(name) Then Return Nothing
        If SkeletonInstance.Default.HasSkeleton = False Then Return Nothing
        Try
            Dim Export As New Poses_class With {
                .Filename = IO.Path.Combine(Wardrobe_Manager_Form.Directorios.PosesSAMRoot, name + ".json"),
                .Source = Poses_class.Pose_Source_Enum.ScreenArcher,
                .Version = 2,
                .Skeleton = "Vanilla",
                .Transforms = New Dictionary(Of String, PoseTransformData),
                .Name = name
            }
            ' ⛔ SAM (ScreenArcher) es un formato AJENO y su escala es UN SOLO float. Verificado en el
            ' fuente canonico: BodySlide/OutfitStudio guarda `float poseScale` (Anim.h) y lee
            ' `FloatAttribute("scale", 1.0f)` (PoseData.cpp) — no hay per-eje en ninguno de los dos.
            ' Asi que aca se PROYECTA a escalar, y se proyecta con `EscalaComoEscalar`, que devuelve
            ' scale_eff.X (NO un promedio) y AVISA por su parametro cuando la proyeccion perdio algo.
            ' Antes esto leia `tr.Scale` a secas: con body-weight aplicado el morph trae escala per-eje,
            ' `ComposeTransforms` caia en su rama no uniforme y `Scale` quedaba en 1.0 ⇒ el .json salia
            ' con scale:1 para TODOS los huesos escalados por peso, en silencio.
            ' ⛔ SE PROYECTA A ESCALAR Y NO SE COPIA EL PER-EJE. Cargar `Scale` Y `ScaleVector` a la vez
            ' viola la convencion de Transform_Class ("quien escribe ScaleVector deja Scale = 1") y el
            ' consumidor MULTIPLICA: con eff=(1.2,1,1) se re-aplicaba (1.44,1.2,1.2), la escala al CUADRADO.
            ' Medido contra la DLL. Y el caso uniforme era peor porque `exacto` daba True y ni siquiera
            ' avisaba.
            ' ⛔ Y TAMPOCO se conserva el per-eje 'para no perder fidelidad': `ScaleX/Y/Z` son <JsonIgnore>,
            ' asi que al reabrir la app vuelven en 1 y la pose CAMBIARIA SOLA. Un dato derivado que
            ' sobrevive a su fuente es un dato podrido. Dejandolos en 1 el objeto en memoria dice lo MISMO
            ' que su .json, y de yapa `EscalaEsUniforme` da True siempre ⇒ `AtributosPerEje()` sale vacio
            ' ⇒ ningun per-eje puede colarse por aca al XML compartido con BodySlide, sin guard extra.
            Dim perdidos = 0
            Dim peorPerdida As Single = 0
            For Each sk In SkeletonInstance.Default.SkeletonDictionary
                ' ⛔ SIN la capa de física. Una pose exportada es un archivo PERSISTIDO que el usuario
                ' publica: si se horneara el delta de física, el .json traería la tela del frame exacto
                ' en que se exportó, y al reabrirlo la pose vendría con esa tela congelada encima.
                ' La física es del render, no de la pose. (Este es el único serializador fuera de la
                ' librería que lee la composición del hueso, así que es el único sitio que lo necesita.)
                Dim tr = sk.Value.LocaLTransformWithoutPhysics
                Dim exacto As Boolean = True
                Dim escalar = tr.EscalaComoEscalar(exacto)
                If Not exacto Then
                    perdidos += 1
                    ' CUANTO se perdio, no solo cuantos. `EscalaComoEscalar` devuelve eff.X, asi que un
                    ' hueso con per-eje (1, k, k) -escala solo en Y/Z, la forma que arma el NNAM del
                    ' cuello- proyecta a 1.0: se pierde la escala ENTERA, no un 2 %. Con un contador de
                    ' bultos los dos casos se ven igual.
                    Dim ef = tr.EffectiveScale
                    peorPerdida = Math.Max(peorPerdida, Math.Max(Math.Abs(ef.Y - escalar), Math.Abs(ef.Z - escalar)))
                End If
                Dim nuevo As New PoseTransformData With {
                    .X = tr.Translation.X,
                    .Y = tr.Translation.Y,
                    .Z = tr.Translation.Z,
                    .Scale = escalar
                }
                Dim degs = Transform_Class.Matrix33ToEulerXYZ(tr.Rotation)
                nuevo.Yaw = degs.X
                nuevo.Pitch = degs.Y
                nuevo.Roll = degs.Z
                Export.Transforms.Add(sk.Key, nuevo)
            Next

            ' Append portability bones the live skeleton lacks (HKX-defined). Live skeleton wins on collision.
            ' ⛔ LOS EXTRA TAMBIEN SE PROYECTAN. Entraban VERBATIM, esquivando la proyeccion que este
            ' mismo bloque instala tres lineas arriba: con escala per-eje su `Scale` vale 1.0 y el .json
            ' salia con `scale: 1` para esos huesos — la escala ENTERA perdida, que es el mismo defecto
            ' que se acaba de arreglar, entrando por la otra puerta.
            If extraBones IsNot Nothing Then
                For Each kv In extraBones
                    If Export.Transforms.ContainsKey(kv.Key) Then Continue For
                    Dim src = kv.Value
                    Dim ef As Single = src.Scale * src.ScaleX
                    ' La uniformidad la decide la ley de la clase, no un umbral escrito aca: tener
                    ' dos respuestas posibles a la misma pregunta, con tolerancias distintas, es drift.
                    If Not Transform_Class.EsUniformeExacta(New System.Numerics.Vector3(src.ScaleX, src.ScaleY, src.ScaleZ)) Then
                        perdidos += 1
                        peorPerdida = Math.Max(peorPerdida, Math.Max(Math.Abs(src.Scale * src.ScaleY - ef),
                                                                     Math.Abs(src.Scale * src.ScaleZ - ef)))
                    End If
                    Export.Transforms.Add(kv.Key, New PoseTransformData With {
                        .X = src.X, .Y = src.Y, .Z = src.Z,
                        .Yaw = src.Yaw, .Pitch = src.Pitch, .Roll = src.Roll,
                        .Scale = ef})
                Next
            End If

            ' ⛔ EL LOG VA ACA, DESPUES de los extraBones. Estaba ARRIBA del bucle, o sea que
            ' `perdidos` y `peorPerdida` se incrementaban en huesos extra que nadie llegaba a leer:
            ' escrituras sin lector. Si la perdida ocurria SOLO en huesos extra, no se logueaba nada --
            ' el fallo mudo se reintroducia en el mismo cambio que vino a sacarlo.
            If perdidos > 0 Then
                Dim peorL = peorPerdida
                Logger.LogLazy(Function() $"[SAM-EXPORT] peor desvio contra la proyeccion: {peorL:F4}")
                Logger.LogLazy(Function() $"[SAM-EXPORT] {perdidos} hueso(s) tenian escala PER-EJE y el " &
                                          "formato SAM solo admite un escalar: se exporto scale_eff.X. " &
                                          "El per-eje completo si viaja en el XML propio de WM.")
            End If

            If IO.Directory.Exists(Wardrobe_Manager_Form.Directorios.PosesSAMRoot) = False Then
                IO.Directory.CreateDirectory(Wardrobe_Manager_Form.Directorios.PosesSAMRoot)
            End If
            Dim jsonOut As String = JsonSerializer.Serialize(Of Poses_class)(Export, _samExportOpts)
            ' ⛔ NO `IO.File.WriteAllText`: CREATE_ALWAYS sobre un destino OCULTO da ACCESS_DENIED y el
            ' archivo sale de su mod bajo MO2/Vortex. Con copia: el export SAM va al árbol de F4SE
            ' (SAF\Poses\Exports) y puede estar pisando una pose que el usuario exportó antes o que
            ' aporta otro mod. Ver Ba2_Bsa_Library\EscrituraEnElLugar.vb.
            ' conBom:=False = lo que emitía WriteAllText sin encoding (UTF8NoBOM).
            EscribirTextoUtf8(Export.Filename, jsonOut, conCopia:=True, conBom:=False)
            Return Export
        Catch ex As Exception
            ' ⚠️ CATCH MUDO PREEXISTENTE, y sigue mudo: sacarlo es cambiar la conducta del export SAM,
            ' que está fuera del alcance de esta ronda. Queda anotado en el censo de catch mudos.
            Return Nothing
        End Try
    End Function
End Module
