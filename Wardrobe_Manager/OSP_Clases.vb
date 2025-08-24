Imports System.Globalization
Imports System.IO
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
            If IO.File.Exists(xmlpath) = False Then Exit Sub
            Try
                Dim rawXml As String = IO.File.ReadAllText(xmlpath)
                ' Cargar el documento desde el string corregido
                Dim doc As XDocument = XDocument.Parse(rawXml)
                For Each pose As XElement In doc.Root.Elements("Pose")
                    Dim pos As New Poses_class With {
                        .Source = Poses_class.Pose_Source_Enum.BodySlide,
                        .Name = pose.Attribute("name").Value.ToString,
                        .Version = "1",
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
            .Version = "1",
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
            If IO.File.Exists(xmlpath) = False Then Exit Sub
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
    Public Sub LoadCategories(xmlPath As String)
        ' Carga el documento XML
        Categories.Clear()
        If IO.File.Exists(xmlPath) = False Then Exit Sub
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

                ' Agrega cada atributo "name" de <Slider>
                For Each sliderElem As XElement In categoryElem.Elements("Slider")
                    Dim sliderName As String() = {CStr(sliderElem.Attribute("name").Value), CStr(sliderElem.Attribute("displayname").Value)}
                    sliderNames.Add(sliderName)
                Next

                Categories.Add(categoryName, sliderNames)
            Next
        Catch ex As Exception
            MsgBox("Error reading Category file " + xmlPath, vbCritical, "Error")
        End Try

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
                    Dim stream = IO.File.Open(Filename, IO.FileMode.Open, FileAccess.Read)
                    Dim reader As New IO.BinaryReader(stream)
                    Header = reader.ReadBytes(4)
                    Version = reader.ReadBytes(4)
                    Datablocks = reader.ReadUInt32
                    For x = 0 To Datablocks - 1
                        Dim Namelenght As Byte = reader.ReadByte
                        Dim namebytes As Byte() = reader.ReadBytes(Namelenght)
                        Dim block As New OSD_Block_Class(Me) With {.BlockName = (System.Text.Encoding.UTF8.GetString(namebytes))}
                        Dim DifDatas = reader.ReadUInt16
                        For y As Int32 = 0 To CType(DifDatas, Int32) - 1
                            Dim Dif As New OSD_DataDiff_Class With {.Index = reader.ReadUInt16(), .X = reader.ReadSingle, .Y = reader.ReadSingle, .Z = reader.ReadSingle}
                            block.DataDiff.Add(Dif)
                        Next
                        Blocks.Add(block)
                    Next
                    reader.Close()
                    stream.Close()
                End SyncLock
            End If
        Next
    End Sub

    Public Sub Clone_block(source As OSD_Block_Class)
        Dim nuewblock = New OSD_Block_Class(Me) With {.BlockName = source.BlockName}
        For Each dat In source.DataDiff
            nuewblock.DataDiff.Add(New OSD_DataDiff_Class() With {.Index = dat.Index, .X = dat.X, .Y = dat.Y, .Z = dat.Z})
        Next
        Me.Blocks.Add(nuewblock)
    End Sub

    Public Sub Save_As(Filename As String, Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then
            If MsgBox("ODS File already exists, replace?", vbYesNo, "Warning") = MsgBoxResult.No Then
                Exit Sub
            End If
        End If

        If IsNothing(Me.Header) Then Exit Sub
        Dim stream = IO.File.Open(Filename, IO.FileMode.Create)
        Dim Writer As New IO.BinaryWriter(stream)

        Writer.Write(Header)
        Writer.Write(Version)
        Writer.Write(CType(Blocks.Count, UInt32))
        For x = 0 To Blocks.Count - 1
            Writer.Write(CType(Blocks(x).BlockName.Length, Byte))
            Writer.Write(System.Text.Encoding.UTF8.GetBytes(Blocks(x).BlockName))
            Dim DifDatas = Blocks(x).DataDiff.Count
            Writer.Write(CType(DifDatas, UInt16))
            For y = 0 To CInt(DifDatas) - 1
                Writer.Write(CType(Blocks(x).DataDiff(y).Index, UInt16))
                Writer.Write(CType(Blocks(x).DataDiff(y).X, Single))
                Writer.Write(CType(Blocks(x).DataDiff(y).Y, Single))
                Writer.Write(CType(Blocks(x).DataDiff(y).Z, Single))
            Next

        Next
        Writer.Flush()
        Writer.Close()
    End Sub
End Class
Public Class OSD_Block_Class
    Public Property BlockName As String
    Public Property DataDiff As New List(Of OSD_DataDiff_Class)
    Public Property ParentOSDContent As OSD_Class
    Sub New(Parent As OSD_Class)
        Me.ParentOSDContent = Parent
    End Sub
    Public ReadOnly Property RelatedData As IEnumerable(Of Slider_Data_class)
        Get
            Return ParentOSDContent.ParentSlider.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.Nombre.Equals(BlockName, StringComparison.OrdinalIgnoreCase))
        End Get
    End Property

End Class
Public Class OSD_DataDiff_Class
    Public Property Index As Integer
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single

End Class

Public Class OSP_Project_Class
    Public Property SliderSets As New List(Of SliderSet_Class)
    Public xml As New XmlDocument

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
            MsgBox("El nombre del proyecto ya existe, no se procesará", vbCritical)
            Return Nothing
        End If
        Dim writer = IO.File.CreateText(Filename)
        writer.WriteLine("<?xml version=" + Chr(34) + "1.0" + Chr(34) + " encoding=" + Chr(34) + "UTF-8" + Chr(34) + "?>")
        writer.WriteLine("<SliderSetInfo version=" + Chr(34) + " 1" + Chr(34) + " ManoloPack=" + Chr(34) + IIf(ManoloPack, "true", "false") + Chr(34) + ">")
        writer.WriteLine("</SliderSetInfo>")
        writer.Flush()
        writer.Close()
        Return New OSP_Project_Class(Filename, True)
    End Function
    Public Function AddProject(ByRef Template As SliderSet_Class) As SliderSet_Class
        Dim Sliderset_Target = New SliderSet_Class(Me.xml.DocumentElement.AppendChild(Me.xml.ImportNode(Template.Nodo.Clone, True)), Me)
        Load_and_CHeck_Project(Sliderset_Target, True)
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
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then Throw New Exception("OSP File already exists")
        Me.xml.Save(NewFilename)
    End Sub
    Public Shared Function Clone_Material_Sub(shad As INiShader, Overwrite As Boolean) As String
        Dim mate As String
        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                mate = CType(shad, BSLightingShaderProperty).Name.String.Correct_Path_Separator
            Case GetType(BSEffectShaderProperty)
                mate = CType(shad, BSEffectShaderProperty).Name.String.Correct_Path_Separator
            Case Else
                Debugger.Break()
                Return Nothing
        End Select
        If mate = "" Then Return mate
        Dim prefix = "Materials\"
        If mate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then mate = mate.Substring(prefix.Length)
        Dim Fil_original As String = prefix + mate
        If FilesDictionary_class.Dictionary.ContainsKey(Fil_original) = False Then Return mate
        If Not FilesDictionary_class.Dictionary(Fil_original).IsLosseFile AndAlso Not Config_App.Allowed_To_Clone(FilesDictionary_class.Dictionary(Fil_original).BA2File) Then Return mate
        Dim Dire As String = IO.Path.GetDirectoryName(Fil_original).Correct_Path_Separator
        Dim regreso As String = Fil_original
        For Each Fil In FilesDictionary_class.Dictionary.Keys.Where(Function(pf) IO.Path.GetDirectoryName(pf).Correct_Path_Separator.Equals(IO.Path.GetDirectoryName(prefix + mate), StringComparison.OrdinalIgnoreCase) AndAlso (pf.EndsWith(".bgsm", StringComparison.OrdinalIgnoreCase) Or pf.EndsWith(".bgem", StringComparison.OrdinalIgnoreCase))).Select(Function(pf) pf)
            If FilesDictionary_class.Dictionary(Fil).IsLosseFile OrElse Config_App.Allowed_To_Clone(FilesDictionary_class.Dictionary(Fil).BA2File) Then
                Dim relative As String = IO.Path.GetRelativePath(prefix, Dire + "\")
                Dim source As String = Dire + "\" + IO.Path.GetFileName(Fil)
                Dim temp As String = Procesa_Material(source, prefix, relative, Overwrite, Not Fil.Correct_Path_Separator.Equals(Fil_original, StringComparison.OrdinalIgnoreCase))
                If Fil.Correct_Path_Separator.Equals(Fil_original, StringComparison.OrdinalIgnoreCase) Then
                    regreso = temp
                End If
            End If
        Next
        If regreso = "" Then Throw New Exception
        Return regreso
    End Function
    Private Shared Function Procesa_Material(source As String, prefix As String, relative As String, overwrite As Boolean, canskip As Boolean) As String

        Select Case IO.Path.GetExtension(source).ToLower
            Case ".bgsm"
                Dim material As New BGSM
                Using ms As New MemoryStream(FilesDictionary_class.GetBytes(source))
                    Using reader As New BinaryReader(ms)
                        material.Deserialize(reader)
                        reader.Close()
                    End Using
                    ms.Close()
                End Using
                Dim exist As Boolean
                Dim Baseexist As Boolean
                material.NormalTexture = CopyTexture(material.NormalTexture, overwrite, exist)
                material.SmoothSpecTexture = CopyTexture(material.SmoothSpecTexture, overwrite, exist)
                material.GreyscaleTexture = CopyTexture(material.GreyscaleTexture, overwrite, exist)
                material.EnvmapTexture = CopyTexture(material.EnvmapTexture, overwrite, exist)
                material.FlowTexture = CopyTexture(material.FlowTexture, overwrite, exist)
                material.GlowTexture = CopyTexture(material.GlowTexture, overwrite, exist)
                material.DisplacementTexture = CopyTexture(material.DisplacementTexture, overwrite, exist)
                material.InnerLayerTexture = CopyTexture(material.InnerLayerTexture, overwrite, exist)
                material.LightingTexture = CopyTexture(material.LightingTexture, overwrite, exist)
                material.SpecularTexture = CopyTexture(material.SpecularTexture, overwrite, exist)
                material.WrinklesTexture = CopyTexture(material.WrinklesTexture, overwrite, exist)
                material.DistanceFieldAlphaTexture = CopyTexture(material.DistanceFieldAlphaTexture, overwrite, exist)
                material.DiffuseTexture = CopyTexture(material.DiffuseTexture, overwrite, Baseexist)
                If Baseexist = False And canskip Then
                    Return source
                End If
                '
                Dim temp = material.RootMaterialPath
                If temp.StartsWith("Materials\", StringComparison.OrdinalIgnoreCase) Then temp = temp.Substring(prefix.Length)
                Dim Filt As String = prefix + temp
                If FilesDictionary_class.Dictionary.ContainsKey(Filt) Then
                    Debugger.Break()
                    Dim relative2 As String = IO.Path.GetRelativePath(prefix, IO.Path.GetDirectoryName(Filt) + "\")
                    material.RootMaterialPath = Procesa_Material(Filt, prefix, relative, overwrite, True)
                End If
                Dim writer As FileStream
                Dim fullpath As String
                If relative.StartsWith("ManoloCloned", StringComparison.OrdinalIgnoreCase) = True OrElse relative.StartsWith("ManoloMods", StringComparison.OrdinalIgnoreCase) = True Then
                    fullpath = relative + IO.Path.GetFileName(source)
                Else
                    fullpath = "ManoloCloned\" + relative + IO.Path.GetFileName(source)
                End If
                Dim newfile As String = IO.Path.Combine(Directorios.Fallout4data, prefix + fullpath).Correct_Path_Separator
                If IO.Directory.Exists(IO.Path.GetDirectoryName(newfile)) = False Then IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(newfile))
                If overwrite = True Then
                    writer = IO.File.Open(newfile, FileMode.Create)
                Else
                    writer = IO.File.Open(newfile, FileMode.OpenOrCreate)
                End If
                material.Save(writer)
                writer.Close()
                Dim Location As New FilesDictionary_class.File_Location With {.BA2File = "", .Index = -1, .FullPath = prefix + fullpath}
                If FilesDictionary_class.Dictionary.TryAdd((prefix + fullpath).Correct_Path_Separator, Location) = False Then
                    If Location.FullPath.Contains("ManoloCloned\") = False AndAlso Location.FullPath.Contains("ManoloMods\") = False Then
                        Debugger.Break()
                        Throw New Exception
                    End If

                End If
                Return fullpath
            Case ".bgem"
                Dim material As New BGEM
                Using ms As New MemoryStream(FilesDictionary_class.GetBytes(source))
                    Using reader As New BinaryReader(ms)
                        material.Deserialize(reader)
                        reader.Close()
                    End Using
                    ms.Close()
                End Using
                Dim exist As Boolean
                Dim baseexist As Boolean
                material.NormalTexture = CopyTexture(material.NormalTexture, overwrite, exist)
                material.BaseTexture = CopyTexture(material.BaseTexture, overwrite, baseexist)
                material.EnvmapMaskTexture = CopyTexture(material.EnvmapMaskTexture, overwrite, exist)
                material.EnvmapTexture = CopyTexture(material.EnvmapTexture, overwrite, exist)
                material.GrayscaleTexture = CopyTexture(material.GrayscaleTexture, overwrite, exist)
                material.LightingTexture = CopyTexture(material.LightingTexture, overwrite, exist)
                material.GlowTexture = CopyTexture(material.GlowTexture, overwrite, exist)
                material.SpecularTexture = CopyTexture(material.SpecularTexture, overwrite, exist)
                If baseexist = False And canskip Then
                    Return source
                End If

                Dim writer As FileStream
                Dim fullpath As String
                If relative.StartsWith("ManoloCloned", StringComparison.OrdinalIgnoreCase) = True OrElse relative.StartsWith("ManoloMods", StringComparison.OrdinalIgnoreCase) = True Then
                    fullpath = relative + IO.Path.GetFileName(source)
                Else
                    fullpath = "ManoloCloned\" + relative + IO.Path.GetFileName(source)
                End If
                Dim newfile As String = IO.Path.Combine(Directorios.Fallout4data, prefix + fullpath)
                If IO.Directory.Exists(IO.Path.GetDirectoryName(newfile)) = False Then IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(newfile))
                If overwrite = True Then
                    writer = IO.File.Open(newfile, FileMode.Create)
                Else
                    writer = IO.File.Open(newfile, FileMode.OpenOrCreate)
                End If
                material.Save(writer)
                writer.Close()
                Dim fullpath2 As String = (prefix + fullpath)
                Dim Location As New FilesDictionary_class.File_Location With {.BA2File = "", .Index = -1, .FullPath = fullpath2.Correct_Path_Separator}
                If FilesDictionary_class.Dictionary.TryAdd(fullpath2.Correct_Path_Separator, Location) = False Then
                    If Location.FullPath.Contains("ManoloCloned\") = False AndAlso Location.FullPath.Contains("ManoloMods\") = False Then
                        Debugger.Break()
                        Throw New Exception
                    End If
                End If

                Return fullpath
            Case Else
                Throw New Exception
        End Select

    End Function
    Private Shared Function CopyTexture(filename As String, Overwrite As Boolean, ByRef exist As Boolean) As String
        If filename = "" Then Return ""
        exist = True
        filename = filename.Correct_Path_Separator
        Dim prefix = "Textures\"
        If filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then filename = filename.Substring(prefix.Length)
        Dim oldfile As String = prefix + filename
        Dim newfilename
        If filename.Contains("ManoloCloned", StringComparison.OrdinalIgnoreCase) = False AndAlso filename.Contains("ManoloMods", StringComparison.OrdinalIgnoreCase) = False Then
            newfilename = "ManoloCloned\" + filename
        Else
            newfilename = filename
        End If
        Dim newfile As String = IO.Path.Combine(Config_App.Current.FO4EDataPath, prefix + newfilename)
        If FilesDictionary_class.Dictionary.ContainsKey(oldfile) = False Then exist = False : Return filename
        If Not FilesDictionary_class.Dictionary(oldfile).IsLosseFile AndAlso Not Config_App.Allowed_To_Clone(FilesDictionary_class.Dictionary(oldfile).BA2File) Then Return filename

        If filename <> newfilename Then
            If IO.Directory.Exists(IO.Path.GetDirectoryName(newfile)) = False Then IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(newfile))
            If IO.File.Exists(newfile) = True Then
                If Overwrite = False Then
                    Return newfilename
                Else
                    IO.File.Delete(newfile)
                End If
            End If
            System.IO.File.WriteAllBytes(newfile, FilesDictionary_class.Dictionary(oldfile).GetBytes)
            Dim fullpath As String = (prefix + newfilename)
            Dim Location As New FilesDictionary_class.File_Location With {.BA2File = "", .Index = -1, .FullPath = fullpath.Correct_Path_Separator}
            If FilesDictionary_class.Dictionary.TryAdd(fullpath.Correct_Path_Separator, Location) = False Then
                If Location.FullPath.Contains("ManoloCloned\") = False AndAlso Location.FullPath.Contains("ManoloMods\") = False Then
                    Debugger.Break()
                    Throw New Exception
                End If
            End If
        End If
        Return newfilename
    End Function

    Public ReadOnly Property IsManoloPack
        Get
            'ManoloPack="true"
            If Not IsNothing(xml.DocumentElement.Attributes("ManoloPack")) AndAlso xml.DocumentElement.Attributes("ManoloPack").Value = "true" Then Return True
            Return False
        End Get
    End Property
    Public Shared Function Load_and_Check_Shapedata(ByRef Sliderset_Target As SliderSet_Class) As Boolean
        Try
            If Sliderset_Target.Unreadable_NIF Then Return False

            If Sliderset_Target.OsdLocalFullPath.Count >= 2 Then Throw New Exception("More than one osd Local file")
            If Sliderset_Target.OsdLocalFullPath.Any Then Sliderset_Target.OSDContent_Local.Load(Sliderset_Target.OsdLocalFullPath.ToArray)
            If Sliderset_Target.OsdExternalFullPath.Any Then Sliderset_Target.OSDContent_External.Load(Sliderset_Target.OsdExternalFullPath.ToArray)

            Sliderset_Target.NIFContent.Load_Manolo(Sliderset_Target.SourceFileFullPath)
            Sliderset_Target.ReadhighHeel()

            If Sliderset_Target.Shapes.Where(Function(pf) pf.RelatedNifShape Is Nothing).Any Then Throw New Exception("Shape without Nif Shapes different")
            If Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pq) pq.RelatedOSDBlocks.Any).Count > Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Count Then Throw New Exception("Datas and OSD blocks different")
            If Sliderset_Target.Sliders.SelectMany(Function(pf) pf.Datas).Select(Function(pf) (pf.Nombre.ToLower + pf.ParentSlider.Nombre.ToLower)).GroupBy(Function(key) key).Any(Function(g) g.Count() > 1) Then Throw New Exception("Duplicated Slider Data")
        Catch ex As Exception
            Sliderset_Target.Unreadable_NIF = True
            MsgBox("Error reading shapedata from project: " + Sliderset_Target.Nombre + " " + ex.Message.ToString, vbCritical + vbOK, "Error")
            Return False
        End Try
        Return True
    End Function


    Public Shared Function Load_and_CHeck_Project(ByRef Sliderset_Target As SliderSet_Class, Deep_Analize As Boolean) As Boolean
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
        If Deep_Analize Then Return OSP_Project_Class.Load_and_Check_Shapedata(Sliderset_Target)
        Return True
    End Function
    Public Sub Lee_Slidersets(Deep_Analize As Boolean)
        SliderSets.Clear()
        Try
            For Each Sset As XmlNode In xml.DocumentElement.SelectNodes("SliderSet")
                Dim Sliderset_target As SliderSet_Class
                If YaEstan.Where(Function(pf) pf.Nombre = Sset.Attributes("name").Value).Any Then
                    Sliderset_target = YaEstan.Where(Function(pf) pf.Nombre = Sset.Attributes("name").Value).First
                    Sliderset_target.Reload(Sset)
                Else
                    Sliderset_target = New SliderSet_Class(Sset, Me)
                End If
                Load_and_CHeck_Project(Sliderset_target, Deep_Analize)
                SliderSets.Add(Sliderset_target)
            Next

        Catch ex As Exception
            MsgBox("Error pricessing OSP file" + Me.Filename, vbCritical, "Error")
        End Try
        ' Chequeos

    End Sub

    Private Function Check_repeated(Nombre As String) As Boolean
        If SliderSets.Where(Function(pf) pf.Nombre.Equals(Nombre, StringComparison.OrdinalIgnoreCase)).Any Then
            MsgBox("El nombre del proyecto ya existe, no se procesará", vbCritical)
            Return False
        End If
        Return True
    End Function
    Public Function Agrega_Proyecto(Sliderset_Source As SliderSet_Class, Nombre_Proyecto As String, Filename As String, ExcludeReference As Boolean, OverwriteShapeFiles As Boolean, Clone_Materials As Boolean, Keep_Physics As Boolean, ChangeOutputDir As Boolean) As SliderSet_Class
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

        Sliderset_Target.HighHeelHeight = Sliderset_Source.HighHeelHeight

        Dim Old_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Sliderset_Source.DataFolderValue), Sliderset_Source.SourceFileValue)
        Dim Old_Osd = Old_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        ' Procesa los cambios de nombre
        Sliderset_Target.Update_Names(Nombre_Proyecto, Me.Nombre)

        ' Exclude reference
        If ExcludeReference = True AndAlso Sliderset_Target.Shapes.Where(Function(pf) pf.Isreference).Any Then
            Sliderset_Target.RemoveShape(Sliderset_Target.Shapes.Where(Function(pf) pf.Isreference).First)
        End If

        ' Clona Material
        If Clone_Materials Then
            For Each shap In Sliderset_Target.Shapes
                If Not IsNothing(shap.RelatedNifShader) Then
                    Dim shad = shap.RelatedNifShader
                    Select Case shad.GetType
                        Case GetType(BSLightingShaderProperty)
                            CType(shad, BSLightingShaderProperty).Name.String = Clone_Material_Sub(shap.RelatedNifShader, OverwriteShapeFiles)
                        Case GetType(BSEffectShaderProperty)
                            CType(shad, BSEffectShaderProperty).Name.String = Clone_Material_Sub(shap.RelatedNifShader, OverwriteShapeFiles)
                        Case Else
                            Debugger.Break()
                            Throw New Exception
                    End Select
                End If
            Next
        End If

        ' Saca Physics
        If Not Keep_Physics Then Sliderset_Target.NIFContent.RemoveBlocksOfType(Of BSClothExtraData)()


        Sliderset_Target.Save_Shapedatas(OverwriteShapeFiles)

        ' Graba el proyecto
        Save_Pack_As(Filename, True)
        Return Sliderset_Target
    End Function

    Public Shared Function Merge_Proyecto(Sliderset_Madre As SliderSet_Class, Sliderset_Source As SliderSet_Class, ExcludeReference As Boolean, OverwriteShapeFiles As Boolean, Clone_Materials As Boolean, Keep_Physics As Boolean) As SliderSet_Class
        ' Add project and update
        Dim Sliderset_Target = New SliderSet_Class(Sliderset_Madre.ParentOSP.xml.ImportNode(Sliderset_Source.Nodo.Clone, True), Sliderset_Madre.ParentOSP)
        If OSP_Project_Class.Load_and_CHeck_Project(Sliderset_Target, True) = False Then Return Nothing

        If Sliderset_Source.HighHeelHeight <> 0 Then If Sliderset_Madre.IsHighHeel = 0 Or Sliderset_Madre.HighHeelHeight = Sliderset_Source.HighHeelHeight Then Sliderset_Madre.HighHeelHeight = Sliderset_Source.HighHeelHeight Else Sliderset_Madre.HighHeelHeight = Math.Max(Sliderset_Madre.HighHeelHeight, Sliderset_Source.HighHeelHeight) : MsgBox("Different High Heels setup. Higher assumed", vbInformation, "Warning")

        Dim Old_Nif = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Sliderset_Target.DataFolderValue), Sliderset_Target.SourceFileValue)
        Dim Old_Osd = Old_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)

        ' Procesa los cambios de nombre
        Sliderset_Target.Update_Names(Sliderset_Madre.Nombre, Sliderset_Madre.ParentOSP.Nombre)


        ' Agrega Sliders Faltantes

        For Each slid In Sliderset_Target.Sliders
            If Sliderset_Madre.Sliders.Where(Function(pf) pf.Nombre.Equals(slid.Nombre, StringComparison.OrdinalIgnoreCase)).Any = False Then
                Dim nodo = Sliderset_Madre.Nodo.AppendChild(Sliderset_Madre.ParentOSP.xml.ImportNode(slid.Nodo.Clone, True))
                For Each ch In nodo.SelectNodes("Data")
                    nodo.RemoveChild(ch)
                Next
                Dim new_slider As New Slider_class(nodo, Sliderset_Madre)
                Sliderset_Madre.Sliders.Add(new_slider)
            End If
        Next

        ' Reference
        If Sliderset_Madre.Shapes.Where(Function(pf) pf.Isreference).Any Then
            For Each extsh In Sliderset_Target.Shapes.Where(Function(pf) pf.Isreference)
                For Each dat In extsh.Related_Slider_data.ToList
                    If dat.Islocal = False Then
                        For Each block In dat.RelatedOSDBlocks.ToList
                            Sliderset_Target.OSDContent_Local.Clone_block(block)
                        Next
                    End If
                    dat.Islocal = True
                    dat.TargetOsd = IO.Path.GetFileName(Sliderset_Madre.OsdLocalFullPath.First)

                Next
                extsh.Datafolder = stringArray.ToList
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
                    If Sliderset_Target.NIFContent.NifShapes.Where(Function(pf) pf.Name.String.Equals(Shap.Nombre, StringComparison.OrdinalIgnoreCase)).Any Then
                        Sliderset_Target.NIFContent.NifShapes.Where(Function(pf) pf.Name.String.Equals(Shap.Nombre, StringComparison.OrdinalIgnoreCase)).First.Name.String = nombre_Nuevo
                    End If
                End If
                Shap.Target = nombre_Nuevo
                Shap.Nombre = nombre_Nuevo
            End If


            ' Agrega shape
            Dim pointer As XmlNode = Sliderset_Madre.Nodo.SelectNodes("Shape").Item(Sliderset_Madre.Nodo.SelectNodes("Shape").Count - 1)
            Dim new_Shape As New Shape_class(Sliderset_Madre.Nodo.InsertAfter(Sliderset_Madre.ParentOSP.xml.ImportNode(Shap.Nodo.Clone, True), pointer), Sliderset_Madre)
            Sliderset_Madre.Shapes.Add(new_Shape)


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
                End If

                For Each block In dat.RelatedLocalOSDBlocks
                    If Sliderset_Madre.OSDContent_Local.Blocks.Where(Function(pf) pf.BlockName.Equals(dat_Nuevo)).Any = False Then
                        Sliderset_Madre.OSDContent_Local.Clone_block(block)
                    End If
                Next

                Dim slid As Slider_class = Sliderset_Madre.Sliders.Where(Function(pf) pf.Nombre.Equals(dat.ParentSlider.Nombre, StringComparison.OrdinalIgnoreCase)).First
                Dim new_dat As New Slider_Data_class(slid.Nodo.AppendChild(Sliderset_Madre.ParentOSP.xml.ImportNode(dat.Nodo.Clone, True)), slid)
                slid.Datas.Add(new_dat)
            Next

        Next

        ' Merge Nifs


        ' Exclude reference for the source
        If ExcludeReference = True AndAlso Sliderset_Target.Shapes.Where(Function(pf) pf.IsReference).Any Then
            Sliderset_Target.RemoveShape(Sliderset_Target.Shapes.Where(Function(pf) pf.IsReference).First)
        End If


        Nifcontent_Class_Manolo.Merge_Shapes_Original(Sliderset_Madre.NIFContent, Sliderset_Target.NIFContent, Keep_Physics)

        ' Exclude reference
        If ExcludeReference = True AndAlso Sliderset_Madre.Shapes.Where(Function(pf) pf.IsReference).Any Then
            Sliderset_Madre.RemoveShape(Sliderset_Madre.Shapes.Where(Function(pf) pf.IsReference).First)
        End If


        ' Clona Material en el proyecto
        If Clone_Materials Then
            For Each shap In Sliderset_Madre.Shapes
                If Not IsNothing(shap.RelatedNifShader) Then
                    Dim shad = shap.RelatedNifShader
                    Select Case shad.GetType
                        Case GetType(BSLightingShaderProperty)
                            CType(shad, BSLightingShaderProperty).Name.String = Clone_Material_Sub(shap.RelatedNifShader, OverwriteShapeFiles)
                        Case GetType(BSEffectShaderProperty)
                            CType(shad, BSEffectShaderProperty).Name.String = Clone_Material_Sub(shap.RelatedNifShader, OverwriteShapeFiles)
                        Case Else
                            Debugger.Break()
                            Throw New Exception
                    End Select
                End If
            Next
        End If

        ' Graba OSD y NIF
        Sliderset_Madre.Save_Shapedatas(True)

        ' Graba el proyecto
        Sliderset_Madre.ParentOSP.Save_Pack_As(Sliderset_Madre.ParentOSP.Filename, True)
        Return Sliderset_Madre
    End Function


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

    Private Shared ReadOnly stringArray As String() = {""}

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


    Public ReadOnly Property IsHighHeel As Boolean
        Get
            Return HighHeelHeight <> 0
        End Get
    End Property
    Public Sub SetPreset(Preset As SlidersPreset_Class, Weight As Config_App.SliderSize)
        For Each slid In Sliders
            slid.Current_Setting = slid.Default_Setting(Weight)
            If Not IsNothing(Preset) Then
                If Preset.Sliders.Where(Function(pf) pf.Name.Equals(slid.Nombre, StringComparison.OrdinalIgnoreCase)).Any Then
                    For Each sli In Preset.Sliders.Where(Function(pf) pf.Name.Equals(slid.Nombre, StringComparison.OrdinalIgnoreCase)).OrderBy(Function(pf) pf.Size)

                        If sli.Size = Config_App.SliderSize.Small Then
                            If Weight = Config_App.SliderSize.Small AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then slid.Current_Setting = sli.Value
                        Else
                            If Weight <> Config_App.SliderSize.Small OrElse Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then slid.Current_Setting = sli.Value
                        End If

                    Next

                End If
            End If

        Next
    End Sub

    Public ReadOnly Property HasPhysics As Boolean
        Get
            Return Me.Shapes.Where(Function(pf) pf.HasPhysics).Any
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
        Shapes.Clear()
        Sliders.Clear()
        NIFContent.Clear()
        HighHeelHeight = 0
    End Sub

    Public Sub Lee_SlidersAndShapes()
        ' Replace your loops with these two one-liners:
        Shapes = Nodo.SelectNodes("Shape").Cast(Of XmlNode)().Select(Function(shap) New Shape_class(shap, Me)).ToList
        Sliders = Nodo.SelectNodes("Slider").Cast(Of XmlNode)().Select(Function(slid) New Slider_class(slid, Me)).ToList
    End Sub
    Public Sub ReadhighHeel()
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                Dim hh0 As String = IO.Path.Combine(IO.Path.Combine(Directorios.ShapedataRoot, Me.ParentOSP.Nombre), Me.Nombre + ".hht")
                Dim hh1 As String = IO.Path.Combine(IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, "F4SE\Plugins\HHS")), Me.OutputFileValue + ".txt")
                Dim hh2 As String = IO.Path.Combine(IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, Me.OutputPathValue)), Me.OutputFileValue + ".txt")
                Dim archivo As StreamReader = Nothing
                If IsNothing(archivo) Then If IO.File.Exists(hh0) Then archivo = New StreamReader(hh0)
                If IsNothing(archivo) Then If IO.File.Exists(hh1) Then archivo = New StreamReader(hh1)
                If IsNothing(archivo) Then If IO.File.Exists(hh2) Then archivo = New StreamReader(hh2)
                If IsNothing(archivo) Then Exit Sub
                Dim lin As String = archivo.ReadLine
                archivo.Close()
                If lin.Contains("="c) = False Then Exit Sub
                Dim sep = lin.Split("=")
                If sep.Length <> 2 Then Exit Sub
                HighHeelHeight = CDbl(sep(1).Replace(".", System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator))

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
                Dim archivo As StreamReader = Nothing
                If IsNothing(archivo) Then If IO.File.Exists(hh0) Then archivo = New StreamReader(hh0)
                If IsNothing(archivo) Then
                    If maxhh <> 0 Then HighHeelHeight = maxhh
                    Exit Sub
                End If

                Dim lin As String = archivo.ReadLine
                archivo.Close()
                If lin.Contains("="c) = False Then Exit Sub
                Dim sep = lin.Split("=")
                If sep.Length <> 2 Then Exit Sub
                HighHeelHeight = CDbl(sep(1).Replace(".", System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator))
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
                Dim hhfile = Path.Combine(Directorios.Fallout4data, Path.Combine(OutputPathValue, OutputFileValue)).Replace(".nif", "txt", StringComparison.OrdinalIgnoreCase)
                If hhfile.EndsWith(".txt") = False Then hhfile += ".txt"
                If HighHeelHeight = 0 Then
                    If IO.File.Exists(hhfile) Then IO.File.Delete(hhfile)
                Else
                    If Config_App.Current.Settings_Build.SaveHHS Then
                        Dim writer = IO.File.CreateText(hhfile)
                        writer.WriteLine("Height=" + HighHeelHeight.ToString.Replace(System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator, "."))
                        writer.Flush()
                        writer.Close()
                    End If
                End If
                    Case Config_App.Game_Enum.Skyrim
                For sizecount = 0 To IIf(Multisize, 1, 0)
                    Dim fil = IO.Path.Combine(IO.Path.Combine(Directorios.Fallout4data, OutputPathValue), OutputFileValue) + IIf(Multisize, "_" + sizecount.ToString, "") + ".nif"
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
            Dim writer = IO.File.CreateText(filename)
            writer.WriteLine("Height=" + HighHeelHeight.ToString.Replace(System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator, "."))
            writer.Flush()
            writer.Close()
        End If

    End Sub

    Public Sub Update_Names(Nombre As String, Pack As String)
        ' Reemplaza nombres
        Dim OldNif = Me.SourceFileFullPath
        Dim Oldosd = ""
        If Me.OsdLocalFullPath.Any Then Oldosd = Me.OsdLocalFullPath.First

        ' Carga OSD y NIF
        OSP_Project_Class.Load_and_Check_Shapedata(Me)

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
    End Sub

    Public Function Check_Unique_Shapename(Prueba As String) As String
        Dim index = 0
        Dim Nuevo As String = Prueba
        While Me.Shapes.Where(Function(pf) pf.Nombre.Equals(Nuevo, StringComparison.OrdinalIgnoreCase)).Any Or Me.Shapes.Where(Function(pf) pf.Target.Equals(Nuevo, StringComparison.OrdinalIgnoreCase)).Any
            Nuevo = Prueba + "_" + index.ToString
            index += 1
        End While
        Return Nuevo
    End Function
    Public Function Check_Unique_DataName(Prueba As String, slidername As String) As String
        Dim index = 0
        Dim Nuevo As String = Prueba
        While Me.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.TargetSlider.Equals(Nuevo + slidername, StringComparison.OrdinalIgnoreCase)).Any
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
                If IsNothing(Nodo.Attributes("GenWeights")) Then
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

        Dim Built_Nif = IO.Path.Combine(Directorios.Fallout4data, IO.Path.Combine(Me.OutputPathValue, Me.OutputFileValue)) + ".nif"
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
    Public Sub Save_Shapedatas(OverwriteShapeFiles As Boolean)
        Dim New_Nif = SourceFileFullPath
        Dim New_Osd = New_Nif.Replace(".nif", ".osd", StringComparison.OrdinalIgnoreCase)
        OSDContent_Local.Save_As(New_Osd, OverwriteShapeFiles)
        NIFContent.Save_As_Manolo(New_Nif, OverwriteShapeFiles)
        SaveHighHeel(New_Nif.Replace(".nif", ".hht"), OverwriteShapeFiles)
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
            If IsNothing(ParentSliderSet.NIFContent) Then Return False
            If IsNothing(ParentSliderSet.NIFContent.Blocks) Then Return False
            Return ParentSliderSet.NIFContent.Blocks.Where(Function(pf) pf.GetType Is GetType(BSClothExtraData)).Any
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
    Public ReadOnly Property IsReference As Boolean
        Get
            If IsExternal And ParentSliderSet.Shapes.Where(Function(pf) pf.IsExternal).Count = 1 Then Return True
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
            If Me.ParentSliderSet.NIFContent.NifShapes.Where(Function(pf) pf.Name.String.Equals(Me.Nombre, StringComparison.OrdinalIgnoreCase)).Any = False Then Return Nothing
            Dim finder = Me.ParentSliderSet.NIFContent.NifShapes.Where(Function(pf) pf.Name.String.Equals(Me.Nombre, StringComparison.OrdinalIgnoreCase)).FirstOrDefault
            If IsNothing(finder) Then Return finder
            Return TryCast(finder, BSTriShape)
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

    Public ReadOnly Property RelatedNifSkin As NiObject
        Get
            If IsNothing(RelatedNifShape) Then Return Nothing
            If IsNothing(RelatedNifShape.SkinInstanceRef) OrElse RelatedNifShape.SkinInstanceRef.Index = -1 Then Return Nothing
            Return TryCast(Me.ParentSliderSet.NIFContent.Blocks(RelatedNifShape.SkinInstanceRef.Index), NiObject)
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
            Select Case RelatedNifSkin.GetType
                Case GetType(BSSkin_Instance)
                    Return TryCast(RelatedNifSkin, BSSkin_Instance).Bones.Indices.Select(Function(pf) CType(Me.ParentSliderSet.NIFContent.Blocks(pf), NiNode)).ToList
                Case GetType(BSDismemberSkinInstance)
                    Return TryCast(RelatedNifSkin, BSDismemberSkinInstance).Bones.Indices.Select(Function(pf) CType(Me.ParentSliderSet.NIFContent.Blocks(pf), NiNode)).ToList
                Case GetType(NiSkinInstance)
                    Return TryCast(RelatedNifSkin, NiSkinInstance).Bones.Indices.Select(Function(pf) CType(Me.ParentSliderSet.NIFContent.Blocks(pf), NiNode)).ToList
                Case Else
                    Debugger.Break()
                    Throw New Exception
            End Select
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
            Return Me.ParentSliderSet.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.RelatedShape Is Me And pf.Islocal = False).Any
        End Get
    End Property
    Public ReadOnly Property HasLocalSliders As Boolean
        Get
            Return Me.ParentSliderSet.Sliders.SelectMany(Function(pf) pf.Datas).Where(Function(pf) pf.RelatedShape Is Me And pf.Islocal = True).Any
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
            Return CSng(Nodo.Attributes("default").Value)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("default")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("default")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("default").Value = value.ToString.ToString.Replace(System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator, ".")
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
            Return CSng(Nodo.Attributes("big").Value)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("big")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("big")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("big").Value = value.ToString.Replace(System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator, ".")
        End Set
    End Property
    Public Property Default_Small_Value As Single
        Get
            If IsNothing(Nodo.Attributes("small")) Then Return 0
            Return CSng(Nodo.Attributes("small").Value)
        End Get
        Set(value As Single)
            If IsNothing(Nodo.Attributes("small")) Then
                Dim attr As XmlAttribute = Me.ParentSliderSet.ParentOSP.xml.CreateAttribute("small")
                attr.Value = 0
                Nodo.Attributes.Append(attr)
            End If
            Nodo.Attributes("small").Value = value.ToString.Replace(System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator, ".")
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
            Return FullText.Split("\")(0)
        End Get
        Set(value As String)
            FullText = value + "\" + TargetSlider
        End Set
    End Property
    Public Property TargetSlider As String
        Get
            Return FullText.Split("\")(1)
        End Get
        Set(value As String)
            FullText = TargetOsd + "\" + value
        End Set
    End Property
    Public ReadOnly Property RelatedShape As Shape_class
        Get
            If ParentSlider.ParentSliderSet.Shapes.Where(Function(pq) pq.Target = Target).Any = 0 Then Return Nothing
            Return ParentSlider.ParentSliderSet.Shapes.Where(Function(pq) pq.Target.Equals(Target, StringComparison.OrdinalIgnoreCase)).First
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
            Return ParentSlider.ParentSliderSet.OSDContent_Local.Blocks.Where(Function(pf) pf.BlockName.Equals(Me.Nombre, StringComparison.OrdinalIgnoreCase) And Me.Islocal = True)
        End Get
    End Property
    Public ReadOnly Property RelatedExternalOSDBlocks As IEnumerable(Of OSD_Block_Class)
        Get
            Return ParentSlider.ParentSliderSet.OSDContent_External.Blocks.Where(Function(pf) pf.BlockName.Equals(Me.Nombre, StringComparison.OrdinalIgnoreCase) And Me.Islocal = False)
        End Get
    End Property

End Class

