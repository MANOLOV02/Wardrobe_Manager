<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class BuildingForm
    Inherits System.Windows.Forms.Form

    'Form reemplaza a Dispose para limpiar la lista de componentes.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Requerido por el Diseñador de Windows Forms
    Private components As System.ComponentModel.IContainer

    'NOTA: el Diseñador de Windows Forms necesita el siguiente procedimiento
    'Se puede modificar usando el Diseñador de Windows Forms.  
    'No lo modifique con el editor de código.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Label1 = New Label()
        ProgressBar1 = New ProgressBar()
        ProgressBar2 = New ProgressBar()
        SuspendLayout()
        ' 
        ' Label1
        ' 
        Label1.Location = New Point(12, 9)
        Label1.Name = "Label1"
        Label1.Size = New Size(557, 21)
        Label1.TabIndex = 0
        Label1.Text = "Building (None)"
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Location = New Point(12, 33)
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(557, 19)
        ProgressBar1.TabIndex = 1
        ' 
        ' ProgressBar2
        ' 
        ProgressBar2.Location = New Point(12, 58)
        ProgressBar2.Name = "ProgressBar2"
        ProgressBar2.Size = New Size(557, 19)
        ProgressBar2.TabIndex = 2
        ' 
        ' BuildingForm
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(581, 84)
        ControlBox = False
        Controls.Add(ProgressBar2)
        Controls.Add(ProgressBar1)
        Controls.Add(Label1)
        FormBorderStyle = FormBorderStyle.FixedToolWindow
        MaximizeBox = False
        MinimizeBox = False
        Name = "BuildingForm"
        ShowIcon = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Building"
        ResumeLayout(False)
    End Sub

    Friend WithEvents Label1 As Label
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents ProgressBar2 As ProgressBar
End Class
