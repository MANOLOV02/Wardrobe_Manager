' Version Uploaded of Wardrobe 2.1.3
Public Class GrayScaleTrackbar
    Inherits PictureBox

    Public Property Minimum As Integer = 0
    Public Property Maximum As Integer = 100

    Private _value As Integer = 50
    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(val As Integer)
            _value = Math.Max(Minimum, Math.Min(Maximum, val))
            Invalidate()
        End Set
    End Property

    Public Event ValueChanged As EventHandler

    Private _dragging As Boolean = False

    Public Sub New()
        DoubleBuffered = True
        SetStyle(ControlStyles.ResizeRedraw, True)
        Cursor = Cursors.Hand
        BackColor = Color.LightGray
    End Sub
    Public Function Getvalue(Percentaje As Single) As Integer
        If IsNothing(BackgroundImage) Then Return Maximum / 2
        Return BackgroundImage.Width * Percentaje
    End Function
    Public Function Setvalue() As Single
        If IsNothing(BackgroundImage) Then Return 0.5
        Return Value / BackgroundImage.Width
    End Function
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

        ' Background: image or BackColor
        If BackgroundImage IsNot Nothing Then
            g.DrawImage(BackgroundImage, Me.ClientRectangle)
        Else
            Using b As New SolidBrush(Me.BackColor)
                g.FillRectangle(b, Me.ClientRectangle)
            End Using
        End If

        ' --- Draw triangle thumb ---
        Dim percent As Double = (Value - Minimum) / (Maximum - Minimum)
        Dim x As Integer = CInt(percent * (Width - 1))

        Dim triWidth As Integer = 12
        Dim triHeight As Integer = 8
        Dim yBottom As Integer = Height - 1

        Dim p1 As New Point(x, yBottom - triHeight)
        Dim p2 As New Point(x - triWidth \ 2, yBottom)
        Dim p3 As New Point(x + triWidth \ 2, yBottom)
        Dim triangle() As Point = {p1, p2, p3}

        ' Shadow
        Dim shadowOffset As Integer = 2
        Dim shadowTriangle() As Point = {
            New Point(p1.X + shadowOffset, p1.Y + shadowOffset),
            New Point(p2.X + shadowOffset, p2.Y + shadowOffset),
            New Point(p3.X + shadowOffset, p3.Y + shadowOffset)
        }
        g.FillPolygon(New SolidBrush(Color.FromArgb(60, Color.Black)), shadowTriangle)

        ' Fill: gray if disabled, otherwise gradient blue
        If Not Me.Enabled Then
            g.FillPolygon(Brushes.Gray, triangle)
        Else
            Using brush As New Drawing2D.LinearGradientBrush(
                p1, p3, Color.FromArgb(80, 140, 255), Color.RoyalBlue)
                g.FillPolygon(brush, triangle)
            End Using
        End If

        g.DrawPolygon(Pens.DarkBlue, triangle)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If Me.Enabled Then
            _dragging = True
            UpdateValueFromMouse(e.X)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If Me.Enabled AndAlso _dragging Then
            UpdateValueFromMouse(e.X)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        _dragging = False
    End Sub

    Private Sub UpdateValueFromMouse(mouseX As Integer)
        Dim percent As Double = Math.Min(1.0, Math.Max(0.0, mouseX / (Width - 1)))
        Value = CInt(Minimum + percent * (Maximum - Minimum))
        RaiseEvent ValueChanged(Me, EventArgs.Empty)
    End Sub

End Class
