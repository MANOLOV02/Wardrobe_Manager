' Version Uploaded of Wardrobe 3.2.0
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Configuration form for the occlusion ray-casting mask tool.
''' Pass the target mesh and occluder meshes at construction; call ShowDialog.
''' If result = OK, ResultVertices contains the vertex indices to add to the mask.
''' </summary>
Public Class OcclusionMask_Form

    Public Property ResultVertices As HashSet(Of Integer) = Nothing

    Private ReadOnly _targetMesh As PreviewModel.RenderableMesh
    Private ReadOnly _raytracer As OcclusionRaytracer
    Public Event ApplyOcclusion(frm As OcclusionMask_Form)
    Private _cts As CancellationTokenSource = Nothing

    Private Enum RunState
        Ready
        Running
        Done
    End Enum
    Private _state As RunState = RunState.Ready

    Private Shared ReadOnly RayCounts As Integer() = {32, 64, 128, 256}

    Public Sub New(targetMesh As PreviewModel.RenderableMesh, occluderMeshes As IEnumerable(Of PreviewModel.RenderableMesh))
        _targetMesh = targetMesh
        _raytracer = New OcclusionRaytracer(occluderMeshes)
        InitializeComponent()
        cboQuality.SelectedIndex = 1
        If Not _raytracer.HasOccluders Then
            btnAction.Enabled = False
            lblStatus.Text = "No visible occluder shapes found."
            lblStatus.ForeColor = Color.DarkRed
        End If
    End Sub

    Private Sub ChkSelfOcclusion_CheckedChanged(sender As Object, e As EventArgs)
        nudSelfMinDist.Enabled = chkSelfOcclusion.Checked
    End Sub

    Private Sub BtnAction_Click(sender As Object, e As EventArgs) Handles btnAction.Click
        Select Case _state
            Case RunState.Running
                _cts?.Cancel()
            Case Else
                StartComputation()
        End Select
    End Sub

    Private Sub StartComputation()
        Dim settings = New OcclusionRaytracer.RaycastSettings With {
            .RayCount = RayCounts(cboQuality.SelectedIndex),
            .NormalBias = CSng(nudBias.Value),
            .MaxDistance = 0,
            .OcclusionThreshold = CSng(nudThreshold.Value),
            .MaskCompleteTrianglesOnly = chkTriangles.Checked,
            .IncludeSelfOcclusion = chkSelfOcclusion.Checked,
            .SelfMinDistance = CSng(nudSelfMinDist.Value)
        }

        _cts?.Dispose()
        _cts = New CancellationTokenSource()
        _state = RunState.Running
        btnAction.Text = "Cancel"
        btnClose.Enabled = False
        btnApply.Enabled = False
        cboQuality.Enabled = False
        nudThreshold.Enabled = False
        nudBias.Enabled = False
        chkTriangles.Enabled = False
        chkSelfOcclusion.Enabled = False
        nudSelfMinDist.Enabled = False
        ResultVertices = Nothing
        progressBar1.Value = 0
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Text = "Processing..."

        Dim progress As IProgress(Of Integer) = New Progress(Of Integer)(Sub(pct)
                                                                             If Me.IsDisposed Then Return
                                                                             progressBar1.Value = Math.Min(pct, 100)
                                                                             lblStatus.Text = $"Processing... {pct}%"
                                                                         End Sub)

        Dim token = _cts.Token
        Task.Run(
            Function()
                Return _raytracer.ComputeOccludedVertices(_targetMesh, settings, progress, token)
            End Function).
            ContinueWith(Sub(t)
                             If Me.IsDisposed Then Return
                             Try
                                 Me.Invoke(Sub()
                                               _state = RunState.Done
                                               btnClose.Enabled = True
                                               cboQuality.Enabled = True
                                               nudThreshold.Enabled = True
                                               nudBias.Enabled = True
                                               chkTriangles.Enabled = True
                                               chkSelfOcclusion.Enabled = True
                                               nudSelfMinDist.Enabled = chkSelfOcclusion.Checked

                                               If token.IsCancellationRequested Then
                                                   progressBar1.Value = 0
                                                   lblStatus.Text = "Cancelled."
                                                   lblStatus.ForeColor = SystemColors.GrayText
                                                   _state = RunState.Ready
                                                   btnAction.Text = "Start"
                                               ElseIf t.IsFaulted Then
                                                   lblStatus.Text = $"Error: {t.Exception?.InnerException?.Message}"
                                                   lblStatus.ForeColor = Color.DarkRed
                                                   btnAction.Text = "Start"
                                                   _state = RunState.Ready
                                               Else
                                                   ResultVertices = t.Result
                                                   Dim count = If(ResultVertices IsNot Nothing, ResultVertices.Count, 0)
                                                   progressBar1.Value = 100
                                                   lblStatus.Text = $"Done: {count} vertices to mask."
                                                   lblStatus.ForeColor = If(count > 0, Color.DarkGreen, SystemColors.GrayText)
                                                   btnAction.Text = "Start"
                                                   btnApply.Enabled = True
                                               End If
                                           End Sub)
                             Catch ex As ObjectDisposedException
                                 ' Form was disposed between IsDisposed check and Invoke call; safe to ignore.
                             End Try
                         End Sub)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _cts?.Cancel()
        _cts?.Dispose()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
        RaiseEvent ApplyOcclusion(Me)
        btnApply.Enabled = False
    End Sub
End Class
