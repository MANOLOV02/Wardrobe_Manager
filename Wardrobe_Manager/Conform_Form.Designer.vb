' Version Uploaded of Wardrobe 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Conform_Form
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
        lblSourceShape = New Label()
        cboSource = New ComboBox()
        lblSliders = New Label()
        clbSliders = New CheckedListBox()
        btnSelectAll = New Button()
        btnSelectNone = New Button()
        lblSearchRadius = New Label()
        nudSearchRadius = New NumericUpDown()
        lblSearchRadiusHint = New Label()
        grpAxis = New GroupBox()
        chkAxisX = New CheckBox()
        chkAxisY = New CheckBox()
        chkAxisZ = New CheckBox()
        chkOverwrite = New CheckBox()
        pnlSeparator = New Panel()
        progressBar1 = New ProgressBar()
        lblStatus = New Label()
        btnAction = New Button()
        btnClose = New Button()
        btnApply = New Button()
        CType(nudSearchRadius, ComponentModel.ISupportInitialize).BeginInit()
        grpAxis.SuspendLayout()
        SuspendLayout()
        ' 
        ' lblSourceShape
        ' 
        lblSourceShape.Location = New Point(14, 16)
        lblSourceShape.Name = "lblSourceShape"
        lblSourceShape.Size = New Size(110, 20)
        lblSourceShape.TabIndex = 0
        lblSourceShape.Text = "Source shape:"
        lblSourceShape.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' cboSource
        ' 
        cboSource.DropDownStyle = ComboBoxStyle.DropDownList
        cboSource.Location = New Point(130, 14)
        cboSource.Name = "cboSource"
        cboSource.Size = New Size(240, 23)
        cboSource.TabIndex = 1
        ' 
        ' lblSliders
        ' 
        lblSliders.Location = New Point(14, 48)
        lblSliders.Name = "lblSliders"
        lblSliders.Size = New Size(356, 18)
        lblSliders.TabIndex = 2
        lblSliders.Text = "Sliders to conform:"
        ' 
        ' clbSliders
        ' 
        clbSliders.CheckOnClick = True
        clbSliders.FormattingEnabled = True
        clbSliders.Location = New Point(14, 68)
        clbSliders.Name = "clbSliders"
        clbSliders.Size = New Size(356, 166)
        clbSliders.TabIndex = 3
        ' 
        ' btnSelectAll
        ' 
        btnSelectAll.Location = New Point(14, 248)
        btnSelectAll.Name = "btnSelectAll"
        btnSelectAll.Size = New Size(80, 22)
        btnSelectAll.TabIndex = 4
        btnSelectAll.Text = "All"
        btnSelectAll.UseVisualStyleBackColor = True
        ' 
        ' btnSelectNone
        ' 
        btnSelectNone.Location = New Point(100, 248)
        btnSelectNone.Name = "btnSelectNone"
        btnSelectNone.Size = New Size(80, 22)
        btnSelectNone.TabIndex = 5
        btnSelectNone.Text = "None"
        btnSelectNone.UseVisualStyleBackColor = True
        ' 
        ' lblSearchRadius
        ' 
        lblSearchRadius.Location = New Point(14, 284)
        lblSearchRadius.Name = "lblSearchRadius"
        lblSearchRadius.Size = New Size(110, 20)
        lblSearchRadius.TabIndex = 6
        lblSearchRadius.Text = "Search Radius:"
        lblSearchRadius.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' nudSearchRadius
        ' 
        nudSearchRadius.DecimalPlaces = 2
        nudSearchRadius.Increment = New Decimal(New Integer() {5, 0, 0, 65536})
        nudSearchRadius.Location = New Point(130, 282)
        nudSearchRadius.Maximum = New Decimal(New Integer() {9999, 0, 0, 0})
        nudSearchRadius.Name = "nudSearchRadius"
        nudSearchRadius.Size = New Size(80, 23)
        nudSearchRadius.TabIndex = 7
        nudSearchRadius.TextAlign = HorizontalAlignment.Right
        nudSearchRadius.Value = New Decimal(New Integer() {10, 0, 0, 0})
        ' 
        ' lblSearchRadiusHint
        ' 
        lblSearchRadiusHint.ForeColor = SystemColors.GrayText
        lblSearchRadiusHint.Location = New Point(218, 284)
        lblSearchRadiusHint.Name = "lblSearchRadiusHint"
        lblSearchRadiusHint.Size = New Size(150, 20)
        lblSearchRadiusHint.TabIndex = 8
        lblSearchRadiusHint.Text = "0 = unlimited"
        ' 
        ' grpAxis
        ' 
        grpAxis.Controls.Add(chkAxisX)
        grpAxis.Controls.Add(chkAxisY)
        grpAxis.Controls.Add(chkAxisZ)
        grpAxis.Location = New Point(14, 316)
        grpAxis.Name = "grpAxis"
        grpAxis.Size = New Size(356, 40)
        grpAxis.TabIndex = 9
        grpAxis.TabStop = False
        grpAxis.Text = "Axis"
        ' 
        ' chkAxisX
        ' 
        chkAxisX.AutoSize = True
        chkAxisX.Checked = True
        chkAxisX.CheckState = CheckState.Checked
        chkAxisX.Location = New Point(10, 16)
        chkAxisX.Name = "chkAxisX"
        chkAxisX.Size = New Size(33, 19)
        chkAxisX.TabIndex = 0
        chkAxisX.Text = "X"
        chkAxisX.UseVisualStyleBackColor = True
        ' 
        ' chkAxisY
        ' 
        chkAxisY.AutoSize = True
        chkAxisY.Checked = True
        chkAxisY.CheckState = CheckState.Checked
        chkAxisY.Location = New Point(60, 16)
        chkAxisY.Name = "chkAxisY"
        chkAxisY.Size = New Size(33, 19)
        chkAxisY.TabIndex = 1
        chkAxisY.Text = "Y"
        chkAxisY.UseVisualStyleBackColor = True
        ' 
        ' chkAxisZ
        ' 
        chkAxisZ.AutoSize = True
        chkAxisZ.Checked = True
        chkAxisZ.CheckState = CheckState.Checked
        chkAxisZ.Location = New Point(110, 16)
        chkAxisZ.Name = "chkAxisZ"
        chkAxisZ.Size = New Size(33, 19)
        chkAxisZ.TabIndex = 2
        chkAxisZ.Text = "Z"
        chkAxisZ.UseVisualStyleBackColor = True
        ' 
        ' chkOverwrite
        ' 
        chkOverwrite.Checked = True
        chkOverwrite.CheckState = CheckState.Checked
        chkOverwrite.Location = New Point(14, 368)
        chkOverwrite.Name = "chkOverwrite"
        chkOverwrite.Size = New Size(356, 22)
        chkOverwrite.TabIndex = 10
        chkOverwrite.Text = "Overwrite existing morph data"
        chkOverwrite.UseVisualStyleBackColor = True
        ' 
        ' pnlSeparator
        ' 
        pnlSeparator.BackColor = SystemColors.ControlDark
        pnlSeparator.Location = New Point(14, 400)
        pnlSeparator.Name = "pnlSeparator"
        pnlSeparator.Size = New Size(356, 1)
        pnlSeparator.TabIndex = 11
        ' 
        ' progressBar1
        ' 
        progressBar1.Location = New Point(14, 410)
        progressBar1.Name = "progressBar1"
        progressBar1.Size = New Size(356, 18)
        progressBar1.TabIndex = 12
        ' 
        ' lblStatus
        ' 
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Location = New Point(14, 436)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(356, 20)
        lblStatus.TabIndex = 13
        lblStatus.Text = "Ready."
        ' 
        ' btnAction
        ' 
        btnAction.Location = New Point(140, 464)
        btnAction.Name = "btnAction"
        btnAction.Size = New Size(100, 26)
        btnAction.TabIndex = 14
        btnAction.Text = "Start"
        btnAction.UseVisualStyleBackColor = True
        ' 
        ' btnClose
        ' 
        btnClose.DialogResult = DialogResult.Cancel
        btnClose.Location = New Point(268, 464)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(100, 26)
        btnClose.TabIndex = 15
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = True
        ' 
        ' btnApply
        ' 
        btnApply.Enabled = False
        btnApply.Location = New Point(14, 464)
        btnApply.Name = "btnApply"
        btnApply.Size = New Size(100, 26)
        btnApply.TabIndex = 16
        btnApply.Text = "Apply"
        btnApply.UseVisualStyleBackColor = True
        ' 
        ' Conform_Form
        ' 
        CancelButton = btnClose
        ClientSize = New Size(398, 502)
        Controls.Add(btnApply)
        Controls.Add(lblSourceShape)
        Controls.Add(cboSource)
        Controls.Add(lblSliders)
        Controls.Add(clbSliders)
        Controls.Add(btnSelectAll)
        Controls.Add(btnSelectNone)
        Controls.Add(lblSearchRadius)
        Controls.Add(nudSearchRadius)
        Controls.Add(lblSearchRadiusHint)
        Controls.Add(grpAxis)
        Controls.Add(chkOverwrite)
        Controls.Add(pnlSeparator)
        Controls.Add(progressBar1)
        Controls.Add(lblStatus)
        Controls.Add(btnAction)
        Controls.Add(btnClose)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "Conform_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Conform Sliders to Shape"
        CType(nudSearchRadius, ComponentModel.ISupportInitialize).EndInit()
        grpAxis.ResumeLayout(False)
        grpAxis.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents lblSourceShape As Label
    Friend WithEvents cboSource As ComboBox
    Friend WithEvents lblSliders As Label
    Friend WithEvents clbSliders As CheckedListBox
    Friend WithEvents btnSelectAll As Button
    Friend WithEvents btnSelectNone As Button
    Friend WithEvents lblSearchRadius As Label
    Friend WithEvents nudSearchRadius As NumericUpDown
    Friend WithEvents lblSearchRadiusHint As Label
    Friend WithEvents grpAxis As GroupBox
    Friend WithEvents chkAxisX As CheckBox
    Friend WithEvents chkAxisY As CheckBox
    Friend WithEvents chkAxisZ As CheckBox
    Friend WithEvents chkOverwrite As CheckBox
    Friend WithEvents pnlSeparator As Panel
    Friend WithEvents progressBar1 As ProgressBar
    Friend WithEvents lblStatus As Label
    Friend WithEvents btnAction As Button
    Friend WithEvents btnClose As Button
    Friend WithEvents btnApply As Button

End Class
