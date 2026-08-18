' Version Uploaded of Wardrobe 3.2.0
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Configuration form for the occlusion ray-casting mask tool.
''' Pass the target mesh and occluder meshes at construction; call ShowDialog.
''' If result = OK, ResultVertices contains the vertex indices to add to the mask.
'''
''' <para>⛔ La herramienta trabaja sobre LA SHAPE SELECCIONADA en el editor, y el resto de las shapes
''' visibles son los ocluders. Eso se muestra arriba de todo en el dialogo: sin decirlo es facil
''' correrla con las botas seleccionadas creyendo que trabaja sobre el cuerpo, y el resultado sale
''' correcto pero parece que "no hace nada".</para>
''' </summary>
Public Class OcclusionMask_Form

    Public Property ResultVertices As HashSet(Of Integer) = Nothing

    Private ReadOnly _targetMesh As PreviewModel.RenderableMesh
    Private ReadOnly _candidatos As New List(Of PreviewModel.RenderableMesh)
    Private _raytracer As OcclusionRaytracer
    Public Event ApplyOcclusion(frm As OcclusionMask_Form)
    Private _cts As CancellationTokenSource = Nothing

    Private Enum RunState
        Ready
        Running
        Done
    End Enum
    Private _state As RunState = RunState.Ready

    ' ⭐ 2 a 16 veces mas rayos que antes (eran 32/64/128/256). Se puede porque el bucle CORTA en cuanto
    ' un rayo escapa: un vertice a la vista cuesta uno o dos rayos, no mil. Lo caro se gasta solo donde
    ' de verdad esta tapado, que es la respuesta que interesa.
    Private Shared ReadOnly RayCounts As Integer() = {128, 256, 512, 1024}
    Private Shared ReadOnly QualityNames As String() =
        {"Medium - 128 rays", "High - 256 rays", "Ultra - 512 rays", "Extreme - 1024 rays"}

    Public Sub New(targetMesh As PreviewModel.RenderableMesh,
                   occluderMeshes As IEnumerable(Of PreviewModel.RenderableMesh))
        _targetMesh = targetMesh
        _candidatos.AddRange(occluderMeshes.Where(Function(m) m IsNot Nothing AndAlso m.MeshData IsNot Nothing))
        InitializeComponent()

        cboQuality.Items.Clear()
        cboQuality.Items.AddRange(QualityNames)
        cboQuality.SelectedIndex = 1

        lblTarget.Text = "Target: " & If(targetMesh?.MeshData IsNot Nothing, targetMesh.MeshData.ShapeName, "(none)")
        PoblarOcluders()

        If clbOccluders.Items.Count = 0 Then
            btnAction.Enabled = False
            ' Caso real y distinto de "nada tapa": no hay OTRA shape en el proyecto. Ya no existe el
            ' filtro automatico por material que antes vaciaba la lista en silencio.
            lblStatus.Text = "No other shapes to occlude this one."
            lblStatus.ForeColor = Color.DarkRed
        End If
    End Sub

    ''' <summary>Llena la lista informativa con la ley de opacidad que va a usar cada ocluder. ⛔ NO es
    ''' una eleccion: la transparencia se resuelve por PUNTO de impacto con la alpha de la textura, asi
    ''' que no hay nada que tildar. Esta para poder VER con que ley entro cada shape.</summary>
    Private Sub PoblarOcluders()
        clbOccluders.Items.Clear()
        For Each m In _candidatos
            Dim mat = m.MeshData.Material
            Dim clase = "opaque"
            ' El flag Decal se muestra pero NO cambia la ley: si es opaco en el punto, tapa.
            Dim esDecal = mat IsNot Nothing AndAlso mat.MaterialBase IsNot Nothing AndAlso mat.MaterialBase.Decal
            If mat IsNot Nothing AndAlso mat.HasAlphaTest Then
                clase = "alpha-test - solid parts block, holes do not"
            ElseIf mat IsNot Nothing AndAlso mat.HasAlphaBlend Then
                Dim a = If(mat.MaterialBase Is Nothing, 1.0F, mat.MaterialBase.Alpha)
                clase = $"alpha-blend - opacity {a:0.00} x texture alpha"
            End If
            Dim tris = 0
            If m.MeshData.Meshgeometry.Indices IsNot Nothing Then tris = m.MeshData.Meshgeometry.Indices.Length \ 3
            If esDecal Then clase &= ", decal"
            clbOccluders.Items.Add($"{m.MeshData.ShapeName}   [{clase}, {tris} tris]")
        Next
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
        ' El BVH se arma en la primera corrida y se reusa: la escena no cambia mientras el dialogo
        ' esta abierto (es modal).
        If _raytracer Is Nothing Then _raytracer = New OcclusionRaytracer(_candidatos)

        Dim settings = New OcclusionRaytracer.RaycastSettings With {
            .RayCount = RayCounts(cboQuality.SelectedIndex),
            .NormalBias = CSng(nudBias.Value),
            .OcclusionThreshold = CSng(nudThreshold.Value),
            .GrazingCutoffDeg = CSng(nudGrazing.Value),
            .MinClearance = CSng(nudClearance.Value),
            .SafetyRings = CInt(nudRings.Value)
        }

        _cts?.Dispose()
        _cts = New CancellationTokenSource()
        ' Capture this run's CTS locally so the continuation can dispose exactly the instance the
        ' background work used — never the field, which a later Start may have already replaced.
        Dim runCts = _cts
        _state = RunState.Running
        btnAction.Text = "Cancel"
        btnClose.Enabled = False
        btnApply.Enabled = False
        HabilitaAjustes(False)
        ResultVertices = Nothing
        progressBar1.Value = 0
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Text = "Processing..."

        Dim progress As IProgress(Of Integer) = New Progress(Of Integer)(Sub(pct)
                                                                             If Me.IsDisposed Then Return
                                                                             progressBar1.Value = Math.Min(pct, 100)
                                                                             lblStatus.Text = $"Processing... {pct}%"
                                                                         End Sub)

        Dim token = runCts.Token
        Task.Run(
            Function()
                Return _raytracer.ComputeOccludedVertices(_targetMesh, settings, progress, token)
            End Function).
            ContinueWith(Sub(t)
                             ' The background work (including its Parallel.For bound to this token)
                             ' has finished by the time the continuation runs, so it's safe to
                             ' dispose the CTS now. Do it in Finally so it happens even when the
                             ' form was disposed and the UI update below is skipped.
                             Try
                                 If Me.IsDisposed Then Return
                                 Try
                                     Me.Invoke(Sub()
                                                   _state = RunState.Done
                                                   btnClose.Enabled = True
                                                   HabilitaAjustes(True)

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
                                                       lblStatus.Text = MensajeResultado(count, settings)
                                                       lblStatus.ForeColor = If(count > 0, Color.DarkGreen, Color.DarkRed)
                                                       btnAction.Text = "Start"
                                                       btnApply.Enabled = count > 0
                                                   End If
                                               End Sub)
                                 Catch ex As ObjectDisposedException
                                     ' Form was disposed between IsDisposed check and Invoke call; safe to ignore.
                                 End Try
                             Finally
                                 runCts.Dispose()
                             End Try
                         End Sub)
    End Sub

    ''' <summary>⛔ Un cero NO se reporta como exito silencioso. "Done: 0 vertices" se lee como "no hay
    ''' nada tapado", cuando casi siempre significa que algun ajuste esta de mas — y el usuario no tiene
    ''' forma de saber cual. El mensaje nombra los sospechosos EN ORDEN de cuanto suelen recortar.</summary>
    Private Function MensajeResultado(count As Integer, settings As OcclusionRaytracer.RaycastSettings) As String
        ' ⭐ Las tres etapas, siempre. Un solo numero final no distingue "los rayos no vieron la region
        ' tapada" de "la erosion se la comio", y esas dos se arreglan en lugares opuestos.
        Dim etapas = $"rays hid {_raytracer.LastRayHidden} -> topology {_raytracer.LastAfterTopology} -> rings {_raytracer.LastAfterRings}"
        If count > 0 Then
            Return $"Done: {count} vertices to mask.   {etapas}   ({settings.RayCount} rays, {_raytracer.OccluderTriangleCount} occluder triangles)"
        End If

        Dim causas As New List(Of String)
        If settings.MinClearance > 0.0F Then causas.Add($"lower Min clearance (now {settings.MinClearance:0.00})")
        If settings.GrazingCutoffDeg < 20.0F Then causas.Add($"raise Ignore grazing (now {settings.GrazingCutoffDeg:0} deg)")
        If settings.SafetyRings > 0 Then causas.Add($"lower Safety rings to 0 (now {settings.SafetyRings}) - it eats narrow patches whole")
        causas.Add("check the occluder list at the top - a see-through material blocks only where its texture alpha is solid")
        Return $"Nothing masked.   {etapas}" & Environment.NewLine & "Try: " & String.Join("; ", causas) & "."
    End Function

    Private Sub HabilitaAjustes(activo As Boolean)
        cboQuality.Enabled = activo
        nudGrazing.Enabled = activo
        nudThreshold.Enabled = activo
        nudClearance.Enabled = activo
        nudRings.Enabled = activo
        nudBias.Enabled = activo
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        ' Request cancellation only. The CTS is disposed by the running task's continuation AFTER the
        ' background work (with its Parallel.For bound to the token) finishes — disposing it here
        ' would race the still-running work that's reading the token. If a prior run already
        ' completed, its continuation may have disposed this CTS already, so Cancel() can throw
        ' ObjectDisposedException — harmless at close time.
        Try
            _cts?.Cancel()
        Catch ex As ObjectDisposedException
        End Try
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
        RaiseEvent ApplyOcclusion(Me)
        btnApply.Enabled = False
    End Sub
End Class
