' Version Uploaded of Wardrobe 2.1.3
' ========================
' == Stubs y utilidades ==
' ========================
Imports System.Numerics
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports NiflySharp.Structs

' =========================
' == Datos de Morph/Shape ==
' =========================


' C++ usa shared_ptr<MorphdataTri>; en .NET las clases ya son por referencia.

Public Class TriFile
    ' map<string, vector<MorphdataTriPtr>>
    Public shapeMorphs As New Dictionary(Of String, List(Of MorphdataTri))()

    Public Enum MorphType As Byte
        MORPHTYPE_POSITION = 0
        MORPHTYPE_UV = 1
    End Enum
    Public Class MorphdataTri
        <JsonPropertyName("name")>
        Public Property Name As String = ""

        <JsonPropertyName("morph")>
        Public Property Morph As String = ""
        <JsonPropertyName("minimum")>
        Public Property Minimum As Single = 0.0F
        <JsonPropertyName("maximum")>
        Public Property Maximum As Single = 1.0F
        <JsonPropertyName("interval")>
        Public Property Interval As Single = 0.01F

        <JsonPropertyName("gender")>
        Public Property Gender As Integer = 1

        <Json.Serialization.JsonIgnore>
        Public type As MorphType = MorphType.MORPHTYPE_POSITION

        <Json.Serialization.JsonIgnore>
        Public offsets As New Dictionary(Of UShort, Vector3)()
    End Class

    Private Shared BSSliders As New List(Of MorphdataTri)
    Private Shared WMSliders As New List(Of MorphdataTri)

    Public Shared Sub Read_Looksmenu_Sliders()
        BSSliders = Deserialize_LooksmenuSiliders(Wardrobe_Manager_Form.Directorios.LooksMenuBSSliders)
        ' Lee los sliders ya grabados solo si no se resetean
        If Config_App.Current.Settings_Build.ResetSlidersEachBuild Then
            WMSliders = Deserialize_LooksmenuSiliders(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        End If
    End Sub

    Public Shared Function Deserialize_LooksmenuSiliders(SliderFile As String) As List(Of MorphdataTri)
        Try
            If IO.File.Exists(SliderFile) = False Then Return New List(Of MorphdataTri)
            Dim json As String = IO.File.ReadAllText(SliderFile)
            Dim model As List(Of MorphdataTri) = JsonSerializer.Deserialize(Of List(Of MorphdataTri))(json, Jsonopts)
            Return model
        Catch ex As Exception
            Return New List(Of MorphdataTri)
        End Try
    End Function

    Private Shared ReadOnly Jsonopts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True, .NumberHandling = JsonNumberHandling.AllowReadingFromString, .WriteIndented = True}

    Public Shared Sub Serialize_LooksmenuAdditionalSiliders()
        Try
            If WMSliders.Count > 0 Then
                Dim dir = IO.Path.GetDirectoryName(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
                If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)
                Dim jsonOut As String = JsonSerializer.Serialize(Of List(Of MorphdataTri))(WMSliders, Jsonopts)
                IO.File.WriteAllText(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders, jsonOut)
            Else
                If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
            End If
        Catch ex As Exception
            MsgBox("Error creating additional sliders for looksmenu", vbCritical, "Error")
        End Try
    End Sub



    Public Function Write(fileName As String) As Boolean
        Try
            Using fs As New IO.FileStream(fileName, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.None)
                Using bw As New IO.BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen:=False)
                    ' Header "TRIP"
                    ' En C++ escriben el uint32_t resultante de "TRIP"_mci; aquí escribimos los 4 bytes ASCII.
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("PIRT" & ""))

                    ' ---- Sección de POSICIONES ----
                    Dim shapeCountPos As UShort = GetShapeCount(MorphType.MORPHTYPE_POSITION)
                    bw.Write(shapeCountPos)

                    If shapeCountPos > 0US Then
                        ' 1) tomar solo shapes que se van a escribir y ordenarlos por nombre (Ordinal)
                        Dim shapesPos = shapeMorphs.Keys.Where(Function(sn) GetMorphCount(sn, MorphType.MORPHTYPE_POSITION) > 0US)

                        For Each shapeName In shapesPos
                            Dim kv = shapeMorphs(shapeName)
                            Dim morphCount As UShort = GetMorphCount(shapeName, MorphType.MORPHTYPE_POSITION)

                            Dim shapeLen As Byte = CByte(shapeName.Length)
                            bw.Write(shapeLen)
                            If shapeLen > 0 Then bw.Write(System.Text.Encoding.ASCII.GetBytes(shapeName))

                            bw.Write(morphCount)

                            ' 2) morphs solo de POSITION, ordenados por nombre (Ordinal)
                            Dim morphsPos = kv.Where(Function(m) m.type = MorphType.MORPHTYPE_POSITION).OrderBy(Function(m) m.morph, StringComparer.Ordinal)

                            For Each morph In morphsPos
                                Dim morphName As String = morph.morph
                                Dim morphLen As Byte = CByte(morphName.Length)
                                bw.Write(morphLen)
                                If morphLen > 0 Then bw.Write(System.Text.Encoding.ASCII.GetBytes(morphName))

                                ' mult = max(|x|,|y|,|z|) / 0x7FFF
                                Dim mult As Single = 0.0F
                                For Each off In morph.offsets.Values
                                    If Math.Abs(off.X) > mult Then mult = Math.Abs(off.X)
                                    If Math.Abs(off.Y) > mult Then mult = Math.Abs(off.Y)
                                    If Math.Abs(off.Z) > mult Then mult = Math.Abs(off.Z)
                                Next
                                mult /= CSng(&H7FFF)
                                bw.Write(mult)

                                Dim mcount As UShort = CUShort(morph.offsets.Count)
                                bw.Write(mcount)

                                ' offsets ordenados por id
                                For Each kvp In morph.offsets.OrderBy(Function(p) p.Key)
                                    Dim id As UShort = kvp.Key
                                    Dim v As Vector3 = kvp.Value
                                    If mult <> 0F Then
                                        Dim sx As Short = CType(Fix(v.X / mult), Short)
                                        Dim sy As Short = CType(Fix(v.Y / mult), Short)
                                        Dim sz As Short = CType(Fix(v.Z / mult), Short)
                                        bw.Write(id) : bw.Write(sx) : bw.Write(sy) : bw.Write(sz)
                                    Else
                                        bw.Write(id) : bw.Write(0S) : bw.Write(0S) : bw.Write(0S)
                                    End If
                                Next
                            Next
                        Next
                    End If

                    ' ---- Sección de UV ----
                    Dim shapeCountUV As UShort = GetShapeCount(MorphType.MORPHTYPE_UV)
                    bw.Write(shapeCountUV)

                    If shapeCountUV > 0US Then
                        Dim shapesUV = shapeMorphs.Keys.Where(Function(sn) GetMorphCount(sn, MorphType.MORPHTYPE_UV) > 0US)

                        For Each shapeName In shapesUV
                            Dim kv = shapeMorphs(shapeName)
                            Dim morphCount As UShort = GetMorphCount(shapeName, MorphType.MORPHTYPE_UV)

                            Dim shapeLen As Byte = CByte(shapeName.Length)
                            bw.Write(shapeLen)
                            If shapeLen > 0 Then bw.Write(System.Text.Encoding.ASCII.GetBytes(shapeName))

                            bw.Write(morphCount)

                            Dim morphsUV = kv.Where(Function(m) m.type = MorphType.MORPHTYPE_UV).OrderBy(Function(m) m.morph, StringComparer.Ordinal)

                            For Each morph In morphsUV
                                Dim morphName As String = morph.morph
                                Dim morphLen As Byte = CByte(morphName.Length)
                                bw.Write(morphLen)
                                If morphLen > 0 Then bw.Write(System.Text.Encoding.ASCII.GetBytes(morphName))

                                ' mult = max(|x|,|y|) / 0x7FFF
                                Dim mult As Single = 0.0F
                                For Each off In morph.offsets.Values
                                    If Math.Abs(off.X) > mult Then mult = Math.Abs(off.X)
                                    If Math.Abs(off.Y) > mult Then mult = Math.Abs(off.Y)
                                Next
                                mult /= CSng(&H7FFF)
                                bw.Write(mult)

                                Dim mcount As UShort = CUShort(morph.offsets.Count)
                                bw.Write(mcount)

                                ' offsets ordenados por id
                                For Each kvp In morph.offsets.OrderBy(Function(p) p.Key)
                                    Dim id As UShort = kvp.Key
                                    Dim v As Vector3 = kvp.Value
                                    If mult <> 0F Then
                                        Dim sx As Short = CType(Fix(v.X / mult), Short)
                                        Dim sy As Short = CType(Fix(v.Y / mult), Short)
                                        bw.Write(id) : bw.Write(sx) : bw.Write(sy)
                                    Else
                                        bw.Write(id) : bw.Write(0S) : bw.Write(0S)
                                    End If
                                Next
                            Next
                        Next
                    End If
                End Using
            End Using
        Catch
            Return False
        End Try

        Return True
    End Function

    Public Sub AddMorph(shapeName As String, data As MorphdataTri)
        Dim list As List(Of MorphdataTri) = Nothing
        If shapeMorphs.TryGetValue(shapeName, list) Then
            Dim exists = list.Exists(Function(md) md.morph = data.morph)
            If Not exists Then list.Add(data)
        Else
            shapeMorphs(shapeName) = New List(Of MorphdataTri)()
            AddMorph(shapeName, data)
        End If
    End Sub

    Public Function GetShapeCount(morphType As MorphType) As UShort
        Dim count As Integer = 0
        For Each kv In shapeMorphs
            If kv.Value.Any(Function(md) md.type = morphType) Then count += 1
        Next
        Return CUShort(count)
    End Function

    Public Function GetMorphCount(shapeName As String, morphType As MorphType) As UShort
        Dim list As List(Of MorphdataTri) = Nothing
        If shapeMorphs.TryGetValue(shapeName, list) Then
            Dim c = list.Where(Function(md) md.type = morphType).Count
            Return CUShort(c)
        End If
        Return 0US
    End Function

    ' ===== Implementación VB.NET del método solicitado =====
    Public Shared Function WriteMorphTRI(triPath As String, sliderSet As SliderSet_Class) As Boolean
        Dim tri As New TriFile()
        Dim triFilePath As String = triPath
        For Each shape In sliderSet.NIFContent.GetShapes
            Dim targetShape = sliderSet.Shapes.Where(Function(pf) pf.RelatedNifShape Is shape).FirstOrDefault
            If targetShape Is Nothing Then Continue For
            Dim shapeVertCount As Integer = shape.VertexCount

            If shapeVertCount <= 0 Then Continue For
            For s As Integer = 0 To sliderSet.Sliders.Count - 1
                Dim idxs = s
                If Not sliderSet.Sliders(s).IsClamp AndAlso Not sliderSet.Sliders(s).IsZap AndAlso (Not sliderSet.Sliders(s).IsManoloFix OrElse Config_App.Current.Settings_Build.SkipManoloFixMorphs = False) Then
                    Dim morph As New MorphdataTri With {
                        .morph = sliderSet.Sliders(s).Nombre
                    }

                    If sliderSet.Sliders(s).IsUV Then
                        morph.type = MorphType.MORPHTYPE_UV
                        Dim uvs As New List(Of Vector2)(Enumerable.Repeat(New Vector2(0.0F, 0.0F), shapeVertCount))
                        Dim dat = targetShape.Related_Slider_data.Where(Function(pf) pf.ParentSlider Is sliderSet.Sliders(idxs)).FirstOrDefault
                        If Not IsNothing(dat) Then
                            Dim DiffData = dat.RelatedOSDBlocks
                            For Each dif In DiffData
                                For Each dif2 In dif.DataDiff
                                    uvs(dif2.Index) = New Vector2(dif2.X, dif2.Y)
                                Next
                            Next
                        End If

                        Dim idxv As Integer = 0
                        For Each uv In uvs
                            Dim v As New Vector3(uv.X, uv.Y, 0.0F)
                            If Not (v.X = 0 AndAlso v.Y = 0 AndAlso v.Z = 0) Then
                                morph.offsets(idxv) = v
                            End If
                            idxv += 1
                        Next
                    Else
                        morph.type = MorphType.MORPHTYPE_POSITION
                        Dim verts As New List(Of Vector3)(Enumerable.Repeat(New Vector3(0.0F, 0.0F, 0.0F), shapeVertCount))
                        Dim dat = targetShape.Related_Slider_data.Where(Function(pf) pf.ParentSlider Is sliderSet.Sliders(idxs)).FirstOrDefault
                        If Not IsNothing(dat) Then
                            Dim DiffData = dat.RelatedOSDBlocks
                            For Each dif In DiffData
                                For Each dif2 In dif.DataDiff
                                    verts(dif2.Index) = New Vector3(dif2.X, dif2.Y, dif2.Z)
                                Next
                            Next
                        End If

                        Dim idxv As Integer = 0
                        For Each v In verts
                            If Not (v.X = 0 AndAlso v.Y = 0 AndAlso v.Z = 0) Then
                                morph.offsets(idxv) = v
                            End If
                            idxv += 1
                        Next
                    End If

                    If morph.offsets.Count > 0 Then
                        tri.AddMorph(shape.Name.String, morph)
                        If Config_App.Current.Settings_Build.AddAddintionalSliders Then
                            If BSSliders.Where(Function(pf) pf.morph.Equals(morph.morph)).Any = False Then
                                If WMSliders.Where(Function(pf) pf.morph.Equals(morph.morph)).Any = False Then
                                    WMSliders.Add(New MorphdataTri With {.name = "$" + morph.morph, .morph = morph.morph})
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        Next

        If Not tri.Write(triFilePath) Then Return False
        Return True
    End Function
    ' ====== LECTURA DEL FORMATO TRI DESDE MEMORIA ======

    Public Shared Function ParseTriFromBytes(data As Byte()) As TriFile
        If data Is Nothing OrElse data.Length < 4 Then
            Throw New FormatException("Datos insuficientes: no hay espacio para el header 'PIRT'.")
        End If

        Dim tri As New TriFile()

        Using ms As New IO.MemoryStream(data, writable:=False)
            Using br As New IO.BinaryReader(ms, System.Text.Encoding.ASCII, leaveOpen:=False)
                ValidateHeader(br)

                ' Sección de POSICIONES
                ReadSection(br, tri, MorphType.MORPHTYPE_POSITION)

                ' Sección de UV
                ReadSection(br, tri, MorphType.MORPHTYPE_UV)
            End Using
        End Using

        Return tri
    End Function

    Public Shared Function ParseTriFromFile(path As String) As TriFile
        If Not IO.File.Exists(path) Then
            Throw New IO.FileNotFoundException("Archivo TRI no encontrado.", path)
        End If
        Dim bytes As Byte() = IO.File.ReadAllBytes(path)
        Return ParseTriFromBytes(bytes)
    End Function

    Private Shared Sub ValidateHeader(br As IO.BinaryReader)
        Dim hdr As Byte() = br.ReadBytes(4)
        If hdr Is Nothing OrElse hdr.Length <> 4 Then
            Throw New FormatException("No se pudo leer el header del TRI.")
        End If
        ' Escritura original: Encoding.ASCII.GetBytes("PIRT")
        If Not (hdr(0) = AscW("P"c) AndAlso hdr(1) = AscW("I"c) AndAlso hdr(2) = AscW("R"c) AndAlso hdr(3) = AscW("T"c)) Then
            Throw New FormatException("Header inválido. Se esperaba 'PIRT'.")
        End If
    End Sub

    Private Shared Sub ReadSection(br As IO.BinaryReader, tri As TriFile, sectionType As MorphType)
        ' shapeCount de la sección (UInt16)
        Dim shapeCount As UShort = br.ReadUInt16()

        For i As Integer = 0 To shapeCount - 1
            ' Nombre del shape
            Dim shapeLen As Integer = br.ReadByte()
            Dim shapeName As String = ReadAscii(br, shapeLen)

            ' Cantidad de morphs de ESTA sección para ese shape
            Dim morphCount As UShort = br.ReadUInt16()

            For m As Integer = 0 To morphCount - 1
                ' Nombre del morph
                Dim morphLen As Integer = br.ReadByte()
                Dim morphName As String = ReadAscii(br, morphLen)

                ' mult y cantidad de offsets
                Dim mult As Single = br.ReadSingle()
                Dim mcount As UShort = br.ReadUInt16()

                Dim morph As New MorphdataTri With {
                    .morph = morphName,
                    .type = sectionType
                }

                For k As Integer = 0 To mcount - 1
                    Dim vid As UShort = br.ReadUInt16()

                    If sectionType = MorphType.MORPHTYPE_POSITION Then
                        Dim sx As Short = br.ReadInt16()
                        Dim sy As Short = br.ReadInt16()
                        Dim sz As Short = br.ReadInt16()

                        Dim x As Single = CSng(sx) * mult
                        Dim y As Single = CSng(sy) * mult
                        Dim z As Single = CSng(sz) * mult

                        ' Descartar ceros exactos para respetar la escritura
                        If Not (x = 0.0F AndAlso y = 0.0F AndAlso z = 0.0F) Then
                            morph.offsets(vid) = New Vector3(x, y, z)
                        End If
                    Else
                        ' MORPHTYPE_UV
                        Dim sx As Short = br.ReadInt16()
                        Dim sy As Short = br.ReadInt16()

                        Dim x As Single = CSng(sx) * mult
                        Dim y As Single = CSng(sy) * mult

                        If Not (x = 0.0F AndAlso y = 0.0F) Then
                            morph.offsets(vid) = New Vector3(x, y, 0.0F)
                        End If
                    End If
                Next

                If morph.offsets.Count > 0 Then
                    tri.AddMorph(shapeName, morph)
                End If
            Next
        Next
    End Sub

    Private Shared Function ReadAscii(br As IO.BinaryReader, len As Integer) As String
        If len < 0 Then
            Throw New FormatException("Longitud de cadena negativa.")
        End If
        If len = 0 Then
            Return String.Empty
        End If
        Dim bytes As Byte() = br.ReadBytes(len)
        If bytes Is Nothing OrElse bytes.Length <> len Then
            Throw New FormatException("No se pudieron leer los bytes del nombre ASCII.")
        End If
        ' Fallback silencioso para bytes > 0x7F: Encoding.ASCII los mapea a '?'
        Return System.Text.Encoding.ASCII.GetString(bytes)
    End Function


End Class

