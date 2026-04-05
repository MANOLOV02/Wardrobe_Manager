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
        Return Current.BSAFiles_Clonables(idx)
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
            If Environment.Is64BitOperatingSystem Then
                Current.BSExePath = Path.Combine(gameDir, "Data\Tools\Bodyslide\BodySlide x64.exe")
                Current.OSExePath = Path.Combine(gameDir, "Data\Tools\Bodyslide\OutfitStudio x64.exe")
            Else
                Current.BSExePath = Path.Combine(gameDir, "Data\Tools\Bodyslide\BodySlide.exe")
                Current.OSExePath = Path.Combine(gameDir, "Data\Tools\Bodyslide\OutfitStudio.exe")
            End If
        Catch
        End Try
    End Sub

    ' ── Persistence ──

    Private Shared ReadOnly ConfigFilePath As String = Path.Combine(Application.StartupPath, "wm_config.json")
    Private Shared ReadOnly SaveOptions As New JsonSerializerOptions With {.WriteIndented = True}

    Public Shared Sub SaveConfig()
        Try
            Dim jsonString As String = JsonSerializer.Serialize(Current, SaveOptions)
            File.WriteAllText(ConfigFilePath, jsonString)
        Catch ex As Exception
            MessageBox.Show("Error saving WM configuration: " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Public Shared Sub LoadConfig()
        Try
            If File.Exists(ConfigFilePath) Then
                Dim jsonString As String = File.ReadAllText(ConfigFilePath)
                Dim cfg = JsonSerializer.Deserialize(Of WM_Config)(jsonString)
                If cfg IsNot Nothing Then Current = cfg
            Else
                ' First run after migration: try to pull WM properties from legacy config.json
                MigrateFromLegacyConfig()
            End If
        Catch ex As Exception
            MessageBox.Show("Error loading WM configuration: " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
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
            Dim doc = JsonDocument.Parse(jsonString)
            Dim root = doc.RootElement

            ' Extract WM properties from legacy JSON (if they exist)
            Dim strVal As String = ""
            If TryGetString(root, "BSExePath", strVal) Then Current.BSExePath = strVal
            If TryGetString(root, "OSExePath", strVal) Then Current.OSExePath = strVal
            If TryGetString(root, "Default_Preset", strVal) Then Current.Default_Preset = strVal

            Dim boolVal As Boolean
            If TryGetBool(root, "Setting_OverWrite", boolVal) Then Current.Setting_OverWrite = boolVal
            If TryGetBool(root, "Setting_ChangeOutDir", boolVal) Then Current.Setting_ChangeOutDir = boolVal
            If TryGetBool(root, "Setting_Automove", boolVal) Then Current.Setting_Automove = boolVal
            If TryGetBool(root, "Setting_ExcludeReference", boolVal) Then Current.Setting_ExcludeReference = boolVal
            If TryGetBool(root, "Setting_Clone_Materials", boolVal) Then Current.Setting_Clone_Materials = boolVal
            If TryGetBool(root, "Setting_Showpacks", boolVal) Then Current.Setting_Showpacks = boolVal
            If TryGetBool(root, "Setting_ShowCBBE", boolVal) Then Current.Setting_ShowCBBE = boolVal
            If TryGetBool(root, "Setting_ShowCollections", boolVal) Then Current.Setting_ShowCollections = boolVal
            If TryGetBool(root, "Setting_ExportSam", boolVal) Then Current.Setting_ExportSam = boolVal

            Dim intVal As Integer
            If TryGetInt(root, "Bodytipe", intVal) Then Current.Bodytipe = CType(intVal, SliderSize)

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

            ' Save the migrated config
            SaveConfig()
            doc.Dispose()

        Catch
            ' Migration is best-effort — defaults are fine
        End Try
    End Sub

    Private Shared Function TryGetString(root As JsonElement, name As String, ByRef value As String) As Boolean
        Dim el As JsonElement
        If root.TryGetProperty(name, el) AndAlso el.ValueKind = JsonValueKind.String Then
            value = el.GetString()
            Return True
        End If
        Return False
    End Function

    Private Shared Function TryGetBool(root As JsonElement, name As String, ByRef value As Boolean) As Boolean
        Dim el As JsonElement
        If root.TryGetProperty(name, el) Then
            If el.ValueKind = JsonValueKind.True Then value = True : Return True
            If el.ValueKind = JsonValueKind.False Then value = False : Return True
        End If
        Return False
    End Function

    Private Shared Function TryGetInt(root As JsonElement, name As String, ByRef value As Integer) As Boolean
        Dim el As JsonElement
        If root.TryGetProperty(name, el) AndAlso el.ValueKind = JsonValueKind.Number Then
            value = el.GetInt32()
            Return True
        End If
        Return False
    End Function
End Class
