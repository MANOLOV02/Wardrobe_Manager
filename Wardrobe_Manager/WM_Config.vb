' Version Uploaded of Wardrobe 3.2.0
Imports System.ComponentModel
Imports System.IO
Imports System.Text.Json
Imports FO4_Base_Library

''' <summary>
''' WM-specific configuration: BodySlide paths, build settings, UI preferences.
''' Persists to its own wm_config.json, separate from library's config.json.
''' </summary>
Public Class WM_Config

    ' ── Singleton ──
    Public Shared Property Current As New WM_Config()

    ' ── Enums ──

    Public Enum SliderSize
        [Default] = 0
        Big = 1
        Small = 2
    End Enum

    ' ── Structs ──

    Public Structure BuildSettings
        Public Property OwnEngine As Boolean
        Public Property SaveTri As Boolean
        Public Property SaveHHS As Boolean
        Public Property DeleteUnbuilt As Boolean
        Public Property DeleteWithProject As Boolean
        Public Property AddAddintionalSliders As Boolean
        Public Property SkipFixMorphs As Boolean
        Public Property ResetSlidersEachBuild As Boolean
        Public Property IgnorePreventri As Boolean
        Public Property BuildInPose As Boolean
        Public Property IgnoreWeightsFlags As Boolean
        Public Property ForceWeights As Boolean
    End Structure

    ' ── Properties (moved from Config_App) ──

    Public Property BSExePath As String = ""
    Public Property OSExePath As String = ""
    Public Property Bodytipe As SliderSize = SliderSize.Default
    Public Property Default_Preset As String = ""
    Public Property Settings_Build As BuildSettings = Default_Build_Settings()
    Public Property Setting_Fix_Uncloned As Boolean = True

    Public Property Setting_OverWrite As Boolean = False
    Public Property Setting_ChangeOutDir As Boolean = False
    Public Property Setting_Automove As Boolean = False
    Public Property Setting_ExcludeReference As Boolean = False
    Public Property Setting_Clone_Materials As Boolean = True
    Public Property Setting_Showpacks As Boolean = False
    Public Property Setting_ShowCBBE As Boolean = True
    Public Property Setting_ShowCollections As Boolean = True
    Public Property Setting_ExportSam As Boolean = False

    ' ── BSA/BA2 file lists and cloning permissions ──

    Public Property BSAFiles_FO4 As New List(Of String)
    Public Property BSAFiles_Clonables_FO4 As New List(Of Boolean)
    Public Property BSAFiles_SSE As New List(Of String)
    Public Property BSAFiles_Clonables_SSE As New List(Of Boolean)

    ' BA2 header version used when packing FO4 archives (per-app setting). See Ba2VersionUI for the values.
    Public Property Ba2Version_FO4 As UInteger = Ba2VersionUI.NextGen

    <System.Text.Json.Serialization.JsonIgnore>
    Public ReadOnly Property BSAFiles As List(Of String)
        Get
            Return If(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, BSAFiles_FO4, BSAFiles_SSE)
        End Get
    End Property

    <System.Text.Json.Serialization.JsonIgnore>
    Public ReadOnly Property BSAFiles_Clonables As List(Of Boolean)
        Get
            Return If(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, BSAFiles_Clonables_FO4, BSAFiles_Clonables_SSE)
        End Get
    End Property

    Public Shared Function Allowed_To_Clone(Ba2File As String) As Boolean
        Dim idx = Current.BSAFiles.FindIndex(Function(s) String.Equals(s, IO.Path.GetFileName(Ba2File), StringComparison.OrdinalIgnoreCase))
        If idx = -1 Then Return False
        ' Defense in depth: BSAFiles and BSAFiles_Clonables are normalized to equal length on load,
        ' but guard the paired index anyway so a desync can never throw an out-of-range exception.
        Dim clonables = Current.BSAFiles_Clonables
        If idx < 0 OrElse idx >= clonables.Count Then Return False
        Return clonables(idx)
    End Function

    ' ── Defaults ──

    Public Shared Function Default_Build_Settings() As BuildSettings
        Return New BuildSettings With {
            .DeleteUnbuilt = True, .DeleteWithProject = True, .SaveHHS = True,
            .SaveTri = False, .OwnEngine = True, .AddAddintionalSliders = True,
            .ResetSlidersEachBuild = False, .SkipFixMorphs = True,
            .IgnorePreventri = False, .BuildInPose = False,
            .ForceWeights = True, .IgnoreWeightsFlags = False
        }
    End Function

    ' ── Computed paths ──

    Public Shared ReadOnly Property BsPath As String
        Get
            If Not Check_BSFolder() Then Return ""
            Return Path.GetDirectoryName(Current.BSExePath)
        End Get
    End Property

    ' ── Validation ──

    Public Shared Function Check_BSFolder() As Boolean
        If Not File.Exists(Current.BSExePath) Then Return False
        If Not Directory.Exists(Path.Combine(Path.GetDirectoryName(Current.BSExePath), "Slidersets")) Then Return False
        Return True
    End Function

    Public Shared Function Check_OsFolder() As Boolean
        If Not File.Exists(Current.OSExePath) Then Return False
        If Not Directory.Exists(Path.Combine(Path.GetDirectoryName(Current.OSExePath), "Shapedata")) Then Return False
        Return True
    End Function

    Private Shared _osSliderMin As Integer? = Nothing
    Private Shared _osSliderMax As Integer? = Nothing

    ''' <summary>Reads SliderMinimum/SliderMaximum from OutfitStudio Config.xml. Cached. Fallback 0/100.</summary>
    Public Shared Function GetOsSliderRange() As (Min As Integer, Max As Integer)
        If _osSliderMin.HasValue AndAlso _osSliderMax.HasValue Then
            Return (_osSliderMin.Value, _osSliderMax.Value)
        End If

        Dim minVal As Integer = 0
        Dim maxVal As Integer = 100
        Try
            Dim osDir As String = Path.GetDirectoryName(Current.OSExePath)
            If Not String.IsNullOrEmpty(osDir) Then
                Dim configPath As String = Path.Combine(osDir, "Config.xml")
                If File.Exists(configPath) Then
                    Dim doc = Xml.Linq.XDocument.Load(configPath)
                    Dim input = doc.Root?.Element("Input")
                    If input IsNot Nothing Then
                        Dim minTxt = input.Element("SliderMinimum")?.Value
                        Dim maxTxt = input.Element("SliderMaximum")?.Value
                        Integer.TryParse(minTxt, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, minVal)
                        Integer.TryParse(maxTxt, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, maxVal)
                    End If
                End If
            End If
        Catch
            ' fallback to defaults
            minVal = 0
            maxVal = 100
        End Try

        If maxVal <= minVal Then
            minVal = 0
            maxVal = 100
        End If

        _osSliderMin = minVal
        _osSliderMax = maxVal
        Return (minVal, maxVal)
    End Function

    Public Shared Function Check_All_Folder() As Boolean
        Return Config_App.Check_FOFolder() AndAlso Check_BSFolder() AndAlso Check_OsFolder()
    End Function

    ''' <summary>Auto-detect BodySlide/OutfitStudio paths from game folder. Call after LoadConfig.</summary>
    Public Shared Sub AutoDetectBSPaths()
        If Current.BSExePath <> "" Then Return
        Dim gamePath = Config_App.Current.FO4ExePath
        If gamePath = "" OrElse Not File.Exists(gamePath) Then Return
        Dim gameDir = Path.GetDirectoryName(gamePath)
        Try
            Dim bsDir = Path.Combine(gameDir, "Data\Tools\Bodyslide")
            Current.BSExePath = ResolveBsSuiteExePath(bsDir, "BodySlide")
            Current.OSExePath = ResolveBsSuiteExePath(bsDir, "OutfitStudio")
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Resolve a BodySlide-suite executable path. The suite historically shipped a 32-bit
    ''' "name.exe" plus a 64-bit "name x64.exe"; Ousnius later unified to a single
    ''' suffix-less "name.exe". On a 64-bit OS prefer the legacy " x64.exe" when it exists,
    ''' otherwise fall back to the unified suffix-less name. On a 32-bit OS use "name.exe".
    ''' </summary>
    Public Shared Function ResolveBsSuiteExePath(bsDir As String, baseName As String) As String
        Dim plain = Path.Combine(bsDir, baseName & ".exe")
        If Environment.Is64BitOperatingSystem Then
            Dim suffixed = Path.Combine(bsDir, baseName & " x64.exe")
            If File.Exists(suffixed) Then Return suffixed
        End If
        Return plain
    End Function

    ' ── Persistence ──

    Private Shared ReadOnly ConfigFilePath As String = Path.Combine(Application.StartupPath, "wm_config.json")

    Public Shared Sub SaveConfig()
        JsonConfigIO.Save(Current, ConfigFilePath, "WM configuration")
    End Sub

    Public Shared Sub LoadConfig()
        Dim cfg = JsonConfigIO.Load(Of WM_Config)(ConfigFilePath, "WM configuration")
        If cfg IsNot Nothing Then
            Current = cfg
        ElseIf Not File.Exists(ConfigFilePath) Then
            ' First run after migration: try to pull WM properties from legacy config.json
            MigrateFromLegacyConfig()
        End If

        ' The file-name list and its parallel clonable-flag list are (de)serialized independently,
        ' so a hand-edited or partially-written config can leave them at different lengths. Every
        ' consumer assumes index-parallel access (BSAFiles(i) ⇄ BSAFiles_Clonables(i)), so restore
        ' that invariant here: pad missing flags with False, drop any extras.
        NormalizeClonableLists()
    End Sub

    ''' <summary>
    ''' Forces BSAFiles_Clonables_* to be index-parallel with their BSAFiles_* counterpart:
    ''' pads short flag lists with False and truncates long ones. Keeps the documented invariant
    ''' (BSAFiles(i) corresponds to BSAFiles_Clonables(i)) true for every consumer.
    ''' </summary>
    Private Shared Sub NormalizeClonableLists()
        If Current Is Nothing Then Return
        NormalizeClonablePair(Current.BSAFiles_FO4, Current.BSAFiles_Clonables_FO4)
        NormalizeClonablePair(Current.BSAFiles_SSE, Current.BSAFiles_Clonables_SSE)
    End Sub

    Private Shared Sub NormalizeClonablePair(files As List(Of String), clonables As List(Of Boolean))
        If files Is Nothing OrElse clonables Is Nothing Then Return
        While clonables.Count < files.Count
            clonables.Add(False)
        End While
        If clonables.Count > files.Count Then
            clonables.RemoveRange(files.Count, clonables.Count - files.Count)
        End If
    End Sub

    ''' <summary>
    ''' One-time migration: reads the old config.json and extracts WM-only properties.
    ''' System.Text.Json ignores unknown properties by default, so reading legacy format
    ''' into a temporary helper class pulls just the WM fields.
    ''' </summary>
    Private Shared Sub MigrateFromLegacyConfig()
        Try
            Dim legacyPath = Path.Combine(Application.StartupPath, "config.json")
            If Not File.Exists(legacyPath) Then Return

            Dim jsonString = File.ReadAllText(legacyPath)
            Dim doc = JsonDocument.Parse(jsonString, New JsonDocumentOptions With {.CommentHandling = JsonCommentHandling.Skip, .AllowTrailingCommas = True})
            Dim root = doc.RootElement

            ' Extract WM properties from legacy JSON (if they exist)
            Dim strVal As String = ""
            If JsonConfigIO.TryGetString(root, "BSExePath", strVal) Then Current.BSExePath = strVal
            If JsonConfigIO.TryGetString(root, "OSExePath", strVal) Then Current.OSExePath = strVal
            If JsonConfigIO.TryGetString(root, "Default_Preset", strVal) Then Current.Default_Preset = strVal

            Dim boolVal As Boolean
            If JsonConfigIO.TryGetBool(root, "Setting_OverWrite", boolVal) Then Current.Setting_OverWrite = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_ChangeOutDir", boolVal) Then Current.Setting_ChangeOutDir = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_Automove", boolVal) Then Current.Setting_Automove = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_ExcludeReference", boolVal) Then Current.Setting_ExcludeReference = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_Clone_Materials", boolVal) Then Current.Setting_Clone_Materials = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_Showpacks", boolVal) Then Current.Setting_Showpacks = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_ShowCBBE", boolVal) Then Current.Setting_ShowCBBE = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_ShowCollections", boolVal) Then Current.Setting_ShowCollections = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_ExportSam", boolVal) Then Current.Setting_ExportSam = boolVal
            If JsonConfigIO.TryGetBool(root, "Setting_Fix_Uncloned", boolVal) Then Current.Setting_Fix_Uncloned = boolVal

            Dim intVal As Integer
            If JsonConfigIO.TryGetInt(root, "Bodytipe", intVal) Then Current.Bodytipe = CType(intVal, SliderSize)

            ' Migrate BuildSettings
            Dim buildEl As JsonElement
            If root.TryGetProperty("Settings_Build", buildEl) Then
                Try
                    Dim bs = JsonSerializer.Deserialize(Of BuildSettings)(buildEl.GetRawText())
                    Current.Settings_Build = bs
                Catch
                End Try
            End If

            ' Migrate BSA file lists
            Dim arrEl As JsonElement
            If root.TryGetProperty("BSAFiles_FO4", arrEl) Then
                Try : Current.BSAFiles_FO4 = JsonSerializer.Deserialize(Of List(Of String))(arrEl.GetRawText()) : Catch : End Try
            End If
            If root.TryGetProperty("BSAFiles_Clonables_FO4", arrEl) Then
                Try : Current.BSAFiles_Clonables_FO4 = JsonSerializer.Deserialize(Of List(Of Boolean))(arrEl.GetRawText()) : Catch : End Try
            End If
            If root.TryGetProperty("BSAFiles_SSE", arrEl) Then
                Try : Current.BSAFiles_SSE = JsonSerializer.Deserialize(Of List(Of String))(arrEl.GetRawText()) : Catch : End Try
            End If
            If root.TryGetProperty("BSAFiles_Clonables_SSE", arrEl) Then
                Try : Current.BSAFiles_Clonables_SSE = JsonSerializer.Deserialize(Of List(Of Boolean))(arrEl.GetRawText()) : Catch : End Try
            End If

            ' The four BSA lists above are deserialized under independent Try/Catch blocks, so a
            ' partial legacy file can leave files/flags at mismatched lengths. Restore the parallel
            ' invariant before persisting the migrated config.
            NormalizeClonableLists()

            ' Save the migrated config
            SaveConfig()
            doc.Dispose()

        Catch
            ' Migration is best-effort — defaults are fine
        End Try
    End Sub

End Class
