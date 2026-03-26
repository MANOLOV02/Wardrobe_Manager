Option Strict On
Option Explicit On

Imports System.IO
Imports System.Linq
Imports System.Text
Imports NiflySharp
Imports NiflySharp.Blocks

Module Program
    Private Const Separator As String = "════════════════════════════════════════════════════════════════"

    ' Track all class structures encountered across all NIF files (for deduplication)
    Private ReadOnly _seenClassStructures As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Sub Main(args As String())
        Dim samplesDir = If(args.Length > 0,
                            args(0),
                            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Physics Samples")))

        If Not Directory.Exists(samplesDir) Then
            Console.Error.WriteLine($"Samples directory not found: {samplesDir}")
            Console.Error.WriteLine("Usage: HkxParserTool [path-to-folder-with-nifs]")
            Environment.ExitCode = 1
            Return
        End If

        Dim nifFiles = Directory.GetFiles(samplesDir, "*.nif").OrderBy(Function(p) p).ToArray()
        If nifFiles.Length = 0 Then
            Console.Error.WriteLine($"No .nif files found in: {samplesDir}")
            Environment.ExitCode = 1
            Return
        End If

        Console.OutputEncoding = Encoding.UTF8
        Console.WriteLine($"HKX Parser Debug Tool  —  {nifFiles.Length} NIF file(s)")
        Console.WriteLine($"Dir: {samplesDir}")
        Console.WriteLine()

        For Each nifPath In nifFiles
            Console.WriteLine(Separator)
            Console.WriteLine($"  NIF: {Path.GetFileName(nifPath)}")
            Console.WriteLine(Separator)
            Console.WriteLine()
            Try
                ProcessNif(nifPath)
            Catch ex As Exception
                WriteError($"FATAL: {ex.GetType().Name}: {ex.Message}")
                Console.WriteLine(ex.StackTrace)
            End Try
            Console.WriteLine()
        Next

        Console.WriteLine("Done.")
        If Not Console.IsInputRedirected Then
            Console.WriteLine("Press any key to exit...")
            Console.ReadKey(True)
        End If
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Main per-NIF entry
    '──────────────────────────────────────────────────────────────────
    Private Sub ProcessNif(nifPath As String)
        Dim nif As New NifFile()
        nif.Load(nifPath)

        Dim clothBlocks = nif.Blocks.OfType(Of BSClothExtraData)().ToList()
        If clothBlocks.Count = 0 Then
            Console.WriteLine("  No BSClothExtraData blocks found.")
            Return
        End If

        Console.WriteLine($"  BSClothExtraData blocks: {clothBlocks.Count}")
        Console.WriteLine()

        For blockIndex = 0 To clothBlocks.Count - 1
            Dim cloth = clothBlocks(blockIndex)
            Dim dataSize = 0
            Try : dataSize = cloth.BinaryData.Data.Count : Catch : End Try
            Console.WriteLine($"  ── Block #{blockIndex}  ({dataSize} bytes) ──────────────────────────────")
            Console.WriteLine()
            If dataSize = 0 Then Console.WriteLine("    (empty payload)") : Continue For

            Try
                Dim packfile = HkxPackfileParser_Class.Parse(cloth)
                Dim graph    = HkxObjectGraphParser_Class.BuildGraph(packfile)

                DumpHeader(packfile)
                DumpSections(packfile)
                DumpClassNames(packfile)
                DumpFixupSummary(packfile)
                DumpObjectIndex(graph)
                DumpRootContainer(graph)
                DumpSkeleton(graph, packfile)
                DumpAllClothConfigs(graph)

                ' Structural analysis: one per unique class across all NIFs
                DumpStructuralAnalysis(graph, packfile)

            Catch ex As Exception
                WriteError($"PARSE ERROR: {ex.GetType().Name}: {ex.Message}")
                Console.WriteLine(ex.StackTrace)
            End Try
        Next
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Header / sections / classnames / fixup summary
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpHeader(packfile As HkxPackfile_Class)
        Dim h = packfile.Header
        Console.WriteLine($"  ┌─ HEADER")
        Console.WriteLine($"  │ Version:     {h.ContentsVersion}  FileVer={h.FileVersion}")
        Console.WriteLine($"  │ PointerSize: {h.PointerSize}  Endian={h.Endianness}  EBCO={h.EmptyBaseClassOptimization}  ReusePad={h.ReusePaddingOptimization}")
        Console.WriteLine($"  │ Sections:    {h.SectionCount}  ContentsSect={h.ContentsSectionIndex}  ContentOffset=0x{h.ContentsSectionOffset:X}")
        Console.WriteLine($"  │ ClassNameSect={h.ContentsClassNameSectionIndex}  ClassNameOffset=0x{h.ContentsClassNameSectionOffset:X}")
        Console.WriteLine($"  │ Flags:0x{h.Flags:X}  MaxPredicate={h.MaxPredicate}  SectHdrRelOff=0x{h.SectionHeaderRelativeOffset:X}")
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    Private Sub DumpSections(packfile As HkxPackfile_Class)
        Console.WriteLine($"  ┌─ SECTIONS ({packfile.Sections.Count})")
        For Each s In packfile.Sections
            Console.WriteLine($"  │ [{s.Index}] ""{s.Name}""  data=0x{s.AbsoluteDataStart:X}–0x{s.DataEndAbsolute:X}  ({s.DataEndAbsolute - s.AbsoluteDataStart} bytes)")
            Console.WriteLine($"  │        Local=0x{s.LocalFixupsAbsoluteStart:X}–0x{s.LocalFixupsAbsoluteEnd:X}  Global=0x{s.GlobalFixupsAbsoluteStart:X}–0x{s.GlobalFixupsAbsoluteEnd:X}  Virt=0x{s.VirtualFixupsAbsoluteStart:X}–0x{s.VirtualFixupsAbsoluteEnd:X}")
        Next
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    Private Sub DumpClassNames(packfile As HkxPackfile_Class)
        Console.WriteLine($"  ┌─ CLASS NAMES ({packfile.ClassNames.Count})")
        For Each cn In packfile.ClassNames
            Console.WriteLine($"  │ 0x{cn.Signature:X8}  @entry=0x{cn.EntryRelativeOffset:X}  ""{cn.Name}""")
        Next
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    Private Sub DumpFixupSummary(packfile As HkxPackfile_Class)
        Console.WriteLine($"  ┌─ FIXUPS  Local={packfile.LocalFixups.Count}  Global={packfile.GlobalFixups.Count}  Virtual={packfile.VirtualFixups.Count}")
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Object index
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpObjectIndex(graph As HkxObjectGraph_Class)
        Console.WriteLine($"  ┌─ OBJECT INDEX ({graph.Objects.Count} objects)")
        For Each obj In graph.Objects.OrderBy(Function(o) o.RelativeOffset)
            Console.WriteLine($"  │ @0x{obj.RelativeOffset:X6}  size={obj.Size,6}  ""{obj.ClassName}""")
        Next
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Root container
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpRootContainer(graph As HkxObjectGraph_Class)
        Dim root = graph.ParseRootLevelContainer()
        If root Is Nothing Then
            Console.WriteLine("  (No hkRootLevelContainer found)")
            Console.WriteLine()
            Return
        End If
        Console.WriteLine($"  ┌─ hkRootLevelContainer ({root.NamedVariants.Count} variants)")
        For Each nv In root.NamedVariants
            Dim objInfo = If(nv.VariantObject IsNot Nothing, $"@0x{nv.VariantObject.RelativeOffset:X}  ""{nv.VariantObject.ClassName}""", "(null)")
            Console.WriteLine($"  │ Name=""{nv.Name}""  Class=""{nv.ClassName}""  → {objInfo}")
        Next
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' hkaSkeleton — complete with hierarchy and pose validation
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpSkeleton(graph As HkxObjectGraph_Class, packfile As HkxPackfile_Class)
        For Each skelObj In graph.GetObjectsByClassName("hkaSkeleton")
            Dim sk = graph.ParseSkeleton(skelObj)
            If sk Is Nothing Then
                WriteError($"hkaSkeleton @0x{skelObj.RelativeOffset:X}: PARSE FAILED")
                Continue For
            End If

            Console.WriteLine($"  ┌─ hkaSkeleton @0x{skelObj.RelativeOffset:X}  size={skelObj.Size}  name=""{sk.Name}""")
            Console.WriteLine($"  │ Bones={If(sk.Bones?.Count, 0)}  ParentIndices={If(sk.ParentIndices?.Count, 0)}  ReferencePose={If(sk.ReferencePose?.Count, 0)}")

            ' Count consistency checks
            If sk.Bones IsNot Nothing AndAlso sk.ParentIndices IsNot Nothing AndAlso sk.Bones.Count <> sk.ParentIndices.Count Then
                WriteWarn($"  │ COUNT MISMATCH: Bones={sk.Bones.Count}  ParentIndices={sk.ParentIndices.Count}")
            End If
            If sk.Bones IsNot Nothing AndAlso sk.ReferencePose IsNot Nothing AndAlso sk.Bones.Count <> sk.ReferencePose.Count Then
                WriteWarn($"  │ COUNT MISMATCH: Bones={sk.Bones.Count}  ReferencePose={sk.ReferencePose.Count}")
            End If

            ' Array fields (offsets / counts)
            Console.WriteLine($"  │ Fields: ParentIndices@0x{sk.ParentIndicesField?.Header?.FieldRelativeOffset:X}  data@0x{sk.ParentIndicesField?.Header?.DataRelativeOffset:X}")
            Console.WriteLine($"  │         Bones@0x{sk.BonesField?.Header?.FieldRelativeOffset:X}  data@0x{sk.BonesField?.Header?.DataRelativeOffset:X}")
            Console.WriteLine($"  │         ReferencePose@0x{sk.ReferencePoseField?.Header?.FieldRelativeOffset:X}  data@0x{sk.ReferencePoseField?.Header?.DataRelativeOffset:X}")

            ' Per-bone detail
            If sk.Bones IsNot Nothing Then
                Dim poseErrors = 0
                For i = 0 To sk.Bones.Count - 1
                    Dim bone = sk.Bones(i)
                    Dim parentIdx = If(sk.ParentIndices IsNot Nothing AndAlso i < sk.ParentIndices.Count, sk.ParentIndices(i), CShort(-99))
                    Dim parentName = If(parentIdx = -1, "(root)", If(parentIdx >= 0 AndAlso sk.Bones IsNot Nothing AndAlso parentIdx < sk.Bones.Count, sk.Bones(parentIdx).Name, $"INVALID[{parentIdx}]"))

                    Dim poseStr = ""
                    If sk.ReferencePose IsNot Nothing AndAlso i < sk.ReferencePose.Count Then
                        Dim p = sk.ReferencePose(i)
                        poseStr = $"  T=({p.Translation.X:F3},{p.Translation.Y:F3},{p.Translation.Z:F3})"
                        poseStr &= $"  R=({p.Rotation.X:F4},{p.Rotation.Y:F4},{p.Rotation.Z:F4},{p.Rotation.W:F4})"
                        poseStr &= $"  S=({p.Scale.X:F3},{p.Scale.Y:F3},{p.Scale.Z:F3})"

                        Dim rl2 = p.Rotation.X * p.Rotation.X + p.Rotation.Y * p.Rotation.Y +
                                  p.Rotation.Z * p.Rotation.Z + p.Rotation.W * p.Rotation.W
                        If Math.Abs(rl2 - 1.0F) > 0.01F Then poseStr &= $"  ⚠ROT_LEN={rl2:F4}" : poseErrors += 1
                        If Not Single.IsFinite(p.Translation.X) OrElse Not Single.IsFinite(p.Translation.Y) OrElse Not Single.IsFinite(p.Translation.Z) Then
                            poseStr &= "  ⚠NaN/Inf" : poseErrors += 1
                        End If
                        If p.Scale.X <= 0.0F OrElse p.Scale.Y <= 0.0F OrElse p.Scale.Z <= 0.0F Then
                            poseStr &= "  ⚠ZERO_SCALE" : poseErrors += 1
                        End If
                    End If

                    Console.WriteLine($"  │ [{i,3}] ""{bone.Name}""  parent=[{parentIdx,3}] {parentName}{poseStr}")
                Next
                If poseErrors > 0 Then WriteWarn($"  │ {poseErrors} pose errors detected!")
            End If
            Console.WriteLine($"  └─")
            Console.WriteLine()
        Next
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' All hclClothData configs
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpAllClothConfigs(graph As HkxObjectGraph_Class)
        For Each clothObj In graph.GetObjectsByClassName("hclClothData")
            Dim cd = graph.ParseClothData(clothObj)
            If cd Is Nothing Then Continue For

            Console.WriteLine($"  ┌─ hclClothData @0x{clothObj.RelativeOffset:X}  name=""{cd.Name}""")
            Console.WriteLine($"  │ SimClothDatas={cd.SimClothDatas.Count}  BufferDefs={cd.BufferDefinitions.Count}  TransformSets={cd.TransformSetDefinitions.Count}")
            Console.WriteLine($"  │ Operators={cd.Operators.Count}  States={cd.ClothStates.Count}  Collidables={cd.Collidables.Count}")

            If cd.Operators.Count > 0 Then
                Console.WriteLine($"  │ Operators:")
                For Each op In cd.Operators
                    DumpOperatorDetails(graph, op)
                Next
            End If

            If cd.Collidables.Count > 0 Then
                Console.WriteLine($"  │ Collidables:")
                For Each colObj In cd.Collidables
                    DumpCollidable(graph, colObj)
                Next
            End If

            For Each simObj In cd.SimClothDatas
                DumpSimClothData(graph, simObj)
            Next

            For Each bufObj In cd.BufferDefinitions
                DumpBufferDefinition(graph, bufObj)
            Next

            For Each tsObj In cd.TransformSetDefinitions
                DumpTransformSetDefinition(graph, tsObj)
            Next

            Console.WriteLine($"  └─")
            Console.WriteLine()
        Next
    End Sub

    Private Sub DumpSimClothData(graph As HkxObjectGraph_Class, obj As HkxVirtualObjectGraph_Class)
        Dim sd = graph.ParseSimClothData(obj)
        If sd Is Nothing Then Return
        Console.WriteLine($"  │   hclSimClothData @0x{obj.RelativeOffset:X}")
        Console.WriteLine($"  │     Particles={sd.Particles.Count}  FixedParticles={sd.FixedParticles.Count}  Triangles={sd.TriangleIndices.Count \ 3}")
        Console.WriteLine($"  │     CollidableTransformIndices={sd.CollidableTransformIndices.Count}  Collidables={sd.Collidables.Count}  Poses={sd.SimClothPoses.Count}  ConstraintSets={sd.StaticConstraintSets.Count}")
        For Each colObj In sd.Collidables
            DumpCollidable(graph, colObj)
        Next
        For Each cs In sd.StaticConstraintSets
            Dim parsed = graph.ParseConstraintSet(cs)
            Dim vp = graph.ParseVolumeConstraintMx(cs)
            If parsed IsNot Nothing Then
                Console.WriteLine($"  │       {cs.ClassName} @0x{cs.RelativeOffset:X}  name=""{parsed.Name}""  count={parsed.ConstraintCount}")
            ElseIf vp IsNot Nothing Then
                Console.WriteLine($"  │       {cs.ClassName} @0x{cs.RelativeOffset:X}  name=""{vp.Name}""  arrays=[{vp.Array1Count},{vp.Array2Count},{vp.Array3Count},{vp.Array4Count}]")
            Else
                Console.WriteLine($"  │       {cs.ClassName} @0x{cs.RelativeOffset:X}")
            End If
        Next
    End Sub

    Private Sub DumpBufferDefinition(graph As HkxObjectGraph_Class, obj As HkxVirtualObjectGraph_Class)
        Dim bd = graph.ParseBufferDefinition(obj)
        If bd Is Nothing Then Return
        Console.WriteLine($"  │   {obj.ClassName} @0x{obj.RelativeOffset:X}  name=""{bd.Name}""  subBuffers={bd.SubBufferCount}  layout={bd.LayoutField}  vertices={bd.VertexCount}")
    End Sub

    Private Sub DumpTransformSetDefinition(graph As HkxObjectGraph_Class, obj As HkxVirtualObjectGraph_Class)
        Dim ts = graph.ParseTransformSetDefinitionData(obj)
        If ts Is Nothing Then Return
        Console.WriteLine($"  │   hclTransformSetDefinition @0x{obj.RelativeOffset:X}  name=""{ts.Name}""  numTransforms={ts.NumTransforms}  floatSlots={ts.NumFloatSlots}")
    End Sub

    Private Sub DumpCollidable(graph As HkxObjectGraph_Class, obj As HkxVirtualObjectGraph_Class)
        Dim col = graph.ParseCollidable(obj)
        If col Is Nothing Then
            Console.WriteLine($"  │   {obj.ClassName} @0x{obj.RelativeOffset:X}  (parse failed)")
            Return
        End If
        Dim pos = If(col.Transform IsNot Nothing,
                     $"  pos=({col.Transform.Translation.X:F3},{col.Transform.Translation.Y:F3},{col.Transform.Translation.Z:F3})",
                     "")
        Dim shapeInfo = ""
        If col.ShapeObject IsNot Nothing Then
            Dim shapeCn = col.ShapeObject.ClassName
            If shapeCn.Equals("hclTaperedCapsuleShape", StringComparison.OrdinalIgnoreCase) Then
                Dim cap = graph.ParseTaperedCapsuleShape(col.ShapeObject)
                If cap IsNot Nothing Then
                    shapeInfo = $"  shape=TaperedCapsule rA={cap.RadiusA:F3} rB={cap.RadiusB:F3}" &
                                $" A=({cap.CentreA.X:F3},{cap.CentreA.Y:F3},{cap.CentreA.Z:F3})" &
                                $" B=({cap.CentreB.X:F3},{cap.CentreB.Y:F3},{cap.CentreB.Z:F3})"
                End If
            ElseIf shapeCn.Equals("hclCapsuleShape", StringComparison.OrdinalIgnoreCase) Then
                Dim cap = graph.ParseCapsuleShape(col.ShapeObject)
                If cap IsNot Nothing Then
                    shapeInfo = $"  shape=Capsule r={cap.Radius:F3}" &
                                $" A=({cap.VertexA.X:F3},{cap.VertexA.Y:F3},{cap.VertexA.Z:F3})" &
                                $" B=({cap.VertexB.X:F3},{cap.VertexB.Y:F3},{cap.VertexB.Z:F3})"
                End If
            Else
                shapeInfo = $"  shape={shapeCn} @0x{col.ShapeObject.RelativeOffset:X}"
            End If
        End If
        Console.WriteLine($"  │   hclCollidable @0x{obj.RelativeOffset:X}  name=""{col.Name}""{pos}  pinch={col.PinchDetectionRadius:F4}{shapeInfo}")
    End Sub

    Private Sub DumpOperatorDetails(graph As HkxObjectGraph_Class, op As HkxVirtualObjectGraph_Class)
        Dim cn = op.ClassName

        ' Dedicated parsers first
        If cn.Equals("hclSimulateOperator", StringComparison.OrdinalIgnoreCase) Then
            Dim p = graph.ParseSimulateOperator(op)
            If p IsNot Nothing Then
                Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{p.Name}""  f24={p.Field24}  f28={p.Field28}  subStates={p.SubStatesCount}")
                Return
            End If
        ElseIf cn.Equals("hclCopyVerticesOperator", StringComparison.OrdinalIgnoreCase) Then
            Dim p = graph.ParseCopyVerticesOperator(op)
            If p IsNot Nothing Then
                Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{p.Name}""  inBuf={p.InputBufferIndex}  outBuf={p.OutputBufferIndex}")
                Return
            End If
        ElseIf cn.Equals("hclGatherAllVerticesOperator", StringComparison.OrdinalIgnoreCase) Then
            Dim p = graph.ParseGatherAllVerticesOperator(op)
            If p IsNot Nothing Then
                Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{p.Name}""  outBuf={p.OutputBufferIndex}  nBufs={p.NumBuffers}  remap={p.VertexRemapping.Count}")
                Return
            End If
        ElseIf cn.Equals("hclMoveParticlesOperator", StringComparison.OrdinalIgnoreCase) Then
            Dim p = graph.ParseMoveParticlesOperator(op)
            If p IsNot Nothing Then
                Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{p.Name}""  particles={p.ParticleIndices.Count}")
                Return
            End If
        ElseIf cn.StartsWith("hclObjectSpaceSkinPN", StringComparison.OrdinalIgnoreCase) OrElse
               cn.StartsWith("hclSimpleMeshBoneDeform", StringComparison.OrdinalIgnoreCase) Then
            ' Detailed parser is in HclRenderGraphParser; use generic for dump
        End If

        ' Constraint sets
        Dim cs = graph.ParseConstraintSet(op)
        If cs IsNot Nothing Then
            Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{cs.Name}""  constraints={cs.ConstraintCount}")
            Return
        End If
        Dim vc = graph.ParseVolumeConstraintMx(op)
        If vc IsNot Nothing Then
            Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{vc.Name}""  arrays=[{vc.Array1Count},{vc.Array2Count},{vc.Array3Count},{vc.Array4Count}]")
            Return
        End If

        ' Generic fallback
        Dim generic = graph.ParseGenericHavokObject(op)
        If generic Is Nothing Then
            Console.WriteLine($"  │   @0x{op.RelativeOffset:X6}  size={op.Size,5}  ""{cn}""  (no parse)")
            Return
        End If
        Dim arrInfo = If(generic.ArrayFields.Count > 0,
                         "  arrays=[" & String.Join(",", generic.ArrayFields.Select(Function(f) f.Header.Count)) & "]", "")
        Dim refInfo = If(generic.ObjectRefs.Count > 0, $"  refs={generic.ObjectRefs.Count}", "")
        Console.WriteLine($"  │   {cn} @0x{op.RelativeOffset:X}  name=""{generic.Name}""{arrInfo}{refInfo}")
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Structural analysis: fixup-based field layout for each unique class
    '──────────────────────────────────────────────────────────────────
    Private Sub DumpStructuralAnalysis(graph As HkxObjectGraph_Class, packfile As HkxPackfile_Class)
        ' Get first instance of each class not yet seen
        Dim section = packfile.GetSection(packfile.Header.ContentsSectionIndex)
        If section Is Nothing Then Return

        Dim byClass = graph.Objects.
            GroupBy(Function(o) o.ClassName, StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(g) g.Key).
            ToList()

        Console.WriteLine($"  ┌─ STRUCTURAL ANALYSIS (fixup-inferred field layouts)")
        For Each grp In byClass
            Dim className = grp.Key
            If _seenClassStructures.Contains(className) Then Continue For
            _seenClassStructures.Add(className)

            Dim obj = grp.OrderBy(Function(o) o.Size).Last() ' Use the largest instance (most data)
            If obj.Size <= 0 Then Continue For

            Dim dumpSize = Math.Min(obj.Size, 256) ' cap at 256 bytes per class
            Dim localFixups  = graph.GetLocalFixupsInRange(obj.RelativeOffset, dumpSize).
                                     OrderBy(Function(f) f.SourceRelativeOffset).ToList()
            Dim globalFixups = graph.GetGlobalFixupsInRange(obj.RelativeOffset, dumpSize).
                                     OrderBy(Function(f) f.SourceRelativeOffset).ToList()

            ' Build lookup: field-relative-offset → annotation
            Dim annotations As New Dictionary(Of Integer, String)

            For Each lf In localFixups
                Dim relFieldOff = lf.SourceRelativeOffset - obj.RelativeOffset
                ' Pointer field (8 bytes). Count follows at +8, capacity at +12 (for 64-bit hkArray)
                Dim destOff = lf.DestinationRelativeOffset
                Dim countOff = relFieldOff + 8
                Dim count = If(countOff + 4 <= dumpSize AndAlso section.AbsoluteDataStart + obj.RelativeOffset + countOff + 3 < packfile.RawBytes.Length,
                               BitConverter.ToInt32(packfile.RawBytes, section.AbsoluteDataStart + obj.RelativeOffset + countOff), 0)
                Dim capOff = relFieldOff + 12
                Dim cap = If(capOff + 4 <= dumpSize AndAlso section.AbsoluteDataStart + obj.RelativeOffset + capOff + 3 < packfile.RawBytes.Length,
                              BitConverter.ToInt32(packfile.RawBytes, section.AbsoluteDataStart + obj.RelativeOffset + capOff) And &H3FFFFFFF, 0)
                annotations(relFieldOff) = $"PTR→data@0x{destOff:X}  count={count}  cap={cap}"
                ' Mark count/cap slots
                If Not annotations.ContainsKey(countOff) Then annotations(countOff) = $"  (count={count})"
                If Not annotations.ContainsKey(capOff) Then annotations(capOff) = $"  (cap={cap})"
            Next

            For Each gf In globalFixups
                Dim relFieldOff = gf.SourceRelativeOffset - obj.RelativeOffset
                Dim targetClass = graph.GetObject(gf.TargetRelativeOffset)?.ClassName
                Dim targetInfo = If(targetClass IsNot Nothing, $"@0x{gf.TargetRelativeOffset:X} ""{targetClass}""", $"@0x{gf.TargetRelativeOffset:X}")
                If Not annotations.ContainsKey(relFieldOff) Then
                    annotations(relFieldOff) = $"GREF→{targetInfo}"
                End If
            Next

            Console.WriteLine($"  │")
            Console.WriteLine($"  │ ── {className}  @0x{obj.RelativeOffset:X}  size={obj.Size}  (showing first {dumpSize} bytes) ──")
            Console.WriteLine($"  │    Local fixups in range: {localFixups.Count}   Global fixups in range: {globalFixups.Count}")

            Dim absObjStart = section.AbsoluteDataStart + obj.RelativeOffset
            For byteOff = 0 To dumpSize - 1 Step 4
                If absObjStart + byteOff + 3 >= packfile.RawBytes.Length Then Continue For

                Dim i32 = BitConverter.ToInt32(packfile.RawBytes, absObjStart + byteOff)
                Dim f32 = BitConverter.ToSingle(packfile.RawBytes, absObjStart + byteOff)
                Dim hexB = $"{packfile.RawBytes(absObjStart + byteOff):X2}{packfile.RawBytes(absObjStart + byteOff + 1):X2}{packfile.RawBytes(absObjStart + byteOff + 2):X2}{packfile.RawBytes(absObjStart + byteOff + 3):X2}"

                Dim ann = ""
                If annotations.ContainsKey(byteOff) Then
                    ann = $"  ← {annotations(byteOff)}"
                ElseIf annotations.ContainsKey(byteOff - 4) AndAlso annotations(byteOff - 4).StartsWith("PTR") Then
                    ann = "  (ptr high dword)"
                Else
                    Dim fStr = If(Single.IsFinite(f32), $"f={f32:F4}", "f=NaN/Inf")
                    ann = $"  i={i32}  {fStr}"
                End If

                Console.WriteLine($"  │  +0x{byteOff:X3}: {hexB}{ann}")
            Next
        Next
        Console.WriteLine($"  └─")
        Console.WriteLine()
    End Sub

    '──────────────────────────────────────────────────────────────────
    ' Helpers
    '──────────────────────────────────────────────────────────────────
    Private Sub WriteError(msg As String)
        Console.ForegroundColor = ConsoleColor.Red
        Console.WriteLine(msg)
        Console.ResetColor()
    End Sub

    Private Sub WriteWarn(msg As String)
        Console.ForegroundColor = ConsoleColor.Yellow
        Console.WriteLine(msg)
        Console.ResetColor()
    End Sub
End Module
