Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Imports Wardrobe_Manager.RecalcTBN

' --- STRUCTURE PARA ALMACENAR GEOMETRÍA SKINEADA ---
Public Structure SkinnedGeometry
    Public Vertices() As Vector3d
    Public BaseVertices() As Vector3d
    Public dirtyMaskIndices As HashSet(Of Integer)              ' Para dirty-tracking de máscara
    Public dirtyVertexIndices As HashSet(Of Integer)
    Public dirtyMaskFlags() As Boolean
    Public dirtyVertexFlags() As Boolean
    Public Normals() As Vector3d
    Public Tangents() As Vector3d
    Public Bitangents() As Vector3d
    Public Uvs_Weight() As Vector3
    Public Eyedata() As Single
    Public ShapeGlobal As Matrix4d
    Public BoneMatsBind() As Matrix4d   ' bind-pose matrices
    Public BoneMatsPose() As Matrix4d  ' pose matrices
    Public VertexColors() As Vector4
    Public VertexMask() As Single
    Public Indices() As UInteger
    Public VertexData As List(Of BSVertexData)
    Public VertexDataSSE As List(Of BSVertexDataSSE)
    Public TriShape As BSTriShape
    Public Boundingcenter As Vector3d
    Public Minv As Vector3d
    Public Maxv As Vector3d
    Public CachedTBN As TBNCache
    Public Version As NiVersion
End Structure
Public Structure MorphData
    Public index As UInteger
    Public PosDiff As Vector3
End Structure


Public Class SkinningHelper
    ''' <summary>
    ''' Extrae vértices, normales, tangentes y bitangentes del shape,
    ''' aplicando el mismo skinning que LoadShapeSafe.
    ''' </summary>
    '''   Public Shared Function ExtractSkinnedGeometry(shape As Shape_class, ApplyPose As Boolean, singleboneskinning As Boolean, RecalculateNormals As Boolean) As SkinnedGeometry
    Public Shared Function ExtractSkinnedGeometry(shape As Shape_class, ApplyPose As Boolean, singleboneskinning As Boolean, RecalculateNormals As Boolean) As SkinnedGeometry
        Dim tri = CType(shape.RelatedNifShape, BSTriShape)
        Dim bones = shape.RelatedBones.ToArray()
        Dim boneTrans = shape.RelatedBoneTransforms.ToArray()

        If boneTrans.Length <> bones.Length Then Throw New Exception("BonesTransform y Bones desincronizados")
        Dim Nifversion = shape.ParentSliderSet.NIFContent.Header.Version
        ' 1) Transformación global del shape
        Dim shapeNode = TryCast(shape.ParentSliderSet.NIFContent.GetParentNode(tri), NiNode)
        Dim GlobalTransform = If(shapeNode IsNot Nothing, Transform_Class.GetGlobalTransform(shapeNode, shape.ParentSliderSet.NIFContent).ToMatrix4d(), Matrix4d.Identity)
        ' 2) Datos brutos
        Dim rawVerts = tri.VertexPositions.Select(Function(v) New Vector3d(v.X, v.Y, v.Z)).ToArray()
        Dim rawNormals() As Vector3d
        Dim rawTangents() As Vector3d
        Dim rawBitangs() As Vector3d

        If tri.HasNormals Then
            rawNormals = tri.Normals.Select(Function(n) Vector3d.Normalize(New Vector3d(n.X, n.Y, n.Z))).ToArray()
        Else
            rawNormals = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
        End If
        If tri.HasTangents Then
            ' INVERTIDAS!!!!!  PARA FO4 ES ASI
            rawTangents = tri.Bitangents.Select(Function(t) Vector3d.Normalize(New Vector3d(t.X, t.Y, t.Z))).ToArray()
            rawBitangs = tri.Tangents.Select(Function(b) Vector3d.Normalize(New Vector3d(b.X, b.Y, b.Z))).ToArray()
        Else
            rawTangents = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
            rawBitangs = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
        End If

        Dim vertexCount As Integer = rawVerts.Length
        If Not ((rawNormals.Length = vertexCount OrElse tri.HasNormals = False) AndAlso (rawTangents.Length = vertexCount OrElse tri.HasNormals = False) AndAlso (rawBitangs.Length = vertexCount OrElse tri.HasNormals = False) AndAlso (tri.HasVertexColors = False Or tri.VertexColors.Count = vertexCount) AndAlso (tri.HasUVs = False OrElse tri.UVs.Count = vertexCount)) Then
            Debugger.Break()
            Throw New Exception("¡Los atributos de los vértices no tienen la misma longitud!")
        End If


        ' 3) Calcular matrices bind-pose y pose actual
        Dim matsBind(bones.Length - 1) As Matrix4d
        Dim matsPose(bones.Length - 1) As Matrix4d
        For k = 0 To bones.Length - 1
            Dim localT = boneTrans(k)
            Dim bindT = Transform_Class.GetGlobalTransform(bones(k), shape.ParentSliderSet.NIFContent)
            matsBind(k) = bindT.ComposeTransforms(localT).ToMatrix4d()
            Dim poseT As Transform_Class

            Dim value As Skeleton_Class.HierarchiBone_class = Nothing

            If ApplyPose AndAlso Not singleboneskinning AndAlso Skeleton_Class.SkeletonDictionary.TryGetValue(bones(k).Name.String, value) Then
                poseT = value.GetGlobalTransform()
                matsPose(k) = poseT.ComposeTransforms(localT).ToMatrix4d()
            Else
                poseT = bindT
                matsPose(k) = matsBind(k)
            End If
        Next

        ' 4) Aplicar skinning CPU
        Dim allVD = Array.Empty(Of BSVertexData)
        Dim allVDSSE = Array.Empty(Of BSVertexDataSSE)

        If Nifversion.IsSSE = False Then If Not IsNothing(tri.VertexData) Then allVD = tri.VertexData.ToArray()
        If Nifversion.IsSSE = True Then If Not IsNothing(tri.VertexDataSSE) Then allVDSSE = tri.VertexDataSSE.ToArray()

        Select Case True
            Case Not singleboneskinning AndAlso bones.Length > 0
                ' Multibone
                Parallel.For(0, vertexCount, Sub(i)
                                                 Dim Mskin As Matrix4d = Matrix4d.Zero
                                                 Dim sumW As Double = 0
                                                 Dim Boneweights As System.Half()
                                                 Dim Boneindices As Byte()
                                                 If Nifversion.IsSSE Then
                                                     Boneweights = allVDSSE(i).BoneWeights
                                                     Boneindices = allVDSSE(i).BoneIndices
                                                 Else
                                                     Boneweights = allVD(i).BoneWeights
                                                     Boneindices = allVD(i).BoneIndices
                                                 End If
                                                 If Boneweights IsNot Nothing AndAlso Boneindices IsNot Nothing Then
                                                     Dim cnt = Math.Min(Boneweights.Length, Boneindices.Length) - 1
                                                     For j = 0 To cnt
                                                         sumW += CType(Boneweights(j), Double)
                                                     Next
                                                     If sumW = 0F Then
                                                         Dim idx0 = If(Boneindices.Length > 0, Boneindices(0), 0)
                                                         idx0 = Math.Max(0, Math.Min(idx0, matsBind.Length - 1))
                                                         Mskin = matsPose(idx0)
                                                     Else
                                                         For j = 0 To cnt
                                                             Dim w = CType(Boneweights(j), Double) / sumW
                                                             Dim idx = Boneindices(j)
                                                             If idx >= 0 AndAlso idx < matsBind.Length Then
                                                                 Mskin += matsPose(idx) * w
                                                             End If
                                                         Next
                                                     End If
                                                 Else
                                                     ' Sin datos de skinning
                                                     Mskin = matsPose(0)
                                                 End If
                                                 Dim Mtot = GlobalTransform * Mskin
                                                 Dim NormalsMat = Create_Normal_Matrix(Mtot)

                                                 rawVerts(i) = Vector3d.TransformPosition(rawVerts(i), Mtot)
                                                 If Not RecalculateNormals Then
                                                     rawNormals(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawNormals(i), NormalsMat))
                                                     rawTangents(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawTangents(i), NormalsMat))
                                                     rawBitangs(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawBitangs(i), NormalsMat))
                                                 End If
                                             End Sub)

            Case singleboneskinning AndAlso bones.Length > 0
                ' Single-bone
                Dim Mtot = GlobalTransform * matsPose(0)
                Dim NormalsMat = Create_Normal_Matrix(Mtot)
                Parallel.For(0, vertexCount, Sub(i)
                                                 rawVerts(i) = Vector3d.TransformPosition(rawVerts(i), Mtot)
                                                 If Not RecalculateNormals Then
                                                     rawNormals(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawNormals(i), NormalsMat))
                                                     rawTangents(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawTangents(i), NormalsMat))
                                                     rawBitangs(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawBitangs(i), NormalsMat))
                                                 End If
                                             End Sub)

            Case Else
                ' Sin huesos
                Dim Mtot = GlobalTransform
                Dim NormalsMat = Create_Normal_Matrix(Mtot)
                Parallel.For(0, vertexCount, Sub(i)
                                                 rawVerts(i) = Vector3d.TransformPosition(rawVerts(i), Mtot)
                                                 If Not RecalculateNormals Then
                                                     rawNormals(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawNormals(i), NormalsMat))
                                                     rawTangents(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawTangents(i), NormalsMat))
                                                     rawBitangs(i) = Vector3d.Normalize(Vector3d.TransformNormal(rawBitangs(i), NormalsMat))
                                                 End If
                                             End Sub)
        End Select
        ' 7) Bounding center en UN solo bucle
        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For Each v In rawVerts
            If v.X < minV.X Then minV.X = v.X
            If v.Y < minV.Y Then minV.Y = v.Y
            If v.Z < minV.Z Then minV.Z = v.Z

            If v.X > maxV.X Then maxV.X = v.X
            If v.Y > maxV.Y Then maxV.Y = v.Y
            If v.Z > maxV.Z Then maxV.Z = v.Z
        Next
        Dim center = (minV + maxV) * 0.5
        Dim geo = New SkinnedGeometry With {
            .Vertices = rawVerts,
            .BaseVertices = rawVerts.ToArray,
            .Normals = rawNormals,
            .Tangents = rawTangents,
            .Bitangents = rawBitangs,
            .ShapeGlobal = GlobalTransform,
            .BoneMatsBind = matsBind,
            .BoneMatsPose = matsPose,
            .Indices = tri.Triangles.SelectMany(Function(t2) New UInteger() {t2.V1, t2.V2, t2.V3}).ToArray(),
            .VertexColors = IIf(tri.HasVertexColors, tri.VertexColors.Select(Function(c) New Vector4(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F, 1.0F)).ToArray(), Enumerable.Repeat(New Vector4(0.1F, 0.1F, 0.1F, 1.0F), rawVerts.Length).ToArray()),
                        .Eyedata = IIf(tri.HasEyeData, tri.EyeData.ToArray, Enumerable.Repeat(0F, rawVerts.Length).ToArray),
            .VertexData = allVD.ToList,
            .VertexDataSSE = allVDSSE.ToList,
            .TriShape = tri,
            .VertexMask = Enumerable.Repeat(0.0F, rawVerts.Length).ToArray(),
            .dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, rawVerts.Length - 1)),
            .dirtyMaskIndices = New HashSet(Of Integer)(Enumerable.Range(0, rawVerts.Length - 1)),
            .dirtyMaskFlags = Enumerable.Repeat(True, rawVerts.Length).ToArray,
            .dirtyVertexFlags = Enumerable.Repeat(True, rawVerts.Length).ToArray,
             .Boundingcenter = center,
             .Minv = minV,
             .Maxv = maxV,
             .CachedTBN = Nothing,
             .Version = Nifversion
        }

        If tri.HasUVs = True Then
            geo.Uvs_Weight = IIf(Nifversion.IsSSE, allVDSSE.Select(Function(vd) New Vector3(vd.UV.U, vd.UV.V, If(vd.BoneWeights?.Length > 0, CType(vd.BoneWeights(0), Single), 0.0F))).ToArray(), allVD.Select(Function(vd) New Vector3(vd.UV.U, vd.UV.V, If(vd.BoneWeights?.Length > 0, CType(vd.BoneWeights(0), Single), 0.0F))).ToArray())
        Else
            geo.Uvs_Weight = IIf(Nifversion.IsSSE, allVDSSE.Select(Function(vd) New Vector3(0, 0, If(vd.BoneWeights?.Length > 0, CType(vd.BoneWeights(0), Single), 0.0F))).ToArray(), allVD.Select(Function(vd) New Vector3(0, 0, If(vd.BoneWeights?.Length > 0, CType(vd.BoneWeights(0), Single), 0.0F))).ToArray())
        End If


        If RecalculateNormals OrElse tri.HasNormals = False OrElse tri.HasTangents = False Then
            Dim opts = Config_App.Current.Setting_TBN
            RecalculateNormalsTangentsBitangents(geo, opts)
        End If
        Return geo
    End Function
    Private Shared Function Create_Normal_Matrix(Origen As Matrix4d) As Matrix4d
        Dim L As New Matrix3d(Origen)
        Dim nm3 = L.Inverted().Transposed()

        ' Reinyectar nm3 en una 4×4 sin traslación
        Dim nm4 As Matrix4d = Matrix4d.Identity
        nm4.M11 = nm3.M11 : nm4.M12 = nm3.M12 : nm4.M13 = nm3.M13
        nm4.M21 = nm3.M21 : nm4.M22 = nm3.M22 : nm4.M23 = nm3.M23
        nm4.M31 = nm3.M31 : nm4.M32 = nm3.M32 : nm4.M33 = nm3.M33
        Return nm4
    End Function
    Public Shared Sub BakeFromMemoryUsingOriginal(Shape As Shape_class, ByRef geom As SkinnedGeometry, ApplyPose As Boolean, inverse As Boolean, ApplyMorph As Boolean, RemoveZaps As Boolean, singleBoneSkinning As Boolean)
        ' 2) Matrices calculadas en ExtractSkinnedGeometry
        Dim matsBind() As Matrix4d = geom.BoneMatsBind
        Dim matsPose() As Matrix4d = geom.BoneMatsPose

        ' 3) Transformación global e inversa
        Dim GlobalTransform As Matrix4d = geom.ShapeGlobal
        Dim InverseGlobal As Matrix4d = GlobalTransform
        InverseGlobal.Invert()

        ' 4) Vértices resultantes de ExtractSkinnedGeometry (world-space)
        Dim worldV() As Vector3d

        ' 4b) Remove Zaps
        If RemoveZaps Then MorphingHelper.RemoveZaps(Shape, geom)

        If ApplyMorph Then
            worldV = geom.Vertices.ToArray
        Else
            worldV = geom.BaseVertices.ToArray
        End If

        Dim worldN() As Vector3d = geom.Normals
        Dim worldT() As Vector3d = geom.Tangents
        Dim worldB() As Vector3d = geom.Bitangents

        ' 5) Datos de skinning por vértice
        Dim allVD = Array.Empty(Of BSVertexData)
        Dim allVDSSE = Array.Empty(Of BSVertexDataSSE)
        If geom.Version.IsSSE = False Then If Not IsNothing(geom.VertexData) Then allVD = geom.VertexData.ToArray()
        If geom.Version.IsSSE = True Then If Not IsNothing(geom.VertexDataSSE) Then allVDSSE = geom.VertexDataSSE.ToArray()
        Dim nifversion = geom.Version
        'A - REVIERTE Skinning y Bakea
        Select Case True
            Case Not singleBoneSkinning AndAlso matsBind.Length > 0
                ' Multibone
                Parallel.For(0, worldV.Length, Sub(i)

                                                   Dim Mskin_Recovery As Matrix4d = Matrix4d.Zero
                                                   Dim Mskin As Matrix4d = Matrix4d.Zero
                                                   Dim Boneweights As System.Half()
                                                   Dim Boneindices As Byte()
                                                   If nifversion.IsSSE Then
                                                       Boneweights = allVDSSE(i).BoneWeights
                                                       Boneindices = allVDSSE(i).BoneIndices
                                                   Else
                                                       Boneweights = allVD(i).BoneWeights
                                                       Boneindices = allVD(i).BoneIndices
                                                   End If
                                                   Dim sumW As Double = 0

                                                   If Boneweights IsNot Nothing AndAlso Boneindices IsNot Nothing Then
                                                       Dim cnt = Math.Min(Boneweights.Length, Boneindices.Length) - 1
                                                       For j = 0 To cnt
                                                           sumW += CType(Boneweights(j), Double)
                                                       Next
                                                       If sumW = 0F Then
                                                           Dim idx0 = If(Boneindices.Length > 0, Boneindices(0), 0)
                                                           idx0 = Math.Max(0, Math.Min(idx0, matsBind.Length - 1))
                                                           Mskin_Recovery = matsPose(idx0)
                                                           Mskin = matsBind(idx0) * Matrix4d.Invert(matsPose(idx0))
                                                       Else
                                                           For j = 0 To cnt
                                                               Dim w = CType(Boneweights(j), Double) / sumW
                                                               Dim idx = Boneindices(j)
                                                               If idx >= 0 AndAlso idx < matsBind.Length Then
                                                                   Mskin_Recovery += matsPose(idx) * w
                                                                   Mskin += (matsBind(idx) * Matrix4d.Invert(matsPose(idx))) * w
                                                               End If
                                                           Next
                                                       End If
                                                   Else
                                                       ' Sin datos de skinning
                                                       Mskin_Recovery = matsPose(0)
                                                       Mskin = matsBind(0) * Matrix4d.Invert(matsPose(0))
                                                   End If

                                                   Dim Mtot_Inverse = GlobalTransform * Mskin_Recovery
                                                   Mtot_Inverse.Invert()
                                                   Dim skinMat = Mskin
                                                   If Not inverse Then skinMat.Invert()
                                                   Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                                                   Dim NormalsMat_Inverse = Create_Normal_Matrix(Mtot_Inverse)
                                                   Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                                                   ' Revert
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), Mtot_Inverse)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat_Inverse))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat_Inverse))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat_Inverse))
                                                   ' Bake
                                                   If ApplyPose Then
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                                   End If
                                               End Sub)

            Case singleBoneSkinning AndAlso matsBind.Length > 0
                ' Single-bone
                Dim Mtot_Inverse = GlobalTransform * matsPose(0)
                Mtot_Inverse.Invert()
                Dim skinMat = matsBind(0) * Matrix4d.Invert(matsPose(0))
                If Not inverse Then skinMat.Invert()
                Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                Dim NormalsMat_Inverse = Create_Normal_Matrix(Mtot_Inverse)
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                Parallel.For(0, worldV.Length, Sub(i)
                                                   ' Revert
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), Mtot_Inverse)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat_Inverse))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat_Inverse))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat_Inverse))
                                                   ' Bake
                                                   If ApplyPose Then
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                                   End If
                                               End Sub)

            Case Else
                ' Sin huesos
                Dim Mtot_Inverse = InverseGlobal
                Dim totalSkinMat = GlobalTransform
                Dim NormalsMat_Inverse = Create_Normal_Matrix(Mtot_Inverse)
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)
                Parallel.For(0, worldV.Length, Sub(i)
                                                   ' Revert
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), Mtot_Inverse)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat_Inverse))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat_Inverse))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat_Inverse))
                                                   ' Bake
                                                   If ApplyPose Then
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                                   End If
                                               End Sub)
        End Select

        If ApplyMorph Then
            geom.Vertices = worldV.ToArray
            geom.BaseVertices = worldV.ToArray
        Else
            geom.Vertices = worldV.ToArray
        End If

        InjectToTrishape(geom)

    End Sub
    Public Shared Sub InjectToTrishape(ByRef geom As SkinnedGeometry)
        Dim nNew As Integer = geom.Vertices.Length
        Dim tri = geom.TriShape
        Dim posN(nNew - 1) As System.Numerics.Vector3
        Dim norN(nNew - 1) As System.Numerics.Vector3
        Dim tanN(nNew - 1) As System.Numerics.Vector3
        Dim bitN(nNew - 1) As System.Numerics.Vector3
        Dim uvN(nNew - 1) As System.Numerics.Vector3
        Dim colN(nNew - 1) As NiflySharp.Structs.Color4

        For i As Integer = 0 To nNew - 1
            Dim v1 = geom.Vertices(i) : posN(i) = New System.Numerics.Vector3(CSng(v1.X), CSng(v1.Y), CSng(v1.Z))
            Dim n1 = geom.Normals(i) : norN(i) = New System.Numerics.Vector3(CSng(n1.X), CSng(n1.Y), CSng(n1.Z))
            ' OJOOO INVERTIDAS
            Dim t1 = geom.Bitangents(i) : tanN(i) = New System.Numerics.Vector3(CSng(t1.X), CSng(t1.Y), CSng(t1.Z))
            Dim b1 = geom.Tangents(i) : bitN(i) = New System.Numerics.Vector3(CSng(b1.X), CSng(b1.Y), CSng(b1.Z))
            Dim uv = geom.Uvs_Weight(i) : uvN(i) = New System.Numerics.Vector3(CSng(uv.X), CSng(uv.Y), 0)
            Dim c = geom.VertexColors(i) : colN(i) = New NiflySharp.Structs.Color4(CSng(c.X), CSng(c.Y), CSng(c.Z), CSng(c.W))
        Next


        Dim idxArr = geom.Indices
        Dim tmpTris(idxArr.Length \ 3 - 1) As Triangle
        Dim w2 As Integer = 0

        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            Dim n1 = CInt(idxArr(tr))
            Dim n2 = CInt(idxArr(tr + 1))
            Dim n3 = CInt(idxArr(tr + 2))
            tmpTris(w2) = New Triangle(n1, n2, n3)
            w2 += 1
        Next

        If geom.Version.IsSSE Then
            tri.SetVertexDataSSE(geom.VertexDataSSE)
        Else
            tri.SetVertexData(geom.VertexData)
        End If
        tri.SetVertexPositions(posN.ToList)
        tri.SetTriangles(geom.Version, tmpTris.ToList)
        If tri.HasNormals Then tri.SetNormals(norN.ToList)
        If tri.HasTangents Then tri.SetTangents(tanN.ToList)
        If tri.HasTangents Then tri.SetBitangents(bitN.ToList)
        If tri.HasUVs Then tri.SetUVs(uvN.ToList)
        If tri.HasVertexColors Then tri.SetVertexColors(colN.ToList)
        If tri.HasEyeData Then tri.SetEyeData(geom.Eyedata.ToList)

        If geom.Vertices.Length = 0 Then
            tri.HasVertices = False
            tri.HasNormals = False
            tri.HasTangents = False
            tri.HasVertexColors = False
            tri.HasEyeData = False
            tri.HasUVs = False

        End If


    End Sub

End Class
Public Class MorphingHelper
    Private Shared Sub LoadMorphTargets(shape As Shape_class, ByRef Geometry As SkinnedGeometry)
        ' 1) Inicializar el diccionario
        shape.MorphDiffs = New Dictionary(Of String, List(Of MorphData))
        ' 2) Número de vértices en el mesh base
        Dim count = Geometry.BaseVertices.Length
        ' 3) Para cada elemento de Related_Slider_data (uno por slider aplicado a esta shape)
        For Each sd In shape.Related_Slider_data
            Dim sliderName = sd.ParentSlider.Nombre
            Dim lista As New List(Of MorphData)
            shape.MorphDiffs.Add(sliderName, lista)
            ' 5) Cada bloque OSD aporta DataDiff con (Index, X,Y,Z)
            For Each block As OSD_Block_Class In sd.RelatedOSDBlocks
                For Each d As OSD_DataDiff_Class In block.DataDiff
                    Dim i = CInt(d.Index)
                    If i >= 0 AndAlso i < count Then
                        ' Guardamos solo la diferencia de posición
                        Dim posDiff As New Vector3(d.X, d.Y, d.Z)
                        lista.Add(New MorphData With {.index = i, .PosDiff = posDiff})
                    End If
                Next

            Next
        Next
    End Sub
    Public Shared Sub ApplyMorph_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, RecalculateNormals As Boolean, AllowMask As Boolean)
        Dim count = Geometry.BaseVertices.Length
        Dim verts = Geometry.BaseVertices.ToArray()

        LoadMorphTargets(shape, Geometry)
        ' Reiniciar máscara y dirty-tracking
        ApplyMask_CPU(shape, Geometry, AllowMask)
        Geometry.dirtyVertexIndices.Clear()

        Dim sliders = shape.Related_Sliders

        For Each s In sliders
            Dim raw = s.Current_Setting
            Dim t = Math.Clamp(raw / 100, 0.0F, 1.0F)
            If Single.IsNaN(t) Then t = 0

            If s.Invert Then t = 1.0F - t

            If s.IsZap Then
                ' Sólo cambia máscara
                For Each morph In shape.MorphDiffs(s.Nombre)
                    Dim i = CInt(morph.index)
                    If shape.ApplyZaps = True Then
                        Geometry.VertexMask(i) = -t

                    End If
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                Next
            Else
                ' Morph normal: mueve vértice
                For Each morph In shape.MorphDiffs(s.Nombre)
                    Dim i = CInt(morph.index)
                    verts(i) = verts(i) + morph.PosDiff * t
                Next
            End If
        Next

        For i = 0 To count - 1
            If Geometry.Vertices(i) <> verts(i) Then
                Geometry.dirtyVertexIndices.Add(i)
                Geometry.dirtyVertexFlags(i) = True
            Else
                Geometry.dirtyVertexFlags(i) = False
            End If
        Next
        Geometry.Vertices = verts
        If RecalculateNormals And Geometry.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(Geometry, opt)
            adicionales.ExceptWith(Geometry.dirtyVertexIndices)
            For Each ad In adicionales
                Geometry.dirtyVertexIndices.Add(ad)
                Geometry.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub

    Public Shared Sub RemoveZaps(shape As Shape_class, ByRef geom As SkinnedGeometry)

        If shape.ParentSliderSet.Sliders.Where(Function(pf) pf.IsZap).Any = False Then Exit Sub

        ' ==== 0) Datos locales / alias ====
        Dim tri As INiShape = geom.TriShape
        Dim vm = geom.VertexMask
        Dim nOld As Integer = geom.Vertices.Length
        Dim haszapped As Boolean = False

        ' ==== 1) Marcas a eliminar 
        Dim removed(nOld - 1) As Boolean
        For i As Integer = 0 To nOld - 1
            removed(i) = (vm(i) < 0)
            haszapped = haszapped Or (vm(i) < 0)
        Next

        If Not haszapped Then Exit Sub

        ' ==== 2) old->new y compactación in-place de arrays en SkinnedGeometry ====
        Dim oldToNew(nOld - 1) As Integer
        For i As Integer = 0 To nOld - 1 : oldToNew(i) = -1 : Next

        Dim w As Integer = 0

        Dim V = geom.Vertices
        Dim VB = geom.BaseVertices
        Dim N = geom.Normals
        Dim T = geom.Tangents
        Dim B = geom.Bitangents
        Dim UVW = geom.Uvs_Weight
        Dim VC = geom.VertexColors
        Dim ED = geom.Eyedata
        Dim VD = geom.VertexData
        Dim VDSSE = geom.VertexDataSSE
        ' NOTA: si VD es List, la paso a array para compactar in-place y luego la rearmo
        Dim VDarr() As BSVertexData = Nothing
        Dim VDarrsse() As BSVertexDataSSE = Nothing

        If geom.Version.IsSSE Then
            VDarrsse = VDSSE.ToArray()
        Else
            VDarr = VD.ToArray()
        End If

        For i As Integer = 0 To nOld - 1
            If Not removed(i) Then
                oldToNew(i) = w
                ' copiar structs tal cual (sin new)
                V(w) = V(i)
                VB(w) = VB(i)
                N(w) = N(i)
                T(w) = T(i)
                B(w) = B(i)
                UVW(w) = UVW(i)
                VC(w) = VC(i)
                If geom.Version.IsSSE Then
                    VDarrsse(w) = VDarrsse(i)
                Else
                    VDarr(w) = VDarr(i)
                End If
                ED(w) = ED(i)
                w += 1
            End If
        Next

        Dim nNew As Integer = w

        ' Redimensionar solo una vez (sin recrear elementos)
        Array.Resize(V, nNew) : geom.Vertices = V
        Array.Resize(VB, nNew) : geom.BaseVertices = VB
        Array.Resize(N, nNew) : geom.Normals = N
        Array.Resize(T, nNew) : geom.Tangents = T
        Array.Resize(B, nNew) : geom.Bitangents = B
        Array.Resize(UVW, nNew) : geom.Uvs_Weight = UVW
        Array.Resize(VC, nNew) : geom.VertexColors = VC
        Array.Resize(ED, nNew) : geom.Eyedata = ED

        ' Rehacer la lista con backing array ya compactado
        If geom.Version.IsSSE Then
            VDSSE.Clear() : VDSSE.Capacity = nNew : VDSSE.AddRange(VDarrsse.AsSpan(0, nNew).ToArray())
        Else
            VD.Clear() : VD.Capacity = nNew : VD.AddRange(VDarr.AsSpan(0, nNew).ToArray())
        End If


        ' ==== 3) Reindexado de triángulos con mínima asignación ====
        Dim idxArr = geom.Indices
        Dim tmpTris(idxArr.Length \ 3 - 1) As Triangle
        Dim w2 As Integer = 0

        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            Dim n1 = oldToNew(CInt(idxArr(tr)))
            Dim n2 = oldToNew(CInt(idxArr(tr + 1)))
            Dim n3 = oldToNew(CInt(idxArr(tr + 2)))
            If n1 >= 0 AndAlso n2 >= 0 AndAlso n3 >= 0 Then
                tmpTris(w2) = New Triangle(n1, n2, n3)
                w2 += 1

            End If
        Next

        If w2 < tmpTris.Length Then ReDim Preserve tmpTris(w2 - 1)

        Dim newIdx(3 * w2 - 1) As UInteger
        For i As Integer = 0 To w2 - 1
            Dim t2 = tmpTris(i)
            Dim base3 = 3 * i
            newIdx(base3) = CUInt(t2.V1)
            newIdx(base3 + 1) = CUInt(t2.V2)
            newIdx(base3 + 2) = CUInt(t2.V3)
        Next
        geom.Indices = newIdx

        ' Cache TBN vaciar para recalcular
        geom.CachedTBN = Nothing

        ' ==== 4) Reindexado de morphs
        For Each dat In shape.Related_Slider_data
            For Each block In dat.RelatedOSDBlocks.ToList
                For Each ddiff In block.DataDiff.ToList
                    ddiff.Index = oldToNew(ddiff.Index)
                    If ddiff.Index < 0 Then
                        block.DataDiff.Remove(ddiff)
                    End If
                Next
                If block.DataDiff.Count = 0 Then
                    block.ParentOSDContent.Blocks.Remove(block)
                End If
            Next
        Next

    End Sub

    Private Shared Sub ApplyMask_CPU(shape As Shape_class, ByRef Geometry As SkinnedGeometry, AllowMask As Boolean)
        Dim count = Geometry.BaseVertices.Length
        If Not AllowMask Then
            Array.Clear(Geometry.VertexMask, 0, count)
            Geometry.dirtyMaskIndices.Clear()
            For i = 0 To count - 1
                Geometry.dirtyMaskFlags(i) = False
            Next
        Else
            Dim maskeds = shape.MaskedVertices
            For i = 0 To count - 1
                If Geometry.VertexMask(i) = -1 Then
                    If shape.MaskedVertices.Contains(i) Then Geometry.VertexMask(i) = 1 Else Geometry.VertexMask(i) = 0
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                End If
                If (Geometry.VertexMask(i) = 0 AndAlso maskeds.Contains(i)) OrElse (Geometry.VertexMask(i) = 1 AndAlso Not maskeds.Contains(i)) Then
                    Geometry.dirtyMaskIndices.Add(i)
                    Geometry.dirtyMaskFlags(i) = True
                    Geometry.VertexMask(i) = 1 - Geometry.VertexMask(i)
                Else
                    If Geometry.VertexMask(i) <> 0 Then
                        Geometry.dirtyMaskIndices.Add(i)
                        Geometry.VertexMask(i) = Geometry.VertexMask(i)
                        Geometry.dirtyMaskFlags(i) = True
                    Else
                        Geometry.dirtyMaskFlags(i) = False
                    End If
                End If
            Next
        End If
    End Sub
End Class


Public Class RecalcTBN
    Public Structure TBNCache
        ' Copia/Referencia de índices del mesh (no se modifica aquí)
        Public Indices As UInteger()
        ' Cantidad de triángulos
        Public TriCount As Integer
        ' Adjacencia: por cada vértice -> lista de triángulos incidentes (ID de tri: [0..TriCount-1])
        Public VertexToTriangles As List(Of Integer)()
        ' Derivadas UV precomputadas por triángulo (dependen SOLO de UV)
        Public Tri_du1 As Double()
        Public Tri_dv1 As Double()
        Public Tri_du2 As Double()
        Public Tri_dv2 As Double()
        Public Tri_det As Double()
    End Structure

    ' -------------------------------
    ' Opciones de calidad / robustez
    ' -------------------------------
    Public Enum NormalWeightMode
        AreaOnly = 0
        AngleOnly = 1
        AreaTimesAngle = 2   ' recomendado (por defecto)
    End Enum

    Public Structure TBNOptions
        Public Property WeightMode As NormalWeightMode          ' cómo pesar contribuciones de caras
        Public Property EpsilonPos As Double                    ' umbral para degenerados geométricos
        Public Property EpsilonUV As Double                     ' umbral para degenerados en UV (det≈0)
        Public Property NormalizeOutputs As Boolean             ' normalizar N/T/B al final
        Public Property ForceOrthogonalBitangent As Boolean     ' si True: B := normalize(N × T)
        Public Property RepairNaNs As Boolean                   ' si True: reemplaza NaN por vectores seguros

        ' --- Welding (opcional) ---
        Public Property EnableWelding As Boolean                ' activa agrupación por posición+UV
        Public Property WeldPosEpsilon As Double                ' tolerancia para posición (en unidades del modelo)
        Public Property WeldUVEpsilon As Double                 ' tolerancia para UV (u,v)
        Public Property WeldByPositionOnly As Boolean           ' Only positions or positions + UV
    End Structure

    Public Shared Function DefaultTBNOptions() As TBNOptions
        Return New TBNOptions With {
                .WeightMode = NormalWeightMode.AreaTimesAngle,
                .EpsilonPos = 0.000000000001,
                .EpsilonUV = 0.000000000001,
                .NormalizeOutputs = True,
                .ForceOrthogonalBitangent = True,     ' false preserva B acumulada si es válida
                .RepairNaNs = True,
                .EnableWelding = False,                ' desactivado por defecto
                .WeldPosEpsilon = 0.000000000001,
                .WeldUVEpsilon = 0.000000000001,
                .WeldByPositionOnly = False           ' Positions + UV
            }
    End Function

    ' =========================================================================
    ' BUILD CACHE (llamar una sola vez al cargar o cuando cambien UV o índices)
    ' - Precomputa:
    '   * VertexToTriangles (adjacencia)
    '   * Derivadas UV por triángulo (du1,dv1,du2,dv2,det)
    ' =========================================================================
    Public Shared Function BuildTBNCache(ByRef Uvs_Weight() As Vector3, ByVal indices As UInteger()) As TBNCache
        Dim nVerts As Integer = Uvs_Weight.Length
        Dim triCount As Integer = indices.Length \ 3
        Dim v2t As List(Of Integer)() = New List(Of Integer)(nVerts - 1) {}
        For v = 0 To nVerts - 1
            v2t(v) = New List(Of Integer)(8)
        Next

        ' Derivadas UV por tri
        Dim du1(triCount - 1) As Double
        Dim dv1(triCount - 1) As Double
        Dim du2(triCount - 1) As Double
        Dim dv2(triCount - 1) As Double
        Dim det(triCount - 1) As Double

        For t As Integer = 0 To triCount - 1
            Dim i0 As Integer = CInt(indices(3 * t + 0))
            Dim i1 As Integer = CInt(indices(3 * t + 1))
            Dim i2 As Integer = CInt(indices(3 * t + 2))

            ' Adjacencia
            Try
                v2t(i0).Add(t)
                v2t(i1).Add(t)
                v2t(i2).Add(t)
            Catch ex As Exception
                Debugger.Break()
                Exit For
            End Try


            ' UV del tri
            Dim uv0 As Vector3 = Uvs_Weight(i0)
            Dim uv1 As Vector3 = Uvs_Weight(i1)
            Dim uv2 As Vector3 = Uvs_Weight(i2)

            Dim _du1 As Double = uv1.X - uv0.X
            Dim _dv1 As Double = uv1.Y - uv0.Y
            Dim _du2 As Double = uv2.X - uv0.X
            Dim _dv2 As Double = uv2.Y - uv0.Y

            du1(t) = _du1 : dv1(t) = _dv1
            du2(t) = _du2 : dv2(t) = _dv2
            det(t) = _du1 * _dv2 - _du2 * _dv1
        Next

        Return New TBNCache With {
            .Indices = indices,
            .TriCount = triCount,
            .VertexToTriangles = v2t,
            .Tri_du1 = du1, .Tri_dv1 = dv1,
            .Tri_du2 = du2, .Tri_dv2 = dv2,
            .Tri_det = det
        }
    End Function

    ' ===========================================================================================
    ' API PÚBLICA: Recalcular N/T/B SOLO para la clausura afectada (dirty + sus triángulos)
    ' - Usa el cache (adjacencia + UV-derivs). Welding opcional (NO cacheado).
    ' ===========================================================================================
    Public Shared Function RecalculateNormalsTangentsBitangents(ByRef geo As SkinnedGeometry, ByVal opts As TBNOptions) As HashSet(Of Integer)
        If IsNothing(geo.CachedTBN.Indices) Then
            geo.CachedTBN = BuildTBNCache(geo.Uvs_Weight, geo.Indices)
        End If
        Dim nVerts As Integer = geo.Vertices.Length

        Dim Vertices_Adicionales As New HashSet(Of Integer)
        If nVerts = 0 OrElse geo.dirtyVertexIndices Is Nothing OrElse geo.dirtyVertexIndices.Count = 0 Then
            Return Vertices_Adicionales ' nada que hacer; si querés todo, pasá todos los índices como dirty
        End If

        ' -------- (Opcional) Welding lógico por posición+UV (NO cacheado) --------
        Dim masterOf() As Integer = Nothing
        Dim membersOf As Dictionary(Of Integer, List(Of Integer)) = Nothing
        If opts.EnableWelding Then
            Vertices_Adicionales.UnionWith(BuildWeldGroups(geo, opts.WeldPosEpsilon, opts.WeldUVEpsilon, opts.WeldByPositionOnly, masterOf, membersOf))
        Else
            masterOf = New Integer(nVerts - 1) {}
            membersOf = New Dictionary(Of Integer, List(Of Integer))(nVerts)
            For i As Integer = 0 To nVerts - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
        End If

        ' -------- 1) Triángulos afectados via adjacencia --------
        Dim affectedTris As New HashSet(Of Integer)()
        For Each vi In geo.dirtyVertexIndices
            If vi < 0 OrElse vi >= nVerts Then Continue For
            Dim triList As List(Of Integer) = geo.CachedTBN.VertexToTriangles(vi)
            For Each t In triList
                affectedTris.Add(t)
            Next
        Next
        If affectedTris.Count = 0 Then Return Vertices_Adicionales

        ' -------- 2) Clausura de vértices a actualizar (incluye grupos por maestro si hay welding) --------
        Dim affectedVerts As New HashSet(Of Integer)(geo.dirtyVertexIndices)
        For Each t In affectedTris
            Dim i0 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 0))
            Dim i1 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 1))
            Dim i2 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 2))
            affectedVerts.Add(i0) : affectedVerts.Add(i1) : affectedVerts.Add(i2)
            affectedVerts.Add(masterOf(i0)) : affectedVerts.Add(masterOf(i1)) : affectedVerts.Add(masterOf(i2))
            Vertices_Adicionales.Add(i0)
            Vertices_Adicionales.Add(i1)
            Vertices_Adicionales.Add(i2)
            Vertices_Adicionales.Add(masterOf(i0))
            Vertices_Adicionales.Add(masterOf(i1))
            Vertices_Adicionales.Add(masterOf(i2))
        Next

        ' -------- 3) Acumuladores (del tamaño de nVerts; escribimos por "maestro") --------
        Dim nAccum() As Vector3d = New Vector3d(nVerts - 1) {}
        Dim tAccum() As Vector3d = New Vector3d(nVerts - 1) {}
        Dim bAccum() As Vector3d = New Vector3d(nVerts - 1) {}

        ' -------- 4) Loop por triángulos afectados (usa UV-derivs cacheadas) --------
        For Each t In affectedTris
            Dim i0 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 0))
            Dim i1 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 1))
            Dim i2 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 2))

            Dim m0 As Integer = masterOf(i0)
            Dim m1 As Integer = masterOf(i1)
            Dim m2 As Integer = masterOf(i2)

            Dim p0 As Vector3d = geo.Vertices(i0)
            Dim p1 As Vector3d = geo.Vertices(i1)
            Dim p2 As Vector3d = geo.Vertices(i2)

            ' Aristas en espacio objeto (posiciones pueden haber cambiado por morph/pose)
            Dim e1 As Vector3d = p1 - p0
            Dim e2 As Vector3d = p2 - p0

            ' Normal de cara no normalizada (proporcional al área * 2)
            Dim fn As Vector3d = Vector3d.Cross(e1, e2)
            Dim area2 As Double = fn.Length
            If area2 <= opts.EpsilonPos Then
                ' Cara degenerada: omitimos contribución
                Continue For
            End If

            ' Pesos por ángulo (si corresponde)
            Dim w0 As Double = 1.0, w1 As Double = 1.0, w2 As Double = 1.0
            If opts.WeightMode <> NormalWeightMode.AreaOnly Then
                w0 = AngleBetweenSafe(e1, e2, opts.EpsilonPos)
                Dim a1 As Vector3d = p0 - p1, b1 As Vector3d = p2 - p1
                w1 = AngleBetweenSafe(a1, b1, opts.EpsilonPos)
                Dim a2 As Vector3d = p0 - p2, b2 As Vector3d = p1 - p2
                w2 = AngleBetweenSafe(a2, b2, opts.EpsilonPos)
            End If
            Dim aw As Double = area2
            Dim wn0 As Double, wn1 As Double, wn2 As Double
            Select Case opts.WeightMode
                Case NormalWeightMode.AreaOnly
                    wn0 = aw : wn1 = aw : wn2 = aw
                Case NormalWeightMode.AngleOnly
                    wn0 = w0 : wn1 = w1 : wn2 = w2
                Case Else ' AreaTimesAngle
                    wn0 = aw * w0 : wn1 = aw * w1 : wn2 = aw * w2
            End Select

            ' ---- Tangente/Bitangente por cara (usando derivadas UV cacheadas) ----
            Dim _du1 As Double = geo.CachedTBN.Tri_du1(t)
            Dim _dv1 As Double = geo.CachedTBN.Tri_dv1(t)
            Dim _du2 As Double = geo.CachedTBN.Tri_du2(t)
            Dim _dv2 As Double = geo.CachedTBN.Tri_dv2(t)
            Dim _det As Double = geo.CachedTBN.Tri_det(t)

            Dim tFace As Vector3d
            Dim bFace As Vector3d

            If Math.Abs(_det) <= opts.EpsilonUV Then
                ' UV degeneradas: fallback estable en el plano de la normal de cara
                Dim nf As Vector3d = Vector3d.Normalize(fn)
                Dim e1p As Vector3d = e1 - nf * Vector3d.Dot(nf, e1)
                If e1p.LengthSquared <= opts.EpsilonPos Then
                    e1p = e2 - nf * Vector3d.Dot(nf, e2)
                End If
                If e1p.LengthSquared <= opts.EpsilonPos Then
                    tFace = Vector3d.Zero
                    bFace = Vector3d.Zero
                Else
                    tFace = Vector3d.Normalize(e1p)
                    bFace = Vector3d.Normalize(Vector3d.Cross(nf, tFace))
                End If
            Else
                Dim r As Double = 1.0 / _det
                tFace = (e1 * _dv2 - e2 * _dv1) * r
                bFace = (e2 * _du1 - e1 * _du2) * r
            End If

            ' ---- Acumulación en índices MAESTROS ----
            nAccum(m0) += fn * wn0 : nAccum(m1) += fn * wn1 : nAccum(m2) += fn * wn2
            tAccum(m0) += tFace * wn0 : tAccum(m1) += tFace * wn1 : tAccum(m2) += tFace * wn2
            bAccum(m0) += bFace * wn0 : bAccum(m1) += bFace * wn1 : bAccum(m2) += bFace * wn2
        Next

        ' -------- 5) Finalización en maestros y propagación a TODOS los miembros del grupo --------
        Dim processedMasters As New HashSet(Of Integer)()
        ' candidatos = maestros de todos los vértices afectados
        Dim candidates As New HashSet(Of Integer)()
        For Each vi In affectedVerts
            candidates.Add(masterOf(vi))
        Next

        For Each m As Integer In candidates
            If processedMasters.Contains(m) Then Continue For

            Dim N As Vector3d = nAccum(m)
            Dim T As Vector3d = tAccum(m)
            Dim B As Vector3d = bAccum(m)

            ' Normal
            If N.LengthSquared <= opts.EpsilonPos OrElse HasNaN(N) Then
                N = New Vector3d(0, 0, 1)
            ElseIf opts.NormalizeOutputs Then
                N = Vector3d.Normalize(N)
            End If

            ' Tangente: Gram–Schmidt contra N
            T += -N * Vector3d.Dot(N, T)
            If T.LengthSquared <= opts.EpsilonPos OrElse HasNaN(T) Then
                T = OrthonormalTangentFromNormal(N)
            ElseIf opts.NormalizeOutputs Then
                T = Vector3d.Normalize(T)
            End If

            ' Bitangente
            Dim Bcross As Vector3d = Vector3d.Cross(N, T)
            Dim s As Double = 1.0
            ' Proyectar B acumulado sobre el plano de N para compararlo de forma estable
            Dim Bproj As Vector3d = B - N * Vector3d.Dot(N, B)
            If Not HasNaN(Bproj) AndAlso Bproj.LengthSquared > opts.EpsilonPos Then
                If Vector3d.Dot(Bcross, Bproj) < 0.0 Then s = -1.0
            End If

            If opts.ForceOrthogonalBitangent Then
                B = Bcross * s
            Else
                ' Ruta original (preserva B acumulado si es válido)
                B += -N * Vector3d.Dot(N, B)
                If B.LengthSquared <= opts.EpsilonPos OrElse HasNaN(B) Then
                    B = Bcross * s
                End If
            End If

            If opts.NormalizeOutputs AndAlso B.LengthSquared > opts.EpsilonPos Then
                B = Vector3d.Normalize(B)
            End If

            If opts.RepairNaNs Then
                If HasNaN(B) Then B = Bcross * s ' si aún falló, caer a cross(N,T) 
            End If

            ' Propagar a TODOS los miembros del grupo (sin alterar topología)
            Dim members As List(Of Integer) = Nothing
            If membersOf.TryGetValue(m, members) Then
                For Each vi As Integer In members
                    geo.Normals(vi) = N
                    geo.Tangents(vi) = T
                    geo.Bitangents(vi) = B
                Next
            Else
                ' fallback: escribir en m por si acaso
                geo.Normals(m) = N
                geo.Tangents(m) = T
                geo.Bitangents(m) = B
            End If

            processedMasters.Add(m)
        Next
        Return Vertices_Adicionales
    End Function

    ' -----------------------
    ' Utilitarios privados
    ' -----------------------

    ' Welding lógico por posición+UV con tolerancias (NO cacheado)
    Private Shared Function BuildWeldGroups(ByRef geo As SkinnedGeometry, ByVal weldPosEpsOrig As Double, ByVal weldUVEps As Double, ByVal byPosOnly As Boolean, ByRef masterOf() As Integer, ByRef membersOf As Dictionary(Of Integer, List(Of Integer))) As HashSet(Of Integer)
        Dim n As Integer = geo.Vertices.Length
        Dim vertices_adicionales As New HashSet(Of Integer)
        masterOf = New Integer(n - 1) {}
        membersOf = New Dictionary(Of Integer, List(Of Integer))(n)
        Dim extent As Vector3d = geo.Maxv - geo.Minv
        Dim diag As Double = extent.Length
        Dim maxSpan As Double = Math.Max(Math.Max(Math.Abs(extent.X), Math.Abs(extent.Y)), Math.Abs(extent.Z))
        ' Heurística de epsilon relativo (elegí uno de los dos L)
        Dim L As Double = If(diag > 0.0, diag, maxSpan)
        ' Parámetros de control (ajustables)
        Dim k As Double = weldPosEpsOrig     ' fracción de la escala de la malla
        Dim floorEps As Double = 0.000000000001
        Dim ceilEps As Double = 0.001   ' evita sobre-soldar en mallas gigantes

        Dim weldPosEps As Double
        If L <= 0.0 Then
            weldPosEps = floorEps
        Else
            weldPosEps = Math.Max(floorEps, Math.Min(ceilEps, k * L))
        End If

        If weldPosEps <= 0 OrElse (Not byPosOnly AndAlso weldUVEps <= 0) OrElse n = 0 Then
            For i As Integer = 0 To n - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
            Return vertices_adicionales
        End If

        ' Hash buckets por celda cuantizada
        Dim buckets As New Dictionary(Of WeldKey, List(Of Integer))(n)

        For i As Integer = 0 To n - 1
            Dim p As Vector3d = geo.Vertices(i)
            Dim uv As Vector3 = geo.Uvs_Weight(i)

            ' Clave cuantizada por tolerancia (redondeo a celda)
            Dim key As WeldKey = WeldKey.From(p, uv, weldPosEps, weldUVEps, byPosOnly)

            Dim list As List(Of Integer) = Nothing
            If Not buckets.TryGetValue(key, list) Then
                list = New List(Of Integer)()
                buckets(key) = list
            End If

            ' Buscar en el bucket si ya existe un maestro compatible (chequeo fino)
            Dim assigned As Boolean = False
            For Each cand As Integer In list.ToList
                Dim posOk As Boolean = ClosePos(geo.Vertices(cand), p, weldPosEps)
                Dim uvOk As Boolean = byPosOnly OrElse CloseUV(geo.Uvs_Weight(cand), uv, weldUVEps)
                If posOk AndAlso uvOk Then
                    masterOf(i) = masterOf(cand)
                    membersOf(masterOf(cand)).Add(i)
                    list.Add(i)
                    vertices_adicionales.Add(i)
                    assigned = True
                    Exit For
                End If
            Next

            If Not assigned Then
                ' Nuevo grupo con i como maestro
                masterOf(i) = i
                list.Add(i)
                membersOf(i) = New List(Of Integer)(4) From {i}
            End If
        Next
        Return vertices_adicionales
    End Function


    ' Clave de bucket (cuantización por eps)
    Private Structure WeldKey
        Public qx As Long, qy As Long, qz As Long
        Public qu As Long, qv As Long

        Public Shared Function From(p As Vector3d, uv As Vector3, posEps As Double, uvEps As Double, byPosOnly As Boolean) As WeldKey
            Dim invPos As Double = If(posEps > 0.0, 1.0 / posEps, 0.0)
            Dim invUV As Double = If(uvEps > 0.0, 1.0 / uvEps, 0.0)

            Dim k As WeldKey
            k.qx = QuantizeToLong(p.X, invPos)
            k.qy = QuantizeToLong(p.Y, invPos)
            k.qz = QuantizeToLong(p.Z, invPos)
            If byPosOnly Then
                k.qu = 0 : k.qv = 0
            Else
                k.qu = QuantizeToLong(uv.X, invUV)
                k.qv = QuantizeToLong(uv.Y, invUV)
            End If
            Return k
        End Function

        Private Shared Function QuantizeToLong(val As Double, invStep As Double) As Long
            If invStep <= 0.0 Then Return 0
            If Double.IsNaN(val) OrElse Double.IsInfinity(val) Then Return 0
            Dim q As Double = Math.Round(val * invStep)
            Const LMAX As Double = 9.2233720368547758E+18
            Const LMIN As Double = -9.2233720368547758E+18
            If q > LMAX Then Return Long.MaxValue
            If q < LMIN Then Return Long.MinValue
            Return CLng(q)
        End Function

        Public Overrides Function GetHashCode() As Integer
            ' versión segura (sin overflow)
            Dim hc As New HashCode()
            hc.Add(qx) : hc.Add(qy) : hc.Add(qz) : hc.Add(qu) : hc.Add(qv)
            Return hc.ToHashCode()
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot WeldKey Then Return False
            Dim o As WeldKey = CType(obj, WeldKey)
            Return qx = o.qx AndAlso qy = o.qy AndAlso qz = o.qz AndAlso qu = o.qu AndAlso qv = o.qv
        End Function
    End Structure

    ' Comparación fina por componente (posición)
    Private Shared Function ClosePos(a As Vector3d, b As Vector3d, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps AndAlso Math.Abs(a.Z - b.Z) <= eps
    End Function

    ' Comparación fina por componente (UV)
    Private Shared Function CloseUV(a As Vector3, b As Vector3, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps
    End Function

    ' Ángulo seguro entre a y b (radianes); 0 si degenerado
    Private Shared Function AngleBetweenSafe(a As Vector3d, b As Vector3d, eps As Double) As Double
        Dim la As Double = a.Length
        Dim lb As Double = b.Length
        If la <= eps OrElse lb <= eps Then Return 0.0
        Dim c As Double = Vector3d.Dot(a, b) / (la * lb)
        If c > 1.0 Then c = 1.0
        If c < -1.0 Then c = -1.0
        Return Math.Acos(c)
    End Function

    ' Tangente ortonormal a partir de una normal: elige un eje auxiliar poco alineado
    Private Shared Function OrthonormalTangentFromNormal(n As Vector3d) As Vector3d
        Dim ax As Vector3d = If(Math.Abs(n.X) < 0.9, New Vector3d(1, 0, 0), New Vector3d(0, 1, 0))
        Dim t As Vector3d = Vector3d.Cross(ax, n)
        If t.LengthSquared <= 1.0E-20 Then t = Vector3d.Cross(New Vector3d(0, 0, 1), n)
        If t.LengthSquared <= 1.0E-20 Then Return New Vector3d(1, 0, 0)
        Return Vector3d.Normalize(t)
    End Function

    Private Shared Function HasNaN(v As Vector3d) As Boolean
        Return Double.IsNaN(v.X) OrElse Double.IsNaN(v.Y) OrElse Double.IsNaN(v.Z)
    End Function

End Class