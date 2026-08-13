Imports System.Drawing
Imports System.Reflection
Imports System.Windows.Forms

''' <summary>
''' Puebla los ImageList de los formularios desde PNG incrustados sueltos, en vez de desde un
''' ImageListStreamer serializado dentro del .resx.
''' </summary>
''' <remarks>
''' ⛔ NO devolver estas imagenes al .resx. El diseñador serializa un ImageList (y un Icon de
''' formulario) como &lt;data mimetype="application/x-microsoft.net.object.binary.base64"&gt;, y
''' GenerateResource trata cualquier resx con un nodo mimetype como "peligroso": si ademas la zona
''' de seguridad del archivo es >= Internet, el build muere con error MSB3821. Medido 2026-08-13:
''' todo lo que cuelga de C:\Users\jvare\OneDrive (el sync root) mapea a zona 4 con MapUrlToZone,
''' incluso archivos que no existen, asi que el gate se dispara solo por vivir ahi. Sacando los
''' mimetype el build pasa; los &lt;metadata type="System.Drawing.Point"&gt; del diseñador no molestan.
''' Como bonus esto saca los MSB3825: un ImageListStreamer se deserializa con BinaryFormatter, que
''' no existe en .NET 9.
'''
''' ⛔ El ORDEN de cada array de keys ES el ImageIndex. Hay Designer y codigo que indexan por numero
''' (Button.ImageIndex = 13) y por nombre (Button.ImageKey = "cancel.ico"): las dos vias tienen que
''' seguir resolviendo a la misma imagen. Si agregas un icono, va AL FINAL.
'''
''' Si abris uno de estos formularios en el diseñador de WinForms y guardas, el diseñador vuelve a
''' escribir el ImageStream en el .resx y el error vuelve. Revisar el resx despues de tocar el UI.
''' </remarks>
Friend Module FormImageLists

    ''' <summary>Iconos de Config_Form, en orden de ImageIndex.</summary>
    Private ReadOnly ConfigFormKeys As String() = {
        "agt_action_success.ico",
        "cancel.ico",
        "thumbnail.ico",
        "attach.ico",
        "agt_update_drivers.ico",
        "add_group.ico",
        "db_comit.ico",
        "db_update.ico"
    }

    ''' <summary>Iconos de Editor_Form, en orden de ImageIndex.</summary>
    Private ReadOnly EditorFormKeys As String() = {
        "agt_forum.ico",
        "configure.ico",
        "fileopen.ico",
        "filesave.ico",
        "filesaveas.ico",
        "agt_action_fail.ico",
        "agt_reload.ico",
        "agt_update_drivers.ico",
        "agt_virussafe.ico",
        "compfile.ico",
        "edit_add.ico",
        "edit_remove.ico",
        "connect_creating.ico",
        "connect_no.ico",
        "button_cancel.ico",
        "runprog.ico",
        "db_add.ico",
        "db_remove.ico",
        "applications-development.ico",
        "editcut.ico",
        "editcopy.ico",
        "filter.ico",
        "tab_duplicate.ico"
    }

    ''' <summary>Iconos de Wardrobe_Manager_Form, en orden de ImageIndex.</summary>
    Private ReadOnly MainFormKeys As String() = {
        "agt_action_fail.ico",
        "agt_action_success.ico",
        "mail_find.ico",
        "edit.ico",
        "1leftarrow.ico",
        "1rightarrow.ico",
        "2leftarrow.ico",
        "2rightarrow.ico",
        "1downarrow1.ico",
        "attach.ico",
        "appearance.ico",
        "folder_sent_mail.ico",
        "gear.ico",
        "personal.ico",
        "layer-visible-off.ico",
        "layer-visible-on.ico",
        "help-hint.ico",
        "Gnome-Video-X-Generic.ico"
    }

    Friend Sub FillConfigForm(list As ImageList)
        Fill(list, "Config_Form", ConfigFormKeys)
    End Sub

    Friend Sub FillEditorForm(list As ImageList)
        Fill(list, "Editor_Form", EditorFormKeys)
    End Sub

    Friend Sub FillMainForm(list As ImageList)
        Fill(list, "Wardrobe_Manager_Form", MainFormKeys)
    End Sub

    ''' <summary>Icono de la ventana de Wardrobe_Manager_Form (era $this.Icon en el resx).</summary>
    Friend Function MainFormIcon() As Icon
        Using stream = Resource("WMIcons.Wardrobe_Manager_Form.ico")
            Return New Icon(stream)
        End Using
    End Function

    ''' <summary>
    ''' ImageSize/ColorDepth/TransparentColor van ANTES de agregar: cambiarlos despues vacia la
    ''' coleccion. Los valores son los que traia el ImageListStreamer original de los tres forms.
    ''' </summary>
    Private Sub Fill(list As ImageList, prefix As String, keys As String())
        list.ImageSize = New Size(16, 16)
        list.ColorDepth = ColorDepth.Depth32Bit
        list.TransparentColor = Color.Transparent
        list.Images.Clear()

        For i = 0 To keys.Length - 1
            Using stream = Resource($"WMIcons.{prefix}_{i:D2}.png")
                Using fromStream As New Bitmap(stream)
                    ' Clone(rect, formato) copia los pixeles tal cual. New Bitmap(imagen) NO sirve:
                    ' hace un DrawImage y altera el alfa parcial (medido: hasta 97 px por icono).
                    ' Hay que despegar la imagen del Stream igual, porque un Bitmap construido
                    ' sobre un Stream exige que el Stream siga vivo mientras GDI+ lo use.
                    Dim copy = fromStream.Clone(New Rectangle(Point.Empty, fromStream.Size), Imaging.PixelFormat.Format32bppArgb)

                    ' El clon NO se libera: Images.Add guarda la referencia al original y crea el
                    ' HIMAGELIST recien cuando alguien pide el Handle (y lo puede volver a crear
                    ' despues). Si se dispone aca, el primer pintado tira ArgumentException
                    ' "Parameter is not valid". La duena pasa a ser la lista.
                    list.Images.Add(keys(i), copy)
                End Using
            End Using
        Next
    End Sub

    Private Function Resource(name As String) As IO.Stream
        Dim stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        If stream Is Nothing Then
            ' Sin esto el fallo aparece mucho despues como un boton sin icono, no como lo que es:
            ' un LogicalName que no coincide con el del .vbproj.
            Throw New IO.FileNotFoundException($"Falta el recurso incrustado '{name}'. Ver el ItemGroup de Resources\FormIcons en Wardrobe_Manager.vbproj.")
        End If
        Return stream
    End Function

End Module
