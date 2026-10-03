Imports NiflySharp

''' <summary>
''' Clona una shape del proyecto en memoria: el bloque del NIF (clon profundo en el mismo archivo), su material,
''' sus morphs de cada slider y su animacion. Referencia canonica: <c>OutfitStudioFrame::OnDupeShape</c> →
''' <c>OutfitProject::DuplicateShape</c> (OutfitStudio.cpp:11586-11631, OutfitProject.cpp:5351-5401) +
''' <c>NifFile::CloneShape</c> (nifly NifFile.cpp:1322-1440).
''' <para>Igual que OS: nombre unico elegido por el usuario; clon profundo sin compartir bloques; sin
''' <c>UpdateSkinPartitions</c> (geometria identica); sin fisica (no copia shapePhysicsFiles); copia de los diffs de
''' TODOS los sliders con el espacio de vertices identico, escritos como locales; el original no se toca.</para>
''' <para>Distinto de OS, a proposito: copia LockNormals/SmoothSeamNormals (decision del usuario) y copia la
''' animacion de las secuencias del NIF (OS no toca secuencias).</para>
''' All changes stay in memory; call Process_render_Changes(True) afterward.
''' </summary>
Public NotInheritable Class ShapeCloneHelper

    Private Sub New()
    End Sub

    Public Shared Function CanClone(shape As Shape_class) As Boolean
        Return shape IsNot Nothing AndAlso shape.RelatedNifShape IsNot Nothing
    End Function

    ''' <summary>Clona <paramref name="shape"/> como <paramref name="nombreNuevo"/> (el llamador ya lo valido unico con
    ''' <c>Check_Unique_Shapename</c> y contra los nombres del NIF) y devuelve la Shape_class nueva, insertada tras el
    ''' original, y cuantos bloques de animacion NO se copiaron porque su controller es de otro objeto.</summary>
    Public Shared Function Clone(shape As Shape_class, sliderSet As SliderSet_Class, nombreNuevo As String) As (Nueva As Shape_class, AnimacionAjena As Integer)
        Dim origNif As INiShape = shape.RelatedNifShape
        If origNif Is Nothing Then Throw New InvalidOperationException("Shape has no NIF data.")
        ' Clon del bloque + animacion, con reversion si falla: sede unica en la libreria (la comparte Split).
        Dim r = sliderSet.NIFContent.ClonarShapeConAnimacion(origNif, nombreNuevo)

        ShapeDuplicacionNucleo.RegistrarMaterial(sliderSet, origNif.Name.String, nombreNuevo)
        Dim nueva = ShapeDuplicacionNucleo.AgregarShapeAlProyecto(shape, sliderSet, nombreNuevo, heredarDataFolder:=False)

        For Each slider In sliderSet.Sliders
            Dim fuente = ShapeDuplicacionNucleo.DatosDeLaShape(slider, shape)
            If fuente.Datos.Count = 0 Then Continue For
            Dim todos As New List(Of OSD_DataDiff_Class)()
            For Each porDato In fuente.Diffs
                For Each lista In porDato
                    todos.AddRange(lista)
                Next
            Next
            ShapeDuplicacionNucleo.EscribirMorphsDelNuevo(sliderSet, slider, nombreNuevo, ShapeDuplicacionNucleo.CopiarDiffs(todos))
        Next

        sliderSet.InvalidateAllLookupCaches()
        Return (nueva, r.Ajenos)
    End Function

End Class
