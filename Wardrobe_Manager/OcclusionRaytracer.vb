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
''' de la textura en el UV interpolado y se evalua la COBERTURA DEL MOTOR de esa shape en el juego
''' activo (<c>MaterialData.EngineColourCoverage</c>, el dueno unico en la libreria: los mismos cortes,
''' umbrales float32, alpha de vertice y de material, blend y UV que suben el pase de color y el
''' z-prepass). El agujero del encaje no frena el rayo, la parte solida si, y un dibujo con blend deja
''' pasar lo que su factor de destino conserva, acumulado en una TRANSMITANCIA a lo largo del rayo: una
''' gasa al 40 % deja pasar y dos capas de esa gasa tapan.
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
        ''' <summary>Luz que todavia pasa (producto de las transmisiones de las capas con blend que el rayo cruza) a
        ''' partir de la cual el rayo se considera frenado: frenado si transmision &lt;= MinTransmission. 0,15 por
        ''' defecto = el 85 % detenido.
        ''' <para>⛔ CRITERIO DEL USUARIO DE LA HERRAMIENTA, no ley del motor (decision del usuario 19-oct-2026, r19e-02): la
        ''' transmision de CADA capa sale de la ley (EngineCoverage.Transmittance sobre el alfa de salida del motor); esto
        ''' solo decide cuanta luz acumulada cuenta como "tapado". Con 0 solo tapa lo totalmente opaco: lo que este bajo
        ''' un blend casi opaco no se enmascara nunca.</para></summary>
        Public MinTransmission As Single
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
                .MinTransmission = 0.15F,
                .GrazingCutoffDeg = 12.0F,
                .MinClearance = 0.0F,
                .SafetyRings = 1
            }
        End Function
    End Structure

    ' ─── Tri storage (positions cached; AABB/BvhNode from BvhHelper) ──────────

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
    ''' La cobertura de UN material, resuelta para poder evaluarla por PUNTO.
    ''' <para>⛔⛔ LA TRANSPARENCIA NO ES UNA PROPIEDAD DE LA SHAPE, ES DE CADA PUNTO. Una version
    ''' anterior descartaba la prenda ENTERA si el material traia alpha-blend o alpha-test. Eso esta mal
    ''' por los dos lados: un vestido opaco con el flag de blend puesto (o con Alpha 0,99, que basta
    ''' para que <c>HasAlphaBlend</c> de True) dejaba de tapar y la herramienta marcaba solo lo de las
    ''' botas; y un encaje que es opaco en el 70 % de su area quedaba reducido a "no tapa nada". Lo que
    ''' decide es la alpha de la TEXTURA en el UV del impacto, que es lo que mira el motor.</para>
    ''' <para>La ley es la del motor y vive en la libreria (<c>MaterialData.EngineColourCoverage</c>); aca
    ''' solo vive el mapa de alpha decodificado y su muestreo.</para>
    ''' </summary>
    Private NotInheritable Class MatInfo
        ''' <summary>False = la malla no tiene material: el estado opaco liso del render
        ''' (<c>ResolveColourDepthState</c>), tapa.</summary>
        Public TieneCobertura As Boolean
        Public Cobertura As PreviewModel.RenderableMesh.MaterialData.EngineCoverage
        Public Mapa As Byte()               ' RGBA8, se lee el canal A
        Public MapaW, MapaH As Integer

        ''' <summary>Alpha de la difusa en (u,v): la UV del motor (escala y despues offset, <c>RestUv</c>), su
        ''' modo de direccionamiento por eje (<c>SamplerLaw</c>: wrap o clamp) y muestreo puntual D3D11 (texel =
        ''' floor(u * ancho)) sobre el mip de 256 que decodifica <c>CargarAlpha</c>. 1 si no hay mapa: el default
        ''' del motor para una difusa vacia o faltante es opaco en alpha (<c>EngineDefaultTextureLaw.Rgba</c>:
        ''' DiffuseMap / DefNormalMap, A = 255).</summary>
        Public Function AlphaTextura(u As Single, v As Single) As Single
            If Mapa Is Nothing OrElse MapaW <= 0 OrElse MapaH <= 0 Then Return 1.0F
            Dim uu = u * Cobertura.UvScale.X + Cobertura.UvOffset.X
            Dim vv = v * Cobertura.UvScale.Y + Cobertura.UvOffset.Y
            uu = If(Cobertura.WrapU, uu - CSng(Math.Floor(uu)), Math.Min(1.0F, Math.Max(0.0F, uu)))
            vv = If(Cobertura.WrapV, vv - CSng(Math.Floor(vv)), Math.Min(1.0F, Math.Max(0.0F, vv)))
            Dim x = Math.Min(MapaW - 1, CInt(Math.Floor(uu * MapaW)))
            Dim y = Math.Min(MapaH - 1, CInt(Math.Floor(vv * MapaH)))
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
    ''' <summary>La cobertura del OBJETIVO en el juego (su lista y su orden dentro de ella, para
    ''' <c>EngineCoverage.DrawsAfter</c>). Nothing = sin material: ningun orden relativo es conocido.</summary>
    Private _coberturaObjetivo As PreviewModel.RenderableMesh.MaterialData.EngineCoverage?
    ''' <summary>Las mallas del BVH, en el orden de su indice de material (TriData.Mat), y el objetivo: RefrescarCoberturas las relee.</summary>
    Private ReadOnly _ocluders As New List(Of PreviewModel.RenderableMesh)
    Private ReadOnly _objetivo As PreviewModel.RenderableMesh
    ''' <summary>Cuantas formas (objetivo incluido) de la ultima RefrescarCoberturas no tienen su orden dentro de la lista
    ''' (EngineCoverage.OrderInputsRead False: claves del render viejas): ahi el orden se toma como desconocido (minimo).</summary>
    Public ReadOnly Property LastOrderUnknown As Integer
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
    Public Sub New(occluderMeshes As IEnumerable(Of PreviewModel.RenderableMesh), targetMesh As PreviewModel.RenderableMesh)
        Dim triList As New List(Of TriData)
        Dim mn = New Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue)
        Dim mx = New Vector3(Single.MinValue, Single.MinValue, Single.MinValue)
        _objetivo = targetMesh
        For Each mesh In occluderMeshes
            If mesh Is Nothing OrElse mesh.MeshData Is Nothing Then Continue For
            _ocluders.Add(mesh)
            AppendMeshTris(triList, mesh, mn, mx, _ocluders.Count - 1)
        Next
        ' Las coberturas NO se arman aca: dependen del orden de dibujo que el render tenga al momento de correr
        ' (claves de RebuildRenderBuckets). Las arma RefrescarCoberturas, que el dialogo llama en cada Run.

        _tris = triList.ToArray()
        _bMin = mn
        _bMax = mx
        If _tris.Length > 0 Then _root = BuildBvhFromTris(_tris)
    End Sub

    ''' <summary>Arma la cobertura del objetivo y de cada ocluder con el estado ACTUAL del render (el orden de dibujo y
    ''' las claves de la lista). Se llama en el hilo del dialogo antes de cada corrida, nunca desde la Task.
    ''' <para>Las texturas de alpha salen de la cache compartida: rearmar no vuelve a tocar disco.</para></summary>
    Public Sub RefrescarCoberturas()
        Dim isSse = Config_App.Current.Game = Config_App.Game_Enum.Skyrim
        Dim sinOrden = 0
        _coberturaObjetivo = Nothing
        Dim matObjetivo = _objetivo?.MeshData?.Material
        If matObjetivo IsNot Nothing AndAlso matObjetivo.MaterialBase IsNot Nothing Then
            _coberturaObjetivo = matObjetivo.EngineColourCoverage(isSse)
            If Not _coberturaObjetivo.Value.OrderInputsRead Then sinOrden += 1
        End If
        _mats.Clear()
        For Each mesh In _ocluders
            Dim mi = ResolverMaterial(mesh, isSse)
            If mi.TieneCobertura AndAlso Not mi.Cobertura.OrderInputsRead Then sinOrden += 1
            _mats.Add(mi)
        Next
        _LastOrderUnknown = sinOrden
    End Sub

    ''' <summary>La cobertura de la malla EN EL JUEGO ACTIVO tal como la dibuja el motor visto desde fuera: la del
    ''' PASE DE COLOR y la del z-prepass que lo precede, que es lo que el jugador ve — NO la del pase de sombras
    ''' (copiar su criterio fue el error de los decal) ni la del pase de profundidad del preview. Se lee del dueno
    ''' unico de la libreria (<c>MaterialData.EngineColourCoverage</c>): los mismos cortes, umbrales float32, alpha
    ''' de vertice y de material, blend, UV y direccionamiento que suben ApplyMaterial y los prepasses. Aca no se
    ''' rearma ninguna ley.</summary>
    Private Shared Function ResolverMaterial(mesh As PreviewModel.RenderableMesh, isSse As Boolean) As MatInfo
        Dim r As New MatInfo
        Dim mat = mesh.MeshData.Material
        If mat Is Nothing OrElse mat.MaterialBase Is Nothing Then Return r
        r.Cobertura = mat.EngineColourCoverage(isSse)
        r.TieneCobertura = True
        Dim c = r.Cobertura
        ' Solo se decodifica la difusa si algun corte o el blend la leen: un proyecto de tela opaca no toca disco.
        Dim leeTextura = c.Drawn AndAlso ((c.InPrepass AndAlso c.Prepass.Test AndAlso c.Prepass.Texture) OrElse
                                          (c.Colour.Test AndAlso c.Colour.Texture) OrElse
                                          ((c.Blend OrElse c.BlendSource.Test) AndAlso c.BlendSource.Texture))
        If Not leeTextura Then Return r
        Dim ruta = FO4UnifiedMaterial_Class.CorrectTexturePath(mat.MaterialBase.Diffuse_or_Base_Texture)
        Dim tex = CargarAlpha(ruta)
        If tex IsNot Nothing Then
            r.Mapa = tex.Rgba8
            r.MapaW = tex.Width
            r.MapaH = tex.Height
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

    ''' <summary>Opacidad EN EL PUNTO de impacto, con las baricentricas del rayo: 0 = no tapa, 1 = tapa del todo.
    ''' Compone la cobertura del motor (<c>MatInfo.Cobertura</c>) por PROFUNDIDAD Y ORDEN DE DIBUJO, que es lo que
    ''' decide en el juego si lo de atras se ve:
    ''' <br/>1) z-prepass: si su corte conserva el fragmento, el motor escribio esa profundidad antes de todo pase
    ''' de color (FO4 0x14181FAA0 al empezar el G-buffer; SSE slot 0x2B) y lo de atras falla el test: tapa.
    ''' <br/>2) color con profundidad EQUAL (SSE, lote opaco): dibuja solo donde su prepass escribio; si no, nada.
    ''' <br/>3) corte del pase de color y kill de su alpha de salida: el fragmento descartado no tapa.
    ''' <br/>4) sin blend: tapa si escribe profundidad (lo de atras, antes o despues, queda oculto) o si se dibuja
    ''' DESPUES del objetivo (lo pisa); dibujado antes sin escribir profundidad, el objetivo lo pisa.
    ''' <br/>5) con blend: lo de atras queda multiplicado por el factor de destino. Dibujado despues del objetivo
    ''' deja pasar ese factor; antes, tapa solo si escribe profundidad.
    ''' <para>El orden "antes / despues" es el del motor (<c>EngineCoverage.DrawsAfter</c>): la lista y, dentro de
    ''' ella, la ley que la fuente fija — la lista alfa ordenada por la clave del motor sobre el centro del world
    ''' bound, vista a lo largo del rayo (el ojo mira en <c>-dir</c>), los enlazados por registro, el lote opaco de SSE
    ''' por O-OP y los buckets de tecnica de FO4 por la clave del render (item 2b: tipo de shader, activacion de la
    ''' tecnica, paquetes grabados en orden de registro y despues la cadena al reves).</para>
    ''' <para>⛔ SOLO donde la fuente no fija el orden (listas 0, decal y billboard de SSE sin insercion leida) o la
    ''' cobertura tiene factores que la CPU no evalua (<c>EngineAlphaCut.Unknown</c>,
    ''' un factor de destino que depende de un color), se toma la opacidad que vale PARA TODO orden y valor posible
    ''' —la minima—: un vertice se enmascara solo si esta tapado CON CERTEZA (<c>OcclusionThreshold</c> 1,0).</para></summary>
    Private Function OpacidadEnImpacto(ByRef td As TriData, bu As Single, bv As Single, dir As Vector3) As Single
        If td.Mat < 0 OrElse td.Mat >= _mats.Count Then Return 1.0F
        Dim mi = _mats(td.Mat)
        If Not mi.TieneCobertura Then Return 1.0F
        Dim c = mi.Cobertura
        If Not c.Drawn Then Return 0.0F

        Dim w = 1.0F - bu - bv
        Dim u = td.UV0.X * w + td.UV1.X * bu + td.UV2.X * bv
        Dim v = td.UV0.Y * w + td.UV1.Y * bu + td.UV2.Y * bv
        Dim texA = mi.AlphaTextura(u, v)
        Dim vc = td.VA0 * w + td.VA1 * bu + td.VA2 * bv

        ' 1) z-prepass.
        If c.InPrepass Then
            Dim kp = c.Prepass.Keeps(texA, vc)
            If Not kp.HasValue Then Return 0.0F
            If kp.Value Then Return 1.0F
        End If
        ' 2) EQUAL contra su propio prepass, que aca no escribio.
        If c.ColourOnPrepass Then Return 0.0F
        ' 3) Corte del pase de color y el kill de su alpha de salida (registros del G-buffer FO4 con bit 15): un
        ' fragmento descartado no escribe ni color ni profundidad.
        Dim kc = c.Colour.Keeps(texA, vc)
        If Not kc.HasValue OrElse Not kc.Value Then Return 0.0F
        Dim kb = c.BlendSource.Keeps(texA, vc)
        If Not kb.HasValue OrElse Not kb.Value Then Return 0.0F
        Dim dib As Boolean? = Nothing
        If _coberturaObjetivo.HasValue Then dib = PreviewModel.RenderableMesh.MaterialData.EngineCoverage.DrawsAfter(c, _coberturaObjetivo.Value, -dir)
        Dim despues = dib.HasValue AndAlso dib.Value
        Dim antes = dib.HasValue AndAlso Not dib.Value
        ' 4) Sin blend.
        If Not c.Blend Then Return If(c.ColourWritesDepth OrElse despues, 1.0F, 0.0F)
        ' 5) Con blend.
        If antes Then Return If(c.ColourWritesDepth, 1.0F, 0.0F)
        If Not despues AndAlso Not c.ColourWritesDepth Then Return 0.0F
        If c.BlendSource.Unknown IsNot Nothing Then Return 0.0F
        Dim t = c.Transmittance(c.BlendSource.Value(texA, vc))
        If Not t.HasValue Then Return 0.0F
        Return Math.Max(0.0F, Math.Min(1.0F, 1.0F - t.Value))
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
    ''' <paramref name="transmisionMinima"/>: RaycastSettings.MinTransmission (criterio del usuario).
    ''' </summary>
    Private Function RayBloqueado(root As BvhNode, tris As TriData(),
                                  orig As Vector3, dir As Vector3,
                                  maxDist As Single, vertExcluido As Integer, transmisionMinima As Single) As Boolean
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

                    Dim op = OpacidadEnImpacto(tris(ti), bu, bv, dir)
                    ' Agujero de encaje / fragmento descartado por el alpha test: no frena nada.
                    If op <= 0.0F Then Continue For
                    ' Opaco: corta en el acto, que es el 99 % de los casos y el camino rapido.
                    If op >= 1.0F Then Return True
                    transmision *= (1.0F - op)
                    If transmision <= transmisionMinima Then Return True
                Next
            Else
                If n.Left IsNot Nothing Then stack.Push(n.Left)
                If n.Right IsNot Nothing Then stack.Push(n.Right)
            End If
        End While

        Return transmision <= transmisionMinima
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
        ' Sin coberturas armadas cada ocluder contaria como opaco (OpacidadEnImpacto: indice fuera de _mats): un llamador que
        ' olvido RefrescarCoberturas es un error, no un resultado.
        If _mats.Count <> _ocluders.Count Then Throw New InvalidOperationException("OcclusionRaytracer: RefrescarCoberturas was not called before ComputeOccludedVertices")

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

                        Dim blocked = RayBloqueado(_root, _tris, biased, worldDir, maxDist, -1, settings.MinTransmission)
                        If Not blocked AndAlso selfRoot IsNot Nothing Then
                            blocked = RayBloqueado(selfRoot, selfTris, biased, worldDir, maxDist, vi, settings.MinTransmission)
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
