' Version Uploaded of Wardrobe 2.1.3
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports NiflySharp.Enums

Module Extensiones
    Public Const MaterialsPrefix As String = "Materials\"
    Public Const TexturesPrefix As String = "Textures\"

    <Extension>
    Public Function Correct_Path_Separator(St As String) As String
        If IsNothing(St) Then Return ""
        Return St.Replace("/", "\")
    End Function

    ''' <summary>Removes prefix (case-insensitive) from the start of the string if present.</summary>
    <Extension>
    Public Function StripPrefix(St As String, prefix As String) As String
        If Not IsNothing(St) AndAlso St.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Return St.Substring(prefix.Length)
        End If
        Return St
    End Function
End Module

Public Class FilesDictionary_class
    Public Shared Property TexturesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = TexturesPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}}
    Public Shared Property MaterialsDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm", ".bgem"}}
    Public Shared Property MaterialsDictionary_BGEM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgem"}}
    Public Shared Property MaterialsDictionary_BGSM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm"}}
    Public Shared Property MeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Meshes\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Shared Property ALLMeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Class DictionaryFilePickerConfig
        ' Debe apuntar a tu ConcurrentDictionary(Of String, File_Location)
        Public Property DictionaryProvider As Func(Of ConcurrentDictionary(Of String, FilesDictionary_class.File_Location))

        ' Prefijo raíz (case-insensitive). Default: "Textures\"
        Public Property RootPrefix As String = TexturesPrefix

        ' Extensiones permitidas (case-insensitive). Default: ".dds"
        Private _allowedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}

        Public Property AllowedExtensions As HashSet(Of String)
            Get
                Return _allowedExtensions
            End Get
            Set(value As HashSet(Of String))
                _allowedExtensions = value
            End Set
        End Property

        Public Sub SetAllowedExtensions(exts As IEnumerable(Of String))
            ArgumentNullException.ThrowIfNull(exts)
            _allowedExtensions = New HashSet(Of String)(exts, StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Function ExtensionAllowed(normalized As String) As Boolean
            Dim fileName = normalized
            Dim iSlash = normalized.LastIndexOf("\"c)
            If iSlash >= 0 AndAlso iSlash < normalized.Length - 1 Then
                fileName = normalized.Substring(iSlash + 1)
            End If
            Dim iDot = fileName.LastIndexOf("."c)
            If iDot < 0 Then Return False
            Dim ext = fileName.Substring(iDot)
            Return AllowedExtensions.Contains(ext)
        End Function
        Public Shared Function PathStartsWithRoot(normalized As String, rootPrefix As String) As Boolean
            If String.IsNullOrEmpty(rootPrefix) Then Return True
            Return normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
        End Function
    End Class

    Private Class DictionaryScanWorkItem
        Public Property IsArchive As Boolean
        Public Property FilePath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue
    End Class
    Public Class File_Location

        Public Property BA2File As String = ""
        Public Property Index As Integer = -1
        Public Property FullPath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue

        Public Function GetBytesFromOpenArchive(pack As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader) As Byte()
            If IsNothing(pack) OrElse IsLosseFile Then Return Array.Empty(Of Byte)
            Try
                Return pack.ExtractToMemory(Index)
            Catch
                Return Array.Empty(Of Byte)
            End Try
        End Function

        Public ReadOnly Property IsLosseFile As Boolean
            Get
                Return BA2File = ""
            End Get
        End Property
        Public Function GetBytes() As Byte()
            If IsLosseFile Then
                If IO.File.Exists(IO.Path.Combine(FO4Path, Me.FullPath)) = False Then Return Array.Empty(Of Byte)
                Return IO.File.ReadAllBytes(IO.Path.Combine(FO4Path, Me.FullPath))
            Else
                Try
                    Using fs As FileStream = File.OpenRead(IO.Path.Combine(FO4Path, Me.BA2File))
                        Using pack As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                            Return pack.ExtractToMemory(Index)
                        End Using
                    End Using
                Catch ex As Exception
                    Return Array.Empty(Of Byte)
                End Try
            End If
        End Function

    End Class
    Private Shared _sliderPresets As New SliderPresetCollection
    Private Shared _fO4Path As String = ""
    Private Shared _dictionary As New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly Extensiones As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds", ".bgsm", ".bgem", ".nif", ".tri"}
    Private Shared _HighHeels_Plugin_Values As New HighHeels_Plugins_values

    Private Shared ReadOnly _KeysByExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _KeysByDirectory As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _KeysByDirectoryExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)

    Public Shared Function GetBytes(File As String) As Byte()
        Dim located_File As File_Location = Nothing
        If Not Dictionary.TryGetValue(NormalizeDictionaryKey(File), located_File) Then
            Return Array.Empty(Of Byte)
        Else
            Return located_File.GetBytes
        End If
    End Function
    Private Shared Function EnumerateSupportedLooseFiles(root As String) As IEnumerable(Of String)
        Dim opts As New EnumerationOptions() With {
        .RecurseSubdirectories = True,
        .IgnoreInaccessible = True
    }

        Return Directory.
        EnumerateFiles(root, "*", opts).
        Where(Function(path) Extensiones.Contains(IO.Path.GetExtension(path)))
    End Function
    Public Shared Function GetMultipleFilesBytes(files As String()) As Byte()()
        If IsNothing(files) OrElse files.Length = 0 Then Return Array.Empty(Of Byte())()

        Dim output As Byte()() = New Byte(files.Length - 1)() {}
        Dim looseIndexes As New Dictionary(Of Integer, File_Location)
        Dim packedGroups As New Dictionary(Of String, List(Of (OutputIndex As Integer, Location As File_Location)))(StringComparer.OrdinalIgnoreCase)

        For i As Integer = 0 To files.Length - 1
            Dim normalizedPath As String = files(i).Correct_Path_Separator
            Dim located_File As File_Location = Nothing

            If Dictionary.TryGetValue(normalizedPath, located_File) = False OrElse IsNothing(located_File) Then
                output(i) = Array.Empty(Of Byte)
                Continue For
            End If

            If located_File.IsLosseFile Then
                looseIndexes.Add(i, located_File)
            Else
                Dim group As List(Of (OutputIndex As Integer, Location As File_Location)) = Nothing
                If packedGroups.TryGetValue(located_File.BA2File, group) = False Then
                    group = New List(Of (OutputIndex As Integer, Location As File_Location))()
                    packedGroups.Add(located_File.BA2File, group)
                End If
                group.Add((i, located_File))
            End If
        Next

        Parallel.ForEach(looseIndexes.Keys, Sub(i As Integer)
                                                Dim located_File As File_Location = looseIndexes(i)
                                                If Not IsNothing(located_File) Then
                                                    output(i) = located_File.GetBytes()
                                                Else
                                                    output(i) = Array.Empty(Of Byte)
                                                End If
                                            End Sub)

        Parallel.ForEach(packedGroups, Sub(group)
                                           Dim archivePath = IO.Path.Combine(FO4Path, group.Key)

                                           Try
                                               Using fs As FileStream = File.OpenRead(archivePath)
                                                   Using pack As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                                                       For Each item In group.Value
                                                           output(item.OutputIndex) = item.Location.GetBytesFromOpenArchive(pack)
                                                       Next
                                                   End Using
                                               End Using
                                           Catch
                                               For Each item In group.Value
                                                   output(item.OutputIndex) = Array.Empty(Of Byte)
                                               Next
                                           End Try
                                       End Sub)

        Return output
    End Function

    Private Shared totalCount As Integer
    Private Shared completed As Integer

    Public Shared Property HighHeels_Plugin_Value As HighHeels_Plugins_values
        Get
            Return _HighHeels_Plugin_Values
        End Get
        Set(value As HighHeels_Plugins_values)
            _HighHeels_Plugin_Values = value
        End Set
    End Property
    Public Shared Property SliderPresets As SliderPresetCollection
        Get
            Return _sliderPresets
        End Get
        Set(value As SliderPresetCollection)
            _sliderPresets = value
        End Set
    End Property

    Public Shared Property FO4Path As String
        Get
            Return _fO4Path
        End Get
        Set(value As String)
            _fO4Path = value
        End Set
    End Property


    Public Shared Property Dictionary As ConcurrentDictionary(Of String, File_Location)
        Get
            Return _dictionary
        End Get
        Set(value As ConcurrentDictionary(Of String, File_Location))
            If IsNothing(value) Then
                _dictionary = New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
            Else
                _dictionary = value
            End If

            RebuildSearchIndexesFromDictionary()
        End Set
    End Property
    Private Shared Function NormalizeDictionaryKey(fullPath As String) As String
        If IsNothing(fullPath) Then Return ""
        Return fullPath.Correct_Path_Separator
    End Function

    Private Shared Function NormalizeDirectoryKey(directoryPath As String) As String
        If IsNothing(directoryPath) Then Return ""
        Dim normalized = directoryPath.Correct_Path_Separator.Trim()

        While normalized.EndsWith("\"c, StringComparison.Ordinal)
            normalized = normalized.Substring(0, normalized.Length - 1)
        End While

        Return normalized
    End Function

    Private Shared Function NormalizeRootPrefix(rootPrefix As String) As String
        Dim normalized = NormalizeDirectoryKey(rootPrefix)
        If String.IsNullOrEmpty(normalized) Then Return ""
        Return normalized & "\"
    End Function

    Private Shared Function NormalizeExtensionKey(extension As String) As String
        If String.IsNullOrWhiteSpace(extension) Then Return ""
        Dim ext = extension.Trim()
        If ext.StartsWith("."c) = False Then ext = "." & ext
        Return ext.ToLowerInvariant()
    End Function

    Private Shared Function BuildDirectoryExtensionBucketKey(directoryPath As String, extension As String) As String
        Return NormalizeDirectoryKey(directoryPath) & "|" & NormalizeExtensionKey(extension)
    End Function

    Private Shared Sub AddKeyToSearchIndex(index As ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte)), bucketKey As String, fullKey As String)
        Dim bucket = index.GetOrAdd(bucketKey, Function(key) New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase))
        bucket.TryAdd(fullKey, 0)
    End Sub

    Private Shared Sub IndexDictionaryKey(fullKey As String)
        fullKey = NormalizeDictionaryKey(fullKey)
        If String.IsNullOrEmpty(fullKey) Then Exit Sub

        Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(fullKey))
        Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(fullKey))

        AddKeyToSearchIndex(_KeysByDirectory, directoryKey, fullKey)

        If extensionKey <> "" Then
            AddKeyToSearchIndex(_KeysByExtension, extensionKey, fullKey)
            AddKeyToSearchIndex(_KeysByDirectoryExtension, BuildDirectoryExtensionBucketKey(directoryKey, extensionKey), fullKey)
        End If
    End Sub

    Private Shared Sub ClearSearchIndexes()
        _KeysByExtension.Clear()
        _KeysByDirectory.Clear()
        _KeysByDirectoryExtension.Clear()
    End Sub

    Private Shared Sub RebuildSearchIndexesFromDictionary()
        ClearSearchIndexes()

        For Each key In _dictionary.Keys
            IndexDictionaryKey(key)
        Next
    End Sub

    Public Shared Function TryAddDictionaryEntry(fullPath As String, location As File_Location) As Boolean
        Dim normalized = NormalizeDictionaryKey(fullPath)
        If _dictionary.TryAdd(normalized, location) Then
            IndexDictionaryKey(normalized)
            Return True
        End If
        Return False
    End Function

    Public Shared Function GetFilesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Dim directoryKey = NormalizeDirectoryKey(directoryPath)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then
            Dim directoryBucket As ConcurrentDictionary(Of String, Byte) = Nothing
            If _KeysByDirectory.TryGetValue(directoryKey, directoryBucket) Then
                For Each key In directoryBucket.Keys
                    results.Add(key)
                Next
            End If
        Else
            For Each ext In extensionSet
                Dim bucketKey = BuildDirectoryExtensionBucketKey(directoryKey, ext)
                Dim directoryExtBucket As ConcurrentDictionary(Of String, Byte) = Nothing

                If _KeysByDirectoryExtension.TryGetValue(bucketKey, directoryExtBucket) Then
                    For Each key In directoryExtBucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        End If

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    Public Shared Function GetFileNamesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As String()
        Return GetFilesInDirectory(directoryPath, allowedExtensions).
        Select(Function(k) IO.Path.GetFileName(k)).
        OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).
        ToArray()
    End Function

    Public Shared Function GetFilteredKeys(config As DictionaryFilePickerConfig) As List(Of String)
        ArgumentNullException.ThrowIfNull(config)
        Return GetFilteredKeys(config.RootPrefix, config.AllowedExtensions)
    End Function

    Public Shared Function GetFilteredKeys(rootPrefix As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Dim normalizedRoot = NormalizeRootPrefix(rootPrefix)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then Return New List(Of String)

        For Each ext In extensionSet
            Dim suffix = "|" & ext   ' ej: "|.dds"

            For Each bucketKey In _KeysByDirectoryExtension.Keys   ' recorre directorios, no archivos
                If Not bucketKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not DictionaryFilePickerConfig.PathStartsWithRoot(bucketKey, normalizedRoot) Then Continue For

                Dim bucket As ConcurrentDictionary(Of String, Byte) = Nothing
                If _KeysByDirectoryExtension.TryGetValue(bucketKey, bucket) Then
                    For Each key In bucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        Next

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String, progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer))) As Task
        Try
            FO4Path = Fo4DataPath
            Dictionary.Clear()
            ClearSearchIndexes()

            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim looseFiles = EnumerateSupportedLooseFiles(Fo4DataPath).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim archivePriority = BuildArchivePriority(ba2Files)

            totalCount = ba2Files.Count + looseFiles.Count
            completed = 0
            progress.Report(("Escaneando archivos...", completed, totalCount))

            Dim workQueue As New ConcurrentQueue(Of DictionaryScanWorkItem)

            For Each ba2 In ba2Files
                Dim ba2Name = Path.GetFileName(ba2)
                Dim sourceOrder As Integer = Integer.MinValue
                If archivePriority.TryGetValue(ba2Name, sourceOrder) = False Then
                    sourceOrder = Integer.MinValue
                End If

                workQueue.Enqueue(New DictionaryScanWorkItem With {
                .IsArchive = True,
                .FilePath = ba2,
                .SourceOrder = sourceOrder
            })
            Next

            For Each file In looseFiles
                workQueue.Enqueue(New DictionaryScanWorkItem With {
                .IsArchive = False,
                .FilePath = file,
                .SourceOrder = Integer.MaxValue
            })
            Next

            Dim workerCount As Integer = Math.Min(4, Math.Max(1, workQueue.Count))

            Dim workers = Enumerable.Range(0, workerCount).
            Select(Function(funza)
                       Return Task.Run(
                           Sub()
                               Dim item As DictionaryScanWorkItem = Nothing

                               While workQueue.TryDequeue(item)
                                   If item.IsArchive Then
                                       ProcessBa2File(item.FilePath, item.SourceOrder, progress)
                                   Else
                                       ProcessLooseFile(item.FilePath, Fo4DataPath, progress)
                                   End If
                               End While
                           End Sub)
                   End Function).
            ToArray()

            Await Task.WhenAll(workers).ConfigureAwait(False)

        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
    End Function
    Private Shared Function GetPluginsTxtPath() As String
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fallout4", "loadorder.txt")
        Else
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyrim Special Edition", "loadorder.txt")
        End If
    End Function

    Private Shared Function ReadPluginsLoadOrder() As List(Of String)
        Dim result As New List(Of String)
        Dim pluginsTxt = GetPluginsTxtPath()

        If File.Exists(pluginsTxt) = False Then Return result

        For Each rawLine In File.ReadLines(pluginsTxt, Encoding.UTF8)
            Dim line = rawLine.Trim()

            If line = "" Then Continue For
            If line.StartsWith("#", StringComparison.OrdinalIgnoreCase) Then Continue For
            If line.StartsWith(";", StringComparison.OrdinalIgnoreCase) Then Continue For

            If line.StartsWith("*", StringComparison.OrdinalIgnoreCase) Then
                line = line.Substring(1).Trim()
            End If

            If line = "" Then Continue For

            Dim ext = Path.GetExtension(line)
            If ext.Equals(".esp", StringComparison.OrdinalIgnoreCase) OrElse
           ext.Equals(".esm", StringComparison.OrdinalIgnoreCase) OrElse
           ext.Equals(".esl", StringComparison.OrdinalIgnoreCase) Then

                result.Add(Path.GetFileName(line))
            End If
        Next

        Return result
    End Function

    Private Shared Function ArchiveBelongsToPlugin(archiveFileName As String, pluginFileName As String) As Boolean
        Dim archiveBase = Path.GetFileNameWithoutExtension(archiveFileName)
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        If archiveBase.Equals(pluginBase, StringComparison.OrdinalIgnoreCase) Then Return True
        If archiveBase.StartsWith(pluginBase & " - ", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    Private Shared Function BuildArchivePriority(ba2Files As List(Of String)) As Dictionary(Of String, Integer)
        Dim result As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim archiveNames = ba2Files.
        Select(Function(p) Path.GetFileName(p)).
        OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).
        ToList()

        Dim fullPathsByName = ba2Files.
        GroupBy(Function(p) Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).
        ToDictionary(Function(g) g.Key, Function(g) g.First(), StringComparer.OrdinalIgnoreCase)

        Dim pending As New HashSet(Of String)(archiveNames, StringComparer.OrdinalIgnoreCase)
        Dim nextOrder As Integer = 0

        Dim baseAndDlcOrder As String() = {
        "Fallout4",
        "DLCRobot",
        "DLCworkshop01",
        "DLCCoast",
        "DLCworkshop02",
        "DLCworkshop03",
        "DLCNukaWorld",
        "DLCUltraHighResolution"
    }

        For Each prefix In baseAndDlcOrder
            Dim matches = pending.
            Where(Function(name) Path.GetFileNameWithoutExtension(name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            For Each match In matches
                result(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        Dim pluginsLoadOrder = ReadPluginsLoadOrder()

        For Each plugin In pluginsLoadOrder
            Dim matches = pending.
            Where(Function(name) ArchiveBelongsToPlugin(name, plugin)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            For Each match In matches
                result(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        Dim fallbackMatches = pending.
        OrderBy(Function(name) File.GetLastWriteTimeUtc(fullPathsByName(name))).
        ThenBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
        ToList()

        For Each match In fallbackMatches
            result(match) = nextOrder
            nextOrder += 1
            pending.Remove(match)
        Next

        Return result
    End Function
    Private Shared Sub ProcessBa2File(ba2 As String, sourceOrder As Integer, progress As IProgress(Of (String, Integer, Integer)))
        Try
            Using fs As FileStream = File.OpenRead(ba2)
                Using arc As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    For Each fil In arc.EntriesFiles
                        Dim standardized = fil.FullPath.Correct_Path_Separator
                        Dim entry As New File_Location With {
                        .BA2File = Path.GetFileName(ba2),
                        .Index = fil.Index,
                        .FullPath = standardized,
                        .SourceOrder = sourceOrder
                    }

                        Dictionary.AddOrUpdate(
                        standardized,
                        entry,
                        Function(key, existing)
                            If Resolve_Conflict(existing, entry) Then
                                Return entry
                            Else
                                Return existing
                            End If
                        End Function)

                        IndexDictionaryKey(standardized)
                    Next
                End Using
            End Using

        Catch ex As Exception
            MsgBox("Error procesing Ba2 " + ba2 + " :" + ex.ToString)
        Finally
            Dim current = Interlocked.Increment(completed)
            progress.Report(($"Procesado: {Path.GetFileName(ba2)}", current, totalCount))
        End Try
    End Sub

    Public Shared Function EnumerateFilesWithSymlinkSupport(root As String, pattern As String, Recursive As Boolean) As IEnumerable(Of String)
        Dim spl() As String = {pattern}
        If pattern.Contains(";"c) Then
            spl = pattern.Split(";"c)
        End If
        Dim result As IEnumerable(Of String) = Enumerable.Empty(Of String)()
        Dim opts As New EnumerationOptions() With {.RecurseSubdirectories = Recursive}

        For Each pat In spl
            result = result.Concat(Directory.EnumerateFiles(root, pat, opts))
        Next
        Return result
    End Function

    Private Shared Sub ProcessLooseFile(file As String, basePath As String, progress As IProgress(Of (String, Integer, Integer)))
        Try
            Dim standardized = Path.GetRelativePath(basePath, file).Correct_Path_Separator

            Dim entry As New File_Location With {
            .BA2File = String.Empty,
            .Index = -1,
            .FullPath = standardized,
            .SourceOrder = Integer.MaxValue
        }

            Dictionary.AddOrUpdate(
            standardized,
            entry,
            Function(key, existing)
                If Resolve_Conflict(existing, entry) Then
                    Return entry
                Else
                    Return existing
                End If
            End Function)

            IndexDictionaryKey(standardized)

        Catch ex As Exception
            MsgBox("Error procesing Loose file " + file + " :" + ex.ToString)
        Finally
            Dim current = Interlocked.Increment(completed)
            progress.Report(($"Procesado: {Path.GetFileName(file)}", current, totalCount))
        End Try
    End Sub

    Private Shared Function Resolve_Conflict(Original As File_Location, Nueva As File_Location) As Boolean
        If IsNothing(Original) Then Return True
        If IsNothing(Nueva) Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile = False Then Return True
        If Original.IsLosseFile AndAlso Nueva.IsLosseFile = False Then Return False

        If Nueva.SourceOrder > Original.SourceOrder Then Return True
        If Nueva.SourceOrder < Original.SourceOrder Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile Then
            Return False
        End If

        Return StringComparer.OrdinalIgnoreCase.Compare(Nueva.BA2File, Original.BA2File) >= 0
    End Function

End Class
