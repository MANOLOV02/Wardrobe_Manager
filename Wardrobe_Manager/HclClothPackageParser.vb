Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq

Public NotInheritable Class HclClothPackageParser_Class
    Public Shared Function Parse(packfile As HkxPackfile_Class) As HclClothPackageGraph_Class
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        Return Parse(HkxObjectGraphParser_Class.BuildGraph(packfile))
    End Function

    Public Shared Function Parse(graph As HkxObjectGraph_Class) As HclClothPackageGraph_Class
        If IsNothing(graph) Then Throw New ArgumentNullException(NameOf(graph))

        Dim result As New HclClothPackageGraph_Class With {
            .Graph = graph,
            .RootContainer = graph.ParseRootLevelContainer()
        }

        Dim skeletonObject = graph.GetObjectsByClassName("hkaSkeleton").FirstOrDefault()
        If Not IsNothing(skeletonObject) Then
            result.Skeleton = graph.ParseSkeleton(skeletonObject)
        End If

        Dim collidables = graph.GetObjectsByClassName("hclCollidable").
            Select(Function(obj) HclStructuredGraphParser_Class.ParseCollidable(graph, obj)).
            Where(Function(detail) Not IsNothing(detail)).
            ToList()
        result.Collidables.AddRange(collidables)

        Dim capsuleShapes = graph.GetObjectsByClassName("hclCapsuleShape").
            Select(Function(obj) HclStructuredGraphParser_Class.ParseCapsuleShape(graph, obj)).
            Where(Function(detail) Not IsNothing(detail)).
            ToList()
        result.CapsuleShapes.AddRange(capsuleShapes)

        For Each clothObject In graph.GetObjectsByClassName("hclClothData")
            Dim clothData = graph.ParseClothData(clothObject)
            If IsNothing(clothData) Then Continue For

            Dim clothConfig As New HclClothConfigGraph_Class With {
                .ClothData = clothData
            }

            clothConfig.SimClothDatas.AddRange(
                clothData.SimClothDatas.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseSimClothData(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.BufferDefinitions.AddRange(
                clothData.BufferDefinitions.
                    Select(Function(obj)
                               If obj.ClassName.Equals("hclScratchBufferDefinition", StringComparison.OrdinalIgnoreCase) Then
                                   Return Nothing
                               End If
                               Return HclStructuredGraphParser_Class.ParseBufferDefinition(graph, obj)
                           End Function).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.ScratchBufferDefinitions.AddRange(
                clothData.BufferDefinitions.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseScratchBufferDefinition(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.TransformSets.AddRange(
                clothData.TransformSetDefinitions.
                    Select(Function(obj) HclRenderGraphParser_Class.ParseTransformSetDefinition(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            For Each op In clothData.Operators
                Select Case NormalizeOperatorClassName(op.ClassName)
                    Case "hclobjectspaceskinpnoperator"
                        clothConfig.ObjectSpaceSkin = HclRenderGraphParser_Class.ParseObjectSpaceSkinPNOperator(graph, op)
                        PopulateSkinBoneNames(clothConfig.ObjectSpaceSkin, result.Skeleton)
                    Case "hclmoveparticlesoperator"
                        clothConfig.MoveParticles = HclStructuredGraphParser_Class.ParseMoveParticlesOperator(graph, op)
                    Case "hclsimulateoperator"
                        clothConfig.Simulate = HclStructuredGraphParser_Class.ParseSimulateOperator(graph, op)
                    Case "hclsimplemeshbonedeformoperator"
                        clothConfig.SimpleMeshBoneDeform = HclRenderGraphParser_Class.ParseSimpleMeshBoneDeformOperator(graph, op, result.Skeleton)
                    Case "hclcopyverticesoperator"
                        clothConfig.CopyVertices = HclStructuredGraphParser_Class.ParseCopyVerticesOperator(graph, op)
                    Case "hclgatherallverticesoperator"
                        clothConfig.GatherAllVertices = HclStructuredGraphParser_Class.ParseGatherAllVerticesOperator(graph, op)
                    Case Else
                        clothConfig.UnknownOperators.Add(op)
                End Select
            Next

            clothConfig.ClothStates.AddRange(
                clothData.ClothStates.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseClothState(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))
            PopulateStateOperatorLinks(clothConfig)
            PopulateStateAccessLinks(clothConfig, result.Skeleton)
            PopulateResolvedMoveParticles(clothConfig)
            PopulateResolvedCollidableBindings(clothConfig, result.Skeleton)
            PopulateResolvedTriangles(clothConfig)
            PopulateSkinCoverage(clothConfig)
            PopulateSkinDefaultPoseMatches(clothConfig)

            result.ClothConfigs.Add(clothConfig)
        Next

        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclStandardLinkConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseStandardLinkConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclStretchLinkConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseStretchLinkConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclBendStiffnessConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseBendStiffnessConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclLocalRangeConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseLocalRangeConstraintSet(graph, obj), Object)))

        Return result
    End Function

    Private Shared Function NormalizeOperatorClassName(className As String) As String
        If String.IsNullOrWhiteSpace(className) Then Return String.Empty
        Return className.Replace(ChrW(0), String.Empty).Trim().ToLowerInvariant()
    End Function

    Private Shared Sub PopulateSkinBoneNames(skin As HclObjectSpaceSkinPNOperatorGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(skin) OrElse IsNothing(skeleton?.Bones) Then Return

        For Each boneIndex In skin.BoneIndices
            If boneIndex >= 0 AndAlso boneIndex < skeleton.Bones.Count Then
                skin.ResolvedBoneNames.Add(skeleton.Bones(CInt(boneIndex)).Name)
            Else
                skin.ResolvedBoneNames.Add("#" & boneIndex.ToString())
            End If
        Next

        For Each subset In skin.TransformSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next

        For Each subset In skin.ThreeBlendSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next

        For Each subset In skin.TwoBlendSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next
    End Sub

    Private Shared Sub PopulateSkinSubsetBoneNames(skin As HclObjectSpaceSkinPNOperatorGraph_Class,
                                                   subset As HclObjectSpaceSkinTransformSubsetGraph_Class,
                                                   skeleton As HkaSkeletonGraph_Class)
        If IsNothing(skin) OrElse IsNothing(subset) OrElse IsNothing(skeleton?.Bones) Then Return

        For Each lane In subset.VertexInfluences
            If lane Is Nothing Then Continue For
            lane.ResolvedSkeletonIndices.Clear()
            lane.ResolvedBoneNames.Clear()
            For Each transformIndex In lane.TransformIndices
                If transformIndex < 0 OrElse transformIndex >= skin.BoneIndices.Count Then Continue For
                Dim skeletonIndex = CInt(skin.BoneIndices(CInt(transformIndex)))
                lane.ResolvedSkeletonIndices.Add(skeletonIndex)
                If skeletonIndex >= 0 AndAlso skeletonIndex < skeleton.Bones.Count Then
                    lane.ResolvedBoneNames.Add(skeleton.Bones(skeletonIndex).Name)
                Else
                    lane.ResolvedBoneNames.Add("#" & skeletonIndex.ToString())
                End If
            Next
        Next
    End Sub

    Private Shared Sub PopulateResolvedMoveParticles(config As HclClothConfigGraph_Class)
        If IsNothing(config?.MoveParticles) Then Return

        For Each sim In config.SimClothDatas
            sim.Field48MatchesMoveParticles = sim.Field48UInt16.SequenceEqual(config.MoveParticles.Pairs.Select(Function(pair) pair.ParticleIndex))
            If sim.Field48MatchesMoveParticles Then
                sim.ResolvedMoveParticlePairs.AddRange(config.MoveParticles.Pairs)
            End If
        Next
    End Sub

    Private Shared Sub PopulateResolvedCollidableBindings(config As HclClothConfigGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(config) Then Return

        For Each sim In config.SimClothDatas
            sim.CollidableBindings.Clear()

            For i = 0 To sim.CollidableDetails.Count - 1
                Dim binding As New HclSimCollidableBinding_Class With {
                    .EntryIndex = i,
                    .Collidable = sim.CollidableDetails(i),
                    .Matrix = If(i < sim.Field98Matrices.Count, sim.Field98Matrices(i), Nothing)
                }

                If i < sim.Field88UInt32.Count Then
                    binding.BoneIndex = CInt(sim.Field88UInt32(i))
                    If Not IsNothing(skeleton?.Bones) AndAlso binding.BoneIndex >= 0 AndAlso binding.BoneIndex < skeleton.Bones.Count Then
                        binding.BoneName = skeleton.Bones(binding.BoneIndex).Name
                    End If
                End If

                If i < sim.Field118Pairs.Count Then
                    binding.TransformSetIndex = sim.Field118Pairs(i).FirstValue
                    binding.ParameterRaw = sim.Field118Pairs(i).SecondValue
                    binding.ParameterSingle = BitConverter.ToSingle(BitConverter.GetBytes(binding.ParameterRaw), 0)
                End If

                sim.CollidableBindings.Add(binding)
            Next
        Next
    End Sub

    Private Shared Sub PopulateResolvedTriangles(config As HclClothConfigGraph_Class)
        If IsNothing(config?.SimpleMeshBoneDeform) Then Return

        Dim sim = config.SimClothDatas.FirstOrDefault()
        If IsNothing(sim) Then Return

        For Each mapping In config.SimpleMeshBoneDeform.BoneMappings
            If mapping.TriangleIndex < 0 OrElse mapping.TriangleIndex >= sim.Triangles.Count Then Continue For
            mapping.ResolvedTriangle = sim.Triangles(mapping.TriangleIndex)
        Next
    End Sub

    Private Shared Sub PopulateSkinCoverage(config As HclClothConfigGraph_Class)
        Dim skin = config?.ObjectSpaceSkin
        If IsNothing(skin) Then Return

        skin.CoveredVertexIndices.Clear()
        skin.CoveredVertexIndices.AddRange(
            skin.SkinBlocks.
                Where(Function(block) block.InfluenceBlock IsNot Nothing).
                SelectMany(Function(block) block.InfluenceBlock.VertexIndices).
                Where(Function(index) index <> UShort.MaxValue).
                Select(Function(index) CInt(index)).
                Distinct().
                OrderBy(Function(index) index))
        skin.CoveredVertexCount = skin.CoveredVertexIndices.Count

        Dim sim = config.SimClothDatas.FirstOrDefault()
        If IsNothing(sim) Then
            skin.SimParticleCount = Nothing
            skin.CoversSimParticles = Nothing
            Return
        End If

        skin.SimParticleCount = sim.Field38Vectors.Count
        skin.CoversSimParticles = (skin.CoveredVertexCount = skin.SimParticleCount.Value)
    End Sub

    Private Shared Sub PopulateSkinDefaultPoseMatches(config As HclClothConfigGraph_Class)
        Dim skin = config?.ObjectSpaceSkin
        Dim sim = config?.SimClothDatas?.FirstOrDefault()
        Dim pose = sim?.DefaultClothPoseDetails?.FirstOrDefault()
        If IsNothing(skin) OrElse IsNothing(sim) OrElse IsNothing(pose) Then Return

        Const epsilon As Double = (1.0R / 256.0R) + 0.00001R

        For Each block In skin.SkinBlocks
            If IsNothing(block) Then Continue For

            Dim matchCount = 0
            Dim totalCount = 0

            For Each entry In block.VertexEntries
                If IsNothing(entry?.Position) Then Continue For
                If entry.VertexIndex < 0 OrElse entry.VertexIndex >= pose.Pose.Count Then Continue For

                Dim expected = pose.Pose(CInt(entry.VertexIndex))
                entry.ExpectedPositionX = expected.X
                entry.ExpectedPositionY = expected.Y
                entry.ExpectedPositionZ = expected.Z
                entry.ExpectedPositionW = expected.W

                Dim dx = entry.Position.X - expected.X
                Dim dy = entry.Position.Y - expected.Y
                Dim dz = entry.Position.Z - expected.Z
                Dim positionErrorValue = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz))
                entry.PositionError = positionErrorValue
                entry.MatchesDefaultPosePosition = (positionErrorValue <= epsilon)

                totalCount += 1
                If entry.MatchesDefaultPosePosition.Value Then matchCount += 1
            Next

            block.MatchedDefaultPosePositions = matchCount
            If totalCount = 0 Then
                block.AllPositionsMatchDefaultPose = Nothing
            Else
                block.AllPositionsMatchDefaultPose = (matchCount = totalCount)
            End If
        Next
    End Sub

    Private Shared Sub PopulateStateAccessLinks(config As HclClothConfigGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(config) Then Return

        Dim bufferNames As New List(Of String)
        bufferNames.AddRange(config.BufferDefinitions.Select(Function(detail) detail.Name))
        bufferNames.AddRange(config.ScratchBufferDefinitions.Select(Function(detail) detail.Name))

        For Each state In config.ClothStates
            For Each access In state.BufferAccesses.Concat(state.AuxiliaryBufferAccesses)
                If access Is Nothing Then Continue For
                If access.BufferIndex >= 0 AndAlso access.BufferIndex < bufferNames.Count Then
                    access.ResolvedBufferName = bufferNames(access.BufferIndex)
                End If
            Next

            If skeleton?.Bones Is Nothing Then Continue For

            For Each transformAccess In state.TransformSetAccesses
                If transformAccess Is Nothing Then Continue For
                For Each component In transformAccess.ComponentAccesses
                    If component Is Nothing Then Continue For
                    component.ResolvedBoneNames.Clear()
                    component.MatchingSkinPaletteIndices.Clear()
                    component.MatchingSkinBoneNames.Clear()
                    For Each boneIndex In component.MaskIndices
                        If boneIndex < 0 OrElse boneIndex >= skeleton.Bones.Count Then Continue For
                        component.ResolvedBoneNames.Add(skeleton.Bones(boneIndex).Name)
                        If config.ObjectSpaceSkin Is Nothing Then Continue For
                        Dim skinPaletteIndex = config.ObjectSpaceSkin.BoneIndices.IndexOf(CUShort(boneIndex))
                        If skinPaletteIndex < 0 Then Continue For
                        component.MatchingSkinPaletteIndices.Add(skinPaletteIndex)
                        If skinPaletteIndex < config.ObjectSpaceSkin.ResolvedBoneNames.Count Then
                            component.MatchingSkinBoneNames.Add(config.ObjectSpaceSkin.ResolvedBoneNames(skinPaletteIndex))
                        Else
                            component.MatchingSkinBoneNames.Add(skeleton.Bones(boneIndex).Name)
                        End If
                    Next
                Next
            Next
        Next
    End Sub

    Private Shared Sub PopulateStateOperatorLinks(config As HclClothConfigGraph_Class)
        If IsNothing(config?.ClothData?.Operators) Then Return

        For Each state In config.ClothStates
            If IsNothing(state) Then Continue For

            For Each opIndex In state.OperatorIndices
                If opIndex < 0 OrElse opIndex >= config.ClothData.Operators.Count Then Continue For
                Dim op = config.ClothData.Operators(opIndex)
                state.ResolvedOperators.Add(op)
                state.ResolvedOperatorNames.Add(If(String.IsNullOrWhiteSpace(op.ClassName), "#" & opIndex.ToString(), op.ClassName))
            Next
        Next
    End Sub
End Class

Public Class HclClothPackageGraph_Class
    Public Property Graph As HkxObjectGraph_Class
    Public Property RootContainer As HkxRootLevelContainerGraph_Class
    Public Property Skeleton As HkaSkeletonGraph_Class
    Public ReadOnly Property ClothConfigs As New List(Of HclClothConfigGraph_Class)
    Public ReadOnly Property Collidables As New List(Of HclCollidableDetail_Class)
    Public ReadOnly Property CapsuleShapes As New List(Of HclCapsuleShapeDetail_Class)
    Public ReadOnly Property ConstraintSets As New List(Of Object)
End Class

Public Class HclClothConfigGraph_Class
    Public Property ClothData As HclClothDataGraph_Class
    Public ReadOnly Property SimClothDatas As New List(Of HclSimClothDataDetail_Class)
    Public ReadOnly Property BufferDefinitions As New List(Of HclBufferDefinitionDetail_Class)
    Public ReadOnly Property ScratchBufferDefinitions As New List(Of HclScratchBufferDefinitionDetail_Class)
    Public ReadOnly Property TransformSets As New List(Of HclTransformSetDefinitionGraph_Class)
    Public Property ObjectSpaceSkin As HclObjectSpaceSkinPNOperatorGraph_Class
    Public Property MoveParticles As HclMoveParticlesOperatorDetail_Class
    Public Property Simulate As HclSimulateOperatorDetail_Class
    Public Property SimpleMeshBoneDeform As HclSimpleMeshBoneDeformOperatorGraph_Class
    Public Property CopyVertices As HclCopyVerticesOperatorDetail_Class
    Public Property GatherAllVertices As HclGatherAllVerticesOperatorDetail_Class
    Public ReadOnly Property UnknownOperators As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ClothStates As New List(Of HclClothStateDetail_Class)
End Class











