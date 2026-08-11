Imports System.IO
Imports System.Runtime.InteropServices
Imports FO4_Base_Library

''' <summary>
''' Modo CONSOLA de Wardrobe Manager: emite los NIF de un <c>.osp</c> entero o de un proyecto suelto,
''' sin abrir la GUI. Es el equivalente del <c>--bake-all</c> de NPC_Manager.
'''
''' ⭐ NO reimplementa el build. Instancia el MISMO <see cref="BuildingForm"/> que usa la interfaz y
''' llama a <see cref="BuildingForm.RunBuild"/> con <see cref="BuildingForm.Headless"/> prendido: los
''' NIF salen por el camino de siempre, con la misma resolución de sliders, zaps, tri y físicas. Si
''' fuera un camino paralelo, probarlo headless no diría nada del build real.
'''
''' Uso:
''' <code>
''' Wardrobe_Manager.exe --build --osp &lt;archivo.osp&gt; [--project &lt;nombre&gt;]...
'''                      [--preset &lt;nombre|archivo.xml&gt;] [--preset-name &lt;nombre dentro del xml&gt;]
'''                      [--size big|small|default] [--game fo4|sse]
'''                      [--executable &lt;ruta al exe del juego&gt;] [--bsexe &lt;ruta al exe de BodySlide&gt;]
'''                      [--tri|--no-tri] [--recalc-normals|--no-recalc-normals]
'''                      [--force-weights|--no-force-weights]
''' Wardrobe_Manager.exe --list --osp &lt;archivo.osp&gt;
''' </code>
''' Códigos de salida: 0 ok · 2 argumentos inválidos · 3 no se pudo cargar el .osp ·
''' 4 el proyecto pedido no existe · 5 el build reportó errores.
''' </summary>
Friend Module WM_Cli

    ' ============================================================================================
    ' Consola para un WinExe
    ' ============================================================================================
    ' El proyecto es <OutputType>WinExe</OutputType>, asi que Windows NO le da consola: todo
    ' Console.WriteLine se pierde salvo que el llamador redirija. AttachConsole(-1) nos engancha a la
    ' consola PADRE (el cmd/PowerShell que nos lanzo); si no hay, AllocConsole abre una propia para
    ' que un run headless nunca sea mudo. Mismo patron que Program.vb de NPC_Manager.
    Private Const ATTACH_PARENT_PROCESS As Integer = -1

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function AttachConsole(dwProcessId As Integer) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function AllocConsole() As Boolean
    End Function

    ''' <summary>Engancha stdout/stderr a una consola real y los reabre en UTF-8. La reapertura es
    ''' obligatoria: el BCL cachea Console.Out en el primer uso y en un WinExe ese handle cacheado es
    ''' el dispositivo nulo, asi que escribir sin reabrir sigue siendo invisible aun ya enganchados.
    ''' Los nombres de proyecto y de slider llevan acentos, y una consola recien abierta esta en una
    ''' codepage OEM que los muestra como mojibake.</summary>
    Private Sub EnsureConsole()
        ' Con consola en mano el reporte de caida va por stderr: un MessageBox modal en un --build headless
        ' colgaria la corrida hasta que alguien lo cierre. Ver Shared\CrashReport.vb.
        CrashReport.UseConsole()
        If Not AttachConsole(ATTACH_PARENT_PROCESS) Then AllocConsole()
        Try
            Dim utf8 As New Text.UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
            Try
                Console.OutputEncoding = utf8
            Catch
                ' Algunos hosts rechazan el cambio de codepage (pipes redirigidos, terminales raras).
                ' El writer de abajo igual emite UTF-8 valido para quien lea el stream.
            End Try
            Dim so = Console.OpenStandardOutput()
            If so IsNot Stream.Null Then Console.SetOut(New StreamWriter(so, utf8) With {.AutoFlush = True})
            Dim se = Console.OpenStandardError()
            If se IsNot Stream.Null Then Console.SetError(New StreamWriter(se, utf8) With {.AutoFlush = True})
        Catch
            ' Sin consola (contexto de servicio/sesion 0). El build corre igual; solo no se ve.
        End Try
    End Sub

    ' ============================================================================================
    ' Parseo de argumentos
    ' ============================================================================================

    Private Function HasFlag(args As List(Of String), name As String) As Boolean
        Return args.Any(Function(a) String.Equals(a, name, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>Valor que sigue a una opcion, o Nothing. Devuelve el PRIMERO.</summary>
    Private Function OptValue(args As List(Of String), name As String) As String
        For i = 0 To args.Count - 2
            If String.Equals(args(i), name, StringComparison.OrdinalIgnoreCase) Then Return args(i + 1)
        Next
        Return Nothing
    End Function

    ''' <summary>Todos los valores de una opcion repetible (p. ej. --project A --project B).</summary>
    Private Function OptValues(args As List(Of String), name As String) As List(Of String)
        Dim res As New List(Of String)
        For i = 0 To args.Count - 2
            If String.Equals(args(i), name, StringComparison.OrdinalIgnoreCase) Then res.Add(args(i + 1))
        Next
        Return res
    End Function

    ''' <summary>Par de flags on/off. Devuelve Nothing si no vino ninguno (⇒ se respeta el config).</summary>
    Private Function TriState(args As List(Of String), onName As String, offName As String) As Boolean?
        If HasFlag(args, offName) Then Return False
        If HasFlag(args, onName) Then Return True
        Return Nothing
    End Function

    ' ============================================================================================
    ' Entrada
    ' ============================================================================================

    ''' <summary>Devuelve True si atendio un modo de consola (el llamador debe cancelar la GUI).</summary>
    Public Function TryRun(argv As IEnumerable(Of String)) As Boolean
        Dim args = If(argv, Enumerable.Empty(Of String)()).ToList()
        If args.Count = 0 Then Return False

        Dim quiereBuild = HasFlag(args, "--build")
        Dim quiereList = HasFlag(args, "--list")
        Dim quiereHelp = HasFlag(args, "--help") OrElse HasFlag(args, "-h") OrElse HasFlag(args, "/?")
        If Not quiereBuild AndAlso Not quiereList AndAlso Not quiereHelp Then Return False

        EnsureConsole()
        If quiereHelp Then
            PrintUsage()
            Environment.ExitCode = 0
            Return True
        End If

        Try
            Environment.ExitCode = Run(args, quiereList)
        Catch ex As Exception
            Console.Error.WriteLine("ERROR: " & ex.GetType().Name & ": " & ex.Message)
            Console.Error.WriteLine(ex.StackTrace)
            Environment.ExitCode = 1
        End Try
        Return True
    End Function

    Private Sub PrintUsage()
        Console.WriteLine("Wardrobe Manager — modo consola")
        Console.WriteLine()
        Console.WriteLine("  --build --osp <archivo.osp> [opciones]   construye los NIF")
        Console.WriteLine("  --list  --osp <archivo.osp>              lista los proyectos del .osp y sale")
        Console.WriteLine()
        Console.WriteLine("Opciones:")
        Console.WriteLine("  --project <nombre>      construye solo ese sliderset (repetible; por defecto, todos)")
        Console.WriteLine("  --preset <nombre|.xml>  preset a aplicar; si es una ruta .xml se carga de ahi")
        Console.WriteLine("  --preset-name <nombre>  cual preset del .xml, cuando el archivo tiene varios")
        Console.WriteLine("  --size big|small|default   peso a construir (default: big)")
        Console.WriteLine("  --game fo4|sse")
        Console.WriteLine("  --executable <exe>      exe del juego (de ahi sale la carpeta Data de salida)")
        Console.WriteLine("  --bsexe <exe>           exe de BodySlide (de ahi salen SliderSets/SliderCategories)")
        Console.WriteLine("  --tri | --no-tri                       fuerza SaveTri")
        Console.WriteLine("  --recalc-normals | --no-recalc-normals")
        Console.WriteLine("  --force-weights | --no-force-weights")
        Console.WriteLine()
        Console.WriteLine("Salida: 0 ok · 2 args · 3 .osp ilegible · 4 proyecto inexistente · 5 errores de build")
    End Sub

    ' ============================================================================================
    ' Cuerpo
    ' ============================================================================================

    Private Function Run(args As List(Of String), soloListar As Boolean) As Integer
        Dim ospPath = OptValue(args, "--osp")
        If String.IsNullOrWhiteSpace(ospPath) Then
            Console.Error.WriteLine("Falta --osp <archivo.osp>.")
            Return 2
        End If
        ospPath = Path.GetFullPath(ospPath)
        If Not File.Exists(ospPath) Then
            Console.Error.WriteLine("No existe el .osp: " & ospPath)
            Return 3
        End If

        ' ── Configuracion persistida ─────────────────────────────────────────────────────────────
        ' ⛔ Va ANTES de aplicar los overrides de la linea de comandos, para que el orden sea
        ' archivo -> flags. Sin esto el headless corria con los defaults del codigo e IGNORABA la
        ' configuracion del usuario: la misma build desde la UI y desde el CLI podian dar distinto
        ' (suavizado de costura, epsilons, welding, normalizacion), y ningun barrido de opciones
        ' sobre el CLI medía nada porque los cambios del config no llegaban.
        Config_App.LoadConfig()
        WM_Config.LoadConfig()

        ' ── Config: juego y rutas ────────────────────────────────────────────────────────────────
        ' FO4EDataPath y BsPath son COMPUTADAS a partir de los exe (Config_Class / WM_Config), asi que
        ' apuntar la salida es apuntar el exe. Es tambien lo que permite armar un sandbox sintetico.
        Dim juego = OptValue(args, "--game")
        If Not String.IsNullOrWhiteSpace(juego) Then
            Select Case juego.ToLowerInvariant()
                Case "fo4", "fallout4", "fallout"
                    Config_App.Current.Game = Config_App.Game_Enum.Fallout4
                Case "sse", "skyrim", "skyrimse"
                    Config_App.Current.Game = Config_App.Game_Enum.Skyrim
                Case Else
                    Console.Error.WriteLine("--game invalido: " & juego & " (usar fo4 o sse)")
                    Return 2
            End Select
        End If

        Dim exeJuego = OptValue(args, "--executable")
        If Not String.IsNullOrWhiteSpace(exeJuego) Then Config_App.Current.FO4ExePath = Path.GetFullPath(exeJuego)
        Dim exeBs = OptValue(args, "--bsexe")
        If Not String.IsNullOrWhiteSpace(exeBs) Then WM_Config.Current.BSExePath = Path.GetFullPath(exeBs)

        ' ── Overrides de opciones de build ───────────────────────────────────────────────────────
        ' Settings_Build es una Structure devuelta POR VALOR desde una propiedad: mutar
        ' `Current.Settings_Build.X` escribiria sobre una copia temporal. Se lee, se muta y se
        ' reescribe entera.
        Dim tri = TriState(args, "--tri", "--no-tri")
        Dim fw = TriState(args, "--force-weights", "--no-force-weights")
        If tri.HasValue OrElse fw.HasValue Then
            Dim sb = WM_Config.Current.Settings_Build
            If tri.HasValue Then sb.SaveTri = tri.Value
            If fw.HasValue Then sb.ForceWeights = fw.Value
            WM_Config.Current.Settings_Build = sb
        End If
        Dim rn = TriState(args, "--recalc-normals", "--no-recalc-normals")
        If rn.HasValue Then Config_App.Current.Setting_RecalculateNormals = rn.Value

        ' ── Carga del .osp ───────────────────────────────────────────────────────────────────────
        Dim ctx = ProjectLoadContext.CreateCollectOnly(False)
        Dim osp As OSP_Project_Class
        Try
            osp = New OSP_Project_Class(ospPath, True, ctx)
        Catch ex As Exception
            Console.Error.WriteLine("No se pudo cargar el .osp: " & ex.Message)
            Return 3
        End Try
        If osp.SliderSets Is Nothing OrElse osp.SliderSets.Count = 0 Then
            Console.Error.WriteLine("El .osp no tiene sliderSets: " & ospPath)
            Return 3
        End If

        If soloListar Then
            Console.WriteLine(ospPath)
            For Each ss In osp.SliderSets
                Console.WriteLine("  {0}   (sliders={1}, shapes={2}, genWeights={3})",
                                  ss.Nombre, ss.Sliders.Count, ss.Shapes.Count, ss.GenWeights)
            Next
            Return 0
        End If

        ' ── Seleccion de proyectos ───────────────────────────────────────────────────────────────
        Dim pedidos = OptValues(args, "--project")
        Dim lista As SliderSet_Class()
        If pedidos.Count = 0 Then
            lista = osp.SliderSets.ToArray()
        Else
            lista = osp.SliderSets.
                Where(Function(ss) pedidos.Any(Function(n) String.Equals(n, ss.Nombre, StringComparison.OrdinalIgnoreCase))).
                ToArray()
            Dim faltantes = pedidos.Where(Function(n) Not osp.SliderSets.Any(
                Function(ss) String.Equals(n, ss.Nombre, StringComparison.OrdinalIgnoreCase))).ToList()
            If faltantes.Count > 0 Then
                Console.Error.WriteLine("No estan en el .osp: " & String.Join(", ", faltantes))
                Return 4
            End If
        End If

        ' ── Preset ───────────────────────────────────────────────────────────────────────────────
        Dim preset As SlidersPreset_Class = Nothing
        Dim nombrePreset = OptValue(args, "--preset")
        ' ⛔ MEDIDO 2026-08-03: sin esto, un .xml con MAS DE UN preset adentro era inalcanzable. Se
        ' cargaba el archivo, no se elegia ninguno (el atajo de abajo pide Count = 1) y despues el
        ' lookup por nombre buscaba LA RUTA como si fuera un nombre de preset ⇒ salia con 4.
        ' ManoloPresets.xml tiene 3 presets: ninguno se podia construir desde el CLI.
        Dim nombreDentro = OptValue(args, "--preset-name")
        If Not String.IsNullOrWhiteSpace(nombrePreset) Then
            If WM_SliderPresets Is Nothing Then WM_SliderPresets = New SliderPresetCollection
            If nombrePreset.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) AndAlso File.Exists(nombrePreset) Then
                WM_SliderPresets.LoadFromXml(Path.GetFullPath(nombrePreset))
                ' El nombre del preset dentro del archivo puede no coincidir con el del archivo. Con
                ' uno solo se toma ese; con varios manda --preset-name.
                If Not String.IsNullOrWhiteSpace(nombreDentro) Then
                    Dim elegido As SlidersPreset_Class = Nothing
                    If WM_SliderPresets.Presets.TryGetValue(nombreDentro, elegido) Then
                        preset = elegido
                    Else
                        preset = WM_SliderPresets.Presets.Values.FirstOrDefault(
                            Function(pr) String.Equals(pr.Name, nombreDentro, StringComparison.OrdinalIgnoreCase))
                    End If
                    If preset Is Nothing Then
                        Console.Error.WriteLine("El preset '" & nombreDentro & "' no esta en " & nombrePreset)
                        If WM_SliderPresets.Presets.Count > 0 Then
                            Console.Error.WriteLine("Contiene: " & String.Join(", ", WM_SliderPresets.Presets.Keys))
                        End If
                        Return 4
                    End If
                ElseIf WM_SliderPresets.Presets.Count = 1 Then
                    preset = WM_SliderPresets.Presets.Values.First()
                ElseIf WM_SliderPresets.Presets.Count > 1 Then
                    Console.Error.WriteLine(nombrePreset & " tiene " & WM_SliderPresets.Presets.Count &
                                            " presets: hace falta --preset-name <nombre>.")
                    Console.Error.WriteLine("Contiene: " & String.Join(", ", WM_SliderPresets.Presets.Keys))
                    Return 2
                End If
            End If
            If preset Is Nothing Then
                Dim hallado As SlidersPreset_Class = Nothing
                If WM_SliderPresets.Presets.TryGetValue(nombrePreset, hallado) Then
                    preset = hallado
                Else
                    preset = WM_SliderPresets.Presets.Values.FirstOrDefault(
                        Function(pr) String.Equals(pr.Name, nombrePreset, StringComparison.OrdinalIgnoreCase))
                End If
            End If
            If preset Is Nothing Then
                Console.Error.WriteLine("No se encontro el preset: " & nombrePreset)
                If WM_SliderPresets.Presets.Count > 0 Then
                    Console.Error.WriteLine("Disponibles: " & String.Join(", ", WM_SliderPresets.Presets.Keys))
                End If
                Return 4
            End If
        End If

        ' ── Peso ─────────────────────────────────────────────────────────────────────────────────
        ' Informativo: BuildingForm decide el peso por sliderset (Multisize ⇒ los dos pases; si no,
        ' Big). Se expone igual porque es lo que el usuario espera poder pedir.
        Dim size = OptValue(args, "--size")
        If Not String.IsNullOrWhiteSpace(size) AndAlso
           Not {"big", "small", "default"}.Contains(size.ToLowerInvariant()) Then
            Console.Error.WriteLine("--size invalido: " & size & " (usar big, small o default)")
            Return 2
        End If

        ' ── Build ────────────────────────────────────────────────────────────────────────────────
        Console.WriteLine("osp      : " & ospPath)
        Console.WriteLine("proyectos: " & String.Join(", ", lista.Select(Function(x) x.Nombre)))
        Console.WriteLine("preset   : " & If(preset Is Nothing, "(ninguno — defaults del sliderset)", preset.Name))
        Console.WriteLine("juego    : " & Config_App.Current.Game.ToString())
        Console.WriteLine("salida   : " & Wardrobe_Manager_Form.Directorios.Fallout4data)
        Console.WriteLine("tri      : " & WM_Config.Current.Settings_Build.SaveTri)
        Console.WriteLine()

        Dim errores As String
        Using f As New BuildingForm(lista, preset, New Poses_class())
            f.Headless = True
            errores = f.RunBuild()
        End Using

        If Not String.IsNullOrWhiteSpace(errores) Then
            Console.Error.WriteLine("Errores de build:")
            Console.Error.WriteLine(errores)
            Return 5
        End If

        Console.WriteLine("OK — " & lista.Length & " proyecto(s) construido(s).")
        Return 0
    End Function

End Module
