' Version Uploaded of Wardrobe 2.1.3
Imports System.DirectoryServices.ActiveDirectory
Imports System.Globalization
Imports System.IO
Imports System.Net.Http.Json
Imports System.Numerics
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Text.RegularExpressions
Imports System.Xml
Imports Material_Editor
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports Wardrobe_Manager.Wardrobe_Manager_Form

Public Class HighHeels_Plugins_values
    Public Property HighHeelsKeys As New Dictionary(Of String, Double)
    Public Class HHJsonItem
        <JsonPropertyName("key")>
        Public Property ItemKey As String

        <JsonPropertyName("value")>
        Public Property ItemValue As Double
    End Class
    Sub Lee_HH_Json_y_txt()
        Dim carpeta As String = Directorios.HighHeels_Plugin
        If IO.Directory.Exists(carpeta) = False Then Exit Sub

        For Each archivo As String In Directory.GetFiles(carpeta, "*.json")
            Dim contenido As String = File.ReadAllText(archivo)
            Dim data = JsonSerializer.Deserialize(Of Dictionary(Of String, List(Of HHJsonItem)))(contenido)

            If data IsNot Nothing Then
                For Each kvp In data
                    For Each item In kvp.Value
                        item.ItemKey = Correct_Path_Separator(item.ItemKey)
                        HighHeelsKeys(item.ItemKey) = item.ItemValue
                    Next
                Next
            End If
        Next

        For Each archivo As String In Directory.GetFiles(carpeta, "*.txt")
            Dim lin As String = ""

            Using archivoSTM As New StreamReader(archivo)
                lin = archivoSTM.ReadLine()
            End Using

            If String.IsNullOrWhiteSpace(lin) Then Continue For
            If lin.Contains("="c) = False Then Continue For

            Dim sep = lin.Split("="c)
            If sep.Length <> 2 Then Continue For

            Dim kvp = New HHJsonItem With {
        .ItemKey = IO.Path.GetFileNameWithoutExtension(archivo) + ".nif",
        .ItemValue = Double.Parse(sep(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)}

            HighHeelsKeys(kvp.ItemKey) = kvp.ItemValue
        Next
    End Sub
    Public Sub LoadFromDirectory()
        HighHeelsKeys.Clear()
        Lee_HH_Json_y_txt()
    End Sub

End Class


Public Class SlidersPreset_Class
    Public Property Name As String = ""
    Public Property SetName As String = ""
    Public Property GroupNames As New List(Of String)
    Public Property Filename As String = ""
    Public Property Sliders As New List(Of PresetSlider_Class)
End Class


Public Class PresetSlider_Class
    Public Property Name As String = ""
    Public Property DisplayName As String = ""
    Public Property Size As Config_App.SliderSize = Config_App.SliderSize.Default
    Public Property Value As Single = 0
    Public Property Category As String = "(Unknown)"
End Class

Public Class Poses_class
    <JsonPropertyName("name")>
    Public Property Name As String

    <JsonPropertyName("skeleton")>
    Public Property Skeleton As String

    <JsonPropertyName("version")>
    Public Property Version As Integer

    ' Las claves internas (AnimObjectA, COM, etc.) se mantienen dinámicas
    <JsonPropertyName("transforms")>
    Public Property Transforms As Dictionary(Of String, PoseTransformData)

    Public Enum Pose_Source_Enum
        WardrobeManager
        BodySlide
        ScreenArcher
        None
    End Enum
    Public Overrides Function ToString() As String
        Return KeyName(Name, Source)
    End Function
    Public Shared Function KeyName(Name As String, sourceType As Pose_Source_Enum) As String
        Select Case sourceType
            Case Pose_Source_Enum.BodySlide
                Return Name + " (BodySlide pose)"
            Case Pose_Source_Enum.ScreenArcher
                Return Name + " (ScreenArcher pose)"
            Case Pose_Source_Enum.WardrobeManager, Pose_Source_Enum.None
                Return Name + " (Wardrobe Manager pose)"
            Case Else
                Return Name + " (Unknown pose)"
        End Select
    End Function
    <JsonIgnore>
    Public Property Source As Pose_Source_Enum = Pose_Source_Enum.ScreenArcher
    <JsonIgnore>
    Public Property Filename As String

    Public Function Clone() As Poses_class
        Dim Clon As New Poses_class With {
            .Name = "Unknown",
            .Skeleton = Skeleton,
            .Version = Version,
            .Source = Pose_Source_Enum.WardrobeManager,
            .Transforms = New Dictionary(Of String, PoseTransformData)
        }
        For Each tr In Transforms
            Dim rot As Vector3
            Dim Tras As Vector3
            Dim sc As Single
            If Source = Pose_Source_Enum.ScreenArcher Then
                Dim Converter = New Transform_Class(tr.Value, Source)
                Dim bon As Skeleton_Class.HierarchiBone_class = Nothing

                If Skeleton_Class.HasSkeleton AndAlso Skeleton_Class.SkeletonDictionary.TryGetValue(tr.Key, bon) Then
                    Converter = bon.OriginalLocaLTransform.Inverse.ComposeTransforms(Converter)
                End If
                rot = Transform_Class.Matrix33ToBSRotation(Converter.Rotation)
                Tras = New Vector3(Converter.Translation.X, Converter.Translation.Y, Converter.Translation.Z)
                sc = Converter.Scale
            Else
                rot = New Vector3(tr.Value.Yaw, tr.Value.Pitch, tr.Value.Roll)
                Tras = New Vector3(tr.Value.X, tr.Value.Y, tr.Value.Z)
                sc = tr.Value.Scale
            End If
            Dim cloned = New PoseTransformData With {.X = Tras.X, .Y = Tras.Y, .Z = Tras.Z, .Yaw = rot.X, .Pitch = rot.Y, .Roll = rot.Z, .Scale = sc}
            Clon.Transforms.Add(tr.Key, cloned)
        Next
        Return Clon
    End Function
End Class

' Nodo de transformaciones --------------------------------------------------
Public Class PoseTransformData
    <JsonPropertyName("pitch")> Public Property Pitch As Single = 0 'Y
    <JsonPropertyName("roll")> Public Property Roll As Single = 0 'Z
    <JsonPropertyName("yaw")> Public Property Yaw As Single = 0 ' X
    <JsonPropertyName("x")> Public Property X As Single = 0
    <JsonPropertyName("y")> Public Property Y As Single = 0
    <JsonPropertyName("z")> Public Property Z As Single = 0
    <JsonPropertyName("scale")> Public Property Scale As Single = 1

    <JsonIgnore>
    Public ReadOnly Property Isidentity As Boolean
        Get
            Return X = 0 AndAlso Y = 0 AndAlso Z = 0 AndAlso Yaw = 0 AndAlso Pitch = 0 AndAlso Roll = 0 AndAlso Scale = 1
        End Get
    End Property

End Class

' ---------------------------------------------------------------------------
' Ejemplo mínimo de carga
' ---------------------------------------------------------------------------
Public Class SliderPresetCollection

    Public Property Presets As New SortedDictionary(Of String, SlidersPreset_Class)
    Public Property Categories As New Dictionary(Of String, List(Of String()))
    Public Property Poses As New Dictionary(Of String, Poses_class)


    Public Sub LoadPosesBS(PosesPath As String)
        If IO.Directory.Exists(PosesPath) = False Then Exit Sub
        Dim filesPoses = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(PosesPath, "*.xml", False).ToList
        For Each xmlpath In filesPoses
            If IO.File.Exists(xmlpath) = False Then Continue For
            Try
                Dim rawXml As String = IO.File.ReadAllText(xmlpath)
                ' Cargar el documento desde el string corregido
                Dim doc As XDocument = XDocument.Parse(rawXml)
                For Each pose As XElement In doc.Root.Elements("Pose")
                    Dim pos As New Poses_class With {
                .Source = Poses_class.Pose_Source_Enum.BodySlide,
                .Name = pose.Attribute("name").Value.ToString,
                .Version = 1,
                .Skeleton = "CBBE",
                .Transforms = New Dictionary(Of String, PoseTransformData)
            }
                    If Not IsNothing(pose.Attribute("WMPose")) Then
                        If pose.Attribute("WMPose").Value = "true" Then
                            pos.Source = Poses_class.Pose_Source_Enum.WardrobeManager
                        End If
                    End If
                    For Each Bonepose As XElement In pose.Elements("Bone")
                        Dim Tr As New PoseTransformData
                        If Not IsNothing(Bonepose.Attribute("scale")) Then
                            Tr.Scale = Single.Parse(Bonepose.Attribute("scale").Value, CultureInfo.InvariantCulture)
                        End If
                        Tr.Yaw = Single.Parse(Bonepose.Attribute("rotX").Value, CultureInfo.InvariantCulture)
                        Tr.Pitch = Single.Parse(Bonepose.Attribute("rotY").Value, CultureInfo.InvariantCulture)
                        Tr.Roll = Single.Parse(Bonepose.Attribute("rotZ").Value, CultureInfo.InvariantCulture)
                        Tr.X = Single.Parse(Bonepose.Attribute("transX").Value, CultureInfo.InvariantCulture)
                        Tr.Y = Single.Parse(Bonepose.Attribute("transY").Value, CultureInfo.InvariantCulture)
                        Tr.Z = Single.Parse(Bonepose.Attribute("transZ").Value, CultureInfo.InvariantCulture)
                        Dim bon = Bonepose.Attribute("name").Value.ToString
                        pos.Transforms.Add(bon, Tr)
                    Next
                    pos.Filename = xmlpath
                    Poses.Add(pos.ToString, pos)
                Next
            Catch ex As Exception
                MsgBox("Error reading pose file " + xmlpath, vbCritical, "Error")
            End Try
        Next
    End Sub
    Public Sub LoadDefaultPose()
        Dim pos As New Poses_class With {
            .Source = Poses_class.Pose_Source_Enum.None,
            .Name = "None",
            .Version = 1,
            .Skeleton = "CBBE",
            .Transforms = New Dictionary(Of String, PoseTransformData)
        }
        Poses.Add(pos.ToString, pos)
    End Sub
    Public Sub LoadPosesSAM(PosesPath As String)
        If IO.Directory.Exists(PosesPath) = False Then Exit Sub
        Dim filesPoses = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(PosesPath, "*.json", False).ToList
        Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True, .NumberHandling = JsonNumberHandling.AllowReadingFromString}
        For Each xmlpath In filesPoses
            If IO.File.Exists(xmlpath) = False Then Continue For
            Try
                Dim json As String = IO.File.ReadAllText(xmlpath)
                Dim model As Poses_class = JsonSerializer.Deserialize(Of Poses_class)(json, opts)
                model.Filename = xmlpath
                Poses.Add(model.ToString, model)
            Catch ex As Exception
                MsgBox("Error reading pose file " + xmlpath, vbCritical, "Error")
            End Try
        Next
    End Sub
    Public Sub LoadCategories(xmlFolder As String)
        ' Carga el documento XML
        Categories.Clear()
        If IO.Directory.Exists(xmlFolder) = False Then Exit Sub
        Dim files = FilesDictionary_class.EnumerateFilesWithSymlinkSupport(xmlFolder, "*.xml", False).ToList
        For Each xmlPath In files
            Try
                Dim rawXml As String = IO.File.ReadAllText(xmlPath)
                ' Arreglar posibles errores de formato: insertar espacio antes de 'displayname'
                rawXml = rawXml.Replace("""displayname", """ displayname")

                ' Cargar el documento desde el string corregido
                Dim doc As XDocument = XDocument.Parse(rawXml)

                ' Recorre cada elemento <Category>
                For Each categoryElem As XElement In doc.Root.Elements("Category")
                    Dim categoryName As String = CStr(categoryElem.Attribute("name").Value)
                    Dim sliderNames As New List(Of String())()
                    If Categories.TryGetValue(categoryName, sliderNames) = False Then
                        sliderNames = New List(Of String())()
                        Categories.Add(categoryName, sliderNames)
                    Else
                        sliderNames = Categories(categoryName)
                    End If

                    ' Agrega cada atributo "name" de <Slider>
                    For Each sliderElem As XElement In categoryElem.Elements("Slider")
                        Dim valname As String = CStr(sliderElem.Attribute("name").Value)
                        Dim valdisplay As String = CStr(sliderElem.Attribute("displayname").Value)
                        Dim sliderName As String() = {valname, valdisplay}
                        Dim match = sliderNames.FirstOrDefault(Function(pf) valname.Equals(pf(0), StringComparison.OrdinalIgnoreCase))
                        If match IsNot Nothing Then
                            match(1) = valdisplay
                        Else
                            sliderNames.Add(sliderName)
                        End If

                    Next

                Next
            Catch ex As Exception
                MsgBox("Error reading Category file " + xmlPath, vbCritical, "Error")
            End Try
        Next

    End Sub

    Public Sub LoadFromXml(path As String)
        Try
            Dim doc = XDocument.Load(path)
            For Each xp In doc.Root.Elements("Preset")
                Dim nameAttr = xp.Attribute("name")?.Value
                Dim setAttr = xp.Attribute("set")?.Value
                If String.IsNullOrEmpty(nameAttr) Then
                    Throw New InvalidDataException($"<Preset> missing required 'name' in '{path}'")
                End If
                If String.IsNullOrEmpty(setAttr) Then
                    Throw New InvalidDataException($"<Preset name=""{nameAttr}""> missing required 'set' in '{path}'")
                End If

                Dim groups = xp.Elements("Group").Select(Function(g) g.Attribute("name")?.Value).Where(Function(n) Not String.IsNullOrEmpty(n)).ToList()


                Dim p As New SlidersPreset_Class With {
                    .Name = nameAttr,
                    .SetName = setAttr,
                    .GroupNames = groups,
                    .Filename = path
                }

                For Each ss In xp.Elements("SetSlider")
                    Dim sliderName = ss.Attribute("name")?.Value
                    Dim valText = ss.Attribute("value")?.Value
                    If String.IsNullOrEmpty(sliderName) Then
                        Throw New InvalidDataException($"<SetSlider> missing 'name' in preset '{nameAttr}' of '{path}'")
                    End If
                    Dim valueInt As Integer
                    If Not Integer.TryParse(valText, valueInt) Then
                        Throw New InvalidDataException($"<SetSlider name=""{sliderName}""> has invalid or missing 'value' in preset '{nameAttr}' of '{path}'")
                    End If

                    Dim sizeRaw = ss.Attribute("size")?.Value?.ToLowerInvariant()
                    Dim sz As Config_App.SliderSize = If(sizeRaw = "small", Config_App.SliderSize.Small,
                                             If(sizeRaw = "big", Config_App.SliderSize.Big,
                                                                  Config_App.SliderSize.Default))

                    p.Sliders.Add(New PresetSlider_Class With {
                        .Name = sliderName,
                        .Size = sz,
                        .Value = valueInt
                         })
                Next
                Dim Nombre As String = p.Name
                Dim subs As Integer = 1
                While Presets.ContainsKey(Nombre)
                    Nombre = p.Name + "_" + subs.ToString
                    subs += 1
                End While
                Presets.Add(Nombre, p)
            Next
        Catch ex As Exception
            MsgBox("Error reading Preset file " + path, vbCritical, "Error")
        End Try

    End Sub
    Public Shared Function Clone(Sl As SlidersPreset_Class, Filename As String, nombre As String) As SlidersPreset_Class
        Dim nuevo As New SlidersPreset_Class With {.Name = nombre, .Filename = Filename, .GroupNames = Sl.GroupNames.ToList, .SetName = Sl.SetName}
        For Each sli In Sl.Sliders
            Dim cop As New PresetSlider_Class With {.Name = sli.Name, .Size = sli.Size, .Value = sli.Value}
            nuevo.Sliders.Add(cop)
        Next
        Return nuevo
    End Function

End Class
Public Class OSD_Class
    Public Property Header As Byte() = {0, 68, 83, 79}
    Public Property Version As Byte() = {1, 0, 0, 0}
    Public Property Datablocks As Integer = 0
    Public Property Blocks As New List(Of OSD_Block_Class)
    Public Property ParentSlider As SliderSet_Class

    Sub New(Parent As SliderSet_Class)
        ParentSlider = Parent
    End Sub
    Public Shared ReadOnly FileLocks As New Dictionary(Of String, Object)()
    Public Shared ReadOnly FileLocksSync As New Object
    Public Sub Load(FilenameParameter As String())
        Dim fileLock As Object
        Blocks.Clear()
        For Each Filename In FilenameParameter
            ' Obtener o crear lock por archivo
            If IO.File.Exists(Filename) Then
                SyncLock FileLocksSync

                    Dim value As Object = Nothing

                    If Not FileLocks.TryGetValue(Filename, value) Then
                        value = New Object()
                        FileLocks(Filename) = value
                    End If
                    fileLock = value
                End SyncLock
                SyncLock fileLock
                    Using stream = IO.File.Open(Filename, IO.FileMode.Open, FileAccess.Read)
                    Using reader As New IO.BinaryReader(stream)
                    Header = reader.ReadBytes(4)
                    Version = reader.ReadBytes(4)
                    Datablocks = CInt(reader.ReadUInt32 And &H7FFFFFFFL)
                    For x = 0 To Datablocks - 1
                        Dim Namelenght As Byte = reader.ReadByte
                        Dim namebytes As Byte() = reader.ReadBytes(Namelenght)
                        Dim block As New OSD_Block_Class(Me) With {.BlockName = (System.Text.Encoding.UTF8.GetString(namebytes))}
                        Dim DifDatas = CType(reader.ReadUInt16, Int32)
                        ' O5.1: Pre-allocate compact arrays + DataDiff list in one pass
                        block.IndicesCompact = New Integer(DifDatas - 1) {}
                        block.DeltasCompact = New Single(DifDatas * 3 - 1) {}
                        block.DataDiff = New List(Of OSD_DataDiff_Class)(DifDatas)
                        For y As Int32 = 0 To DifDatas - 1
                            Dim idx = reader.ReadUInt16()
                            Dim vx = reader.ReadSingle()
                            Dim vy = reader.ReadSingle()
                            Dim vz = reader.ReadSingle()
                            block.IndicesCompact(y) = idx
                            block.DeltasCompact(y * 3) = vx
                            block.DeltasCompact(y * 3 + 1) = vy
                            block.DeltasCompact(y * 3 + 2) = vz
                            block.DataDiff.Add(New OSD_DataDiff_Class With {.Index = idx, .X = vx, .Y = vy, .Z = vz})
                        Next
                        Blocks.Add(block)
                    Next
                    End Using
                    End Using
                End SyncLock
            End If
        Next
        If Not IsNothing(ParentSlider) Then ParentSlider.InvalidateShapeDataLookupCache()
    End Sub

    Public Sub Clone_block(source As OSD_Block_Class)
        Dim nuewblock = New OSD_Block_Class(Me) With {.BlockName = source.BlockName}
        For Each dat In source.DataDiff
            nuewblock.DataDiff.Add(New OSD_DataDiff_Class() With {.Index = dat.Index, .X = dat.X, .Y = dat.Y, .Z = dat.Z})
        Next
        ' O5.1: Clone compact arrays if available
        If source.IndicesCompact IsNot Nothing Then
            nuewblock.IndicesCompact = CType(source.IndicesCompact.Clone(), Integer())
            nuewblock.DeltasCompact = CType(source.DeltasCompact.Clone(), Single())
        Else
            nuewblock.RebuildCompactArrays()
        End If
        Me.Blocks.Add(nuewblock)
        If Not IsNothing(ParentSlider) Then ParentSlider.InvalidateShapeDataLookupCache()

    End Sub

    Public Sub Save_As(Filename As String, Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then
            If MsgBox("ODS File already exists, replace?", vbYesNo, "Warning") = MsgBoxResult.No Then
                Exit Sub
            End If
        End If

        If IsNothing(Me.Header) Then Exit Sub
        Using stream = IO.File.Open(Filename, IO.FileMode.Create)
        Using Writer As New IO.BinaryWriter(stream)

        Writer.Write(Header)
        Writer.Write(Version)
        Writer.Write(CType(Blocks.Count, UInt32))
        For x = 0 To Blocks.Count - 1
            Dim blk = Blocks(x)
            Writer.Write(CType(blk.BlockName.Length, Byte))
            Writer.Write(System.Text.Encoding.UTF8.GetBytes(blk.BlockName))
            Dim DifDatas = blk.DataDiff.Count
            Writer.Write(CType(DifDatas, UInt16))
            ' O5.1: Use compact arrays when available for faster sequential write
            If blk.IndicesCompact IsNot Nothing AndAlso blk.IndicesCompact.Length = DifDatas Then
                For y = 0 To DifDatas - 1
                    Writer.Write(CType(blk.IndicesCompact(y), UInt16))
                    Writer.Write(blk.DeltasCompact(y * 3))
                    Writer.Write(blk.DeltasCompact(y * 3 + 1))
                    Writer.Write(blk.DeltasCompact(y * 3 + 2))
                Next
            Else
                For y = 0 To DifDatas - 1
                    Writer.Write(CType(blk.DataDiff(y).Index, UInt16))
                    Writer.Write(CType(blk.DataDiff(y).X, Single))
                    Writer.Write(CType(blk.DataDiff(y).Y, Single))
                    Writer.Write(CType(blk.DataDiff(y).Z, Single))
                Next
            End If
        Next
        Writer.Flush()
        End Using
        End Using
    End Sub
End Class
Public Class OSD_Block_Class
    Public Property BlockName As String
    Public Property DataDiff As New List(Of OSD_DataDiff_Class)
    Public Property ParentOSDContent As OSD_Class

    ' --- O5.1: Compact array storage for cache-friendly access ---
    ' Populated during binary Load; avoids per-element object traversal in hot paths.
    ' IndicesCompact(i) corresponds to DeltasCompact(i*3), DeltasCompact(i*3+1), DeltasCompact(i*3+2)
    Public IndicesCompact() As Integer
    Public DeltasCompact() As Single ' interleaved [x0,y0,z0, x1,y1,z1, ...]

    Sub New(Parent As OSD_Class)
        Me.ParentOSDContent = Parent
    End Sub

    ''' <summary>
    ''' Rebuilds compact arrays from the current DataDiff list.
    ''' Call after modifying DataDiff externally (editor, conform, merge, etc.).
    ''' </summary>
    Public Sub RebuildCompactArrays()
        Dim n = DataDiff.Count
        If n = 0 Then
            IndicesCompact = Array.Empty(Of Integer)()
            DeltasCompact = Array.Empty(Of Single)()
            Exit Sub
        End If

        IndicesCompact = New Integer(n - 1) {}
        DeltasCompact = New Single(n * 3 - 1) {}
        For i = 0 To n - 1
            Dim d = DataDiff(i)
            IndicesCompact(i) = d.Index
            DeltasCompact(i * 3) = d.X
            DeltasCompact(i * 3 + 1) = d.Y
            DeltasCompact(i * 3 + 2) = d.Z
        Next
    End Sub

    Public ReadOnly Property RelatedData As IEnumerable(Of Slider_Data_class)
        Get
            If IsNothing(ParentOSDContent) OrElse IsNothing(ParentOSDContent.ParentSlider) OrElse IsNothing(ParentOSDContent.ParentSlider.Sliders) Then Return Enumerable.Empty(Of Slider_Data_class)()
            Return ParentOSDContent.ParentSlider.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.Nombre.Equals(BlockName, StringComparison.OrdinalIgnoreCase))
        End Get
    End Property

    ''' <summary>Deep copy of DataDiff list (snapshot before mutation).</summary>
    Public Function SnapshotDiffs() As List(Of OSD_DataDiff_Class)
        Return DataDiff.Select(Function(d) New OSD_DataDiff_Class() With {.Index = d.Index, .X = d.X, .Y = d.Y, .Z = d.Z}).ToList()
    End Function

End Class
Public Class OSD_DataDiff_Class
    Public Property Index As Integer
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single

End Class
Public Class Clone_Materials_class
    Private Shared ReadOnly allowedExtensions As String() = New String() {".bgsm", ".bgem"}
    Private Class ClonePlan
        Public ReadOnly MaterialJobs As New Dictionary(Of String, MaterialJob)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly TextureJobs As New Dictionary(Of String, TextureJob)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly Bindings As New List(Of ShapeMaterialBinding)
    End Class

    Private Class ShapeMaterialBinding
        Public Property Shader As INiShader
        Public Property MaterialSourceKey As String = ""
        Public Property FallbackShaderMaterialName As String = ""
    End Class

    Private Class MaterialJob
        Public Property Source As String = ""
        Public Property Extension As String = ""
        Public Property RelativeSourceDirectory As String = ""
        Public Property TargetReference As String = ""
        Public Property TargetFullPath As String = ""
        Public Property Required As Boolean = False

        Public Property Scanned As Boolean = False
        Public Property Resolving As Boolean = False
        Public Property Resolved As Boolean = False
        Public Property Succeeded As Boolean = False
        Public Property NeedsWrite As Boolean = False

        Public Property FinalReference As String = ""

        Public Property BaseTexturePropertyName As String = ""
        Public Property RootMaterialSource As String = ""
        Public Property RootMaterialResolved As String = ""

        Public ReadOnly TextureReferences As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly ResolvedTextureReferences As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    End Class

    Private Class TextureJob
        Public Property SourceKey As String = ""
        Public Property OriginalRelative As String = ""
        Public Property TargetRelative As String = ""
        Public Property TargetFullPath As String = ""

        Public Property Resolved As Boolean = False
        Public Property Succeeded As Boolean = False
        Public Property NeedsWrite As Boolean = False
        Public Property FinalRelative As String = ""
    End Class
    Private Shared Function BuildTextureDictionaryKey(filename As String) As String
        If String.IsNullOrWhiteSpace(filename) Then Return ""

        Dim normalized = filename.Correct_Path_Separator.StripPrefix(TexturesPrefix)
        Return (TexturesPrefix & normalized).Correct_Path_Separator
    End Function

    Private Shared Function PrefetchDictionaryFiles(paths As IEnumerable(Of String)) As Dictionary(Of String, Byte())
        Dim result As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)
        If IsNothing(paths) Then Return result

        Dim normalized = paths.
    Where(Function(p) String.IsNullOrWhiteSpace(p) = False).
    Select(Function(p) p.Correct_Path_Separator).
    Distinct(StringComparer.OrdinalIgnoreCase).
    Where(Function(p)
              Dim location As FilesDictionary_class.File_Location = Nothing
              Return FilesDictionary_class.Dictionary.TryGetValue(p, location) AndAlso
                     Not IsNothing(location) AndAlso
                     location.IsLosseFile = False
          End Function).
    ToArray()

        If normalized.Length = 0 Then Return result

        Dim loaded = FilesDictionary_class.GetMultipleFilesBytes(normalized)

        For i As Integer = 0 To normalized.Length - 1
            result(normalized(i)) = loaded(i)
        Next

        Return result
    End Function

    Public Shared Sub Clone_Materials_For_Project(project As SliderSet_Class, overwrite As Boolean)
        Dim plan As New ClonePlan
        If OSP_Project_Class.Load_and_Check_Shapedata(project, True) = False Then Exit Sub
        CollectClonePlan(project, plan)

        For Each job In plan.MaterialJobs.Values.ToList()
            ResolveMaterialJob(plan, job, overwrite)
        Next

        CommitTextureJobs(plan, overwrite)
        CommitMaterialJobs(plan, overwrite)
        ApplyShapeBindings(plan)

        project.InvalidateAllLookupCaches()
        project.Save_Shapedatas(True)
    End Sub

    Private Shared Sub CollectClonePlan(project As SliderSet_Class, plan As ClonePlan)
        For Each shap In project.Shapes
            If IsNothing(shap.RelatedNifShader) Then Continue For

            Dim shad = shap.RelatedNifShader
            Dim originalMaterialName As String = GetShaderMaterialName(shad)
            Dim materialSourceKey As String = NormalizeMaterialSourceKey(originalMaterialName)

            plan.Bindings.Add(New ShapeMaterialBinding With {
                .Shader = shad,
                .MaterialSourceKey = materialSourceKey,
                .FallbackShaderMaterialName = NormalizeMaterialReference(originalMaterialName)
            })

            If materialSourceKey <> "" Then
                RegisterMaterialDirectory(plan, materialSourceKey)
            End If
        Next
    End Sub

    Private Shared Function GetShaderMaterialName(shad As INiShader) As String
        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                Return CType(shad, BSLightingShaderProperty).Name.String.Correct_Path_Separator
            Case GetType(BSEffectShaderProperty)
                Return CType(shad, BSEffectShaderProperty).Name.String.Correct_Path_Separator
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
    End Function

    Private Shared Sub SetShaderMaterialName(shad As INiShader, value As String)
        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                CType(shad, BSLightingShaderProperty).Name.String = value
            Case GetType(BSEffectShaderProperty)
                CType(shad, BSEffectShaderProperty).Name.String = value
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
    End Sub

    Private Shared Function NormalizeRelativeDirectory(relative As String) As String
        Dim normalized = relative.Correct_Path_Separator
        If normalized = "." OrElse normalized = ".\" Then Return ""
        If normalized <> "" AndAlso normalized.EndsWith("\"c) = False Then normalized &= "\"
        Return normalized
    End Function

    Private Shared Function NormalizeMaterialReference(materialName As String) As String
        Return materialName.Correct_Path_Separator.StripPrefix(MaterialsPrefix)
    End Function

    Private Shared Function NormalizeMaterialSourceKey(materialName As String) As String
        Dim mate As String = NormalizeMaterialReference(materialName)
        If mate = "" Then Return ""
        Return (MaterialsPrefix & mate).Correct_Path_Separator
    End Function

    Private Shared Function NormalizeTextureReference(textureName As String) As String
        Return textureName.Correct_Path_Separator.StripPrefix(TexturesPrefix)
    End Function

    Private Shared Function BuildClonedTextureRelativePath(textureName As String) As String
        Dim normalized = NormalizeTextureReference(textureName)
        If normalized = "" Then Return ""

        If normalized.Contains("ManoloCloned", StringComparison.OrdinalIgnoreCase) OrElse
           normalized.Contains("ManoloMods", StringComparison.OrdinalIgnoreCase) Then
            Return normalized
        End If

        Return "ManoloCloned\" & normalized
    End Function

    Private Shared Function GetOrCreateMaterialJob(plan As ClonePlan, source As String, required As Boolean) As MaterialJob
        Dim normalizedSource As String = NormalizeMaterialSourceKey(source)
        If normalizedSource = "" Then Return Nothing

        Dim existing As MaterialJob = Nothing
        If plan.MaterialJobs.TryGetValue(normalizedSource, existing) Then
            If required Then existing.Required = True
            Return existing
        End If

        Dim directory As String = IO.Path.GetDirectoryName(normalizedSource).Correct_Path_Separator
        Dim relativeDir As String = NormalizeRelativeDirectory(IO.Path.GetRelativePath(MaterialsPrefix, directory & "\"))

        Dim job As New MaterialJob With {
            .Source = normalizedSource,
            .Extension = IO.Path.GetExtension(normalizedSource).ToLowerInvariant(),
            .RelativeSourceDirectory = relativeDir,
            .TargetReference = BuildClonedMaterialRelativePath(normalizedSource, relativeDir),
            .Required = required
        }

        job.TargetFullPath = IO.Path.Combine(Directorios.Fallout4data, MaterialsPrefix & job.TargetReference).Correct_Path_Separator
        job.FinalReference = job.Source

        plan.MaterialJobs.Add(normalizedSource, job)
        Return job
    End Function

    Private Shared Function GetOrCreateTextureJob(plan As ClonePlan, textureReference As String) As TextureJob
        Dim sourceKey As String = BuildTextureDictionaryKey(textureReference)
        If sourceKey = "" Then Return Nothing

        Dim existing As TextureJob = Nothing
        If plan.TextureJobs.TryGetValue(sourceKey, existing) Then
            Return existing
        End If

        Dim originalRelative As String = NormalizeTextureReference(textureReference)
        Dim targetRelative As String = BuildClonedTextureRelativePath(textureReference)

        Dim job As New TextureJob With {
            .SourceKey = sourceKey,
            .OriginalRelative = originalRelative,
            .TargetRelative = targetRelative,
            .TargetFullPath = IO.Path.Combine(Config_App.Current.FO4EDataPath, TexturesPrefix & targetRelative).Correct_Path_Separator,
            .FinalRelative = originalRelative
        }

        plan.TextureJobs.Add(sourceKey, job)
        Return job
    End Function

    Private Shared Sub RegisterMaterialDirectory(plan As ClonePlan, originalMaterialSource As String)
        Dim normalizedOriginal As String = NormalizeMaterialSourceKey(originalMaterialSource)
        If normalizedOriginal = "" Then Exit Sub
        If FilesDictionary_class.Dictionary.ContainsKey(normalizedOriginal) = False Then Exit Sub

        Dim originalLocation = FilesDictionary_class.Dictionary(normalizedOriginal)
        If Not originalLocation.IsLosseFile AndAlso Not Config_App.Allowed_To_Clone(originalLocation.BA2File) Then Exit Sub

        Dim directory As String = IO.Path.GetDirectoryName(normalizedOriginal).Correct_Path_Separator

        For Each fil In FilesDictionary_class.GetFilesInDirectory(directory, allowedExtensions)
            Dim normalizedFil As String = fil.Correct_Path_Separator

            Dim location = FilesDictionary_class.Dictionary(normalizedFil)
            If location.IsLosseFile OrElse Config_App.Allowed_To_Clone(location.BA2File) Then
                GetOrCreateMaterialJob(plan, normalizedFil, normalizedFil.Equals(normalizedOriginal, StringComparison.OrdinalIgnoreCase))
            End If
        Next
    End Sub

    Private Shared Sub RegisterMaterialTextureReference(plan As ClonePlan, job As MaterialJob, propertyName As String, textureReference As String)
        Dim normalizedReference As String = textureReference.Correct_Path_Separator
        job.TextureReferences(propertyName) = normalizedReference

        If String.IsNullOrWhiteSpace(normalizedReference) = False Then
            GetOrCreateTextureJob(plan, normalizedReference)
        End If
    End Sub

    Private Shared Sub ScanMaterialJob(plan As ClonePlan, job As MaterialJob)
        If job.Scanned Then Exit Sub

        Select Case job.Extension
            Case ".bgsm"
                Dim material As New BGSM
                Using ms As New MemoryStream(FilesDictionary_class.GetBytes(job.Source))
                    Using reader As New BinaryReader(ms)
                        material.Deserialize(reader)
                    End Using
                End Using

                job.BaseTexturePropertyName = "DiffuseTexture"

                RegisterMaterialTextureReference(plan, job, "NormalTexture", material.NormalTexture)
                RegisterMaterialTextureReference(plan, job, "SmoothSpecTexture", material.SmoothSpecTexture)
                RegisterMaterialTextureReference(plan, job, "GreyscaleTexture", material.GreyscaleTexture)
                RegisterMaterialTextureReference(plan, job, "EnvmapTexture", material.EnvmapTexture)
                RegisterMaterialTextureReference(plan, job, "FlowTexture", material.FlowTexture)
                RegisterMaterialTextureReference(plan, job, "GlowTexture", material.GlowTexture)
                RegisterMaterialTextureReference(plan, job, "DisplacementTexture", material.DisplacementTexture)
                RegisterMaterialTextureReference(plan, job, "InnerLayerTexture", material.InnerLayerTexture)
                RegisterMaterialTextureReference(plan, job, "LightingTexture", material.LightingTexture)
                RegisterMaterialTextureReference(plan, job, "SpecularTexture", material.SpecularTexture)
                RegisterMaterialTextureReference(plan, job, "WrinklesTexture", material.WrinklesTexture)
                RegisterMaterialTextureReference(plan, job, "DistanceFieldAlphaTexture", material.DistanceFieldAlphaTexture)
                RegisterMaterialTextureReference(plan, job, "DiffuseTexture", material.DiffuseTexture)

                Dim temp = material.RootMaterialPath.Correct_Path_Separator
                temp = temp.StripPrefix(MaterialsPrefix)

                If temp <> "" Then
                    Dim rootSource As String = (MaterialsPrefix & temp).Correct_Path_Separator

                    Dim rootLocation As FilesDictionary_class.File_Location = Nothing
                    If FilesDictionary_class.Dictionary.TryGetValue(rootSource, rootLocation) Then
                        If rootSource.Equals(job.Source, StringComparison.OrdinalIgnoreCase) = False Then
                            If rootLocation.IsLosseFile OrElse Config_App.Allowed_To_Clone(rootLocation.BA2File) Then
                                job.RootMaterialSource = rootSource
                                GetOrCreateMaterialJob(plan, rootSource, False)
                            End If
                        End If
                    End If
                End If

            Case ".bgem"
                Dim material As New BGEM
                Using ms As New MemoryStream(FilesDictionary_class.GetBytes(job.Source))
                    Using reader As New BinaryReader(ms)
                        material.Deserialize(reader)
                    End Using
                End Using

                job.BaseTexturePropertyName = "BaseTexture"

                RegisterMaterialTextureReference(plan, job, "NormalTexture", material.NormalTexture)
                RegisterMaterialTextureReference(plan, job, "BaseTexture", material.BaseTexture)
                RegisterMaterialTextureReference(plan, job, "EnvmapMaskTexture", material.EnvmapMaskTexture)
                RegisterMaterialTextureReference(plan, job, "EnvmapTexture", material.EnvmapTexture)
                RegisterMaterialTextureReference(plan, job, "GrayscaleTexture", material.GrayscaleTexture)
                RegisterMaterialTextureReference(plan, job, "LightingTexture", material.LightingTexture)
                RegisterMaterialTextureReference(plan, job, "GlowTexture", material.GlowTexture)
                RegisterMaterialTextureReference(plan, job, "SpecularTexture", material.SpecularTexture)

            Case Else
                Throw New Exception
        End Select

        job.Scanned = True
    End Sub

    Private Shared Function ResolveTextureReference(plan As ClonePlan, textureReference As String, overwrite As Boolean, ByRef succeeded As Boolean) As String
        If String.IsNullOrWhiteSpace(textureReference) Then
            succeeded = True
            Return ""
        End If

        Dim job = GetOrCreateTextureJob(plan, textureReference)
        If IsNothing(job) Then
            succeeded = True
            Return ""
        End If

        ResolveTextureJob(job, overwrite)
        succeeded = job.Succeeded
        Return job.FinalRelative
    End Function

    Private Shared Sub ResolveTextureJob(job As TextureJob, overwrite As Boolean)
        If job.Resolved Then Exit Sub

        job.Resolved = True
        job.Succeeded = False
        job.NeedsWrite = False
        job.FinalRelative = job.OriginalRelative

        If job.SourceKey = "" Then
            job.Succeeded = True
            Exit Sub
        End If

        Dim location As FilesDictionary_class.File_Location = Nothing

        FilesDictionary_class.Dictionary.TryGetValue(job.SourceKey, location)

        If IsNothing(location) Then
            Exit Sub
        End If

        If Not location.IsLosseFile AndAlso Not Config_App.Allowed_To_Clone(location.BA2File) Then
            Exit Sub
        End If

        If job.TargetRelative.Equals(job.OriginalRelative, StringComparison.OrdinalIgnoreCase) Then
            job.Succeeded = True
            job.NeedsWrite = False
            job.FinalRelative = job.OriginalRelative
            Exit Sub
        End If

        If IO.File.Exists(job.TargetFullPath) AndAlso overwrite = False Then
            job.Succeeded = True
            job.NeedsWrite = False
            job.FinalRelative = job.TargetRelative
            Exit Sub
        End If

        job.Succeeded = True
        job.NeedsWrite = True
        job.FinalRelative = job.TargetRelative
    End Sub

    Private Shared Sub ResolveMaterialJob(plan As ClonePlan, job As MaterialJob, overwrite As Boolean)
        If job.Resolved Then Exit Sub

        If job.Resolving Then
            Throw New Exception("Circular RootMaterialPath: " & job.Source)
        End If

        job.Resolving = True
        Try
            If job.Scanned = False Then
                ScanMaterialJob(plan, job)
            End If

            Dim baseSucceeded As Boolean = True

            For Each kv In job.TextureReferences
                Dim textureSucceeded As Boolean
                Dim resolvedReference As String = ResolveTextureReference(plan, kv.Value, overwrite, textureSucceeded)

                job.ResolvedTextureReferences(kv.Key) = resolvedReference

                If kv.Key.Equals(job.BaseTexturePropertyName, StringComparison.OrdinalIgnoreCase) Then
                    baseSucceeded = textureSucceeded
                End If
            Next

            If job.RootMaterialSource <> "" Then
                Dim rootJob = GetOrCreateMaterialJob(plan, job.RootMaterialSource, False)
                ResolveMaterialJob(plan, rootJob, overwrite)
                job.RootMaterialResolved = rootJob.FinalReference
            End If

            Dim sourceReference As String = NormalizeMaterialReference(job.Source)

            If job.Required = False AndAlso baseSucceeded = False Then
                job.Succeeded = False
                job.NeedsWrite = False
                job.FinalReference = sourceReference
            Else
                job.Succeeded = True

                If job.TargetReference.Equals(sourceReference, StringComparison.OrdinalIgnoreCase) Then
                    job.NeedsWrite = False
                    job.FinalReference = sourceReference
                Else
                    job.FinalReference = job.TargetReference

                    If IO.File.Exists(job.TargetFullPath) AndAlso overwrite = False Then
                        job.NeedsWrite = False
                    Else
                        job.NeedsWrite = True
                    End If
                End If
            End If

            job.Resolved = True
        Finally
            job.Resolving = False
        End Try
    End Sub

    Private Shared Sub CommitTextureJobs(plan As ClonePlan, overwrite As Boolean)
        Dim pending = plan.TextureJobs.Values.Where(Function(pf) pf.NeedsWrite AndAlso pf.Succeeded).ToList
        If pending.Count = 0 Then Exit Sub

        Dim packedSources = pending.
            Where(Function(pf)
                      If FilesDictionary_class.Dictionary.ContainsKey(pf.SourceKey) = False Then Return False
                      Return FilesDictionary_class.Dictionary(pf.SourceKey).IsLosseFile = False
                  End Function).
            Select(Function(pf) pf.SourceKey)

        Dim prefetchedPackedBytes = PrefetchDictionaryFiles(packedSources)

        For Each job In pending
            Dim location As FilesDictionary_class.File_Location = Nothing
            FilesDictionary_class.Dictionary.TryGetValue(job.SourceKey, location)
            If IsNothing(location) Then Continue For
            If location.IsLosseFile Then
                Dim sourceLooseFile As String = IO.Path.Combine(FilesDictionary_class.FO4Path, location.FullPath)
                Dim sourceFull As String = IO.Path.GetFullPath(sourceLooseFile)
                Dim targetFull As String = IO.Path.GetFullPath(job.TargetFullPath)

                If sourceFull.Equals(targetFull, StringComparison.OrdinalIgnoreCase) Then
                    RegisterGeneratedDictionaryFile(TexturesPrefix & job.TargetRelative)
                    Continue For
                End If
            End If

            If IO.Directory.Exists(IO.Path.GetDirectoryName(job.TargetFullPath)) = False Then
                IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(job.TargetFullPath))
            End If

            If IO.File.Exists(job.TargetFullPath) Then
                If overwrite = False Then
                    RegisterGeneratedDictionaryFile(TexturesPrefix & job.TargetRelative)
                    Continue For
                Else
                    IO.File.Delete(job.TargetFullPath)
                End If
            End If

            If location.IsLosseFile Then
                Dim sourceLooseFile As String = IO.Path.Combine(FilesDictionary_class.FO4Path, location.FullPath)
                IO.File.Copy(sourceLooseFile, job.TargetFullPath, False)
            Else
                Dim bytes As Byte() = Nothing
                prefetchedPackedBytes.TryGetValue(job.SourceKey, bytes)

                If IsNothing(bytes) OrElse bytes.Length = 0 Then
                    bytes = location.GetBytes()
                End If

                If IsNothing(bytes) OrElse bytes.Length = 0 Then
                    Throw New Exception("Cannot copy texture: " & job.SourceKey)
                End If

                IO.File.WriteAllBytes(job.TargetFullPath, bytes)
            End If

            RegisterGeneratedDictionaryFile("Textures\" & job.TargetRelative)
        Next
    End Sub

    Private Shared Sub WriteMaterialJob(job As MaterialJob, overwrite As Boolean, saveAction As Action(Of Stream))
        If IO.Directory.Exists(IO.Path.GetDirectoryName(job.TargetFullPath)) = False Then
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(job.TargetFullPath))
        End If

        If IO.File.Exists(job.TargetFullPath) Then
            If overwrite = False Then
                RegisterGeneratedDictionaryFile(MaterialsPrefix & job.TargetReference)
                Exit Sub
            Else
                IO.File.Delete(job.TargetFullPath)
            End If
        End If

        Using writer As FileStream = IO.File.Open(job.TargetFullPath, FileMode.Create)
            saveAction(writer)
        End Using

        RegisterGeneratedDictionaryFile("Materials\" & job.TargetReference)
    End Sub
    Private Shared Function BuildClonedMaterialRelativePath(source As String, relative As String) As String
        If relative.StartsWith("ManoloCloned", StringComparison.OrdinalIgnoreCase) OrElse
       relative.StartsWith("ManoloMods", StringComparison.OrdinalIgnoreCase) Then
            Return relative + IO.Path.GetFileName(source)
        End If

        Return "ManoloCloned\" + relative + IO.Path.GetFileName(source)
    End Function
    Private Shared Sub RegisterGeneratedDictionaryFile(fullPath As String)
        Dim normalized As String = fullPath.Correct_Path_Separator
        Dim location As New FilesDictionary_class.File_Location With {
        .BA2File = "",
        .Index = -1,
        .FullPath = normalized
    }

        If FilesDictionary_class.TryAddDictionaryEntry(normalized, location) = False Then
            If location.FullPath.Contains("ManoloCloned\", StringComparison.OrdinalIgnoreCase) = False AndAlso
           location.FullPath.Contains("ManoloMods\", StringComparison.OrdinalIgnoreCase) = False Then
                Debugger.Break()
                Throw New Exception
            End If
        End If
    End Sub
    Private Shared Sub ApplyBGSMResolvedReferences(material As BGSM, job As MaterialJob)
        Dim value As String = Nothing
        If job.ResolvedTextureReferences.TryGetValue("NormalTexture", value) Then material.NormalTexture = value
        If job.ResolvedTextureReferences.TryGetValue("SmoothSpecTexture", value) Then material.SmoothSpecTexture = value
        If job.ResolvedTextureReferences.TryGetValue("GreyscaleTexture", value) Then material.GreyscaleTexture = value
        If job.ResolvedTextureReferences.TryGetValue("EnvmapTexture", value) Then material.EnvmapTexture = value
        If job.ResolvedTextureReferences.TryGetValue("FlowTexture", value) Then material.FlowTexture = value
        If job.ResolvedTextureReferences.TryGetValue("GlowTexture", value) Then material.GlowTexture = value
        If job.ResolvedTextureReferences.TryGetValue("DisplacementTexture", value) Then material.DisplacementTexture = value
        If job.ResolvedTextureReferences.TryGetValue("InnerLayerTexture", value) Then material.InnerLayerTexture = value
        If job.ResolvedTextureReferences.TryGetValue("LightingTexture", value) Then material.LightingTexture = value
        If job.ResolvedTextureReferences.TryGetValue("SpecularTexture", value) Then material.SpecularTexture = value
        If job.ResolvedTextureReferences.TryGetValue("WrinklesTexture", value) Then material.WrinklesTexture = value
        If job.ResolvedTextureReferences.TryGetValue("DistanceFieldAlphaTexture", value) Then material.DistanceFieldAlphaTexture = value
        If job.ResolvedTextureReferences.TryGetValue("DiffuseTexture", value) Then material.DiffuseTexture = value
        If job.RootMaterialSource <> "" AndAlso job.RootMaterialResolved <> "" Then
            material.RootMaterialPath = job.RootMaterialResolved
        End If
    End Sub

    Private Shared Sub ApplyBGEMResolvedReferences(material As BGEM, job As MaterialJob)
        Dim value As String = Nothing
        If job.ResolvedTextureReferences.TryGetValue("NormalTexture", value) Then material.NormalTexture = value
        If job.ResolvedTextureReferences.TryGetValue("BaseTexture", value) Then material.BaseTexture = value
        If job.ResolvedTextureReferences.TryGetValue("EnvmapMaskTexture", value) Then material.EnvmapMaskTexture = value
        If job.ResolvedTextureReferences.TryGetValue("EnvmapTexture", value) Then material.EnvmapTexture = value
        If job.ResolvedTextureReferences.TryGetValue("GrayscaleTexture", value) Then material.GrayscaleTexture = value
        If job.ResolvedTextureReferences.TryGetValue("LightingTexture", value) Then material.LightingTexture = value
        If job.ResolvedTextureReferences.TryGetValue("GlowTexture", value) Then material.GlowTexture = value
        If job.ResolvedTextureReferences.TryGetValue("SpecularTexture", value) Then material.SpecularTexture = value
    End Sub

    Private Shared Sub CommitMaterialJobs(plan As ClonePlan, overwrite As Boolean)
        Dim pending = plan.MaterialJobs.Values.Where(Function(pf) pf.NeedsWrite AndAlso pf.Succeeded).ToList
        If pending.Count = 0 Then Exit Sub

        For Each job In pending
            Select Case job.Extension
                Case ".bgsm"
                    Dim material As New BGSM
                    Using ms As New MemoryStream(FilesDictionary_class.GetBytes(job.Source))
                        Using reader As New BinaryReader(ms)
                            material.Deserialize(reader)
                        End Using
                    End Using

                    ApplyBGSMResolvedReferences(material, job)

                    WriteMaterialJob(job, overwrite, Sub(writer)
                                                         material.Save(writer)
                                                     End Sub)

                Case ".bgem"
                    Dim material As New BGEM
                    Using ms As New MemoryStream(FilesDictionary_class.GetBytes(job.Source))
                        Using reader As New BinaryReader(ms)
                            material.Deserialize(reader)
                        End Using
                    End Using

                    ApplyBGEMResolvedReferences(material, job)

                    WriteMaterialJob(job, overwrite, Sub(writer)
                                                         material.Save(writer)
                                                     End Sub)

                Case Else
                    Throw New Exception
            End Select
        Next
    End Sub

    Private Shared Sub ApplyShapeBindings(plan As ClonePlan)
        For Each binding In plan.Bindings
            Dim finalName As String = binding.FallbackShaderMaterialName

            If binding.MaterialSourceKey <> "" Then
                Dim job As MaterialJob = Nothing
                If plan.MaterialJobs.TryGetValue(binding.MaterialSourceKey, job) Then
                    finalName = job.FinalReference
                End If
            End If

            SetShaderMaterialName(binding.Shader, finalName)
        Next
    End Sub
End Class
Public Class OSP_Project_Class
    Public Property SliderSets As New List(Of SliderSet_Class)
    Public xml As New XmlDocument
    Private Shared ReadOnly LoadedShapeDataSlots As New List(Of SliderSet_Class)
    Private Shared ReadOnly LoadedShapeDataSlotsSync As New Object()

    Public Shared Sub ForgetLoadedShapeDataSlot(slider As SliderSet_Class)
        If IsNothing(slider) Then Exit Sub

        SyncLock LoadedShapeDataSlotsSync
            LoadedShapeDataSlots.Remove(slider)
        End SyncLock
    End Sub
    Public Shared Property Default_Memory As Integer = 3
    Public Shared Property Default_Memory_Pause As Boolean = False
    ''' <summary>
    ''' The sliderset currently shown in the preview. Set by the preview control before
    ''' rendering so the LRU never evicts it even when other slidersets are loaded.
    ''' </summary>
    Public Shared Property PinnedForPreview As SliderSet_Class = Nothing
    Public Shared Sub RememberLoadedShapeDataSlot(slider As SliderSet_Class)
        If IsNothing(slider) Then Exit Sub

        Dim evicted As SliderSet_Class

        SyncLock LoadedShapeDataSlotsSync
            LoadedShapeDataSlots.Remove(slider)
            LoadedShapeDataSlots.Add(slider)
            If Not Default_Memory_Pause Then
                While LoadedShapeDataSlots.Count > Default_Memory
                    evicted = LoadedShapeDataSlots(0)
                    LoadedShapeDataSlots.RemoveAt(0)
                    ' Never evict the sliderset currently shown in the preview —
                    ' its VBOs depend on shapedata being live (RelatedNifShape, materials).
                    If Not IsNothing(evicted) AndAlso
                       Not Object.ReferenceEquals(evicted, slider) AndAlso
                       Not Object.ReferenceEquals(evicted, PinnedForPreview) Then
                        evicted.UnloadShapeData(False)
                    End If
                End While
            End If
        End SyncLock

    End Sub

    Private YaEstan As New List(Of SliderSet_Class)

    Sub New(Osd_File As String, Deep_Analize As Boolean)
        Try
            xml.Load(Osd_File)
            Lee_Slidersets(Deep_Analize)
        Catch ex As Exception
            MsgBox("Error reading OSP:" + Osd_File, vbCritical, "Error")
        End Try
    End Sub
    Sub New()
        Try
            Dim xmlDoc As New XmlDocument()
            Dim xmlDeclaration As XmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", Nothing)
            xmlDoc.AppendChild(xmlDeclaration)
            Dim root As XmlElement = xmlDoc.CreateElement("SliderSetInfo")
            root.SetAttribute("version", "1")
            root.SetAttribute("ManoloPack", "false")
            xmlDoc.AppendChild(root)
            xml = xmlDoc
        Catch ex As Exception
            MsgBox("Error Creating OSP:", "Error")
        End Try
    End Sub
    Sub Reload(Deep_Analize As Boolean)
        Try
            xml.Load(Me.Filename)
            YaEstan = Me.SliderSets.ToList
            Lee_Slidersets(Deep_Analize)
        Catch ex As Exception
            MsgBox("Error reading OSP:" + Me.Filename, vbCritical, "Error")
        End Try
    End Sub

    Public Shared Function Create_New(Filename As String, Overwrite_If_Exist As Boolean, ManoloPack As Boolean) As OSP_Project_Class
        If IO.File.Exists(Filename) AndAlso Overwrite_If_Exist = False Then
            MsgBox("Project name already exists, it will not be processed", vbCritical, "Duplicated")
            Return Nothing
        End If
        Using writer = IO.File.CreateText(Filename)
            writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")
            writer.WriteLine("<SliderSetInfo version=" + Chr(34) + " 1" + Chr(34) + " ManoloPack=" + Chr(34) + IIf(ManoloPack, "true", "false") + Chr(34) + ">")
            writer.WriteLine("</SliderSetInfo>")
            writer.Flush()
        End Using
        Return New OSP_Project_Class(Filename, True)
    End Function
    Public Function AddProject(ByRef Template As SliderSet_Class) As SliderSet_Class
        Dim Sliderset_Target = New SliderSet_Class(Me.xml.DocumentElement.AppendChild(Me.xml.ImportNode(Template.Nodo.Clone, True)), Me)
        Load_and_CHeck_Project(Sliderset_Target)
        Load_and_Check_Shapedata(Sliderset_Target, True)
        SliderSets.Add(Sliderset_Target)
        Return Sliderset_Target
    End Function

    Public Sub RemoveProject(ByRef Target As SliderSet_Class)
        Target.NIFContent.Clear()
        Target.Remove_DataShapeFiles()
        Me.xml.DocumentElement.RemoveChild(Target.Nodo)
        Me.SliderSets.Remove(Target)
        Save_Pack_As(Filename, True)

    End Sub
    Public Sub Save_Pack(Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then Throw New Exception("OSP File already exists")
        Save_Pack_As(Me.Filename, Overwrite)
    End Sub

    Public Sub Save_Pack_As(NewFilename As String, Overwrite As Boolean)
        If IO.File.Exists(NewFilename) AndAlso Overwrite = False Then Throw New Exception("OSP File already exists")
        Me.xml.Save(NewFilename)
        For Each slider In Me.SliderSets
            slider.LastProjectFileSignature = slider.GetProjectFileSignature()
        Next
    End Sub




    Public ReadOnly Property IsManoloPack
        Get
            'ManoloPack="true"
            If Not IsNothing(xml.DocumentElement.Attributes("ManoloPack")) AndAlso xml.DocumentElement.Attributes("ManoloPack").Value = "true" Then Return True
            Return False
        End Get
    End Property
    Public Shared Function Load_and_Check_Shapedata(ByRef Sliderset_Target As SliderSet_Class, verbose As Boolean) As Boolean
        Dim currentProjectSignature As String = ""
        Dim currentShapeDataSignature As String = ""

        Try
            currentProjectSignature = Sliderset_Target.GetProjectFileSignature()

            If Sliderset_Target.LastProjectFileSignature <> "" AndAlso
           Not String.Equals(Sliderset_Target.LastProjectFileSignature, currentProjectSignature, StringComparison.Ordinal) Then

                Sliderset_Target.Unreadable_Project = False
                Sliderset_Target.Unreadable_NIF = False

                Sliderset_Target.ParentOSP.Reload(False)

                If Sliderset_Target.ParentOSP.SliderSets.Contains(Sliderset_Target) = False Then
                    Sliderset_Target.Unreadable_Project = True
                    If verbose Then
                        MsgBox("Project changed on disk and the selected sliderset no longer exists. Refresh the list.", vbCritical + vbOK, "Error")
                    End If
                    Return False
                End If

                currentProjectSignature = Sliderset_Target.GetProjectFileSignature()
            End If

            If Sliderset_Target.Unreadable_Project Then Return False

            currentShapeDataSignature = Sliderset_Target.GetShapeDataSignature()

            If Sliderset_Target.ShapeDataLoaded AndAlso
           String.Equals(Sliderset_Target.LastShapeDataSignature, currentShapeDataSignature, StringComparison.Ordinal) Then

                Sliderset_Target.LastShapeDataAccessUtc = Date.UtcNow
                RememberLoadedShapeDataSlot(Sliderset_Target)
                Return True
            End If

            If Sliderset_Target.Unreadable_NIF AndAlso
           String.Equals(Sliderset_Target.LastShapeDataSignature, currentShapeDataSignature, StringComparison.Ordinal) Then
                Return False
            End If

            If Sliderset_Target.ShapeDataLoaded Then
                Sliderset_Target.UnloadShapeData(False)
            End If

            Sliderset_Target.Unreadable_NIF = False
            Sliderset_Target.InvalidateShapeDataLookupCache()

            Dim localOsdPaths = Sliderset_Target.OsdLocalFullPath _
            .Select(Function(pf) Correct_Path_Separator(pf)) _
            .Distinct(StringComparer.OrdinalIgnoreCase) _
            .ToArray()

            Dim externalOsdPaths = Sliderset_Target.OsdExternalFullPath _
            .Select(Function(pf) Correct_Path_Separator(pf)) _
            .Distinct(StringComparer.OrdinalIgnoreCase) _
            .ToArray()

            If localOsdPaths.Length >= 2 Then Throw New Exception("More than one osd Local file")
            If localOsdPaths.Length <> 0 Then Sliderset_Target.OSDContent_Local.Load(localOsdPaths)
            If externalOsdPaths.Length <> 0 Then Sliderset_Target.OSDContent_External.Load(externalOsdPaths)

            Sliderset_Target.NIFContent.Load_Manolo(Sliderset_Target.SourceFileFullPath)

            ' SSE: load HDT-SMP XML physics - 1) shapedata folder, 2) fallback to game output folder
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
                Dim xmlPath = IO.Path.ChangeExtension(Sliderset_Target.SourceFileFullPath, ".xml")
                Dim fallbackPath = Sliderset_Target.OutputFullPathBase & ".xml"
                Dim candidate = If(IO.File.Exists(xmlPath), xmlPath, If(IO.File.Exists(fallbackPath), fallbackPath, Nothing))
                If candidate IsNot Nothing Then
                    Dim raw = IO.File.ReadAllText(candidate, System.Text.Encoding.UTF8)
                    Sliderset_Target.PhysicsXmlContent = If(SliderSet_Class.IsValidSmpXml(raw), raw, Nothing)
                Else
                    Sliderset_Target.PhysicsXmlContent = Nothing
                End If
            End If

            Sliderset_Target.ReadhighHeel()

            Sliderset_Target.ShapeDataLoaded = True
            Sliderset_Target.InvalidateShapeDataLookupCache()
            Sliderset_Target.RebuildShapeDataLookupCache()

            If Sliderset_Target.Shapes.Any(Function(pf) pf.RelatedNifShape Is Nothing) Then Throw New Exception("Shape without Nif Shapes different")
            If Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pq) pq.RelatedOSDBlocks.Any).Count > Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Count Then Throw New Exception("Datas and OSD blocks different")
            If Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Select(Function(pf) (pf.Nombre.ToLower + pf.ParentSlider.Nombre.ToLower)).GroupBy(Function(key) key).Any(Function(g) g.Count() > 1) Then Throw New Exception("Duplicated Slider Data")

            Sliderset_Target.LastProjectFileSignature = currentProjectSignature
            Sliderset_Target.LastShapeDataSignature = currentShapeDataSignature
            Sliderset_Target.LastShapeDataAccessUtc = Date.UtcNow

            RememberLoadedShapeDataSlot(Sliderset_Target)

        Catch ex As Exception
            Sliderset_Target.UnloadShapeData(False)
            Sliderset_Target.ShapeDataLoaded = False
            If Sliderset_Target.Unreadable_Project = False Then
                Sliderset_Target.Unreadable_NIF = True
            End If
            Sliderset_Target.LastProjectFileSignature = currentProjectSignature
            Sliderset_Target.LastShapeDataSignature = currentShapeDataSignature
            If verbose Then MsgBox("Error reading shapedata from project: " + Sliderset_Target.Nombre + " " + ex.Message.ToString, vbCritical + vbOK, "Error")
            Return False
        End Try

        Return True
    End Function

    Public Shared Function Load_and_CHeck_Project(ByRef Sliderset_Target As SliderSet_Class) As Boolean
        Dim nombre As String = "(Sin Nombre)"
        If Sliderset_Target.Unreadable_Project Then Return False

        Try
            Dim Sset = Sliderset_Target.Nodo

            If Sset.Attributes("name").Value = "" Then Throw New Exception("No name") Else nombre = Sset.Attributes("name").Value
            If Sset.SelectNodes("DataFolder").Count <> 1 Then Throw New Exception("Datafolder not found or more than one")
            If Sset.SelectNodes("SourceFile").Count <> 1 Then Throw New Exception("SourceFile not found or more than one")
            If Sset.SelectNodes("OutputPath").Count <> 1 Then Throw New Exception("OutputPath not found or more than one")
            If Sset.SelectNodes("OutputFile").Count <> 1 Then Throw New Exception("OutputFile not found or more than one")
            If Sset.SelectNodes("Shape").Count < 1 Then Throw New Exception("No Shapes")
            'If Sliderset_Target.Shapes.Where(Function(pf) pf.IsExternal).Count > 1 Then Throw New Exception("More than one external shape")
            'If Sliderset_Target.Shapes.Where(Function(pf) pf.Nombre <> pf.Target).Count > 1 Then Throw New Exception("Shape name and target doesnt match")
        Catch ex As Exception
            Sliderset_Target.Unreadable_Project = True
            MsgBox("Error reading project: " + nombre + " " + ex.Message.ToString, vbCritical + vbOK, "Error")
            Return False
        End Try

        ''ESTO LO HACE LENTO PERO SIRVE PARA CONTROLLAR
        Return True
    End Function

    Public Sub Lee_Slidersets(Deep_Analize As Boolean)
        SliderSets.Clear()
        Try
            For Each Sset As XmlNode In xml.DocumentElement.SelectNodes("SliderSet")
                Dim Sliderset_target As SliderSet_Class
                Sliderset_target = YaEstan.FirstOrDefault(Function(pf) pf.Nombre.Equals(Sset.Attributes("name").Value, StringComparison.OrdinalIgnoreCase))
                If Not IsNothing(Sliderset_target) Then
                    Sliderset_target.Reload(Sset)
                Else
                    Sliderset_target = New SliderSet_Class(Sset, Me)
                End If
                Load_and_CHeck_Project(Sliderset_target)
                If Deep_Analize Then Load_and_Check_Shapedata(Sliderset_target, True)
                SliderSets.Add(Sliderset_target)
            Next

        Catch ex As Exception
            MsgBox("Error pricessing OSP file" + Me.Filename, vbCritical, "Error")
        End Try
        ' Chequeos

    End Sub

    Private Function Check_repeated(Nombre As String) As Boolean
        If SliderSets.Any(Function(pf) pf.Nombre.Equals(Nombre, StringComparison.OrdinalIgnoreCase)) Then
            MsgBox("Project name already exists, it will not be processed", vbCritical, "Duplicated")
            Return False
        End If
        Return True
    End Function
    Public Function Agrega_Proyecto(Sliderset_Source As SliderSet_Class, Nombre_Proyecto As String, Filename As String, ExcludeReference As Boolean, OverwriteShapeFiles As Boolean, Keep_Physics As Boolean, ChangeOutputDir As Boolean) As SliderSet_Class
        If Check_repeated(Nombre_Proyecto) = False Then Return Nothing
        ' Add project and update
        Dim Sliderset_Target As SliderSet_Class = AddProject(Sliderset_Source)
        If ChangeOutputDir AndAlso Sliderset_Target.OutputPathValue.Contains("ManoloCloned") = False Then
            If Sliderset_Target.OutputPathValue.Correct_Path_Separator.StartsWith("meshes\", StringComparison.OrdinalIgnoreCase) Then
                Sliderset_Target.OutputPathValue = String.Concat("meshes\ManoloCloned\", Sliderset_Target.OutputPathValue.Correct_Path_Separator.AsSpan("meshes\".Length))
            Else
                Sliderset_Target.OutputPathValue = "meshes\ManoloCloned\" + Sliderset_Target.OutputPathValue.Correct_Path_Separator
            End If
        End If


        Dim Old_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Sliderset_Source.DataFolderValue), Sliderset_Source.SourceFileValue)
        Dim Old_Osd = Old_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        ' Procesa los cambios de nombre
        Sliderset_Target.Update_Names(Nombre_Proyecto, Me.Nombre)

        ' Define High Heels
        Sliderset_Target.HighHeelHeight = Sliderset_Source.HighHeelHeight

        ' Exclude reference
        If ExcludeReference = True Then
            Dim refShape = Sliderset_Target.Shapes.FirstOrDefault(Function(pf) pf.IsReference)
            If refShape IsNot Nothing Then Sliderset_Target.RemoveShape(refShape)
        End If

        ' Saca Physics — BSClothExtraData applies to both FO4 and SSE; sidecar XML is SSE HDT-SMP only
        If Not Keep_Physics Then
            Sliderset_Target.NIFContent.RemoveBlocksOfType(Of BSClothExtraData)()
            Sliderset_Target.PhysicsXmlContent = Nothing
        End If

        ' Make Local Sliders
        Make_Sliders_Local(Sliderset_Target)
        Sliderset_Target.InvalidateAllLookupCaches()
        Sliderset_Target.Save_Shapedatas(OverwriteShapeFiles)

        ' Graba el proyecto
        Save_Pack_As(Filename, True)
        Return Sliderset_Target
    End Function

    Public Shared Function Merge_Proyecto(Sliderset_Madre As SliderSet_Class, Sliderset_Source As SliderSet_Class, ExcludeReference As Boolean, Keep_Physics As Boolean) As SliderSet_Class
        ' Add project and update
        Dim Sliderset_Target = New SliderSet_Class(Sliderset_Madre.ParentOSP.xml.ImportNode(Sliderset_Source.Nodo.Clone, True), Sliderset_Madre.ParentOSP)
        If OSP_Project_Class.Load_and_CHeck_Project(Sliderset_Target) = False OrElse OSP_Project_Class.Load_and_Check_Shapedata(Sliderset_Target, True) = False Then Return Nothing

        ' Define HighHeels
        If Sliderset_Target.HighHeelHeight <> 0 Then
            If Sliderset_Madre.IsHighHeel = 0 Or Sliderset_Madre.HighHeelHeight = Sliderset_Target.HighHeelHeight Then Sliderset_Madre.HighHeelHeight = Sliderset_Target.HighHeelHeight Else Sliderset_Madre.HighHeelHeight = Math.Max(Sliderset_Madre.HighHeelHeight, Sliderset_Target.HighHeelHeight) : MsgBox("Different High Heels setup. Higher assumed", vbInformation, "Warning")
        End If

        Dim Old_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Sliderset_Target.DataFolderValue), Sliderset_Target.SourceFileValue)
        Dim Old_Osd = Old_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        ' Procesa los cambios de nombre
        Sliderset_Target.Update_Names(Sliderset_Madre.Nombre, Sliderset_Madre.ParentOSP.Nombre)

        ' Agrega Sliders Faltantes
        For Each slid In Sliderset_Target.Sliders
            If Not Sliderset_Madre.Sliders.Any(Function(pf) pf.Nombre.Equals(slid.Nombre, StringComparison.OrdinalIgnoreCase)) Then
                Dim nodo = Sliderset_Madre.Nodo.AppendChild(Sliderset_Madre.ParentOSP.xml.ImportNode(slid.Nodo.Clone, True))
                For Each ch In nodo.SelectNodes("Data")
                    nodo.RemoveChild(ch)
                Next
                Dim new_slider As New Slider_class(nodo, Sliderset_Madre)
                Sliderset_Madre.Sliders.Add(new_slider)
            End If
        Next
        Sliderset_Target.InvalidateAllLookupCaches()

        ' Exclude reference for the source
        If ExcludeReference = True Then
            Dim refShapeSource = Sliderset_Target.Shapes.FirstOrDefault(Function(pf) pf.IsReference)
            If refShapeSource IsNot Nothing Then Sliderset_Target.RemoveShape(refShapeSource)
        End If

        ' Reference
        If Sliderset_Madre.Shapes.Any(Function(pf) pf.IsReference) Then
            For Each extsh In Sliderset_Target.Shapes.Where(Function(pf) pf.IsReference)
                For Each dat In extsh.Related_Slider_data.ToList
                    If dat.Islocal = False Then
                        For Each block In dat.RelatedOSDBlocks.ToList
                            Sliderset_Target.OSDContent_Local.Clone_block(block)
                        Next
                    End If
                    dat.Islocal = True
                    dat.TargetOsd = IO.Path.GetFileName(Sliderset_Madre.OsdLocalFullPath.First)
                Next
                extsh.Datafolder = ClearReferenceStringArray.ToList
            Next
        End If

        ' Procesa Merge
        Dim subind As Integer = 0
        For Each Shap In Sliderset_Target.Shapes

            ' Primero cambia el nombre
            Dim nombre_Viejo As String = Shap.Nombre
            Dim nombre_Nuevo As String = Sliderset_Madre.Check_Unique_Shapename(nombre_Viejo)


            If nombre_Viejo <> nombre_Nuevo Then
                For Each dat In Shap.Related_Slider_data.ToList
                    dat.Target = nombre_Nuevo
                Next

                If Shap.Target <> nombre_Nuevo Then
                    Dim nifShap = Sliderset_Target.NIFContent.NifShapes.FirstOrDefault(Function(pf) pf.Name.String.Equals(Shap.Nombre, StringComparison.OrdinalIgnoreCase))
                    If nifShap IsNot Nothing Then
                        nifShap.Name.String = nombre_Nuevo
                    End If
                End If
                Shap.Target = nombre_Nuevo
                Shap.Nombre = nombre_Nuevo
                Sliderset_Target.InvalidateAllLookupCaches()
            End If

            ' Agrega shape
            Dim pointer As XmlNode = Sliderset_Madre.Nodo.SelectNodes("Shape").Item(Sliderset_Madre.Nodo.SelectNodes("Shape").Count - 1)
            Dim new_Shape As New Shape_class(Sliderset_Madre.Nodo.InsertAfter(Sliderset_Madre.ParentOSP.xml.ImportNode(Shap.Nodo.Clone, True), pointer), Sliderset_Madre)
            Sliderset_Madre.Shapes.Add(new_Shape)
            Sliderset_Madre.InvalidateAllLookupCaches()

            ' Agrega dat
            For Each dat In Shap.Related_Slider_data.ToList
                ' Primero cambia el nombre
                Dim dat_Viejo As String = dat.TargetSlider
                Dim dat_Nuevo As String = Sliderset_Madre.Check_Unique_DataName(dat.Target.Replace(":", "_"), dat.ParentSlider.Nombre)
                If dat_Nuevo <> dat_Viejo Then
                    For Each osd In dat.RelatedLocalOSDBlocks.ToList
                        osd.BlockName = dat_Nuevo
                    Next
                    dat.TargetSlider = dat_Nuevo
                    dat.Nombre = dat_Nuevo
                    Sliderset_Target.InvalidateAllLookupCaches()
                End If

                For Each block In dat.RelatedLocalOSDBlocks
                    If Not Sliderset_Madre.OSDContent_Local.Blocks.Any(Function(pf) pf.BlockName.Equals(dat_Nuevo, StringComparison.OrdinalIgnoreCase)) Then
                        Sliderset_Madre.OSDContent_Local.Clone_block(block)
                    End If
                Next

                Dim slid As Slider_class = Sliderset_Madre.Sliders.FirstOrDefault(Function(pf) pf.Nombre.Equals(dat.ParentSlider.Nombre, StringComparison.OrdinalIgnoreCase))
                If slid Is Nothing Then Continue For
                Dim new_dat As New Slider_Data_class(slid.Nodo.AppendChild(Sliderset_Madre.ParentOSP.xml.ImportNode(dat.Nodo.Clone, True)), slid)
                slid.Datas.Add(new_dat)
                Sliderset_Madre.InvalidateAllLookupCaches()
            Next

        Next

        ' Merge Shapes
        Nifcontent_Class_Manolo.Merge_Shapes_Original(Sliderset_Madre.NIFContent, Sliderset_Target.NIFContent, Keep_Physics)
        Sliderset_Madre.InvalidateAllLookupCaches()

        ' SSE: merge HDT-SMP XML physics (take first, same behaviour as FO4 BSClothExtraData)
        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso Keep_Physics Then
            If String.IsNullOrEmpty(Sliderset_Madre.PhysicsXmlContent) Then
                Sliderset_Madre.PhysicsXmlContent = Sliderset_Target.PhysicsXmlContent
            ElseIf Not String.IsNullOrEmpty(Sliderset_Target.PhysicsXmlContent) Then
                MsgBox("The destination mesh already has physics. Physics from the merged mesh will be omitted.", vbInformation, "Merge Physics")
            End If
        End If

        ' Make Local Sliders
        Make_Sliders_Local(Sliderset_Madre)

        ' Graba OSD y NIF
        Sliderset_Madre.Save_Shapedatas(True)

        ' Graba el proyecto
        Sliderset_Madre.ParentOSP.Save_Pack_As(Sliderset_Madre.ParentOSP.Filename, True)
        Return Sliderset_Madre
    End Function

    Private Shared Sub Make_Sliders_Local(Sliderset_Target As SliderSet_Class, Optional KeepSafeReferences As Boolean = True)
        For Each extsh In Sliderset_Target.Shapes.Where(Function(pf) pf.IsExternal)
            If extsh.IsSafeReference = False OrElse KeepSafeReferences = False Then
                For Each dat In extsh.Related_Slider_data.ToList
                    If dat.Islocal = False Then
                        For Each block In dat.RelatedOSDBlocks.ToList
                            Sliderset_Target.OSDContent_Local.Clone_block(block)
                        Next
                    End If
                    dat.Islocal = True
                    dat.TargetOsd = IO.Path.GetFileName(Sliderset_Target.OsdLocalFullPath.First)
                Next
                extsh.Datafolder = OldReferenceStringArray.ToList
            End If
        Next
        Sliderset_Target.InvalidateAllLookupCaches()
    End Sub
    Public Overrides Function ToString() As String
        Return Nombre
    End Function
    Public ReadOnly Property Filename As String
        Get
            If IsNothing(xml) Then Return "Unknown"
            Return xml.BaseURI.Replace("file:///", "").Correct_Path_Separator
        End Get
    End Property

    Public ReadOnly Property Filename_WithoutPath As String
        Get
            Return IO.Path.GetFileName(Filename)
        End Get
    End Property
    Public ReadOnly Property Nombre As String
        Get
            Return IO.Path.GetFileNameWithoutExtension(Filename)
        End Get
    End Property

    Private Shared ReadOnly OldReferenceStringArray As String() = {"Old Reference"}
    Private Shared ReadOnly ClearReferenceStringArray As String() = {""}

End Class
Public Class SliderSet_Class
    Public Property Nodo As XmlNode
    Public Property OSDContent_Local As New OSD_Class(Me)
    Public Property OSDContent_External As New OSD_Class(Me)
    Public Property Unreadable_Project As Boolean = False
    Public Property Unreadable_NIF As Boolean = False
    Public Property NIFContent As New Nifcontent_Class_Manolo(Me)
    Public Property ParentOSP As OSP_Project_Class
    Public Property Shapes As New List(Of Shape_class)
    Public Property Sliders As New List(Of Slider_class)
    Public Property HighHeelHeight As Double = 0
    Public Property ShapeDataLoaded As Boolean = False
    Public Property LastShapeDataSignature As String = ""
    Public Property LastShapeDataAccessUtc As Date = Date.MinValue
    Public Property LastProjectFileSignature As String = ""
    ''' <summary>
    ''' Paths of .bgsm/.bgem files referenced by the NIF, captured after each successful load.
    ''' Persisted across UnloadShapeData so GetShapeDataSignature can detect material changes
    ''' between loads (the file-state signature includes these paths starting from the second load).
    ''' </summary>
    Public CachedMaterialPaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private _MetadataLookupCacheValid As Boolean = False
    Private _ShapeDataLookupCacheValid As Boolean = False

    Private _ShapeByTargetCache As New Dictionary(Of String, Shape_class)(StringComparer.OrdinalIgnoreCase)
    Private _ShapeHasLocalCache As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
    Private _ShapeHasExternalCache As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)

    Private _NifShapeByNameCache As New Dictionary(Of String, BSTriShape)(StringComparer.OrdinalIgnoreCase)
    Private _LocalOsdBlocksByNameCache As New Dictionary(Of String, List(Of OSD_Block_Class))(StringComparer.OrdinalIgnoreCase)
    Private _ExternalOsdBlocksByNameCache As New Dictionary(Of String, List(Of OSD_Block_Class))(StringComparer.OrdinalIgnoreCase)
    Public Property BypassDiskShapeDataLoad As Boolean = False
    ''' <summary>SSE HDT-SMP XML physics content. Nothing = no physics. Parallel to BSClothExtraData for FO4.</summary>
    Public Property PhysicsXmlContent As String = Nothing

    Public Sub InvalidateMetadataLookupCache()
        _MetadataLookupCacheValid = False
    End Sub

    Public Sub InvalidateShapeDataLookupCache()
        _ShapeDataLookupCacheValid = False
        ' C-3: Invalidate cached morph diffs so LoadMorphTargets rebuilds them on next use
        For Each shp In Shapes
            shp.MorphDiffs = Nothing
        Next
    End Sub
    Public Sub RebuildShapeDataLookupCache()
        _ShapeDataLookupCacheValid = False
        EnsureShapeDataLookupCache()
    End Sub
    Public Sub InvalidateAllLookupCaches()
        _MetadataLookupCacheValid = False
        InvalidateShapeDataLookupCache()  ' also clears MorphDiffs on all shapes
    End Sub

    Private Sub EnsureMetadataLookupCache()
        If _MetadataLookupCacheValid Then Exit Sub

        _ShapeByTargetCache = New Dictionary(Of String, Shape_class)(StringComparer.OrdinalIgnoreCase)
        _ShapeHasLocalCache = New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
        _ShapeHasExternalCache = New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)

        For Each shap In Shapes
            _ShapeByTargetCache.TryAdd(shap.Target, shap)
            _ShapeHasLocalCache.TryAdd(shap.Target, False)
            _ShapeHasExternalCache.TryAdd(shap.Target, False)
        Next

        For Each slid In Sliders
            For Each dat In slid.Datas
                If String.IsNullOrWhiteSpace(dat.Target) Then Continue For

                If dat.Islocal Then
                    If _ShapeHasLocalCache.ContainsKey(dat.Target) Then _ShapeHasLocalCache(dat.Target) = True
                Else
                    If _ShapeHasExternalCache.ContainsKey(dat.Target) Then _ShapeHasExternalCache(dat.Target) = True
                End If
            Next
        Next

        _MetadataLookupCacheValid = True
    End Sub

    Private Sub EnsureShapeDataLookupCache()
        If _ShapeDataLookupCacheValid Then Exit Sub

        _NifShapeByNameCache = New Dictionary(Of String, BSTriShape)(StringComparer.OrdinalIgnoreCase)
        _LocalOsdBlocksByNameCache = New Dictionary(Of String, List(Of OSD_Block_Class))(StringComparer.OrdinalIgnoreCase)
        _ExternalOsdBlocksByNameCache = New Dictionary(Of String, List(Of OSD_Block_Class))(StringComparer.OrdinalIgnoreCase)

        If ShapeDataLoaded = False Then Exit Sub

        If Not IsNothing(NIFContent) AndAlso Not IsNothing(NIFContent.NifShapes) Then
            For Each nifShape In NIFContent.NifShapes
                Dim tri = TryCast(nifShape, BSTriShape)
                If IsNothing(tri) Then Continue For
                If IsNothing(tri.Name) OrElse String.IsNullOrWhiteSpace(tri.Name.String) Then Continue For

                _NifShapeByNameCache.TryAdd(tri.Name.String, tri)
            Next
        End If

        If Not IsNothing(OSDContent_Local) AndAlso Not IsNothing(OSDContent_Local.Blocks) Then
            For Each block In OSDContent_Local.Blocks
                If IsNothing(block) OrElse String.IsNullOrWhiteSpace(block.BlockName) Then Continue For

                Dim value As List(Of OSD_Block_Class) = Nothing
                If Not _LocalOsdBlocksByNameCache.TryGetValue(block.BlockName, value) Then
                    value = New List(Of OSD_Block_Class)
                    _LocalOsdBlocksByNameCache.Add(block.BlockName, value)
                End If

                value.Add(block)
            Next
        End If

        If Not IsNothing(OSDContent_External) AndAlso Not IsNothing(OSDContent_External.Blocks) Then
            For Each block In OSDContent_External.Blocks
                If IsNothing(block) OrElse String.IsNullOrWhiteSpace(block.BlockName) Then Continue For

                Dim value As List(Of OSD_Block_Class) = Nothing
                If Not _ExternalOsdBlocksByNameCache.TryGetValue(block.BlockName, value) Then
                    value = New List(Of OSD_Block_Class)
                    _ExternalOsdBlocksByNameCache.Add(block.BlockName, value)
                End If

                value.Add(block)
            Next
        End If

        _ShapeDataLookupCacheValid = True
    End Sub
    Friend Function GetShapeByTargetCached(target As String) As Shape_class
        EnsureMetadataLookupCache()
        Dim result As Shape_class = Nothing
        If _ShapeByTargetCache.TryGetValue(target, result) Then Return result
        Return Nothing
    End Function

    Friend Function GetNifShapeByNameCached(name As String) As BSTriShape
        EnsureShapeDataLookupCache()
        Dim result As BSTriShape = Nothing
        If _NifShapeByNameCache.TryGetValue(name, result) Then Return result
        Return Nothing
    End Function

    Friend Function GetLocalOsdBlocksByNameCached(name As String) As IEnumerable(Of OSD_Block_Class)
        EnsureShapeDataLookupCache()
        Dim result As List(Of OSD_Block_Class) = Nothing
        If _LocalOsdBlocksByNameCache.TryGetValue(name, result) Then Return result
        Return Enumerable.Empty(Of OSD_Block_Class)()
    End Function

    Friend Function GetExternalOsdBlocksByNameCached(name As String) As IEnumerable(Of OSD_Block_Class)
        EnsureShapeDataLookupCache()
        Dim result As List(Of OSD_Block_Class) = Nothing
        If _ExternalOsdBlocksByNameCache.TryGetValue(name, result) Then Return result
        Return Enumerable.Empty(Of OSD_Block_Class)()
    End Function

    Friend Function GetShapeHasLocalCached(target As String) As Boolean
        EnsureMetadataLookupCache()
        Dim result As Boolean = False
        If _ShapeHasLocalCache.TryGetValue(target, result) Then Return result
        Return False
    End Function

    Friend Function GetShapeHasExternalCached(target As String) As Boolean
        EnsureMetadataLookupCache()
        Dim result As Boolean = False
        If _ShapeHasExternalCache.TryGetValue(target, result) Then Return result
        Return False
    End Function
    Private Shared Function BuildFileStateSignature(fullPath As String) As String
        Dim normalized As String = ""
        If Not String.IsNullOrWhiteSpace(fullPath) Then normalized = Correct_Path_Separator(fullPath)

        If String.IsNullOrWhiteSpace(normalized) Then Return "|0|-1|0"
        If IO.File.Exists(normalized) = False Then Return normalized & "|0|-1|0"

        Dim info As New IO.FileInfo(normalized)
        Return normalized & "|1|" &
           info.Length.ToString(Global.System.Globalization.CultureInfo.InvariantCulture) & "|" &
           info.LastWriteTimeUtc.Ticks.ToString(Global.System.Globalization.CultureInfo.InvariantCulture)
    End Function

    Public Function GetProjectFileSignature() As String
        If IsNothing(ParentOSP) Then Return ""
        Return BuildFileStateSignature(ParentOSP.Filename)
    End Function

    Public Function GetShapeDataSignature() As String
        Dim files As New List(Of String) From {SourceFileFullPath}

        files.AddRange(OsdLocalFullPath)
        files.AddRange(OsdExternalFullPath)

        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                files.Add(IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.ParentOSP.Nombre), Me.Nombre + ".hht"))
                files.Add(Me.OutputFullPathBase & ".txt")
            Case Config_App.Game_Enum.Skyrim
                files.Add(IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.ParentOSP.Nombre), Me.Nombre + ".hht"))
                files.Add(IO.Path.ChangeExtension(SourceFileFullPath, ".xml"))
                files.Add(OutputFullPathBase & ".xml")
        End Select

        ' Include .bgsm/.bgem paths captured at last load time so that material edits on
        ' disk invalidate the cache without requiring a NIF change (Finding 4).
        ' CachedMaterialPaths is empty on the very first load; from the second load onward
        ' it reflects the materials the NIF referenced when it was last deserialized.
        files.AddRange(CachedMaterialPaths)

        Return String.Join(vbLf,
        files _
        .Where(Function(pf) Not String.IsNullOrWhiteSpace(pf)) _
        .Select(Function(pf) Correct_Path_Separator(pf)) _
        .Distinct(StringComparer.OrdinalIgnoreCase) _
        .OrderBy(Function(pf) pf.ToUpperInvariant()) _
        .Select(Function(pf) BuildFileStateSignature(pf)))
    End Function

    Public Sub MarkShapeDataAsLoaded()
        ' Capture material file paths from the newly loaded NIF so that future calls to
        ' GetShapeDataSignature() can detect .bgsm/.bgem changes without reloading the NIF first.
        CachedMaterialPaths.Clear()
        If NIFContent IsNot Nothing Then
            For Each rm In NIFContent.BaseMaterials.Values
                If rm IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(rm.path) Then
                    CachedMaterialPaths.Add(rm.path)
                End If
            Next
        End If
        LastShapeDataSignature = GetShapeDataSignature()
        ShapeDataLoaded = True
        LastShapeDataAccessUtc = Date.UtcNow
        OSP_Project_Class.RememberLoadedShapeDataSlot(Me)
    End Sub

    Public Sub UnloadShapeData(Optional clearFileSignatures As Boolean = False)
        OSP_Project_Class.ForgetLoadedShapeDataSlot(Me)
        InvalidateShapeDataLookupCache()

        OSDContent_Local = New OSD_Class(Me)
        OSDContent_External = New OSD_Class(Me)
        NIFContent = New Nifcontent_Class_Manolo(Me)
        PhysicsXmlContent = Nothing
        HighHeelHeight = 0
        ShapeDataLoaded = False
        InvalidateAllLookupCaches()

        If clearFileSignatures Then
            LastShapeDataSignature = ""
            Unreadable_NIF = False
        End If
    End Sub

    Public ReadOnly Property IsHighHeel As Boolean
        Get
            Return HighHeelHeight <> 0
        End Get
    End Property

    Public Sub SetPreset(Preset As SlidersPreset_Class, Weight As Config_App.SliderSize)
        For Each slid In Sliders
            slid.Current_Setting = slid.Default_Setting(Weight)
            If IsNothing(Preset) Then Continue For

            Dim matches = Preset.Sliders.
            Where(Function(pf) pf.Name.Equals(slid.Nombre, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(pf) pf.Size).
            ToList()

            If matches.Count = 0 Then Continue For

            If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Dim presetDefault = matches.FirstOrDefault(Function(pf) pf.Size = Config_App.SliderSize.Default)
                Dim presetBig = matches.FirstOrDefault(Function(pf) pf.Size = Config_App.SliderSize.Big)

                If presetDefault IsNot Nothing Then
                    slid.Current_Setting = presetDefault.Value
                ElseIf presetBig IsNot Nothing Then
                    slid.Current_Setting = presetBig.Value
                End If

                Continue For
            End If

            For Each sli In matches
                If sli.Size = Config_App.SliderSize.Small Then
                    If Weight = Config_App.SliderSize.Small Then slid.Current_Setting = sli.Value
                Else
                    If Weight <> Config_App.SliderSize.Small Then slid.Current_Setting = sli.Value
                End If
            Next
        Next
    End Sub
    Public ReadOnly Property HasPhysics As Boolean
        Get
            ' SSE can have BSClothExtraData (vanilla Havok) AND/OR sidecar HDT-SMP XML
            If Not String.IsNullOrEmpty(PhysicsXmlContent) Then Return True
            Return Me.Shapes.Any(Function(pf) pf.HasPhysics)
        End Get
    End Property

    Sub New(ByRef el As XmlNode, ByRef OSP As OSP_Project_Class)
        Nodo = el
        ParentOSP = OSP
        Lee_SlidersAndShapes()
    End Sub
    Sub New(ByRef OSP As OSP_Project_Class)
        Try
            ParentOSP = OSP
            Dim sliderSetNode As XmlElement = OSP.xml.CreateElement("SliderSet")
            sliderSetNode.SetAttribute("name", "Unknown")

            ' Crear y agregar <DataFolder>
            Dim dataFolderNode As XmlElement = OSP.xml.CreateElement("DataFolder")
            dataFolderNode.InnerText = "Unknown"
            sliderSetNode.AppendChild(dataFolderNode)

            ' Crear y agregar <SourceFile>
            Dim sourceFileNode As XmlElement = OSP.xml.CreateElement("SourceFile")
            sourceFileNode.InnerText = "Unknown.nif"
            sliderSetNode.AppendChild(sourceFileNode)

            ' Crear y agregar <OutputPath>
            Dim outputPathNode As XmlElement = OSP.xml.CreateElement("OutputPath")
            outputPathNode.InnerText = "Meshes\Unknown"
            sliderSetNode.AppendChild(outputPathNode)

            ' Crear y agregar <OutputFile> con atributos
            Dim outputFileNode As XmlElement = OSP.xml.CreateElement("OutputFile")
            outputFileNode.SetAttribute("GenWeights", "false")
            outputFileNode.SetAttribute("PreventMorphFile", "false")
            outputFileNode.SetAttribute("KeepZappedShapes", "false")
            outputFileNode.InnerText = "Unknown"
            sliderSetNode.AppendChild(outputFileNode)
            Nodo = sliderSetNode
            OSP.xml.DocumentElement.AppendChild(sliderSetNode)

        Catch ex As Exception
            MsgBox("Error Creating Sliderset", "Error")
        End Try
    End Sub

    Sub Reload(DeepAnalize As Boolean)
        Unreadable_NIF = False
        Unreadable_Project = False
        Me.ParentOSP.Reload(DeepAnalize)
    End Sub
    Sub Reload(el As XmlNode)
        Clear()
        Nodo = el
        Lee_SlidersAndShapes()
    End Sub
    Sub Clear()
        UnloadShapeData(True)
        Shapes.Clear()
        Sliders.Clear()
        InvalidateAllLookupCaches()
    End Sub

    Public Sub Lee_SlidersAndShapes()
        Shapes = Nodo.SelectNodes("Shape").Cast(Of XmlNode)().Select(Function(shap) New Shape_class(shap, Me)).ToList
        Sliders = Nodo.SelectNodes("Slider").Cast(Of XmlNode)().Select(Function(slid) New Slider_class(slid, Me)).ToList
        LastProjectFileSignature = GetProjectFileSignature()
        InvalidateMetadataLookupCache()
    End Sub
    Public Shared Function ReadHighHeelTXT(archivoName As String) As Double
        Dim lin As String
        Using archivo = New StreamReader(archivoName)
            lin = archivo.ReadLine
        End Using
        If lin.Contains("="c) = False Then Return 0
        Dim sep = lin.Split("=")
        If sep.Length <> 2 Then Return 0
        Return Double.Parse(sep(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
    End Function
    Public Sub ReadhighHeel()
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                Dim hh0 As String = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.ParentOSP.Nombre), Me.Nombre + ".hht")
                Dim hh1 As String = Me.OutputFullPathBase.Correct_Path_Separator & ".nif"
                'aDim hh1b As String = IO.Path.Combine(Directorios.HighHeels_Plugin, Me.OutputFileValue + ".json")
                Dim hh2 As String = Me.OutputFullPathBase & ".txt"

                If IO.File.Exists(hh0) Then HighHeelHeight = ReadHighHeelTXT(hh0) : Exit Sub
                If FilesDictionary_class.HighHeels_Plugin_Value.HighHeelsKeys.Any(Function(pf) hh1.EndsWith(pf.Key, StringComparison.CurrentCultureIgnoreCase)) Then
                    HighHeelHeight = FilesDictionary_class.HighHeels_Plugin_Value.HighHeelsKeys.OrderByDescending(Function(pf) pf.Key.Length).First(Function(pf) hh1.EndsWith(pf.Key, StringComparison.CurrentCultureIgnoreCase)).Value
                    Exit Sub
                End If
                If IO.File.Exists(hh2) Then HighHeelHeight = ReadHighHeelTXT(hh2) : Exit Sub

                HighHeelHeight = 0

            Case Config_App.Game_Enum.Skyrim
                Dim maxhh = 0
                If Not IsNothing(Me.NIFContent) Then
                    For Each shap In Me.NIFContent.GetShapes
                        For Each edr In shap.ExtraDataList.References
                            Dim ed = TryCast(Me.NIFContent.Blocks(edr.Index), NiFloatExtraData)
                            If Not IsNothing(ed) Then
                                If ed.Name.String = "HH_OFFSET" Then
                                    If ed.FloatData > maxhh Then maxhh = ed.FloatData
                                End If
                            End If
                        Next

                    Next
                End If

                Dim hh0 As String = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.ParentOSP.Nombre), Me.Nombre + ".hht")

                If IO.File.Exists(hh0) = False Then
                    HighHeelHeight = maxhh
                    Exit Sub
                End If

                Dim lin As String = ""
                Using archivo As New StreamReader(hh0)
                    lin = archivo.ReadLine()
                End Using

                If String.IsNullOrWhiteSpace(lin) OrElse lin.Contains("="c) = False Then
                    HighHeelHeight = maxhh
                    Exit Sub
                End If

                Dim sep = lin.Split("="c)
                If sep.Length <> 2 Then
                    HighHeelHeight = maxhh
                    Exit Sub
                End If

                HighHeelHeight = Double.Parse(sep(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
        End Select

    End Sub
    Public Function Multisize() As Boolean
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then Return False
        If Config_App.Current.Settings_Build.IgnoreWeightsFlags = False Then Return Me.GenWeights
        Return Config_App.Current.Settings_Build.ForceWeights
    End Function

    Public Sub SaveHighHeelBuild(Optional NifSource As Nifcontent_Class_Manolo = Nothing)
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                Dim hhfile = OutputFullPathBase.Replace(".nif", "txt", StringComparison.OrdinalIgnoreCase)
                If hhfile.EndsWith(".txt") = False Then hhfile += ".txt"
                If HighHeelHeight = 0 Then
                    If IO.File.Exists(hhfile) Then IO.File.Delete(hhfile)
                Else
                    If Config_App.Current.Settings_Build.SaveHHS Then
                        Dim writer = IO.File.CreateText(hhfile)
                        writer.WriteLine("Height=" + HighHeelHeight.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        writer.Flush()
                        writer.Close()
                    End If
                End If
            Case Config_App.Game_Enum.Skyrim
                For sizecount = 0 To IIf(Multisize, 1, 0)
                    Dim fil = OutputFullPathBase + If(Multisize(), "_" + sizecount.ToString, "") + ".nif"
                    Dim NIF As New Nifcontent_Class_Manolo
                    If Config_App.Current.Settings_Build.SaveHHS OrElse Config_App.Current.Settings_Build.DeleteUnbuilt Then
                        If IsNothing(NifSource) Then
                            NIF.Load(fil)
                        Else
                            NIF = NifSource
                        End If
                        For Each shap In NIF.GetShapes.ToList
                            For Each edr In shap.ExtraDataList.References.ToList
                                Dim ed = TryCast(NIF.Blocks(edr.Index), NiFloatExtraData)
                                If Not IsNothing(ed) Then
                                    If ed.Name.String = "HH_OFFSET" Then
                                        shap.ExtraDataList.RemoveBlockRef(edr.Index)
                                        NIF.RemoveBlock(ed)
                                    End If
                                End If
                            Next
                        Next
                        NIF.RemoveUnreferencedBlocks()
                    End If
                    If HighHeelHeight > 0 AndAlso Config_App.Current.Settings_Build.SaveHHS Then
                        For Each firs In NIF.GetShapes
                            If Not IsNothing(firs) Then
                                Try
                                    Dim triExtraData As New NiFloatExtraData()
                                    Dim nam = New NiStringRef("HH_OFFSET")
                                    triExtraData.Name = nam
                                    triExtraData.FloatData = CSng(HighHeelHeight)
                                    Dim extraDataId As UInteger = NIF.AddBlock(triExtraData)
                                    firs.ExtraDataList.AddBlockRef(extraDataId)
                                    If IsNothing(NifSource) Then
                                        NIF.Save_As_Manolo(fil, True)
                                    End If
                                    Exit For
                                Catch ex As Exception
                                    Debugger.Break()
                                End Try
                            End If
                        Next
                    End If
                Next
        End Select

    End Sub
    Public Sub SaveHighHeel(filename As String, Overwrite As Boolean)
        If IO.File.Exists(filename) And Overwrite = False Then Throw New Exception
        If HighHeelHeight = 0 Then
            If IO.File.Exists(filename) Then IO.File.Delete(filename)
        Else
            Using writer = IO.File.CreateText(filename)
                writer.WriteLine("Height=" + HighHeelHeight.ToString(System.Globalization.CultureInfo.InvariantCulture))
                writer.Flush()
            End Using
        End If

    End Sub

    Public Sub Update_Names(Nombre As String, Pack As String)
        ' Reemplaza nombres
        Dim OldNif = Me.SourceFileFullPath
        Dim Oldosd = ""
        If Me.OsdLocalFullPath.Any Then Oldosd = Me.OsdLocalFullPath.First

        ' Carga OSD y NIF
        OSP_Project_Class.Load_and_Check_Shapedata(Me, True)

        Me.Nombre = Nombre
        Me.DataFolderValue = Pack.ToString
        Me.SourceFileValue = Nombre + ".nif"
        Dim New_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Pack), Nombre + ".nif")
        Dim New_Osd = New_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)
        If IO.Directory.Exists(IO.Path.GetDirectoryName(New_Nif)) = False Then
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(New_Nif))

        End If


        ' Reemplaza Data OSD References
        For Each slid In Sliders
            For Each dat In slid.Datas
                If dat.Islocal AndAlso dat.TargetOsd.Equals(IO.Path.GetFileName(Oldosd), StringComparison.OrdinalIgnoreCase) Then
                    dat.TargetOsd = IO.Path.GetFileName(New_Osd)
                End If
            Next
        Next
        InvalidateMetadataLookupCache()
    End Sub

    Public Function Check_Unique_Shapename(Prueba As String) As String
        Dim index = 0
        Dim Nuevo As String = Prueba
        While Me.Shapes.Any(Function(pf) pf.Nombre.Equals(Nuevo, StringComparison.OrdinalIgnoreCase)) Or Me.Shapes.Any(Function(pf) pf.Target.Equals(Nuevo, StringComparison.OrdinalIgnoreCase))
            Nuevo = Prueba + "_" + index.ToString
            index += 1
        End While
        Return Nuevo
    End Function
    Public Function Check_Unique_DataName(Prueba As String, slidername As String) As String
        Dim index = 0
        Dim Nuevo As String = Prueba
        While Me.Sliders.SelectMany(Function(pf) pf.Datas).Any(Function(pf) pf.TargetSlider.Equals(Nuevo + slidername, StringComparison.OrdinalIgnoreCase))
            Nuevo = Prueba + "_" + index.ToString
            index += 1
        End While
        Return Nuevo + slidername
    End Function


    Public Property Nombre As String
        Get
            Return Nodo.Attributes("name").Value
        End Get
        Set(value As String)
            Nodo.Attributes("name").Value = value
        End Set
    End Property
    Public ReadOnly Property OutputFile As XmlNode
        Get
            Return Nodo.SelectNodes("OutputFile")(0)
        End Get

    End Property
    Public ReadOnly Property OutputPath As XmlNode
        Get
            Return Nodo.SelectNodes("OutputPath")(0)
        End Get
    End Property
    Public ReadOnly Property SourceFile As XmlNode
        Get
            Return Nodo.SelectNodes("SourceFile")(0)
        End Get
    End Property
    Public ReadOnly Property DataFolder As XmlNode
        Get
            Return Nodo.SelectNodes("DataFolder")(0)
        End Get
    End Property
    Public Property OutputFileValue As String
        Get
            Return OutputFile.FirstChild.Value
        End Get
        Set(value As String)
            Nodo.SelectNodes("OutputFile")(0).FirstChild.Value = value
        End Set
    End Property
    Public ReadOnly Property Description As XmlNode
        Get
            If Nodo.SelectNodes("Description").Count = 0 Then Return Nothing
            Return Nodo.SelectNodes("Description")(0)
        End Get

    End Property
    Public Property DescriptionValue As String
        Get
            If IsNothing(Description) Then Return ""
            Return Description.InnerText
        End Get
        Set(value As String)
            If IsNothing(Description) Then
                Dim nue As XmlNode = Me.ParentOSP.xml.CreateElement("Description")
                Nodo.AppendChild(nue)
            End If
            Nodo.SelectNodes("Description")(0).InnerText = value
        End Set
    End Property

    Public Property KeepZappedShapes As Boolean
        Get
            If IsNothing(Nodo.Attributes("KeepZappedShapes")) Then Return False
            Return Nodo.Attributes("KeepZappedShapes").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If IsNothing(Nodo.Attributes("KeepZappedShapes")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("KeepZappedShapes")
                    attr.Value = "false"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("KeepZappedShapes").Value = "false"
                End If
            Else
                If IsNothing(Nodo.Attributes("KeepZappedShapes")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("KeepZappedShapes")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("KeepZappedShapes").Value = "true"
                End If
            End If
        End Set
    End Property
    Public Property PreventMorphFile As Boolean
        Get
            If IsNothing(Nodo.Attributes("PreventMorphFile")) Then Return False
            Return Nodo.Attributes("PreventMorphFile").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If IsNothing(Nodo.Attributes("PreventMorphFile")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("PreventMorphFile")
                    attr.Value = "false"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("PreventMorphFile").Value = "false"
                End If
            Else
                If IsNothing(Nodo.Attributes("PreventMorphFile")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("PreventMorphFile")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("PreventMorphFile").Value = "true"
                End If
            End If

        End Set
    End Property

    Public Property GenWeights As Boolean
        Get
            If IsNothing(OutputFile.Attributes("GenWeights")) Then Return False
            Return OutputFile.Attributes("GenWeights").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If IsNothing(OutputFile.Attributes("GenWeights")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("GenWeights")
                    attr.Value = "false"
                    OutputFile.Attributes.Append(attr)
                Else
                    OutputFile.Attributes("GenWeights").Value = "false"
                End If
            Else
                If IsNothing(OutputFile.Attributes("GenWeights")) Then
                    Dim attr As XmlAttribute = Me.ParentOSP.xml.CreateAttribute("GenWeights")
                    attr.Value = "true"
                    OutputFile.Attributes.Append(attr)
                Else
                    OutputFile.Attributes("GenWeights").Value = "true"
                End If
            End If

        End Set
    End Property
    Public Property OutputPathValue As String
        Get
            Return OutputPath.FirstChild.Value
        End Get
        Set(value As String)
            OutputPath.FirstChild.Value = value
        End Set
    End Property
    Public Property SourceFileValue As String
        Get
            Return SourceFile.FirstChild.Value
        End Get
        Set(value As String)
            Nodo.SelectNodes("SourceFile")(0).FirstChild.Value = value
        End Set
    End Property
    Public ReadOnly Property SourceFileFullPath As String
        Get
            Return IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, DataFolderValue), IO.Path.GetFileName(SourceFileValue))
        End Get
    End Property
    ''' <summary>Base output path (no extension): Fallout4data\OutputPath\OutputFile</summary>
    Public ReadOnly Property OutputFullPathBase As String
        Get
            Return IO.Path.Combine(Directorios.Fallout4data, OutputPathValue, OutputFileValue)
        End Get
    End Property

    Public Property DataFolderValue As String
        Get
            Return DataFolder.FirstChild.Value
        End Get
        Set(value As String)
            Nodo.SelectNodes("DataFolder")(0).FirstChild.Value = value
        End Set

    End Property

    Public ReadOnly Property OsdLocalFullPath As IEnumerable(Of String)
        Get
            Dim result = Sliders.SelectMany(Function(pf) pf.Datas.Where(Function(pq) pq.Islocal = True).Select(Function(pq) IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, DataFolderValue), IO.Path.GetFileName(pq.TargetOsd.ToString)))).Distinct
            If Not result.Any() Then
                Return {IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, DataFolderValue), SourceFileValue.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase))}
            End If
            If result.Count = 1 AndAlso result(0) = IO.Path.Combine(Directorios.ShapedataRoot, DataFolderValue) Then
                Return {IO.Path.Combine(result(0), SourceFileValue.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase))}
            End If
            Return result
        End Get
    End Property
    Public ReadOnly Property OsdExternalFullPath As IEnumerable(Of String)
        Get
            Return Sliders.SelectMany(Function(pf) pf.Datas.Where(Function(pq) pq.Islocal = False).Select(Function(pq) IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, pq.RelatedShape.Datafolder.Last), IO.Path.GetFileName(pq.TargetOsd.ToString)))).Distinct
        End Get
    End Property

    Public Sub Remove_DataShapeFiles()
        Dim Legacy_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.DataFolderValue), Me.SourceFileValue)
        Dim Legacy_Osd = Legacy_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)
        Dim Legacy_htt = Legacy_Nif.Replace(".nif", ".hht", StringComparison.OrdinalIgnoreCase)

        Dim Built_Nif = Me.OutputFullPathBase & ".nif"
        Dim Built_htt = Legacy_Nif.Replace(".nif", ".txt", StringComparison.OrdinalIgnoreCase)
        Dim Built_Tri = Legacy_Nif.Replace(".nif", ".tri", StringComparison.OrdinalIgnoreCase)

        If IO.File.Exists(Legacy_Nif) Then IO.File.Delete(Legacy_Nif)
        If IO.File.Exists(Legacy_Osd) Then IO.File.Delete(Legacy_Osd)
        If IO.File.Exists(Legacy_htt) Then IO.File.Delete(Legacy_htt)

        If Config_App.Current.Settings_Build.DeleteWithProject Then
            If IO.File.Exists(Built_Nif) Then IO.File.Delete(Built_Nif)
            If IO.File.Exists(Built_htt) Then IO.File.Delete(Built_htt)
            If IO.File.Exists(Built_Tri) Then IO.File.Delete(Built_Tri)
        End If


        Dim Dir = IO.Path.GetDirectoryName(Legacy_Nif)
        If IO.Directory.Exists(Dir) Then
            If IO.Directory.GetFileSystemEntries(Dir).Length = 0 Then
                IO.Directory.Delete(Dir)
            End If
        End If
        Dir = IO.Path.GetDirectoryName(Built_Nif)
        If IO.Directory.Exists(Dir) Then
            If IO.Directory.GetFileSystemEntries(Dir).Length = 0 Then
                IO.Directory.Delete(Dir)
            End If
        End If
    End Sub


    Public Sub RemoveShape(Shape As Shape_class)
        If Not IsNothing(Shape.RelatedNifShape) Then NIFContent.RemoveShape_Manolo(Shape.RelatedNifShape)
        For Each slid In Sliders
            For Each dat In slid.Datas.ToList
                If dat.RelatedShape Is Shape Then
                    For Each block In dat.RelatedLocalOSDBlocks.ToList
                        OSDContent_Local.Blocks.Remove(block)
                    Next
                    slid.Nodo.RemoveChild(dat.Nodo)
                    slid.Datas.Remove(dat)
                End If
            Next
        Next
        Nodo.RemoveChild(Shape.Nodo)
        Shapes.Remove(Shape)
        InvalidateAllLookupCaches()
    End Sub
    ''' <summary>
    ''' Returns True if the string is well-formed XML whose root element is a known
    ''' HDT-SMP physics config root: &lt;system&gt; (classic SMP) or &lt;hdt-smp&gt; (SMP 3.x).
    ''' </summary>
    Public Shared Function IsValidSmpXml(content As String) As Boolean
        If String.IsNullOrWhiteSpace(content) Then Return False
        Try
            Dim doc As New XmlDocument()
            doc.LoadXml(content)
            Dim root = doc.DocumentElement
            If root Is Nothing Then Return False
            Return root.LocalName.Equals("system", StringComparison.OrdinalIgnoreCase) OrElse
                   root.LocalName.Equals("hdt-smp", StringComparison.OrdinalIgnoreCase)
        Catch ex As XmlException
            Return False
        End Try
    End Function

    Public Sub Save_Shapedatas(OverwriteShapeFiles As Boolean)
        Dim New_Nif = SourceFileFullPath
        Dim New_Osd = New_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        OSDContent_Local.Save_As(New_Osd, OverwriteShapeFiles)
        NIFContent.Save_As_Manolo(New_Nif, OverwriteShapeFiles)
        SaveHighHeel(New_Nif.Replace(".nif", ".hht", StringComparison.OrdinalIgnoreCase), OverwriteShapeFiles)

        ' SSE: save or delete HDT-SMP XML physics alongside NIF
        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
            Dim xmlPath = IO.Path.ChangeExtension(New_Nif, ".xml")
            If Not String.IsNullOrEmpty(PhysicsXmlContent) Then
                If OverwriteShapeFiles OrElse Not IO.File.Exists(xmlPath) Then
                    IO.File.WriteAllText(xmlPath, PhysicsXmlContent, System.Text.Encoding.UTF8)
                End If
            ElseIf IO.File.Exists(xmlPath) Then
                IO.File.Delete(xmlPath)
            End If
        End If

        ShapeDataLoaded = False
        LastShapeDataSignature = ""
        Unreadable_NIF = False
    End Sub

End Class
Public Class Shape_class
    Public Property Nodo As XmlNode
    Public Property ParentSliderSet As SliderSet_Class
    Public Property MorphDiffs() As Dictionary(Of String, List(Of MorphData))

    Sub New(ByRef el As XmlNode, ByRef Sliderset As SliderSet_Class)
        Nodo = el
        ParentSliderSet = Sliderset
    End Sub
    Sub New(Name As String, ByRef Sliderset As SliderSet_Class)
        Dim shapeNode As XmlElement = Sliderset.ParentOSP.xml.CreateElement("Shape")
        shapeNode.SetAttribute("target", Name)
        shapeNode.InnerText = Name
        Nodo = shapeNode
        Sliderset.Nodo.AppendChild(Nodo)
        ParentSliderSet = Sliderset
    End Sub

    Public ReadOnly Property HasPhysics As Boolean
        Get
            ' SSE can have BSClothExtraData (vanilla Havok) AND/OR sidecar HDT-SMP XML
            If Not String.IsNullOrEmpty(ParentSliderSet.PhysicsXmlContent) Then Return True
            If IsNothing(ParentSliderSet.NIFContent) Then Return False
            If IsNothing(ParentSliderSet.NIFContent.Blocks) Then Return False
            Return ParentSliderSet.NIFContent.Blocks.Any(Function(pf) pf.GetType Is GetType(BSClothExtraData))
        End Get
    End Property

    Public Property Datafolder As List(Of String)
        Get
            If IsNothing(Nodo.Attributes("DataFolder")) Then Return stringArray.ToList
            Return Nodo.Attributes("DataFolder").Value.Split(";").ToList
        End Get
        Set(value As List(Of String))
            If value.Count = 0 Then
                If Not IsNothing(Nodo.Attributes("DataFolder")) Then Nodo.Attributes.Remove(Nodo.Attributes("DataFolder"))
            Else
                If IsNothing(Nodo.Attributes("DataFolder")) Then
                    Dim attr As XmlAttribute = ParentSliderSet.ParentOSP.xml.CreateAttribute("DataFolder")
                    attr.Value = String.Join(";", value)
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("DataFolder").Value = String.Join(";", value)
                End If
            End If
        End Set
    End Property
    Public ReadOnly Property IsExternal As Boolean
        Get
            Return (Datafolder.Count > 1 OrElse (Datafolder(0) <> ""))
        End Get
    End Property

    Public ReadOnly Property IsSafeReference As Boolean
        Get
            If Datafolder.First.Equals("CBBE", StringComparison.OrdinalIgnoreCase) Then Return True
            If Datafolder.First.Equals("Body - References", StringComparison.OrdinalIgnoreCase) Then Return True
            Return False
        End Get
    End Property
    Public ReadOnly Property IsReference As Boolean
        Get
            If IsExternal AndAlso ParentSliderSet.Shapes.Where(Function(pf) pf.IsExternal).Count = 1 Then Return True
            Return IsExternal And HasExternalSliders
        End Get
    End Property

    Public ReadOnly Property IsSkinned As Boolean
        Get
            If IsNothing(RelatedNifShape) Then Return False
            Return RelatedNifShape.IsSkinned
        End Get
    End Property

    Public ReadOnly Property RelatedNifShape As BSTriShape
        Get
            Return Me.ParentSliderSet.GetNifShapeByNameCached(Me.Nombre)
        End Get
    End Property



    Public Property ShowTexture As Boolean = True
    Public Property ShowMask As Boolean = False
    Public Property ShowWeight As Boolean = False
    Public Property ShowVertexColor As Boolean = False
    Public Property RenderHide As Boolean = False
    Public Property ApplyZaps As Boolean = True
    Public Property Wireframe As Boolean = False
    Public Property Wirecolor As Color = Color.LightGray ' Color Malla
    Public Property WireAlpha As Single = 0.5 ' Alpha Malla
    Public Property TintColor As Color = Color.White ' TINTE
    Public Property MaskedVertices As New HashSet(Of Integer)()

    Public ReadOnly Property RelatedMaterial As Nifcontent_Class_Manolo.RelatedMaterial_Class
        Get
            If IsNothing(RelatedNifShape) Then Return Nothing
            Return Me.ParentSliderSet.NIFContent.BaseMaterials(RelatedNifShape.Name.String)

        End Get
    End Property

    Public ReadOnly Property RelatedNifShader As INiShader
        Get
            Return Me.ParentSliderSet.NIFContent.GetShader(RelatedNifShape)
        End Get
    End Property

    Public ReadOnly Property RelatedNifSkin As INiSkin
        Get
            If IsNothing(RelatedNifShape) Then Return Nothing
            If IsNothing(RelatedNifShape.SkinInstanceRef) OrElse RelatedNifShape.SkinInstanceRef.Index = -1 Then Return Nothing

            Return TryCast(Me.ParentSliderSet.NIFContent.Blocks(RelatedNifShape.SkinInstanceRef.Index), INiSkin)
        End Get
    End Property

    Public ReadOnly Property RelatedBoneTransforms As List(Of Transform_Class)
        Get
            If IsNothing(RelatedNifSkin) Then Return New List(Of Transform_Class)
            Dim regreso As New List(Of Transform_Class)
            Select Case RelatedNifSkin.GetType
                Case GetType(BSSkin_Instance)
                    Dim resu = Me.ParentSliderSet.NIFContent.Blocks(TryCast(RelatedNifSkin, BSSkin_Instance).Data.Index)
                    Dim bl = TryCast(resu, BSSkin_BoneData).BoneList
                    For Each bon In bl
                        regreso.Add(New Transform_Class(bon))
                    Next
                Case GetType(BSDismemberSkinInstance)
                    Dim resu = Me.ParentSliderSet.NIFContent.Blocks(TryCast(RelatedNifSkin, BSDismemberSkinInstance).Data.Index)
                    Dim bl = TryCast(resu, NiSkinData).BoneList
                    For Each bon In bl
                        regreso.Add(New Transform_Class(bon))
                    Next
                Case GetType(NiSkinInstance)
                    Dim resu = Me.ParentSliderSet.NIFContent.Blocks(TryCast(RelatedNifSkin, NiSkinInstance).Data.Index)
                    Dim bl = TryCast(resu, NiSkinData).BoneList
                    For Each bon In bl
                        regreso.Add(New Transform_Class(bon))
                    Next
                Case Else
                    Throw New Exception
            End Select
            Return regreso
        End Get
    End Property

    Public ReadOnly Property RelatedBones As List(Of NiNode)
        Get
            If IsNothing(RelatedNifSkin) Then Return New List(Of NiNode)
            Return RelatedNifSkin.Bones.Indices.Select(Function(pf) CType(Me.ParentSliderSet.NIFContent.Blocks(pf), NiNode)).ToList
            Return New List(Of NiNode)
        End Get
    End Property
    Public ReadOnly Property Related_Slider_data As IEnumerable(Of Slider_Data_class)
        Get
            Return Me.ParentSliderSet.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.RelatedShape Is Me)
        End Get
    End Property
    Public ReadOnly Property Related_Sliders As IEnumerable(Of Slider_class)
        Get
            Return Me.ParentSliderSet.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.RelatedShape Is Me).Select(Function(pf) pf.ParentSlider).Distinct
        End Get
    End Property

    Public ReadOnly Property HasExternalSliders As Boolean
        Get
            Return Me.ParentSliderSet.GetShapeHasExternalCached(Me.Target)
        End Get
    End Property
    Public ReadOnly Property HasLocalSliders As Boolean
        Get
            Return Me.ParentSliderSet.GetShapeHasLocalCached(Me.Target)
        End Get
    End Property
    Public ReadOnly Property HasMixedSliders As Boolean
        Get
            Return HasLocalSliders And HasExternalSliders
        End Get
    End Property
    Public Property Target As String
        Get
            Return Nodo.Attributes("target").Value
        End Get
        Set(value As String)
            Nodo.Attributes("target").Value = value
        End Set
    End Property
    Public Property Nombre As String
        Get
            Return Nodo.FirstChild.Value
        End Get
        Set(value As String)
            Nodo.FirstChild.Value = value
        End Set
    End Property
    Private Shared ReadOnly stringArray As String() = {""}
End Class
Public Class Slider_class
    Public Property Nodo As XmlNode
    Public Property ParentSliderSet As SliderSet_Class

    Public Property Datas As New List(Of Slider_Data_class)

    Sub New(ByRef el As XmlNode, ByRef Sliderset As SliderSet_Class)
        Nodo = el
        ParentSliderSet = Sliderset
        Lee_Data()
    End Sub
    Sub New(Name As String, ByRef Sliderset As SliderSet_Class, tipo As TriFile.MorphType)
        Dim SliderNode As XmlElement = Sliderset.ParentOSP.xml.CreateElement("Slider")
        SliderNode.SetAttribute("invert", "false")
        SliderNode.SetAttribute("default", "0")
        SliderNode.SetAttribute("name", Name)
        If tipo = TriFile.MorphType.MORPHTYPE_UV Then Me.IsUV = True
        Nodo = SliderNode
        Sliderset.Nodo.AppendChild(Nodo)
        ParentSliderSet = Sliderset
    End Sub

    Public Sub Lee_Data()
        Datas.Clear()
        Dim newdat As Slider_Data_class
        For Each Dat As XmlNode In Nodo.SelectNodes("Data")
            newdat = New Slider_Data_class(Dat, Me)
            Datas.Add(newdat)
        Next
    End Sub
    Public Property Nombre As String
        Get
            Return Nodo.Attributes("name").Value
        End Get
        Set(value As String)
            Nodo.Attributes("name").Value = value
        End Set
    End Property
    Public Property Invert As Boolean
        Get
            Return Nodo.Attributes("invert").Value
        End Get
        Set(value As Boolean)
            If value = True Then
                Nodo.Attributes("invert").Value = "true"
            Else
                Nodo.Attributes("invert").Value = "false"
            End If
        End Set
    End Property

    Public Property Default_Setting(size As Config_App.SliderSize) As Single
        Get
            If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Return Default_Setting_FO
            Else
                Return Default_Setting_SSE(size)
            End If
        End Get
        Set(value As Single)
            If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Default_Setting_FO = value
            Else
                Default_Setting_SSE(size) = value
            End If
        End Set
    End Property
    Public Property Default_Setting_FO As Single
        Get
            If IsNothing(Nodo.Attributes("default")) Then Return 0
            Return Single.Parse(Nodo.Attributes("default").Value, System.Globalization.CultureInfo.InvariantCulture)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("default")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("default")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("default").Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        End Set
    End Property
    Public Property Default_Setting_SSE(size) As Single
        Get
            If Config_App.Current.Bodytipe = Config_App.SliderSize.Small Then
                Return Default_Small_Value
            Else
                Return Default_Big_Value
            End If
        End Get
        Set(value As Single)
            If Config_App.Current.Bodytipe = Config_App.SliderSize.Small Then
                Default_Small_Value = value
            Else
                Default_Big_Value = value
            End If
        End Set
    End Property
    Public Property Default_Big_Value As Single
        Get
            If IsNothing(Nodo.Attributes("big")) Then Return 0
            Return Single.Parse(Nodo.Attributes("big").Value, System.Globalization.CultureInfo.InvariantCulture)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("big")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("big")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("big").Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        End Set
    End Property
    Public Property Default_Small_Value As Single
        Get
            If IsNothing(Nodo.Attributes("small")) Then Return 0
            Return Single.Parse(Nodo.Attributes("small").Value, System.Globalization.CultureInfo.InvariantCulture)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("small")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("small")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("small").Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        End Set
    End Property

    Private _Current As Single = 0
    Public Property Current_Setting As Single
        Get
            Return _Current
        End Get
        Set(value As Single)
            _Current = value
        End Set
    End Property

    Public Property IsZap As Boolean
        Get
            If IsNothing(Nodo.Attributes("zap")) Then Return False
            Return Nodo.Attributes("zap").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If Not IsNothing(Nodo.Attributes("zap")) Then Nodo.Attributes.RemoveNamedItem("zap")
            Else
                If IsNothing(Nodo.Attributes("zap")) Then
                    Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("zap")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("zap").Value = "true"
                End If
            End If

        End Set
    End Property
    Public Property IsClamp As Boolean
        Get
            If IsNothing(Nodo.Attributes("clamp")) Then Return False
            Return Nodo.Attributes("clamp").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If Not IsNothing(Nodo.Attributes("clamp")) Then Nodo.Attributes.RemoveNamedItem("clamp")
            Else
                If IsNothing(Nodo.Attributes("clamp")) Then
                    Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("clamp")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("clamp").Value = "true"
                End If
            End If

        End Set
    End Property
    Public Property IsUV As Boolean
        Get
            If IsNothing(Nodo.Attributes("uv")) Then Return False
            Return Nodo.Attributes("uv").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If Not IsNothing(Nodo.Attributes("uv")) Then Nodo.Attributes.RemoveNamedItem("uv")
            Else
                If IsNothing(Nodo.Attributes("uv")) Then
                    Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("uv")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("uv").Value = "true"
                End If
            End If

        End Set
    End Property

    Public Property IsManoloFix As Boolean
        Get
            If IsNothing(Nodo.Attributes("manolofix")) Then Return False
            Return Nodo.Attributes("manolofix").Value
        End Get
        Set(value As Boolean)
            If value = False Then
                If Not IsNothing(Nodo.Attributes("manolofix")) Then Nodo.Attributes.RemoveNamedItem("manolofix")
            Else
                If IsNothing(Nodo.Attributes("manolofix")) Then
                    Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("manolofix")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("manolofix").Value = "true"
                End If
            End If

        End Set
    End Property
End Class

Public Class Slider_Data_class
    Public Property Nodo As XmlNode
    Public Property ParentSlider As Slider_class

    Sub New(ByRef el As XmlNode, ByRef Slider As Slider_class)
        Nodo = el
        ParentSlider = Slider
    End Sub
    Sub New(datnombre As String, ByRef Slider As Slider_class, Shapename As String, fileosd As String)
        Dim SliderNode As XmlElement = Slider.ParentSliderSet.ParentOSP.xml.CreateElement("Data")
        SliderNode.SetAttribute("name", datnombre)
        SliderNode.SetAttribute("target", Shapename)
        SliderNode.SetAttribute("local", "true")
        SliderNode.InnerText = fileosd + "\" + Shapename.Replace(":", "_") + Slider.Nombre
        Nodo = SliderNode
        Slider.Nodo.AppendChild(Nodo)
        ParentSlider = Slider
    End Sub

    Public Property Nombre As String
        Get
            Return Nodo.Attributes("name").Value
        End Get
        Set(value As String)
            Nodo.Attributes("name").Value = value
        End Set
    End Property

    Public Property Target As String
        Get
            Return Nodo.Attributes("target").Value
        End Get
        Set(value As String)
            Nodo.Attributes("target").Value = value
        End Set
    End Property


    Public Property Islocal As Boolean
        Get
            If IsNothing(Nodo.Attributes("local")) Then Return False
            If Nodo.Attributes("local").Value = "true" Then Return True
            Return False
        End Get
        Set(value As Boolean)
            If value = False Then
                If Not IsNothing(Nodo.Attributes("local")) Then Nodo.Attributes.RemoveNamedItem("local")
            Else
                If IsNothing(Nodo.Attributes("local")) Then
                    Dim attr As XmlAttribute = ParentSlider.ParentSliderSet.ParentOSP.xml.CreateAttribute("local")
                    attr.Value = "true"
                    Nodo.Attributes.Append(attr)
                Else
                    Nodo.Attributes("local").Value = "true"
                End If
            End If
        End Set
    End Property
    Public Property FullText As String
        Get
            Return Nodo.FirstChild.Value
        End Get
        Set(value As String)
            Nodo.FirstChild.Value = value
        End Set
    End Property
    Public Property TargetOsd As String
        Get
            Dim parts = FullText.Split("\"c)
            Return parts(0)
        End Get
        Set(value As String)
            FullText = value + "\" + TargetSlider
        End Set
    End Property
    Public Property TargetSlider As String
        Get
            Dim parts = FullText.Split("\"c)
            If parts.Length > 1 Then Return parts(1) Else Return ""
        End Get
        Set(value As String)
            FullText = TargetOsd + "\" + value
        End Set
    End Property
    Public ReadOnly Property RelatedShape As Shape_class
        Get
            Return ParentSlider.ParentSliderSet.GetShapeByTargetCached(Target)
        End Get
    End Property
    Public ReadOnly Property RelatedOSDBlocks As IEnumerable(Of OSD_Block_Class)
        Get
            If Me.Islocal Then
                Return RelatedLocalOSDBlocks
            Else
                Return RelatedExternalOSDBlocks
            End If
        End Get
    End Property

    Public ReadOnly Property RelatedLocalOSDBlocks As IEnumerable(Of OSD_Block_Class)
        Get
            If Me.Islocal = False Then Return Enumerable.Empty(Of OSD_Block_Class)()
            Return ParentSlider.ParentSliderSet.GetLocalOsdBlocksByNameCached(Me.Nombre)
        End Get
    End Property
    Public ReadOnly Property RelatedExternalOSDBlocks As IEnumerable(Of OSD_Block_Class)
        Get
            If Me.Islocal Then Return Enumerable.Empty(Of OSD_Block_Class)()
            Return ParentSlider.ParentSliderSet.GetExternalOsdBlocksByNameCached(Me.Nombre)
        End Get
    End Property

    ''' <summary>
    ''' If this data is already local, returns its local OSD blocks.
    ''' Otherwise clones the external blocks into the local OSD, marks the data as local,
    ''' and returns the new local blocks. Avoids creating duplicate local blocks by name.
    ''' </summary>
    Public Function MaterializeEditableLocalBlocks() As List(Of OSD_Block_Class)
        If Islocal Then Return RelatedLocalOSDBlocks.ToList()

        Dim sourceBlocks = RelatedExternalOSDBlocks.ToList()
        If sourceBlocks.Count = 0 Then Return New List(Of OSD_Block_Class)()

        Dim sliderSet = ParentSlider.ParentSliderSet
        Dim osdFilename = IO.Path.GetFileName(sliderSet.SourceFileFullPath).Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)
        Dim clones As New List(Of OSD_Block_Class)()

        For Each sourceBlock In sourceBlocks
            Dim existing = sliderSet.OSDContent_Local.Blocks.FirstOrDefault(
                Function(b) b.BlockName.Equals(sourceBlock.BlockName, StringComparison.OrdinalIgnoreCase))
            If existing IsNot Nothing Then
                clones.Add(existing)
            Else
                Dim clone = New OSD_Block_Class(sliderSet.OSDContent_Local) With {.BlockName = sourceBlock.BlockName}
                clone.DataDiff.AddRange(sourceBlock.SnapshotDiffs())
                clone.RebuildCompactArrays()
                sliderSet.OSDContent_Local.Blocks.Add(clone)
                clones.Add(clone)
            End If
        Next

        Islocal = True
        TargetOsd = osdFilename
        Return clones
    End Function
    Public Shared Function GetEditableTargetBlock(targetShape As Shape_class,
                                                slider As Slider_class,
                                                sliderSet As SliderSet_Class,
                                                osdFilename As String) As OSD_Block_Class
        Dim targetDatas = slider.Datas.Where(
            Function(d) d.Target.Equals(targetShape.Target, StringComparison.OrdinalIgnoreCase)).ToList()
        If targetDatas.Any(Function(d) d.Islocal) Then
            targetDatas = targetDatas.Where(Function(d) d.Islocal).ToList()
        End If

        For Each dat In targetDatas
            dat.MaterializeEditableLocalBlocks()
        Next

        Dim blockName = targetShape.Target.Replace(":", "_") & slider.Nombre
        Dim targetBlock = sliderSet.OSDContent_Local.Blocks.FirstOrDefault(
            Function(b) b.BlockName.Equals(blockName, StringComparison.OrdinalIgnoreCase))

        If targetBlock IsNot Nothing Then Return targetBlock
        targetBlock = New OSD_Block_Class(sliderSet.OSDContent_Local) With {.BlockName = blockName}
        sliderSet.OSDContent_Local.Blocks.Add(targetBlock)

        If targetDatas.Count = 0 Then
            slider.Datas.Add(New Slider_Data_class(blockName, slider, targetShape.Target, osdFilename))
        End If

        Return targetBlock
    End Function
End Class

