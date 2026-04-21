' Version Uploaded of Wardrobe 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Wardrobe_Manager_Form
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
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

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(Wardrobe_Manager_Form))
        ComboboxPacks = New ComboBox()
        Label1 = New Label()
        Label2 = New Label()
        TextBox_SourceName = New TextBox()
        ImageList1 = New ImageList(components)
        ListView2 = New ListView()
        Shapecol = New ColumnHeader()
        ShapeTypeCol = New ColumnHeader()
        Local = New ColumnHeader()
        ColumnHeader5 = New ColumnHeader()
        Datasources = New ColumnHeader()
        Exclude_Reference_Checkbox = New CheckBox()
        Ovewrite_DataFiles = New CheckBox()
        NewPackButton = New Button()
        ListViewTargets = New ListView()
        ColumnHeader3 = New ColumnHeader()
        ColumnHeader7 = New ColumnHeader()
        ColumnHeader4 = New ColumnHeader()
        EditTargetButton = New Button()
        Auto_Move_Check = New CheckBox()
        GroupBox1 = New GroupBox()
        RadioButton3 = New RadioButton()
        Physics_Label = New Label()
        ButtonDataSheetSelected = New Button()
        ButtonPreviewSelected = New Button()
        Panel_Preview_Container = New Panel()
        Label6 = New Label()
        RadioButton2 = New RadioButton()
        RadioButton1 = New RadioButton()
        TableLayoutPanel1 = New TableLayoutPanel()
        RecalculateNormalsCheck = New CheckBox()
        SingleBoneCheck = New CheckBox()
        ButtonOpenConfig = New Button()
        PhysicsCheckbox = New CheckBox()
        CloneMaterialsCheck = New CheckBox()
        OutputDirChangeCheck = New CheckBox()
        TextBox_TargetName = New TextBox()
        Label3 = New Label()
        GroupBox2 = New GroupBox()
        TableLayoutPanel2 = New TableLayoutPanel()
        ButtonBuildFullPack = New Button()
        ButtonEditInternally = New Button()
        ButtonBuildSingles = New Button()
        ButtonDelete = New Button()
        CloneButton = New Button()
        RenameButton = New Button()
        MergeIntoTargetButton = New Button()
        ExtractSingleButton = New Button()
        Label7 = New Label()
        ComboBoxPresets = New ComboBox()
        SplitPrincipal_1 = New SplitContainer()
        Split_Split_y_Menu_Sources = New SplitContainer()
        Panel2 = New Panel()
        CheckBoxFixUncloned = New CheckBox()
        CheckBoxReloadDict = New CheckBox()
        TextBox2 = New TextBox()
        ShowCollectionsCheck = New CheckBox()
        ShowCBBECheck = New CheckBox()
        RefreshButton = New Button()
        Label5 = New Label()
        DeepAnalize_check = New CheckBox()
        CheckShowpacks = New CheckBox()
        Split_Panel_y_Lista_Source = New SplitContainer()
        ListViewSources = New ListView()
        ColumnHeader1 = New ColumnHeader()
        ColumnHeader6 = New ColumnHeader()
        ColumnHeader2 = New ColumnHeader()
        GroupBox3 = New GroupBox()
        TableLayoutPanel3 = New TableLayoutPanel()
        ButtonCreateFromNif = New Button()
        ButtonDeleteSource = New Button()
        MergeInSelectedButton = New Button()
        CopytoPackButton = New Button()
        EditButton = New Button()
        MovetoDiscardedButton = New Button()
        MoveToProcessedButton = New Button()
        MergeButton = New Button()
        ButtonSourceInternalEdit = New Button()
        Split_Principal2 = New SplitContainer()
        Split_Previiew_y_Menu = New SplitContainer()
        Split_Preview = New SplitContainer()
        Panel3 = New Panel()
        ComboBoxSize = New ComboBox()
        TableLayoutPanel4 = New TableLayoutPanel()
        ButtonLightRigSettings = New Button()
        ButtonLeftPanel = New Button()
        ButtonRightPanel = New Button()
        ColorComboBox1 = New ColorComboBox()
        ProgressBar1 = New ProgressBar()
        Label4 = New Label()
        ButtonSkeleton = New Button()
        ComboBoxPoses = New ComboBox()
        GroupBox4 = New GroupBox()
        Split_Split_y_Menu_Target = New SplitContainer()
        Split_Panel_y_Lista_target = New SplitContainer()
        Panel4 = New Panel()
        ToolTip1 = New ToolTip(components)
        GroupBox1.SuspendLayout()
        Panel_Preview_Container.SuspendLayout()
        TableLayoutPanel1.SuspendLayout()
        GroupBox2.SuspendLayout()
        TableLayoutPanel2.SuspendLayout()
        CType(SplitPrincipal_1, ComponentModel.ISupportInitialize).BeginInit()
        SplitPrincipal_1.Panel1.SuspendLayout()
        SplitPrincipal_1.Panel2.SuspendLayout()
        SplitPrincipal_1.SuspendLayout()
        CType(Split_Split_y_Menu_Sources, ComponentModel.ISupportInitialize).BeginInit()
        Split_Split_y_Menu_Sources.Panel1.SuspendLayout()
        Split_Split_y_Menu_Sources.Panel2.SuspendLayout()
        Split_Split_y_Menu_Sources.SuspendLayout()
        Panel2.SuspendLayout()
        CType(Split_Panel_y_Lista_Source, ComponentModel.ISupportInitialize).BeginInit()
        Split_Panel_y_Lista_Source.Panel2.SuspendLayout()
        Split_Panel_y_Lista_Source.SuspendLayout()
        GroupBox3.SuspendLayout()
        TableLayoutPanel3.SuspendLayout()
        CType(Split_Principal2, ComponentModel.ISupportInitialize).BeginInit()
        Split_Principal2.Panel1.SuspendLayout()
        Split_Principal2.Panel2.SuspendLayout()
        Split_Principal2.SuspendLayout()
        CType(Split_Previiew_y_Menu, ComponentModel.ISupportInitialize).BeginInit()
        Split_Previiew_y_Menu.Panel1.SuspendLayout()
        Split_Previiew_y_Menu.Panel2.SuspendLayout()
        Split_Previiew_y_Menu.SuspendLayout()
        CType(Split_Preview, ComponentModel.ISupportInitialize).BeginInit()
        Split_Preview.Panel1.SuspendLayout()
        Split_Preview.Panel2.SuspendLayout()
        Split_Preview.SuspendLayout()
        Panel3.SuspendLayout()
        TableLayoutPanel4.SuspendLayout()
        GroupBox4.SuspendLayout()
        CType(Split_Split_y_Menu_Target, ComponentModel.ISupportInitialize).BeginInit()
        Split_Split_y_Menu_Target.Panel1.SuspendLayout()
        Split_Split_y_Menu_Target.Panel2.SuspendLayout()
        Split_Split_y_Menu_Target.SuspendLayout()
        CType(Split_Panel_y_Lista_target, ComponentModel.ISupportInitialize).BeginInit()
        Split_Panel_y_Lista_target.Panel1.SuspendLayout()
        Split_Panel_y_Lista_target.Panel2.SuspendLayout()
        Split_Panel_y_Lista_target.SuspendLayout()
        Panel4.SuspendLayout()
        SuspendLayout()
        ' 
        ' ComboboxPacks
        ' 
        ComboboxPacks.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        ComboboxPacks.DropDownStyle = ComboBoxStyle.DropDownList
        ComboboxPacks.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        ComboboxPacks.FormattingEnabled = True
        ComboboxPacks.Location = New Point(154, 34)
        ComboboxPacks.Name = "ComboboxPacks"
        ComboboxPacks.Size = New Size(408, 29)
        ComboboxPacks.TabIndex = 1
        ToolTip1.SetToolTip(ComboboxPacks, "Select the current destination pack. All target operations apply to this pack.")
        ' 
        ' Label1
        ' 
        Label1.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label1.Location = New Point(6, 34)
        Label1.Name = "Label1"
        Label1.Size = New Size(132, 29)
        Label1.TabIndex = 2
        Label1.Text = "Selected pack"
        Label1.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' Label2
        ' 
        Label2.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label2.Location = New Point(0, 4)
        Label2.Name = "Label2"
        Label2.Size = New Size(138, 28)
        Label2.TabIndex = 3
        Label2.Text = "Name override"
        Label2.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' TextBox_SourceName
        ' 
        TextBox_SourceName.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        TextBox_SourceName.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        TextBox_SourceName.Location = New Point(139, 3)
        TextBox_SourceName.Name = "TextBox_SourceName"
        TextBox_SourceName.Size = New Size(451, 29)
        TextBox_SourceName.TabIndex = 0
        ToolTip1.SetToolTip(TextBox_SourceName, "Filter source projects by name, description, or file.")
        ' 
        ' ImageList1
        ' 
        ImageList1.ColorDepth = ColorDepth.Depth32Bit
        ImageList1.ImageStream = CType(resources.GetObject("ImageList1.ImageStream"), ImageListStreamer)
        ImageList1.TransparentColor = Color.Transparent
        ImageList1.Images.SetKeyName(0, "agt_action_fail.ico")
        ImageList1.Images.SetKeyName(1, "agt_action_success.ico")
        ImageList1.Images.SetKeyName(2, "mail_find.ico")
        ImageList1.Images.SetKeyName(3, "edit.ico")
        ImageList1.Images.SetKeyName(4, "1leftarrow.ico")
        ImageList1.Images.SetKeyName(5, "1rightarrow.ico")
        ImageList1.Images.SetKeyName(6, "2leftarrow.ico")
        ImageList1.Images.SetKeyName(7, "2rightarrow.ico")
        ImageList1.Images.SetKeyName(8, "1downarrow1.ico")
        ImageList1.Images.SetKeyName(9, "attach.ico")
        ImageList1.Images.SetKeyName(10, "appearance.ico")
        ImageList1.Images.SetKeyName(11, "folder_sent_mail.ico")
        ImageList1.Images.SetKeyName(12, "gear.ico")
        ImageList1.Images.SetKeyName(13, "personal.ico")
        ImageList1.Images.SetKeyName(14, "layer-visible-off.ico")
        ImageList1.Images.SetKeyName(15, "layer-visible-on.ico")
        ImageList1.Images.SetKeyName(16, "help-hint.ico")
        ' 
        ' ListView2
        ' 
        ListView2.Columns.AddRange(New ColumnHeader() {Shapecol, ShapeTypeCol, Local, ColumnHeader5, Datasources})
        ListView2.Dock = DockStyle.Fill
        ListView2.FullRowSelect = True
        ListView2.Location = New Point(0, 0)
        ListView2.Name = "ListView2"
        ListView2.Size = New Size(584, 681)
        ListView2.TabIndex = 12
        ListView2.UseCompatibleStateImageBehavior = False
        ListView2.View = View.Details
        ListView2.Visible = False
        ' 
        ' Shapecol
        '
        Shapecol.Text = "Shape / Output"
        Shapecol.Width = 160
        '
        ' ShapeTypeCol
        '
        ShapeTypeCol.Text = "Type"
        ShapeTypeCol.Width = 130
        '
        ' Local
        '
        Local.Text = "Local"
        Local.Width = 55
        '
        ' ColumnHeader5
        '
        ColumnHeader5.Text = "HighHeel"
        ColumnHeader5.Width = 60
        '
        ' Datasources
        '
        Datasources.Text = "Datafolder / Output dir"
        Datasources.Width = 175
        ' 
        ' Exclude_Reference_Checkbox
        ' 
        Exclude_Reference_Checkbox.AutoSize = True
        Exclude_Reference_Checkbox.Dock = DockStyle.Fill
        Exclude_Reference_Checkbox.Location = New Point(3, 3)
        Exclude_Reference_Checkbox.Name = "Exclude_Reference_Checkbox"
        Exclude_Reference_Checkbox.Size = New Size(189, 28)
        Exclude_Reference_Checkbox.TabIndex = 0
        Exclude_Reference_Checkbox.Text = "Exclude reference"
        ToolTip1.SetToolTip(Exclude_Reference_Checkbox, "When enabled, removes the reference shape while copying or merging projects.")
        Exclude_Reference_Checkbox.UseVisualStyleBackColor = True
        ' 
        ' Ovewrite_DataFiles
        ' 
        Ovewrite_DataFiles.AutoSize = True
        Ovewrite_DataFiles.Dock = DockStyle.Fill
        Ovewrite_DataFiles.Location = New Point(3, 37)
        Ovewrite_DataFiles.Name = "Ovewrite_DataFiles"
        Ovewrite_DataFiles.Size = New Size(189, 28)
        Ovewrite_DataFiles.TabIndex = 3
        Ovewrite_DataFiles.Text = "Overwrite files"
        ToolTip1.SetToolTip(Ovewrite_DataFiles, "Allow existing output files, shapedata, materials, or textures to be overwritten without asking.")
        Ovewrite_DataFiles.UseVisualStyleBackColor = True
        ' 
        ' NewPackButton
        ' 
        NewPackButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        NewPackButton.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        NewPackButton.Location = New Point(576, 36)
        NewPackButton.Name = "NewPackButton"
        NewPackButton.Size = New Size(64, 29)
        NewPackButton.TabIndex = 2
        NewPackButton.Text = "New"
        ToolTip1.SetToolTip(NewPackButton, "Create a new empty Wardrobe Manager pack.")
        NewPackButton.UseVisualStyleBackColor = True
        ' 
        ' ListViewTargets
        ' 
        ListViewTargets.Columns.AddRange(New ColumnHeader() {ColumnHeader3, ColumnHeader7, ColumnHeader4})
        ListViewTargets.Dock = DockStyle.Fill
        ListViewTargets.FullRowSelect = True
        ListViewTargets.Location = New Point(0, 0)
        ListViewTargets.Name = "ListViewTargets"
        ListViewTargets.Size = New Size(646, 820)
        ListViewTargets.TabIndex = 0
        ToolTip1.SetToolTip(ListViewTargets, "Targets projects loaded from SliderSets. Select one or more projects to copy, merge, edit, or build.")
        ListViewTargets.UseCompatibleStateImageBehavior = False
        ListViewTargets.View = View.Details
        ' 
        ' ColumnHeader3
        ' 
        ColumnHeader3.Text = "Name"
        ColumnHeader3.Width = 242
        ' 
        ' ColumnHeader7
        ' 
        ColumnHeader7.Text = "Description"
        ColumnHeader7.Width = 200
        ' 
        ' ColumnHeader4
        ' 
        ColumnHeader4.Text = "File"
        ColumnHeader4.Width = 200
        ' 
        ' EditTargetButton
        ' 
        EditTargetButton.Dock = DockStyle.Fill
        EditTargetButton.Enabled = False
        EditTargetButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        EditTargetButton.ImageAlign = ContentAlignment.MiddleLeft
        EditTargetButton.ImageIndex = 9
        EditTargetButton.ImageList = ImageList1
        EditTargetButton.Location = New Point(3, 71)
        EditTargetButton.Name = "EditTargetButton"
        EditTargetButton.Size = New Size(207, 29)
        EditTargetButton.TabIndex = 5
        EditTargetButton.Text = "Edit in OS"
        EditTargetButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(EditTargetButton, "Open the selected target project in Outfit Studio.")
        EditTargetButton.UseVisualStyleBackColor = True
        ' 
        ' Auto_Move_Check
        ' 
        Auto_Move_Check.AutoSize = True
        Auto_Move_Check.Dock = DockStyle.Fill
        Auto_Move_Check.Location = New Point(198, 37)
        Auto_Move_Check.Name = "Auto_Move_Check"
        Auto_Move_Check.Size = New Size(189, 28)
        Auto_Move_Check.TabIndex = 4
        Auto_Move_Check.Text = "Auto move"
        ToolTip1.SetToolTip(Auto_Move_Check, "After a successful operation, move processed source projects to the Processed folder automatically.")
        Auto_Move_Check.UseVisualStyleBackColor = True
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(RadioButton3)
        GroupBox1.Controls.Add(Physics_Label)
        GroupBox1.Controls.Add(ButtonDataSheetSelected)
        GroupBox1.Controls.Add(ButtonPreviewSelected)
        GroupBox1.Controls.Add(Panel_Preview_Container)
        GroupBox1.Controls.Add(Label6)
        GroupBox1.Controls.Add(RadioButton2)
        GroupBox1.Controls.Add(RadioButton1)
        GroupBox1.Dock = DockStyle.Fill
        GroupBox1.Location = New Point(0, 0)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(592, 752)
        GroupBox1.TabIndex = 30
        GroupBox1.TabStop = False
        GroupBox1.Text = "Details for"
        ' 
        ' RadioButton3
        ' 
        RadioButton3.AutoSize = True
        RadioButton3.Checked = True
        RadioButton3.Location = New Point(175, 21)
        RadioButton3.Name = "RadioButton3"
        RadioButton3.Size = New Size(51, 19)
        RadioButton3.TabIndex = 41
        RadioButton3.TabStop = True
        RadioButton3.Text = "Auto"
        ToolTip1.SetToolTip(RadioButton3, "Automatically show preview and details for the list that currently has focus.")
        RadioButton3.UseVisualStyleBackColor = True
        ' 
        ' Physics_Label
        ' 
        Physics_Label.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Physics_Label.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Physics_Label.ForeColor = Color.Brown
        Physics_Label.Location = New Point(267, 38)
        Physics_Label.Name = "Physics_Label"
        Physics_Label.Size = New Size(322, 20)
        Physics_Label.TabIndex = 39
        Physics_Label.Text = "Physics"
        Physics_Label.TextAlign = ContentAlignment.MiddleLeft
        Physics_Label.Visible = False
        ' 
        ' ButtonDataSheetSelected
        ' 
        ButtonDataSheetSelected.Location = New Point(83, 46)
        ButtonDataSheetSelected.Name = "ButtonDataSheetSelected"
        ButtonDataSheetSelected.Size = New Size(72, 22)
        ButtonDataSheetSelected.TabIndex = 38
        ButtonDataSheetSelected.Text = "Data"
        ToolTip1.SetToolTip(ButtonDataSheetSelected, "Show project data instead of the 3D preview.")
        ButtonDataSheetSelected.UseVisualStyleBackColor = True
        ' 
        ' ButtonPreviewSelected
        ' 
        ButtonPreviewSelected.FlatStyle = FlatStyle.Flat
        ButtonPreviewSelected.Location = New Point(5, 46)
        ButtonPreviewSelected.Name = "ButtonPreviewSelected"
        ButtonPreviewSelected.Size = New Size(72, 22)
        ButtonPreviewSelected.TabIndex = 37
        ButtonPreviewSelected.Text = "Preview"
        ToolTip1.SetToolTip(ButtonPreviewSelected, "Show the 3D preview for the selected project.")
        ButtonPreviewSelected.UseVisualStyleBackColor = True
        ' 
        ' Panel_Preview_Container
        ' 
        Panel_Preview_Container.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        Panel_Preview_Container.BorderStyle = BorderStyle.FixedSingle
        Panel_Preview_Container.Controls.Add(ListView2)
        Panel_Preview_Container.Location = New Point(2, 69)
        Panel_Preview_Container.Name = "Panel_Preview_Container"
        Panel_Preview_Container.Size = New Size(586, 683)
        Panel_Preview_Container.TabIndex = 36
        ' 
        ' Label6
        ' 
        Label6.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        Label6.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label6.ForeColor = Color.Red
        Label6.Location = New Point(267, 18)
        Label6.Name = "Label6"
        Label6.Size = New Size(319, 20)
        Label6.TabIndex = 31
        Label6.Text = "High Heels"
        Label6.TextAlign = ContentAlignment.MiddleLeft
        Label6.Visible = False
        ' 
        ' RadioButton2
        ' 
        RadioButton2.AutoSize = True
        RadioButton2.Location = New Point(91, 21)
        RadioButton2.Name = "RadioButton2"
        RadioButton2.Size = New Size(58, 19)
        RadioButton2.TabIndex = 14
        RadioButton2.Text = "Target"
        ToolTip1.SetToolTip(RadioButton2, "Show preview and  details for the selected target project.")
        RadioButton2.UseVisualStyleBackColor = True
        ' 
        ' RadioButton1
        ' 
        RadioButton1.AutoSize = True
        RadioButton1.Location = New Point(5, 21)
        RadioButton1.Name = "RadioButton1"
        RadioButton1.Size = New Size(61, 19)
        RadioButton1.TabIndex = 13
        RadioButton1.Text = "Source"
        ToolTip1.SetToolTip(RadioButton1, "Show preview and details for the selected source project.")
        RadioButton1.UseVisualStyleBackColor = True
        ' 
        ' TableLayoutPanel1
        ' 
        TableLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink
        TableLayoutPanel1.ColumnCount = 3
        TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.33333F))
        TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333359F))
        TableLayoutPanel1.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333359F))
        TableLayoutPanel1.Controls.Add(RecalculateNormalsCheck, 1, 2)
        TableLayoutPanel1.Controls.Add(SingleBoneCheck, 2, 2)
        TableLayoutPanel1.Controls.Add(ButtonOpenConfig, 0, 2)
        TableLayoutPanel1.Controls.Add(Exclude_Reference_Checkbox, 0, 0)
        TableLayoutPanel1.Controls.Add(PhysicsCheckbox, 1, 0)
        TableLayoutPanel1.Controls.Add(CloneMaterialsCheck, 2, 0)
        TableLayoutPanel1.Controls.Add(Ovewrite_DataFiles, 0, 1)
        TableLayoutPanel1.Controls.Add(OutputDirChangeCheck, 2, 1)
        TableLayoutPanel1.Controls.Add(Auto_Move_Check, 1, 1)
        TableLayoutPanel1.Dock = DockStyle.Fill
        TableLayoutPanel1.Location = New Point(3, 19)
        TableLayoutPanel1.Name = "TableLayoutPanel1"
        TableLayoutPanel1.RowCount = 3
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.Absolute, 20F))
        TableLayoutPanel1.Size = New Size(586, 103)
        TableLayoutPanel1.TabIndex = 41
        ' 
        ' RecalculateNormalsCheck
        ' 
        RecalculateNormalsCheck.AutoSize = True
        RecalculateNormalsCheck.Checked = True
        RecalculateNormalsCheck.CheckState = CheckState.Checked
        RecalculateNormalsCheck.Dock = DockStyle.Fill
        RecalculateNormalsCheck.Location = New Point(198, 71)
        RecalculateNormalsCheck.Name = "RecalculateNormalsCheck"
        RecalculateNormalsCheck.Size = New Size(189, 29)
        RecalculateNormalsCheck.TabIndex = 9
        RecalculateNormalsCheck.Text = "Recalculate Normals"
        ToolTip1.SetToolTip(RecalculateNormalsCheck, "Recalculate normals in preview and building using the current normal reconstruction settings.")
        RecalculateNormalsCheck.UseVisualStyleBackColor = True
        ' 
        ' SingleBoneCheck
        ' 
        SingleBoneCheck.AutoSize = True
        SingleBoneCheck.Dock = DockStyle.Fill
        SingleBoneCheck.Location = New Point(393, 71)
        SingleBoneCheck.Name = "SingleBoneCheck"
        SingleBoneCheck.Size = New Size(190, 29)
        SingleBoneCheck.TabIndex = 8
        SingleBoneCheck.Text = "Single bone skinning"
        ToolTip1.SetToolTip(SingleBoneCheck, "Use single-bone skinning in preview. Faster, more resilient to complex physics, but posing support is not compatible.")
        SingleBoneCheck.UseVisualStyleBackColor = True
        ' 
        ' ButtonOpenConfig
        ' 
        ButtonOpenConfig.Dock = DockStyle.Fill
        ButtonOpenConfig.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonOpenConfig.ImageAlign = ContentAlignment.MiddleLeft
        ButtonOpenConfig.ImageIndex = 12
        ButtonOpenConfig.ImageList = ImageList1
        ButtonOpenConfig.Location = New Point(3, 71)
        ButtonOpenConfig.Name = "ButtonOpenConfig"
        ButtonOpenConfig.Size = New Size(189, 29)
        ButtonOpenConfig.TabIndex = 7
        ButtonOpenConfig.Text = "Settings"
        ButtonOpenConfig.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonOpenConfig, "Open application settings, paths, build options, and rendering options.")
        ButtonOpenConfig.UseVisualStyleBackColor = True
        ' 
        ' PhysicsCheckbox
        ' 
        PhysicsCheckbox.AutoSize = True
        PhysicsCheckbox.Checked = True
        PhysicsCheckbox.CheckState = CheckState.Checked
        PhysicsCheckbox.Dock = DockStyle.Fill
        PhysicsCheckbox.Location = New Point(198, 3)
        PhysicsCheckbox.Name = "PhysicsCheckbox"
        PhysicsCheckbox.Size = New Size(189, 28)
        PhysicsCheckbox.TabIndex = 1
        PhysicsCheckbox.Text = "Keep physics"
        ToolTip1.SetToolTip(PhysicsCheckbox, "Keep physics during copy and merge operations (BSClothExtraData for FO4, HDT-SMP XML for SSE).")
        PhysicsCheckbox.UseVisualStyleBackColor = True
        ' 
        ' CloneMaterialsCheck
        ' 
        CloneMaterialsCheck.AutoSize = True
        CloneMaterialsCheck.Checked = True
        CloneMaterialsCheck.CheckState = CheckState.Checked
        CloneMaterialsCheck.Dock = DockStyle.Fill
        CloneMaterialsCheck.Location = New Point(393, 3)
        CloneMaterialsCheck.Name = "CloneMaterialsCheck"
        CloneMaterialsCheck.Size = New Size(190, 28)
        CloneMaterialsCheck.TabIndex = 2
        CloneMaterialsCheck.Text = "Clone materials"
        ToolTip1.SetToolTip(CloneMaterialsCheck, "Clone referenced material and texture files to independent loose files (for ba2 originals must be allowed in settings).")
        CloneMaterialsCheck.UseVisualStyleBackColor = True
        ' 
        ' OutputDirChangeCheck
        ' 
        OutputDirChangeCheck.AutoSize = True
        OutputDirChangeCheck.Checked = True
        OutputDirChangeCheck.CheckState = CheckState.Checked
        OutputDirChangeCheck.Dock = DockStyle.Fill
        OutputDirChangeCheck.Location = New Point(393, 37)
        OutputDirChangeCheck.Name = "OutputDirChangeCheck"
        OutputDirChangeCheck.Size = New Size(190, 28)
        OutputDirChangeCheck.TabIndex = 5
        OutputDirChangeCheck.Text = "Change out dir."
        ToolTip1.SetToolTip(OutputDirChangeCheck, "Rewrite the output directory to a safe cloned path instead of keeping the original mesh output path (disable it for replacers).")
        OutputDirChangeCheck.UseVisualStyleBackColor = True
        ' 
        ' TextBox_TargetName
        ' 
        TextBox_TargetName.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        TextBox_TargetName.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        TextBox_TargetName.Location = New Point(154, 3)
        TextBox_TargetName.Name = "TextBox_TargetName"
        TextBox_TargetName.Size = New Size(486, 29)
        TextBox_TargetName.TabIndex = 0
        ToolTip1.SetToolTip(TextBox_TargetName, "Optional name override used when creating or copying target projects.")
        ' 
        ' Label3
        ' 
        Label3.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label3.Location = New Point(6, 3)
        Label3.Name = "Label3"
        Label3.Size = New Size(132, 29)
        Label3.TabIndex = 32
        Label3.Text = "Project"
        Label3.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' GroupBox2
        ' 
        GroupBox2.Controls.Add(TableLayoutPanel2)
        GroupBox2.Dock = DockStyle.Fill
        GroupBox2.Location = New Point(3, 3)
        GroupBox2.Name = "GroupBox2"
        GroupBox2.Size = New Size(646, 125)
        GroupBox2.TabIndex = 33
        GroupBox2.TabStop = False
        GroupBox2.Text = "Project options"
        ' 
        ' TableLayoutPanel2
        ' 
        TableLayoutPanel2.ColumnCount = 3
        TableLayoutPanel2.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.Controls.Add(ButtonBuildFullPack, 2, 2)
        TableLayoutPanel2.Controls.Add(ButtonEditInternally, 1, 2)
        TableLayoutPanel2.Controls.Add(ButtonBuildSingles, 2, 0)
        TableLayoutPanel2.Controls.Add(ButtonDelete, 0, 0)
        TableLayoutPanel2.Controls.Add(EditTargetButton, 0, 2)
        TableLayoutPanel2.Controls.Add(CloneButton, 2, 1)
        TableLayoutPanel2.Controls.Add(RenameButton, 1, 0)
        TableLayoutPanel2.Controls.Add(MergeIntoTargetButton, 1, 1)
        TableLayoutPanel2.Controls.Add(ExtractSingleButton, 0, 1)
        TableLayoutPanel2.Dock = DockStyle.Fill
        TableLayoutPanel2.Location = New Point(3, 19)
        TableLayoutPanel2.Name = "TableLayoutPanel2"
        TableLayoutPanel2.RowCount = 3
        TableLayoutPanel2.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel2.Size = New Size(640, 103)
        TableLayoutPanel2.TabIndex = 6
        ' 
        ' ButtonBuildFullPack
        ' 
        ButtonBuildFullPack.Dock = DockStyle.Fill
        ButtonBuildFullPack.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonBuildFullPack.ImageAlign = ContentAlignment.MiddleLeft
        ButtonBuildFullPack.ImageIndex = 11
        ButtonBuildFullPack.ImageList = ImageList1
        ButtonBuildFullPack.Location = New Point(429, 71)
        ButtonBuildFullPack.Name = "ButtonBuildFullPack"
        ButtonBuildFullPack.Size = New Size(208, 29)
        ButtonBuildFullPack.TabIndex = 8
        ButtonBuildFullPack.Text = "Build full pack"
        ButtonBuildFullPack.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonBuildFullPack, "Build every project currently listed in the selected pack.")
        ButtonBuildFullPack.UseVisualStyleBackColor = True
        ' 
        ' ButtonEditInternally
        ' 
        ButtonEditInternally.Dock = DockStyle.Fill
        ButtonEditInternally.Enabled = False
        ButtonEditInternally.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonEditInternally.ImageAlign = ContentAlignment.MiddleLeft
        ButtonEditInternally.ImageIndex = 10
        ButtonEditInternally.ImageList = ImageList1
        ButtonEditInternally.Location = New Point(216, 71)
        ButtonEditInternally.Name = "ButtonEditInternally"
        ButtonEditInternally.Size = New Size(207, 29)
        ButtonEditInternally.TabIndex = 7
        ButtonEditInternally.Text = "Edit internally"
        ButtonEditInternally.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonEditInternally, "Open the selected target project in the internal editor.")
        ButtonEditInternally.UseVisualStyleBackColor = True
        ' 
        ' ButtonBuildSingles
        ' 
        ButtonBuildSingles.Dock = DockStyle.Fill
        ButtonBuildSingles.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonBuildSingles.ImageAlign = ContentAlignment.MiddleLeft
        ButtonBuildSingles.ImageIndex = 11
        ButtonBuildSingles.ImageList = ImageList1
        ButtonBuildSingles.Location = New Point(429, 3)
        ButtonBuildSingles.Name = "ButtonBuildSingles"
        ButtonBuildSingles.Size = New Size(208, 28)
        ButtonBuildSingles.TabIndex = 6
        ButtonBuildSingles.Text = "Build"
        ButtonBuildSingles.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonBuildSingles, "Build the selected target projects to final game files.")
        ButtonBuildSingles.UseVisualStyleBackColor = True
        ' 
        ' ButtonDelete
        ' 
        ButtonDelete.Dock = DockStyle.Fill
        ButtonDelete.Enabled = False
        ButtonDelete.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonDelete.ImageAlign = ContentAlignment.MiddleLeft
        ButtonDelete.ImageKey = "agt_action_fail.ico"
        ButtonDelete.ImageList = ImageList1
        ButtonDelete.Location = New Point(3, 3)
        ButtonDelete.Name = "ButtonDelete"
        ButtonDelete.Size = New Size(207, 28)
        ButtonDelete.TabIndex = 0
        ButtonDelete.Text = "Delete"
        ButtonDelete.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonDelete, "Delete the selected target project from the current pack.")
        ButtonDelete.UseVisualStyleBackColor = True
        ' 
        ' CloneButton
        ' 
        CloneButton.Dock = DockStyle.Fill
        CloneButton.Enabled = False
        CloneButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        CloneButton.ImageAlign = ContentAlignment.MiddleLeft
        CloneButton.ImageIndex = 8
        CloneButton.ImageList = ImageList1
        CloneButton.Location = New Point(429, 37)
        CloneButton.Name = "CloneButton"
        CloneButton.Size = New Size(208, 28)
        CloneButton.TabIndex = 4
        CloneButton.Text = "Clone target"
        CloneButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(CloneButton, "Clone the selected target project inside the current pack.")
        CloneButton.UseVisualStyleBackColor = True
        ' 
        ' RenameButton
        ' 
        RenameButton.Dock = DockStyle.Fill
        RenameButton.Enabled = False
        RenameButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        RenameButton.ImageAlign = ContentAlignment.MiddleLeft
        RenameButton.ImageIndex = 3
        RenameButton.ImageList = ImageList1
        RenameButton.Location = New Point(216, 3)
        RenameButton.Name = "RenameButton"
        RenameButton.Size = New Size(207, 28)
        RenameButton.TabIndex = 1
        RenameButton.Text = "Rename"
        RenameButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(RenameButton, "Rename the selected target project.")
        RenameButton.UseVisualStyleBackColor = True
        ' 
        ' MergeIntoTargetButton
        ' 
        MergeIntoTargetButton.Dock = DockStyle.Fill
        MergeIntoTargetButton.Enabled = False
        MergeIntoTargetButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        MergeIntoTargetButton.ImageAlign = ContentAlignment.MiddleLeft
        MergeIntoTargetButton.ImageIndex = 7
        MergeIntoTargetButton.ImageList = ImageList1
        MergeIntoTargetButton.Location = New Point(216, 37)
        MergeIntoTargetButton.Name = "MergeIntoTargetButton"
        MergeIntoTargetButton.Size = New Size(207, 28)
        MergeIntoTargetButton.TabIndex = 3
        MergeIntoTargetButton.Text = "Merge sources"
        MergeIntoTargetButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(MergeIntoTargetButton, "Merge the selected source projects into the selected target project.")
        MergeIntoTargetButton.UseVisualStyleBackColor = True
        ' 
        ' ExtractSingleButton
        ' 
        ExtractSingleButton.Dock = DockStyle.Fill
        ExtractSingleButton.Enabled = False
        ExtractSingleButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ExtractSingleButton.ImageAlign = ContentAlignment.MiddleLeft
        ExtractSingleButton.ImageIndex = 4
        ExtractSingleButton.ImageList = ImageList1
        ExtractSingleButton.Location = New Point(3, 37)
        ExtractSingleButton.Name = "ExtractSingleButton"
        ExtractSingleButton.Size = New Size(207, 28)
        ExtractSingleButton.TabIndex = 2
        ExtractSingleButton.Text = "Extract to single"
        ExtractSingleButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ExtractSingleButton, "Extract the selected target project into a standalone source project.")
        ExtractSingleButton.UseVisualStyleBackColor = True
        ' 
        ' Label7
        ' 
        Label7.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label7.Location = New Point(0, 36)
        Label7.Name = "Label7"
        Label7.Size = New Size(141, 27)
        Label7.TabIndex = 37
        Label7.Text = "Preset"
        Label7.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' ComboBoxPresets
        ' 
        ComboBoxPresets.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        ComboBoxPresets.DropDownStyle = ComboBoxStyle.DropDownList
        ComboBoxPresets.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        ComboBoxPresets.FormattingEnabled = True
        ComboBoxPresets.Location = New Point(138, 34)
        ComboBoxPresets.Name = "ComboBoxPresets"
        ComboBoxPresets.Size = New Size(347, 29)
        ComboBoxPresets.TabIndex = 1
        ToolTip1.SetToolTip(ComboBoxPresets, "Select the preset sliders applied to preview and building.")
        ' 
        ' SplitPrincipal_1
        ' 
        SplitPrincipal_1.Dock = DockStyle.Fill
        SplitPrincipal_1.Location = New Point(0, 0)
        SplitPrincipal_1.Name = "SplitPrincipal_1"
        ' 
        ' SplitPrincipal_1.Panel1
        ' 
        SplitPrincipal_1.Panel1.Controls.Add(Split_Split_y_Menu_Sources)
        SplitPrincipal_1.Panel1MinSize = 500
        ' 
        ' SplitPrincipal_1.Panel2
        ' 
        SplitPrincipal_1.Panel2.Controls.Add(Split_Principal2)
        SplitPrincipal_1.Size = New Size(1904, 1041)
        SplitPrincipal_1.SplitterDistance = 650
        SplitPrincipal_1.SplitterWidth = 2
        SplitPrincipal_1.TabIndex = 39
        ' 
        ' Split_Split_y_Menu_Sources
        ' 
        Split_Split_y_Menu_Sources.Dock = DockStyle.Fill
        Split_Split_y_Menu_Sources.FixedPanel = FixedPanel.Panel2
        Split_Split_y_Menu_Sources.IsSplitterFixed = True
        Split_Split_y_Menu_Sources.Location = New Point(0, 0)
        Split_Split_y_Menu_Sources.Name = "Split_Split_y_Menu_Sources"
        Split_Split_y_Menu_Sources.Orientation = Orientation.Horizontal
        ' 
        ' Split_Split_y_Menu_Sources.Panel1
        ' 
        Split_Split_y_Menu_Sources.Panel1.Controls.Add(Panel2)
        Split_Split_y_Menu_Sources.Panel1.Controls.Add(Split_Panel_y_Lista_Source)
        Split_Split_y_Menu_Sources.Panel1.Padding = New Padding(3)
        ' 
        ' Split_Split_y_Menu_Sources.Panel2
        ' 
        Split_Split_y_Menu_Sources.Panel2.Controls.Add(GroupBox3)
        Split_Split_y_Menu_Sources.Panel2.Padding = New Padding(3)
        Split_Split_y_Menu_Sources.Panel2MinSize = 131
        Split_Split_y_Menu_Sources.Size = New Size(650, 1041)
        Split_Split_y_Menu_Sources.SplitterDistance = 908
        Split_Split_y_Menu_Sources.SplitterWidth = 2
        Split_Split_y_Menu_Sources.TabIndex = 41
        ' 
        ' Panel2
        ' 
        Panel2.AutoSizeMode = AutoSizeMode.GrowAndShrink
        Panel2.Controls.Add(CheckBoxFixUncloned)
        Panel2.Controls.Add(CheckBoxReloadDict)
        Panel2.Controls.Add(TextBox2)
        Panel2.Controls.Add(ShowCollectionsCheck)
        Panel2.Controls.Add(ShowCBBECheck)
        Panel2.Controls.Add(RefreshButton)
        Panel2.Controls.Add(Label5)
        Panel2.Controls.Add(DeepAnalize_check)
        Panel2.Controls.Add(CheckShowpacks)
        Panel2.Dock = DockStyle.Top
        Panel2.Location = New Point(3, 3)
        Panel2.Name = "Panel2"
        Panel2.Size = New Size(644, 80)
        Panel2.TabIndex = 1
        ' 
        ' CheckBoxFixUncloned
        ' 
        CheckBoxFixUncloned.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        CheckBoxFixUncloned.AutoSize = True
        CheckBoxFixUncloned.Enabled = False
        CheckBoxFixUncloned.Location = New Point(520, 59)
        CheckBoxFixUncloned.Name = "CheckBoxFixUncloned"
        CheckBoxFixUncloned.Size = New Size(98, 19)
        CheckBoxFixUncloned.TabIndex = 25
        CheckBoxFixUncloned.Text = "Fix un-cloned"
        ToolTip1.SetToolTip(CheckBoxFixUncloned, "Mark the file dictionary to be rebuilt on the next refresh.")
        CheckBoxFixUncloned.UseVisualStyleBackColor = True
        ' 
        ' CheckBoxReloadDict
        ' 
        CheckBoxReloadDict.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        CheckBoxReloadDict.AutoSize = True
        CheckBoxReloadDict.Location = New Point(520, 40)
        CheckBoxReloadDict.Name = "CheckBoxReloadDict"
        CheckBoxReloadDict.Size = New Size(118, 19)
        CheckBoxReloadDict.TabIndex = 24
        CheckBoxReloadDict.Text = "Reload dictionary"
        ToolTip1.SetToolTip(CheckBoxReloadDict, "Mark the file dictionary to be rebuilt on the next refresh.")
        CheckBoxReloadDict.UseVisualStyleBackColor = True
        ' 
        ' TextBox2
        ' 
        TextBox2.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        TextBox2.Font = New Font("Segoe UI", 12F, FontStyle.Bold)
        TextBox2.Location = New Point(170, 9)
        TextBox2.Name = "TextBox2"
        TextBox2.Size = New Size(468, 29)
        TextBox2.TabIndex = 0
        ' 
        ' ShowCollectionsCheck
        ' 
        ShowCollectionsCheck.AutoSize = True
        ShowCollectionsCheck.Checked = True
        ShowCollectionsCheck.CheckState = CheckState.Checked
        ShowCollectionsCheck.Location = New Point(3, 59)
        ShowCollectionsCheck.Name = "ShowCollectionsCheck"
        ShowCollectionsCheck.Size = New Size(117, 19)
        ShowCollectionsCheck.TabIndex = 4
        ShowCollectionsCheck.Text = "Show Collections"
        ToolTip1.SetToolTip(ShowCollectionsCheck, "Show collection-style projects (multiple project osp) in the source list.")
        ShowCollectionsCheck.UseVisualStyleBackColor = True
        ' 
        ' ShowCBBECheck
        ' 
        ShowCBBECheck.AutoSize = True
        ShowCBBECheck.Location = New Point(170, 40)
        ShowCBBECheck.Name = "ShowCBBECheck"
        ShowCBBECheck.Size = New Size(86, 19)
        ShowCBBECheck.TabIndex = 1
        ShowCBBECheck.Text = "Show CBBE"
        ToolTip1.SetToolTip(ShowCBBECheck, "Show CBBE-related projects (Body and Vanilla outfits) in the source list.")
        ShowCBBECheck.UseVisualStyleBackColor = True
        ' 
        ' RefreshButton
        ' 
        RefreshButton.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        RefreshButton.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        RefreshButton.Location = New Point(286, 44)
        RefreshButton.Name = "RefreshButton"
        RefreshButton.Size = New Size(228, 29)
        RefreshButton.TabIndex = 2
        RefreshButton.Text = "Refresh"
        ToolTip1.SetToolTip(RefreshButton, "Reload projects, presets, poses, skeleton, and dictionaries from disk.")
        RefreshButton.UseVisualStyleBackColor = True
        ' 
        ' Label5
        ' 
        Label5.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label5.Location = New Point(3, 10)
        Label5.Name = "Label5"
        Label5.Size = New Size(164, 28)
        Label5.TabIndex = 23
        Label5.Text = "Filter"
        Label5.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' DeepAnalize_check
        ' 
        DeepAnalize_check.AutoSize = True
        DeepAnalize_check.Location = New Point(170, 59)
        DeepAnalize_check.Name = "DeepAnalize_check"
        DeepAnalize_check.Size = New Size(95, 19)
        DeepAnalize_check.TabIndex = 5
        DeepAnalize_check.Text = "Deep analyze"
        ToolTip1.SetToolTip(DeepAnalize_check, "Perform deeper validation when loading projects, including shapedata and OSD consistency checks (very slow).")
        DeepAnalize_check.UseVisualStyleBackColor = True
        ' 
        ' CheckShowpacks
        ' 
        CheckShowpacks.AutoSize = True
        CheckShowpacks.Location = New Point(3, 40)
        CheckShowpacks.Name = "CheckShowpacks"
        CheckShowpacks.Size = New Size(88, 19)
        CheckShowpacks.TabIndex = 3
        CheckShowpacks.Text = "Show packs"
        ToolTip1.SetToolTip(CheckShowpacks, "Show Wardrobe Manager pack files also in the source list.")
        CheckShowpacks.UseVisualStyleBackColor = True
        ' 
        ' Split_Panel_y_Lista_Source
        ' 
        Split_Panel_y_Lista_Source.Dock = DockStyle.Fill
        Split_Panel_y_Lista_Source.FixedPanel = FixedPanel.Panel1
        Split_Panel_y_Lista_Source.IsSplitterFixed = True
        Split_Panel_y_Lista_Source.Location = New Point(3, 3)
        Split_Panel_y_Lista_Source.Name = "Split_Panel_y_Lista_Source"
        Split_Panel_y_Lista_Source.Orientation = Orientation.Horizontal
        ' 
        ' Split_Panel_y_Lista_Source.Panel2
        ' 
        Split_Panel_y_Lista_Source.Panel2.Controls.Add(ListViewSources)
        Split_Panel_y_Lista_Source.Size = New Size(644, 902)
        Split_Panel_y_Lista_Source.SplitterDistance = 80
        Split_Panel_y_Lista_Source.SplitterWidth = 2
        Split_Panel_y_Lista_Source.TabIndex = 40
        ' 
        ' ListViewSources
        ' 
        ListViewSources.AllowColumnReorder = True
        ListViewSources.Columns.AddRange(New ColumnHeader() {ColumnHeader1, ColumnHeader6, ColumnHeader2})
        ListViewSources.Dock = DockStyle.Fill
        ListViewSources.FullRowSelect = True
        ListViewSources.Location = New Point(0, 0)
        ListViewSources.Name = "ListViewSources"
        ListViewSources.Size = New Size(644, 820)
        ListViewSources.TabIndex = 0
        ToolTip1.SetToolTip(ListViewSources, "Source projects loaded from SliderSets. Select one or more projects to copy, merge, edit, or remove.")
        ListViewSources.UseCompatibleStateImageBehavior = False
        ListViewSources.View = View.Details
        ' 
        ' ColumnHeader1
        ' 
        ColumnHeader1.Text = "Name"
        ColumnHeader1.Width = 240
        ' 
        ' ColumnHeader6
        ' 
        ColumnHeader6.Text = "Description"
        ColumnHeader6.Width = 200
        ' 
        ' ColumnHeader2
        ' 
        ColumnHeader2.Text = "File"
        ColumnHeader2.Width = 200
        ' 
        ' GroupBox3
        ' 
        GroupBox3.Controls.Add(TableLayoutPanel3)
        GroupBox3.Dock = DockStyle.Fill
        GroupBox3.Location = New Point(3, 3)
        GroupBox3.Name = "GroupBox3"
        GroupBox3.Size = New Size(644, 125)
        GroupBox3.TabIndex = 34
        GroupBox3.TabStop = False
        GroupBox3.Text = "Source options"
        ' 
        ' TableLayoutPanel3
        ' 
        TableLayoutPanel3.ColumnCount = 3
        TableLayoutPanel3.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.Controls.Add(ButtonCreateFromNif, 2, 2)
        TableLayoutPanel3.Controls.Add(ButtonDeleteSource, 2, 1)
        TableLayoutPanel3.Controls.Add(MergeInSelectedButton, 2, 0)
        TableLayoutPanel3.Controls.Add(CopytoPackButton, 0, 0)
        TableLayoutPanel3.Controls.Add(EditButton, 0, 2)
        TableLayoutPanel3.Controls.Add(MovetoDiscardedButton, 1, 1)
        TableLayoutPanel3.Controls.Add(MoveToProcessedButton, 0, 1)
        TableLayoutPanel3.Controls.Add(MergeButton, 1, 0)
        TableLayoutPanel3.Controls.Add(ButtonSourceInternalEdit, 1, 2)
        TableLayoutPanel3.Dock = DockStyle.Fill
        TableLayoutPanel3.Location = New Point(3, 19)
        TableLayoutPanel3.Name = "TableLayoutPanel3"
        TableLayoutPanel3.RowCount = 3
        TableLayoutPanel3.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333321F))
        TableLayoutPanel3.Size = New Size(638, 103)
        TableLayoutPanel3.TabIndex = 5
        ' 
        ' ButtonCreateFromNif
        ' 
        ButtonCreateFromNif.Dock = DockStyle.Fill
        ButtonCreateFromNif.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonCreateFromNif.ImageAlign = ContentAlignment.MiddleLeft
        ButtonCreateFromNif.ImageIndex = 8
        ButtonCreateFromNif.ImageList = ImageList1
        ButtonCreateFromNif.Location = New Point(427, 71)
        ButtonCreateFromNif.Name = "ButtonCreateFromNif"
        ButtonCreateFromNif.Size = New Size(208, 29)
        ButtonCreateFromNif.TabIndex = 10
        ButtonCreateFromNif.Text = "Create from NIF"
        ButtonCreateFromNif.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonCreateFromNif, "Create a new project from a NIF file and optionally import sliders from a TRI file.")
        ButtonCreateFromNif.UseVisualStyleBackColor = True
        ' 
        ' ButtonDeleteSource
        ' 
        ButtonDeleteSource.Dock = DockStyle.Fill
        ButtonDeleteSource.Enabled = False
        ButtonDeleteSource.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonDeleteSource.ImageAlign = ContentAlignment.MiddleLeft
        ButtonDeleteSource.ImageKey = "agt_action_fail.ico"
        ButtonDeleteSource.ImageList = ImageList1
        ButtonDeleteSource.Location = New Point(427, 37)
        ButtonDeleteSource.Name = "ButtonDeleteSource"
        ButtonDeleteSource.Size = New Size(208, 28)
        ButtonDeleteSource.TabIndex = 9
        ButtonDeleteSource.Text = "Delete"
        ButtonDeleteSource.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonDeleteSource, "Delete the selected source project. If it becomes empty, its OSP file may also be removed.")
        ButtonDeleteSource.UseVisualStyleBackColor = True
        ' 
        ' MergeInSelectedButton
        ' 
        MergeInSelectedButton.Dock = DockStyle.Fill
        MergeInSelectedButton.Enabled = False
        MergeInSelectedButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        MergeInSelectedButton.ImageAlign = ContentAlignment.MiddleLeft
        MergeInSelectedButton.ImageIndex = 7
        MergeInSelectedButton.ImageList = ImageList1
        MergeInSelectedButton.Location = New Point(427, 3)
        MergeInSelectedButton.Name = "MergeInSelectedButton"
        MergeInSelectedButton.Size = New Size(208, 28)
        MergeInSelectedButton.TabIndex = 5
        MergeInSelectedButton.Text = "Merge in selected"
        MergeInSelectedButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(MergeInSelectedButton, "Merge selected sources into the selected target project.")
        MergeInSelectedButton.UseVisualStyleBackColor = True
        ' 
        ' CopytoPackButton
        ' 
        CopytoPackButton.Dock = DockStyle.Fill
        CopytoPackButton.Enabled = False
        CopytoPackButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        CopytoPackButton.ImageAlign = ContentAlignment.MiddleLeft
        CopytoPackButton.ImageIndex = 5
        CopytoPackButton.ImageList = ImageList1
        CopytoPackButton.Location = New Point(3, 3)
        CopytoPackButton.Name = "CopytoPackButton"
        CopytoPackButton.Size = New Size(206, 28)
        CopytoPackButton.TabIndex = 0
        CopytoPackButton.Text = "Copy singles"
        CopytoPackButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(CopytoPackButton, "Copy each selected source project into the current pack as a new target.")
        CopytoPackButton.UseVisualStyleBackColor = True
        ' 
        ' EditButton
        ' 
        EditButton.Dock = DockStyle.Fill
        EditButton.Enabled = False
        EditButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        EditButton.ImageAlign = ContentAlignment.MiddleLeft
        EditButton.ImageIndex = 9
        EditButton.ImageList = ImageList1
        EditButton.Location = New Point(3, 71)
        EditButton.Name = "EditButton"
        EditButton.Size = New Size(206, 29)
        EditButton.TabIndex = 4
        EditButton.Text = "Edit in OS"
        EditButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(EditButton, "Open the selected source project in Outfit Studio. It is reloaded after save.")
        EditButton.UseVisualStyleBackColor = True
        ' 
        ' MovetoDiscardedButton
        ' 
        MovetoDiscardedButton.BackgroundImageLayout = ImageLayout.Zoom
        MovetoDiscardedButton.Dock = DockStyle.Fill
        MovetoDiscardedButton.Enabled = False
        MovetoDiscardedButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        MovetoDiscardedButton.ImageAlign = ContentAlignment.MiddleLeft
        MovetoDiscardedButton.ImageIndex = 0
        MovetoDiscardedButton.ImageList = ImageList1
        MovetoDiscardedButton.Location = New Point(215, 37)
        MovetoDiscardedButton.Name = "MovetoDiscardedButton"
        MovetoDiscardedButton.Size = New Size(206, 28)
        MovetoDiscardedButton.TabIndex = 3
        MovetoDiscardedButton.Text = "Move to discarded"
        MovetoDiscardedButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(MovetoDiscardedButton, "Move selected source projects to the Discarded folder.")
        MovetoDiscardedButton.UseVisualStyleBackColor = True
        ' 
        ' MoveToProcessedButton
        ' 
        MoveToProcessedButton.BackgroundImageLayout = ImageLayout.Zoom
        MoveToProcessedButton.Dock = DockStyle.Fill
        MoveToProcessedButton.Enabled = False
        MoveToProcessedButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        MoveToProcessedButton.ImageAlign = ContentAlignment.MiddleLeft
        MoveToProcessedButton.ImageIndex = 0
        MoveToProcessedButton.ImageList = ImageList1
        MoveToProcessedButton.Location = New Point(3, 37)
        MoveToProcessedButton.Name = "MoveToProcessedButton"
        MoveToProcessedButton.Size = New Size(206, 28)
        MoveToProcessedButton.TabIndex = 2
        MoveToProcessedButton.Text = "Move to processed"
        MoveToProcessedButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(MoveToProcessedButton, "Move selected source projects to the Processed folder.")
        MoveToProcessedButton.UseVisualStyleBackColor = True
        ' 
        ' MergeButton
        ' 
        MergeButton.Dock = DockStyle.Fill
        MergeButton.Enabled = False
        MergeButton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        MergeButton.ImageAlign = ContentAlignment.MiddleLeft
        MergeButton.ImageIndex = 7
        MergeButton.ImageList = ImageList1
        MergeButton.Location = New Point(215, 3)
        MergeButton.Name = "MergeButton"
        MergeButton.Size = New Size(206, 28)
        MergeButton.TabIndex = 1
        MergeButton.Text = "Merge in new"
        MergeButton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(MergeButton, "Create a new target project by merging the selected source projects.")
        MergeButton.UseVisualStyleBackColor = True
        ' 
        ' ButtonSourceInternalEdit
        ' 
        ButtonSourceInternalEdit.Dock = DockStyle.Fill
        ButtonSourceInternalEdit.Enabled = False
        ButtonSourceInternalEdit.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonSourceInternalEdit.ImageAlign = ContentAlignment.MiddleLeft
        ButtonSourceInternalEdit.ImageIndex = 10
        ButtonSourceInternalEdit.ImageList = ImageList1
        ButtonSourceInternalEdit.Location = New Point(215, 71)
        ButtonSourceInternalEdit.Name = "ButtonSourceInternalEdit"
        ButtonSourceInternalEdit.Size = New Size(206, 29)
        ButtonSourceInternalEdit.TabIndex = 8
        ButtonSourceInternalEdit.Text = "Edit internally"
        ButtonSourceInternalEdit.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonSourceInternalEdit, "Open the selected source project in the internal editor.")
        ButtonSourceInternalEdit.UseVisualStyleBackColor = True
        ' 
        ' Split_Principal2
        ' 
        Split_Principal2.Dock = DockStyle.Fill
        Split_Principal2.Location = New Point(0, 0)
        Split_Principal2.Name = "Split_Principal2"
        ' 
        ' Split_Principal2.Panel1
        ' 
        Split_Principal2.Panel1.Controls.Add(Split_Previiew_y_Menu)
        ' 
        ' Split_Principal2.Panel2
        ' 
        Split_Principal2.Panel2.Controls.Add(Split_Split_y_Menu_Target)
        Split_Principal2.Panel2MinSize = 500
        Split_Principal2.Size = New Size(1252, 1041)
        Split_Principal2.SplitterDistance = 598
        Split_Principal2.SplitterWidth = 2
        Split_Principal2.TabIndex = 0
        ' 
        ' Split_Previiew_y_Menu
        ' 
        Split_Previiew_y_Menu.Dock = DockStyle.Fill
        Split_Previiew_y_Menu.FixedPanel = FixedPanel.Panel2
        Split_Previiew_y_Menu.IsSplitterFixed = True
        Split_Previiew_y_Menu.Location = New Point(0, 0)
        Split_Previiew_y_Menu.Name = "Split_Previiew_y_Menu"
        Split_Previiew_y_Menu.Orientation = Orientation.Horizontal
        ' 
        ' Split_Previiew_y_Menu.Panel1
        ' 
        Split_Previiew_y_Menu.Panel1.Controls.Add(Split_Preview)
        Split_Previiew_y_Menu.Panel1.Padding = New Padding(3)
        ' 
        ' Split_Previiew_y_Menu.Panel2
        ' 
        Split_Previiew_y_Menu.Panel2.Controls.Add(GroupBox4)
        Split_Previiew_y_Menu.Panel2.Padding = New Padding(3)
        Split_Previiew_y_Menu.Panel2MinSize = 131
        Split_Previiew_y_Menu.Size = New Size(598, 1041)
        Split_Previiew_y_Menu.SplitterDistance = 908
        Split_Previiew_y_Menu.SplitterWidth = 2
        Split_Previiew_y_Menu.TabIndex = 41
        ' 
        ' Split_Preview
        ' 
        Split_Preview.Dock = DockStyle.Fill
        Split_Preview.FixedPanel = FixedPanel.Panel1
        Split_Preview.IsSplitterFixed = True
        Split_Preview.Location = New Point(3, 3)
        Split_Preview.Name = "Split_Preview"
        Split_Preview.Orientation = Orientation.Horizontal
        ' 
        ' Split_Preview.Panel1
        ' 
        Split_Preview.Panel1.Controls.Add(Panel3)
        ' 
        ' Split_Preview.Panel2
        ' 
        Split_Preview.Panel2.Controls.Add(GroupBox1)
        Split_Preview.Size = New Size(592, 902)
        Split_Preview.SplitterDistance = 148
        Split_Preview.SplitterWidth = 2
        Split_Preview.TabIndex = 40
        ' 
        ' Panel3
        ' 
        Panel3.Controls.Add(ComboBoxSize)
        Panel3.Controls.Add(TableLayoutPanel4)
        Panel3.Controls.Add(ProgressBar1)
        Panel3.Controls.Add(Label4)
        Panel3.Controls.Add(ButtonSkeleton)
        Panel3.Controls.Add(ComboBoxPoses)
        Panel3.Controls.Add(TextBox_SourceName)
        Panel3.Controls.Add(Label2)
        Panel3.Controls.Add(Label7)
        Panel3.Controls.Add(ComboBoxPresets)
        Panel3.Dock = DockStyle.Fill
        Panel3.Location = New Point(0, 0)
        Panel3.Name = "Panel3"
        Panel3.Size = New Size(592, 148)
        Panel3.TabIndex = 41
        ' 
        ' ComboBoxSize
        ' 
        ComboBoxSize.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        ComboBoxSize.DropDownStyle = ComboBoxStyle.DropDownList
        ComboBoxSize.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        ComboBoxSize.FormattingEnabled = True
        ComboBoxSize.Items.AddRange(New Object() {"Default", "Big", "Small"})
        ComboBoxSize.Location = New Point(491, 34)
        ComboBoxSize.Name = "ComboBoxSize"
        ComboBoxSize.Size = New Size(98, 29)
        ComboBoxSize.TabIndex = 46
        ToolTip1.SetToolTip(ComboBoxSize, "Select the body size variant used for preview and build operations. (SkyrimSSE Big and Small support)")
        ' 
        ' TableLayoutPanel4
        ' 
        TableLayoutPanel4.ColumnCount = 4
        TableLayoutPanel4.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25F))
        TableLayoutPanel4.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25F))
        TableLayoutPanel4.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25F))
        TableLayoutPanel4.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25F))
        TableLayoutPanel4.Controls.Add(ButtonLightRigSettings, 2, 0)
        TableLayoutPanel4.Controls.Add(ButtonLeftPanel, 0, 0)
        TableLayoutPanel4.Controls.Add(ButtonRightPanel, 3, 0)
        TableLayoutPanel4.Controls.Add(ColorComboBox1, 1, 0)
        TableLayoutPanel4.Dock = DockStyle.Bottom
        TableLayoutPanel4.Location = New Point(0, 114)
        TableLayoutPanel4.Name = "TableLayoutPanel4"
        TableLayoutPanel4.RowCount = 1
        TableLayoutPanel4.RowStyles.Add(New RowStyle(SizeType.Percent, 100F))
        TableLayoutPanel4.Size = New Size(592, 34)
        TableLayoutPanel4.TabIndex = 45
        ' 
        ' ButtonLightRigSettings
        ' 
        ButtonLightRigSettings.Dock = DockStyle.Fill
        ButtonLightRigSettings.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonLightRigSettings.ImageAlign = ContentAlignment.MiddleLeft
        ButtonLightRigSettings.ImageIndex = 16
        ButtonLightRigSettings.ImageList = ImageList1
        ButtonLightRigSettings.Location = New Point(299, 3)
        ButtonLightRigSettings.Name = "ButtonLightRigSettings"
        ButtonLightRigSettings.Size = New Size(142, 28)
        ButtonLightRigSettings.TabIndex = 46
        ButtonLightRigSettings.Text = "Lights"
        ButtonLightRigSettings.TextAlign = ContentAlignment.MiddleRight
        ButtonLightRigSettings.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonLightRigSettings, "Open preview light rig settings.")
        ButtonLightRigSettings.UseVisualStyleBackColor = True
        ' 
        ' ButtonLeftPanel
        ' 
        ButtonLeftPanel.Dock = DockStyle.Fill
        ButtonLeftPanel.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonLeftPanel.ImageAlign = ContentAlignment.MiddleLeft
        ButtonLeftPanel.ImageIndex = 15
        ButtonLeftPanel.ImageList = ImageList1
        ButtonLeftPanel.Location = New Point(3, 3)
        ButtonLeftPanel.Name = "ButtonLeftPanel"
        ButtonLeftPanel.Size = New Size(142, 28)
        ButtonLeftPanel.TabIndex = 43
        ButtonLeftPanel.Text = "Left panel"
        ButtonLeftPanel.TextAlign = ContentAlignment.MiddleLeft
        ButtonLeftPanel.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonLeftPanel, "Collapse or expand the left-side panel.")
        ButtonLeftPanel.UseVisualStyleBackColor = True
        ' 
        ' ButtonRightPanel
        ' 
        ButtonRightPanel.Dock = DockStyle.Fill
        ButtonRightPanel.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonRightPanel.ImageAlign = ContentAlignment.MiddleLeft
        ButtonRightPanel.ImageIndex = 15
        ButtonRightPanel.ImageList = ImageList1
        ButtonRightPanel.Location = New Point(447, 3)
        ButtonRightPanel.Name = "ButtonRightPanel"
        ButtonRightPanel.Size = New Size(142, 28)
        ButtonRightPanel.TabIndex = 44
        ButtonRightPanel.Text = "Right panel"
        ButtonRightPanel.TextAlign = ContentAlignment.MiddleRight
        ButtonRightPanel.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonRightPanel, "Collapse or expand the right-side panel.")
        ButtonRightPanel.UseVisualStyleBackColor = True
        ' 
        ' ColorComboBox1
        ' 
        ColorComboBox1.Dibuja = False
        ColorComboBox1.Dock = DockStyle.Fill
        ColorComboBox1.DrawMode = DrawMode.OwnerDrawFixed
        ColorComboBox1.DropDownStyle = ComboBoxStyle.DropDownList
        ColorComboBox1.FormattingEnabled = True
        ColorComboBox1.Location = New Point(151, 3)
        ColorComboBox1.Name = "ColorComboBox1"
        ColorComboBox1.SelectedColor = Color.Black
        ColorComboBox1.Size = New Size(142, 24)
        ColorComboBox1.TabIndex = 45
        ToolTip1.SetToolTip(ColorComboBox1, "Select the preview background color.")
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        ProgressBar1.Location = New Point(4, 97)
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(584, 11)
        ProgressBar1.TabIndex = 42
        ' 
        ' Label4
        ' 
        Label4.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label4.Location = New Point(0, 67)
        Label4.Name = "Label4"
        Label4.Size = New Size(141, 28)
        Label4.TabIndex = 41
        Label4.Text = "Pose"
        Label4.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' ButtonSkeleton
        ' 
        ButtonSkeleton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        ButtonSkeleton.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold)
        ButtonSkeleton.ImageAlign = ContentAlignment.MiddleLeft
        ButtonSkeleton.ImageIndex = 13
        ButtonSkeleton.ImageList = ImageList1
        ButtonSkeleton.Location = New Point(491, 67)
        ButtonSkeleton.Name = "ButtonSkeleton"
        ButtonSkeleton.Size = New Size(99, 29)
        ButtonSkeleton.TabIndex = 40
        ButtonSkeleton.Text = "Skeleton"
        ButtonSkeleton.TextImageRelation = TextImageRelation.ImageBeforeText
        ToolTip1.SetToolTip(ButtonSkeleton, "Select the skeleton NIF used for preview, posing, and skinning.")
        ButtonSkeleton.UseVisualStyleBackColor = True
        ' 
        ' ComboBoxPoses
        ' 
        ComboBoxPoses.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        ComboBoxPoses.DropDownStyle = ComboBoxStyle.DropDownList
        ComboBoxPoses.Font = New Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        ComboBoxPoses.FormattingEnabled = True
        ComboBoxPoses.Location = New Point(139, 66)
        ComboBoxPoses.Name = "ComboBoxPoses"
        ComboBoxPoses.Size = New Size(346, 29)
        ComboBoxPoses.TabIndex = 38
        ToolTip1.SetToolTip(ComboBoxPoses, "Select the preview pose.")
        ' 
        ' GroupBox4
        ' 
        GroupBox4.Controls.Add(TableLayoutPanel1)
        GroupBox4.Dock = DockStyle.Fill
        GroupBox4.Location = New Point(3, 3)
        GroupBox4.Name = "GroupBox4"
        GroupBox4.Size = New Size(592, 125)
        GroupBox4.TabIndex = 42
        GroupBox4.TabStop = False
        GroupBox4.Text = "File processing and rendering options"
        ' 
        ' Split_Split_y_Menu_Target
        ' 
        Split_Split_y_Menu_Target.Dock = DockStyle.Fill
        Split_Split_y_Menu_Target.FixedPanel = FixedPanel.Panel2
        Split_Split_y_Menu_Target.IsSplitterFixed = True
        Split_Split_y_Menu_Target.Location = New Point(0, 0)
        Split_Split_y_Menu_Target.Name = "Split_Split_y_Menu_Target"
        Split_Split_y_Menu_Target.Orientation = Orientation.Horizontal
        ' 
        ' Split_Split_y_Menu_Target.Panel1
        ' 
        Split_Split_y_Menu_Target.Panel1.Controls.Add(Split_Panel_y_Lista_target)
        Split_Split_y_Menu_Target.Panel1.Padding = New Padding(3)
        ' 
        ' Split_Split_y_Menu_Target.Panel2
        ' 
        Split_Split_y_Menu_Target.Panel2.Controls.Add(GroupBox2)
        Split_Split_y_Menu_Target.Panel2.Padding = New Padding(3)
        Split_Split_y_Menu_Target.Panel2MinSize = 131
        Split_Split_y_Menu_Target.Size = New Size(652, 1041)
        Split_Split_y_Menu_Target.SplitterDistance = 908
        Split_Split_y_Menu_Target.SplitterWidth = 2
        Split_Split_y_Menu_Target.TabIndex = 42
        ' 
        ' Split_Panel_y_Lista_target
        ' 
        Split_Panel_y_Lista_target.Dock = DockStyle.Fill
        Split_Panel_y_Lista_target.FixedPanel = FixedPanel.Panel1
        Split_Panel_y_Lista_target.IsSplitterFixed = True
        Split_Panel_y_Lista_target.Location = New Point(3, 3)
        Split_Panel_y_Lista_target.Name = "Split_Panel_y_Lista_target"
        Split_Panel_y_Lista_target.Orientation = Orientation.Horizontal
        ' 
        ' Split_Panel_y_Lista_target.Panel1
        ' 
        Split_Panel_y_Lista_target.Panel1.Controls.Add(Panel4)
        ' 
        ' Split_Panel_y_Lista_target.Panel2
        ' 
        Split_Panel_y_Lista_target.Panel2.Controls.Add(ListViewTargets)
        Split_Panel_y_Lista_target.Size = New Size(646, 902)
        Split_Panel_y_Lista_target.SplitterDistance = 80
        Split_Panel_y_Lista_target.SplitterWidth = 2
        Split_Panel_y_Lista_target.TabIndex = 41
        ' 
        ' Panel4
        ' 
        Panel4.AutoSizeMode = AutoSizeMode.GrowAndShrink
        Panel4.Controls.Add(Label3)
        Panel4.Controls.Add(ComboboxPacks)
        Panel4.Controls.Add(Label1)
        Panel4.Controls.Add(NewPackButton)
        Panel4.Controls.Add(TextBox_TargetName)
        Panel4.Dock = DockStyle.Top
        Panel4.Location = New Point(0, 0)
        Panel4.Name = "Panel4"
        Panel4.Size = New Size(646, 77)
        Panel4.TabIndex = 40
        ' 
        ' Wardrobe_Manager_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1904, 1041)
        Controls.Add(SplitPrincipal_1)
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        KeyPreview = True
        MinimumSize = New Size(1410, 825)
        Name = "Wardrobe_Manager_Form"
        StartPosition = FormStartPosition.CenterScreen
        Text = "FO4 / SSE - Wardrobe manager"
        WindowState = FormWindowState.Maximized
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        Panel_Preview_Container.ResumeLayout(False)
        TableLayoutPanel1.ResumeLayout(False)
        TableLayoutPanel1.PerformLayout()
        GroupBox2.ResumeLayout(False)
        TableLayoutPanel2.ResumeLayout(False)
        SplitPrincipal_1.Panel1.ResumeLayout(False)
        SplitPrincipal_1.Panel2.ResumeLayout(False)
        CType(SplitPrincipal_1, ComponentModel.ISupportInitialize).EndInit()
        SplitPrincipal_1.ResumeLayout(False)
        Split_Split_y_Menu_Sources.Panel1.ResumeLayout(False)
        Split_Split_y_Menu_Sources.Panel2.ResumeLayout(False)
        CType(Split_Split_y_Menu_Sources, ComponentModel.ISupportInitialize).EndInit()
        Split_Split_y_Menu_Sources.ResumeLayout(False)
        Panel2.ResumeLayout(False)
        Panel2.PerformLayout()
        Split_Panel_y_Lista_Source.Panel2.ResumeLayout(False)
        CType(Split_Panel_y_Lista_Source, ComponentModel.ISupportInitialize).EndInit()
        Split_Panel_y_Lista_Source.ResumeLayout(False)
        GroupBox3.ResumeLayout(False)
        TableLayoutPanel3.ResumeLayout(False)
        Split_Principal2.Panel1.ResumeLayout(False)
        Split_Principal2.Panel2.ResumeLayout(False)
        CType(Split_Principal2, ComponentModel.ISupportInitialize).EndInit()
        Split_Principal2.ResumeLayout(False)
        Split_Previiew_y_Menu.Panel1.ResumeLayout(False)
        Split_Previiew_y_Menu.Panel2.ResumeLayout(False)
        CType(Split_Previiew_y_Menu, ComponentModel.ISupportInitialize).EndInit()
        Split_Previiew_y_Menu.ResumeLayout(False)
        Split_Preview.Panel1.ResumeLayout(False)
        Split_Preview.Panel2.ResumeLayout(False)
        CType(Split_Preview, ComponentModel.ISupportInitialize).EndInit()
        Split_Preview.ResumeLayout(False)
        Panel3.ResumeLayout(False)
        Panel3.PerformLayout()
        TableLayoutPanel4.ResumeLayout(False)
        GroupBox4.ResumeLayout(False)
        Split_Split_y_Menu_Target.Panel1.ResumeLayout(False)
        Split_Split_y_Menu_Target.Panel2.ResumeLayout(False)
        CType(Split_Split_y_Menu_Target, ComponentModel.ISupportInitialize).EndInit()
        Split_Split_y_Menu_Target.ResumeLayout(False)
        Split_Panel_y_Lista_target.Panel1.ResumeLayout(False)
        Split_Panel_y_Lista_target.Panel2.ResumeLayout(False)
        CType(Split_Panel_y_Lista_target, ComponentModel.ISupportInitialize).EndInit()
        Split_Panel_y_Lista_target.ResumeLayout(False)
        Panel4.ResumeLayout(False)
        Panel4.PerformLayout()
        ResumeLayout(False)
    End Sub
    Friend WithEvents ComboboxPacks As ComboBox
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents TextBox_SourceName As TextBox
    Friend WithEvents ImageList1 As ImageList
    Friend WithEvents ListView2 As ListView
    Friend WithEvents Shapecol As ColumnHeader
    Friend WithEvents Datasources As ColumnHeader
    Friend WithEvents Exclude_Reference_Checkbox As CheckBox
    Friend WithEvents Ovewrite_DataFiles As CheckBox
    Friend WithEvents NewPackButton As Button
    Friend WithEvents ListViewTargets As ListView
    Friend WithEvents ColumnHeader3 As ColumnHeader
    Friend WithEvents ColumnHeader4 As ColumnHeader
    Friend WithEvents EditTargetButton As Button
    Friend WithEvents Auto_Move_Check As CheckBox
    Friend WithEvents Local As ColumnHeader
    Friend WithEvents ColumnHeader5 As ColumnHeader
    Friend WithEvents ShapeTypeCol As ColumnHeader
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents RadioButton2 As RadioButton
    Friend WithEvents RadioButton1 As RadioButton
    Friend WithEvents CloneMaterialsCheck As CheckBox
    Friend WithEvents Label6 As Label
    Friend WithEvents TextBox_TargetName As TextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents GroupBox2 As GroupBox
    Friend WithEvents ButtonDelete As Button
    Friend WithEvents OutputDirChangeCheck As CheckBox
    Friend WithEvents ButtonDataSheetSelected As Button
    Friend WithEvents ButtonPreviewSelected As Button
    Friend WithEvents Physics_Label As Label
    Friend WithEvents PhysicsCheckbox As CheckBox
    Friend WithEvents Label7 As Label
    Friend WithEvents ComboBoxPresets As ComboBox
    Friend WithEvents RadioButton3 As RadioButton
    Friend WithEvents MergeIntoTargetButton As Button
    Friend WithEvents ExtractSingleButton As Button
    Friend WithEvents RenameButton As Button
    Friend WithEvents CloneButton As Button
    Friend WithEvents SplitPrincipal_1 As SplitContainer
    Friend WithEvents Split_Principal2 As SplitContainer
    Friend WithEvents Split_Preview As SplitContainer
    Friend WithEvents Panel3 As Panel
    Friend WithEvents TableLayoutPanel1 As TableLayoutPanel
    Friend WithEvents Panel4 As Panel
    Friend WithEvents Split_Panel_y_Lista_target As SplitContainer
    Friend WithEvents Split_Split_y_Menu_Target As SplitContainer
    Friend WithEvents Split_Previiew_y_Menu As SplitContainer
    Friend WithEvents GroupBox4 As GroupBox
    Friend WithEvents Panel_Preview_Container As Panel
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents TableLayoutPanel2 As TableLayoutPanel
    Friend WithEvents Split_Split_y_Menu_Sources As SplitContainer
    Friend WithEvents Panel2 As Panel
    Friend WithEvents TextBox2 As TextBox
    Friend WithEvents ShowCollectionsCheck As CheckBox
    Friend WithEvents ShowCBBECheck As CheckBox
    Friend WithEvents RefreshButton As Button
    Friend WithEvents Label5 As Label
    Friend WithEvents DeepAnalize_check As CheckBox
    Friend WithEvents CheckShowpacks As CheckBox
    Friend WithEvents Split_Panel_y_Lista_Source As SplitContainer
    Friend WithEvents ListViewSources As ListView
    Friend WithEvents ColumnHeader1 As ColumnHeader
    Friend WithEvents ColumnHeader2 As ColumnHeader
    Friend WithEvents GroupBox3 As GroupBox
    Friend WithEvents TableLayoutPanel3 As TableLayoutPanel
    Friend WithEvents CopytoPackButton As Button
    Friend WithEvents EditButton As Button
    Friend WithEvents MovetoDiscardedButton As Button
    Friend WithEvents MoveToProcessedButton As Button
    Friend WithEvents MergeButton As Button
    Friend WithEvents MergeInSelectedButton As Button
    Friend WithEvents ButtonBuildSingles As Button
    Friend WithEvents ButtonEditInternally As Button
    Friend WithEvents ButtonSourceInternalEdit As Button
    Friend WithEvents ButtonBuildFullPack As Button
    Friend WithEvents ButtonOpenConfig As Button
    Friend WithEvents ButtonDeleteSource As Button
    Friend WithEvents SingleBoneCheck As CheckBox
    Friend WithEvents ButtonCreateFromNif As Button
    Friend WithEvents ButtonSkeleton As Button
    Friend WithEvents ComboBoxPoses As ComboBox
    Friend WithEvents Label4 As Label
    Friend WithEvents RecalculateNormalsCheck As CheckBox
    Friend WithEvents ButtonRightPanel As Button
    Friend WithEvents ButtonLeftPanel As Button
    Friend WithEvents TableLayoutPanel4 As TableLayoutPanel
    Friend WithEvents ColorComboBox1 As ColorComboBox
    Friend WithEvents ColumnHeader7 As ColumnHeader
    Friend WithEvents ColumnHeader6 As ColumnHeader
    Friend WithEvents ButtonLightRigSettings As Button
    Friend WithEvents CheckBoxReloadDict As CheckBox
    Friend WithEvents ComboBoxSize As ComboBox
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents CheckBoxFixUncloned As CheckBox
End Class
