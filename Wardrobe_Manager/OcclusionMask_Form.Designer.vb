' Version Uploaded of Wardrobe 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class OcclusionMask_Form
    ' Hereda de FO4_Base_Library.IconFormBase, que aporta los ImageList compartidos IconsSmall (16x16)
    ' e IconsLarge (24x24): los iconos viven UNA sola vez, en el resx de ese formulario base.
    ' El formulario base NO tiene controles y no fija Size/Text/Icon/AutoScale, asi que heredar de
    ' el no cambia el aspecto de nada. Ver el remarks de IconFormBase.vb.
    ' ⛔ Los iconos se eligen SIEMPRE por ImageKey, nunca por ImageIndex: el orden del ImageList
    ' compartido se corre solo con agregar un PNG a Resources\Icons.
    Inherits FO4_Base_Library.IconFormBase

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    ' ─────────────────────────────────────────────────────────────────────────────────────────────
    ' LAYOUT EN DOS COLUMNAS. Antes era una sola columna de casi 720 px de alto: los dos grupos de
    ' ajustes uno debajo del otro no entraban comodos en pantalla y obligaban a barrer de arriba abajo
    ' para comparar dos numeros que se leen juntos.
    ' Estructura: cabecera a lo ancho (target + ocluders) / los dos grupos LADO A LADO / pie a lo ancho
    ' (progreso + estado + botones). Los dos grupos comparten la MISMA grilla interna —etiqueta en
    ' x=12, control en x=118, explicacion a lo ancho debajo— asi que la vista salta de uno al otro sin
    ' reacomodarse.
    '
    ' ⛔ CADA PARAMETRO LLEVA SU EXPLICACION AL LADO, no un tooltip. Un tooltip que hay que descubrir
    ' pasando el mouse no existe para el que no sabe que hay algo que leer, y estos parametros deciden
    ' si el resultado abre un hueco en el mod. El texto gris dice QUE PASA si se mueve, no que es.
    ' ⛔ Todo el texto visible va en INGLES, como el resto de la UI de la aplicacion.
    ' ─────────────────────────────────────────────────────────────────────────────────────────────
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        lblTarget = New Label()
        lblOccluders = New Label()
        clbOccluders = New ListBox()
        lblOccludersHint = New Label()
        grpDetect = New GroupBox()
        lblQuality = New Label()
        cboQuality = New ComboBox()
        lblQualityHint = New Label()
        lblGrazing = New Label()
        nudGrazing = New NumericUpDown()
        lblGrazingHint = New Label()
        lblThreshold = New Label()
        nudThreshold = New NumericUpDown()
        lblThresholdHint = New Label()
        grpSafety = New GroupBox()
        lblClearance = New Label()
        nudClearance = New NumericUpDown()
        lblClearanceHint = New Label()
        lblRings = New Label()
        nudRings = New NumericUpDown()
        lblRingsHint = New Label()
        lblBias = New Label()
        nudBias = New NumericUpDown()
        lblBiasHint = New Label()
        progressBar1 = New ProgressBar()
        lblStatus = New Label()
        btnAction = New Button()
        btnClose = New Button()
        btnApply = New Button()
        grpDetect.SuspendLayout()
        grpSafety.SuspendLayout()
        CType(nudGrazing, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(nudThreshold, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(nudClearance, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(nudRings, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(nudBias, System.ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        '
        ' ══ CABECERA ═════════════════════════════════════════════════════════
        '
        ' lblTarget
        '
        lblTarget.AutoSize = False
        lblTarget.Font = New Font(Font, FontStyle.Bold)
        lblTarget.Location = New Point(14, 12)
        lblTarget.Name = "lblTarget"
        lblTarget.Size = New Size(758, 19)
        lblTarget.TabIndex = 0
        lblTarget.Text = "Target:"
        '
        ' lblOccluders
        '
        lblOccluders.AutoSize = True
        lblOccluders.Location = New Point(14, 38)
        lblOccluders.Name = "lblOccluders"
        lblOccluders.Size = New Size(160, 15)
        lblOccluders.TabIndex = 1
        lblOccluders.Text = "Shapes that can occlude it"
        '
        ' clbOccluders
        '
        ' Lista INFORMATIVA: el usuario no elige nada. La opacidad se resuelve por PUNTO de impacto
        ' (ver MatInfo en OcclusionRaytracer), asi que no hay ninguna decision por shape que tomar.
        ' Sirve para VER que entro como ocluder y con que ley, que es lo que faltaba cuando el resultado
        ' salia corto y no habia forma de saber por que.
        clbOccluders.IntegralHeight = False
        clbOccluders.Location = New Point(14, 58)
        clbOccluders.Name = "clbOccluders"
        clbOccluders.SelectionMode = SelectionMode.None
        clbOccluders.Size = New Size(758, 74)
        clbOccluders.TabIndex = 2
        '
        ' lblOccludersHint
        '
        lblOccludersHint.AutoSize = False
        lblOccludersHint.ForeColor = SystemColors.GrayText
        lblOccludersHint.Location = New Point(14, 136)
        lblOccludersHint.Name = "lblOccludersHint"
        lblOccludersHint.Size = New Size(758, 17)
        lblOccludersHint.TabIndex = 3
        lblOccludersHint.Text = "All of them block rays. Transparency is resolved per hit point from the texture alpha, not per shape."
        '
        ' ══ COLUMNA IZQUIERDA ════════════════════════════════════════════════
        '
        ' grpDetect
        '
        grpDetect.Controls.Add(lblQuality)
        grpDetect.Controls.Add(cboQuality)
        grpDetect.Controls.Add(lblQualityHint)
        grpDetect.Controls.Add(lblGrazing)
        grpDetect.Controls.Add(nudGrazing)
        grpDetect.Controls.Add(lblGrazingHint)
        grpDetect.Controls.Add(lblThreshold)
        grpDetect.Controls.Add(nudThreshold)
        grpDetect.Controls.Add(lblThresholdHint)
        grpDetect.Location = New Point(14, 162)
        grpDetect.Name = "grpDetect"
        grpDetect.Size = New Size(372, 233)
        grpDetect.TabIndex = 4
        grpDetect.TabStop = False
        grpDetect.Text = "What counts as hidden"
        '
        ' lblQuality
        '
        lblQuality.AutoSize = True
        lblQuality.Location = New Point(12, 29)
        lblQuality.Name = "lblQuality"
        lblQuality.Size = New Size(94, 15)
        lblQuality.TabIndex = 0
        lblQuality.Text = "Rays per vertex"
        '
        ' cboQuality
        '
        cboQuality.DropDownStyle = ComboBoxStyle.DropDownList
        cboQuality.FormattingEnabled = True
        cboQuality.Location = New Point(118, 26)
        cboQuality.Name = "cboQuality"
        cboQuality.Size = New Size(236, 23)
        cboQuality.TabIndex = 1
        '
        ' lblQualityHint
        '
        lblQualityHint.AutoSize = False
        lblQualityHint.ForeColor = SystemColors.GrayText
        lblQualityHint.Location = New Point(12, 53)
        lblQualityHint.Name = "lblQualityHint"
        lblQualityHint.Size = New Size(342, 34)
        lblQualityHint.TabIndex = 2
        lblQualityHint.Text = "More rays find smaller gaps. But with the value below at 1.00 they also add chances to leak, so raise both together."
        '
        ' lblGrazing
        '
        lblGrazing.AutoSize = True
        lblGrazing.Location = New Point(12, 95)
        lblGrazing.Name = "lblGrazing"
        lblGrazing.Size = New Size(94, 15)
        lblGrazing.TabIndex = 3
        lblGrazing.Text = "Ignore grazing"
        '
        ' nudGrazing
        '
        nudGrazing.Location = New Point(118, 93)
        nudGrazing.Maximum = New Decimal(New Integer() {60, 0, 0, 0})
        nudGrazing.Name = "nudGrazing"
        nudGrazing.Size = New Size(70, 23)
        nudGrazing.TabIndex = 4
        nudGrazing.Value = New Decimal(New Integer() {12, 0, 0, 0})
        '
        ' lblGrazingHint
        '
        lblGrazingHint.AutoSize = False
        lblGrazingHint.ForeColor = SystemColors.GrayText
        lblGrazingHint.Location = New Point(12, 120)
        lblGrazingHint.Name = "lblGrazingHint"
        lblGrazingHint.Size = New Size(342, 34)
        lblGrazingHint.TabIndex = 5
        lblGrazingHint.Text = "Degrees above the skin. Flatter rays slide along the body and escape under the hem. 0 = full hemisphere."
        '
        ' lblThreshold
        '
        lblThreshold.AutoSize = True
        lblThreshold.Location = New Point(12, 162)
        lblThreshold.Name = "lblThreshold"
        lblThreshold.Size = New Size(102, 15)
        lblThreshold.TabIndex = 6
        lblThreshold.Text = "Rays that must hit"
        '
        ' nudThreshold
        '
        nudThreshold.DecimalPlaces = 2
        nudThreshold.Increment = New Decimal(New Integer() {1, 0, 0, 131072})
        nudThreshold.Location = New Point(118, 160)
        nudThreshold.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        nudThreshold.Minimum = New Decimal(New Integer() {5, 0, 0, 65536})
        nudThreshold.Name = "nudThreshold"
        nudThreshold.Size = New Size(70, 23)
        nudThreshold.TabIndex = 7
        nudThreshold.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        ' lblThresholdHint
        '
        lblThresholdHint.AutoSize = False
        lblThresholdHint.ForeColor = SystemColors.GrayText
        lblThresholdHint.Location = New Point(12, 187)
        lblThresholdHint.Name = "lblThresholdHint"
        lblThresholdHint.Size = New Size(342, 34)
        lblThresholdHint.TabIndex = 8
        lblThresholdHint.Text = "1.00 = no ray may escape, so more rays give LESS mask. With 1024 rays use 0.98."
        '
        ' ══ COLUMNA DERECHA ══════════════════════════════════════════════════
        '
        ' grpSafety
        '
        grpSafety.Controls.Add(lblClearance)
        grpSafety.Controls.Add(nudClearance)
        grpSafety.Controls.Add(lblClearanceHint)
        grpSafety.Controls.Add(lblRings)
        grpSafety.Controls.Add(nudRings)
        grpSafety.Controls.Add(lblRingsHint)
        grpSafety.Controls.Add(lblBias)
        grpSafety.Controls.Add(nudBias)
        grpSafety.Controls.Add(lblBiasHint)
        grpSafety.Location = New Point(400, 162)
        grpSafety.Name = "grpSafety"
        grpSafety.Size = New Size(372, 233)
        grpSafety.TabIndex = 5
        grpSafety.TabStop = False
        grpSafety.Text = "Safety margin"
        '
        ' lblClearance
        '
        lblClearance.AutoSize = True
        lblClearance.Location = New Point(12, 29)
        lblClearance.Name = "lblClearance"
        lblClearance.Size = New Size(94, 15)
        lblClearance.TabIndex = 0
        lblClearance.Text = "Min clearance"
        '
        ' nudClearance
        '
        nudClearance.DecimalPlaces = 2
        nudClearance.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudClearance.Location = New Point(118, 27)
        nudClearance.Maximum = New Decimal(New Integer() {5, 0, 0, 0})
        nudClearance.Name = "nudClearance"
        nudClearance.Size = New Size(70, 23)
        nudClearance.TabIndex = 1
        '
        ' lblClearanceHint
        '
        lblClearanceHint.AutoSize = False
        lblClearanceHint.ForeColor = SystemColors.GrayText
        lblClearanceHint.Location = New Point(12, 53)
        lblClearanceHint.Name = "lblClearanceHint"
        lblClearanceHint.Size = New Size(342, 34)
        lblClearanceHint.TabIndex = 2
        lblClearanceHint.Text = "NIF units the garment must sit above the skin. A skin-tight outfit needs 0."
        '
        ' lblRings
        '
        lblRings.AutoSize = True
        lblRings.Location = New Point(12, 95)
        lblRings.Name = "lblRings"
        lblRings.Size = New Size(94, 15)
        lblRings.TabIndex = 3
        lblRings.Text = "Safety rings"
        '
        ' nudRings
        '
        nudRings.Location = New Point(118, 93)
        nudRings.Maximum = New Decimal(New Integer() {3, 0, 0, 0})
        nudRings.Name = "nudRings"
        nudRings.Size = New Size(70, 23)
        nudRings.TabIndex = 4
        nudRings.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        ' lblRingsHint
        '
        lblRingsHint.AutoSize = False
        lblRingsHint.ForeColor = SystemColors.GrayText
        lblRingsHint.Location = New Point(12, 120)
        lblRingsHint.Name = "lblRingsHint"
        lblRingsHint.Size = New Size(342, 34)
        lblRingsHint.TabIndex = 5
        lblRingsHint.Text = "Extra shrink of the mask border, on top of the rule that already prevents holes. It eats narrow patches whole: try 0."
        '
        ' lblBias
        '
        lblBias.AutoSize = True
        lblBias.Location = New Point(12, 162)
        lblBias.Name = "lblBias"
        lblBias.Size = New Size(94, 15)
        lblBias.TabIndex = 6
        lblBias.Text = "Normal bias"
        '
        ' nudBias
        '
        ' ⛔⛔ DEFAULT 0,01, NO 0,50 — el 0,50 heredado era EL bug de fondo. El bias corre el ORIGEN del
        ' rayo hacia afuera antes de disparar, y una prenda ajustada se apoya a 0,2-0,4 unidades de la
        ' piel: con medio unidad el origen quedaba DEL OTRO LADO DE LA TELA y desde ahi todos los rayos
        ' escapan, o sea que el torso bajo un vestido daba "visible". Las botas, gruesas y a 1-2
        ' unidades, seguian tapando — por eso lo unico que se enmascaraba eran las pantorrillas.
        ' Y el bias metrico ya no hace falta para nada: la auto-interseccion se excluye por TOPOLOGIA
        ' (se saltean los triangulos incidentes al vertice), no por distancia.
        nudBias.DecimalPlaces = 2
        nudBias.Increment = New Decimal(New Integer() {1, 0, 0, 131072})
        nudBias.Location = New Point(118, 160)
        nudBias.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        nudBias.Name = "nudBias"
        nudBias.Size = New Size(70, 23)
        nudBias.TabIndex = 7
        nudBias.Value = New Decimal(New Integer() {1, 0, 0, 131072})
        '
        ' lblBiasHint
        '
        lblBiasHint.AutoSize = False
        lblBiasHint.ForeColor = SystemColors.GrayText
        lblBiasHint.Location = New Point(12, 187)
        lblBiasHint.Name = "lblBiasHint"
        lblBiasHint.Size = New Size(342, 34)
        lblBiasHint.TabIndex = 8
        lblBiasHint.Text = "Keep it tiny, or the ray starts outside the garment and nothing blocks it."
        '
        ' ══ PIE ══════════════════════════════════════════════════════════════
        '
        ' progressBar1
        '
        progressBar1.Location = New Point(14, 409)
        progressBar1.Name = "progressBar1"
        progressBar1.Size = New Size(758, 14)
        progressBar1.TabIndex = 6
        '
        ' lblStatus
        '
        lblStatus.AutoSize = False
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Location = New Point(14, 429)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(758, 34)
        lblStatus.TabIndex = 7
        lblStatus.Text = "Ready."
        '
        ' btnAction
        '
        btnAction.Location = New Point(14, 471)
        btnAction.Name = "btnAction"
        btnAction.Size = New Size(110, 28)
        btnAction.TabIndex = 8
        btnAction.Text = "Start"
        btnAction.UseVisualStyleBackColor = True
        '
        ' btnApply
        '
        btnApply.Enabled = False
        btnApply.Location = New Point(578, 471)
        btnApply.Name = "btnApply"
        btnApply.Size = New Size(92, 28)
        btnApply.TabIndex = 9
        btnApply.Text = "Apply"
        btnApply.UseVisualStyleBackColor = True
        '
        ' btnClose
        '
        btnClose.DialogResult = DialogResult.Cancel
        btnClose.Location = New Point(680, 471)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(92, 28)
        btnClose.TabIndex = 10
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = True
        '
        ' OcclusionMask_Form
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        CancelButton = btnClose
        ClientSize = New Size(786, 515)
        Controls.Add(lblTarget)
        Controls.Add(lblOccluders)
        Controls.Add(clbOccluders)
        Controls.Add(lblOccludersHint)
        Controls.Add(grpDetect)
        Controls.Add(grpSafety)
        Controls.Add(progressBar1)
        Controls.Add(lblStatus)
        Controls.Add(btnAction)
        Controls.Add(btnApply)
        Controls.Add(btnClose)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "OcclusionMask_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Mask Occluded"
        grpDetect.ResumeLayout(False)
        grpDetect.PerformLayout()
        grpSafety.ResumeLayout(False)
        grpSafety.PerformLayout()
        CType(nudGrazing, System.ComponentModel.ISupportInitialize).EndInit()
        CType(nudThreshold, System.ComponentModel.ISupportInitialize).EndInit()
        CType(nudClearance, System.ComponentModel.ISupportInitialize).EndInit()
        CType(nudRings, System.ComponentModel.ISupportInitialize).EndInit()
        CType(nudBias, System.ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents lblTarget As Label
    Friend WithEvents lblOccluders As Label
    Friend WithEvents clbOccluders As ListBox
    Friend WithEvents lblOccludersHint As Label
    Friend WithEvents grpDetect As GroupBox
    Friend WithEvents lblQuality As Label
    Friend WithEvents cboQuality As ComboBox
    Friend WithEvents lblQualityHint As Label
    Friend WithEvents lblGrazing As Label
    Friend WithEvents nudGrazing As NumericUpDown
    Friend WithEvents lblGrazingHint As Label
    Friend WithEvents lblThreshold As Label
    Friend WithEvents nudThreshold As NumericUpDown
    Friend WithEvents lblThresholdHint As Label
    Friend WithEvents grpSafety As GroupBox
    Friend WithEvents lblClearance As Label
    Friend WithEvents nudClearance As NumericUpDown
    Friend WithEvents lblClearanceHint As Label
    Friend WithEvents lblRings As Label
    Friend WithEvents nudRings As NumericUpDown
    Friend WithEvents lblRingsHint As Label
    Friend WithEvents lblBias As Label
    Friend WithEvents nudBias As NumericUpDown
    Friend WithEvents lblBiasHint As Label
    Friend WithEvents progressBar1 As ProgressBar
    Friend WithEvents lblStatus As Label
    Friend WithEvents btnAction As Button
    Friend WithEvents btnClose As Button
    Friend WithEvents btnApply As Button
End Class
