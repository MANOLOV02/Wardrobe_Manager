Imports OpenTK.Mathematics
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading.Tasks
Imports FO4_Base_Library

' ============================================================================
' LooksMenu Slider Descriptors and TRI Build Logic
' MorphdataTri = slider metadata for LooksMenu JSON export (not TRI I/O).
' TRI binary I/O is handled entirely by FO4_Base_Library.TriFileParser/TriFileWriter.
' ============================================================================

''' <summary>
''' LooksMenu slider descriptor. Used for JSON serialization of slider metadata
''' and as an intermediate when building TRI files from SliderSet data.
''' NOT a TRI file format class - TRI I/O uses FO4_Base_Library.TriFile directly.
''' </summary>
Public Class MorphdataTri
    <JsonPropertyName("name")>
    Public Property Name As String = ""

    <JsonPropertyName("morph")>
    Public Property Morph As String = ""
    <JsonPropertyName("minimum")>
    Public Property Minimum As Single = 0.0F
    <JsonPropertyName("maximum")>
    Public Property Maximum As Single = 1.0F
    <JsonPropertyName("interval")>
    Public Property Interval As Single = 0.01F

    ''' <summary>0 = masculino, 1 = femenino (BodyMorphInterface.cpp / ScaleformNatives.cpp).
    ''' Default 0 para empatar a jsoncpp: `entry["gender"].asInt()` sobre un miembro AUSENTE devuelve 0
    ''' (json_value.cpp, `case nullValue: return 0`). Con default 1 un sliders.json ajeno sin el campo
    ''' se leia como femenino, el de-dupe acertaba de casualidad y WM no emitia su propia entrada.
    ''' WM siempre serializa el campo explicito, asi que su propio archivo no se ve afectado.</summary>
    <JsonPropertyName("gender")>
    Public Property Gender As Integer = 0

    <JsonIgnore>
    Public MorphType As TriMorphType = TriMorphType.Position

    <JsonIgnore>
    Public Offsets As New Dictionary(Of UShort, Vector3)()
End Class

''' <summary>
''' LooksMenu slider management and TRI build from SliderSet data.
''' </summary>
Public Module LooksMenuSliders

    Private BSSliders As New List(Of MorphdataTri)
    Private WMSliders As New List(Of MorphdataTri)
    Private ReadOnly Jsonopts As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .NumberHandling = JsonNumberHandling.AllowReadingFromString,
        .WriteIndented = True
    }

    ''' <summary>
    ''' Carga los sliders ya registrados por OTROS mods, para no volver a emitirlos.
    '''
    ''' El motor (BodyMorphInterface::LoadBodyGenSliderMods) guarda en m_sliderMap[gender][morph] por
    ''' ASIGNACION: el ultimo en load order gana. Mirar solo CBBE.esp dejaba que WM re-emitiera los
    ''' morphs de cualquier otro body (Fusion Girl, Atomic Beauty, BodyTalk) y le pisara su name
    ''' localizado y su min/max/interval.
    '''
    ''' ⚠️ Esto NO es un espejo exacto de ForEachMod (Utilities.cpp), que itera los PLUGINS
    ''' CARGADOS, no el directorio. Estado de los dos huecos que tenia:
    '''   • De mas: una carpeta Sliders\&lt;plugin&gt;\ cuyo ESP ya no esta en el load order la vemos
    '''     nosotros y el motor no ⇒ suprimimos un morph que in-game falta. ⛔ ES DECISION DE DISEÑO,
    '''     no un bug pendiente: WM no tiene seleccion de plugin, asi que toma todas las carpetas.
    '''     La direccion es la SEGURA (no emitimos ⇒ no pisamos nada) y cuesta un slider ausente.
    '''     Es la direccion que hay que preferir, justamente porque fallar del otro lado PISA.
    '''   • De menos: LoadBodyGenSliders abre con BSResourceNiBinaryStream, o sea que tambien levanta
    '''     un sliders.json EMPAQUETADO EN BA2. ✅ CERRADO: <see cref="ReadForeignSlidersFromArchives"/>
    '''     hace la segunda pasada por FilesDictionary_class, que aplica la misma precedencia
    '''     suelto-sobre-archive que el motor.
    ''' </summary>
    Public Sub Read_Looksmenu_Sliders()
        BSSliders = ReadForeignLooksMenuSliders()
        WMSliders = New List(Of MorphdataTri)
        If WM_Config.Current.Settings_Build.ResetSlidersEachBuild = False Then
            WMSliders = DeserializeLooksMenuSliders(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        End If
    End Sub

    ''' <summary>Raiz de los sliders de f4ee, relativa a Data. Literal del motor:
    ''' <c>std::string sliderPath("F4SE\\Plugins\\F4EE\\Sliders\\")</c>
    ''' (BodyMorphInterface.cpp).</summary>
    Private Const F4eeSlidersRoot As String = "F4SE\Plugins\F4EE\Sliders"

    ''' <summary>
    ''' Sliders adicionales que YA declaro otro mod. Re-emitir uno ajeno NO es cosmetico: el registro
    ''' del motor es una ASIGNACION, asi que gana el ULTIMO en cargar y le pisa al mod ajeno su
    ''' <c>name</c> localizado, su <c>min/max/interval</c> y su <c>sort</c>:
    ''' <code>m_sliderMap[pBodySlider->gender][pBodySlider->morph] = pBodySlider;</code>
    ''' (BodyMorphInterface.cpp). ⛔ No confundir con el <c>emplace</c> first-wins del lector de
    ''' <c>.osd</c> (DiffData.cpp): otro archivo, contrato opuesto.
    '''
    ''' El motor los descubre por DOS caminos distintos, y no son intercambiables:
    ''' <list type="number">
    ''' <item><b>Por mod</b> (<c>LoadBodyGenSliderMods</c>): una carpeta por plugin, leida con
    ''' <c>BSResourceNiBinaryStream</c> — o sea por el SISTEMA DE RECURSOS, que resuelve
    ''' <b>suelto O dentro de un BA2</b>. Escanear solo el disco dejaba invisible a todo mod que
    ''' shippee su <c>sliders.json</c> empaquetado.</item>
    ''' <item><b>Loose</b>: <c>IDirectoryIterator</c> sobre <c>Data\...\Sliders\Loose\*.json</c>,
    ''' que es FILESYSTEM PURO. Un json de Loose metido en un BA2 el motor NO lo ve, asi que acá
    ''' tampoco se busca ahi.</item>
    ''' </list>
    ''' ⛔ El camino por mod del motor solo recorre los plugins CARGADOS (<c>ForEachMod</c>) y nombra la
    ''' carpeta con <c>modInfo->name</c>. WM no tiene seleccion de plugin: por decision de diseño toma
    ''' TODAS las carpetas. De mas es inofensivo (a lo sumo se auto-cede un nombre); de menos seria
    ''' emitir un duplicado.
    ''' </summary>
    Private Function ReadForeignLooksMenuSliders() As List(Of MorphdataTri)
        Dim result As New List(Of MorphdataTri)
        Dim ourFolder As String = ""
        Dim leidasDelDisco As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Try
            Dim ourFile = Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders
            Dim slidersRoot = IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(ourFile))
            ourFolder = IO.Path.GetFileName(IO.Path.GetDirectoryName(ourFile))
            If Not String.IsNullOrEmpty(slidersRoot) AndAlso IO.Directory.Exists(slidersRoot) Then
                ' 'dir' colisiona con la funcion Dir() del runtime de VB — no usar como iterador.
                For Each sliderDir In IO.Directory.EnumerateDirectories(slidersRoot)
                    Dim leaf = IO.Path.GetFileName(sliderDir)
                    If leaf.Equals(ourFolder, StringComparison.OrdinalIgnoreCase) Then Continue For
                    If leaf.Equals("Loose", StringComparison.OrdinalIgnoreCase) Then
                        ' El motor escanea Loose\*.json sin depender de ningun plugin.
                        For Each looseFile In IO.Directory.EnumerateFiles(sliderDir, "*.json")
                            result.AddRange(DeserializeLooksMenuSliders(looseFile))
                        Next
                    Else
                        Dim jsonPath = IO.Path.Combine(sliderDir, "sliders.json")
                        If IO.File.Exists(jsonPath) Then
                            leidasDelDisco.Add(leaf)
                            result.AddRange(DeserializeLooksMenuSliders(jsonPath))
                        End If
                    End If
                Next
            End If
        Catch
            ' Best-effort: sin lista de ajenos el de-dupe queda mas conservador, no incorrecto.
        End Try

        result.AddRange(ReadForeignSlidersFromArchives(ourFolder, leidasDelDisco))
        Return result
    End Function

    ''' <summary>
    ''' Segunda pasada: los <c>sliders.json</c> que viven DENTRO de un BA2/BSA. Los resuelve el
    ''' FilesDictionary, que es el equivalente de WM al sistema de recursos del motor (y ya aplica la
    ''' misma precedencia: un suelto le gana al archive). Se saltean las carpetas que la pasada de
    ''' disco ya leyo — leerlas de nuevo desde el archive duplicaria la entrada Y le daria prioridad
    ''' al archive sobre el suelto, al reves que el motor.
    ''' Solo FO4: los additional sliders son de LooksMenu/f4ee y no existen en SSE.
    ''' </summary>
    Private Function ReadForeignSlidersFromArchives(ourFolder As String,
                                                    leidasDelDisco As HashSet(Of String)) As List(Of MorphdataTri)
        Dim result As New List(Of MorphdataTri)
        If Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then Return result
        Try
            For Each key In FilesDictionary_class.GetFilteredKeys(F4eeSlidersRoot, {".json"})
                If Not key.EndsWith("\sliders.json", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim folder = IO.Path.GetFileName(IO.Path.GetDirectoryName(key))
                If String.IsNullOrEmpty(folder) Then Continue For
                If folder.Equals(ourFolder, StringComparison.OrdinalIgnoreCase) Then Continue For
                ' Loose es filesystem puro para el motor: un json empaquetado ahi NO lo carga.
                If folder.Equals("Loose", StringComparison.OrdinalIgnoreCase) Then Continue For
                If leidasDelDisco.Contains(folder) Then Continue For

                Dim bytes = FilesDictionary_class.GetBytes(key)
                If bytes Is Nothing OrElse bytes.Length = 0 Then Continue For
                result.AddRange(DeserializeLooksMenuSlidersJson(Text.Encoding.UTF8.GetString(bytes)))
            Next
        Catch
            ' Best-effort, igual que la pasada de disco.
        End Try
        Return result
    End Function

    ''' <summary>
    ''' Equivalente de <c>!v.IsZero(true)</c> de BSOS, el filtro de emision de offsets
    ''' (TriFile.cpp / Object3d.hpp): descarta el vertice solo si los TRES componentes
    ''' quedan por debajo de EPSILON. Comparar contra cero exacto dejaba en el .tri sumas residuales
    ''' (p. ej. 1.5e-4 + -1.0e-4) que BodySlide descarta.
    ''' </summary>
    Private Function IsOffsetNegligible(x As Single, y As Single, z As Single) As Boolean
        Return Math.Abs(x) < OSD_Class.OsdDiffEpsilon AndAlso
               Math.Abs(y) < OSD_Class.OsdDiffEpsilon AndAlso
               Math.Abs(z) < OSD_Class.OsdDiffEpsilon
    End Function

    ''' <summary>
    ''' Valores de "gender" que WM debe emitir, en la codificacion del motor: 0 = masculino,
    ''' 1 = femenino (BodyMorphInterface.cpp indexa m_sliderMap[gender][morph], y
    ''' ScaleformNatives.cpp pasa gender==1 como isFemale). CBBE emite 1 en sus 83 entradas.
    ''' </summary>
    Private Function EngineGenders() As Integer()
        Select Case WM_Config.Current.Settings_Build.AdditionalSlidersGender
            Case WM_Config.SliderGender.Male
                Return New Integer() {0}
            Case WM_Config.SliderGender.Both
                Return New Integer() {0, 1}
            Case Else
                Return New Integer() {1}
        End Select
    End Function

    Public Function DeserializeLooksMenuSliders(sliderFile As String) As List(Of MorphdataTri)
        Try
            If Not IO.File.Exists(sliderFile) Then Return New List(Of MorphdataTri)
            Return DeserializeLooksMenuSlidersJson(IO.File.ReadAllText(sliderFile))
        Catch
            Return New List(Of MorphdataTri)
        End Try
    End Function

    ''' <summary>
    ''' El mismo parseo pero desde el CONTENIDO, para los sliders.json que salen de un BA2.
    '''
    ''' ⛔ NO usa <c>JsonSerializer.Deserialize</c>: System.Text.Json y jsoncpp discrepan en cinco
    ''' puntos, y CUATRO caen del lado peligroso (damos por ocupado un nombre que el motor SI
    ''' registra ⇒ no emitimos el nuestro ⇒ le pisamos la metadata al mod ajeno, porque
    ''' <c>m_sliderMap[gender][morph] = pBodySlider</c> es asignacion y gana el ultimo). Se replica a
    ''' mano lo que hace <c>BodySlider::Parse</c> (BodyMorphInterface.cpp) sobre los valores
    ''' de jsoncpp:
    ''' <list type="bullet">
    ''' <item><c>name</c>/<c>morph</c>: <c>asCString()</c>. Sobre cualquier cosa que no sea string
    ''' (ausente, null, numero, bool, objeto) LANZA ⇒ <c>Parse</c> devuelve false ⇒ la entrada no se
    ''' registra. ⚠️ Un string VACIO explicito si lo registra el motor; aca se descarta igual, porque
    ''' una clave vacia no puede colisionar con ningun morph real nuestro.</item>
    ''' <item><c>gender</c>: <c>asInt()</c> — null ⇒ 0, bool ⇒ 0/1, real ⇒ trunca, <b>string ⇒ LANZA</b>.
    ''' Y despues <c>if(gender == 0 || gender == 1)</c> sobre un <c>UInt8</c>
    ''' (BodyMorphInterface.h), o sea con el valor TRUNCADO a 8 bits: 256 entra como masculino.</item>
    ''' <item><c>minimum</c>/<c>maximum</c>/<c>interval</c>: <c>asFloat()</c>, mismas reglas. No los
    ''' mira nadie para decidir el registro, pero un null en cualquiera de ellos hacia que
    ''' <c>Deserialize</c> tirara y — con el Try envolviendo el array — nos costaba el ARCHIVO entero.</item>
    ''' <item>Comentarios <c>//</c>: <c>Json::Reader</c> se construye con <c>Features::all()</c>, o sea
    ''' <c>allowComments_ = true</c>. <c>JsonDocument</c> los rechaza por default ⇒ se pasa
    ''' <c>CommentHandling.Skip</c>. Las comas colgando y NaN/Infinity los rechazan LOS DOS, asi que
    ''' ahi el default sirve.</item>
    ''' <item>Root que es un OBJETO en vez de un array: <c>for(auto&amp; item : root)</c> itera
    ''' los MIEMBROS de un objeto igual que los elementos de un array. Se replica.</item>
    ''' </list>
    ''' Un json que ni siquiera parsea devuelve lista vacia: el motor loguea y sale.
    ''' </summary>
    Public Function DeserializeLooksMenuSlidersJson(json As String) As List(Of MorphdataTri)
        Dim result As New List(Of MorphdataTri)
        If String.IsNullOrWhiteSpace(json) Then Return result
        Try
            ' BOM: File.ReadAllText lo saca solo, pero los bytes crudos del BA2 no.
            Using doc = JsonDocument.Parse(json.TrimStart(ChrW(&HFEFF)), JsonDocOpts)
                Dim items As IEnumerable(Of JsonElement)
                Select Case doc.RootElement.ValueKind
                    Case JsonValueKind.Array
                        items = doc.RootElement.EnumerateArray().ToList()
                    Case JsonValueKind.Object
                        items = doc.RootElement.EnumerateObject().Select(Function(pr) pr.Value).ToList()
                    Case Else
                        Return result
                End Select

                ' POR ENTRADA, no el array entero: el motor descarta UNA entrada, no el archivo
                ' (BodyMorphInterface.cpp).
                For Each item In items
                    Dim entry = ParseSliderEntry(item)
                    If entry IsNot Nothing Then result.Add(entry)
                Next
            End Using
        Catch
            Return New List(Of MorphdataTri)
        End Try
        Return result
    End Function

    ''' <summary>Opciones que empatan a <c>Json::Reader</c> con <c>Features::all()</c>: comentarios
    ''' permitidos, comas colgando NO (jsoncpp tambien las rechaza).</summary>
    Private ReadOnly JsonDocOpts As New JsonDocumentOptions With {
        .CommentHandling = JsonCommentHandling.Skip,
        .AllowTrailingCommas = False
    }

    ''' <summary>Replica de <c>BodySlider::Parse</c> (BodyMorphInterface.cpp) + el gate de
    ''' gender. Devuelve Nothing cuando el motor NO registraria la entrada.
    ''' ⛔ El <c>Try</c> envuelve TODO el cuerpo a proposito: en el canonico el <c>catch</c> es POR
    ''' ENTRADA y devolver false descarta esa entrada y nada mas. Sin el, una conversion estrecha de
    ''' VB (que lanza <c>OverflowException</c>, no satura) subia hasta el Try del ARCHIVO y nos
    ''' costaba el sliders.json entero — el modo de falla que esto vino a eliminar.</summary>
    Private Function ParseSliderEntry(item As JsonElement) As MorphdataTri
        Try
            If item.ValueKind <> JsonValueKind.Object Then Return Nothing

            Dim nombre As String = Nothing
            Dim morph As String = Nothing
            If Not TryAsCString(item, "name", nombre) Then Return Nothing
            If Not TryAsCString(item, "morph", morph) Then Return Nothing
            If nombre.Length = 0 OrElse morph.Length = 0 Then Return Nothing

            ' asInt(): sobre un real, jsoncpp comprueba el rango de Int32 y LANZA si no entra
            ' (value.cpp, JSON_ASSERT_MESSAGE(InRange(...))) => la entrada no se registra.
            Dim gender As Integer, sort As Integer
            If Not TryAsInt(item, "gender", gender) Then Return Nothing
            If Not TryAsInt(item, "sort", sort) Then Return Nothing

            ' asFloat() es `static_cast<float>(asDouble())`: NO comprueba rango, satura a +/-inf.
            Dim minimo As Single, maximo As Single, intervalo As Single
            If Not TryAsFloat(item, "minimum", minimo) Then Return Nothing
            If Not TryAsFloat(item, "maximum", maximo) Then Return Nothing
            If Not TryAsFloat(item, "interval", intervalo) Then Return Nothing

            ' `UInt8 gender` (BodyMorphInterface.h) cargado desde asInt(): trunca a 8 bits ANTES del
            ' `if(gender == 0 || gender == 1)`. Un 256 entra como masculino.
            Dim g As Integer = gender And &HFF
            If g <> 0 AndAlso g <> 1 Then Return Nothing

            Return New MorphdataTri With {
                .Name = nombre,
                .Morph = morph,
                .Gender = g,
                .Minimum = minimo,
                .Maximum = maximo,
                .Interval = intervalo
            }
        Catch
            ' Igual que el catch de Parse: se descarta ESTA entrada, no el archivo.
            Return Nothing
        End Try
    End Function

    ''' <summary><c>asCString()</c>: solo un string JSON pasa; el resto lanza en jsoncpp.
    ''' Un miembro AUSENTE es <c>nullValue</c>, que tambien lanza.</summary>
    Private Function TryAsCString(obj As JsonElement, nombre As String, ByRef valor As String) As Boolean
        Dim el As JsonElement
        If Not obj.TryGetProperty(nombre, el) Then Return False
        If el.ValueKind <> JsonValueKind.String Then Return False
        valor = el.GetString()
        Return valor IsNot Nothing
    End Function

    ''' <summary><c>asInt()</c>: ausente/null ⇒ 0; bool ⇒ 0/1; entero o real ⇒ trunca hacia cero, pero
    ''' <b>lanza si no entra en Int32</b>; string, objeto o array ⇒ lanza. Lanzar = descartar la
    ''' entrada.</summary>
    Private Function TryAsInt(obj As JsonElement, nombre As String, ByRef valor As Integer) As Boolean
        valor = 0
        Dim d As Double
        If Not TryAsRaw(obj, nombre, d) Then Return False
        If Double.IsNaN(d) Then Return False
        Dim t As Double = Math.Truncate(d)
        If t < Integer.MinValue OrElse t > Integer.MaxValue Then Return False
        valor = CInt(t)
        Return True
    End Function

    ''' <summary><c>asFloat()</c> = <c>static_cast&lt;float&gt;(asDouble())</c>: SIN comprobacion de
    ''' rango, satura a infinito. Mismas reglas de tipo que <see cref="TryAsInt"/>.</summary>
    Private Function TryAsFloat(obj As JsonElement, nombre As String, ByRef valor As Single) As Boolean
        valor = 0.0F
        Dim d As Double
        If Not TryAsRaw(obj, nombre, d) Then Return False
        If Double.IsNaN(d) Then
            valor = Single.NaN
        ElseIf d > Single.MaxValue Then
            valor = Single.PositiveInfinity
        ElseIf d < Single.MinValue Then
            valor = Single.NegativeInfinity
        Else
            valor = CSng(d)
        End If
        Return True
    End Function

    ''' <summary>Valor numerico crudo con las reglas de tipo de jsoncpp. False = lanza = descartar.</summary>
    Private Function TryAsRaw(obj As JsonElement, nombre As String, ByRef valor As Double) As Boolean
        valor = 0
        Dim el As JsonElement
        If Not obj.TryGetProperty(nombre, el) Then Return True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined, JsonValueKind.False
                Return True
            Case JsonValueKind.True
                valor = 1
                Return True
            Case JsonValueKind.Number
                Return el.TryGetDouble(valor)
            Case Else
                Return False
        End Select
    End Function


    Public Sub Serialize_LooksmenuAdditionalSiliders()
        Try
            If WMSliders.Count > 0 Then
                Dim dir = IO.Path.GetDirectoryName(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
                If Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)
                Dim jsonOut = JsonSerializer.Serialize(Of List(Of MorphdataTri))(WMSliders, Jsonopts)
                ' ⛔ NO `IO.File.WriteAllText`: CREATE_ALWAYS sobre un destino OCULTO da ACCESS_DENIED, y
                ' el borrado+alta implícito saca el archivo del árbol virtual de MO2 y corta el hardlink
                ' de Vortex. Ver Ba2_Bsa_Library\EscrituraEnElLugar.vb.
                ' ⛔ Y VA CON COPIA aunque WM lo reescriba en cada build. El archivo vive en el árbol de
                ' un mod de TERCEROS (F4SE\Plugins\F4EE\Sliders\...\sliders.json) y WM no puede probar
                ' autoría exclusiva de su contenido: si alguien lo editó a mano o lo aporta otro mod,
                ' pisarlo sin red destruye el único ejemplar. El costo es UN archivo por build.
                ' conBom:=False = lo que emitía WriteAllText sin encoding (UTF8NoBOM); el lector es
                ' LooksMenu y sus bytes no se cambian acá.
                EscribirTextoUtf8(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders, jsonOut, conCopia:=True, conBom:=False)
            Else
                If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then
                    IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
                End If
            End If
        Catch ex As Exception
            MsgBox("Error creating additional sliders for looksmenu", vbCritical, "Error")
        End Try
    End Sub

    ''' <summary>
    ''' Build a TRI file from a SliderSet and write it to disk.
    ''' Constructs FO4_Base_Library.TriFile directly - no wrapper class.
    ''' </summary>
    ''' <param name="skipShapes">
    ''' Shapes que no deben aportar morphs. Se usa para las 100 % zapeadas que se conservan ocultas:
    ''' BodySlide tampoco las emite (con la geometria intacta, su erase de rangos deja todos los
    ''' offsets en cero y el morph no se agrega — BodySlideApp.cpp).
    ''' </param>
    Public Function WriteMorphTRI(triPath As String, sliderSet As SliderSet_Class,
                                  Optional skipShapes As HashSet(Of String) = Nothing) As Boolean
        Dim tri As New TriFile()
        ' Gate por juego, igual que la serializacion al cierre del batch: sliders.json es de LooksMenu,
        ' o sea FO4. RaceMenu no tiene registro equivalente — enumera los morphs del propio .tri con
        ' NiOverride.GetMorphNames (PapyrusNiOverride.cpp). Sin el gate, un batch de Skyrim
        ' acumulaba entradas en WMSliders que despues nadie escribia.
        Dim addAdditional = WM_Config.Current.Settings_Build.AddAddintionalSliders AndAlso
                            Config_App.Current.Game = Config_App.Game_Enum.Fallout4
        Dim genders = EngineGenders()

        ' Candidate sliders (not Clamp/Zap) don't depend on the shape — build once.
        ' Los sliders Fix (IsManoloFix) SÍ se emiten: son morphs reales del canónico. Los fix-ZAP ya
        ' quedan afuera por el predicado canónico de zap de abajo — no hace falta (ni debe haber)
        ' una condición aparte para ellos.
        ' ⛔⛔ EL PREDICADO DEL ZAP ES `ResolveSlider(...).Kind`, NO `IsZap`. Acá decía `Not IsZap` a secas,
        ' y la ley canónica —MorphingHelper, tomada de BodySlideApp::BuildListBodies— es
        ' `bZap && !bUV`: un slider con IsZap=True Y IsUV=True NO es un zap, es un morph UV. O sea que ese
        ' slider se APLICABA como UvMorph en el render y en el bake, pero quedaba fuera de los candidatos
        ' del .tri ⇒ se perdía del archivo, en silencio. Que TriFiles sabe emitir morphs UV lo prueba el
        ' bloque de abajo, que ya hace `.MorphType = If(slider.IsUV, TriMorphType.UV, …)`.
        Dim candidateIndices As New List(Of Integer)
        For s = 0 To sliderSet.Sliders.Count - 1
            Dim esZap = MorphingHelper.ResolveSlider(sliderSet.Sliders(s)).Kind = SliderKind.Zap
            If Not sliderSet.Sliders(s).IsClamp AndAlso Not esZap Then
                candidateIndices.Add(s)
            End If
        Next

        For Each shape In sliderSet.NIFContent.GetShapes
            If skipShapes IsNot Nothing AndAlso skipShapes.Contains(shape.Name.String) Then Continue For
            Dim targetShape = sliderSet.Shapes.Where(Function(pf) pf.RelatedNifShape Is shape).FirstOrDefault
            If targetShape Is Nothing Then Continue For
            Dim shapeVertCount As Integer = shape.VertexCount
            If shapeVertCount <= 0 Then Continue For

            ' Pre-allocate for parallel morph quantization
            Dim morphResults(candidateIndices.Count - 1) As TriMorphEntry
            Dim morphNames(candidateIndices.Count - 1) As String
            Dim morphIsUV(candidateIndices.Count - 1) As Boolean

            Dim localTargetShape = targetShape
            Dim localVertCount = shapeVertCount
            Parallel.For(0, candidateIndices.Count,
                Sub(ci)
                    Dim slider = sliderSet.Sliders(candidateIndices(ci))
                    Dim entry As New TriMorphEntry With {
                        .Name = slider.Nombre,
                        .MorphType = If(slider.IsUV, TriMorphType.UV, TriMorphType.Position)
                    }

                    If slider.IsUV Then
                        Dim uvs(localVertCount - 1) As Vector2
                        Dim dat = localTargetShape.Related_Slider_data.
                            Where(Function(pf) pf.ParentSlider Is slider).
                            OrderByDescending(Function(pf) pf.Islocal).FirstOrDefault
                        If dat IsNot Nothing Then
                            For Each dif In dat.RelatedOSDBlocks
                                For Each dif2 In dif.DataDiff
                                    ' Guard against a stale OSD diff whose index exceeds this shape's
                                    ' vertex count — would throw inside Parallel.For and abort the TRI build.
                                    If dif2.Index >= 0 AndAlso dif2.Index < localVertCount Then
                                        ' ACUMULA, no pisa: DiffDataSets::ApplyUVDiff suma (+=) tanto entre
                                        ' indices repetidos dentro de un bloque como el motor de morphs de
                                        ' WM (ApplyMorph_CPU) entre bloques homonimos, que Materialize-
                                        ' EditableLocalBlocks crea deliberadamente. Asignar dejaba el .tri
                                        ' distinto de lo que la propia app renderiza.
                                        uvs(dif2.Index) += New Vector2(dif2.X, dif2.Y)
                                    End If
                                Next
                            Next
                        End If
                        For idxv = 0 To localVertCount - 1
                            If Not IsOffsetNegligible(uvs(idxv).X, uvs(idxv).Y, 0.0F) Then
                                entry.Offsets(CUShort(idxv)) = New Vector3(uvs(idxv).X, uvs(idxv).Y, 0.0F)
                            End If
                        Next
                    Else
                        Dim verts(localVertCount - 1) As Vector3
                        Dim dat = localTargetShape.Related_Slider_data.
                            Where(Function(pf) pf.ParentSlider Is slider).
                            OrderByDescending(Function(pf) pf.Islocal).FirstOrDefault
                        If dat IsNot Nothing Then
                            For Each dif In dat.RelatedOSDBlocks
                                For Each dif2 In dif.DataDiff
                                    ' Guard against a stale OSD diff whose index exceeds this shape's
                                    ' vertex count — would throw inside Parallel.For and abort the TRI build.
                                    If dif2.Index >= 0 AndAlso dif2.Index < localVertCount Then
                                        ' ACUMULA, no pisa — ver la nota en la rama UV de arriba.
                                        verts(dif2.Index) += New Vector3(dif2.X, dif2.Y, dif2.Z)
                                    End If
                                Next
                            Next
                        End If
                        For idxv = 0 To localVertCount - 1
                            If Not IsOffsetNegligible(verts(idxv).X, verts(idxv).Y, verts(idxv).Z) Then
                                entry.Offsets(CUShort(idxv)) = verts(idxv)
                            End If
                        Next
                    End If

                    If entry.Offsets.Count > 0 Then
                        morphResults(ci) = entry
                        morphNames(ci) = slider.Nombre
                        morphIsUV(ci) = slider.IsUV
                    End If
                End Sub)

            ' Sequential merge + WMSliders tracking
            Dim shapeName = shape.Name.String
            For ci = 0 To morphResults.Length - 1
                Dim entry = morphResults(ci)
                If entry IsNot Nothing Then
                    tri.AddMorph(shapeName, entry)
                    ' Los morphs UV NO se registran como sliders: f4ee lee la seccion de posiciones del
                    ' PIRT y retorna sin tocar la seccion UV (BodyMorphInterface.cpp), asi que un
                    ' slider UV en LooksMenu es un control muerto. El .tri SI la sigue llevando porque
                    ' skee64 (RaceMenu) si la lee (BodyMorphInterface.cpp).
                    If addAdditional AndAlso Not morphIsUV(ci) Then
                        Dim mname = morphNames(ci)
                        For Each g In genders
                            Dim gender = g
                            If Not BSSliders.Any(Function(pf) pf.Gender = gender AndAlso pf.Morph.Equals(mname, StringComparison.OrdinalIgnoreCase)) Then
                                If Not WMSliders.Any(Function(pf) pf.Gender = gender AndAlso pf.Morph.Equals(mname, StringComparison.OrdinalIgnoreCase)) Then
                                    WMSliders.Add(New MorphdataTri With {.Name = "$" + mname, .Morph = mname, .Gender = gender})
                                End If
                            End If
                        Next
                    End If
                End If
            Next
        Next

        Return tri.Write(triPath)
    End Function

End Module
