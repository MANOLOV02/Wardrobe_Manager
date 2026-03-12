Option Strict On
Option Explicit On

Imports System.Linq
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics

Public NotInheritable Class HkxClothPreviewHelper_Class
    Public Shared Function TryBuildBoneOverrides(shape As Shape_class,
                                                 applyPose As Boolean,
                                                 singleboneskinning As Boolean,
                                                 ByRef bindOverrides As Dictionary(Of String, Transform_Class),
                                                 ByRef poseOverrides As Dictionary(Of String, Transform_Class)) As Boolean
        bindOverrides = Nothing
        poseOverrides = Nothing

        If IsNothing(shape) OrElse singleboneskinning Then Return False
        If IsNothing(shape.ParentSliderSet) OrElse IsNothing(shape.ParentSliderSet.NIFContent) Then Return False
        If Not shape.HasPhysics Then Return False
        If IsNothing(shape.RelatedNifShape) OrElse IsNothing(shape.RelatedBones) OrElse shape.RelatedBones.Count = 0 Then Return False
        If Not Skeleton_Class.HasSkeleton Then Return False

        Dim cloth = shape.ParentSliderSet.NIFContent.Blocks.OfType(Of BSClothExtraData)().FirstOrDefault()
        If IsNothing(cloth) Then Return False

        Dim shapeBoneLookup = shape.RelatedBones.
            Where(Function(bone) bone IsNot Nothing AndAlso bone.Name IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(bone.Name.String)).
            GroupBy(Function(bone) NormalizeBoneName(bone.Name.String), StringComparer.OrdinalIgnoreCase).
            ToDictionary(Function(group) group.Key, Function(group) group.First(), StringComparer.OrdinalIgnoreCase)

        If shapeBoneLookup.Count = 0 Then Return False

        Try
            Dim packfile = HkxPackfileParser_Class.Parse(cloth)
            Dim graph = HkxObjectGraphParser_Class.BuildGraph(packfile)
            Dim skeletonObject = graph.GetObjectsByClassName("hkaSkeleton").FirstOrDefault()
            If IsNothing(skeletonObject) Then Return False

            Dim skeleton = graph.ParseSkeleton(skeletonObject)
            If IsNothing(skeleton) OrElse IsNothing(skeleton.Bones) OrElse IsNothing(skeleton.ReferencePose) OrElse IsNothing(skeleton.ParentIndices) Then Return False
            If skeleton.Bones.Count = 0 OrElse skeleton.ReferencePose.Count <> skeleton.Bones.Count Then Return False

            Dim hkxBoneLookup = skeleton.Bones.
                Where(Function(bone) bone IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(bone.Name)).
                GroupBy(Function(bone) NormalizeBoneName(bone.Name), StringComparer.OrdinalIgnoreCase).
                ToDictionary(Function(group) group.Key,
                             Function(group) group.First().Index,
                             StringComparer.OrdinalIgnoreCase)

            Dim bindGlobals = BuildSkeletonGlobals(skeleton, applySkeletonPose:=False)
            bindOverrides = New Dictionary(Of String, Transform_Class)(StringComparer.OrdinalIgnoreCase)
            poseOverrides = New Dictionary(Of String, Transform_Class)(StringComparer.OrdinalIgnoreCase)

            For Each kvp In shapeBoneLookup
                Dim shapeBone = kvp.Value
                Dim shapeBoneName = shapeBone.Name.String
                If Skeleton_Class.SkeletonDictionary.ContainsKey(shapeBoneName) Then Continue For
                If HasSkeletonAncestorInNif(shapeBone, shape.ParentSliderSet.NIFContent) Then Continue For

                Dim targetIndex As Integer = -1
                If Not hkxBoneLookup.TryGetValue(kvp.Key, targetIndex) Then Continue For
                If targetIndex < 0 OrElse targetIndex >= bindGlobals.Count Then Continue For

                Dim driverIndex = FindNearestSkeletonAncestorIndex(skeleton, targetIndex)
                If driverIndex < 0 OrElse driverIndex >= bindGlobals.Count Then Continue For

                Dim driverName = skeleton.Bones(driverIndex).Name
                Dim driverBone As Skeleton_Class.HierarchiBone_class = Nothing
                If Not Skeleton_Class.SkeletonDictionary.TryGetValue(driverName, driverBone) Then Continue For

                Dim hkxDriverBind = bindGlobals(driverIndex)
                Dim hkxTargetBind = bindGlobals(targetIndex)
                bindOverrides(shapeBoneName) = BuildSplicedBindTransform(driverBone.OriginalGetGlobalTransform, hkxDriverBind, hkxTargetBind)
                If applyPose Then
                    poseOverrides(shapeBoneName) = BuildSplicedPoseTransform(driverBone.GetGlobalTransform, hkxDriverBind, hkxTargetBind)
                End If
            Next

            If bindOverrides.Count = 0 Then bindOverrides = Nothing
            If poseOverrides.Count = 0 Then poseOverrides = Nothing
            Return Not IsNothing(bindOverrides) OrElse Not IsNothing(poseOverrides)
        Catch
            bindOverrides = Nothing
            poseOverrides = Nothing
            Return False
        End Try
    End Function
    Private Shared Function BuildSkeletonGlobals(skeleton As HkaSkeletonGraph_Class, applySkeletonPose As Boolean) As List(Of Transform_Class)
        Dim cache As New List(Of Transform_Class)
        For i = 0 To skeleton.Bones.Count - 1
            cache.Add(Nothing)
        Next

        Dim visiting(skeleton.Bones.Count - 1) As Boolean
        For i = 0 To skeleton.Bones.Count - 1
            cache(i) = ResolveGlobalTransform(skeleton, i, applySkeletonPose, cache, visiting)
        Next

        Return cache
    End Function

    Private Shared Function ResolveGlobalTransform(skeleton As HkaSkeletonGraph_Class,
                                                   index As Integer,
                                                   applySkeletonPose As Boolean,
                                                   cache As List(Of Transform_Class),
                                                   visiting() As Boolean) As Transform_Class
        If index < 0 OrElse index >= skeleton.Bones.Count Then Return New Transform_Class()

        Dim cached = cache(index)
        If Not IsNothing(cached) Then Return CloneTransform(cached)

        If visiting(index) Then
            Return LocalReferencePoseToTransform(skeleton.ReferencePose(index))
        End If

        visiting(index) = True

        Dim boneName = skeleton.Bones(index).Name
        If applySkeletonPose AndAlso Not String.IsNullOrWhiteSpace(boneName) Then
            Dim value As Skeleton_Class.HierarchiBone_class = Nothing
            If Skeleton_Class.SkeletonDictionary.TryGetValue(boneName, value) Then
                Dim posed = value.GetGlobalTransform()
                cache(index) = CloneTransform(posed)
                visiting(index) = False
                Return CloneTransform(posed)
            End If
        End If

        Dim localTransform = LocalReferencePoseToTransform(skeleton.ReferencePose(index))
        Dim parentIndex As Integer = -1
        If Not IsNothing(skeleton.ParentIndices) AndAlso index < skeleton.ParentIndices.Count Then
            parentIndex = skeleton.ParentIndices(index)
        End If

        Dim result = CloneTransform(localTransform)
        If parentIndex >= 0 Then
            Dim parentGlobal = ResolveGlobalTransform(skeleton, parentIndex, applySkeletonPose, cache, visiting)
            result = parentGlobal.ComposeTransforms(localTransform)
        End If

        cache(index) = CloneTransform(result)
        visiting(index) = False
        Return CloneTransform(result)
    End Function

    Private Shared Function LocalReferencePoseToTransform(source As HkxQsTransformGraph_Class) As Transform_Class
        If IsNothing(source) Then Return New Transform_Class()

        Return New Transform_Class With {
            .Translation = New Numerics.Vector3(source.Translation.X, source.Translation.Y, source.Translation.Z),
            .Rotation = QuaternionToMatrix33(source.Rotation),
            .Scale = ResolveUniformScale(source.Scale)
        }
    End Function

    Private Shared Function ResolveUniformScale(scale As HkxVector4Graph_Class) As Single
        If IsNothing(scale) Then Return 1.0F

        Dim values = {scale.X, scale.Y, scale.Z}.
            Where(Function(value) Single.IsFinite(value) AndAlso Math.Abs(value) > 0.000001F).
            ToArray()

        If values.Length = 0 Then Return 1.0F
        Return CSng(values.Average())
    End Function

    Private Shared Function ConvertHkxTranslation(source As HkxVector4Graph_Class) As Numerics.Vector3
        If IsNothing(source) Then Return New Numerics.Vector3(0, 0, 0)
        Return New Numerics.Vector3(-source.Y, source.X, source.Z)
    End Function

    Private Shared Function ConvertHkxRotation(source As HkxQuaternionGraph_Class) As Matrix33
        Dim basis = GetHkxToNifBasis()
        Dim raw = QuaternionToMatrix33(source)
        Dim transposed = TransposeMatrix33(raw)
        Return MultiplyMatrix33(MultiplyMatrix33(basis, transposed), TransposeMatrix33(basis))
    End Function

    Private Shared Function GetHkxToNifBasis() As Matrix33
        Return New Matrix33 With {
            .M11 = 0.0F, .M12 = -1.0F, .M13 = 0.0F,
            .M21 = 1.0F, .M22 = 0.0F, .M23 = 0.0F,
            .M31 = 0.0F, .M32 = 0.0F, .M33 = 1.0F
        }
    End Function

    Private Shared Function MultiplyMatrix33(a As Matrix33, b As Matrix33) As Matrix33
        Dim r As New Matrix33
        r.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31
        r.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32
        r.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33
        r.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31
        r.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32
        r.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33
        r.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31
        r.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32
        r.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        Return r
    End Function

    Private Shared Function TransposeMatrix33(m As Matrix33) As Matrix33
        Return New Matrix33 With {
            .M11 = m.M11, .M12 = m.M21, .M13 = m.M31,
            .M21 = m.M12, .M22 = m.M22, .M23 = m.M32,
            .M31 = m.M13, .M32 = m.M23, .M33 = m.M33
        }
    End Function

    Private Shared Function QuaternionToMatrix33(source As HkxQuaternionGraph_Class) As Matrix33
        If IsNothing(source) Then Return New Matrix33 With {.M11 = 1.0F, .M22 = 1.0F, .M33 = 1.0F}

        Dim x = source.X
        Dim y = source.Y
        Dim z = source.Z
        Dim w = source.W
        Dim length = CSng(Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w)))

        If length <= 0.000001F Then
            Return New Matrix33 With {.M11 = 1.0F, .M22 = 1.0F, .M33 = 1.0F}
        End If

        x /= length
        y /= length
        z /= length
        w /= length

        Dim xx = x * x
        Dim yy = y * y
        Dim zz = z * z
        Dim xy = x * y
        Dim xz = x * z
        Dim yz = y * z
        Dim wx = w * x
        Dim wy = w * y
        Dim wz = w * z

        Return New Matrix33 With {
            .M11 = 1.0F - (2.0F * (yy + zz)),
            .M12 = 2.0F * (xy - wz),
            .M13 = 2.0F * (xz + wy),
            .M21 = 2.0F * (xy + wz),
            .M22 = 1.0F - (2.0F * (xx + zz)),
            .M23 = 2.0F * (yz - wx),
            .M31 = 2.0F * (xz - wy),
            .M32 = 2.0F * (yz + wx),
            .M33 = 1.0F - (2.0F * (xx + yy))
        }
    End Function

    Private Shared Function HasSkeletonAncestorInNif(node As NiNode,
                                                     currentNif As Nifcontent_Class_Manolo) As Boolean
        If node Is Nothing OrElse currentNif Is Nothing Then Return False

        Dim current = TryCast(currentNif.GetParentNode(node), NiNode)
        While current IsNot Nothing
            Dim currentName = If(current.Name Is Nothing, String.Empty, current.Name.String)
            If Not String.IsNullOrWhiteSpace(currentName) AndAlso Skeleton_Class.SkeletonDictionary.ContainsKey(currentName) Then
                Return True
            End If
            current = TryCast(currentNif.GetParentNode(current), NiNode)
        End While

        Return False
    End Function
    Private Shared Function FindNearestSkeletonAncestorIndex(skeleton As HkaSkeletonGraph_Class, startIndex As Integer) As Integer
        If IsNothing(skeleton) OrElse IsNothing(skeleton.Bones) OrElse IsNothing(skeleton.ParentIndices) Then Return -1

        Dim visited As New HashSet(Of Integer)
        Dim current = startIndex
        While current >= 0 AndAlso current < skeleton.Bones.Count AndAlso Not visited.Contains(current)
            visited.Add(current)

            Dim boneName = skeleton.Bones(current).Name
            If Not String.IsNullOrWhiteSpace(boneName) AndAlso Skeleton_Class.SkeletonDictionary.ContainsKey(boneName) Then
                Return current
            End If

            If current >= skeleton.ParentIndices.Count Then Exit While
            current = skeleton.ParentIndices(current)
        End While

        Return -1
    End Function
    Private Shared Function BuildSplicedBindTransform(driverBind As Transform_Class,
                                                      hkxDriverBind As Transform_Class,
                                                      hkxTargetBind As Transform_Class) As Transform_Class
        Dim hkxRelativeBind = hkxDriverBind.Inverse.ComposeTransforms(hkxTargetBind)
        Return driverBind.ComposeTransforms(hkxRelativeBind)
    End Function

    Private Shared Function BuildSplicedPoseTransform(driverPose As Transform_Class,
                                                      hkxDriverBind As Transform_Class,
                                                      hkxTargetBind As Transform_Class) As Transform_Class
        Dim hkxRelativeBind = hkxDriverBind.Inverse.ComposeTransforms(hkxTargetBind)
        Return driverPose.ComposeTransforms(hkxRelativeBind)
    End Function
    Private Shared Function CloneTransform(source As Transform_Class) As Transform_Class
        If IsNothing(source) Then Return New Transform_Class()

        Return New Transform_Class With {
            .Rotation = source.Rotation,
            .Translation = source.Translation,
            .Scale = source.Scale
        }
    End Function

    Private Shared Function NormalizeBoneName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return String.Empty
        Return name.Trim().ToUpperInvariant()
    End Function
End Class
