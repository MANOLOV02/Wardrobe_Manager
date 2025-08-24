' Version Uploaded of Wardrobe 2.1.3
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Config_Form
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Config_Form))
        TextBox1 = New TextBox()
        Button1 = New Button()
        Label1 = New Label()
        ImageList1 = New ImageList(components)
        Label2 = New Label()
        TextBox2 = New TextBox()
        Label3 = New Label()
        TextBox3 = New TextBox()
        Button2 = New Button()
        Button3 = New Button()
        ListView1 = New ListView()
        Ba2_Name = New ColumnHeader()
        Label4 = New Label()
        Label5 = New Label()
        Button4 = New Button()
        Label6 = New Label()
        TextBox4 = New TextBox()
        GroupBox1 = New GroupBox()
        NumericUpDownUVEps = New NumericUpDown()
        NormalsRepairNan = New CheckBox()
        NormalsForceOrthogonal = New CheckBox()
        NormalsNormalize = New CheckBox()
        RadioButtoncombined = New RadioButton()
        RadioButtonByangles = New RadioButton()
        RadioButtonByArea = New RadioButton()
        Label9 = New Label()
        Label8 = New Label()
        NumericUpDownPositionEps = New NumericUpDown()
        SingleBoneCheck = New CheckBox()
        RecalculateNormalsCheck = New CheckBox()
        TabControl1 = New TabControl()
        TabPage1 = New TabPage()
        ComboBoxGame = New ComboBox()
        Label7 = New Label()
        TabPage2 = New TabPage()
        GroupBox3 = New GroupBox()
        CheckBoxFreeze = New CheckBox()
        CheckBoxzoomreset = New CheckBox()
        CheckBoxanglereset = New CheckBox()
        Button6 = New Button()
        Button5 = New Button()
        GroupBox2 = New GroupBox()
        NumericUpDownWeldEpsUv = New NumericUpDown()
        RadioButtonWeldboth = New RadioButton()
        RadioButtonWeldpsonly = New RadioButton()
        Label10 = New Label()
        Label11 = New Label()
        NumericUpDownWeldEpspos = New NumericUpDown()
        CheckBoxWelding = New CheckBox()
        TabPage3 = New TabPage()
        GroupBoxweights = New GroupBox()
        RadioButtonAllwaysWeight = New RadioButton()
        RadioButtonNeverWeights = New RadioButton()
        CheckBoxweightignore = New CheckBox()
        CheckBoxBuildInPose = New CheckBox()
        CheckBoxIgnorePrevent = New CheckBox()
        Button8 = New Button()
        Button7 = New Button()
        GroupBoxLooksmenu = New GroupBox()
        CheckBoxLMASkipManoloFixes = New CheckBox()
        CheckBoxLMReseteachBuild = New CheckBox()
        CheckBoxLMAddAditionals = New CheckBox()
        CheckBoxDeletewithProject = New CheckBox()
        CheckBoxDeleteBefore = New CheckBox()
        CheckBoxBuildTri = New CheckBox()
        RadioButtonBSEngine = New RadioButton()
        CheckBoxBuildHH = New CheckBox()
        RadioButtonWMEngine = New RadioButton()
        Label12 = New Label()
        GroupBox1.SuspendLayout()
        CType(NumericUpDownUVEps, ComponentModel.ISupportInitialize).BeginInit()
        CType(NumericUpDownPositionEps, ComponentModel.ISupportInitialize).BeginInit()
        TabControl1.SuspendLayout()
        TabPage1.SuspendLayout()
        TabPage2.SuspendLayout()
        GroupBox3.SuspendLayout()
        GroupBox2.SuspendLayout()
        CType(NumericUpDownWeldEpsUv, ComponentModel.ISupportInitialize).BeginInit()
        CType(NumericUpDownWeldEpspos, ComponentModel.ISupportInitialize).BeginInit()
        TabPage3.SuspendLayout()
        GroupBoxweights.SuspendLayout()
        GroupBoxLooksmenu.SuspendLayout()
        SuspendLayout()
        ' 
        ' TextBox1
        ' 
        TextBox1.Location = New Point(197, 45)
        TextBox1.Name = "TextBox1"
        TextBox1.ReadOnly = True
        TextBox1.Size = New Size(682, 23)
        TextBox1.TabIndex = 0
        ' 
        ' Button1
        ' 
        Button1.Location = New Point(885, 45)
        Button1.Name = "Button1"
        Button1.Size = New Size(53, 23)
        Button1.TabIndex = 1
        Button1.Text = "...."
        Button1.UseVisualStyleBackColor = True
        ' 
        ' Label1
        ' 
        Label1.ImageAlign = ContentAlignment.MiddleRight
        Label1.ImageIndex = 1
        Label1.ImageList = ImageList1
        Label1.Location = New Point(6, 45)
        Label1.Name = "Label1"
        Label1.Size = New Size(193, 23)
        Label1.TabIndex = 2
        Label1.Text = "FO4 / SSE executable path"
        Label1.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' ImageList1
        ' 
        ImageList1.ColorDepth = ColorDepth.Depth32Bit
        ImageList1.ImageStream = CType(resources.GetObject("ImageList1.ImageStream"), ImageListStreamer)
        ImageList1.TransparentColor = Color.Transparent
        ImageList1.Images.SetKeyName(0, "agt_action_success.ico")
        ImageList1.Images.SetKeyName(1, "cancel.ico")
        ImageList1.Images.SetKeyName(2, "thumbnail.ico")
        ImageList1.Images.SetKeyName(3, "attach.ico")
        ImageList1.Images.SetKeyName(4, "agt_update_drivers.ico")
        ' 
        ' Label2
        ' 
        Label2.ImageAlign = ContentAlignment.MiddleRight
        Label2.ImageIndex = 1
        Label2.ImageList = ImageList1
        Label2.Location = New Point(6, 74)
        Label2.Name = "Label2"
        Label2.Size = New Size(193, 23)
        Label2.TabIndex = 4
        Label2.Text = "Bodyslide extecutable path"
        Label2.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TextBox2
        ' 
        TextBox2.Location = New Point(197, 74)
        TextBox2.Name = "TextBox2"
        TextBox2.ReadOnly = True
        TextBox2.Size = New Size(682, 23)
        TextBox2.TabIndex = 3
        ' 
        ' Label3
        ' 
        Label3.ImageAlign = ContentAlignment.MiddleRight
        Label3.ImageIndex = 1
        Label3.ImageList = ImageList1
        Label3.Location = New Point(6, 103)
        Label3.Name = "Label3"
        Label3.Size = New Size(193, 23)
        Label3.TabIndex = 6
        Label3.Text = "Outfit studio extecutable path"
        Label3.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TextBox3
        ' 
        TextBox3.Location = New Point(197, 103)
        TextBox3.Name = "TextBox3"
        TextBox3.ReadOnly = True
        TextBox3.Size = New Size(682, 23)
        TextBox3.TabIndex = 5
        ' 
        ' Button2
        ' 
        Button2.Location = New Point(885, 74)
        Button2.Name = "Button2"
        Button2.Size = New Size(53, 23)
        Button2.TabIndex = 7
        Button2.Text = "...."
        Button2.UseVisualStyleBackColor = True
        ' 
        ' Button3
        ' 
        Button3.Location = New Point(885, 103)
        Button3.Name = "Button3"
        Button3.Size = New Size(53, 23)
        Button3.TabIndex = 8
        Button3.Text = "...."
        Button3.UseVisualStyleBackColor = True
        ' 
        ' ListView1
        ' 
        ListView1.CheckBoxes = True
        ListView1.Columns.AddRange(New ColumnHeader() {Ba2_Name})
        ListView1.FullRowSelect = True
        ListView1.Location = New Point(6, 191)
        ListView1.MultiSelect = False
        ListView1.Name = "ListView1"
        ListView1.Size = New Size(873, 248)
        ListView1.TabIndex = 9
        ListView1.UseCompatibleStateImageBehavior = False
        ListView1.View = View.Details
        ' 
        ' Ba2_Name
        ' 
        Ba2_Name.Text = "Ba2 file name"
        Ba2_Name.Width = 460
        ' 
        ' Label4
        ' 
        Label4.Font = New Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label4.ImageAlign = ContentAlignment.MiddleRight
        Label4.ImageIndex = 0
        Label4.Location = New Point(6, 165)
        Label4.Name = "Label4"
        Label4.Size = New Size(326, 23)
        Label4.TabIndex = 10
        Label4.Text = "Mark as cloneable or not cloneable materials"
        Label4.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Label5
        ' 
        Label5.Font = New Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label5.ForeColor = Color.IndianRed
        Label5.ImageAlign = ContentAlignment.MiddleRight
        Label5.ImageIndex = 0
        Label5.Location = New Point(431, 165)
        Label5.Name = "Label5"
        Label5.Size = New Size(448, 23)
        Label5.TabIndex = 11
        Label5.Text = "Do not select base game or mods you plan to maintain! It will consume space"
        Label5.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Button4
        ' 
        Button4.Location = New Point(885, 132)
        Button4.Name = "Button4"
        Button4.Size = New Size(53, 23)
        Button4.TabIndex = 14
        Button4.Text = "...."
        Button4.UseVisualStyleBackColor = True
        ' 
        ' Label6
        ' 
        Label6.ImageAlign = ContentAlignment.MiddleRight
        Label6.ImageIndex = 1
        Label6.ImageList = ImageList1
        Label6.Location = New Point(6, 132)
        Label6.Name = "Label6"
        Label6.Size = New Size(193, 23)
        Label6.TabIndex = 13
        Label6.Text = "Skeleton path"
        Label6.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TextBox4
        ' 
        TextBox4.Location = New Point(197, 132)
        TextBox4.Name = "TextBox4"
        TextBox4.ReadOnly = True
        TextBox4.Size = New Size(682, 23)
        TextBox4.TabIndex = 12
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(NumericUpDownUVEps)
        GroupBox1.Controls.Add(NormalsRepairNan)
        GroupBox1.Controls.Add(NormalsForceOrthogonal)
        GroupBox1.Controls.Add(NormalsNormalize)
        GroupBox1.Controls.Add(RadioButtoncombined)
        GroupBox1.Controls.Add(RadioButtonByangles)
        GroupBox1.Controls.Add(RadioButtonByArea)
        GroupBox1.Controls.Add(Label9)
        GroupBox1.Controls.Add(Label8)
        GroupBox1.Controls.Add(NumericUpDownPositionEps)
        GroupBox1.Location = New Point(3, 52)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(935, 121)
        GroupBox1.TabIndex = 15
        GroupBox1.TabStop = False
        GroupBox1.Text = "Normals recalculation (smoothing)"
        ' 
        ' NumericUpDownUVEps
        ' 
        NumericUpDownUVEps.DecimalPlaces = 12
        NumericUpDownUVEps.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        NumericUpDownUVEps.Location = New Point(115, 92)
        NumericUpDownUVEps.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        NumericUpDownUVEps.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        NumericUpDownUVEps.Name = "NumericUpDownUVEps"
        NumericUpDownUVEps.Size = New Size(128, 23)
        NumericUpDownUVEps.TabIndex = 25
        NumericUpDownUVEps.TextAlign = HorizontalAlignment.Right
        NumericUpDownUVEps.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' NormalsRepairNan
        ' 
        NormalsRepairNan.AutoSize = True
        NormalsRepairNan.Checked = True
        NormalsRepairNan.CheckState = CheckState.Checked
        NormalsRepairNan.Location = New Point(486, 22)
        NormalsRepairNan.Name = "NormalsRepairNan"
        NormalsRepairNan.Size = New Size(86, 19)
        NormalsRepairNan.TabIndex = 24
        NormalsRepairNan.Text = "Repair NaN"
        NormalsRepairNan.UseVisualStyleBackColor = True
        ' 
        ' NormalsForceOrthogonal
        ' 
        NormalsForceOrthogonal.AutoSize = True
        NormalsForceOrthogonal.Checked = True
        NormalsForceOrthogonal.CheckState = CheckState.Checked
        NormalsForceOrthogonal.Location = New Point(274, 22)
        NormalsForceOrthogonal.Name = "NormalsForceOrthogonal"
        NormalsForceOrthogonal.Size = New Size(171, 19)
        NormalsForceOrthogonal.TabIndex = 23
        NormalsForceOrthogonal.Text = "Force orthogonal bitangent"
        NormalsForceOrthogonal.UseVisualStyleBackColor = True
        ' 
        ' NormalsNormalize
        ' 
        NormalsNormalize.AutoSize = True
        NormalsNormalize.Checked = True
        NormalsNormalize.CheckState = CheckState.Checked
        NormalsNormalize.Enabled = False
        NormalsNormalize.Location = New Point(6, 22)
        NormalsNormalize.Name = "NormalsNormalize"
        NormalsNormalize.Size = New Size(80, 19)
        NormalsNormalize.TabIndex = 22
        NormalsNormalize.Text = "Normalize"
        NormalsNormalize.UseVisualStyleBackColor = True
        ' 
        ' RadioButtoncombined
        ' 
        RadioButtoncombined.AutoSize = True
        RadioButtoncombined.Checked = True
        RadioButtoncombined.Location = New Point(486, 43)
        RadioButtoncombined.Name = "RadioButtoncombined"
        RadioButtoncombined.Size = New Size(164, 19)
        RadioButtoncombined.TabIndex = 21
        RadioButtoncombined.TabStop = True
        RadioButtoncombined.Text = "Weight by area and angles"
        RadioButtoncombined.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonByangles
        ' 
        RadioButtonByangles.AutoSize = True
        RadioButtonByangles.Location = New Point(274, 43)
        RadioButtonByangles.Name = "RadioButtonByangles"
        RadioButtonByangles.Size = New Size(116, 19)
        RadioButtonByangles.TabIndex = 20
        RadioButtonByangles.Text = "Weight by angles"
        RadioButtonByangles.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonByArea
        ' 
        RadioButtonByArea.AutoSize = True
        RadioButtonByArea.Location = New Point(5, 43)
        RadioButtonByArea.Name = "RadioButtonByArea"
        RadioButtonByArea.Size = New Size(104, 19)
        RadioButtonByArea.TabIndex = 19
        RadioButtonByArea.Text = "Weight by area"
        RadioButtonByArea.UseVisualStyleBackColor = True
        ' 
        ' Label9
        ' 
        Label9.ImageAlign = ContentAlignment.MiddleRight
        Label9.Location = New Point(6, 92)
        Label9.Name = "Label9"
        Label9.Size = New Size(107, 23)
        Label9.TabIndex = 18
        Label9.Text = "UV Epsilon"
        Label9.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Label8
        ' 
        Label8.ImageAlign = ContentAlignment.MiddleRight
        Label8.Location = New Point(6, 66)
        Label8.Name = "Label8"
        Label8.Size = New Size(107, 23)
        Label8.TabIndex = 16
        Label8.Text = "Position epsilon"
        Label8.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' NumericUpDownPositionEps
        ' 
        NumericUpDownPositionEps.DecimalPlaces = 12
        NumericUpDownPositionEps.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        NumericUpDownPositionEps.Location = New Point(115, 66)
        NumericUpDownPositionEps.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        NumericUpDownPositionEps.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        NumericUpDownPositionEps.Name = "NumericUpDownPositionEps"
        NumericUpDownPositionEps.Size = New Size(128, 23)
        NumericUpDownPositionEps.TabIndex = 15
        NumericUpDownPositionEps.TextAlign = HorizontalAlignment.Right
        NumericUpDownPositionEps.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' SingleBoneCheck
        ' 
        SingleBoneCheck.AutoSize = True
        SingleBoneCheck.Location = New Point(3, 6)
        SingleBoneCheck.Name = "SingleBoneCheck"
        SingleBoneCheck.Size = New Size(229, 19)
        SingleBoneCheck.TabIndex = 11
        SingleBoneCheck.Text = "Single bone skinning (Disables posing)"
        SingleBoneCheck.UseVisualStyleBackColor = True
        ' 
        ' RecalculateNormalsCheck
        ' 
        RecalculateNormalsCheck.AutoSize = True
        RecalculateNormalsCheck.Checked = True
        RecalculateNormalsCheck.CheckState = CheckState.Checked
        RecalculateNormalsCheck.Location = New Point(3, 27)
        RecalculateNormalsCheck.Name = "RecalculateNormalsCheck"
        RecalculateNormalsCheck.Size = New Size(134, 19)
        RecalculateNormalsCheck.TabIndex = 10
        RecalculateNormalsCheck.Text = "Recalculate Normals"
        RecalculateNormalsCheck.UseVisualStyleBackColor = True
        ' 
        ' TabControl1
        ' 
        TabControl1.Appearance = TabAppearance.Buttons
        TabControl1.Controls.Add(TabPage1)
        TabControl1.Controls.Add(TabPage2)
        TabControl1.Controls.Add(TabPage3)
        TabControl1.Dock = DockStyle.Fill
        TabControl1.ImageList = ImageList1
        TabControl1.Location = New Point(0, 0)
        TabControl1.Name = "TabControl1"
        TabControl1.SelectedIndex = 0
        TabControl1.Size = New Size(952, 480)
        TabControl1.TabIndex = 16
        ' 
        ' TabPage1
        ' 
        TabPage1.BackColor = SystemColors.Control
        TabPage1.Controls.Add(ComboBoxGame)
        TabPage1.Controls.Add(Label7)
        TabPage1.Controls.Add(Label1)
        TabPage1.Controls.Add(Button4)
        TabPage1.Controls.Add(TextBox1)
        TabPage1.Controls.Add(Label6)
        TabPage1.Controls.Add(Button1)
        TabPage1.Controls.Add(TextBox4)
        TabPage1.Controls.Add(TextBox2)
        TabPage1.Controls.Add(Label5)
        TabPage1.Controls.Add(Label2)
        TabPage1.Controls.Add(Label4)
        TabPage1.Controls.Add(TextBox3)
        TabPage1.Controls.Add(ListView1)
        TabPage1.Controls.Add(Label3)
        TabPage1.Controls.Add(Button3)
        TabPage1.Controls.Add(Button2)
        TabPage1.ImageKey = "attach.ico"
        TabPage1.Location = New Point(4, 27)
        TabPage1.Name = "TabPage1"
        TabPage1.Padding = New Padding(3)
        TabPage1.Size = New Size(944, 449)
        TabPage1.TabIndex = 0
        TabPage1.Text = "Files and Clonning"
        ' 
        ' ComboBoxGame
        ' 
        ComboBoxGame.DropDownStyle = ComboBoxStyle.DropDownList
        ComboBoxGame.FormattingEnabled = True
        ComboBoxGame.Items.AddRange(New Object() {"Fallout 4", "Skyrm Special Edition"})
        ComboBoxGame.Location = New Point(197, 16)
        ComboBoxGame.Name = "ComboBoxGame"
        ComboBoxGame.Size = New Size(168, 23)
        ComboBoxGame.TabIndex = 16
        ' 
        ' Label7
        ' 
        Label7.ImageAlign = ContentAlignment.MiddleRight
        Label7.ImageList = ImageList1
        Label7.Location = New Point(6, 15)
        Label7.Name = "Label7"
        Label7.Size = New Size(193, 23)
        Label7.TabIndex = 15
        Label7.Text = "Game"
        Label7.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TabPage2
        ' 
        TabPage2.Controls.Add(GroupBox3)
        TabPage2.Controls.Add(Button6)
        TabPage2.Controls.Add(Button5)
        TabPage2.Controls.Add(GroupBox2)
        TabPage2.Controls.Add(CheckBoxWelding)
        TabPage2.Controls.Add(GroupBox1)
        TabPage2.Controls.Add(RecalculateNormalsCheck)
        TabPage2.Controls.Add(SingleBoneCheck)
        TabPage2.ImageKey = "thumbnail.ico"
        TabPage2.Location = New Point(4, 27)
        TabPage2.Name = "TabPage2"
        TabPage2.Padding = New Padding(3)
        TabPage2.Size = New Size(944, 449)
        TabPage2.TabIndex = 1
        TabPage2.Text = "Rendering"
        TabPage2.UseVisualStyleBackColor = True
        ' 
        ' GroupBox3
        ' 
        GroupBox3.Controls.Add(CheckBoxFreeze)
        GroupBox3.Controls.Add(CheckBoxzoomreset)
        GroupBox3.Controls.Add(CheckBoxanglereset)
        GroupBox3.Location = New Point(3, 312)
        GroupBox3.Name = "GroupBox3"
        GroupBox3.Size = New Size(933, 50)
        GroupBox3.TabIndex = 30
        GroupBox3.TabStop = False
        GroupBox3.Text = "Camera"
        ' 
        ' CheckBoxFreeze
        ' 
        CheckBoxFreeze.AutoSize = True
        CheckBoxFreeze.Location = New Point(486, 22)
        CheckBoxFreeze.Name = "CheckBoxFreeze"
        CheckBoxFreeze.Size = New Size(424, 19)
        CheckBoxFreeze.TabIndex = 33
        CheckBoxFreeze.Text = "Completely freeze camera on nif change (make sure to uncheck it after use)"
        CheckBoxFreeze.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxzoomreset
        ' 
        CheckBoxzoomreset.AutoSize = True
        CheckBoxzoomreset.Checked = True
        CheckBoxzoomreset.CheckState = CheckState.Checked
        CheckBoxzoomreset.Location = New Point(274, 22)
        CheckBoxzoomreset.Name = "CheckBoxzoomreset"
        CheckBoxzoomreset.Size = New Size(145, 19)
        CheckBoxzoomreset.TabIndex = 32
        CheckBoxzoomreset.Text = "Reset to optimal zoom"
        CheckBoxzoomreset.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxanglereset
        ' 
        CheckBoxanglereset.AutoSize = True
        CheckBoxanglereset.Checked = True
        CheckBoxanglereset.CheckState = CheckState.Checked
        CheckBoxanglereset.Location = New Point(6, 22)
        CheckBoxanglereset.Name = "CheckBoxanglereset"
        CheckBoxanglereset.Size = New Size(99, 19)
        CheckBoxanglereset.TabIndex = 31
        CheckBoxanglereset.Text = "Reset rotation"
        CheckBoxanglereset.UseVisualStyleBackColor = True
        ' 
        ' Button6
        ' 
        Button6.ImageKey = "thumbnail.ico"
        Button6.ImageList = ImageList1
        Button6.Location = New Point(3, 408)
        Button6.Name = "Button6"
        Button6.Size = New Size(240, 34)
        Button6.TabIndex = 29
        Button6.Text = "Apply to rendered project"
        Button6.TextImageRelation = TextImageRelation.ImageBeforeText
        Button6.UseVisualStyleBackColor = True
        ' 
        ' Button5
        ' 
        Button5.ImageKey = "agt_action_success.ico"
        Button5.ImageList = ImageList1
        Button5.Location = New Point(3, 368)
        Button5.Name = "Button5"
        Button5.Size = New Size(240, 34)
        Button5.TabIndex = 28
        Button5.Text = "Reset all changes to defaults"
        Button5.TextImageRelation = TextImageRelation.ImageBeforeText
        Button5.UseVisualStyleBackColor = True
        ' 
        ' GroupBox2
        ' 
        GroupBox2.Controls.Add(NumericUpDownWeldEpsUv)
        GroupBox2.Controls.Add(RadioButtonWeldboth)
        GroupBox2.Controls.Add(RadioButtonWeldpsonly)
        GroupBox2.Controls.Add(Label10)
        GroupBox2.Controls.Add(Label11)
        GroupBox2.Controls.Add(NumericUpDownWeldEpspos)
        GroupBox2.Location = New Point(3, 204)
        GroupBox2.Name = "GroupBox2"
        GroupBox2.Size = New Size(933, 102)
        GroupBox2.TabIndex = 27
        GroupBox2.TabStop = False
        GroupBox2.Text = "Welding"
        ' 
        ' NumericUpDownWeldEpsUv
        ' 
        NumericUpDownWeldEpsUv.DecimalPlaces = 12
        NumericUpDownWeldEpsUv.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        NumericUpDownWeldEpsUv.Location = New Point(115, 72)
        NumericUpDownWeldEpsUv.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        NumericUpDownWeldEpsUv.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        NumericUpDownWeldEpsUv.Name = "NumericUpDownWeldEpsUv"
        NumericUpDownWeldEpsUv.Size = New Size(128, 23)
        NumericUpDownWeldEpsUv.TabIndex = 25
        NumericUpDownWeldEpsUv.TextAlign = HorizontalAlignment.Right
        NumericUpDownWeldEpsUv.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' RadioButtonWeldboth
        ' 
        RadioButtonWeldboth.AutoSize = True
        RadioButtonWeldboth.Checked = True
        RadioButtonWeldboth.Location = New Point(274, 22)
        RadioButtonWeldboth.Name = "RadioButtonWeldboth"
        RadioButtonWeldboth.Size = New Size(130, 19)
        RadioButtonWeldboth.TabIndex = 20
        RadioButtonWeldboth.TabStop = True
        RadioButtonWeldboth.Text = "By position and UVs"
        RadioButtonWeldboth.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonWeldpsonly
        ' 
        RadioButtonWeldpsonly.AutoSize = True
        RadioButtonWeldpsonly.Location = New Point(5, 22)
        RadioButtonWeldpsonly.Name = "RadioButtonWeldpsonly"
        RadioButtonWeldpsonly.Size = New Size(110, 19)
        RadioButtonWeldpsonly.TabIndex = 19
        RadioButtonWeldpsonly.Text = "By position only"
        RadioButtonWeldpsonly.UseVisualStyleBackColor = True
        ' 
        ' Label10
        ' 
        Label10.ImageAlign = ContentAlignment.MiddleRight
        Label10.Location = New Point(6, 72)
        Label10.Name = "Label10"
        Label10.Size = New Size(107, 23)
        Label10.TabIndex = 18
        Label10.Text = "UV Epsilon"
        Label10.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Label11
        ' 
        Label11.ImageAlign = ContentAlignment.MiddleRight
        Label11.Location = New Point(6, 47)
        Label11.Name = "Label11"
        Label11.Size = New Size(107, 23)
        Label11.TabIndex = 16
        Label11.Text = "Mesh Scale factor"
        Label11.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' NumericUpDownWeldEpspos
        ' 
        NumericUpDownWeldEpspos.DecimalPlaces = 12
        NumericUpDownWeldEpspos.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        NumericUpDownWeldEpspos.Location = New Point(115, 47)
        NumericUpDownWeldEpspos.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        NumericUpDownWeldEpspos.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        NumericUpDownWeldEpspos.Name = "NumericUpDownWeldEpspos"
        NumericUpDownWeldEpspos.Size = New Size(128, 23)
        NumericUpDownWeldEpspos.TabIndex = 15
        NumericUpDownWeldEpspos.TextAlign = HorizontalAlignment.Right
        NumericUpDownWeldEpspos.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' CheckBoxWelding
        ' 
        CheckBoxWelding.AutoSize = True
        CheckBoxWelding.Checked = True
        CheckBoxWelding.CheckState = CheckState.Checked
        CheckBoxWelding.Location = New Point(3, 179)
        CheckBoxWelding.Name = "CheckBoxWelding"
        CheckBoxWelding.Size = New Size(160, 19)
        CheckBoxWelding.TabIndex = 26
        CheckBoxWelding.Text = "Weld Vertices for normals"
        CheckBoxWelding.UseVisualStyleBackColor = True
        ' 
        ' TabPage3
        ' 
        TabPage3.Controls.Add(GroupBoxweights)
        TabPage3.Controls.Add(CheckBoxBuildInPose)
        TabPage3.Controls.Add(CheckBoxIgnorePrevent)
        TabPage3.Controls.Add(Button8)
        TabPage3.Controls.Add(Button7)
        TabPage3.Controls.Add(GroupBoxLooksmenu)
        TabPage3.Controls.Add(CheckBoxDeletewithProject)
        TabPage3.Controls.Add(CheckBoxDeleteBefore)
        TabPage3.Controls.Add(CheckBoxBuildTri)
        TabPage3.Controls.Add(RadioButtonBSEngine)
        TabPage3.Controls.Add(CheckBoxBuildHH)
        TabPage3.Controls.Add(RadioButtonWMEngine)
        TabPage3.Controls.Add(Label12)
        TabPage3.ImageKey = "agt_update_drivers.ico"
        TabPage3.Location = New Point(4, 27)
        TabPage3.Name = "TabPage3"
        TabPage3.Size = New Size(944, 449)
        TabPage3.TabIndex = 2
        TabPage3.Text = "Building"
        ' 
        ' GroupBoxweights
        ' 
        GroupBoxweights.Controls.Add(RadioButtonAllwaysWeight)
        GroupBoxweights.Controls.Add(RadioButtonNeverWeights)
        GroupBoxweights.Controls.Add(CheckBoxweightignore)
        GroupBoxweights.Location = New Point(3, 224)
        GroupBoxweights.Name = "GroupBoxweights"
        GroupBoxweights.Size = New Size(933, 55)
        GroupBoxweights.TabIndex = 32
        GroupBoxweights.TabStop = False
        GroupBoxweights.Text = "Weights generation"
        ' 
        ' RadioButtonAllwaysWeight
        ' 
        RadioButtonAllwaysWeight.AutoSize = True
        RadioButtonAllwaysWeight.Checked = True
        RadioButtonAllwaysWeight.Location = New Point(276, 21)
        RadioButtonAllwaysWeight.Name = "RadioButtonAllwaysWeight"
        RadioButtonAllwaysWeight.Size = New Size(139, 19)
        RadioButtonAllwaysWeight.TabIndex = 30
        RadioButtonAllwaysWeight.TabStop = True
        RadioButtonAllwaysWeight.Text = "Allways build weights"
        RadioButtonAllwaysWeight.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonNeverWeights
        ' 
        RadioButtonNeverWeights.AutoSize = True
        RadioButtonNeverWeights.Location = New Point(437, 21)
        RadioButtonNeverWeights.Name = "RadioButtonNeverWeights"
        RadioButtonNeverWeights.Size = New Size(130, 19)
        RadioButtonNeverWeights.TabIndex = 31
        RadioButtonNeverWeights.Text = "Never build weights"
        RadioButtonNeverWeights.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxweightignore
        ' 
        CheckBoxweightignore.AutoSize = True
        CheckBoxweightignore.Location = New Point(9, 22)
        CheckBoxweightignore.Name = "CheckBoxweightignore"
        CheckBoxweightignore.Size = New Size(151, 19)
        CheckBoxweightignore.TabIndex = 29
        CheckBoxweightignore.Text = "Ignore  project property"
        CheckBoxweightignore.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxBuildInPose
        ' 
        CheckBoxBuildInPose.AutoSize = True
        CheckBoxBuildInPose.Location = New Point(11, 138)
        CheckBoxBuildInPose.Name = "CheckBoxBuildInPose"
        CheckBoxBuildInPose.Size = New Size(287, 19)
        CheckBoxBuildInPose.TabIndex = 32
        CheckBoxBuildInPose.Text = "Build in pose (not recommended for player/npcs)"
        CheckBoxBuildInPose.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxIgnorePrevent
        ' 
        CheckBoxIgnorePrevent.AutoSize = True
        CheckBoxIgnorePrevent.Location = New Point(167, 113)
        CheckBoxIgnorePrevent.Name = "CheckBoxIgnorePrevent"
        CheckBoxIgnorePrevent.Size = New Size(319, 19)
        CheckBoxIgnorePrevent.TabIndex = 31
        CheckBoxIgnorePrevent.Text = "Ignore prevent morph file attribute (not recommended)"
        CheckBoxIgnorePrevent.UseVisualStyleBackColor = True
        ' 
        ' Button8
        ' 
        Button8.ImageKey = "cancel.ico"
        Button8.ImageList = ImageList1
        Button8.Location = New Point(6, 334)
        Button8.Name = "Button8"
        Button8.Size = New Size(240, 34)
        Button8.TabIndex = 30
        Button8.Text = "Delete saved additional sliders"
        Button8.TextImageRelation = TextImageRelation.ImageBeforeText
        Button8.UseVisualStyleBackColor = True
        ' 
        ' Button7
        ' 
        Button7.ImageKey = "agt_action_success.ico"
        Button7.ImageList = ImageList1
        Button7.Location = New Point(6, 294)
        Button7.Name = "Button7"
        Button7.Size = New Size(240, 34)
        Button7.TabIndex = 29
        Button7.Text = "Reset all changes to defaults"
        Button7.TextImageRelation = TextImageRelation.ImageBeforeText
        Button7.UseVisualStyleBackColor = True
        ' 
        ' GroupBoxLooksmenu
        ' 
        GroupBoxLooksmenu.Controls.Add(CheckBoxLMASkipManoloFixes)
        GroupBoxLooksmenu.Controls.Add(CheckBoxLMReseteachBuild)
        GroupBoxLooksmenu.Controls.Add(CheckBoxLMAddAditionals)
        GroupBoxLooksmenu.Location = New Point(3, 163)
        GroupBoxLooksmenu.Name = "GroupBoxLooksmenu"
        GroupBoxLooksmenu.Size = New Size(933, 55)
        GroupBoxLooksmenu.TabIndex = 28
        GroupBoxLooksmenu.TabStop = False
        GroupBoxLooksmenu.Text = "Looks menu integration (Needs .esp instaled)"
        ' 
        ' CheckBoxLMASkipManoloFixes
        ' 
        CheckBoxLMASkipManoloFixes.AutoSize = True
        CheckBoxLMASkipManoloFixes.Checked = True
        CheckBoxLMASkipManoloFixes.CheckState = CheckState.Checked
        CheckBoxLMASkipManoloFixes.Location = New Point(177, 22)
        CheckBoxLMASkipManoloFixes.Name = "CheckBoxLMASkipManoloFixes"
        CheckBoxLMASkipManoloFixes.Size = New Size(216, 19)
        CheckBoxLMASkipManoloFixes.TabIndex = 31
        CheckBoxLMASkipManoloFixes.Text = "Skip Wardrobe manager ""Fix"" sliders"
        CheckBoxLMASkipManoloFixes.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxLMReseteachBuild
        ' 
        CheckBoxLMReseteachBuild.AutoSize = True
        CheckBoxLMReseteachBuild.Checked = True
        CheckBoxLMReseteachBuild.CheckState = CheckState.Checked
        CheckBoxLMReseteachBuild.Location = New Point(437, 22)
        CheckBoxLMReseteachBuild.Name = "CheckBoxLMReseteachBuild"
        CheckBoxLMReseteachBuild.Size = New Size(221, 19)
        CheckBoxLMReseteachBuild.TabIndex = 30
        CheckBoxLMReseteachBuild.Text = "Reset additional sliders on each build"
        CheckBoxLMReseteachBuild.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxLMAddAditionals
        ' 
        CheckBoxLMAddAditionals.AutoSize = True
        CheckBoxLMAddAditionals.Checked = True
        CheckBoxLMAddAditionals.CheckState = CheckState.Checked
        CheckBoxLMAddAditionals.Location = New Point(9, 22)
        CheckBoxLMAddAditionals.Name = "CheckBoxLMAddAditionals"
        CheckBoxLMAddAditionals.Size = New Size(140, 19)
        CheckBoxLMAddAditionals.TabIndex = 29
        CheckBoxLMAddAditionals.Text = "Add additional sliders"
        CheckBoxLMAddAditionals.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxDeletewithProject
        ' 
        CheckBoxDeletewithProject.AutoSize = True
        CheckBoxDeletewithProject.Checked = True
        CheckBoxDeletewithProject.CheckState = CheckState.Checked
        CheckBoxDeletewithProject.Location = New Point(11, 88)
        CheckBoxDeletewithProject.Name = "CheckBoxDeletewithProject"
        CheckBoxDeletewithProject.Size = New Size(235, 19)
        CheckBoxDeletewithProject.TabIndex = 26
        CheckBoxDeletewithProject.Text = "Delete built files when project is deleted"
        CheckBoxDeletewithProject.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxDeleteBefore
        ' 
        CheckBoxDeleteBefore.AutoSize = True
        CheckBoxDeleteBefore.Checked = True
        CheckBoxDeleteBefore.CheckState = CheckState.Checked
        CheckBoxDeleteBefore.Location = New Point(11, 63)
        CheckBoxDeleteBefore.Name = "CheckBoxDeleteBefore"
        CheckBoxDeleteBefore.Size = New Size(295, 19)
        CheckBoxDeleteBefore.TabIndex = 25
        CheckBoxDeleteBefore.Text = "Delete files  if exist before building (recommended)"
        CheckBoxDeleteBefore.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxBuildTri
        ' 
        CheckBoxBuildTri.AutoSize = True
        CheckBoxBuildTri.Checked = True
        CheckBoxBuildTri.CheckState = CheckState.Checked
        CheckBoxBuildTri.Location = New Point(11, 113)
        CheckBoxBuildTri.Name = "CheckBoxBuildTri"
        CheckBoxBuildTri.Size = New Size(127, 19)
        CheckBoxBuildTri.TabIndex = 24
        CheckBoxBuildTri.Text = "Build Tri Morph file"
        CheckBoxBuildTri.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonBSEngine
        ' 
        RadioButtonBSEngine.AutoSize = True
        RadioButtonBSEngine.Location = New Point(133, 13)
        RadioButtonBSEngine.Name = "RadioButtonBSEngine"
        RadioButtonBSEngine.Size = New Size(116, 19)
        RadioButtonBSEngine.TabIndex = 19
        RadioButtonBSEngine.Text = "BodySlide engine"
        RadioButtonBSEngine.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxBuildHH
        ' 
        CheckBoxBuildHH.AutoSize = True
        CheckBoxBuildHH.Checked = True
        CheckBoxBuildHH.CheckState = CheckState.Checked
        CheckBoxBuildHH.Location = New Point(11, 38)
        CheckBoxBuildHH.Name = "CheckBoxBuildHH"
        CheckBoxBuildHH.Size = New Size(138, 19)
        CheckBoxBuildHH.TabIndex = 23
        CheckBoxBuildHH.Text = "Build High Heels files"
        CheckBoxBuildHH.UseVisualStyleBackColor = True
        ' 
        ' RadioButtonWMEngine
        ' 
        RadioButtonWMEngine.AutoSize = True
        RadioButtonWMEngine.Checked = True
        RadioButtonWMEngine.Location = New Point(279, 13)
        RadioButtonWMEngine.Name = "RadioButtonWMEngine"
        RadioButtonWMEngine.Size = New Size(169, 19)
        RadioButtonWMEngine.TabIndex = 20
        RadioButtonWMEngine.TabStop = True
        RadioButtonWMEngine.Text = "Wardrobe Manager Engine "
        RadioButtonWMEngine.UseVisualStyleBackColor = True
        ' 
        ' Label12
        ' 
        Label12.Font = New Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label12.ImageAlign = ContentAlignment.MiddleRight
        Label12.Location = New Point(8, 11)
        Label12.Name = "Label12"
        Label12.Size = New Size(107, 23)
        Label12.TabIndex = 16
        Label12.Text = "Build engine:"
        Label12.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Config_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(952, 480)
        Controls.Add(TabControl1)
        FormBorderStyle = FormBorderStyle.FixedToolWindow
        MaximizeBox = False
        MinimizeBox = False
        Name = "Config_Form"
        StartPosition = FormStartPosition.CenterParent
        Text = "Configuration"
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        CType(NumericUpDownUVEps, ComponentModel.ISupportInitialize).EndInit()
        CType(NumericUpDownPositionEps, ComponentModel.ISupportInitialize).EndInit()
        TabControl1.ResumeLayout(False)
        TabPage1.ResumeLayout(False)
        TabPage1.PerformLayout()
        TabPage2.ResumeLayout(False)
        TabPage2.PerformLayout()
        GroupBox3.ResumeLayout(False)
        GroupBox3.PerformLayout()
        GroupBox2.ResumeLayout(False)
        GroupBox2.PerformLayout()
        CType(NumericUpDownWeldEpsUv, ComponentModel.ISupportInitialize).EndInit()
        CType(NumericUpDownWeldEpspos, ComponentModel.ISupportInitialize).EndInit()
        TabPage3.ResumeLayout(False)
        TabPage3.PerformLayout()
        GroupBoxweights.ResumeLayout(False)
        GroupBoxweights.PerformLayout()
        GroupBoxLooksmenu.ResumeLayout(False)
        GroupBoxLooksmenu.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents TextBox1 As TextBox
    Friend WithEvents Button1 As Button
    Friend WithEvents Label1 As Label
    Friend WithEvents ImageList1 As ImageList
    Friend WithEvents Label2 As Label
    Friend WithEvents TextBox2 As TextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents TextBox3 As TextBox
    Friend WithEvents Button2 As Button
    Friend WithEvents Button3 As Button
    Friend WithEvents ListView1 As ListView
    Friend WithEvents Ba2_Name As ColumnHeader
    Friend WithEvents Label4 As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents Button4 As Button
    Friend WithEvents Label6 As Label
    Friend WithEvents TextBox4 As TextBox
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents RecalculateNormalsCheck As CheckBox
    Friend WithEvents Label8 As Label
    Friend WithEvents NumericUpDownPositionEps As NumericUpDown
    Friend WithEvents SingleBoneCheck As CheckBox
    Friend WithEvents Label9 As Label
    Friend WithEvents TabControl1 As TabControl
    Friend WithEvents TabPage1 As TabPage
    Friend WithEvents TabPage2 As TabPage
    Friend WithEvents TabPage3 As TabPage
    Friend WithEvents RadioButtonByArea As RadioButton
    Friend WithEvents RadioButtoncombined As RadioButton
    Friend WithEvents RadioButtonByangles As RadioButton
    Friend WithEvents NormalsNormalize As CheckBox
    Friend WithEvents NormalsRepairNan As CheckBox
    Friend WithEvents NormalsForceOrthogonal As CheckBox
    Friend WithEvents NumericUpDownUVEps As NumericUpDown
    Friend WithEvents CheckBoxWelding As CheckBox
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents NumericUpDownWeldEpsUv As NumericUpDown
    Friend WithEvents RadioButtonWeldboth As RadioButton
    Friend WithEvents RadioButtonWeldpsonly As RadioButton
    Friend WithEvents Label10 As Label
    Friend WithEvents Label11 As Label
    Friend WithEvents NumericUpDownWeldEpspos As NumericUpDown
    Friend WithEvents Button5 As Button
    Friend WithEvents Button6 As Button
    Friend WithEvents GroupBox3 As GroupBox
    Friend WithEvents CheckBoxzoomreset As CheckBox
    Friend WithEvents CheckBoxanglereset As CheckBox
    Friend WithEvents CheckBoxDeletewithProject As CheckBox
    Friend WithEvents CheckBoxDeleteBefore As CheckBox
    Friend WithEvents CheckBoxBuildTri As CheckBox
    Friend WithEvents RadioButtonBSEngine As RadioButton
    Friend WithEvents CheckBoxBuildHH As CheckBox
    Friend WithEvents RadioButtonWMEngine As RadioButton
    Friend WithEvents Label12 As Label
    Friend WithEvents Button8 As Button
    Friend WithEvents Button7 As Button
    Friend WithEvents GroupBoxLooksmenu As GroupBox
    Friend WithEvents CheckBoxLMASkipManoloFixes As CheckBox
    Friend WithEvents CheckBoxLMReseteachBuild As CheckBox
    Friend WithEvents CheckBoxLMAddAditionals As CheckBox
    Friend WithEvents CheckBoxIgnorePrevent As CheckBox
    Friend WithEvents CheckBoxBuildInPose As CheckBox
    Friend WithEvents CheckBoxFreeze As CheckBox
    Friend WithEvents ComboBoxGame As ComboBox
    Friend WithEvents Label7 As Label
    Friend WithEvents GroupBoxweights As GroupBox
    Friend WithEvents RadioButtonAllwaysWeight As RadioButton
    Friend WithEvents RadioButtonNeverWeights As RadioButton
    Friend WithEvents CheckBoxweightignore As CheckBox
End Class
