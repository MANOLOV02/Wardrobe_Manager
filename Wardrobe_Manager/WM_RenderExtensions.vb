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

    <Extension()>
    Public Sub Update_Render(ctrl As PreviewControl, seleccionado As SliderSet_Class, Force As Boolean,
                             Preset As SlidersPreset_Class, Pose As Poses_class, weight As WM_Config.SliderSize)
        Dim _sw As New System.Diagnostics.Stopwatch() : _sw.Start()
        If ctrl.Disposing OrElse ctrl.IsDisposed Then Exit Sub
        If Not ctrl.Visible Then Exit Sub

        Dim s = GetState(ctrl)

        If IsNothing(seleccionado) Then
            ctrl.Model.FloorOffset = 0
            ctrl.Processing_Status("Select project")
            Exit Sub
        End If

        If seleccionado.Unreadable_Project Then
            ctrl.Model.FloorOffset = 0
            ctrl.Processing_Status("Unreadable...")
            Exit Sub
        End If
        If seleccionado.BypassDiskShapeDataLoad = False Then
            If OSP_Project_Class.Load_and_Check_Shapedata(seleccionado, False) = False Then
                ctrl.Model.FloorOffset = 0
                ctrl.Processing_Status("Unreadable...")
                Exit Sub
            End If
        End If

        If Not ctrl.PlayingAnimation Then Cursor.Current = Cursors.WaitCursor
        OSP_Project_Class.PinnedForPreview = seleccionado

        ' Snapshot previous state for change detection
        Dim prevPreset = s.Last_Preset
        Dim prevSize = s.Last_size

        ' Detect what changed
        Dim sameSet = (s.Last_rendered Is seleccionado) AndAlso ctrl.Model.Cleaned = False AndAlso Force = False
        Dim presetChanged = Not (prevPreset Is Preset) OrElse (prevSize <> weight)
        Dim skipPresetApply = sameSet AndAlso Not presetChanged

        ' Apply slider weights from preset. During animation playback the pose changes every
        ' tick, but the slider preset usually does not, so avoid reapplying morph setup.
        If Not skipPresetApply Then
            seleccionado.SetPreset(Preset, weight)
            s.Last_size = weight
            s.Last_Preset = Preset
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
            For Each sk In SkeletonInstance.Default.SkeletonDictionary
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

            ' Append portability bones the live skeleton lacks (HKX-defined). Live skeleton wins on collision.
            If extraBones IsNot Nothing Then
                For Each kv In extraBones
                    If Not Export.Transforms.ContainsKey(kv.Key) Then Export.Transforms.Add(kv.Key, kv.Value)
                Next
            End If

            If IO.Directory.Exists(Wardrobe_Manager_Form.Directorios.PosesSAMRoot) = False Then
                IO.Directory.CreateDirectory(Wardrobe_Manager_Form.Directorios.PosesSAMRoot)
            End If
            Dim jsonOut As String = JsonSerializer.Serialize(Of Poses_class)(Export, _samExportOpts)
            IO.File.WriteAllText(Export.Filename, jsonOut)
            Return Export
        Catch ex As Exception
            Return Nothing
        End Try
    End Function
End Module
