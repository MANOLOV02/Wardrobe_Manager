Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.IO
Imports System.Reflection
Imports Material_Editor
Imports Material_Editor.BaseMaterialFile
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Graphics.ES11
Imports OpenTK.Graphics.OpenGL

<AttributeUsage(AttributeTargets.Property)>
Public Class BGSMOnlyAttribute
    Inherits Attribute
End Class

<AttributeUsage(AttributeTargets.Property)>
Public Class BGEMOnlyAttribute
    Inherits Attribute
End Class

Public Class FO4UnifiedMaterialDescriptor
    Inherits CustomTypeDescriptor

    Private ReadOnly instance As FO4UnifiedMaterial_Class

    Public Sub New(parent As ICustomTypeDescriptor, instance As FO4UnifiedMaterial_Class)
        MyBase.New(parent)
        Me.instance = instance
    End Sub

    Public Overrides Function GetProperties(attributes As Attribute()) As PropertyDescriptorCollection
        Dim props As PropertyDescriptorCollection = MyBase.GetProperties(attributes)
        Return FilterProperties(props)
    End Function

    Public Overrides Function GetProperties() As PropertyDescriptorCollection
        Dim props As PropertyDescriptorCollection = MyBase.GetProperties()
        Return FilterProperties(props)
    End Function

    Private Function FilterProperties(props As PropertyDescriptorCollection) As PropertyDescriptorCollection
        Dim filtered As New List(Of PropertyDescriptor)()
        Dim currentType As Type = instance.Underlying_Material.GetType()

        For Each prop As PropertyDescriptor In props
            If prop.Attributes(GetType(BGSMOnlyAttribute)) IsNot Nothing AndAlso currentType IsNot GetType(BGSM) Then
                Continue For
            End If
            If prop.Attributes(GetType(BGEMOnlyAttribute)) IsNot Nothing AndAlso currentType IsNot GetType(BGEM) Then
                Continue For
            End If
            filtered.Add(prop)
        Next

        Return New PropertyDescriptorCollection(filtered.ToArray())
    End Function
End Class

Public Class FO4UnifiedMaterialProvider
    Inherits TypeDescriptionProvider

    Private ReadOnly baseProvider As TypeDescriptionProvider

    Public Sub New()
        MyBase.New(TypeDescriptor.GetProvider(GetType(FO4UnifiedMaterial_Class)))
        baseProvider = TypeDescriptor.GetProvider(GetType(FO4UnifiedMaterial_Class))
    End Sub

    Public Overrides Function GetTypeDescriptor(objectType As Type, instance As Object) As ICustomTypeDescriptor
        Dim defaultDescriptor As ICustomTypeDescriptor = baseProvider.GetTypeDescriptor(objectType, instance)
        Return New FO4UnifiedMaterialDescriptor(defaultDescriptor, CType(instance, FO4UnifiedMaterial_Class))
    End Function
End Class

<TypeDescriptionProvider(GetType(FO4UnifiedMaterialProvider))>
Public Class FO4UnifiedMaterial_Class
    <Browsable(False)>
    Public Property Underlying_Material As Material_Editor.BaseMaterialFile = New BGEM
    <Browsable(False)>
    Public Property MaskWrites As MaskWriteFlags
        Get
            Return Underlying_Material.MaskWrites
        End Get
        Set(value As MaskWriteFlags)
            Underlying_Material.MaskWrites = value
        End Set
    End Property

    <Category("(Type)")>
    Public ReadOnly Property MaterialType As Type
        Get
            Return Underlying_Material.GetType
        End Get
    End Property

    <Category("Alpha")>
    <DefaultValue(AlphaBlendModeType.Unknown)>
    Public Property AlphaBlendMode As Material_Editor.BaseMaterialFile.AlphaBlendModeType
        Get
            Return Underlying_Material.AlphaBlendMode
        End Get
        Set(value As Material_Editor.BaseMaterialFile.AlphaBlendModeType)
            Underlying_Material.AlphaBlendMode = value
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property NormalTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).NormalTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).NormalTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).NormalTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).NormalTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property Diffuse_or_Base_Texture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DiffuseTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BaseTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DiffuseTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property SmoothSpecTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SmoothSpecTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SmoothSpecTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GreyscaleTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GreyscaleTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).GrayscaleTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GreyscaleTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).GrayscaleTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GlowTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GlowTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).GlowTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GlowTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).GlowTexture = value
            End Select
        End Set
    End Property
    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property EnvmapTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EnvmapTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EnvmapTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EnvmapTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EnvmapTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property SpecularTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SpecularTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SpecularTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property LightingTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).LightingTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).LightingTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).LightingTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).LightingTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property FlowTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).FlowTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).FlowTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property DisplacementTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DisplacementTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DisplacementTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property InnerLayerTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).InnerLayerTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).InnerLayerTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property WrinklesTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).WrinklesTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).WrinklesTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property DistanceFieldAlphaTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DistanceFieldAlphaTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DistanceFieldAlphaTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGEMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property EnvmapMaskTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return ""
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EnvmapMaskTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No operation
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EnvmapMaskTexture = value
            End Select
        End Set
    End Property
    <Category("Alpha")>
    <DefaultValue(1.0F)>
    Public Property Alpha As Single
        Get
            Return Underlying_Material.Alpha
        End Get
        Set(value As Single)
            Underlying_Material.Alpha = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(0F)>
    Public Property UOffset As Single
        Get
            Return Underlying_Material.UOffset
        End Get
        Set(value As Single)
            Underlying_Material.UOffset = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(0F)>
    Public Property VOffset As Single
        Get
            Return Underlying_Material.VOffset
        End Get
        Set(value As Single)
            Underlying_Material.VOffset = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(1.0F)>
    Public Property UScale As Single
        Get
            Return Underlying_Material.UScale
        End Get
        Set(value As Single)
            Underlying_Material.UScale = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(1.0F)>
    Public Property VScale As Single
        Get
            Return Underlying_Material.VScale
        End Get
        Set(value As Single)
            Underlying_Material.VScale = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(False)>
    Public Property AlphaTest As Boolean
        Get
            Return Underlying_Material.AlphaTest
        End Get
        Set(value As Boolean)
            Underlying_Material.AlphaTest = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(CType(128, Byte))>
    Public Property AlphaTestRef As Byte
        Get
            Return Underlying_Material.AlphaTestRef
        End Get
        Set(value As Byte)
            Underlying_Material.AlphaTestRef = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(False)>
    Public Property Decal As Boolean
        Get
            Return Underlying_Material.Decal
        End Get
        Set(value As Boolean)
            Underlying_Material.Decal = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(False)>
    Public Property DepthBias As Boolean
        Get
            Return Underlying_Material.DepthBias
        End Get
        Set(value As Boolean)
            Underlying_Material.DepthBias = value
        End Set
    End Property

    <Category("Coloring")>
    <DefaultValue(False)>
    Public Property GrayscaleToPaletteColor As Boolean
        Get
            Return Underlying_Material.GrayscaleToPaletteColor
        End Get
        Set(value As Boolean)
            Underlying_Material.GrayscaleToPaletteColor = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(False)>
    Public Property TwoSided As Boolean
        Get
            Return Underlying_Material.TwoSided
        End Get
        Set(value As Boolean)
            Underlying_Material.TwoSided = value
        End Set
    End Property


    <Category("Lighting")>
    Public Property EnvironmentMapping As Boolean
        Get
            Return Underlying_Material.EnvironmentMapping
        End Get
        Set(value As Boolean)
            Underlying_Material.EnvironmentMapping = value
        End Set
    End Property

    <Category("Lighting")>
    Public Property EnvironmentMappingMaskScale As Single
        Get
            Return Underlying_Material.EnvironmentMappingMaskScale
        End Get
        Set(value As Single)
            Underlying_Material.EnvironmentMappingMaskScale = value
        End Set
    End Property

    <Category("Lighting")>
    Public Property NonOccluder As Boolean
        Get
            Return Underlying_Material.NonOccluder
        End Get
        Set(value As Boolean)
            Underlying_Material.NonOccluder = value
        End Set
    End Property

    <Category("Lighting")>
    Public Property Refraction As Boolean
        Get
            Return Underlying_Material.Refraction
        End Get
        Set(value As Boolean)
            Underlying_Material.Refraction = value
        End Set
    End Property

    <Category("Lighting")>
    Public Property RefractionFalloff As Boolean
        Get
            Return Underlying_Material.RefractionFalloff
        End Get
        Set(value As Boolean)
            Underlying_Material.RefractionFalloff = value
        End Set
    End Property

    <Category("Lighting")>
    Public Property RefractionPower As Single
        Get
            Return Underlying_Material.RefractionPower
        End Get
        Set(value As Single)
            Underlying_Material.RefractionPower = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(True)>
    Public Property TileU As Boolean
        Get
            Return Underlying_Material.TileU
        End Get
        Set(value As Boolean)
            Underlying_Material.TileU = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(True)>
    Public Property TileV As Boolean
        Get
            Return Underlying_Material.TileV
        End Get
        Set(value As Boolean)
            Underlying_Material.TileV = value
        End Set
    End Property

    <Category("(Type)")>
    Public ReadOnly Property Version As UInteger
        Get
            Return Underlying_Material.Version
        End Get
    End Property

    <Category("Lighting")>
    Public Property WetnessControlScreenSpaceReflections As Boolean
        Get
            Return Underlying_Material.WetnessControlScreenSpaceReflections
        End Get
        Set(value As Boolean)
            Underlying_Material.WetnessControlScreenSpaceReflections = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(True)>
    Public Property ZBufferTest As Boolean
        Get
            Return Underlying_Material.ZBufferTest
        End Get
        Set(value As Boolean)
            Underlying_Material.ZBufferTest = value
        End Set
    End Property

    <Category("Alpha")>
    <DefaultValue(True)>
    Public Property ZBufferWrite As Boolean
        Get
            Return Underlying_Material.ZBufferWrite
        End Get
        Set(value As Boolean)
            Underlying_Material.ZBufferWrite = value
        End Set
    End Property
    <Category("Coloring")>
    <DefaultValue(0.5F)>
    Public Property GrayscaleToPaletteScale As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GrayscaleToPaletteScale
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BaseColorScale
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GrayscaleToPaletteScale = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseColorScale = value
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property SpecularEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularEnabled
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularEnabled = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(False)>
    Public Property ModelSpaceNormals As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).ModelSpaceNormals
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).ModelSpaceNormals = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property EmitEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EmitEnabled
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmitEnabled = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property SubsurfaceLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SubsurfaceLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SubsurfaceLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    Public Property BackLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).BackLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).BackLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    Public Property BackLightPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).BackLightPower
                Case GetType(BGEM)
                    Return 1
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).BackLightPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    Public Property RimLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RimLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RimLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    Public Property RimPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RimPower
                Case GetType(BGEM)
                    Return 1
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RimPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property


    <Category("Lighting")>
    Public Property Glowmap As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Glowmap
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).Glowmap
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Glowmap = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).Glowmap = value
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    Public Property Smoothness As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Smoothness
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Smoothness = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property


    <Category("Lighting")>
    <BGSMOnly()>
    Public Property SpecularColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).SpecularColor)
                Case GetType(BGEM)
                    Return System.Drawing.Color.FromArgb(255, 255, 255, 255)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularColor = ColorToUInteger(value)
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    Public Property EmittanceColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).EmittanceColor)
                Case GetType(BGEM)
                    Return UIntegerToColor(CType(Underlying_Material, BGEM).EmittanceColor)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmittanceColor = ColorToUInteger(value)
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EmittanceColor = ColorToUInteger(value)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property SpecularMult As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularMult
                Case GetType(BGEM)
                    Return 1
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularMult = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property EmittanceMult As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EmittanceMult
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmittanceMult = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property FresnelPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).FresnelPower
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).FresnelPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property SubsurfaceLightingRolloff As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SubsurfaceLightingRolloff
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SubsurfaceLightingRolloff = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    ' Propiedades exclusivas BGSM

    <Category("Material")>
    <BGSMOnly()>
    Public Property RootMaterialPath As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RootMaterialPath
                Case GetType(BGEM)
                    Return ""
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RootMaterialPath = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property PBR As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).PBR
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).PBR = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property CustomPorosity As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).CustomPorosity
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).CustomPorosity = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property PorosityValue As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).PorosityValue
                Case GetType(BGEM)
                    Return 0
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).PorosityValue = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property Hair As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Hair
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Hair = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property HairTintColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).HairTintColor)
                Case GetType(BGEM)
                    Return Color.Black
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).HairTintColor = ColorToUInteger(value)
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property Tree As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Tree
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Tree = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property Facegen As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Facegen
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Facegen = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Material")>
    <BGSMOnly()>
    Public Property SkinTint As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SkinTint
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SkinTint = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property
    ' Propiedades exclusivas BGEM

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffColorEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffColorEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffColorEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffStartAngle As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStartAngle
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStartAngle = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffStopAngle As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStopAngle
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStopAngle = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffStartOpacity As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStartOpacity
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStartOpacity = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Falloff")>
    <BGEMOnly>
    Public Property FalloffStopOpacity As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStopOpacity
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStopOpacity = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property LightingInfluence As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).LightingInfluence
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).LightingInfluence = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property BloodEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BloodEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BloodEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property


    <Category("Lighting")>
    <BGEMOnly>
    Public Property EffectLightingEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EffectLightingEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EffectLightingEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property SoftEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SoftEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SoftEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property SoftDepth As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SoftDepth
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SoftDepth = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property EffectPbrSpecular As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EffectPbrSpecular
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EffectPbrSpecular = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property AdaptativeEmissive_ExposureOffset As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_ExposureOffset
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_ExposureOffset = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property AdaptativeEmissive_FinalExposureMin As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMin
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMin = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGEMOnly>
    Public Property AdaptativeEmissive_FinalExposureMax As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMax
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMax = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property
    Public Shared Function Determine_Alphablend(Source As NiflySharp.Enums.AlphaFunction, dest As NiflySharp.Enums.AlphaFunction) As AlphaBlendModeType

        If Source = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dest = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA Then Return AlphaBlendModeType.Standard

        If Source = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dest = NiflySharp.Enums.AlphaFunction.ONE Then Return AlphaBlendModeType.Additive

        If Source = NiflySharp.Enums.AlphaFunction.DEST_COLOR AndAlso dest = NiflySharp.Enums.AlphaFunction.ZERO Then Return AlphaBlendModeType.Multiplicative

        If Source = NiflySharp.Enums.AlphaFunction.ONE AndAlso dest = NiflySharp.Enums.AlphaFunction.ZERO Then Return AlphaBlendModeType.None

        Return AlphaBlendModeType.Unknown
    End Function
    Public Shared Function Determine_AlphaFlags(Mode As AlphaBlendModeType) As NiflySharp.Enums.AlphaFunction()
        If Mode = AlphaBlendModeType.Standard Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
        If Mode = AlphaBlendModeType.Additive Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.ONE}
        If Mode = AlphaBlendModeType.Multiplicative Then Return {NiflySharp.Enums.AlphaFunction.DEST_COLOR, NiflySharp.Enums.AlphaFunction.ZERO}
        If Mode = AlphaBlendModeType.Unknown Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
        If Mode = AlphaBlendModeType.None Then Return {NiflySharp.Enums.AlphaFunction.ONE, NiflySharp.Enums.AlphaFunction.ZERO}
        Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
    End Function
    Public Sub Create_From_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty)
        If Nif.Valid = False Then Exit Sub
        Dim mat As BGSM
        If Not IsNothing(shad) Then
            mat = New BGSM With {
            .TwoSided = shad.DoubleSided,
            .UOffset = shad.UVOffset.U,
            .VOffset = shad.UVOffset.V,
            .UScale = shad.UVScale.U,
            .VScale = shad.UVScale.V,
            .EmitEnabled = shad.Emissive,
            .EmittanceColor = NifColorColorToUInteger(shad.EmissiveColor),
            .EnvironmentMappingMaskScale = shad.EnvironmentMapScale,
            .ModelSpaceNormals = shad.ModelSpace,
            .BackLighting = shad.HasBacklight
                                                      }

            ' REVISARRRRR!!!!!!!
            If Not IsNothing(shad.TextureSetRef) AndAlso shad.TextureSetRef.Index <> -1 Then
                mat.DiffuseTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(0).Content
                mat.NormalTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(1).Content
                mat.GlowTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(2).Content
                mat.DisplacementTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(3).Content
                mat.EnvmapTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(4).Content
                mat.FlowTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(5).Content
                mat.LightingTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(6).Content
                mat.SmoothSpecTexture = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet).Textures(7).Content
            End If
        Else
            mat = New BGSM
        End If
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        If Not IsNothing(shap.AlphaPropertyRef) AndAlso shap.AlphaPropertyRef.Index <> -1 Then
            Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
            mat.AlphaTest = alp.Flags.AlphaTest
            mat.AlphaTestRef = alp.Threshold
            If alp.Flags.AlphaBlend Then
                mat.AlphaBlendMode = Determine_Alphablend(alp.Flags.SourceBlendMode, alp.Flags.DestinationBlendMode)
                If alp.Flags.TestFunc = Enums.TestFunction.TEST_NEVER Or (alp.Flags.TestFunc = Enums.TestFunction.TEST_GREATER And mat.AlphaTestRef = 0) Then
                    mat.AlphaBlendMode = AlphaBlendModeType.None
                End If
            Else
                mat.AlphaBlendMode = AlphaBlendModeType.None
            End If
        End If
        Underlying_Material = mat
    End Sub


    Public Sub Create_From_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty)
        If Nif.Valid = False Then Exit Sub
        Dim mat As BGEM
        If Not IsNothing(shad) Then
            mat = New BGEM With {
            .TwoSided = shad.DoubleSided,
            .BaseTexture = If(shad._sourceTexture?.Content, String.Empty),
            .GrayscaleTexture = If(shad._greyscaleTexture?.Content, String.Empty),
            .NormalTexture = If(shad._normalTexture?.Content, String.Empty),
            .EnvmapMaskTexture = If(shad._envMaskTexture?.Content, String.Empty),
            .EnvmapTexture = If(shad._envMapTexture?.Content, String.Empty), ' ← ojo con esto
            .LightingTexture = If(shad._lightingTexture?.Content, String.Empty),
            .SpecularTexture = If(shad._reflectanceTexture?.Content, String.Empty),
            .GlowTexture = If(shad._emitGradientTexture?.Content, String.Empty),
            .UOffset = shad.UVOffset.U,
            .VOffset = shad.UVOffset.V,
            .UScale = shad.UVScale.U,
            .VScale = shad.UVScale.V,
            .EnvironmentMappingMaskScale = shad.EnvironmentMapScale,
            .EmittanceColor = ColorToUInteger(NifColorToColor(shad._emittanceColor))
            }
        Else
            mat = New BGEM
        End If
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        If Not IsNothing(shap.AlphaPropertyRef) AndAlso shap.AlphaPropertyRef.Index <> -1 Then
            Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
            mat.AlphaTest = alp.Flags.AlphaTest
            mat.AlphaTestRef = alp.Threshold
            If alp.Flags.AlphaBlend Then
                mat.AlphaBlendMode = Determine_Alphablend(alp.Flags.SourceBlendMode, alp.Flags.DestinationBlendMode)
                If alp.Flags.TestFunc = Enums.TestFunction.TEST_NEVER Or (alp.Flags.TestFunc = Enums.TestFunction.TEST_GREATER And mat.AlphaTestRef = 0) Then
                    mat.AlphaBlendMode = AlphaBlendModeType.None
                End If
            Else
                mat.AlphaBlendMode = AlphaBlendModeType.None
            End If
        End If
        Underlying_Material = mat
    End Sub
    Public Shared Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty, Mat As BGEM)
        If Nif.Valid = False Then Exit Sub
        shad.DoubleSided = Mat.TwoSided
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad._environmentMapScale = Mat.EnvironmentMappingMaskScale
        shad._emittanceColor = UIntegerToNifColor3(Mat.EmittanceColor)
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        If IsNothing(shad._sourceTexture) Then
            shad._sourceTexture = New NiString4(Mat.BaseTexture)
        Else
            shad._sourceTexture.Content = Mat.BaseTexture
        End If


        If IsNothing(shad._normalTexture) Then
            shad._normalTexture = New NiString4(Mat.NormalTexture)
        Else
            shad._normalTexture.Content = Mat.NormalTexture
        End If

        If IsNothing(shad._greyscaleTexture) Then
            shad._greyscaleTexture = New NiString4(Mat.GrayscaleTexture)
        Else
            shad._greyscaleTexture.Content = Mat.GrayscaleTexture
        End If

        If IsNothing(shad._envMaskTexture) Then
            shad._envMaskTexture = New NiString4(Mat.EnvmapTexture)
        Else
            shad._envMaskTexture.Content = Mat.EnvmapTexture
        End If

        If IsNothing(shad._lightingTexture) Then
            shad._lightingTexture = New NiString4(Mat.LightingTexture)
        Else
            shad._lightingTexture.Content = Mat.LightingTexture
        End If

        If IsNothing(shad._reflectanceTexture) Then
            shad._reflectanceTexture = New NiString4(Mat.SpecularTexture)
        Else
            shad._reflectanceTexture.Content = Mat.SpecularTexture
        End If

        If IsNothing(shad._emitGradientTexture) Then
            shad._emitGradientTexture = New NiString4(Mat.GlowTexture)
        Else
            shad._emitGradientTexture.Content = Mat.GlowTexture
        End If

        If IsNothing(shap.AlphaPropertyRef) OrElse shap.AlphaPropertyRef.Index = -1 Then
            shap.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = Nif.AddBlock(New NiAlphaProperty)}
        End If
        Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
        alp.Flags.AlphaTest = Mat.AlphaTest
        alp.Threshold = Mat.AlphaTestRef
        alp.Flags.AlphaBlend = Not Mat.AlphaBlendMode = AlphaBlendModeType.None
        Dim func = Determine_AlphaFlags(Mat.AlphaBlendMode)
        alp.Flags.SourceBlendMode = func(0)
        alp.Flags.DestinationBlendMode = func(1)
    End Sub
    Public Shared Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty, Mat As BGSM)
        If Nif.Valid = False Then Exit Sub
        shad.DoubleSided = Mat.TwoSided
        Dim TextSet = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet)
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad.Emissive = Mat.EmitEnabled
        shad.EmissiveColor = UIntegerToNifColor4(Mat.EmittanceColor)
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        shad.ModelSpace = Mat.ModelSpaceNormals
        shad.HasBacklight = Mat.BackLighting

        If IsNothing(shad.TextureSetRef) OrElse shad.TextureSetRef.Index = -1 Then
            Dim texset1 = New BSShaderTextureSet
            shad._textureSet = New NiBlockRef(Of BSShaderTextureSet) With {.Index = Nif.AddBlock(texset1)}
            texset1.Textures = New List(Of NiString4)
        End If

        Dim texset = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet)

        While texset.Textures.Count < 8
            texset.Textures.Add(New NiString4 With {.Content = ""})
        End While

        ' REVISARRRRR!!!!!!!
        texset.Textures(0).Content = Mat.DiffuseTexture
        texset.Textures(1).Content = Mat.NormalTexture
        texset.Textures(2).Content = Mat.GlowTexture
        texset.Textures(3).Content = Mat.DisplacementTexture
        texset.Textures(4).Content = Mat.EnvmapTexture
        texset.Textures(5).Content = Mat.FlowTexture
        texset.Textures(6).Content = Mat.LightingTexture
        texset.Textures(7).Content = Mat.SmoothSpecTexture

        If IsNothing(shap.AlphaPropertyRef) OrElse shap.AlphaPropertyRef.Index = -1 Then
            shap.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = Nif.AddBlock(New NiAlphaProperty)}
        End If
        Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
        alp.Flags.AlphaTest = Mat.AlphaTest
        alp.Threshold = Mat.AlphaTestRef
        alp.Flags.AlphaBlend = Not Mat.AlphaBlendMode = AlphaBlendModeType.None
        Dim func = Determine_AlphaFlags(Mat.AlphaBlendMode)
        alp.Flags.SourceBlendMode = func(0)
        alp.Flags.DestinationBlendMode = func(1)
    End Sub
    Public Sub Deserialize(Memory As Byte(), type As Type)
        If Memory.Length = 0 Then Exit Sub
        Using ms As New MemoryStream(Memory)
            Using reader As New BinaryReader(ms)
                Select Case type
                    Case GetType(BGSM)
                        Underlying_Material = New BGSM
                    Case GetType(BGEM)
                        Underlying_Material = New BGEM
                    Case Else
                        Throw New Exception("Tipo no soportado en Deserialize.")
                End Select
                Underlying_Material.Deserialize(reader)
                reader.Close()
            End Using
            ms.Close()
        End Using
    End Sub

    Public Sub Deserialize(Diccionario As String, type As Type)
        Deserialize(FilesDictionary_class.GetBytes(Diccionario), type)
    End Sub

    Private Shared Function UIntegerToColor(color As UInteger) As Color
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return System.Drawing.Color.FromArgb(255, r, g, b)
    End Function
    Private Shared Function UIntegerToNifColor3(color As UInteger) As NiflySharp.Structs.Color3
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return New Color3(r / 255, b / 255, g / 255)
    End Function
    Private Shared Function UIntegerToNifColor4(color As UInteger) As NiflySharp.Structs.Color4
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return New Color4(r / 255, b / 255, g / 255, 1)
    End Function
    Public Shared Function NifColorColorToUInteger(color As NiflySharp.Structs.Color4) As UInteger
        Return ColorToUInteger(System.Drawing.Color.FromArgb(color.A * 255, color.R * 255, color.G * 255, color.B * 255))
    End Function
    Public Shared Function NifColorToColor(color As NiflySharp.Structs.Color4) As Color
        Return System.Drawing.Color.FromArgb(color.A * 255, color.R * 255, color.G * 255, color.B * 255)
    End Function
    Public Shared Function NifColorToColor(color As NiflySharp.Structs.Color3) As Color
        Return System.Drawing.Color.FromArgb(255, color.R * 255, color.G * 255, color.B * 255)
    End Function

    Public Shared Function ColorToUInteger(c As Color) As UInteger
        Return CType((CUInt(c.R) << 16) Or (CUInt(c.G) << 8) Or CUInt(c.B), UInteger)
    End Function

    Public Shared Function CorrectTexturePath(Texture As String) As String
        If String.IsNullOrEmpty(Texture) Then Return ""
        Dim t As String = Texture.Correct_Path_Separator.ToLowerInvariant()
        If Not t.StartsWith("textures\") Then
            t = "textures\" & t
        End If
        Return t
    End Function

    Public Shared Function CorrectMaterialPath(Texture As String) As String
        If String.IsNullOrEmpty(Texture) Then Return ""
        Dim t As String = Texture.Correct_Path_Separator.ToLowerInvariant()
        If Not t.StartsWith("materials\") Then
            t = "materials\" & t
        End If
        Return t
    End Function
    Shared Sub New()

    End Sub
    ''' <summary>
    ''' Compara dos instancias de FO4UnifiedMaterial_Class inspeccionando
    ''' cada propiedad y trazando su valor y resultado.
    ''' Nothing vs Nothing = True; Nothing vs objeto real = False
    ''' </summary>
    Public Shared Function AreEqualWithTrace(a As FO4UnifiedMaterial_Class, b As FO4UnifiedMaterial_Class) As Boolean
        ' Caso de nulos
        If a Is Nothing OrElse b Is Nothing Then
            Return a Is b
        End If

        Dim tipo As Type = GetType(FO4UnifiedMaterial_Class)
        Dim props = tipo.GetProperties(BindingFlags.Public Or BindingFlags.Instance) _
                       .Where(Function(p) p.GetIndexParameters().Length = 0)

        For Each prop In props
            Dim valA = prop.GetValue(a, Nothing)
            Dim valB = prop.GetValue(b, Nothing)
            Dim equal As Boolean
            Select Case prop.PropertyType
                Case GetType(Single)
                    equal = CType(valA, Single) = CType(valB, Single)
                Case GetType(String)
                    equal = String.Equals(CStr(valA), CStr(valB), StringComparison.OrdinalIgnoreCase)
                Case GetType(Type)
                    equal = valA.Equals(valB)
                Case GetType(Material_Editor.BaseMaterialFile)
                    equal = True
                Case Else
                    equal = Object.Equals(valA, valB)
            End Select


            If Not equal Then
                Return False
            End If
        Next

        Return True
    End Function

    ''' <summary>
    ''' Compara dos instancias de FO4UnifiedMaterial_Class usando el comparador generado.
    ''' </summary>
    Public Function AreEqualTo(b As FO4UnifiedMaterial_Class) As Boolean
        If Me Is Nothing OrElse b Is Nothing Then Return Me Is b
        Return AreEqualWithTrace(Me, b)
    End Function

End Class
Public Class DictionaryFilePickerEditor
    Inherits UITypeEditor
    Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
        Return UITypeEditorEditStyle.Modal
    End Function
    Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
        Dim dictProvider = FilesDictionary_class.TexturesDictionary_Filter.DictionaryProvider
        If dictProvider Is Nothing Then
            MessageBox.Show("Configurar DictionaryFilePickerConfig.DictionaryProvider antes de usar.", "Selector (diccionario)", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return value
        End If

        Dim dict = dictProvider.Invoke()
        Dim filtered = dict.Keys.Where(Function(k) FilesDictionary_class.DictionaryFilePickerConfig.PathStartsWithRoot(k, FilesDictionary_class.TexturesDictionary_Filter.RootPrefix) And FilesDictionary_class.TexturesDictionary_Filter.ExtensionAllowed(k)).Order.ToList()
        Dim initialKey As String = TryCast(value, String).Correct_Path_Separator
        If Not initialKey.StartsWith("Textures\", StringComparison.OrdinalIgnoreCase) Then
            initialKey = "Textures\" + initialKey
        End If

        Using frm As New DictionaryFilePicker_Form(filtered, FilesDictionary_class.TexturesDictionary_Filter.RootPrefix, FilesDictionary_class.TexturesDictionary_Filter.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.OK Then
                Dim sel = frm.DictionaryPicker_Control1.SelectedKey
                If Not String.IsNullOrEmpty(sel) Then Return IO.Path.GetRelativePath("Textures\", sel)
            End If
        End Using

        Return value
    End Function


End Class

Public Class InhertedBGEMShader
    Inherits BSEffectShaderProperty
    Public ReadOnly Property Emitcolor As UInteger
        Get
            Return FO4UnifiedMaterial_Class.ColorToUInteger(FO4UnifiedMaterial_Class.NifColorToColor(Me._emittanceColor))
        End Get
    End Property
    Public ReadOnly Property NormalTexture As String
        Get
            If IsNothing(Me._normalTexture) Or IsNothing(Me._normalTexture) Then Return ""
            Return Me._normalTexture.Content
        End Get
    End Property
    Public ReadOnly Property BaseTexture As String
        Get
            If IsNothing(Me._sourceTexture) Or IsNothing(Me._sourceTexture) Then Return ""
            Return Me._sourceTexture.Content
        End Get
    End Property
    Public ReadOnly Property GrayscaleTexture As String
        Get
            If IsNothing(Me._greyscaleTexture) Or IsNothing(Me._greyscaleTexture) Then Return ""
            Return Me._greyscaleTexture.Content
        End Get
    End Property
    Public ReadOnly Property EnvMapTexture As String
        Get
            If IsNothing(Me._envMapTexture) Or IsNothing(Me._envMapTexture) Then Return ""
            Return Me._envMapTexture.Content
        End Get
    End Property
    Public ReadOnly Property EnvMaskTexture As String
        Get
            If IsNothing(Me._envMaskTexture) Or IsNothing(Me._envMaskTexture) Then Return ""
            Return Me._envMaskTexture.Content
        End Get
    End Property
    Public ReadOnly Property LightingTexture As String
        Get
            If IsNothing(Me._lightingTexture) Or IsNothing(Me._lightingTexture) Then Return ""
            Return Me._lightingTexture.Content
        End Get
    End Property
    Public ReadOnly Property ReflectanceTexture As String
        Get
            If IsNothing(Me._reflectanceTexture) Or IsNothing(Me._reflectanceTexture) Then Return ""
            Return Me._reflectanceTexture.Content
        End Get
    End Property
    Public ReadOnly Property GlowTexture As String
        Get
            If IsNothing(Me._emitGradientTexture) Or IsNothing(Me._emitGradientTexture) Then Return ""
            Return Me._emitGradientTexture.Content
        End Get
    End Property
End Class
Public Class InhertedBGSMshader
    Inherits BSLightingShaderProperty
End Class