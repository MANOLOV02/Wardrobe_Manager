' Version Uploaded of Wardrobe 3.1.0
Imports OpenTK.Mathematics
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' CPU BVH-based occlusion raytracer. Builds once from occluder meshes, then
''' ComputeOccludedVertices can be called to find hidden vertices on a target mesh.
''' All positions are world-space (obtained via SkinningHelper.GetWorldVertices/GetWorldNormals cache).
''' Uses BvhHelper for shared AABB/BvhNode/BuildBvh infrastructure.
''' </summary>
Public Class OcclusionRaytracer

    Public Structure RaycastSettings
        ''' <summary>Number of hemisphere rays per vertex for occlusion test.</summary>
        Public RayCount As Integer
        ''' <summary>Offset along vertex normal before casting, to avoid self-intersection (NIF units).</summary>
        Public NormalBias As Single
        ''' <summary>Max ray distance. 0 = auto (2x scene diagonal).</summary>
        Public MaxDistance As Single
        ''' <summary>Fraction of hemisphere rays that must hit occluders (0.5–1.0). 1.0 = certainty.</summary>
        Public OcclusionThreshold As Single
        ''' <summary>When True, mask only vertices belonging to fully occluded triangles (all 3 verts hidden).</summary>
        Public MaskCompleteTrianglesOnly As Boolean
        ''' <summary>
        ''' When True, rays that escape external occluders are also tested against the target
        ''' shape itself. Handles cases like torso blocked by its own legs below a dress hem.
        ''' </summary>
        Public IncludeSelfOcclusion As Boolean
        ''' <summary>
        ''' Minimum hit distance for self-occlusion rays, to skip the local surface.
        ''' 0 = auto (5% of target bounding diagonal, min 1 NIF unit).
        ''' </summary>
        Public SelfMinDistance As Single

        Public Shared Function Balanced() As RaycastSettings
            Return New RaycastSettings With {
                .RayCount = 64,
                .NormalBias = 0.5F,
                .MaxDistance = 0,
                .OcclusionThreshold = 1.0F,
                .MaskCompleteTrianglesOnly = True,
                .IncludeSelfOcclusion = True,
                .SelfMinDistance = 0
            }
        End Function
    End Structure

    ' ─── Tri storage (positions cached; AABB/BvhNode from BvhHelper) ──────────

    Private Structure TriData
        Public V0, V1, V2 As Vector3
        Public Centroid As Vector3
        Public Bounds As AABB
    End Structure

    ' ─── Instance state ──────────────────────────────────────────────────────

    Private ReadOnly _tris As TriData()
    Private ReadOnly _root As BvhNode
    Private ReadOnly _sceneDiag As Single

    ''' <summary>
    ''' Builds the BVH from all occluder meshes (world-space Meshgeometry.Vertices).
    ''' Call once; then reuse for multiple ComputeOccludedVertices calls.
    ''' </summary>
    Public Sub New(occluderMeshes As IEnumerable(Of PreviewModel.RenderableMesh))
        Dim triList As New List(Of TriData)
        For Each mesh In occluderMeshes
            AppendMeshTris(triList, mesh)
        Next
        _tris = triList.ToArray()
        If _tris.Length > 0 Then
            _root = BuildBvhFromTris(_tris)
            _sceneDiag = (_root.Bounds.Max - _root.Bounds.Min).Length
        End If
    End Sub

    Public ReadOnly Property HasOccluders As Boolean
        Get
            Return _root IsNot Nothing
        End Get
    End Property

    ' ─── Mesh ingestion ──────────────────────────────────────────────────────

    Private Shared Sub AppendMeshTris(triList As List(Of TriData), mesh As PreviewModel.RenderableMesh)
        Dim verts = If(mesh?.MeshData?.Meshgeometry.Vertices IsNot Nothing, SkinningHelper.GetWorldVertices(mesh.MeshData.Meshgeometry), Nothing)
        Dim idx = mesh?.MeshData?.Meshgeometry.Indices
        If verts Is Nothing OrElse idx Is Nothing OrElse idx.Length < 3 Then Exit Sub

        Dim i = 0
        While i + 2 < idx.Length
            Dim v0 = ToV3(verts(CInt(idx(i))))
            Dim v1 = ToV3(verts(CInt(idx(i + 1))))
            Dim v2 = ToV3(verts(CInt(idx(i + 2))))
            Dim td As TriData
            td.V0 = v0 : td.V1 = v1 : td.V2 = v2
            td.Centroid = (v0 + v1 + v2) / 3.0F
            td.Bounds = AABB.FromTriangle(v0, v1, v2)
            triList.Add(td)
            i += 3
        End While
    End Sub

    ''' <summary>Extracts bounds/centroids from a TriData array and delegates to BvhHelper.BuildBvh.</summary>
    Private Shared Function BuildBvhFromTris(tris As TriData()) As BvhNode
        Dim bounds(tris.Length - 1) As AABB
        Dim centroids(tris.Length - 1) As Vector3
        For i = 0 To tris.Length - 1
            bounds(i) = tris(i).Bounds
            centroids(i) = tris(i).Centroid
        Next
        Dim indices = Enumerable.Range(0, tris.Length).ToArray()
        Return BvhHelper.BuildBvh(bounds, centroids, indices, 0, indices.Length, 0)
    End Function

    ' ─── Ray–BVH traversal ───────────────────────────────────────────────────

    ''' <summary>
    ''' Returns True if the ray hits any triangle in [root/tris] with minDist &lt; t &lt;= maxDist.
    ''' minDist &gt; 0 skips hits too close to the origin (used for self-occlusion).
    ''' </summary>
    Private Shared Function RayHitsBvh(root As BvhNode, tris As TriData(),
                                        orig As Vector3, dir As Vector3,
                                        minDist As Single, maxDist As Single) As Boolean
        If root Is Nothing Then Return False

        Dim dirInv = SafeInv(dir)
        Dim stack As New Stack(Of BvhNode)(32)
        stack.Push(root)

        While stack.Count > 0
            Dim n = stack.Pop()
            If Not n.Bounds.Intersects(orig, dirInv) Then Continue While

            If n.IsLeaf Then
                For Each ti In n.LeafIndices
                    Dim t As Single
                    If MollerTrumbore(orig, dir, tris(ti), t) AndAlso t > minDist AndAlso t <= maxDist Then
                        Return True
                    End If
                Next
            Else
                If n.Left IsNot Nothing Then stack.Push(n.Left)
                If n.Right IsNot Nothing Then stack.Push(n.Right)
            End If
        End While

        Return False
    End Function

    ''' <summary>
    ''' Counts intersections with [root/tris] for inside-mesh parity test.
    ''' Only used for external occluders (minDist = 0 implied).
    ''' </summary>
    Private Shared Function CountBvhIntersections(root As BvhNode, tris As TriData(),
                                                    orig As Vector3, dir As Vector3,
                                                    maxDist As Single) As Integer
        If root Is Nothing Then Return 0

        Dim dirInv = SafeInv(dir)
        Dim count = 0
        Dim stack As New Stack(Of BvhNode)(32)
        stack.Push(root)

        While stack.Count > 0
            Dim n = stack.Pop()
            If Not n.Bounds.Intersects(orig, dirInv) Then Continue While

            If n.IsLeaf Then
                For Each ti In n.LeafIndices
                    Dim t As Single
                    If MollerTrumbore(orig, dir, tris(ti), t) AndAlso t > 0.0F AndAlso t <= maxDist Then
                        count += 1
                    End If
                Next
            Else
                If n.Left IsNot Nothing Then stack.Push(n.Left)
                If n.Right IsNot Nothing Then stack.Push(n.Right)
            End If
        End While

        Return count
    End Function

    ' ─── Geometry helpers ────────────────────────────────────────────────────

    Private Shared Function MollerTrumbore(orig As Vector3, dir As Vector3, tri As TriData, ByRef t As Single) As Boolean
        Const EPS As Single = 1e-7F
        Dim edge1 = tri.V1 - tri.V0
        Dim edge2 = tri.V2 - tri.V0
        Dim h = Vector3.Cross(dir, edge2)
        Dim a = Vector3.Dot(edge1, h)
        If Math.Abs(a) < EPS Then t = 0 : Return False
        Dim f = 1.0F / a
        Dim s = orig - tri.V0
        Dim u = f * Vector3.Dot(s, h)
        If u < 0.0F OrElse u > 1.0F Then t = 0 : Return False
        Dim q = Vector3.Cross(s, edge1)
        Dim v = f * Vector3.Dot(dir, q)
        If v < 0.0F OrElse u + v > 1.0F Then t = 0 : Return False
        t = f * Vector3.Dot(edge2, q)
        Return True
    End Function

    Private Shared Function AlignToNormal(dir As Vector3, normal As Vector3) As Vector3
        Dim n = normal
        If n.LengthSquared < 0.0001F Then n = Vector3.UnitY
        n.Normalize()
        Dim arbitrary = If(Math.Abs(n.X) < 0.9F, Vector3.UnitX, Vector3.UnitY)
        Dim right = Vector3.Cross(n, arbitrary)
        right.Normalize()
        Dim fwd = Vector3.Cross(right, n)
        Return right * dir.X + n * dir.Y + fwd * dir.Z
    End Function

    Private Shared Function BuildHemisphereDirs(count As Integer) As Vector3()
        Dim dirs(count - 1) As Vector3
        Dim goldenAngle = Math.PI * (3.0 - Math.Sqrt(5.0))
        For i = 0 To count - 1
            Dim y = 1.0 - (i + 0.5) / count
            Dim r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y))
            Dim theta = goldenAngle * i
            dirs(i) = New Vector3(CSng(r * Math.Cos(theta)), CSng(y), CSng(r * Math.Sin(theta)))
        Next
        Return dirs
    End Function

    Private Shared Function SafeInv(dir As Vector3) As Vector3
        Return New Vector3(
            If(Math.Abs(dir.X) > 1e-7F, 1.0F / dir.X, Single.MaxValue),
            If(Math.Abs(dir.Y) > 1e-7F, 1.0F / dir.Y, Single.MaxValue),
            If(Math.Abs(dir.Z) > 1e-7F, 1.0F / dir.Z, Single.MaxValue))
    End Function

    Private Shared Function ToV3(v As Vector3d) As Vector3
        Return New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z))
    End Function

    ' ─── Main entry point ────────────────────────────────────────────────────

    ''' <summary>
    ''' Computes the set of vertex indices on targetMesh that are occluded.
    ''' Per-ray logic:
    '''   1. Test against external occluder BVH (minDist=0).
    '''   2. If miss AND IncludeSelfOcclusion: test against target's own BVH (minDist=SelfMinDistance),
    '''      so torso rays blocked by the body's own legs are correctly counted.
    ''' Runs in parallel; call from a background Task.
    ''' </summary>
    Public Function ComputeOccludedVertices(
            targetMesh As PreviewModel.RenderableMesh,
            settings As RaycastSettings,
            progress As IProgress(Of Integer),
            ct As CancellationToken) As HashSet(Of Integer)

        Dim result As New HashSet(Of Integer)
        If _root Is Nothing Then Return result

        Dim verts = If(targetMesh?.MeshData?.Meshgeometry.Vertices IsNot Nothing, SkinningHelper.GetWorldVertices(targetMesh.MeshData.Meshgeometry), Nothing)
        Dim norms = If(targetMesh?.MeshData?.Meshgeometry.Normals IsNot Nothing, SkinningHelper.GetWorldNormals(targetMesh.MeshData.Meshgeometry), Nothing)
        If verts Is Nothing OrElse norms Is Nothing OrElse verts.Length = 0 Then Return result

        Dim vertCount = verts.Length
        Dim maxDist = If(settings.MaxDistance > 0, settings.MaxDistance, _sceneDiag * 2.0F)
        If maxDist < 1.0F Then maxDist = 10000.0F

        ' Build self-BVH from target mesh if requested
        Dim selfRoot As BvhNode = Nothing
        Dim selfTris As TriData() = Nothing
        Dim selfMinDist As Single = 0.0F

        If settings.IncludeSelfOcclusion Then
            Dim selfList As New List(Of TriData)
            AppendMeshTris(selfList, targetMesh)
            If selfList.Count > 0 Then
                selfTris = selfList.ToArray()
                selfRoot = BuildBvhFromTris(selfTris)

                ' Auto self-min distance: 5% of target bounding diagonal, at least 1 NIF unit
                If settings.SelfMinDistance > 0 Then
                    selfMinDist = settings.SelfMinDistance
                Else
                    SkinningHelper.ComputeWorldBounds(targetMesh.MeshData.Meshgeometry)
                    Dim selfMin = ToV3(targetMesh.MeshData.Meshgeometry.Minv)
                    Dim selfMax = ToV3(targetMesh.MeshData.Meshgeometry.Maxv)
                    selfMinDist = Math.Max(1.0F, (selfMax - selfMin).Length * 0.05F)
                End If
            End If
        End If

        Dim hemisphereDirs = BuildHemisphereDirs(settings.RayCount)
        Dim thresholdHits = CInt(Math.Ceiling(settings.OcclusionThreshold * settings.RayCount))

        Dim axialDirs As Vector3() = {
            Vector3.UnitX, -Vector3.UnitX,
            Vector3.UnitY, -Vector3.UnitY,
            Vector3.UnitZ, -Vector3.UnitZ
        }

        Dim occluded(vertCount - 1) As Boolean
        Dim processed As Integer = 0

        Dim opts = New ParallelOptions With {.CancellationToken = ct}
        Try
            Parallel.For(0, vertCount, opts,
                Sub(vi)
                    Dim pos = ToV3(verts(vi))
                    Dim norm = ToV3(norms(vi))
                    If norm.LengthSquared < 0.0001F Then norm = Vector3.UnitY Else norm.Normalize()
                    Dim biased = pos + norm * settings.NormalBias

                    ' Layer 1: inside-mesh parity test on external occluders only (fast path)
                    Dim insideVotes = 0
                    For Each ax In axialDirs
                        If CountBvhIntersections(_root, _tris, biased, ax, maxDist) Mod 2 = 1 Then
                            insideVotes += 1
                        End If
                    Next

                    If insideVotes = 6 Then
                        ' Unanimously inside external occluder → certainly hidden
                        occluded(vi) = True
                    Else
                        ' Layer 2: hemisphere test — each ray blocked by external OR (if enabled) self
                        Dim hits = 0
                        For Each hd In hemisphereDirs
                            Dim worldDir = AlignToNormal(hd, norm)

                            Dim blocked = RayHitsBvh(_root, _tris, biased, worldDir, 0.0F, maxDist)

                            ' Self-occlusion: rays that escape the external occluders (e.g. past
                            ' the dress hem) are still blocked by the body's own geometry (legs/groin)
                            If Not blocked AndAlso selfRoot IsNot Nothing Then
                                blocked = RayHitsBvh(selfRoot, selfTris, biased, worldDir, selfMinDist, maxDist)
                            End If

                            If blocked Then hits += 1
                        Next
                        occluded(vi) = (hits >= thresholdHits)
                    End If

                    Dim p = Interlocked.Increment(processed)
                    If p Mod 100 = 0 Then progress?.Report(CInt(p * 100 / vertCount))
                End Sub)
        Catch ex As OperationCanceledException
            Return result
        End Try

        ' Apply MaskMode
        If settings.MaskCompleteTrianglesOnly Then
            Dim triIndices = targetMesh.MeshData.Meshgeometry.Indices
            If triIndices IsNot Nothing Then
                For i = 0 To triIndices.Length - 3 Step 3
                    Dim i0 = CInt(triIndices(i))
                    Dim i1 = CInt(triIndices(i + 1))
                    Dim i2 = CInt(triIndices(i + 2))
                    If occluded(i0) AndAlso occluded(i1) AndAlso occluded(i2) Then
                        result.Add(i0)
                        result.Add(i1)
                        result.Add(i2)
                    End If
                Next
            End If
        Else
            For vi = 0 To vertCount - 1
                If occluded(vi) Then result.Add(vi)
            Next
        End If

        progress?.Report(100)
        Return result
    End Function

End Class
