Imports System.IO
Imports System.Net
Imports System.Xml
Imports K4os.Hash.xxHash
Imports NiflySharp
Imports NiflySharp.Blocks

Public Class Create_from_Nif_Form
    Private WithEvents EditPreviewControl As New PreviewControl
    Private Selected_OSP As New OSP_Project_Class
    Private selected_slider As New SliderSet_Class(Selected_OSP)
    Private HasSaved As Boolean = False

    Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub
    Public Sub New(keys As List(Of String), rootPrefix As String, allowedExts As HashSet(Of String), initialkey As String)
        InitializeComponent()
        ArgumentNullException.ThrowIfNull(keys)
        ArgumentNullException.ThrowIfNull(allowedExts)
        Me.DictionaryPicker_Control1.Initialize(keys, rootPrefix, allowedExts)
        Me.DictionaryPicker_Control1.Preselect(initialkey)
        Me.DictionaryPicker_Control1.AllowClone = False
        Me.DictionaryPicker_Control1.btnOk.Text = "Create"
        Me.DictionaryPicker_Control1.btnCancel.Text = "Exit"
    End Sub


    Private Sub Create_from_Nif_2_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        EditPreviewControl = New PreviewControl With {.Dock = DockStyle.Fill}
        Panel1.Controls.Add(EditPreviewControl)
        EditPreviewControl.Model.SingleBoneSkinning = False
        EditPreviewControl.Model.RecalculateNormals = False
        EditPreviewControl.AllowMask = False
    End Sub

    Private Sub DictionaryPicker_Control1_OkClicked() Handles DictionaryPicker_Control1.OkClicked
        Try
            If IsNothing(selected_slider) Then Exit Sub
            If selected_slider.Unreadable_NIF Then Throw New Exception("Unreadable NIF")
            If selected_slider.Unreadable_Project Then Throw New Exception("Unreadable Project")
            Dim OSPFIle = Path.Combine(Wardrobe_Manager_Form.Directorios.SliderSetsRoot, TextBox1.Text) + ".osp"
            If IO.File.Exists(OSPFIle) Then Throw New Exception("OSP File already exist")
            Dim New_Nif = Path.Combine(Wardrobe_Manager_Form.Directorios.ShapedataRoot, TextBox1.Text + "\" + TextBox1.Text + ".nif")
            Dim New_osd = Path.Combine(Wardrobe_Manager_Form.Directorios.ShapedataRoot, TextBox1.Text + "\" + TextBox1.Text + ".osd")

            If Directory.Exists(Path.GetDirectoryName(New_Nif)) = False Then
                Directory.CreateDirectory(Path.GetDirectoryName(New_Nif))
            End If

            For Each sli In selected_slider.Sliders
                For Each da In sli.Datas
                    da.TargetOsd = IO.Path.GetFileName(New_osd)
                Next
            Next
            selected_slider.NIFContent.Save_As_Manolo(New_Nif, False)
            selected_slider.OSDContent_Local.Save_As(New_osd, False)
            selected_slider.Nombre = TextBox1.Text
            selected_slider.DataFolderValue = TextBox1.Text
            selected_slider.SourceFileValue = TextBox1.Text + ".nif"
            Selected_OSP.Save_Pack_As(OSPFIle, False)
            Me.HasSaved = True
            MsgBox("Project created", vbInformation, "Success")

        Catch ex As Exception
            MsgBox("Error creating project:" + ex.ToString, vbCritical, "Error")
        End Try
    End Sub

    Private Sub DictionaryPicker_Control1_CancelClicked() Handles DictionaryPicker_Control1.CancelClicked
        Me.Close()

    End Sub

    Private Sub Create_from_Nif_2_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        EditPreviewControl.Clean()
        EditPreviewControl.Dispose()
        If Me.HasSaved = True Then
            Me.DialogResult = DialogResult.Yes
        Else
            Me.DialogResult = DialogResult.No
        End If
    End Sub




    Private Sub Read_selected(key As String)
        Dim fil As String = key
        Dim tri = fil.Replace(".nif", ".tri")
        selected_slider.ParentOSP.xml.DocumentElement.InnerText = ""
        selected_slider = New SliderSet_Class(selected_slider.ParentOSP)

        CheckBox1.Enabled = FilesDictionary_class.Dictionary.ContainsKey(tri)

        Try
            Dim TriFileParese As TriFile = Nothing

            Dim value As FilesDictionary_class.File_Location = Nothing

            If FilesDictionary_class.Dictionary.TryGetValue(tri, value) AndAlso CheckBox1.Checked = True Then
                Try
                    TriFileParese = TriFile.ParseTriFromBytes(value.GetBytes)
                Catch ex As Exception
                End Try

            End If

            selected_slider.NIFContent.Load_Manolo(FilesDictionary_class.Dictionary(fil).GetBytes)
            For Each shap In selected_slider.NIFContent.GetShapes
                If Nifcontent_Class_Manolo.SupportedShape(shap.GetType) Then
                    Dim shapec As New Shape_class(shap.Name.String, selected_slider)
                    selected_slider.Shapes.Add(shapec)
                End If
            Next

            selected_slider.OSDContent_Local = New OSD_Class(selected_slider)

            If Not IsNothing(TriFileParese) Then
                Try
                    For Each shapeMorph In TriFileParese.shapeMorphs
                        For Each morp In shapeMorph.Value
                            If selected_slider.Sliders.Where(Function(pf) pf.Nombre.Equals(morp.Morph)).Any = False Then
                                selected_slider.Sliders.Add(New Slider_class(morp.Morph, selected_slider, morp.type))
                            End If
                            Dim slider = selected_slider.Sliders.Where(Function(pf) pf.Nombre.Equals(morp.Morph)).First
                            Dim dat As Slider_Data_class
                            Dim datnombre = shapeMorph.Key.Replace(":", "_") + slider.Nombre
                            If slider.Datas.Where(Function(pf) pf.Nombre = datnombre).Any = False Then
                                slider.Datas.Add(New Slider_Data_class(datnombre, slider, shapeMorph.Key, "Tochange.osd"))
                            End If
                            dat = slider.Datas.Where(Function(pf) pf.Nombre = datnombre).First

                            Dim block = New OSD_Block_Class(selected_slider.OSDContent_Local) With {.BlockName = dat.Nombre, .ParentOSDContent = selected_slider.OSDContent_Local, .DataDiff = New List(Of OSD_DataDiff_Class)}
                            selected_slider.OSDContent_Local.Blocks.Add(block)
                            For Each dif In morp.offsets
                                Dim newdd = New OSD_DataDiff_Class With {.Index = dif.Key, .X = dif.Value.X, .Y = dif.Value.Y, .Z = dif.Value.Z}
                                block.DataDiff.Add(newdd)
                            Next
                        Next
                    Next
                Catch ex As Exception
                    Debugger.Break()
                End Try
            End If

            selected_slider.Unreadable_NIF = False

        Catch ex As Exception
            Debugger.Break()
            selected_slider.Unreadable_NIF = True
        End Try


        TextBox1.Text = IO.Path.GetFileNameWithoutExtension(fil)
        selected_slider.OutputPathValue = IO.Path.GetDirectoryName(fil)
        selected_slider.OutputFileValue = IO.Path.GetFileNameWithoutExtension(fil)
        selected_slider.SourceFileValue = fil
        EditPreviewControl.Model.Last_rendered = Nothing
        EditPreviewControl.Update_Render(selected_slider, True, Nothing, Nothing, Config_App.SliderSize.Default)
    End Sub
    Private Last_key As String = ""
    Private Sub DictionaryPicker_Control1_SelectionChanged(Key As String) Handles DictionaryPicker_Control1.SelectionChanged
        Last_key = Key
        If Key <> "" Then
            Read_selected(Key)
        Else
            EditPreviewControl.Model.Clean(False)
            CheckBox1.Enabled = False
        End If
    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged
        If Last_key <> "" Then Read_selected(Last_key)
    End Sub
End Class