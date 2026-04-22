' Version Uploaded of Wardrobe 3.2.0
Imports System.Linq
Imports System.Numerics
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs

Public NotInheritable Class PhysicsWeightCollapseHelper
    Private Const WeightEpsilon As Single = 0.0001F

    Private Sub New()
    End Sub

    Public Shared Function TryCollapseInjectedWeightsBeforeRemovingPhysics(slider As SliderSet_Class, ByRef report As String) As Boolean
        Return TryCollapseInjectedWeightsInternal(slider, report, False)
    End Function

    Public Shared Function TryCollapseInjectedWeightsAndExpandPaletteBeforeRemovingPhysics(slider As SliderSet_Class, ByRef report As String) As Boolean
        Return TryCollapseInjectedWeightsInternal(slider, report, True)
    End Function

    Private Shared Function TryCollapseInjectedWeightsInternal(slider As SliderSet_Class, ByRef report As String, allowPaletteExpansion As Boolean) As Boolean
        report = ""
        If slider Is Nothing OrElse slider.NIFContent Is Nothing Then
            report = "The current slider set has no loaded NIF content."
            Return False
        End If

        Dim clothBlocks = slider.NIFContent.Blocks.OfType(Of BSClothExtraData)().ToList()
        If clothBlocks.Count = 0 Then
            report = "The current project has no BSClothExtraData blocks."
            Return True
        End If

        If SkeletonInstance.Default.HasSkeleton = False AndAlso SkeletonInstance.Default.LoadFromConfig(False, False) = False Then
            report = "The base skeleton could not be loaded."
            Return False
        End If

        SkeletonInstance.Default.PrepareForShapes(slider.Shapes)

        Dim injectedReplacementMap = BuildInjectedBoneReplacementMap(slider)
        Dim plans As New List(Of ShapeRewritePlan)
        Dim touchedShapes = 0
        Dim touchedVertices = 0

        For Each shape In slider.Shapes
            Dim plan = PlanShapeRewrite(shape, injectedReplacementMap, allowPaletteExpansion, report)
            If plan Is Nothing Then
                If String.IsNullOrWhiteSpace(report) = False Then Return False
                Continue For
            End If
            plans.Add(plan)
            touchedShapes += 1
            touchedVertices += plan.VertexRewrites.Count
        Next

        If plans.Count = 0 Then
            report = "No injected cloth-bone weights were found in the loaded shapes."
            Return True
        End If

        For Each plan In plans
            If ApplyShapeRewritePlan(plan, report) = False Then Return False
        Next

        report = $"Collapsed injected cloth weights on {touchedShapes} shape(s) and rewrote {touchedVertices} vertex/vertices."
        Return True
    End Function

    Private Shared Function BuildInjectedBoneReplacementMap(slider As SliderSet_Class) As Dictionary(Of String, Dictionary(Of String, Single))
        Dim result As New Dictionary(Of String, Dictionary(Of String, Single))(StringComparer.OrdinalIgnoreCase)
        Dim injectedUsedByShapes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each shape In slider.Shapes
            For Each boneName In GetShapeBoneNames(shape)
                Dim normalized = NormalizeBoneName(boneName)
                If String.IsNullOrWhiteSpace(normalized) Then Continue For
                If SkeletonInstance.Default.IsInjectedBone(normalized) Then injectedUsedByShapes.Add(normalized)
            Next
        Next

        For Each cloth In slider.NIFContent.Blocks.OfType(Of BSClothExtraData)()
            Try
                Dim package = HclClothPackageParser_Class.Parse(HkxPackfileParser_Class.Parse(cloth))
                For Each config In package.ClothConfigs
                    Dim vertexDist = BuildObjectSpaceSkinVertexDistributions(config?.ObjectSpaceSkin)
                    If config?.SimpleMeshBoneDeform Is Nothing Then Continue For

                    For Each mapping In config.SimpleMeshBoneDeform.BoneMappings
                        Dim boneName = NormalizeBoneName(mapping?.BoneName)
                        If String.IsNullOrWhiteSpace(boneName) Then Continue For

                        Dim dist = CollapseToNonInjectedBones(BuildReplacementFromTriangleMapping(mapping, vertexDist))
                        If dist.Count = 0 Then Continue For

                        Dim current As Dictionary(Of String, Single) = Nothing
                        If result.TryGetValue(boneName, current) = False OrElse IsBetterDistribution(dist, current) Then
                            result(boneName) = dist
                        End If
                    Next
                Next
            Catch
            End Try
        Next

        For Each injectedBone In injectedUsedByShapes
            Dim dist As Dictionary(Of String, Single) = Nothing
            If result.TryGetValue(injectedBone, dist) Then dist = CollapseToNonInjectedBones(dist)
            If dist Is Nothing OrElse dist.Count = 0 Then dist = BuildAncestorFallbackReplacement(injectedBone)
            If dist.Count > 0 Then result(injectedBone) = dist
        Next

        Return result
    End Function

    Private Shared Function PlanShapeRewrite(shape As Shape_class,
                                             injectedReplacementMap As Dictionary(Of String, Dictionary(Of String, Single)),
                                             allowPaletteExpansion As Boolean,
                                             ByRef report As String) As ShapeRewritePlan
        If shape Is Nothing Then Return Nothing

        Dim context As ShapeAccessContext = Nothing
        If TryResolveShapeContext(shape, context, report) = False Then
            If String.IsNullOrWhiteSpace(report) Then Return Nothing
            Return Nothing
        End If

        Dim plan As New ShapeRewritePlan With {
            .Shape = shape,
            .NifShape = context.NifShape,
            .TriShape = context.TriShape,
            .Skin = context.Skin,
            .SkinData = context.SkinData,
            .UseSse = context.UseSse,
            .AllVertexInfluences = CloneInfluenceSnapshot(context.SourceInfluences)
        }

        Dim palette As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To context.BoneNames.Count - 1
            Dim boneName = NormalizeBoneName(context.BoneNames(i))
            If String.IsNullOrWhiteSpace(boneName) Then Continue For
            If palette.ContainsKey(boneName) = False Then palette.Add(boneName, i)
        Next

        Dim pendingPaletteBones As New List(Of String)
        Dim pendingPaletteSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim foundInjectedWeights = False

        For vertexIndex = 0 To context.SourceInfluences.Count - 1
            Dim currentInfluences = context.SourceInfluences(vertexIndex)
            Dim sawInjected = False
            Dim collapsed = CollapseVertexWeights(shape,
                                                  context.BoneNames,
                                                  currentInfluences,
                                                  injectedReplacementMap,
                                                  palette,
                                                  pendingPaletteBones,
                                                  pendingPaletteSet,
                                                  allowPaletteExpansion,
                                                  sawInjected,
                                                  report)
            If collapsed Is Nothing Then Return Nothing
            If sawInjected = False Then Continue For

            foundInjectedWeights = True
            plan.AllVertexInfluences(vertexIndex) = collapsed

            Dim newIndices As Byte() = Nothing
            Dim newWeights As Half() = Nothing
            ConvertInfluenceListToSkinArrays(collapsed, newIndices, newWeights)

            plan.VertexRewrites.Add(New VertexRewritePlan With {
                .VertexIndex = vertexIndex,
                .Influences = collapsed,
                .BoneIndices = newIndices,
                .BoneWeights = newWeights
            })
        Next

        If foundInjectedWeights = False Then Return Nothing

        plan.MissingPaletteBones.AddRange(pendingPaletteBones)
        Return plan
    End Function

    Private Shared Function ApplyShapeRewritePlan(plan As ShapeRewritePlan, ByRef report As String) As Boolean
        If plan Is Nothing OrElse plan.Shape Is Nothing OrElse plan.NifShape Is Nothing Then
            report = "Internal error applying the physics collapse plan."
            Return False
        End If

        If plan.MissingPaletteBones.Count > 0 Then
            For Each boneName In plan.MissingPaletteBones
                If EnsureBaseBoneInSkinPalette(plan.Shape, plan.NifShape, boneName, report) < 0 Then Return False
            Next
        End If

        If plan.TriShape IsNot Nothing Then
            If ApplyTriShapeVertexRewrites(plan, report) = False Then Return False
        End If

        If plan.SkinData IsNot Nothing Then
            If RewriteNiSkinDataWeights(plan, report) = False Then Return False
        End If

        plan.NifShape.UpdateBounds()

        If TypeOf plan.Skin Is NiSkinInstance Then
            plan.Shape.ParentSliderSet.NIFContent.UpdateSkinPartitions(plan.NifShape)
        End If

        Return True
    End Function
    Private Shared Function ApplyTriShapeVertexRewrites(plan As ShapeRewritePlan, ByRef report As String) As Boolean
        Dim tri = plan.TriShape
        If tri Is Nothing Then Return True

        If plan.UseSse Then
            If tri.VertexDataSSE Is Nothing Then
                report = $"Shape '{plan.Shape.Target}' has no SSE vertex data to rewrite."
                Return False
            End If

            Dim all = tri.VertexDataSSE.ToList()
            For Each rewrite In plan.VertexRewrites
                If rewrite.VertexIndex < 0 OrElse rewrite.VertexIndex >= all.Count Then
                    report = $"Vertex {rewrite.VertexIndex} is outside the SSE vertex buffer for shape '{plan.Shape.Target}'."
                    Return False
                End If

                Dim vertex = all(rewrite.VertexIndex)
                vertex.BoneIndices = CType(rewrite.BoneIndices.Clone(), Byte())
                vertex.BoneWeights = CType(rewrite.BoneWeights.Clone(), Half())
                all(rewrite.VertexIndex) = vertex
            Next
            tri.SetVertexDataSSE(all)
        Else
            If tri.VertexData Is Nothing Then
                report = $"Shape '{plan.Shape.Target}' has no vertex data to rewrite."
                Return False
            End If

            Dim all = tri.VertexData.ToList()
            For Each rewrite In plan.VertexRewrites
                If rewrite.VertexIndex < 0 OrElse rewrite.VertexIndex >= all.Count Then
                    report = $"Vertex {rewrite.VertexIndex} is outside the vertex buffer for shape '{plan.Shape.Target}'."
                    Return False
                End If

                Dim vertex = all(rewrite.VertexIndex)
                vertex.BoneIndices = CType(rewrite.BoneIndices.Clone(), Byte())
                vertex.BoneWeights = CType(rewrite.BoneWeights.Clone(), Half())
                all(rewrite.VertexIndex) = vertex
            Next
            tri.SetVertexData(all)
        End If

        Return True
    End Function

    Private Shared Function RewriteNiSkinDataWeights(plan As ShapeRewritePlan, ByRef report As String) As Boolean
        Dim skinData = plan.SkinData
        If skinData Is Nothing Then Return True

        skinData.HasVertexWeights = True

        Dim boneList = If(skinData.BoneList, New List(Of BoneData)()).ToList()
        If boneList.Count = 0 AndAlso plan.Skin IsNot Nothing AndAlso plan.Skin.Bones IsNot Nothing Then
            boneList = New List(Of BoneData)(plan.Skin.Bones.Count)
        End If

        While boneList.Count < plan.Skin.Bones.Count
            boneList.Add(New BoneData With {
                .SkinTransform = New NiTransform(),
                .BoundingSphere = New NiBound(),
                .VertexWeights = New List(Of BoneVertData)(),
                .NumVertices = 0
            })
        End While

        For i = 0 To boneList.Count - 1
            Dim bone = boneList(i)
            bone.VertexWeights = New List(Of BoneVertData)()
            bone.NumVertices = 0
            boneList(i) = bone
        Next

        For vertexIndex = 0 To plan.AllVertexInfluences.Count - 1
            For Each influence In plan.AllVertexInfluences(vertexIndex)
                If influence.Weight <= WeightEpsilon Then Continue For
                If influence.PaletteIndex < 0 OrElse influence.PaletteIndex >= boneList.Count Then
                    report = $"Shape '{plan.Shape.Target}' resolved palette index {influence.PaletteIndex}, but NiSkinData only contains {boneList.Count} bones."
                    Return False
                End If

                Dim bone = boneList(influence.PaletteIndex)
                If bone.VertexWeights Is Nothing Then bone.VertexWeights = New List(Of BoneVertData)()
                bone.VertexWeights.Add(New BoneVertData With {
                    .Index = CUShort(vertexIndex),
                    .Weight = influence.Weight
                })
                bone.NumVertices = CUShort(Math.Min(UShort.MaxValue, bone.VertexWeights.Count))
                boneList(influence.PaletteIndex) = bone
            Next
        Next

        skinData.BoneList = boneList
        skinData.NumBones = CUInt(boneList.Count)
        Return True
    End Function

    Private Shared Function CollapseVertexWeights(shape As Shape_class,
                                                  boneNames As List(Of String),
                                                  currentInfluences As List(Of VertexInfluence),
                                                  injectedReplacementMap As Dictionary(Of String, Dictionary(Of String, Single)),
                                                  palette As Dictionary(Of String, Integer),
                                                  pendingPaletteBones As List(Of String),
                                                  pendingPaletteSet As HashSet(Of String),
                                                  allowPaletteExpansion As Boolean,
                                                  ByRef sawInjected As Boolean,
                                                  ByRef report As String) As List(Of VertexInfluence)
        Dim merged As New Dictionary(Of Integer, Single)
        sawInjected = False

        If currentInfluences Is Nothing Then currentInfluences = New List(Of VertexInfluence)()

        For Each influence In currentInfluences
            If influence.Weight <= WeightEpsilon Then Continue For

            Dim localIndex = influence.PaletteIndex
            If localIndex < 0 OrElse localIndex >= boneNames.Count Then
                report = $"A vertex in shape '{shape.Target}' references palette index {localIndex}, but the palette only contains {boneNames.Count} bones."
                Return Nothing
            End If

            Dim boneName = NormalizeBoneName(boneNames(localIndex))
            If String.IsNullOrWhiteSpace(boneName) Then Continue For

            If SkeletonInstance.Default.IsInjectedBone(boneName) = False Then
                AddWeight(merged, localIndex, influence.Weight)
                Continue For
            End If

            sawInjected = True

            Dim replacement As Dictionary(Of String, Single) = Nothing
            If injectedReplacementMap.TryGetValue(boneName, replacement) = False OrElse replacement Is Nothing OrElse replacement.Count = 0 Then
                replacement = BuildAncestorFallbackReplacement(boneName)
            End If

            Dim paletteReplacement = ResolveReplacementForPalette(replacement,
                                                                 palette,
                                                                 pendingPaletteBones,
                                                                 pendingPaletteSet,
                                                                 allowPaletteExpansion,
                                                                 shape,
                                                                 boneName,
                                                                 report)
            If paletteReplacement Is Nothing Then Return Nothing

            For Each kvp In paletteReplacement
                AddWeight(merged, kvp.Key, influence.Weight * kvp.Value)
            Next
        Next

        If merged.Count = 0 Then
            report = $"The injected weights for shape '{shape.Target}' could not be collapsed into any non-injected bone."
            Return Nothing
        End If

        Dim ordered = ConvertInfluenceMapToOrderedList(merged, 4, True)
        If ordered.Count = 0 Then
            report = $"All replacement weights collapsed to zero for shape '{shape.Target}'."
            Return Nothing
        End If

        Return ordered
    End Function

    Private Shared Function ResolveReplacementForPalette(replacement As Dictionary(Of String, Single),
                                                         palette As Dictionary(Of String, Integer),
                                                         pendingPaletteBones As List(Of String),
                                                         pendingPaletteSet As HashSet(Of String),
                                                         allowPaletteExpansion As Boolean,
                                                         shape As Shape_class,
                                                         sourceInjectedBone As String,
                                                         ByRef report As String) As Dictionary(Of Integer, Single)
        Dim result As New Dictionary(Of Integer, Single)
        If replacement Is Nothing OrElse replacement.Count = 0 Then
            report = $"No replacement distribution could be built for injected bone '{sourceInjectedBone}'."
            Return Nothing
        End If

        For Each kvp In replacement
            If kvp.Value <= WeightEpsilon Then Continue For

            Dim targetBone = NormalizeBoneName(kvp.Key)
            If String.IsNullOrWhiteSpace(targetBone) Then Continue For

            Dim targetIndex = GetPredictedPaletteIndex(targetBone, palette, pendingPaletteBones)
            If targetIndex < 0 Then
                If allowPaletteExpansion Then
                    If SkeletonInstance.Default.IsInjectedBone(targetBone) Then targetBone = FindNearestNonInjectedAncestor(targetBone)
                    If String.IsNullOrWhiteSpace(targetBone) OrElse SkeletonInstance.Default.SkeletonDictionary.ContainsKey(targetBone) = False Then
                        report = $"The replacement bone '{kvp.Key}' for injected bone '{sourceInjectedBone}' does not exist in the base skeleton."
                        Return Nothing
                    End If
                    If pendingPaletteSet.Add(targetBone) Then pendingPaletteBones.Add(targetBone)
                    targetIndex = GetPredictedPaletteIndex(targetBone, palette, pendingPaletteBones)
                Else
                    Dim fallback = FindNearestBoneInSet(targetBone, palette.Keys.Concat(pendingPaletteBones))
                    If String.IsNullOrWhiteSpace(fallback) Then
                        report = $"Shape '{shape.Target}' needs bone '{targetBone}' in the palette, but phase 1 is not allowed to add bones."
                        Return Nothing
                    End If
                    targetIndex = GetPredictedPaletteIndex(fallback, palette, pendingPaletteBones)
                End If
            End If

            If targetIndex < 0 OrElse targetIndex > Byte.MaxValue Then
                report = $"The palette index resolved for bone '{targetBone}' is invalid."
                Return Nothing
            End If

            AddWeight(result, targetIndex, kvp.Value)
        Next

        NormalizeDistribution(result)
        Return result
    End Function

    Private Shared Function EnsureBaseBoneInSkinPalette(shape As Shape_class, nifShape As INiShape, boneName As String, ByRef report As String) As Integer
        boneName = NormalizeBoneName(boneName)
        If String.IsNullOrWhiteSpace(boneName) Then
            report = "A blank bone name cannot be added to the skin palette."
            Return -1
        End If
        Dim existingBoneNames = GetShapeBoneNames(shape, nifShape)
        Dim existingPaletteIndex = existingBoneNames.FindIndex(Function(name) String.Equals(NormalizeBoneName(name), boneName, StringComparison.OrdinalIgnoreCase))
        If existingPaletteIndex >= 0 Then Return existingPaletteIndex

        Dim skin = ResolveSkin(shape, nifShape)
        If skin Is Nothing Then
            report = $"Shape '{shape.Target}' has no skin container to expand."
            Return -1
        End If

        Dim boneBlockIndex = EnsureBaseBoneNodeExistsInNif(shape.ParentSliderSet, boneName, report)
        If boneBlockIndex < 0 Then Return -1

        If skin.Bones Is Nothing Then skin.Bones = New NiBlockPtrArray(Of NiNode)
        skin.Bones.AddBlockRef(boneBlockIndex)
        skin.NumBones = CUInt(skin.Bones.Count)

        If TypeOf skin Is BSSkin_Instance Then
            Dim typedSkin = DirectCast(skin, BSSkin_Instance)
            Dim boneData = TryCast(shape.ParentSliderSet.NIFContent.Blocks(typedSkin.Data.Index), BSSkin_BoneData)
            If boneData Is Nothing Then
                report = $"Shape '{shape.Target}' has no BSSkin_BoneData block."
                Return -1
            End If

            If boneData.BoneList Is Nothing Then boneData.BoneList = New List(Of BSSkinBoneTrans)()
            boneData.BoneList.Add(BuildBsSkinBoneTransForAddedBaseBone(shape, nifShape, boneName))
            boneData.NumBones = CUInt(boneData.BoneList.Count)
            Return skin.Bones.Count - 1
        End If

        If TypeOf skin Is NiSkinInstance Then
            Dim typedSkin = DirectCast(skin, NiSkinInstance)
            Dim skinData = TryCast(shape.ParentSliderSet.NIFContent.Blocks(typedSkin.Data.Index), NiSkinData)
            If skinData Is Nothing Then
                report = $"Shape '{shape.Target}' has no NiSkinData block."
                Return -1
            End If

            If skinData.BoneList Is Nothing Then skinData.BoneList = New List(Of BoneData)()
            skinData.BoneList.Add(BuildNiSkinBoneDataForAddedBaseBone(shape, nifShape, boneName))
            skinData.NumBones = CUInt(skinData.BoneList.Count)
            skinData.HasVertexWeights = True
            Return skin.Bones.Count - 1
        End If

        report = $"Palette expansion before removing physics is not implemented for skin type '{skin.GetType().Name}'."
        Return -1
    End Function

    Private Shared Function EnsureBaseBoneNodeExistsInNif(slider As SliderSet_Class, boneName As String, ByRef report As String) As Integer
        boneName = NormalizeBoneName(boneName)

        Dim existing = slider.NIFContent.FindBlockByName(Of NiNode)(boneName)
        Dim existingIndex As Integer
        If existing IsNot Nothing AndAlso slider.NIFContent.GetBlockIndex(existing, existingIndex) Then Return existingIndex

        If SkeletonInstance.Default.Skeleton Is Nothing Then
            report = $"The base skeleton is not loaded, so bone '{boneName}' cannot be cloned."
            Return -1
        End If

        Dim sourceNode = SkeletonInstance.Default.Skeleton.FindBlockByName(Of NiNode)(boneName)
        If sourceNode Is Nothing Then
            report = $"Bone '{boneName}' does not exist in the base skeleton."
            Return -1
        End If

        Dim rootNode = slider.NIFContent.GetRootNode()
        If rootNode Is Nothing Then
            report = "The current NIF has no root node."
            Return -1
        End If

        Dim parentNode As NiNode = rootNode
        Dim sourceParent = SkeletonInstance.Default.GetParentNodeSkeleton(boneName)
        If sourceParent IsNot Nothing Then
            Dim parentIndex = EnsureBaseBoneNodeExistsInNif(slider, sourceParent.Name.String, report)
            If parentIndex < 0 Then Return -1
            parentNode = TryCast(slider.NIFContent.Blocks(parentIndex), NiNode)
            If parentNode Is Nothing Then parentNode = rootNode
        End If

        Dim clonedIndex = slider.NIFContent.CloneNamedNode(boneName, SkeletonInstance.Default.Skeleton)
        If clonedIndex < 0 Then
            report = $"Bone '{boneName}' could not be cloned into the current NIF."
            Return -1
        End If

        If parentNode.Children Is Nothing Then parentNode.Children = New NiBlockRefArray(Of NiAVObject)
        parentNode.Children.AddBlockRef(clonedIndex)
        Return clonedIndex
    End Function

    Private Shared Function BuildBsSkinBoneTransForAddedBaseBone(shape As Shape_class, nifShape As INiShape, boneName As String) As BSSkinBoneTrans
        Dim templateSphere As New NiBound()
        Dim skin = TryCast(ResolveSkin(shape, nifShape), BSSkin_Instance)
        If skin IsNot Nothing Then
            Dim existingBoneData = TryCast(shape.ParentSliderSet.NIFContent.Blocks(skin.Data.Index), BSSkin_BoneData)
            If existingBoneData IsNot Nothing AndAlso existingBoneData.BoneList IsNot Nothing AndAlso existingBoneData.BoneList.Count > 0 Then
                templateSphere = existingBoneData.BoneList(0).BoundingSphere
            End If
        End If

        Dim localSkin = BuildLocalSkinTransformForAddedBaseBone(shape, nifShape, boneName)
        Return New BSSkinBoneTrans With {
            .BoundingSphere = templateSphere,
            .Rotation = localSkin.Rotation,
            .Translation = localSkin.Translation,
            .Scale = localSkin.Scale
        }
    End Function

    Private Shared Function BuildNiSkinBoneDataForAddedBaseBone(shape As Shape_class, nifShape As INiShape, boneName As String) As BoneData
        Dim templateSphere As New NiBound()
        Dim skin = TryCast(ResolveSkin(shape, nifShape), NiSkinInstance)
        If skin IsNot Nothing Then
            Dim existingSkinData = TryCast(shape.ParentSliderSet.NIFContent.Blocks(skin.Data.Index), NiSkinData)
            If existingSkinData IsNot Nothing AndAlso existingSkinData.BoneList IsNot Nothing AndAlso existingSkinData.BoneList.Count > 0 Then
                templateSphere = existingSkinData.BoneList(0).BoundingSphere
            End If
        End If

        Dim localSkin = BuildLocalSkinTransformForAddedBaseBone(shape, nifShape, boneName)
        Return New BoneData With {
            .BoundingSphere = templateSphere,
            .SkinTransform = New NiTransform With {
                .Rotation = localSkin.Rotation,
                .Translation = localSkin.Translation,
                .Scale = localSkin.Scale
            },
            .VertexWeights = New List(Of BoneVertData)(),
            .NumVertices = 0
        }
    End Function

    Private Shared Function BuildLocalSkinTransformForAddedBaseBone(shape As Shape_class, nifShape As INiShape, boneName As String) As Transform_Class
        Dim bindBone As HierarchiBone_class = Nothing
        If SkeletonInstance.Default.SkeletonDictionary.TryGetValue(boneName, bindBone) = False OrElse bindBone Is Nothing Then
            Return New Transform_Class()
        End If

        Dim shapeNode = TryCast(shape.ParentSliderSet.NIFContent.GetParentNode(nifShape), NiNode)
        If shapeNode Is Nothing Then shapeNode = shape.ParentSliderSet.NIFContent.GetRootNode()

        Dim shapeGlobal As Transform_Class = If(shapeNode Is Nothing,
                                                New Transform_Class(),
                                                Transform_Class.GetGlobalTransform(shapeNode, shape.ParentSliderSet.NIFContent))
        Return bindBone.OriginalGetGlobalTransform.Inverse.ComposeTransforms(shapeGlobal)
    End Function

    Private Shared Function TryResolveShapeContext(shape As Shape_class, ByRef context As ShapeAccessContext, ByRef report As String) As Boolean
        context = Nothing
        If shape Is Nothing OrElse shape.ParentSliderSet Is Nothing OrElse shape.ParentSliderSet.NIFContent Is Nothing Then Return False

        Dim nifShape = ResolveNifShape(shape)
        If nifShape Is Nothing Then Return False

        Dim skin = ResolveSkin(shape, nifShape)
        If skin Is Nothing Then Return False

        Dim boneNames = GetShapeBoneNames(shape, nifShape)
        If boneNames.Count = 0 Then Return False

        Dim tri = TryCast(nifShape, BSTriShape)
        Dim skinData As NiSkinData = Nothing
        Dim niSkin = TryCast(skin, NiSkinInstance)
        If niSkin IsNot Nothing AndAlso niSkin.Data IsNot Nothing AndAlso niSkin.Data.Index >= 0 Then
            skinData = TryCast(shape.ParentSliderSet.NIFContent.Blocks(niSkin.Data.Index), NiSkinData)
        End If

        Dim useSse = False
        Dim sourceInfluences = BuildVertexInfluenceSnapshot(nifShape, tri, skinData, useSse, report)
        If sourceInfluences Is Nothing OrElse sourceInfluences.Count = 0 Then Return False

        context = New ShapeAccessContext With {
            .NifShape = nifShape,
            .TriShape = tri,
            .Skin = skin,
            .SkinData = skinData,
            .BoneNames = boneNames,
            .SourceInfluences = sourceInfluences,
            .UseSse = useSse
        }
        Return True
    End Function
    Private Shared Function BuildVertexInfluenceSnapshot(nifShape As INiShape,
                                                         tri As BSTriShape,
                                                         skinData As NiSkinData,
                                                         ByRef useSse As Boolean,
                                                         ByRef report As String) As List(Of List(Of VertexInfluence))
        Dim triSnapshot = BuildTriShapeInfluenceSnapshot(tri, useSse)
        If triSnapshot IsNot Nothing AndAlso HasAnyInfluences(triSnapshot) Then Return triSnapshot

        Dim vertexCount = If(nifShape Is Nothing, 0, CInt(nifShape.VertexCount))
        Dim skinSnapshot = BuildNiSkinDataInfluenceSnapshot(skinData, vertexCount, report)
        If skinSnapshot IsNot Nothing AndAlso HasAnyInfluences(skinSnapshot) Then Return skinSnapshot

        If triSnapshot IsNot Nothing Then Return triSnapshot
        Return skinSnapshot
    End Function

    Private Shared Function BuildTriShapeInfluenceSnapshot(tri As BSTriShape, ByRef useSse As Boolean) As List(Of List(Of VertexInfluence))
        useSse = False
        If tri Is Nothing Then Return Nothing

        useSse = tri.VertexDataSSE IsNot Nothing AndAlso tri.VertexDataSSE.Count > 0
        Dim vertexCount = If(useSse,
                             If(tri.VertexDataSSE Is Nothing, 0, tri.VertexDataSSE.Count),
                             If(tri.VertexData Is Nothing, 0, tri.VertexData.Count))
        If vertexCount <= 0 Then Return Nothing

        Dim snapshot As New List(Of List(Of VertexInfluence))(vertexCount)
        For vertexIndex = 0 To vertexCount - 1
            Dim indices As Byte() = Nothing
            Dim weights As Half() = Nothing
            If TryGetVertexSkinData(tri, useSse, vertexIndex, indices, weights) Then
                Dim merged As New Dictionary(Of Integer, Single)
                Dim limit = Math.Min(indices.Length, weights.Length) - 1
                For i = 0 To limit
                    AddWeight(merged, CInt(indices(i)), CSng(weights(i)))
                Next
                snapshot.Add(ConvertInfluenceMapToOrderedList(merged, 4, True))
            Else
                snapshot.Add(New List(Of VertexInfluence)())
            End If
        Next

        Return snapshot
    End Function

    Private Shared Function BuildNiSkinDataInfluenceSnapshot(skinData As NiSkinData,
                                                             vertexCount As Integer,
                                                             ByRef report As String) As List(Of List(Of VertexInfluence))
        If skinData Is Nothing Then Return Nothing

        Dim inferredVertexCount = vertexCount
        If skinData.BoneList IsNot Nothing Then
            For Each bone In skinData.BoneList
                If bone.VertexWeights Is Nothing Then Continue For
                For Each bw In bone.VertexWeights
                    inferredVertexCount = Math.Max(inferredVertexCount, CInt(bw.Index) + 1)
                Next
            Next
        End If

        If inferredVertexCount <= 0 Then Return Nothing

        Dim merged As New List(Of Dictionary(Of Integer, Single))(inferredVertexCount)
        For i = 0 To inferredVertexCount - 1
            merged.Add(New Dictionary(Of Integer, Single)())
        Next

        If skinData.BoneList IsNot Nothing Then
            For boneIndex = 0 To skinData.BoneList.Count - 1
                Dim bone = skinData.BoneList(boneIndex)
                If bone.VertexWeights Is Nothing Then Continue For
                For Each bw In bone.VertexWeights
                    If bw.Index >= inferredVertexCount Then
                        report = $"A NiSkinData entry references vertex {bw.Index}, but the shape only exposes {inferredVertexCount} vertices."
                        Return Nothing
                    End If
                    AddWeight(merged(CInt(bw.Index)), boneIndex, bw.Weight)
                Next
            Next
        End If

        Dim snapshot As New List(Of List(Of VertexInfluence))(inferredVertexCount)
        For Each dict In merged
            snapshot.Add(ConvertInfluenceMapToOrderedList(dict, 4, True))
        Next
        Return snapshot
    End Function

    Private Shared Function ResolveNifShape(shape As Shape_class) As INiShape
        If shape Is Nothing OrElse shape.ParentSliderSet Is Nothing OrElse shape.ParentSliderSet.NIFContent Is Nothing Then Return Nothing

        Dim expectedNames = New List(Of String) From {
            NormalizeBoneName(shape.Nombre),
            NormalizeBoneName(shape.Target)
        }

        For Each nifShape In shape.ParentSliderSet.NIFContent.NifShapes
            Dim nifName = NormalizeBoneName(nifShape?.Name?.String)
            If String.IsNullOrWhiteSpace(nifName) Then Continue For
            If expectedNames.Any(Function(name) String.IsNullOrWhiteSpace(name) = False AndAlso String.Equals(name, nifName, StringComparison.OrdinalIgnoreCase)) Then Return nifShape
        Next

        Return Nothing
    End Function

    Private Shared Function ResolveSkin(shape As Shape_class, nifShape As INiShape) As INiSkin
        If shape Is Nothing OrElse nifShape Is Nothing OrElse shape.ParentSliderSet Is Nothing OrElse shape.ParentSliderSet.NIFContent Is Nothing Then Return Nothing
        If nifShape.SkinInstanceRef Is Nothing OrElse nifShape.SkinInstanceRef.Index < 0 Then Return Nothing
        Return TryCast(shape.ParentSliderSet.NIFContent.Blocks(nifShape.SkinInstanceRef.Index), INiSkin)
    End Function

    Private Shared Function GetShapeBoneNames(shape As Shape_class, Optional nifShape As INiShape = Nothing) As List(Of String)
        Dim boneNames As New List(Of String)
        If shape Is Nothing OrElse shape.ParentSliderSet Is Nothing OrElse shape.ParentSliderSet.NIFContent Is Nothing Then Return boneNames

        If nifShape Is Nothing Then nifShape = ResolveNifShape(shape)
        Dim skin = ResolveSkin(shape, nifShape)
        If skin Is Nothing OrElse skin.Bones Is Nothing Then Return boneNames

        For Each boneIndex In skin.Bones.Indices
            If boneIndex < 0 OrElse boneIndex >= shape.ParentSliderSet.NIFContent.Blocks.Count Then Continue For
            Dim node = TryCast(shape.ParentSliderSet.NIFContent.Blocks(boneIndex), NiNode)
            If node Is Nothing OrElse node.Name Is Nothing Then Continue For
            boneNames.Add(node.Name.String)
        Next

        Return boneNames
    End Function

    Private Shared Function BuildObjectSpaceSkinVertexDistributions(skin As HclObjectSpaceSkinPNOperatorGraph_Class) As Dictionary(Of Integer, Dictionary(Of String, Single))
        Dim result As New Dictionary(Of Integer, Dictionary(Of String, Single))
        If skin Is Nothing Then Return result

        For Each block In skin.SkinBlocks
            If block?.InfluenceBlock Is Nothing Then Continue For
            For Each entry In block.VertexEntries
                If entry Is Nothing Then Continue For
                If entry.VertexIndex = UShort.MaxValue Then Continue For
                If entry.SlotIndex < 0 OrElse entry.SlotIndex >= block.InfluenceBlock.VertexInfluences.Count Then Continue For

                Dim lane = block.InfluenceBlock.VertexInfluences(entry.SlotIndex)
                Dim dist = BuildDistributionFromLane(skin, lane)
                If dist.Count = 0 Then Continue For

                Dim vertexIndex = CInt(entry.VertexIndex)
                If result.ContainsKey(vertexIndex) Then
                    result(vertexIndex) = MergeBoneDistributions(result(vertexIndex), dist)
                Else
                    result(vertexIndex) = dist
                End If
            Next
        Next

        Return result
    End Function

    Private Shared Function BuildDistributionFromLane(skin As HclObjectSpaceSkinPNOperatorGraph_Class, lane As HclObjectSpaceSkinVertexInfluenceGraph_Class) As Dictionary(Of String, Single)
        Dim result As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        If skin Is Nothing OrElse lane Is Nothing Then Return result

        Dim limit = Math.Min(lane.TransformIndices.Count, lane.WeightBytes.Count) - 1
        For i = 0 To limit
            Dim weight = CSng(lane.WeightBytes(i))
            If weight <= 0.0F Then Continue For

            Dim boneName = ""
            If i < lane.ResolvedBoneNames.Count Then boneName = NormalizeBoneName(lane.ResolvedBoneNames(i))
            If String.IsNullOrWhiteSpace(boneName) Then
                Dim transformIndex = CInt(lane.TransformIndices(i))
                If transformIndex >= 0 AndAlso transformIndex < skin.ResolvedBoneNames.Count Then boneName = NormalizeBoneName(skin.ResolvedBoneNames(transformIndex))
            End If

            If String.IsNullOrWhiteSpace(boneName) OrElse boneName.StartsWith("#", StringComparison.Ordinal) Then Continue For
            AddWeight(result, boneName, weight)
        Next

        NormalizeDistribution(result)
        Return result
    End Function
    Private Shared Function BuildReplacementFromTriangleMapping(mapping As HclSimpleMeshBoneDeformMapping_Class,
                                                                vertexDistributions As Dictionary(Of Integer, Dictionary(Of String, Single))) As Dictionary(Of String, Single)
        Dim result As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        If mapping?.ResolvedTriangle Is Nothing OrElse vertexDistributions Is Nothing Then Return result

        Dim triangle = mapping.ResolvedTriangle
        Dim vertices = {CInt(triangle.Value0), CInt(triangle.Value1), CInt(triangle.Value2)}
        Dim matched = 0

        For Each vertexIndex In vertices
            Dim dist As Dictionary(Of String, Single) = Nothing
            If vertexDistributions.TryGetValue(vertexIndex, dist) = False Then Continue For
            matched += 1
            For Each kvp In dist
                AddWeight(result, kvp.Key, kvp.Value)
            Next
        Next

        If matched > 0 Then
            Dim scale = 1.0F / matched
            For Each key In result.Keys.ToList()
                result(key) *= scale
            Next
        End If

        NormalizeDistribution(result)
        Return result
    End Function

    Private Shared Function CollapseToNonInjectedBones(source As Dictionary(Of String, Single)) As Dictionary(Of String, Single)
        Dim result As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        If source Is Nothing Then Return result

        For Each kvp In source
            If kvp.Value <= WeightEpsilon Then Continue For
            Dim targetBone = NormalizeBoneName(kvp.Key)
            If String.IsNullOrWhiteSpace(targetBone) Then Continue For
            If SkeletonInstance.Default.IsInjectedBone(targetBone) Then targetBone = FindNearestNonInjectedAncestor(targetBone)
            If String.IsNullOrWhiteSpace(targetBone) Then Continue For
            AddWeight(result, targetBone, kvp.Value)
        Next

        NormalizeDistribution(result)
        Return result
    End Function

    Private Shared Function BuildAncestorFallbackReplacement(sourceBone As String) As Dictionary(Of String, Single)
        Dim result As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        Dim ancestor = FindNearestNonInjectedAncestor(sourceBone)
        If String.IsNullOrWhiteSpace(ancestor) = False Then result(ancestor) = 1.0F
        Return result
    End Function

    Private Shared Function FindNearestNonInjectedAncestor(boneName As String) As String
        boneName = NormalizeBoneName(boneName)
        If String.IsNullOrWhiteSpace(boneName) Then Return ""

        Dim current As HierarchiBone_class = Nothing
        If SkeletonInstance.Default.SkeletonDictionary.TryGetValue(boneName, current) = False Then Return ""

        While current IsNot Nothing
            If SkeletonInstance.Default.IsInjectedBone(current.BoneName) = False Then Return current.BoneName
            current = current.Parent
        End While

        Return ""
    End Function

    Private Shared Function FindNearestBoneInSet(boneName As String, candidateBones As IEnumerable(Of String)) As String
        Dim candidateSet As New HashSet(Of String)(candidateBones.Where(Function(name) String.IsNullOrWhiteSpace(name) = False), StringComparer.OrdinalIgnoreCase)
        If candidateSet.Count = 0 Then Return ""

        boneName = NormalizeBoneName(boneName)
        If String.IsNullOrWhiteSpace(boneName) Then Return ""
        If candidateSet.Contains(boneName) Then Return boneName

        Dim current As HierarchiBone_class = Nothing
        If SkeletonInstance.Default.SkeletonDictionary.TryGetValue(boneName, current) = False Then Return ""

        current = current.Parent
        While current IsNot Nothing
            If SkeletonInstance.Default.IsInjectedBone(current.BoneName) = False AndAlso candidateSet.Contains(current.BoneName) Then Return current.BoneName
            current = current.Parent
        End While

        Return ""
    End Function

    Private Shared Function GetPredictedPaletteIndex(boneName As String,
                                                     palette As Dictionary(Of String, Integer),
                                                     pendingPaletteBones As List(Of String)) As Integer
        Dim direct As Integer
        If palette.TryGetValue(boneName, direct) Then Return direct

        For i = 0 To pendingPaletteBones.Count - 1
            If String.Equals(pendingPaletteBones(i), boneName, StringComparison.OrdinalIgnoreCase) Then Return palette.Count + i
        Next

        Return -1
    End Function

    Private Shared Function MergeBoneDistributions(left As Dictionary(Of String, Single), right As Dictionary(Of String, Single)) As Dictionary(Of String, Single)
        Dim result As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        If left IsNot Nothing Then
            For Each kvp In left
                AddWeight(result, kvp.Key, kvp.Value)
            Next
        End If
        If right IsNot Nothing Then
            For Each kvp In right
                AddWeight(result, kvp.Key, kvp.Value)
            Next
        End If
        NormalizeDistribution(result)
        Return result
    End Function

    Private Shared Function IsBetterDistribution(candidate As Dictionary(Of String, Single), current As Dictionary(Of String, Single)) As Boolean
        If candidate Is Nothing OrElse candidate.Count = 0 Then Return False
        If current Is Nothing OrElse current.Count = 0 Then Return True
        If candidate.Count <> current.Count Then Return candidate.Count > current.Count
        Return candidate.Values.Sum() > current.Values.Sum()
    End Function

    Private Shared Function TryGetVertexSkinData(tri As BSTriShape,
                                                 useSse As Boolean,
                                                 vertexIndex As Integer,
                                                 ByRef boneIndices As Byte(),
                                                 ByRef boneWeights As Half()) As Boolean
        boneIndices = Nothing
        boneWeights = Nothing

        If useSse Then
            If tri.VertexDataSSE Is Nothing OrElse vertexIndex < 0 OrElse vertexIndex >= tri.VertexDataSSE.Count Then Return False
            boneIndices = tri.VertexDataSSE(vertexIndex).BoneIndices
            boneWeights = tri.VertexDataSSE(vertexIndex).BoneWeights
        Else
            If tri.VertexData Is Nothing OrElse vertexIndex < 0 OrElse vertexIndex >= tri.VertexData.Count Then Return False
            boneIndices = tri.VertexData(vertexIndex).BoneIndices
            boneWeights = tri.VertexData(vertexIndex).BoneWeights
        End If

        Return boneIndices IsNot Nothing AndAlso boneWeights IsNot Nothing
    End Function

    Private Shared Sub ConvertInfluenceListToSkinArrays(influences As List(Of VertexInfluence), ByRef boneIndices As Byte(), ByRef boneWeights As Half())
        boneIndices = New Byte(3) {}
        boneWeights = New Half(3) {}
        If influences Is Nothing Then Exit Sub

        For i = 0 To Math.Min(3, influences.Count - 1)
            boneIndices(i) = CByte(influences(i).PaletteIndex)
            boneWeights(i) = CType(influences(i).Weight, Half)
        Next
    End Sub

    Private Shared Function ConvertInfluenceMapToOrderedList(source As Dictionary(Of Integer, Single),
                                                             maxInfluences As Integer,
                                                             normalize As Boolean) As List(Of VertexInfluence)
        Dim result As New List(Of VertexInfluence)()
        If source Is Nothing OrElse source.Count = 0 Then Return result

        Dim entries = source.
            Where(Function(kvp) kvp.Value > WeightEpsilon).
            OrderByDescending(Function(kvp) kvp.Value).
            ThenBy(Function(kvp) kvp.Key).
            ToList()

        If maxInfluences > 0 AndAlso entries.Count > maxInfluences Then
            entries = entries.Take(maxInfluences).ToList()
        End If

        If entries.Count = 0 Then Return result

        Dim total = entries.Sum(Function(kvp) kvp.Value)
        If total <= WeightEpsilon Then Return result

        For Each kvp In entries
            Dim weight = kvp.Value
            If normalize Then weight = weight / total
            result.Add(New VertexInfluence With {
                .PaletteIndex = kvp.Key,
                .Weight = weight
            })
        Next

        Return result
    End Function
    Private Shared Function CloneInfluenceSnapshot(source As List(Of List(Of VertexInfluence))) As List(Of List(Of VertexInfluence))
        Dim clone As New List(Of List(Of VertexInfluence))
        If source Is Nothing Then Return clone

        For Each influences In source
            If influences Is Nothing Then
                clone.Add(New List(Of VertexInfluence)())
            Else
                clone.Add(influences.Select(Function(entry) New VertexInfluence With {
                    .PaletteIndex = entry.PaletteIndex,
                    .Weight = entry.Weight
                }).ToList())
            End If
        Next

        Return clone
    End Function

    Private Shared Function HasAnyInfluences(snapshot As List(Of List(Of VertexInfluence))) As Boolean
        If snapshot Is Nothing Then Return False
        Return snapshot.Any(Function(entry) entry IsNot Nothing AndAlso entry.Any(Function(weight) weight.Weight > WeightEpsilon))
    End Function

    Private Shared Sub AddWeight(target As Dictionary(Of String, Single), boneName As String, weight As Single)
        If weight <= 0.0F OrElse String.IsNullOrWhiteSpace(boneName) Then Exit Sub
        If target.ContainsKey(boneName) Then
            target(boneName) += weight
        Else
            target.Add(boneName, weight)
        End If
    End Sub

    Private Shared Sub AddWeight(target As Dictionary(Of Integer, Single), paletteIndex As Integer, weight As Single)
        If weight <= 0.0F OrElse paletteIndex < 0 Then Exit Sub
        If target.ContainsKey(paletteIndex) Then
            target(paletteIndex) += weight
        Else
            target.Add(paletteIndex, weight)
        End If
    End Sub

    Private Shared Sub NormalizeDistribution(target As Dictionary(Of String, Single))
        If target Is Nothing OrElse target.Count = 0 Then Exit Sub
        Dim total = target.Values.Sum()
        If total <= WeightEpsilon Then
            target.Clear()
            Exit Sub
        End If

        For Each key In target.Keys.ToList()
            target(key) = target(key) / total
        Next
    End Sub

    Private Shared Sub NormalizeDistribution(target As Dictionary(Of Integer, Single))
        If target Is Nothing OrElse target.Count = 0 Then Exit Sub
        Dim total = target.Values.Sum()
        If total <= WeightEpsilon Then
            target.Clear()
            Exit Sub
        End If

        For Each key In target.Keys.ToList()
            target(key) = target(key) / total
        Next
    End Sub

    Private Shared Function NormalizeBoneName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return ""
        Return name.Trim()
    End Function

    Private NotInheritable Class ShapeAccessContext
        Public Property NifShape As INiShape
        Public Property TriShape As BSTriShape
        Public Property Skin As INiSkin
        Public Property SkinData As NiSkinData
        Public Property BoneNames As List(Of String)
        Public Property SourceInfluences As List(Of List(Of VertexInfluence))
        Public Property UseSse As Boolean
    End Class

    Private NotInheritable Class ShapeRewritePlan
        Public Property Shape As Shape_class
        Public Property NifShape As INiShape
        Public Property TriShape As BSTriShape
        Public Property Skin As INiSkin
        Public Property SkinData As NiSkinData
        Public Property UseSse As Boolean
        Public Property AllVertexInfluences As List(Of List(Of VertexInfluence))
        Public ReadOnly Property MissingPaletteBones As New List(Of String)
        Public ReadOnly Property VertexRewrites As New List(Of VertexRewritePlan)
    End Class

    Private Structure VertexInfluence
        Public Property PaletteIndex As Integer
        Public Property Weight As Single
    End Structure

    Private Structure VertexRewritePlan
        Public Property VertexIndex As Integer
        Public Property Influences As List(Of VertexInfluence)
        Public Property BoneIndices As Byte()
        Public Property BoneWeights As Half()
    End Structure
End Class

