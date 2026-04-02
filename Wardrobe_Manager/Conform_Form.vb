Imports OpenTK.Mathematics
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Xml

''' <summary>
''' Conform Sliders form: copies slider morphs from a source shape onto a target shape
''' by projecting each target vertex onto the source surface and barycentric-interpolating
''' the deltas (higher quality than k-nearest approaches, no gaps).
''' Source shape is selected in the form; target shape is passed at construction.
''' </summary>
Public Class Conform_Form

    Private ReadOnly _sliderSet As SliderSet_Class
    Private ReadOnly _targetShape As Shape_class
    Public Event Apply_Conformed(frm As Conform_Form)
    Private _cts As CancellationTokenSource = Nothing

    Private Enum RunState
        Ready
        Running
        Done
    End Enum
    Private _state As RunState = RunState.Ready
    Private _results As List(Of ConformHelper.ConformResult) = Nothing
    Private _overwriteAtApply As Boolean = False

    Public Sub New(sliderSet As SliderSet_Class, targetShape As Shape_class)
        _sliderSet = sliderSet
        _targetShape = targetShape
        InitializeComponent()
        PopulateControls()
    End Sub

    Private Sub PopulateControls()
        cboSource.Items.Clear()
        For Each shap In _sliderSet.Shapes
            If shap IsNot _targetShape Then
                cboSource.Items.Add(shap.Target)
            End If
        Next
        If cboSource.Items.Count > 0 Then cboSource.SelectedIndex = 0

        clbSliders.Items.Clear()
        For Each slid In _sliderSet.Sliders
            clbSliders.Items.Add(slid.Nombre, True)
        Next

        If cboSource.Items.Count = 0 Then
            btnAction.Enabled = False
            lblStatus.Text = "No other shapes available as source."
            lblStatus.ForeColor = Color.DarkRed
        End If
    End Sub

    Private Sub btnSelectAll_Click(sender As Object, e As EventArgs) Handles btnSelectAll.Click
        For i = 0 To clbSliders.Items.Count - 1
            clbSliders.SetItemChecked(i, True)
        Next
    End Sub

    Private Sub btnSelectNone_Click(sender As Object, e As EventArgs) Handles btnSelectNone.Click
        For i = 0 To clbSliders.Items.Count - 1
            clbSliders.SetItemChecked(i, False)
        Next
    End Sub

    Private Sub btnAction_Click(sender As Object, e As EventArgs) Handles btnAction.Click
        Select Case _state
            Case RunState.Running
                _cts?.Cancel()
            Case Else
                StartComputation()
        End Select
    End Sub

    Private Async Sub StartComputation()
        Dim sourceShapeName = TryCast(cboSource.SelectedItem, String)
        If String.IsNullOrEmpty(sourceShapeName) Then Exit Sub

        Dim checkedSliders As New List(Of String)
        For i = 0 To clbSliders.Items.Count - 1
            If clbSliders.GetItemChecked(i) Then
                checkedSliders.Add(TryCast(clbSliders.Items(i), String))
            End If
        Next
        If checkedSliders.Count = 0 Then
            MsgBox("Select at least one slider.", vbInformation, "Conform")
            Exit Sub
        End If

        Dim settings As New ConformHelper.ConformSettings With {
            .SearchRadius = CSng(nudSearchRadius.Value),
            .AxisX = chkAxisX.Checked,
            .AxisY = chkAxisY.Checked,
            .AxisZ = chkAxisZ.Checked,
            .Overwrite = chkOverwrite.Checked
        }
        _overwriteAtApply = chkOverwrite.Checked

        _cts = New CancellationTokenSource()
        _state = RunState.Running
        _results = Nothing
        btnAction.Text = "Cancel"
        SetControlsEnabled(False)
        progressBar1.Value = 0
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Text = "Loading shape data..."

        Dim progress As IProgress(Of Integer) = New Progress(Of Integer)(Sub(pct)
                                                                             If Me.IsDisposed Then Return
                                                                             progressBar1.Value = Math.Min(pct, 100)
                                                                             lblStatus.Text = $"Processing... {pct}%"
                                                                         End Sub)

        Dim token = _cts.Token
        Dim sliderSet = _sliderSet
        Dim targetShape = _targetShape
        btnApply.Enabled = False
        Try
            _results = Await Task.Run(
                Function()
                    If Not OSP_Project_Class.Load_and_Check_Shapedata(sliderSet, False) Then
                        Throw New InvalidOperationException("Could not load shape data for the project.")
                    End If

                    Dim sourceShape = sliderSet.Shapes.FirstOrDefault(
                        Function(s) s.Target.Equals(sourceShapeName, StringComparison.OrdinalIgnoreCase))
                    If sourceShape Is Nothing OrElse sourceShape.RelatedNifShape Is Nothing Then
                        Throw New InvalidOperationException($"Source shape '{sourceShapeName}' not found or has no NIF data.")
                    End If
                    If targetShape.RelatedNifShape Is Nothing Then
                        Throw New InvalidOperationException("Target shape has no NIF data.")
                    End If

                    Dim srcNif = sourceShape.RelatedNifShape
                    Dim srcPosRaw = srcNif.VertexPositions
                    If srcPosRaw Is Nothing OrElse srcPosRaw.Count = 0 Then
                        Throw New InvalidOperationException("Source shape has no vertex positions.")
                    End If
                    Dim srcPos(srcPosRaw.Count - 1) As Vector3
                    For i = 0 To srcPosRaw.Count - 1
                        srcPos(i) = New Vector3(srcPosRaw(i).X, srcPosRaw(i).Y, srcPosRaw(i).Z)
                    Next

                    Dim sourceTris = srcNif.Triangles
                    If sourceTris Is Nothing OrElse sourceTris.Count = 0 Then
                        Throw New InvalidOperationException("Source shape has no triangles.")
                    End If

                    Dim tgtNif = targetShape.RelatedNifShape
                    Dim tgtPosRaw = tgtNif.VertexPositions
                    If tgtPosRaw Is Nothing OrElse tgtPosRaw.Count = 0 Then
                        Throw New InvalidOperationException("Target shape has no vertex positions.")
                    End If
                    Dim tgtPos(tgtPosRaw.Count - 1) As Vector3
                    For i = 0 To tgtPosRaw.Count - 1
                        tgtPos(i) = New Vector3(tgtPosRaw(i).X, tgtPosRaw(i).Y, tgtPosRaw(i).Z)
                    Next

                    ' Build per-slider delta dictionaries from source OSD
                    Dim sliderDeltaList As New List(Of (SliderName As String, Deltas As Dictionary(Of Integer, Vector3)))
                    For Each sliderName In checkedSliders
                        token.ThrowIfCancellationRequested()
                        Dim slider = sliderSet.Sliders.FirstOrDefault(
                            Function(s) s.Nombre.Equals(sliderName, StringComparison.OrdinalIgnoreCase))
                        If slider Is Nothing Then Continue For

                        Dim dat = slider.Datas.FirstOrDefault(
                            Function(d) d.Target.Equals(sourceShape.Target, StringComparison.OrdinalIgnoreCase) AndAlso d.Islocal)
                        If dat Is Nothing Then
                            dat = slider.Datas.FirstOrDefault(
                                Function(d) d.Target.Equals(sourceShape.Target, StringComparison.OrdinalIgnoreCase))
                        End If
                        If dat Is Nothing Then Continue For

                        Dim block = dat.RelatedOSDBlocks.FirstOrDefault()
                        If block Is Nothing OrElse block.DataDiff.Count = 0 Then Continue For

                        Dim deltaDict As New Dictionary(Of Integer, Vector3)(block.DataDiff.Count)
                        For Each diff In block.DataDiff
                            deltaDict(diff.Index) = New Vector3(diff.X, diff.Y, diff.Z)
                        Next
                        sliderDeltaList.Add((sliderName, deltaDict))
                    Next

                    If sliderDeltaList.Count = 0 Then
                        Throw New InvalidOperationException("No slider data found on source shape for the selected sliders.")
                    End If

                    Return ConformHelper.ComputeConform(srcPos, sourceTris, sliderDeltaList, tgtPos, settings, progress, token)
                End Function)

            Dim totalDeltas = _results.Sum(Function(r) r.Deltas.Count)
            progressBar1.Value = 100
            lblStatus.Text = $"Done: {_results.Count} slider(s), {totalDeltas} vertex deltas."
            lblStatus.ForeColor = If(totalDeltas > 0, Color.DarkGreen, SystemColors.GrayText)
            _state = RunState.Done
            btnApply.Enabled = True
            btnAction.Text = "Start"

        Catch ex As OperationCanceledException
            progressBar1.Value = 0
            lblStatus.Text = "Cancelled."
            lblStatus.ForeColor = SystemColors.GrayText
            _state = RunState.Ready
            btnAction.Text = "Start"

        Catch ex As Exception
            lblStatus.Text = $"Error: {ex.Message}"
            lblStatus.ForeColor = Color.DarkRed
            _state = RunState.Ready
            btnAction.Text = "Start"
            btnApply.Enabled = False
        Finally
            SetControlsEnabled(True)
        End Try
    End Sub

    Private Sub SetControlsEnabled(enabled As Boolean)
        btnClose.Enabled = enabled
        cboSource.Enabled = enabled
        clbSliders.Enabled = enabled
        btnSelectAll.Enabled = enabled
        btnSelectNone.Enabled = enabled
        nudSearchRadius.Enabled = enabled
        chkAxisX.Enabled = enabled
        chkAxisY.Enabled = enabled
        chkAxisZ.Enabled = enabled
        chkOverwrite.Enabled = enabled
    End Sub

    Private Sub ApplyAndSave()
        If _results Is Nothing OrElse _results.Count = 0 Then Exit Sub
        ConformHelper.ApplyConformResults(_results, _targetShape, _sliderSet, _overwriteAtApply)
        _sliderSet.InvalidateAllLookupCaches()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _cts?.Cancel()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles btnApply.Click
        ApplyAndSave()
        RaiseEvent Apply_Conformed(Me)
        btnApply.Enabled = False
    End Sub
End Class
