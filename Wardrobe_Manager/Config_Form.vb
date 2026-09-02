' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.Threading

Public Class Config_Form
    Private initialgame As Config_App.Game_Enum
    ' Captured at form Load. Pack/Unpack discover archives via FilesDictionary, which was filled
    ' at startup against this path. If the user retargets to a different game/path mid-session the
    ' dictionary is stale, so we lock the buttons until WM is restarted (or the user reverts).
    Private initialDataPath As String = ""
    Public Sub New()
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()


        ' Enable double-buffering on the two labels that get hammered by progress updates during
        ' Pack/Unpack — without this the Text-per-tick assignments cause visible flicker. Done
        ' via reflection so the Designer can keep these as plain Label (a custom subclass would
        ' be wiped out the next time the Designer round-trips this form).
        EnableDoubleBuffer(PackProgressLabel)
        EnableDoubleBuffer(PackLastActionLabel)
    End Sub

    ''' <summary>
    ''' Turns on double-buffering for an arbitrary Control. Equivalent to deriving a subclass
    ''' that calls SetStyle in its ctor, but applied to an existing instance so we don't have
    ''' to introduce a Designer-incompatible custom control.
    ''' </summary>
    Private Shared Sub EnableDoubleBuffer(c As Control)
        If c Is Nothing Then Return
        Dim prop = GetType(Control).GetProperty(
            "DoubleBuffered",
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        prop?.SetValue(c, True, Nothing)
    End Sub
    ''' <summary>
    ''' ⛔ Queda en False si <see cref="Setea_Build_Options"/> no llego hasta el final. Sin esto,
    ''' CUALQUIER excepcion a mitad de la carga —el Catch de abajo la tragaba en silencio— dejaba los
    ''' controles que faltaban con el valor del Designer, y al cerrar `Graba_Build_Options` los
    ''' escribia encima de la configuracion del usuario. Medido: con `EpsilonPos = 0` (el default) y
    ''' el minimo del control en 1e-12, abrir y cerrar el dialogo revertia EpsilonPos,
    ''' WeldByPositionOnly y TODA la seccion de build.
    ''' </summary>
    Private _cargaCompleta As Boolean = False

    Private Sub Setea_Build_Options()
        _cargaCompleta = False
        Try
            RadioButtonBSEngine.Checked = (WM_Config.Current.Settings_Build.OwnEngine = False)
            RadioButtonWMEngine.Checked = (WM_Config.Current.Settings_Build.OwnEngine = True)
            CheckBoxBuildHH.Checked = WM_Config.Current.Settings_Build.SaveHHS
            CheckBoxBuildTri.Checked = WM_Config.Current.Settings_Build.SaveTri
            CheckBoxDeletewithProject.Checked = WM_Config.Current.Settings_Build.DeleteWithProject
            CheckBoxDeleteBefore.Checked = WM_Config.Current.Settings_Build.DeleteUnbuilt
            CheckBoxLMReseteachBuild.Checked = WM_Config.Current.Settings_Build.ResetSlidersEachBuild
            CheckBoxLMAddAditionals.Checked = WM_Config.Current.Settings_Build.AddAddintionalSliders
            ' Clamp obligatorio: SelectedIndex fuera de rango tira ArgumentOutOfRangeException, y el
            ' Catch de este bloque no reanuda la carga — un valor invalido en wm_config.json deja sin
            ' cargar todo lo que viene despues (IgnorePreventri, BuildInPose, flags de weights, GRID).
            ComboBoxLMGender.SelectedIndex = Math.Clamp(CInt(WM_Config.Current.Settings_Build.AdditionalSlidersGender), 0, ComboBoxLMGender.Items.Count - 1)
            CheckBoxIgnorePrevent.Checked = WM_Config.Current.Settings_Build.IgnorePreventri
            CheckBoxBuildInPose.Checked = WM_Config.Current.Settings_Build.BuildInPose
            CheckBoxForceCloned.Checked = WM_Config.Current.Settings_Build.ForceClonedOnBuild
            CheckBoxweightignore.Checked = WM_Config.Current.Settings_Build.IgnoreWeightsFlags
            RadioButtonAllwaysWeight.Checked = WM_Config.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Checked = Not WM_Config.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Enabled = WM_Config.Current.Settings_Build.IgnoreWeightsFlags AndAlso WM_Config.Current.Settings_Build.OwnEngine
            RadioButtonAllwaysWeight.Enabled = WM_Config.Current.Settings_Build.IgnoreWeightsFlags AndAlso WM_Config.Current.Settings_Build.OwnEngine
            CheckBoxweightignore.Enabled = WM_Config.Current.Settings_Build.OwnEngine = True

            _cargaCompleta = True

        Catch ex As Exception
            ' No se traga en silencio: se avisa Y se bloquea el guardado, porque guardar desde una
            ' pantalla a medio cargar destruye la configuracion.
            MessageBox.Show("Could not load all settings into this dialog, so nothing will be saved when it closes." & vbCrLf & vbCrLf & ex.Message,
                            "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try

    End Sub
    Private Sub Graba_Build_Options()
        ' ⛔ ACA NO HAY NADA DE RENDER, Y NO ES UN OLVIDO. Normales/TBN/welding, skinning, camara y grilla
        ' del piso viven en Config_App (la libreria) y los edita el dialogo COMPARTIDO
        ' FO4_Base_Library.LightRigForm, pestana "Rendering", que tambien usa FO4_NPC_Manager. NO agregar
        ' una pestana Rendering aca: dejarla visible con la escritura desactivada hace que el usuario
        ' destilde algo y se descarte en silencio; dejar la escritura viva pisa al cerrar la config que el
        ' dialogo compartido acaba de guardar, con los valores del Designer. Es el modo de falla que
        ' documenta _cargaCompleta arriba.

        Dim buildSet = New WM_Config.BuildSettings With {
            .DeleteUnbuilt = CheckBoxDeleteBefore.Checked,
            .DeleteWithProject = CheckBoxDeletewithProject.Checked,
            .OwnEngine = Not RadioButtonBSEngine.Checked,
            .SaveHHS = CheckBoxBuildHH.Checked,
            .SaveTri = CheckBoxBuildTri.Checked,
            .ResetSlidersEachBuild = CheckBoxLMReseteachBuild.Checked,
            .AddAddintionalSliders = CheckBoxLMAddAditionals.Checked,
            .AdditionalSlidersGender = CType(Math.Max(0, ComboBoxLMGender.SelectedIndex), WM_Config.SliderGender),
            .IgnorePreventri = CheckBoxIgnorePrevent.Checked,
            .BuildInPose = CheckBoxBuildInPose.Checked,
            .IgnoreWeightsFlags = CheckBoxweightignore.Checked,
         .ForceWeights = RadioButtonAllwaysWeight.Checked,
            .ForceClonedOnBuild = CheckBoxForceCloned.Checked
                    }

        WM_Config.Current.Settings_Build = buildSet
    End Sub

    ''' <summary>
    ''' Block form closure while a Pack/Unpack is in flight. If the user really wants to close,
    ''' offer to stop the operation first; on confirmation we trigger the same safe Cancel that
    ''' the Stop button uses and let the worker reach its next checkpoint. Closing again after
    ''' the operation finishes proceeds normally.
    ''' </summary>
    Private Sub Config_Form_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Dim cts = _packCts
        If cts Is Nothing OrElse cts.IsCancellationRequested Then Return

        e.Cancel = True
        Dim ans = MessageBox.Show(
            "A Pack/Unpack operation is still running. Stop it safely and close after the current archive finishes?",
            "Operation in progress",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If ans = DialogResult.Yes Then
            StopButton.Enabled = False
            StopButton.Text = "Stopping…"
            PackProgressLabel.Text = "Stop requested — finishing current archive safely…"
            cts.Cancel()
        End If
    End Sub

    Private Sub Config_Form_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        TextBox1.Text = Config_App.Current.FO4ExePath
        TextBox2.Text = WM_Config.Current.BSExePath
        TextBox3.Text = WM_Config.Current.OSExePath
        TextBox4.Text = Config_App.Current.SkeletonPath
        ComboBoxGame.SelectedIndex = Config_App.Current.Game
        initialgame = Config_App.Current.Game
        initialDataPath = If(Config_App.Current.FO4EDataPath, "")
        RefreshPluginsTxtRow()
        Setea_Build_Options()
        Button8.Enabled = IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        Check_Folders()
        Check_GameMismatch()
        InitClonedMaterialTab()
    End Sub

    ' ====== Cloned Material tab logic ======
    ' All UI controls for this tab live in Config_Form.Designer.vb (TabPagePack and children).
    ' Code-behind only owns the run-state fields (CTS, elapsed timer, started-at).
    Private _packStartedAt As DateTime
    Private _packElapsedTimer As System.Windows.Forms.Timer
    Private _packCts As CancellationTokenSource

    Private Sub InitClonedMaterialTab()
        _packElapsedTimer = New System.Windows.Forms.Timer() With {.Interval = 1000}
        AddHandler _packElapsedTimer.Tick, AddressOf PackElapsedTimer_Tick
        ' Index 0 = v8 (Next Gen, default); Index 1 = v1 (Old Gen / universal). WM no ofrece Loose.
        Ba2VersionUI.PopulateBa2VersionCombo(PackBa2VersionCombo, includeLoose:=False)
        PackBa2VersionCombo.SelectedIndex = Ba2VersionUI.Ba2VersionToComboIndex(WM_Config.Current.Ba2Version_FO4)
        UpdateBa2VersionVisibility()
        RefreshClonedMaterialStatus()
    End Sub

    ''' <summary>
    ''' The BA2 header version selector is FO4-only: when packing for SSE the packer writes
    ''' BSA v105 (no version choice), so the control is hidden. Defensive against the same
    ''' during-InitializeComponent reentrancy documented in RefreshClonedMaterialStatus.
    ''' </summary>
    Private Sub UpdateBa2VersionVisibility()
        If PackBa2VersionCombo Is Nothing OrElse PackBa2VersionLabel Is Nothing Then Return
        Dim isFo4 As Boolean = (Config_App.Current.Game = Config_App.Game_Enum.Fallout4)
        PackBa2VersionLabel.Visible = isFo4
        PackBa2VersionCombo.Visible = isFo4
    End Sub

    Private Sub PackBa2VersionCombo_SelectedIndexChanged(sender As Object, e As EventArgs) Handles PackBa2VersionCombo.SelectedIndexChanged
        ' Index 0 = v8 (Next Gen, default); Index 1 = v1 (Old Gen / universal).
        WM_Config.Current.Ba2Version_FO4 = Ba2VersionUI.ComboIndexToBa2Version(PackBa2VersionCombo.SelectedIndex)
    End Sub

    Private Sub PackElapsedTimer_Tick(sender As Object, e As EventArgs)
        If Not PackElapsedLabel.Visible Then Return
        Dim elapsed = DateTime.UtcNow - _packStartedAt
        PackElapsedLabel.Text = $"Elapsed: {Math.Floor(elapsed.TotalMinutes):00}:{elapsed.Seconds:00}"
    End Sub

    Private Sub StopButton_Click(sender As Object, e As EventArgs) Handles StopButton.Click
        Dim cts = _packCts
        If cts Is Nothing OrElse cts.IsCancellationRequested Then Return
        StopButton.Enabled = False
        StopButton.Text = "Stopping…"
        PackProgressLabel.Text = "Stop requested — finishing current archive safely…"
        cts.Cancel()
    End Sub

    ''' <summary>True cuando <c>PackLastActionLabel</c> tiene un RESULTADO de Pack/Unpack que tiene que
    ''' sobrevivir a <see cref="RefreshClonedMaterialStatus"/>.
    ''' <para>⛔ EXISTE PARA SACARLE LA SEMÁNTICA AL COLOR. El refresh borraba el label mirando si el
    ''' <c>ForeColor</c> era <c>DarkRed</c>; como la rama de fallo del unpack pinta DarkRed y el refresh
    ''' corre en el <c>Finally</c> del mismo handler, el mensaje de error —con el aviso de archives sin
    ''' remontar y la ruta del reporte— se borraba en el mismo turno. Con un flag propio, el color puede
    ''' elegirse por SEVERIDAD sin cambiar la conducta: pintar DarkOrange para esquivar el borrado habría
    ''' sido mentirle al usuario sobre la gravedad.</para></summary>
    Private _packLabelPersistente As Boolean

    ''' <summary>El RESULTADO persistente (texto y color) del último Pack/Unpack, y el AVISO de estado del
    ''' refresh, guardados POR SEPARADO. El <c>Text</c> del label es siempre la composición de los dos.
    ''' <para>⛔⛔ ESTO REEMPLAZA LA CIRUGÍA DE STRINGS, y con ella tres defectos de una vez. Antes el
    ''' aviso de estado se APENDEABA al texto del label, y de ahí salían: (1) el apéndice NUNCA se
    ''' retiraba —la limpieza vivía en la rama del contexto, que no corre cuando el flag está prendido—,
    ''' así que un error transitorio quedaba pegado al resultado para siempre; (2) un error DISTINTO al
    ''' primero se SUPRIMÍA, porque la guarda de duplicado miraba el literal genérico
    ''' <c>"Error reading status"</c> y no <i>este</i> error; y (3) el color quedaba amarrado a una sola
    ''' de las dos cosas. Recomponiendo, el aviso es un CAMPO que vale lo que valga hoy —o <c>""</c> si el
    ''' refresh salió limpio— y las tres desaparecen: no hay qué retirar, no hay qué deduplicar, y cada
    ''' parte se pinta por su cuenta.</para></summary>
    Private _resultadoTexto As String = ""
    Private _resultadoColor As Drawing.Color = SystemColors.ControlText
    Private _avisoEstado As String = ""

    ''' <summary>Punto ÚNICO por el que se escribe un resultado persistente en el label del Pack/Unpack.
    ''' Prende el flag; el color es sólo presentación.
    ''' <para>Que sea uno solo es el punto: prender el flag rama por rama a mano es exactamente cómo se
    ''' olvida la que importa.</para>
    ''' <para>Son <b>SIETE</b> ramas, no ocho: <c>PackButton_Click</c> tiene tres (cancelación, éxito y
    ''' fallo general — no tiene una rama parcial, porque <c>Pack</c> no tiene un equivalente de
    ''' <c>UnpackParcialException</c>) y <c>UnpackButton_Click</c> tiene cuatro (cancelación, éxito,
    ''' unpack parcial y fallo general). El gate cuenta esas siete llamadas EXACTAS: ver D9.6.</para></summary>
    Private Sub EscribirResultadoPack(texto As String, color As Drawing.Color)
        _resultadoTexto = texto
        _resultadoColor = color
        _packLabelPersistente = True
        ComponerLabelPack()
    End Sub

    ''' <summary>Arma el <c>Text</c> del label a partir de las DOS piezas. Es el único que escribe el
    ''' control, así que no hay estado guardado en el texto ni nadie que tenga que recortarlo.
    ''' <para>El color sale de la SEVERIDAD: si hay aviso de estado manda <c>DarkRed</c> —no poder leer el
    ''' estado del disco es lo más grave que hay para mostrar acá— y si no, el color del resultado. El
    ''' aviso deja de estar amarrado al color del resultado y viceversa.</para></summary>
    ''' <summary>Escribe un texto TRANSITORIO en el label ("Starting…", el hito por archive) fijando los
    ''' campos y recomponiendo, pero SIN prender el flag de persistente.
    ''' <para>⛔⛔ TIENE QUE PASAR POR LOS CAMPOS AUNQUE SEA TRANSITORIO. Estas tres escrituras iban
    ''' directo al control, así que <c>_resultadoTexto</c> se quedaba con el resultado de la corrida
    ''' ANTERIOR; y como <c>RefreshClonedMaterialStatus</c> recompone desde los campos, cualquier refresh
    ''' a mitad de la corrida —cambiar <c>ComboBoxGame</c> con el pack en vuelo, que
    ''' <c>SetPackButtonsBusy</c> NO bloquea— RESUCITABA ese resultado viejo encima del "Starting…". Con
    ''' la recomposición, "lo que el label muestra" dejó de poder vivir sólo en el control.</para>
    ''' <para>NO prende el flag: es transitorio, y el refresh puede barrerlo cuando ya no haya nada que
    ''' conservar. Eso lo diferencia de <see cref="EscribirResultadoPack"/>.</para></summary>
    Private Sub EscribirTransitorio(texto As String)
        _resultadoTexto = texto
        _resultadoColor = SystemColors.ControlText
        ComponerLabelPack()
    End Sub

    Private Sub ComponerLabelPack()
        If PackLastActionLabel Is Nothing Then Return
        Dim partes = {_resultadoTexto, _avisoEstado}.Where(Function(p) Not String.IsNullOrEmpty(p))
        PackLastActionLabel.Text = String.Join("  |  ", partes)
        PackLastActionLabel.ForeColor = If(_avisoEstado <> "", Drawing.Color.DarkRed, _resultadoColor)
    End Sub

    Private Sub RefreshClonedMaterialStatus()
        ' Defensive: this gets called from ComboBoxGame.SelectedIndexChanged, which fires DURING
        ' InitializeComponent() the moment the Designer assigns ComboBoxGame.SelectedIndex — before
        ' the rest of the Pack tab controls have been instantiated. Check the LAST-created Pack
        ' control (PackLastActionLabel, declared at the end of the InitializeComponent block) so
        ' the early reentrant call bails out cleanly. Once Form_Load runs the explicit
        ' RefreshClonedMaterialStatus invocation populates everything correctly.
        If PackLastActionLabel Is Nothing Then Return

        Try
            Dim s = WM_PackUnpack.GetStatus()
            Dim mb As Func(Of Long, String) = Function(b) (b / 1024.0 / 1024.0).ToString("N1") & " MB"

            Dim looseTotal = s.LooseMaterialCount + s.LooseTextureCount
            PackStatusLooseValue.Text =
                $"{looseTotal:N0}  ({s.LooseMaterialCount:N0} materials, {s.LooseTextureCount:N0} textures)"
            PackStatusLooseSizeValue.Text = mb(s.LooseTotalBytes)
            Dim packedTotal = s.PackedMaterialCount + s.PackedTextureCount
            PackStatusArchivesValue.Text =
                $"{s.Plugins.Count:N0} plugins ({s.PackedMaterialCount:N0} materials, {s.PackedTextureCount:N0} textures)"
            PackStatusArchiveSizeValue.Text = mb(s.ArchiveTotalBytes)

            Dim contextValid = IsClonedMaterialContextValid()
            Dim hasLoose = looseTotal > 0
            Dim hasArchives = s.Archives.Count > 0
            PackButton.Enabled = contextValid AndAlso hasLoose
            UnpackButton.Enabled = contextValid AndAlso hasArchives

            ' ⛔⛔ EL COLOR NO ES ESTADO, y el TEXTO tampoco. El aviso de este método es un CAMPO
            ' (`_avisoEstado`) que se recalcula entero en cada pasada, y el label se RECOMPONE a partir de
            ' él y del resultado persistente. Antes acá se escribía el control directo y el aviso vivía
            ' dentro del string: de ahí salían el apéndice que no se retiraba nunca y la deduplicación por
            ' literal. Recomponiendo, "no hay aviso" es simplemente `""`.
            If Not contextValid Then
                _avisoEstado =
                    "Game / data path changed since startup. Pack/Unpack disabled until you " &
                    "revert to the original game and path, or close and reopen Wardrobe Manager " &
                    "(the file dictionary needs to be rebuilt against the new target)."
                ' ⛔⛔ ACA NO SE APAGA EL FLAG, y el motivo que había escrito era FALSO. Decía "este aviso
                ' se re-deriva cada refresh, no es un resultado que conservar" — cierto, pero el flag no
                ' gobierna al AVISO: `_avisoEstado` ya se re-deriva incondicionalmente en las dos ramas
                ' de este If. Lo único que apagarlo lograba era MATAR EL RESULTADO: un ida-y-vuelta del
                ' ComboBoxGame (inválido → válido) borraba el WARNING de archives sin remontar, que en la
                ' rama de ÉXITO del unpack es su ÚNICO portador — sin modal y sin log en Release.
                ' Si el contexto inválido tiene algo que decir, lo dice `_avisoEstado`; no se dice matando
                ' un resultado que sigue siendo cierto.
            Else
                _avisoEstado = ""
                ' ⛔ EL TRANSITORIO TAMBIÉN ES ESTADO DEL LABEL. Con el flag en False —el PRIMER Pack de
                ' la sesión, donde todavía no hay resultado— un refresh a mitad de la corrida entraba acá
                ' y VACIABA el "Starting…"/el hito por archive hasta el tick siguiente. Y ese refresh es
                ' alcanzable con el pack EN VUELO: `SetPackButtonsBusy` deshabilita los botones pero no
                ' bloquea `ComboBoxGame.SelectedIndexChanged`. Antes de la recomposición el transitorio
                ' sobrevivía —vivía en el control— así que esto sería una regresión del propio rediseño.
                If Not _packLabelPersistente AndAlso Not _packEnCurso Then
                    ' Ni resultado que conservar ni corrida en curso: el label queda vacío.
                    _resultadoTexto = ""
                    _resultadoColor = SystemColors.ControlText
                End If
            End If
            ComponerLabelPack()
        Catch ex As Exception
            ' Surface the throwing call site so first-chance exceptions in the debugger point at
            ' the actual root cause, not at this catch line.
            Dim site = ex.TargetSite
            Dim where = If(site Is Nothing, "(unknown)", $"{site.DeclaringType?.Name}.{site.Name}")
            PackStatusLooseValue.Text = "—"
            PackStatusLooseSizeValue.Text = "—"
            PackStatusArchivesValue.Text = "—"
            PackStatusArchiveSizeValue.Text = "—"
            ' ⛔⛔ EL AVISO ES UN CAMPO Y EL LABEL SE RECOMPONE: el resultado persistente NO se pisa y el
            ' aviso NO se pega al texto. `GetStatus` toca el disco y un `FileNotFoundException` transitorio
            ' es normal acá (MO2 / OneDrive / el AV borrando un .dds entre la enumeración y el stat).
            ' Las tres formas que esto reemplaza, y lo que cada una rompía:
            '   · escribir el error INCONDICIONALMENTE destruía el resultado del último Pack/Unpack — y el
            '     caso peor es el ÉXITO del unpack, donde el label es el ÚNICO portador del WARNING de
            '     archives sin remontar y de la ruta del .txt (no hay modal). Peor aún, correlacionado: si
            '     se perdieron permisos, el remonte falla Y el stat tira, o sea que el aviso se borraba
            '     POR SU PROPIA CAUSA;
            '   · SUPRIMIRLO cuando había resultado salvaba el resultado pero perdía el diagnóstico para
            '     siempre si el fallo no era transitorio;
            '   · APENDEARLO al string dejaba un apéndice que nunca se retiraba (la limpieza vivía en la
            '     rama del contexto, que con el flag prendido no corre) y suprimía un error DISTINTO al
            '     primero, porque la guarda de duplicado miraba el literal genérico y no ESTE error.
            ' Con el campo: siempre está el aviso ACTUAL (o ninguno), siempre está el resultado, y no hay
            ' nada que retirar ni que deduplicar.
            _avisoEstado = $"Error reading status [{where}]: {ex.Message}"
            ComponerLabelPack()
            PackButton.Enabled = False
            UnpackButton.Enabled = False
        End Try
    End Sub

    Private Function IsClonedMaterialContextValid() As Boolean
        If Config_App.Current.Game <> initialgame Then Return False
        Dim currentDataPath = If(Config_App.Current.FO4EDataPath, "")
        Return String.Equals(currentDataPath, initialDataPath, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Async Sub PackButton_Click(sender As Object, e As EventArgs) Handles PackButton.Click
        SetPackButtonsBusy(True)
        _packCts = New CancellationTokenSource()
        EscribirTransitorio("Starting pack…")
        Try
            Dim progress As New Progress(Of WM_PackUnpack.PackProgress)(AddressOf OnPackProgress)
            Dim result = Await WM_PackUnpack.PackAsync(progress, _packCts.Token)
            ' ⛔ EL RESUMEN FINAL NOMBRA LOS HUERFANOS. Este label se arma con los conteos del
            ' PackagerResult y NO lee el stream de progreso, así que sin esto el aviso —que sí viaja por
            ' ReportStage— quedaba sólo en el label de progreso y el resumen no mencionaba nada.
            ' El aviso de huérfanos (benigno) y el de remonte (GRAVE: contenido invisible) van los dos al
            ' resumen persistente. El segundo es el que no tiene otro portador.
            Dim huerfanos = WM_PackUnpack.UltimoAvisoHuerfanos & WM_PackUnpack.UltimoAvisoRemontePack
            If _packCts.IsCancellationRequested Then
                EscribirResultadoPack($"Pack stopped by user. Wrote {result.Archives.Count} archive(s), " &
                                      $"{result.Plugins.Count} new plugin(s) before stop. Remaining loose files left untouched." &
                                      huerfanos, Drawing.Color.DarkOrange)
            Else
                EscribirResultadoPack($"Pack complete. Wrote {result.Archives.Count} archive(s), " &
                                      $"{result.Plugins.Count} new plugin(s); skipped {result.Skipped.Count} unchanged." &
                                      huerfanos, SystemColors.ControlText)
            End If
        Catch ex As Exception
            ' ⛔ LA RAMA DE FALLO TAMBIÉN NOMBRA SU AVISO. Era la ÚNICA de las siete que no lo hacía, y el
            ' `.txt` de huérfanos ya está escrito en disco para cuando se llega acá: sin esto, su ruta no
            ' aparecía en ningún lado y el archivo quedaba huérfano de su propio reporte.
            Dim avisoHuerfanosFallo = WM_PackUnpack.UltimoAvisoHuerfanos
            EscribirResultadoPack("Pack failed: " & ex.Message & avisoHuerfanosFallo, Drawing.Color.DarkRed)
            MessageBox.Show(ex.ToString() & If(avisoHuerfanosFallo = "", "",
                                               Environment.NewLine & Environment.NewLine & avisoHuerfanosFallo.Trim()),
                            "Pack failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _packCts?.Dispose()
            _packCts = Nothing
            SetPackButtonsBusy(False)
            RefreshClonedMaterialStatus()
        End Try
    End Sub

    Private Async Sub UnpackButton_Click(sender As Object, e As EventArgs) Handles UnpackButton.Click
        ' Confirm: Unpack permanently deletes all WM_ClonePack archives + plugins.
        Dim ok = MessageBox.Show(
            "Extract all WM_ClonePack archives back to loose files and remove the archives + plugins?",
            "Unpack", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If ok <> DialogResult.Yes Then Return

        SetPackButtonsBusy(True)
        _packCts = New CancellationTokenSource()
        EscribirTransitorio("Starting unpack…")
        Try
            Dim progress As New Progress(Of WM_PackUnpack.PackProgress)(AddressOf OnPackProgress)
            Dim result = Await WM_PackUnpack.UnpackAsync(progress, _packCts.Token)
            ' ⛔⛔ EL AVISO DE REMONTE VA EN ESTE LABEL, QUE ES EL QUE QUEDA. Un archive sin remontar deja
            ' su contenido invisible por el resto de la sesión, y ese aviso viajaba SÓLO por
            ' `PackProgressLabel` — que el `Finally` de abajo apaga (`SetPackButtonsBusy(False)` ⇒
            ' `PackProgressLabel.Visible = False`) en el MISMO turno. El caso peor es el de ÉXITO: sin
            ' modal, con el label diciendo "Unpack complete." en color normal, el aviso se apagaba con el
            ' label de progreso y el usuario nunca se enteraba. Va en las CUATRO ramas (éxito,
            ' cancelación, unpack parcial y fallo general), y en la de éxito además tiñe el label:
            ' "complete" en color normal con un archive invisible es una mentira.
            ' ⛔⛔ CADA RAMA USA UN LOCAL CON NOMBRE PROPIO, Y NINGUNO ES PREFIJO DE OTRO. Antes las ramas
            ' compartían `avisoRemonte`, y su sentencia
            ' `PackLastActionLabel.Text &= avisoRemonte` es PREFIJO ESTRICTO de la de la rama parcial
            ' (`… &= avisoRemonteParcial`): borrar la rama de ÉXITO dejaba el chequeo de fuente en verde
            ' porque la línea de la otra rama lo satisfacía. Con nombres mutuamente no-prefijos cada
            ' ancla es única POR CONSTRUCCIÓN y no por cuidado del que la escribe — el gate lo verifica.
            Dim resguardos = result.CopiasDeSueltos.Count + result.Huerfanos.Count
            Dim colaResguardos = If(resguardos = 0, "",
                                    $" {resguardos} backup(s) of your own loose files kept next to them " &
                                    "(*.bak.unpack*) — delete them when you're happy.")
            If _packCts.IsCancellationRequested Then
                ' ⛔ LA CANCELACIÓN TAMBIÉN NOMBRA LOS RESGUARDOS. Un Stop a mitad ya pudo dejar copias
                ' `.bak.unpack` del usuario en disco; no decirlas acá era la misma omisión que la rama de
                ' éxito ya tenía cerrada.
                Dim avisoRemonteCancel = WM_PackUnpack.UltimoAvisoRemonte
                EscribirResultadoPack("Unpack stopped by user." & colaResguardos & avisoRemonteCancel,
                                      Drawing.Color.DarkOrange)
            Else
                Dim avisoRemonteExito = WM_PackUnpack.UltimoAvisoRemonte
                EscribirResultadoPack($"Unpack complete. Removed {result.ArchivesRemoved.Count} archive(s) and " &
                                      $"{result.PluginsRemoved.Count} plugin(s); wrote {result.LooseFilesWritten.Count} loose file(s)." &
                                      colaResguardos & avisoRemonteExito,
                                      If(avisoRemonteExito = "", SystemColors.ControlText, Drawing.Color.DarkOrange))
            End If

            ' ⛔ UN UNPACK PARCIAL NO ES "Unpack failed", Y DECIRLO ASI MANDA A DESHACER LO QUE SI SALIO.
            ' `UnpackParcialException` llega acá con su resultado: `WM_PackUnpack` YA registró los sueltos
            ' que se escribieron y ninguno de los archives con una entrada fallada se borró. Lo que el
            ' usuario tiene que ver es las DOS cosas — cuánto salió y qué falló, con el nombre de cada
            ' entrada— y que volver a correr Unpack sigue donde quedó. Va como Warning, no como Error.
            ' Este Catch va ANTES del general: el orden es la selección.
        Catch exParcial As BSA_BA2_Library_DLL.BethesdaArchive.Core.UnpackParcialException
            Dim r = exParcial.Resultado
            Dim avisoRemonteParcial = WM_PackUnpack.UltimoAvisoRemonte
            EscribirResultadoPack($"Unpack incomplete: {r.Fallos.Count} entry(ies) failed. " &
                                  $"Wrote {r.LooseFilesWritten.Count} loose file(s), removed " &
                                  $"{r.ArchivesRemoved.Count} archive(s) and {r.PluginsRemoved.Count} plugin(s); " &
                                  $"{r.ArchivesConservados.Count} archive(s) left in place." & avisoRemonteParcial,
                                  Drawing.Color.DarkOrange)
            ' ⛔ EL DETALLE COMPLETO VA A UN ARCHIVO CUYA RUTA EL DIALOGO NOMBRA; EL DIALOGO SE RECORTA.
            ' (Acá decía "va AL LOG", que es lo contrario del código y de lo que explica `DetalleRecortado`
            ' treinta líneas abajo: en Release no hay log, y remitir a él era pedirle al usuario algo
            ' imposible. El comentario quedó del estado anterior.)
            ' `UnpackParcialException.Message` es el Join de TODOS los fallos, uno por entrada: sobre un
            ' archive grande con el destino de solo lectura eso son decenas de miles de lineas en un
            ' MessageBox, que deja de ser legible y puede no poder mostrarse. El recorte es de
            ' PRESENTACION —cuántas líneas entran en un diálogo— y no un umbral de conducta: no cambia qué
            ' se extrae, qué se borra ni cuándo se tira. La conducta del packer no se toca (cortar el
            ' barrido por "demasiados fallos" SÍ sería un umbral inventado, y por eso el reader que itera
            ' N entradas queda exactamente como está).
            MessageBox.Show(DetalleRecortado(exParcial, r), "Unpack incomplete",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            ' ⛔⛔ LA CUARTA SALIDA TAMBIEN DICE EL AVISO, y es la que MAS lo necesita. Acá cae el fallo
            ' ANCHO del pre-pass (un .ba2 tomado por el AV / MO2 / OneDrive), y en ese escenario el
            ' remonte falla POR EL MISMO LOCK: el aviso está poblado casi siempre. Sin esto, el usuario
            ' leía "Unpack failed: cannot access …" y se quedaba sin saber lo importante — que su archive
            ' quedó DESMONTADO y que hay un .txt con el detalle. Un mensaje de error que omite el daño
            ' real es peor que uno genérico.
            Dim avisoRemonteFallo = WM_PackUnpack.UltimoAvisoRemonte
            ' ⛔ DarkRed A PROPÓSITO Y SIN MIEDO: el borrado que se comía este mensaje era del refresh
            ' mirando el COLOR, y eso ya no existe (ver `_packLabelPersistente`). Esquivarlo pintando
            ' DarkOrange habría sido mentirle al usuario sobre la severidad para sortear un bug.
            EscribirResultadoPack("Unpack failed: " & ex.Message & avisoRemonteFallo, Drawing.Color.DarkRed)
            MessageBox.Show(ex.ToString() & If(avisoRemonteFallo = "", "",
                                               Environment.NewLine & Environment.NewLine & avisoRemonteFallo.Trim()),
                            "Unpack failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _packCts?.Dispose()
            _packCts = Nothing
            SetPackButtonsBusy(False)
            RefreshClonedMaterialStatus()
        End Try
    End Sub

    ''' <summary>Cuántas líneas de una lista larga entran en un diálogo antes de dejar de ser legible.
    ''' <para>⛔ ES UN LÍMITE DE PRESENTACIÓN, NO UNA REGLA DE CONDUCTA, y la distinción importa porque el
    ''' repo prohíbe los umbrales inventados: esto no decide qué se extrae, qué se borra ni cuándo se
    ''' tira — sólo cuántos renglones se muestran. El detalle COMPLETO va a un ARCHIVO cuya ruta el
    ''' diálogo nombra, así que nada queda oculto y el usuario puede llegar a lo que falta.</para></summary>
    Private Const LINEAS_EN_DIALOGO As Integer = 20

    ''' <summary>Arma el texto del diálogo de unpack incompleto: las listas recortadas + la RUTA REAL del
    ''' archivo con el detalle completo.
    ''' <para>⛔⛔ NO SE REMITE "AL LOG", Y ESE ERA EL DEFECTO. El recorte decía <i>"the full list is in
    ''' the log"</i> y en Release <b>no hay log</b>: <c>Logger.Enabled</c> queda en False y su setter
    ''' descarta cualquier True. Sobre un unpack con 3.002 fallos, el usuario veía 20 y los otros 2.982
    ''' se perdían — con un mensaje que le pedía ir a buscarlos a un lado que no existe. Es literal el
    ''' precedente de <c>ApplicationEvents</c>: <i>"logueaba a NINGUN LADO y el cartel igual decía
    ''' «Details have been logged»"</i>. Ahora se escribe un archivo de verdad (misma forma que
    ''' <c>CrashReport</c>: al lado del exe, con fallback a <c>%TEMP%</c>) y el diálogo dice DÓNDE
    ''' quedó — y si no se pudo escribir, lo dice también en vez de mentir.</para></summary>
    Private Shared Function DetalleRecortado(exParcial As BSA_BA2_Library_DLL.BethesdaArchive.Core.UnpackParcialException,
                                             r As BSA_BA2_Library_DLL.BethesdaArchive.Core.UnpackResult) As String
        ' El archivo lleva TODO: el mensaje entero del packer (una línea por entrada fallada) y las dos
        ' listas completas. Se escribe UNA vez y las tres secciones del diálogo citan la misma ruta.
        Dim completo As New Text.StringBuilder()
        completo.AppendLine("Wardrobe Manager - unpack incomplete. Full detail.")
        completo.AppendLine("Re-running Unpack picks up where it left off; nothing here was deleted.")
        completo.AppendLine()
        completo.AppendLine(exParcial.Message)
        AgregarListaCompleta(completo, "Your previous loose files were backed up", r.CopiasDeSueltos)
        AgregarListaCompleta(completo, "Backups left by earlier runs, still on disk", r.Huerfanos)
        ' El aviso de remonte también entra al archivo: es el dato GRAVE del lote y no puede vivir sólo
        ' en un label. Ya trae su propia ruta de detalle si hubo fallos.
        ' ⛔ EL NOMBRE NO PUEDE EMPEZAR CON `avisoRemonte`: los cuatro locales de las ramas del handler
        ' viven bajo esa invariante de no-prefijo (D9.0) y este quinto identificador la VIOLABA — es
        ' prefijo estricto de los cuatro, así que un ancla de rama podía satisfacerse con ESTA línea.
        ' `detalleAvisoRemonte` no es prefijo de ninguno ni ninguno lo es de él.
        Dim detalleAvisoRemonte = WM_PackUnpack.UltimoAvisoRemonte
        If detalleAvisoRemonte <> "" Then
            completo.AppendLine()
            completo.AppendLine(detalleAvisoRemonte.Trim())
        End If
        Dim ruta = WM_PackUnpack.EscribirReporte("unpack_incomplete", completo.ToString())

        Dim lineas = exParcial.Message.Split(New String() {Environment.NewLine}, StringSplitOptions.None)
        Dim sb As New Text.StringBuilder()
        For i = 0 To Math.Min(lineas.Length, LINEAS_EN_DIALOGO) - 1
            sb.AppendLine(lineas(i))
        Next
        If lineas.Length > LINEAS_EN_DIALOGO Then
            sb.AppendLine($"… and {lineas.Length - LINEAS_EN_DIALOGO} more.")
        End If
        AgregarLista(sb, "Your previous loose files were backed up", r.CopiasDeSueltos)
        AgregarLista(sb, "Backups left by earlier runs, still on disk", r.Huerfanos)
        If detalleAvisoRemonte <> "" Then
            sb.AppendLine()
            sb.AppendLine(detalleAvisoRemonte.Trim())
        End If
        ' ⛔ LA RUTA SE DICE UNA SOLA VEZ, AL FINAL. Antes cada sección recortada la repetía ("… and N
        ' more (full list: C:\…)") y con las tres listas largas el mismo path salía tres veces en el
        ' mismo diálogo, que es ruido, no información.
        sb.AppendLine()
        sb.AppendLine(If(ruta = "",
                         "The full report could NOT be written to disk (the app folder and %TEMP% both refused).",
                         $"Full report: {ruta}"))
        Return sb.ToString()
    End Function

    Private Shared Sub AgregarLista(sb As Text.StringBuilder, titulo As String, items As List(Of String))
        If items Is Nothing OrElse items.Count = 0 Then Return
        sb.AppendLine()
        sb.AppendLine($"{titulo} ({items.Count}):")
        For Each s In items.Take(LINEAS_EN_DIALOGO)
            sb.AppendLine(s)
        Next
        If items.Count > LINEAS_EN_DIALOGO Then
            sb.AppendLine($"… and {items.Count - LINEAS_EN_DIALOGO} more.")
        End If
    End Sub

    Private Shared Sub AgregarListaCompleta(sb As Text.StringBuilder, titulo As String, items As List(Of String))
        If items Is Nothing OrElse items.Count = 0 Then Return
        sb.AppendLine()
        sb.AppendLine($"{titulo} ({items.Count}):")
        For Each s In items
            sb.AppendLine(s)
        Next
    End Sub

    ' Progress(Of T) marshals callbacks to the UI thread automatically — safe to touch controls.
    ' Three different controls get updated at different rates:
    '   - PackProgressLabel: per-tick, every Stage update. Double-buffered so it doesn't flicker.
    '   - PackProgressBar: only when Max >= 0 (Max < 0 = "leave the bar alone, this is a milestone
    '     report that doesn't change progress numbers").
    '   - PackLastActionLabel (bottom box): only when BoxText is non-empty. Low-frequency
    '     milestones (per archive started/finished); never on per-entry ticks.
    Private Sub OnPackProgress(p As WM_PackUnpack.PackProgress)
        If p Is Nothing Then Return
        PackProgressLabel.Text = p.Stage

        If p.Max >= 0 Then
            If p.Max > 0 Then
                PackProgressBar.Style = ProgressBarStyle.Continuous
                PackProgressBar.Maximum = p.Max
                ' Setting Value twice with the second one being the desired value is a known WinForms
                ' workaround: the animation lags badly without it on the Continuous style.
                Dim clamped = Math.Max(0, Math.Min(p.Current, p.Max))
                PackProgressBar.Value = clamped
            Else
                PackProgressBar.Style = ProgressBarStyle.Marquee
            End If
        End If

        If Not String.IsNullOrEmpty(p.BoxText) Then
            EscribirTransitorio(p.BoxText)
        End If
    End Sub

    ''' <summary>True mientras un Pack/Unpack está EN VUELO. Lo consume
    ''' <see cref="RefreshClonedMaterialStatus"/> para no vaciar el texto TRANSITORIO del label: el
    ''' refresh es alcanzable a mitad de la corrida (cambiar de juego — los botones están deshabilitados
    ''' pero el <c>SelectedIndexChanged</c> no), y sin esto el "Starting…" desaparecía hasta el tick
    ''' siguiente en la primera corrida de la sesión.</summary>
    Private _packEnCurso As Boolean

    Private Sub SetPackButtonsBusy(busy As Boolean)
        _packEnCurso = busy
        PackButton.Enabled = Not busy
        UnpackButton.Enabled = Not busy
        StopButton.Visible = busy
        StopButton.Enabled = busy
        StopButton.Text = "Stop"
        PackProgressBar.Visible = busy
        PackProgressLabel.Visible = busy
        PackElapsedLabel.Visible = busy

        If busy Then
            PackProgressBar.Style = ProgressBarStyle.Marquee
            PackProgressBar.Value = 0
            PackProgressLabel.Text = "Starting…"
            PackElapsedLabel.Text = "Elapsed: 00:00"
            _packStartedAt = DateTime.UtcNow
            _packElapsedTimer.Start()
        Else
            _packElapsedTimer.Stop()
        End If

        ' Lock other tabs while busy. We don't disable the TabPage controls (.Enabled = False on
        ' a TabPage greys everything but lets the user still click around inside it on some
        ' themes); instead we keep the user pinned to the current tab via SelectedIndexChanging,
        ' and we disable every sibling TabPage.Enabled so its controls visibly grey out.
        For Each tp As TabPage In TabControl1.TabPages
            If tp Is TabPagePack Then Continue For
            tp.Enabled = Not busy
        Next
        If busy Then
            ' Force selection back to the pack tab so the user sees the progress.
            TabControl1.SelectedTab = TabPagePack
            AddHandler TabControl1.Selecting, AddressOf TabControl1_LockSelection
        Else
            RemoveHandler TabControl1.Selecting, AddressOf TabControl1_LockSelection
        End If

        Cursor = If(busy, Cursors.WaitCursor, Cursors.Default)
    End Sub

    Private Sub TabControl1_LockSelection(sender As Object, e As TabControlCancelEventArgs)
        ' Reject any attempt to leave the pack tab while a Pack/Unpack is running.
        If e.TabPage IsNot TabPagePack Then e.Cancel = True
    End Sub

    ''' <summary>
    ''' Tilde o cruz en la etiqueta de estado, contra el ImageList compartido <c>IconsSmall</c> que
    ''' viene de <see cref="FO4_Base_Library.IconFormBase"/>.
    ''' </summary>
    ''' <remarks>⛔ Por CLAVE y no por indice. Antes esto era <c>ImageIndex = 1</c> contra el
    ''' ImageList propio del formulario, donde 0 era el tilde y 1 la cruz. En la lista compartida ese
    ''' orden es otro —y ademas se corre solo con agregar un PNG a Resources\Icons— asi que un indice
    ''' hardcodeado pintaria el icono equivocado sin que nada falle.</remarks>
    Private Shared Sub Tick(etiqueta As Label, ok As Boolean)
        etiqueta.ImageKey = If(ok, "AgtActionSuccess", "Cancel")
    End Sub

    Private Function Check_Folders() As Boolean
        Tick(Label1, Config_App.Check_FOFolder)
        Tick(Label2, WM_Config.Check_BSFolder)
        Tick(Label3, WM_Config.Check_OsFolder)
        Tick(Label6, Config_App.Check_Skeleton)

        Dim folderschek As Boolean = Config_App.Check_FOFolder And WM_Config.Check_BSFolder And WM_Config.Check_OsFolder
        If folderschek Then
            ListView1.Items.Clear()
            Dim oldbsa = WM_Config.Current.BSAFiles.ToList
            Dim oldchecks = WM_Config.Current.BSAFiles_Clonables.ToList
            WM_Config.Current.BSAFiles.Clear()
            WM_Config.Current.BSAFiles_Clonables.Clear()
            Dim idx2 As Integer = 0
            For Each fil In FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Config_App.Current.FO4EDataPath, "*.ba2;*.bsa", False).Order
                Dim it As New ListViewItem(IO.Path.GetFileName(fil))
                Dim idx = oldbsa.FindIndex(Function(s) String.Equals(s, IO.Path.GetFileName(fil), StringComparison.OrdinalIgnoreCase))
                WM_Config.Current.BSAFiles.Add(IO.Path.GetFileName(fil))
                If idx <> -1 Then
                    WM_Config.Current.BSAFiles_Clonables.Add(oldchecks(idx))
                Else
                    WM_Config.Current.BSAFiles_Clonables.Add(False)
                End If
                ListView1.Items.Add(it)
                it.Tag = idx2
                it.Checked = WM_Config.Current.BSAFiles_Clonables(idx2)
                idx2 += 1
            Next

        End If
        Return folderschek
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox1.Text))
        If String.IsNullOrEmpty(result) Then Return
        Config_App.Current.FO4ExePath = result
        TextBox1.Text = Config_App.Current.FO4ExePath
        Check_GameMismatch()
        ' El exe decide la variante (plana / VR) y con ella los candidatos de carpeta. Un override que el
        ' usuario haya fijado NO se toca: lo fijó porque el automático no le servía.
        RefreshPluginsTxtRow()
        Dim exe = IO.Path.GetFileName(Config_App.Current.FO4ExePath)
        If exe.ToLower.Contains("fallout4", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Fallout4
        If exe.ToLower.Contains("skyrimse", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Skyrim Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Skyrim

        Dim pathS As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "Tools", "CalienteTools")

        If Config_App.Check_FOFolder And (TextBox2.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or WM_Config.Check_BSFolder = False) Then
            Dim bsDir = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide")
            TextBox2.Text = WM_Config.ResolveBsSuiteExePath(bsDir, "BodySlide")
            WM_Config.Current.BSExePath = TextBox2.Text
        End If
        If Config_App.Check_FOFolder And (TextBox3.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or WM_Config.Check_OsFolder = False) Then
            Dim osDir = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide")
            TextBox3.Text = WM_Config.ResolveBsSuiteExePath(osDir, "OutfitStudio")
            WM_Config.Current.OSExePath = TextBox3.Text
        End If
        If Config_App.Check_FOFolder And (TextBox4.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or Config_App.Check_Skeleton = False) Then
            Dim skel As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "res\skeleton_fo4.nif", "res\skeleton_female_sse.nif")
            TextBox4.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\" + skel)
            Config_App.Current.SkeletonPath = TextBox4.Text
            SkeletonInstance.Default.Skeleton = Nothing
        End If
        Check_Folders()
        RefreshClonedMaterialStatus()
    End Sub

    Private Shared Function Search_exe(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an executable file"
            dlg.Filter = "EXE files (*.exe)|*.exe"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Shared Function Search_Nif(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an skeleton nif"
            dlg.Filter = "NIF files (*.nif)|*.nif"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox2.Text))
        If String.IsNullOrEmpty(result) Then Return
        WM_Config.Current.BSExePath = result
        TextBox2.Text = WM_Config.Current.BSExePath
        Check_Folders()
    End Sub
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim result = Search_exe(IO.Path.GetDirectoryName(TextBox3.Text))
        If String.IsNullOrEmpty(result) Then Return
        WM_Config.Current.OSExePath = result
        TextBox3.Text = WM_Config.Current.OSExePath
        Check_Folders()
    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView1.SelectedIndexChanged

    End Sub

    Private Sub ListView1_ItemCheck(sender As Object, e As ItemCheckEventArgs) Handles ListView1.ItemCheck
        If CInt(ListView1.Items(e.Index).Tag) < -1 Or CInt(ListView1.Items(e.Index).Tag) > WM_Config.Current.BSAFiles_Clonables.Count - 1 Then
#If DEBUG Then
            Debugger.Break()
#End If
        End If
        WM_Config.Current.BSAFiles_Clonables(ListView1.Items(e.Index).Tag) = IIf(e.NewValue = CheckState.Checked, True, False)
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Dim result = Search_Nif(IO.Path.GetDirectoryName(TextBox4.Text))
        If String.IsNullOrEmpty(result) Then Return
        Config_App.Current.SkeletonPath = result
        TextBox4.Text = Config_App.Current.SkeletonPath
        SkeletonInstance.Default.Skeleton = Nothing
        Check_Folders()
    End Sub


    Private actualizar = False
    Private Sub Config_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        actualizar = True
    End Sub

    Private Sub Config_Form_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If initialgame <> Config_App.Current.Game Then
            If Not IsNothing(Me.Owner) AndAlso Me.Owner.GetType Is GetType(Wardrobe_Manager_Form) Then
                If Not IsNothing(CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict) Then
                    CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict.Checked = True
                End If
            End If
        End If
        ' ⛔ Sólo si la pantalla se cargó ENTERA: ver _cargaCompleta.
        If _cargaCompleta Then Graba_Build_Options()
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        WM_Config.Current.Settings_Build = WM_Config.Default_Build_Settings
        Setea_Build_Options()
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        Try
            If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
            Button8.Enabled = False
        Catch ex As Exception
#If DEBUG Then
            Debugger.Break()
#End If
        End Try

    End Sub

    Private Sub CheckBoxBuildTri_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxBuildTri.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonWMEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonWMEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxForceCloned.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonBSEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonBSEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub TabPage3_Click(sender As Object, e As EventArgs) Handles TabPage3.Click

    End Sub

    Private Sub ComboBoxGame_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxGame.SelectedIndexChanged
        If ComboBoxGame.SelectedIndex <> -1 Then
            Config_App.Current.Game = ComboBoxGame.SelectedIndex
            GroupBoxweights.Enabled = ComboBoxGame.SelectedIndex <> 0
            GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
            Check_GameMismatch()
            ' El override del Plugins.txt es POR JUEGO: cambiar de juego cambia de slot.
            RefreshPluginsTxtRow()
            RefreshClonedMaterialStatus()
            UpdateBa2VersionVisibility()
        End If
    End Sub

    Private Sub Check_GameMismatch()
        Dim exe = Config_App.Current.FO4ExePath.ToLowerInvariant()
        Dim isFO4Exe = exe.Contains("fallout4", StringComparison.OrdinalIgnoreCase)
        Dim isSkyrimExe = exe.Contains("skyrim", StringComparison.OrdinalIgnoreCase) Or exe.Contains("sse", StringComparison.OrdinalIgnoreCase)
        Dim game = Config_App.Current.Game
        Dim mismatch = (game = Config_App.Game_Enum.Fallout4 AndAlso isSkyrimExe) OrElse
                       (game = Config_App.Game_Enum.Skyrim AndAlso isFO4Exe)
        LabelGameMismatch.Visible = mismatch
    End Sub

    ' ==============================================================================================
    ' Load order (Plugins.txt) — automático, con opción de fijarlo a mano. Persistido POR JUEGO.
    ' ==============================================================================================
    ' ⭐ WM SÍ depende de este archivo, aunque no cargue un solo plugin: Fill_DictionaryAsync deriva de
    '    ReadActiveLoadOrder la PRIORIDAD de los BA2/BSA (BuildArchivePriority). Sin él, `loadedOrder` queda
    '    en los masters implícitos, ningún archive de mod cae en el grupo de "plugins cargados", TODOS pasan
    '    a huérfanos con orden NEGATIVO — o sea por debajo de vanilla — y WM muestra la malla/textura/material
    '    de vanilla donde debería mostrar la del mod. Sin un solo error. Por eso vale una fila propia acá.
    ' ⛔ El .ini NO va en esta pantalla: sus dos únicos consumidores (PluginEncodingSettings y
    '    LocalizedStrings) decodifican strings de PLUGINS, y WM no lee ni escribe ESPs. Para WM es inerte.
    '    El selector de inis vive en el Preflight del NPC Manager; como el override se guarda en Config_App
    '    (compartido) y por juego, lo que se fije allá lo hereda WM y viceversa.

    ''' <summary>Repinta la fila del Plugins.txt. Es barato: la resolución está memoizada por
    ''' (exe, juego, overrides).
    '''
    ''' <para>⚠️ Sale por la puerta si los controles todavía no existen: igual que
    ''' <c>RefreshClonedMaterialStatus</c>, esto se llama desde <c>ComboBoxGame.SelectedIndexChanged</c>, que
    ''' dispara DURANTE <c>InitializeComponent()</c> — en el momento en que el Designer asigna
    ''' <c>SelectedIndex</c>, con medio formulario sin instanciar.</para></summary>
    Private Sub RefreshPluginsTxtRow()
        If TextBoxPluginsTxt Is Nothing OrElse LabelPluginsTxt Is Nothing OrElse ButtonAutoPluginsTxt Is Nothing Then Return

        Dim r = GamePathsResolver.Resolve()
        Dim overridden = (r.PluginsTxtOrigin = GamePathsResolver.PathOrigin.UserOverride)

        TextBoxPluginsTxt.Text = If(r.HasPluginsTxt, r.PluginsTxtPath, "")
        TextBoxPluginsTxt.PlaceholderText = "Not found — click .... to pick it"
        ' El COLOR es lo que distingue "lo dedujo la app" de "lo elegiste vos". Un usuario que ve gris y otro
        ' que ve negro están mirando problemas distintos.
        TextBoxPluginsTxt.ForeColor = If(overridden, SystemColors.WindowText, SystemColors.GrayText)
        ButtonAutoPluginsTxt.Enabled = overridden

        ' El estado va en el ICONO de la etiqueta, igual que las otras cuatro filas de esta pantalla
        ' (ver Tick). Se hace acá y no en Check_Folders porque esto no depende de una carpeta sino del
        ' resolver, y se repinta en más momentos (cambio de juego, Browse, Auto). El detalle —de dónde
        ' salió la ruta, o por qué falta— va al tooltip.
        Tick(LabelPluginsTxt, r.HasPluginsTxt)
        ToolTip1.SetToolTip(TextBoxPluginsTxt, r.StatusLine)
    End Sub

    Private Sub ButtonBrowsePluginsTxt_Click(sender As Object, e As EventArgs) Handles ButtonBrowsePluginsTxt.Click
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select the game's Plugins.txt"
            dlg.Filter = "Plugins.txt|Plugins.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            Try
                Dim dir = IO.Path.GetDirectoryName(TextBoxPluginsTxt.Text)
                If Not String.IsNullOrEmpty(dir) AndAlso IO.Directory.Exists(dir) Then dlg.InitialDirectory = dir
            Catch
            End Try
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Config_App.Current.SetActivePluginsTxtOverride(dlg.FileName)
        End Using
        RefreshPluginsTxtRow()
    End Sub

    Private Sub ButtonAutoPluginsTxt_Click(sender As Object, e As EventArgs) Handles ButtonAutoPluginsTxt.Click
        Config_App.Current.SetActivePluginsTxtOverride("")
        RefreshPluginsTxtRow()
    End Sub

    Private Sub CheckBoxweightignore_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxweightignore.CheckedChanged
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked

    End Sub

End Class