' Version Uploaded of Wardrobe 3.2.0
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs

''' <summary>
''' Splits a shape that has masked vertices into two shapes in-memory:
'''   - Original keeps any triangle that is not fully masked.
'''   - New shape named <target>_Split keeps triangles whose all vertices are masked.
''' Mixed triangles remain on the original shape so the visible cut border does not open a gap.
''' OSD blocks are filtered and index-remapped for each half.
''' All changes stay in memory; call Process_render_Changes(True) afterward.
''' </summary>
Public Class SplitShapeHelper

    Private Shared Function RemapDiffs(sourceDiffs As IEnumerable(Of OSD_DataDiff_Class),
                                       remap As IReadOnlyDictionary(Of Integer, Integer)) As List(Of OSD_DataDiff_Class)
        Return sourceDiffs.
            Where(Function(d) remap.ContainsKey(d.Index)).
            Select(Function(d) New OSD_DataDiff_Class() With {
                .Index = remap(d.Index), .X = d.X, .Y = d.Y, .Z = d.Z
            }).ToList()
    End Function

    Public Shared Function CanSplit(shape As Shape_class) As Boolean
        Return shape IsNot Nothing AndAlso
               shape.MaskedVertices.Count > 0 AndAlso
               shape.RelatedNifShape IsNot Nothing
    End Function

    Public Shared Sub Split(shape As Shape_class, sliderSet As SliderSet_Class)
        ' INiShape (BSTriShape, BSSubIndex, BSDynamic, BSMeshLOD, NiTriShape, BSLOD,
        ' NiTriStrips, BSSegmented).  Polymorphic vertex/skin handling via adapter.
        Dim origNif As INiShape = shape.RelatedNifShape
        If origNif Is Nothing Then Throw New InvalidOperationException("Shape has no NIF data.")

        Dim maskedSet = shape.MaskedVertices
        If maskedSet.Count = 0 Then Throw New InvalidOperationException("Shape has no masked vertices.")

        ' Adapter for the original shape — single source of truth for vertex/skin reads
        ' regardless of BSTriShape vs NiTriBasedGeom layout.  No more BS/NiTri branching
        ' in this file — everything flows through the adapter + ShapeArrays (including
        ' Skinning).
        Dim origGeom As IShapeGeometry = ShapeGeometryFactory.[For](origNif, sliderSet.NIFContent)
        Dim totalVerts As Integer = origGeom.VertexCount
        If totalVerts = 0 Then Throw New InvalidOperationException("Shape has no vertex data.")

        ' 1. Classify triangles using original indices.
        ' Original keeps any triangle that is not fully masked.
        ' Split shape gets only fully masked triangles.
        ' Track per-half the OLD triangle index so the adapter can redistribute Segments
        ' / LOD sizes via TriangleRemap (see SkinningHelper.ApplyShapeGeometry signature).
        Dim origTrisRaw As New List(Of (V0 As Integer, V1 As Integer, V2 As Integer))
        Dim splitTrisRaw As New List(Of (V0 As Integer, V1 As Integer, V2 As Integer))
        Dim origTriOldIdx As New List(Of Integer)
        Dim splitTriOldIdx As New List(Of Integer)
        Dim oldTriCounter As Integer = 0

        For Each t In origGeom.GetTriangles()
            Dim v0 = CInt(t(CUShort(0)))
            Dim v1 = CInt(t(CUShort(1)))
            Dim v2 = CInt(t(CUShort(2)))
            Dim m0 = maskedSet.Contains(v0)
            Dim m1 = maskedSet.Contains(v1)
            Dim m2 = maskedSet.Contains(v2)

            If m0 AndAlso m1 AndAlso m2 Then
                splitTrisRaw.Add((v0, v1, v2))
                splitTriOldIdx.Add(oldTriCounter)
            Else
                origTrisRaw.Add((v0, v1, v2))
                origTriOldIdx.Add(oldTriCounter)
            End If
            oldTriCounter += 1
        Next

        ' 2. Collect vertices actually referenced by surviving triangles.
        Dim usedOrig As New HashSet(Of Integer)()
        For Each t In origTrisRaw
            usedOrig.Add(t.V0)
            usedOrig.Add(t.V1)
            usedOrig.Add(t.V2)
        Next

        Dim usedSplit As New HashSet(Of Integer)()
        For Each t In splitTrisRaw
            usedSplit.Add(t.V0)
            usedSplit.Add(t.V1)
            usedSplit.Add(t.V2)
        Next

        If usedOrig.Count = 0 Then Throw New InvalidOperationException("No triangles remain in the original shape after split.")
        If usedSplit.Count = 0 Then Throw New InvalidOperationException("No triangles remain in the split shape - nothing to split off.")

        ' 3. Build compacted index remappings.
        Dim origRemap As New Dictionary(Of Integer, Integer)(usedOrig.Count)
        Dim oi = 0
        For Each v In usedOrig.OrderBy(Function(x) x)
            origRemap(v) = oi
            oi += 1
        Next

        Dim splitRemap As New Dictionary(Of Integer, Integer)(usedSplit.Count)
        Dim si = 0
        For Each v In usedSplit.OrderBy(Function(x) x)
            splitRemap(v) = si
            si += 1
        Next

        ' 4. Build remapped triangle lists.
        Dim origTris = origTrisRaw.Select(Function(t) New Triangle(
            CUShort(origRemap(t.V0)), CUShort(origRemap(t.V1)), CUShort(origRemap(t.V2)))).ToList()
        Dim splitTris = splitTrisRaw.Select(Function(t) New Triangle(
            CUShort(splitRemap(t.V0)), CUShort(splitRemap(t.V1)), CUShort(splitRemap(t.V2)))).ToList()

        ' 5. Unique split name.
        Dim splitName = sliderSet.Check_Unique_Shapename(shape.Target & "_Split")

        ' 6. Clone NIF shape while original still has full vertex data.  CloneShape_Original
        ' takes INiShape so it works for any supported family (BSTriShape, BSSubIndex,
        ' NiTriShape, BSSegmented, etc.).
        Dim splitNifRaw As INiShape = sliderSet.NIFContent.CloneShape_Original(origNif, splitName, sliderSet.NIFContent)

        ' Deep-copy safety: for BSSubIndexTriShape / BSSegmentedTriShape, NiflySharp's
        ' clone may or may not deep-copy the Segments list (List<BSGeometrySegmentData>
        ' of structs — reference shared unless NiflySharp explicitly clones).  If the
        ' split half shares the original's Segments reference, our post-Apply
        ' RedistributeSegments on split would mutate the original's Segments too →
        ' both halves end up with the split-half's segment layout → dismember broken on
        ' both.  Force a shallow-clone of the Segments list to break the alias.
        Dim splitSubIdx = TryCast(splitNifRaw, BSSubIndexTriShape)
        Dim origSubIdx = TryCast(origNif, BSSubIndexTriShape)
        If splitSubIdx IsNot Nothing AndAlso origSubIdx IsNot Nothing AndAlso
           splitSubIdx.Segments IsNot Nothing AndAlso
           Object.ReferenceEquals(splitSubIdx.Segments, origSubIdx.Segments) Then
            ' Shallow copy of the List (structs are value-copied; SubSegment lists are
            ' the only nested reference — also needs its own copy per segment).
            Dim cloned As New List(Of NiflySharp.Structs.BSGeometrySegmentData)(splitSubIdx.Segments.Count)
            For Each seg In splitSubIdx.Segments
                Dim segCopy = seg   ' struct copy (Flags/StartIndex/NumPrimitives/etc.)
                If seg.SubSegment IsNot Nothing Then
                    segCopy.SubSegment = New List(Of NiflySharp.Structs.BSGeometrySubSegment)(seg.SubSegment)
                End If
                cloned.Add(segCopy)
            Next
            splitSubIdx.Segments = cloned
        End If

        ' Polymorphic split-shape adapter — BSTriShape-family clones into BSTriShape,
        ' NiTriShape-family clones into NiTriShape (NiflySharp's CloneShape preserves the
        ' concrete type).  Reuse the existing origGeom from the entry block.
        Dim splitGeom As IShapeGeometry = If(splitNifRaw Is Nothing, Nothing,
                                              ShapeGeometryFactory.[For](splitNifRaw, sliderSet.NIFContent))

        ' 7. Update vertex data via centralized helper (snapshot -> filter -> apply).
        Dim snap = SkinningHelper.SnapshotSeparateArrays(origGeom)
        Dim origArrays = snap.FilterByIndices(usedOrig)
        Dim splitArrays = snap.FilterByIndices(usedSplit)

        ' Provenance maps for both halves so ApplyShapeGeometry can redistribute Segments
        ' / LOD sizes when the underlying shape is BSSubIndex / BSMeshLOD / BSSegmented.
        Dim origProv = TriangleRemap.SameShape(origTriOldIdx)
        Dim splitProv = TriangleRemap.SameShape(splitTriOldIdx)

        ' Unified apply: ShapeArrays (positions/normals/tangents/uvs/colors + Skinning) +
        ' triangles + provenance.  Works for BSTriShape family AND NiTriShape family —
        ' the adapter's ResizeVertices + per-field setters + SetSkinning handle the
        ' layout differences internally.
        SkinningHelper.ApplyShapeGeometry(origGeom, origTris, origArrays, origProv)
        If splitGeom IsNot Nothing Then
            SkinningHelper.ApplyShapeGeometry(splitGeom, splitTris, splitArrays, splitProv)
        End If

        origGeom.UpdateBounds()
        splitGeom?.UpdateBounds()

        ' 8. Skin partitions — regeneration via NifContent for both halves; handles
        ' BSTriShape and NiTriShape uniformly (both go through UpdateSkinPartitions).
        sliderSet.NIFContent.RemapSkinPartitionTriangles(origNif, origRemap)
        If splitNifRaw IsNot Nothing Then sliderSet.NIFContent.RemapSkinPartitionTriangles(splitNifRaw, splitRemap)
        sliderSet.NIFContent.UpdateSkinPartitions(origNif)
        If splitNifRaw IsNot Nothing Then sliderSet.NIFContent.UpdateSkinPartitions(splitNifRaw)

        ' 9. Register split shape in BaseMaterials.
        ' Create a new RelatedMaterial_Class wrapper (not an alias) so that changing the
        ' material path/reference on the split does not mutate the original and vice versa.
        Dim bm = sliderSet.NIFContent.BaseMaterials
        Dim origNifName = origNif.Name.String
        If bm.ContainsKey(origNifName) AndAlso Not bm.ContainsKey(splitName) Then
            Dim orig = bm(origNifName)
            bm.Add(splitName, New Nifcontent_Class_Manolo.RelatedMaterial_Class With {
                .path = orig.path,
                .material = orig.material
            })
        End If

        ' 10. Add Shape_class entry in OSP XML.
        Dim xml = sliderSet.ParentOSP.xml
        Dim splitShapeNode = CType(xml.CreateElement("Shape"), Xml.XmlElement)
        splitShapeNode.SetAttribute("target", splitName)
        splitShapeNode.AppendChild(xml.CreateTextNode(splitName))
        sliderSet.Nodo.InsertAfter(splitShapeNode, shape.Nodo)
        Dim splitShape As New Shape_class(splitShapeNode, sliderSet)
        splitShape.Datafolder = shape.Datafolder.ToList()
        sliderSet.Shapes.Insert(sliderSet.Shapes.IndexOf(shape) + 1, splitShape)

        ' 11. OSD: external blocks are read-only. Any remap happens on local copies.
        Dim osdFilename = IO.Path.GetFileName(
            sliderSet.SourceFileFullPath).Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)
        Dim osdContent = sliderSet.OSDContent_Local

        For Each slider In sliderSet.Sliders
            Dim origDats = slider.Datas.Where(
                Function(d) d.Target.Equals(shape.Target, StringComparison.OrdinalIgnoreCase)).ToList()
            If origDats.Any(Function(d) d.Islocal) Then
                origDats = origDats.Where(Function(d) d.Islocal).ToList()
            End If
            If origDats.Count = 0 Then Continue For

            Dim allDiff As New List(Of OSD_DataDiff_Class)()

            For Each dat In origDats
                Dim sourceBlocks = If(dat.Islocal,
                    dat.RelatedLocalOSDBlocks.ToList(),
                    dat.RelatedExternalOSDBlocks.ToList())
                Dim sourceDiffs = sourceBlocks.Select(Function(block) block.SnapshotDiffs()).ToList()
                Dim editableBlocks = dat.MaterializeEditableLocalBlocks()

                For Each diffList In sourceDiffs
                    allDiff.AddRange(diffList)
                Next

                For i = 0 To editableBlocks.Count - 1
                    Dim kept = RemapDiffs(sourceDiffs(i), origRemap)
                    editableBlocks(i).DataDiff.Clear()
                    editableBlocks(i).DataDiff.AddRange(kept)
                    editableBlocks(i).RebuildCompactArrays()
                Next
            Next

            Dim splitDiff = RemapDiffs(allDiff, splitRemap)
            If splitDiff.Count = 0 Then Continue For

            Dim splitBlockName = splitName.Replace(":", "_") & slider.Nombre
            Dim splitBlock = osdContent.Blocks.FirstOrDefault(
                Function(b) b.BlockName.Equals(splitBlockName, StringComparison.OrdinalIgnoreCase))
            If splitBlock Is Nothing Then
                splitBlock = New OSD_Block_Class(osdContent) With {.BlockName = splitBlockName}
                osdContent.Blocks.Add(splitBlock)
            End If

            splitBlock.DataDiff.Clear()
            splitBlock.DataDiff.AddRange(splitDiff)
            splitBlock.RebuildCompactArrays()

            Dim splitDat = slider.Datas.FirstOrDefault(
                Function(d) d.Target.Equals(splitName, StringComparison.OrdinalIgnoreCase) AndAlso
                            d.Nombre.Equals(splitBlockName, StringComparison.OrdinalIgnoreCase))
            If splitDat Is Nothing Then
                slider.Datas.Add(New Slider_Data_class(splitBlockName, slider, splitName, osdFilename))
            Else
                splitDat.Islocal = True
                splitDat.TargetOsd = osdFilename
            End If
        Next

        ' 12. Clear mask on original and rebuild caches.
        shape.MaskedVertices.Clear()
        sliderSet.InvalidateAllLookupCaches()
    End Sub

End Class
