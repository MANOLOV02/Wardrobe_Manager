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

        ' ── 1b. Build merged bone list and per-donor bone remaps (NiSkinInstance path) ──
        ' SSE/Oblivion shapes carry their per-vertex skin inline (same flat BoneIndices array
        ' as FO4) AND a separate NiSkinData.BoneList.  Both representations index the SAME bone
        ' palette, so the merged palette + per-donor remap must be computed ONCE here, BEFORE
        ' the step-2 donor append, and then reused by step 5b for NiSkinData/NiSkinInstance.
        ' For FO4 shapes targetNifRaw.SkinInstanceRef resolves to a BSSkin_Instance (not a
        ' NiSkinInstance) so niSkinInst is Nothing and this block is skipped — FO4 keeps using
        ' donorBoneRemaps (step 1) exclusively, unchanged.
        Dim niSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(targetNifRaw.SkinInstanceRef)
        Dim niMergedBoneNifIndices As List(Of Integer) = Nothing
        Dim niDonorBoneRemaps As List(Of Dictionary(Of Integer, Integer)) = Nothing
        ' Parallel to niMergedBoneNifIndices: the merged per-bone BIND data (skin-to-bone
        ' SkinTransform + BoundingSphere) for the NiSkinData.BoneList.  Seeded from the TARGET's
        ' BoneList for its existing bones and EXTENDED with the donor's own BoneData for each
        ' donor-unique bone (mirrors the FO4 mergedBoneTrans path, step 1).  VertexWeights are
        ' deliberately NOT captured here — RebuildNiSkinData (re)authors them from the merged
        ' per-vertex skinning; only the bind (SkinTransform/BoundingSphere) is carried.
        Dim niMergedBind As List(Of BoneData) = Nothing
        If niSkinInst IsNot Nothing Then
            niMergedBoneNifIndices = niSkinInst.Bones.Indices.ToList()
            niDonorBoneRemaps = New List(Of Dictionary(Of Integer, Integer))()
            niMergedBind = New List(Of BoneData)()

            ' Seed merged bind from the target's NiSkinData.BoneList, index-aligned with the
            ' target's palette entries already in niMergedBoneNifIndices.  Pad with a default
            ' (identity) BoneData if the target BoneList is shorter than its palette (defensive).
            Dim tNiSkinData = sliderSet.NIFContent.GetBlock(niSkinInst.Data)
            For paletteIdx = 0 To niMergedBoneNifIndices.Count - 1
                If tNiSkinData IsNot Nothing AndAlso paletteIdx < tNiSkinData.BoneList.Count Then
                    niMergedBind.Add(tNiSkinData.BoneList(paletteIdx))
                Else
                    niMergedBind.Add(New BoneData())
                End If
            Next

            For Each donor In donorShapes
                Dim niRemap As New Dictionary(Of Integer, Integer)()
                niDonorBoneRemaps.Add(niRemap)
                ' SkinInstanceRef is defined on INiShape — no need to downcast.  The old
                ' `DirectCast(..., BSTriShape)` was a residue from the pre-refactor path
                ' and fails for NiTriShape family.
                Dim dNiSkinInst = sliderSet.NIFContent.GetBlock(Of NiSkinInstance)(
                    donor.RelatedNifShape.SkinInstanceRef)
                If dNiSkinInst Is Nothing Then Continue For
                Dim dNiSkinData = sliderSet.NIFContent.GetBlock(dNiSkinInst.Data)
                Dim dBoneIdxList = dNiSkinInst.Bones.Indices.ToList()
                For localIdx = 0 To dBoneIdxList.Count - 1
                    Dim nifBlockIdx = dBoneIdxList(localIdx)
                    Dim mergedIdx = niMergedBoneNifIndices.IndexOf(nifBlockIdx)
                    If mergedIdx = -1 Then
                        mergedIdx = niMergedBoneNifIndices.Count
                        niMergedBoneNifIndices.Add(nifBlockIdx)
                        ' Donor-unique bone: capture the donor's own bind (SkinTransform +
                        ' BoundingSphere) so niMergedBind stays index-aligned.  Under the merge's
                        ' same-skin-space assumption this equals OS's recomputed skin-to-bone.
                        ' Fall back to a default (identity) when the donor carries no BoneData.
                        Dim donorBind As New BoneData()
                        If dNiSkinData IsNot Nothing AndAlso localIdx < dNiSkinData.BoneList.Count Then
                            Dim src = dNiSkinData.BoneList(localIdx)
                            donorBind.SkinTransform = src.SkinTransform
                            donorBind.BoundingSphere = src.BoundingSphere
                        End If
                        niMergedBind.Add(donorBind)
                    End If
                    niRemap(localIdx) = mergedIdx
                Next
            Next
        End If

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
        ' NiSkinData/NiSkinPartition (NiTri).  The donor's inline indices reference the
        ' DONOR palette; ShapeArrays.Append keeps the TARGET palette (SkinningHelper.vb:1302),
        ' so the indices MUST be remapped to the merged palette here, BEFORE Append, for both
        ' skin families.  FO4 uses donorBoneRemaps (step 1); SSE uses niDonorBoneRemaps (1b).
        ' The two are mutually exclusive per shape (BSSkin_Instance XOR NiSkinInstance), so the
        ' FO4 remap takes precedence and the SSE remap fills in only when FO4 produced none.
        For di = 0 To donorShapes.Count - 1
            Dim donorArrays = SkinningHelper.SnapshotSeparateArrays(donorShapes(di).IR_Geometry)
            Dim effectiveRemap = donorBoneRemaps(di)
            If effectiveRemap.Count = 0 AndAlso niDonorBoneRemaps IsNot Nothing Then effectiveRemap = niDonorBoneRemaps(di)
            If donorArrays.Skinning.HasValue AndAlso effectiveRemap.Count > 0 Then
                Dim sk = donorArrays.Skinning.Value
                If sk.BoneIndices IsNot Nothing Then
                    ' Clone the BoneIndices array before mutating — Skinning is a struct
                    ' but its array field is a shared reference.
                    Dim cloned(sk.BoneIndices.Length - 1) As Byte
                    Array.Copy(sk.BoneIndices, cloned, sk.BoneIndices.Length)
                    Dim value As Integer = Nothing
                    For b = 0 To cloned.Length - 1
                        Dim orig As Integer = CInt(cloned(b))
                        If effectiveRemap.TryGetValue(orig, value) Then cloned(b) = CByte(value And &HFF)
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
        MergeMetadataAfterApply(targetGeom, donorShapes)

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

        ' ── 5b. Reconcile NiSkinInstance + NiSkinData STRUCTURE for SSE shapes ───
        ' BSSkin_Instance (FO4) was handled above.  For NiSkinInstance (SSE/Oblivion):
        ' the per-vertex NiSkinData.BoneList[].VertexWeights are NO LONGER authored here.
        ' They were already written ONCE, completely and correctly for ALL merged vertices
        ' (target + donors), by BSTriShapeGeometry.SetSkinning → RebuildNiSkinData during the
        ' ApplyShapeGeometry call above (step "Apply all geometry via centralized helper"):
        ' that call passes the FULL merged per-vertex skinning whose donor bone indices were
        ' already remapped to the merged palette in step 2, and RebuildNiSkinData re-inits and
        ' repopulates every bone's VertexWeights from it.  Re-appending donor weights here would
        ' DOUBLE-WRITE them (each donor vertex listed twice, weights summing to 2.0).
        '
        ' What RebuildNiSkinData does NOT touch — and what step 5b therefore still reconciles —
        ' is the bone PALETTE / BoneList structure: the NiSkinInstance.Bones block-ref array and
        ' growing BoneList so its Count matches that palette (bones with zero weights included).
        ' The merged palette (niMergedBoneNifIndices) was computed ONCE in step 1b — the same
        ' palette the inline per-vertex remap in step 2 (and hence RebuildNiSkinData) used — so
        ' both representations stay in sync.
        If niSkinInst IsNot Nothing Then
            ' Write merged bone list back to NiSkinInstance (palette block-refs; RebuildNiSkinData
            ' does NOT write this).
            niSkinInst.Bones.SetIndices(niMergedBoneNifIndices)
            niSkinInst.NumBones = CUInt(niMergedBoneNifIndices.Count)

            Dim niSkinData = sliderSet.NIFContent.GetBlock(niSkinInst.Data)
            If niSkinData IsNot Nothing Then
                ' Grow BoneList to the merged palette count so BoneList.Count matches the Bones
                ' palette even for bones with no weights.  Donor-unique bone BINDS are copied
                ' from the donor below (mergedBind, built in step 1b) — mirroring the FO4
                ' mergedBoneTrans path: the per-bone SkinTransform is the skin-to-bone bind that
                ' both WM's renderer (NifRenderableShape.vb → New Transform_Class(bon)) and the
                ' in-game engine read, so a default identity here deformed donor-unique-weighted
                ' vertices wrongly.  Under the merge's same-skin-space assumption (donor vertex
                ' positions concatenated into the target buffer, shared bones reuse the target
                ' bind) the donor's stored skin-to-bone equals OS's recomputed value, so COPYING
                ' it is correct.
                Do While niSkinData.BoneList.Count < niMergedBoneNifIndices.Count
                    niSkinData.BoneList.Add(New BoneData())
                Loop
                niSkinData.NumBones = CUInt(niSkinData.BoneList.Count)

                ' Init only still-Nothing VertexWeights lists (BoneData is a struct).  Lists
                ' RebuildNiSkinData already populated are left intact; this only covers
                ' palette-padding bones added above that carry no weights.
                For b = 0 To niSkinData.BoneList.Count - 1
                    Dim bd = niSkinData.BoneList(b)
                    If bd.VertexWeights Is Nothing Then
                        bd.VertexWeights = New List(Of BoneVertData)()
                        niSkinData.BoneList(b) = bd
                    End If
                Next

                ' Write the merged per-bone bind into BoneList: SkinTransform + BoundingSphere
                ' only.  Idempotent for shared bones (mergedBind was seeded from the target's
                ' own BoneList); replaces the identity bind with the donor's real bind for
                ' donor-unique bones.  VertexWeights/NumVertices are left untouched — those are
                ' authored by RebuildNiSkinData and recomputed just below.
                Dim bindCount As Integer = Math.Min(niSkinData.BoneList.Count,
                                                    If(niMergedBind IsNot Nothing, niMergedBind.Count, 0))
                For b = 0 To bindCount - 1
                    Dim bd = niSkinData.BoneList(b)
                    bd.SkinTransform = niMergedBind(b).SkinTransform
                    bd.BoundingSphere = niMergedBind(b).BoundingSphere
                    niSkinData.BoneList(b) = bd
                Next

                ' Recompute per-bone NumVertices from the now-single-write VertexWeights counts
                ' (idempotent with RebuildNiSkinData's own NumVertices write).
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
    ''' Merge-specific segmentation handling for BSSubIndexTriShape.  Called after
    ''' SkinningHelper.ApplyShapeGeometry has written the merged triangle list (target +
    ''' donors concat).  At entry: targetGeom.Triangles already contains target tris
    ''' followed by donor[0] tris followed by donor[1] tris...  Target's pre-merge
    ''' segmentation is intact in targetGeom (RedistributeSegments preserved it during the
    ''' SetTriangles call inside ApplyShapeGeometry, since donors arrive as cross-shape
    ''' provenance which RedistributeSegments ignores by design).
    '''
    ''' Conservative semantic routing — superset of BS-OS CopyGeo with strict safety.
    ''' OS rejects merges whose source/target segmentation differs at all (CheckMerge at
    ''' OutfitProject.cpp:3917-3943 requires identical SSFFile, identical segs.size,
    ''' identical sub counts, identical userSlotID + material per (si, ssi) position).  When
    ''' OS accepts, every donor sub matches a target sub by structural equality, so
    ''' triParts copied verbatim from donor land in the equivalent target slot.  We
    ''' generalize: per donor sub, look for any target sub with identical
    ''' (UserSlotID, Material, ExtraData).  If found, route donor triangles in that
    ''' partID to the target's matching partID.  If not found, route to 0 (target's first
    ''' parent segment, same fallback as the previous WM behaviour and as OS's default
    ''' partition).
    '''
    ''' Invariant: this routine NEVER adds parents/subs to mergedSnapshot.Info nor mutates
    ''' any existing field.  Only mergedSnapshot.TriParts is updated.  Therefore
    ''' SetSegmentation receives a (Info, TriParts) pair where Info is exactly the target's
    ''' pre-merge segmentation — no risk of OOB in oldToNewPartIDs (every triParts[i] is
    ''' either -1, 0, or a partID that already exists in Info), no risk of
    ''' PerSegmentData.Count desync, no risk of inheriting donor extraData with mismatched
    ''' NumCutOffsets.  When OS would have accepted the merge, we produce the same result
    ''' as OS.  When OS would have rejected, we produce the same result as the prior WM
    ''' behaviour (donor → partition 0) plus best-effort routing for any sub that happens
    ''' to match exactly.
    '''
    ''' BSMeshLODTriShape merge LOD handling is done by the adapter's
    ''' ReorderTrianglesByLODTier during ApplyShapeGeometry — this function only handles
    ''' BSSubIndex segmentation.
    ''' </summary>
    Private Shared Sub MergeMetadataAfterApply(targetGeom As IShapeGeometry,
                                                donorShapes As List(Of Shape_class))
        Dim merged = BSTriShapeGeometry.GetSegmentation(targetGeom)
        If merged.IsEmpty Then Return

        ' Build flat list of target subs with their PartID for O(targetSubs × donorSubs)
        ' structural lookup.  Counts are tiny (<30 subs typical) so no hashing needed —
        ' SequenceEqual on ExtraData is required anyway and would dominate any hash bucket.
        Dim targetSubs As New List(Of BSTriShapeGeometry.NifSubSegmentInfo)()
        For Each parentSeg In merged.Info.Segs
            For Each sub_ In parentSeg.Subs
                targetSubs.Add(sub_)
            Next
        Next

        ' Locate where each donor's triangles begin in the merged geometry.  Mirrors the
        ' concat order in step 3 of Merge: target tris first, then donor[0], then donor[1]...
        Dim triCursor As Integer = targetGeom.TriangleCount
        For di = donorShapes.Count - 1 To 0 Step -1
            triCursor -= donorShapes(di).IR_Geometry.TriangleCount
        Next

        For di = 0 To donorShapes.Count - 1
            Dim donorGeom = donorShapes(di).IR_Geometry
            Dim donorSnap = BSTriShapeGeometry.GetSegmentation(donorGeom)
            Dim donorTriCount As Integer = donorGeom.TriangleCount

            If donorSnap.IsEmpty Then
                ' Donor has no segmentation — its triangles already default to 0 in
                ' merged.TriParts (init loop in GetSegmentation sets -1, SetSegmentation's
                ' guard at BSTriShapeGeometry.vb:698 then leaves triParts[i] at 0).
                triCursor += donorTriCount
                Continue For
            End If

            ' Per-donor remap: donor partID → target partID (or -1 = no match → fallback to 0).
            ' Reset for each donor so cross-donor scope can never leak.
            Dim donorPartIDRemap As New Dictionary(Of Integer, Integer)()

            For Each donorParent In donorSnap.Info.Segs
                For Each donorSub In donorParent.Subs
                    Dim matchPartID As Integer = -1
                    For Each targetSub In targetSubs
                        If targetSub.UserSlotID = donorSub.UserSlotID AndAlso
                           targetSub.Material = donorSub.Material AndAlso
                           ExtraDataEqual(targetSub.ExtraData, donorSub.ExtraData) Then
                            matchPartID = targetSub.PartID
                            Exit For
                        End If
                    Next
                    If matchPartID >= 0 Then
                        donorPartIDRemap(donorSub.PartID) = matchPartID
                    End If
                Next
            Next

            ' Apply remap to donor triangles in mergedSnapshot.TriParts.  Triangles whose
            ' donor partID has no entry in donorPartIDRemap stay at -1 → SetSegmentation
            ' routes them to partition 0 of the target (canonical fallback).
            For j = 0 To donorTriCount - 1
                Dim donorPart = donorSnap.TriParts(j)
                If donorPart >= 0 Then
                    Dim mappedTo As Integer
                    If donorPartIDRemap.TryGetValue(donorPart, mappedTo) Then
                        merged.TriParts(triCursor + j) = mappedTo
                    End If
                    ' else: leave as -1 → SetSegmentation falls back to partition 0
                End If
            Next

            triCursor += donorTriCount
        Next

        BSTriShapeGeometry.SetSegmentation(targetGeom, merged.Info, merged.TriParts)
    End Sub

    ''' <summary>
    ''' Bit-exact comparison of two ExtraData (CutOffsets) lists.  Required for sub-segment
    ''' match: NaN must not equal NaN under IEEE 754, and routing donor triangles into a
    ''' target sub whose CutOffsets differ even by one bit could change in-game cut behaviour.
    ''' Conservative: when in doubt, treat as different and fall back to partition 0.
    ''' </summary>
    Private Shared Function ExtraDataEqual(a As List(Of Single), b As List(Of Single)) As Boolean
        Dim aCount As Integer = If(a Is Nothing, 0, a.Count)
        Dim bCount As Integer = If(b Is Nothing, 0, b.Count)
        If aCount <> bCount Then Return False
        If aCount = 0 Then Return True
        For i = 0 To aCount - 1
            If BitConverter.SingleToInt32Bits(a(i)) <> BitConverter.SingleToInt32Bits(b(i)) Then
                Return False
            End If
        Next
        Return True
    End Function

End Class
