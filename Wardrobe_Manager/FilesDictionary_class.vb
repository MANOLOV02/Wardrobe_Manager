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
                        Using pack As New UnifiedBethesdaArchive(fs)
                            Return pack.ExtractToMemory(Index)
                        End Using
                    End Using
                Catch ex As Exception
                    Return Array.Empty(Of Byte)
                End Try

                'If IO.Path.GetExtension(BA2File).Contains("ba2", StringComparison.OrdinalIgnoreCase) Then
                '    Dim pack As New SharpBSABA2.BA2Util.BA2(IO.Path.Combine(FO4Path, Me.BA2File))
                '    Dim _Uncompressed2 = pack.Files(Index).GetDataStream.ToArray
                '    pack.Close()
                '    Return _Uncompressed2
                'Else
                '    Dim pack As New SharpBSABA2.BSAUtil.BSA(IO.Path.Combine(FO4Path, Me.BA2File))
                '    Dim _Uncompressed2 = pack.Files(Index).GetDataStream.ToArray
                '    pack.Close()
                '    Return _Uncompressed2
                'End If
            End If
        End Function

    End Class
    Private Shared _sliderPresets As New SliderPresetCollection
    Private Shared _fO4Path As String = ""
    Private Shared _dictionary As New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly Extensiones As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds", ".bgsm", ".bgem", ".nif", ".tri"}



    Public Shared Function GetBytes(File As String) As Byte()
        Dim located_File As File_Location = Nothing
        If Not Dictionary.TryGetValue(File.Correct_Path_Separator, located_File) Then
            Return Array.Empty(Of Byte)
        Else
            Return located_File.GetBytes
        End If
    End Function

    Public Shared Function GetMultipleFilesBytes(files As String()) As Byte()()
        Dim output As Byte()() = New Byte(files.Length - 1)() {}

        Parallel.For(0, files.Length, Sub(i As Int32)
                                          Dim file As String = files(i)
                                          Dim normalizedPath As String = file.Correct_Path_Separator
                                          Dim located_File As File_Location = Nothing
                                          If Dictionary.TryGetValue(normalizedPath, located_File) = False Then
                                              output(i) = Array.Empty(Of Byte)
                                          Else
                                              output(i) = located_File.GetBytes()
                                          End If

                                      End Sub)
        Return output
    End Function


    Private Shared totalCount As Integer
    Private Shared completed As Integer

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
            _dictionary = value
        End Set
    End Property

    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String, progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer))) As Task
        Try
            FO4Path = Fo4DataPath
            Dictionary.Clear()
            ' Obtener archivos
            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).ToList()
            Dim looseFiles = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*", True).ToList()
            ' Total para progreso
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
                Using arc As New UnifiedBethesdaArchive(fs)
                    For Each fil In arc.EntriesFiles
                        Dim standardized = fil.FullPath.Correct_Path_Separator
                        Dim entry As New File_Location With {.BA2File = Path.GetFileName(ba2), .Index = fil.Index, .FullPath = standardized}
                        Dictionary.AddOrUpdate(standardized, entry, Function(key, existing)
                                                                        If Resolve_Conflict(existing, entry) Then Return entry Else Return existing
                                                                    End Function)
                    Next
                End Using
            End Using
            'If IO.Path.GetExtension(ba2).Contains("ba2", StringComparison.OrdinalIgnoreCase) Then
            '    'Dim pack As New SharpBSABA2.BA2Util.BA2(ba2)
            '    'For Each fil In pack.Files.Where(Function(pf) Extensiones.Contains(Path.GetExtension(pf.FileName)))

            '    '    Dim standardized = fil.FullPath.Correct_Path_Separator
            '    '    Dim entry As New File_Location With {.BA2File = Path.GetFileName(ba2), .Index = fil.Index, .FullPath = standardized}
            '    '    Dictionary.AddOrUpdate(standardized, entry, Function(key, existing)
            '    '                                                    If Resolve_Conflict(existing, entry) Then Return entry Else Return existing
            '    '                                                End Function)
            '    'Next
            '    'pack.Close()
            'Else
            '    Dim pack As New SharpBSABA2.BSAUtil.BSA(ba2)
            '    For Each fil In pack.Files.Where(Function(pf) Extensiones.Contains(Path.GetExtension(pf.FileName)))
            '        Dim standardized = fil.FullPath.Correct_Path_Separator
            '        Dim idx = fil.Index
            '        Dim entry As New File_Location With {.BA2File = Path.GetFileName(ba2), .Index = idx, .FullPath = standardized}
            '        Dictionary.AddOrUpdate(standardized, entry, Function(key, existing)
            '                                                        If Resolve_Conflict(existing, entry) Then Return entry Else Return existing
            '                                                    End Function)
            '    Next
            '    pack.Close()
            'End If

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
            Dim ext = Path.GetExtension(file)
            If Extensiones.Contains(ext) Then
                Dim standardized = Path.GetRelativePath(basePath, file).Correct_Path_Separator
                Dim entry As New File_Location With {.BA2File = String.Empty, .Index = -1, .FullPath = standardized}
                Dictionary.AddOrUpdate(standardized, entry, Function(key, existing) entry)
            End If
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
        If Original.BA2File.Contains(".bsa") = False Then
            Debugger.Break()
        End If
        Return True
    End Function

End Class
