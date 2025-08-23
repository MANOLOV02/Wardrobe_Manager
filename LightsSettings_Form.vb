Imports Wardrobe_Manager.Config_App
Partial Public Class LightRigForm
    Inherits Form

    Private ReadOnly cam As OrbitCamera


    Public Sub New()

        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        ' Rango de sliders: 0..1.50
        For Each tb In New TrackBar() {tbKey, tbFillL, tbFillR, tbBack, tambient}
            tb.Minimum = 0 : tb.Maximum = 150 : tb.TickFrequency = 10
        Next

        CargarValoresIniciales()
        AddHandlers()
    End Sub

    ' ====== Valores recomendados (coinciden con tu rig) ======
    Private Sub CargarValoresIniciales()
        ' Strengths
        tbKey.Value = CInt(Config_App.Current.Setting_Lightrig.DirectL.Strength * 100)
        tbFillL.Value = CInt(Config_App.Current.Setting_Lightrig.FillLight_1.Strength * 100)
        tbFillR.Value = CInt(Config_App.Current.Setting_Lightrig.FillLight_2.Strength * 100)
        tbBack.Value = CInt(Config_App.Current.Setting_Lightrig.BackLight.Strength * 100)
        tambient.Value = CInt(Config_App.Current.Setting_Lightrig.Ambient * 100)

        ' Frontal (Key): Forward=+1
        nudK_U.Value = Config_App.Current.Setting_Lightrig.DirectL.Up : nudK_D.Value = Config_App.Current.Setting_Lightrig.DirectL.Down
        nudK_L.Value = Config_App.Current.Setting_Lightrig.DirectL.Left : nudK_R.Value = Config_App.Current.Setting_Lightrig.DirectL.Right
        nudK_F.Value = Config_App.Current.Setting_Lightrig.DirectL.Forward : nudK_B.Value = Config_App.Current.Setting_Lightrig.DirectL.Back

        ' Fill Izquierda: vector NEGADO de (0.4*up -0.6*right -0.7*forward)
        ' w = -v => Up=-0.4, Right=+0.6, Forward=+0.7
        nudL_U.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Up : nudL_D.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Down
        nudL_L.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Left : nudL_R.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Right
        nudL_F.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Forward : nudL_B.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Back

        ' Fill Derecha: vector NEGADO de (0.4*up +0.6*right -0.7*forward)
        ' w = -v => Up=-0.4, Right=-0.6, Forward=+0.7
        nudR_U.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Up : nudR_D.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Down
        nudR_L.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Left : nudR_R.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Right
        nudR_F.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Forward : nudR_B.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Back

        ' Contraluz (Back): 0.6*up -0.2*right +1.0*forward
        nudB_U.Value = Config_App.Current.Setting_Lightrig.BackLight.Up : nudB_D.Value = Config_App.Current.Setting_Lightrig.BackLight.Down
        nudB_L.Value = Config_App.Current.Setting_Lightrig.BackLight.Left : nudB_R.Value = Config_App.Current.Setting_Lightrig.BackLight.Right
        nudB_F.Value = Config_App.Current.Setting_Lightrig.BackLight.Forward : nudB_B.Value = Config_App.Current.Setting_Lightrig.BackLight.Back


        ActualizarEtiquetas()
        VolcarUIenModelo()
    End Sub

    Private Sub AddHandlers()
        AddHandler tbKey.Scroll, AddressOf ActualizarEtiquetas
        AddHandler tbFillL.Scroll, AddressOf ActualizarEtiquetas
        AddHandler tbFillR.Scroll, AddressOf ActualizarEtiquetas
        AddHandler tbBack.Scroll, AddressOf ActualizarEtiquetas
        AddHandler tbBack.Scroll, AddressOf ActualizarEtiquetas
        AddHandler tambient.Scroll, AddressOf ActualizarEtiquetas

        Dim nudChanged As EventHandler = Sub(sender, e) VolcarUIenModelo()

        For Each nud In New NumericUpDown() {
            nudK_U, nudK_D, nudK_L, nudK_R, nudK_F, nudK_B,
            nudL_U, nudL_D, nudL_L, nudL_R, nudL_F, nudL_B,
            nudR_U, nudR_D, nudR_L, nudR_R, nudR_F, nudR_B,
            nudB_U, nudB_D, nudB_L, nudB_R, nudB_F, nudB_B}
            AddHandler nud.ValueChanged, nudChanged
        Next
    End Sub

    Private Sub ActualizarEtiquetas()
        lblKeyStrength.Text = $"Strength {tbKey.Value / 100.0:F2}"
        lblFillLStrength.Text = $"Strength {tbFillL.Value / 100.0:F2}"
        lblFillRStrength.Text = $"Strength {tbFillR.Value / 100.0:F2}"
        lblBackStrength.Text = $"Strength {tbBack.Value / 100.0:F2}"
        lblambient.Text = $"ambient {tambient.Value / 100.0:F2}"
        VolcarUIenModelo()
    End Sub

    Private _preventchanges As Boolean = False
    ' ====== Transferencia UI -> Modelo ======
    Private Sub VolcarUIenModelo()
        If _PreventChanges = False Then
            Dim Lrig = New LightsRig_struct With {.Ambient = CSng(tambient.Value / 100.0F),
            .DirectL = New LightData_struct With {.Strength = CSng(tbKey.Value / 100.0F), .Left = CSng(nudK_L.Value), .Right = CSng(nudK_R.Value), .Back = CSng(nudK_B.Value), .Down = CSng(nudK_D.Value), .Forward = CSng(nudK_F.Value), .Up = CSng(nudK_U.Value)},
            .FillLight_1 = New LightData_struct With {.Strength = CSng(tbFillL.Value / 100.0F), .Left = CSng(nudL_L.Value), .Right = CSng(nudL_R.Value), .Back = CSng(nudL_B.Value), .Down = CSng(nudL_D.Value), .Forward = CSng(nudL_F.Value), .Up = CSng(nudL_U.Value)},
            .FillLight_2 = New LightData_struct With {.Strength = CSng(tbFillR.Value / 100.0F), .Left = CSng(nudR_L.Value), .Right = CSng(nudR_R.Value), .Back = CSng(nudR_B.Value), .Down = CSng(nudR_D.Value), .Forward = CSng(nudR_F.Value), .Up = CSng(nudR_U.Value)},
            .BackLight = New LightData_struct With {.Strength = CSng(tbBack.Value / 100.0F), .Left = CSng(nudB_L.Value), .Right = CSng(nudB_R.Value), .Back = CSng(nudB_B.Value), .Down = CSng(nudB_D.Value), .Forward = CSng(nudB_F.Value), .Up = CSng(nudB_U.Value)}}
            Config_App.Current.Setting_Lightrig = Lrig
            If Not IsNothing(Owner) Then
                CType(Me.Owner, Wardrobe_Manager_Form).preview_Control.updateRequired = True
                CType(Me.Owner, Wardrobe_Manager_Form).preview_Control.Update()
            End If
        End If
    End Sub

    Private Sub BtnReset_Click(sender As Object, e As EventArgs) Handles btnReset.Click
        _preventchanges = True
        Config_App.Current.Setting_Lightrig = Default_Lights()
        CargarValoresIniciales()
        _preventchanges = False
        VolcarUIenModelo()
    End Sub
End Class
