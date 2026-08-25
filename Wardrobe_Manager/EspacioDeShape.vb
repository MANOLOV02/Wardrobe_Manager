Imports System.Numerics
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs

''' <summary>Reconciliación del SISTEMA DE COORDENADAS de una shape: llevarla a global y dejar su
''' transformada en identidad, que es la precondición para copiar vértices entre dos shapes.
'''
''' <para><b>Por qué existe.</b> Cada shape tiene su propio <c>shapeToGlobal</c>. El merge concatena los
''' vértices del donante en el buffer del target TAL CUAL: si los dos espacios no coinciden, los vértices
''' caen en otro lado. Desplazamiento típico medido: <b>120,847 u — la altura del cuerpo</b>, o sea que la
''' prenda donante aparece a la altura de la cabeza o en el piso.</para>
'''
''' <para>⛔ <b>SYNC — commit <c>cb77cf5b</c> (15-ago-2026) de BodySlide/Outfit Studio</b>,
''' <c>OutfitProject::PrepareCopyGeo</c> (<c>OutfitProject.cpp:5300-5311</c>):
''' <code>
''' // The vertices are collected in the source shape's coordinates and appended to
''' // the target shape, so both shapes need to share a coordinate system. If they
''' // don't, apply the transforms to the geometry of both shapes and clear them,
''' // which leaves the meshes where they are in global coordinates.
''' if (!workAnim.GetTransformShapeToGlobal(source).IsNearlyEqualTo(workAnim.GetTransformShapeToGlobal(target))) {
'''     ApplyShapeTransformToGeometry(source);
'''     ApplyShapeTransformToGeometry(target);
''' }
''' </code></para>
'''
''' <para><b>ALCANCE medido sobre los dos BodySlide del usuario</b> (predicado canónico
''' <c>IsNearlyEqualTo</c>, no un umbral inventado):
''' FO4 <b>517 de 1.905</b> sliderSets multi-shape con espacios distintos —220 con desplazamiento ≥10 u,
''' 6 entre 1 y 10, 291 por debajo de 1 u—; SSE <b>32 de 1.195</b> —28 y 4, sin ruido—.
''' Casos: <c>Accesories - Aprons.osp</c> / <c>Vtaw 9 Sexy Maid Apron</c> (120,847 u) y
''' <c>CBBE CC Replacers.osp</c> / <c>Hands - Wraithguard</c> (120,354 u).</para>
'''
''' <para>⚠️ <b>El costo, medido y aceptado por el usuario:</b> los 291 sub-1 u también se reescriben,
''' porque el predicado canónico los marca (la diferencia está en la rotación, cuya tolerancia no escala
''' con los 120 u de la traslación). De 3.069.500 vértices del donante, <b>1.925.349 cambian en el dato
''' GUARDADO</b> (267 de los 291 sets tienen al menos uno) y <b>1.581 shapes half-float se re-cuantizan</b>,
''' la peor en <b>0,062478 u</b> (SSE: 34 shapes, peor 0,034605).</para></summary>
Public NotInheritable Class EspacioDeShape

    Private Sub New()
    End Sub

    ''' <summary>`NiTransform` (el struct del NIF) ⇄ `Transform_Class`. No hay conversion en la clase:
    ''' sus constructores cubren NiNode / BSSkinBoneTrans / BoneData / Matrix4(d), no el struct pelado.</summary>
    Private Shared Function DesdeNiTransform(t As NiTransform) As Transform_Class
        Return New Transform_Class With {.Rotation = t.Rotation, .Translation = t.Translation, .Scale = t.Scale}
    End Function

    Private Shared Function ANiTransform(t As Transform_Class) As NiTransform
        Dim ex As Boolean
        Return New NiTransform With {.Rotation = t.Rotation, .Translation = t.Translation, .Scale = t.EscalaComoEscalar(ex)}
    End Function

    ' ═══════════════════════════════════════════════════════════════════════════════════════════════
    ' Predicado canónico de igualdad. NO ES UN UMBRAL DE LA APP.
    ' ═══════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>SYNC: <c>nifly\include\Object3d.hpp:16</c> <c>constexpr float EPSILON = 0.0001f;</c></summary>
    Private Const EPSILON As Single = 0.0001F

    ''' <summary>SYNC: <c>Object3d.hpp:21-24</c>. La tolerancia ESCALA con la magnitud, con piso 1:
    ''' <c>scale = max(|a|, |b|, 1)</c> y <c>|a-b| &lt;= EPSILON * scale</c>. Por eso una traslación de
    ''' 120,84 u tolera ~0,0121 y una entrada de matriz de rotación sólo 0,0001 — y por eso los 291 sets
    ''' "de ruido" fallan el predicado aunque su desplazamiento sea sub-1 u.</summary>
    Private Shared Function CasiIguales(a As Single, b As Single) As Boolean
        Dim escala As Single = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1.0F)
        Return Math.Abs(a - b) <= EPSILON * escala
    End Function

    Private Shared Function CasiIguales(a As Vector3, b As Vector3) As Boolean
        Return CasiIguales(a.X, b.X) AndAlso CasiIguales(a.Y, b.Y) AndAlso CasiIguales(a.Z, b.Z)
    End Function

    Private Shared Function CasiIguales(a As Matrix33, b As Matrix33) As Boolean
        Return CasiIguales(a.M11, b.M11) AndAlso CasiIguales(a.M12, b.M12) AndAlso CasiIguales(a.M13, b.M13) AndAlso
               CasiIguales(a.M21, b.M21) AndAlso CasiIguales(a.M22, b.M22) AndAlso CasiIguales(a.M23, b.M23) AndAlso
               CasiIguales(a.M31, b.M31) AndAlso CasiIguales(a.M32, b.M32) AndAlso CasiIguales(a.M33, b.M33)
    End Function

    ''' <summary>SYNC: <c>MatTransform::IsNearlyEqualTo</c> (<c>Object3d.hpp:1129-1132</c>): traslación,
    ''' rotación y escala, cada componente por <see cref="CasiIguales"/>.</summary>
    Public Shared Function MismoEspacio(a As Transform_Class, b As Transform_Class) As Boolean
        If a Is Nothing OrElse b Is Nothing Then Return a Is b
        Dim exA As Boolean, exB As Boolean
        Return CasiIguales(a.Translation, b.Translation) AndAlso
               CasiIguales(a.Rotation, b.Rotation) AndAlso
               CasiIguales(a.EscalaComoEscalar(exA), b.EscalaComoEscalar(exB))
    End Function

    Private Shared Function EsIdentidad(t As Transform_Class) As Boolean
        Return MismoEspacio(t, New Transform_Class())
    End Function

    ' ═══════════════════════════════════════════════════════════════════════════════════════════════
    ' shapeToGlobal
    ' ═══════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>SYNC: <c>AnimInfo::GetTransformShapeToGlobal</c> (<c>Anim.cpp:781-793</c>).
    ''' <list type="bullet">
    ''' <item>Skinned → <c>inverse(xformGlobalToSkin)</c>, SIN recorrer el parent chain.</item>
    ''' <item>Unskinned → la cadena de nodos acumulada, que es lo que ya hace
    ''' <c>Transform_Class.GetGlobalTransform</c>.</item></list></summary>
    Public Shared Function ShapeToGlobal(shape As Shape_class) As Transform_Class
        If shape Is Nothing Then Return New Transform_Class()
        Dim nifShape = shape.RelatedNifShape
        If nifShape Is Nothing Then Return New Transform_Class()
        Dim nif = shape.ParentSliderSet?.NIFContent
        If nif Is Nothing Then Return New Transform_Class()

        If Not nifShape.IsSkinned Then
            Return Transform_Class.GetGlobalTransform(nifShape, nif)
        End If
        Return GlobalToSkin(shape).Inverse()
    End Function

    ''' <summary>SYNC: <c>AnimSkin::LoadFromNif</c> (<c>Anim.cpp:128-161</c>). DOS fuentes, en este orden:
    ''' <list type="number">
    ''' <item><b>gotGTS</b>: el NIF trae la transformada directamente
    ''' (<c>NiSkinData.SkinTransform</c> — es el caso de SSE).</item>
    ''' <item>Si no la trae (FO4 con <c>BSSkin_Instance</c>, que no tiene ese campo): se DERIVA por hueso
    ''' como <c>inverse(boneToGlobal ∘ skinToBone)</c> y se toma la <b>MEDIANA</b>
    ''' (<c>CalcMedianMatTransform</c>) — no el promedio, y no el primero.</item></list>
    '''
    ''' <para>⭐ <b>Por qué el <c>boneToGlobal</c> sale del propio NIF y no de un esqueleto de referencia.</b>
    ''' El canónico lo pide a <c>AnimSkeleton</c> (<c>res\skeleton_fo4.nif</c>). Lo medí sobre el corpus:
    ''' <b>170.832 muestras, 0 distintas</b> entre la cadena de nodos del propio NIF y el esqueleto de
    ''' referencia. Así que este arreglo NO necesita cargar un archivo externo, y eso no es una
    ''' simplificación: es una equivalencia medida.</para>
    '''
    ''' <para>⚠️ La mediana importa: <b>1.709 de 10.446 shapes de FO4</b> tienen binds inconsistentes entre
    ''' huesos (734 con más de 1 u de discrepancia). Tomar el primer hueso daría otra respuesta.</para></summary>
    Private Shared Function GlobalToSkin(shape As Shape_class) As Transform_Class
        Dim nif = shape.ParentSliderSet.NIFContent

        ' (1) gotGTS — SSE: NiSkinData trae la transformada.
        Dim niSkin = TryCast(shape.RelatedNifSkin, NiSkinInstance)
        Dim niSkinData As NiSkinData = Nothing
        If niSkin IsNot Nothing Then
            niSkinData = TryCast(nif.Blocks(niSkin.Data.Index), NiSkinData)
            If niSkinData IsNot Nothing Then Return DesdeNiTransform(niSkinData.SkinTransform)
        End If

        ' (2) Derivación por hueso + mediana — FO4 (BSSkin_Instance no tiene el campo).
        Dim bsSkin = TryCast(shape.RelatedNifSkin, BSSkin_Instance)
        Dim ids As List(Of Integer) = Nothing
        If bsSkin IsNot Nothing AndAlso bsSkin.Bones IsNot Nothing Then
            ids = bsSkin.Bones.Indices.Select(Function(x) CInt(x)).ToList()
        ElseIf niSkin IsNot Nothing AndAlso niSkin.Bones IsNot Nothing Then
            ids = niSkin.Bones.Indices.Select(Function(x) CInt(x)).ToList()
        End If
        If ids Is Nothing OrElse ids.Count = 0 Then Return New Transform_Class()

        Dim bsBoneData As BSSkin_BoneData = Nothing
        If bsSkin IsNot Nothing Then bsBoneData = TryCast(nif.Blocks(bsSkin.Data.Index), BSSkin_BoneData)

        Dim cada As New List(Of Transform_Class)()
        Dim newID As Integer = 0
        For Each id In ids
            Dim node = TryCast(nif.Blocks(id), NiNode)
            If node Is Nothing Then Continue For
            Dim boneToGlobal = Transform_Class.GetGlobalTransform(node, nif)
            Dim skinToBone As Transform_Class = Nothing
            If bsBoneData IsNot Nothing AndAlso newID < bsBoneData.BoneList.Count Then
                skinToBone = New Transform_Class(bsBoneData.BoneList(newID))
            ElseIf niSkinData IsNot Nothing AndAlso newID < niSkinData.BoneList.Count Then
                skinToBone = New Transform_Class(niSkinData.BoneList(newID))
            End If
            If skinToBone IsNot Nothing Then
                cada.Add(boneToGlobal.ComposeTransforms(skinToBone).Inverse())
            End If
            newID += 1
        Next
        ' Anim.cpp:168-170: sólo se pisa si hubo al menos uno. Sin ninguno queda la identidad, que es lo
        ' que devuelve GetTransformGlobalToShape cuando no hay entrada de skinning (Anim.cpp:795-800).
        If cada.Count = 0 Then Return New Transform_Class()
        Return MedianaDeTransformadas(cada)
    End Function

    ' ═══════════════════════════════════════════════════════════════════════════════════════════════
    ' Mediana de transformadas — port literal de nifly
    ' ═══════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>SYNC: <c>CalcMedianOfFloats</c> (<c>Object3d.cpp:6-19</c>). Con n par promedia los dos
    ''' centrales; con n impar devuelve el central. NO es el promedio de todos.</summary>
    Private Shared Function MedianaDeFloats(datos As List(Of Single)) As Single
        Dim n = datos.Count
        If n <= 0 Then Return 0.0F
        Dim orden = datos.OrderBy(Function(x) x).ToList()
        If (n And 1) = 1 Then Return orden(n \ 2)
        Return (orden(n \ 2) + orden(n \ 2 - 1)) / 2.0F
    End Function

    ''' <summary>SYNC: <c>CalcMedianOfVector3</c> (<c>Object3d.cpp:138-159</c>). Mediana POR COMPONENTE:
    ''' el resultado puede no ser ninguno de los vectores de entrada, y así lo hace el canónico.</summary>
    Private Shared Function MedianaDeVector3(datos As List(Of Vector3)) As Vector3
        If datos.Count = 0 Then Return New Vector3(0, 0, 0)
        Return New Vector3(MedianaDeFloats(datos.Select(Function(v) v.X).ToList()),
                           MedianaDeFloats(datos.Select(Function(v) v.Y).ToList()),
                           MedianaDeFloats(datos.Select(Function(v) v.Z).ToList()))
    End Function

    ''' <summary>SYNC: <c>RotVecToMat</c> (<c>Object3d.cpp:21-44</c>). Vector eje-ángulo → matriz.
    ''' El <c>onemcosang</c> por la vía del seno cuando <c>cosang &gt; .5</c> es del canónico: evita la
    ''' cancelación catastrófica de <c>1 - cos</c> para ángulos chicos, que es justo el régimen de la
    ''' mediana (todos los huesos casi coinciden).</summary>
    Private Shared Function RotVecAMatriz(v As Vector3) As Matrix33
        Dim angle As Double = Math.Sqrt(CDbl(v.X) * v.X + CDbl(v.Y) * v.Y + CDbl(v.Z) * v.Z)
        Dim cosang As Double = Math.Cos(angle)
        Dim sinang As Double = Math.Sin(angle)
        Dim onemcosang As Double
        If cosang > 0.5 Then
            onemcosang = sinang * sinang / (1 + cosang)
        Else
            onemcosang = 1 - cosang
        End If
        Dim n As Vector3 = If(angle <> 0.0, New Vector3(CSng(v.X / angle), CSng(v.Y / angle), CSng(v.Z / angle)),
                                            New Vector3(1.0F, 0.0F, 0.0F))
        Dim m As New Matrix33
        m.M11 = CSng(n.X * n.X * onemcosang + cosang)
        m.M22 = CSng(n.Y * n.Y * onemcosang + cosang)
        m.M33 = CSng(n.Z * n.Z * onemcosang + cosang)
        m.M12 = CSng(n.X * n.Y * onemcosang + n.Z * sinang)
        m.M21 = CSng(n.X * n.Y * onemcosang - n.Z * sinang)
        m.M23 = CSng(n.Y * n.Z * onemcosang + n.X * sinang)
        m.M32 = CSng(n.Y * n.Z * onemcosang - n.X * sinang)
        m.M31 = CSng(n.Z * n.X * onemcosang + n.Y * sinang)
        m.M13 = CSng(n.Z * n.X * onemcosang - n.Y * sinang)
        Return m
    End Function

    ''' <summary>SYNC: <c>RotMatToVec</c> (<c>Object3d.cpp:46-86</c>). Matriz → vector eje-ángulo, con las
    ''' TRES ramas del canónico (ángulo chico por <c>asin</c>, medio por <c>acos</c>, y el caso degenerado
    ''' de 180° que se resuelve por la diagonal con los clamps a 0 que evitan el NaN).</summary>
    Private Shared Function RotMatrizAVec(m As Matrix33) As Vector3
        Dim cosang As Double = (CDbl(m.M11) + m.M22 + m.M33 - 1) * 0.5
        If cosang > 0.5 Then
            Dim v As New Vector3(m.M23 - m.M32, m.M31 - m.M13, m.M12 - m.M21)
            Dim sin2ang As Double = v.Length()
            If sin2ang = 0.0 Then Return New Vector3(0, 0, 0)
            Return v * CSng(Math.Asin(sin2ang * 0.5) / sin2ang)
        End If
        If cosang > -1 Then
            Dim v As New Vector3(m.M23 - m.M32, m.M31 - m.M13, m.M12 - m.M21)
            v = Vector3.Normalize(v)
            Return v * CSng(Math.Acos(cosang))
        End If
        Dim x As Double = (CDbl(m.M11) - cosang) * 0.5
        Dim y As Double = (CDbl(m.M22) - cosang) * 0.5
        Dim z As Double = (CDbl(m.M33) - cosang) * 0.5
        If x < 0.0 Then x = 0.0
        If y < 0.0 Then y = 0.0
        If z < 0.0 Then z = 0.0
        Dim w As New Vector3(CSng(Math.Sqrt(x)), CSng(Math.Sqrt(y)), CSng(Math.Sqrt(z)))
        w = Vector3.Normalize(w)
        If m.M23 < m.M32 Then w.X = -w.X
        If m.M31 < m.M13 Then w.Y = -w.Y
        If m.M12 < m.M21 Then w.Z = -w.Z
        Return w * CSng(Math.PI)
    End Function

    ''' <summary>Producto de matrices estándar <c>a · b</c>, fila de <paramref name="a"/> por columna de
    ''' <paramref name="b"/>. SYNC: <c>Matrix3::operator*</c> (<c>Object3d.hpp:545-557</c>).</summary>
    Private Shared Function Mult(a As Matrix33, b As Matrix33) As Matrix33
        Dim r As New Matrix33
        r.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31
        r.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32
        r.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33
        r.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31
        r.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32
        r.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33
        r.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31
        r.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32
        r.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        Return r
    End Function

    Private Shared Function Transpuesta(a As Matrix33) As Matrix33
        Dim r As New Matrix33
        r.M11 = a.M11 : r.M12 = a.M21 : r.M13 = a.M31
        r.M21 = a.M12 : r.M22 = a.M22 : r.M23 = a.M32
        r.M31 = a.M13 : r.M32 = a.M23 : r.M33 = a.M33
        Return r
    End Function

    ''' <summary>SYNC: <c>CalcMedianRotation</c> (<c>Object3d.cpp:161-188</c>). La mediana de rotaciones NO
    ''' es componente a componente sobre la matriz: se promedia primero en el espacio eje-ángulo para
    ''' fijar un punto base en la variedad de rotaciones, se re-basan todas contra él, se toma la mediana
    ''' de los vectores re-basados y se vuelve a componer. Hacerlo directo sobre las 9 entradas da una
    ''' matriz que ni siquiera es una rotación.</summary>
    Private Shared Function MedianaDeRotaciones(rots As List(Of Matrix33)) As Matrix33
        Dim n = rots.Count
        If n = 0 Then Return New Matrix33 With {.M11 = 1, .M22 = 1, .M33 = 1}
        Dim sum1 As New Vector3(0, 0, 0)
        For Each r In rots
            sum1 += RotMatrizAVec(r)
        Next
        sum1 = New Vector3(sum1.X / n, sum1.Y / n, sum1.Z / n)
        Dim base = RotVecAMatriz(sum1)
        Dim baseinv = Transpuesta(base)
        Dim vecs As New List(Of Vector3)(n)
        For Each r In rots
            vecs.Add(RotMatrizAVec(Mult(baseinv, r)))
        Next
        Return Mult(base, RotVecAMatriz(MedianaDeVector3(vecs)))
    End Function

    ''' <summary>SYNC: <c>CalcMedianMatTransform</c> (<c>Object3d.cpp:190-209</c>).</summary>
    Private Shared Function MedianaDeTransformadas(ts As List(Of Transform_Class)) As Transform_Class
        If ts.Count = 0 Then Return New Transform_Class()
        Dim res As New Transform_Class()
        res.Rotation = MedianaDeRotaciones(ts.Select(Function(t) t.Rotation).ToList())
        res.Translation = MedianaDeVector3(ts.Select(Function(t) t.Translation).ToList())
        Dim ex As Boolean
        res.Scale = MedianaDeFloats(ts.Select(Function(t) t.EscalaComoEscalar(ex)).ToList())
        Return res
    End Function

    ' ═══════════════════════════════════════════════════════════════════════════════════════════════
    ' Aplicar la transformada a la geometría
    ' ═══════════════════════════════════════════════════════════════════════════════════════════════

    Private Shared Function AplicarAPunto(t As Transform_Class, v As Vector3) As Vector3
        ' SYNC: MatTransform::ApplyTransform (Object3d.hpp:1099-1101) — escalar, rotar, trasladar.
        Dim s = t.EffectiveScale
        Dim p As New Vector3(v.X * s.X, v.Y * s.Y, v.Z * s.Z)
        Dim r = t.Rotation
        Return New Vector3(r.M11 * p.X + r.M12 * p.Y + r.M13 * p.Z + t.Translation.X,
                           r.M21 * p.X + r.M22 * p.Y + r.M23 * p.Z + t.Translation.Y,
                           r.M31 * p.X + r.M32 * p.Y + r.M33 * p.Z + t.Translation.Z)
    End Function

    Private Shared Function AplicarADiferencia(t As Transform_Class, v As Vector3) As Vector3
        ' SYNC: MatTransform::ApplyTransformToDiff (Object3d.hpp:1105-1107) — SIN traslación.
        Dim s = t.EffectiveScale
        Dim p As New Vector3(v.X * s.X, v.Y * s.Y, v.Z * s.Z)
        Dim r = t.Rotation
        Return New Vector3(r.M11 * p.X + r.M12 * p.Y + r.M13 * p.Z,
                           r.M21 * p.X + r.M22 * p.Y + r.M23 * p.Z,
                           r.M31 * p.X + r.M32 * p.Y + r.M33 * p.Z)
    End Function

    Private Shared Function AplicarADireccion(t As Transform_Class, v As Vector3) As Vector3
        ' SYNC: MatTransform::ApplyTransformToDir (Object3d.hpp:1111-1113) — SÓLO rotación.
        Dim r = t.Rotation
        Return New Vector3(r.M11 * v.X + r.M12 * v.Y + r.M13 * v.Z,
                           r.M21 * v.X + r.M22 * v.Y + r.M23 * v.Z,
                           r.M31 * v.X + r.M32 * v.Y + r.M33 * v.Z)
    End Function

    ''' <summary>SYNC: <c>OutfitProject::ApplyTransformToShapeGeometry</c>
    ''' (<c>OutfitProject.cpp:2645-2686</c>). Los CUATRO pasos, y los cuatro importan:
    ''' <list type="number">
    ''' <item><b>vértices</b> ← <c>t.ApplyTransform(v)</c>;</item>
    ''' <item><b>diffs de POSICIÓN del .osd</b> ← <c>t.ApplyTransformToDiff(d)</c>, <b>salteando los
    ''' sliders uv, clamp y zap</b> (<c>:2661-2663</c>) — un uv no es un desplazamiento en el espacio y
    ''' rotarlo lo destruye;</item>
    ''' <item><b>normales</b> ← <c>t.ApplyTransformToDir(n)</c>, y <b>sólo si la rotación no es
    ''' identidad</b> (<c>:2674-2675</c>): con traslación pura las normales no cambian, y re-escribirlas
    ''' sería re-cuantizarlas sin motivo;</item>
    ''' <item>lo hace el llamador: dejar la transformada de la shape en identidad.</item></list>
    '''
    ''' <para>⛔ Un arreglo que sólo moviera los VÉRTICES rompería los morphs: los deltas del <c>.osd</c>
    ''' quedarían expresados en el espacio viejo y cada slider empujaría la geometría en la dirección
    ''' equivocada. Por eso el paso 2 no es opcional.</para></summary>
    Private Shared Sub AplicarTransformALaGeometria(shape As Shape_class, sliderSet As SliderSet_Class, t As Transform_Class)
        Dim geom = shape.IR_Geometry
        If geom Is Nothing Then Return

        ' (1) vértices
        Dim verts = geom.GetVertexPositions()
        If verts IsNot Nothing AndAlso verts.Count > 0 Then
            Dim nuevos As New List(Of Vector3)(verts.Count)
            For Each v In verts
                nuevos.Add(AplicarAPunto(t, v))
            Next
            geom.SetVertexPositions(nuevos)
        End If

        ' (2) diffs de posición del .osd, salteando uv / clamp / zap
        For Each slider In sliderSet.Sliders
            If slider Is Nothing Then Continue For
            If slider.IsUV OrElse slider.IsClamp OrElse slider.IsZap Then Continue For
            For Each dat In slider.Datas
                If dat Is Nothing OrElse Not String.Equals(dat.Target, shape.Target, StringComparison.OrdinalIgnoreCase) Then Continue For
                For Each bloque In dat.RelatedOSDBlocks
                    If bloque Is Nothing OrElse bloque.DataDiff Is Nothing Then Continue For
                    For k = 0 To bloque.DataDiff.Count - 1
                        Dim d = bloque.DataDiff(k)
                        Dim nd = AplicarADiferencia(t, New Vector3(d.X, d.Y, d.Z))
                        d.X = nd.X : d.Y = nd.Y : d.Z = nd.Z
                    Next
                    bloque.RebuildCompactArrays()
                Next
            Next
        Next

        ' (3) normales — sólo si la rotación NO es identidad
        Dim rotIdentidad As New Matrix33 With {.M11 = 1, .M22 = 1, .M33 = 1}
        If CasiIguales(t.Rotation, rotIdentidad) Then Return
        If Not geom.HasNormals Then Return
        Dim norms = geom.GetNormals()
        If norms Is Nothing OrElse norms.Count <> If(verts Is Nothing, 0, verts.Count) Then Return
        Dim nuevasN As New List(Of Vector3)(norms.Count)
        For Each nn In norms
            nuevasN.Add(AplicarADireccion(t, nn))
        Next
        geom.SetNormals(nuevasN)
    End Sub

    ''' <summary>SYNC: <c>OutfitProject::ApplyShapeTransformToGeometry</c>
    ''' (<c>OutfitProject.cpp:2688-2703</c>). Lleva la geometría a coordenadas globales y deja la
    ''' transformada de la shape en IDENTIDAD, de modo que la malla no se mueve de donde está.
    ''' Devuelve False —sin tocar nada— cuando la shape ya está en global.</summary>
    Public Shared Function LlevarAGlobal(shape As Shape_class, sliderSet As SliderSet_Class) As Boolean
        If shape Is Nothing OrElse sliderSet Is Nothing Then Return False
        Dim nifShape = shape.RelatedNifShape
        If nifShape Is Nothing Then Return False

        Dim vieja = ShapeToGlobal(shape)
        If EsIdentidad(vieja) Then Return False

        ' newShapeToGlobal = identidad ⇒ la transformada a aplicar es inverse(identidad) ∘ vieja = vieja.
        AplicarTransformALaGeometria(shape, sliderSet, vieja)
        PonerShapeToGlobalEnIdentidad(shape)
        shape.IR_Geometry?.UpdateBounds()
        Return True
    End Function

    ''' <summary>SYNC: <c>AnimInfo::SetTransformShapeToGlobal</c> (<c>Anim.cpp:805-819</c>) con
    ''' <c>newShapeToGlobal = identidad</c>, más <c>ChangeGlobalToSkinTransform</c>
    ''' (<c>Anim.cpp:313-317</c>).
    ''' <list type="bullet">
    ''' <item><b>Skinned</b>: <c>globalToSkin</c> ← identidad Y el <c>skinToBone</c> de <b>CADA</b> hueso
    ''' recalculado como <c>inverse(identidad ∘ boneToGlobal) = inverse(boneToGlobal)</c>
    ''' (<c>RecalcXFormSkinToBone</c>, <c>Anim.cpp:302-305</c>). Saltear esto deja los binds describiendo
    ''' el espacio viejo: la malla se ve bien parada y se deforma mal.</item>
    ''' <item><b>Unskinned</b>: la transformada al padre pasa a
    ''' <c>inverse(parentToGlobal)</c>.</item></list></summary>
    Private Shared Sub PonerShapeToGlobalEnIdentidad(shape As Shape_class)
        Dim nif = shape.ParentSliderSet.NIFContent
        Dim nifShape = shape.RelatedNifShape

        If Not nifShape.IsSkinned Then
            Dim padre = TryCast(nif.GetParentNode(nifShape), NiNode)
            Dim parentToGlobal As Transform_Class = If(padre Is Nothing, New Transform_Class(),
                                                       Transform_Class.GetGlobalTransform(padre, nif))
            Dim nuevaLocal = parentToGlobal.Inverse()
            nifShape.Translation = nuevaLocal.Translation
            nifShape.Rotation = nuevaLocal.Rotation
            nifShape.Scale = nuevaLocal.Scale
            Return
        End If

        ' Skinned. globalToSkin ← identidad.
        Dim niSkin = TryCast(shape.RelatedNifSkin, NiSkinInstance)
        Dim niSkinData As NiSkinData = Nothing
        If niSkin IsNot Nothing Then niSkinData = TryCast(nif.Blocks(niSkin.Data.Index), NiSkinData)
        If niSkinData IsNot Nothing Then
            niSkinData.SkinTransform = ANiTransform(New Transform_Class())
        End If

        ' skinToBone de CADA hueso ← inverse(boneToGlobal).
        Dim bsSkin = TryCast(shape.RelatedNifSkin, BSSkin_Instance)
        Dim bsBoneData As BSSkin_BoneData = Nothing
        Dim ids As List(Of Integer) = Nothing
        If bsSkin IsNot Nothing AndAlso bsSkin.Bones IsNot Nothing Then
            ids = bsSkin.Bones.Indices.Select(Function(x) CInt(x)).ToList()
            bsBoneData = TryCast(nif.Blocks(bsSkin.Data.Index), BSSkin_BoneData)
        ElseIf niSkin IsNot Nothing AndAlso niSkin.Bones IsNot Nothing Then
            ids = niSkin.Bones.Indices.Select(Function(x) CInt(x)).ToList()
        End If
        If ids Is Nothing Then Return

        Dim newID As Integer = 0
        For Each id In ids
            Dim node = TryCast(nif.Blocks(id), NiNode)
            If node Is Nothing Then Continue For
            Dim nuevoSkinToBone = Transform_Class.GetGlobalTransform(node, nif).Inverse()
            If bsBoneData IsNot Nothing AndAlso newID < bsBoneData.BoneList.Count Then
                Dim bt = bsBoneData.BoneList(newID)
                bt.Translation = nuevoSkinToBone.Translation
                bt.Rotation = nuevoSkinToBone.Rotation
                bt.Scale = nuevoSkinToBone.Scale
                bsBoneData.BoneList(newID) = bt
            ElseIf niSkinData IsNot Nothing AndAlso newID < niSkinData.BoneList.Count Then
                Dim bd = niSkinData.BoneList(newID)
                bd.SkinTransform = ANiTransform(nuevoSkinToBone)
                niSkinData.BoneList(newID) = bd
            End If
            newID += 1
        Next
    End Sub

    ''' <summary>⭐ LA PUERTA. Reconcilia el espacio de <paramref name="a"/> y <paramref name="b"/> si
    ''' difieren, exactamente como <c>PrepareCopyGeo</c>: o los dos van a global, o no se toca ninguno.
    ''' Devuelve True si movió algo.</summary>
    Public Shared Function ReconciliarSiDifieren(a As Shape_class, b As Shape_class, sliderSet As SliderSet_Class) As Boolean
        If a Is Nothing OrElse b Is Nothing OrElse sliderSet Is Nothing Then Return False
        If MismoEspacio(ShapeToGlobal(a), ShapeToGlobal(b)) Then Return False
        Dim m1 = LlevarAGlobal(a, sliderSet)
        Dim m2 = LlevarAGlobal(b, sliderSet)
        Return m1 OrElse m2
    End Function

End Class
