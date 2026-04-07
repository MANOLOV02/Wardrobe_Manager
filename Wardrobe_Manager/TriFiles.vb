Imports OpenTK.Mathematics
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading.Tasks
Imports FO4_Base_Library

' ============================================================================
' LooksMenu Slider Descriptors and TRI Build Logic
' MorphdataTri = slider metadata for LooksMenu JSON export (not TRI I/O).
' TRI binary I/O is handled entirely by FO4_Base_Library.TriFileParser/TriFileWriter.
' ============================================================================

''' <summary>
''' LooksMenu slider descriptor. Used for JSON serialization of slider metadata
''' and as an intermediate when building TRI files from SliderSet data.
''' NOT a TRI file format class - TRI I/O uses FO4_Base_Library.TriFile directly.
''' </summary>
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

    <JsonIgnore>
    Public MorphType As TriMorphType = TriMorphType.Position

    <JsonIgnore>
    Public Offsets As New Dictionary(Of UShort, Vector3)()
End Class

''' <summary>
''' LooksMenu slider management and TRI build from SliderSet data.
''' </summary>
Public Module LooksMenuSliders

    Private BSSliders As New List(Of MorphdataTri)
    Private WMSliders As New List(Of MorphdataTri)
    Private ReadOnly Jsonopts As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .NumberHandling = JsonNumberHandling.AllowReadingFromString,
        .WriteIndented = True
    }

    Public Sub Read_Looksmenu_Sliders()
        BSSliders = DeserializeLooksMenuSliders(Wardrobe_Manager_Form.Directorios.LooksMenuBSSliders)
        WMSliders = New List(Of MorphdataTri)
        If WM_Config.Current.Settings_Build.ResetSlidersEachBuild = False Then
            WMSliders = DeserializeLooksMenuSliders(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        End If
    End Sub

    Public Function DeserializeLooksMenuSliders(sliderFile As String) As List(Of MorphdataTri)
        Try
            If Not IO.File.Exists(sliderFile) Then Return New List(Of MorphdataTri)
            Dim json = IO.File.ReadAllText(sliderFile)
            Dim model = JsonSerializer.Deserialize(Of List(Of MorphdataTri))(json, Jsonopts)
            Return If(model, New List(Of MorphdataTri))
        Catch
            Return New List(Of MorphdataTri)
        End Try
    End Function

    Public Sub Serialize_LooksmenuAdditionalSiliders()
        Try
            If WMSliders.Count > 0 Then
                Dim dir = IO.Path.GetDirectoryName(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
                If Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)
                Dim jsonOut = JsonSerializer.Serialize(Of List(Of MorphdataTri))(WMSliders, Jsonopts)
                IO.File.WriteAllText(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders, jsonOut)
            Else
                If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then
                    IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
                End If
            End If
        Catch ex As Exception
            MsgBox("Error creating additional sliders for looksmenu", vbCritical, "Error")
        End Try
    End Sub

    ''' <summary>
    ''' Build a TRI file from a SliderSet and write it to disk.
    ''' Constructs FO4_Base_Library.TriFile directly - no wrapper class.
    ''' </summary>
    Public Function WriteMorphTRI(triPath As String, sliderSet As SliderSet_Class) As Boolean
        Dim tri As New TriFile()
        Dim addAdditional = WM_Config.Current.Settings_Build.AddAddintionalSliders
        Dim skipManoloFix = WM_Config.Current.Settings_Build.SkipFixMorphs

        For Each shape In sliderSet.NIFContent.GetShapes
            Dim targetShape = sliderSet.Shapes.Where(Function(pf) pf.RelatedNifShape Is shape).FirstOrDefault
            If targetShape Is Nothing Then Continue For
            Dim shapeVertCount As Integer = shape.VertexCount
            If shapeVertCount <= 0 Then Continue For

            Dim candidateIndices As New List(Of Integer)
            For s = 0 To sliderSet.Sliders.Count - 1
                If Not sliderSet.Sliders(s).IsClamp AndAlso Not sliderSet.Sliders(s).IsZap AndAlso
                   (Not sliderSet.Sliders(s).IsManoloFix OrElse skipManoloFix = False) Then
                    candidateIndices.Add(s)
                End If
            Next

            ' Pre-allocate for parallel morph quantization
            Dim morphResults(candidateIndices.Count - 1) As TriMorphEntry
            Dim morphNames(candidateIndices.Count - 1) As String

            Dim localTargetShape = targetShape
            Dim localVertCount = shapeVertCount
            Parallel.For(0, candidateIndices.Count,
                Sub(ci)
                    Dim slider = sliderSet.Sliders(candidateIndices(ci))
                    Dim entry As New TriMorphEntry With {
                        .Name = slider.Nombre,
                        .MorphType = If(slider.IsUV, TriMorphType.UV, TriMorphType.Position)
                    }

                    If slider.IsUV Then
                        Dim uvs(localVertCount - 1) As Vector2
                        Dim dat = localTargetShape.Related_Slider_data.
                            Where(Function(pf) pf.ParentSlider Is slider).
                            OrderByDescending(Function(pf) pf.Islocal).FirstOrDefault
                        If dat IsNot Nothing Then
                            For Each dif In dat.RelatedOSDBlocks
                                For Each dif2 In dif.DataDiff
                                    uvs(dif2.Index) = New Vector2(dif2.X, dif2.Y)
                                Next
                            Next
                        End If
                        For idxv = 0 To localVertCount - 1
                            If uvs(idxv).X <> 0 OrElse uvs(idxv).Y <> 0 Then
                                entry.Offsets(CUShort(idxv)) = New Vector3(uvs(idxv).X, uvs(idxv).Y, 0.0F)
                            End If
                        Next
                    Else
                        Dim verts(localVertCount - 1) As Vector3
                        Dim dat = localTargetShape.Related_Slider_data.
                            Where(Function(pf) pf.ParentSlider Is slider).
                            OrderByDescending(Function(pf) pf.Islocal).FirstOrDefault
                        If dat IsNot Nothing Then
                            For Each dif In dat.RelatedOSDBlocks
                                For Each dif2 In dif.DataDiff
                                    verts(dif2.Index) = New Vector3(dif2.X, dif2.Y, dif2.Z)
                                Next
                            Next
                        End If
                        For idxv = 0 To localVertCount - 1
                            If verts(idxv).X <> 0 OrElse verts(idxv).Y <> 0 OrElse verts(idxv).Z <> 0 Then
                                entry.Offsets(CUShort(idxv)) = verts(idxv)
                            End If
                        Next
                    End If

                    If entry.Offsets.Count > 0 Then
                        morphResults(ci) = entry
                        morphNames(ci) = slider.Nombre
                    End If
                End Sub)

            ' Sequential merge + WMSliders tracking
            Dim shapeName = shape.Name.String
            For ci = 0 To morphResults.Length - 1
                Dim entry = morphResults(ci)
                If entry IsNot Nothing Then
                    tri.AddMorph(shapeName, entry)
                    If addAdditional Then
                        Dim mname = morphNames(ci)
                        If Not BSSliders.Any(Function(pf) pf.Morph.Equals(mname, StringComparison.OrdinalIgnoreCase)) Then
                            If Not WMSliders.Any(Function(pf) pf.Morph.Equals(mname, StringComparison.OrdinalIgnoreCase)) Then
                                WMSliders.Add(New MorphdataTri With {.Name = "$" + mname, .Morph = mname})
                            End If
                        End If
                    End If
                End If
            Next
        Next

        Return tri.Write(triPath)
    End Function

End Module
