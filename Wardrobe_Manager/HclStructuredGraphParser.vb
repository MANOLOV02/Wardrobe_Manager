Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq

Public NotInheritable Class HclStructuredGraphParser_Class
    Public Shared Function ParseSimClothData(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimClothDataDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim collidableObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HA8)
        Dim constraintObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HB8)
        Dim defaultPoseObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HD8)

        Dim result As New HclSimClothDataDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H30),
            .Field38Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38)),
            .Field48UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)),
            .Field58UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H58)),
            .Field68Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H68)),
            .Field88UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H88)),
            .Field98Matrices = ReadMatrix4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H98)),
            .Collidables = collidableObjects,
            .ConstraintSets = constraintObjects,
            .DefaultClothPoses = defaultPoseObjects,
            .FieldF8UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &HF8)),
            .Field108Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H108)),
            .Field118Pairs = ReadUInt32PairArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H118))
        }

        result.ParticleDatas.AddRange(ParseSimParticleData(result.Field38Vectors))
        result.FixedParticleIndices.AddRange(result.Field48UInt16.Select(Function(value) CInt(value)))
        result.Triangles.AddRange(ReadUInt16TriangleArray(result.Field58UInt16))
        result.StaticCollisionMasks.AddRange(result.FieldF8UInt32)
        result.PinchDetectionFlags.AddRange(result.Field108Bytes)
        result.CollidableDetails.AddRange(collidableObjects.Select(Function(obj) ParseCollidable(graph, obj)).Where(Function(detail) Not IsNothing(detail)))
        result.DefaultClothPoseDetails.AddRange(defaultPoseObjects.Select(Function(obj) graph.ParseSimClothPose(obj)).Where(Function(detail) Not IsNothing(detail)))
        result.ConstraintDetails.AddRange(constraintObjects.Select(Function(obj) ParseConstraintObject(graph, obj)).Where(Function(detail) Not IsNothing(detail)))
        Return result
    End Function

    Public Shared Function ParseClothState(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclClothStateDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclClothState", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclClothStateDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Field18UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H18)),
            .Field28Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H28)),
            .Field38Structs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38), 32),
            .Field48Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)),
            .FieldB0Structs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HB0), 72),
            .FieldC0Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HC0)),
            .FieldD8Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HD8)),
            .FieldF0Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HF0)),
            .Field108Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H108)),
            .Field120Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H120)),
            .Field138Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H138))
        }

        result.OperatorIndices.AddRange(result.Field18UInt32.Select(Function(value) CInt(value)))
        result.BufferAccesses.AddRange(ParseStateBufferAccessArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H28)))
        result.AuxiliaryBufferAccesses.AddRange(ParseStateBufferAccessArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)))
        result.TransformAccessContainers.AddRange(ParseStateTransformAccessContainerArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38)))
        For Each container In result.TransformAccessContainers
            result.TransformSetAccesses.AddRange(container.Accesses)
        Next
        Return result
    End Function

    Private Shared Function ParseStateBufferAccessArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclClothStateBufferAccessDetail_Class)
        Dim result As New List(Of HclClothStateBufferAccessDetail_Class)
        For Each raw In ReadRawStructArray(graph, field, 16)
            Dim access = ParseStateBufferAccess(raw)
            If Not IsNothing(access) Then result.Add(access)
        Next
        Return result
    End Function

    Private Shared Function ParseStateBufferAccess(raw As HkxRawStructGraph_Class) As HclClothStateBufferAccessDetail_Class
        If IsNothing(raw) Then Return Nothing

        Dim result As New HclClothStateBufferAccessDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .Word0 = If(raw.UInt32Values.Count > 0, raw.UInt32Values(0), 0UI),
            .Word1 = If(raw.UInt32Values.Count > 1, raw.UInt32Values(1), 0UI),
            .Word2 = If(raw.UInt32Values.Count > 2, raw.UInt32Values(2), 0UI),
            .Word3 = If(raw.UInt32Values.Count > 3, raw.UInt32Values(3), 0UI)
        }

        result.BufferIndex = CInt(result.Word0)
        result.AccessCode = CInt(result.Word1)
        result.AccessCodeLowByte = result.AccessCode And &HFF
        result.AccessCodeHighByte = (result.AccessCode >> 8) And &HFF
        Return result
    End Function

    Private Shared Function ParseStateTransformAccessContainerArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclClothStateTransformAccessContainerDetail_Class)
        Dim result As New List(Of HclClothStateTransformAccessContainerDetail_Class)
        For Each raw In ReadRawStructArray(graph, field, 32)
            Dim container = ParseStateTransformAccessContainer(graph, raw)
            If Not IsNothing(container) Then result.Add(container)
        Next
        Return result
    End Function

    Private Shared Function ParseStateTransformAccessContainer(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class) As HclClothStateTransformAccessContainerDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing

        Dim nestedHeader = graph.ReadArrayHeader(raw.EntryRelativeOffset + &H10)
        Dim result As New HclClothStateTransformAccessContainerDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .NestedAccessHeader = nestedHeader
        }
        result.HeaderUInt32.AddRange(raw.UInt32Values.Take(4))

        For Each nestedRaw In ReadRawStructArray(graph, nestedHeader, 72)
            Dim access = ParseStateTransformSetAccess(graph, nestedRaw)
            If Not IsNothing(access) Then result.Accesses.Add(access)
        Next

        Return result
    End Function

    Private Shared Function ParseStateTransformSetAccess(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class) As HclClothStateTransformSetAccessDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing

        Dim result As New HclClothStateTransformSetAccessDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw
        }

        For subIndex = 0 To 2
            Dim componentAccess = ParseStateTransformComponentAccess(graph, raw, subIndex)
            If Not IsNothing(componentAccess) Then result.ComponentAccesses.Add(componentAccess)
        Next

        result.HasAnyMaskData = result.ComponentAccesses.Any(Function(access) access.MaskBytes.Any(Function(value) value <> 0))
        Return result
    End Function

    Private Shared Function ParseStateTransformComponentAccess(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class, subIndex As Integer) As HclClothStateTransformComponentAccessDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing
        If subIndex < 0 OrElse subIndex > 2 Then Return Nothing

        Dim wordBase = subIndex * 6
        Dim headerOffset = raw.EntryRelativeOffset + (subIndex * 24)
        Dim header = graph.ReadArrayHeader(headerOffset)

        Dim result As New HclClothStateTransformComponentAccessDetail_Class With {
            .SubIndex = subIndex,
            .HeaderRelativeOffset = headerOffset,
            .ArrayHeader = header,
            .MaskBytes = ReadByteArray(graph, header),
            .MaskCount = header.Count,
            .CapacityAndFlags = header.CapacityAndFlags,
            .TransformCount = If(raw.UInt32Values.Count > wordBase + 4, CInt(raw.UInt32Values(wordBase + 4)), 0),
            .ReservedValue = If(raw.UInt32Values.Count > wordBase + 5, raw.UInt32Values(wordBase + 5), 0UI)
        }

        result.MaskIndices.AddRange(DecodeMaskIndices(result.MaskBytes))
        Return result
    End Function

    Public Shared Function ParseBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = ReadPayloadBytes(graph, source, &H20),
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Public Shared Function ParseScratchBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclScratchBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclScratchBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclScratchBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = ReadPayloadBytes(graph, source, &H20),
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Public Shared Function ParseMoveParticlesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclMoveParticlesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclMoveParticlesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclMoveParticlesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .Pairs = ReadVertexParticlePairs(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20))
        }
    End Function

    Public Shared Function ParseSimulateOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimulateOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimulateOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclSimulateOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 6),
            .Configs = ReadUInt32ConfigArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H30))
        }
    End Function

    Public Shared Function ParseCopyVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCopyVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCopyVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclCopyVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = payloadBytes,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes)
        }
    End Function

    Public Shared Function ParseGatherAllVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclGatherAllVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclGatherAllVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclGatherAllVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = payloadBytes,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .GatheredVertexIndices = DecodePackedUInt16List(payloadUInt32.Skip(12), If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0)),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes)
        }
    End Function

    Private Shared Function DecodePackedUInt16List(words As IEnumerable(Of UInteger), takeCount As Integer) As List(Of UShort)
        Dim result As New List(Of UShort)
        If IsNothing(words) OrElse takeCount <= 0 Then Return result

        For Each word In words
            If result.Count < takeCount Then result.Add(CUShort(word And &HFFFFUI))
            If result.Count < takeCount Then result.Add(CUShort((word >> 16) And &HFFFFUI))
            If result.Count >= takeCount Then Exit For
        Next

        Return result
    End Function

    Public Shared Function ParseCapsuleShape(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCapsuleShapeDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCapsuleShape", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim vectorCount = Math.Max(0, (source.Size - &H10) \ 16)
        Return New HclCapsuleShapeDetail_Class With {
            .SourceObject = source,
            .Vectors = ReadVector4Block(graph, source.RelativeOffset + &H10, vectorCount)
        }
    End Function

    Public Shared Function ParseCollidable(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCollidableDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCollidable", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclCollidableDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .ShapeObject = graph.ResolveGlobalObject(source.RelativeOffset + &H88),
            .ShapeDetail = ParseCapsuleShape(graph, graph.ResolveGlobalObject(source.RelativeOffset + &H88)),
            .PayloadRelativeOffset = source.RelativeOffset + &H18,
            .PayloadBytes = ReadPayloadBytes(graph, source, &H18),
            .PayloadUInt32 = ReadPayloadUInt32(graph, source, &H18)
        }
    End Function

    Public Shared Function ParseStandardLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStandardLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStandardLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 12)
        Dim result As New HclStandardLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseStretchLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStretchLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStretchLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 12)
        Dim result As New HclStretchLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseBendStiffnessConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBendStiffnessConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBendStiffnessConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 32)
        Dim result As New HclBendStiffnessConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseBendConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseLocalRangeConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclLocalRangeConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclLocalRangeConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawConstraints = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 16)
        Dim result As New HclLocalRangeConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Constraints = rawConstraints
        }
        result.ConstraintDetails.AddRange(ParseLocalRangeConstraints(rawConstraints))
        Return result
    End Function
    Private Shared Function ParseSimParticleData(values As IEnumerable(Of HkxVector4Graph_Class)) As List(Of HclSimParticleDataGraph_Class)
        Dim result As New List(Of HclSimParticleDataGraph_Class)
        If IsNothing(values) Then Return result

        Dim entryIndex = 0
        For Each value In values
            If IsNothing(value) Then Continue For
            result.Add(New HclSimParticleDataGraph_Class With {
                .EntryIndex = entryIndex,
                .Mass = value.X,
                .InverseMass = value.Y,
                .Radius = value.Z,
                .Friction = value.W
            })
            entryIndex += 1
        Next

        Return result
    End Function

    Private Shared Function ParseDistanceConstraints(rawLinks As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclDistanceConstraintGraph_Class)
        Dim result As New List(Of HclDistanceConstraintGraph_Class)
        If IsNothing(rawLinks) Then Return result

        For Each raw In rawLinks
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 12 Then Continue For
            result.Add(New HclDistanceConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .ParticleA = BitConverter.ToUInt16(raw.RawBytes, 0),
                .ParticleB = BitConverter.ToUInt16(raw.RawBytes, 2),
                .RestLength = BitConverter.ToSingle(raw.RawBytes, 4),
                .Stiffness = BitConverter.ToSingle(raw.RawBytes, 8)
            })
        Next

        Return result
    End Function

    Private Shared Function ParseBendConstraints(rawLinks As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclBendConstraintGraph_Class)
        Dim result As New List(Of HclBendConstraintGraph_Class)
        If IsNothing(rawLinks) Then Return result

        For Each raw In rawLinks
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 32 Then Continue For
            result.Add(New HclBendConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .WeightA = BitConverter.ToSingle(raw.RawBytes, 0),
                .WeightB = BitConverter.ToSingle(raw.RawBytes, 4),
                .WeightC = BitConverter.ToSingle(raw.RawBytes, 8),
                .WeightD = BitConverter.ToSingle(raw.RawBytes, 12),
                .BendStiffness = BitConverter.ToSingle(raw.RawBytes, 16),
                .RestCurvature = BitConverter.ToSingle(raw.RawBytes, 20),
                .ParticleA = BitConverter.ToUInt16(raw.RawBytes, 24),
                .ParticleB = BitConverter.ToUInt16(raw.RawBytes, 26),
                .ParticleC = BitConverter.ToUInt16(raw.RawBytes, 28),
                .ParticleD = BitConverter.ToUInt16(raw.RawBytes, 30)
            })
        Next

        Return result
    End Function

    Private Shared Function ParseLocalRangeConstraints(rawConstraints As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclLocalRangeConstraintGraph_Class)
        Dim result As New List(Of HclLocalRangeConstraintGraph_Class)
        If IsNothing(rawConstraints) Then Return result

        For Each raw In rawConstraints
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 16 Then Continue For
            result.Add(New HclLocalRangeConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .ParticleIndex = BitConverter.ToUInt16(raw.RawBytes, 0),
                .ReferenceVertexIndex = BitConverter.ToUInt16(raw.RawBytes, 2),
                .MaximumDistance = BitConverter.ToSingle(raw.RawBytes, 4),
                .MaximumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 8),
                .MinimumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 12)
            })
        Next

        Return result
    End Function

    Public Shared Function ParseConstraintObject(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Object
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing

        Select Case source.ClassName.ToLowerInvariant()
            Case "hclstandardlinkconstraintset"
                Return ParseStandardLinkConstraintSet(graph, source)
            Case "hclstretchlinkconstraintset"
                Return ParseStretchLinkConstraintSet(graph, source)
            Case "hclbendstiffnessconstraintset"
                Return ParseBendStiffnessConstraintSet(graph, source)
            Case "hcllocalrangeconstraintset"
                Return ParseLocalRangeConstraintSet(graph, source)
            Case Else
                Return source
        End Select
    End Function

    Private Shared Function ReadVertexParticlePairs(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclMoveParticlesVertexParticlePairGraph_Class)
        Dim result As New List(Of HclMoveParticlesVertexParticlePairGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 4)
            result.Add(New HclMoveParticlesVertexParticlePairGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .VertexIndex = ReadUInt16(graph, entryOffset),
                .ParticleIndex = ReadUInt16(graph, entryOffset + 2)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadUInt32ConfigArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclSimulateOperatorConfigGraph_Class)
        Dim result As New List(Of HclSimulateOperatorConfigGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 4)
            result.Add(New HclSimulateOperatorConfigGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .Value = ReadUInt32(graph, entryOffset)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadUInt16TriangleArray(values As IEnumerable(Of UShort)) As List(Of HkxUInt16TriangleGraph_Class)
        Dim result As New List(Of HkxUInt16TriangleGraph_Class)
        If IsNothing(values) Then Return result

        Dim items = values.ToList()
        For i = 0 To (items.Count \ 3) - 1
            Dim baseIndex = i * 3
            result.Add(New HkxUInt16TriangleGraph_Class With {
                .TriangleIndex = i,
                .Value0 = items(baseIndex),
                .Value1 = items(baseIndex + 1),
                .Value2 = items(baseIndex + 2)
            })
        Next

        Return result
    End Function

    Private Shared Function ExtractPrintableAscii(bytes As Byte()) As String
        If IsNothing(bytes) OrElse bytes.Length = 0 Then Return String.Empty

        Dim chars = bytes.
            SkipWhile(Function(b) b = 0).
            Select(Function(b) If(b >= 32 AndAlso b <= 126, ChrW(b), ControlChars.NullChar)).
            ToArray()

        Dim text = New String(chars)
        Dim parts = text.Split(ControlChars.NullChar).Where(Function(part) part.Length >= 4).ToList()
        If parts.Count = 0 Then Return String.Empty
        Return parts(parts.Count - 1)
    End Function

    Private Shared Function ReadUInt32Block(graph As HkxObjectGraph_Class, relativeOffset As Integer, count As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If count <= 0 Then Return result

        For i = 0 To count - 1
            result.Add(ReadUInt32(graph, relativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadUInt32PairArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxUInt32PairGraph_Class)
        Dim result As New List(Of HkxUInt32PairGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 8)
            result.Add(New HkxUInt32PairGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .FirstValue = ReadUInt32(graph, entryOffset),
                .SecondValue = ReadUInt32(graph, entryOffset + 4)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadRawStructArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class, structSize As Integer) As List(Of HkxRawStructGraph_Class)
        Dim result As New List(Of HkxRawStructGraph_Class)
        If IsNothing(field) OrElse structSize <= 0 OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * structSize)
            result.Add(CreateRawStruct(graph, i, entryOffset, structSize))
        Next

        Return result
    End Function

    Private Shared Function CreateRawStruct(graph As HkxObjectGraph_Class, entryIndex As Integer, entryRelativeOffset As Integer, byteCount As Integer) As HkxRawStructGraph_Class
        Dim bytes = ReadBytes(graph, entryRelativeOffset, byteCount)
        Dim result As New HkxRawStructGraph_Class With {
            .EntryIndex = entryIndex,
            .EntryRelativeOffset = entryRelativeOffset,
            .RawBytes = bytes
        }

        For i = 0 To (bytes.Length \ 2) - 1
            result.UInt16Values.Add(BitConverter.ToUInt16(bytes, i * 2))
        Next

        For i = 0 To (bytes.Length \ 4) - 1
            result.UInt32Values.Add(BitConverter.ToUInt32(bytes, i * 4))
            result.SingleValues.Add(BitConverter.ToSingle(bytes, i * 4))
        Next

        Return result
    End Function

    Private Shared Function ReadVector4Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxVector4Graph_Class)
        If IsNothing(field) Then Return New List(Of HkxVector4Graph_Class)
        Return ReadVector4Block(graph, field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function ReadVector4Block(graph As HkxObjectGraph_Class, dataRelativeOffset As Integer, count As Integer) As List(Of HkxVector4Graph_Class)
        Dim result As New List(Of HkxVector4Graph_Class)
        If count <= 0 OrElse dataRelativeOffset < 0 Then Return result

        For i = 0 To count - 1
            Dim entryOffset = dataRelativeOffset + (i * 16)
            result.Add(New HkxVector4Graph_Class With {
                .X = graph.ReadSingle(entryOffset + 0),
                .Y = graph.ReadSingle(entryOffset + 4),
                .Z = graph.ReadSingle(entryOffset + 8),
                .W = graph.ReadSingle(entryOffset + 12)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadMatrix4Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxMatrix4Graph_Class)
        Dim result As New List(Of HkxMatrix4Graph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim matrixOffset = field.DataRelativeOffset + (i * 64)
            Dim values(15) As Single
            For j = 0 To 15
                values(j) = graph.ReadSingle(matrixOffset + (j * 4))
            Next
            result.Add(New HkxMatrix4Graph_Class With {
                .RelativeOffset = matrixOffset,
                .Values = values
            })
        Next

        Return result
    End Function

    Private Shared Function ReadUInt16Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of UShort)
        Dim result As New List(Of UShort)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            result.Add(ReadUInt16(graph, field.DataRelativeOffset + (i * 2)))
        Next

        Return result
    End Function

    Private Shared Function ReadUInt32Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            result.Add(ReadUInt32(graph, field.DataRelativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadByteArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As Byte()
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(graph, field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function DecodeMaskIndices(mask As Byte()) As List(Of Integer)
        Dim result As New List(Of Integer)
        If IsNothing(mask) Then Return result

        For byteIndex = 0 To mask.Length - 1
            Dim value = mask(byteIndex)
            For bit = 0 To 7
                If (value And CByte(1 << bit)) <> 0 Then
                    result.Add((byteIndex * 8) + bit)
                End If
            Next
        Next

        Return result
    End Function

    Private Shared Function ReadPayloadBytes(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class, payloadOffset As Integer) As Byte()
        Dim byteCount = Math.Max(0, source.Size - payloadOffset)
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(graph, source.RelativeOffset + payloadOffset, byteCount)
    End Function

    Private Shared Function ReadPayloadUInt32(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class, payloadOffset As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        Dim bytes = ReadPayloadBytes(graph, source, payloadOffset)
        For i = 0 To (bytes.Length \ 4) - 1
            result.Add(BitConverter.ToUInt32(bytes, i * 4))
        Next
        Return result
    End Function

    Private Shared Function ReadBytes(graph As HkxObjectGraph_Class, relativeOffset As Integer, byteCount As Integer) As Byte()
        If IsNothing(graph) OrElse byteCount <= 0 Then Return Array.Empty(Of Byte)()
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        Dim result(byteCount - 1) As Byte
        Array.Copy(graph.Packfile.RawBytes, absoluteOffset, result, 0, byteCount)
        Return result
    End Function

    Private Shared Function ReadUInt16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UShort
        Return BitConverter.ToUInt16(graph.Packfile.RawBytes, graph.ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Private Shared Function ReadUInt32(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UInteger
        Return BitConverter.ToUInt32(graph.Packfile.RawBytes, graph.ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function
End Class

Public Class HclSimClothDataDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field38Vectors As List(Of HkxVector4Graph_Class)
    Public ReadOnly Property ParticleDatas As New List(Of HclSimParticleDataGraph_Class)
    Public Property Field48UInt16 As List(Of UShort)
    Public ReadOnly Property FixedParticleIndices As New List(Of Integer)
    Public Property Field48MatchesMoveParticles As Boolean
    Public ReadOnly Property ResolvedMoveParticlePairs As New List(Of HclMoveParticlesVertexParticlePairGraph_Class)
    Public Property Field58UInt16 As List(Of UShort)
    Public ReadOnly Property Triangles As New List(Of HkxUInt16TriangleGraph_Class)
    Public Property Field68Bytes As Byte()
    Public Property Field88UInt32 As List(Of UInteger)
    Public Property Field98Matrices As List(Of HkxMatrix4Graph_Class)
    Public Property Collidables As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property CollidableDetails As New List(Of HclCollidableDetail_Class)
    Public ReadOnly Property CollidableBindings As New List(Of HclSimCollidableBinding_Class)
    Public Property ConstraintSets As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ConstraintDetails As New List(Of Object)
    Public Property DefaultClothPoses As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property DefaultClothPoseDetails As New List(Of HclSimClothPoseGraph_Class)
    Public Property FieldF8UInt32 As List(Of UInteger)
    Public ReadOnly Property StaticCollisionMasks As New List(Of UInteger)
    Public Property Field108Bytes As Byte()
    Public ReadOnly Property PinchDetectionFlags As New List(Of Byte)
    Public Property Field118Pairs As List(Of HkxUInt32PairGraph_Class)
End Class

Public Class HclSimCollidableBinding_Class
    Public Property EntryIndex As Integer
    Public Property BoneIndex As Integer
    Public Property BoneName As String
    Public Property TransformSetIndex As UInteger
    Public Property ParameterRaw As UInteger
    Public Property ParameterSingle As Single
    Public Property Collidable As HclCollidableDetail_Class
    Public Property Matrix As HkxMatrix4Graph_Class
End Class

Public Class HclClothStateDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field18UInt32 As List(Of UInteger)
    Public ReadOnly Property OperatorIndices As New List(Of Integer)
    Public ReadOnly Property ResolvedOperators As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ResolvedOperatorNames As New List(Of String)
    Public Property Field28Vectors As List(Of HkxVector4Graph_Class)
    Public Property Field38Structs As List(Of HkxRawStructGraph_Class)
    Public Property Field48Vectors As List(Of HkxVector4Graph_Class)
    Public Property FieldB0Structs As List(Of HkxRawStructGraph_Class)
    Public Property FieldC0Bytes As Byte()
    Public Property FieldD8Bytes As Byte()
    Public Property FieldF0Bytes As Byte()
    Public Property Field108Bytes As Byte()
    Public Property Field120Bytes As Byte()
    Public Property Field138Bytes As Byte()
    Public ReadOnly Property BufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property AuxiliaryBufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property TransformAccessContainers As New List(Of HclClothStateTransformAccessContainerDetail_Class)
    Public ReadOnly Property TransformSetAccesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateBufferAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property Word0 As UInteger
    Public Property Word1 As UInteger
    Public Property Word2 As UInteger
    Public Property Word3 As UInteger
    Public Property BufferIndex As Integer
    Public Property AccessCode As Integer
    Public Property AccessCodeLowByte As Integer
    Public Property AccessCodeHighByte As Integer
    Public Property ResolvedBufferName As String
End Class

Public Class HclClothStateTransformAccessContainerDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public ReadOnly Property HeaderUInt32 As New List(Of UInteger)
    Public Property NestedAccessHeader As HkxObjectArrayHeader_Class
    Public ReadOnly Property Accesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateTransformSetAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public ReadOnly Property ComponentAccesses As New List(Of HclClothStateTransformComponentAccessDetail_Class)
    Public Property HasAnyMaskData As Boolean
End Class

Public Class HclClothStateTransformComponentAccessDetail_Class
    Public Property SubIndex As Integer
    Public Property HeaderRelativeOffset As Integer
    Public Property ArrayHeader As HkxObjectArrayHeader_Class
    Public Property MaskBytes As Byte()
    Public Property MaskCount As Integer
    Public Property CapacityAndFlags As Integer
    Public Property TransformCount As Integer
    Public ReadOnly Property MatchingSkinPaletteIndices As New List(Of Integer)
    Public ReadOnly Property MatchingSkinBoneNames As New List(Of String)
    Public Property ReservedValue As UInteger
    Public ReadOnly Property MaskIndices As New List(Of Integer)
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)
End Class

Public Class HclBufferDefinitionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclScratchBufferDefinitionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclMoveParticlesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property Pairs As List(Of HclMoveParticlesVertexParticlePairGraph_Class)
End Class

Public Class HclMoveParticlesVertexParticlePairGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property VertexIndex As UShort
    Public Property ParticleIndex As UShort
End Class

Public Class HclSimulateOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property Configs As List(Of HclSimulateOperatorConfigGraph_Class)
End Class

Public Class HclSimulateOperatorConfigGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Value As UInteger
    Public Property ResolvedConstraintName As String
    Public Property ResolvedConstraintType As String
End Class

Public Class HclCopyVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
End Class

Public Class HclGatherAllVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
    Public Property GatheredVertexIndices As New List(Of UShort)
End Class

Public Class HclCollidableDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ShapeObject As HkxVirtualObjectGraph_Class
    Public Property ShapeDetail As HclCapsuleShapeDetail_Class
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
End Class

Public Class HclCapsuleShapeDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Vectors As List(Of HkxVector4Graph_Class)
End Class

Public Class HclStandardLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Public Class HclStretchLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Public Class HclBendStiffnessConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclBendConstraintGraph_Class)
End Class

Public Class HclLocalRangeConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Constraints As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property ConstraintDetails As New List(Of HclLocalRangeConstraintGraph_Class)
End Class

Public Class HclSimParticleDataGraph_Class
    Public Property EntryIndex As Integer
    Public Property Mass As Single
    Public Property InverseMass As Single
    Public Property Radius As Single
    Public Property Friction As Single
End Class

Public Class HclDistanceConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property ParticleA As UShort
    Public Property ParticleB As UShort
    Public Property RestLength As Single
    Public Property Stiffness As Single
End Class

Public Class HclBendConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property WeightA As Single
    Public Property WeightB As Single
    Public Property WeightC As Single
    Public Property WeightD As Single
    Public Property ParticleA As UShort
    Public Property ParticleB As UShort
    Public Property ParticleC As UShort
    Public Property ParticleD As UShort
    Public Property BendStiffness As Single
    Public Property RestCurvature As Single
End Class

Public Class HclLocalRangeConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property ParticleIndex As UShort
    Public Property ReferenceVertexIndex As UShort
    Public Property MaximumDistance As Single
    Public Property MaximumNormalDistance As Single
    Public Property MinimumNormalDistance As Single
End Class

Public Class HkxUInt16TriangleGraph_Class
    Public Property TriangleIndex As Integer
    Public Property Value0 As UShort
    Public Property Value1 As UShort
    Public Property Value2 As UShort
End Class

Public Class HkxUInt32PairGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property FirstValue As UInteger
    Public Property SecondValue As UInteger
End Class

Public Class HkxRawStructGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawBytes As Byte()
    Public ReadOnly Property UInt16Values As New List(Of UShort)
    Public ReadOnly Property UInt32Values As New List(Of UInteger)
    Public ReadOnly Property SingleValues As New List(Of Single)
End Class














