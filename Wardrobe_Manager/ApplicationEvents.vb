' Version Uploaded of Wardrobe 3.2.0
Namespace My
    ' The following events are available for MyApplication:
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and the application is already active. 
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.

    ' **NEW** ApplyApplicationDefaults: Raised when the application queries default values to be set for the application.

    ' Example:
    ' Private Sub MyApplication_ApplyApplicationDefaults(sender As Object, e As ApplyApplicationDefaultsEventArgs) Handles Me.ApplyApplicationDefaults
    '
    '   ' Setting the application-wide default Font:
    '   e.Font = New Font(FontFamily.GenericSansSerif, 12, FontStyle.Regular)
    '
    '   ' Setting the HighDpiMode for the Application:
    '   e.HighDpiMode = HighDpiMode.PerMonitorV2
    '
    '   ' If a splash dialog is used, this sets the minimum display time:
    '   e.MinimumSplashScreenDisplayTime = 4000
    ' End Sub

    Partial Friend Class MyApplication

        ' HighDpiMode = DpiUnaware: Windows hace bitmap-scaling de la ventana
        ' al DPI del monitor. UI luce algo blurry a >100% pero el LAYOUT es
        ' idéntico a cualquier DPI — fonts/controles no se reescalan, así
        ' las proporciones del header vs preview no cambian (issue real del
        ' Wardrobe_Manager_Form donde el header crecido a 125% comía altura
        ' del preview). Para usar PerMonitorV2 hay que primero hacer que el
        ' GLControl cree backbuffer en pixels físicos (no soportado en la
        ' versión actual de OpenTK).
        Private Sub MyApplication_ApplyApplicationDefaults(sender As Object, e As Microsoft.VisualBasic.ApplicationServices.ApplyApplicationDefaultsEventArgs) Handles Me.ApplyApplicationDefaults
            e.HighDpiMode = HighDpiMode.DpiUnaware
        End Sub

        Private Sub MyApplication_Startup(sender As Object, e As Microsoft.VisualBasic.ApplicationServices.StartupEventArgs) Handles Me.Startup
            ' PRIMERO DE TODO: el handler de AppDomain cubre los hilos que NO son el de UI (el build corre en
            ' background), donde MyApplication.UnhandledException no llega. Ver Shared\CrashReport.vb.
            CrashReport.Install()

            ' Initialize WM-specific hooks for the shared library
            WM_RenderExtensions.InitializeWM()

            ' MODO CONSOLA. Se atiende ANTES de crear la ventana principal: si TryRun reconoce un modo
            ' (--build / --list / --help) corre ahi mismo y cancela el arranque de la GUI, igual que el
            ' --bake-all de NPC_Manager. Sin argumentos reconocidos devuelve False y la app abre normal.
            ' Va DESPUES de InitializeWM porque el build usa los hooks que registra.
            If WM_Cli.TryRun(e.CommandLine) Then
                e.Cancel = True
                Return
            End If
            ' Logger habilitado SOLO en Debug builds. En Release: Logger.Enabled queda en False y todos los
            ' Logger.Log/LogLazy retornan early sin allocar — y, mas importante, TODOS los bloques
            ' `If Logger.Enabled Then ...` de diagnostico no corren. ⭐ DOBLE CANDADO: ademas de este
            ' `#If DEBUG`, el propio setter de Logger.Enabled DESCARTA cualquier True en Release (ver
            ' Logger.vb), asi que un `Logger.Enabled = True` suelto en release no prende nada.
#If DEBUG Then
            FO4_Base_Library.Logger.Enabled = True
            FO4_Base_Library.Logger.Initialize(IO.Path.Combine(System.Windows.Forms.Application.StartupPath, "fo4lib.log"))
#End If
        End Sub

        ''' <summary>⛔ NO VA POR <c>Logger</c>: en Release <c>Logger.Enabled</c> queda en False y su setter
        ''' descarta cualquier True, asi que esto logueaba a NINGUN LADO y el cartel igual decia "Details have
        ''' been logged". CrashReport escribe un archivo de verdad y muestra donde quedo.
        ''' <para>⛔ Y TERMINA. Con <c>ExitApplication = False</c> la app seguia viva despues de una excepcion no
        ''' controlada, con el estado que hubiera quedado — en una app que ESCRIBE NIF eso es corrupcion
        ''' silenciosa, que es peor que cerrarse.</para></summary>
        Private Sub MyApplication_UnhandledException(sender As Object, e As Microsoft.VisualBasic.ApplicationServices.UnhandledExceptionEventArgs) Handles Me.UnhandledException
            CrashReport.Report(e.Exception, "unhandled")
            e.ExitApplication = True
        End Sub

    End Class
End Namespace
