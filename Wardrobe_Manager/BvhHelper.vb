Imports OpenTK.Mathematics

''' <summary>
''' Shared BVH infrastructure used by OcclusionRaytracer and ConformHelper.
''' Defines the AABB structure, BvhNode class, and the generic BuildBvh function.
''' Members are accessible without module prefix throughout the assembly.
''' </summary>
Friend Module BvhHelper

    Friend Structure AABB
        Public Min As Vector3
        Public Max As Vector3

        Public Shared Function FromTriangle(v0 As Vector3, v1 As Vector3, v2 As Vector3) As AABB
            Return New AABB With {
                .Min = Vector3.ComponentMin(v0, Vector3.ComponentMin(v1, v2)),
                .Max = Vector3.ComponentMax(v0, Vector3.ComponentMax(v1, v2))
            }
        End Function

        Public Shared Function Merge(a As AABB, b As AABB) As AABB
            Return New AABB With {
                .Min = Vector3.ComponentMin(a.Min, b.Min),
                .Max = Vector3.ComponentMax(a.Max, b.Max)
            }
        End Function

        Public ReadOnly Property Center As Vector3
            Get
                Return (Min + Max) * 0.5F
            End Get
        End Property

        ''' <summary>Slab-method AABB vs ray test. dirInv = 1/direction (use SafeInv).</summary>
        Public Function Intersects(orig As Vector3, dirInv As Vector3) As Boolean
            Dim tx1 = (Min.X - orig.X) * dirInv.X
            Dim tx2 = (Max.X - orig.X) * dirInv.X
            Dim tmin = Math.Min(tx1, tx2)
            Dim tmax = Math.Max(tx1, tx2)

            Dim ty1 = (Min.Y - orig.Y) * dirInv.Y
            Dim ty2 = (Max.Y - orig.Y) * dirInv.Y
            tmin = Math.Max(tmin, Math.Min(ty1, ty2))
            tmax = Math.Min(tmax, Math.Max(ty1, ty2))

            Dim tz1 = (Min.Z - orig.Z) * dirInv.Z
            Dim tz2 = (Max.Z - orig.Z) * dirInv.Z
            tmin = Math.Max(tmin, Math.Min(tz1, tz2))
            tmax = Math.Min(tmax, Math.Max(tz1, tz2))

            Return tmax >= tmin AndAlso tmax > 0.0F
        End Function

        ''' <summary>Min squared distance from point p to this AABB. Returns 0 if p is inside.</summary>
        Public Function MinSqDist(p As Vector3) As Single
            Dim dx = Math.Max(0F, Math.Max(Min.X - p.X, p.X - Max.X))
            Dim dy = Math.Max(0F, Math.Max(Min.Y - p.Y, p.Y - Max.Y))
            Dim dz = Math.Max(0F, Math.Max(Min.Z - p.Z, p.Z - Max.Z))
            Return dx * dx + dy * dy + dz * dz
        End Function
    End Structure

    Friend Class BvhNode
        Public Bounds As AABB
        Public Left As BvhNode = Nothing
        Public Right As BvhNode = Nothing
        Public LeafIndices As Integer() = Nothing

        Public ReadOnly Property IsLeaf As Boolean
            Get
                Return LeafIndices IsNot Nothing
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Builds a median-split BVH from pre-computed per-element bounds and centroids.
    ''' <paramref name="bounds"/> and <paramref name="centroids"/> must have the same length.
    ''' <paramref name="indices"/> lists which elements to partition (sub-range at each level).
    ''' Leaf LeafIndices store original element indices (same indexing as bounds/centroids).
    ''' </summary>
    Friend Function BuildBvh(bounds As AABB(), centroids As Vector3(),
                              indices As List(Of Integer), depth As Integer) As BvhNode
        Dim node As New BvhNode
        Dim b = bounds(indices(0))
        For i = 1 To indices.Count - 1
            b = AABB.Merge(b, bounds(indices(i)))
        Next
        node.Bounds = b

        If indices.Count <= 8 OrElse depth >= 24 Then
            node.LeafIndices = indices.ToArray()
            Return node
        End If

        Dim ext = b.Max - b.Min
        Dim axis = 0
        If ext.Y > ext.X Then axis = 1
        If ext.Z > If(axis = 0, ext.X, ext.Y) Then axis = 2

        Dim sorted = indices.OrderBy(Function(i2)
                                         Dim c = centroids(i2)
                                         Return If(axis = 0, CDbl(c.X), If(axis = 1, CDbl(c.Y), CDbl(c.Z)))
                                     End Function).ToList()

        Dim mid = sorted.Count \ 2
        node.Left = BuildBvh(bounds, centroids, sorted.Take(mid).ToList(), depth + 1)
        node.Right = BuildBvh(bounds, centroids, sorted.Skip(mid).ToList(), depth + 1)
        Return node
    End Function

End Module
