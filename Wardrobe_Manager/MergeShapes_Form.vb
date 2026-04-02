' Version Uploaded of Wardrobe 2.1.3
Public Class MergeShapes_Form

    Private _currentShape As Shape_class
    Private _sliderSet As SliderSet_Class

    ''' <summary>The chosen target shape after OK.</summary>
    Public Property TargetShape As Shape_class

    ''' <summary>The donor shapes to merge into TargetShape after OK (does not include TargetShape).</summary>
    Public Property DonorShapes As List(Of Shape_class)

    Sub New(currentShape As Shape_class, sliderSet As SliderSet_Class)
        InitializeComponent()
        _currentShape = currentShape
        _sliderSet = sliderSet
    End Sub

    Private Sub MergeShapes_Form_Load(sender As Object, e As EventArgs) Handles Me.Load
        Dim shaderType = MergeShapesHelper.GetShaderType(_currentShape)
        Dim shaderName = If(shaderType Is GetType(NiflySharp.Blocks.BSLightingShaderProperty), "BGSM", If(shaderType Is GetType(NiflySharp.Blocks.BSEffectShaderProperty), "BGEM", "?"))
        lblCurrentShape.Text = $"Current shape: {_currentShape.Nombre} ({shaderName})"

        clbShapes.Items.Clear()
        For Each s In _sliderSet.Shapes
            If s Is _currentShape Then Continue For
            Dim st = MergeShapesHelper.GetShaderType(s)
            Dim sn = If(st Is GetType(NiflySharp.Blocks.BSLightingShaderProperty), "BGSM", If(st Is GetType(NiflySharp.Blocks.BSEffectShaderProperty), "BGEM", "?"))
            clbShapes.Items.Add(New ShapeItem(s, sn))
        Next

        UpdateTargetCombo()
        ValidateCompatibility()
    End Sub

    Private Sub ClbShapes_ItemCheck(sender As Object, e As ItemCheckEventArgs) Handles clbShapes.ItemCheck
        ' ItemCheck fires before the state changes, so defer update
        BeginInvoke(Sub()
                        UpdateTargetCombo()
                        ValidateCompatibility()
                    End Sub)
    End Sub

    Private Sub CboTarget_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboTarget.SelectedIndexChanged
        ValidateCompatibility()
    End Sub

    Private Sub UpdateTargetCombo()
        Dim previousTarget = TryCast(cboTarget.SelectedItem, ShapeItem)?.Shape
        cboTarget.Items.Clear()

        ' Current shape always included
        Dim currentItem = New ShapeItem(_currentShape, GetShaderAbbr(_currentShape))
        cboTarget.Items.Add(currentItem)

        ' Add checked shapes
        For i = 0 To clbShapes.Items.Count - 1
            If clbShapes.GetItemChecked(i) Then
                cboTarget.Items.Add(clbShapes.Items(i))
            End If
        Next

        ' Restore previous selection or default to first
        Dim restored = cboTarget.Items.Cast(Of ShapeItem)().FirstOrDefault(Function(si) si.Shape Is previousTarget)
        cboTarget.SelectedItem = If(restored, If(cboTarget.Items.Count > 0, cboTarget.Items(0), Nothing))
    End Sub

    Private Sub ValidateCompatibility()
        Dim target = TryCast(cboTarget.SelectedItem, ShapeItem)
        If target Is Nothing OrElse cboTarget.Items.Count < 2 Then
            lblCompatibility.ForeColor = Drawing.Color.Gray
            lblCompatibility.Text = "Select at least one additional shape to merge."
            btnMerge.Enabled = False
            Return
        End If

        Dim allShapes = cboTarget.Items.Cast(Of ShapeItem)().Select(Function(si) si.Shape).ToList()
        Dim compatible = MergeShapesHelper.AreCompatible(allShapes)

        If compatible Then
            lblCompatibility.ForeColor = Drawing.Color.DarkGreen
            lblCompatibility.Text = $"OK — {allShapes.Count} shapes, all {GetShaderAbbr(target.Shape)}. Target: {target.Shape.Nombre}."
            btnMerge.Enabled = True
        Else
            lblCompatibility.ForeColor = Drawing.Color.Red
            lblCompatibility.Text = "Incompatible shaders (BGEM vs BGSM). All shapes must use the same shader type."
            btnMerge.Enabled = False
        End If
    End Sub

    Private Function GetShaderAbbr(shape As Shape_class) As String
        Dim st = MergeShapesHelper.GetShaderType(shape)
        Return If(st Is GetType(NiflySharp.Blocks.BSLightingShaderProperty), "BGSM", If(st Is GetType(NiflySharp.Blocks.BSEffectShaderProperty), "BGEM", "?"))
    End Function

    Private Sub BtnMerge_Click(sender As Object, e As EventArgs) Handles btnMerge.Click
        Dim targetItem = TryCast(cboTarget.SelectedItem, ShapeItem)
        If targetItem Is Nothing Then Return

        TargetShape = targetItem.Shape
        DonorShapes = cboTarget.Items.Cast(Of ShapeItem)() _
                                     .Where(Function(si) si.Shape IsNot TargetShape) _
                                     .Select(Function(si) si.Shape) _
                                     .ToList()

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ' ── Helper class ──────────────────────────────────────────────────────────
    Private Class ShapeItem
        Public ReadOnly Shape As Shape_class
        Private ReadOnly _shaderAbbr As String
        Sub New(shape As Shape_class, shaderAbbr As String)
            Me.Shape = shape
            Me._shaderAbbr = shaderAbbr
        End Sub
        Public Overrides Function ToString() As String
            Return $"{Shape.Nombre} ({_shaderAbbr})"
        End Function
    End Class

End Class
