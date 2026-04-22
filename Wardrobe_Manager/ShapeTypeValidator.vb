' Version Uploaded of Wardrobe 3.2.0
Imports System.IO
Imports System.Text.Json
Imports NiflySharp
Imports NiflySharp.Blocks
Imports FO4_Base_Library

''' <summary>
''' Harness automatizado que al cargar un NIF nuevo detecta tipos de shape aún no validados
''' y corre una batería A/B/C/D sobre una COPIA del NIF (el original del usuario queda
''' intacto).  Reusa completamente APIs de NiflySharp + operaciones de producción
''' (SplitShapeHelper, MergeShapesHelper, pipeline de zap de BuildingForm) — no reimplementa
''' nada.  La comparación es byte-diff post save/reload (round-trip determinístico de
''' NiflySharp) + counts via public getters, NO comparator custom con epsilons.
'''
''' Flow:
'''   1. Load cache JSON (shape_validator_cache.json)
'''   2. Enumera shapes en el slider set, calcula tipos únicos no-validados
'''   3. Por tipo nuevo: toma primer shape de ese tipo, corre A/B/C/D sobre copia temporal
'''   4. Escribe log plaintext a %TEMP%/WM_ShapeValidator/validation_&lt;timestamp&gt;.log
'''   5. Marca tipo como "Pending" en el cache (usuario confirma leyendo log)
'''   6. MsgBox con resumen + path al log
'''
''' Usuario revisa log → dice "tipo X OK" → asistente edita JSON marcando Validated.
''' </summary>
Public Class ShapeTypeValidator

    Private Shared ReadOnly CacheFilePath As String =
        Path.Combine(Application.StartupPath, "shape_validator_cache.json")

    Private Shared ReadOnly LogDir As String =
        Path.Combine(Path.GetTempPath(), "WM_ShapeValidator")

    Private Shared ReadOnly SaveOptions As New JsonSerializerOptions With {.WriteIndented = True}

    ''' <summary>
    ''' JSON cache schema — una entry per (shape type, game era) combinación.  BSTriShape-SSE
    ''' y BSTriShape-FO4 son entries distintas porque el layout en disco (packed struct
    ''' BSVertexData vs BSVertexDataSSE) es diferente y cada uno ejercita código diferente
    ''' en el adapter.  Sin la era, validar un BSTriShape FO4 marcaría como "Validated" el
    ''' BSTriShape SSE y nunca lo testearíamos.
    ''' </summary>
    Public Class ValidatorCacheEntry
        Public Property TypeFullName As String
        Public Property GameEra As String = ""          ' "SSE" | "FO4" | "SK" | "LE" | "Other"
        Public Property Status As String = "Pending"   ' "Pending" | "Validated" | "Failed"
        Public Property LastTestTime As DateTime
        Public Property LastResults As String          ' "A=PASS B=PASS C=FAIL D=PASS"
        Public Property SampleNif As String
    End Class

    Public Class ValidatorCache
        Public Property Entries As List(Of ValidatorCacheEntry) = New List(Of ValidatorCacheEntry)
    End Class

    ''' <summary>
    ''' Compound (type, era) key used both for cache lookup and for iteration.  Wrapping in
    ''' a struct (instead of a Tuple) gives the log + cache a single clear identifier.
    ''' </summary>
    Private Structure TypeEraKey
        Public ShapeType As Type
        Public Era As String
        Public ReadOnly Property Key As String
            Get
                Return $"{ShapeType.FullName}|{Era}"
            End Get
        End Property
        Public ReadOnly Property DisplayName As String
            Get
                Return $"{ShapeType.Name} ({Era})"
            End Get
        End Property
    End Structure

    ''' <summary>
    ''' Per-test result used to build the log + cache status.  Success means all counts
    ''' and byte-diffs matched expectations; Mensaje captures the human-readable detail.
    ''' </summary>
    Private Structure TestResult
        Public Success As Boolean
        Public Mensaje As String
        ' Free-form payload; used by RunTestB to report the donor shape name created by the
        ' split (it's non-deterministic when the original target already contains "_Split"
        ' — Check_Unique_Shapename will append "_0", "_1", ... to avoid collisions).  Test C
        ' reads this to find the correct donor instead of guessing via string contains.
        Public Payload As String
    End Structure

    ''' <summary>
    ''' Entry point invoked from CreatefromNif_Form after NIF load.  No-op if all shape
    ''' types already Validated in cache.  Any exception inside is caught by the caller's
    ''' Try/Catch so the validator never blocks the user's NIF load.
    '''
    ''' IMPORTANT: the validator does NOT re-load the NIF from disk (the passed nifLabel
    ''' may be a relative-to-Data path, a BA2 entry, etc.  not resolvable with File.IO).
    ''' Instead it serializes the already-loaded sliderSet.NIFContent to a temp file and
    ''' uses THAT as the canonical source for all subsequent A/B/C/D operations on copies.
    ''' </summary>
    Public Shared Sub ValidateUntestedTypes(sliderSet As SliderSet_Class, nifLabel As String)
        If sliderSet Is Nothing OrElse sliderSet.NIFContent Is Nothing Then Return
        If sliderSet.Shapes Is Nothing OrElse sliderSet.Shapes.Count = 0 Then Return

        Dim cache = LoadCache()

        ' (type, era) únicos presentes en el NIF actual.  Era derivada de Header.Version
        ' — mismo tipo en SSE vs FO4 es entry distinta porque el layout packed vertex y el
        ' código del adapter son diferentes.
        '
        ' Filtro por `Nifcontent_Class_Manolo.SupportedShape` — tipos no soportados
        ' (NiParticles, NiParticleSystem, BSStripParticleSystem, etc.) no tienen adapter
        ' IShapeGeometry y no deben pasar por A/B/C/D.  Antes del filtro, un NIF con
        ' sistema de partículas (p.ej. AnimObjectBlowTorch.nif) disparaba FAIL en todo,
        ' aunque los tipos soportados de ese mismo NIF estaban OK.
        Dim currentEra As String = GetEraTag(sliderSet.NIFContent.Header?.Version)
        Dim presentKeys As New Dictionary(Of String, TypeEraKey)(StringComparer.OrdinalIgnoreCase)
        For Each shap In sliderSet.NIFContent.GetShapes()
            If shap Is Nothing Then Continue For
            If Not Nifcontent_Class_Manolo.SupportedShape(shap.GetType()) Then Continue For
            Dim tek As New TypeEraKey With {.ShapeType = shap.GetType(), .Era = currentEra}
            If Not presentKeys.ContainsKey(tek.Key) Then presentKeys(tek.Key) = tek
        Next

        ' (type, era) que aún no están Validated en cache
        Dim validatedKeys As New HashSet(Of String)(
            cache.Entries.Where(Function(e) e.Status = "Validated").
                         Select(Function(e) $"{e.TypeFullName}|{If(e.GameEra, "")}"),
            StringComparer.OrdinalIgnoreCase)
        Dim pendingKeys = presentKeys.Values.Where(Function(k) Not validatedKeys.Contains(k.Key)).ToList()
        If pendingKeys.Count = 0 Then Return

        ' Log file por ejecución
        Directory.CreateDirectory(LogDir)
        Dim logPath = Path.Combine(LogDir, $"validation_{DateTime.Now:yyyyMMdd_HHmmss}.log")
        AppendLog(logPath, $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Source label: {nifLabel} | Era: {currentEra} ===")
        AppendLog(logPath, $"Types untested: {String.Join(", ", pendingKeys.Select(Function(k) k.DisplayName))}")

        ' Serialize the in-memory NIF to a disk-resolvable temp file ONCE.  All subsequent
        ' ops (A/B/C/D) load fresh copies from this path — never touches the relative-path
        ' `nifLabel` which may point into a BA2 or into a location we can't resolve.
        Dim canonicalTempNif As String
        Try
            canonicalTempNif = Path.Combine(LogDir, $"source_{DateTime.Now:yyyyMMdd_HHmmss}.nif")
            sliderSet.NIFContent.Save_As_Manolo(canonicalTempNif, True)
            AppendLog(logPath, $"In-memory NIF serialized to canonical temp: {canonicalTempNif}")

            ' Pre-flight integrity: si el NIF de partida ya viene con cuentas inconsistentes
            ' (PrimSum!=TC, LOD sum mal, etc.) los tests no pueden distinguir regresiones de
            ' nuestro código vs. NIFs corruptos aguas arriba.  Loguearlo al principio deja
            ' constancia del baseline antes de cualquier operación.
            Dim baselineCheck As New Nifcontent_Class_Manolo
            baselineCheck.Load_Manolo(canonicalTempNif)
            Dim baselineIssues = ValidateAllShapes(baselineCheck)
            If baselineIssues.Count = 0 Then
                AppendLog(logPath, "Baseline integrity: OK (source NIF shapes pass all invariants)")
            Else
                AppendLog(logPath, "Baseline integrity: ISSUES in source NIF:")
                For Each iss In baselineIssues
                    AppendLog(logPath, $"    - {iss}")
                Next
            End If
        Catch ex As Exception
            AppendLog(logPath, $"FATAL: could not serialize in-memory NIF to temp: {ex.Message}")
            MsgBox("Shape Validator: no se pudo crear la copia temporal del NIF." & vbCrLf & ex.Message,
                   vbExclamation, "Shape Type Validator")
            Return
        End Try

        ' Pre-run MsgBox: anuncia qué va a correr antes de ejecutar (el usuario sabe que
        ' la UI va a quedar bloqueada durante los tests).  OKCancel para que el usuario
        ' pueda abortar si acaba de cargar un NIF enorme y no quiere esperar.
        Dim pendingNames = String.Join(", ", pendingKeys.Select(Function(k) k.DisplayName))
        Dim preResp = MsgBox(
            $"Shape Validator va a correr tests A/B/C/D/E + integrity (F/I) + palette (G) para combos no-validados:" & vbCrLf &
            vbCrLf & pendingNames & vbCrLf & vbCrLf &
            "La operación corre sobre una copia aislada del NIF (el que cargaste queda intacto)." & vbCrLf &
            $"Log destino: {logPath}" & vbCrLf & vbCrLf &
            "¿Continuar?",
            vbOKCancel Or vbInformation, "Shape Type Validator")
        If preResp <> vbOK Then
            AppendLog(logPath, "User cancelled before run.")
            Return
        End If

        Dim summary As New List(Of String)()

        For Each tek In pendingKeys
            ' Primer shape del tipo en cuestión
            Dim sampleShape = sliderSet.NIFContent.GetShapes().FirstOrDefault(Function(s) s.GetType() Is tek.ShapeType)
            If sampleShape Is Nothing Then Continue For
            Dim shapeName = If(sampleShape.Name?.String, "(unnamed)")

            AppendLog(logPath, "")
            AppendLog(logPath, $"[{tek.DisplayName}] shape='{shapeName}'")
            AppendLog(logPath, $"  Metadata (pre): {GetMetadataSummary(sampleShape)}")

            ' A) Round-trip + E) Triple idempotency (doble round-trip, 3 saves byte-equal)
            Dim resA = RunTestA(canonicalTempNif, logPath)
            AppendLog(logPath, $"  A+E round-trip+idempotency: {If(resA.Success, "PASS", "FAIL")} ({resA.Mensaje})")

            ' B) Split 50% — requiere copia aislada en temp
            Dim tempSplitNif = Path.Combine(LogDir, $"B_{tek.ShapeType.Name}_{DateTime.Now:HHmmssfff}.nif")
            Dim resB = RunTestB(canonicalTempNif, shapeName, tempSplitNif, logPath)
            AppendLog(logPath, $"  B split 50%: {If(resB.Success, "PASS", "FAIL")} ({resB.Mensaje})")

            ' C) Merge (requiere éxito previo de B) + G) Bone palette lossless.
            ' Pasamos el donorName capturado en B (diff pre/post split) para que el match
            ' target+donor sea exacto y no falle por heurística "_Split" cuando el nombre
            ' original ya contenía ese sufijo.
            Dim resC As TestResult
            If resB.Success Then
                resC = RunTestC(tempSplitNif, shapeName, resB.Payload, logPath)
                AppendLog(logPath, $"  C+G merge+palette: {If(resC.Success, "PASS", "FAIL")} ({resC.Mensaje})")
            Else
                resC = New TestResult With {.Success = False, .Mensaje = "skipped (B failed)"}
                AppendLog(logPath, "  C+G merge+palette: SKIP (B failed)")
            End If

            ' D) Zap 100%+50% (requiere éxito previo de B)
            Dim resD As TestResult
            If resB.Success Then
                resD = RunTestD(tempSplitNif, logPath)
                AppendLog(logPath, $"  D zap 100%+50%: {If(resD.Success, "PASS", "FAIL")} ({resD.Mensaje})")
            Else
                resD = New TestResult With {.Success = False, .Mensaje = "skipped (B failed)"}
                AppendLog(logPath, "  D zap: SKIP (B failed)")
            End If

            ' J) Cross-type rejection (se corre una sola vez por batch, no por tipo — lo
            '    dejamos por fuera del loop)

            ' Cache entry (Pending — usuario confirma manualmente)
            Dim results = $"A={ResultTag(resA)} B={ResultTag(resB)} C={ResultTag(resC)} D={ResultTag(resD)}"
            UpsertCacheEntry(cache, New ValidatorCacheEntry With {
                .TypeFullName = tek.ShapeType.FullName,
                .GameEra = tek.Era,
                .Status = "Pending",
                .LastTestTime = DateTime.Now,
                .LastResults = results,
                .SampleNif = nifLabel
            })
            AppendLog(logPath, $"  → Status: Pending review | {results}")
            summary.Add($"{tek.DisplayName}: {results}")
        Next

        ' Tests H (morph slider applicability) y K (edge cases crafted NIFs) requieren setup
        ' fuera del harness generic-per-type (OSP con sliders reales, NIF con VC cerca de
        ' 65535, etc.) y se testean manualmente.  Documentado en el log para que el usuario
        ' no asuma que "PASS all" los cubre.
        AppendLog(logPath, "")
        AppendLog(logPath, "[Deferred] H (morph slider applicability) — requiere OSP con sliders reales, correr desde Editor")
        AppendLog(logPath, "[Deferred] J (cross-type rejection) — test scenario separado, no integrado aún")
        AppendLog(logPath, "[Deferred] K (edge cases — zap 100% único shape, VC~65K, strips degenerate) — requiere NIFs crafted")

        SaveCache(cache)

        MsgBox(
            $"Shape Validator: {pendingKeys.Count} combo(s) (tipo, era) testeado(s)." & vbCrLf & vbCrLf &
            String.Join(vbCrLf, summary) & vbCrLf & vbCrLf &
            $"Log: {logPath}",
            vbInformation, "Shape Type Validator")
    End Sub

    ''' <summary>
    ''' Test A: round-trip determinístico.  Save original → load fresh → save de nuevo →
    ''' byte-diff.  Si NiflySharp serializa/deserializa consistentemente (que es su
    ''' contrato), los dos saves deben producir bytes idénticos.  Cualquier diferencia
    ''' indica que el estado post-load difiere del estado post-save inicial.
    ''' </summary>
    ''' <summary>
    ''' Test A (round-trip determinismo) + Test E (triple idempotency).  Guarda el NIF
    ''' original 3 veces, recargando entre guardados.  Los 3 archivos deben ser byte-equal.
    ''' Diff entre el 1er y 2do save = estado no-serializado (el clásico "round-trip no
    ''' funciona").  Diff entre el 2do y 3er = no-idempotencia (estado que cambia en cada
    ''' save a pesar de venir de disk).  Ambos son bugs de adapter/serialización.
    ''' </summary>
    Private Shared Function RunTestA(origNifPath As String, logPath As String) As TestResult
        Dim tempA1 As String = Nothing
        Dim tempA2 As String = Nothing
        Dim tempA3 As String = Nothing
        Try
            tempA1 = Path.Combine(LogDir, $"A_save1_{DateTime.Now:HHmmssfff}.nif")
            tempA2 = Path.Combine(LogDir, $"A_save2_{DateTime.Now:HHmmssfff}.nif")
            tempA3 = Path.Combine(LogDir, $"A_save3_{DateTime.Now:HHmmssfff}.nif")

            Dim nif1 As New Nifcontent_Class_Manolo
            nif1.Load_Manolo(origNifPath)
            nif1.Save_As_Manolo(tempA1, True)

            Dim nif2 As New Nifcontent_Class_Manolo
            nif2.Load_Manolo(tempA1)
            nif2.Save_As_Manolo(tempA2, True)

            Dim nif3 As New Nifcontent_Class_Manolo
            nif3.Load_Manolo(tempA2)
            nif3.Save_As_Manolo(tempA3, True)

            Dim diff12 = ByteDiffFiles(tempA1, tempA2)
            Dim diff23 = ByteDiffFiles(tempA2, tempA3)
            Dim byteResult As String
            If diff12.Equal AndAlso diff23.Equal Then
                byteResult = "3 saves byte-equal (A round-trip + E idempotent)"
            ElseIf Not diff12.Equal Then
                byteResult = $"A FAIL: save1 vs save2 diff at offset {diff12.FirstDiffOffset}"
            Else
                byteResult = $"E FAIL: save2 vs save3 diff at offset {diff23.FirstDiffOffset} (A round-trip ok)"
            End If

            ' Integrity post-third-load.
            Dim integrityIssues = ValidateAllShapes(nif3)
            Dim integrityStr = FormatIntegrity(integrityIssues)

            If diff12.Equal AndAlso diff23.Equal AndAlso integrityIssues.Count = 0 Then
                Return New TestResult With {.Success = True, .Mensaje = byteResult & integrityStr}
            Else
                Return New TestResult With {.Success = False, .Mensaje = byteResult & integrityStr}
            End If
        Catch ex As Exception
            Return New TestResult With {.Success = False, .Mensaje = "exception: " & ex.Message & " | TRACE: " & FormatStackTrace(ex)}
        End Try
    End Function

    ''' <summary>
    ''' Test B: split del primer shape de este tipo en el NIF sobre una copia.  Marca 50%
    ''' de los vértices como masked (primeros N/2) y llama SplitShapeHelper.Split tal como
    ''' Editor_Form lo haría.  Luego guarda y verifica que el NIF tenga +1 shape y los
    ''' counts/metadata tipo-específica sean coherentes.
    ''' </summary>
    Private Shared Function RunTestB(origNifPath As String, shapeName As String,
                                      tempSplitNif As String, logPath As String) As TestResult
        Try
            ' Cargo copia aislada via SliderSet throwaway
            Dim throwaway = BuildThrowawaySliderSet(origNifPath)
            Dim shapeToSplit = throwaway.Shapes.FirstOrDefault(Function(s) s.Target.Equals(shapeName, StringComparison.OrdinalIgnoreCase))
            If shapeToSplit Is Nothing Then
                Return New TestResult With {.Success = False, .Mensaje = $"shape '{shapeName}' not found in throwaway"}
            End If

            Dim origShape = shapeToSplit.RelatedNifShape
            If origShape Is Nothing Then
                Return New TestResult With {.Success = False, .Mensaje = "RelatedNifShape is Nothing"}
            End If

            Dim vcOriginal As Integer = CInt(origShape.VertexCount)
            Dim origMetadata = GetMetadataSummary(origShape)

            ' Marca primeros 50% de vertex indices — patrón igual a Editor_Form ButtonMaskAll
            Dim halfCount = vcOriginal \ 2
            If halfCount <= 0 Then
                Return New TestResult With {.Success = False, .Mensaje = $"VertexCount {vcOriginal} too small to split"}
            End If
            shapeToSplit.MaskedVertices.UnionWith(Enumerable.Range(0, halfCount))

            ' Snapshot shape names PRE-split; the new donor is whichever name appears ONLY
            ' after the split.  This is robust to shapes whose original name already contains
            ' "_Split" (e.g. Dawnguard body1 "Body1_fem_Split") where SplitShapeHelper's
            ' `target & "_Split"` convention collides with an existing shape and
            ' Check_Unique_Shapename appends "_0"/"_1"/... suffixes we can't predict.
            Dim preSplitNames As New HashSet(Of String)(
                throwaway.NIFContent.GetShapes().Select(Function(s) s.Name.String),
                StringComparer.OrdinalIgnoreCase)

            SplitShapeHelper.Split(shapeToSplit, throwaway)
            throwaway.NIFContent.Save_As_Manolo(tempSplitNif, True)

            ' Donor name = the NEW shape name that didn't exist pre-split.
            Dim postSplitNames = throwaway.NIFContent.GetShapes().Select(Function(s) s.Name.String).ToList()
            Dim donorName As String = postSplitNames.FirstOrDefault(Function(n) Not preSplitNames.Contains(n))

            ' Reload fresh y verifica
            Dim reloaded As New Nifcontent_Class_Manolo
            reloaded.Load_Manolo(tempSplitNif)

            Dim shapeCount = reloaded.GetShapes().Count()
            Dim origCount = throwaway.NIFContent.GetShapes().Count()
            ' throwaway ya tiene las 2 halves post-split; comparamos contra NIF original pre-split:
            Dim preSplitCount As Integer
            Dim preSplitNif As New Nifcontent_Class_Manolo
            preSplitNif.Load_Manolo(origNifPath)
            preSplitCount = preSplitNif.GetShapes().Count()

            If shapeCount <> preSplitCount + 1 Then
                Return New TestResult With {.Success = False,
                    .Mensaje = $"expected {preSplitCount + 1} shapes, got {shapeCount}"}
            End If

            ' Summary per half
            Dim halves = reloaded.GetShapes().
                Where(Function(s) s.Name.String.StartsWith(shapeName, StringComparison.OrdinalIgnoreCase)).
                ToList()
            Dim halfCounts As New List(Of String)()
            For Each h In halves
                halfCounts.Add($"{h.Name.String}:{GetMetadataSummary(h)}")
            Next

            ' Integrity post-split: todas las shapes del NIF deben ser legales (halves y
            ' cualquier shape que no tocamos).  El split emite Segments/LOD nuevas vía
            ' RedistributeSegments; bugs de redistribución aparecen aquí como PrimSum!=TC,
            ' LodSum!=TC, SubSegment sum mismatch, o triangle idx out-of-bounds.
            Dim integrityIssues = ValidateAllShapes(reloaded)
            Dim integrityStr = FormatIntegrity(integrityIssues)

            Dim baseMsg = $"shapes={shapeCount} (pre={preSplitCount}); donor='{If(donorName, "??")}'; halves=[{String.Join(" | ", halfCounts)}]"
            If integrityIssues.Count = 0 Then
                Return New TestResult With {.Success = True, .Mensaje = baseMsg & integrityStr, .Payload = donorName}
            Else
                Return New TestResult With {.Success = False, .Mensaje = baseMsg & integrityStr, .Payload = donorName}
            End If
        Catch ex As Exception
            Return New TestResult With {.Success = False, .Mensaje = "exception: " & ex.Message & " | TRACE: " & FormatStackTrace(ex)}
        End Try
    End Function

    ''' <summary>
    ''' Test C: merge del output de B.  Carga el NIF de split, encuentra target+donor por
    ''' nombres EXACTOS (origShapeName + donorNameFromB) — no por heurística de Contains,
    ''' que falla cuando el shape original ya contenía "_Split" en su nombre.
    ''' </summary>
    Private Shared Function RunTestC(splitNifPath As String, origShapeName As String, donorName As String, logPath As String) As TestResult
        Try
            Dim throwaway = BuildThrowawaySliderSet(splitNifPath)

            ' Pick target + donor by exact name.  donorName was captured by RunTestB from
            ' the diff of shape names pre/post split (robust to shape names that already
            ' contain "_Split" like Dawnguard's Body1_fem_Split).
            Dim target = throwaway.Shapes.FirstOrDefault(Function(s) s.Target.Equals(origShapeName, StringComparison.OrdinalIgnoreCase))
            Dim donor As Shape_class = Nothing
            If Not String.IsNullOrEmpty(donorName) Then
                donor = throwaway.Shapes.FirstOrDefault(Function(s) s.Target.Equals(donorName, StringComparison.OrdinalIgnoreCase))
            End If
            If target Is Nothing OrElse donor Is Nothing Then
                Return New TestResult With {.Success = False,
                    .Mensaje = $"could not locate target+donor halves (target={If(target?.Target, "null")}, donor={If(donor?.Target, donorName)})"}
            End If

            Dim preMergeTargetVc As Integer = CInt(target.RelatedNifShape.VertexCount)
            Dim preMergeDonorVc As Integer = CInt(donor.RelatedNifShape.VertexCount)

            ' [Test G] Snapshot de bone NIF-block indices pre-merge (target ∪ donor).
            ' Post-merge, el target debe referenciar al menos la UNION de bones (donor bones
            ' agregados al palette del target sin pisar los existentes).  Palette más chica
            ' = bones del donor se perdieron → vertices del donor quedarán mal skinneados.
            Dim preMergeBoneUnion As New HashSet(Of Integer)(GetShapeBoneNifIndices(target.RelatedNifShape, throwaway.NIFContent))
            For Each b In GetShapeBoneNifIndices(donor.RelatedNifShape, throwaway.NIFContent)
                preMergeBoneUnion.Add(b)
            Next

            MergeShapesHelper.Merge(target, New List(Of Shape_class) From {donor}, throwaway)

            Dim tempMergeNif = Path.Combine(LogDir, $"C_merged_{DateTime.Now:HHmmssfff}.nif")
            throwaway.NIFContent.Save_As_Manolo(tempMergeNif, True)

            Dim reloaded As New Nifcontent_Class_Manolo
            reloaded.Load_Manolo(tempMergeNif)
            Dim mergedShape = reloaded.GetShapes().FirstOrDefault(Function(s) s.Name.String.Equals(origShapeName, StringComparison.OrdinalIgnoreCase))
            If mergedShape Is Nothing Then
                Return New TestResult With {.Success = False, .Mensaje = "merged shape not found post-reload"}
            End If

            Dim mergedVc As Integer = CInt(mergedShape.VertexCount)
            Dim expectedVc = preMergeTargetVc + preMergeDonorVc

            ' Integrity post-merge: captura el bug de double-count PrimSum=3970 vs TC=2698
            ' (fix de RedistributeSegments synthetic-only fallback).  Valida que las
            ' Segments/LOD post-merge suman exactamente TC sin duplicaciones.
            Dim integrityIssues = ValidateAllShapes(reloaded)
            Dim integrityStr = FormatIntegrity(integrityIssues)

            ' [Test G] Palette lossless: el palette del shape merged debe contener TODOS los
            ' bones del union pre-merge.  Si algún bone del donor no aparece en el merged
            ' palette, los vértices del donor están skinneados contra un bone que el shape
            ' ya no referencia → crash/corrupt pose en render.
            Dim postMergeBones As New HashSet(Of Integer)(GetShapeBoneNifIndices(mergedShape, reloaded))
            Dim missingBones As New List(Of Integer)()
            For Each b In preMergeBoneUnion
                If Not postMergeBones.Contains(b) Then missingBones.Add(b)
            Next
            Dim paletteStr As String
            If missingBones.Count = 0 Then
                paletteStr = $" + palette={postMergeBones.Count} bones (union pre={preMergeBoneUnion.Count} preserved)"
            Else
                paletteStr = $" + palette=FAIL missing {missingBones.Count} donor bones (pre={preMergeBoneUnion.Count}, post={postMergeBones.Count})"
                integrityIssues.Add($"merged palette missing bones: {String.Join(",", missingBones)}")
            End If

            Dim baseMsg = $"merged VC={mergedVc}; metadata=[{GetMetadataSummary(mergedShape)}]" & paletteStr

            If mergedVc <> expectedVc Then
                Return New TestResult With {.Success = False,
                    .Mensaje = $"VC mismatch: expected {expectedVc} (={preMergeTargetVc}+{preMergeDonorVc}), got {mergedVc}" & integrityStr}
            End If

            If integrityIssues.Count = 0 AndAlso missingBones.Count = 0 Then
                Return New TestResult With {.Success = True, .Mensaje = baseMsg & integrityStr}
            Else
                Return New TestResult With {.Success = False, .Mensaje = baseMsg & integrityStr}
            End If
        Catch ex As Exception
            Return New TestResult With {.Success = False, .Mensaje = "exception: " & ex.Message & " | TRACE: " & FormatStackTrace(ex)}
        End Try
    End Function

    ''' <summary>
    ''' Test D: sobre el NIF de split, zap 100% de la primera shape + 50% de la segunda.
    ''' Pipeline exacto de BuildingForm.vb:76-94 — Extract → VertexMask=-1 → Bake con
    ''' RemoveZaps=True + ZapGeometryModifier → UpdateSkinPartitions.
    ''' </summary>
    Private Shared Function RunTestD(splitNifPath As String, logPath As String) As TestResult
        Try
            Dim throwaway = BuildThrowawaySliderSet(splitNifPath)
            If throwaway.Shapes.Count < 2 Then
                Return New TestResult With {.Success = False,
                    .Mensaje = $"need ≥2 shapes, got {throwaway.Shapes.Count}"}
            End If

            Dim shape1 = throwaway.Shapes(0)
            Dim shape2 = throwaway.Shapes(1)
            Dim shape1Name = shape1.Target
            Dim shape2Name = shape2.Target
            Dim shape2PreVc As Integer = CInt(shape2.RelatedNifShape.VertexCount)

            ' Shape 1: zap 100%
            ZapShapePercent(shape1, throwaway, 1.0)
            ' Shape 2: zap 50%
            ZapShapePercent(shape2, throwaway, 0.5)

            Dim tempZapNif = Path.Combine(LogDir, $"D_zap_{DateTime.Now:HHmmssfff}.nif")
            throwaway.NIFContent.Save_As_Manolo(tempZapNif, True)

            Dim reloaded As New Nifcontent_Class_Manolo
            reloaded.Load_Manolo(tempZapNif)

            Dim reloadedShape1 = reloaded.GetShapes().FirstOrDefault(Function(s) s.Name.String.Equals(shape1Name, StringComparison.OrdinalIgnoreCase))
            Dim reloadedShape2 = reloaded.GetShapes().FirstOrDefault(Function(s) s.Name.String.Equals(shape2Name, StringComparison.OrdinalIgnoreCase))

            ' Shape 1 puede estar: (a) removido del NIF; (b) presente con VC=0.
            Dim shape1Gone = (reloadedShape1 Is Nothing) OrElse (reloadedShape1.VertexCount = 0)

            ' Shape 2 debería tener aproximadamente mitad de verts (±tolerancia por fully-masked triangles)
            Dim shape2PostVc As Integer = If(reloadedShape2 Is Nothing, -1, CInt(reloadedShape2.VertexCount))
            Dim shape2ExpectedRoughly = shape2PreVc \ 2
            Dim shape2OK = (shape2PostVc >= 0) AndAlso (shape2PostVc <= shape2ExpectedRoughly + 10)  ' tolerance

            ' Integrity post-zap: shape2 tras zap 50% tiene Segments/LOD redistribuidas por
            ' ApplyShapeGeometry.  Valida que los counts quedaron consistentes (PrimSum==TC,
            ' LodSum==TC, triangle idx < VC, weights suman 1.0).  Si shape1 se borró del NIF
            ' ValidateAllShapes iterará solo las shapes presentes.
            Dim integrityIssues = ValidateAllShapes(reloaded)
            Dim integrityStr = FormatIntegrity(integrityIssues)

            Dim baseMsg As String
            If shape1Gone AndAlso shape2OK Then
                baseMsg = $"shape1 removed/empty; shape2 VC={shape2PostVc} (pre={shape2PreVc}, target≈{shape2ExpectedRoughly})"
            Else
                baseMsg = $"shape1Gone={shape1Gone} shape2VC={shape2PostVc} (pre={shape2PreVc}, target≈{shape2ExpectedRoughly})"
            End If

            Dim topLineOk = shape1Gone AndAlso shape2OK
            If topLineOk AndAlso integrityIssues.Count = 0 Then
                Return New TestResult With {.Success = True, .Mensaje = baseMsg & integrityStr}
            Else
                Return New TestResult With {.Success = False, .Mensaje = baseMsg & integrityStr}
            End If
        Catch ex As Exception
            Return New TestResult With {.Success = False, .Mensaje = "exception: " & ex.Message & " | TRACE: " & FormatStackTrace(ex)}
        End Try
    End Function

    ''' <summary>
    ''' Pipeline de zap en producción (BuildingForm.vb:76-94) aplicado a una shape con un
    ''' porcentaje dado de vertices marcados.  Reusa ExtractSkinnedGeometry,
    ''' BakeFromMemoryUsingOriginal, ZapGeometryModifier y UpdateSkinPartitions tal como
    ''' corren durante un build real.
    ''' </summary>
    Private Shared Sub ZapShapePercent(shape As Shape_class, sliderSet As SliderSet_Class, pct As Double)
        ' Test harness must extract/bake against bind regardless of any stale pose left on the
        ' default SkeletonInstance by previous renders. Reset() ensures DeltaTransforms=Nothing
        ' so Extract/Bake collapse to bind transforms.
        SkeletonInstance.Default.Reset()
        Dim geom = SkinningHelper.ExtractSkinnedGeometry(shape,
                                                         singleboneskinning:=False,
                                                         RecalculateNormals:=False)
        Dim vc As Integer = geom.Vertices.Length
        Dim zapCount As Integer = CInt(Math.Floor(vc * pct))
        For i = 0 To zapCount - 1
            geom.VertexMask(i) = -1.0F
        Next
        SkinningHelper.BakeFromMemoryUsingOriginal(shape, geom,
                                                    inverse:=False,
                                                    ApplyMorph:=True, RemoveZaps:=True,
                                                    singleBoneSkinning:=False,
                                                    geometryModifier:=New ZapGeometryModifier())
        ' Post-bake: update partition unless shape fully zapped + not keeping empty
        If geom.Vertices.Length = 0 AndAlso sliderSet.KeepZappedShapes = False Then
            sliderSet.RemoveShape(shape)
        Else
            sliderSet.NIFContent.UpdateSkinPartitions(shape.RelatedNifShape)
        End If
    End Sub

    ''' <summary>
    ''' Builds an isolated throwaway SliderSet_Class wrapping a fresh Nifcontent_Class_Manolo
    ''' loaded from disk.  Uses the canonical `SliderSet_Class(OSP)` constructor (OSP_Clases.vb:2378)
    ''' which wires up Nodo + OSP.xml.DocumentElement.AppendChild correctly — required
    ''' because Shape_class.New(Name, Sliderset) at OSP_Clases.vb:2952 appends a &lt;Shape&gt;
    ''' XML element to Sliderset.Nodo and would NRE without it.
    '''
    ''' Needs: sliderSet.NIFContent, sliderSet.Shapes populated from NIF, OSD content
    ''' initialized.  Mirrors the setup CreatefromNif_Form does at line 142-146.
    ''' </summary>
    Private Shared Function BuildThrowawaySliderSet(nifPath As String) As SliderSet_Class
        Dim dummyOsp As New OSP_Project_Class()
        ' Use the no-xml-node constructor that creates a fully-formed empty SliderSet,
        ' including DataFolder/SourceFile/OutputPath/OutputFile children + Nodo + OSP append.
        Dim sliderSet As New SliderSet_Class(dummyOsp)
        sliderSet.NIFContent = New Nifcontent_Class_Manolo
        sliderSet.NIFContent.Load_Manolo(nifPath)

        ' Enumera shapes igual que CreatefromNif_Form
        For Each shap In sliderSet.NIFContent.GetShapes()
            If Nifcontent_Class_Manolo.SupportedShape(shap.GetType()) Then
                Dim sc As New Shape_class(shap.Name.String, sliderSet)
                sliderSet.Shapes.Add(sc)
            End If
        Next
        sliderSet.OSDContent_Local = New OSD_Class(sliderSet)
        sliderSet.Unreadable_NIF = False
        sliderSet.ShapeDataLoaded = True

        ' Fake zap slider to bypass the IsZap guard at MorphingHelper.RemoveZaps:127.
        ' Without any IsZap slider in the set, RemoveZaps exits early and the zap pipeline
        ' becomes a no-op — test D would always report 0 removed verts.  The fake slider
        ' has no Data so morph evaluation skips it; only the guard cares it exists.
        Dim fakeZap As New Slider_class("__ValidatorFakeZap__", sliderSet, TriMorphType.Position)
        fakeZap.IsZap = True
        sliderSet.Sliders.Add(fakeZap)

        sliderSet.InvalidateAllLookupCaches()
        sliderSet.RebuildShapeDataLookupCache()
        Return sliderSet
    End Function

    ''' <summary>
    ''' Byte-diff determinístico: lee ambos archivos como byte arrays y reporta si son
    ''' iguales + first diff offset.  NiflySharp serializa determinísticamente (mismo
    ''' content → mismos bytes), así que cualquier diff revela cambios de estado no-serial.
    ''' </summary>
    Private Shared Function ByteDiffFiles(pathA As String, pathB As String) As (Equal As Boolean, FirstDiffOffset As Integer)
        Dim bytesA = File.ReadAllBytes(pathA)
        Dim bytesB = File.ReadAllBytes(pathB)
        If bytesA.Length <> bytesB.Length Then
            Return (False, Math.Min(bytesA.Length, bytesB.Length))
        End If
        For i = 0 To bytesA.Length - 1
            If bytesA(i) <> bytesB(i) Then Return (False, i)
        Next
        Return (True, -1)
    End Function

    ''' <summary>
    ''' Full integrity check invoked post-op in every test.  Returns an empty list when the
    ''' shape is consistent; otherwise each entry is a human-readable issue that will make
    ''' the enclosing test FAIL.  The checks here are crash-prevention invariants for the
    ''' in-game dismember engine + renderer, plus per-type count consistency that caught the
    ''' merge C double-count regression.
    '''
    ''' NO comparator con epsilons: solo invariantes hard (== / &lt; / finite).  El único
    ''' epsilon tolerado es 0.01 en la suma de bone weights por vértice (cuantización half
    ''' float + rounding NiflySharp hacen que la suma típica sea 1.0 ± 3e-3).
    ''' </summary>
    Private Shared Function ValidateShapeIntegrity(shape As INiShape, nif As Nifcontent_Class_Manolo) As List(Of String)
        Dim issues As New List(Of String)()
        If shape Is Nothing Then
            issues.Add("shape is Nothing")
            Return issues
        End If

        Dim vc As Integer = CInt(shape.VertexCount)
        Dim tc As Integer = shape.TriangleCount

        ' 1. Build adapter to reuse polymorphic getters (adapters verified by test A round-trip).
        Dim geom As IShapeGeometry = Nothing
        Try
            geom = ShapeGeometryFactory.[For](shape, nif)
        Catch ex As Exception
            issues.Add($"adapter build failed: {ex.Message}")
            Return issues
        End Try

        ' 2. Triangle vertex indices bounded by VertexCount (crash-prevention for renderer).
        Dim tris = geom.GetTriangles()
        For i = 0 To tris.Count - 1
            Dim t = tris(i)
            If CInt(t.V1) >= vc OrElse CInt(t.V2) >= vc OrElse CInt(t.V3) >= vc Then
                issues.Add($"tri[{i}] references vert >= VC ({t.V1},{t.V2},{t.V3} vs VC={vc})")
                Exit For  ' report once; likely many follow
            End If
        Next

        ' 3. Parallel per-vertex array counts match VertexCount.
        If geom.HasNormals Then
            Dim n = geom.GetNormals().Count
            If n <> vc Then issues.Add($"Normals.Count={n} != VC={vc}")
        End If
        If geom.HasTangents Then
            Dim n = geom.GetTangents().Count
            If n <> vc Then issues.Add($"Tangents.Count={n} != VC={vc}")
            Dim m = geom.GetBitangents().Count
            If m <> vc Then issues.Add($"Bitangents.Count={m} != VC={vc}")
        End If
        If geom.HasUVs Then
            Dim n = geom.GetUVs().Count
            If n <> vc Then issues.Add($"UVs.Count={n} != VC={vc}")
        End If
        If geom.HasVertexColors Then
            Dim n = geom.GetVertexColors().Count
            If n <> vc Then issues.Add($"VertexColors.Count={n} != VC={vc}")
        End If

        ' 4. No NaN/Inf in positions / normals (would corrupt bounds, skinning, render).
        Dim positions = geom.GetVertexPositions()
        For i = 0 To positions.Count - 1
            Dim p = positions(i)
            If Single.IsNaN(p.X) OrElse Single.IsNaN(p.Y) OrElse Single.IsNaN(p.Z) OrElse
               Single.IsInfinity(p.X) OrElse Single.IsInfinity(p.Y) OrElse Single.IsInfinity(p.Z) Then
                issues.Add($"position[{i}] has NaN/Inf")
                Exit For
            End If
        Next

        ' 5. Bounds sphere finite + radius >= 0 (game crashes on NaN bounds).
        Dim b = geom.Bounds
        If Single.IsNaN(b.Center.X) OrElse Single.IsNaN(b.Radius) OrElse b.Radius < 0.0F Then
            issues.Add($"bounds invalid: center={b.Center} radius={b.Radius}")
        End If

        ' 6. Per-type metadata consistency.
        Select Case shape.GetType()
            Case GetType(BSSubIndexTriShape)
                Dim s = DirectCast(shape, BSSubIndexTriShape)
                If s.Segments IsNot Nothing Then
                    Dim primSum As Long = s.Segments.Sum(Function(x) CLng(x.NumPrimitives))
                    If primSum <> tc Then
                        issues.Add($"BSSubIndex PrimSum={primSum} != TC={tc} (double-count or missing triangles in Segments)")
                    End If
                    ' Per-segment: sum(SubSegment.NumPrimitives) == parent.NumPrimitives (when SubSegments present).
                    For si = 0 To s.Segments.Count - 1
                        Dim seg = s.Segments(si)
                        If seg.SubSegment IsNot Nothing AndAlso seg.SubSegment.Count > 0 Then
                            Dim subSum As Long = seg.SubSegment.Sum(Function(ss) CLng(ss.NumPrimitives))
                            If subSum <> CLng(seg.NumPrimitives) Then
                                issues.Add($"BSSubIndex seg[{si}] SubSegSum={subSum} != seg.NumPrimitives={seg.NumPrimitives}")
                            End If
                        End If
                    Next
                End If

            Case GetType(BSMeshLODTriShape)
                Dim s = DirectCast(shape, BSMeshLODTriShape)
                Dim lodSum As Long = CLng(s.LOD0Size) + CLng(s.LOD1Size) + CLng(s.LOD2Size)
                If lodSum <> tc Then
                    issues.Add($"BSMeshLOD LodSum={lodSum} != TC={tc}")
                End If

            Case GetType(BSLODTriShape)
                Dim s = DirectCast(shape, BSLODTriShape)
                Dim lodSum As Long = CLng(s.LOD0Size) + CLng(s.LOD1Size) + CLng(s.LOD2Size)
                If lodSum <> tc Then
                    issues.Add($"BSLOD LodSum={lodSum} != TC={tc}")
                End If

            Case GetType(BSSegmentedTriShape)
                Dim segList = TryCast(NiTriShapeGeometry.SegmentedSegmentField.GetValue(shape),
                                       List(Of NiflySharp.Structs.BSGeometrySegmentData))
                If segList IsNot Nothing Then
                    Dim primSum As Long = segList.Sum(Function(x) CLng(x.NumPrimitives))
                    If primSum <> tc Then
                        issues.Add($"BSSegmented PrimSum={primSum} != TC={tc}")
                    End If
                End If
        End Select

        ' 7. [Test I] Shader ref preservation — post-op el shape debe mantener una
        '    referencia válida a su shader property block.  Split/merge/zap nunca debe
        '    cambiar el material del shape.  Index=-1 o fuera de rango = shape sin
        '    material (aparece invisible o con error-shader en juego).
        If shape.ShaderPropertyRef IsNot Nothing AndAlso shape.ShaderPropertyRef.Index >= 0 Then
            If shape.ShaderPropertyRef.Index >= nif.Blocks.Count Then
                issues.Add($"ShaderPropertyRef.Index={shape.ShaderPropertyRef.Index} out of range (Blocks.Count={nif.Blocks.Count})")
            ElseIf nif.Blocks(shape.ShaderPropertyRef.Index) Is Nothing Then
                issues.Add($"ShaderPropertyRef points to null block at index {shape.ShaderPropertyRef.Index}")
            End If
        End If

        ' 8. [Test F] Skin partition integrity — post-op la partition debe existir y sus
        '    VertexMap / Triangles deben ser consistentes con el VertexCount del shape.
        '    UpdateSkinPartitions produce esto; un bug en el adapter de skinning puede
        '    hacer que la partition quede con vertex refs out-of-range (crash en render).
        If shape.SkinInstanceRef IsNot Nothing AndAlso shape.SkinInstanceRef.Index >= 0 AndAlso
           shape.SkinInstanceRef.Index < nif.Blocks.Count Then
            Dim skinInst = TryCast(nif.Blocks(shape.SkinInstanceRef.Index), NiSkinInstance)
            If skinInst IsNot Nothing AndAlso skinInst.SkinPartition IsNot Nothing AndAlso
               skinInst.SkinPartition.Index >= 0 AndAlso skinInst.SkinPartition.Index < nif.Blocks.Count Then
                Dim part = TryCast(nif.Blocks(skinInst.SkinPartition.Index), NiSkinPartition)
                If part IsNot Nothing AndAlso part.Partitions IsNot Nothing Then
                    For pi = 0 To part.Partitions.Count - 1
                        Dim p = part.Partitions(pi)
                        ' VertexMap entries must be < VertexCount (they're shape-space indices).
                        If p.VertexMap IsNot Nothing Then
                            For vmi = 0 To p.VertexMap.Count - 1
                                If CInt(p.VertexMap(vmi)) >= vc Then
                                    issues.Add($"partition[{pi}].VertexMap[{vmi}]={p.VertexMap(vmi)} >= VC={vc}")
                                    Exit For
                                End If
                            Next
                        End If
                        ' TrianglesCopy: "true triangles" — indices into the shape's vertex
                        ' list (NiSkinPartition.cs:16 comment: "TrianglesCopy always uses
                        ' indices into the shape's vertex list").  Must be < VertexCount,
                        ' NOT VertexMap.Count (local-space is for the other field below).
                        If p.TrianglesCopy IsNot Nothing Then
                            For ti = 0 To p.TrianglesCopy.Count - 1
                                Dim t = p.TrianglesCopy(ti)
                                If CInt(t.V1) >= vc OrElse CInt(t.V2) >= vc OrElse CInt(t.V3) >= vc Then
                                    issues.Add($"partition[{pi}].TrianglesCopy[{ti}] shape idx >= VC={vc}")
                                    Exit For
                                End If
                            Next
                        End If
                        ' partition.Triangles: semantics depend on NiflySharp's internal
                        ' `mappedIndices` flag (NiSkinPartition.cs:14-17) — when true the
                        ' indices are local (into VertexMap), when false they're shape-space.
                        ' The flag is `internal` and flips based on file version + NiflySharp
                        ' ops, so we can't reliably pick the right upper bound from outside.
                        ' Vanilla SSE hair NIFs (hair01.nif, hairline16.nif) load with
                        ' partition.Triangles values that exceed VertexMap.Count — they
                        ' still work in-game, which means either the flag is false for them
                        ' or NiflySharp regenerates the mapped form on save.  Either way,
                        ' our check was producing false positives on baseline.  Drop it;
                        ' TrianglesCopy (shape-space, unambiguous) above already catches the
                        ' real crash-prevention invariant for this family.
                    Next
                End If
            End If
        End If

        ' 9. Skin weights: Σ per vertex ≈ 1.0 ± 0.01 (half-float quantization tolerated).
        '    Unskinned shapes have IsSkinned=False → no-op.
        If geom.IsSkinned Then
            Dim sk = geom.GetSkinning()
            If sk.BoneWeights IsNot Nothing AndAlso sk.WeightsPerVertex > 0 AndAlso sk.VertexCount = vc Then
                Dim wpv = sk.WeightsPerVertex
                Dim flaggedZero As Integer = 0
                Dim flaggedSum As Integer = 0
                For vi = 0 To vc - 1
                    Dim sum As Single = 0.0F
                    For j = 0 To wpv - 1
                        sum += CSng(sk.BoneWeights(vi * wpv + j))
                    Next
                    If sum = 0.0F Then
                        flaggedZero += 1
                    ElseIf Math.Abs(sum - 1.0F) > 0.01F Then
                        flaggedSum += 1
                    End If
                Next
                ' ALL-ZERO weights: only fail if >1% of verts are affected.  Vanilla LE NIFs
                ' often have a handful of orphan verts with zero weights (daedric gauntlet
                ' had 6 of 907 post-zap).  Our SetSkinning adds dummy (bone 0, weight 0)
                ' entries for verts that would otherwise break NiflySharp's UpdateSkinPartitions
                ' Dictionary lookup — those legitimate dummies also report as "all-zero".
                ' The real crash-prevention concern is WIDESPREAD zero weights (bone palette
                ' breakage after merge), not individual orphans.
                Dim zeroThreshold As Integer = Math.Max(10, vc \ 100)
                If flaggedZero > zeroThreshold Then
                    issues.Add($"{flaggedZero} verts with ALL-ZERO bone weights (>{zeroThreshold} threshold; possible bone palette breakage)")
                End If
                If flaggedSum > 0 Then issues.Add($"{flaggedSum} verts with Σ weights outside 1.0 ± 0.01")
            Else
                issues.Add($"IsSkinned=True but skinning data malformed (BoneWeights null or VC mismatch)")
            End If
        End If

        Return issues
    End Function

    ''' <summary>
    ''' Formats a validation run as " + integrity=OK" on pass, or " + integrity=[issue1; issue2; ...]"
    ''' on fail.  Issues are appended to the existing test message so the log shows why a
    ''' test that looked like PASS in counts actually FAILed integrity.
    ''' </summary>
    Private Shared Function FormatIntegrity(issues As List(Of String)) As String
        If issues Is Nothing OrElse issues.Count = 0 Then Return " + integrity=OK"
        Return " + integrity=FAIL[" & String.Join("; ", issues) & "]"
    End Function

    ''' <summary>
    ''' Checks integrity of every shape in a reloaded NIF and aggregates the issues
    ''' per-shape.  Used by tests A/B/C/D post-op.
    ''' </summary>
    Private Shared Function ValidateAllShapes(nif As Nifcontent_Class_Manolo) As List(Of String)
        Dim all As New List(Of String)()
        For Each sh In nif.GetShapes()
            If sh Is Nothing Then Continue For
            ' Skip unsupported shape types (particles, strips-particles, etc.).  These have
            ' no IShapeGeometry adapter and would be flagged as "adapter build failed" even
            ' though they're not part of our refactor scope.  They live in the same NIF as
            ' supported shapes (animated meshes with particle effects) so filtering here
            ' lets the supported shapes still be validated.
            If Not Nifcontent_Class_Manolo.SupportedShape(sh.GetType()) Then Continue For
            Dim shapeName As String = If(sh.Name?.String, "(unnamed)")
            Dim issues = ValidateShapeIntegrity(sh, nif)
            For Each iss In issues
                all.Add($"'{shapeName}': {iss}")
            Next
        Next
        Return all
    End Function

    ''' <summary>
    ''' Select Case por tipo de shape — describe el estado concreto del shape usando getters
    ''' públicos de NiflySharp donde están disponibles, o reflection sobre los FieldInfo
    ''' ya definidos en los adapters (reusados vía Friend visibility — no crea FieldInfo
    ''' nuevos).
    ''' </summary>
    Private Shared Function GetMetadataSummary(shape As INiShape) As String
        If shape Is Nothing Then Return "(null)"
        Dim vc As Integer = CInt(shape.VertexCount)
        Dim tc As Integer = shape.TriangleCount

        Select Case shape.GetType()
            Case GetType(BSSubIndexTriShape)
                Dim s = DirectCast(shape, BSSubIndexTriShape)
                Dim segCount As Integer = If(s.Segments Is Nothing, 0, s.Segments.Count)
                Dim primSum As Long = If(s.Segments Is Nothing, 0L, s.Segments.Sum(Function(x) CLng(x.NumPrimitives)))
                Return $"VC={vc} TC={tc} Segments={segCount} PrimSum={primSum}"
            Case GetType(BSMeshLODTriShape)
                Dim s = DirectCast(shape, BSMeshLODTriShape)
                Return $"VC={vc} TC={tc} LOD0={s.LOD0Size} LOD1={s.LOD1Size} LOD2={s.LOD2Size} LodSum={s.LOD0Size + s.LOD1Size + s.LOD2Size}"
            Case GetType(BSLODTriShape)
                Dim s = DirectCast(shape, BSLODTriShape)
                Return $"VC={vc} TC={tc} LOD0={s.LOD0Size} LOD1={s.LOD1Size} LOD2={s.LOD2Size} LodSum={s.LOD0Size + s.LOD1Size + s.LOD2Size}"
            Case GetType(BSSegmentedTriShape)
                ' Reflection en _segment via el FieldInfo ya definido en NiTriShapeGeometry
                Dim segList = TryCast(NiTriShapeGeometry.SegmentedSegmentField.GetValue(shape),
                                       List(Of NiflySharp.Structs.BSGeometrySegmentData))
                Dim segCount As Integer = If(segList Is Nothing, 0, segList.Count)
                Return $"VC={vc} TC={tc} Segments={segCount}"
            Case GetType(NiTriStrips)
                ' Reflection sobre el data block via StripNumStripsField ya definido
                Dim d As NiflySharp.Blocks.NiTriBasedGeomData = Nothing
                If shape.DataRef IsNot Nothing AndAlso shape.DataRef.Index >= 0 Then
                    ' NiflySharp's DataRef points into the NIF block list; we approximate by reading
                    ' the count of _numStrips via reflection on a cast NiTriStripsData if available.
                End If
                Return $"VC={vc} TC={tc} (strips: see log)"
            Case Else
                Return $"VC={vc} TC={tc}"
        End Select
    End Function

    Private Shared Function ResultTag(r As TestResult) As String
        Return If(r.Success, "PASS", "FAIL")
    End Function

    ''' <summary>
    ''' Extracts the top 5 frames of an exception's stack trace, one per line (prefixed with
    ''' "  at ") + message.  Gives us the file:line of the actual throw without VS debugger.
    ''' Drops the long noisy top lines (System.Collections.*, System.Reflection.*) so the
    ''' first user-code or NiflySharp frame is visible up front.
    ''' </summary>
    Private Shared Function FormatStackTrace(ex As Exception) As String
        If ex Is Nothing OrElse String.IsNullOrEmpty(ex.StackTrace) Then Return "(no stack)"
        Dim lines = ex.StackTrace.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        Dim filtered = lines.Where(Function(l) Not l.Contains("System.Collections.") AndAlso
                                                Not l.Contains("System.Linq.") AndAlso
                                                Not l.Contains("System.Reflection.")).Take(6).ToList()
        Return String.Join(" | ", filtered.Select(Function(l) l.Trim()))
    End Function

    ''' <summary>
    ''' Returns a short era tag ("SSE", "FO4", "SK", "LE", "Other") for the NIF version.
    ''' Used as part of the cache key so BSTriShape-FO4 and BSTriShape-SSE are validated
    ''' independently (different on-disk layout + different adapter code paths).
    ''' </summary>
    ''' <summary>
    ''' Returns the NIF-block indices of bones referenced by a shape's skin instance.
    ''' Used by Test G (palette lossless after merge).  Works for BSSkin_Instance (FO4) and
    ''' NiSkinInstance (SSE/Oblivion) uniformly via the Bones child list.  Returns an empty
    ''' set for unskinned shapes.
    ''' </summary>
    Private Shared Function GetShapeBoneNifIndices(shape As INiShape, nif As Nifcontent_Class_Manolo) As IEnumerable(Of Integer)
        If shape Is Nothing OrElse shape.SkinInstanceRef Is Nothing OrElse shape.SkinInstanceRef.Index < 0 Then
            Return Array.Empty(Of Integer)()
        End If
        If shape.SkinInstanceRef.Index >= nif.Blocks.Count Then Return Array.Empty(Of Integer)()
        Dim blk = nif.Blocks(shape.SkinInstanceRef.Index)
        Dim niSkin = TryCast(blk, NiSkinInstance)
        If niSkin IsNot Nothing AndAlso niSkin.Bones IsNot Nothing Then
            Return niSkin.Bones.Indices
        End If
        Dim bsSkin = TryCast(blk, BSSkin_Instance)
        If bsSkin IsNot Nothing AndAlso bsSkin.Bones IsNot Nothing Then
            Return bsSkin.Bones.Indices
        End If
        Return Array.Empty(Of Integer)()
    End Function

    Private Shared Function GetEraTag(v As NiVersion) As String
        If v Is Nothing Then Return "Other"
        If v.IsSSE() Then Return "SSE"
        If v.IsFO4() Then Return "FO4"
        If v.IsSK() Then Return "SK"
        If v.IsFO76() Then Return "FO76"
        If v.IsSF() Then Return "SF"
        If v.IsFO3() Then Return "FO3"
        If v.IsOB() Then Return "OB"
        Return "Other"
    End Function

    ' ─── Cache persistence (System.Text.Json) ───

    Private Shared Function LoadCache() As ValidatorCache
        Try
            If Not File.Exists(CacheFilePath) Then Return New ValidatorCache()
            Dim json = File.ReadAllText(CacheFilePath)
            Dim cache = JsonSerializer.Deserialize(Of ValidatorCache)(json)
            If cache Is Nothing Then Return New ValidatorCache()
            Return cache
        Catch
            Return New ValidatorCache()
        End Try
    End Function

    Private Shared Sub SaveCache(cache As ValidatorCache)
        Try
            Dim json = JsonSerializer.Serialize(cache, SaveOptions)
            File.WriteAllText(CacheFilePath, json)
        Catch
            ' cache save failure is non-fatal — validator still ran
        End Try
    End Sub

    Private Shared Sub UpsertCacheEntry(cache As ValidatorCache, entry As ValidatorCacheEntry)
        ' Match on (TypeFullName, GameEra) compound key — BSTriShape-FO4 y BSTriShape-SSE
        ' son entries independientes para que se testeen por separado.
        Dim existing = cache.Entries.FirstOrDefault(
            Function(e) String.Equals(e.TypeFullName, entry.TypeFullName, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(If(e.GameEra, ""), If(entry.GameEra, ""), StringComparison.OrdinalIgnoreCase))
        If existing IsNot Nothing Then
            ' Preservar Status="Validated" si ya estaba así y tests siguen verdes
            If existing.Status = "Validated" AndAlso entry.LastResults.Contains("FAIL") Then
                entry.Status = "Failed"
            ElseIf existing.Status = "Validated" Then
                entry.Status = "Validated"  ' mantener validado
            End If
            cache.Entries.Remove(existing)
        End If
        cache.Entries.Add(entry)
    End Sub

    Private Shared Sub AppendLog(logPath As String, line As String)
        Try
            File.AppendAllText(logPath, line & Environment.NewLine)
        Catch
            ' log write failure is non-fatal
        End Try
    End Sub

End Class
