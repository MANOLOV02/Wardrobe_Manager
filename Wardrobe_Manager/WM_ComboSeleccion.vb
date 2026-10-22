' Ola 7g (p7e-10)
Imports System.Runtime.CompilerServices

''' <summary>THE SELECTION RULE OF WM's PRESET AND POSE COMBOS (ola 7g, p7e-10), one place for the Editor and the main window.</summary>
Public Module WM_ComboSeleccion

    ''' <summary>Selects in <paramref name="combo"/> the item whose text is exactly <paramref name="nombre"/> (Ordinal: the
    ''' presets' and poses' dictionaries are ordinal, WM_SliderPresets), else the first item, else none - SelectedIndex -1 on an
    ''' empty list (a BodySlide SliderPresets folder with no preset), never an exception. <paramref name="nombre"/> Nothing: the
    ''' first item, if any. Not a prefix match (ComboBox.FindString would take "BodySlim" for a missing "Body").</summary>
    <Extension()>
    Public Sub SeleccionarExactoOPrimero(combo As ComboBox, nombre As String)
        Dim idx = -1
        If nombre IsNot Nothing Then
            For i = 0 To combo.Items.Count - 1
                If String.Equals(combo.Items(i)?.ToString(), nombre, StringComparison.Ordinal) Then idx = i : Exit For
            Next
        End If
        If idx = -1 AndAlso combo.Items.Count > 0 Then idx = 0
        combo.SelectedIndex = idx
    End Sub

    ''' <summary>Removes <paramref name="item"/> from <paramref name="combo"/> and leaves the rule's selection for no name (the first item
    ''' that stays, else none), made BEFORE the removal (ola 7g round 2, p7g-02): ComboBox.Items.Remove of the selected item drops the
    ''' selection to -1 without SelectedIndexChanged, so a selection made after it could leave the combo's handler unrun (the
    ''' Editor's Actualiza_Preset) - with the last item, -1 over -1. Selecting first changes the index whenever the selected item
    ''' goes, the last one included (to -1), so the handler runs once, through its event. An item that is not the selected one is
    ''' only removed.</summary>
    <Extension()>
    Public Sub QuitarItem(combo As ComboBox, item As Object)
        Dim idx = combo.Items.IndexOf(item)
        If idx = -1 Then Exit Sub
#If DEBUG Then
        ' Ola 7g round 2 tester (WmMismaEscenaGate --ola7g, R2-4): remove first, select after (Items.Remove of the selected item drops
        ' the selection to -1 with no event; with the last item the rule then leaves -1 over -1).
        If WM_RenderExtensions.GateMutantOn("g7-quitaritem-remove-first") Then
            combo.Items.RemoveAt(idx)
            combo.SeleccionarExactoOPrimero(Nothing)
            Exit Sub
        End If
#End If
        If combo.SelectedIndex = idx Then combo.SelectedIndex = If(combo.Items.Count = 1, -1, If(idx = 0, 1, 0))
        combo.Items.RemoveAt(idx)
    End Sub
End Module
