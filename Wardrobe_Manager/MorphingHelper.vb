Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Imports FO4_Base_Library
Imports FO4_Base_Library.RecalcTBN

Public Class MorphingHelper
    Friend Shared Sub LoadMorphTargets(shape As Shape_class, ByRef Geometry As SkinnedGeometry)
        ' C-3: Skip rebuild if morph data is already cached (invalidated via InvalidateShapeDataLookupCache)
        If shape.MorphDiffs IsNot Nothing Then Exit Sub

        ' 1) Inicializar el diccionario
        shape.MorphDiffs = New Dictionary(Of String, List(Of MorphData))
        ' 2) Número de vértices en el mesh base
        Dim count = Geometry.BaseVertices.Length
        ' 3) Para cada elemento de Related_Slider_data (uno por slider aplicado a esta shape)
        For Each sd In shape.Related_Slider_data.
            GroupBy(Function(pf) pf.ParentSlider.Nombre, StringComparer.OrdinalIgnoreCase).
            Select(Function(g) g.OrderByDescending(Function(pf) pf.Islocal).First()).
            ToList()
            Dim sliderName = sd.ParentSlider.Nombre
            Dim lista As New List(Of MorphData)
            shape.MorphDiffs.Add(sliderName, lista)
            ' 5) Cada bloque OSD aporta DataDiff con (Index, X,Y,Z)
            ' D-2: Use compact arrays when available for cache-friendly iteration
            For Each block As OSD_Block_Class In sd.RelatedOSDBlocks
                If block.IndicesCompact IsNot Nothing AndAlso block.IndicesCompact.Length = block.DataDiff.Count Then
                    Dim idx = block.IndicesCompact
                    Dim dlt = block.DeltasCompact
                    For j = 0 To idx.Length - 1
                        If idx(j) >= 0 AndAlso idx(j) < count Then
                            lista.Add(New MorphData With {.index = CUInt(idx(j)), .PosDiff = New Vector3(dlt(j * 3), dlt(j * 3 + 1), dlt(j * 3 + 2))})
                        End If
                    Next
                Else
                    For Each d As OSD_DataDiff_Class In block.DataDiff
                        Dim i = CInt(d.Index)
                        If i >= 0 AndAlso i < count Then
                            lista.Add(New MorphData With {.index = CUInt(i), .PosDiff = New Vector3(d.X, d.Y, d.Z)})
                        End If
                    Next
                End If
            Next
        Next
    End Sub
    Public Shared Sub ApplyMorph_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, RecalculateNormals As Boolean, AllowMask As Boolean)
        Dim count = Geometry.NifLocalVertices.Length
        ' Start from NIF local space (pre-skinning) so deltas are applied in the correct space
        Dim verts = Geometry.NifLocalVertices.ToArray()

        LoadMorphTargets(shape, Geometry)
        ' Reiniciar máscara y dirty-tracking
        ApplyMask_CPU(shape, Geometry, AllowMask)
        Geometry.dirtyVertexIndices.Clear()

        Dim sliders = shape.Related_Sliders

        For Each s In sliders
            Dim raw = s.Current_Setting
            Dim t = Math.Clamp(raw / 100, 0.0F, 1.0F)
            If Single.IsNaN(t) Then t = 0

            If s.Invert Then t = 1.0F - t

            If s.IsZap Then
                ' Sólo cambia máscara
                For Each morph In shape.MorphDiffs(s.Nombre)
                    Dim i = CInt(morph.index)
                    If shape.ApplyZaps = True Then
                        Geometry.VertexMask(i) = -t

                    End If
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                Next
            Else
                ' Morph normal: mueve vértice en espacio local NIF
                ' O2.3: Skip morph deltas with negligible magnitude
                For Each morph In shape.MorphDiffs(s.Nombre)
                    Dim i = CInt(morph.index)
                    Dim delta = morph.PosDiff * t
                    If delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z < 0.000001F Then Continue For
                    verts(i) = verts(i) + delta
                Next
            End If
        Next

        ' GPU skinning: morphed verts stay in local space — GPU will transform them
        ' (ApplySkinningToLocalVerts removed — no longer needed)

        For i = 0 To count - 1
            If Geometry.Vertices(i) <> verts(i) Then
                Geometry.dirtyVertexIndices.Add(i)
                Geometry.dirtyVertexFlags(i) = True
            Else
                Geometry.dirtyVertexFlags(i) = False
            End If
        Next
        ' O2.3: If dirty count exceeds 60% of vertex count, mark all dirty (full update is cheaper than sparse HashSet lookups)
        If Geometry.dirtyVertexIndices.Count > count * 0.6 Then
            Geometry.dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, count))
            For i = 0 To count - 1
                Geometry.dirtyVertexFlags(i) = True
            Next
        End If
        Geometry.Vertices = verts
        ' Invalidate world-space cache since local positions changed
        Geometry.WorldCacheValid = False
        Geometry.CachedWorldVertices = Nothing
        Geometry.CachedWorldNormals = Nothing
        If RecalculateNormals And Geometry.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(Geometry, opt)
            adicionales.ExceptWith(Geometry.dirtyVertexIndices)
            For Each ad In adicionales
                Geometry.dirtyVertexIndices.Add(ad)
                Geometry.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub

    Public Shared Sub RemoveZaps(shape As Shape_class, ByRef geom As SkinnedGeometry)

        If Not shape.ParentSliderSet.Sliders.Any(Function(pf) pf.IsZap) Then Exit Sub

        ' ==== 0) Datos locales / alias ====
        Dim tri As INiShape = geom.TriShape
        Dim vm = geom.VertexMask
        Dim nOld As Integer = geom.Vertices.Length
        Dim haszapped As Boolean = False

        ' ==== 1) Marcas a eliminar
        Dim removed(nOld - 1) As Boolean
        For i As Integer = 0 To nOld - 1
            removed(i) = (vm(i) < 0)
            haszapped = haszapped Or (vm(i) < 0)
        Next

        If Not haszapped Then Exit Sub

        ' ==== 2) old->new y compactación in-place de arrays en SkinnedGeometry ====
        Dim oldToNew(nOld - 1) As Integer
        For i As Integer = 0 To nOld - 1 : oldToNew(i) = -1 : Next

        Dim w As Integer = 0

        Dim V = geom.Vertices
        Dim VB = geom.BaseVertices
        Dim N = geom.Normals
        Dim T = geom.Tangents
        Dim B = geom.Bitangents
        Dim UVW = geom.Uvs_Weight
        Dim VC = geom.VertexColors
        Dim ED = geom.Eyedata
        Dim VD = geom.VertexData
        Dim VDSSE = geom.VertexDataSSE
        ' NOTA: si VD es List, la paso a array para compactar in-place y luego la rearmo
        Dim VDarr() As BSVertexData = Nothing
        Dim VDarrsse() As BSVertexDataSSE = Nothing

        If geom.Version.IsSSE Then
            VDarrsse = VDSSE.ToArray()
        Else
            VDarr = VD.ToArray()
        End If

        For i As Integer = 0 To nOld - 1
            If Not removed(i) Then
                oldToNew(i) = w
                ' copiar structs tal cual (sin new)
                V(w) = V(i)
                VB(w) = VB(i)
                N(w) = N(i)
                T(w) = T(i)
                B(w) = B(i)
                UVW(w) = UVW(i)
                VC(w) = VC(i)
                If geom.Version.IsSSE Then
                    VDarrsse(w) = VDarrsse(i)
                Else
                    VDarr(w) = VDarr(i)
                End If
                ED(w) = ED(i)
                w += 1
            End If
        Next

        Dim nNew As Integer = w

        ' Redimensionar solo una vez (sin recrear elementos)
        Array.Resize(V, nNew) : geom.Vertices = V
        Array.Resize(VB, nNew) : geom.BaseVertices = VB
        Array.Resize(N, nNew) : geom.Normals = N
        Array.Resize(T, nNew) : geom.Tangents = T
        Array.Resize(B, nNew) : geom.Bitangents = B
        Array.Resize(UVW, nNew) : geom.Uvs_Weight = UVW
        Array.Resize(VC, nNew) : geom.VertexColors = VC
        Array.Resize(ED, nNew) : geom.Eyedata = ED

        ' Rehacer la lista con backing array ya compactado
        If geom.Version.IsSSE Then
            VDSSE.Clear() : VDSSE.Capacity = nNew : VDSSE.AddRange(VDarrsse.AsSpan(0, nNew).ToArray())
        Else
            VD.Clear() : VD.Capacity = nNew : VD.AddRange(VDarr.AsSpan(0, nNew).ToArray())
        End If


        ' ==== 3) Reindexado de triángulos con mínima asignación ====
        Dim idxArr = geom.Indices
        Dim tmpTris(idxArr.Length \ 3 - 1) As Triangle
        Dim w2 As Integer = 0

        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            Dim n1 = oldToNew(CInt(idxArr(tr)))
            Dim n2 = oldToNew(CInt(idxArr(tr + 1)))
            Dim n3 = oldToNew(CInt(idxArr(tr + 2)))
            If n1 >= 0 AndAlso n2 >= 0 AndAlso n3 >= 0 Then
                tmpTris(w2) = New Triangle(n1, n2, n3)
                w2 += 1

            End If
        Next

        If w2 < tmpTris.Length Then ReDim Preserve tmpTris(w2 - 1)

        Dim newIdx(3 * w2 - 1) As UInteger
        For i As Integer = 0 To w2 - 1
            Dim t2 = tmpTris(i)
            Dim base3 = 3 * i
            newIdx(base3) = CUInt(t2.V1)
            newIdx(base3 + 1) = CUInt(t2.V2)
            newIdx(base3 + 2) = CUInt(t2.V3)
        Next
        geom.Indices = newIdx

        ' Cache TBN vaciar para recalcular
        geom.CachedTBN = Nothing

        ' ==== 3b) Remap skin partition body-part assignments ====
        ' After vertex compaction the partition's TrianglesCopy still holds old indices.
        ' Remap them so UpdateSkinPartitions can match triangles to the correct body parts.
        Dim remapDict As New Dictionary(Of Integer, Integer)(nNew)
        For i As Integer = 0 To nOld - 1
            If oldToNew(i) >= 0 Then remapDict(i) = oldToNew(i)
        Next
        shape.ParentSliderSet.NIFContent.RemapSkinPartitionTriangles(geom.TriShape, remapDict)

        ' ==== 4) Reindexado de morphs
        For Each dat In shape.Related_Slider_data.
            GroupBy(Function(pf) pf.Nombre, StringComparer.OrdinalIgnoreCase).
            Select(Function(g) g.OrderByDescending(Function(pf) pf.Islocal).First()).
            ToList()
            For Each block In dat.MaterializeEditableLocalBlocks().ToList()
                For Each ddiff In block.DataDiff.ToList()
                    Dim oldIdx As Integer = CInt(ddiff.Index)
                    If oldIdx < 0 OrElse oldIdx >= oldToNew.Length Then
                        Debugger.Break()
                        block.DataDiff.Remove(ddiff)
                        Continue For
                    End If
                    ddiff.Index = oldToNew(oldIdx)
                    If ddiff.Index < 0 Then
                        block.DataDiff.Remove(ddiff)
                    End If
                Next
                If block.DataDiff.Count = 0 Then
                    block.ParentOSDContent.Blocks.Remove(block)
                Else
                    block.RebuildCompactArrays()
                End If
            Next
        Next
    End Sub

    Private Shared Sub ApplyMask_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, AllowMask As Boolean)
        Dim count = Geometry.BaseVertices.Length
        If Not AllowMask Then
            Array.Clear(Geometry.VertexMask, 0, count)
            Geometry.dirtyMaskIndices.Clear()
            For i = 0 To count - 1
                Geometry.dirtyMaskFlags(i) = False
            Next
        Else
            Dim maskeds = shape.MaskedVertices
            For i = 0 To count - 1
                If Geometry.VertexMask(i) = -1 Then
                    If shape.MaskedVertices.Contains(i) Then Geometry.VertexMask(i) = 1 Else Geometry.VertexMask(i) = 0
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                End If
                If (Geometry.VertexMask(i) = 0 AndAlso maskeds.Contains(i)) OrElse (Geometry.VertexMask(i) = 1 AndAlso Not maskeds.Contains(i)) Then
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                    Geometry.VertexMask(i) = 1 - Geometry.VertexMask(i)
                Else
                    If Geometry.VertexMask(i) <> 0 Then
                        Geometry.dirtyMaskIndices.Add(i)
                        Geometry.VertexMask(i) = Geometry.VertexMask(i)
                        Geometry.dirtyMaskFlags(i) = True
                    Else
                        Geometry.dirtyMaskFlags(i) = False
                    End If
                End If
            Next
        End If
    End Sub
End Class

''' <summary>
''' IMorphResolver implementation for Wardrobe Manager's slider-based morphs.
''' Resolves OSD slider data into generic MorphChannels that MorphEngine can apply.
''' </summary>
Public Class SliderMorphResolver
    Implements IMorphResolver

    Public Function ResolveMorphPlan(shape As IRenderableShape, geom As SkinnedGeometry) As MorphPlan Implements IMorphResolver.ResolveMorphPlan
        Dim plan As New MorphPlan
        Dim wmShape = TryCast(shape, Shape_class)
        If wmShape Is Nothing Then Return plan

        ' Reuse LoadMorphTargets to avoid duplicating morph-loading logic
        MorphingHelper.LoadMorphTargets(wmShape, geom)

        ' Build channels from active sliders
        For Each s In wmShape.Related_Sliders
            Dim raw = s.Current_Setting
            Dim t = Math.Clamp(raw / 100, 0.0F, 1.0F)
            If Single.IsNaN(t) Then t = 0
            If s.Invert Then t = 1.0F - t

            Dim deltas As List(Of MorphData) = Nothing
            If wmShape.MorphDiffs.TryGetValue(s.Nombre, deltas) Then
                plan.Channels.Add(New MorphChannel(s.Nombre, t, deltas, s.IsZap AndAlso wmShape.ApplyZaps))
            End If
        Next

        Return plan
    End Function
End Class

''' <summary>
''' IGeometryModifier implementation for WM's zap removal (topology compaction).
''' Removes vertices marked with negative mask values and reindexes triangles + morphs.
''' </summary>
Public Class ZapGeometryModifier
    Implements IGeometryModifier

    Public Sub Apply(shape As IRenderableShape, ByRef geom As SkinnedGeometry) Implements IGeometryModifier.Apply
        Dim wmShape = TryCast(shape, Shape_class)
        If wmShape IsNot Nothing Then
            MorphingHelper.RemoveZaps(wmShape, geom)
        End If
    End Sub
End Class
