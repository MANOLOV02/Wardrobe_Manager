Imports System.Xml
Imports NiflySharp

''' <summary>
''' Rename de una shape del proyecto: todo lo que la nombra por texto. Canon: <c>OutfitProject::RenameShape</c>
''' (OutfitProject.cpp:5713-5783), sin sus dos defectos (datos externos que quedan apuntando al nombre viejo, y alias
''' borrados por PREFIJO en <c>Automorph::RenameShape</c>), y además con lo que la app ya mantiene y OS no: secuencias y
''' palette del NIF, el XML de HDT-SMP y el material por shape.
''' <para>DOS CAPAS, cada una con un solo dueño:</para>
''' <list type="bullet">
''' <item><see cref="RenombrarIdentidad"/>: NIF (+ animación + palette), &lt;Shape&gt;, BaseMaterials, XML de SMP,
''' &lt;ReferenceInfo ShapeName&gt; y cachés. La usan el botón Rename y Merge (que conserva su propia ley de datos).</item>
''' <item>Datos (<see cref="PlanearDatos"/> / <see cref="AplicarDatos"/>): la ley de nombres de OS para cada &lt;Data&gt;
''' y la copia a local de los externos. Solo el botón Rename.</item>
''' </list>
''' </summary>
Public NotInheritable Class ShapeRenameHelper

    Private Sub New()
    End Sub

    ''' <summary>⛔ SEDE ÚNICA de "¿sirve este nombre para una shape nueva o renombrada?" (Rename y Clone).
    ''' <list type="bullet">
    ''' <item>No vacío y sin espacios alrededor (los llamadores recortan, como OS no: la app lo hace en Clone).</item>
    ''' <item>Único en el proyecto (nombre o target, OrdinalIgnoreCase: así resuelven las cachés de la app) y en el NIF
    ''' (que puede traer shapes que el .osp no lista; OS valida contra workNif.GetShapeNames, OutfitProject.cpp:2477-2483).
    ''' <paramref name="propia"/> se excluye: un rename que solo cambia mayúsculas es válido.</item>
    ''' <item>Representable en Latin-1: el NIF escribe sus strings en Latin-1 (NiString.cs) y el .osp en UTF-8; un char
    ''' fuera de Latin-1 deja el NIF con '?' y el .osp con el real, y el proyecto deja de abrir (la carga exige que toda
    ''' &lt;Shape&gt; resuelva contra el NIF).</item>
    ''' <item>Sin '\' ni '/': son los separadores del texto de un &lt;Data&gt; (archivo.osd\bloque; BodySlide corta en el
    ''' último '/' o '\', SliderSet.cpp:354-362).</item>
    ''' <item>≤ 255 bytes Latin-1 (nombre de shape en el .tri, TriFileParser) y, por cada slider con datos de
    ''' <paramref name="formaDeDatos"/>, nombre de bloque <c>nombre_ + slider</c> ≤ 255 bytes UTF-8 (formato .osd).</item>
    ''' </list></summary>
    ''' <returns>El motivo (inglés, para el usuario) o Nothing si el nombre sirve.</returns>
    Public Shared Function MotivoNombreInvalido(ss As SliderSet_Class, nombre As String,
                                                Optional propia As Shape_class = Nothing,
                                                Optional formaDeDatos As Shape_class = Nothing) As String
        If String.IsNullOrWhiteSpace(nombre) Then Return "The name cannot be empty."
        If Not String.Equals(nombre, nombre.Trim(), StringComparison.Ordinal) Then Return "The name cannot start or end with spaces."
        If nombre.IndexOfAny({"\"c, "/"c}) >= 0 Then Return "The name cannot contain '\' or '/'."
        Dim latin1 = Text.Encoding.Latin1
        If Not String.Equals(latin1.GetString(latin1.GetBytes(nombre)), nombre, StringComparison.Ordinal) Then
            Return "The name can only use Latin-1 characters (NIF files store shape names in that encoding)."
        End If
        If latin1.GetByteCount(nombre) > 255 Then Return "The name is longer than 255 characters (limit of the .tri format)."
        If ss.Shapes.Any(Function(x) x IsNot propia AndAlso
                             (x.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase) OrElse x.Target.Equals(nombre, StringComparison.OrdinalIgnoreCase))) Then
            Return $"A shape named '{nombre}' already exists in the project."
        End If
        Dim nifPropia = propia?.RelatedNifShape
        If ss.NIFContent.NifShapes.Any(Function(n) n IsNot nifPropia AndAlso String.Equals(n.Name?.String, nombre, StringComparison.OrdinalIgnoreCase)) Then
            Return $"A shape named '{nombre}' already exists in the mesh."
        End If
        If formaDeDatos IsNot Nothing Then
            Dim prefijo = nombre.Replace(":", "_")
            For Each sl In ss.Sliders
                If Not sl.Datas.Any(Function(d) d.Target.Equals(formaDeDatos.Target, StringComparison.OrdinalIgnoreCase)) Then Continue For
                If Text.Encoding.UTF8.GetByteCount(prefijo & sl.Nombre) > 255 Then
                    Return $"The morph data name for slider '{sl.Nombre}' would exceed 255 bytes (limit of the .osd format)."
                End If
            Next
        End If
        Return Nothing
    End Function

    ''' <summary>Un &lt;Data&gt; de la shape y lo que se le hace, decidido ANTES de mutar nada.</summary>
    Friend NotInheritable Class DatoPlaneado
        Public Dato As Slider_Data_class
        Public NombreNuevo As String
        ''' <summary>Local con bloques EXCLUSIVOS: sus bloques (objetos capturados antes de mutar; se renombran).</summary>
        Public BloquesLocales As List(Of OSD_Block_Class)
        ''' <summary>Externo, o local con algún bloque que también nombra OTRO &lt;Data&gt; local: la foto de los diffs de
        ''' cada bloque. Se crean bloques locales NUEVOS con el nombre nuevo; los originales quedan para su otro dueño.</summary>
        Public DiffsACopiar As List(Of List(Of OSD_DataDiff_Class))
        Public EraExterno As Boolean
    End Class

    ''' <summary>Nombre nuevo de un dato, ley de OS (SliderData.h:73-82) en su orden: (i) es prefijo+slider ⇒
    ''' nuevo_+slider; (ii) empieza con el prefijo ⇒ nuevo_+resto; (iii) termina con el slider ⇒ nuevo_+slider; (iv) si
    ''' no, se conserva. Prefijo viejo en sus dos variantes (crudo y con ':'→'_', la convención de la app al crear
    ''' bloques); nuevo_ = nuevo con ':'→'_' (decisión del usuario 3-oct-2026). Comparación Ordinal, la de OS.</summary>
    Public Shared Function NombreDeDatoNuevo(nombreViejo As String, targetViejo As String, nuevo As String, slider As String) As String
        Dim nuevoSub = nuevo.Replace(":", "_")
        Dim prefijos = {targetViejo, targetViejo.Replace(":", "_")}.Distinct(StringComparer.Ordinal).ToList()
        For Each p In prefijos
            If String.Equals(nombreViejo, p & slider, StringComparison.Ordinal) Then Return nuevoSub & slider
        Next
        For Each p In prefijos
            If nombreViejo.StartsWith(p, StringComparison.Ordinal) Then Return nuevoSub & nombreViejo.Substring(p.Length)
        Next
        If nombreViejo.EndsWith(slider, StringComparison.Ordinal) Then Return nuevoSub & slider
        Return nombreViejo
    End Function

    ''' <summary>El plan de datos: cada &lt;Data&gt; cuyo target es el de la shape (exacto sin mayúsculas, como resuelve la
    ''' app; nunca por prefijo — defecto de Automorph::RenameShape), con sus bloques capturados como OBJETOS antes de
    ''' cualquier mutación (las cachés resuelven por nombre y se invalidan al final). Lanza, sin haber mutado nada:
    ''' <list type="bullet">
    ''' <item>si un dato EXTERNO no tiene bloques legibles (decisión del usuario 3-oct-2026: se niega el rename; no hay
    ''' datos que copiar al local);</item>
    ''' <item>si un nombre nuevo pasa de 255 bytes UTF-8 (formato .osd; la rama (ii) puede alargar el nombre);</item>
    ''' <item>si un nombre nuevo choca con un bloque local o un &lt;Data&gt; que no es de este plan (MergeBlocksByName
    ''' sumaría los diffs al guardar; la carga rechazaría "Duplicated Slider Data").</item>
    ''' </list>
    ''' Un bloque local que también nombra OTRO &lt;Data&gt; local (estado que dejaba el defecto ya corregido de
    ''' MaterializeEditableLocalBlocks) no se renombra: este dato recibe su copia.</summary>
    Friend Shared Function PlanearDatos(shape As Shape_class, ss As SliderSet_Class, nuevo As String) As List(Of DatoPlaneado)
        Dim targetViejo = shape.Target
        Dim plan As New List(Of DatoPlaneado)
        For Each sl In ss.Sliders
            For Each d In sl.Datas
                If Not d.Target.Equals(targetViejo, StringComparison.OrdinalIgnoreCase) Then Continue For
                plan.Add(New DatoPlaneado With {.Dato = d, .NombreNuevo = NombreDeDatoNuevo(d.Nombre, targetViejo, nuevo, sl.Nombre)})
            Next
        Next
        Dim delPlan As New HashSet(Of Slider_Data_class)(plan.Select(Function(x) x.Dato), System.Collections.Generic.ReferenceEqualityComparer.Instance)
        ' Nombres que resuelven <Data> locales que NO son de este plan (la resolución es por nombre, sin mayúsculas).
        Dim nombresAjenos As New HashSet(Of String)(
            ss.Sliders.SelectMany(Function(sl) sl.Datas).Where(Function(d) d.Islocal AndAlso Not delPlan.Contains(d)).Select(Function(d) d.Nombre),
            StringComparer.OrdinalIgnoreCase)

        For Each p In plan
            Dim d = p.Dato
            If d.Islocal Then
                Dim bloques = d.RelatedLocalOSDBlocks.ToList()
                If bloques.Any(Function(b) nombresAjenos.Contains(b.BlockName)) Then
                    p.DiffsACopiar = bloques.Select(Function(b) b.SnapshotDiffs()).ToList()
                Else
                    p.BloquesLocales = bloques
                End If
            Else
                Dim ext = d.RelatedExternalOSDBlocks.ToList()
                If ext.Count = 0 Then
                    Throw New InvalidOperationException($"The morph data '{d.Nombre}' of slider '{d.ParentSlider.Nombre}' comes from an external file that could not be read, so it cannot be copied under the new name.")
                End If
                p.DiffsACopiar = ext.Select(Function(b) b.SnapshotDiffs()).ToList()
                p.EraExterno = True
            End If
            If Text.Encoding.UTF8.GetByteCount(p.NombreNuevo) > 255 Then
                Throw New InvalidOperationException($"The morph data name '{p.NombreNuevo}' would exceed 255 bytes (limit of the .osd format).")
            End If
        Next

        Dim bloquesDelPlan As New HashSet(Of OSD_Block_Class)(plan.Where(Function(x) x.BloquesLocales IsNot Nothing).SelectMany(Function(x) x.BloquesLocales),
                                                             System.Collections.Generic.ReferenceEqualityComparer.Instance)
        ' Un nombre por dato (un dato con varios bloques homónimos los conserva homónimos: es la suma que ya hace hoy la
        ' lectura por nombre).
        For Each g In plan.GroupBy(Function(x) x.NombreNuevo, StringComparer.OrdinalIgnoreCase)
            If g.Count() > 1 Then Throw New InvalidOperationException($"Two morph data entries would both be named '{g.Key}'.")
            If ss.OSDContent_Local.Blocks.Any(Function(b) Not bloquesDelPlan.Contains(b) AndAlso b.BlockName.Equals(g.Key, StringComparison.OrdinalIgnoreCase)) Then
                Throw New InvalidOperationException($"The project already has morph data named '{g.Key}'.")
            End If
        Next
        ' Clave de la carga: name + slider (OSP_Clases Load_and_Check_Shapedata, "Duplicated Slider Data").
        Dim claves As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each sl In ss.Sliders
            For Each d In sl.Datas
                If Not delPlan.Contains(d) Then claves.Add(d.Nombre & "|" & sl.Nombre)
            Next
        Next
        For Each p In plan
            If Not claves.Add(p.NombreNuevo & "|" & p.Dato.ParentSlider.Nombre) Then
                Throw New InvalidOperationException($"Slider '{p.Dato.ParentSlider.Nombre}' already has morph data named '{p.NombreNuevo}'.")
            End If
        Next
        Return plan
    End Function

    ''' <summary>Aplica el plan de datos. No lanza: todo lo que podía fallar se resolvió en <see cref="PlanearDatos"/>.
    ''' Bloques exclusivos: BlockName, name, texto y target cambian JUNTOS. Externos (decisión del usuario 3-oct-2026, la
    ''' ley de OS para la base —OutfitProject.cpp:5775— para toda shape) y bloques compartidos con otro dato: bloques
    ''' locales NUEVOS con el nombre nuevo desde la foto, y el dato queda local.</summary>
    ''' <returns>(datos renombrados, datos externos copiados a local, datos locales con bloque compartido copiados)</returns>
    Friend Shared Function AplicarDatos(ss As SliderSet_Class, plan As List(Of DatoPlaneado), nuevo As String) As (Datos As Integer, Externos As Integer, Compartidos As Integer)
        Dim externos = 0, compartidos = 0
        For Each p In plan
            Dim d = p.Dato
            If p.BloquesLocales IsNot Nothing Then
                For Each b In p.BloquesLocales
                    b.BlockName = p.NombreNuevo
                Next
            Else
                For Each diffs In p.DiffsACopiar
                    Dim b As New OSD_Block_Class(ss.OSDContent_Local) With {.BlockName = p.NombreNuevo}
                    b.DataDiff.AddRange(diffs)
                    b.RebuildCompactArrays()
                    ss.OSDContent_Local.Blocks.Add(b)
                Next
                If p.EraExterno Then
                    d.Islocal = True
                    d.TargetOsd = ss.LocalOsdFileName
                    externos += 1
                Else
                    compartidos += 1
                End If
            End If
            d.Nombre = p.NombreNuevo
            d.TargetSlider = p.NombreNuevo
            d.Target = nuevo
        Next
        Return (plan.Count, externos, compartidos)
    End Function

    ''' <summary>Resultado de <see cref="RenombrarIdentidad"/>.</summary>
    Public Structure ResultadoIdentidad
        Public BloquesAnimacion As Integer
        Public EntradasPalette As Integer
        Public SmpCambios As Integer
        ''' <summary>El proyecto tiene XML de SMP y no se pudo parsear: no se tocó (su física queda con el nombre viejo).</summary>
        Public SmpNoParseable As Boolean
        Public ReferenceInfo As Boolean
    End Structure

    ''' <summary>La identidad de la shape pasa a <paramref name="nuevo"/>. Precondición: el nombre ya lo validó el
    ''' llamador (cada uno en su dominio de unicidad). No lanza.</summary>
    Public Shared Function RenombrarIdentidad(shape As Shape_class, ss As SliderSet_Class, nuevo As String) As ResultadoIdentidad
        Dim r As New ResultadoIdentidad
        Dim nif = ss.NIFContent
        ' Todo lo que se resuelve por el nombre viejo, ANTES de cambiarlo (RelatedNifShape resuelve por Nombre).
        Dim nifShape = shape.RelatedNifShape
        Dim viejoNif = nifShape?.Name?.String
        Dim viejoNombre = shape.Nombre

        ' Material: la MISMA entrada (mismo objeto: ediciones sin guardar viajan con la shape) bajo la clave nueva. Una
        ' entrada vieja con el nombre nuevo (de una shape borrada antes) se descarta: le daría a esta otro material.
        Dim bm = nif.BaseMaterials
        Dim entrada As Nifcontent_Class_Manolo.RelatedMaterial_Class = Nothing
        Dim tenia = viejoNif IsNot Nothing AndAlso bm.TryGetValue(viejoNif, entrada)
        If tenia Then bm.Remove(viejoNif)
        bm.Remove(nuevo)
        If tenia Then bm(nuevo) = entrada

        If nifShape IsNot Nothing Then
            Dim a = nif.RenombrarShapeConAnimacion(nifShape, nuevo)
            r.BloquesAnimacion = a.Bloques
            r.EntradasPalette = a.Palette
        End If

        shape.Nombre = nuevo
        shape.Target = nuevo

        ' HDT-SMP liga la física por el nombre de la shape del NIF.
        If Not String.IsNullOrEmpty(ss.PhysicsXmlContent) Then
            Dim smp = SmpPhysicsXml.RenombrarShape(ss.PhysicsXmlContent, If(viejoNif, viejoNombre), nuevo)
            If smp.Parseo Then
                ss.PhysicsXmlContent = smp.Xml
                r.SmpCambios = smp.Cambios
            Else
                r.SmpNoParseable = True
            End If
        End If

        ' <ReferenceInfo ShapeName>: OS lo reescribe con el nombre de la base salvo que el set referencie OTRO proyecto
        ' (ProjectFile + ProjectName + ShapeName, OutfitProject.cpp:461-466). Decisión del usuario 3-oct-2026.
        Dim refInfo = TryCast(ss.Nodo.SelectSingleNode("ReferenceInfo"), XmlElement)
        If refInfo IsNot Nothing AndAlso
           Not (refInfo.GetAttribute("ProjectFile").Length > 0 AndAlso refInfo.GetAttribute("ProjectName").Length > 0) AndAlso
           String.Equals(refInfo.GetAttribute("ShapeName"), viejoNombre, StringComparison.Ordinal) Then
            refInfo.SetAttribute("ShapeName", nuevo)
            r.ReferenceInfo = True
        End If

        ss.InvalidateAllLookupCaches()
        Return r
    End Function

    ''' <summary>Resultado del rename completo (para la UI y los gates).</summary>
    Public Structure ResultadoRename
        Public Identidad As ResultadoIdentidad
        Public Datos As Integer
        Public ExternosCopiados As Integer
        Public CompartidosCopiados As Integer
    End Structure

    ''' <summary>El plan completo del botón, sin mutar: validación del nombre (la de los nombres de datos la hace el plan
    ''' sobre los que de verdad produce), plan de datos y XML de SMP parseable.</summary>
    Private Shared Function Planear(shape As Shape_class, ss As SliderSet_Class, nuevo As String) As List(Of DatoPlaneado)
        Dim motivo = MotivoNombreInvalido(ss, nuevo, shape)
        If motivo IsNot Nothing Then Throw New InvalidOperationException(motivo)
        Dim plan = PlanearDatos(shape, ss, nuevo)
        If Not String.IsNullOrEmpty(ss.PhysicsXmlContent) Then
            Dim prueba = SmpPhysicsXml.RenombrarShape(ss.PhysicsXmlContent, If(shape.RelatedNifShape?.Name?.String, shape.Nombre), nuevo)
            If Not prueba.Parseo Then
                Throw New InvalidOperationException("The project's physics XML cannot be parsed, so its reference to this shape cannot be renamed.")
            End If
        End If
        Return plan
    End Function

    ''' <summary>Por qué no se puede renombrar (para la UI, antes de confirmar), o Nothing. Es el MISMO plan que
    ''' <see cref="Renombrar"/>: el diálogo no puede prometer otra cosa.</summary>
    Public Shared Function MotivoParaNoRenombrar(shape As Shape_class, ss As SliderSet_Class, nuevo As String) As String
        Try
            Planear(shape, ss, nuevo)
            Return Nothing
        Catch ex As InvalidOperationException
            Return ex.Message
        End Try
    End Function

    ''' <summary>Rename completo (botón). Plan primero; si algo no cierra, lanza SIN haber tocado nada. Después datos e
    ''' identidad.</summary>
    Public Shared Function Renombrar(shape As Shape_class, ss As SliderSet_Class, nuevo As String) As ResultadoRename
        Dim plan = Planear(shape, ss, nuevo)
        Dim d = AplicarDatos(ss, plan, nuevo)
        Dim id = RenombrarIdentidad(shape, ss, nuevo)
        Return New ResultadoRename With {.Identidad = id, .Datos = d.Datos, .ExternosCopiados = d.Externos, .CompartidosCopiados = d.Compartidos}
    End Function

End Class
