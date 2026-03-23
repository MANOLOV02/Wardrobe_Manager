' Version Uploaded of Wardrobe 2.1.3
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports NiflySharp.Enums

Module Extensiones
    <Extension>
    Public Function Correct_Path_Separator(St As String) As String
        If IsNothing(St) Then Return ""
        Return St.Replace("/", "\")
    End Function
End Module
Public Class FilesDictionary_class
    Public Shared Property TexturesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Textures\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}}
    Public Shared Property MaterialsDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Materials\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm", ".bgem"}}
    Public Shared Property MaterialsDictionary_BGEM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Materials\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgem"}}
    Public Shared Property MaterialsDictionary_BGSM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Materials\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm"}}
    Public Shared Property MeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Meshes\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Shared Property ALLMeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Class DictionaryFilePickerConfig
        ' Debe apuntar a tu ConcurrentDictionary(Of String, File_Location)
        Public Property DictionaryProvider As Func(Of ConcurrentDictionary(Of String, FilesDictionary_class.File_Location))

        ' Prefijo raíz (case-insensitive). Default: "Textures\"
        Public Property RootPrefix As String = "Textures\"

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


    Public Class File_Location

        Public Property BA2File As String = ""
        Public Property Index As Integer = -1
        Public Property FullPath As String = ""
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
        Dim opts As New EnumerationOptions() With {.RecurseSubdirectories = True}
        Dim patterns As String() = {"*.dds", "*.bgsm", "*.bgem", "*.nif", "*.tri"}

        Dim result As IEnumerable(Of String) = Enumerable.Empty(Of String)()
        For Each pat In patterns
            result = result.Concat(Directory.EnumerateFiles(root, pat, opts))
        Next

        Return result
    End Function
    Public Shared Function GetMultipleFilesBytes(files As String()) As Byte()()
        If IsNothing(files) OrElse files.Length = 0 Then Return Array.Empty(Of Byte())()

        Dim output As Byte()() = New Byte(files.Length - 1)() {}
        Dim looseIndexes As New List(Of Integer)
        Dim packedGroups As New Dictionary(Of String, List(Of (OutputIndex As Integer, Location As File_Location)))(StringComparer.OrdinalIgnoreCase)

        For i As Integer = 0 To files.Length - 1
            Dim normalizedPath As String = files(i).Correct_Path_Separator
            Dim located_File As File_Location = Nothing

            If Dictionary.TryGetValue(normalizedPath, located_File) = False OrElse IsNothing(located_File) Then
                output(i) = Array.Empty(Of Byte)
                Continue For
            End If

            If located_File.IsLosseFile Then
                looseIndexes.Add(i)
            Else
                Dim group As List(Of (OutputIndex As Integer, Location As File_Location)) = Nothing
                If packedGroups.TryGetValue(located_File.BA2File, group) = False Then
                    group = New List(Of (OutputIndex As Integer, Location As File_Location))()
                    packedGroups.Add(located_File.BA2File, group)
                End If
                group.Add((i, located_File))
            End If
        Next

        Parallel.ForEach(looseIndexes, Sub(i As Integer)
                                           Dim located_File As File_Location = Nothing
                                           If Dictionary.TryGetValue(files(i).Correct_Path_Separator, located_File) AndAlso Not IsNothing(located_File) Then
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
            Dim extBucket As ConcurrentDictionary(Of String, Byte) = Nothing
            If _KeysByExtension.TryGetValue(ext, extBucket) Then
                For Each key In extBucket.Keys
                    If DictionaryFilePickerConfig.PathStartsWithRoot(key, normalizedRoot) Then
                        results.Add(key)
                    End If
                Next
            End If
        Next

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function
    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String, progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer))) As Task
        Try
            FO4Path = Fo4DataPath
            Dictionary.Clear()
            ClearSearchIndexes()
            ' Obtener archivos
            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).ToList()
            Dim looseFiles = EnumerateSupportedLooseFiles(Fo4DataPath).ToList()

            totalCount = ba2Files.Count + looseFiles.Count
            completed = 0
            progress.Report(("Escaneando archivos...", completed, totalCount))

            ' Limitar el número de tareas concurrentes
            Dim semaphore = New System.Threading.SemaphoreSlim(4) ' Ajusta según CPU/hardware

            ' Procesar archivos .ba2
            Dim ba2Tasks = ba2Files.Select(Function(ba2)
                                               Return ProcessBa2FileAsync(ba2, progress, semaphore)
                                           End Function)
            ' Procesar archivos sueldots
            Dim looseTasks = looseFiles.Select(Function(file)
                                                   Return ProcessLooseFileAsync(file, Fo4DataPath, progress, semaphore)
                                               End Function)
            ' Esperar a que todas las tareas terminen
            Dim allTasks = ba2Tasks.Concat(looseTasks).ToList()
            Await Task.WhenAll(allTasks).ConfigureAwait(False)
        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
    End Function

    Private Shared Async Function ProcessBa2FileAsync(ba2 As String, progress As IProgress(Of (String, Integer, Integer)), semaphore As SemaphoreSlim) As Task
        Await semaphore.WaitAsync().ConfigureAwait(False)
        Try
            Using fs As FileStream = File.OpenRead(ba2) ' o .bsa
                Dim i As Integer = 0
                Using arc As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    For Each fil In arc.EntriesFiles
                        Dim standardized = fil.FullPath.Correct_Path_Separator
                        Dim entry As New File_Location With {.BA2File = Path.GetFileName(ba2), .Index = fil.Index, .FullPath = standardized}
                        Dictionary.AddOrUpdate(standardized, entry, Function(key, existing)
                                                                        If Resolve_Conflict(existing, entry) Then Return entry Else Return existing
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
            semaphore.Release()
        End Try

    End Function

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

    Private Shared Async Function ProcessLooseFileAsync(file As String, basePath As String, progress As IProgress(Of (String, Integer, Integer)), semaphore As SemaphoreSlim) As Task
        Await semaphore.WaitAsync().ConfigureAwait(False)
        Try
            Dim standardized = Path.GetRelativePath(basePath, file).Correct_Path_Separator
            Dim entry As New File_Location With {.BA2File = String.Empty, .Index = -1, .FullPath = standardized}
            Dictionary.AddOrUpdate(standardized, entry, Function(key, existing) entry)
            IndexDictionaryKey(standardized)
        Catch ex As Exception
            MsgBox("Error procesing Loose file " + file + " :" + ex.ToString)
        Finally
            Dim current = Interlocked.Increment(completed)
            progress.Report(($"Procesado: {Path.GetFileName(file)}", current, totalCount))
            semaphore.Release()
        End Try
    End Function

    Private Shared Function Resolve_Conflict(Original As File_Location, Nueva As File_Location) As Boolean
        If Nueva.BA2File.StartsWith("DLCUltraHighResolution", StringComparison.OrdinalIgnoreCase) Then Return True
        If Original.BA2File.StartsWith("DLCUltraHighResolution", StringComparison.OrdinalIgnoreCase) Then Return False
        If Nueva.BA2File.StartsWith("Fallout4", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("Fallout4", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("unofficial fallout 4 patch", StringComparison.OrdinalIgnoreCase) Then Return True
        If Original.BA2File.StartsWith("unofficial fallout 4 patch", StringComparison.OrdinalIgnoreCase) Then Return False
        If Nueva.BA2File.StartsWith("Scrap Everything", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("Scrap Everything", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) And Original.BA2File.StartsWith("cc", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("cc", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) And Original.BA2File.StartsWith("DLCNUKA", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLCNUKA", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("DLCROBOT", StringComparison.OrdinalIgnoreCase) And Original.BA2File.StartsWith("DLCNUKA", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("DLCROBOT", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLCNUKA", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("DLCROBOT", StringComparison.OrdinalIgnoreCase) And Original.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("DLCROBOT", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) Then Return True
        If Nueva.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) And Original.BA2File.StartsWith("DLCNUKA", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("DLCworkshop03", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) Then Return True
        If Original.BA2File.StartsWith("DLCCOAST", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLCworkshop03", StringComparison.OrdinalIgnoreCase) Then Return False

        If Original.BA2File.StartsWith("ccBGSFO4038", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("ccBGSFO4044", StringComparison.OrdinalIgnoreCase) Then Return False
        If Original.BA2File.StartsWith("ccBGSFO4044", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("ccBGSFO4038", StringComparison.OrdinalIgnoreCase) Then Return True
        If Original.BA2File.StartsWith("Alternative Satellite World Maps - Textures", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) Then Return True
        If Original.BA2File.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) And Nueva.BA2File.StartsWith("Alternative Satellite World Maps - Textures", StringComparison.OrdinalIgnoreCase) Then Return False

        If Original.BA2File.Contains(".bsa") = False Then
            Debugger.Break()
        End If
        Return True
    End Function

End Class
