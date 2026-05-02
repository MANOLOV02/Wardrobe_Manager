' Version Uploaded of Wardrobe 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class OcclusionMask_Form
    Inherits System.Windows.Forms.Form

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

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        lblQuality = New Label()
        cboQuality = New ComboBox()
        lblThreshold = New Label()
        nudThreshold = New NumericUpDown()
        lblThresholdHint = New Label()
        lblNormalBias = New Label()
        nudBias = New NumericUpDown()
        lblNormalBiasHint = New Label()
        chkTriangles = New CheckBox()
        chkSelfOcclusion = New CheckBox()
        lblSelfMinDist = New Label()
        nudSelfMinDist = New NumericUpDown()
        lblSelfMinDistHint = New Label()
        pnlSeparator = New Panel()
        progressBar1 = New ProgressBar()
        lblStatus = New Label()
        btnAction = New Button()
        btnClose = New Button()
        btnApply = New Button()
        CType(nudThreshold, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudBias, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudSelfMinDist, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' lblQuality
        ' 
        lblQuality.Location = New Point(14, 16)
        lblQuality.Name = "lblQuality"
        lblQuality.Size = New Size(110, 20)
        lblQuality.TabIndex = 0
        lblQuality.Text = "Quality:"
        lblQuality.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' cboQuality
        ' 
        cboQuality.DropDownStyle = ComboBoxStyle.DropDownList
        cboQuality.Items.AddRange(New Object() {"Fast  (32 rays)", "Balanced  (64 rays)", "Quality  (128 rays)", "Ultra  (256 rays)"})
        cboQuality.Location = New Point(130, 14)
        cboQuality.Name = "cboQuality"
        cboQuality.Size = New Size(200, 23)
        cboQuality.TabIndex = 1
        ' 
        ' lblThreshold
        ' 
        lblThreshold.Location = New Point(14, 50)
        lblThreshold.Name = "lblThreshold"
        lblThreshold.Size = New Size(110, 20)
        lblThreshold.TabIndex = 2
        lblThreshold.Text = "Threshold:"
        lblThreshold.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' nudThreshold
        ' 
        nudThreshold.DecimalPlaces = 2
        nudThreshold.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudThreshold.Location = New Point(130, 48)
        nudThreshold.Maximum = New Decimal(New Integer() {1, 0, 0, 0})
        nudThreshold.Minimum = New Decimal(New Integer() {5, 0, 0, 65536})
        nudThreshold.Name = "nudThreshold"
        nudThreshold.Size = New Size(70, 23)
        nudThreshold.TabIndex = 3
        nudThreshold.TextAlign = HorizontalAlignment.Right
        nudThreshold.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' lblThresholdHint
        ' 
        lblThresholdHint.ForeColor = SystemColors.GrayText
        lblThresholdHint.Location = New Point(208, 50)
        lblThresholdHint.Name = "lblThresholdHint"
        lblThresholdHint.Size = New Size(130, 20)
        lblThresholdHint.TabIndex = 4
        lblThresholdHint.Text = "1.0 = certain"
        ' 
        ' lblNormalBias
        ' 
        lblNormalBias.Location = New Point(14, 84)
        lblNormalBias.Name = "lblNormalBias"
        lblNormalBias.Size = New Size(110, 20)
        lblNormalBias.TabIndex = 5
        lblNormalBias.Text = "Normal Bias:"
        lblNormalBias.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' nudBias
        ' 
        nudBias.DecimalPlaces = 2
        nudBias.Increment = New Decimal(New Integer() {1, 0, 0, 65536})
        nudBias.Location = New Point(130, 82)
        nudBias.Maximum = New Decimal(New Integer() {5, 0, 0, 0})
        nudBias.Minimum = New Decimal(New Integer() {1, 0, 0, 131072})
        nudBias.Name = "nudBias"
        nudBias.Size = New Size(70, 23)
        nudBias.TabIndex = 6
        nudBias.TextAlign = HorizontalAlignment.Right
        nudBias.Value = New Decimal(New Integer() {5, 0, 0, 131072})
        ' 
        ' lblNormalBiasHint
        ' 
        lblNormalBiasHint.ForeColor = SystemColors.GrayText
        lblNormalBiasHint.Location = New Point(208, 84)
        lblNormalBiasHint.Name = "lblNormalBiasHint"
        lblNormalBiasHint.Size = New Size(130, 20)
        lblNormalBiasHint.TabIndex = 7
        lblNormalBiasHint.Text = "NIF units"
        ' 
        ' chkTriangles
        ' 
        chkTriangles.Checked = True
        chkTriangles.CheckState = CheckState.Checked
        chkTriangles.Location = New Point(14, 116)
        chkTriangles.Name = "chkTriangles"
        chkTriangles.Size = New Size(330, 22)
        chkTriangles.TabIndex = 8
        chkTriangles.Text = "Complete triangles only  (all 3 verts occluded)"
        chkTriangles.UseVisualStyleBackColor = True
        ' 
        ' chkSelfOcclusion
        ' 
        chkSelfOcclusion.Checked = True
        chkSelfOcclusion.CheckState = CheckState.Checked
        chkSelfOcclusion.Location = New Point(14, 146)
        chkSelfOcclusion.Name = "chkSelfOcclusion"
        chkSelfOcclusion.Size = New Size(330, 22)
        chkSelfOcclusion.TabIndex = 9
        chkSelfOcclusion.Text = "Self-occlusion  (e.g. legs block torso under a dress)"
        chkSelfOcclusion.UseVisualStyleBackColor = True
        ' 
        ' lblSelfMinDist
        ' 
        lblSelfMinDist.Location = New Point(14, 176)
        lblSelfMinDist.Name = "lblSelfMinDist"
        lblSelfMinDist.Size = New Size(110, 20)
        lblSelfMinDist.TabIndex = 10
        lblSelfMinDist.Text = "  Self min dist:"
        lblSelfMinDist.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' nudSelfMinDist
        ' 
        nudSelfMinDist.DecimalPlaces = 1
        nudSelfMinDist.Increment = New Decimal(New Integer() {5, 0, 0, 65536})
        nudSelfMinDist.Location = New Point(130, 174)
        nudSelfMinDist.Maximum = New Decimal(New Integer() {50, 0, 0, 0})
        nudSelfMinDist.Name = "nudSelfMinDist"
        nudSelfMinDist.Size = New Size(70, 23)
        nudSelfMinDist.TabIndex = 11
        nudSelfMinDist.TextAlign = HorizontalAlignment.Right
        ' 
        ' lblSelfMinDistHint
        ' 
        lblSelfMinDistHint.ForeColor = SystemColors.GrayText
        lblSelfMinDistHint.Location = New Point(208, 176)
        lblSelfMinDistHint.Name = "lblSelfMinDistHint"
        lblSelfMinDistHint.Size = New Size(130, 20)
        lblSelfMinDistHint.TabIndex = 12
        lblSelfMinDistHint.Text = "0 = auto"
        ' 
        ' pnlSeparator
        ' 
        pnlSeparator.BackColor = SystemColors.ControlDark
        pnlSeparator.Location = New Point(14, 208)
        pnlSeparator.Name = "pnlSeparator"
        pnlSeparator.Size = New Size(332, 1)
        pnlSeparator.TabIndex = 13
        ' 
        ' progressBar1
        ' 
        progressBar1.Location = New Point(14, 218)
        progressBar1.Name = "progressBar1"
        progressBar1.Size = New Size(332, 18)
        progressBar1.TabIndex = 14
        ' 
        ' lblStatus
        ' 
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Location = New Point(14, 244)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(332, 20)
        lblStatus.TabIndex = 15
        lblStatus.Text = "Ready."
        ' 
        ' btnAction
        ' 
        btnAction.Location = New Point(130, 274)
        btnAction.Name = "btnAction"
        btnAction.Size = New Size(100, 26)
        btnAction.TabIndex = 16
        btnAction.Text = "Start"
        btnAction.UseVisualStyleBackColor = True
        ' 
        ' btnClose
        ' 
        btnClose.DialogResult = DialogResult.Cancel
        btnClose.Location = New Point(246, 274)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(100, 26)
        btnClose.TabIndex = 17
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = True
        ' 
        ' btnApply
        ' 
        btnApply.Enabled = False
        btnApply.Location = New Point(14, 274)
        btnApply.Name = "btnApply"
        btnApply.Size = New Size(100, 26)
        btnApply.TabIndex = 18
        btnApply.Text = "Apply"
        btnApply.UseVisualStyleBackColor = True
        ' 
        ' OcclusionMask_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        CancelButton = btnClose
        ClientSize = New Size(360, 312)
        Controls.Add(btnApply)
        Controls.Add(lblQuality)
        Controls.Add(cboQuality)
        Controls.Add(lblThreshold)
        Controls.Add(nudThreshold)
        Controls.Add(lblThresholdHint)
        Controls.Add(lblNormalBias)
        Controls.Add(nudBias)
        Controls.Add(lblNormalBiasHint)
        Controls.Add(chkTriangles)
        Controls.Add(chkSelfOcclusion)
        Controls.Add(lblSelfMinDist)
        Controls.Add(nudSelfMinDist)
        Controls.Add(lblSelfMinDistHint)
        Controls.Add(pnlSeparator)
        Controls.Add(progressBar1)
        Controls.Add(lblStatus)
        Controls.Add(btnAction)
        Controls.Add(btnClose)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "OcclusionMask_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Mask Occluded Vertices"
        CType(nudThreshold, ComponentModel.ISupportInitialize).EndInit()
        CType(nudBias, ComponentModel.ISupportInitialize).EndInit()
        CType(nudSelfMinDist, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
    End Sub

    Friend WithEvents lblQuality As Label
    Friend WithEvents cboQuality As ComboBox
    Friend WithEvents lblThreshold As Label
    Friend WithEvents nudThreshold As NumericUpDown
    Friend WithEvents lblThresholdHint As Label
    Friend WithEvents lblNormalBias As Label
    Friend WithEvents nudBias As NumericUpDown
    Friend WithEvents lblNormalBiasHint As Label
    Friend WithEvents chkTriangles As CheckBox
    Friend WithEvents chkSelfOcclusion As CheckBox
    Friend WithEvents lblSelfMinDist As Label
    Friend WithEvents nudSelfMinDist As NumericUpDown
    Friend WithEvents lblSelfMinDistHint As Label
    Friend WithEvents pnlSeparator As Panel
    Friend WithEvents progressBar1 As ProgressBar
    Friend WithEvents lblStatus As Label
    Friend WithEvents btnAction As Button
    Friend WithEvents btnClose As Button
    Friend WithEvents btnApply As Button

End Class
