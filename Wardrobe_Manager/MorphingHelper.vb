' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Imports FO4_Base_Library
Imports FO4_Base_Library.RecalcTBN

''' <summary>Qué es un slider a la hora de aplicarlo. `Clamp` NO es un tipo: un clamp recorre el loop
''' normal como morph (o como zap, si además lo es) y ADEMÁS recibe el segundo pase.</summary>
Public Enum SliderKind
    Morph = 0
    Zap = 1
    UvMorph = 2
End Enum

Public Class MorphingHelper

    ''' <summary>
    ''' ÚNICA fuente de verdad para "qué es este slider y con qué peso se aplica". La usan el BAKE
    ''' (<see cref="ApplyMorph_CPU"/>) y el RENDER (<see cref="SliderMorphResolver"/>), así que
    ''' RENDER == BAKE queda garantizado por construcción y no por dos copias que hay que mantener.
    '''
    ''' Ley tomada de BodySlideApp::BuildListBodies (el BATCH build, :4342-4400), que es el camino que
    ''' arma un pack. ⚠️ BodySlide es internamente inconsistente: su otro camino
    ''' (BuildBodies → ApplySliders:1327) NO invierte los zaps. Manda el batch.
    '''   1. `invert` se aplica SIEMPRE, antes de decidir el tipo (:4367-4371).
    '''   2. zap sólo si `bZap &amp;&amp; !bUV` (:4373); si es zap+uv, es un morph UV.
    '''   3. `bClamp` NO saca al slider del loop — sigue por ApplyDiff (:4392) y el clamp se aplica
    '''      después, en un segundo pase (:4402-4413).
    '''   4. El peso NO se clampea a [0,1]: `GetBigPresetValue` (SliderManager.cpp:232-238) devuelve el
    '''      valor verbatim y el editor de WM habilita valores extremos a propósito
    '''      (Editor_Form: AllowExtremeValues = True). Sólo se normaliza NaN.
    ''' </summary>
    Public Shared Function ResolveSlider(isZap As Boolean, isUV As Boolean, invert As Boolean,
                                         rawSetting As Single) As (Kind As SliderKind, Weight As Single)
        Dim t As Single = rawSetting / 100.0F
        If Single.IsNaN(t) Then t = 0.0F
        If invert Then t = 1.0F - t
        If isZap AndAlso Not isUV Then Return (SliderKind.Zap, t)
        If isUV Then Return (SliderKind.UvMorph, t)
        Return (SliderKind.Morph, t)
    End Function

    ''' <summary>
    ''' Overload sobre un slider real: elige el valor CRUDO correcto antes de aplicar la ley.
    ''' Un ZAP usa <see cref="Slider_class.Zap_Setting_Big"/> (BodySlide decide los zaps solo con
    ''' `vbig` y borra el mismo conjunto en los dos pesos); todo lo demas usa el valor vivo.
    ''' La usan el BAKE y el RENDER, asi que no pueden divergir.
    ''' </summary>
    Public Shared Function ResolveSlider(s As Slider_class) As (Kind As SliderKind, Weight As Single)
        Dim isZap = s.IsZap AndAlso Not s.IsUV
        Return ResolveSlider(s.IsZap, s.IsUV, s.Invert, If(isZap, s.Zap_Setting_Big, s.Current_Setting))
    End Function

    Friend Shared Sub LoadMorphTargets(shape As Shape_class, ByRef Geometry As SkinnedGeometry)
        ' C-3: Skip rebuild if morph data is already cached (invalidated via InvalidateShapeDataLookupCache)
        If shape.MorphDiffs IsNot Nothing Then Exit Sub

        ' 1) Inicializar el diccionario
        ' OrdinalIgnoreCase: las claves se agrupan mas abajo con StringComparer.OrdinalIgnoreCase, asi
        ' que con un dict Ordinal dos sliders "Belly"/"belly" dejaban la clave del primero y el
        ' indexador de ApplyMorph_CPU tiraba KeyNotFoundException adentro del Parallel.ForEach.
        shape.MorphDiffs = New Dictionary(Of String, List(Of MorphData))(StringComparer.OrdinalIgnoreCase)
        ' 2) Número de vértices en el mesh base
        Dim count = Geometry.BaseVertices.Length
        ' 3) Para cada elemento de Related_Slider_data (uno por slider aplicado a esta shape)
        For Each sd In shape.Related_Slider_data.
            GroupBy(Function(pf) pf.ParentSlider.Nombre, StringComparer.OrdinalIgnoreCase).
            Select(Function(g) g.OrderByDescending(Function(pf) pf.Islocal).First()).
            ToList()
            Dim sliderName = sd.ParentSlider.Nombre
            Dim lista As New List(Of MorphData)
            shape.MorphDiffs.Add(sliderName, lista)
            ' 5) Cada bloque OSD aporta DataDiff con (Index, X,Y,Z)
            ' D-2: Use compact arrays when available for cache-friendly iteration
            For Each block As OSD_Block_Class In sd.RelatedOSDBlocks
                If block.IndicesCompact IsNot Nothing AndAlso block.IndicesCompact.Length = block.DataDiff.Count Then
                    Dim idx = block.IndicesCompact
                    Dim dlt = block.DeltasCompact
                    For j = 0 To idx.Length - 1
                        If idx(j) >= 0 AndAlso idx(j) < count Then
                            lista.Add(New MorphData With {.index = CUInt(idx(j)), .PosDiff = New Vector3(dlt(j * 3), dlt(j * 3 + 1), dlt(j * 3 + 2))})
                        End If
                    Next
                Else
                    For Each d As OSD_DataDiff_Class In block.DataDiff
                        Dim i = CInt(d.Index)
                        If i >= 0 AndAlso i < count Then
                            lista.Add(New MorphData With {.index = CUInt(i), .PosDiff = New Vector3(d.X, d.Y, d.Z)})
                        End If
                    Next
                End If
            Next
        Next
    End Sub
    ''' <param name="buildSize">
    ''' Peso que se está construyendo. Sólo lo usa el gate del segundo pase de CLAMP, que en BodySlide
    ''' va contra el DEFAULT crudo del slider para ese peso (`defBigValue`/`defSmallValue`,
    ''' BodySlideApp.cpp:4406/:4410), NO contra el valor vivo — un preset puede pisar el valor de un
    ''' clamp (GetBigPresetValue:4349 corre para todos), pero el clamp se aplica igual según su default.
    ''' Default Big: es lo que emite BodySlide cuando no hay GenWeights.
    ''' </param>
    Public Shared Sub ApplyMorph_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, RecalculateNormals As Boolean, AllowMask As Boolean,
                                     Optional buildSize As WM_Config.SliderSize = WM_Config.SliderSize.Big)
        Dim count = Geometry.NifLocalVertices.Length
        ' Start from NIF local space (pre-skinning) so deltas are applied in the correct space
        Dim verts = Geometry.NifLocalVertices.ToArray()

        LoadMorphTargets(shape, Geometry)
        ' Las UVs parten SIEMPRE de la base del NIF, igual que las posiciones parten de
        ' NifLocalVertices: ApplyUVDiff ACUMULA, asi que sin este reset construir dos veces la misma
        ' geom (los dos pesos, o preview + build) sumaria el slider uv dos veces.
        ' El reset informa si REALMENTE cambio el array: eso, y no el flag UvsMorphed, es lo que dice
        ' si hay que recalcular la base tangente. Mirar el flag daba falsos positivos cuando las UVs
        ' ya estaban en la base.
        Dim uvTocados As New HashSet(Of Integer)()
        Dim resetCambioUVs As Boolean = MorphEngine.ResetUvsFromBase(Geometry, uvTocados)
        ' Reiniciar máscara y dirty-tracking
        ApplyMask_CPU(shape, Geometry, AllowMask)
        Geometry.dirtyVertexIndices.Clear()

        ' .ToList() a proposito: Related_Sliders es un query DIFERIDO (SelectMany + Where + GroupBy)
        ' sobre las listas que cuelgan del XmlDocument compartido del .osp, y aca se recorre DOS veces
        ' (el loop de morphs y el 2do pase de clamps). Sin materializar, la segunda pasada re-ejecuta
        ' el query entero desde adentro del Parallel.ForEach por shape del build.
        Dim sliders = shape.Related_Sliders.ToList()
        Dim movioUVs As Boolean = False

        ' Tipo y peso salen de MorphingHelper.ResolveSlider — la MISMA función que usa el render.
        ' Un CLAMP no sale del loop: recorre esto como morph (o como zap, si además lo es) y recibe
        ' ADEMÁS el segundo pase de abajo, igual que BuildListBodies.
        For Each s In sliders
            Dim k = ResolveSlider(s)
            Dim t = k.Weight

            Dim deltas As List(Of MorphData) = Nothing
            ' TryGetValue, no el indexador: un MorphDiffs cacheado de un estado anterior sin este
            ' slider tiraba KeyNotFoundException DENTRO del Parallel.ForEach y se llevaba puesto el
            ' proyecto entero, mientras el render (que ya usaba TryGetValue) lo omitía en silencio.
            If Not shape.MorphDiffs.TryGetValue(s.Nombre, deltas) Then Continue For

            Select Case k.Kind
                Case SliderKind.Zap
                    ' Sólo cambia máscara. Un zap en 0 no aporta NADA: ZapIdx es una UNIÓN que nunca
                    ' se limpia, así que escribir -0.0F igual PISA el negativo de otro zap solapado.
                    If t > 0.0F Then
                        For Each morph In deltas
                            ' GetDiffIndices usa `fabs(componente) > threshold` con threshold=0 por
                            ' default (DiffData.h:84): un vértice con delta todo-cero NO se zapea.
                            Dim d = morph.PosDiff
                            If d.X = 0.0F AndAlso d.Y = 0.0F AndAlso d.Z = 0.0F Then Continue For
                            Dim i = CInt(morph.index)
                            If shape.ApplyZaps = True Then Geometry.VertexMask(i) = -t
                            Geometry.dirtyMaskIndices.Add(i)
                            Geometry.dirtyMaskFlags(i) = True
                        Next
                    End If

                Case SliderKind.UvMorph
                    ' Un slider UV mueve UVs, NUNCA vértices. DiffDataSets::ApplyUVDiff
                    ' (DiffData.cpp:458-487) ACUMULA sobre el array de uvs:
                    '     uvs[i].u += diff.x * percent ; uvs[i].v += diff.y * percent
                    ' con `percent == 0` como único early-out. Uvs_Weight empaqueta (U, V, peso del
                    ' primer hueso): la Z NO se toca. InjectToTrishape escribe las UVs desde este
                    ' mismo array (SkinningHelper.vb:779), así que morphearlo acá sale al NIF.
                    ' Antes WM no tenía canal UV en el build: sus deltas se sumaban a POSICIONES
                    ' (deformando la malla) y las UVs salían sin morphear.
                    ' Medido: 165 sliders uv en 41 proyectos, incluido CBBE.osp.
                    If t <> 0.0F AndAlso Geometry.Uvs_Weight IsNot Nothing Then
                        Dim uvCount = Geometry.Uvs_Weight.Length
                        For Each morph In deltas
                            Dim iu = CInt(morph.index)
                            If iu < 0 OrElse iu >= uvCount Then Continue For
                            Dim cur = Geometry.Uvs_Weight(iu)
                            Geometry.Uvs_Weight(iu) = New Vector3(cur.X + morph.PosDiff.X * t,
                                                                  cur.Y + morph.PosDiff.Y * t,
                                                                  cur.Z)
                            uvTocados.Add(iu)
                            movioUVs = True
                        Next
                    End If

                Case Else
                    ' Morph normal. SIN umbral de magnitud: DiffDataSets::ApplyDiff (DiffData.cpp:489)
                    ' suma cada entrada sin mirar el tamaño; su único early-out es `percent == 0`.
                    If t <> 0.0F Then
                        For Each morph In deltas
                            Dim i = CInt(morph.index)
                            verts(i) = verts(i) + morph.PosDiff * t
                        Next
                    End If
            End Select
        Next

        ' SEGUNDO PASE — CLAMP, después de TODOS los morphs (BodySlideApp.cpp:4402-4413).
        ' ApplyClamp hace ASIGNACIÓN ABSOLUTA (DiffData.cpp:533-535): `verts[i] = diff`, sin sumar y sin
        ' escalar por el valor del slider.
        '
        ' El gate va contra el DEFAULT del slider para este peso, ya pasado por el pre-pase de
        ' zapToggles (EffectiveDefault: el canonico compara el defSmallValue MUTADO) (`defBigValue > 0` /
        ' `defSmallValue > 0`, :4406/:4410), NO contra el valor vivo: un preset puede pisar el valor de
        ' un clamp, pero el pase de clamp corre igual según su default.
        For Each s In sliders
            If Not s.IsClamp Then Continue For
            Dim cv = s.EffectiveDefault(buildSize)
            If Single.IsNaN(cv) OrElse cv <= 0.0F Then Continue For
            Dim clampDeltas As List(Of MorphData) = Nothing
            If Not shape.MorphDiffs.TryGetValue(s.Nombre, clampDeltas) Then Continue For
            For Each cd In clampDeltas
                Dim i = CInt(cd.index)
                If i >= 0 AndAlso i < count Then verts(i) = cd.PosDiff
            Next
        Next

        ' GPU skinning: morphed verts stay in local space — GPU will transform them
        ' (ApplySkinningToLocalVerts removed — no longer needed)

        For i = 0 To count - 1
            If Geometry.Vertices(i) <> verts(i) Then
                Geometry.dirtyVertexIndices.Add(i)
                Geometry.dirtyVertexFlags(i) = True
            Else
                Geometry.dirtyVertexFlags(i) = False
            End If
        Next
        ' O2.3: If dirty count exceeds 60% of vertex count, mark all dirty (full update is cheaper than sparse HashSet lookups)
        If Geometry.dirtyVertexIndices.Count > count * 0.6 Then
            Geometry.dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, count))
            For i = 0 To count - 1
                Geometry.dirtyVertexFlags(i) = True
            Next
        End If
        Geometry.Vertices = verts
        ' El cache de TBN guarda las DERIVADAS UV por triangulo (RecalcTBN.BuildTBNCache), asi que un
        ' slider uv lo invalida. ⛔ NO alcanzaba con hacerlo en el render: ExtractSkinnedGeometry ya
        ' construye el cache cuando la shape no trae normales o tangentes en el NIF fuente
        ' (SkinningHelper: `If RecalculateNormals OrElse Not HasNormals OrElse Not HasTangents`), y
        ' ese cache es PRE-morph. Sin esto, un proyecto con slider uv sobre una shape sin tangentes
        ' horneaba las tangentes contra las UVs sin morphear — RENDER == BAKE roto del lado del bake.
        ' ⭐ Se refrescan SOLO las derivadas UV de los triangulos tocados en vez de tirar el cache
        ' entero: la adjacencia (VertexToTriangles) depende de los INDICES, que un slider uv no mueve,
        ' y rehacerla es la parte cara.
        If movioUVs OrElse resetCambioUVs Then RecalcTBN.RefreshUvDerivatives(Geometry, uvTocados)
        ' Invalidate world-space cache since local positions changed
        Geometry.WorldCacheValid = False
        Geometry.CachedWorldVertices = Nothing
        Geometry.CachedWorldNormals = Nothing
        ' ⭐ La base tangente depende de las UVs, asi que un slider uv la invalida — pero NO mueve un
        ' solo vertice, con lo que dirtyVertexIndices queda vacio y el recalculo de abajo no corria
        ' NUNCA: se tiraba el cache y no habia quien lo reconstruyera.
        ' Canonico: CalcTangentsForShape corre INCONDICIONAL en la fase 3 del build
        ' (BodySlideApp.cpp:4501 y :4529), fuera de todo gate de vertices; lo unico gateado ahi son
        ' las NORMALES (por lockNormals, :4494). Por eso, cuando el unico cambio son UVs, se fuerza
        ' el recalculo y despues se RESTAURAN las normales: el efecto neto es "solo tangentes",
        ' que es exactamente lo que hace el canonico.
        ' Contrato de SkinnedGeometry.UvsMorphed: lo lee MorphEngine.ApplyMorphPlan para saber si tiene
        ' que restaurar aunque el plan ya no traiga canales uv. Un geom que pasara por el bake y
        ' despues por el render llegaba con el flag mintiendo y el render se saltaba el reset.
        Geometry.UvsMorphed = movioUVs
        ' Si el reset devolvio las UVs a la base, tambien cambiaron: el cache de TBN queda viejo igual.
        If resetCambioUVs Then movioUVs = True

        ' Sin triangulos no hay base tangente que recalcular, y BuildTBNCache desreferencia el array
        ' de indices. El recalculo por UV corre en casos donde el de normales nunca corria, asi que
        ' el guard va aca y no en el llamador.
        Dim puedeTBN As Boolean = Geometry.Indices IsNot Nothing AndAlso Geometry.Indices.Length >= 3 AndAlso
                                  Geometry.Uvs_Weight IsNot Nothing AndAlso Geometry.Normals IsNot Nothing
        If movioUVs AndAlso Not puedeTBN Then movioUVs = False

        ' ⭐ `soloTangentes` NO se decide por el ajuste de recalcular normales, sino por si SE MOVIERON
        ' VERTICES. Las normales se derivan de POSICIONES; las UVs no entran en su calculo. El
        ' canonico lo separa igual: `CalcNormalsForShape` (posiciones, gateada por lockNormals) y
        ' `CalcTangentsForShape` (UVs) son dos pases distintos (BodySlideApp.cpp:4494-4501).
        ' ⛔ MEDIDO en un build real (UBE brows, slider uv `Thin` a 100): con el ajuste en True esto
        ' daba False, corria el recalculo COMPLETO y las 501 normales pasaban de AUTORADAS (las del
        ' NIF fuente, que es lo que queda cuando nada esta sucio) a CALCULADAS — un salto de hasta
        ' 0,279 con las posiciones IDENTICAS. Mover un slider uv un 1 % te cambiaba todas las
        ' normales de la malla.
        Dim huboCambioDePosicion As Boolean = Geometry.dirtyVertexIndices.Count > 0
        ' ⛔ La condicion es `Not (pidioNormales AndAlso huboCambioDePosicion)`, NO
        ' `uv AndAlso Not posicion`. Esa version anterior cubria solo el caso UV-PURO: con un slider
        ' uv Y uno de posicion a la vez, `huboCambioDePosicion` daba True, KeepExistingNormals caia a
        ' False y las normales se recalculaban AUNQUE el ajuste estuviera apagado — o sea que el uv
        ' reactivaba por la ventana un recalculo que el usuario habia desactivado.
        ' Las normales se recomputan si y solo si el usuario lo pidio Y se movio geometria; las UVs
        ' nunca las tocan. Es la separacion del canonico: `if (!lockNormals) CalcNormalsForShape`
        ' (posiciones) y `CalcTangentsForShape` (UVs) son dos pases independientes
        ' (BodySlideApp.cpp:4494-4501).
        Dim soloTangentes As Boolean = Not (RecalculateNormals AndAlso huboCambioDePosicion)
        If movioUVs Then
            ' ⭐ SOLO los vertices cuyas UV se movieron. RecalculateNormalsTangentsBitangents hace la
            ' clausura sola (dirty -> triangulos incidentes -> los 3 vertices de cada uno) y elige
            ' acumuladores SPARSE por debajo del 40 % de los triangulos. Marcar la malla entera
            ' forzaba el camino full y el maximo trabajo posible.
            For Each iv In uvTocados
                If iv >= 0 AndAlso iv < count Then
                    Geometry.dirtyVertexIndices.Add(iv)
                    Geometry.dirtyVertexFlags(iv) = True
                End If
            Next
        End If

        If ((RecalculateNormals AndAlso huboCambioDePosicion) OrElse movioUVs) AndAlso Geometry.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            opt.KeepExistingNormals = soloTangentes
            ' ⚠️ MEDIDO y DESCARTADO (2026-08-03): forzar acá el recálculo de la malla ENTERA en vez de
            ' la clausura de lo sucio NO cambia un solo byte de la salida — con un preset real la
            ' clausura ya cubre 22.658 de 22.708 vértices. Se probó porque parecía explicar la
            ' divergencia de tangentes contra BodySlide, y no la explica. No re-intentarlo: es costo
            ' puro. Ver [[66-paridad-contra-bodyslide-real]].
            ' Devuelve una List, no un HashSet, y puede repetir vertices que ya estaban sucios: los
            ' dos Add de abajo son idempotentes, asi que el ExceptWith que habia aca era optimizacion.
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(Geometry, opt)
            For Each ad In adicionales
                Geometry.dirtyVertexIndices.Add(ad)
                Geometry.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub

    ''' <summary>
    ''' Compacta la geometria quitando los vertices marcados y reindexa triangulos y particiones.
    ''' Devuelve el mapa old-&gt;new (indice viejo -&gt; nuevo, o -1 si el vertice se fue), o Nothing si
    ''' no habia nada que zapear.
    '''
    ''' SOLO toca <paramref name="geom"/> y el bloque de NIF de esta shape, asi que es seguro
    ''' dentro del Parallel.ForEach del build. El reindex de los morphs del OSD vive aparte en
    ''' <see cref="ReindexMorphsAfterZap"/> porque escribe en el XmlDocument del OSP y en las listas
    ''' de bloques del sliderset, ambos COMPARTIDOS entre shapes.
    ''' </summary>
    ''' <param name="keepZappedShapes">
    ''' Se pasa desde el caller SERIAL a proposito: leerlo aca seria
    ''' <c>SliderSet_Class.KeepZappedShapes</c> -&gt; <c>OutputFile</c> -&gt; <c>Nodo.SelectNodes(...)</c>,
    ''' o sea una consulta XPath sobre el XmlDocument compartido dentro del Parallel.ForEach.
    ''' </param>
    Public Shared Function RemoveZaps(shape As Shape_class, ByRef geom As SkinnedGeometry,
                                      keepZappedShapes As Boolean,
                                      ByRef fullyZappedKept As Boolean) As Integer()
        fullyZappedKept = False

        If Not shape.ParentSliderSet.Sliders.Any(Function(pf) pf.IsZap) Then Return Nothing

        ' ==== 0) Datos locales / alias ====
        ' Geometry backing shape (INiShape) — used below for partition operations.
        Dim tri As INiShape = geom.Geometry?.BackingShape
        ' Sin shape de respaldo no se puede remapear la skin partition (paso 3b). Si la geometria
        ' TIENE skinning, compactar igual dejaria el TrianglesCopy en indices pre-compactacion => un
        ' .nif con el skinning corrupto, en silencio. Se sale sin tocar nada: no zapear es
        ' recuperable, escribir una particion desalineada no. Sin skinning no hay nada que remapear.
        If tri Is Nothing AndAlso geom.Skinning.BoneIndices IsNot Nothing Then Return Nothing
        Dim vm = geom.VertexMask
        Dim nOld As Integer = geom.Vertices.Length
        Dim haszapped As Boolean = False

        ' ==== 1) Marcas a eliminar
        Dim removed(nOld - 1) As Boolean
        For i As Integer = 0 To nOld - 1
            removed(i) = (vm(i) < 0)
            haszapped = haszapped Or (vm(i) < 0)
        Next

        If Not haszapped Then Return Nothing

        ' BodySlideApp.cpp:3618 — si la shape quedaria COMPLETAMENTE zapeada y el proyecto pide
        ' conservarla, BodySlide NO le borra un solo vertice: deja la geometria intacta y prende el
        ' flag hidden. WM la vaciaba, dejando una BSTriShape de 0 vertices (malformada). Se sale
        ' antes de tocar nada y el caller estampa el flag.
        Dim survivors As Integer = 0
        For i As Integer = 0 To nOld - 1
            If Not removed(i) Then survivors += 1
        Next
        If survivors = 0 AndAlso keepZappedShapes Then
            fullyZappedKept = True
            Return Nothing
        End If

        ' ==== 2) old->new y compactación in-place de arrays en SkinnedGeometry ====
        Dim oldToNew(nOld - 1) As Integer
        For i As Integer = 0 To nOld - 1 : oldToNew(i) = -1 : Next

        Dim w As Integer = 0

        Dim V = geom.Vertices
        Dim VB = geom.BaseVertices
        Dim N = geom.Normals
        Dim T = geom.Tangents
        Dim B = geom.Bitangents
        Dim UVW = geom.Uvs_Weight
        Dim VC = geom.VertexColors
        Dim ED = geom.Eyedata

        ' Polymorphic per-vertex skinning compaction — parallel slot copy keyed by survivor
        ' position.  Works uniformly for BSTriShape (flat bytes + halfs) and NiTriShape
        ' (same flat layout, sourced from NiSkinPartition or NiSkinData).  The packed
        ' BSVertexData/SSE list is rebuilt by adapter.ResizeVertices in InjectToTrishape
        ' — no need for an in-place struct copy here anymore.
        Dim hasSkin As Boolean = (geom.Skinning.BoneIndices IsNot Nothing AndAlso geom.Skinning.BoneWeights IsNot Nothing AndAlso geom.Skinning.VertexCount = nOld)
        Dim skinWpv As Integer = If(geom.Skinning.WeightsPerVertex > 0, geom.Skinning.WeightsPerVertex, 4)
        Dim skinIdxArr = geom.Skinning.BoneIndices
        Dim skinWgtArr = geom.Skinning.BoneWeights

        For i As Integer = 0 To nOld - 1
            If Not removed(i) Then
                oldToNew(i) = w
                ' copiar structs tal cual (sin new)
                V(w) = V(i)
                VB(w) = VB(i)
                N(w) = N(i)
                T(w) = T(i)
                B(w) = B(i)
                UVW(w) = UVW(i)
                VC(w) = VC(i)
                ED(w) = ED(i)
                If hasSkin Then
                    Dim srcBase As Integer = i * skinWpv
                    Dim dstBase As Integer = w * skinWpv
                    If srcBase <> dstBase Then
                        For j = 0 To skinWpv - 1
                            skinIdxArr(dstBase + j) = skinIdxArr(srcBase + j)
                            skinWgtArr(dstBase + j) = skinWgtArr(srcBase + j)
                        Next
                    End If
                End If
                w += 1
            End If
        Next

        Dim nNew As Integer = w

        ' Redimensionar solo una vez (sin recrear elementos)
        Array.Resize(V, nNew) : geom.Vertices = V
        Array.Resize(VB, nNew) : geom.BaseVertices = VB
        Array.Resize(N, nNew) : geom.Normals = N
        Array.Resize(T, nNew) : geom.Tangents = T
        Array.Resize(B, nNew) : geom.Bitangents = B
        Array.Resize(UVW, nNew) : geom.Uvs_Weight = UVW
        ' BaseUvs_Weight se compacta con el MISMO mapa: es la base a la que vuelve
        ' MorphEngine.ResetUvsFromBase. Dejarla con el largo viejo no tiraba — el guard de largo de
        ' ResetUvsFromBase la re-tomaba en silencio DESDE LAS UVs YA MORPHEADAS Y ZAPEADAS, o sea
        ' que el modo de falla era una doble aplicacion muda del slider uv en la pasada siguiente.
        If geom.BaseUvs_Weight IsNot Nothing AndAlso geom.BaseUvs_Weight.Length >= nOld Then
            Dim BUV = geom.BaseUvs_Weight
            For i = 0 To nOld - 1
                Dim dst = oldToNew(i)
                If dst >= 0 Then BUV(dst) = BUV(i)
            Next
            Array.Resize(BUV, nNew) : geom.BaseUvs_Weight = BUV
        End If
        Array.Resize(VC, nNew) : geom.VertexColors = VC
        Array.Resize(ED, nNew) : geom.Eyedata = ED

        ' Resize compacted skinning to new vertex count.  Published in InjectToTrishape
        ' via adapter.SetSkinning (writes BSVertexData inline for BS, rebuilds NiSkinData
        ' BoneList for NiTri).
        If hasSkin Then
            Dim newIdxArr(nNew * skinWpv - 1) As Byte
            Dim newWgtArr(nNew * skinWpv - 1) As System.Half
            Array.Copy(skinIdxArr, newIdxArr, nNew * skinWpv)
            Array.Copy(skinWgtArr, newWgtArr, nNew * skinWpv)
            geom.Skinning = New ShapeSkinningData() With {
                .BoneIndices = newIdxArr,
                .BoneWeights = newWgtArr,
                .WeightsPerVertex = skinWpv,
                .VertexCount = nNew,
                .BoneRefIndices = geom.Skinning.BoneRefIndices
            }
        End If


        ' ==== 3) Reindexado de triángulos con mínima asignación + tracking de provenance
        ' (oldTriIdx por cada nuevo triángulo) — el adapter usa esto para redistribuir
        ' Segments/LOD sizes en BSSubIndex / BSMeshLOD / BSSegmented.  Para BSTriShape plano
        ' la lista existe pero no se consume.
        Dim idxArr = geom.Indices
        Dim tmpTris(idxArr.Length \ 3 - 1) As Triangle
        Dim provenance As New List(Of Integer)(idxArr.Length \ 3)
        Dim w2 As Integer = 0

        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            Dim oldTriIdx As Integer = tr \ 3
            Dim n1 = oldToNew(CInt(idxArr(tr)))
            Dim n2 = oldToNew(CInt(idxArr(tr + 1)))
            Dim n3 = oldToNew(CInt(idxArr(tr + 2)))
            If n1 >= 0 AndAlso n2 >= 0 AndAlso n3 >= 0 Then
                tmpTris(w2) = New Triangle(n1, n2, n3)
                provenance.Add(oldTriIdx)
                w2 += 1

            End If
        Next

        If w2 < tmpTris.Length Then ReDim Preserve tmpTris(w2 - 1)
        geom.TriangleProvenance = TriangleRemap.SameShape(provenance)

        Dim newIdx(3 * w2 - 1) As UInteger
        For i As Integer = 0 To w2 - 1
            Dim t2 = tmpTris(i)
            Dim base3 = 3 * i
            newIdx(base3) = CUInt(t2.V1)
            newIdx(base3 + 1) = CUInt(t2.V2)
            newIdx(base3 + 2) = CUInt(t2.V3)
        Next
        geom.Indices = newIdx

        ' Cache TBN vaciar para recalcular
        geom.CachedTBN = Nothing

        ' ==== 3b) Remap skin partition body-part assignments ====
        ' After vertex compaction the partition's TrianglesCopy still holds old indices.
        ' Remap them so UpdateSkinPartitions can match triangles to the correct body parts.
        Dim remapDict As New Dictionary(Of Integer, Integer)(nNew)
        For i As Integer = 0 To nOld - 1
            If oldToNew(i) >= 0 Then remapDict(i) = oldToNew(i)
        Next
        shape.ParentSliderSet.NIFContent.RemapSkinPartitionTriangles(tri, remapDict)

        Return oldToNew
    End Function

    ''' <summary>
    ''' Paso 4 del zap: reindexa los diffs del OSD al espacio de indices post-compactacion.
    '''
    ''' ⛔ NO es thread-safe y NO puede correr dentro de un Parallel.ForEach por shape:
    '''   • muta el DataDiff de bloques del OSD del sliderset, y `Related_Slider_data` es un
    '''     IEnumerable LAZY que lee el XmlDocument compartido del OSP desde los otros hilos;
    '''   • `Blocks.Remove` toca un List(Of T) compartido.
    ''' (Ya no escribe en el XML: ver la nota sobre MaterializeEditableLocalBlocks abajo.)
    '''
    ''' Llamar desde la fase SERIAL del build, con el oldToNew que devolvio <see cref="RemoveZaps"/>.
    ''' </summary>
    ''' <param name="alreadyRemapped">
    ''' Bloques ya reindexados en esta pasada de build, por identidad. Un mismo bloque alcanzado desde
    ''' dos shapes se remapearia dos veces (la segunda sobre indices ya movidos). Pasar el MISMO set
    ''' para todas las shapes del size.
    ''' </param>
    Public Shared Sub ReindexMorphsAfterZap(shape As Shape_class, oldToNew As Integer(),
                                            alreadyRemapped As HashSet(Of OSD_Block_Class))
        If oldToNew Is Nothing Then Exit Sub

        ' SIN GroupBy: agrupar por `pf.Nombre` (el nombre del BLOQUE) y quedarse con uno solo salteaba
        ' Datas de OTROS sliders que compartieran nombre de bloque — sus diffs quedaban con indices
        ' pre-zap mientras LoadMorphTargets (que agrupa por ParentSlider.Nombre) si los leia. La
        ' idempotencia la da `alreadyRemapped`, por identidad de bloque, asi que recorrer todos es
        ' correcto y completo.
        For Each dat In shape.Related_Slider_data.ToList()
            ' ⛔ NO usar MaterializeEditableLocalBlocks aca. Convertir un Data externo a local escribe
            ' Islocal=True + TargetOsd en el XML del clon, y ese XML SOBREVIVE al UnloadShapeData del
            ' size siguiente mientras los bloques clonados (que sólo vivian en memoria) se pierden.
            ' En Sizecount=1 el Data ya no entra en OsdExternalFullPath, el .osd externo no se recarga
            ' y el .osd local de disco no tiene ese bloque ⇒ el _1.nif salia SIN los morphs de todo
            ' slider que fuera externo en una shape con zaps.
            ' Mutar el bloque que el Data realmente referencia (local o externo) es seguro: el builder
            ' es un clon descartable, cada size recarga el OSD desde disco, y BuildingForm nunca graba
            ' el proyecto.
            For Each block In dat.RelatedOSDBlocks.ToList()
                If alreadyRemapped IsNot Nothing AndAlso Not alreadyRemapped.Add(block) Then Continue For
                For Each ddiff In block.DataDiff.ToList()
                    Dim oldIdx As Integer = CInt(ddiff.Index)
                    If oldIdx < 0 OrElse oldIdx >= oldToNew.Length Then
#If DEBUG Then
                        Debugger.Break()
#End If
                        block.DataDiff.Remove(ddiff)
                        Continue For
                    End If
                    ddiff.Index = oldToNew(oldIdx)
                    If ddiff.Index < 0 Then
                        block.DataDiff.Remove(ddiff)
                    End If
                Next
                If block.DataDiff.Count = 0 Then
                    block.ParentOSDContent.Blocks.Remove(block)
                    ' Quirúrgico: los caches por nombre del sliderset se poblaron recorriendo Blocks,
                    ' así que hay que sacarlo de ahí o lo siguen devolviendo. Invalidarlos enteros
                    ' obligaba a re-barrer el .osd externo completo por cada shape zapeada.
                    shape.ParentSliderSet.ForgetOsdBlockFromCaches(block)
                Else
                    block.RebuildCompactArrays()
                End If
            Next
        Next

        ' Invalidate the in-memory MorphDiffs cache so the next LoadMorphTargets rebuilds
        ' it from the (now remapped) OSD DataDiff indices + the (now compacted) VC.
        ' Without this, BuildingForm's multi-size iteration (build_0 → build_1) would
        ' re-use MorphDiffs built against pre-compaction VC, producing IndexOutOfRange
        ' when applying morphs to the shrunken NifLocalVertices array.
        shape.MorphDiffs = Nothing
    End Sub

    Private Shared Sub ApplyMask_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, AllowMask As Boolean)
        Dim count = Geometry.BaseVertices.Length
        If Not AllowMask Then
            Array.Clear(Geometry.VertexMask, 0, count)
            Geometry.dirtyMaskIndices.Clear()
            For i = 0 To count - 1
                Geometry.dirtyMaskFlags(i) = False
            Next
        Else
            Dim maskeds = shape.MaskedVertices
            For i = 0 To count - 1
                ' `< 0`, no `= -1`: un zap parcial deja -0.5 y comparar contra -1 exacto lo dejaba
                ' pegado para siempre. Con el guard `t > 0` del loop de zaps ya nadie reescribe ese
                ' vertice, asi que el reset tiene que limpiar CUALQUIER negativo.
                If Geometry.VertexMask(i) < 0 Then
                    If shape.MaskedVertices.Contains(i) Then Geometry.VertexMask(i) = 1 Else Geometry.VertexMask(i) = 0
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                End If
                If (Geometry.VertexMask(i) = 0 AndAlso maskeds.Contains(i)) OrElse (Geometry.VertexMask(i) = 1 AndAlso Not maskeds.Contains(i)) Then
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                    Geometry.VertexMask(i) = 1 - Geometry.VertexMask(i)
                Else
                    If Geometry.VertexMask(i) <> 0 Then
                        Geometry.dirtyMaskIndices.Add(i)
                        Geometry.VertexMask(i) = Geometry.VertexMask(i)
                        Geometry.dirtyMaskFlags(i) = True
                    Else
                        Geometry.dirtyMaskFlags(i) = False
                    End If
                End If
            Next
        End If
    End Sub
End Class

''' <summary>
''' IMorphResolver implementation for Wardrobe Manager's slider-based morphs.
''' Resolves OSD slider data into generic MorphChannels that MorphEngine can apply.
''' </summary>
Public Class SliderMorphResolver
    Implements IMorphResolver

    ''' <summary>Peso que se está previsualizando. Sólo lo usa el gate del segundo pase de clamp, que
    ''' va contra el DEFAULT del slider para ese peso — la misma ley que el bake
    ''' (<see cref="MorphingHelper.ApplyMorph_CPU"/>). Big por default, que es lo que emite BodySlide
    ''' cuando no hay GenWeights.</summary>
    Public Property BuildSize As WM_Config.SliderSize = WM_Config.SliderSize.Big

    Public Function ResolveMorphPlan(shape As IRenderableShape, geom As SkinnedGeometry) As MorphPlan Implements IMorphResolver.ResolveMorphPlan
        Dim plan As New MorphPlan
        Dim wmShape = TryCast(shape, Shape_class)
        If wmShape Is Nothing Then Return plan

        ' Reuse LoadMorphTargets to avoid duplicating morph-loading logic
        MorphingHelper.LoadMorphTargets(wmShape, geom)

        ' Build channels from active sliders
        For Each s In wmShape.Related_Sliders
            ' MISMA funcion que el bake (ApplyMorph_CPU): RENDER == BAKE por construccion.
            Dim k = MorphingHelper.ResolveSlider(s)
            Dim deltas As List(Of MorphData) = Nothing
            If wmShape.MorphDiffs.TryGetValue(s.Nombre, deltas) Then
                ' El CLAMP se emite ANTES del early-out de UV: el loop de clamps de BodySlide
                ' (BuildListBodies:4402-4413) NO tiene guard de `bUV`, asi que un slider uv+clamp
                ' recibe ApplyUVDiff en la fase 1 Y ApplyClamp sobre POSICIONES en el 2do pase.
                ' El bake ya lo hacia (su 2do pase no filtra UV); el render lo salteaba.
                ' El `IsClamp` va PRIMERO: EffectiveDefault termina en SliderSet_Class.GenWeights, y
                ' esto corre dentro del Parallel.ForEach por shape del pipeline de render. Evaluarlo
                ' para todos los sliders era ~60 lecturas del XML compartido por shape por update.
                If s.IsClamp Then
                    Dim clampDefault = s.EffectiveDefault(BuildSize)
                    If Not Single.IsNaN(clampDefault) AndAlso clampDefault > 0.0F Then
                        plan.Channels.Add(New MorphChannel(s.Nombre, 1.0F, deltas, False,
                                                           engineApplied:=True, applyCkBlockGate:=False,
                                                           isClamp:=True))
                    End If
                End If

                ' Un slider UV no emite canal de POSICION: sus deltas van al array de UVs, no a los
                ' vertices (BuildListBodies:4392 -> DiffDataSets::ApplyUVDiff). Emitirlo como canal
                ' de posicion deformaba la malla. Se emite marcado IsUvMorph, que el MorphEngine
                ' saltea en el loop de posiciones y aplica en ApplyUvChannels — el MISMO resultado
                ' que el bake, que morphea Geometry.Uvs_Weight.
                If k.Kind = SliderKind.UvMorph Then
                    plan.Channels.Add(New MorphChannel(s.Nombre, k.Weight, deltas, False,
                                                       engineApplied:=True, applyCkBlockGate:=False,
                                                       isClamp:=False, isUvMorph:=True))
                    Continue For
                End If

                Dim isZap = (k.Kind = SliderKind.Zap)
                ' DiffDataSets::GetDiffIndices usa `fabs(componente) > threshold` con threshold=0: un
                ' vertice cuyo delta de zap es todo-cero NO se zapea. El filtro va ACA y no en el
                ' MorphEngine porque es una ley del OSD de BodySlide: otros resolvers (HairTopZapResolver
                ' de NPC_Manager) emiten PosDiff=Zero a proposito, usando la lista como puro indice.
                ' Lista nueva, no mutar: `deltas` es la cache compartida shape.MorphDiffs.
                Dim channelDeltas = If(isZap, FilterNonZeroDeltas(deltas), deltas)
                ' applyCkBlockGate:=False — estos deltas salen de un .osd de BodySlide, no de un .tri de
                ' FaceGen: no los aplica ningun applier del motor, y ApplyDiff no tiene gate alguno.
                plan.Channels.Add(New MorphChannel(s.Nombre, k.Weight, channelDeltas,
                                                   isZap AndAlso wmShape.ApplyZaps,
                                                   engineApplied:=True, applyCkBlockGate:=False))

            End If
        Next

        Return plan
    End Function

    ''' <summary>Copia sin los deltas todo-cero. Devuelve la misma instancia si no habia ninguno,
    ''' para no asignar por frame en el caso normal.</summary>
    Private Shared Function FilterNonZeroDeltas(deltas As List(Of MorphData)) As List(Of MorphData)
        If deltas Is Nothing Then Return Nothing
        Dim zeros As Integer = 0
        For Each m In deltas
            If m.PosDiff.X = 0.0F AndAlso m.PosDiff.Y = 0.0F AndAlso m.PosDiff.Z = 0.0F Then zeros += 1
        Next
        If zeros = 0 Then Return deltas

        Dim filtered As New List(Of MorphData)(deltas.Count - zeros)
        For Each m In deltas
            If m.PosDiff.X <> 0.0F OrElse m.PosDiff.Y <> 0.0F OrElse m.PosDiff.Z <> 0.0F Then filtered.Add(m)
        Next
        Return filtered
    End Function
End Class

''' <summary>
''' IGeometryModifier implementation for WM's zap removal (topology compaction).
''' Removes vertices marked with negative mask values and reindexes triangles + morphs.
''' </summary>
Public Class ZapGeometryModifier
    Implements IGeometryModifier

    ''' <summary>
    ''' Mapa old-&gt;new que dejo el ultimo Apply, o Nothing si no hubo zaps. El caller DEBE pasarlo a
    ''' <see cref="MorphingHelper.ReindexMorphsAfterZap"/> desde la fase serial: ese paso escribe en
    ''' el XmlDocument del OSP y en las listas de bloques del sliderset, compartidos entre shapes.
    ''' Usar una instancia de modifier POR SHAPE (asi es como lo hace BuildingForm).
    ''' </summary>
    Public Property VertexRemap As Integer()

    ''' <summary>
    ''' True cuando la shape quedaba 100 % zapeada y el sliderset pide conservarla: la geometria NO
    ''' se toco y el caller debe prender el flag hidden, como hace BodySlideApp.cpp:3618.
    ''' </summary>
    Public Property FullyZappedKept As Boolean

    ''' <summary>Se resuelve en el caller serial: leerlo por shape dentro del Parallel.ForEach
    ''' dispararia un XPath sobre el XmlDocument compartido del OSP.</summary>
    Private ReadOnly _keepZappedShapes As Boolean

    Public Sub New(keepZappedShapes As Boolean)
        _keepZappedShapes = keepZappedShapes
    End Sub

    Public Sub Apply(shape As IRenderableShape, ByRef geom As SkinnedGeometry) Implements IGeometryModifier.Apply
        Dim wmShape = TryCast(shape, Shape_class)
        If wmShape IsNot Nothing Then
            Dim kept As Boolean = False
            VertexRemap = MorphingHelper.RemoveZaps(wmShape, geom, _keepZappedShapes, kept)
            FullyZappedKept = kept
        End If
    End Sub
End Class
