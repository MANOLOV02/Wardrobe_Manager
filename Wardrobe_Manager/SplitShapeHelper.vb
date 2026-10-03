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

    Public Shared Function CanSplit(shape As Shape_class) As Boolean
        Return shape IsNot Nothing AndAlso
               shape.MaskedVertices.Count > 0 AndAlso
               shape.RelatedNifShape IsNot Nothing
    End Function

    ''' <returns>Cuantos bloques de animacion de la shape NO se copiaron a la mitad nueva porque su controller es de
    ''' otro objeto (el llamador lo informa).</returns>
    Public Shared Function Split(shape As Shape_class, sliderSet As SliderSet_Class) As Integer
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

        ' 3b. Los MISMOS dos mapas, en la forma que pide el re-mapeo de LOCKEDNORM: un `Integer()`
        ' dimensionado sobre el espacio VIEJO completo, todo en −1 y sólo los sobrevivientes con su
        ' índice nuevo. Es el `indexCollapse` del canónico (nifly NifFile.cpp:4328-4353) y la misma forma
        ' que produce `MorphingHelper.RemoveZaps` para el zap del build.
        ' ⛔ POR QUÉ ESTÁ ACÁ: partir una shape COMPACTA el espacio de vértices igual que un zap, y la
        ' lista LOCKEDNORM quedaba apuntando a los números viejos — o sea a OTROS vértices. Medido sobre
        ' `FemaleHead` (11.498 vértices, 83 bloqueados): la mitad original quedaba con 21 de 83 índices
        ' fuera de rango y 23 apuntando a otro vértice, y la mitad nueva —que es un CLON, así que se lleva
        ' el extra data entero— con los 83 fuera de rango. Consumidor: skee64, que saltea esos vértices al
        ' recalcular normales después de un body morph.
        ' ALCANCE medido: 118 shapes en 114 NIF de SSE, 135 shapes en 105 sliderSets. 0 en FO4.
        Dim viejoCount As Integer = origGeom.VertexCount
        Dim origCollapse(viejoCount - 1) As Integer
        Dim splitCollapse(viejoCount - 1) As Integer
        For i = 0 To viejoCount - 1
            origCollapse(i) = -1
            splitCollapse(i) = -1
        Next
        For Each kv In origRemap
            If kv.Key >= 0 AndAlso kv.Key < viejoCount Then origCollapse(kv.Key) = kv.Value
        Next
        For Each kv In splitRemap
            If kv.Key >= 0 AndAlso kv.Key < viejoCount Then splitCollapse(kv.Key) = kv.Value
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
        ' NiTriShape, BSSegmented, etc.).  NiflySharp's BSSubIndexTriShape copy constructor
        ' (BSSubIndexTriShape.g.cs) deep-clones both _segment (ConvertAll +
        ' BSGeometrySegmentData.DeepClone) and _segmentData (BSGeometrySegmentSharedData
        ' .DeepClone) — split half ends up with structurally independent segmentation, no
        ' alias breaking required from app side.
        ' Clon del bloque + su ANIMACION (ControlledBlocks por ref/nombre, ObjectPalette), con reversion si falla:
        ' sede unica en la libreria, compartida con Clone. Va ANTES de tocar la geometria: si la copia de animacion
        ' falla, el NIF queda como estaba y el original sin compactar.
        Dim clonado = sliderSet.NIFContent.ClonarShapeConAnimacion(origNif, splitName)
        Dim splitNifRaw As INiShape = clonado.Clon

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

        ' 7b. LOCKEDNORM al espacio nuevo, en LAS DOS MITADES. Va acá —después de escribir la geometría—
        ' por la misma razón que en el zap del build: el extra data pertenece al bloque de la shape, no a
        ' los arrays, así que sobrevive intacto a `ApplyShapeGeometry` y hay que renumerarlo aparte.
        origGeom.RemapLockedNormalIndices(origCollapse)
        splitGeom?.RemapLockedNormalIndices(splitCollapse)

        origGeom.UpdateBounds()
        splitGeom?.UpdateBounds()

        ' 8. Skin partitions — regeneration via NifContent for both halves; handles
        ' BSTriShape and NiTriShape uniformly (both go through UpdateSkinPartitions).
        sliderSet.NIFContent.RemapSkinPartitionTriangles(origNif, origRemap)
        If splitNifRaw IsNot Nothing Then sliderSet.NIFContent.RemapSkinPartitionTriangles(splitNifRaw, splitRemap)
        sliderSet.NIFContent.UpdateSkinPartitions(origNif)
        If splitNifRaw IsNot Nothing Then sliderSet.NIFContent.UpdateSkinPartitions(splitNifRaw)

        ' 9-10. Material propio (NO alias), nodo <Shape> y Shape_class: nucleo compartido con Clone.
        ShapeDuplicacionNucleo.RegistrarMaterial(sliderSet, origNif.Name.String, splitName)
        ShapeDuplicacionNucleo.AgregarShapeAlProyecto(shape, sliderSet, splitName, heredarDataFolder:=True)

        ' 11. OSD: los bloques externos son de solo lectura; todo remapeo va a copias locales. La mitad ORIGINAL se
        ' reescribe con su remapeo (exclusivo de Split); la nueva recibe el suyo por el nucleo compartido.
        For Each slider In sliderSet.Sliders
            Dim fuente = ShapeDuplicacionNucleo.DatosDeLaShape(slider, shape)
            If fuente.Datos.Count = 0 Then Continue For

            Dim allDiff As New List(Of OSD_DataDiff_Class)()
            For d = 0 To fuente.Datos.Count - 1
                Dim sourceDiffs = fuente.Diffs(d)
                For Each diffList In sourceDiffs
                    allDiff.AddRange(diffList)
                Next
                Dim editableBlocks = fuente.Datos(d).MaterializeEditableLocalBlocks()
                For i = 0 To editableBlocks.Count - 1
                    Dim kept = ShapeDuplicacionNucleo.RemapDiffs(sourceDiffs(i), origRemap)
                    editableBlocks(i).DataDiff.Clear()
                    editableBlocks(i).DataDiff.AddRange(kept)
                    editableBlocks(i).RebuildCompactArrays()
                Next
            Next

            ShapeDuplicacionNucleo.EscribirMorphsDelNuevo(sliderSet, slider, splitName,
                                                          ShapeDuplicacionNucleo.RemapDiffs(allDiff, splitRemap))
        Next

        ' 12. Clear mask on original and rebuild caches.
        shape.MaskedVertices.Clear()
        sliderSet.InvalidateAllLookupCaches()
        Return clonado.Ajenos
    End Function

End Class
