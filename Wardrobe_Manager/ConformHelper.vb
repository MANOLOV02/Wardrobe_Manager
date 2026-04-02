' Version Uploaded of Wardrobe 2.1.3
Imports OpenTK.Mathematics
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Slider conform via BVH closest-point projection onto the source surface.
''' For each target vertex the closest triangle on the source mesh is found and
''' the three source-vertex deltas are barycentric-interpolated.  This gives
''' smoother, gap-free results compared to k-nearest-vertex approaches.
'''
''' UI parameters match BodySlide conventions (Search Radius / Axis flags).
''' Uses BvhHelper for shared AABB / BvhNode / BuildBvh infrastructure.
''' </summary>
Public Class ConformHelper

    ' ─── Settings ─────────────────────────────────────────────────────────────

    Public Structure ConformSettings
        ''' <summary>
        ''' Maximum distance in NIF units from the target vertex to the nearest point on the
        ''' source surface.  0 = unlimited (always project).
        ''' BodySlide equivalent: Search Radius (default 10).
        ''' </summary>
        Public SearchRadius As Single
        ''' <summary>Apply delta on X axis.</summary>
        Public AxisX As Boolean
        ''' <summary>Apply delta on Y axis.</summary>
        Public AxisY As Boolean
        ''' <summary>Apply delta on Z axis.</summary>
        Public AxisZ As Boolean
        ''' <summary>When True, overwrite OSD blocks that already have data.</summary>
        Public Overwrite As Boolean

        Public Shared Function BodySlideDefault() As ConformSettings
            Return New ConformSettings With {
                .SearchRadius = 10.0F,
                .AxisX = True, .AxisY = True, .AxisZ = True,
                .Overwrite = False
            }
        End Function
    End Structure

    ' ─── Result ───────────────────────────────────────────────────────────────

    Public Class ConformResult
        Public ReadOnly SliderName As String
        Public ReadOnly Deltas As List(Of OSD_DataDiff_Class)
        Public Sub New(sliderName As String)
            Me.SliderName = sliderName
            Deltas = New List(Of OSD_DataDiff_Class)()
        End Sub
    End Class

    ' ─── Triangle BVH ─────────────────────────────────────────────────────────

    Private Structure TriData
        Public I0, I1, I2 As Integer  ' original vertex indices
        Public V0, V1, V2 As Vector3  ' cached positions
        Public Centroid As Vector3
        Public Bounds As AABB
    End Structure

    Private Shared Function BuildTriBvh(positions As Vector3(),
                                         tris As List(Of NiflySharp.Structs.Triangle)) As (root As BvhNode, triArr As TriData())
        Dim arr(tris.Count - 1) As TriData
        For i = 0 To tris.Count - 1
            Dim t = tris(i)
            Dim v0 = positions(CInt(t(0)))
            Dim v1 = positions(CInt(t(1)))
            Dim v2 = positions(CInt(t(2)))
            arr(i).I0 = CInt(t(0)) : arr(i).I1 = CInt(t(1)) : arr(i).I2 = CInt(t(2))
            arr(i).V0 = v0 : arr(i).V1 = v1 : arr(i).V2 = v2
            arr(i).Centroid = (v0 + v1 + v2) / 3.0F
            arr(i).Bounds = AABB.FromTriangle(v0, v1, v2)
        Next
        Dim bounds(arr.Length - 1) As AABB
        Dim centroids(arr.Length - 1) As Vector3
        For i = 0 To arr.Length - 1
            bounds(i) = arr(i).Bounds
            centroids(i) = arr(i).Centroid
        Next
        Dim indices = Enumerable.Range(0, arr.Length).ToArray()
        Dim root = BvhHelper.BuildBvh(bounds, centroids, indices, 0, indices.Length, 0)
        Return (root, arr)
    End Function

    ' ─── Closest-point on triangle (Ericson §5.1.5) ──────────────────────────

    Private Shared Function ClosestPointOnTriangle(p As Vector3, a As Vector3, b As Vector3, c As Vector3,
                                                    ByRef b0 As Single, ByRef b1 As Single, ByRef b2 As Single) As Vector3
        Dim ab = b - a : Dim ac = c - a : Dim ap = p - a
        Dim d1 = Vector3.Dot(ab, ap) : Dim d2 = Vector3.Dot(ac, ap)
        If d1 <= 0.0F AndAlso d2 <= 0.0F Then b0 = 1 : b1 = 0 : b2 = 0 : Return a

        Dim bp = p - b
        Dim d3 = Vector3.Dot(ab, bp) : Dim d4 = Vector3.Dot(ac, bp)
        If d3 >= 0.0F AndAlso d4 <= d3 Then b0 = 0 : b1 = 1 : b2 = 0 : Return b

        Dim vc = d1 * d4 - d3 * d2
        If vc <= 0.0F AndAlso d1 >= 0.0F AndAlso d3 <= 0.0F Then
            Dim v = d1 / (d1 - d3) : b0 = 1 - v : b1 = v : b2 = 0 : Return a + ab * v
        End If

        Dim cp = p - c
        Dim d5 = Vector3.Dot(ab, cp) : Dim d6 = Vector3.Dot(ac, cp)
        If d6 >= 0.0F AndAlso d5 <= d6 Then b0 = 0 : b1 = 0 : b2 = 1 : Return c

        Dim vb = d5 * d2 - d1 * d6
        If vb <= 0.0F AndAlso d2 >= 0.0F AndAlso d6 <= 0.0F Then
            Dim w = d2 / (d2 - d6) : b0 = 1 - w : b1 = 0 : b2 = w : Return a + ac * w
        End If

        Dim va = d3 * d6 - d5 * d4
        If va <= 0.0F AndAlso (d4 - d3) >= 0.0F AndAlso (d5 - d6) >= 0.0F Then
            Dim w = (d4 - d3) / ((d4 - d3) + (d5 - d6)) : b0 = 0 : b1 = 1 - w : b2 = w : Return b + (c - b) * w
        End If

        Dim denom = 1.0F / (va + vb + vc)
        Dim bv = vb * denom : Dim bw = vc * denom
        b0 = 1 - bv - bw : b1 = bv : b2 = bw : Return a + ab * bv + ac * bw
    End Function

    Private Shared Sub ClosestOnBvh(node As BvhNode, triArr As TriData(), query As Vector3,
                                     ByRef bestSq As Single, ByRef bestTri As Integer,
                                     ByRef bestB0 As Single, ByRef bestB1 As Single, ByRef bestB2 As Single)
        If node.Bounds.MinSqDist(query) >= bestSq Then Return
        If node.IsLeaf Then
            For Each ti In node.LeafIndices
                Dim lb0, lb1, lb2 As Single
                Dim cp = ClosestPointOnTriangle(query, triArr(ti).V0, triArr(ti).V1, triArr(ti).V2, lb0, lb1, lb2)
                Dim sq = (cp - query).LengthSquared
                If sq < bestSq Then
                    bestSq = sq : bestTri = ti
                    bestB0 = lb0 : bestB1 = lb1 : bestB2 = lb2
                End If
            Next
        Else
            Dim dL = If(node.Left IsNot Nothing, node.Left.Bounds.MinSqDist(query), Single.MaxValue)
            Dim dR = If(node.Right IsNot Nothing, node.Right.Bounds.MinSqDist(query), Single.MaxValue)
            If dL <= dR Then
                If node.Left IsNot Nothing Then ClosestOnBvh(node.Left, triArr, query, bestSq, bestTri, bestB0, bestB1, bestB2)
                If node.Right IsNot Nothing Then ClosestOnBvh(node.Right, triArr, query, bestSq, bestTri, bestB0, bestB1, bestB2)
            Else
                If node.Right IsNot Nothing Then ClosestOnBvh(node.Right, triArr, query, bestSq, bestTri, bestB0, bestB1, bestB2)
                If node.Left IsNot Nothing Then ClosestOnBvh(node.Left, triArr, query, bestSq, bestTri, bestB0, bestB1, bestB2)
            End If
        End If
    End Sub

    ' ─── Public API ───────────────────────────────────────────────────────────

    ''' <summary>
    ''' Background-safe computation.  Builds one BVH from the source surface, then for each
    ''' slider projects every target vertex onto the nearest triangle and barycentric-interpolates
    ''' the source deltas.  SearchRadius=0 means unlimited (always project).
    ''' </summary>
    Public Shared Function ComputeConform(
            sourcePositions As Vector3(),
            sourceTris As List(Of NiflySharp.Structs.Triangle),
            sliderDeltas As List(Of (SliderName As String, Deltas As Dictionary(Of Integer, Vector3))),
            targetPositions As Vector3(),
            settings As ConformSettings,
            progress As IProgress(Of Integer),
            ct As CancellationToken) As List(Of ConformResult)

        Dim bvh = BuildTriBvh(sourcePositions, sourceTris)
        Dim root = bvh.root
        Dim triArr = bvh.triArr
        Dim maxSq = If(settings.SearchRadius > 0, settings.SearchRadius * settings.SearchRadius, Single.MaxValue)

        Dim results As New List(Of ConformResult)
        Dim totalWork = CLng(sliderDeltas.Count) * targetPositions.Length
        Dim done As Long = 0

        For Each entry In sliderDeltas
            ct.ThrowIfCancellationRequested()
            Dim result As New ConformResult(entry.SliderName)
            Dim srcDeltas = entry.Deltas

            Dim perVertex(targetPositions.Length - 1) As (X As Single, Y As Single, Z As Single, HasValue As Boolean)

            Parallel.For(0, targetPositions.Length,
                New ParallelOptions With {.CancellationToken = ct},
                Sub(i)
                    Dim q = targetPositions(i)
                    Dim bestSq As Single = maxSq
                    Dim bestTri As Integer = -1
                    Dim b0, b1, b2 As Single
                    ClosestOnBvh(root, triArr, q, bestSq, bestTri, b0, b1, b2)

                    If bestTri >= 0 Then
                        Dim d0 = Vector3.Zero, d1v = Vector3.Zero, d2v = Vector3.Zero
                        srcDeltas.TryGetValue(triArr(bestTri).I0, d0)
                        srcDeltas.TryGetValue(triArr(bestTri).I1, d1v)
                        srcDeltas.TryGetValue(triArr(bestTri).I2, d2v)

                        Dim ax = If(settings.AxisX, b0 * d0.X + b1 * d1v.X + b2 * d2v.X, 0.0F)
                        Dim ay = If(settings.AxisY, b0 * d0.Y + b1 * d1v.Y + b2 * d2v.Y, 0.0F)
                        Dim az = If(settings.AxisZ, b0 * d0.Z + b1 * d1v.Z + b2 * d2v.Z, 0.0F)

                        If ax <> 0.0F OrElse ay <> 0.0F OrElse az <> 0.0F Then
                            perVertex(i) = (ax, ay, az, True)
                        End If
                    End If

                    Interlocked.Increment(done)
                    progress?.Report(CInt(done * 100L \ totalWork))
                End Sub)

            For i = 0 To perVertex.Length - 1
                If perVertex(i).HasValue Then
                    result.Deltas.Add(New OSD_DataDiff_Class() With {
                        .Index = i, .X = perVertex(i).X,
                        .Y = perVertex(i).Y, .Z = perVertex(i).Z})
                End If
            Next
            results.Add(result)
        Next

        Return results
    End Function

    Private Shared Function EnsureEditableLocalConformBlock(slider As Slider_class,
                                                            targetName As String,
                                                            blockName As String,
                                                            sliderSet As SliderSet_Class,
                                                            osdFilename As String) As OSD_Block_Class
        Dim dat = slider.Datas.FirstOrDefault(
            Function(d) d.Target.Equals(targetName, StringComparison.OrdinalIgnoreCase) AndAlso d.Islocal)
        If dat Is Nothing Then
            dat = slider.Datas.FirstOrDefault(
                Function(d) d.Target.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        End If
        If dat Is Nothing Then
            slider.Datas.Add(New Slider_Data_class(blockName, slider, targetName, osdFilename))
            dat = slider.Datas.Last()
        ElseIf Not dat.Islocal Then
            dat.MaterializeEditableLocalBlocks()
        End If

        Dim block = sliderSet.OSDContent_Local.Blocks.FirstOrDefault(
            Function(b) b.BlockName.Equals(blockName, StringComparison.OrdinalIgnoreCase))
        If block Is Nothing Then
            block = New OSD_Block_Class(sliderSet.OSDContent_Local) With {.BlockName = blockName}
            sliderSet.OSDContent_Local.Blocks.Add(block)
        End If

        Return block
    End Function

    ''' <summary>
    ''' Applies computed conform results to the OSD structure.  Call on the UI thread.
    ''' Follow with sliderSet.InvalidateAllLookupCaches() and Save_Shapedatas().
    ''' </summary>
    Public Shared Sub ApplyConformResults(
            results As List(Of ConformResult),
            targetShape As Shape_class,
            sliderSet As SliderSet_Class,
            overwrite As Boolean)

        Dim targetName = targetShape.Target
        Dim osdFilename = IO.Path.GetFileName(
            sliderSet.SourceFileFullPath).Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        For Each res In results
            If res.Deltas.Count = 0 Then Continue For

            Dim slider = sliderSet.Sliders.FirstOrDefault(
                Function(s) s.Nombre.Equals(res.SliderName, StringComparison.OrdinalIgnoreCase))
            If slider Is Nothing Then Continue For

            Dim blockName = targetName.Replace(":", "_") & slider.Nombre
            Dim block = EnsureEditableLocalConformBlock(slider, targetName, blockName, sliderSet, osdFilename)

            If Not overwrite AndAlso block.DataDiff.Count > 0 Then Continue For

            block.DataDiff.Clear()
            For Each d In res.Deltas
                block.DataDiff.Add(New OSD_DataDiff_Class() With {
                    .Index = d.Index, .X = d.X, .Y = d.Y, .Z = d.Z})
            Next
            block.RebuildCompactArrays()
        Next
    End Sub

End Class
