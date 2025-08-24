Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports Material_Editor.BaseMaterialFile
Imports OpenTK.GLControl
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports OpenTK.Windowing.Common
Imports OpenTK.Windowing.Common.Input
Imports Windows.Win32.System.Diagnostics


Public Class TextOverlayRenderer
    Private vao As Integer
    Private vbo As Integer
    Private shaderProgram As Integer
    Private textureID As Integer
    Private textWidth As Integer
    Private textHeight As Integer
    Private ReadOnly Labels As New Dictionary(Of String, Bitmap)

    Public Sub New()
        CompileShaders()
        InitBuffers()
        textureID = GL.GenTexture()
    End Sub

    Public Sub SetText(text As String, Optional fontSize As Integer = 32, Optional fontName As String = "Arial")
        Dim bmp As Bitmap
        If Labels.ContainsKey(text) = True Then
            bmp = Labels(text)
        Else
            bmp = GenerateTextBitmap(text, fontSize, fontName)
            Labels.Add(text, bmp)
        End If
        textWidth = bmp.Width
        textHeight = bmp.Height
        Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        bmp.UnlockBits(data)
    End Sub

    Public Sub RenderCentered(screenWidth As Integer, screenHeight As Integer)
        If textureID = 0 OrElse textWidth = 0 OrElse textHeight = 0 Then Return

        Dim x = (screenWidth - textWidth) \ 2
        Dim y = (screenHeight - textHeight) \ 2
        RenderAt(x, y, textWidth, textHeight, screenWidth, screenHeight)
    End Sub

    Public Sub RenderAt(x As Integer, y As Integer, width As Integer, height As Integer, screenW As Integer, screenH As Integer)
        If shaderProgram = 0 OrElse textureID = 0 Then Exit Sub

        GL.Disable(EnableCap.DepthTest)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.Blend)
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)

        GL.UseProgram(shaderProgram)

        Dim locSize = GL.GetUniformLocation(shaderProgram, "uSize")
        Dim locPos = GL.GetUniformLocation(shaderProgram, "uPosition")
        Dim locScreen = GL.GetUniformLocation(shaderProgram, "uScreenSize")

        GL.Uniform2(locSize, CSng(width), CSng(height))
        GL.Uniform2(locPos, CSng(x), CSng(y))
        GL.Uniform2(locScreen, CSng(screenW), CSng(screenH))

        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        GL.Uniform1(GL.GetUniformLocation(shaderProgram, "uTexture"), 0)

        GL.BindVertexArray(vao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        GL.BindVertexArray(0)

        GL.UseProgram(0)
        GL.Enable(EnableCap.DepthTest)
    End Sub

    Private Sub InitBuffers()
        vao = GL.GenVertexArray()
        vbo = GL.GenBuffer()

        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)

        ' Quad 0–1 with UVs
        Dim vertices As Single() = {
            0F, 0F, 0F, 0F,
            1.0F, 0F, 1.0F, 0F,
            0F, 1.0F, 0F, 1.0F,
            1.0F, 1.0F, 1.0F, 1.0F
        }

        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 4, vertices, BufferUsageHint.StaticDraw)

        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, 4 * 4, 0)
        GL.EnableVertexAttribArray(1)
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, False, 4 * 4, 2 * 4)

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
    End Sub

    Private Sub CompileShaders()
        Dim vertexShaderSrc As String =
"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

uniform vec2 uSize;
uniform vec2 uPosition;
uniform vec2 uScreenSize;

void main()
{
    vec2 pixelPos = aPos * uSize + uPosition;
    vec2 ndc = (pixelPos / uScreenSize) * 2.0 - 1.0;
    ndc.y = -ndc.y;
    gl_Position = vec4(ndc, 0.0, 1.0);
    TexCoord = aTexCoord;
}"
        Dim fragmentShaderSrc As String =
"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}"

        Dim vertexShader = GL.CreateShader(ShaderType.VertexShader)
        Dim fragmentShader = GL.CreateShader(ShaderType.FragmentShader)

        GL.ShaderSource(vertexShader, vertexShaderSrc)
        GL.ShaderSource(fragmentShader, fragmentShaderSrc)

        GL.CompileShader(vertexShader)
        Dim vLog = GL.GetShaderInfoLog(vertexShader)

        GL.CompileShader(fragmentShader)
        Dim fLog = GL.GetShaderInfoLog(fragmentShader)

        shaderProgram = GL.CreateProgram()
        GL.AttachShader(shaderProgram, vertexShader)
        GL.AttachShader(shaderProgram, fragmentShader)
        GL.LinkProgram(shaderProgram)

        Dim linkLog = GL.GetProgramInfoLog(shaderProgram)

        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
    End Sub

    Private Shared Function GenerateTextBitmap(text As String, fontSize As Integer, fontName As String) As Bitmap
        Using testBmp As New Bitmap(1, 1)
            Using g As Graphics = Graphics.FromImage(testBmp)
                Dim fnt As New Font(fontName, fontSize, FontStyle.Bold)
                Dim size As SizeF = g.MeasureString(text, fnt)
                Dim bmp As New Bitmap(CInt(Math.Ceiling(size.Width)), CInt(Math.Ceiling(size.Height)), Imaging.PixelFormat.Format32bppArgb)
                Using g2 As Graphics = Graphics.FromImage(bmp)
                    g2.Clear(Color.Transparent)
                    g2.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                    g2.DrawString(text, fnt, Brushes.Gray, 0, 0)
                End Using
                Return bmp
            End Using
        End Using
    End Function

    Public Sub Clean()
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0
        If textureID > 0 Then GL.DeleteTexture(textureID) : textureID = 0
        If shaderProgram > 0 Then GL.DeleteProgram(shaderProgram) : shaderProgram = 0
        For Each lab In Labels
            lab.Value.Dispose()
        Next
        Labels.Clear()
    End Sub
End Class
Public Class PreviewControl
    Inherits OpenTK.GLControl.GLControl
    Private overlay As TextOverlayRenderer
    Public SharedActiveShader As Shader_Class
    Public WithEvents RenderTimer As New System.Windows.Forms.Timer
    Private DebugProc As DebugProc
    Public Property AllowMask As Boolean = False
    Public defaultWhiteTex As Integer
    Public defaultNormalTex As Integer
    Public defaultCubeMap As Integer
    Public Property BrushRadiusPx As Integer = 5
    Public Property InvertMasking As Boolean = False


    ''' <summary>
    ''' Crea una textura 2D de w×h píxeles con el color indicado.
    ''' </summary>
    ''' 
    Private Shared Function CreateColorTexture(w As Integer, h As Integer, r As Byte, g As Byte, b As Byte, a As Byte) As Integer
        If w <= 0 OrElse h <= 0 Then Throw New ArgumentOutOfRangeException("w/h deben ser > 0")

        ' Evita overflow en el tamaño del array
        Dim total As Long = CLng(w) * CLng(h) * 4L
        If total > Integer.MaxValue Then Throw New OutOfMemoryException("Textura demasiado grande.")

        ' Rellena RGBA
        Dim pixelData(CInt(total) - 1) As Byte
        For i As Integer = 0 To pixelData.Length - 1 Step 4
            pixelData(i + 0) = r
            pixelData(i + 1) = g
            pixelData(i + 2) = b
            pixelData(i + 3) = a
        Next

        Dim texID As Integer = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, texID)

        ' Alineación segura
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)

        GL.TexImage2D(TextureTarget.Texture2D,
                  level:=0,
                  internalformat:=PixelInternalFormat.Rgba8,
                  width:=w, height:=h,
                  border:=0,
                  format:=OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,
                  type:=PixelType.UnsignedByte,
                  pixels:=pixelData)

        ' Filtros y wrap
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))

        GL.BindTexture(TextureTarget.Texture2D, 0)
        Return texID
    End Function

    ''' <summary>
    ''' Inicializa defaultWhiteTex, defaultNormalTex y defaultCubeMap como 4×4.
    ''' Llamar una vez tras crear el contexto GL.
    ''' </summary>
    Public Sub GenerateDefaultTextures()
        ' 4×4 blanco puro
        defaultWhiteTex = CreateColorTexture(4, 4, 255, 255, 255, 255)

        ' 4×4 normal map por defecto: (0.5,0.5,1) → (128,128,255)
        defaultNormalTex = CreateColorTexture(4, 4, 128, 128, 128, 128)

        ' Cubemap 4×4 blanco en todas las caras
        defaultCubeMap = GL.GenTexture()
        GL.BindTexture(TextureTarget.TextureCubeMap, defaultCubeMap)

        ' Preparamos datos 4×4 blancos para cada cara
        Dim faceData(4 * 4 * 4 - 1) As Byte
        For i As Integer = 0 To faceData.Length - 1 Step 4
            faceData(i + 0) = 255
            faceData(i + 1) = 255
            faceData(i + 2) = 255
            faceData(i + 3) = 255
        Next

        Dim faces As TextureTarget() = {
            TextureTarget.TextureCubeMapPositiveX,
            TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY,
            TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ,
            TextureTarget.TextureCubeMapNegativeZ
        }
        For Each face In faces
            GL.TexImage2D(face,
                          level:=0,
                          internalformat:=PixelInternalFormat.Rgba,
                          width:=4, height:=4,
                          border:=0,
                          format:=OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,
                          type:=PixelType.UnsignedByte,
                          pixels:=faceData)
        Next

        ' Filtros y wrap para cubemap
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))

        GL.BindTexture(TextureTarget.TextureCubeMap, 0)
    End Sub


    Public Class VerticesAffectedEventArgs
        Inherits EventArgs
        Public ReadOnly Property Affected As New Dictionary(Of Shape_class, HashSet(Of Integer))
        Public Sub New(d As Dictionary(Of Shape_class, HashSet(Of Integer)))
            For Each sh In d.Keys
                Affected.TryAdd(sh, New HashSet(Of Integer))
                Affected(sh).UnionWith(d(sh))
            Next

        End Sub
    End Class
    Private Sub DebugCallback(source As DebugSource, glType As DebugType, id As Integer, severity As DebugSeverity, length As Integer, message As IntPtr, userParam As IntPtr)
        If severity = DebugSeverity.DebugSeverityHigh Or glType = DebugType.DebugTypeError Then
            If glType = DebugType.DebugTypeError Then
#If DEBUG Then
                Debugger.Break()
                Dim Errorx = GL.GetError
#End If

            End If
            Dim msg As String = Marshal.PtrToStringAnsi(message, length)
            Debug.Print($"GL {glType} [{severity}] ({id}): {msg}")
        End If
    End Sub

    Private ReadOnly Property IsInDesignMode As Boolean
        Get
            Return LicenseManager.UsageMode = LicenseUsageMode.Designtime OrElse
               (Not Me.Created AndAlso (Me.Site IsNot Nothing AndAlso Me.Site.DesignMode))
        End Get
    End Property

    Private _Model As PreviewModel
    Public camera As New OrbitCamera()
    Private projection As Matrix4
    Public updateRequired As Boolean = True

    Public Sub Processing_Status(Texto As String)
        Me.MakeCurrent()
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        If Not IsNothing(overlay) Then
            overlay.SetText(Texto)
            overlay.RenderCentered(Me.Width, Me.Height)
        End If
        SwapBuffers()
        Application.DoEvents()
    End Sub


    Public Property Model As PreviewModel
        Get
            If _Model Is Nothing AndAlso Not IsInDesignMode Then
                _Model = New PreviewModel(Me)
            End If
            Return _Model
        End Get
        Set(value As PreviewModel)
            _Model = value
        End Set
    End Property
    Public Sub New()
        Me.New(New GLControlSettings With {
        .API = ContextAPI.OpenGL,
        .APIVersion = New Version(4, 4),
        .Flags = ContextFlags.ForwardCompatible,
        .Profile = ContextProfile.Core
    })

    End Sub
    Public Sub New(settings As GLControlSettings)
        MyBase.New(settings)
        RenderTimer = New System.Windows.Forms.Timer With {
            .Interval = 16    ' 16 ms ≈ 60 Hz
            }
        RenderTimer.Start()
    End Sub
    Public Sub Update_Render_LastLoaded(force As Boolean)
        Update_Render(Model.Last_rendered, force, Model.Last_Preset, Model.Last_Pose, Model.Last_size)
    End Sub



    Public Sub Update_Render(seleccionado As SliderSet_Class, Force As Boolean, Preset As SlidersPreset_Class, Pose As Poses_class, weight As Config_App.SliderSize)
        If Me.Disposing = True Or Me.IsDisposed Then Exit Sub

        If Visible = False Then Exit Sub
        If IsNothing(seleccionado) OrElse seleccionado.Unreadable_Project OrElse seleccionado.Unreadable_NIF Then
            If IsNothing(seleccionado) Then
                Model.Processing_Status_GL("Select project")
            Else
                Model.Processing_Status_GL("Unreadable...")
            End If
            Exit Sub
        End If
        Cursor.Current = Cursors.WaitCursor
        seleccionado.SetPreset(Preset, weight)
        Model.Last_size = weight
        Model.Last_Preset = Preset
        If (Model.Last_rendered Is seleccionado AndAlso Model.Last_Pose Is Pose AndAlso Force = False) AndAlso Model.Cleaned = False Then
            Model.Process_Textures_GL()
            For Each mesh In Model.meshes
                MorphingHelper.ApplyMorph_CPU(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry, Model.RecalculateNormals, AllowMask)
                mesh.UpdateSkinBuffers_GL()
            Next
            RefreshRender()
        Else
            Dim ResetCamerabool As Boolean = True
            If IsNothing(Model.Last_rendered) OrElse Not (Model.Last_rendered Is seleccionado) Then
                Model.Clean(True)
                Model.Processing_Status_GL("Loading...")
                Model.CleanTextures()
            Else
                Model.Clean(False)
                ResetCamerabool = False
                If Not (Pose Is Model.Last_Pose) Then ResetCamerabool = True
            End If
            Model.Last_Pose = Pose
            Model.LoadShapesParallel(seleccionado.Shapes)
            Model.Setup_GL()
            For Each mesh In Model.meshes
                MorphingHelper.ApplyMorph_CPU(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry, Model.RecalculateNormals, AllowMask)
                mesh.UpdateSkinBuffers_GL()
            Next
            If ResetCamerabool Then ResetCamera()
            RefreshRender()
        End If
        Cursor.Current = Cursors.Default
    End Sub
    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        If Me.IsInDesignMode Then Return
        ApplyResize(True)
        GenerateDefaultTextures()
        SharedActiveShader = New Shader_Class
        ' 1) Aseguramos que el contexto GL está activo
        Me.MakeCurrent()

        ' 2) (Opcional) Debug Output para capturar sólo errores
        GL.Enable(EnableCap.DebugOutput)
        GL.Enable(EnableCap.DebugOutputSynchronous)
        DebugProc = AddressOf DebugCallback
        GL.DebugMessageCallback(DebugProc, IntPtr.Zero)
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityHigh, 0, Array.Empty(Of Integer)(), True)

        ' 3) Estado GL estándar
        GL.Enable(EnableCap.DepthTest)
        GL.DepthFunc(DepthFunction.Lequal)

        GL.Enable(EnableCap.CullFace)
        GL.CullFace(TriangleFace.Back)
        GL.FrontFace(FrontFaceDirection.Ccw)

        overlay = New TextOverlayRenderer()

    End Sub

    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnLocationChanged(e)
    End Sub
    Private lastW As Integer = -1
    Private lastH As Integer = -1
    Protected Overrides Sub OnResize(e As EventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnResize(e)
        ApplyResize(False)
    End Sub
    Public Sub ApplyResize(Force As Boolean)
        If Me.IsInDesignMode Then Return
        If Force OrElse (Me.Width <> lastW OrElse Me.Height <> lastH) Then
            GL.Viewport(0, 0, Me.Width, Me.Height)
            lastW = Me.Width
            lastH = Me.Height
            UpdateProjection(True)
        End If
    End Sub
    ' === Frustum dinámico ===
    Private lastNear As Single = 0.1F
    Private lastFar As Single = 1000.0F

    ' Recalcula la proyección en función del tamaño de escena y la distancia actual de la cámara.
    Public Sub UpdateProjection(Optional force As Boolean = False)
        If Me.Height <= 0 Then Return

        ' Bounds de escena (si no hay meshes aún, usa un AABB mínimo)
        Dim minB As Vector3
        Dim maxB As Vector3
        If Model IsNot Nothing AndAlso Model.meshes IsNot Nothing AndAlso Model.meshes.Count > 0 Then
            GetSceneBounds(minB, maxB)
        Else
            minB = New Vector3(-1.0F)
            maxB = New Vector3(1.0F)
        End If

        Dim size As Vector3 = maxB - minB
        ' Ejes: X=ancho, Y=profundidad, Z=alto (tu código ya usa esta convención)
        Dim halfW As Single = Math.Abs(size.X) * 0.5F
        Dim halfD As Single = Math.Abs(size.Y) * 0.5F
        Dim halfH As Single = Math.Abs(size.Z) * 0.5F

        ' Radio: cuanto “crece” la escena alrededor del centro
        Dim radius As Single = Math.Max(halfW, Math.Max(halfD, halfH))

        ' Distancia actual cámara → foco
        Dim eyeToCenter As Single = Math.Max(1.0F, camera.distance)

        ' Margen para asegurar que no clippea por el far plane
        Dim margin As Single = 0.2F

        ' Far plane sugerido: distancia + radio + margen
        Dim farZ As Single = eyeToCenter + radius * (1.0F + margin) + 1.0F
        ' Mínimo razonable para escenas pequeñas
        farZ = Math.Max(1000.0F, farZ)

        ' Near plane: suficientemente pequeño, pero no exagerado para no perder precisión de Z
        Dim nearZ As Single = Math.Max(0.05F, farZ / 10000.0F)

        ' Evitar recalcular si el cambio es mínimo
        If Not force AndAlso Math.Abs(farZ - lastFar) < 1.0F AndAlso Math.Abs(nearZ - lastNear) < 0.01F Then
            Return
        End If

        Dim aspect As Single = Me.Width / CSng(Math.Max(1, Me.Height))
        Dim fovY As Single = MathHelper.DegreesToRadians(45.0F)

        projection = Matrix4.CreatePerspectiveFieldOfView(fovY, aspect, nearZ, farZ)
        lastNear = nearZ
        lastFar = farZ
        updateRequired = True
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If Me.IsInDesignMode OrElse Not updateRequired Then Exit Sub
        MyBase.OnPaint(e)
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        If Model.Can_Render Then
            Model.RenderAll(projection, camera)
        End If

        SwapBuffers()
        GL.DepthMask(True)
        GL.Disable(EnableCap.Blend)
        updateRequired = False
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) = 0 Then
            lastX = e.X
            lastY = e.Y
        End If
        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) <> 0 Then
            lastX = e.X
            lastY = e.Y
        End If
        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Alt) <> 0 Then
            lastX = e.X
            lastY = e.Y
        End If
    End Sub


    Private lastX As Integer
    Private lastY As Integer
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnMouseMove(e)
        ' Left drag sin Ctrl ni Alt: salir de FreeMode (si aplica) y luego ROTATE orbit manteniendo el mismo radio
        ' Left drag sin Ctrl ni Alt: salimos de free-cam (si era el caso) y rotamos en orbit
        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) = 0 AndAlso (Control.ModifierKeys And Keys.Alt) = 0 Then
            ' Si venimos de free-cam, restauramos el radio original
            ' Ahora la rotación orbital normal
            Dim dx = e.X - lastX
            Dim dy = e.Y - lastY
            lastX = e.X
            lastY = e.Y

            camera.Rotate(dx, dy)
            updateRequired = True
            Return
        End If

        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Alt) <> 0 Then
            ' Calcula delta de ratón
            ' Calcula delta de ratón
            Dim dx = e.X - lastX
            Dim dy = e.Y - lastY
            lastX = e.X
            lastY = e.Y
            camera.PanScreen(dx, dy)
            updateRequired = True
            Return
        End If


        ' 2) Barrido con Ctrl + botón izquierdo
        If AllowMask AndAlso e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) <> 0 Then
            Cursor.Current = Cursors.Hand
            Dim vw = Me.Width
            Dim vh = Me.Height
            Dim r2 As Single = BrushRadiusPx * BrushRadiusPx
            ' — Hoist de matrices: calcula viewProj una sola vez
            Dim viewMatrix As Matrix4 = camera.GetViewMatrix()
            Dim viewProj As Matrix4 = viewMatrix * projection
            Dim camPos = camera.GetEyePosition()
            For Each mesh In Model.meshes.Where(Function(pf) pf.MeshData.Shape.ShowMask)
                Dim key = mesh.MeshData.Shape
                Dim verts = mesh.MeshData.Meshgeometry.Vertices
                Dim norms = mesh.MeshData.Meshgeometry.Normals

                For i = 0 To verts.Length - 1
                    If mesh.MeshData.Meshgeometry.VertexMask(i) = -1 And mesh.MeshData.Shape.ApplyZaps Then Continue For
                    If mesh.MeshData.Meshgeometry.VertexMask(i) = -1 Then If mesh.MeshData.Shape.MaskedVertices.Contains(i) Then mesh.MeshData.Meshgeometry.VertexMask(i) = 1 Else mesh.MeshData.Meshgeometry.VertexMask(i) = 0
                    If (mesh.MeshData.Meshgeometry.VertexMask(i) = 1 AndAlso Not InvertMasking) OrElse (mesh.MeshData.Meshgeometry.VertexMask(i) = 0 AndAlso InvertMasking) Then Continue For
                    ' 2.1b) Filtrar solo vértices de la cara delantera (normal-camera)
                    Dim normal As Vector3 = norms(i)
                    Dim toCam As Vector3 = camPos - verts(i)
                    If Vector3.Dot(normal, toCam) <= 0 Then Continue For

                    Dim clipPos As Vector4 = New Vector4(verts(i), 1.0F) * viewProj


                    ' 2.2) Filtrado de frustum (W>0) — opcional quitar para probar
                    If clipPos.W <= 0 Then Continue For

                    ' 2.3) De clip a NDC
                    Dim ndcX = clipPos.X / clipPos.W
                    Dim ndcY = clipPos.Y / clipPos.W

                    ' 2.4) De NDC a ventana (0,0 arriba)
                    Dim sx = (ndcX + 1.0F) * 0.5F * vw
                    Dim sy = (1.0F - ndcY) * 0.5F * vh

                    ' 2.5) Calcula distancia al cursor
                    Dim dx2 = sx - e.X
                    Dim dy2 = sy - e.Y
                    Dim dist2 = dx2 * dx2 + dy2 * dy2

                    ' 2.6) Si entra en el radio, lo marcamos
                    If dist2 <= r2 Then
                        mesh.MeshData.Meshgeometry.dirtyMaskIndices.Add(i)
                        mesh.MeshData.Meshgeometry.dirtyMaskFlags(i) = True
                        mesh.MeshData.Meshgeometry.VertexMask(i) = 1 - mesh.MeshData.Meshgeometry.VertexMask(i)
                        If InvertMasking Then mesh.MeshData.Shape.MaskedVertices.Remove(i) Else mesh.MeshData.Shape.MaskedVertices.Add(i)
                        Me.updateRequired = True
                    End If
                Next
                mesh.UpdateUpdateSkinBuffersMask_GL()
            Next
            Me.Invalidate()
            Return
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        Cursor.Current = Cursors.Default

    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnMouseWheel(e)
        camera.Zoom(e.Delta / 120.0F)
        UpdateProjection(False)
        updateRequired = True
    End Sub

    Public Sub RefreshRender()
        updateRequired = True
        Me.Invalidate()
    End Sub
    Public Sub ResetCamera(Optional Force As Boolean = False)
        If Me.IsInDesignMode Then Return

        Dim oldcamera = camera
        camera = New OrbitCamera()
        CenterCamera()

        If Not Config_App.Current.Settings_Camara.ResetAngles And Not Force Then
            camera.angleX = oldcamera.angleX
            camera.angleY = oldcamera.angleY
            camera.UpdateDirectionFromAngles()
        End If
        If Not Config_App.Current.Settings_Camara.ResetZoom And Not Force Then
            If oldcamera.Optimaldistance <> 0 Then
                camera.distance *= (oldcamera.distance / oldcamera.Optimaldistance)
                camera.distance = Math.Clamp(camera.Optimaldistance, camera.MinDistance, camera.MaxDistance)
            End If
        End If

        If Config_App.Current.Settings_Camara.FreezeCamera And oldcamera.Optimaldistance <> 0 And Not Force Then
            camera = oldcamera
        End If

    End Sub

    Public Sub GetSceneBounds(ByRef min As Vector3, ByRef max As Vector3)
        min = New Vector3(Single.MaxValue)
        max = New Vector3(Single.MinValue)
        For Each mesh In Model.meshes
            min = Vector3.ComponentMin(min, mesh.MeshData.Meshgeometry.Minv)
            max = Vector3.ComponentMax(max, mesh.MeshData.Meshgeometry.Maxv)
        Next
    End Sub
    Public Sub CenterCamera()
        If Me.IsInDesignMode Then Return

        ' 1) AABB
        Dim minB As Vector3, maxB As Vector3
        GetSceneBounds(minB, maxB)

        ' 2) Centro y tamaño
        Dim center As Vector3 = (minB + maxB) * 0.5F
        Dim size As Vector3 = maxB - minB

        ' 3) Focus y orbit mode
        camera.FocusPosition = center

        ' 4) Parámetros de cámara
        Dim fovY As Single = MathHelper.DegreesToRadians(45.0F)
        Dim aspect As Single = Me.Width / CSng(Me.Height)

        ' ** Usamos Z para altura, X para anchura y Y para profundidad (hacia la cámara) **
        Dim halfH As Single = size.Z * 0.5F   ' vertical ← Z
        Dim halfW As Single = size.X * 0.5F   ' horizontal ← X
        Dim halfD As Single = size.Y * 0.5F   ' profundidad ← Y

        ' 5) Calculamos distancias mínimas sin margen
        Dim distH = halfH / CSng(Math.Tan(fovY * 0.5F))
        Dim fovX = 2.0F * CSng(Math.Atan(Math.Tan(fovY * 0.5F) * aspect))
        Dim distW = halfW / CSng(Math.Tan(fovX * 0.5F))

        ' 6) Margen uniforme (p.ej. 15% extra)
        Dim marginPct As Single = 0.1F
        ' SUMAMOS la media profundidad para asegurar que el punto más cercano también entra en FOV
        Dim baseDistance As Single = halfD + Math.Max(distH, distW)
        Dim idealDistance As Single = baseDistance * (1.0F + marginPct)
        camera.MaxDistance = idealDistance * 10
        camera.MinDistance = idealDistance / 10
        camera.distance = Math.Clamp(idealDistance, camera.MinDistance, camera.MaxDistance)
        camera.Optimaldistance = camera.distance

        ' 7) Reset ángulos y orientación
        camera.angleX = 0F
        camera.angleY = 0F
        camera.UpdateDirectionFromAngles()
        camera.Up = Vector3.UnitZ
        UpdateProjection(True)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        MyBase.Dispose(disposing)
    End Sub
    Private Sub RenderTimer_Tick(sender As Object, e As EventArgs) Handles RenderTimer.Tick
        If updateRequired AndAlso RenderTimer.Enabled = True Then
            Me.Invalidate()  ' disparará OnPaint
        End If
    End Sub
    Public Sub Clean()
        If overlay IsNot Nothing Then
            overlay.Clean()
            overlay = Nothing
        End If

        If _Model IsNot Nothing Then
            _Model.Clean(True)
            _Model.CleanTextures()
            _Model = Nothing
        End If
        If SharedActiveShader IsNot Nothing Then
            SharedActiveShader.Dispose() ' Asegúrate que Shader_Class tenga este método
            SharedActiveShader = Nothing
        End If

        If RenderTimer IsNot Nothing Then
            RenderTimer.Stop()
            RenderTimer.Dispose()
            RenderTimer = Nothing
        End If
        If defaultWhiteTex <> 0 Then GL.DeleteTexture(defaultWhiteTex)
        If defaultNormalTex <> 0 Then GL.DeleteTexture(defaultNormalTex)
        If defaultCubeMap <> 0 Then GL.DeleteTexture(defaultCubeMap)
        GL.DebugMessageCallback(Nothing, IntPtr.Zero)
    End Sub
    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
Public Class PreviewModel

    Public Textures_Dictionary As New Dictionary(Of String, Texture_Loaded_Class)(StringComparison.OrdinalIgnoreCase)
    Public Can_Render As Boolean = False
    Public meshes As New List(Of RenderableMesh)
    Private ReadOnly ParentControl As PreviewControl
    Public Property Last_rendered As SliderSet_Class
    Public Property Last_Pose As Poses_class = Nothing
    Public Property Last_size As Config_App.SliderSize = Config_App.SliderSize.Default

    Public Last_Preset As SlidersPreset_Class = Nothing
    Public Property Cleaned As Boolean = True
    Public Property SingleBoneSkinning As Boolean = False
    Public Property RecalculateNormals As Boolean = True

    Public Class Texture_Loaded_Class
        Public Property Loaded As Boolean = False
        Public Property Cubemap As Boolean = False
        Public Property Path As String = ""
        Public Property Size As New Size
        Public Property DGXFormat_Original As Integer
        Public Property DGXFormat_Final As Integer
        Public Property Texture_ID As Integer

    End Class
    Public Class RenderableMesh
        Public Class MeshData_Class
            Sub New(Parent As RenderableMesh)
                ParentMesh = Parent
            End Sub
            Sub New()
            End Sub
            Public Property ParentMesh As RenderableMesh
            Public ReadOnly Property ShapeName As String
                Get
                    Return Shape.Nombre
                End Get
            End Property

            Public ReadOnly Property Idx As Integer
                Get
                    Return Shape.ParentSliderSet.Shapes.IndexOf(Shape)
                End Get
            End Property

            Public Meshgeometry As SkinnedGeometry
            Public Property Material As MaterialData
            Public Property Transform As Matrix4 = Matrix4.Identity
            Public Property Shape As Shape_class

        End Class


        Public vao As Integer
        Public ebo As Integer
        Private vboPosition As Integer
        Private vboNormal As Integer
        Private vboTangent As Integer
        Private vboBitangent As Integer

        Public vboColorAlpha As Integer
        Public vboUVMaskWeight As Integer



        ' Añade **sólo** estas dos líneas:
        Private vboMask As Integer                                    ' VBO dedicado a máscara
        Public MeshData As MeshData_Class
        Private indexCount As Integer
        Public Class MaterialData
            Sub New(Parent As MeshData_Class)
                ParentMeshData = Parent
            End Sub
            Public Property ParentMeshData As MeshData_Class
            Public ReadOnly Property MaterialBase As FO4UnifiedMaterial_Class
                Get
                    Return ParentMeshData.Shape.RelatedMaterial.material
                End Get
            End Property

            Public ReadOnly Property HasAlphaBlend
                Get
                    If IsNothing(ParentMeshData.Shape.RelatedMaterial) Then Return False
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.None Then Return False
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Standard Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Multiplicative Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Additive Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Unknown Then Return GetAlphaFromShape()
                    Debugger.Break()
                    Return False
                End Get
            End Property
            Private Function GetAlphaFromShape()
                If Not IsNothing(ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef) AndAlso ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef.Index <> -1 Then
                    Dim alp = CType(ParentMeshData.Shape.ParentSliderSet.NIFContent.Blocks(ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef.Index), NiflySharp.Blocks.NiAlphaProperty)
                    If alp.Flags.AlphaBlend Then
                        Return True
                    Else
                        Return False
                    End If
                End If
                Return False
            End Function


            Public ReadOnly Property HasAlphaTest
                Get
                    If HasAlphaBlend = True Then Return False
                    If IsNothing(ParentMeshData.Shape.RelatedMaterial) Then Return False
                    Return MaterialBase.AlphaTest
                End Get
            End Property
            Public Function Calculate_Blending() As Integer()
                Select Case MaterialBase.AlphaBlendMode
                    Case AlphaBlendModeType.Standard
                        Return {BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha}
                    Case AlphaBlendModeType.Additive
                        Return {BlendingFactor.SrcAlpha, BlendingFactor.One}
                    Case AlphaBlendModeType.Multiplicative
                        Return {BlendingFactor.DstColor, BlendingFactor.Zero}
                    Case AlphaBlendModeType.Unknown
                        Dim src As BlendingFactor = BlendingFactor.SrcAlpha
                        Dim dst As BlendingFactor = BlendingFactor.OneMinusSrcAlpha
                        Try
                            If Not IsNothing(ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef) AndAlso ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef.Index <> -1 Then
                                Dim alp = CType(ParentMeshData.Shape.ParentSliderSet.NIFContent.Blocks(ParentMeshData.Shape.RelatedNifShape.AlphaPropertyRef.Index), NiflySharp.Blocks.NiAlphaProperty)

                                Select Case alp.Flags.SourceBlendMode
                                    Case NiflySharp.Enums.AlphaFunction.DEST_ALPHA
                                        src = BlendingFactor.DstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.DEST_COLOR
                                        src = BlendingFactor.DstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_ALPHA
                                        src = BlendingFactor.OneMinusDstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_COLOR
                                        src = BlendingFactor.OneMinusDstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
                                        src = BlendingFactor.OneMinusSrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_COLOR
                                        src = BlendingFactor.OneMinusSrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ONE
                                        src = BlendingFactor.One
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA
                                        src = BlendingFactor.SrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA_SATURATE
                                        src = BlendingFactor.SrcAlphaSaturate
                                    Case NiflySharp.Enums.AlphaFunction.SRC_COLOR
                                        src = BlendingFactor.SrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ZERO
                                        src = BlendingFactor.Zero
                                End Select
                                Select Case alp.Flags.DestinationBlendMode
                                    Case NiflySharp.Enums.AlphaFunction.DEST_ALPHA
                                        dst = BlendingFactor.DstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.DEST_COLOR
                                        dst = BlendingFactor.DstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_ALPHA
                                        dst = BlendingFactor.OneMinusDstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_COLOR
                                        dst = BlendingFactor.OneMinusDstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
                                        dst = BlendingFactor.OneMinusSrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_COLOR
                                        dst = BlendingFactor.OneMinusSrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ONE
                                        dst = BlendingFactor.One
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA
                                        dst = BlendingFactor.SrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA_SATURATE
                                        dst = BlendingFactor.SrcAlphaSaturate
                                    Case NiflySharp.Enums.AlphaFunction.SRC_COLOR
                                        dst = BlendingFactor.SrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ZERO
                                        dst = BlendingFactor.Zero
                                End Select

                            End If
                        Catch ex As Exception
                            Debugger.Break()
                        End Try

                        Return {src, dst}
                    Case Else
                        Throw New Exception
                End Select

            End Function


            Public ReadOnly Property Textures_Path_List As IEnumerable(Of String)
                Get
                    Return {FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.NormalTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SmoothSpecTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GlowTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DisplacementTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.InnerLayerTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.LightingTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SpecularTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.WrinklesTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DistanceFieldAlphaTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapMaskTexture)
                                              }
                End Get
            End Property
            Public ReadOnly Property DiffuseTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property NormalTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.NormalTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property SpecularTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SpecularTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property SmoothSpecTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SmoothSpecTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property EnvmapTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property GreyscaleTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property GlowTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GlowTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property WrinklesTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.WrinklesTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property DisplacementTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DisplacementTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property InnerLayerTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.InnerLayerTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property LightingTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.LightingTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property DistanceFieldAlphaTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DistanceFieldAlphaTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property

            Public ReadOnly Property EnvmapMaskTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapMaskTexture)
                    If key = "" Then
                        key = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture)
                        If key = "" Then Return 0
                    End If
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Cubemap = True Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property
            Public ReadOnly Property FlowTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture)
                    If key = "" Then Return 0
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return 0
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Texture_ID
                End Get
            End Property

            Public ReadOnly Property HasCubemap As Boolean
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture)
                    If key = "" Then Return False
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return False
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Cubemap
                End Get
            End Property

            Public ReadOnly Property HasGrayscale As Boolean
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture)
                    If key = "" Then Return False
                    If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.ContainsKey(key) = False Then Return False
                    Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary(key).Loaded
                End Get
            End Property



        End Class

        Private ReadOnly ParentModel As PreviewModel

        Public Sub Clean()
            ' — Eliminar VAO y buffers de atributos —
            If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
            If ebo > 0 Then GL.DeleteBuffer(ebo) : ebo = 0
            If vboPosition > 0 Then GL.DeleteBuffer(vboPosition) : vboPosition = 0
            If vboNormal > 0 Then GL.DeleteBuffer(vboNormal) : vboNormal = 0
            If vboTangent > 0 Then GL.DeleteBuffer(vboTangent) : vboTangent = 0
            If vboBitangent > 0 Then GL.DeleteBuffer(vboBitangent) : vboBitangent = 0
            If vboColorAlpha > 0 Then GL.DeleteBuffer(vboColorAlpha) : vboColorAlpha = 0
            If vboUVMaskWeight > 0 Then GL.DeleteBuffer(vboUVMaskWeight) : vboUVMaskWeight = 0
            If vboMask > 0 Then GL.DeleteBuffer(vboMask) : vboMask = 0

            ' — Reducir flags de dirty-tracking a mínima expresión —
            MeshData.Meshgeometry = Nothing
        End Sub

        Public Sub New(data As MeshData_Class, Parent_Model As PreviewModel)
            MeshData = data
            ParentModel = Parent_Model
            MeshData.ParentMesh = Me
        End Sub

        Public Sub UpdateSkinBuffers_GL()
            ' Actualiza VBOs de Normales, Tangentes, Bitangentes y Posiciones usando MapBufferRange en un solo bucle
            If MeshData.Meshgeometry.dirtyVertexIndices.Count > 0 Then
                Const elementSize As Integer = 3 * 4  ' bytes por vértice y por atributo
                Dim totalBytes As Integer = MeshData.Meshgeometry.Vertices.Length * elementSize
                Dim mapMask As MapBufferAccessMask = MapBufferAccessMask.MapWriteBit Or MapBufferAccessMask.MapUnsynchronizedBit

                ' Mapear buffers
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                Dim ptrN As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                Dim ptrT As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                Dim ptrB As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                Dim ptrP As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)

                ' Un solo bucle para actualizar todos los atributos
                For Each i As Integer In MeshData.Meshgeometry.dirtyVertexIndices
                    Dim offsetBytes As Int64 = CLng(i) * elementSize
                    Dim baseN As IntPtr = ptrN + offsetBytes
                    Dim baseT As IntPtr = ptrT + offsetBytes
                    Dim baseB As IntPtr = ptrB + offsetBytes
                    Dim baseP As IntPtr = ptrP + offsetBytes

                    ' Normales
                    Dim n = MeshData.Meshgeometry.Normals(i)
                    Marshal.Copy(New Single() {n.X, n.Y, n.Z}, 0, baseN, 3)
                    MeshData.Meshgeometry.dirtyVertexFlags(i) = False

                    ' Tangentes
                    Dim t = MeshData.Meshgeometry.Tangents(i)
                    Marshal.Copy(New Single() {t.X, t.Y, t.Z}, 0, baseT, 3)

                    ' Bitangentes
                    Dim b = MeshData.Meshgeometry.Bitangents(i)
                    Marshal.Copy(New Single() {b.X, b.Y, b.Z}, 0, baseB, 3)

                    ' Posiciones
                    Dim v = MeshData.Meshgeometry.Vertices(i)
                    Marshal.Copy(New Single() {v.X, v.Y, v.Z}, 0, baseP, 3)
                Next

                ' Desmapear en orden inverso
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)

                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)

                MeshData.Meshgeometry.dirtyVertexIndices.Clear()
            End If
            UpdateUpdateSkinBuffersMask_GL()
        End Sub
        Public Sub UpdateUpdateSkinBuffersMask_GL()
            If MeshData.Meshgeometry.dirtyMaskIndices.Count > 0 Then
                Const maskSize As Integer = 4 ' bytes por máscara
                Dim totalMaskBytes As Integer = MeshData.Meshgeometry.VertexMask.Length * maskSize
                ' Usar misma lógica de MapBufferRange y MapUnsynchronizedBit
                Dim mapMask As MapBufferAccessMask = MapBufferAccessMask.MapWriteBit Or MapBufferAccessMask.MapUnsynchronizedBit

                ' Mapear buffer de máscara
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
                Dim ptrM As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalMaskBytes, mapMask)

                ' Un solo bucle para escribir máscaras sucias
                For Each i As Integer In MeshData.Meshgeometry.dirtyMaskIndices
                    Dim offsetBytes As Int64 = CLng(i) * maskSize
                    Dim baseM As IntPtr = ptrM + offsetBytes
                    Dim mBytes() As Byte = BitConverter.GetBytes(MeshData.Meshgeometry.VertexMask(i))
                    Marshal.Copy(mBytes, 0, baseM, maskSize)
                    MeshData.Meshgeometry.dirtyMaskFlags(i) = False
                Next

                ' Desmapear buffer de máscara
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                MeshData.Meshgeometry.dirtyMaskIndices.Clear()
            End If
        End Sub

        Public Sub SetupMesh_GL()
            vao = GL.GenVertexArray()
            ebo = GL.GenBuffer()
            vboPosition = GL.GenBuffer()
            vboNormal = GL.GenBuffer()
            vboTangent = GL.GenBuffer()
            vboBitangent = GL.GenBuffer()
            vboColorAlpha = GL.GenBuffer()
            vboUVMaskWeight = GL.GenBuffer()
            vboMask = GL.GenBuffer()

            Dim count = MeshData.Meshgeometry.Vertices.Length

            GL.BindVertexArray(vao)

            Dim posF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Vertices, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim nrmF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Normals, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim tanF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Tangents, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim bitanF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Bitangents, Function(v) New Vector3(v.X, v.Y, v.Z))

            ' POSICIONES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
            GL.BufferData(BufferTarget.ArrayBuffer, posF.Length * 3 * 4, posF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' NORMALES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
            GL.BufferData(BufferTarget.ArrayBuffer, nrmF.Length * 3 * 4, nrmF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' TANGENTES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
            GL.BufferData(BufferTarget.ArrayBuffer, tanF.Length * 3 * 4, tanF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(2)
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' BITANGENTES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
            GL.BufferData(BufferTarget.ArrayBuffer, bitanF.Length * 3 * 4, bitanF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(3)
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' COLOR + ALPHA — StaticDraw

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColorAlpha)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.VertexColors.Length * 4 * 4, MeshData.Meshgeometry.VertexColors, BufferUsageHint.StaticDraw)
            GL.EnableVertexAttribArray(4)
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, False, 4 * 4, 0)
            GL.EnableVertexAttribArray(5)
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, False, 4 * 4, 3 * 4)

            ' UV + WEIGHT — StaticDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboUVMaskWeight)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.Uvs_Weight.Length * 3 * 4, MeshData.Meshgeometry.Uvs_Weight, BufferUsageHint.StaticDraw)
            GL.EnableVertexAttribArray(6)
            GL.VertexAttribPointer(6, 2, VertexAttribPointerType.Float, False, 3 * 4, 0)
            GL.EnableVertexAttribArray(8)
            GL.VertexAttribPointer(8, 1, VertexAttribPointerType.Float, False, 3 * 4, 2 * 4)

            ' MÁSCARA — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.VertexMask.Length * 4, MeshData.Meshgeometry.VertexMask, BufferUsageHint.DynamicDraw)

            GL.EnableVertexAttribArray(7)
            GL.VertexAttribPointer(7, 1, VertexAttribPointerType.Float, False, 4, 0)

            ' EBO
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
            GL.BufferData(BufferTarget.ElementArrayBuffer, MeshData.Meshgeometry.Indices.Length * 4, MeshData.Meshgeometry.Indices, BufferUsageHint.StaticDraw)
            GL.BindVertexArray(0)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            indexCount = MeshData.Meshgeometry.Indices.Length
        End Sub


        Public Sub Render(projection As Matrix4, ByRef camera As OrbitCamera)

            If IsNothing(MeshData.Shape) OrElse MeshData.Shape.RenderHide = True Then Exit Sub
            If IsNothing(Me.MeshData.Shape.RelatedNifShape) Then Exit Sub
            '=============================== MATRICES ===============================
            Dim model As Matrix4 = MeshData.Transform
            Dim view As Matrix4 = camera.GetViewMatrix()
            Dim modelView As Matrix4 = view * model

            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert()
            normalMatrix.Transpose()

            Dim modelViewInverse As Matrix4 = modelView.Inverted()


            '=============================== SHADER ===============================
            Me.ParentModel.ParentControl.SharedActiveShader.Use()
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix4("matProjection", projection)
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix4("matView", view)
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix4("matModel", model)
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix4("matModelView", modelView)
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix4("matModelViewInverse", modelViewInverse)
            Me.ParentModel.ParentControl.SharedActiveShader.SetMatrix3("mv_normalMatrix", normalMatrix)
            ApplyMaterial(MeshData.Material)

            '=============================== DRAW ===============================
            GL.BindVertexArray(vao)
            Dim mat = MeshData.Material.MaterialBase
            Dim isTwoSidedBlended As Boolean = MeshData.Material.HasAlphaBlend AndAlso mat.TwoSided AndAlso Not MeshData.Shape.Wireframe

            If isTwoSidedBlended Then
                ' — Pasada 1: caras traseras (culling de frontales) —
                GL.Enable(EnableCap.CullFace)
                GL.CullFace(TriangleFace.Front)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)

                ' — Pasada 2: caras frontales (culling de traseras) —
                GL.CullFace(TriangleFace.Back)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            Else
                ' — Caso normal: una sola pasada —
                If mat.TwoSided Then
                    GL.Disable(EnableCap.CullFace)      ' dibuja front y back
                Else
                    GL.Enable(EnableCap.CullFace)
                    GL.CullFace(TriangleFace.Back)    ' descarta caras traseras
                End If

                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            End If

            ' (Opcional) restaurar estado si luego renderizas más cosas:
            GL.DepthMask(True)
            GL.Disable(EnableCap.Blend)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            GL.CullFace(TriangleFace.Back)

        End Sub

        Public Sub ApplyMaterial(material As PreviewModel.RenderableMesh.MaterialData)



            '===============================
            ' 🎨 PROPIEDADES DE COLOR BÁSICO
            '===============================
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("color", Shader_Class.Color_to_Vector(MeshData.Shape.Wirecolor))
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("WireAlpha", MeshData.Shape.WireAlpha)
            'If MeshData.Material.MaterialBase.SkinTint Then
            '    MeshData.Shape.TintColor = MeshData.Material.MaterialBase.HairTintColor
            'Else
            '    MeshData.Shape.TintColor = Color.White
            'End If
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("subColor", Shader_Class.Color_to_Vector(MeshData.Shape.TintColor))

            '===============================
            ' 🟢 TOGGLES DE VISUALIZACIÓN
            '===============================
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bShowTexture", Me.MeshData.Shape.ShowTexture)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bShowMask", Me.MeshData.Shape.ShowMask)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bShowWeight", Me.MeshData.Shape.ShowWeight)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bShowVertexColor", Me.MeshData.Shape.ShowVertexColor And Me.MeshData.Shape.RelatedNifShape.HasVertexColors)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bShowVertexAlpha", Me.MeshData.Shape.ShowVertexColor And Me.MeshData.Shape.RelatedNifShape.HasVertexColors)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bApplyZap", Me.MeshData.Shape.ApplyZaps)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bWireframe", Me.MeshData.Shape.Wireframe)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bHide", Me.MeshData.Shape.RenderHide)


            '===============================
            ' 💡 ILUMINACIÓN PRINCIPAL
            '===============================
            ' 💡 ILUMINACIÓN PRINCIPAL

            ' main “frontal” light
            Dim cam = ParentModel.ParentControl.camera

            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bLightEnabled", True)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("ambient", Config_App.Current.Setting_Lightrig.Ambient)

            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("frontal.diffuse", Config_App.Current.Setting_Lightrig.DirectL.GetDifuse)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("frontal.direction", Config_App.Current.Setting_Lightrig.DirectL.GetDirection(cam))
            ' Luz direccional 0
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional0.diffuse", Config_App.Current.Setting_Lightrig.FillLight_1.GetDifuse)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional0.direction", Config_App.Current.Setting_Lightrig.FillLight_1.GetDirection(cam))

            ' Luz direccional 1
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional1.diffuse", Config_App.Current.Setting_Lightrig.FillLight_2.GetDifuse)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional1.direction", Config_App.Current.Setting_Lightrig.FillLight_2.GetDirection(cam))

            ' Luz direccional 2
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional2.diffuse", Config_App.Current.Setting_Lightrig.BackLight.GetDifuse)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("directional2.direction", Config_App.Current.Setting_Lightrig.BackLight.GetDirection(cam))

            '===============================
            ' 🧪 TEXTURAS (Sample BINDs)
            '===============================
            If material.DiffuseTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texDiffuse", material.DiffuseTexture_ID, TextureUnit.Texture0)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texDiffuse", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture0)
            End If

            If material.NormalTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texNormal", material.NormalTexture_ID, TextureUnit.Texture1)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texNormal", Me.ParentModel.ParentControl.defaultNormalTex, TextureUnit.Texture1)
            End If

            If material.EnvmapTexture_ID <> 0 AndAlso material.HasCubemap Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindCubeMap("texCubemap", material.EnvmapTexture_ID, TextureUnit.Texture2)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindCubeMap("texCubemap", Me.ParentModel.ParentControl.defaultCubeMap, TextureUnit.Texture2)
            End If

            If material.EnvmapMaskTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texEnvMask", material.EnvmapMaskTexture_ID, TextureUnit.Texture3)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texEnvMask", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture3)
            End If

            If material.SmoothSpecTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texSpecular", material.SmoothSpecTexture_ID, TextureUnit.Texture4)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texSpecular", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture4)
            End If

            If material.GreyscaleTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texGreyscale", material.GreyscaleTexture_ID, TextureUnit.Texture5)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texGreyscale", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture5)
            End If

            If material.GlowTexture_ID <> 0 Then
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texGlowmap", material.GlowTexture_ID, TextureUnit.Texture6)
            Else
                Me.ParentModel.ParentControl.SharedActiveShader.BindTexture("texGlowmap", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture6)
            End If

            '===============================
            ' ⚙️ PROPIEDADES DEL MATERIAL
            '===============================
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector2("uvOffset", New Vector2(material.MaterialBase.UOffset, material.MaterialBase.VOffset))
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector2("uvScale", New Vector2(material.MaterialBase.UScale, material.MaterialBase.VScale))
            ' Umbral de alpha (solo necesario si usás discard por transparencia)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("alphaThreshold", material.MaterialBase.AlphaTestRef / 255)

            '===============================
            ' 🧩 TOGGLES DE EFECTOS Y SOMBREADO
            '===============================
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bCubemap", material.HasCubemap)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bAlphaTest", material.HasAlphaTest)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bEnvMask", material.EnvmapMaskTexture_ID <> 0)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bNormalMap", material.NormalTexture_ID <> 0)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bGreyscaleColor", material.MaterialBase.GrayscaleToPaletteColor AndAlso material.GreyscaleTexture_ID <> 0)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bSpecular", material.MaterialBase.SpecularEnabled AndAlso material.SmoothSpecTexture_ID <> 0)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bModelSpace", material.MaterialBase.ModelSpaceNormals)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bEmissive", material.MaterialBase.EmitEnabled)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bSoftlight", material.MaterialBase.SubsurfaceLighting)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bGlowmap", material.MaterialBase.Glowmap AndAlso material.GlowTexture_ID <> 0)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("shininess", material.MaterialBase.Smoothness)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("specularColor", Shader_Class.Color_to_Vector(material.MaterialBase.SpecularColor))
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("specularStrength", material.MaterialBase.SpecularMult)
            Me.ParentModel.ParentControl.SharedActiveShader.SetVector3("emissiveColor", Shader_Class.Color_to_Vector(material.MaterialBase.EmittanceColor))
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("emissiveMultiple", material.MaterialBase.EmittanceMult)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("fresnelPower", material.MaterialBase.FresnelPower)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("subsurfaceRolloff", material.MaterialBase.SubsurfaceLightingRolloff)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("paletteScale", material.MaterialBase.GrayscaleToPaletteScale)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("envReflection", material.MaterialBase.EnvironmentMappingMaskScale)

            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bBacklight", material.MaterialBase.BackLighting)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("backlightPower", material.MaterialBase.BackLightPower)
            Me.ParentModel.ParentControl.SharedActiveShader.SetBool("bRimlight", material.MaterialBase.RimLighting)
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("rimlightPower", material.MaterialBase.RimPower)

            'SetVector3("softlightColor", New Vector3(1.0F, 1.0F, 1.0F))
            'SetFloat("softlightPower", 0.4F)
            'SetFloat("softlightDesaturation", 0.4F)


            ' === DebugMode ===

            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("DebugMode", Me.ParentModel.ParentControl.SharedActiveShader.Debugmode)

            ' Alpha global
            Me.ParentModel.ParentControl.SharedActiveShader.SetFloat("alpha", material.MaterialBase.Alpha)

            ' === Depth Test ===
            If material.MaterialBase.ZBufferTest OrElse (material.HasAlphaBlend = False) Then
                GL.Enable(EnableCap.DepthTest)
                GL.DepthFunc(DepthFunction.Lequal)   ' o el que uses por defecto
            Else
                GL.Disable(EnableCap.DepthTest)
            End If

            ' === Depth Write ===
            Dim writeDepth As Boolean
            If material.HasAlphaBlend Or MeshData.Shape.Wireframe Then
                writeDepth = False
            Else
                writeDepth = material.MaterialBase.ZBufferWrite
            End If
            GL.DepthMask(writeDepth)

            ' === Blending / Alpha Test / Wireframe ===
            If MeshData.Shape.Wireframe Then
                ' Pasada en modo wireframe
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line)
                GL.Enable(EnableCap.Blend)
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
            ElseIf material.HasAlphaBlend Then
                ' Blending estándar
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Enable(EnableCap.Blend)
                Dim blend = material.Calculate_Blending()
                GL.BlendFunc(CType(blend(0), BlendingFactor), CType(blend(1), BlendingFactor))
            ElseIf material.HasAlphaTest Then
                ' Alpha test (recorte)
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            Else
                ' Material completamente opaco
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            End If

            ' === Culling ===
            If material.MaterialBase.TwoSided Then
                GL.Disable(EnableCap.CullFace)
            Else
                GL.Enable(EnableCap.CullFace)
            End If
        End Sub
        Public Sub ExportMeshToOBJ(rutaArchivo As String)
            Using sw As New StreamWriter(rutaArchivo, False, Encoding.UTF8)

                sw.WriteLine("# Exportado por ExportMeshToOBJ")
                sw.WriteLine("# Shape: " & MeshData.ShapeName)

                ' 🔷 Vértices
                For Each v In MeshData.Meshgeometry.Vertices
                    sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z))
                Next

                ' 🔷 Normales
                If MeshData.Meshgeometry.Normals IsNot Nothing AndAlso MeshData.Meshgeometry.Normals.Length = MeshData.Meshgeometry.Vertices.Length Then
                    For Each n In MeshData.Meshgeometry.Normals
                        sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "vn {0} {1} {2}", n.X, n.Y, n.Z))
                    Next
                End If

                ' 🔷 UVs
                If MeshData.Meshgeometry.Uvs_Weight IsNot Nothing AndAlso MeshData.Meshgeometry.Uvs_Weight.Length = MeshData.Meshgeometry.Vertices.Length Then
                    For Each uv In MeshData.Meshgeometry.Uvs_Weight
                        sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "vt {0} {1}", uv.X, 1 - uv.Y)) ' invertir V
                    Next
                End If

                ' 🔷 Caras (triángulos)
                Dim tieneUV As Boolean = MeshData.Meshgeometry.Uvs_Weight IsNot Nothing AndAlso MeshData.Meshgeometry.Uvs_Weight.Length = MeshData.Meshgeometry.Vertices.Length
                Dim tieneNorm As Boolean = MeshData.Meshgeometry.Normals IsNot Nothing AndAlso MeshData.Meshgeometry.Normals.Length = MeshData.Meshgeometry.Vertices.Length

                For i = 0 To MeshData.Meshgeometry.Indices.Length - 1 Step 3
                    Dim i1 = MeshData.Meshgeometry.Indices(i) + 1
                    Dim i2 = MeshData.Meshgeometry.Indices(i + 1) + 1
                    Dim i3 = MeshData.Meshgeometry.Indices(i + 2) + 1

                    Dim f1 As String = i1.ToString()
                    Dim f2 As String = i2.ToString()
                    Dim f3 As String = i3.ToString()

                    If tieneUV AndAlso tieneNorm Then
                        f1 &= "/" & i1 & "/" & i1
                        f2 &= "/" & i2 & "/" & i2
                        f3 &= "/" & i3 & "/" & i3
                    ElseIf tieneUV Then
                        f1 &= "/" & i1
                        f2 &= "/" & i2
                        f3 &= "/" & i3
                    ElseIf tieneNorm Then
                        f1 &= "//" & i1
                        f2 &= "//" & i2
                        f3 &= "//" & i3
                    End If

                    sw.WriteLine("f " & f1 & " " & f2 & " " & f3)
                Next

            End Using
        End Sub

        Protected Overrides Sub Finalize()
            MyBase.Finalize()
        End Sub
    End Class

    Public Sub New(Parent_control As PreviewControl)
        ParentControl = Parent_control
    End Sub

    Public Sub Processing_Status_GL(text As String)
        Me.ParentControl.Processing_Status(text)
    End Sub
    Public Sub LoadShapesParallel(shapes As IEnumerable(Of Shape_class))
        If Not shapes.Any() Then Exit Sub
        Last_rendered = shapes(0).ParentSliderSet
        Skeleton_Class.AppplyPoseToSkeleton(Last_Pose)
        Dim result As New ConcurrentBag(Of RenderableMesh)
        Parallel.ForEach(shapes, Sub(shape)
                                     Dim mesh = LoadShapeSafe(shape)

                                     If mesh IsNot Nothing Then result.Add(mesh)
                                 End Sub)
        meshes.AddRange(result)
    End Sub

    Public Sub BakeOrInvertPose(inverse As Boolean)
        If IsNothing(Last_rendered) Then Exit Sub
        For Each shap In Last_rendered.Shapes
            BakeOrInvertPose(shap, inverse)
        Next
    End Sub

    Public Sub BakeOrInvertPose(Shape As Shape_class, inverse As Boolean)
        Dim mesh = Me.meshes.Where(Function(pf) pf.MeshData.Shape Is Shape).First
        SkinningHelper.BakeFromMemoryUsingOriginal(Shape, mesh.MeshData.Meshgeometry, ApplyPose:=HasPose, inverse:=inverse, ApplyMorph:=False, RemoveZaps:=False, SingleBoneSkinning)
    End Sub

    Public ReadOnly Property HasPose
        Get
            If Skeleton_Class.HasSkeleton AndAlso Not IsNothing(Last_Pose) AndAlso Last_Pose.Source <> Poses_class.Pose_Source_Enum.None Then Return True
            Return False
        End Get
    End Property

    Private Function LoadShapeSafe(shape As Shape_class) As RenderableMesh
        Try
            ' 1) Obtener shape y datos de skin (siempre BSTriShape)
            If IsNothing(shape.RelatedNifShape) Then Return Nothing
            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shape, ApplyPose:=HasPose, SingleBoneSkinning, RecalculateNormals)

            ' 2) Rellenar MeshData con la geometría final
            Dim mesh As New RenderableMesh.MeshData_Class With {
                .Shape = shape,
                .Meshgeometry = geom
                        }
            mesh.Material = New RenderableMesh.MaterialData(mesh)
            Dim Renderable = New RenderableMesh(mesh, Me)

            Return Renderable
        Catch ex As Exception
            Debug.Print("[EXCEPTION] " & ex.Message)
            Debugger.Break()
            Return Nothing
        End Try
    End Function

    Public Sub Setup_GL()
        Process_Indices_GL()
        Process_Textures_GL()
        ParentControl.RenderTimer.Start()
        ParentControl.UpdateProjection(True)  ' ← ya hay meshes/bounds; ajusta frustum
        Can_Render = True
        Cleaned = False
    End Sub

    Private Sub Process_Indices_GL()
        ParentControl.MakeCurrent()
        For Each mesh In meshes
            mesh.SetupMesh_GL()
        Next
    End Sub

    Private ReadOnly Last_Loaded_Textures As New HashSet(Of String)
    Public Sub Process_Textures_GL()
        Me.ParentControl.MakeCurrent()
        Dim texturas As New HashSet(Of String)
        texturas.UnionWith(Me.meshes.SelectMany(Function(pf) pf.MeshData.Material.Textures_Path_List).Where(Function(pf) pf <> "").Distinct().Where(Function(pf) Textures_Dictionary.ContainsKey(pf) = False))
        texturas.ExceptWith(Last_Loaded_Textures)
        If texturas.Count > 0 Then
            Last_Loaded_Textures.UnionWith(texturas)
            Me.ParentControl.Processing_Status("Texturing")
            Dim agregar = Load_And_GenerateOpenGLTextures_FromDictionary(texturas.ToArray, True, True)
            For Each a In agregar
                Textures_Dictionary.Add(a.Key, a.Value)
            Next
        End If

    End Sub

    Public Sub CleanTextures()
        ' — Eliminar texturas cargadas —
        Dim seen As New HashSet(Of UInteger)
        For Each texID In Textures_Dictionary.Values.Select(Function(pf) pf.Texture_ID)
            If texID > 0 AndAlso Not seen.Contains(texID) Then
                GL.DeleteTexture(texID)
                seen.Add(texID)
            End If
        Next
        ' Limpia diccionario
        Textures_Dictionary.Clear()
        Last_Loaded_Textures.Clear()
    End Sub

    Public Sub Clean(ShowText As Boolean)
        Cleaned = True
        Can_Render = False
        If Not IsNothing(ParentControl.RenderTimer) Then ParentControl.RenderTimer.Stop()
        ParentControl.MakeCurrent()
        ParentControl.updateRequired = True
        If ShowText Then Me.ParentControl.Processing_Status("Cleaned")
        ' Limpia meshes internamente
        For Each mesh In meshes
            mesh.Clean()
        Next
        ' Borra Meshes
        meshes.Clear()

        Dim i = 0
        While GL.GetError() <> ErrorCode.NoError
            i += 1
            If i > 10 Then Debugger.Break() : Exit While
        End While
    End Sub
    Structure MeshDepth
        Public Mesh As RenderableMesh
        Public Depth As Single
    End Structure
    Public Sub RenderAll(projection As Matrix4, camera As OrbitCamera)
        ' Clasificación por tipo de alpha
        Dim cutout = meshes.Where(Function(m) Not m.MeshData.Material?.HasAlphaBlend AndAlso m.MeshData.Material?.HasAlphaTest AndAlso Not m.MeshData.Shape.Wireframe).OrderBy(Function(pf) pf.MeshData.Idx)
        Dim opaque = meshes.Where(Function(m) Not m.MeshData.Material?.HasAlphaBlend AndAlso Not m.MeshData.Material?.HasAlphaTest AndAlso Not m.MeshData.Shape.Wireframe).OrderBy(Function(pf) pf.MeshData.Idx)
        Dim blended = meshes.Where(Function(m) m.MeshData.Material?.HasAlphaBlend OrElse m.MeshData.Shape.Wireframe).OrderBy(Function(pf) pf.MeshData.Idx)

        ' 1. OPAQUE — sin blending, depth write habilitado
        For Each mesh In opaque
            mesh.Render(projection, camera)
        Next

        ' 2. CUTOUT — alpha test, sin blending, depth write habilitado
        For Each mesh In cutout
            mesh.Render(projection, camera)
        Next

        ' 3. BLENDED — requiere ordenamiento por profundidad
        Dim viewMatrix = camera.GetViewMatrix()
        Dim sorted = blended.Select(Function(m)
                                        Dim viewPos = Vector3.TransformPosition(m.MeshData.Meshgeometry.Boundingcenter, viewMatrix)
                                        Return New MeshDepth With {.Mesh = m, .Depth = -viewPos.Z}
                                    End Function).OrderByDescending(Function(x) x.Depth).Select(Function(x) x.Mesh).ToList()
        For Each mesh In sorted
            mesh.Render(projection, camera)
        Next
    End Sub
End Class

Public Class OrbitCamera
    ' Para modo orbit
    Friend angleX As Single
    Friend angleY As Single
    Public distance As Single

    Public Optimaldistance As Single = 0
    Public Property FocusPosition As Vector3
    Public Property MinDistance As Single = 20
    Public Property MaxDistance As Single = 900

    Public Property Forward As Vector3
    Public upWorld As Vector3 = Vector3.UnitZ
    Public right As Vector3 = Vector3.Normalize(Vector3.Cross(Forward, upWorld))
    Public upPlane As Vector3 = Vector3.Normalize(Vector3.Cross(right, Forward))

    Public Property Up As Vector3

    Public Sub New()
        angleX = 0
        angleY = 0
        distance = 167
        FocusPosition = Vector3.Zero
        UpdateDirectionFromAngles()
        Up = Vector3.UnitZ         ' ← cambio aquí: Z-up
    End Sub

    Public Sub UpdateDirectionFromAngles()
        Dim cosElev = CSng(Math.Cos(angleY))
        Dim sinElev = CSng(Math.Sin(angleY))
        Dim cosAz = CSng(Math.Cos(angleX))
        Dim sinAz = CSng(Math.Sin(angleX))
        Forward = Vector3.Normalize(New Vector3(cosElev * sinAz, cosElev * cosAz, sinElev))
        right = Vector3.Normalize(Vector3.Cross(Forward, upWorld))
        upPlane = Vector3.Normalize(Vector3.Cross(right, Forward))
    End Sub

    Public Sub Rotate(dx As Single, dy As Single)
        angleX += dx * pixelScale
        angleY = Math.Clamp(angleY + dy * pixelScale, -1.5F, 1.5F)
        UpdateDirectionFromAngles()
    End Sub
    Public Sub PanWorld(dx As Single, dy As Single)
        FocusPosition += (-dx) * right + dy * upPlane
    End Sub

    ' Variante: pan a partir de arrastre en pantalla (dx,dy en píxeles).
    ' pixelScale: cuánto vale 1 píxel en unidades de mundo a la distancia actual.
    Public Sub PanScreen(dxPixels As Single, dyPixels As Single)
        Dim pixelScale2 As Single = distance * pixelScale * 0.2F
        PanWorld(-dxPixels * pixelScale2, dyPixels * pixelScale2)
    End Sub

    Const pixelScale As Single = 0.01F

    Public Sub Zoom(delta As Single)
        Dim factor As Single = MathF.Exp(-pixelScale * 5 * delta)   ' acercar: steps>0 ⇒ reduce distancia
        distance = Math.Clamp(distance * factor, MinDistance, MaxDistance)
    End Sub

    Public Function GetViewMatrix() As Matrix4
        Dim upDir As New Vector3(0, 0, 1)   ' Z-up
        Dim eye, target As Vector3

        eye = FocusPosition + Forward * distance
        target = FocusPosition

        Return Matrix4.LookAt(eye, target, upDir)
    End Function

    Public Function GetEyePosition() As Vector3
        Return FocusPosition + Forward * distance
    End Function
End Class


