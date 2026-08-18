' Version Uploaded of Wardrobe 3.2.0
Imports OpenTK.Mathematics
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Raytracer de oclusion sobre BVH en CPU. Construye una vez desde las mallas ocluders y despues
''' ComputeOccludedVertices devuelve los vertices tapados de una malla objetivo.
''' Todas las posiciones son de MUNDO (via el cache de SkinningHelper.GetWorldVertices).
''' Usa BvhHelper para el AABB/BvhNode/BuildBvh compartidos.
'''
''' <para><b>⭐ LA REGLA QUE EVITA LOS HUECOS (lo mas importante de este archivo).</b>
''' El zap borra un triangulo si CUALQUIERA de sus tres vertices esta zapeado
''' (<c>Render.EnsureZapIndexBuffer</c>, <c>MorphingHelper.RemoveZaps</c>). La version anterior
''' enmascaraba los 3 vertices de cada triangulo oculto, con lo cual lo BORRADO era lo oculto MAS UN
''' ANILLO: un hueco de un anillo de triangulos en todo el perimetro del parche, siempre, sin importar
''' el umbral. La regla correcta es la INVERSA — enmascarar un vertice solo si TODOS sus triangulos
''' incidentes estan ocultos — y con ella <c>borrados ⊆ ocultos</c>: si t tiene un vertice v
''' enmascarado, todo triangulo incidente a v esta oculto, y t es uno de ellos. Demostrado, no
''' argumentado.</para>
'''
''' <para><b>Cambios de calidad respecto de la version anterior:</b>
''' <br/>· <b>Muestreo 2 a 16 veces mas denso</b> (128/256/512/1024 rayos contra 64), y ademas
''' <b>gratis para el caso comun</b>: con el umbral en 1.0 alcanza UN rayo que escape para saber que
''' el vertice se ve, asi que se corta ahi. Lo caro se paga solo en los vertices que de verdad estan
''' tapados, que son la respuesta.
''' <br/>· <b>Auto-oclusion exacta</b>: se excluyen los triangulos INCIDENTES al vertice, por indice.
''' La <c>SelfMinDistance</c> anterior (5 % de la diagonal ≈ 6 unidades en un cuerpo) dejaba ciegas
''' axila, entrepierna, bajo-pecho y entre-dedos, y bloqueaba a distancia larga de forma arbitraria.
''' <br/>· <b>Se elimino el test de paridad "inside"</b>: aplicaba un predicado de superficie CERRADA
''' a una union de prendas ABIERTAS (cuello, mangas, cintura), donde no esta definido, y costaba SEIS
''' travesias completas del BVH sin early-exit por vertice — el camino mas caro de la corrida.
''' <br/>· <b>Transparencia resuelta POR PUNTO</b>, no por shape: en cada impacto se samplea la alpha
''' de la textura en el UV interpolado. El alpha-test descarta el fragmento por debajo de su
''' <c>AlphaTestRef</c> (el agujero del encaje no frena el rayo, la parte solida si), y el alpha-blend
''' aporta <c>alphaTextura x Alpha</c> a una TRANSMITANCIA acumulada a lo largo del rayo: una gasa al
''' 40 % deja pasar y dos capas de esa gasa tapan.
''' <br/>⛔ El flag <c>Decal</c> NO exime de tapar, y excluirlo fue un error: se copio el criterio del
''' pase de sombras, donde un decal se saltea porque es coplanar y solo aporta z-fighting a la
''' SILUETA. Para la VISIBILIDAD es al reves — si el material es opaco en ese punto, tapa lo que hay
''' detras, sea decal o no. Hay prendas enteras autoradas como decal.
''' <br/>· <b>Corte por distancia</b> tomado de la escena COMPLETA y aplicado tambien en el test de
''' caja, en vez de solo del BVH de ocluders.</para>
''' </summary>
Public Class OcclusionRaytracer

    Public Structure RaycastSettings
        ''' <summary>Rayos por vertice sobre el hemisferio de la normal.</summary>
        Public RayCount As Integer
        ''' <summary>Desplazamiento del ORIGEN del rayo a lo largo de la normal, en unidades NIF.
        ''' <para>⛔⛔ TIENE QUE SER MINIMO. Una prenda ajustada se apoya a 0,2-0,4 unidades de la piel:
        ''' con un bias de 0,5 —el default heredado— el origen del rayo queda DEL OTRO LADO DE LA TELA y
        ''' desde ahi TODOS los rayos escapan, asi que el torso bajo un vestido daba "visible". Las
        ''' botas, gruesas y separadas 1-2 unidades, seguian tapando: por eso el sintoma era "solo
        ''' enmascara las pantorrillas".</para>
        ''' <para>Ya no cumple ninguna funcion estructural: la auto-interseccion se excluye por
        ''' TOPOLOGIA (triangulos incidentes al vertice), no por distancia. Queda solo como escape
        ''' numerico.</para></summary>
        Public NormalBias As Single
        ''' <summary>Fraccion de rayos que tienen que chocar para considerar el vertice tapado
        ''' (0,5–1,0). 1,0 = certeza.</summary>
        Public OcclusionThreshold As Single
        ''' <summary>
        ''' ⭐⭐ ANGULO MINIMO SOBRE EL PLANO TANGENTE, en grados. Los rayos mas rasantes que esto NO se
        ''' disparan.
        ''' <para><b>Es el parametro que mas cambia el resultado, y la razon es geometrica.</b> El
        ''' hemisferio uniforme incluye direcciones casi paralelas a la piel. Sobre una zona plana o
        ''' convexa —la panza, el muslo, la espalda— esos rayos viajan pegados a la superficie durante
        ''' decenas de unidades y terminan saliendo por debajo del ruedo o por una bocamanga. Con el
        ''' umbral en 1,0 alcanza UNO para declarar el vertice visible, asi que una region perfectamente
        ''' tapada quedaba sin enmascarar por rayos que ningun observador real puede seguir.</para>
        ''' <para>0 = hemisferio completo (comportamiento anterior). 12 grados descarta el ~20 % mas
        ''' rasante del hemisferio.</para>
        ''' </summary>
        Public GrazingCutoffDeg As Single
        ''' <summary>
        ''' Distancia minima que la tela tiene que estar POR ENCIMA de la piel, en unidades NIF, medida
        ''' a lo largo de la normal del vertice. 0 = apagado.
        ''' <para>Un vertice tapado por una tela que le pasa a 0,02 unidades esta cubierto HOY: cualquier
        ''' slider, pose o fisica lo asoma. Este es el unico parametro que mira la holgura y no la
        ''' visibilidad, y en una prenda muy ajustada al cuerpo hay que dejarlo en 0 o no enmascara
        ''' nada.</para>
        ''' </summary>
        Public MinClearance As Single
        ''' <summary>Anillos de erosion EXTRA, ademas de la regla topologica. Cada uno mete el borde
        ''' del borrado un triangulo mas adentro de la zona cubierta.</summary>
        Public SafetyRings As Integer

        Public Shared Function Balanced() As RaycastSettings
            Return New RaycastSettings With {
                .RayCount = 256,
                .NormalBias = 0.01F,
                .OcclusionThreshold = 1.0F,
                .GrazingCutoffDeg = 12.0F,
                .MinClearance = 0.0F,
                .SafetyRings = 1
            }
        End Function
    End Structure

    ' ─── Tri storage (positions cached; AABB/BvhNode from BvhHelper) ──────────

    ''' <summary>Fraccion de luz que todavia pasa por debajo de la cual el rayo se considera frenado.
    ''' 0,15 = el 85 % detenido. ⛔ No es 0: exigir opacidad total dejaria sin enmascarar todo lo que
    ''' este bajo un material con el flag de blend puesto, que en la practica es opaco.</summary>
    Private Const TransmisionMinima As Single = 0.15F

    Private Structure TriData
        Public V0, V1, V2 As Vector3
        Public Centroid As Vector3
        Public Bounds As AABB
        ''' <summary>Indices de vertice DENTRO de la malla de origen. Solo se usan en el BVH propio
        ''' del objetivo, para saltear los triangulos incidentes al vertice que dispara.</summary>
        Public I0, I1, I2 As Integer
        ''' <summary>Indice en la tabla de materiales. -1 = opaco sin datos (el objetivo).</summary>
        Public Mat As Integer
        ''' <summary>UV por vertice y alpha de vertice: hacen falta para resolver la opacidad EN EL
        ''' PUNTO de impacto, interpolando con las baricentricas que devuelve Moller-Trumbore.</summary>
        Public UV0, UV1, UV2 As Vector2
        Public VA0, VA1, VA2 As Single
    End Structure

    ''' <summary>
    ''' Ley de opacidad de UN material, resuelta para poder evaluarla por PUNTO.
    ''' <para>⛔⛔ LA TRANSPARENCIA NO ES UNA PROPIEDAD DE LA SHAPE, ES DE CADA PUNTO. Una version
    ''' anterior descartaba la prenda ENTERA si el material traia alpha-blend o alpha-test. Eso esta mal
    ''' por los dos lados: un vestido opaco con el flag de blend puesto (o con Alpha 0,99, que basta
    ''' para que <c>HasAlphaBlend</c> de True) dejaba de tapar y la herramienta marcaba solo lo de las
    ''' botas; y un encaje que es opaco en el 70 % de su area quedaba reducido a "no tapa nada". Lo que
    ''' decide es la alpha de la TEXTURA en el UV del impacto, que es lo que mira el motor.</para>
    ''' </summary>
    Private NotInheritable Class MatInfo
        Public Opaco As Boolean
        Public UsaTest As Boolean
        Public Umbral As Single             ' AlphaTestRef / 255
        Public UsaBlend As Boolean
        Public AlphaMaterial As Single = 1.0F
        Public UsaAlphaVertice As Boolean
        Public UOff, VOff, UScale, VScale As Single
        Public Mapa As Byte()               ' RGBA8, se lee el canal A
        Public MapaW, MapaH As Integer

        ''' <summary>Alpha de la textura en (u,v). 1 si no hay mapa: el fallback del motor para un
        ''' sampler sin difusa es la BLANCA, no el negro.</summary>
        Public Function AlphaTextura(u As Single, v As Single) As Single
            If Mapa Is Nothing OrElse MapaW <= 0 OrElse MapaH <= 0 Then Return 1.0F
            Dim uu = u * UScale + UOff
            Dim vv = v * VScale + VOff
            uu -= CSng(Math.Floor(uu))
            vv -= CSng(Math.Floor(vv))
            Dim x = CInt(uu * (MapaW - 1))
            Dim y = CInt(vv * (MapaH - 1))
            If x < 0 Then x = 0
            If y < 0 Then y = 0
            If x >= MapaW Then x = MapaW - 1
            If y >= MapaH Then y = MapaH - 1
            Return Mapa((y * MapaW + x) * 4 + 3) / 255.0F
        End Function
    End Class

    ' ─── Instance state ──────────────────────────────────────────────────────

    Private ReadOnly _tris As TriData()
    Private ReadOnly _root As BvhNode
    Private ReadOnly _bMin As Vector3
    Private ReadOnly _bMax As Vector3

    ''' <summary>Cuentas de la ULTIMA corrida, etapa por etapa. ⛔ Es un INSTRUMENTO, no estado: nadie
    ''' decide nada con esto. Existe porque "enmascara poco" tiene dos causas que se arreglan en lugares
    ''' opuestos —el trazado no vio la region tapada, o la erosion se la comio— y sin separarlas hay que
    ''' adivinar. Con un parche angosto (el empeine bajo una bota) la erosion topologica mas un anillo
    ''' pueden borrarlo entero, y desde afuera se lee igual que si los rayos hubieran fallado.</summary>
    Public ReadOnly Property LastRayHidden As Integer
    Public ReadOnly Property LastAfterTopology As Integer
    Public ReadOnly Property LastAfterRings As Integer
    Private ReadOnly _mats As New List(Of MatInfo)
    ''' <summary>Cache de mapas de alpha decodificados, por ruta. Se decodifica a 256x256 (DecodeDds
    ''' acepta un mip preferido): alcanza de sobra para decidir "hay tela o hay agujero" y baja el costo
    ''' dos ordenes de magnitud contra la textura completa.</summary>
    Private Shared ReadOnly _cacheAlpha As New Dictionary(Of String, FaceTintCpuCompositor.DecodedTex)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Construye el BVH con las mallas que el CALLER decidio que tapan.
    ''' <para>⛔ ACA NO SE FILTRA POR MATERIAL, y filtrar fue un error medido. El predicado
    ''' <c>HasAlphaBlend</c> es <c>AlphaBlendEnabled OrElse Alpha &lt; 1</c>, y muchas prendas opacas a
    ''' la vista traen ese flag o un Alpha de 0,99: descartandolas en silencio, un vestido dejaba de
    ''' tapar y la herramienta marcaba solo lo de las botas, sin una pista de por que. La decision vive
    ''' en la lista con casillas del dialogo, donde se VE.</para></summary>
    Public Sub New(occluderMeshes As IEnumerable(Of PreviewModel.RenderableMesh))
        Dim triList As New List(Of TriData)
        Dim mn = New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim mx = New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)

        For Each mesh In occluderMeshes
            If mesh Is Nothing OrElse mesh.MeshData Is Nothing Then Continue For
            _mats.Add(ResolverMaterial(mesh))
            AppendMeshTris(triList, mesh, mn, mx, _mats.Count - 1)
        Next

        _tris = triList.ToArray()
        _bMin = mn
        _bMax = mx
        If _tris.Length > 0 Then _root = BuildBvhFromTris(_tris)
    End Sub

    ''' <summary>Traduce el material de la malla a la ley que se evalua por punto. Es la MISMA lectura
    ''' que hace el pase de profundidad del render (Render.vb, RenderDepthOnly): mismo AlphaTestRef,
    ''' mismo escalar Alpha, mismo gate de alpha de vertice, misma transformacion de UV.</summary>
    Private Shared Function ResolverMaterial(mesh As PreviewModel.RenderableMesh) As MatInfo
        Dim r As New MatInfo With {.Opaco = True, .UScale = 1.0F, .VScale = 1.0F}
        Dim mat = mesh.MeshData.Material
        If mat Is Nothing Then Return r
        Dim mb = mat.MaterialBase

        r.UsaTest = mat.HasAlphaTest
        r.UsaBlend = mat.HasAlphaBlend

        ' ⛔ `MaterialData.UseVertexAlpha` es Friend de la libreria, asi que el predicado se rearma aca
        ' con la MISMA ley (Render.vb:2408-2431): hay alpha de vertice si la shape muestra color de
        ' vertice Y la geometria lo tiene, salvo en Tree/TreeAnim, que usan ese canal para otra cosa.
        Dim shp = mesh.MeshData.Shape
        Dim geo = mesh.MeshData.Meshgeometry.Geometry
        Dim usaVC = shp IsNot Nothing AndAlso shp.ShowVertexColor AndAlso
                    geo IsNot Nothing AndAlso geo.HasVertexColors
        r.UsaAlphaVertice = usaVC AndAlso
                            (mb Is Nothing OrElse Not (mb.Tree OrElse mb.NifShaderType = NiflySharp.Enums.BSLightingShaderType.TreeAnim))
        If mb IsNot Nothing Then
            r.Umbral = mb.AlphaTestRef / 255.0F
            r.AlphaMaterial = mb.Alpha
            r.UOff = mb.UOffset : r.VOff = mb.VOffset
            r.UScale = mb.UScale : r.VScale = mb.VScale
        End If
        r.Opaco = Not r.UsaTest AndAlso Not r.UsaBlend
        If r.Opaco Then Return r

        ' Solo se decodifica la difusa de lo que NO es opaco: un proyecto de tela opaca no toca disco.
        If mb IsNot Nothing Then
            Dim ruta = FO4UnifiedMaterial_Class.CorrectTexturePath(mb.Diffuse_or_Base_Texture)
            Dim tex = CargarAlpha(ruta)
            If tex IsNot Nothing Then
                r.Mapa = tex.Rgba8
                r.MapaW = tex.Width
                r.MapaH = tex.Height
            End If
        End If
        Return r
    End Function

    Private Shared Function CargarAlpha(ruta As String) As FaceTintCpuCompositor.DecodedTex
        If String.IsNullOrWhiteSpace(ruta) Then Return Nothing
        Dim clave = FaceTintInputBuilder.NormalizeDictionaryKeyWithTexturesPrefix(ruta)
        SyncLock _cacheAlpha
            Dim hit As FaceTintCpuCompositor.DecodedTex = Nothing
            If _cacheAlpha.TryGetValue(clave, hit) Then Return hit
        End SyncLock

        Dim tex As FaceTintCpuCompositor.DecodedTex = Nothing
        Try
            Dim loc As FilesDictionary_class.File_Location = Nothing
            If FilesDictionary_class.Dictionary.TryGetValue(clave, loc) Then
                Dim bytes = loc.GetBytes()
                If bytes IsNot Nothing AndAlso bytes.Length > 0 Then tex = FaceTintCpuCompositor.DecodeDds(bytes, 256, 256)
            End If
        Catch
            tex = Nothing
        End Try

        SyncLock _cacheAlpha
            _cacheAlpha(clave) = tex
        End SyncLock
        Return tex
    End Function

    ''' <summary>Opacidad EN EL PUNTO de impacto, con las baricentricas del rayo. 0 = el motor descarta
    ''' ese fragmento (agujero del encaje) y no tapa; 1 = tapa del todo.</summary>
    Private Function OpacidadEnImpacto(ByRef td As TriData, bu As Single, bv As Single) As Single
        If td.Mat < 0 OrElse td.Mat >= _mats.Count Then Return 1.0F
        Dim mi = _mats(td.Mat)
        If mi.Opaco Then Return 1.0F

        Dim w = 1.0F - bu - bv
        Dim u = td.UV0.X * w + td.UV1.X * bu + td.UV2.X * bv
        Dim v = td.UV0.Y * w + td.UV1.Y * bu + td.UV2.Y * bv
        Dim a = mi.AlphaTextura(u, v)
        If mi.UsaAlphaVertice Then a *= (td.VA0 * w + td.VA1 * bu + td.VA2 * bv)

        If mi.UsaTest Then
            ' ⛔ En SSE el escalar Alpha entra DENTRO del test; en FO4 se aplica despues. Misma
            ' divergencia que documenta el fragment de profundidad del render.
            Dim aTest = If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, a * mi.AlphaMaterial, a)
            If aTest < mi.Umbral Then Return 0.0F
        End If
        If mi.UsaBlend Then Return Math.Max(0.0F, Math.Min(1.0F, a * mi.AlphaMaterial))
        Return 1.0F
    End Function

    Public ReadOnly Property HasOccluders As Boolean
        Get
            Return _root IsNot Nothing
        End Get
    End Property

    ''' <summary>Cuantos ocluders quedaron despues del filtro de material. Sirve para avisar
    ''' "no hay ocluders opacos" en vez de devolver un resultado vacio que parece un fallo.</summary>
    Public ReadOnly Property OccluderTriangleCount As Integer
        Get
            Return If(_tris Is Nothing, 0, _tris.Length)
        End Get
    End Property

    ' ─── Mesh ingestion ──────────────────────────────────────────────────────

    Private Shared Sub AppendMeshTris(triList As List(Of TriData), mesh As PreviewModel.RenderableMesh,
                                      ByRef mn As Vector3, ByRef mx As Vector3, matIdx As Integer)
        Dim verts = If(mesh?.MeshData?.Meshgeometry.Vertices IsNot Nothing, SkinningHelper.GetWorldVertices(mesh.MeshData.Meshgeometry), Nothing)
        Dim idx = mesh?.MeshData?.Meshgeometry.Indices
        If verts Is Nothing OrElse idx Is Nothing OrElse idx.Length < 3 Then Exit Sub
        Dim uvs = mesh.MeshData.Meshgeometry.Uvs_Weight
        Dim cols = mesh.MeshData.Meshgeometry.VertexColors

        Dim i = 0
        While i + 2 < idx.Length
            Dim a = CInt(idx(i)), b = CInt(idx(i + 1)), c = CInt(idx(i + 2))
            Dim v0 = ToV3(verts(a))
            Dim v1 = ToV3(verts(b))
            Dim v2 = ToV3(verts(c))
            Dim td As TriData
            td.V0 = v0 : td.V1 = v1 : td.V2 = v2
            td.I0 = a : td.I1 = b : td.I2 = c
            td.Mat = matIdx
            If uvs IsNot Nothing AndAlso c < uvs.Length Then
                td.UV0 = New Vector2(uvs(a).X, uvs(a).Y)
                td.UV1 = New Vector2(uvs(b).X, uvs(b).Y)
                td.UV2 = New Vector2(uvs(c).X, uvs(c).Y)
            End If
            td.VA0 = 1.0F : td.VA1 = 1.0F : td.VA2 = 1.0F
            If cols IsNot Nothing AndAlso c < cols.Length Then
                td.VA0 = CSng(cols(a).W) : td.VA1 = CSng(cols(b).W) : td.VA2 = CSng(cols(c).W)
            End If
            td.Centroid = (v0 + v1 + v2) / 3.0F
            td.Bounds = AABB.FromTriangle(v0, v1, v2)
            triList.Add(td)
            mn = Vector3.ComponentMin(mn, td.Bounds.Min)
            mx = Vector3.ComponentMax(mx, td.Bounds.Max)
            i += 3
        End While
    End Sub

    ''' <summary>Extracts bounds/centroids from a TriData array and delegates to BvhHelper.BuildBvh.</summary>
    Private Shared Function BuildBvhFromTris(tris As TriData()) As BvhNode
        Dim bounds(tris.Length - 1) As AABB
        Dim centroids(tris.Length - 1) As Vector3
        For i = 0 To tris.Length - 1
            bounds(i) = tris(i).Bounds
            centroids(i) = tris(i).Centroid
        Next
        Dim indices = Enumerable.Range(0, tris.Length).ToArray()
        Return BvhHelper.BuildBvh(bounds, centroids, indices, 0, indices.Length, 0)
    End Function

    ' ─── Ray–BVH traversal ───────────────────────────────────────────────────

    ''' <summary>
    ''' Pila de recorrido del BVH, UNA POR HILO y reusada entre rayos.
    ''' Antes se alocaba una Stack nueva en CADA rayo: del orden de 1,5 millones de allocations por
    ''' corrida, todas del mismo tamano y con la vida del rayo. El recorrido es estrictamente local a
    ''' la llamada, asi que reusar el buffer no cambia ningun resultado.
    ''' El Clear() al entrar NO es opcional: una salida temprana (el Return del primer impacto) deja
    ''' nodos adentro y el rayo siguiente arrancaria con basura.
    ''' </summary>
    <ThreadStatic>
    Private Shared _pilaBvh As Stack(Of BvhNode)

    Private Shared Function PilaDeRecorrido() As Stack(Of BvhNode)
        If _pilaBvh Is Nothing Then _pilaBvh = New Stack(Of BvhNode)(64)
        Return _pilaBvh
    End Function

    ''' <summary>Test de caja contra el rayo que SI respeta el corte por distancia.
    ''' <para>⛔ <c>AABB.Intersects</c> (BvhHelper) ignora <c>maxDist</c>: acepta cualquier caja que el
    ''' rayo cruce, por lejos que este, y el recorrido baja a hojas que no pueden aportar. No se toca
    ''' esa funcion porque <c>ConformHelper</c> la comparte; el test corregido vive aca.</para></summary>
    Private Shared Function CajaCortada(ByRef b As AABB, ByRef orig As Vector3, ByRef dirInv As Vector3, maxDist As Single) As Boolean
        Dim tx1 = (b.Min.X - orig.X) * dirInv.X
        Dim tx2 = (b.Max.X - orig.X) * dirInv.X
        Dim tmin = Math.Min(tx1, tx2)
        Dim tmax = Math.Max(tx1, tx2)

        Dim ty1 = (b.Min.Y - orig.Y) * dirInv.Y
        Dim ty2 = (b.Max.Y - orig.Y) * dirInv.Y
        tmin = Math.Max(tmin, Math.Min(ty1, ty2))
        tmax = Math.Min(tmax, Math.Max(ty1, ty2))

        Dim tz1 = (b.Min.Z - orig.Z) * dirInv.Z
        Dim tz2 = (b.Max.Z - orig.Z) * dirInv.Z
        tmin = Math.Max(tmin, Math.Min(tz1, tz2))
        tmax = Math.Min(tmax, Math.Max(tz1, tz2))

        Return tmax >= tmin AndAlso tmax > 0.0F AndAlso tmin <= maxDist
    End Function

    ''' <summary>
    ''' True si el rayo choca con algun triangulo de [root/tris] a distancia 0 &lt; t &lt;= maxDist.
    ''' <paramref name="vertExcluido"/> &gt;= 0 saltea los triangulos que TOCAN ese vertice: es la
    ''' exclusion exacta de auto-interseccion, y reemplaza a la vieja distancia minima metrica.
    ''' </summary>
    Private Function RayBloqueado(root As BvhNode, tris As TriData(),
                                  orig As Vector3, dir As Vector3,
                                  maxDist As Single, vertExcluido As Integer) As Boolean
        If root Is Nothing Then Return False

        Dim dirInv = SafeInv(dir)
        Dim stack = PilaDeRecorrido()
        stack.Clear()
        stack.Push(root)
        Dim transmision As Single = 1.0F

        While stack.Count > 0
            Dim n = stack.Pop()
            If Not CajaCortada(n.Bounds, orig, dirInv, maxDist) Then Continue While

            If n.IsLeaf Then
                For Each ti In n.LeafIndices
                    If vertExcluido >= 0 Then
                        Dim td = tris(ti)
                        If td.I0 = vertExcluido OrElse td.I1 = vertExcluido OrElse td.I2 = vertExcluido Then Continue For
                    End If
                    Dim t As Single, bu As Single, bv As Single
                    If Not MollerTrumbore(orig, dir, tris(ti), t, bu, bv) Then Continue For
                    If t <= 0.0F OrElse t > maxDist Then Continue For

                    Dim op = OpacidadEnImpacto(tris(ti), bu, bv)
                    ' Agujero de encaje / fragmento descartado por el alpha test: no frena nada.
                    If op <= 0.0F Then Continue For
                    ' Opaco: corta en el acto, que es el 99 % de los casos y el camino rapido.
                    If op >= 1.0F Then Return True
                    transmision *= (1.0F - op)
                    If transmision <= TransmisionMinima Then Return True
                Next
            Else
                If n.Left IsNot Nothing Then stack.Push(n.Left)
                If n.Right IsNot Nothing Then stack.Push(n.Right)
            End If
        End While

        Return transmision <= TransmisionMinima
    End Function

    ''' <summary>Distancia al impacto mas cercano, o -1 si no hay ninguno. Se usa solo para la holgura
    ''' minima, y solo sobre vertices que YA salieron tapados: no entra en el camino caliente.</summary>
    Private Function RayNearestHit(root As BvhNode, tris As TriData(),
                                          orig As Vector3, dir As Vector3, maxDist As Single) As Single
        If root Is Nothing Then Return -1.0F
        Dim dirInv = SafeInv(dir)
        Dim stack = PilaDeRecorrido()
        stack.Clear()
        stack.Push(root)
        Dim mejor As Single = Single.MaxValue

        While stack.Count > 0
            Dim n = stack.Pop()
            If Not CajaCortada(n.Bounds, orig, dirInv, maxDist) Then Continue While
            If n.IsLeaf Then
                For Each ti In n.LeafIndices
                    Dim t As Single
                    Dim bu As Single, bv As Single
                    If MollerTrumbore(orig, dir, tris(ti), t, bu, bv) AndAlso t > 0.0F AndAlso t < mejor Then mejor = t
                Next
            Else
                If n.Left IsNot Nothing Then stack.Push(n.Left)
                If n.Right IsNot Nothing Then stack.Push(n.Right)
            End If
        End While

        If mejor = Single.MaxValue Then Return -1.0F
        Return mejor
    End Function

    ' ─── Geometry helpers ────────────────────────────────────────────────────

    ''' <param name="bu">Baricentricas del impacto. Con ellas se interpolan UV y alpha de vertice para
    ''' resolver la opacidad EN EL PUNTO, que es lo que decide si el rayo pasa o no.</param>
    Private Shared Function MollerTrumbore(orig As Vector3, dir As Vector3, tri As TriData, ByRef t As Single,
                                           ByRef bu As Single, ByRef bv As Single) As Boolean
        ' ⛔ EPS de ARISTA, no solo de paralelismo. Con `u < 0` estricto, un rayo que pasa exactamente
        ' por la arista compartida entre dos triangulos puede fallar LOS DOS y colarse por una fisura
        ' que no existe. Con el umbral en 1.0 esa fuga alcanza para declarar visible un vertice tapado
        ' y dejar cuerpo suelto bajo la ropa. La tolerancia hace que la arista pertenezca a los dos.
        Const EPS As Single = 1e-7F
        Const BORDE As Single = 1e-6F
        Dim edge1 = tri.V1 - tri.V0
        Dim edge2 = tri.V2 - tri.V0
        Dim h = Vector3.Cross(dir, edge2)
        Dim a = Vector3.Dot(edge1, h)
        If Math.Abs(a) < EPS Then t = 0 : Return False
        Dim f = 1.0F / a
        Dim s = orig - tri.V0
        Dim u = f * Vector3.Dot(s, h)
        If u < -BORDE OrElse u > 1.0F + BORDE Then t = 0 : Return False
        Dim q = Vector3.Cross(s, edge1)
        Dim v = f * Vector3.Dot(dir, q)
        If v < -BORDE OrElse u + v > 1.0F + BORDE Then t = 0 : Return False
        t = f * Vector3.Dot(edge2, q)
        bu = u
        bv = v
        Return True
    End Function

    Private Shared Function AlignToNormal(dir As Vector3, normal As Vector3) As Vector3
        Dim n = normal
        If n.LengthSquared < 0.0001F Then n = Vector3.UnitY
        n.Normalize()
        Dim arbitrary = If(Math.Abs(n.X) < 0.9F, Vector3.UnitX, Vector3.UnitY)
        Dim right = Vector3.Cross(n, arbitrary)
        right.Normalize()
        Dim fwd = Vector3.Cross(right, n)
        Return right * dir.X + n * dir.Y + fwd * dir.Z
    End Function

    ''' <summary>Hemisferio uniforme por AREA (Arquimedes: y uniforme ⇒ solido uniforme), con espiral
    ''' aurea. Determinista: dos corridas identicas dan el mismo conjunto, bit a bit.</summary>
    Private Shared Function BuildHemisphereDirs(count As Integer, grazingDeg As Single) As Vector3()
        Dim dirs(count - 1) As Vector3
        Dim goldenAngle = Math.PI * (3.0 - Math.Sqrt(5.0))
        ' El casquete arranca en sin(corte) en vez de en 0: se reparten uniformemente por AREA sobre el
        ' casquete util, no sobre el hemisferio entero. Asi el corte no solo elimina rayos, sino que
        ' REDISTRIBUYE los que quedan — con el mismo presupuesto se muestrea mas fino donde importa.
        Dim yMin = Math.Sin(Math.Max(0.0, Math.Min(80.0, grazingDeg)) * Math.PI / 180.0)
        For i = 0 To count - 1
            Dim y = 1.0 - (1.0 - yMin) * (i + 0.5) / count
            Dim r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y))
            Dim theta = goldenAngle * i
            dirs(i) = New Vector3(CSng(r * Math.Cos(theta)), CSng(y), CSng(r * Math.Sin(theta)))
        Next
        Return dirs
    End Function

    Private Shared Function SafeInv(dir As Vector3) As Vector3
        Return New Vector3(
            If(Math.Abs(dir.X) > 1e-7F, 1.0F / dir.X, Single.MaxValue),
            If(Math.Abs(dir.Y) > 1e-7F, 1.0F / dir.Y, Single.MaxValue),
            If(Math.Abs(dir.Z) > 1e-7F, 1.0F / dir.Z, Single.MaxValue))
    End Function

    Private Shared Function ToV3(v As Vector3d) As Vector3
        Return New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z))
    End Function

    ' ─── Main entry point ────────────────────────────────────────────────────

    ''' <summary>
    ''' Devuelve los indices de vertice de targetMesh que quedan tapados.
    ''' Por rayo: se prueba contra el BVH de ocluders externos y, si escapa, contra el BVH del PROPIO
    ''' objetivo salteando los triangulos incidentes al vertice (torso tapado por su propia pierna
    ''' debajo del ruedo, axila, entrepierna).
    ''' Corre en paralelo; llamar desde una Task de fondo.
    ''' </summary>
    Public Function ComputeOccludedVertices(
            targetMesh As PreviewModel.RenderableMesh,
            settings As RaycastSettings,
            progress As IProgress(Of Integer),
            ct As CancellationToken) As HashSet(Of Integer)

        Dim result As New HashSet(Of Integer)
        If _root Is Nothing Then Return result

        Dim verts = If(targetMesh?.MeshData?.Meshgeometry.Vertices IsNot Nothing, SkinningHelper.GetWorldVertices(targetMesh.MeshData.Meshgeometry), Nothing)
        Dim norms = If(targetMesh?.MeshData?.Meshgeometry.Normals IsNot Nothing, SkinningHelper.GetWorldNormals(targetMesh.MeshData.Meshgeometry), Nothing)
        Dim triIndices = targetMesh?.MeshData?.Meshgeometry.Indices
        If verts Is Nothing OrElse norms Is Nothing OrElse verts.Length = 0 OrElse triIndices Is Nothing Then Return result

        Dim vertCount = verts.Length

        ' BVH propio del objetivo (auto-oclusion). Siempre: no es una opcion, es parte de la pregunta.
        Dim selfList As New List(Of TriData)
        Dim smn = New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim smx = New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)
        AppendMeshTris(selfList, targetMesh, smn, smx, -1)
        Dim selfTris = selfList.ToArray()
        Dim selfRoot As BvhNode = Nothing
        If selfTris.Length > 0 Then selfRoot = BuildBvhFromTris(selfTris)

        ' ⛔ El corte sale de la escena COMPLETA (ocluders ∪ objetivo). Antes salia solo del BVH de
        ' ocluders: con un ocluder chico —un guante— el corte quedaba en decenas de unidades y recortaba
        ' rayos que tenian que seguir viaje, dejando de contar impactos legitimos.
        Dim eMin = Vector3.ComponentMin(_bMin, smn)
        Dim eMax = Vector3.ComponentMax(_bMax, smx)
        Dim maxDist = (eMax - eMin).Length * 2.0F
        If maxDist < 1.0F Then maxDist = 10000.0F

        Dim hemisphereDirs = BuildHemisphereDirs(settings.RayCount, settings.GrazingCutoffDeg)
        Dim thresholdHits = CInt(Math.Ceiling(settings.OcclusionThreshold * settings.RayCount))
        ' Cuantos escapes bastan para declararlo VISIBLE. Con umbral 1.0 es UNO: por eso el caso comun
        ' —un vertice a la vista— cuesta uno o dos rayos y no 1024. Es lo que hace pagable el muestreo
        ' denso: lo caro se gasta solo en los vertices que de verdad estan tapados.
        Dim escapesParaVisible = settings.RayCount - thresholdHits + 1

        Dim occluded(vertCount - 1) As Boolean
        Dim processed As Integer = 0

        Dim opts = New ParallelOptions With {.CancellationToken = ct}
        Try
            Parallel.For(0, vertCount, opts,
                Sub(vi)
                    Dim pos = ToV3(verts(vi))
                    Dim norm = ToV3(norms(vi))
                    If norm.LengthSquared < 0.0001F Then norm = Vector3.UnitY Else norm.Normalize()
                    Dim biased = pos + norm * settings.NormalBias

                    Dim escapes = 0
                    For Each hd In hemisphereDirs
                        Dim worldDir = AlignToNormal(hd, norm)

                        Dim blocked = RayBloqueado(_root, _tris, biased, worldDir, maxDist, -1)
                        If Not blocked AndAlso selfRoot IsNot Nothing Then
                            blocked = RayBloqueado(selfRoot, selfTris, biased, worldDir, maxDist, vi)
                        End If

                        If Not blocked Then
                            escapes += 1
                            If escapes >= escapesParaVisible Then Exit For
                        End If
                    Next
                    Dim tapado = (escapes < escapesParaVisible)

                    ' Holgura minima: la tela tiene que estar al menos a esta distancia POR ENCIMA de la
                    ' piel. Se evalua solo si el vertice ya salio tapado, asi que no cuesta nada en el
                    ' caso comun.
                    If tapado AndAlso settings.MinClearance > 0.0F Then
                        Dim d = RayNearestHit(_root, _tris, biased, norm, maxDist)
                        If d >= 0.0F AndAlso d < settings.MinClearance Then tapado = False
                    End If

                    occluded(vi) = tapado

                    Dim p = Interlocked.Increment(processed)
                    If p Mod 256 = 0 Then progress?.Report(CInt(p * 100L / vertCount))
                End Sub)
        Catch ex As OperationCanceledException
            Return result
        End Try

        ' ═══ EROSION TOPOLOGICA — ver el comentario de la clase ═══
        Dim nt = triIndices.Length \ 3
        Dim triOculto(Math.Max(0, nt - 1)) As Boolean
        For t = 0 To nt - 1
            Dim i0 = CInt(triIndices(t * 3))
            Dim i1 = CInt(triIndices(t * 3 + 1))
            Dim i2 = CInt(triIndices(t * 3 + 2))
            triOculto(t) = occluded(i0) AndAlso occluded(i1) AndAlso occluded(i2)
        Next

        ' Un vertice se enmascara solo si TODOS sus triangulos incidentes estan ocultos.
        Dim incidentes(vertCount - 1) As Integer
        Dim ocultosInc(vertCount - 1) As Integer
        For t = 0 To nt - 1
            For k = 0 To 2
                Dim v = CInt(triIndices(t * 3 + k))
                incidentes(v) += 1
                If triOculto(t) Then ocultosInc(v) += 1
            Next
        Next

        Dim mask(vertCount - 1) As Boolean
        For v = 0 To vertCount - 1
            mask(v) = incidentes(v) > 0 AndAlso incidentes(v) = ocultosInc(v)
        Next

        Dim nRayos = 0, nTopo = 0
        For v = 0 To vertCount - 1
            If occluded(v) Then nRayos += 1
            If mask(v) Then nTopo += 1
        Next
        _LastRayHidden = nRayos
        _LastAfterTopology = nTopo

        ' Anillos extra: cada pasada saca los vertices enmascarados que comparten triangulo con uno que
        ' no lo esta, metiendo el borde del borrado mas adentro de la zona cubierta. Se decide contra
        ' una copia por pasada: erosionar in situ propagaria el anillo en cascada dentro del barrido.
        For anillo = 1 To Math.Max(0, settings.SafetyRings)
            Dim tocaBorde(vertCount - 1) As Boolean
            For t = 0 To nt - 1
                Dim i0 = CInt(triIndices(t * 3))
                Dim i1 = CInt(triIndices(t * 3 + 1))
                Dim i2 = CInt(triIndices(t * 3 + 2))
                If mask(i0) AndAlso mask(i1) AndAlso mask(i2) Then Continue For
                If mask(i0) Then tocaBorde(i0) = True
                If mask(i1) Then tocaBorde(i1) = True
                If mask(i2) Then tocaBorde(i2) = True
            Next
            For v = 0 To vertCount - 1
                If tocaBorde(v) Then mask(v) = False
            Next
        Next

        For v = 0 To vertCount - 1
            If mask(v) Then result.Add(v)
        Next
        _LastAfterRings = result.Count

        progress?.Report(100)
        Return result
    End Function

End Class
