' Version Uploaded of Wardrobe 3.1.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Create_from_Nif_Form
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
        components = New ComponentModel.Container()
        DictionaryPicker_Control1 = New DictionaryPicker_Control()
        SplitContainer1 = New SplitContainer()
        SplitContainer2 = New SplitContainer()
        Panel1 = New Panel()
        TextBox1 = New TextBox()
        CheckBox1 = New CheckBox()
        chkDirSkeleton = New CheckBox()
        ToolTip1 = New ToolTip(components)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        CType(SplitContainer2, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer2.Panel1.SuspendLayout()
        SplitContainer2.Panel2.SuspendLayout()
        SplitContainer2.SuspendLayout()
        SuspendLayout()
        ' 
        ' DictionaryPicker_Control1
        ' 
        DictionaryPicker_Control1.AllowClone = True
        DictionaryPicker_Control1.Dock = DockStyle.Fill
        DictionaryPicker_Control1.Location = New Point(0, 0)
        DictionaryPicker_Control1.Name = "DictionaryPicker_Control1"
        DictionaryPicker_Control1.Size = New Size(716, 582)
        DictionaryPicker_Control1.TabIndex = 0
        ToolTip1.SetToolTip(DictionaryPicker_Control1, "Browse and select a NIF file from the file dictionary.")
        ' 
        ' SplitContainer1
        ' 
        SplitContainer1.Dock = DockStyle.Fill
        SplitContainer1.Location = New Point(0, 0)
        SplitContainer1.Name = "SplitContainer1"
        ' 
        ' SplitContainer1.Panel1
        ' 
        SplitContainer1.Panel1.Controls.Add(DictionaryPicker_Control1)
        ' 
        ' SplitContainer1.Panel2
        ' 
        SplitContainer1.Panel2.Controls.Add(SplitContainer2)
        SplitContainer1.Size = New Size(1236, 582)
        SplitContainer1.SplitterDistance = 716
        SplitContainer1.TabIndex = 1
        ' 
        ' SplitContainer2
        ' 
        SplitContainer2.Dock = DockStyle.Fill
        SplitContainer2.FixedPanel = FixedPanel.Panel2
        SplitContainer2.IsSplitterFixed = True
        SplitContainer2.Location = New Point(0, 0)
        SplitContainer2.Name = "SplitContainer2"
        SplitContainer2.Orientation = Orientation.Horizontal
        ' 
        ' SplitContainer2.Panel1
        ' 
        SplitContainer2.Panel1.Controls.Add(Panel1)
        ' 
        ' SplitContainer2.Panel2
        ' 
        SplitContainer2.Panel2.Controls.Add(TextBox1)
        SplitContainer2.Panel2.Controls.Add(CheckBox1)
        SplitContainer2.Panel2.Controls.Add(chkDirSkeleton)
        SplitContainer2.Size = New Size(516, 582)
        SplitContainer2.SplitterDistance = 533
        SplitContainer2.TabIndex = 0
        ' 
        ' Panel1
        ' 
        Panel1.Dock = DockStyle.Fill
        Panel1.Location = New Point(0, 0)
        Panel1.Name = "Panel1"
        Panel1.Size = New Size(516, 533)
        Panel1.TabIndex = 0
        ' 
        ' TextBox1
        ' 
        TextBox1.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        TextBox1.Location = New Point(11, 10)
        TextBox1.Name = "TextBox1"
        TextBox1.Size = New Size(257, 23)
        TextBox1.TabIndex = 1
        ToolTip1.SetToolTip(TextBox1, "Name of the new project to be created from the selected NIF.")
        ' 
        ' CheckBox1
        ' 
        CheckBox1.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        CheckBox1.AutoSize = True
        CheckBox1.Checked = True
        CheckBox1.CheckState = CheckState.Checked
        CheckBox1.Enabled = False
        CheckBox1.Location = New Point(287, 3)
        CheckBox1.Name = "CheckBox1"
        CheckBox1.Size = New Size(217, 19)
        CheckBox1.TabIndex = 0
        CheckBox1.Text = "Create sliders from .tri file if possible"
        ToolTip1.SetToolTip(CheckBox1, "If a matching TRI file exists, import morph sliders from it when creating the project.")
        CheckBox1.UseVisualStyleBackColor = True
        ' 
        ' chkDirSkeleton
        ' 
        chkDirSkeleton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        chkDirSkeleton.AutoSize = True
        chkDirSkeleton.Checked = True
        chkDirSkeleton.CheckState = CheckState.Checked
        chkDirSkeleton.Enabled = False
        chkDirSkeleton.Location = New Point(287, 23)
        chkDirSkeleton.Name = "chkDirSkeleton"
        chkDirSkeleton.Size = New Size(142, 19)
        chkDirSkeleton.TabIndex = 2
        chkDirSkeleton.Text = "Use directory skeleton"
        ToolTip1.SetToolTip(chkDirSkeleton, "Use a skeleton.nif from the same directory as the selected NIF for preview.")
        chkDirSkeleton.UseVisualStyleBackColor = True
        ' 
        ' Create_from_Nif_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1236, 582)
        Controls.Add(SplitContainer1)
        MaximizeBox = False
        MinimizeBox = False
        MinimumSize = New Size(500, 500)
        Name = "Create_from_Nif_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Create project from nif file"
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        SplitContainer2.Panel1.ResumeLayout(False)
        SplitContainer2.Panel2.ResumeLayout(False)
        SplitContainer2.Panel2.PerformLayout()
        CType(SplitContainer2, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer2.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents DictionaryPicker_Control1 As DictionaryPicker_Control
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents SplitContainer2 As SplitContainer
    Friend WithEvents Panel1 As Panel
    Friend WithEvents CheckBox1 As CheckBox
    Friend WithEvents TextBox1 As TextBox
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents chkDirSkeleton As CheckBox
End Class
