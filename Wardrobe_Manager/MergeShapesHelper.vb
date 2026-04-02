Imports NiflySharp.Blocks
Imports NiflySharp.Structs

''' <summary>
''' Merges donorShapes into targetShape in-memory:
'''   � NIF: vertex data concatenated (target first), triangles offset, bones union-remapped.
'''   � OSD: donor deltas offset by cumulative vertex count and merged into target blocks.
'''   � OSP: donor Shape_class objects removed via SliderSet.RemoveShape (handles XML, datas, OSD blocks).
''' Vertices at seams are NOT welded � duplicates are kept for safety and morph correctness.
''' Call InvalidateAllLookupCaches + Process_render_Changes(True) after this returns.
''' </summary>
Public Class MergeShapesHelper




    Public Shared Function GetShaderType(shape As Shape_class) As Type
        Dim shader = shape.ParentSliderSet.NIFContent.GetShader(shape.RelatedNifShape)
        If shader Is Nothing Then Return Nothing
        If TypeOf shader Is BSLightingShaderProperty Then Return GetType(BSLightingShaderProperty)
        If TypeOf shader Is BSEffectShaderProperty Then Return GetType(BSEffectShaderProperty)
        Return Nothing
    End Function

    Public Shared Function AreCompatible(shapes As IEnumerable(Of Shape_class)) As Boolean
        Dim types = shapes.Select(Function(s) GetShaderType(s)).Distinct().ToList()
        Return types.Count = 1 AndAlso types(0) IsNot Nothing
    End Function

    Public Shared Sub Merge(targetShape As Shape_class, donorShapes As List(Of Shape_class), sliderSet As SliderSet_Class)
        Dim targetNif = targetShape.RelatedNifShape
        If targetNif Is Nothing Then Throw New InvalidOperationException("Target shape has no NIF data.")
        If donorShapes.Count = 0 Then Throw New InvalidOperationException("No donor shapes specified.")

        Dim version = sliderSet.NIFContent.Header.Version
        Dim isSSE = targetNif.VertexDataSSE IsNot Nothing AndAlso targetNif.VertexDataSSE.Count > 0

        ' ── 1. Build merged bone list and per-donor bone remaps ───────────────
        Dim mergedBoneNifIndices As New List(Of Integer)()   ' NIF block index of each merged bone
        Dim mergedBoneTrans As New List(Of BSSkinBoneTrans)() ' Bind transforms in same order

        Dim targetSkin = TryCast(targetShape.RelatedNifSkin, BSSkin_Instance)
        If targetSkin IsNot Nothing Then
            For Each idx In targetSkin.Bones.Indices
                mergedBoneNifIndices.Add(idx)
            Next
            Dim tbd = TryCast(sliderSet.NIFContent.Blocks(targetSkin.Data.Index), BSSkin_BoneData)
            If tbd IsNot Nothing Then mergedBoneTrans.AddRange(tbd.BoneList)
        End If

        ' donorBoneRemap(di)(localBoneIdx) = mergedBoneIdx
        Dim donorBoneRemaps As New List(Of Dictionary(Of Integer, Integer))()
        For Each donor In donorShapes
            Dim remap As New Dictionary(Of Integer, Integer)()
            donorBoneRemaps.Add(remap)

            Dim dSkin = TryCast(donor.RelatedNifSkin, BSSkin_Instance)
            If dSkin Is Nothing Then Continue For

            Dim dBoneIdxList = dSkin.Bones.Indices.ToList()
            Dim dbd = TryCast(sliderSet.NIFContent.Blocks(dSkin.Data.Index), BSSkin_BoneData)

            For localIdx = 0 To dBoneIdxList.Count - 1
                Dim nifBlockIdx = dBoneIdxList(localIdx)
                Dim mergedIdx = mergedBoneNifIndices.IndexOf(nifBlockIdx)
                If mergedIdx = -1 Then
                    mergedIdx = mergedBoneNifIndices.Count
                    mergedBoneNifIndices.Add(nifBlockIdx)
                    If dbd IsNot Nothing AndAlso localIdx < dbd.BoneList.Count Then
                        mergedBoneTrans.Add(dbd.BoneList(localIdx))
                    End If
                End If
                remap(localIdx) = mergedIdx
            Next
        Next

        ' ── 2. Snapshot vertex data and compute donor offsets ─────────────────
        ' Vertices are NOT welded — seam duplicates are kept as-is.
        Dim donorOffsets As New List(Of Integer)()
        Dim cumulative = If(isSSE, targetNif.VertexDataSSE.Count, targetNif.VertexData.Count)

        For Each donor In donorShapes
            donorOffsets.Add(cumulative)
            cumulative += If(isSSE,
                             donor.RelatedNifShape.VertexDataSSE.Count,
                             donor.RelatedNifShape.VertexData.Count)
        Next

        ' Snapshot triangles and separate arrays BEFORE SetVertexDataSSE — it resets them.
        Dim allTris = targetNif.Triangles.ToList()
        Dim mergedArrays = SkinningHelper.SnapshotSeparateArrays(targetNif)
        For Each donor In donorShapes
            mergedArrays.Append(SkinningHelper.SnapshotSeparateArrays(donor.RelatedNifShape))
        Next

        ' ── 2b. Capture per-triangle body-part assignments BEFORE geometry changes ───
        ' GetTriangleBodyParts returns Nothing for FO4 (BSSkin_Instance) — no-op for FO4.
        Dim targetBodyParts = sliderSet.NIFContent.GetTriangleBodyParts(targetNif)
        Dim donorBodyParts As New List(Of List(Of Integer))()
        For Each donor In donorShapes
            donorBodyParts.Add(sliderSet.NIFContent.GetTriangleBodyParts(donor.RelatedNifShape))
        Next

        ' ── 3. Concatenate vertex data with bone index remapping ──────────────
        Dim allSSE As List(Of BSVertexDataSSE) = Nothing
        Dim allNon As List(Of BSVertexData) = Nothing
        If isSSE Then
            allSSE = targetNif.VertexDataSSE.ToList()
            For di = 0 To donorShapes.Count - 1
                Dim remap = donorBoneRemaps(di)
                For Each vd In donorShapes(di).RelatedNifShape.VertexDataSSE
                    Dim nv = vd   ' struct copy

                    Dim value As Integer = Nothing

                    If nv.BoneIndices IsNot Nothing Then
                        nv.BoneIndices = nv.BoneIndices.ToArray()   ' clone ref array before mutating
                        For b = 0 To nv.BoneIndices.Length - 1
                            Dim orig = CInt(nv.BoneIndices(b))
                            If remap.TryGetValue(orig, value) Then nv.BoneIndices(b) = CByte(value)
                        Next
                    End If
                    allSSE.Add(nv)
                Next
            Next
        Else
            allNon = targetNif.VertexData.ToList()
            For di = 0 To donorShapes.Count - 1
                Dim remap = donorBoneRemaps(di)
                For Each vd In donorShapes(di).RelatedNifShape.VertexData
                    Dim nv = vd   ' struct copy

                    Dim value As Integer = Nothing

                    If nv.BoneIndices IsNot Nothing Then
                        nv.BoneIndices = nv.BoneIndices.ToArray()   ' clone ref array before mutating
                        For b = 0 To nv.BoneIndices.Length - 1
                            Dim orig = CInt(nv.BoneIndices(b))
                            If remap.TryGetValue(orig, value) Then nv.BoneIndices(b) = CByte(value)
                        Next
                    End If
                    allNon.Add(nv)
                Next
            Next
        End If

        ' ── 4. Concatenate triangles with vertex offset ───────────────────────
        For di = 0 To donorShapes.Count - 1
            Dim off = donorOffsets(di)
            For Each t In donorShapes(di).RelatedNifShape.Triangles
                allTris.Add(New Triangle(
                    CUShort(CInt(t(CUShort(0))) + off),
                    CUShort(CInt(t(CUShort(1))) + off),
                    CUShort(CInt(t(CUShort(2))) + off)))
            Next
        Next

        ' ── Apply all geometry via centralized helper ─────────────────────────
        SkinningHelper.ApplyShapeGeometry(targetNif, version, isSSE, allSSE, allNon, allTris, mergedArrays)

        ' ── 5. Update BSSkin_Instance bones and bone data ────────────────────
        If targetSkin IsNot Nothing Then
            targetSkin.Bones.Clear()
            For Each idx In mergedBoneNifIndices
                targetSkin.Bones.AddBlockRef(idx)
            Next
            Dim tbd = TryCast(sliderSet.NIFContent.Blocks(targetSkin.Data.Index), BSSkin_BoneData)
            If tbd IsNot Nothing Then tbd.BoneList = mergedBoneTrans
        End If

        targetNif.UpdateBounds()

        ' ── 5b. Update NiSkinInstance + NiSkinData for SSE shapes ────────────────
        ' BSSkin_Instance (FO4) was handled above.  For NiSkinInstance (SSE/Oblivion):
        ' NiSkinData.BoneList is what UpdateSkinPartitions reads to build vertBoneWeights.
        ' After merge, donor vertices are absent → crash.  Rebuild the bone list here.
        Dim niSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(targetNif.SkinInstanceRef)
        If niSkinInst IsNot Nothing Then
            ' Build merged NiSkinInstance bone list from the target + all donors.
            Dim niMergedBoneNifIndices = niSkinInst.Bones.Indices.ToList()
            Dim niDonorBoneRemaps As New List(Of Dictionary(Of Integer, Integer))()

            For Each donor In donorShapes
                Dim niRemap As New Dictionary(Of Integer, Integer)()
                niDonorBoneRemaps.Add(niRemap)
                Dim dNiSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(
                    donor.RelatedNifShape.SkinInstanceRef)
                If dNiSkinInst Is Nothing Then Continue For
                Dim dBoneIdxList = dNiSkinInst.Bones.Indices.ToList()
                For localIdx = 0 To dBoneIdxList.Count - 1
                    Dim nifBlockIdx = dBoneIdxList(localIdx)
                    Dim mergedIdx = niMergedBoneNifIndices.IndexOf(nifBlockIdx)
                    If mergedIdx = -1 Then
                        mergedIdx = niMergedBoneNifIndices.Count
                        niMergedBoneNifIndices.Add(nifBlockIdx)
                    End If
                    niRemap(localIdx) = mergedIdx
                Next
            Next

            ' Write merged bone list back to NiSkinInstance.
            niSkinInst.Bones.SetIndices(niMergedBoneNifIndices)
            niSkinInst.NumBones = CUInt(niMergedBoneNifIndices.Count)

            ' Update NiSkinData.BoneList with donor vertex weights.
            Dim niSkinData = sliderSet.NIFContent.GetBlock(niSkinInst.Data)
            If niSkinData IsNot Nothing Then
                ' Grow BoneList to fit all merged bones.
                Do While niSkinData.BoneList.Count < niMergedBoneNifIndices.Count
                    niSkinData.BoneList.Add(New BoneData())
                Loop
                niSkinData.NumBones = CUInt(niSkinData.BoneList.Count)

                ' Ensure all VertexWeights lists are initialized (BoneData is a struct).
                For b = 0 To niSkinData.BoneList.Count - 1
                    Dim bd = niSkinData.BoneList(b)
                    If bd.VertexWeights Is Nothing Then
                        bd.VertexWeights = New List(Of BoneVertData)()
                        niSkinData.BoneList(b) = bd
                    End If
                Next

                ' Append donor vertex weights, remapping bone index and offsetting vertex index.
                For di = 0 To donorShapes.Count - 1
                    Dim vertOffset = donorOffsets(di)
                    Dim niRemap = niDonorBoneRemaps(di)
                    Dim dNiSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(
                        donorShapes(di).RelatedNifShape.SkinInstanceRef)
                    If dNiSkinInst Is Nothing Then Continue For
                    Dim dNiSkinData = sliderSet.NIFContent.GetBlock(dNiSkinInst.Data)
                    If dNiSkinData Is Nothing Then Continue For

                    For localBoneIdx = 0 To dNiSkinData.BoneList.Count - 1
                        Dim mergedBoneIdx As Integer
                        If Not niRemap.TryGetValue(localBoneIdx, mergedBoneIdx) Then Continue For
                        Dim dBoneData = dNiSkinData.BoneList(localBoneIdx)
                        If dBoneData.VertexWeights Is Nothing Then Continue For
                        For Each vw In dBoneData.VertexWeights
                            niSkinData.BoneList(mergedBoneIdx).VertexWeights.Add(
                                New BoneVertData() With {
                                    .Index = CUShort(CInt(vw.Index) + vertOffset),
                                    .Weight = vw.Weight
                                })
                        Next
                    Next
                Next

                ' Update NumVertices per bone.
                For b = 0 To niSkinData.BoneList.Count - 1
                    Dim bd = niSkinData.BoneList(b)
                    bd.NumVertices = CUShort(If(bd.VertexWeights IsNot Nothing, bd.VertexWeights.Count, 0))
                    niSkinData.BoneList(b) = bd
                Next
            End If
        End If

        ' ── 5d. Restore body-part assignments for all triangles (target + donors) ───
        ' targetBodyParts is Nothing for FO4 → SetTriangleBodyParts is skipped entirely.
        If targetBodyParts IsNot Nothing Then
            Dim mergedBP As New List(Of Integer)(allTris.Count)
            mergedBP.AddRange(targetBodyParts)
            For di = 0 To donorShapes.Count - 1
                Dim dBP = donorBodyParts(di)
                If dBP IsNot Nothing Then
                    mergedBP.AddRange(dBP)
                Else
                    ' Donor had no NiSkinPartition — fall back to partition 0.
                    mergedBP.AddRange(Enumerable.Repeat(-1, donorShapes(di).RelatedNifShape.Triangles.Count))
                End If
            Next
            sliderSet.NIFContent.SetTriangleBodyParts(targetNif, mergedBP)
        End If

        sliderSet.NIFContent.UpdateSkinPartitions(targetNif)

        ' ── 6. Merge OSD blocks: donor deltas offset into target blocks ───────
        Dim osdFilename = IO.Path.GetFileName(
            sliderSet.SourceFileFullPath).Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        For Each slider In sliderSet.Sliders
            Dim targetBlock = Slider_Data_class.GetEditableTargetBlock(targetShape, slider, sliderSet, osdFilename)

            For di = 0 To donorShapes.Count - 1
                Dim donor = donorShapes(di)
                Dim vertOffset = donorOffsets(di)

                ' Collect diffs from ALL donor entries (local + external).
                Dim donorDatas = slider.Datas.Where(
                Function(d) d.Target.Equals(donor.Target, StringComparison.OrdinalIgnoreCase)).
                                ToList()
                If donorDatas.Any(Function(d) d.Islocal) Then
                    donorDatas = donorDatas.Where(Function(d) d.Islocal).ToList()
                End If

                Dim diffs = donorDatas.
                    SelectMany(Function(d) d.RelatedOSDBlocks).
                    SelectMany(Function(b) b.SnapshotDiffs()).
                    Select(Function(d) New OSD_DataDiff_Class() With {
                        .Index = d.Index + vertOffset, .X = d.X, .Y = d.Y, .Z = d.Z}).
                    ToList()
                If diffs.Count = 0 Then Continue For

                targetBlock.DataDiff.AddRange(diffs)
            Next

            targetBlock.RebuildCompactArrays()
        Next

        ' ── 7. Remove donor entries from BaseMaterials ────────────────────────
        Dim bm = sliderSet.NIFContent.BaseMaterials
        For Each donor In donorShapes
            Dim donorNifName = donor.RelatedNifShape?.Name?.String
            If donorNifName IsNot Nothing Then bm.Remove(donorNifName)
        Next

        ' ── 8. Remove donor shapes (NIF blocks + OSD + XML + slider datas) ────
        ' RemoveShape handles everything; call after OSD merge so donor blocks
        ' are still accessible during step 6.
        For Each donor In donorShapes
            sliderSet.RemoveShape(donor)
        Next

        sliderSet.InvalidateAllLookupCaches()
    End Sub

End Class
