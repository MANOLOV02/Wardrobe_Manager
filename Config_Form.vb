Imports System.ComponentModel

Public Class Config_Form
    Private initialgame As Config_App.Game_Enum
    Public Sub New()
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        TextBox1.Text = Config_App.Current.FO4ExePath
        TextBox2.Text = Config_App.Current.BSExePath
        TextBox3.Text = Config_App.Current.OSExePath
        TextBox4.Text = Config_App.Current.SkeletonPath
        ComboBoxGame.SelectedIndex = Config_App.Current.Game
        initialgame = Config_App.Current.Game
        Setea_Render_Options()
        Button8.Enabled = IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub
    Private Sub Setea_Render_Options()
        Try
            RecalculateNormalsCheck.Checked = Config_App.Current.Setting_RecalculateNormals
            SingleBoneCheck.Checked = Config_App.Current.Setting_SingleBoneSkinning

            NormalsForceOrthogonal.Checked = Config_App.Current.Setting_TBN.ForceOrthogonalBitangent
            NormalsRepairNan.Checked = Config_App.Current.Setting_TBN.RepairNaNs
            CheckBoxWelding.Checked = Config_App.Current.Setting_TBN.EnableWelding
            NumericUpDownPositionEps.Value = Config_App.Current.Setting_TBN.EpsilonPos
            NumericUpDownUVEps.Value = Config_App.Current.Setting_TBN.EpsilonUV
            NormalsNormalize.Checked = Config_App.Current.Setting_TBN.NormalizeOutputs
            RadioButtonWeldpsonly.Checked = Config_App.Current.Setting_TBN.WeldByPositionOnly
            RadioButtonWeldboth.Checked = Not Config_App.Current.Setting_TBN.WeldByPositionOnly
            NumericUpDownWeldEpspos.Value = Config_App.Current.Setting_TBN.WeldUVEpsilon
            NumericUpDownWeldEpsUv.Value = Config_App.Current.Setting_TBN.WeldPosEpsilon
            RadioButtonByArea.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AreaOnly)
            RadioButtonByangles.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AngleOnly)
            RadioButtoncombined.Checked = (Config_App.Current.Setting_TBN.WeightMode = RecalcTBN.NormalWeightMode.AreaTimesAngle)
            CheckBoxanglereset.Checked = Config_App.Current.Settings_Camara.ResetAngles
            CheckBoxzoomreset.Checked = Config_App.Current.Settings_Camara.ResetZoom
            RadioButtonBSEngine.Checked = (Config_App.Current.Settings_Build.OwnEngine = False)
            RadioButtonWMEngine.Checked = (Config_App.Current.Settings_Build.OwnEngine = True)
            CheckBoxBuildHH.Checked = Config_App.Current.Settings_Build.SaveHHS
            CheckBoxBuildTri.Checked = Config_App.Current.Settings_Build.SaveTri
            CheckBoxDeletewithProject.Checked = Config_App.Current.Settings_Build.DeleteWithProject
            CheckBoxDeleteBefore.Checked = Config_App.Current.Settings_Build.DeleteUnbuilt
            CheckBoxLMReseteachBuild.Checked = Config_App.Current.Settings_Build.ResetSlidersEachBuild
            CheckBoxLMASkipManoloFixes.Checked = Config_App.Current.Settings_Build.SkipManoloFixMorphs
            CheckBoxLMAddAditionals.Checked = Config_App.Current.Settings_Build.AddAddintionalSliders
            CheckBoxIgnorePrevent.Checked = Config_App.Current.Settings_Build.IgnorePreventri
            CheckBoxBuildInPose.Checked = Config_App.Current.Settings_Build.BuildInPose
            CheckBoxFreeze.Checked = Config_App.Current.Settings_Camara.FreezeCamera
            CheckBoxweightignore.Checked = Config_App.Current.Settings_Build.IgnoreWeightsFlags
            RadioButtonAllwaysWeight.Checked = Config_App.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Checked = Not Config_App.Current.Settings_Build.ForceWeights
            RadioButtonNeverWeights.Enabled = Config_App.Current.Settings_Build.IgnoreWeightsFlags AndAlso Config_App.Current.Settings_Build.OwnEngine
            RadioButtonAllwaysWeight.Enabled = Config_App.Current.Settings_Build.IgnoreWeightsFlags AndAlso Config_App.Current.Settings_Build.OwnEngine
            CheckBoxweightignore.Enabled = Config_App.Current.Settings_Build.OwnEngine = True
        Catch ex As Exception
            'MsgBox("Render options error", vbCritical, "Reset render options")
        End Try

    End Sub
    Private Sub Graba_Render_Options()
        Dim opts = New RecalcTBN.TBNOptions With {
          .ForceOrthogonalBitangent = NormalsForceOrthogonal.Checked,
          .EnableWelding = CheckBoxWelding.Checked,
          .EpsilonUV = NumericUpDownUVEps.Value,
          .EpsilonPos = NumericUpDownPositionEps.Value,
          .NormalizeOutputs = NormalsNormalize.Checked,
          .RepairNaNs = NormalsRepairNan.Checked,
          .WeightMode = IIf(RadioButtoncombined.Checked, RecalcTBN.NormalWeightMode.AreaTimesAngle, IIf(RadioButtonByangles.Checked, RecalcTBN.NormalWeightMode.AngleOnly, RecalcTBN.NormalWeightMode.AreaOnly)),
          .WeldByPositionOnly = RadioButtonWeldpsonly.Checked,
          .WeldPosEpsilon = NumericUpDownWeldEpspos.Value,
          .WeldUVEpsilon = NumericUpDownWeldEpsUv.Value
          }

        Dim buildSet = New Config_App.BuildSettings With {
            .DeleteUnbuilt = CheckBoxDeleteBefore.Checked,
            .DeleteWithProject = CheckBoxDeletewithProject.Checked,
            .OwnEngine = Not RadioButtonBSEngine.Checked,
            .SaveHHS = CheckBoxBuildHH.Checked,
            .SaveTri = CheckBoxBuildTri.Checked,
            .ResetSlidersEachBuild = CheckBoxLMReseteachBuild.Checked,
            .SkipManoloFixMorphs = CheckBoxLMASkipManoloFixes.Checked,
            .AddAddintionalSliders = CheckBoxLMAddAditionals.Checked,
            .IgnorePreventri = CheckBoxIgnorePrevent.Checked,
            .BuildInPose = CheckBoxBuildInPose.Checked,
            .IgnoreWeightsFlags = CheckBoxweightignore.Checked,
         .ForceWeights = RadioButtonAllwaysWeight.Checked
                    }
        Dim cam = New Config_App.CameraSettings With {.ResetAngles = CheckBoxanglereset.Checked, .ResetZoom = CheckBoxzoomreset.Checked, .FreezeCamera = CheckBoxFreeze.Checked}
        Config_App.Current.Settings_Camara = cam
        Config_App.Current.Settings_Build = buildSet
        Config_App.Current.Setting_TBN = opts
        Config_App.Current.Setting_RecalculateNormals = RecalculateNormalsCheck.Checked
        Config_App.Current.Setting_SingleBoneSkinning = SingleBoneCheck.Checked
    End Sub

    Private Sub Config_Form_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Check_Folders()
    End Sub

    Private Function Check_Folders() As Boolean
        If Config_App.Check_FOFolder = False Then Label1.ImageIndex = 1 Else Label1.ImageIndex = 0
        If Config_App.Check_BSFolder = False Then Label2.ImageIndex = 1 Else Label2.ImageIndex = 0
        If Config_App.Check_OsFolder = False Then Label3.ImageIndex = 1 Else Label3.ImageIndex = 0
        If Config_App.Check_Skeleton = False Then Label6.ImageIndex = 1 Else Label6.ImageIndex = 0

        Dim folderschek As Boolean = Config_App.Check_FOFolder And Config_App.Check_BSFolder And Config_App.Check_OsFolder
        If folderschek Then
            ListView1.Items.Clear()
            Dim oldbsa = Config_App.Current.BSAFiles.ToList
            Dim oldchecks = Config_App.Current.BSAFiles_Clonables.ToList
            Config_App.Current.BSAFiles.Clear()
            Config_App.Current.BSAFiles_Clonables.Clear()
            Dim idx2 As Integer = 0
            For Each fil In FilesDictionary_class.EnumerateFilesWithSymlinkSupport(Config_App.Current.FO4EDataPath, "*.ba2;*.bsa", False).Order
                Dim it As New ListViewItem(IO.Path.GetFileName(fil))
                Dim idx = oldbsa.FindIndex(Function(s) String.Equals(s, IO.Path.GetFileName(fil), StringComparison.OrdinalIgnoreCase))
                Config_App.Current.BSAFiles.Add(IO.Path.GetFileName(fil))
                If idx <> -1 Then
                    Config_App.Current.BSAFiles_Clonables.Add(oldchecks(idx))
                Else
                    Config_App.Current.BSAFiles_Clonables.Add(False)
                End If
                ListView1.Items.Add(it)
                it.Tag = idx2
                it.Checked = Config_App.Current.BSAFiles_Clonables(idx2)
                idx2 += 1
            Next

        End If
        Return folderschek
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Config_App.Current.FO4ExePath = Search_exe(IO.Path.GetDirectoryName(TextBox1.Text))
        TextBox1.Text = Config_App.Current.FO4ExePath
        Dim exe = IO.Path.GetFileName(Config_App.Current.FO4ExePath)
        If exe.ToLower.Contains("fallout4", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Fallout4
        If exe.ToLower.Contains("skyrimse", StringComparison.CurrentCultureIgnoreCase) AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Skyrim Then ComboBoxGame.SelectedIndex = Config_App.Game_Enum.Skyrim

        Dim pathS As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "Tools", "CalienteTools")

        If Config_App.Check_FOFolder And (TextBox2.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or Config_App.Check_BSFolder = False) Then
            If Environment.Is64BitOperatingSystem Then
                TextBox2.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\BodySlide x64.exe")
            Else
                TextBox2.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\BodySlide.exe")
            End If
            Config_App.Current.BSExePath = TextBox2.Text
        End If
        If Config_App.Check_FOFolder And (TextBox3.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or Config_App.Check_OsFolder = False) Then
            If Environment.Is64BitOperatingSystem Then
                TextBox3.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\OutfitStudio x64.exe")
            Else
                TextBox3.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\OutfitStudio.exe")
            End If
            Config_App.Current.OSExePath = TextBox3.Text
        End If
        If Config_App.Check_FOFolder And (TextBox4.Text.Contains(Config_App.Current.FO4EDataPath, StringComparison.OrdinalIgnoreCase) = False Or Config_App.Check_Skeleton = False) Then
            Dim skel As String = IIf(Config_App.Current.Game = Config_App.Game_Enum.Fallout4, "res\skeleton_fo4.nif", "res\skeleton_female_sse.nif")
            TextBox4.Text = IO.Path.Combine(IO.Path.GetDirectoryName(TextBox1.Text), "Data\" + pathS + "\Bodyslide\" + skel)
            Config_App.Current.SkeletonPath = TextBox4.Text
            Skeleton_Class.Skeleton = Nothing
        End If
        Check_Folders()
    End Sub

    Private Shared Function Search_exe(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an executable file"
            dlg.Filter = "EXE files (*.exe)|*.exe"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Shared Function Search_Nif(initalpath As String) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Select an skeleton nif"
            dlg.Filter = "NIF files (*.nif)|*.nif"
            dlg.CheckFileExists = True
            dlg.CheckPathExists = True
            dlg.Multiselect = False
            dlg.InitialDirectory = initalpath
            If dlg.ShowDialog() = DialogResult.OK Then
                Return dlg.FileName
            Else
                Return String.Empty
            End If
        End Using
    End Function
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Config_App.Current.BSExePath = Search_exe(IO.Path.GetDirectoryName(TextBox2.Text))
        TextBox2.Text = Config_App.Current.BSExePath
        Check_Folders()
    End Sub
    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Config_App.Current.OSExePath = Search_exe(IO.Path.GetDirectoryName(TextBox3.Text))
        TextBox3.Text = Config_App.Current.OSExePath
        Check_Folders()
    End Sub

    Private Sub ListView1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListView1.SelectedIndexChanged

    End Sub

    Private Sub ListView1_ItemCheck(sender As Object, e As ItemCheckEventArgs) Handles ListView1.ItemCheck
        If CInt(ListView1.Items(e.Index).Tag) < -1 Or CInt(ListView1.Items(e.Index).Tag) > Config_App.Current.BSAFiles_Clonables.Count - 1 Then
            Debugger.Break()
        End If
        Config_App.Current.BSAFiles_Clonables(ListView1.Items(e.Index).Tag) = IIf(e.NewValue = CheckState.Checked, True, False)
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Config_App.Current.SkeletonPath = Search_Nif(IO.Path.GetDirectoryName(TextBox4.Text))
        TextBox4.Text = Config_App.Current.SkeletonPath
        Skeleton_Class.Skeleton = Nothing
        Check_Folders()
    End Sub


    Private Sub Button5_Click(sender As Object, e As EventArgs)
        SingleBoneCheck.Checked = False
        RecalculateNormalsCheck.Checked = True
    End Sub
    Private actualizar = False
    Private Sub Config_Form_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        actualizar = True
    End Sub

    Private Sub Config_Form_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If initialgame <> Config_App.Current.Game Then
            If Not IsNothing(Me.Owner) AndAlso Me.Owner.GetType Is GetType(Wardrobe_Manager_Form) Then
                If Not IsNothing(CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict) Then
                    CType(Owner, Wardrobe_Manager_Form).CheckBoxReloadDict.Checked = True
                End If
            End If
        End If
        Graba_Render_Options()
    End Sub

    Private Sub Button5_Click_1(sender As Object, e As EventArgs) Handles Button5.Click
        Config_App.Current.Setting_SingleBoneSkinning = False
        Config_App.Current.Setting_RecalculateNormals = True
        Config_App.Current.Setting_TBN = RecalcTBN.DefaultTBNOptions
        Config_App.Current.Settings_Camara = Config_App.Default_CameraSettings
        Setea_Render_Options()
    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click

        Graba_Render_Options()
        If Not IsNothing(Me.Owner) AndAlso Me.Owner.GetType Is GetType(Wardrobe_Manager_Form) Then
            If Not IsNothing(CType(Owner, Wardrobe_Manager_Form).preview_Control) Then
                CType(Owner, Wardrobe_Manager_Form).preview_Control.Model.Clean(False)
                CType(Owner, Wardrobe_Manager_Form).preview_Control.Model.RecalculateNormals = RecalculateNormalsCheck.Checked
                CType(Owner, Wardrobe_Manager_Form).preview_Control.Model.SingleBoneSkinning = SingleBoneCheck.Checked
                CType(Owner, Wardrobe_Manager_Form).preview_Control.Update_Render_LastLoaded(True)
            End If
        End If
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click
        Config_App.Current.Settings_Build = Config_App.Default_Build_Settings
        Setea_Render_Options()
    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        Try
            If IO.File.Exists(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders) Then IO.File.Delete(Wardrobe_Manager_Form.Directorios.LooksMenuWMSliders)
            Button8.Enabled = False
        Catch ex As Exception
            Debugger.Break()
        End Try

    End Sub

    Private Sub CheckBoxBuildTri_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxBuildTri.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonWMEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonWMEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub RadioButtonBSEngine_CheckedChanged(sender As Object, e As EventArgs) Handles RadioButtonBSEngine.CheckedChanged
        GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        CheckBoxBuildInPose.Enabled = RadioButtonWMEngine.Checked
        CheckBoxIgnorePrevent.Enabled = CheckBoxBuildTri.Checked AndAlso RadioButtonWMEngine.Checked
        CheckBoxweightignore.Enabled = RadioButtonWMEngine.Checked
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
    End Sub

    Private Sub TabPage3_Click(sender As Object, e As EventArgs) Handles TabPage3.Click

    End Sub

    Private Sub CheckBoxzoomreset_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxzoomreset.CheckedChanged

    End Sub

    Private Sub CheckBoxFreeze_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxFreeze.CheckedChanged
        CheckBoxzoomreset.Enabled = Not CheckBoxFreeze.Checked
        CheckBoxanglereset.Enabled = Not CheckBoxFreeze.Checked
    End Sub

    Private Sub ComboBoxGame_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBoxGame.SelectedIndexChanged
        If ComboBoxGame.SelectedIndex <> -1 Then
            Config_App.Current.Game = ComboBoxGame.SelectedIndex
            GroupBoxweights.Enabled = ComboBoxGame.SelectedIndex <> 0
            GroupBoxLooksmenu.Enabled = CheckBoxBuildTri.Checked And RadioButtonWMEngine.Checked AndAlso ComboBoxGame.SelectedIndex = 0
        End If
    End Sub

    Private Sub CheckBoxweightignore_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxweightignore.CheckedChanged
        RadioButtonNeverWeights.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked
        RadioButtonAllwaysWeight.Enabled = CheckBoxweightignore.Checked AndAlso RadioButtonWMEngine.Checked

    End Sub
End Class