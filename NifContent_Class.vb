Imports System.IO
Imports System.Security.Cryptography.X509Certificates
Imports Material_Editor
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Enums
Imports Wardrobe_Manager.Wardrobe_Manager_Form


Public Class Skeleton_Class
    Inherits Nifcontent_Class_Manolo
    Public Shared Property Skeleton As Nifcontent_Class_Manolo = Nothing
    Public Shared Property SkeletonStructure As New List(Of HierarchiBone_class)(StringComparison.OrdinalIgnoreCase)
    Public Shared Property SkeletonDictionary As New Dictionary(Of String, HierarchiBone_class)(StringComparison.OrdinalIgnoreCase)
    Public Shared Sub AppplyPoseToSkeleton(Pose As Poses_class)
        If HasSkeleton = False Then Exit Sub

        Reset()

        If IsNothing(Pose) Then Exit Sub

        For Each posbon In Pose.Transforms
            Dim bon As HierarchiBone_class = Nothing
            If SkeletonDictionary.TryGetValue(posbon.Key, bon) Then
                Dim Bonetrans = bon.OriginalLocaLTransform
                Dim posetrans = New Transform_Class(posbon.Value, Pose.Source)
                Dim trans As Transform_Class
                If Pose.Source = Poses_class.Pose_Source_Enum.ScreenArcher Then
                    trans = Bonetrans.Inverse.ComposeTransforms(posetrans)
                Else
                    trans = posetrans
                End If
                Dim reemplazo = bon
                reemplazo.DeltaTransform = trans
                bon = reemplazo
            End If
        Next

    End Sub
    Public Class HierarchiBone_class
        Public ReadOnly Property LocaLTransform As Transform_Class
            Get
                If IsNothing(DeltaTransform) Then Return OriginalLocaLTransform
                Return OriginalLocaLTransform.ComposeTransforms(DeltaTransform)
            End Get
        End Property
        Public DeltaTransform As Transform_Class = Nothing
        Public OriginalLocaLTransform As Transform_Class
        Public BoneName As String
        Public Parent As HierarchiBone_class
        Public Childrens As New List(Of HierarchiBone_class)
        Public ReadOnly Property GetGlobalTransform As Transform_Class
            Get
                If IsNothing(Parent) Then Return LocaLTransform
                Return Parent.GetGlobalTransform.ComposeTransforms(LocaLTransform)
            End Get
        End Property
        Public ReadOnly Property OriginalGetGlobalTransform As Transform_Class
            Get
                If IsNothing(Parent) Then Return OriginalLocaLTransform
                Return Parent.OriginalGetGlobalTransform.ComposeTransforms(OriginalLocaLTransform)
            End Get
        End Property

    End Class
    Public Shared ReadOnly Property HasSkeleton As Boolean
        Get
            If IsNothing(Skeleton) Then Return False
            If Skeleton.Blocks.Count = 0 Then Return False
            Return True
        End Get
    End Property

    Public Shared Function LoadSkeleton(Force As Boolean, relative As Boolean) As Boolean
        Try
            If Force = False AndAlso HasSkeleton Then Return True
            Skeleton = New Nifcontent_Class_Manolo
            SkeletonStructure.Clear()
            SkeletonDictionary.Clear()
            If relative = False Then
                Skeleton.Load_Manolo(Directorios.SkeletonPath)
            Else
                Dim relativestr = IO.Path.GetRelativePath(Directorios.Fallout4data, Directorios.SkeletonPath)
                Skeleton.Load_Manolo(FilesDictionary_class.Dictionary(relativestr).GetBytes)
            End If
            For Each bon As NiNode In Skeleton.Blocks.Where(Function(pf) pf.GetType Is GetType(NiNode))
                Dim par = GetParentNodeSkeleton(bon.Name.String)
                If IsNothing(par) OrElse par.GetType Is GetType(NiflySharp.Blocks.BSFadeNode) Then
                    If IsNothing(par) Then
                        AddBone(Nothing, bon)
                    Else
                        AddBone(Nothing, par)
                    End If
                End If
            Next
            Return SkeletonDictionary.Any
        Catch ex As Exception
            Skeleton = Nothing
            Return False
        End Try
    End Function
    Public Shared Sub Reset()
        For Each bon In SkeletonDictionary.Values
            bon.DeltaTransform = Nothing
        Next
    End Sub
    Private Shared Sub AddBone(Parent As HierarchiBone_class, Bone As NiNode)
        Dim Donde As HierarchiBone_class
        Dim nuevo As HierarchiBone_class
        If IsNothing(Parent) Then
            Donde = New HierarchiBone_class
            SkeletonStructure.Add(Donde)
            nuevo = Donde
        Else
            nuevo = New HierarchiBone_class
            Parent.Childrens.Add(nuevo)
        End If
        nuevo.Parent = Parent
        nuevo.BoneName = Bone.Name.String
        nuevo.DeltaTransform = Nothing
        nuevo.OriginalLocaLTransform = New Transform_Class(Bone)
        SkeletonDictionary.Add(Bone.Name.String, nuevo)
        For Each chil In Bone.Children.References
            Dim child As NiNode = Skeleton.Blocks(chil.Index)
            AddBone(nuevo, child)
        Next
    End Sub

    Public Shared Function GetParentNodeNameSkeleton(bone As String) As String
        If HasSkeleton = False Then Return ""
        Dim childIndex As Integer
        Dim child = Skeleton.FindBlockByName(Of NiNode)(bone)

        If Not Skeleton.GetBlockIndex(child, childIndex) Then
            Return Nothing
        End If

        Dim nodes = Skeleton.Blocks.OfType(Of NiNode)().Where(Function(n) n IsNot child)
        Dim result = nodes.FirstOrDefault(Function(n) n.Children.Indices.Contains(childIndex))
        If IsNothing(result) Then Return ""
        Return result.Name.String
    End Function
    Public Shared Function GetParentNodeSkeleton(bone As String) As NiNode
        If HasSkeleton = False Then Return Nothing
        Dim childIndex As Integer
        Dim child = Skeleton.FindBlockByName(Of NiNode)(bone)

        If Not Skeleton.GetBlockIndex(child, childIndex) Then
            Return Nothing
        End If

        Dim nodes = Skeleton.Blocks.OfType(Of NiNode)().Where(Function(n) n IsNot child)
        Return nodes.FirstOrDefault(Function(n) n.Children.Indices.Contains(childIndex))
    End Function
    Public Function GetParentNodeSkeleton_ToCurrent(node As NiNode) As NiNode
        If IsNothing(node) Then Return Nothing
        Dim par = GetParentNodeSkeleton(node.Name.String)
        If IsNothing(par) Then Return MyBase.GetParentNode(node)
        Return par
    End Function
End Class
Public Class Nifcontent_Class_Manolo
    Inherits NiflySharp.NifFile

    Private Function CloneBrute() As Nifcontent_Class_Manolo
        Dim result = New Nifcontent_Class_Manolo(Me.ParentSlider)
        Using ms As New MemoryStream()
            ' Serialize this object into the memory stream
            Me.Save(ms)

            ' Reset position to read from start
            ms.Position = 0

            ' Load into result from memory stream
            result.Load(ms)
        End Using
        Return result
    End Function
    Public Property ParentSlider As SliderSet_Class
    Sub New(Parent As SliderSet_Class)
        Me.ParentSlider = Parent
    End Sub
    Sub New()
    End Sub
    Public BaseMaterials As New SortedDictionary(Of String, RelatedMaterial_Class)
    Public Class RelatedMaterial_Class
        Public path As String
        Public material As FO4UnifiedMaterial_Class
    End Class
    Public Sub Load_Manolo(Filename As String)
        Try
            Dim fileBytes As Byte() = File.ReadAllBytes(Filename)
            Load_Manolo(fileBytes)
        Catch ex As Exception
            Debugger.Break()
            Throw New Exception(ex.Message)
        End Try
    End Sub
    Public Sub Load_Manolo(FileBytes As Byte())
        Try
            Using ms As New MemoryStream(FileBytes)
                ms.Position = 0
                MyBase.Load(ms)
            End Using
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try
        BaseMaterials.Clear()

        For Each shap In Me.GetShapes
            If SupportedShape(shap.GetType) Then
                BaseMaterials.Add(shap.Name.String, GetRelatedMaterial(shap))
            End If
        Next

        If HasUnknownBlocks Then
            Debugger.Break()
            Throw New Exception("Unknown blocks")
        End If
    End Sub
    Public Shared Function SupportedShape(shapetype As Type) As Boolean
        Select Case shapetype
            Case GetType(NiTriShape)
                Debugger.Break()
                Return False
            Case GetType(NiParticles), GetType(BSStripParticleSystem), GetType(NiParticleSystem)
                Return False
            Case GetType(BSSubIndexTriShape), GetType(BSTriShape), GetType(BSLODTriShape), GetType(BSSegmentedTriShape), GetType(BSMeshLODTriShape), GetType(BSDynamicTriShape)
                Return True
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
    End Function
    Public Sub AddTriData(shapeName As String, triPath As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If

        If target IsNot Nothing Then
            AssignExtraData(target, triPath)
        End If
    End Sub
    Public Sub RemoveTriData(shapeName As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If
        If Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData
                ed = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) Then
                    'AssignExtraData()
                    If ed.Name.String = "BODYTRI" Then
                        target.ExtraDataList.RemoveBlockRef(ref.Index)
                        RemoveBlock(ed)
                        RemoveUnreferencedBlocks()
                        Exit Sub
                    End If
                End If
            Next
        End If
    End Sub

    Public Function AssignExtraData(target As NiAVObject, triPath As String) As UInteger
        If Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData
                ed = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) Then
                    If ed.Name.String = "BODYTRI" Then
                        ed.Name.String = "BODYTRI"
                        ed.StringData.String = triPath
                        Return ref.Index
                    End If
                End If
            Next
        End If
        Dim triExtraData As New NiStringExtraData()
        Dim nam = New NiStringRef("BODYTRI")
        triExtraData.Name = nam
        Dim pat = New NiStringRef(triPath)
        triExtraData.StringData = pat
        Dim extraDataId As UInteger = AddBlock(triExtraData)
        target.ExtraDataList.AddBlockRef(extraDataId)
        Return extraDataId
    End Function
    Public Function GetRelatedMaterial(shap As INiShape) As RelatedMaterial_Class
        Dim prefix = "materials\"
        Dim shad = GetShader(shap)
        If IsNothing(shad) Then
            Dim material As New FO4UnifiedMaterial_Class
            material.Create_From_Shader(Me, shap, CType(shad, BSLightingShaderProperty))
            Dim related As New RelatedMaterial_Class With {.material = material, .path = ""}
            Return related
        End If

        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                Dim material As New FO4UnifiedMaterial_Class
                Dim fullpath = CType(shad, BSLightingShaderProperty).Name.String.Correct_Path_Separator
                If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
                If fullpath = "" Then
                    material.Create_From_Shader(Me, shap, CType(shad, BSLightingShaderProperty))
                Else
                    material.Deserialize(prefix + fullpath, GetType(BGSM))
                End If
                Dim related As New RelatedMaterial_Class With {.material = material, .path = fullpath}
                Return related
            Case GetType(BSEffectShaderProperty)
                Dim material As New FO4UnifiedMaterial_Class
                Dim fullpath = CType(shad, BSEffectShaderProperty).Name.String.Correct_Path_Separator
                If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then fullpath = fullpath.Substring(prefix.Length)
                If fullpath = "" Then
                    material.Create_From_Shader(Me, shap, CType(shad, BSEffectShaderProperty))
                Else
                    material.Deserialize(prefix + fullpath, GetType(BGEM))
                End If
                Dim related As New RelatedMaterial_Class With {.material = material, .path = fullpath}
                Return related
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
    End Function
    Public Sub SetRelatedMaterial(shap As INiShape, MatPath As String, mat As FO4UnifiedMaterial_Class)
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                MatPath = MatPath.Correct_Path_Separator
                Dim prefix = "materials\"
                If MatPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then MatPath = MatPath.Substring(prefix.Length)
                Dim shad = GetShader(shap)
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        CType(shad, BSLightingShaderProperty).Name.String = MatPath
                    Case GetType(BSEffectShaderProperty)
                        CType(shad, BSEffectShaderProperty).Name.String = MatPath
                    Case Else
                        Debugger.Break()
                        Throw New Exception
                End Select
            Case Config_App.Game_Enum.Skyrim
                MatPath = MatPath.Correct_Path_Separator
                Dim prefix = "materials\"
                If MatPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then MatPath = MatPath.Substring(prefix.Length)
                Dim shad = GetShader(shap)
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        FO4UnifiedMaterial_Class.Save_To_Shader(Me, shap, CType(shad, BSLightingShaderProperty), mat.Underlying_Material)
                        CType(shad, BSLightingShaderProperty).Name.String = MatPath
                    Case GetType(BSEffectShaderProperty)
                        FO4UnifiedMaterial_Class.Save_To_Shader(Me, shap, CType(shad, BSEffectShaderProperty), mat.Underlying_Material)
                        CType(shad, BSEffectShaderProperty).Name.String = MatPath
                    Case Else
                        Debugger.Break()
                        Throw New Exception
                End Select
        End Select

    End Sub
    Public Sub Save_As_Manolo(Filename As String, Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then
            If MsgBox("NIF File already exists, replace?", vbYesNo, "Warning") = MsgBoxResult.No Then
                Exit Sub
            End If
        End If
        If MyBase.Save(Filename) <> 0 Then
            Throw New Exception("Error saving NIF")
        End If
    End Sub

    Public ReadOnly Property NifShapes As IEnumerable(Of NiflySharp.INiShape)
        Get
            Return Me.GetShapes
        End Get
    End Property

    Public Sub RemoveShape_Manolo(Shape As INiShape)
        Me.RemoveBlock(Shape)
        Me.RemoveUnreferencedBlocks()
    End Sub
    Public Shared Sub Merge_Shapes_Original(DestNif As Nifcontent_Class_Manolo, SrcNif As Nifcontent_Class_Manolo, MergeClothesData As Boolean)
        SrcNif.GetRootNode().Name.String = "Scene Root"
        If Not MergeClothesData Then SrcNif.RemoveBlocksOfType(Of BSClothExtraData)()
        SrcNif.RemoveUnreferencedBlocks()
        For Each shap In SrcNif.GetShapes.ToList
            DestNif.CloneShape_Original(shap, shap.Name.String, SrcNif)
        Next
    End Sub

    Public Function CloneShape_Original(srcShape As INiShape, destShapeName As String, srcNif As Nifcontent_Class_Manolo) As INiShape
        If srcNif Is Nothing Then srcNif = Me
        If srcShape Is Nothing Then
            Return Nothing
        End If

        If SupportedShape(srcShape.GetType) = False Then
            Throw New Exception
        End If

        Dim rootNode = GetRootNode()
        Dim srcRootNode = srcNif.GetRootNode()

        ' Geometry
        Dim destShapeS As INiShape = srcShape.Clone ' ACA VA CLONE

        If srcShape.GetType Is GetType(BSDynamicTriShape) Then
            Dim idx = srcNif.Blocks.IndexOf(srcShape)
            Dim clon = srcNif.CloneBrute
            destShapeS = clon.Blocks(idx)
            'revisar
            ' El clone es bruto
            Debugger.Break()
        End If

        Dim destShape As INiShape = destShapeS
        destShape.Name.String = destShapeName

        Dim destId As Integer = AddBlock(destShapeS)
        If srcNif Is Me Then
            ' Assign copied geometry to the same parent
            Dim parentNode = GetParentNode(srcShape)
            parentNode?.Children.AddBlockRef(destId)
        ElseIf rootNode IsNot Nothing Then
            rootNode.Children.AddBlockRef(destId)
        End If

        ' Children
        CloneChildren(destShape, srcNif)

        ' Geometry Data
        Dim destGeomData = CType(GetBlock(Of NiObject)(destShape.DataRef()), NiTriBasedGeomData)

        If destGeomData IsNot Nothing Then
            destShape.GeometryData = destGeomData
        End If

        ' Shader
        If Not IsNothing(GetShader(destShape)) Then
            If (GetShader(destShape).GetType Is GetType(BSLightingShaderProperty)) = True Then
                Dim destShader As BSLightingShaderProperty = GetShader(destShape)
                If destShader IsNot Nothing Then
                    If Header.Version().IsSK() OrElse Header.Version().IsSSE() Then
                        ' Kill normals and tangents
                        If destShader.ModelSpace Then
                            destShape.HasNormals = False
                            destShape.HasTangents = False
                        End If
                    End If
                End If
            Else
                If GetShader(destShape).GetType Is GetType(BSEffectShaderProperty) = False Then
                    Debugger.Break()
                End If
            End If
        End If

        ' Bones
        Dim srcBoneList As New List(Of String)
        Dim sourcBoneCont As NiObject = srcNif.GetBlock(Of NiObject)(srcShape.SkinInstanceRef())
        If Not IsNothing(sourcBoneCont) Then
            Select Case sourcBoneCont.GetType
                Case GetType(BSSkin_Instance)
                    For Each bon In TryCast(sourcBoneCont, BSSkin_Instance).Bones.References
                        Dim nod = srcNif.GetBlock(bon)
                        srcBoneList.Add(CType(nod, NiNode).Name.String)
                    Next
                Case GetType(BSDismemberSkinInstance)
                    For Each bon In TryCast(sourcBoneCont, BSDismemberSkinInstance).Bones.References
                        Dim nod = srcNif.GetBlock(bon)
                        srcBoneList.Add(CType(nod, NiNode).Name.String)
                    Next
                Case GetType(NiSkinInstance)
                    For Each bon In TryCast(sourcBoneCont, NiSkinInstance).Bones.References
                        Dim nod = srcNif.GetBlock(bon)
                        srcBoneList.Add(CType(nod, NiNode).Name.String)
                    Next
                Case Else
                    Throw New Exception
            End Select
        End If

        Dim destBoneCont As NiObject = GetBlock(Of NiObject)(destShape.SkinInstanceRef())
        If destBoneCont IsNot Nothing Then
            If Not IsNothing(destBoneCont) Then
                Select Case destBoneCont.GetType
                    Case GetType(BSSkin_Instance)
                        TryCast(destBoneCont, BSSkin_Instance).Bones.Clear()
                    Case GetType(BSDismemberSkinInstance)
                        TryCast(destBoneCont, BSDismemberSkinInstance).Bones.Clear()
                    Case GetType(NiSkinInstance)
                        TryCast(destBoneCont, NiSkinInstance).Bones.Clear()
                    Case Else
                        Throw New Exception
                End Select
            End If
        End If

        If rootNode IsNot Nothing AndAlso srcRootNode IsNot Nothing Then
            For Each child In srcRootNode.References
                Dim srcChildNode = srcNif.GetBlock(Of NiNode)(child)
                If srcChildNode IsNot Nothing Then
                    CloneNodes_Action(srcChildNode, rootNode, srcNif)
                End If
            Next
        End If

        ' Add bones to container if used in skin
        If destBoneCont IsNot Nothing Then
            For Each boneName In srcBoneList
                Dim node = FindBlockByName(Of NiNode)(boneName)
                Dim boneID As Integer = Blocks.IndexOf(node)
                If node IsNot Nothing Then
                    Select Case destBoneCont.GetType
                        Case GetType(BSSkin_Instance)
                            TryCast(destBoneCont, BSSkin_Instance).Bones.AddBlockRef(boneID)
                        Case GetType(BSDismemberSkinInstance)
                            TryCast(destBoneCont, BSDismemberSkinInstance).Bones.AddBlockRef(boneID)
                        Case GetType(NiSkinInstance)
                            TryCast(destBoneCont, NiSkinInstance).Bones.AddBlockRef(boneID)
                        Case Else
                            Throw New Exception
                    End Select
                End If
            Next
        End If
        Return destShape
    End Function
    Private Sub CloneChildren(block As NiObject, srcNif As Nifcontent_Class_Manolo)
        If srcNif Is Nothing Then
            srcNif = Me
        End If
        ' Asignar nuevas referencias y cadenas, volver a enlazar punteros donde sea posible
        cloneBlock_Action(block, -1, -1, srcNif)
    End Sub
    Private Sub CloneBlock_Action(b As NiObject, parentOldId As Integer, parentNewId As Integer, ByRef srcnif As Nifcontent_Class_Manolo)
        For Each r In b.References
            Dim srcChild = srcnif.GetBlock(Of NiObject)(r)
            If srcChild IsNot Nothing Then

                Dim destChildS = srcChild.Clone   ' ACA VA CLONE
                Dim destChild = destChildS
                Dim destId As Integer = AddBlock(destChildS)

                Dim oldId As Integer = r.Index
                r.Index = destId

                For Each Str2 In destChild.StringRefs
                    Dim strId As Integer = Header.AddOrFindStringId(Str2.String, False)
                    Str2.Index = strId
                Next

                If parentOldId <> -1 Then
                    For Each p In destChild.Pointers
                        If p.Index = parentOldId Then
                            p.Index = parentNewId
                        End If
                    Next

                    CloneBlock_Action(destChild, parentOldId, parentNewId, srcnif)
                Else
                    CloneBlock_Action(destChild, oldId, destId, srcnif)
                End If
            Else
                Debugger.Break()
            End If
        Next
    End Sub

    Private Sub CloneNodes_Action(srcNode As NiNode, ByRef rootNode As NiNode, ByRef srcnif As NifFile)
        Dim boneName As String = srcNode.Name.String

        ' Insert as root child by default
        Dim nodeParent As NiNode = rootNode

        ' Look for existing node to use as parent instead
        Dim srcNodeParent = srcnif.GetParentNode(srcNode)
        If srcNodeParent IsNot Nothing Then
            Dim parent = FindBlockByName(Of NiNode)(srcNodeParent.Name.String)
            If parent IsNot Nothing Then
                nodeParent = parent
            End If
        End If

        Dim node = FindBlockByName(Of NiNode)(boneName)
        Dim boneID As Integer = Blocks.IndexOf(node)
        If node Is Nothing Then
            ' Clone missing node into the right parent
            boneID = CloneNamedNode_Manolo(boneName, srcnif)
            nodeParent.Children.AddBlockRef(boneID)
        Else
            ' Move existing node to non-root parent
            Dim oldParent = GetParentNode(node)
            If oldParent IsNot Nothing AndAlso oldParent IsNot nodeParent AndAlso nodeParent IsNot rootNode Then
                Dim sour = srcnif.FindBlockByName(Of NiNode)(boneName)

                For Each ref In oldParent.References
                    If ref.Index = boneID Then
                        ref.Clear()
                    End If
                Next

                Dim dest = FindBlockByName(Of NiNode)(boneName)

                nodeParent.Children.AddBlockRef(boneID)
                dest.Scale = sour.Scale
                dest.Translation = sour.Translation
                dest.Rotation = sour.Rotation
            End If
        End If

        ' Recurse children
        For Each child In srcNode.References
            Dim childNodet = srcnif.GetBlock(Of NiAVObject)(child)
            Dim childNode = TryCast(childNodet, NiNode)
            If childNode IsNot Nothing Then
                CloneNodes_Action(childNode, rootNode, srcnif)
            End If
        Next
    End Sub
    Private Function CloneNamedNode_Manolo(nodeName As String, srcnif As Nifcontent_Class_Manolo) As Integer
        If srcnif Is Nothing Then
            srcnif = Me
        End If
        Dim srcNode = srcnif.FindBlockByName(Of NiAVObject)(nodeName)
        If srcNode Is Nothing Then
            Debugger.Break()
        End If

        Dim destNode As NiNode = srcNode.Clone ' ACA VA CLONE
        destNode.Name.String = nodeName
        If Not IsNothing(destNode.CollisionObject) Then destNode.CollisionObject.Clear()
        If Not IsNothing(destNode.Controller) Then destNode.Controller.Clear()
        If Not IsNothing(destNode.Children) Then destNode.Children.Clear()
        If Not IsNothing(destNode.Effects) Then destNode.Effects.Clear()

        Return AddBlock(destNode)
    End Function
End Class
