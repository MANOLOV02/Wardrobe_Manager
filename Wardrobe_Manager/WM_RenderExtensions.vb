' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
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
        FilesDictionary_class.RegisterExtensions(".osp", ".xml")
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

        Cursor.Current = Cursors.WaitCursor
        OSP_Project_Class.PinnedForPreview = seleccionado

        ' Snapshot previous state for change detection
        Dim prevPreset = s.Last_Preset
        Dim prevSize = s.Last_size

        ' Apply slider weights from preset
        seleccionado.SetPreset(Preset, weight)
        s.Last_size = weight
        s.Last_Preset = Preset

        ' Detect what changed
        Dim sameSet = (s.Last_rendered Is seleccionado) AndAlso ctrl.Model.Cleaned = False AndAlso Force = False
        Dim presetChanged = Not (prevPreset Is Preset) OrElse (prevSize <> weight)
        ' Pose change detected against the SkeletonInstance's last applied pose (only used to
        ' pick the dirty flag below). Apply pose UNCONDITIONALLY here — idempotent and trivial
        ' (~200 bones × ~5µs), guarantees DeltaTransforms reflect the requested pose even if
        ' another flow (e.g. CreatefromNif with Pose=Nothing) reset them in between frames.
        Dim poseChanged = Not (SkeletonInstance.Default.Pose Is Pose)
        SkeletonInstance.Default.ApplyPose(Pose)

        ' Fill the intent — the pipeline decides HOW based on dirty flags.
        Dim intent = ctrl.Intent
        intent.Shapes = seleccionado.Shapes
        intent.FloorOffset = -seleccionado.HighHeelHeight
        intent.RecalculateNormals = ctrl.Model.RecalculateNormals
        intent.SkeletonResolver = Nothing  ' default skeleton resolver
        intent.MorphResolver = New SliderMorphResolver()
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
            intent.MarkDirty(RenderDirtyFlags.Pose Or RenderDirtyFlags.Camera)
            If presetChanged Then intent.MarkDirty(RenderDirtyFlags.Morphs)

        Else
            ' Morph-only: same set, same pose, preset/size may have changed
            intent.MarkDirty(RenderDirtyFlags.Morphs Or RenderDirtyFlags.Textures)
        End If

        ' Signal the control — pipeline executes synchronously for now
        ctrl.InvalidateRender()

        _sw.Stop()
        ctrl.LastUpdateMs = _sw.Elapsed.TotalMilliseconds
        Cursor.Current = Cursors.Default
    End Sub
End Module
