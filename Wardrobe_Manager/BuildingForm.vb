' Version Uploaded of Wardrobe 3.2.0

Imports System.Threading.Tasks
Imports Wardrobe_Manager.Wardrobe_Manager_Form

Public Class BuildingForm

    ''' <summary>Rutas ABSOLUTAS de los artefactos que ESTE build escribió o conservó a propósito.
    ''' Lo consume el barrido de "no construidos" de <c>Wardrobe_Manager_Form.Build</c>, que corre DESPUÉS
    ''' del build y borra sólo lo que quedó de la corrida anterior.
    ''' <para>⛔ Se anota donde se DECIDE cada artefacto (el NIF al grabarlo, el .tri también cuando se
    ''' conserva), no se predice desde las opciones: la ley del .tri tiene cuatro condiciones y su
    ''' veredicto lo toma <see cref="VeredictoDelTri"/>.</para>
    ''' <para>⛔ SÓLO ENTRA LO QUE EL BARRIDO PUEDE BORRAR, y el barrido sólo mira NIF y .tri (ver
    ''' <see cref="CandidatosDeBarrido"/>). El <c>.txt</c> de tacones y el <c>.xml</c> de física NO se
    ''' anotan: cada uno tiene un único dueño que resuelve su ciclo de vida completo —
    ''' <c>SaveHighHeelBuild</c> y el bloque de física de <see cref="RunBuild"/>—, así que anotarlos era
    ''' escritura sin lector.</para>
    ''' <para>Con el motor de BodySlide este conjunto queda VACÍO —lo escribe el proceso externo, no
    ''' nosotros— y por eso ahí no se barre.</para></summary>
    Public ReadOnly Property ArtefactosDelBuild As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Los <c>OutputFullPathBase</c> que ESTE build usó de verdad, uno por proyecto construido.
    ''' El barrido calcula sus candidatos a partir de ACÁ y no del sliderset original, por dos motivos
    ''' medidos: con <c>ForceClonedOnBuild</c> el build escribe sobre un CLON cuyo output apunta a
    ''' <c>meshes\ManoloCloned\&lt;pack&gt;\</c>, así que los candidatos del original no intersecarían nunca con
    ''' lo escrito y el barrido borraría los artefactos del mod original; y un proyecto que falló no entra
    ''' acá, así que el barrido no se lleva puesta su salida buena de la corrida anterior.</summary>
    Public ReadOnly Property BasesDelBuild As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Private ReadOnly _Lista() As SliderSet_Class
    Private ReadOnly _Preset As SlidersPreset_Class
    Private ReadOnly _Pose As Poses_class

    Sub New(Que() As SliderSet_Class, Preset As SlidersPreset_Class, Pose As Poses_class)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        _Lista = Que
        _Preset = Preset
        _Pose = Pose
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    ''' <summary>De quien es el <c>.tri</c> que hay en la base de salida. ⛔ ES UN VEREDICTO UNICO Y
    ''' TIENE UN SOLO PRODUCTOR (<see cref="VeredictoDelTri"/>): lo consumen las TRES decisiones que se
    ''' toman sobre ese archivo — escribirlo, borrarlo, y anotarlo como artefacto de este build.
    ''' <para>⛔ ANTES ERAN DOS PREDICADOS (<c>ExistingTriIsForeign</c> / <c>ExistingTriIsBodySlide</c>)
    ''' leidos en momentos distintos, y uno de los dos SOLO se evaluaba dentro de
    ''' <c>If Settings_Build.SaveTri</c>. Con el default de fabrica (<c>SaveTri=False</c>) el veredicto
    ''' "ajeno" no se calculaba nunca, asi que el .tri no entraba en <c>ArtefactosDelBuild</c> y el
    ''' barrido de "no construidos" lo borraba — justo el archivo que el build habia decidido conservar,
    ''' 20 lineas mas arriba, por la ley opuesta. Dos leyes, dos lugares, resultado opuesto.</para></summary>
    Friend Enum DuenoDelTri
        ''' <summary>No hay archivo en esa ruta.</summary>
        NoHay
        ''' <summary>Header "PIRT": body-tri de BodySlide, lo escribimos nosotros y se regenera.</summary>
        Nuestro
        ''' <summary>Header distinto de "PIRT" (tipicamente un FRTRI003 de FaceGen) O ILEGIBLE. En los dos
        ''' casos NO se toca: un archivo que no pudimos leer no se pisa ni se borra a ciegas.</summary>
        Ajeno
    End Enum

    ''' <summary>El unico lector del header del <c>.tri</c>. "PIRT" es lo que compara <c>IsBodyTriFile</c>
    ''' de BSOS (TriFile.cpp) contra <c>"TRIP"_mci</c> — un uint32 que escrito little-endian da los bytes
    ''' P,I,R,T.
    ''' <para>⛔ SE LLAMA SIEMPRE, no solo cuando vamos a escribir. El costo es una apertura y 4 bytes por
    ''' proyecto; antes se leia DOS veces cuando <c>SaveTri</c> estaba prendido y CERO cuando estaba
    ''' apagado, que es el default.</para></summary>
    Friend Shared Function VeredictoDelTri(triPath As String) As DuenoDelTri
        Try
            If String.IsNullOrWhiteSpace(triPath) OrElse Not IO.File.Exists(triPath) Then Return DuenoDelTri.NoHay
            Using fs As New IO.FileStream(triPath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
                ' Un archivo de menos de 4 bytes es NUESTRO, no ajeno: WriteTriToFile abre con
                ' FileMode.Create (trunca primero), asi que un throw a mitad de escritura deja
                ' exactamente eso. Tratarlo como ajeno bloqueaba el proyecto PARA SIEMPRE.
                ' Se decide por fs.Length, NO por el retorno de Read: Read puede devolver menos bytes
                ' de los pedidos sin que el archivo este truncado (red, placeholder de OneDrive, AV),
                ' y con eso un FRTRI003 valido se habria reportado como nuestro y lo pisabamos.
                If fs.Length < 4 Then Return DuenoDelTri.Nuestro
                Dim buf(3) As Byte
                fs.ReadExactly(buf, 0, 4)
                Return If(System.Text.Encoding.ASCII.GetString(buf) = "PIRT", DuenoDelTri.Nuestro, DuenoDelTri.Ajeno)
            End Using
        Catch
            ' ⛔ Ilegible ⇒ AJENO, y esta rama es la que de verdad se dispara en produccion. El archivo
            ' tomado por otro proceso, el placeholder deshidratado de OneDrive y el AV en el medio caen
            ' todos aca. Tratarlo como ajeno es lo unico seguro: no se pisa y no se borra.
            Return DuenoDelTri.Ajeno
        End Try
    End Function

    ' ============================================================================================
    ' LA DERIVACION DE LOS NOMBRES DE SALIDA — UNA SOLA, Y VIVE ACA
    ' ============================================================================================
    ' ⛔ POR QUE ESTA ACA Y NO EN CADA LLAMADOR. Los nombres de los artefactos de un build salian de
    ' CUATRO expresiones distintas: este formulario (que ESCRIBE), los dos barridos de
    ' Wardrobe_Manager_Form y Remove_DataShapeFiles. Y no coincidian: los barridos le sacaban el ".nif"
    ' final a la raiz antes de componer, y el escritor no.
    '
    ' MEDIDO sobre el corpus del usuario (5.574 sliderSets: 3.196 FO4 + 2.378 SSE): 2 traen ".nif"
    ' DENTRO de <OutputFile> — "CBBE Vanilla 1st Person.osp" y "CBBE Vanilla Replacers.osp", los dos
    ' con <OutputFile>1stPersonGauntlets.nif</OutputFile> y OutputPath meshes\armor\nightingale\f.
    ' En el disco los artefactos construidos son "1stPersonGauntlets.nif_0.nif" y ".nif_1.nif" (existen
    ' los dos), y los candidatos que calculaba el barrido —"1stPersonGauntlets.nif", "_0.nif", "_1.nif"—
    ' no existen ninguno. O sea que el strip NO describia lo que el escritor produce, y de ahi salian
    ' las dos mitades del defecto: el barrido nunca limpiaba los .nif_0/_1.nif rancios de esos dos
    ' proyectos, y en cambio "1stPersonGauntlets.nif" —el nombre natural del asset del mod— quedaba
    ' como candidato a borrar SIN que ningun build lo hubiera escrito jamas.
    ' Peor en BorrarAntesDeConstruirConMotorExterno, que hace el mismo strip SIN conjunto de artefactos
    ' que lo frene: ahi el borrado es incondicional y el build escribe otro nombre, o sea que no lo
    ' rehace nunca.

    ''' <summary>Los NIF que este build escribe para un base de salida. Es lo que produce el escritor,
    ''' no lo que el barrido considera — para eso esta <see cref="CandidatosDeBarrido"/>.
    ''' <para>⛔ NO se le saca el ".nif" al base. El base es <c>OutputPath\OutputFile</c> tal cual lo
    ''' declara el .osp, y BodySlide compone igual (<c>outputPath + outputFile + "_0.nif"</c>): un
    ''' proyecto con <c>OutputFile</c> = "x.nif" emite "x.nif_0.nif", que es lo que hay en disco.</para></summary>
    Friend Shared Function NifsEscritos(baseSalida As String, multisize As Boolean) As List(Of String)
        Dim r As New List(Of String)
        If String.IsNullOrEmpty(baseSalida) Then Return r
        If multisize Then
            r.Add(baseSalida & "_0.nif")
            r.Add(baseSalida & "_1.nif")
        Else
            r.Add(baseSalida & ".nif")
        End If
        Return r
    End Function

    ''' <summary>Los nombres que el barrido de "no construidos" considera para un base. Es un
    ''' SUPERCONJUNTO de <see cref="NifsEscritos"/> a proposito, y no es un descuido: se listan los tres
    ''' nombres de NIF posibles porque si este build escribio <c>_0</c>/<c>_1</c>, el "&lt;base&gt;.nif"
    ''' suelto que dejo una corrida anterior (cuando el proyecto todavia no era multisize) es justamente
    ''' lo que hay que sacar.
    ''' <para>⛔ EL <c>.txt</c> DE TACONES NO ESTA EN LA LISTA, Y ESO ES LA LEY. Su unico dueño es
    ''' <c>SliderSet_Class.SaveHighHeelBuild</c>. Ver el comentario de la emision de tacones en
    ''' <c>RunBuild</c>.</para>
    ''' <para>⛔ Y EL <c>.xml</c> DE FISICA TAMPOCO. Su ciclo de vida entero —escribir si el proyecto
    ''' tiene fisica, borrar si no— lo resuelve <c>RunBuild</c> en la misma pasada, con mantenimiento del
    ''' diccionario incluido. No hay estado rancio que barrer.</para></summary>
    Friend Shared Function CandidatosDeBarrido(baseSalida As String) As List(Of String)
        Dim r As New List(Of String)
        If String.IsNullOrEmpty(baseSalida) Then Return r
        r.Add(baseSalida & ".nif")
        r.Add(baseSalida & "_0.nif")
        r.Add(baseSalida & "_1.nif")
        r.Add(baseSalida & ".tri")
        Return r
    End Function

    ''' <summary>Borrar un artefacto de build es SIEMPRE este par: sacarlo del disco y sacarlo del
    ''' <c>FilesDictionary</c>. Nunca uno solo de los dos.
    ''' <para>⛔ POR QUÉ. Las entradas que da de alta <see cref="RunBuild"/> llevan <c>BA2File = ""</c>,
    ''' o sea <c>IsLosseFile = True</c>. Los dos barridos borraban el archivo y dejaban la entrada, así
    ''' que el diccionario quedaba declarando un SUELTO QUE NO EXISTE — y un suelto TAPA la copia
    ''' empaquetada del BA2 para todo consumidor que resuelva por <c>IsLosseFile</c>. El propio
    ''' <c>RunBuild</c> ya mantenía la simetría en cada borrado suyo (el .tri y el .xml de física); en
    ''' los barridos faltaba.</para>
    ''' <para>Best-effort en las dos mitades: un artefacto que no se pudo borrar no invalida el build, y
    ''' un path fuera de <c>Fallout4data</c> no puede dar clave relativa — no se deja tirar por eso.</para>
    ''' <para>⛔⛔ SE DESREGISTRA EL <b>SUELTO</b>, NO LA CLAVE. Las dos mitades tienen leyes distintas:</para>
    ''' <list type="bullet">
    ''' <item><b>Borrar</b> es best-effort. Si no se pudo, el archivo SIGUE ahí y la entrada todavía
    ''' describe la realidad: no se toca nada.</item>
    ''' <item><b>Desregistrar</b> corre cuando el borrado CORRIÓ de verdad, o cuando el archivo ya no
    ''' estaba <b>y lo que el diccionario declara en esa clave es un SUELTO</b>.</item>
    ''' </list>
    ''' <para>⛔ POR QUÉ NO ALCANZA CON "el archivo no está". <c>RemoveDictionaryEntry</c> NO mira
    ''' <see cref="FilesDictionary_class.File_Location.IsLosseFile"/>: saca al GANADOR de la clave y popea
    ''' lo que hubiera debajo. Los candidatos que este barrido recorre —<c>&lt;base&gt;.nif</c>,
    ''' <c>_0</c>, <c>_1</c>, <c>.tri</c>— son exactamente lo que un mod normal shipea DENTRO de su
    ''' <c>.ba2</c> y sin sueltos; así que con el motor de BodySlide y <c>DeleteUnbuilt</c> (el DEFAULT)
    ''' un "desregistrar siempre" desmontaba del diccionario los NIF EMPAQUETADOS de cada prenda, que
    ''' este build no escribió y no podría borrar, y el preview y el pack dejaban de resolverlos por el
    ''' resto de la sesión. El <c>If Not File.Exists Then Return</c> anterior los dejaba intactos: el
    ''' arreglo del fantasma, hecho sin esta guarda, cambiaba un defecto por otro peor.</para>
    ''' <para>⛔ Y EL FANTASMA SÍ SE VA, que es la mitad que faltaba de verdad: una entrada SUELTA de un
    ''' artefacto que ya no existe —lo borró el usuario, otro build, el gestor de mods— se quedaba para
    ''' siempre, y un suelto TAPA la copia empaquetada del BA2 para todo consumidor que resuelva por
    ''' <c>IsLosseFile</c>. Ése —y sólo ése— es el caso que el early-return se comía.
    ''' Gate: <c>Tools\WmEscrituraGate</c> A6.5..A6.10, que cubren las CUATRO combinaciones de
    ''' {el archivo estaba / no estaba} × {el ganador es suelto / es de archive}. Menos que las cuatro
    ''' deja la ley a medias: con sólo A6.5–A6.8 la puerta "el borrado SÍ corrió" quedaba en cero y ahí
    ''' se coló exactamente este defecto.</para></summary>
    Friend Shared Sub BorrarArtefactoYDesregistrar(ruta As String)
        If IO.File.Exists(ruta) Then
            Try
                IO.File.Delete(ruta)
            Catch
                ' No se pudo borrar: el archivo SIGUE ahí, así que la entrada del diccionario todavía
                ' describe la realidad y no hay que sacarla.
                Return
            End Try
        End If
        Try
            Dim clave = IO.Path.GetRelativePath(Directorios.Fallout4data, ruta).Correct_Path_Separator
            ' ⛔⛔ LA GUARDA VALE EN LAS DOS PUERTAS, Y ESTUVO ADENTRO DEL `If Not seBorro` — o sea que
            ' con el archivo PRESENTE el borrado del diccionario salía incondicional y se llevaba igual a
            ' la entrada empaquetada. La puerta `seBorro = True` no estaba medida por ningún caso.
            ' Y es alcanzable con el config de fábrica: con el motor de BodySlide, WM NO registra lo que
            ' escribe el proceso externo —las altas viven sólo en el camino del motor PROPIO, ver
            ' <c>RunBuild</c>—, así que el suelto está en disco y el GANADOR de esa clave sigue siendo la
            ' entrada del <c>.ba2</c> del mod. El build siguiente de la MISMA sesión borra el suelto
            ' (existe ⇒ el `Delete` corre) y desmontaba un BA2 que nunca fue suyo.
            ' Por eso ya no hace falta saber SI se borró: la pregunta es una sola y es sobre el ganador.
            ' LA LEY ES UNA: se desregistra si-y-sólo-si el GANADOR de la clave es SUELTO. Si el ganador
            ' es una entrada de archive, el suelto que borramos —o que no estaba— nunca fue el ganador, y
            ' no hay nada nuestro que sacar. Gate: A6.5..A6.10 de Tools\WmEscrituraGate, que cubren las
            ' cuatro combinaciones de {archivo estaba / no estaba} × {ganador suelto / de archive}.
            Dim loc As FilesDictionary_class.File_Location = Nothing
            If Not FilesDictionary_class.Dictionary.TryGetValue(clave, loc) Then Return
            If loc Is Nothing OrElse Not loc.IsLosseFile Then Return
            FilesDictionary_class.RemoveDictionaryEntry(clave)
        Catch
        End Try
    End Sub

    ''' <summary>El borrado PREVIO, para el único camino donde no se puede observar lo escrito: el motor
    ''' de BodySlide. Bajo Mod Organizer esto sigue mandando la salida a `overwrite` —es la conducta que
    ''' ese camino tuvo siempre— pero es preferible a apagar la opción en silencio.
    ''' <para>⛔ LOS CANDIDATOS SALEN DE <see cref="CandidatosDeBarrido"/>, la misma derivación que usa el
    ''' escritor. Acá vivía un segundo cálculo que le sacaba el ".nif" final a la raíz, y era el más
    ''' peligroso de los dos porque en este camino el borrado es INCONDICIONAL: no hay conjunto de
    ''' artefactos que lo frene. MEDIDO sobre el corpus: con el strip se borraba
    ''' "1stPersonGauntlets.nif" —el asset del mod— y el build escribía "1stPersonGauntlets.nif_0.nif",
    ''' o sea que no lo rehacía nunca.</para>
    ''' <para>⛔⛔ Y EL <c>.tri</c> PASA POR <see cref="VeredictoDelTri"/>, COMO LAS OTRAS DOS DECISIONES.
    ''' Es la ley que la cabecera de <see cref="DuenoDelTri"/> declara —UN VEREDICTO, TRES decisiones— y
    ''' este camino era la tercera tomada por su cuenta: recorría <see cref="CandidatosDeBarrido"/>, que
    ''' incluye el <c>.tri</c>, y borraba INCONDICIONAL. El caso de producción sale con el config de
    ''' FÁBRICA (motor de BodySlide + <c>DeleteUnbuilt=True</c>): se llevaba puesto el <c>FRTRI003</c> de
    ''' FaceGen que vive al lado de la base de salida — el MISMO archivo que el build conserva a propósito
    ''' 20 líneas más arriba— y encima BodySlide no lo rehace, porque no es suyo. Un <c>.tri</c> ILEGIBLE
    ''' cuenta como ajeno por la misma función, así que tampoco se toca.
    ''' <b>El <c>.tri</c> NUESTRO (header PIRT) se sigue borrando</b>: acá se aplica la ley, no se apaga
    ''' el barrido. Gate: <c>Tools\WmEscrituraGate</c> A8.1/A8.2/A8.3.</para></summary>
    Friend Shared Sub BorrarAntesDeConstruirConMotorExterno(que As SliderSet_Class())
        If WM_Config.Current.Settings_Build.DeleteUnbuilt = False Then Return
        If que Is Nothing Then Return
        For Each projecto In que
            For Each cand In CandidatosDeBarrido(projecto.OutputFullPathBase)
                ' El único candidato con dueño posible es el .tri; para todo lo demás VeredictoDelTri no
                ' es la pregunta. Se compara por extensión y no por posición en la lista: el orden de
                ' CandidatosDeBarrido no es un contrato.
                If cand.EndsWith(".tri", StringComparison.OrdinalIgnoreCase) AndAlso
                   VeredictoDelTri(cand) = DuenoDelTri.Ajeno Then Continue For
                BorrarArtefactoYDesregistrar(cand)
            Next
        Next
    End Sub

    ''' <summary>"Delete unbuilt": borra los artefactos que quedaron de la corrida ANTERIOR y que este
    ''' build no escribió ni conservó a propósito.
    ''' <para>⛔ Corre DESPUÉS de construir, no antes. El borrado previo sacaba los archivos del mod bajo
    ''' Mod Organizer —borrar los saca del árbol virtual, y lo que el build escribe después es un archivo
    ''' NUEVO, que cae en `overwrite`—, así que cada build se llevaba la prenda fuera de su mod.</para>
    ''' <para>⛔ Y ACÁ NO SE DECIDE NADA: se CONSUME el veredicto que ya tomó el build
    ''' (<see cref="ArtefactosDelBuild"/>). La ley del .tri tiene cuatro condiciones y su veredicto lo
    ''' toma <see cref="VeredictoDelTri"/>; la del .txt de tacones vive entera en
    ''' <c>SaveHighHeelBuild</c> y por eso el .txt NI SIQUIERA ES CANDIDATO. Re-derivar cualquiera de las
    ''' dos acá es el defecto que este comentario existe para no repetir.</para>
    ''' <para>⛔ ES <c>Friend Shared</c> Y NO UN MIEMBRO DEL FORMULARIO. Vivía como <c>Private Sub</c> de
    ''' instancia de <c>Wardrobe_Manager_Form</c>, y por eso el <c>--build</c> del CLI no podía llamarlo:
    ''' <c>DeleteUnbuilt</c> era un no-op silencioso ahí, con la opción prendida por defecto. Acá lo
    ''' consumen los dos caminos y además puede invocarlo un gate sin construir la ventana.</para>
    ''' <para>Con el motor de BodySlide no se barre: los archivos los escribe un proceso externo, así que
    ''' no hay conjunto observable, y adivinarlo por fecha borraría un .nif bueno del mod del usuario.</para></summary>
    Friend Shared Sub BorrarNoConstruidos(bases As HashSet(Of String), artefactos As HashSet(Of String))
        If WM_Config.Current.Settings_Build.DeleteUnbuilt = False Then Return
        If artefactos Is Nothing OrElse bases Is Nothing Then Return

        ' Los candidatos salen del base que USÓ EL ESCRITOR, no del sliderset original: con
        ' ForceClonedOnBuild el build escribe sobre un clon que apunta a meshes\ManoloCloned\<pack>\, y
        ' calcularlos sobre el original haría que ningún candidato intersecara con lo escrito — el
        ' barrido borraría los artefactos del mod original en cada build. Y un proyecto que falló no
        ' aparece en `bases`, así que su salida de la corrida anterior no se toca.
        For Each baseName In bases
            ' ⛔ SIN STRIP DEL ".nif". La lista es la MISMA que compone el escritor; acá había una
            ' segunda derivación que le sacaba el ".nif" final al base, y por eso ningún candidato
            ' intersecaba con lo escrito para los proyectos cuyo <OutputFile> ya trae la extensión.
            ' Ver CandidatosDeBarrido, donde está la medición sobre el corpus.
            For Each cand In CandidatosDeBarrido(baseName)
                If artefactos.Contains(cand) Then Continue For
                BorrarArtefactoYDesregistrar(cand)
            Next
        Next
    End Sub

    ''' <summary>
    ''' Modo sin ventana: suprime los dos puntos interactivos del final (el dialogo de issues de carga y
    ''' el MsgBox de errores) para que <see cref="RunBuild"/> pueda correr desde el CLI. NO cambia nada
    ''' de lo que se construye — los NIF salen por el mismo camino que con la ventana abierta.
    ''' </summary>
    Public Property Headless As Boolean = False

    ''' <summary>El gate del SIMD del skinning, UNA vez por proceso. <c>Lazy</c> con
    ''' <c>ExecutionAndPublication</c> ⇒ si dos builds arrancan a la vez, el test corre una sola vez y
    ''' los dos ven el mismo resultado. Cuesta ~5 ms con el JIT en frio.</summary>
    Private Shared ReadOnly _skinSimdGate As New Lazy(Of String)(
        Function() SkinningHelper.SkinningSimdSelfTest(),
        Threading.LazyThreadSafetyMode.ExecutionAndPublication)

    ''' <summary>Lanza si el blend vectorial no es bit-identico al escalar. Lo llama
    ''' <see cref="RunBuild"/> antes del primer NIF.</summary>
    Friend Shared Sub EnsureSkinSimdGate()
        Dim r = _skinSimdGate.Value
        If r.Length = 0 Then Return
        Throw New InvalidOperationException(
            "Parity gate FAILED [skin-blend] — the vector blend of skin matrices is NOT bit-identical to the " &
            "scalar one ⇒ vertices would come out different depending on the CPU. Building now would produce NIFs that do not " &
            "describe the law. Details: " & r)
    End Sub

    Private Sub BuildingForm_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        RunBuild()
        Me.Close()
    End Sub

    ''' <summary>
    ''' Corre el batch completo y devuelve "" si no hubo errores, o el texto acumulado.
    ''' ⭐ Es el MISMO cuerpo que corria en el handler de Shown: se extrajo para que el CLI pueda
    ''' instanciar el form y construir SIN mostrarlo (equivalente al --bake-all de NPC_Manager).
    ''' Los controles de progreso se siguen escribiendo — en un form no mostrado eso solo fuerza la
    ''' creacion perezosa del handle y no requiere bomba de mensajes.
    ''' </summary>
    Public Function RunBuild() As String
        ' ⛔ GATE DEL BLEND VECTORIAL, antes del primer NIF. Corre la funcion real
        ' SkinningHelper.BlendBoneMatrices por sus dos caminos (vectorial y escalar) y los compara bit
        ' a bit; si divergen, los vertices que se escriban dependerian de la CPU de quien construye, y
        ' la app SE DISTRIBUYE (00-reglas-app-distribuida).
        ' ⭐ Va ACA, no en un PhaseReport del final: el equivalente de FaceGen vivia en un reporte que
        ' solo corria al terminar el barrido, asi que un build normal no lo ejecutaba NUNCA y un
        ' mismatch aparecia con los bytes ya en disco (61-perf-simd-evaluacion).
        EnsureSkinSimdGate()
        ProgressBar1.Value = 0
        ProgressBar2.Value = 0
        ProgressBar1.Maximum = 5
        ProgressBar2.Maximum = _Lista.Length * 2
        Dim DummyOSP As New OSP_Project_Class
        Dim Errores As String = ""
        Dim Nombre As String = "Unknown"
        ' Lee los sliders de looksmenu si se graba tri
        If WM_Config.Current.Settings_Build.SaveTri Then LooksMenuSliders.Read_Looksmenu_Sliders()
        OSP_Project_Class.Default_Memory_Pause = True
        ' Context unico y compartido para todo el batch de builds. Acumulamos los
        ' issues de load en effectiveContext.Issues y al final disparamos un solo
        ' ShowLoadIssuesDialog con la lista agregada, en vez de N popups individuales.
        Dim buildLoadContext = ProjectLoadContext.CreateCollectOnly(False)
        Dim has_pose = (WM_Config.Current.Settings_Build.BuildInPose AndAlso _Pose.Source <> Poses_class.Pose_Source_Enum.None)
        For Each sliderset_target In _Lista
            Try
                ' ⛔ El nombre se fija ACA, antes de que nada pueda tirar. Estaba asignado recien
                ' dentro del bucle de sizes, DESPUES del chequeo de carga, asi que un proyecto que no
                ' cargaba se reportaba como "Unknown: Could not load shape data for build." y no habia
                ' forma de saber cual de los 9 proyectos de un .osp era. (Con 43 .osp instalados, el
                ' usuario no tiene como averiguarlo; hubo que construirlos de a uno para encontrarlo.)
                ' Solo alimenta el texto del error y el label de progreso: no cambia que se construye.
                Nombre = sliderset_target.Nombre
                Dim NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                Dim builder As New SliderSet_Class(NodoClone, DummyOSP)
                ' Force the cloned output dir (per-pack) when the build setting is on. Runs on the
                ' temporary clone only, so the pack name comes from the original sliderset's ParentOSP.
                If WM_Config.Current.Settings_Build.ForceClonedOnBuild Then
                    builder.ForceClonedOutputDir(If(sliderset_target.ParentOSP?.Nombre, ""))
                End If
                ' Shapedata loaded on the builder clone below, not on sliderset_target
                Dim size As WM_Config.SliderSize = WM_Config.SliderSize.Default
                ' Decididos en Sizecount=0 y reusados por el resto de los sizes: el .tri es uno solo
                ' para todo el proyecto, pero cada size necesita saber si estampar el BODYTRI.
                Dim triBlocked As Boolean = False
                Dim triWritten As Boolean = False
                ' El veredicto del .tri, tomado UNA vez en Sizecount=0 y reusado por el resto de los
                ' sizes: el .tri es uno solo para todo el proyecto. Ver DuenoDelTri.
                Dim triDueno As DuenoDelTri = DuenoDelTri.NoHay
                For Sizecount = 0 To CInt(IIf(sliderset_target.Multisize, 1, 0))
                    ProgressBar1.Value = 0
                    ' Cada peso debe partir de la geometría PRISTINE. Sin esto, la pasada _1 (Big)
                    ' hereda el NIFContent ya bakeado con Small: Load_and_Check_Shapedata skipea la
                    ' recarga (ShapeDataLoaded + misma signature) y BakeFromMemoryUsingOriginal de la
                    ' pasada anterior ya inyectó los vértices morphados al trishape. Resultado: sin
                    ' preset _1 sale byte-idéntico a _0 (deltas Big=0 sobre base ya morphada); con
                    ' preset, _1 = small+big APILADOS. OS (BodySlideApp::BuildBodies) aplica cada
                    ' peso desde cero sobre la base — replicamos eso recargando.
                    '
                    ' ⛔⛔ Y EL PROYECTO TAMBIÉN, NO SÓLO LA GEOMETRÍA. `RemoveShape` no se limita a
                    ' sacar la shape del NIF: le borra el `<Shape>` y sus `<Data>` al XML DEL CLON
                    ' (`Nodo.RemoveChild(Shape.Nodo)`, OSP_Clases.vb). Y el clon se creaba UNA sola vez
                    ' para TODOS los pesos, así que esa mutilación cruzaba de una pasada a la otra:
                    ' una shape que queda 100 % zapeada en el pase Small desaparecía del proyecto, y en
                    ' el pase Big `Lee_SlidersAndShapes` releía un XML que ya no la tenía ⇒ nadie la
                    ' procesaba, nadie la zapeaba, y el NIF recién recargado de disco la escribía
                    ' INTACTA. Resultado: `_0` y `_1` con TOPOLOGÍAS DISTINTAS y el `.tri` indexado
                    ' contra una sola de las dos.
                    ' MEDIDO sobre el corpus de SSE: 4 sliderSets de CBBE (`Prisoner Bloody`
                    ' y `Roughspun Tunic`, con y sin Physics) perdían la shape `Bra` en `_0` y la
                    ' conservaban en `_1`; el `_1` salía byte-idéntico a lo que emitía 1.4.0.
                    ' ⚠️ Re-clonar es lo mismo que ya se hace con la geometría: el estado del pase
                    ' anterior no puede sobrevivir. Es inerte para todo proyecto donde ninguna shape se
                    ' remueve, que es el caso normal.
                    If Sizecount > 0 Then
                        builder.UnloadShapeData(False)
                        NodoClone = DummyOSP.xml.ImportNode(sliderset_target.Nodo.Clone, True)
                        builder = New SliderSet_Class(NodoClone, DummyOSP)
                        If WM_Config.Current.Settings_Build.ForceClonedOnBuild Then
                            builder.ForceClonedOutputDir(If(sliderset_target.ParentOSP?.Nombre, ""))
                        End If
                    End If
                    ProgressBar1.Maximum = (builder.Shapes.Count * 4 + 6)
                    If OSP_Project_Class.Load_and_CHeck_Project(builder, buildLoadContext) = False OrElse OSP_Project_Class.Load_and_Check_Shapedata(builder, buildLoadContext) = False Then
                        ' ⛔ La CAUSA real ya la tiene el contexto: los dos Load_* atrapan su excepcion
                        ' y la reportan con ReportLoadIssue ("Block 'X' exists in more than one local
                        ' osd file", "Shape without Nif Shapes different", ...). Este Throw la tapaba con un texto
                        ' generico y el usuario quedaba sin saber que revisar del mod. Se arrastra el
                        ' ultimo issue de ESTE proyecto, si lo hay.
                        Dim causa = buildLoadContext.Issues.
                            LastOrDefault(Function(i) String.Equals(i.ProjectName, sliderset_target.Nombre, StringComparison.OrdinalIgnoreCase))
                        Dim detalle = If(causa Is Nothing OrElse String.IsNullOrWhiteSpace(causa.Message), "", " — " & causa.Message)
                        Throw New InvalidOperationException("Could not load shape data for build." & detalle)
                    End If
                    ProgressBar1.Value += 1
                    ' El clon NO puede resolver su propio HH: ForceClonedOutputDir ya le reescribió el
                    ' OutputPath (la detección FO4 va contra ese path) y su ParentOSP es un dummy sin
                    ' archivo. Y leer el campo crudo del original tampoco servía: la lista descarga cada
                    ' sliderset tras cargarlo y UnloadShapeData lo pone en 0, así que el build interno
                    ' horneaba 0 y BORRABA los tacones. Se resuelve sobre el ORIGINAL y se estampa como
                    ' autorizado en el clon.
                    builder.HighHeelHeight = sliderset_target.ResolveEffectiveHighHeel(buildLoadContext)
                    builder.HighHeelAuthored = True
                    SkeletonInstance.Default.PrepareForShapes(builder.Shapes)
                    SkeletonInstance.Default.ApplyPose(If(has_pose, _Pose, Nothing))
                    ProgressBar1.Value += 1

                    ' ⛔ El nombre del NIF sale de la derivacion UNICA, no de una expresion local: es la
                    ' misma que consumen los dos barridos y Remove_DataShapeFiles. Ver NifsEscritos.
                    Dim fil = NifsEscritos(builder.OutputFullPathBase, sliderset_target.Multisize)(Sizecount)
                    Dim tri = builder.OutputFullPathBase + ".tri"
                    Dim Tridata = builder.BodyTriRelativePath
                    Dim dir = IO.Path.GetDirectoryName(fil)
                    Nombre = sliderset_target.Nombre
                    Label1.Text = "Building: " + Nombre + IIf(sliderset_target.Multisize(), "_" + Sizecount.ToString, "")
                    Application.DoEvents()
                    ' Multisize() == GenWeights(). BodySlide SIN GenWeights emite UN solo mesh y lo hace
                    ' con `vbig` / `defBigValue` — `vsmall` sólo existe dentro de
                    ' `if (currentSet.GenWeights())` (BodySlideApp.cpp).
                    ' Mapear el pase único a Small hacía que un proyecto SSE no-multisize leyera
                    ' Default_Small_Value en vez de Default_Big_Value.
                    ' Ni esto ni Multisize() se gatean por juego. MEDIDO contra el binario de FO4:
                    ' GenWeights="false" ⇒ 1 archivo, "true" ⇒ _0 y _1, y AUSENTE ⇒ _0 y _1 tambien.
                    ' Sobre el disco no cambia nada (los 3.560 sliderSets de FO4 traen el atributo en
                    ' false), sólo gobierna .osp de terceros.
                    If sliderset_target.Multisize Then
                        size = If(Sizecount = 0, WM_Config.SliderSize.Small, WM_Config.SliderSize.Big)
                    Else
                        size = WM_Config.SliderSize.Big
                    End If
                    ' 0 - cargo morph
                    builder.SetPreset(_Preset, size)
                    ProgressBar1.Value += 1
                    ' --- O6.1: Parallel shape processing (compute-heavy part) ---
                    Dim shapeList = builder.Shapes.ToList
                    Dim shapeResults As New System.Collections.Concurrent.ConcurrentDictionary(Of Shape_class, SkinnedGeometry)
                    ' El reindex de morphs post-zap NO puede correr dentro del Parallel.ForEach: escribe
                    ' en el XmlDocument del OSP y en OSDContent_Local.Blocks, ambos compartidos por todas
                    ' las shapes. Se recolecta aca el mapa old->new y se aplica en la fase 2 (serial).
                    Dim zapMods As New System.Collections.Concurrent.ConcurrentDictionary(Of Shape_class, ZapGeometryModifier)
                    ' Shapes 100 % zapeadas que se conservan ocultas: BodySlide tampoco emite sus morphs
                    ' en el .tri (con la geometria intacta su erase de rangos deja todos los offsets en
                    ' cero, BodySlideApp.cpp), asi que se las excluye explicitamente.
                    Dim hiddenZappedShapes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    ' Compartido por TODAS las shapes del size: un mismo bloque OSD alcanzado desde dos
                    ' shapes no se puede remapear dos veces (la segunda iria sobre indices ya movidos).
                    Dim remappedBlocks As New HashSet(Of OSD_Block_Class)(ReferenceEqualityComparer.Instance)
                    Dim localKeepZapped = builder.KeepZappedShapes
                    Dim localSize = size
                    Dim localSingleBone = Config_App.Current.Setting_SingleBoneSkinning
                    Dim localRecalcNormals = Config_App.Current.Setting_RecalculateNormals

                    ' Phase 1: parallel compute (Extract + Morph + Bake/InjectToTrishape).
                    ' Pose state was applied above via ApplyPose (or Reset when has_pose=False);
                    ' Extract/Bake read it from the SkeletonInstance's DeltaTransforms.
                    Parallel.ForEach(shapeList.Where(Function(s) s.RelatedNifShape IsNot Nothing),
                        Sub(shap)
                            ' 1- cargo geometria.
                            ' ⭐ UN SOLO recalculo COMPLETO, aca, y solo si el usuario lo pidio; de ahi en
                            ' mas el morph refresca unicamente la clausura de lo que se movio. Estaba
                            ' forzado a False, y eso dejaba SIN TOCAR toda shape que el preset no mueve:
                            ' salia con el marco tangente del asset original, roto o no. MEDIDO sobre el
                            ' corpus: 784 shapes en FO4 y 118 en SSE con la base hasta 90 grados fuera de
                            ' ortogonal, heredada del mod.
                            ' ⛔ No es trabajo tirado ni cambia de espacio, que es lo que parecia:
                            '   · `ApplyMorph_CPU` reescribe posiciones, UVs y mascara pero NO toca
                            '     Normals/Tangents/Bitangents, asi que este recalculo SOBREVIVE al morph.
                            '   · `rawVerts` y `NifLocalVertices` son la MISMA copia (SkinningHelper:571),
                            '     o sea el mismo espacio en el que despues trabaja el morph.
                            '   · La pose ya esta en el esqueleto (ApplyPose, arriba) y el skinning lo
                            '     aplica el bake, que transforma N/T/B junto con las posiciones.
                            ' Para una shape que el preset SI mueve queda redundante con la clausura
                            ' posterior — y esta medido que la clausura sola ya cubre 22.658 de 22.708
                            ' vertices—, pero para la que NO se mueve es la unica oportunidad.
                            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shap, singleboneskinning:=localSingleBone, RecalculateNormals:=localRecalcNormals)
                            ' 3- aplico morph (y recalculo normales si esta elegido)
                            MorphingHelper.ApplyMorph_CPU(shap, geom, localRecalcNormals, AllowMask:=False, buildSize:=localSize)
                            ' 4- Borro zaps y revierto bakeo (includes InjectToTrishape per-shape)
                            Dim zapMod As New ZapGeometryModifier(localKeepZapped)
                            SkinningHelper.BakeFromMemoryUsingOriginal(shap, geom, inverse:=False, ApplyMorph:=True, RemoveZaps:=True, singleBoneSkinning:=localSingleBone, geometryModifier:=zapMod)
                            zapMods(shap) = zapMod
                            shapeResults(shap) = geom
                        End Sub)

                    ' Phase 2: sequential NIF structure updates + progress
                    For Each shap In shapeList
                        If shap.RelatedNifShape IsNot Nothing Then
                            Dim geom As SkinnedGeometry = Nothing
                            If shapeResults.TryGetValue(shap, geom) Then
                                ' Paso 4 del zap, fuera del paralelo. Antes de RemoveShape: si la shape se
                                ' va, RemoveShape ya borra sus Datas y bloques locales.
                                Dim zapMod As ZapGeometryModifier = Nothing
                                zapMods.TryGetValue(shap, zapMod)
                                If zapMod IsNot Nothing Then MorphingHelper.ReindexMorphsAfterZap(shap, zapMod.VertexRemap, remappedBlocks)
                                ' El LOCKEDNORM viaja en el MISMO espacio de índices que los morphs y hay
                                ' que renumerarlo con el mismo mapa: el canónico lo hace dentro de
                                ' `DeleteVertsForShape` (nifly NifFile.cpp:4328-4353) y acá no lo hacía
                                ' nadie. Va al lado de la reindexación de morphs, en la fase SERIAL.
                                If zapMod IsNot Nothing AndAlso zapMod.VertexRemap IsNot Nothing Then
                                    shap.IR_Geometry?.RemapLockedNormalIndices(zapMod.VertexRemap)
                                End If
                                ProgressBar1.Value += 3 ' account for extract+morph+bake steps
                                If builder.KeepZappedShapes = False AndAlso geom.Vertices.Length = 0 Then
                                    builder.RemoveShape(shap)
                                    ProgressBar1.Value += 1
                                Else
                                    ' Shape 100 % zapeada que se conserva: oculta con geometria intacta,
                                    ' igual que BodySlideApp.cpp.
                                    If zapMod IsNot Nothing AndAlso zapMod.FullyZappedKept Then
                                        builder.NIFContent.SetShapeHidden(shap.RelatedNifShape)
                                        hiddenZappedShapes.Add(shap.RelatedNifShape.Name.String)
                                    End If
                                    builder.NIFContent.UpdateSkinPartitions(shap.RelatedNifShape)
                                    ProgressBar1.Value += 1
                                End If
                            Else
                                ' Shape was filtered or failed silently in the parallel phase
#If DEBUG Then
                                Debugger.Break()
#End If
                                ProgressBar1.Value += 4
                            End If
                        Else
                            ProgressBar1.Value += 4
                        End If
                    Next

                    If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

                    ' El engine resuelve el material leyendo el Name del shader como path relativo a
                    ' Data\, así que debe contener el ancla "Materials\". WM lo guarda pelado
                    ' (Clone_Materials setea "ManoloCloned\..." y SetRelatedMaterial strippea el prefijo),
                    ' lo que se ve bien in-app pero deja al engine sin encontrar el bgsm/bgem in-game.
                    ' Se lo devolvemos aquí, antes de grabar cualquier NIF de salida (incluido el variant
                    ' de high-heels, que opera sobre este mismo NIFContent). Independiente del flag ForceClone.
                    builder.NIFContent.EnsureMaterialPrefixForGame()

                    ' Grabo bloque tri si hace falta. GAME-AWARE, replicando OutfitStudio (BodySlideApp.cpp,
                    ' AddTriData / BuildBodies):
                    '   • FO4/FO4VR/FO76 → BODYTRI en el NODO RAÍZ (AddTriData toRoot=true).
                    '   • Skyrim/SSE     → BODYTRI en un NiShape: el PRIMER shape (en orden del sliderset) que
                    '     existe en el NIF y tiene >0 vértices; solo UNO (triEnd se apaga tras el primero).
                    ' skee64 (RaceMenu) lo lee con VisitObjects → lo encuentra en el shape; escribirlo a la raíz
                    ' en SSE no es fiel a OutfitStudio (y rompe lectores shape-only). El .tri en sí es idéntico
                    ' (PIRT/TRIP) en ambos juegos — solo cambia DÓNDE se marca en el NIF.
                    ' `triKeep` de BodySlideApp.cpp = PreventMorphFile (salvo IgnorePreventri,
                    ' extension de WM) O un .tri ajeno ya presente. El nombre del .tri sale del nombre del
                    ' NIF, asi que un proyecto que apunte a una malla de cabeza machacaria el FRTRI003 de
                    ' chargen del juego — un archivo ajeno que no se regenera.
                    Dim triAllowed As Boolean = (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri)
                    If Sizecount = 0 Then
                        triWritten = False
                        ' ⛔ EL VEREDICTO SE TOMA ACA, UNA VEZ, Y SIEMPRE — no dentro del `If SaveTri`.
                        ' Es el unico productor del hecho "de quien es este .tri", y lo consumen las tres
                        ' decisiones de abajo: escribir, borrar, y anotar como artefacto. Calcularlo solo
                        ' cuando ibamos a escribir dejaba al barrido decidiendo por su cuenta sobre el
                        ' mismo archivo, con la ley opuesta. Ver DuenoDelTri.
                        triDueno = VeredictoDelTri(tri)
                        triBlocked = (triDueno = DuenoDelTri.Ajeno)
                        ' Solo se AVISA cuando de verdad ibamos a escribir, como BSOS, que hace el
                        ' chequeo dentro de `if (tri && !triKeep)`. El veredicto se calcula siempre; el
                        ' mensaje al usuario sigue atado a la intencion de escribir, que es lo canonico.
                        If WM_Config.Current.Settings_Build.SaveTri AndAlso triAllowed Then
                            If triBlocked Then
                                If Errores <> "" Then Errores += vbCrLf
                                Errores += Nombre & ": kept the existing non-BodySlide .tri at " & tri & " (morphs skipped)"
                            Else
                                ' El .tri se escribe ANTES de grabar el NIF: si falla, el BODYTRI de abajo
                                ' no se estampa y el NIF sale coherente. Al reves (como estaba) el NIF ya
                                ' habia salido apuntando a un archivo inexistente, y cortar por excepcion
                                ' ademas se llevaba puesto el resto de los sizes del proyecto.
                                triWritten = LooksMenuSliders.WriteMorphTRI(tri, builder, hiddenZappedShapes)
                                If Not triWritten Then
                                    If Errores <> "" Then Errores += vbCrLf
                                    Errores += Nombre & ": failed to write the morph .tri at " & tri
                                End If
                            End If
                        End If
                    End If

                    If WM_Config.Current.Settings_Build.SaveTri AndAlso triWritten AndAlso triAllowed Then
                        ' Purgar + elegir anfitrión + estampar, los tres pasos en un solo sitio:
                        ' `NifContent_Class.SetTriData`, transcripción de `BodySlideApp.cpp:5113-5151`.
                        ' `toRoot` es lo único game-aware: FO4/FO4VR/FO76 leen el BODYTRI del NODO RAÍZ y
                        ' Skyrim/SSE de una shape (el comentario de :325-327 lo explica).
                        '
                        ' ⛔ ACÁ HABÍA UNA ELECCIÓN DE ANFITRIÓN POR ORDEN ALFABÉTICO, y su comentario
                        ' citaba el `std::map<std::string, SliderSetShape>` de `SliderSet.h`. Ese map
                        ' ordena el bucle de shapes de `BuildBodies`, NO la elección del anfitrión del
                        ' BODYTRI: `SetTriData` la hace con `for (auto& shape : nif.GetShapes())`
                        ' (BodySlideApp.cpp:5136-5141), o sea la primera shape con vértices en ORDEN DE
                        ' BLOQUE y sobre TODAS las del NIF, no sólo las que lista el .osp. MEDIDO: 697 de
                        ' los 2.393 sliderSets de SSE cambian de anfitriona. Es cambio de BYTES, no de
                        ' comportamiento — skee recorre el árbol entero con `VisitObjects`
                        ' (BodyMorphInterface.cpp:689-701), así que da igual de qué shape cuelgue mientras
                        ' haya UNO SOLO, que es lo que la purga garantiza.
                        builder.NIFContent.SetTriData(Tridata, Config_App.Current.Game <> Config_App.Game_Enum.Skyrim)
                    Else
                        ' ⛔ DIVERGENCIA DELIBERADA CON EL CANÓNICO, decidida por el usuario (24-ago-2026).
                        ' Sin .tri, `BodySlideApp.cpp:4867-4882` NO llama a `SetTriData`: deja el BODYTRI
                        ' heredado del NIF fuente Y borra el .tri del disco, o sea que el NIF construido
                        ' queda apuntando a un archivo que no existe. Acá lo quitamos, que es más sano.
                        ' Antes esto era un `RemoveTriData` por raíz y por shape, que sólo sacaba UNO por
                        ' objetivo y no veía los que colgaran de otro lado; la purga los saca todos.
                        builder.NIFContent.PurgarTodosLosBodyTri()
                    End If

                    ' High Heels. El alta/baja en el diccionario ya la hace la emisión (antes vivía
                    ' acá, y por eso el build con el motor de BodySlide nunca lo actualizaba).
                    ' ⛔⛔ EL .txt DE TACONES TIENE UN SOLO DUEÑO: SaveHighHeelBuild. ACA NO SE ANOTA NADA,
                    ' y el barrido tampoco lo lista como candidato (ver CandidatosDeBarrido).
                    '
                    ' ⛔ ACA HABIA UNA SEGUNDA LEY, y hacia perder el dato del mod. El .txt se anotaba como
                    ' artefacto con `hhEscrito.GetValueOrDefault() OrElse (Not hhEscrito.HasValue AndAlso
                    ' Not DeleteUnbuilt)`, y esa segunda clausula era CODIGO MUERTO: BorrarNoConstruidos
                    ' retorna en su PRIMERA linea cuando DeleteUnbuilt=False, y no hay otro consumidor del
                    ' conjunto. O sea que con `SaveHHS=False` + un proyecto CON tacones —el caso del
                    ' proyecto adoptado— SaveHighHeelBuild devolvia Nothing (no lo gestiono A PROPOSITO,
                    ' "ese artefacto es del mod" dice su docstring) y el barrido lo borraba igual: tacon en
                    ' el piso in-game.
                    '
                    ' ⚠️ CAMBIO DE CONDUCTA DECLARADO, decidido en la ronda del 01-sep. El comentario que
                    ' estaba aca justificaba ese borrado: "apagar SaveHHS dejaba un .txt rancio con la
                    ' altura vieja". Ese costo se ACEPTA, y este es el porque de que se acepte: un .txt
                    ' rancio da una altura equivocada que el usuario puede corregir prendiendo SaveHHS y
                    ' reconstruyendo; un .txt borrado destruye la ULTIMA fuente de autodeteccion de la
                    ' altura (SliderSet_Class.ReadhighHeel la lee de "<base>.txt" cuando no hay .hht), y
                    ' eso WM no lo puede regenerar. No destruir el ultimo ejemplar gana sobre no dejar un
                    ' valor viejo.
                    '
                    ' Sigue siendo solo FO4: en Skyrim el alto de tacones viaja DENTRO del NIF y no existe
                    ' ningun .txt que preservar (SaveHighHeelBuild va por SyncHHOffsetInNif).
                    builder.SaveHighHeelBuild(builder.NIFContent)
                    ProgressBar1.Value += 1


                    ' SSE: ajustar el link in-NIF de física HDT-SMP ("HDT Skinned Mesh Physics Object") al
                    ' path del sidecar de SALIDA (paths de build), o removerlo si el proyecto no tiene física.
                    ' El motor lee ESE path; el sidecar se copia más abajo (una vez, en Sizecount=0). Corre
                    ' por cada size porque cada NIF de salida necesita el link ajustado. Mismo modelo que HH_OFFSET.
                    If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                        If Not String.IsNullOrEmpty(builder.PhysicsXmlContent) Then
                            builder.NIFContent.SetSmpPhysicsXmlPath(SliderSet_Class.BuildSmpInNifPath(builder.OutputFullPathBase + ".xml"))
                        Else
                            builder.NIFContent.RemoveSmpPhysicsExtraData()
                        End If
                    End If

                    ' Grabo nif
                    builder.NIFContent.Save_As_Manolo(fil, True)
                    ArtefactosDelBuild.Add(fil)
                    Dim nifRelative As String = IO.Path.GetRelativePath(Directorios.Fallout4data, fil).Correct_Path_Separator
                    FilesDictionary_class.AddOrUpdateDictionaryEntry(nifRelative, New FilesDictionary_class.File_Location With {
                        .BA2File = "", .Index = -1, .FullPath = nifRelative, .FileDate = Date.Now})

                    ProgressBar1.Value += 1



                    If Sizecount = 0 Then
                        ' Grabo archivo tri
                        Dim triRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, tri).Correct_Path_Separator
                        ' Para el barrido de "no construidos": el .tri cuenta como artefacto de este build
                        ' tanto si se escribió como si se CONSERVÓ a propósito (triBlocked = .tri ajeno o
                        ' ilegible, o PreventMorphFile sin IgnorePreventri). Si sólo contara lo escrito, el
                        ' barrido borraría justo el que el build decidió no tocar, y encima después de
                        ' haberle dicho al usuario "kept the existing .tri".
                        ' ⛔ `triBlocked` sale del veredicto UNICO, que ahora se calcula SIEMPRE. Antes se
                        ' calculaba solo dentro de `If SaveTri`, y con el default de fabrica (SaveTri=False)
                        ' quedaba en False: un .tri ajeno o ilegible NO entraba en el conjunto y el barrido
                        ' lo borraba.
                        If triWritten OrElse triBlocked OrElse
                           (builder.PreventMorphFile AndAlso Not WM_Config.Current.Settings_Build.IgnorePreventri) Then
                            ArtefactosDelBuild.Add(tri)
                        End If
                        If triWritten Then
                            FilesDictionary_class.AddOrUpdateDictionaryEntry(triRelative, New FilesDictionary_class.File_Location With {
                                .BA2File = "", .Index = -1, .FullPath = triRelative, .FileDate = Date.Now})
                        ElseIf Not WM_Config.Current.Settings_Build.SaveTri AndAlso triDueno = DuenoDelTri.Nuestro AndAlso (builder.PreventMorphFile = False OrElse WM_Config.Current.Settings_Build.IgnorePreventri) Then
                            ' BodySlideApp.cpp: sin morphs, el .tri viejo queda huerfano — nadie lo
                            ' referencia ya (el BODYTRI se quito arriba) pero se empaqueta igual en el FOMOD/BA2
                            ' con morphs de una geometria que ya cambio. Solo se borra si es NUESTRO (PIRT);
                            ' un FRTRI003 ajeno —o un .tri que no se pudo leer— nunca se toca.
                            '
                            ' ⛔ EL PREDICADO ES EL VEREDICTO UNICO, no una segunda lectura del header. Aca
                            ' vivia `If ExistingTriIsBodySlide(tri)`, que era la SEGUNDA derivacion del
                            ' mismo hecho: abria el archivo otra vez y podia contestar distinto que el
                            ' `triBlocked` de arriba si el estado del archivo cambiaba en el medio (lock,
                            ' hidratacion de OneDrive). `triDueno = Nuestro` implica `Not triBlocked`, asi
                            ' que la condicion vieja queda subsumida.
                            '
                            ' El guard de PreventMorphFile es obligatorio: en BSOS `triKeep` apaga TANTO la
                            ' escritura COMO el borrado, asi que un proyecto marcado "prevent morph file"
                            ' conserva su .tri intacto. Sin esta condicion lo borrabamos.
                            IO.File.Delete(tri)
                            FilesDictionary_class.RemoveDictionaryEntry(triRelative)
                        End If
                        ' SSE: copia o borra XML de física HDT-SMP junto al NIF de salida (una sola vez, no depende del size)
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                            Dim outXml = builder.OutputFullPathBase + ".xml"
                            Dim xmlRelative = IO.Path.GetRelativePath(Directorios.Fallout4data, outXml).Correct_Path_Separator
                            If Not String.IsNullOrEmpty(builder.PhysicsXmlContent) Then
                                ' ⛔ NO `IO.File.WriteAllText`: pide CREATE_ALWAYS, y CREATE_ALWAYS sobre un
                                ' destino con FILE_ATTRIBUTE_HIDDEN da ACCESS_DENIED (lo deja OneDrive, lo
                                ' dejan los desempaquetadores). Ademas el borrado+alta implicito saca el
                                ' archivo del arbol virtual de MO2 y corta el hardlink de Vortex. La ley y
                                ' su porque viven en Ba2_Bsa_Library\EscrituraEnElLugar.vb.
                                ' Va por `Escribir` y no por `GuardarConCopia`: es SALIDA DE BUILD, se
                                ' regenera en la corrida siguiente.
                                ' conBom:=True = lo que emitía `WriteAllText(..., Encoding.UTF8)`. El
                                ' lector es HDT-SMP; sus bytes no cambian acá.
                                EscribirTextoUtf8(outXml, builder.PhysicsXmlContent, conCopia:=False, conBom:=True)
                                ' ⛔ ACA HABIA UN `ArtefactosDelBuild.Add(outXml)` INERTE: el barrido nunca
                                ' genero un candidato .xml (ver CandidatosDeBarrido), asi que anotarlo no
                                ' protegia nada. Y no hace falta que lo proteja: estas cuatro lineas son el
                                ' ciclo de vida COMPLETO del .xml —se escribe si hay fisica, se borra si no,
                                ' con el diccionario al dia en los dos casos— o sea que no puede quedar un
                                ' .xml rancio para que el barrido lo levante.
                                FilesDictionary_class.AddOrUpdateDictionaryEntry(xmlRelative, New FilesDictionary_class.File_Location With {
                                    .BA2File = "", .Index = -1, .FullPath = xmlRelative, .FileDate = Date.Now})
                            ElseIf IO.File.Exists(outXml) Then
                                IO.File.Delete(outXml)
                                FilesDictionary_class.RemoveDictionaryEntry(xmlRelative)
                            End If
                        End If
                    End If
                    ProgressBar1.Value += 1

                    Nombre = "Unknown"
                    ProgressBar2.Value += IIf(sliderset_target.Multisize, 1, 2)
                Next
                ' ⛔ La base se registra ACA, con el proyecto TERMINADO ENTERO, no apenas se graba el NIF:
                ' despues del NIF todavia pueden tirar el .tri, el .xml de fisica o los registros del
                ' diccionario, y si la base ya estuviera anotada el barrido daria por stale —y borraria—
                ' el .tri o el .txt BUENOS de la corrida anterior, que este build no llego a rehacer.
                ' Es el base REAL que uso el escritor: con ForceClonedOnBuild no es el del proyecto original.
                BasesDelBuild.Add(builder.OutputFullPathBase)
                'ComparadorTrip.CompararArchivos(tri, tri.Replace("_WM.tri", "_WM.tri2"))
            Catch ex As Exception
                ' Sin el mensaje de la excepcion el dialog final solo listaba nombres de proyecto,
                ' que no alcanza para distinguir un .tri no escrito de un load fallido.
                If Errores <> "" Then Errores += vbCrLf
                Errores += Nombre & ": " & ex.Message
            End Try

        Next
        OSP_Project_Class.Default_Memory_Pause = False
        ' Grabo archivo sliders.json
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 AndAlso WM_Config.Current.Settings_Build.AddAddintionalSliders AndAlso WM_Config.Current.Settings_Build.SaveTri Then LooksMenuSliders.Serialize_LooksmenuAdditionalSiliders()

        ' Mostrar los issues de load acumulados durante todo el batch en un solo dialog
        If Not Headless AndAlso buildLoadContext.Issues IsNot Nothing AndAlso buildLoadContext.Issues.Count > 0 Then
            Dim batchHandler = OSP_Project_Class.InteractiveIssueBatchDisplay
            If batchHandler IsNot Nothing Then
                Try
                    batchHandler.Invoke(buildLoadContext.Issues)
                Catch
                End Try
            End If
        End If

        If Errores <> "" AndAlso Not Headless Then
            MsgBox("Error building the following projects:" & vbCrLf & Errores)
        End If
        Return Errores
    End Function


End Class
