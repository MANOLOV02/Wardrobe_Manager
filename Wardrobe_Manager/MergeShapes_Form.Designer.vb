' Version Uploaded of Wardrobe 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class MergeShapes_Form
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
        lblCurrentShape = New Label()
        lblSelectShapes = New Label()
        clbShapes = New CheckedListBox()
        lblTarget = New Label()
        cboTarget = New ComboBox()
        lblCompatibility = New Label()
        btnMerge = New Button()
        btnCancel = New Button()
        SuspendLayout()
        '
        ' lblCurrentShape
        '
        lblCurrentShape.AutoSize = False
        lblCurrentShape.Location = New Point(16, 16)
        lblCurrentShape.Name = "lblCurrentShape"
        lblCurrentShape.Size = New Size(390, 20)
        lblCurrentShape.TabIndex = 0
        lblCurrentShape.Text = "Current shape:"
        '
        ' lblSelectShapes
        '
        lblSelectShapes.AutoSize = True
        lblSelectShapes.Location = New Point(16, 44)
        lblSelectShapes.Name = "lblSelectShapes"
        lblSelectShapes.TabIndex = 1
        lblSelectShapes.Text = "Include in merge (current shape is always included):"
        '
        ' clbShapes
        '
        clbShapes.CheckOnClick = True
        clbShapes.FormattingEnabled = True
        clbShapes.Location = New Point(16, 64)
        clbShapes.Name = "clbShapes"
        clbShapes.Size = New Size(390, 139)
        clbShapes.TabIndex = 2
        '
        ' lblTarget
        '
        lblTarget.AutoSize = True
        lblTarget.Location = New Point(16, 218)
        lblTarget.Name = "lblTarget"
        lblTarget.TabIndex = 3
        lblTarget.Text = "Target shape (keeps its name && shader):"
        '
        ' cboTarget
        '
        cboTarget.DropDownStyle = ComboBoxStyle.DropDownList
        cboTarget.FormattingEnabled = True
        cboTarget.Location = New Point(16, 238)
        cboTarget.Name = "cboTarget"
        cboTarget.Size = New Size(390, 23)
        cboTarget.TabIndex = 4
        '
        ' lblCompatibility
        '
        lblCompatibility.AutoSize = False
        lblCompatibility.Location = New Point(16, 276)
        lblCompatibility.Name = "lblCompatibility"
        lblCompatibility.Size = New Size(390, 40)
        lblCompatibility.TabIndex = 5
        lblCompatibility.Text = ""
        '
        ' btnMerge
        '
        btnMerge.Location = New Point(16, 328)
        btnMerge.Name = "btnMerge"
        btnMerge.Size = New Size(186, 28)
        btnMerge.TabIndex = 6
        btnMerge.Text = "Merge"
        btnMerge.UseVisualStyleBackColor = True
        '
        ' btnCancel
        '
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Location = New Point(220, 328)
        btnCancel.Name = "btnCancel"
        btnCancel.Size = New Size(186, 28)
        btnCancel.TabIndex = 7
        btnCancel.Text = "Cancel"
        btnCancel.UseVisualStyleBackColor = True
        '
        ' MergeShapes_Form
        '
        AcceptButton = btnMerge
        CancelButton = btnCancel
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        ClientSize = New Size(422, 374)
        Controls.Add(lblCurrentShape)
        Controls.Add(lblSelectShapes)
        Controls.Add(clbShapes)
        Controls.Add(lblTarget)
        Controls.Add(cboTarget)
        Controls.Add(lblCompatibility)
        Controls.Add(btnMerge)
        Controls.Add(btnCancel)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "MergeShapes_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Merge Shapes"
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents lblCurrentShape As Label
    Friend WithEvents lblSelectShapes As Label
    Friend WithEvents clbShapes As CheckedListBox
    Friend WithEvents lblTarget As Label
    Friend WithEvents cboTarget As ComboBox
    Friend WithEvents lblCompatibility As Label
    Friend WithEvents btnMerge As Button
    Friend WithEvents btnCancel As Button
End Class
