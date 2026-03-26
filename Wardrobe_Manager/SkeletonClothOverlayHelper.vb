Imports System.Linq
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics

' =============================================================================
' ESTADO: ACTIVO — ruta principal de bone injection para physics en el render.
' -----------------------------------------------------------------------------
' InjectMissingBonesIntoLiveSkeleton: llamado desde
'   Skeleton_Class.PrepareSkeletonForShapes → NifContent_Class.
' Parsea el hkaSkeleton del BSClothExtraData e inyecta los huesos de física
' que no existen en el esqueleto del juego como HierarchiBone_class temporales.
'
' LocalReferencePoseToTransform: usa OpenTK Matrix4 → Transform_Class(Matrix4).
' Es la implementación CORRECTA y consistente con el resto del render.
'
' PENDIENTES CONOCIDOS:
'  - LocalReferencePoseToTransform y ResolveUniformScale están duplicadas aquí
'    y en HclCollisionPoseHelper.vb. Candidatas a extraer a módulo compartido
'    cuando se decida conectar HclCollisionPoseHelper al render.
'  - Debugger.Break() en el catch de InjectMissingBonesIntoLiveSkeleton:
'    útil para debug, evaluar si dejar o quitar en producción.
'  - NormalizeBoneName usa ToUpperInvariant(). Consistente con el resto de
'    bone lookups (OrdinalIgnoreCase). Revisar si hay casos edge con nombres
'    de huesos que usen caracteres no-ASCII.
' =============================================================================

Public NotInheritable Class SkeletonClothOverlayHelper_Class

    ' Parses the BSClothExtraData from a NIF and returns the HKX skeleton.
    ' Returns Nothing if the NIF has no cloth data or the skeleton cannot be parsed.
    ' Call once per unique NIFContent and cache the result when processing multiple shapes.
    Public Shared Function ParseClothSkeleton(nifContent As Nifcontent_Class_Manolo) As HkaSkeletonGraph_Class
        Dim cloth = nifContent?.Blocks.OfType(Of BSClothExtraData)().FirstOrDefault()
        If cloth Is Nothing Then Return Nothing
        Try
            Dim graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(cloth))
            Dim skeletonObject = graph.GetObjectsByClassName("hkaSkeleton").FirstOrDefault()
            If skeletonObject Is Nothing Then Return Nothing
            Dim skeleton = graph.ParseSkeleton(skeletonObject)
            If skeleton Is Nothing OrElse skeleton.Bones Is Nothing OrElse skeleton.ReferencePose Is Nothing OrElse skeleton.ParentIndices Is Nothing Then Return Nothing
            If skeleton.Bones.Count = 0 OrElse skeleton.ReferencePose.Count <> skeleton.Bones.Count Then Return Nothing
            Return skeleton
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public Shared Sub InjectMissingBonesIntoLiveSkeleton(shape As Shape_class,
                                                         injectedBones As System.Collections.Generic.HashSet(Of String),
                                                         Optional cachedSkeleton As HkaSkeletonGraph_Class = Nothing)
        If IsNothing(shape) OrElse Not Skeleton_Class.HasSkeleton Then Exit Sub
        If IsNothing(shape.RelatedBones) OrElse shape.RelatedBones.Count = 0 Then Exit Sub
        If Not shape.HasPhysics Then Exit Sub
        If IsNothing(shape.ParentSliderSet) OrElse IsNothing(shape.ParentSliderSet.NIFContent) Then Exit Sub

        Dim skeleton As HkaSkeletonGraph_Class
        If cachedSkeleton IsNot Nothing Then
            skeleton = cachedSkeleton
        Else
            skeleton = ParseClothSkeleton(shape.ParentSliderSet.NIFContent)
            If skeleton Is Nothing Then Exit Sub
        End If

        Dim shapeName = If(IsNothing(shape.RelatedNifShape) OrElse IsNothing(shape.RelatedNifShape.Name), "<shape>", shape.RelatedNifShape.Name.String)

        Try
            ' Build name→index lookup once per skeleton (case-insensitive, no extra ToUpperInvariant needed)
            Dim hkxBoneLookup = skeleton.Bones.
                Where(Function(bone) bone IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(bone.Name)).
                GroupBy(Function(bone) bone.Name.Trim(), StringComparer.OrdinalIgnoreCase).
                ToDictionary(Function(group) group.Key,
                             Function(group) group.First().Index,
                             StringComparer.OrdinalIgnoreCase)

            For Each shapeBone In shape.RelatedBones
                If IsNothing(shapeBone) OrElse IsNothing(shapeBone.Name) Then Continue For

                Dim shapeBoneName = shapeBone.Name.String
                If String.IsNullOrWhiteSpace(shapeBoneName) Then Continue For
                shapeBoneName = shapeBoneName.Trim()
                If Skeleton_Class.SkeletonDictionary.ContainsKey(shapeBoneName) Then Continue For

                Dim targetIndex As Integer = -1
                If Not hkxBoneLookup.TryGetValue(shapeBoneName, targetIndex) Then Continue For
                EnsureLiveInjectedBone(targetIndex, skeleton, injectedBones, shapeName, shapeBoneName)
            Next
        Catch ex As Exception
            Debugger.Break()
        End Try
    End Sub

    Private Shared Function EnsureLiveInjectedBone(index As Integer,
                                                   skeleton As HkaSkeletonGraph_Class,
                                                   injectedBones As System.Collections.Generic.HashSet(Of String),
                                                   shapeName As String,
                                                   Optional requestedName As String = Nothing) As Skeleton_Class.HierarchiBone_class
        If IsNothing(skeleton) OrElse IsNothing(skeleton.Bones) OrElse index < 0 OrElse index >= skeleton.Bones.Count Then Return Nothing

        Dim boneName = skeleton.Bones(index).Name
        If String.IsNullOrWhiteSpace(boneName) Then Return Nothing
        Dim dictionaryKey = If(String.IsNullOrWhiteSpace(requestedName), boneName, requestedName.Trim())

        Dim existing As Skeleton_Class.HierarchiBone_class = Nothing
        If Skeleton_Class.SkeletonDictionary.TryGetValue(dictionaryKey, existing) Then Return existing
        If Not dictionaryKey.Equals(boneName, StringComparison.OrdinalIgnoreCase) AndAlso Skeleton_Class.SkeletonDictionary.TryGetValue(boneName, existing) Then Return existing

        Dim parentBone As Skeleton_Class.HierarchiBone_class = Nothing
        Dim parentIndex = If(index < skeleton.ParentIndices.Count, CInt(skeleton.ParentIndices(index)), -1)
        If parentIndex >= 0 Then
            parentBone = EnsureLiveInjectedBone(parentIndex, skeleton, injectedBones, shapeName)
        End If

        Dim nuevo As New Skeleton_Class.HierarchiBone_class With {
            .BoneName = dictionaryKey,
            .Parent = parentBone,
            .DeltaTransform = Nothing,
            .OriginalLocaLTransform = LocalReferencePoseToTransform(skeleton.ReferencePose(index))
        }

        If IsNothing(parentBone) Then
            Skeleton_Class.SkeletonStructure.Add(nuevo)
        Else
            parentBone.Childrens.Add(nuevo)
        End If

        Skeleton_Class.SkeletonDictionary.Add(dictionaryKey, nuevo)
        If Not IsNothing(injectedBones) Then injectedBones.Add(dictionaryKey)
        Return nuevo
    End Function

    Private Shared Function LocalReferencePoseToTransform(source As HkxQsTransformGraph_Class) As Transform_Class
        If IsNothing(source) Then Return New Transform_Class()

        Dim scale = ResolveUniformScale(source.Scale)
        Dim rotation As Quaternion
        If IsNothing(source.Rotation) Then
            rotation = Quaternion.Identity
        Else
            rotation = New Quaternion(source.Rotation.X, source.Rotation.Y, source.Rotation.Z, source.Rotation.W)
            If rotation.LengthSquared <= 0.000001F Then
                rotation = Quaternion.Identity
            Else
                rotation = Quaternion.Normalize(rotation)
            End If
        End If

        Dim transformMatrix =
            Matrix4.CreateScale(scale) *
            Matrix4.CreateFromQuaternion(rotation) *
            Matrix4.CreateTranslation(source.Translation.X, source.Translation.Y, source.Translation.Z)

        Return New Transform_Class(transformMatrix)
    End Function

    Private Shared Function ResolveUniformScale(scale As HkxVector4Graph_Class) As Single
        If IsNothing(scale) Then Return 1.0F

        Dim values = {scale.X, scale.Y, scale.Z}.
            Where(Function(value) Single.IsFinite(value) AndAlso Math.Abs(value) > 0.000001F).
            ToArray()

        If values.Length = 0 Then Return 1.0F
        Return CSng(values.Average())
    End Function

    Private Shared Function NormalizeBoneName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return String.Empty
        Return name.Trim().ToUpperInvariant()
    End Function
End Class