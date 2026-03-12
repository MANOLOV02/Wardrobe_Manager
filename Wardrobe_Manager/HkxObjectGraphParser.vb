Option Strict On
Option Explicit On

Imports System.IO
Imports System.Linq
Imports System.Text

Public NotInheritable Class HkxObjectGraphParser_Class
    Public Shared Function BuildGraph(packfile As HkxPackfile_Class) As HkxObjectGraph_Class
        Return New HkxObjectGraph_Class(packfile)
    End Function
End Class

Public Class HkxObjectGraph_Class
    Public ReadOnly Property Packfile As HkxPackfile_Class
    Public ReadOnly Property ContentsSection As HkxPackfileSection_Class
    Public ReadOnly Property Objects As New List(Of HkxVirtualObjectGraph_Class)

    Private ReadOnly _localFixupsBySource As New Dictionary(Of Integer, HkxLocalFixupEntry_Class)
    Private ReadOnly _globalFixupsBySource As New Dictionary(Of Integer, HkxGlobalFixupEntry_Class)
    Private ReadOnly _objectsByOffset As New Dictionary(Of Integer, HkxVirtualObjectGraph_Class)
    Private ReadOnly _objectsByClassName As New Dictionary(Of String, List(Of HkxVirtualObjectGraph_Class))(StringComparer.OrdinalIgnoreCase)

    Public Sub New(packfile As HkxPackfile_Class)
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        If IsNothing(packfile.Header) Then Throw New InvalidOperationException("The HKX packfile has not been parsed.")

        Me.Packfile = packfile
        Me.ContentsSection = packfile.GetSection(packfile.Header.ContentsSectionIndex)
        If IsNothing(Me.ContentsSection) Then Throw New InvalidOperationException("The HKX contents section was not found.")

        BuildIndices()
    End Sub

    Private Sub BuildIndices()
        For Each fixup In Packfile.LocalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex)
            If Not _localFixupsBySource.ContainsKey(fixup.SourceRelativeOffset) Then
                _localFixupsBySource.Add(fixup.SourceRelativeOffset, fixup)
            End If
        Next

        For Each fixup In Packfile.GlobalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex)
            If Not _globalFixupsBySource.ContainsKey(fixup.SourceRelativeOffset) Then
                _globalFixupsBySource.Add(fixup.SourceRelativeOffset, fixup)
            End If
        Next

        Dim dataRelativeEnd = ContentsSection.DataEndAbsolute - ContentsSection.AbsoluteDataStart
        Dim orderedVirtualFixups = Packfile.VirtualFixups.
            Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).
            OrderBy(Function(pf) pf.ObjectRelativeOffset).
            ToList()

        For i = 0 To orderedVirtualFixups.Count - 1
            Dim fixup = orderedVirtualFixups(i)
            Dim classEntry = Packfile.GetClassName(fixup.ClassNameSectionIndex, fixup.ClassNameRelativeOffset)
            Dim size = If(i < orderedVirtualFixups.Count - 1,
                          orderedVirtualFixups(i + 1).ObjectRelativeOffset - fixup.ObjectRelativeOffset,
                          dataRelativeEnd - fixup.ObjectRelativeOffset)

            Dim obj As New HkxVirtualObjectGraph_Class With {
                .SectionIndex = fixup.SectionIndex,
                .RelativeOffset = fixup.ObjectRelativeOffset,
                .AbsoluteOffset = ContentsSection.AbsoluteDataStart + fixup.ObjectRelativeOffset,
                .ClassNameSectionIndex = fixup.ClassNameSectionIndex,
                .ClassNameRelativeOffset = fixup.ClassNameRelativeOffset,
                .ClassName = If(classEntry?.Name, String.Empty),
                .Size = size
            }

            Objects.Add(obj)
            _objectsByOffset(obj.RelativeOffset) = obj

            If Not _objectsByClassName.ContainsKey(obj.ClassName) Then
                _objectsByClassName.Add(obj.ClassName, New List(Of HkxVirtualObjectGraph_Class))
            End If
            _objectsByClassName(obj.ClassName).Add(obj)
        Next
    End Sub

    Public Function GetObject(relativeOffset As Integer) As HkxVirtualObjectGraph_Class
        Dim value As HkxVirtualObjectGraph_Class = Nothing
        If _objectsByOffset.TryGetValue(relativeOffset, value) Then Return value
        Return Nothing
    End Function

    Public Function GetObjectsByClassName(className As String) As IEnumerable(Of HkxVirtualObjectGraph_Class)
        If String.IsNullOrWhiteSpace(className) Then Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
        Dim values As List(Of HkxVirtualObjectGraph_Class) = Nothing
        If _objectsByClassName.TryGetValue(className, values) Then Return values
        Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
    End Function

    Public Function GetRootObject() As HkxVirtualObjectGraph_Class
        If IsNothing(Packfile.RootObject) Then Return Nothing
        Return GetObject(Packfile.RootObject.RelativeOffset)
    End Function
    Public Function TryGetLocalFixup(sourceRelativeOffset As Integer, ByRef result As HkxLocalFixupEntry_Class) As Boolean
        Return _localFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function TryGetGlobalFixup(sourceRelativeOffset As Integer, ByRef result As HkxGlobalFixupEntry_Class) As Boolean
        Return _globalFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function GetLocalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxLocalFixupEntry_Class)
        Dim result As New List(Of HkxLocalFixupEntry_Class)
        If byteCount <= 0 Then Return result

        Dim rangeEnd = relativeOffset + byteCount
        For Each fixup In Packfile.LocalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).OrderBy(Function(pf) pf.SourceRelativeOffset)
            If fixup.SourceRelativeOffset < relativeOffset Then Continue For
            If fixup.SourceRelativeOffset >= rangeEnd Then Exit For
            result.Add(fixup)
        Next

        Return result
    End Function

    Public Function GetGlobalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxGlobalFixupEntry_Class)
        Dim result As New List(Of HkxGlobalFixupEntry_Class)
        If byteCount <= 0 Then Return result

        Dim rangeEnd = relativeOffset + byteCount
        For Each fixup In Packfile.GlobalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).OrderBy(Function(pf) pf.SourceRelativeOffset)
            If fixup.SourceRelativeOffset < relativeOffset Then Continue For
            If fixup.SourceRelativeOffset >= rangeEnd Then Exit For
            result.Add(fixup)
        Next

        Return result
    End Function

    Public Function ResolveLocalPointer(sourceRelativeOffset As Integer) As Integer?
        Dim fixup As HkxLocalFixupEntry_Class = Nothing
        If Not TryGetLocalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return fixup.DestinationRelativeOffset
    End Function

    Public Function ResolveGlobalObject(sourceRelativeOffset As Integer) As HkxVirtualObjectGraph_Class
        Dim fixup As HkxGlobalFixupEntry_Class = Nothing
        If Not TryGetGlobalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return GetObject(fixup.TargetRelativeOffset)
    End Function

    Public Function ResolveLocalString(sourceRelativeOffset As Integer) As String
        Dim destination = ResolveLocalPointer(sourceRelativeOffset)
        If Not destination.HasValue Then Return String.Empty
        Return ReadNullTerminatedString(destination.Value)
    End Function

    Public Function ReadNullTerminatedString(relativeOffset As Integer) As String
        Dim absoluteOffset = ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < ContentsSection.AbsoluteDataStart OrElse absoluteOffset >= ContentsSection.DataEndAbsolute Then Return String.Empty

        Dim endOffset = absoluteOffset
        While endOffset < ContentsSection.DataEndAbsolute AndAlso Packfile.RawBytes(endOffset) <> 0
            endOffset += 1
        End While

        Return Encoding.ASCII.GetString(Packfile.RawBytes, absoluteOffset, endOffset - absoluteOffset)
    End Function

    Public Function ReadInt16(relativeOffset As Integer) As Short
        EnsureReadable(relativeOffset, 2)
        Return BitConverter.ToInt16(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadInt32(relativeOffset As Integer) As Integer
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToInt32(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadSingle(relativeOffset As Integer) As Single
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToSingle(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadArrayHeader(fieldRelativeOffset As Integer) As HkxObjectArrayHeader_Class
        Dim pointer = ResolveLocalPointer(fieldRelativeOffset)
        Return New HkxObjectArrayHeader_Class With {
            .FieldRelativeOffset = fieldRelativeOffset,
            .DataRelativeOffset = If(pointer.HasValue, pointer.Value, -1),
            .Count = ReadInt32(fieldRelativeOffset + 8),
            .CapacityAndFlags = ReadInt32(fieldRelativeOffset + 12)
        }
    End Function

    Public Function ReadStructureOffsets(fieldRelativeOffset As Integer, itemSize As Integer) As List(Of Integer)
        Dim result As New List(Of Integer)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If itemSize <= 0 OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(header.DataRelativeOffset + (i * itemSize))
        Next

        Return result
    End Function

    Public Function ReadObjectReferenceArray(fieldRelativeOffset As Integer) As List(Of HkxVirtualObjectGraph_Class)
        Dim result As New List(Of HkxVirtualObjectGraph_Class)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        Dim stride = Math.Max(1, CInt(Packfile.Header.PointerSize))
        For i = 0 To header.Count - 1
            Dim obj = ResolveGlobalObject(header.DataRelativeOffset + (i * stride))
            If Not IsNothing(obj) Then result.Add(obj)
        Next

        Return result
    End Function

    Public Function ReadInt16Array(fieldRelativeOffset As Integer) As List(Of Short)
        Dim result As New List(Of Short)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(ReadInt16(header.DataRelativeOffset + (i * 2)))
        Next

        Return result
    End Function

    Private Sub EnsureReadable(relativeOffset As Integer, byteCount As Integer)
        Dim dataRelativeEnd = ContentsSection.DataEndAbsolute - ContentsSection.AbsoluteDataStart
        If relativeOffset < 0 OrElse byteCount < 0 OrElse relativeOffset + byteCount > dataRelativeEnd Then
            Throw New InvalidDataException($"Requested HKX range is out of bounds: offset=0x{relativeOffset:X} size={byteCount}.")
        End If
    End Sub
    Public Function ParseRootLevelContainer() As HkxRootLevelContainerGraph_Class
        Dim rootObject = GetRootObject()
        If IsNothing(rootObject) OrElse Not rootObject.ClassName.Equals("hkRootLevelContainer", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HkxRootLevelContainerGraph_Class With {
            .SourceObject = rootObject
        }

        For Each variantOffset In ReadStructureOffsets(rootObject.RelativeOffset, 24)
            result.NamedVariants.Add(ReadNamedVariant(variantOffset))
        Next

        Return result
    End Function

    Public Function ParseClothData(source As HkxVirtualObjectGraph_Class) As HclClothDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclClothDataGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .UnknownValue78 = ReadInt32(source.RelativeOffset + &H78),
            .UnknownValue7C = ReadInt32(source.RelativeOffset + &H7C),
            .SimClothDatas = ReadObjectReferenceArray(source.RelativeOffset + &H18),
            .BufferDefinitions = ReadObjectReferenceArray(source.RelativeOffset + &H28),
            .TransformSetDefinitions = ReadObjectReferenceArray(source.RelativeOffset + &H38),
            .Operators = ReadObjectReferenceArray(source.RelativeOffset + &H48),
            .ClothStates = ReadObjectReferenceArray(source.RelativeOffset + &H58)
        }

        result.Fields.Add(CreateArrayField(source, &H18, "SimClothDatas"))
        result.Fields.Add(CreateArrayField(source, &H28, "BufferDefinitions"))
        result.Fields.Add(CreateArrayField(source, &H38, "TransformSetDefinitions"))
        result.Fields.Add(CreateArrayField(source, &H48, "Operators"))
        result.Fields.Add(CreateArrayField(source, &H58, "ClothStates"))

        Return result
    End Function

    Public Function ParseSimClothData(source As HkxVirtualObjectGraph_Class) As HclSimClothDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclSimClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclSimClothDataGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H30),
            .UnknownFloat20 = ReadSingle(source.RelativeOffset + &H20),
            .UnknownFloat24 = ReadSingle(source.RelativeOffset + &H24),
            .UnknownFloat28 = ReadSingle(source.RelativeOffset + &H28),
            .UnknownFloat2C = ReadSingle(source.RelativeOffset + &H2C)
        }

        For Each field In {
            New With {.Offset = &H38, .Name = "Array_38"},
            New With {.Offset = &H48, .Name = "Array_48"},
            New With {.Offset = &H58, .Name = "Array_58"},
            New With {.Offset = &H68, .Name = "Array_68"},
            New With {.Offset = &H88, .Name = "Array_88"},
            New With {.Offset = &H98, .Name = "Array_98"},
            New With {.Offset = &HA8, .Name = "Array_A8"},
            New With {.Offset = &HB8, .Name = "Array_B8"},
            New With {.Offset = &HD8, .Name = "Array_D8"},
            New With {.Offset = &HF8, .Name = "Array_F8"},
            New With {.Offset = &H108, .Name = "Array_108"},
            New With {.Offset = &H118, .Name = "Array_118"}
        }
            result.ArrayFields.Add(CreateArrayField(source, field.Offset, field.Name))
        Next

        Return result
    End Function

    Public Function ParseClothState(source As HkxVirtualObjectGraph_Class) As HclClothStateGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclClothState", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclClothStateGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10)
        }

        For Each field In {
            New With {.Offset = &H18, .Name = "Array_18"},
            New With {.Offset = &H28, .Name = "Array_28"},
            New With {.Offset = &H38, .Name = "Array_38"},
            New With {.Offset = &H48, .Name = "Array_48"},
            New With {.Offset = &HB0, .Name = "Array_B0"},
            New With {.Offset = &HC0, .Name = "Array_C0"},
            New With {.Offset = &HD8, .Name = "Array_D8"},
            New With {.Offset = &HF0, .Name = "Array_F0"},
            New With {.Offset = &H108, .Name = "Array_108"},
            New With {.Offset = &H120, .Name = "Array_120"},
            New With {.Offset = &H138, .Name = "Array_138"}
        }
            Dim arrayField = CreateArrayField(source, field.Offset, field.Name)
            result.ArrayFields.Add(arrayField)

            Dim objectRefs = ReadObjectReferenceArray(source.RelativeOffset + field.Offset)
            If objectRefs.Count > 0 Then
                result.ObjectReferenceArrays.Add(New HkxObjectReferenceArrayGraph_Class With {
                    .FieldName = field.Name,
                    .Objects = objectRefs
                })
            End If
        Next

        Return result
    End Function

    Public Function ParseSimClothPose(source As HkxVirtualObjectGraph_Class) As HclSimClothPoseGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclSimClothPose", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclSimClothPoseGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .PoseField = CreateArrayField(source, &H18, "Pose")
        }

        result.Pose = ReadVector4Array(result.PoseField)
        Return result
    End Function

    Public Function ParseCollidable(source As HkxVirtualObjectGraph_Class) As HclCollidableGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclCollidable", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclCollidableGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .ShapeObject = ResolveGlobalObject(source.RelativeOffset + &H88)
        }
    End Function

    Public Function ParseSkeleton(source As HkxVirtualObjectGraph_Class) As HkaSkeletonGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaSkeleton", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HkaSkeletonGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .ParentIndices = ReadInt16Array(source.RelativeOffset + &H18),
            .ParentIndicesField = CreateArrayField(source, &H18, "ParentIndices"),
            .BonesField = CreateArrayField(source, &H28, "Bones"),
            .ReferencePoseField = CreateArrayField(source, &H38, "ReferencePose"),
            .FloatSlotsField = CreateArrayField(source, &H48, "FloatSlots"),
            .LocalFramesField = CreateArrayField(source, &H58, "LocalFrames"),
            .PartitionsField = CreateArrayField(source, &H68, "Partitions"),
            .PartitionNamesField = CreateArrayField(source, &H78, "PartitionNames")
        }

        result.Bones = ReadSkeletonBones(result.BonesField)
        result.ReferencePose = ReadQsTransformArray(result.ReferencePoseField)
        Return result
    End Function

    Private Function ReadSkeletonBones(field As HkxObjectArrayField_Class) As List(Of HkaBoneGraph_Class)
        Dim result As New List(Of HkaBoneGraph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Const boneStride As Integer = 16
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * boneStride)
            result.Add(New HkaBoneGraph_Class With {
                .Index = i,
                .EntryRelativeOffset = entryOffset,
                .Name = ResolveLocalString(entryOffset)
            })
        Next

        Return result
    End Function

    Private Function ReadVector4Array(field As HkxObjectArrayField_Class) As List(Of HkxVector4Graph_Class)
        Dim result As New List(Of HkxVector4Graph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Const vectorStride As Integer = 16
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * vectorStride)
            result.Add(New HkxVector4Graph_Class With {
                .X = ReadSingle(entryOffset + 0),
                .Y = ReadSingle(entryOffset + 4),
                .Z = ReadSingle(entryOffset + 8),
                .W = ReadSingle(entryOffset + 12)
            })
        Next

        Return result
    End Function

    Private Function ReadQsTransformArray(field As HkxObjectArrayField_Class) As List(Of HkxQsTransformGraph_Class)
        Dim result As New List(Of HkxQsTransformGraph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Const transformStride As Integer = 48
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * transformStride)
            result.Add(New HkxQsTransformGraph_Class With {
                .Index = i,
                .EntryRelativeOffset = entryOffset,
                .Translation = New HkxVector4Graph_Class With {
                    .X = ReadSingle(entryOffset + 0),
                    .Y = ReadSingle(entryOffset + 4),
                    .Z = ReadSingle(entryOffset + 8),
                    .W = ReadSingle(entryOffset + 12)
                },
                .Rotation = New HkxQuaternionGraph_Class With {
                    .X = ReadSingle(entryOffset + 16),
                    .Y = ReadSingle(entryOffset + 20),
                    .Z = ReadSingle(entryOffset + 24),
                    .W = ReadSingle(entryOffset + 28)
                },
                .Scale = New HkxVector4Graph_Class With {
                    .X = ReadSingle(entryOffset + 32),
                    .Y = ReadSingle(entryOffset + 36),
                    .Z = ReadSingle(entryOffset + 40),
                    .W = ReadSingle(entryOffset + 44)
                }
            })
        Next

        Return result
    End Function
    Private Function ReadNamedVariant(namedVariantRelativeOffset As Integer) As HkxNamedVariantGraph_Class
        Return New HkxNamedVariantGraph_Class With {
            .RelativeOffset = namedVariantRelativeOffset,
            .Name = ResolveLocalString(namedVariantRelativeOffset),
            .ClassName = ResolveLocalString(namedVariantRelativeOffset + 8),
            .VariantObject = ResolveGlobalObject(namedVariantRelativeOffset + 16)
        }
    End Function

    Private Function CreateArrayField(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer, fieldName As String) As HkxObjectArrayField_Class
        Return New HkxObjectArrayField_Class With {
            .FieldName = fieldName,
            .Header = ReadArrayHeader(source.RelativeOffset + fieldOffset)
        }
    End Function
End Class
Public Class HkxVirtualObjectGraph_Class
    Public Property SectionIndex As Integer
    Public Property RelativeOffset As Integer
    Public Property AbsoluteOffset As Integer
    Public Property ClassNameSectionIndex As Integer
    Public Property ClassNameRelativeOffset As Integer
    Public Property ClassName As String
    Public Property Size As Integer
End Class

Public Class HkxObjectArrayHeader_Class
    Public Property FieldRelativeOffset As Integer
    Public Property DataRelativeOffset As Integer
    Public Property Count As Integer
    Public Property CapacityAndFlags As Integer
End Class

Public Class HkxObjectArrayField_Class
    Public Property FieldName As String
    Public Property Header As HkxObjectArrayHeader_Class
End Class

Public Class HkxRootLevelContainerGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property NamedVariants As New List(Of HkxNamedVariantGraph_Class)
End Class

Public Class HkxNamedVariantGraph_Class
    Public Property RelativeOffset As Integer
    Public Property Name As String
    Public Property ClassName As String
    Public Property VariantObject As HkxVirtualObjectGraph_Class
End Class

Public Class HclClothDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property UnknownValue78 As Integer
    Public Property UnknownValue7C As Integer
    Public Property SimClothDatas As List(Of HkxVirtualObjectGraph_Class)
    Public Property BufferDefinitions As List(Of HkxVirtualObjectGraph_Class)
    Public Property TransformSetDefinitions As List(Of HkxVirtualObjectGraph_Class)
    Public Property Operators As List(Of HkxVirtualObjectGraph_Class)
    Public Property ClothStates As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Fields As New List(Of HkxObjectArrayField_Class)
End Class

Public Class HclSimClothDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property UnknownFloat20 As Single
    Public Property UnknownFloat24 As Single
    Public Property UnknownFloat28 As Single
    Public Property UnknownFloat2C As Single
    Public ReadOnly Property ArrayFields As New List(Of HkxObjectArrayField_Class)
End Class

Public Class HclClothStateGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property ArrayFields As New List(Of HkxObjectArrayField_Class)
    Public ReadOnly Property ObjectReferenceArrays As New List(Of HkxObjectReferenceArrayGraph_Class)
End Class

Public Class HclSimClothPoseGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PoseField As HkxObjectArrayField_Class
    Public Property Pose As List(Of HkxVector4Graph_Class)
End Class

Public Class HkxObjectReferenceArrayGraph_Class
    Public Property FieldName As String
    Public Property Objects As List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HclCollidableGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ShapeObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkaSkeletonGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ParentIndices As List(Of Short)
    Public Property ParentIndicesField As HkxObjectArrayField_Class
    Public Property BonesField As HkxObjectArrayField_Class
    Public Property ReferencePoseField As HkxObjectArrayField_Class
    Public Property FloatSlotsField As HkxObjectArrayField_Class
    Public Property LocalFramesField As HkxObjectArrayField_Class
    Public Property PartitionsField As HkxObjectArrayField_Class
    Public Property PartitionNamesField As HkxObjectArrayField_Class
    Public Property Bones As List(Of HkaBoneGraph_Class)
    Public Property ReferencePose As List(Of HkxQsTransformGraph_Class)
End Class

Public Class HkaBoneGraph_Class
    Public Property Index As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Name As String
End Class



Public Class HkxQsTransformGraph_Class
    Public Property Index As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Translation As HkxVector4Graph_Class
    Public Property Rotation As HkxQuaternionGraph_Class
    Public Property Scale As HkxVector4Graph_Class
End Class

Public Class HkxVector4Graph_Class
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single
    Public Property W As Single
End Class

Public Class HkxQuaternionGraph_Class
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single
    Public Property W As Single
End Class



