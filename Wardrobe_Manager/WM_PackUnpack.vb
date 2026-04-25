Option Strict On
Imports System.IO
Imports System.Threading
Imports BSA_BA2_Library_DLL.BethesdaArchive.Core

''' <summary>
''' Bridges Clone_Materials_class' loose output (under Materials\ManoloCloned\ and
''' Textures\ManoloCloned\) and ArchivePackager's archive set. Pack collects the loose
''' files into BA2/BSA companion archives plus their dummy plugins; Unpack reverses the
''' operation. Only materials (.bgsm/.bgem) and textures (.dds) under ManoloCloned\ are
''' touched — the NIF/OSD/TRI clone outputs stay loose as the user requested.
''' </summary>
Public Module WM_PackUnpack
    Public Const MOD_BASE_NAME As String = "WM_ClonePack"
    Public Const CLONED_PREFIX As String = "ManoloCloned\"

    ' Per-game caps. SSE BSA has a hard u32 offset limit at 4GB — 3GB leaves margin for header
    ' overhead and LZ4 frame inflation. FO4 BA2 uses u64 offsets but the engine is reported
    ' unstable >4GB; 3GB is the safe sweet spot.
    Private Const MAX_BYTES_FO4 As Long = 3L * 1024L * 1024L * 1024L
    Private Const MAX_BYTES_SSE As Long = 3L * 1024L * 1024L * 1024L

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
                status.LooseTotalBytes += New FileInfo(f).Length
            Next
        End If
        If Directory.Exists(textureRoot) Then
            For Each f In EnumerateLooseFiles(textureRoot, {".dds"})
                status.LooseTextureCount += 1
                status.LooseTotalBytes += New FileInfo(f).Length
            Next
        End If

        Dim setInfo = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        status.Archives.AddRange(setInfo.Archives)
        status.Plugins.AddRange(setInfo.Plugins)
        For Each a In setInfo.Archives
            status.ArchiveTotalBytes += New FileInfo(a).Length
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
    ''' Pack the current loose set into BA2/BSA + dummy plugin(s). After a successful pack:
    '''   - newly written archives are mounted via FilesDictionary.RegisterArchive
    '''   - the loose source files are deleted and removed from FilesDictionary
    ''' Failures abort before deletion: rollback inside ArchivePackager keeps existing archives
    ''' intact, and loose files are left alone if anything went wrong.
    ''' </summary>
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
        Dim dataDir = Config_App.Current.FO4EDataPath
        If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then
            Throw New InvalidOperationException("Data folder not configured / missing.")
        End If

        Dim game = MapGame(Config_App.Current.Game)
        Dim chunkMaxComp As Long = If(game = GameKind.FO4_BA2, MAX_BYTES_FO4, MAX_BYTES_SSE)

        ' --- Walk loose: paths + sizes only, no bytes loaded yet. Memory ≈ 50 B/entry. ---
        ReportStage(progress, "Scanning loose files…", 0, 0)
        Dim allLoose As List(Of LooseFileRef) = WalkLooseWithSizes(dataDir)
        If allLoose.Count = 0 Then
            ReportStage(progress, "Nothing to pack.", 0, 0)
            Return New PackagerResult()
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
                                          MakeTextureEntry(dataDir, lf.FullPath),
                                          MakeMaterialEntry(dataDir, lf.FullPath))
                        Catch
                            micro(i) = Nothing
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
                    FlushChunk(dataDir, game, chunkEntries, chunkSources, chunkCompBytes, chunkMaxComp,
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
            FlushChunk(dataDir, game, chunkEntries, chunkSources, chunkCompBytes, chunkMaxComp,
                       accumResult, progress, totalEntries, entriesDone, ct)
            entriesDone += chunkEntries.Count
        End If

        If cancelled Then
            ReportStage(progress,
                        $"Stopped. Wrote {accumResult.Archives.Count} archive(s), {accumResult.Plugins.Count} plugin(s) before stop. Remaining loose files left untouched.",
                        entriesDone, totalEntries)
        Else
            ReportStage(progress,
                        $"Done. Wrote {accumResult.Archives.Count} archive(s), {accumResult.Plugins.Count} plugin(s); skipped {accumResult.Skipped.Count} unchanged.",
                        totalEntries, totalEntries)
        End If
        Return accumResult
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
        ' them. FilesDictionary keeps a pooled reader/FileStream alive (lazy, populated when
        ' anything previously called GetBytes; idle handles linger up to 30 s before the cleanup
        ' timer fires). FileStream uses FileShare.Read by default — that lets other reads in but
        ' BLOCKS File.Move / File.Delete with a sharing violation. Explicitly disposing the pool
        ' here makes the rewrite path race-free.
        Dim preSet = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        For Each archivePath In preSet.Archives
            Try
                FilesDictionary_class.UnregisterArchive(archivePath)
            Catch
            End Try
        Next

        Dim req As New PackagerRequest With {
            .Game = game,
            .ModBaseName = MOD_BASE_NAME,
            .OutputDir = dataDir,
            .Entries = chunkEntries,
            .MaxArchiveBytes = chunkMaxComp,
            .BundleAlreadyCompressed = True,
            .ReuseThreshold = 1.0,
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

        Dim chunkResult As PackagerResult
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

        ' Drop the entries' bytes ASAP so the next chunk has clean memory.
        For Each ve In chunkEntries
            ve.Data = Nothing
            ve.PreCompressedBytes = Nothing
        Next

        ' Re-mount EVERY archive in the set, not just the ones rewritten this chunk: we
        ' Unregistered them all at the top of this method to free pool handles, and
        ' chunkResult.Archives only includes the ones the packager actually touched
        ' (Skipped / unchanged ones aren't there). DiscoverArchiveSet picks up everything.
        Dim postSet = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        For Each archivePath In postSet.Archives
            Try
                FilesDictionary_class.UnregisterArchive(archivePath)
            Catch
            End Try
            FilesDictionary_class.RegisterArchive(archivePath)
        Next

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
        Dim dataDir = Config_App.Current.FO4EDataPath
        If String.IsNullOrEmpty(dataDir) OrElse Not Directory.Exists(dataDir) Then
            Throw New InvalidOperationException("Data folder not configured / missing.")
        End If

        Dim setInfo = ArchivePackager.DiscoverArchiveSet(dataDir, MOD_BASE_NAME)
        If setInfo.Archives.Count = 0 AndAlso setInfo.Plugins.Count = 0 Then
            ReportStage(progress, "Nothing to unpack.", 0, 0)
            Return New UnpackResult()
        End If

        ' Drop any in-process readers/index entries for the soon-to-be-deleted archives so the
        ' file handles don't block deletion and stale entries don't survive in the dictionary.
        For Each archivePath In setInfo.Archives
            Try
                FilesDictionary_class.UnregisterArchive(archivePath)
            Catch
            End Try
        Next

        ' Safe checkpoint: archives unmounted but not deleted yet. Cancelling here is safe.
        If ct.IsCancellationRequested Then
            ReportStage(progress, "Stopped before unpack.", 0, 0)
            Return New UnpackResult()
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

        Dim result = ArchivePackager.Unpack(req, onEntry, ct, onArchiveStart)

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

        ReportStage(progress,
                    $"Done. Removed {result.ArchivesRemoved.Count} archive(s), {result.PluginsRemoved.Count} plugin(s); wrote {looseTotal} loose file(s).",
                    looseTotal, looseTotal)
        Return result
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

    Private Sub BuildBundleFromLoose(dataDir As String,
                                     ByRef loosePaths As List(Of String),
                                     ByRef entries As List(Of VirtualEntry))
        Dim materialRoot = Path.Combine(dataDir, MaterialsPrefix & CLONED_PREFIX.TrimEnd("\"c))
        Dim textureRoot = Path.Combine(dataDir, TexturesPrefix & CLONED_PREFIX.TrimEnd("\"c))

        If Directory.Exists(materialRoot) Then
            For Each f In EnumerateLooseFiles(materialRoot, {".bgsm", ".bgem"})
                Dim ve = MakeMaterialEntry(dataDir, f)
                If ve IsNot Nothing Then
                    entries.Add(ve)
                    loosePaths.Add(f)
                End If
            Next
        End If

        If Directory.Exists(textureRoot) Then
            For Each f In EnumerateLooseFiles(textureRoot, {".dds"})
                Dim ve = MakeTextureEntry(dataDir, f)
                If ve IsNot Nothing Then
                    entries.Add(ve)
                    loosePaths.Add(f)
                End If
            Next
        End If
    End Sub

    ''' <summary>
    ''' Builds a VirtualEntry for a .bgsm/.bgem/.mat. Reads the file, computes CRC32 of the raw
    ''' bytes (used by ComputeDiff for idempotent re-Pack), then compresses up front via
    ''' PayloadCompressor so distribution sees the exact archive footprint and the writer
    ''' stream-copies the bytes verbatim. Materials always go through the GNRL path on FO4 or
    ''' the BSA path on SSE.
    ''' </summary>
    Private Function MakeMaterialEntry(dataDir As String, fullPath As String) As VirtualEntry
        Dim relUnderData = Path.GetRelativePath(dataDir, fullPath).Correct_Path_Separator
        Dim bytes = IO.File.ReadAllBytes(fullPath)
        Dim dir As String = "", file As String = ""
        PathUtil.SplitDirFile(relUnderData, dir, file)
        Dim crc = Ba2WriterCommon.Crc32Bytes(bytes)

        Dim ve As New VirtualEntry With {
            .Directory = dir,
            .FileName = file,
            .Crc32 = crc
        }

        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
            ' BSA: compress with LZ4 frame matching the archive's GlobalCompressed flag (true).
            Dim cp = PayloadCompressor.CompressForBsa(bytes, wantCompressed:=True)
            ve.PreCompressed = True
            ve.PreCompressedBytes = cp.Bytes
            ve.PreCompressedCompSize = cp.CompSize
            ve.PreCompressedDecompSize = cp.DecompSize
        Else
            ' BA2 GNRL: Zlib (default preset, v8 by default — matches Ba2WriterGNRL.Options defaults).
            Dim cp = PayloadCompressor.CompressForBa2Gnrl(bytes,
                version:=8UI,
                compressionFormat:=Ba2WriterCommon.CompressionFormat.Zip,
                preset:=Ba2WriterCommon.ZlibPreset.Default)
            ve.PreCompressed = True
            ve.PreCompressedBytes = cp.Bytes
            ve.PreCompressedCompSize = cp.CompSize
            ve.PreCompressedDecompSize = cp.DecompSize
        End If

        Return ve
    End Function

    ''' <summary>
    ''' Builds a VirtualEntry for a .dds. The shape and contract depends on the target format:
    '''   - FO4 (BA2 DX10): the DDS header is parsed (μs via Loader.GetDdsMetadata) to populate
    '''     ve.Width/Height/MipCount/etc., then the stripped payload (mip data only) is compressed
    '''     up front. CRC32 is taken over the raw stripped payload — same bytes ComputeDiff
    '''     compares against (the writer reconstructs the DDS header from metadata at extract time).
    '''   - SSE (BSA): the entire .dds file is treated as opaque bytes, compressed with LZ4 frame
    '''     to match the archive's GlobalCompressed flag. CRC32 is over the whole file.
    ''' </summary>
    Private Function MakeTextureEntry(dataDir As String, fullPath As String) As VirtualEntry
        Dim relUnderData = Path.GetRelativePath(dataDir, fullPath).Correct_Path_Separator
        Dim bytes = File.ReadAllBytes(fullPath)

        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
            Dim dir As String = "", file As String = ""
            PathUtil.SplitDirFile(relUnderData, dir, file)
            Dim cp = PayloadCompressor.CompressForBsa(bytes, wantCompressed:=True)
            Return New VirtualEntry With {
                .Directory = dir,
                .FileName = file,
                .Crc32 = Ba2WriterCommon.Crc32Bytes(bytes),
                .PreCompressed = True,
                .PreCompressedBytes = cp.Bytes,
                .PreCompressedCompSize = cp.CompSize,
                .PreCompressedDecompSize = cp.DecompSize
            }
        End If

        ' FO4 BA2 DX10: parse header → metadata + stripped payload → compress payload.
        Dim ve = Dx10Importer.FromDdsBytes(bytes, relUnderData)
        Dim payload = If(ve.Data, Array.Empty(Of Byte)())
        ve.Crc32 = Ba2WriterCommon.Crc32Bytes(payload)
        Dim cpDx10 = PayloadCompressor.CompressForBa2Dx10(payload,
            version:=8UI,
            compressionFormat:=Ba2WriterCommon.CompressionFormat.Zip,
            preset:=Ba2WriterCommon.ZlibPreset.Default)
        ve.Data = Nothing                           ' free raw payload — only the compressed copy is needed downstream
        ve.PreCompressed = True
        ve.PreCompressedBytes = cpDx10.Bytes
        ve.PreCompressedCompSize = cpDx10.CompSize
        ve.PreCompressedDecompSize = cpDx10.DecompSize
        Return ve
    End Function

    Private Function MapGame(g As Config_App.Game_Enum) As GameKind
        Select Case g
            Case Config_App.Game_Enum.Fallout4 : Return GameKind.FO4_BA2
            Case Config_App.Game_Enum.Skyrim : Return GameKind.SSE_BSA
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(g))
        End Select
    End Function

    Private Function MapGameBack(g As GameKind) As Config_App.Game_Enum
        Select Case g
            Case GameKind.FO4_BA2 : Return Config_App.Game_Enum.Fallout4
            Case GameKind.SSE_BSA : Return Config_App.Game_Enum.Skyrim
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(g))
        End Select
    End Function

End Module
