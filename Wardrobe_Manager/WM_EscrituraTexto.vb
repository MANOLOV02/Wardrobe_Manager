' Version Uploaded of Wardrobe 3.2.0

''' <summary>Los sidecars de texto de WM, escritos por la ley de <c>EscrituraEnElLugar</c> y no por
''' <c>File.CreateText</c> / <c>File.WriteAllText</c>.
''' <para>⛔ POR QUÉ EXISTE ESTE MÓDULO Y NO SE LLAMA A LA LIBRERÍA DIRECTO EN CADA SITIO. Son ocho
''' sidecars de texto (el .txt de tacones del build, el .hht del proyecto, los dos .xml de física
''' HDT-SMP, el sliders.json de LooksMenu, el export SAM, el .osp recién creado y el group XML del
''' build) y todos necesitan exactamente lo mismo: UTF-8 sin BOM, y el <c>leaveOpen:=True</c> del
''' contrato del cuerpo. Repetir el lambda ocho veces es repetir ocho veces la oportunidad de
''' olvidarse el <c>leaveOpen</c> — que es el defecto que ya se llevó puesta la app una vez
''' (<c>OSD_Class.Save_As</c>, documentado en la cabecera de <c>EscribirNucleo</c>).</para>
''' <para>⛔ LA LEY NO SE REPITE ACÁ: vive en <c>Ba2_Bsa_Library\EscrituraEnElLugar.vb</c> — por qué no
''' se usa CREATE_ALWAYS (el atributo OCULTO que dejan OneDrive y los desempaquetadores da
''' ACCESS_DENIED), por qué no hay temporal ni rename (el VFS de MO2 y el hardlink de Vortex), y cuál
''' es la diferencia entre <c>Escribir</c> y <c>GuardarConCopia</c>. Acá sólo se elige entre las dos.</para>
''' <para><b>Cómo se elige</b>, y es la única decisión que este módulo toma: <paramref name="conCopia"/>
''' False para la SALIDA REGENERABLE (lo que el build rehace en la corrida siguiente) y True para el
''' DATO DEL USUARIO (lo que, si se pierde, no vuelve). Es la misma partición que ya declara el
''' docstring de <c>Escribir</c>.</para></summary>
Friend Module WM_EscrituraTexto

    ''' <summary>Escribe <paramref name="texto"/> en <paramref name="destino"/> como UTF-8.
    ''' <para>⛔⛔ <paramref name="conBom"/> ES OBLIGATORIO DE MIRAR EN CADA SITIO, Y NO TIENE UN DEFAULT
    ''' "SANO". Los dos primitivos que este módulo reemplaza NO coinciden entre sí:</para>
    ''' <list type="bullet">
    ''' <item><c>File.CreateText</c> y <c>File.WriteAllText(path, texto)</c> escriben <c>UTF8NoBOM</c>
    ''' ⇒ <c>conBom:=False</c>.</item>
    ''' <item><c>File.WriteAllText(path, texto, Encoding.UTF8)</c> escribe CON BOM, porque
    ''' <c>Encoding.UTF8</c> tiene <c>encoderShouldEmitUTF8Identifier = True</c> ⇒ <c>conBom:=True</c>.</item>
    ''' </list>
    ''' <para>Cada llamador pasa el que tenía, así que el cambio es neutro en bytes POR CONSTRUCCIÓN.
    ''' ⛔ No unificar los dos por prolijidad: son archivos que leen terceros (HDT-SMP, LooksMenu, HHS) y
    ''' un cambio de bytes en un artefacto del juego lo decide el usuario, no este módulo.</para>
    ''' <para>Los saltos de línea los pone el llamador: <c>WriteLine</c> agregaba <c>vbCrLf</c> y los
    ''' llamadores lo conservan explícito.</para>
    ''' <para>⛔ <c>leaveOpen:=True</c> NO ES OPCIONAL: el cuerpo NO es dueño del stream. Sin esto el
    ''' <c>End Using</c> del StreamWriter CIERRA el FileStream que abrió <c>EscribirNucleo</c>, y el
    ''' <c>Flush(True)</c> posterior revienta con ObjectDisposedException. La guarda del contrato de
    ''' EscrituraEnElLugar lo detecta y falla diciendo qué se rompió, pero acá directamente no pasa.</para></summary>
    ''' <param name="lote">OPCIONAL y sólo relevante con <paramref name="conCopia"/>: cuando este texto es
    ''' una ETAPA del guardado de un proyecto (el <c>.hht</c> de tacones, el <c>.xml</c> de física SMP), su
    ''' copia no se borra al terminar ESTE archivo sino al confirmarse el CONJUNTO — así un fallo en una
    ''' etapa posterior puede devolver también a ésta. Con <c>Nothing</c> (el default) la conducta es
    ''' EXACTAMENTE la de antes. Ver <c>EscrituraEnElLugar.NuevoLote</c>.</param>
    Friend Sub EscribirTextoUtf8(destino As String, texto As String, conCopia As Boolean, conBom As Boolean,
                                 Optional lote As BSA_BA2_Library_DLL.EscrituraEnElLugar.LoteConCopias = Nothing)
        Dim enc As New Text.UTF8Encoding(encoderShouldEmitUTF8Identifier:=conBom)
        Dim cuerpo As Action(Of IO.Stream) =
            Sub(fs)
                Using w As New IO.StreamWriter(fs, enc, 1024, leaveOpen:=True)
                    w.Write(If(texto, ""))
                    w.Flush()
                End Using
            End Sub

        If conCopia Then
            BSA_BA2_Library_DLL.EscrituraEnElLugar.GuardarConCopia(destino, cuerpo, lote)
        Else
            BSA_BA2_Library_DLL.EscrituraEnElLugar.Escribir(destino, cuerpo)
        End If
    End Sub

    ''' <summary>Guarda un <c>XDocument</c> por la misma ley. Es el reemplazo de <c>doc.Save(path)</c>,
    ''' que abre el destino con CREATE_ALWAYS por adentro.
    ''' <para>⛔⛔ SE SERIALIZA A <b>BYTES</b>, NO A TEXTO, Y ESO NO ES UN DETALLE. La versión anterior
    ''' de esta función serializaba con <c>XmlWriter.Create(StringBuilder)</c> y escribía el String como
    ''' UTF-8 sin BOM. Un <c>XmlWriter</c> sobre un destino de TEXTO declara el encoding DEL WRITER —un
    ''' <c>StringWriter</c> es UTF-16— y el <c>.Encoding</c> de las settings se IGNORA, así que la
    ''' declaración salía <c>encoding="utf-16"</c> arriba de bytes UTF-8. MEDIDO sobre net8: el archivo
    ''' resultante NO LO ABRE NADIE — <c>XDocument.Load</c> tira <i>"There is no Unicode byte order mark.
    ''' Cannot switch to Unicode."</i>—, y como <c>GuardarConCopia</c> borra su copia al salir BIEN, el
    ''' preset del usuario quedaba ilegible y sin respaldo. Escribiendo sobre un <c>MemoryStream</c> el
    ''' writer conoce el encoding real y emite la declaración —y el BOM— que le corresponde.</para>
    ''' <para>⛔ LA VARA ES <c>doc.Save(path)</c>, BYTE POR BYTE, y por eso las settings se derivan igual
    ''' que ahí y no "a gusto": <c>XDocument.Save(String)</c> arma un <c>XmlWriterSettings</c> con
    ''' <c>Indent = True</c> y, si el documento trae <c>XDeclaration</c> con encoding, le pisa el
    ''' <c>.Encoding</c> con <c>Encoding.GetEncoding(&lt;ese nombre&gt;)</c>. Lo que emite —MEDIDO, no
    ''' deducido— es <b>BOM EF BB BF + <c>encoding="utf-8"</c></b>, y la declaración va SIEMPRE, también
    ''' cuando el documento no tiene <c>XDeclaration</c> (<c>WriteTo</c> llama a <c>WriteStartDocument</c>
    ''' igual). Por eso acá NO se toca <c>OmitXmlDeclaration</c>: ponerlo en True cuando la declaración
    ''' falta era una segunda divergencia contra el escritor viejo. El docstring anterior decía "SIN BOM,
    ''' que es lo que emitía XDocument.Save(String)" y eso era FALSO.
    ''' <br/>⚠️ <b>ALCANCE DE ESA EQUIVALENCIA, dicho y no dado por universal:</b> vale para
    ''' <c>doc.Save(path)</c> con <c>SaveOptions.None</c> —el default, y el único que usaban los tres
    ''' sitios que esto reemplaza— y sobre documentos sin anotaciones de formato.
    ''' <c>SaveOptions.DisableFormatting</c> apaga el <c>Indent</c> y
    ''' <c>SaveOptions.OmitDuplicateNamespaces</c> agrega <c>NamespaceHandling</c>; ninguna de las dos es
    ''' alcanzable desde acá porque esta función no toma <c>SaveOptions</c>. Si alguna vez hay que
    ''' aceptarlas, se propagan a estas mismas settings — no se asume que el resultado sigue siendo el
    ''' mismo.
    ''' ⛔ Cambiar los bytes de un preset o de una pose lo decide el usuario, no este módulo: los tres
    ''' llamadores (presets y poses del editor, poses del formulario) reemplazan un <c>doc.Save(path)</c>.
    ''' Gate: <c>Tools\WmEscrituraGate</c> B8.1/B8.2 corre los DOS escritores sobre el mismo
    ''' <c>XDocument</c> y compara los bytes —la referencia se mide, no se cablea—, y B9 repite el
    ''' round-trip sobre presets y poses REALES commiteados en <c>Tools\WmEscrituraGate\fixtures</c>,
    ''' incluido uno SIN declaración, que es el caso que divergía.</para>
    ''' <para>⛔ SE SERIALIZA ENTERO ANTES DE TOCAR EL DISCO, a propósito. <c>XDocument.Save(Stream)</c>
    ''' escribiría directo, pero entonces un fallo de serialización (un nodo inválido) saldría DESPUÉS del
    ''' <c>SetLength(0)</c> y dejaría el archivo del usuario truncado. Serializando antes, cualquier error
    ''' ocurre con el destino todavía intacto — y si igual algo falla escribiendo,
    ''' <c>GuardarConCopia</c> restaura desde su copia.</para>
    ''' <para>⛔ Y NO pasa por <see cref="EscribirTextoUtf8"/>: ese escribe TEXTO con un encoding que elige
    ''' el llamador, y acá el encoding —y el BOM— los decide la declaración del documento. El cuerpo no
    ''' envuelve el stream en nada, así que la trampa del <c>leaveOpen</c> ni se presenta.</para></summary>
    ''' <para>⛔⛔ LA DERIVACIÓN YA NO VIVE ACÁ: DELEGA EN <c>FO4_Base_Library.EscrituraXml</c>. Esta
    ''' función tenía una copia privada e idéntica de las settings, y el 2026-09-02 apareció una SEGUNDA
    ''' casa de la misma ley en la librería compartida porque NPC_Manager escribe el MISMO
    ''' <c>WardrobeManagerPoses.xml</c>. Dos derivaciones del mismo archivo es exactamente la forma de que
    ''' un día diverjan y los bytes del preset del usuario dependan de QUIÉN lo guardó. Se queda UNA, la de
    ''' la librería; acá sobrevive el docstring porque es donde está escrito POR QUÉ las settings son las
    ''' que son. El envoltorio se conserva —no se reemplazan los llamadores por la llamada directa— para
    ''' que Wardrobe_Manager siga teniendo un solo punto de entrada de escritura de texto/XML.</para></summary>
    Friend Sub GuardarXDocumentConCopia(destino As String, doc As XDocument)
        FO4_Base_Library.EscrituraXml.GuardarXDocumentConCopia(destino, doc)
    End Sub

End Module
