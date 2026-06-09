<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class HkxPoseImport_Form
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
        components = New ComponentModel.Container()
        DictionaryPicker_Control1 = New DictionaryPicker_Control()
        SplitContainer1 = New SplitContainer()
        SplitContainer2 = New SplitContainer()
        PanelPreview = New Panel()
        PanelControls = New Panel()
        NumericFrameMs = New NumericUpDown()
        LabelFrameMs = New Label()
        ButtonPlay = New Button()
        FrameSlider = New TinySliderTextBox()
        LabelFrame = New Label()
        TextBoxPoseName = New TextBox()
        LabelPoseName = New Label()
        ToolTip1 = New ToolTip(components)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        CType(SplitContainer2, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer2.Panel1.SuspendLayout()
        SplitContainer2.Panel2.SuspendLayout()
        SplitContainer2.SuspendLayout()
        PanelControls.SuspendLayout()
        CType(NumericFrameMs, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' DictionaryPicker_Control1
        ' 
        DictionaryPicker_Control1.AllowClone = False
        DictionaryPicker_Control1.Dock = DockStyle.Fill
        DictionaryPicker_Control1.Location = New Point(0, 0)
        DictionaryPicker_Control1.Name = "DictionaryPicker_Control1"
        DictionaryPicker_Control1.Size = New Size(690, 621)
        DictionaryPicker_Control1.TabIndex = 0
        ToolTip1.SetToolTip(DictionaryPicker_Control1, "Browse and select an HKX animation from the file dictionary.")
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
        SplitContainer1.Size = New Size(1264, 621)
        SplitContainer1.SplitterDistance = 690
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
        SplitContainer2.Panel1.Controls.Add(PanelPreview)
        ' 
        ' SplitContainer2.Panel2
        ' 
        SplitContainer2.Panel2.Controls.Add(PanelControls)
        SplitContainer2.Size = New Size(570, 621)
        SplitContainer2.SplitterDistance = 523
        SplitContainer2.TabIndex = 0
        ' 
        ' PanelPreview
        ' 
        PanelPreview.Dock = DockStyle.Fill
        PanelPreview.Location = New Point(0, 0)
        PanelPreview.Name = "PanelPreview"
        PanelPreview.Size = New Size(570, 523)
        PanelPreview.TabIndex = 0
        ' 
        ' PanelControls
        ' 
        PanelControls.Controls.Add(NumericFrameMs)
        PanelControls.Controls.Add(LabelFrameMs)
        PanelControls.Controls.Add(ButtonPlay)
        PanelControls.Controls.Add(FrameSlider)
        PanelControls.Controls.Add(LabelFrame)
        PanelControls.Controls.Add(TextBoxPoseName)
        PanelControls.Controls.Add(LabelPoseName)
        PanelControls.Dock = DockStyle.Fill
        PanelControls.Location = New Point(0, 0)
        PanelControls.Name = "PanelControls"
        PanelControls.Size = New Size(570, 94)
        PanelControls.TabIndex = 0
        ' 
        ' NumericFrameMs
        ' 
        NumericFrameMs.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        NumericFrameMs.Location = New Point(505, 64)
        NumericFrameMs.Maximum = New Decimal(New Integer() {120, 0, 0, 0})
        NumericFrameMs.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        NumericFrameMs.Name = "NumericFrameMs"
        NumericFrameMs.Size = New Size(62, 23)
        NumericFrameMs.TabIndex = 6
        NumericFrameMs.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(NumericFrameMs, "Frames per second. Defaults from the HKX frame rate when available.")
        NumericFrameMs.Value = New Decimal(New Integer() {30, 0, 0, 0})
        ' 
        ' LabelFrameMs
        ' 
        LabelFrameMs.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        LabelFrameMs.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        LabelFrameMs.Location = New Point(416, 62)
        LabelFrameMs.Name = "LabelFrameMs"
        LabelFrameMs.Size = New Size(83, 24)
        LabelFrameMs.TabIndex = 5
        LabelFrameMs.Text = "FPS"
        LabelFrameMs.TextAlign = ContentAlignment.MiddleRight
        ' 
        ' ButtonPlay
        ' 
        ButtonPlay.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonPlay.Location = New Point(98, 63)
        ButtonPlay.Name = "ButtonPlay"
        ButtonPlay.Size = New Size(89, 25)
        ButtonPlay.TabIndex = 4
        ButtonPlay.Text = "Play"
        ToolTip1.SetToolTip(ButtonPlay, "Loop the selected HKX animation in the preview.")
        ButtonPlay.UseVisualStyleBackColor = True
        ' 
        ' FrameSlider
        ' 
        FrameSlider.AccentColor = SystemColors.HotTrack
        FrameSlider.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        FrameSlider.BackColor = SystemColors.Control
        FrameSlider.DisplayFormat = "0"
        FrameSlider.Location = New Point(98, 35)
        FrameSlider.Maximum = 0R
        FrameSlider.MinimumSize = New Size(100, 24)
        FrameSlider.Name = "FrameSlider"
        FrameSlider.ShowTicks = True
        FrameSlider.Size = New Size(469, 24)
        FrameSlider.TabIndex = 3
        FrameSlider.TextBoxTextAlign = HorizontalAlignment.Right
        FrameSlider.ThumbColor = SystemColors.HotTrack
        FrameSlider.ThumbRadius = 4F
        ToolTip1.SetToolTip(FrameSlider, "Select the HKX frame to preview/import.")
        FrameSlider.TrackColor = SystemColors.ControlDark
        ' 
        ' LabelFrame
        ' 
        LabelFrame.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        LabelFrame.Location = New Point(10, 35)
        LabelFrame.Name = "LabelFrame"
        LabelFrame.Size = New Size(82, 24)
        LabelFrame.TabIndex = 2
        LabelFrame.Text = "Frame"
        LabelFrame.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TextBoxPoseName
        ' 
        TextBoxPoseName.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        TextBoxPoseName.Location = New Point(98, 7)
        TextBoxPoseName.Name = "TextBoxPoseName"
        TextBoxPoseName.Size = New Size(469, 23)
        TextBoxPoseName.TabIndex = 1
        ToolTip1.SetToolTip(TextBoxPoseName, "Name of the Wardrobe Manager pose to save.")
        ' 
        ' LabelPoseName
        ' 
        LabelPoseName.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        LabelPoseName.Location = New Point(10, 6)
        LabelPoseName.Name = "LabelPoseName"
        LabelPoseName.Size = New Size(82, 24)
        LabelPoseName.TabIndex = 0
        LabelPoseName.Text = "Pose name"
        LabelPoseName.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' HkxPoseImport_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1264, 621)
        Controls.Add(SplitContainer1)
        MaximizeBox = False
        MinimizeBox = False
        MinimumSize = New Size(900, 560)
        Name = "HkxPoseImport_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Import HKX pose"
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        SplitContainer2.Panel1.ResumeLayout(False)
        SplitContainer2.Panel2.ResumeLayout(False)
        CType(SplitContainer2, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer2.ResumeLayout(False)
        PanelControls.ResumeLayout(False)
        PanelControls.PerformLayout()
        CType(NumericFrameMs, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
    End Sub

    Friend WithEvents DictionaryPicker_Control1 As DictionaryPicker_Control
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents SplitContainer2 As SplitContainer
    Friend WithEvents PanelPreview As Panel
    Friend WithEvents PanelControls As Panel
    Friend WithEvents LabelPoseName As Label
    Friend WithEvents TextBoxPoseName As TextBox
    Friend WithEvents LabelFrame As Label
    Friend WithEvents FrameSlider As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents ButtonPlay As Button
    Friend WithEvents LabelFrameMs As Label
    Friend WithEvents NumericFrameMs As NumericUpDown
    Friend WithEvents ToolTip1 As ToolTip
End Class
