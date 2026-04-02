' Version Uploaded of Wardrobe 2.1.3
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
        Dim origNif = shape.RelatedNifShape
        If origNif Is Nothing Then Throw New InvalidOperationException("Shape has no NIF data.")

        Dim maskedSet = shape.MaskedVertices
        If maskedSet.Count = 0 Then Throw New InvalidOperationException("Shape has no masked vertices.")

        Dim isSSE = origNif.VertexDataSSE IsNot Nothing AndAlso origNif.VertexDataSSE.Count > 0
        Dim totalVerts = If(isSSE, origNif.VertexDataSSE.Count, origNif.VertexData.Count)
        If totalVerts = 0 Then Throw New InvalidOperationException("Shape has no vertex data.")

        ' 1. Classify triangles using original indices.
        ' Original keeps any triangle that is not fully masked.
        ' Split shape gets only fully masked triangles.
        Dim origTrisRaw As New List(Of (V0 As Integer, V1 As Integer, V2 As Integer))
        Dim splitTrisRaw As New List(Of (V0 As Integer, V1 As Integer, V2 As Integer))

        For Each t In origNif.Triangles
            Dim v0 = CInt(t(CUShort(0)))
            Dim v1 = CInt(t(CUShort(1)))
            Dim v2 = CInt(t(CUShort(2)))
            Dim m0 = maskedSet.Contains(v0)
            Dim m1 = maskedSet.Contains(v1)
            Dim m2 = maskedSet.Contains(v2)

            If m0 AndAlso m1 AndAlso m2 Then
                splitTrisRaw.Add((v0, v1, v2))
            Else
                origTrisRaw.Add((v0, v1, v2))
            End If
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
        Dim version = sliderSet.NIFContent.Header.Version

        ' 6. Clone NIF shape while original still has full vertex data.
        Dim splitNifRaw = sliderSet.NIFContent.CloneShape_Original(origNif, splitName, sliderSet.NIFContent)
        Dim splitBST = TryCast(splitNifRaw, BSTriShape)

        ' 7. Update vertex data via centralized helper (snapshot -> filter -> apply).
        Dim snap = SkinningHelper.SnapshotSeparateArrays(origNif)
        Dim origArrays = snap.FilterByIndices(usedOrig)
        Dim splitArrays = snap.FilterByIndices(usedSplit)

        If isSSE Then
            Dim all = origNif.VertexDataSSE.ToList()
            SkinningHelper.ApplyShapeGeometry(origNif, version, True,
                all.Where(Function(v, i) usedOrig.Contains(i)).ToList(), Nothing, origTris, origArrays)
            SkinningHelper.ApplyShapeGeometry(splitBST, version, True,
                all.Where(Function(v, i) usedSplit.Contains(i)).ToList(), Nothing, splitTris, splitArrays)
        Else
            Dim all = origNif.VertexData.ToList()
            SkinningHelper.ApplyShapeGeometry(origNif, version, False,
                Nothing, all.Where(Function(v, i) usedOrig.Contains(i)).ToList(), origTris, origArrays)
            SkinningHelper.ApplyShapeGeometry(splitBST, version, False,
                Nothing, all.Where(Function(v, i) usedSplit.Contains(i)).ToList(), splitTris, splitArrays)
        End If

        origNif.UpdateBounds()
        splitNifRaw?.UpdateBounds()

        ' 8. Skin partitions.
        sliderSet.NIFContent.RemapSkinPartitionTriangles(origNif, origRemap)
        If splitBST IsNot Nothing Then sliderSet.NIFContent.RemapSkinPartitionTriangles(splitBST, splitRemap)
        sliderSet.NIFContent.UpdateSkinPartitions(origNif)
        If splitBST IsNot Nothing Then sliderSet.NIFContent.UpdateSkinPartitions(splitBST)

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
