Imports NiflySharp

''' <summary>
''' Lo que comparten Split y Clone cuando dan de alta una shape NUEVA en el proyecto a partir de otra: el material
''' propio, el nodo &lt;Shape&gt; del .osp + su Shape_class, y los morphs (bloques OSD locales) del nuevo nombre.
''' ⛔ SEDE UNICA: Split y Clone no pueden divergir en ninguna de estas tres cosas.
''' <para>NO incluye "reescribir el original": eso es exclusivo de Split (que compacta el espacio de vertices de la
''' mitad original). Clone no toca ningun bloque del original — igual que <c>OutfitProject::DuplicateShape</c> de
''' Outfit Studio, que sólo hace <c>CopySet</c> (OutfitProject.cpp:5351-5401).</para>
''' </summary>
Friend NotInheritable Class ShapeDuplicacionNucleo

    Private Sub New()
    End Sub

    ''' <summary>Alta del material del nuevo en <c>BaseMaterials</c>: wrapper propio Y material propio
    ''' (<see cref="FO4UnifiedMaterial_Class.DuplicarConEstado"/>), con el mismo path. Ley: "una copia por shape,
    ''' como en la carga" — dos shapes que nombran el mismo .bgsm cargan dos objetos. Antes Split compartia el MISMO
    ''' objeto material entre las dos mitades: editar una editaba la otra.</summary>
    Public Shared Sub RegistrarMaterial(sliderSet As SliderSet_Class, nombreNifOrigen As String, nombreNuevo As String)
        Dim bm = sliderSet.NIFContent.BaseMaterials
        Dim orig As Nifcontent_Class_Manolo.RelatedMaterial_Class = Nothing
        ' SIEMPRE se asigna (no "si falta"): una entrada vieja con ese nombre —de una shape borrada antes, que
        ' Check_Unique_Shapename vuelve a proponer— le daria al nuevo el material de OTRA shape.
        If bm.TryGetValue(nombreNifOrigen, orig) Then
            bm(nombreNuevo) = New Nifcontent_Class_Manolo.RelatedMaterial_Class With {
                .path = orig.path,
                .material = orig.material?.DuplicarConEstado()
            }
        End If
    End Sub

    ''' <summary>Nodo &lt;Shape&gt; del .osp (insertado tras el original) y su Shape_class (insertada tras el original
    ''' en <c>Shapes</c>).
    ''' <para>⛔ LOS TRES ATRIBUTOS DE NORMALES VIAJAN CON LA SHAPE NUEVA, verbatim y solo si estan. El canonico los
    ''' propaga por shape al escribir el proyecto (OutfitProject.cpp:486-488). Sin esta copia, partir/clonar una prenda
    ''' con <c>LockNormals="true"</c> deja una con las normales autoradas y la otra recalculadas. Para Clone es un
    ''' desvio CONSCIENTE de Outfit Studio (su clon nace con los defaults), decidido por el usuario.</para>
    ''' <para><paramref name="heredarDataFolder"/>: Split conserva su conducta (hereda); Clone no, porque todos sus
    ''' datos son locales y en OS el clon no tiene dataFolders.</para></summary>
    Public Shared Function AgregarShapeAlProyecto(shape As Shape_class, sliderSet As SliderSet_Class, nombreNuevo As String,
                                                  heredarDataFolder As Boolean) As Shape_class
        Dim xml = sliderSet.ParentOSP.xml
        Dim nodo = CType(xml.CreateElement("Shape"), Xml.XmlElement)
        nodo.SetAttribute("target", nombreNuevo)
        For Each atributo In {"LockNormals", "SmoothSeamNormals", "SmoothSeamNormalsAngle"}
            Dim a = shape.Nodo?.Attributes(atributo)
            If a IsNot Nothing Then nodo.SetAttribute(atributo, a.Value)
        Next
        nodo.AppendChild(xml.CreateTextNode(nombreNuevo))
        sliderSet.Nodo.InsertAfter(nodo, shape.Nodo)
        Dim nueva As New Shape_class(nodo, sliderSet)
        If heredarDataFolder Then nueva.Datafolder = shape.Datafolder.ToList()
        sliderSet.Shapes.Insert(sliderSet.Shapes.IndexOf(shape) + 1, nueva)
        Return nueva
    End Function

    ''' <summary>Los datos de un slider que apuntan a la shape (los LOCALES tienen prioridad si hay de los dos) y, por
    ''' cada uno, la foto de sus diffs tomada ANTES de cualquier materializacion. Los bloques externos son de solo
    ''' lectura: se leen, nunca se escriben.</summary>
    Public Shared Function DatosDeLaShape(slider As Slider_class, shape As Shape_class) As (Datos As List(Of Slider_Data_class), Diffs As List(Of List(Of List(Of OSD_DataDiff_Class))))
        Dim datos = slider.Datas.Where(
            Function(d) d.Target.Equals(shape.Target, StringComparison.OrdinalIgnoreCase)).ToList()
        If datos.Any(Function(d) d.Islocal) Then datos = datos.Where(Function(d) d.Islocal).ToList()
        Dim diffs As New List(Of List(Of List(Of OSD_DataDiff_Class)))
        For Each dat In datos
            Dim bloques = If(dat.Islocal, dat.RelatedLocalOSDBlocks.ToList(), dat.RelatedExternalOSDBlocks.ToList())
            diffs.Add(bloques.Select(Function(b) b.SnapshotDiffs()).ToList())
        Next
        Return (datos, diffs)
    End Function

    ''' <summary>Los diffs que sobreviven a una compactacion del espacio de vertices, renumerados.</summary>
    Public Shared Function RemapDiffs(fuente As IEnumerable(Of OSD_DataDiff_Class),
                                      remap As IReadOnlyDictionary(Of Integer, Integer)) As List(Of OSD_DataDiff_Class)
        Return fuente.
            Where(Function(d) remap.ContainsKey(d.Index)).
            Select(Function(d) New OSD_DataDiff_Class() With {
                .Index = remap(d.Index), .X = d.X, .Y = d.Y, .Z = d.Z
            }).ToList()
    End Function

    ''' <summary>Copia identica (objetos nuevos, mismos indices): el espacio de vertices del clon ES el del original.</summary>
    Public Shared Function CopiarDiffs(fuente As IEnumerable(Of OSD_DataDiff_Class)) As List(Of OSD_DataDiff_Class)
        Return fuente.Select(Function(d) New OSD_DataDiff_Class() With {.Index = d.Index, .X = d.X, .Y = d.Y, .Z = d.Z}).ToList()
    End Function

    ''' <summary>Escribe (o reemplaza) el bloque OSD LOCAL del nuevo para un slider y su entrada &lt;Data&gt;.
    ''' Nombre del bloque: nuevo.Replace(":", "_") &amp; slider — la convencion de la app (OS conserva ":"; el nombre es
    ''' solo un alias, sin efecto en la conducta).</summary>
    Public Shared Sub EscribirMorphsDelNuevo(sliderSet As SliderSet_Class, slider As Slider_class, nombreNuevo As String,
                                             diffs As List(Of OSD_DataDiff_Class))
        If diffs.Count = 0 Then Exit Sub
        Dim osdFilename = sliderSet.LocalOsdFileName
        Dim osdContent = sliderSet.OSDContent_Local
        Dim nombreBloque = nombreNuevo.Replace(":", "_") & slider.Nombre
        Dim bloque = osdContent.Blocks.FirstOrDefault(
            Function(b) b.BlockName.Equals(nombreBloque, StringComparison.OrdinalIgnoreCase))
        If bloque Is Nothing Then
            bloque = New OSD_Block_Class(osdContent) With {.BlockName = nombreBloque}
            osdContent.Blocks.Add(bloque)
        End If
        bloque.DataDiff.Clear()
        bloque.DataDiff.AddRange(diffs)
        bloque.RebuildCompactArrays()

        Dim dat = slider.Datas.FirstOrDefault(
            Function(d) d.Target.Equals(nombreNuevo, StringComparison.OrdinalIgnoreCase) AndAlso
                        d.Nombre.Equals(nombreBloque, StringComparison.OrdinalIgnoreCase))
        If dat Is Nothing Then
            slider.Datas.Add(New Slider_Data_class(nombreBloque, slider, nombreNuevo, osdFilename))
        Else
            dat.Islocal = True
            dat.TargetOsd = osdFilename
        End If
    End Sub

End Class
