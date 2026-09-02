Option Strict On
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Threading
Imports BSA_BA2_Library_DLL.BethesdaArchive.Core
Imports FO4_Base_Library.Archives

''' <summary>
''' Bridges Clone_Materials_class' loose output (under Materials\ManoloCloned\ and
''' Textures\ManoloCloned\) and ArchivePackager's archive set. Pack collects the loose
''' files into BA2/BSA companion archives plus their dummy plugins; Unpack reverses the
''' operation. Only materials (.bgsm/.bgem) and textures (.dds) under ManoloCloned\ are
''' touched — the NIF/OSD/TRI clone outputs stay loose as the user requested.
''' </summary>
Public Module WM_PackUnpack
    Public Const MOD_BASE_NAME As String = "WM_ClonePack"

    ''' <summary>Aviso de archivos huérfanos de la ÚLTIMA corrida de <see cref="Pack"/>, o <c>""</c>.
    ''' <para>Existe porque el resumen final de la UI se arma con los conteos del <c>PackagerResult</c> y
    ''' no lee el stream de progreso; sin esto, el aviso salía por un <c>ReportStage</c> intermedio que el
    ''' tick siguiente pisaba, o sea que el usuario no lo veía nunca.</para></summary>
    Public Property UltimoAvisoHuerfanos As String = ""

    ''' <summary>Aviso de archives que NO se pudieron volver a montar en la última corrida de
    ''' <see cref="Unpack"/>, o <c>""</c>.
    ''' <para>⛔⛔ ES EL AVISO GRAVE Y POR ESO NO PUEDE VIVIR EN EL STREAM DE PROGRESO. Un archive que
    ''' quedó sin remontar tiene su contenido INVISIBLE para la app por el resto de la sesión, sin que
    ''' nada haya fallado con él. Salía sólo por <c>ReportStage</c> → <c>PackProgressLabel</c>, y ese
    ''' label lo OCULTA el <c>Finally</c> del handler (<c>SetPackButtonsBusy(False)</c> ⇒
    ''' <c>PackProgressLabel.Visible = False</c>) en el MISMO turno: el usuario no llegaba a leerlo nunca.
    ''' El caso peor es el de ÉXITO — no hay diálogo, el label persistente dice "Unpack complete." en
    ''' color normal, y el aviso se apagó con el label de progreso.</para>
    ''' <para>El docstring de <c>RemontarConservados</c> promete "el llamador lo dice"; esto es lo que lo
    ''' hace verdad en las CUATRO ramas del resumen: éxito, cancelación, unpack parcial y fallo general
    ''' —esta última es la que MÁS lo necesita, porque ahí cae el pre-pass con el archive tomado y el
    ''' remonte falla por el mismo lock.</para></summary>
    Public Property UltimoAvisoRemonte As String = ""

    ''' <summary>Archives que el re-montaje post-flush del PACK no pudo volver a montar, acumulados a lo
    ''' largo de todos los chunks de una corrida. Se resetea al entrar a <see cref="Pack"/>.
    ''' <para>⛔ Mismo estatus que <see cref="UltimoAvisoRemonte"/> del Unpack: un archive que queda fuera
    ''' del diccionario tiene su contenido INVISIBLE hasta un refresh completo, sin que nada haya fallado
    ''' con los bytes en disco. Antes este camino lo tragaba con un <c>Catch</c> pelado.</para></summary>
    Friend ReadOnly archivesNoRemontados As New List(Of String)

    ''' <summary>Aviso compuesto de <see cref="archivesNoRemontados"/>, o <c>""</c>. Lo consume el resumen
    ''' final del Pack en <c>Config_Form</c>.</summary>
    Public ReadOnly Property UltimoAvisoRemontePack As String
        Get
            If archivesNoRemontados.Count = 0 Then Return ""
            Return $" WARNING: {archivesNoRemontados.Count} archive(s) could NOT be re-mounted and their " &
                   "contents stay invisible until you restart or refresh: " &
                   String.Join(" | ", archivesNoRemontados)
        End Get
    End Property
    Public Const CLONED_PREFIX As String = "ManoloCloned\"

    ' Per-game caps. SSE BSA has a hard u32 offset limit at 4GB — 3GB leaves margin for header
    ' overhead and LZ4 frame inflation. FO4 BA2 uses u64 offsets but the engine is reported
    ' unstable >4GB; 3GB is the safe sweet spot.
    ' El tope es del FORMATO, no de esta app: vive en PackagerRequest.MaxArchiveBytesDefault.
    Private ReadOnly MAX_BYTES_FO4 As Long = BSA_BA2_Library_DLL.BethesdaArchive.Core.PackagerRequest.MaxArchiveBytesDefault
        Private ReadOnly MAX_BYTES_SSE As Long = BSA_BA2_Library_DLL.BethesdaArchive.Core.PackagerRequest.MaxArchiveBytesDefault

    Public Class StatusInfo
        Public Property LooseMaterialCount As Integer
        Public Property LooseTextureCount As Integer
        Public Property LooseTotalBytes As Long
        Public Property Archives As New List(Of String)
        Public Property Plugins As New List(Of String)
        Public Property ArchiveTotalBytes As Long

        ' Aggregated info from inside the archives — populated only from the file table parsed
        ' at Open(); no payload reads. PackedDecompressedBytes excludes BSA compressed entries
        ' (their u32 decompSize lives at the start of each payload and isn't read here).
        Public Property PackedMaterialCount As Integer
        Public Property PackedTextureCount As Integer
        Public Property PackedDecompressedBytes As Long
        Public Property PackedDecompressedIncomplete As Boolean
    End Class

    ''' <summary>
    ''' Progress payload reported by Pack/Unpack. Stage describes the phase, Current/Max drive
    ''' the progress bar (Max = 0 means "indeterminate", UI shows marquee-style).
    ''' </summary>
    Public Class PackProgress
        Public Property Stage As String = ""
        Public Property Current As Integer
        Public Property Max As Integer
        ' When non-empty, the form replaces the bottom action-status label (PackLastActionLabel)
        ' with this text. Used for low-frequency, milestone-level updates ("Processing archive X
        ' of Y — name") that survive without flickering. Per-entry status keeps using Stage and
        ' updates only the upper progress label.
        Public Property BoxText As String = ""
    End Class

    ''' <summary>Snapshot of what's currently loose vs already packed. Drives the UI label.</summary>
    Public Function GetStatus() As StatusInfo
        Dim status As New StatusInfo()
        Dim dataDir = Config_App.Current.FO4EDataPath
        If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then Return status

        Dim materialRoot = Path.Combine(dataDir, MaterialsPrefix & CLONED_PREFIX.TrimEnd("\"c))
        Dim textureRoot = Path.Combine(dataDir, TexturesPrefix & CLONED_PREFIX.TrimEnd("\"c))

        If Directory.Exists(materialRoot) Then
            For Each f In EnumerateLooseFiles(materialRoot, {".bgsm", ".bgem"})
                status.LooseMaterialCount += 1
                status.LooseTotalBytes += TamanoSeguro(f)
            Next
        End If
        If Directory.Exists(textureRoot) Then
            For Each f In EnumerateLooseFiles(textureRoot, {".dds"})
                status.LooseTextureCount += 1
                status.LooseTotalBytes += TamanoSeguro(f)
            Next
        End If

        Dim setInfo = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        status.Archives.AddRange(setInfo.Archives)
        status.Plugins.AddRange(setInfo.Plugins)
        For Each a In setInfo.Archives
            status.ArchiveTotalBytes += TamanoSeguro(a)
        Next

        ' Open each archive (file table only — no payload reads) and classify its entries by
        ' extension to count materials/textures and sum decompressed sizes. ArchiveEntry.DecompressedSize
        ' is populated from data already parsed at Open(); BSA compressed entries report 0 (unknown)
        ' so we flag the total as incomplete in that case.
        For Each archivePath In setInfo.Archives
            Try
                Using fs As FileStream = File.OpenRead(archivePath)
                    Using reader As New BethesdaReader(fs)
                        For Each entry In reader.EntriesFiles
                            Dim ext = Path.GetExtension(entry.FileName).ToLowerInvariant()
                            Select Case ext
                                Case ".bgsm", ".bgem"
                                    status.PackedMaterialCount += 1
                                Case ".dds"
                                    status.PackedTextureCount += 1
                            End Select
                            If entry.DecompressedSize > 0 Then
                                status.PackedDecompressedBytes += entry.DecompressedSize
                            Else
                                status.PackedDecompressedIncomplete = True
                            End If
                        Next
                    End Using
                End Using
            Catch
                ' Best-effort: if a single archive is unreadable, leave its counts out and continue.
                status.PackedDecompressedIncomplete = True
            End Try
        Next

        Return status
    End Function

    ''' <summary>
    ''' Background-thread wrapper. Use from UI handlers via Await.
    ''' Cancellation: callers can pass a CancellationToken to request a clean stop. Cancellation
    ''' is only checked at safe checkpoints (between chunks, and just before each archive write
    ''' inside a chunk) — never mid-write — so the on-disk archive set is always consistent
    ''' regardless of when the user clicks Stop. Already-written chunks stay packed; the
    ''' remaining loose files are left untouched.
    ''' </summary>
    Public Async Function PackAsync(Optional progress As IProgress(Of PackProgress) = Nothing,
                                     Optional ct As CancellationToken = Nothing) As Task(Of PackagerResult)
        Return Await Task.Run(Function() Pack(progress, ct))
    End Function

    ' Micro-batch size for the parallel load+compress pass. Each pass holds up to MICRO_BATCH
    ' raw files in flight at once across all worker threads, so peak transient RAM per pass is
    ' bounded by MICRO_BATCH × max-file-size (≈ 64 × 100 MB = 6 GB worst case for huge DDS, but
    ' typical mix lands well under 1 GB). After each pass the resulting compressed entries get
    ' folded into the main buffer one by one and the worker memory is freed.
    Private Const MICRO_BATCH As Integer = 64

    Public Function Pack(Optional progress As IProgress(Of PackProgress) = Nothing,
                          Optional ct As CancellationToken = Nothing) As PackagerResult
        ' ⛔ PRIMERA SENTENCIA, igual que en Unpack. Estaba DESPUÉS de los dos throws de abajo, así que un
        ' Pack que moría por config o por wrapper dejaba vivo el aviso de huérfanos de la corrida
        ' anterior. Mismo defecto, misma cura: el estado publicado se resetea donde pasan TODAS las
        ' salidas. (Y esto es lo que hace verdadera la simetría que el comentario de Unpack invoca.)
        ' Gate: D9.3b, la puerta hermana de D9.3 — sin testigo propio, ésta es la que vuelve.
        UltimoAvisoHuerfanos = ""
        archivesNoRemontados.Clear()

        Dim dataDir = Config_App.Current.FO4EDataPath
        If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then
            Throw New InvalidOperationException("Data folder not configured / missing.")
        End If

        ' ⛔ El pack escribe BA2 DX10 y para eso PARSEA cada .dds con el wrapper nativo (MakeTextureEntry →
        ' Dx10Importer.FromDdsBytes → Loader.GetDdsMetadata). Con el wrapper desajustado cada textura tira, la
        ' come el Catch del micro-batch, y la causa se va por Logger.LogLazy — que en Release NO EXISTE: el
        ' usuario ve "failed N file(s)" sin motivo y se queda con un BA2 de puros materiales. Se chequea UNA
        ' vez acá; adentro de MakeTextureEntry correría dentro del Parallel.For.
        Dim fallaWrapperPack = DirectXTexWrapperGate.Verificar()
        If fallaWrapperPack <> "" Then Throw New InvalidOperationException(fallaWrapperPack)

        ' ⛔⛔ LOS HUERFANOS SE CENSAN UNA VEZ, ACA, Y SE DICEN EN EL RESUMEN FINAL.
        ' `ArchiveSetInfo.Huerfanos` son EXACTAMENTE los `<archive>.ba2.bak` / `.bsa.bak` que dejo el
        ' rename de 2.0.2: hasta 3 GiB por pieza que NO borra nadie —a proposito, son del usuario— y que
        ' hasta hoy tampoco MENCIONABA nadie (su docstring decia "el llamador los muestra" y el llamador
        ' no existia).
        ' ⚠️ LOS `.bak.unpack` NO ESTAN EN ESTA LISTA, y acá antes decía que sí. No es un olvido de
        ' `DiscoverArchiveSet` ni algo que se pueda "sumar": esos viven bajo `LooseDataDir` con el nombre
        ' del SUELTO, no bajo `OutputDir` con el prefijo del mod, así que ese barrido no los puede ver
        ' (está escrito en `ArchivePackager.DiscoverArchiveSet`, en el ⛔ de Huérfanos). Quien los conoce
        ' y los reporta es `Unpack`, por `UnpackResult.Huerfanos`, y el diálogo de unpack incompleto ya
        ' los lista. Son dos listas distintas de dos caminos distintos; confundirlas era prometer acá una
        ' cobertura que este censo no puede dar.
        ' ⛔ ESTO VIVIA ADENTRO DE `FlushChunk` Y ERA TRES DEFECTOS EN UNO: se recalculaba POR CHUNK (5
        ' chunks = 5 pasadas de `FileInfo.Length` sobre archivos de gigabytes), el texto de progreso que
        ' emitia lo PISABA la propia FlushChunk unas lineas mas abajo —o sea que el usuario no lo veia
        ' nunca—, y las rutas se iban por `Logger`, que en Release NO EXISTE (ver EscribirReporte).
        ' Ahora: un censo, el detalle a un archivo REAL cuya ruta se nombra, y el aviso viaja al resumen
        ' final. No se borra NADA: el reporte lleva el tamaño para que el usuario decida.
        ' ⛔ Y VA DESPUÉS DEL EARLY-RETURN DE "nada que empaquetar", no antes: escribir el .txt acá arriba
        ' hacía que un Pack NO-OP —el caso más común de todos, apretar Pack sin nada suelto nuevo— dejara
        ' igual un archivo de reporte. Ver dónde está ahora.
        Dim avisoHuerfanos As String = ""

        Dim game = MapGame(Config_App.Current.Game)
        Dim chunkMaxComp As Long = If(game = GameKind.FO4_BA2, MAX_BYTES_FO4, MAX_BYTES_SSE)
        ' BA2 header version is FO4-only; the packager ignores it for SSE (BSA v105).
        Dim ba2Version As UInteger = WM_Config.Current.Ba2Version_FO4

        ' --- Walk loose: paths + sizes only, no bytes loaded yet. Memory ≈ 50 B/entry. ---
        ReportStage(progress, "Scanning loose files…", 0, 0)
        Dim allLoose As List(Of LooseFileRef) = WalkLooseWithSizes(dataDir)
        If allLoose.Count = 0 Then
            ReportStage(progress, "Nothing to pack.", 0, 0)
            Return New PackagerResult()
        End If

        ' El censo de huérfanos y su reporte, YA pasado el early-return: un Pack que no tiene nada que
        ' hacer no deja archivos por el camino.
        ' ⚠️ ACUMULACIÓN SIN TECHO, DECLARADA: cada Pack con huérfanos escribe un `.txt` nuevo (llevan
        ' timestamp, no se pisan). Es deliberado y es la misma postura del resto: borrar del disco del
        ' usuario es decisión del usuario, y un reporte que se pisa a sí mismo pierde el de la corrida
        ' anterior. La ruta se nombra en el resumen, así que el usuario sabe qué son y dónde están. Si
        ' alguna vez molesta, la poda la decide él — no se inventa acá una política de retención.
        Dim censoHuerfanos = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME).Huerfanos
        If censoHuerfanos.Count > 0 Then
            Dim total As Long = 0
            For Each h In censoHuerfanos
                Try
                    total += New FileInfo(h).Length
                Catch
                End Try
            Next
            Dim mb = Math.Round(total / 1048576.0, 1)
            Dim ruta = EscribirReporte("leftover_backups",
                                       $"Leftover backup files next to the {MOD_BASE_NAME} archive set." & vbCrLf &
                                       "Nothing in the app deletes these - they are yours. Remove them if you don't want them." & vbCrLf &
                                       $"Count: {censoHuerfanos.Count}   Total: {mb:N1} MB" & vbCrLf & vbCrLf &
                                       String.Join(vbCrLf, censoHuerfanos))
            avisoHuerfanos = $" {censoHuerfanos.Count} leftover backup file(s) ({mb:N1} MB) sit next to the " &
                             "archive set; nothing deletes them." &
                             If(ruta = "", " (the list could NOT be written to disk).", $" List: {ruta}")
            ' Y queda disponible para el resumen FINAL de la UI, que arma su texto con los conteos del
            ' PackagerResult y no ve el stream de progreso.
            UltimoAvisoHuerfanos = avisoHuerfanos
        End If

        Dim accumResult As New PackagerResult()
        Dim totalEntries = allLoose.Count
        Dim entriesDone As Integer = 0

        ' Main accumulator: pre-compressed VirtualEntry list paired with the source loose paths
        ' (so we can delete them after a successful flush). When the running compressed total
        ' would exceed chunkMaxComp, flush the buffer to the packager before adding the new one.
        Dim chunkEntries As New List(Of VirtualEntry)
        Dim chunkSources As New List(Of String)
        Dim chunkCompBytes As Long = 0

        Dim parOpts As New ParallelOptions With {
            .MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
            .CancellationToken = ct
        }

        Dim looseIndex As Integer = 0
        Dim cancelled As Boolean = False

        ' Thread-safe record of files that failed to load+compress. The parallel pass can't throw
        ' (it would abort the whole pack), so each failure is logged and tracked here; the count is
        ' surfaced in the final report so dropped files are never silent.
        Dim failedSources As New ConcurrentBag(Of String)

        While looseIndex < allLoose.Count
            If ct.IsCancellationRequested Then
                cancelled = True
                Exit While
            End If

            ' --- Build one micro-batch and load+compress it in parallel ----------------------
            Dim batchSize = Math.Min(MICRO_BATCH, allLoose.Count - looseIndex)
            Dim micro(batchSize - 1) As VirtualEntry

            Try
                Parallel.For(0, batchSize, parOpts,
                    Sub(i)
                        Dim lf = allLoose(looseIndex + i)
                        Try
                            micro(i) = If(lf.IsTexture,
                                          MakeTextureEntry(dataDir, lf.FullPath, lf.FullPath, Config_App.Current.Game),
                                          MakeMaterialEntry(dataDir, lf.FullPath, lf.FullPath, Config_App.Current.Game))
                        Catch ex As Exception
                            micro(i) = Nothing
                            failedSources.Add(lf.FullPath)
                            Dim failedPath = lf.FullPath
                            Logger.LogLazy(Function() $"[WM-PACK] Failed to load+compress loose file '{failedPath}': {ex}")
                        End Try
                    End Sub)
            Catch ex As OperationCanceledException
                cancelled = True
                Exit While
            End Try

            If ct.IsCancellationRequested Then
                cancelled = True
                Exit While
            End If

            ' --- Fold the compressed batch into the main buffer, flushing on cap ------------
            For i = 0 To batchSize - 1
                Dim ve = micro(i)
                Dim lf = allLoose(looseIndex + i)
                If ve Is Nothing Then Continue For

                Dim veCompSize As Long = If(ve.PreCompressedCompSize > 0UI, CLng(ve.PreCompressedCompSize), CLng(ve.PreCompressedDecompSize))

                ' If adding this entry would overflow the cap AND the buffer already has something,
                ' flush first so the next archive starts fresh and this entry seeds it.
                If chunkEntries.Count > 0 AndAlso chunkCompBytes + veCompSize > chunkMaxComp Then
                    FlushChunk(dataDir, game, ba2Version, chunkEntries, chunkSources, chunkCompBytes, chunkMaxComp,
                               accumResult, progress, totalEntries, entriesDone, ct)
                    entriesDone += chunkEntries.Count
                    chunkEntries = New List(Of VirtualEntry)
                    chunkSources = New List(Of String)
                    chunkCompBytes = 0
                    If ct.IsCancellationRequested Then
                        cancelled = True
                        Exit For
                    End If
                End If

                chunkEntries.Add(ve)
                chunkSources.Add(lf.FullPath)
                chunkCompBytes += veCompSize
            Next

            looseIndex += batchSize

            ReportStage(progress,
                        $"Compressed {looseIndex:N0}/{totalEntries:N0} (buffer {chunkCompBytes / (1024.0 * 1024.0 * 1024.0):N2} GB / {chunkMaxComp / (1024.0 * 1024.0 * 1024.0):N1} GB)",
                        looseIndex, totalEntries)
        End While

        ' Final flush: whatever survived the cancellation check or completed the loop.
        If chunkEntries.Count > 0 AndAlso Not cancelled Then
            FlushChunk(dataDir, game, ba2Version, chunkEntries, chunkSources, chunkCompBytes, chunkMaxComp,
                       accumResult, progress, totalEntries, entriesDone, ct)
            entriesDone += chunkEntries.Count
        End If

        Dim failedCount = failedSources.Count
        Dim failedSuffix = If(failedCount > 0, $"; failed {failedCount} file(s)", "")

        ' El aviso de huérfanos viaja hasta ACÁ y no se emite en el medio: un texto de progreso a mitad
        ' del pack lo pisa el tick siguiente. Va en el mensaje FINAL, que es el que queda a la vista.
        If cancelled Then
            ReportStage(progress,
                        $"Stopped. Wrote {accumResult.Archives.Count} archive(s), {accumResult.Plugins.Count} plugin(s) before stop. Remaining loose files left untouched.{failedSuffix}{avisoHuerfanos}",
                        entriesDone, totalEntries)
        Else
            ReportStage(progress,
                        $"Done. Wrote {accumResult.Archives.Count} archive(s), {accumResult.Plugins.Count} plugin(s); skipped {accumResult.Skipped.Count} unchanged.{failedSuffix}{avisoHuerfanos}",
                        totalEntries, totalEntries)
        End If
        Return accumResult
    End Function

    ''' <summary>Mapa <c>&lt;nombre de archive&gt; → SourceOrder</c> tal como está AHORA en el diccionario.
    ''' Se llama SIEMPRE antes de desmontar: después la información ya no existe.
    ''' <para>⛔ POR QUÉ IMPORTA: <c>SourceOrder</c> es la prioridad con la que un archive gana o pierde
    ''' un conflicto de ruta. <c>RegisterArchive</c> sin ese argumento lo aplana a
    ''' <c>ArchiveSourceOrder_RuntimeRegistered</c> (<c>Integer.MaxValue-1</c>), que le gana a TODO
    ''' archive de plugin — o sea que un desmontar+montar "neutro" puede cambiar en silencio qué archive
    ''' resuelve una ruta. La ley vale para los DOS caminos que desmontan (Pack y Unpack).</para>
    ''' <para>⛔⛔ SALE DEL REGISTRO DE MONTAJE, NO DE BARRER EL DICCIONARIO, y esa diferencia ERA UN
    ''' DEFECTO. El diccionario sólo expone al GANADOR de cada clave: un archive cuyas rutas estén TODAS
    ''' sombreadas por otro no aparecía en el mapa, así que se remontaba con el default
    ''' <c>ArchiveSourceOrder_RuntimeRegistered</c> — que le gana a todo archive de plugin. O sea que un
    ''' Pack o un Unpack convertían un archive PERDEDOR en GANADOR sin que cambiara un byte en disco, y
    ''' después la app servía los assets del otro. Estaba anotado como "riesgo residual" y era un defecto
    ''' real: el revisor externo lo confirmó.
    ''' <para>La fuente correcta es <c>FilesDictionary_class.TryGetArchiveSourceOrder</c>, que anota el
    ''' orden AL MONTAR y por eso conoce también a los sombreados. Se consulta por archive del set —no se
    ''' barre nada— así que además es O(archives) en vez de O(diccionario).</para></para></summary>
    Private Function CapturarOrdenDeArchives(archives As IEnumerable(Of String)) As Dictionary(Of String, Integer)
        Dim mapa As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        If archives Is Nothing Then Return mapa
        For Each a In archives
            Try
                Dim nombre = Path.GetFileName(a)
                Dim orden As Integer
                If FilesDictionary_class.TryGetArchiveSourceOrder(nombre, orden) Then mapa(nombre) = orden
            Catch
                ' Best-effort por archive: no poder leer UNA prioridad no puede tumbar el pack ni el
                ' unpack, y el que falte cae al default (que es lo que pasaba con TODOS antes).
            End Try
        Next
        Return mapa
    End Function

    ''' <summary>Escribe un reporte LARGO a un archivo de verdad y devuelve la ruta, o <c>""</c> si no se
    ''' pudo. Es el mecanismo con el que el diálogo y el resumen pueden decir "está acá" en vez de
    ''' remitir a un log que no existe.
    ''' <para>⛔⛔ POR QUÉ NO VA POR <c>Logger</c>, Y ESTE ES EL PRECEDENTE ESCRITO. En Release
    ''' <c>Logger.Enabled</c> queda en False y <b>su propio setter descarta cualquier True</b>
    ''' (<c>ApplicationEvents.vb</c>: el <c>#If DEBUG</c> más el doble candado del setter). Mandar una
    ''' lista de miles de fallos por <c>Logger.LogLazy</c> y después decirle al usuario "the full list is
    ''' in the log" es EXACTAMENTE el defecto que <c>CrashReport</c> vino a cerrar: <i>"esto logueaba a
    ''' NINGUN LADO y el cartel igual decía «Details have been logged»"</i>. Se pedía algo imposible de
    ''' cumplir.</para>
    ''' <para>MISMA FORMA QUE <c>CrashReport.TryWrite</c>, a propósito: al lado del exe y, si esa carpeta
    ''' no admite escritura, <c>%TEMP%</c>; UTF-8 CON BOM porque el usuario lo abre en cualquier editor y
    ''' lo pega en un foro. No se linkea <c>CrashReport</c> mismo porque ese es el reporte de CAÍDA —
    ''' tiene un guard de "sólo el primero", muestra un MessageBox propio y acumula con
    ''' <c>AppendAllText</c> sobre el mismo archivo; acá se quiere un archivo por corrida, sin diálogo y
    ''' sin guard.</para>
    ''' <para>Nada de acá puede tirar: no poder escribir el detalle no puede convertirse en la excepción
    ''' que tape el problema que se estaba reportando. Devuelve <c>""</c> y el llamador lo dice.</para></summary>
    Friend Function EscribirReporte(nombreBase As String, cuerpo As String) As String
        Dim intento =
            Function(carpeta As String) As String
                Try
                    If String.IsNullOrEmpty(carpeta) Then Return ""
                    Dim destino = Path.Combine(
                        carpeta, $"WardrobeManager_{nombreBase}_{DateTime.Now:yyyyMMdd_HHmmss}.txt")
                    File.WriteAllText(destino, cuerpo,
                                      New Text.UTF8Encoding(encoderShouldEmitUTF8Identifier:=True))
                    Return destino
                Catch
                    Return ""
                End Try
            End Function
        Try
            Dim r = intento(AppContext.BaseDirectory)
            If r = "" Then r = intento(Path.GetTempPath())
            Return r
        Catch
            Return ""
        End Try
    End Function

    Private Class LooseFileRef
        Public Property FullPath As String
        Public Property Size As Long
        Public Property IsTexture As Boolean
    End Class

    ''' <summary>
    ''' Enumerates loose files under ManoloCloned\ collecting only path + file size + bucket flag.
    ''' Does NOT open or read the files — that's deferred to FlushChunk so memory stays bounded.
    ''' </summary>
    Private Function WalkLooseWithSizes(dataDir As String) As List(Of LooseFileRef)
        Dim materialRoot = Path.Combine(dataDir, MaterialsPrefix & CLONED_PREFIX.TrimEnd("\"c))
        Dim textureRoot = Path.Combine(dataDir, TexturesPrefix & CLONED_PREFIX.TrimEnd("\"c))
        Dim list As New List(Of LooseFileRef)

        If Directory.Exists(materialRoot) Then
            For Each f In EnumerateLooseFiles(materialRoot, {".bgsm", ".bgem"})
                list.Add(New LooseFileRef With {
                    .FullPath = f,
                    .Size = New FileInfo(f).Length,
                    .IsTexture = False
                })
            Next
        End If
        If Directory.Exists(textureRoot) Then
            For Each f In EnumerateLooseFiles(textureRoot, {".dds"})
                list.Add(New LooseFileRef With {
                    .FullPath = f,
                    .Size = New FileInfo(f).Length,
                    .IsTexture = True
                })
            Next
        End If
        Return list
    End Function

    ''' <summary>
    ''' Hands a fully pre-compressed bundle to ArchivePackager.Pack. The caller (Pack) is
    ''' responsible for filling chunkEntries with VirtualEntries that already have PreCompressed
    ''' set and PreCompressedCompSize / PreCompressedDecompSize populated, so distribution can
    ''' work on exact compressed sizes and the resulting archive lands close to the cap.
    '''
    ''' chunkSources is the parallel list of loose-file paths backing each VE (same length, same
    ''' index) so we can delete them post-pack without re-deriving the path from the VE.
    '''
    ''' Memory peak per call ≈ chunkCompBytes (the in-RAM bundle the writer streams to disk).
    ''' </summary>
    Private Sub FlushChunk(dataDir As String,
                            game As GameKind,
                            ba2Version As UInteger,
                            chunkEntries As List(Of VirtualEntry),
                            chunkSources As List(Of String),
                            chunkCompBytes As Long,
                            chunkMaxComp As Long,
                            accumResult As PackagerResult,
                            progress As IProgress(Of PackProgress),
                            totalEntries As Integer,
                            entriesDone As Integer,
                            ct As CancellationToken)
        If ct.IsCancellationRequested Then Return
        If chunkEntries.Count = 0 Then Return

        ReportStage(progress,
                    $"Writing archive ({chunkEntries.Count:N0} entries, {chunkCompBytes / (1024.0 * 1024.0 * 1024.0):N2} GB compressed)…",
                    entriesDone, totalEntries)

        ' Unregister any existing WM_ClonePack* archives BEFORE the packager tries to rewrite
        ' them: hay que dejar de SERVIR entradas del archive viejo, cuyos índices dejan de valer
        ' apenas se reescribe.
        ' ⛔⛔ ACÁ DECÍA "Explicitly disposing the pool here makes the rewrite path race-free". ERA FALSO.
        ' Vaciar el pool no alcanza: un reader ALQUILADO ya salió del pool y su FileStream vive todo el
        ' ExtractToMemory, así que UnregisterArchive vuelve con ese handle abierto. Lo que hace que el
        ' packager funcione con un lector en vuelo era el FileShare.Delete de
        ' FilesDictionary_class.AbrirArchiveParaLectura, no este loop.
        ' ⛔ ESO YA NO ALCANZA: el packager dejo de renombrar y ahora VUELCA el archive nuevo ENCIMA del
        ' original (para que no se salga del mod bajo Mod Organizer), y volcar pide ESCRITURA, que las
        ' lecturas no comparten. Con un lease en vuelo el volcado reintenta y, si no puede, el pack falla
        ' limpio con el archive viejo intacto. Ver EscrituraEnElLugar.VolcarEncima.
        Dim preSet = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)

        ' El orden se captura ANTES de desmontar; el re-montaje de más abajo lo restaura. Ver
        ' CapturarOrdenDeArchives: la misma ley que aplica el remonte del Unpack.
        Dim ordenPrevioPack = CapturarOrdenDeArchives(preSet.Archives)

        For Each archivePath In preSet.Archives
            Try
                FilesDictionary_class.UnregisterArchive(archivePath)
            Catch
            End Try
        Next

        Dim req As New PackagerRequest With {
            .Game = game,
            .Ba2Version = ba2Version,
            .ModBaseName = MOD_BASE_NAME,
            .OutputDir = dataDir,
            .Entries = chunkEntries,
            .MaxArchiveBytes = chunkMaxComp,
            .BundleAlreadyCompressed = True,
            .MinFreeSpaceToFill = 100L * 1024L * 1024L,
            .Overflow = ArchiveOverflowPolicy.SplitByPlugin,
            .PluginWriter = Sub(p As String, g As GameKind)
                                PluginWriter.WriteLightMasterDummy(p, MapGameBack(g), "Wardrobe Manager")
                            End Sub
        }

        ' Tell the form which slot is about to be created (the next free numbered slot, or the
        ' base name if no archives exist yet). Updates the bottom box label only — no flicker.
        Dim nextSlotName = PredictNextSlotName(preSet, MOD_BASE_NAME)
        ReportStageBox(progress,
                       $"Writing archive ({chunkEntries.Count:N0} entries, {chunkCompBytes / (1024.0 * 1024.0 * 1024.0):N2} GB compressed)…",
                       $"Creating archive — {nextSlotName}",
                       -1, -1)

        ' Wire writer events to drive the per-entry progress bar within this chunk.
        _writerProgress = progress
        _writerCounter = entriesDone
        _writerTotal = totalEntries

        AddHandler Ba2WriterGNRL.Writed, AddressOf OnWriterWrited
        AddHandler Ba2WriterDX10.Writed, AddressOf OnWriterWrited
        AddHandler BsaWriter.Writed, AddressOf OnWriterWrited

        ' ⛔⛔ EL RE-MONTAJE VA EN UN `Finally`, Y ES EL MISMO `Try` QUE ENVUELVE AL `Pack`. Antes el `Pack`
        ' corría sin red desde acá (re-lanza sus fallos) y el llamador tampoco lo atrapaba: si tiraba, el
        ' loop de re-montaje de más abajo NUNCA corría, y los archives que este método desregistró al
        ' empezar quedaban DESMONTADOS el resto de la sesión. El diccionario perdía esas entradas y la UI
        ' mostraba assets faltantes sin un solo error a la vista. NPC_Manager ya lo hacía bien: sus tres
        ' call sites de FlushChunk capturan y su paso de re-montaje corre igual.
        Dim chunkResult As PackagerResult = Nothing
        Try
            Try
                chunkResult = ArchivePackager.Pack(req)
            Finally
                RemoveHandler Ba2WriterGNRL.Writed, AddressOf OnWriterWrited
                RemoveHandler Ba2WriterDX10.Writed, AddressOf OnWriterWrited
                RemoveHandler BsaWriter.Writed, AddressOf OnWriterWrited
                _writerProgress = Nothing
            End Try

            accumResult.Archives.AddRange(chunkResult.Archives)
            accumResult.Plugins.AddRange(chunkResult.Plugins)
            accumResult.Skipped.AddRange(chunkResult.Skipped)

            ' After-the-fact summary of what got written this chunk (in case the packager produced
            ' more than one archive — e.g. Main + Textures for FO4).
            If chunkResult.Archives.Count > 0 Then
                Dim names = String.Join(", ", chunkResult.Archives.Select(Function(p) Path.GetFileName(p)))
                ReportStageBox(progress,
                               $"Wrote archive(s): {names}",
                               $"Wrote: {names}",
                               -1, -1)
            End If
        Finally
            ' Drop the entries' bytes ASAP so the next chunk has clean memory.
            For Each ve In chunkEntries
                ve.Data = Nothing
                ve.PreCompressedBytes = Nothing
            Next

            ' Re-mount EVERY archive in the set, not just the ones rewritten this chunk: we
            ' Unregistered them all at the top of this method to free pool handles, and
            ' chunkResult.Archives only includes the ones the packager actually touched
            ' (Skipped / unchanged ones aren't there). DiscoverArchiveSet picks up everything.
            ' Corre TAMBIÉN si el Pack falló: es el estado del diccionario lo que hay que restaurar,
            ' y con el pack a medias importa más, no menos.
            Try
                Dim postSet = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
                For Each archivePath In postSet.Archives
                    ' ⛔ EL Try VA ADENTRO DEL For. Envolviendo el loop entero, un archive que desaparece
                    ' entre el Discover y el Register (antivirus, MO2, el usuario) abortaba el loop y dejaba
                    ' TODOS los archives siguientes desmontados —con su generación ya bumpeada, o sea todos
                    ' sus assets resolviendo a nada— en silencio y por el resto de la sesión.
                    ' ⛔⛔ Y EL `SourceOrder` SE RESTAURA, IGUAL QUE EN EL REMONTE DEL UNPACK. Este camino
                    ' llamaba `RegisterArchive(archivePath)` a secas, o sea que APLANABA la prioridad a
                    ' `ArchiveSourceOrder_RuntimeRegistered` — la MISMA ley que `RemontarConservados`
                    ' declara 300 líneas más abajo, violada acá arriba. No es una exención: el Pack
                    ' re-monta EL MISMO set que acaba de desmontar, así que el orden relativo entre sus
                    ' archives decide igual quién gana una ruta repetida. Se usa el mapa capturado ANTES
                    ' del desregistro de este mismo camino.
                    ' ⛔⛔ DOS `Try` SEPARADOS Y SIN `Catch` PELADO — la MISMA forma que el packer de
                    ' FaceGen, y acá estaba la versión defectuosa que aquel comentario declara superada.
                    ' Con las dos llamadas en un solo bloque, un `UnregisterArchive` que tira SALTEA el
                    ' `Register`: el archive queda FUERA del diccionario con su ContentGen ya bumpeado —o
                    ' sea todos sus assets resolviendo a nada— y encima con el flag de
                    ' `_registeredArchives` puesto (`DesmontarBajoCandado` lo baja al final), así que todo
                    ' reintento posterior sale por el guard de idempotencia sin montar nada. Y el `Catch`
                    ' pelado lo dejaba SILENCIOSO.
                    Dim nombreArchivoPack = Path.GetFileName(archivePath)
                    Try
                        FilesDictionary_class.UnregisterArchive(archivePath)
                    Catch ex As Exception
                        archivesNoRemontados.Add($"{nombreArchivoPack} (unmount): {ex.Message}")
                    End Try
                    Try
                        Dim ordenPack As Integer
                        If ordenPrevioPack.TryGetValue(nombreArchivoPack, ordenPack) Then
                            FilesDictionary_class.RegisterArchive(archivePath, ordenPack)
                        Else
                            FilesDictionary_class.RegisterArchive(archivePath)
                        End If
                    Catch ex As Exception
                        archivesNoRemontados.Add($"{nombreArchivoPack}: {ex.Message}")
                    End Try
                Next
            Catch
                ' Restaurar el montaje no puede convertirse en una segunda excepción que tape la primera.
            End Try
        End Try

        ' Delete the loose sources of this batch (sanity-guarded to ManoloCloned\ paths).
        ' After deleting each file, walk up emptied parent directories and prune them too,
        ' STOPPING before removing any directory named "ManoloCloned" (the root of the cloned
        ' tree must persist even when empty).
        For Each src In chunkSources
            Try
                Dim relUnderData = Path.GetRelativePath(dataDir, src).Correct_Path_Separator
                If Not relUnderData.Contains("ManoloCloned\", StringComparison.OrdinalIgnoreCase) Then Continue For
                File.Delete(src)
                FilesDictionary_class.RemoveDictionaryEntry(relUnderData)
                PruneEmptyAncestors(src, dataDir)
            Catch
                ' Leave it; archive already has the content.
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Climbs the directory tree from the file's parent, removing each directory that has been
    ''' left empty by a recent delete. Stops at the first non-empty ancestor or at any directory
    ''' named "ManoloCloned" (case-insensitive) — that root must survive even when empty so
    ''' subsequent clones land in a consistent tree. Also stops if it would step out of dataDir.
    ''' </summary>
    Private Sub PruneEmptyAncestors(deletedFilePath As String, dataDir As String)
        Dim dir = Path.GetDirectoryName(deletedFilePath)
        Dim dataFull = Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar)
        While Not String.IsNullOrEmpty(dir) AndAlso Directory.Exists(dir)
            ' Refuse to delete the ManoloCloned root.
            Dim leaf = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar))
            If String.Equals(leaf, "ManoloCloned", StringComparison.OrdinalIgnoreCase) Then Exit While

            ' Refuse to climb above dataDir (sanity).
            Dim dirFull = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar)
            If Not dirFull.StartsWith(dataFull, StringComparison.OrdinalIgnoreCase) Then Exit While
            If String.Equals(dirFull, dataFull, StringComparison.OrdinalIgnoreCase) Then Exit While

            ' Stop if this directory still has content.
            If Directory.EnumerateFileSystemEntries(dir).Any() Then Exit While

            Try
                Directory.Delete(dir)
            Catch
                Exit While
            End Try
            dir = Path.GetDirectoryName(dir)
        End While
    End Sub

    ''' <summary>
    ''' Reverse Pack: extract every entry from the WM_ClonePack archive set as loose under
    ''' Data\..., then remove the archives and their plugins. After a successful unpack:
    '''   - archives are unmounted via FilesDictionary.UnregisterArchive
    '''   - extracted loose files are added to FilesDictionary so they're resolvable this session
    ''' </summary>
    Public Async Function UnpackAsync(Optional progress As IProgress(Of PackProgress) = Nothing,
                                       Optional ct As CancellationToken = Nothing) As Task(Of UnpackResult)
        Return Await Task.Run(Function() Unpack(progress, ct))
    End Function

    Public Function Unpack(Optional progress As IProgress(Of PackProgress) = Nothing,
                            Optional ct As CancellationToken = Nothing) As UnpackResult
        ' ⛔⛔ SE LIMPIA AL ENTRAR, NO AL REMONTAR, Y ES LA PRIMERA SENTENCIA DEL MÉTODO.
        ' `RemontarConservados` lo resetea, pero hay salidas que NUNCA llegan a llamarlo: el early-return
        ' de "Nothing to unpack" y los DOS throws de acá abajo (data path y wrapper). En esas tres
        ' sobrevivía el aviso de la corrida ANTERIOR. El escenario es concreto y feo: Unpack #1 deja un
        ' archive sin remontar, el usuario desactiva ese mod, Unpack #2 no tiene nada que hacer… y muestra
        ' el WARNING VIEJO nombrando un .ba2 que ya no existe — en DarkOrange, porque el teñido lo vuelve
        ' creíble. Un aviso obsoleto con formato de alarma es peor que no tener aviso.
        ' La entrada es el único punto por el que pasan TODAS las salidas, incluidas las que se van antes
        ' de hacer nada; por eso el reset va acá y no en el camino feliz.
        ' (Antes este párrafo se justificaba "igual que Pack": era FALSO — Pack limpiaba DESPUÉS de sus
        ' dos throws, así que la simetría no existía. Se corrigió allá también, y ahora sí es simétrico.)
        UltimoAvisoRemonte = ""

        Dim dataDir = Config_App.Current.FO4EDataPath
        If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then
            Throw New InvalidOperationException("Data folder not configured / missing.")
        End If

        ' ⛔⛔ ACÁ EL WRAPPER ROTO ES PÉRDIDA DE DATOS, no una degradación. Extraer una entrada DX10 pasa por
        ' Loader.EncodeDDSHeader; con el wrapper desajustado devuelve 0 bytes, el unpack ESCRIBE un .dds
        ' vacío por cada textura y después BORRA el .ba2, que era la única copia. Se chequea antes de tocar
        ' nada.
        Dim fallaWrapperUnpack = DirectXTexWrapperGate.Verificar()
        If fallaWrapperUnpack <> "" Then Throw New InvalidOperationException(fallaWrapperUnpack)

        Dim setInfo = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        If setInfo.Archives.Count = 0 AndAlso setInfo.Plugins.Count = 0 Then
            ReportStage(progress, "Nothing to unpack.", 0, 0)
            Return New UnpackResult()
        End If

        ' Drop any in-process readers/index entries for the soon-to-be-deleted archives so the
        ' file handles don't block deletion and stale entries don't survive in the dictionary.
        ' ⛔ EL `SourceOrder` SE CAPTURA ANTES DE DESMONTAR. Es la prioridad con la que cada archive gana
        ' o pierde un conflicto de ruta; `RegisterArchive` sin argumento lo aplana a
        ' `ArchiveSourceOrder_RuntimeRegistered` (Integer.MaxValue-1), que le gana a TODO archive de
        ' plugin. Remontar con el default cambiaría en silencio quién gana entre dos archives del MISMO
        ' set, así que se guarda el valor real y se restaura. La captura es UNA pasada sobre el
        ' diccionario —la misma escala que el registro de sueltos que ya hace esta función— y se hace
        ' ANTES del desregistro porque después la información ya no existe.
        Dim ordenPrevio = CapturarOrdenDeArchives(setInfo.Archives)

        For Each archivePath In setInfo.Archives
            Try
                FilesDictionary_class.UnregisterArchive(archivePath)
            Catch
            End Try
        Next

        ' Checkpoint de cancelación: los archives están DESMONTADOS pero todavía no se borró ninguno.
        ' ⛔ "Cancelling here is safe" ERA FALSO Y ESO DECÍA ACÁ. Nada se había borrado del DISCO, sí —
        ' pero el desregistro de arriba ya había corrido, así que cancelar en esta ventana dejaba los N
        ' archives del set vivos y DESMONTADOS: el usuario aprieta Stop, no se toca un solo byte, y su
        ' contenido empaquetado desaparece de la app hasta un `Fill_Dictionary` completo. Se remontan.
        If ct.IsCancellationRequested Then
            Dim previo As New UnpackResult()
            For Each archivePath In setInfo.Archives
                previo.ArchivesConservados.Add(archivePath)
            Next
            Dim falloPrevio = RemontarConservados(previo, ordenPrevio)
            ReportStage(progress,
                        If(falloPrevio.Count = 0,
                           "Stopped before unpack. Archives left mounted and untouched.",
                           $"Stopped before unpack. {falloPrevio.Count} archive(s) could NOT be re-mounted: " &
                           String.Join(", ", falloPrevio.Select(AddressOf Path.GetFileName))),
                        0, 0)
            Return previo
        End If

        ReportStage(progress, $"Unpacking {setInfo.Archives.Count} archive(s)…", 0, 0)
        Dim req As New UnpackRequest With {
            .OutputDir = dataDir,
            .ModBaseName = MOD_BASE_NAME,
            .LooseDataDir = dataDir
        }

        ' Per-entry progress callback: report every 32 entries (or on the last one) to keep the
        ' UI thread under-saturated even on large archive sets. The callback runs on whatever
        ' thread invokes onEntry inside the lib (here the same Task.Run worker), so handing it
        ' off through the IProgress(Of T) marshals back to the UI thread for free.
        Dim onEntry As Action(Of Integer, Integer, String) =
            Sub(done As Integer, total As Integer, relPath As String)
                If (done And &H1F) = 0 OrElse done = total Then
                    ReportStage(progress, $"Extracting… {done:N0}/{total:N0} — {relPath}", done, total)
                End If
            End Sub

        ' Per-archive milestone callback: low-frequency, fires once at the start of each archive
        ' the lib opens. Updates the bottom box label with the archive currently being processed
        ' (no flicker, archives extract in seconds-to-minutes range). Max=-1 tells the form to
        ' leave the progress bar alone — we don't want to reset the per-entry progress to marquee.
        Dim onArchiveStart As Action(Of String, Integer, Integer) =
            Sub(archivePath As String, archiveIdx As Integer, archiveCount As Integer)
                Dim name = Path.GetFileName(archivePath)
                ReportStageBox(progress,
                               $"Extracting archive {archiveIdx}/{archiveCount} — {name}",
                               $"Processing archive {archiveIdx} of {archiveCount} — {name}",
                               -1, -1)
            End Sub

        ' ⛔⛔ EL CAMINO DE ERROR TAMBIEN REGISTRA. Arriba se DESREGISTRARON los archives (hay que
        ' hacerlo: si no, se siguen sirviendo entradas de un archive que se va a borrar) y los sueltos se
        ' registran ABAJO. Con la excepcion saliendo derecho por el medio, el diccionario quedaba sin las
        ' entradas del archive Y sin las de los sueltos: contenido que YA ESTA EN DISCO y que la app no ve
        ' hasta un Fill_Dictionary completo. `UnpackParcialException` trae el resultado justamente para
        ' que esta mitad corra igual; despues se vuelve a tirar, porque los fallos son del usuario y no se
        ' tragan. Ver ArchivePackager.UnpackParcialException. Gate: Tools\UnpackSueltosGate U6.
        Dim result As UnpackResult
        Dim parcial As UnpackParcialException = Nothing
        ' El stack ORIGINAL se conserva con ExceptionDispatchInfo: un `Throw parcial` fuera del Catch lo
        ' pisaria con el de la re-tirada y el log diria que el fallo nacio en esta linea.
        Dim reTirar As Runtime.ExceptionServices.ExceptionDispatchInfo = Nothing
        Try
            result = ArchivePackager.Unpack(req, onEntry, ct, onArchiveStart)
        Catch ex As UnpackParcialException
            parcial = ex
            result = ex.Resultado
            reTirar = Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex)
        Catch exOtra As Exception
            ' ⛔⛔ LA CUARTA PUERTA, Y ES LA MAS BARATA DE DISPARAR. El PRE-PASS de conteo de `Unpack`
            ' abre cada archive para sumar entradas y deja propagar IOException /
            ' UnauthorizedAccessException CRUDAS — deliberado: si un archive esta tomado se surfacea
            ' temprano y NO se toca un solo byte. Pero esa excepcion no es `UnpackParcialException`, sale
            ' ANTES de que la libreria pueble `ArchivesConservados`, y acá arriba ya desregistramos los N.
            ' Con un `Catch` tipado, un .ba2 tomado por el antivirus, por MO2 o por OneDrive dejaba el set
            ' ENTERO desmontado, con todo intacto en disco y un "Unpack failed" sobre cero daño.
            ' El resultado se arma ACA con lo unico que se sabe cierto: los archives del set que SIGUEN en
            ' disco. Medicion del contrato de la libreria: Tools\UnpackSueltosGate U10.
            result = New UnpackResult()
            For Each archivePath In setInfo.Archives
                If File.Exists(archivePath) Then result.ArchivesConservados.Add(archivePath)
            Next
            reTirar = Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exOtra)
            Dim falloAncho = RemontarConservados(result, ordenPrevio)
            ReportStage(progress,
                        $"Unpack failed before extracting. {result.ArchivesConservados.Count - falloAncho.Count} " &
                        $"archive(s) re-mounted, nothing was written." &
                        If(falloAncho.Count = 0, "",
                           $" {falloAncho.Count} could NOT be re-mounted: " &
                           String.Join(", ", falloAncho.Select(AddressOf Path.GetFileName))),
                        0, 0)
            reTirar.Throw()
        End Try

        ' Add the freshly extracted loose files to FilesDictionary as loose entries so previews
        ' don't need a full Fill_DictionaryAsync rebuild to find them.
        Dim looseTotal = result.LooseFilesWritten.Count
        Dim looseDone As Integer = 0
        For Each loosePath In result.LooseFilesWritten
            Try
                Dim relUnderData = Path.GetRelativePath(dataDir, loosePath).Correct_Path_Separator
                Dim loc As New FilesDictionary_class.File_Location With {
                    .BA2File = "",
                    .Index = -1,
                    .FullPath = relUnderData,
                    .SourceOrder = Integer.MaxValue,
                    .FileDate = File.GetLastWriteTime(loosePath)
                }
                FilesDictionary_class.AddOrUpdateDictionaryEntry(relUnderData, loc)
            Catch
            End Try
            looseDone += 1
            If (looseDone Mod 64) = 0 OrElse looseDone = looseTotal Then
                ReportStage(progress, $"Registering loose files… ({looseDone}/{looseTotal})", looseDone, looseTotal)
            End If
        Next

        ' ⛔⛔ Y LOS ARCHIVES QUE SOBREVIVIERON VUELVEN AL DICCIONARIO. Arriba se desregistraron LOS N del
        ' set —hay que hacerlo, si no se siguen sirviendo entradas de archives que se van a borrar—, pero
        ' el Unpack puede salir temprano y dejar archives VIVOS EN DISCO: el `Exit For` de uno fallido
        ' deja a todos los POSTERIORES intactos, y una CANCELACIÓN deja todos los que faltaban. Sin este
        ' remonte quedaban desmontados hasta un `Fill_Dictionary` completo — contenido invisible sin que
        ' nada hubiera fallado con ellos. Corre en TODAS las salidas —éxito, unpack parcial, cancelación y
        ' el fallo ancho del pre-pass— porque `ArchivesConservados` se llena siempre; en el éxito la lista
        ' está vacía y esto es un no-op.
        Dim noRemontados = RemontarConservados(result, ordenPrevio)

        ' ⛔ Y RECIEN ACA se vuelve a tirar: lo escrito ya quedo REGISTRADO, y el mensaje del packer —que
        ' nombra cada entrada que fallo y su causa— llega al usuario intacto.
        ' El remonte se REPORTA con la verdad: si alguno no se pudo, se nombra. Decir "conservados" a
        ' secas cuando uno quedó desmontado es la mentira que este texto existe para no repetir.
        Dim colaRemonte = If(noRemontados.Count = 0, "",
                             $" {noRemontados.Count} archive(s) could NOT be re-mounted and stay invisible " &
                             $"until a full refresh: " & String.Join(", ", noRemontados.Select(AddressOf Path.GetFileName)))

        If parcial IsNot Nothing Then
            ReportStage(progress,
                        $"Unpack incomplete: {parcial.Resultado.Fallos.Count} entry(ies) failed; " &
                        $"{looseTotal} loose file(s) were written and registered." & colaRemonte,
                        looseTotal, looseTotal)
            reTirar.Throw()
        End If

        ReportStage(progress,
                    $"Done. Removed {result.ArchivesRemoved.Count} archive(s), {result.PluginsRemoved.Count} plugin(s); wrote {looseTotal} loose file(s)." & colaRemonte,
                    looseTotal, looseTotal)
        Return result
    End Function

    ''' <summary>Vuelve a montar en el <c>FilesDictionary</c> los archives que el Unpack CONSERVÓ.
    ''' <para>⛔ POR QUÉ HACE FALTA: este módulo desregistra los N archives del set ANTES de llamar a
    ''' <c>ArchivePackager.Unpack</c>, y el Unpack tiene dos salidas tempranas que dejan archives vivos en
    ''' disco —un archive fallido corta el barrido con <c>Exit For</c> y deja los posteriores intactos, y
    ''' una cancelación deja todos los que faltaban. Sin remontarlos quedan invisibles para la app sin
    ''' que nada haya fallado con ellos.</para>
    ''' <para>Best-effort por archive y a propósito: que uno no se pueda volver a montar (lo borró alguien
    ''' en el medio, permisos) no puede tumbar al resto ni convertir un unpack parcial en un crash. Lo que
    ''' no se pudo remontar lo resuelve el próximo <c>Fill_Dictionary</c>.</para>
    ''' <para>⛔ PERO NO ES SILENCIOSO: <b>devuelve los que NO pudo remontar</b>, y el llamador lo dice.
    ''' Antes tragaba el fallo y la UI afirmaba igual "archive(s) left in place and still loaded" — una
    ''' frase que era MENTIRA justo en el caso que importa, porque el usuario se queda sin saber que hay
    ''' contenido suyo invisible.</para>
    ''' <para>⛔ Y RESTAURA EL <c>SourceOrder</c>. Es la prioridad con la que el archive gana o pierde un
    ''' conflicto de ruta; <c>RegisterArchive</c> sin argumento lo aplana a
    ''' <c>ArchiveSourceOrder_RuntimeRegistered</c>, que le gana a todo archive de plugin. Con un mapa
    ''' vacío (no se pudo leer el diccionario antes de desmontar) se cae al default y el aplanado queda
    ''' declarado acá en vez de pasar en silencio: el riesgo residual es entre archives del MISMO set,
    ''' donde el orden relativo decide quién gana una ruta repetida.</para></summary>
    Private Function RemontarConservados(result As UnpackResult,
                                         ordenPrevio As Dictionary(Of String, Integer)) As List(Of String)
        Dim fallaron As New List(Of String)
        ' ⛔ EL AVISO SE PUBLICA ACA, en el único lugar por el que pasan las cuatro salidas. Ponerlo en
        ' cada llamador es cómo se olvida una rama — que es literalmente lo que pasó con el label.
        UltimoAvisoRemonte = ""
        If result Is Nothing Then Return fallaron
        For Each archivePath In result.ArchivesConservados
            Try
                If Not File.Exists(archivePath) Then Continue For
                Dim orden As Integer
                If ordenPrevio IsNot Nothing AndAlso
                   ordenPrevio.TryGetValue(Path.GetFileName(archivePath), orden) Then
                    FilesDictionary_class.RegisterArchive(archivePath, orden)
                Else
                    FilesDictionary_class.RegisterArchive(archivePath)
                End If
            Catch ex As Exception
                fallaron.Add(archivePath)
                Logger.LogLazy(Function() $"[UNPACK] no se pudo remontar '{archivePath}': {ex.GetType().Name}: {ex.Message}")
            End Try
        Next
        If fallaron.Count > 0 Then
            ' Detalle completo a un archivo REAL (el Logger de arriba no existe en Release) y la ruta
            ' viaja en el aviso, para que el resumen persistente pueda decir dónde mirar.
            Dim ruta = EscribirReporte("archives_not_remounted",
                                       "Wardrobe Manager - archives that could NOT be re-mounted after Unpack." & vbCrLf &
                                       "They are still on disk and intact, but the app will not resolve their" & vbCrLf &
                                       "contents until the file dictionary is rebuilt (restart or full refresh)." & vbCrLf &
                                       $"Count: {fallaron.Count}" & vbCrLf & vbCrLf &
                                       String.Join(vbCrLf, fallaron))
            UltimoAvisoRemonte = $" WARNING: {fallaron.Count} archive(s) could NOT be re-mounted and their " &
                                 "contents stay invisible until you restart or refresh: " &
                                 String.Join(", ", fallaron.Select(AddressOf Path.GetFileName)) &
                                 If(ruta = "", ".", $". Details: {ruta}")
        End If
        Return fallaron
    End Function

    ' ---- progress helpers (module-level state shared by the writer event handlers) ----

    Private _writerProgress As IProgress(Of PackProgress)
    Private _writerCounter As Integer
    Private _writerTotal As Integer

    Private Sub OnWriterWrited()
        Dim n = Interlocked.Increment(_writerCounter)
        Dim p = _writerProgress
        If p IsNot Nothing Then
            ' Throttle to roughly every 8 entries; the .NET Progress(Of T) marshals to the UI
            ' thread, and reporting per entry on a 100k-bundle just floods the dispatcher.
            If (n And &H7) = 0 OrElse n = _writerTotal Then
                ReportStage(p, $"Packing entries… ({n}/{_writerTotal})", n, _writerTotal)
            End If
        End If
    End Sub

    Private Sub ReportStage(progress As IProgress(Of PackProgress), stage As String, current As Integer, max As Integer)
        progress?.Report(New PackProgress With {.Stage = stage, .Current = current, .Max = max})
    End Sub

    ' Variant that also sets BoxText so the form can update the bottom status label with a
    ' low-frequency milestone (e.g. "Processing archive 3 of 12 — name.ba2").
    Private Sub ReportStageBox(progress As IProgress(Of PackProgress), stage As String, boxText As String, current As Integer, max As Integer)
        progress?.Report(New PackProgress With {.Stage = stage, .BoxText = boxText, .Current = current, .Max = max})
    End Sub

    ''' <summary>
    ''' Best-effort prediction of which plugin slot the packager is going to create for the next
    ''' chunk. Used to fill in a "Creating archive — name" hint BEFORE the packager runs (the
    ''' actual archive paths come back in PackagerResult.Archives but only after Pack returns).
    ''' Walks the existing plugin set in OutputDir, finds the highest numeric suffix, returns the
    ''' next one (or the base name if no slot 1 exists yet). Doesn't claim to be authoritative —
    ''' the packager may anchor to an existing slot if there's a path match. After Pack we
    ''' overwrite the box text with the real names from chunkResult.Archives.
    ''' </summary>
    Private Function PredictNextSlotName(setInfo As ArchiveSetInfo, baseName As String) As String
        Dim maxSlot As Integer = 0
        Dim hasBase As Boolean = False
        For Each pluginPath In setInfo.Plugins
            Dim stem = Path.GetFileNameWithoutExtension(pluginPath)
            If String.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase) Then
                hasBase = True
                Continue For
            End If
            If stem.StartsWith(baseName, StringComparison.OrdinalIgnoreCase) Then
                Dim suffix = stem.Substring(baseName.Length)
                Dim n As Integer
                If Integer.TryParse(suffix, n) AndAlso n >= 2 Then
                    If n > maxSlot Then maxSlot = n
                End If
            End If
        Next
        If Not hasBase Then Return baseName
        Return baseName & (Math.Max(maxSlot, 1) + 1).ToString()
    End Function

    ' ---- helpers ----

    ''' <summary>Tamaño de un archivo, o 0 si desapareció o no se puede mirar.
    ''' <para>⛔⛔ EL `IgnoreInaccessible` DE LA ENUMERACIÓN NO CUBRE EL STAT, y esa asimetría era el
    ''' agujero. <c>EnumerateLooseFiles</c> enumera con <c>IgnoreInaccessible = True</c>, así que un
    ''' archivo que desaparece o al que no se puede entrar NO rompe el barrido; pero el
    ''' <c>New FileInfo(f).Length</c> que venía después SÍ tiraba. Entre la enumeración y el stat hay una
    ''' ventana real y estrecha en la que MO2, Vortex, OneDrive o el antivirus borran o bloquean un .dds
    ''' —el propio <c>Catch</c> de <c>RefreshClonedMaterialStatus</c> declara ese transitorio como
    ''' NORMAL—, y con eso se caía la lectura de estado ENTERA por un archivo de una carpeta de miles.
    ''' <para>⚠️ LA CONDUCTA REAL, y NO es "lo mismo que ya pasaba con los inaccesibles" —eso era falso—:
    ''' <c>IgnoreInaccessible</c> saca al archivo del barrido ENTERO, así que no suma ni al CONTEO ni a
    ''' los bytes; esto otro lo deja CONTADO con <b>0 bytes</b>. Son dos conductas distintas y la
    ''' diferencia es deliberada: el archivo existía cuando se enumeró, así que contarlo describe mejor
    ''' lo que hay que un salto silencioso. Lo que importa es el CONTEO —de ahí salen los botones de
    ''' Pack/Unpack y la decisión de si hay algo que empaquetar—; el total de bytes es display, y quedar
    ''' corto en unos KB de un archivo que desapareció no cambia ninguna decisión.
    ''' No se inventa un total: se informa lo que se pudo medir.</para></para></summary>
    Private Function TamanoSeguro(path As String) As Long
        Try
            Return New FileInfo(path).Length
        Catch
            Return 0
        End Try
    End Function

    Private Iterator Function EnumerateLooseFiles(root As String, extensions As String()) As IEnumerable(Of String)
        Dim opts As New EnumerationOptions() With {
            .RecurseSubdirectories = True,
            .IgnoreInaccessible = True
        }
        For Each ext In extensions
            For Each f In Directory.EnumerateFiles(root, "*" & ext, opts)
                Yield f
            Next
        Next
    End Function

    ''' <summary>
    ''' Builds a VirtualEntry for a .bgsm/.bgem/.mat. Reads the file, computes CRC32 of the raw
    ''' bytes (used by ComputeDiff for idempotent re-Pack), then compresses up front via
    ''' PayloadCompressor so distribution sees the exact archive footprint and the writer
    ''' stream-copies the bytes verbatim. Materials always go through the GNRL path on FO4 or
    ''' the BSA path on SSE.
    ''' </summary>

    ''' <summary>
    ''' Builds a VirtualEntry for a .dds. The shape and contract depends on the target format:
    '''   - FO4 (BA2 DX10): the DDS header is parsed (μs via Loader.GetDdsMetadata) to populate
    '''     ve.Width/Height/MipCount/etc., then the stripped payload (mip data only) is compressed
    '''     up front. CRC32 is taken over the raw stripped payload — same bytes ComputeDiff
    '''     compares against (the writer reconstructs the DDS header from metadata at extract time).
    '''   - SSE (BSA): the entire .dds file is treated as opaque bytes, compressed with LZ4 frame
    '''     to match the archive's GlobalCompressed flag. CRC32 is over the whole file.
    ''' </summary>



End Module
