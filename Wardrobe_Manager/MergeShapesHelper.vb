' Version Uploaded of Wardrobe 3.2.0
Imports NiflySharp
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
        ' Unified merge — works for the BSTriShape family AND NiTriShape family.  Per-vertex
        ' positions/normals/tangents/etc. are concatenated via ShapeArrays (polymorphic);
        ' per-vertex skin comes through ShapeArrays.Skinning (flat ShapeSkinningData).
        ' Bone palette remap is applied to the flat BoneIndices byte array during concat.
        Dim targetNifRaw As INiShape = targetShape.RelatedNifShape
        If targetNifRaw Is Nothing Then Throw New InvalidOperationException("Target shape has no NIF data.")
        If donorShapes.Count = 0 Then Throw New InvalidOperationException("No donor shapes specified.")

        Dim targetGeom As IShapeGeometry = targetShape.IR_Geometry

        ' ── 1. Build merged bone list and per-donor bone remaps (BSSkin_Instance path) ──
        ' Merged palette tracks NIF block indices of each bone; donor-local bone idx gets
        ' translated to merged-local idx via donorBoneRemaps(di)(localIdx) → mergedIdx.
        Dim mergedBoneNifIndices As New List(Of Integer)()
        Dim mergedBoneTrans As New List(Of BSSkinBoneTrans)()

        Dim targetSkin = TryCast(targetShape.RelatedNifSkin, BSSkin_Instance)
        If targetSkin IsNot Nothing Then
            For Each idx In targetSkin.Bones.Indices
                mergedBoneNifIndices.Add(idx)
            Next
            Dim tbd = TryCast(sliderSet.NIFContent.Blocks(targetSkin.Data.Index), BSSkin_BoneData)
            If tbd IsNot Nothing Then mergedBoneTrans.AddRange(tbd.BoneList)
        End If

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

        ' ── 2. Snapshot target + donors polymorphically, compute donor vertex offsets ──
        Dim donorOffsets As New List(Of Integer)()
        Dim cumulative As Integer = targetGeom.VertexCount

        Dim allTris = targetGeom.GetTriangles()
        Dim mergedArrays = SkinningHelper.SnapshotSeparateArrays(targetGeom)

        For Each donor In donorShapes
            donorOffsets.Add(cumulative)
            cumulative += donor.IR_Geometry.VertexCount
        Next

        ' Apply bone remap to each donor's Skinning.BoneIndices, then append per-vertex
        ' arrays to mergedArrays.  Works for any shape family — BoneIndices is a flat
        ' byte array regardless of whether skin came from BSVertexData (BS) or
        ' NiSkinData/NiSkinPartition (NiTri).
        For di = 0 To donorShapes.Count - 1
            Dim donorArrays = SkinningHelper.SnapshotSeparateArrays(donorShapes(di).IR_Geometry)
            Dim boneRemap = donorBoneRemaps(di)
            If donorArrays.Skinning.HasValue AndAlso boneRemap.Count > 0 Then
                Dim sk = donorArrays.Skinning.Value
                If sk.BoneIndices IsNot Nothing Then
                    ' Clone the BoneIndices array before mutating — Skinning is a struct
                    ' but its array field is a shared reference.
                    Dim cloned(sk.BoneIndices.Length - 1) As Byte
                    Array.Copy(sk.BoneIndices, cloned, sk.BoneIndices.Length)
                    Dim value As Integer = Nothing
                    For b = 0 To cloned.Length - 1
                        Dim orig As Integer = CInt(cloned(b))
                        If boneRemap.TryGetValue(orig, value) Then cloned(b) = CByte(value And &HFF)
                    Next
                    Dim remappedSk = sk
                    remappedSk.BoneIndices = cloned
                    donorArrays.Skinning = remappedSk
                End If
            End If
            mergedArrays.Append(donorArrays)
        Next

        ' ── 2b. Capture per-triangle body-part assignments BEFORE geometry changes ───
        ' GetTriangleBodyParts returns Nothing for FO4 BSSkin_Instance — no-op for FO4.
        Dim targetBodyParts = sliderSet.NIFContent.GetTriangleBodyParts(targetNifRaw)
        Dim donorBodyParts As New List(Of List(Of Integer))()
        For Each donor In donorShapes
            donorBodyParts.Add(sliderSet.NIFContent.GetTriangleBodyParts(donor.RelatedNifShape))
        Next

        ' ── 3. Concatenate triangles with vertex offset, building multi-source provenance ──
        ' Target triangles: same-shape provenance (oldIdx = original tri idx).
        ' Donor triangles: cross-shape provenance (donor adapter + donor-local oldIdx).
        ' The adapter's RedistributeSegments preserves target segments; donor segments are
        ' appended manually in MergeMetadataAfterApply.
        Dim mergedProv As New List(Of TriangleSource)(allTris.Count + 100)
        For tIdx = 0 To allTris.Count - 1
            mergedProv.Add(New TriangleSource(Nothing, tIdx))
        Next
        For di = 0 To donorShapes.Count - 1
            Dim off = donorOffsets(di)
            Dim donorGeomForProv = donorShapes(di).IR_Geometry
            Dim donorTriList = donorGeomForProv.GetTriangles()
            For dIdx = 0 To donorTriList.Count - 1
                Dim t = donorTriList(dIdx)
                allTris.Add(New Triangle(
                    CUShort(CInt(t.V1) + off),
                    CUShort(CInt(t.V2) + off),
                    CUShort(CInt(t.V3) + off)))
                mergedProv.Add(New TriangleSource(donorGeomForProv, dIdx))
            Next
        Next
        Dim mergedProvenance As New TriangleRemap(mergedProv)

        ' ── Apply all geometry via centralized helper (fully polymorphic) ─────
        SkinningHelper.ApplyShapeGeometry(targetGeom, allTris, mergedArrays, mergedProvenance)

        ' ── 4b. Merge-specific metadata append (BSSubIndex Segments + BSMeshLOD lossy).
        ' The adapter's provenance handler preserves target Segments correctly (same-shape
        ' entries → counted) but skips cross-shape entries (donors).  Append donor segments
        ' here with proper StartIndex offset so dismember regions survive merge.  Only
        ' applies when target is BSTriShape family (BSSubIndex/BSMeshLOD specifically);
        ' for NiTri family targets MergeMetadataAfterApply no-ops internally.
        MergeMetadataAfterApply(targetNifRaw, donorShapes, donorOffsets)

        ' ── 5. Update BSSkin_Instance bones and bone data ────────────────────
        If targetSkin IsNot Nothing Then
            targetSkin.Bones.Clear()
            For Each idx In mergedBoneNifIndices
                targetSkin.Bones.AddBlockRef(idx)
            Next
            Dim tbd = TryCast(sliderSet.NIFContent.Blocks(targetSkin.Data.Index), BSSkin_BoneData)
            If tbd IsNot Nothing Then tbd.BoneList = mergedBoneTrans
        End If

        targetGeom.UpdateBounds()

        ' ── 5b. Update NiSkinInstance + NiSkinData for SSE shapes ────────────────
        ' BSSkin_Instance (FO4) was handled above.  For NiSkinInstance (SSE/Oblivion):
        ' NiSkinData.BoneList is what UpdateSkinPartitions reads to build vertBoneWeights.
        ' After merge, donor vertices are absent → crash.  Rebuild the bone list here.
        Dim niSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(targetNifRaw.SkinInstanceRef)
        If niSkinInst IsNot Nothing Then
            ' Build merged NiSkinInstance bone list from the target + all donors.
            Dim niMergedBoneNifIndices = niSkinInst.Bones.Indices.ToList()
            Dim niDonorBoneRemaps As New List(Of Dictionary(Of Integer, Integer))()

            For Each donor In donorShapes
                Dim niRemap As New Dictionary(Of Integer, Integer)()
                niDonorBoneRemaps.Add(niRemap)
                ' SkinInstanceRef is defined on INiShape — no need to downcast.  The old
                ' `DirectCast(..., BSTriShape)` was a residue from the pre-refactor path
                ' and fails for NiTriShape family.
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
                        donorShapes(di).RelatedNifShape?.SkinInstanceRef)
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
                    mergedBP.AddRange(Enumerable.Repeat(-1, donorShapes(di).IR_Geometry.TriangleCount))
                End If
            Next
            sliderSet.NIFContent.SetTriangleBodyParts(targetNifRaw, mergedBP)
        End If

        sliderSet.NIFContent.UpdateSkinPartitions(targetNifRaw)

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

    ''' <summary>
    ''' Merge-specific append/collapse for count-derived metadata that the per-triangle
    ''' provenance redistribution can't express on its own.  Called after
    ''' SkinningHelper.ApplyShapeGeometry has written the merged triangle list (target +
    ''' donors concat) and the adapter has already preserved target-side metadata via the
    ''' same-shape provenance entries.
    '''
    ''' BSSubIndexTriShape: target's Segments are intact post-Apply; append each donor's
    ''' Segments with StartIndex offset by accumulated triangle count (×3 for vertex-index
    ''' units).  SubSegmentDatas inherit the same offset.  Preserves dismember granularity
    ''' across the merged shape.
    '''
    ''' BSMeshLODTriShape: lossy collapse to LOD2 — target's LOD layout doesn't survive
    ''' a concat that doesn't reorder triangles per LOD bucket.  Same canonical behaviour
    ''' as BodySlide-and-Outfit-Studio Geometry.cpp:1522 BSMeshLODTriShape::
    ''' notifyVerticesDelete (lodSize2 = numTriangles).
    '''
    ''' DEBUGGER.BREAK / TO TEST: this path activates only on actor/armor merges where the
    ''' target is BSSubIndex (rare in BodySlide outfits).  First execution should be
    ''' verified in NifSkope: target Segments preserved + donor Segments appended at correct
    ''' StartIndex.  See memory: pending_tests_shape_metadata.md
    ''' </summary>
    Private Shared Sub MergeMetadataAfterApply(targetNif As INiShape,
                                                donorShapes As List(Of Shape_class),
                                                donorOffsets As List(Of Integer))
        Dim targetSubIndex = TryCast(targetNif, BSSubIndexTriShape)
        ' BSMeshLOD merge LOD handling is now done by the adapter's ReorderTrianglesByLODTier
        ' during ApplyShapeGeometry — cross-shape (donor) triangles default to LOD2, target
        ' triangles preserve their original tier.  This function only handles BSSubIndex
        ' donor-segment append (the one piece the adapter's same-shape RedistributeSegments
        ' can't cover since donor entries are cross-shape).
        If targetSubIndex Is Nothing Then Return

        ' BSSubIndex: append donor segments with offset.
        If targetSubIndex IsNot Nothing Then
            Dim merged As New List(Of BSGeometrySegmentData)()
            If targetSubIndex.Segments IsNot Nothing Then merged.AddRange(targetSubIndex.Segments)
            For di = 0 To donorShapes.Count - 1
                Dim donorSub = TryCast(donorShapes(di).RelatedNifShape, BSSubIndexTriShape)
                If donorSub Is Nothing OrElse donorSub.Segments Is Nothing Then Continue For
                Dim triOffset As UInteger = CUInt(donorOffsets(di))   ' donor triangle offset = vertex offset / 3 — actually donor triangles started at the END of target triangles.
                ' Actually triangle offset = current cumulative triangle count BEFORE this donor.
                ' donorOffsets(di) is VERTEX offset, not triangle.  Need to recompute triangle offset.
                Dim triOffsetTris As Integer = targetSubIndex.TriangleCount
                For prev = 0 To di - 1
                    triOffsetTris += donorShapes(prev).RelatedNifShape.TriangleCount
                Next
                Dim startIdxOffsetUnits As UInteger = CUInt(triOffsetTris) * 3UI
                For Each dSeg In donorSub.Segments
                    Dim newSeg As New BSGeometrySegmentData() With {
                        .Flags = dSeg.Flags,
                        .StartIndex = dSeg.StartIndex + startIdxOffsetUnits,
                        .NumPrimitives = dSeg.NumPrimitives,
                        .ParentArrayIndex = dSeg.ParentArrayIndex,
                        .NumSubSegments = dSeg.NumSubSegments,
                        .SubSegment = OffsetSubSegments(dSeg.SubSegment, startIdxOffsetUnits)
                    }
                    merged.Add(newSeg)
                Next
            Next
            targetSubIndex.Segments = merged
        End If
    End Sub

    ''' <summary>
    ''' Helper: clones a sub-segment list and bumps every StartIndex by the given offset
    ''' (in vertex-index units, ×3 from triangle count).  NumPrimitives and ParentArrayIndex
    ''' are preserved verbatim.
    ''' </summary>
    Private Shared Function OffsetSubSegments(subs As List(Of BSGeometrySubSegment),
                                               startIdxOffsetUnits As UInteger) As List(Of BSGeometrySubSegment)
        If subs Is Nothing Then Return New List(Of BSGeometrySubSegment)()
        Dim result As New List(Of BSGeometrySubSegment)(subs.Count)
        For Each s In subs
            result.Add(New BSGeometrySubSegment() With {
                .StartIndex = s.StartIndex + startIdxOffsetUnits,
                .NumPrimitives = s.NumPrimitives,
                .ParentArrayIndex = s.ParentArrayIndex,
                .Unused = s.Unused
            })
        Next
        Return result
    End Function

End Class
