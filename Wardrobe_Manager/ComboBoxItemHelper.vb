''' <summary>Operaciones sobre los ítems de un ComboBox que no tienen que tocar la selección.</summary>
Public NotInheritable Class ComboBoxItemHelper

    Private Sub New()
    End Sub

    ''' <summary>Reemplaza el texto del ítem <paramref name="indice"/> SIN disparar <c>SelectedIndexChanged</c> y
    ''' conservando la selección.
    ''' <para>⛔ <c>Items(i) = x</c> no sirve: sobre el ítem seleccionado hace NativeRemoveAt + NativeInsert y re-asigna la
    ''' selección, que pasa por -1 y dispara el evento (en el editor eso recarga el material desde disco y tira las
    ''' ediciones sin guardar); y si el texto nuevo solo difiere en mayúsculas ni siquiera re-pinta (compara ignorando
    ''' mayúsculas).</para></summary>
    Public Shared Sub ReemplazarSinEvento(combo As ComboBox, indice As Integer, texto As String, manejador As EventHandler)
        Dim seleccionado = combo.SelectedIndex
        RemoveHandler combo.SelectedIndexChanged, manejador
        combo.BeginUpdate()
        Try
            combo.Items.RemoveAt(indice)
            combo.Items.Insert(indice, texto)
            combo.SelectedIndex = seleccionado
        Finally
            combo.EndUpdate()
            AddHandler combo.SelectedIndexChanged, manejador
        End Try
    End Sub

End Class
