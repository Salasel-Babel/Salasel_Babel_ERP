using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Chain;
using Babel.Compliance.Zatca.Documents;
using Babel.Compliance.Zatca.Onboarding;
using Babel.Compliance.Zatca.Qr;
using Babel.Compliance.Zatca.Signing;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>متجه واحد: معرّفه، وما يقيسه بالعربية، وناتجه.</summary>
internal sealed record ZatcaVector(string Id, string DescriptionAr, Func<ZatcaVectorResult> Execute);

internal sealed record ZatcaVectorResult(
    string? Text = null,
    string? BytesHex = null,
    string? Sha256 = null,
    string? Note = null);

/// <summary>
/// <b>المتجهات الذهبية لمسار الهيئة — وحدود ما تُثبته مكتوبة هنا لا في تقرير.</b>
/// <para/>
/// <b>ما تُثبته:</b> أن كل بايت يخرج من هذا المزوّد <b>حتمي</b>؛ لا يتحرّك بترقية حزمة،
/// ولا بثقافة نظام، ولا بترتيب تعداد، ولا بإعادة بناء. وهذا هو الشرط الذي بلا تحققه
/// تصير كل مقارنة لاحقة عبثاً.
/// <para/>
/// <b>ما لا تُثبته، ولا تدّعيه:</b> أن الهيئة تقبل هذه البايتات. بيئة البناء محجوبة عن
/// الهيئة (‏403 مقيس)، فلا مرجع يُقارَن به. <b>ومتجه ذهبي مبنيّ على ترميز خاطئ يُجمّد
/// الخطأ بدقّة بايتية ويحرسه من التصحيح</b> — ولذلك كل متجه هنا مقرون ببند في
/// <c>docs/evidence/verification-debt.md</c> يقول ما الذي يُغلقه.
/// <para/>
/// <b>وما هو خارج المتجهات عمداً:</b> كل ما يعتمد على مفتاح أو شهادة. لا مفتاح خاص في
/// هذا المستودع بحال، فالتوقيع يُولَّد وقت التشغيل ويُتحقَّق منه <b>تشفيرياً</b> في
/// <see cref="ZatcaSignatureTests"/> بدل أن يُثبَّت بايتياً.
/// </summary>
internal static class ZatcaGoldenSet
{
    /// <summary>معرّف المجموعة. أي تغيير في التمثيل يوجب رقماً جديداً ومجموعة جديدة.</summary>
    public const string FormatVersion = "zatca.v1";

    public const string FileName = "zatca-vectors.v1.json";

    private static readonly UTF8Encoding Utf8 = new(false);

    private static ZatcaDocumentRenderer Renderer { get; } = new(ZatcaFixtures.Seller);

    public static IReadOnlyList<ZatcaVector> All { get; } = Build();

    private static List<ZatcaVector> Build()
    {
        List<ZatcaVector> vectors = [];

        void Add(string id, string ar, Func<ZatcaVectorResult> execute) =>
            vectors.Add(new ZatcaVector(id, ar, execute));

        // ── (1) المستند: بايتات قانونية وبصمة ────────────────────────────────
        Add("standard.body.canonical",
            "الفاتورة القياسية كاملةً بعد التوحيد القياسي، بمواضع التوقيع وQR فارغة",
            () => Bytes(Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).Body));

        Add("standard.signing.input",
            "بايتات التوقيع للفاتورة القياسية: بعد استبعاد المجموعات الثلاث وقبل الختم",
            () => Bytes(Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).SigningInput));

        Add("standard.invoice.hash",
            "بصمة الفاتورة القياسية — وهي ما يدخل مرجع SignedInfo الأول ووسم QR السادس وجسم الإرسال",
            () => new ZatcaVectorResult(
                Text: ZatcaDocumentRenderer.InvoiceHashBase64(
                    Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).SigningInputDigest.Span),
                Note: "base64 لبايتات البصمة الخام — لا للنصّ الستّ‌عشري"));

        Add("simplified.body.canonical",
            "الفاتورة المبسّطة كاملةً — بلا مشتري، والسمة name تبدأ بـ02",
            () => Bytes(Renderer.Render(ZatcaFixtures.Simplified(), ZatcaFixtures.Slot(2)).Body));

        Add("simplified.invoice.hash",
            "بصمة الفاتورة المبسّطة — تختلف عن القياسية لأن نوع المستند ومساره داخل البايتات",
            () => new ZatcaVectorResult(
                Text: ZatcaDocumentRenderer.InvoiceHashBase64(
                    Renderer.Render(ZatcaFixtures.Simplified(), ZatcaFixtures.Slot(2)).SigningInputDigest.Span)));

        Add("domain.chain.digest.equals.invoice.hash",
            "‼ سلسلتنا هي سلسلة الهيئة نفسها: DomainChainDigest يساوي SigningInputDigest",
            () =>
            {
                RenderedDocument rendered = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2));
                bool same = rendered.DomainChainDigest.Span.SequenceEqual(rendered.SigningInputDigest.Span);
                return new ZatcaVectorResult(
                    Text: same ? "identical" : "different",
                    Note: "سلسلة ثانية مستقلة تعني سلسلة تتحقّق عندنا وليست التي تفحصها الجهة");
            });

        // ── (2) السلسلة: العدّاد والبصمة السابقة ─────────────────────────────
        Add("chain.pih.first.document",
            "بصمة الفاتورة السابقة للمستند الأول: بذرة ثابتة منشورة، لا بصمة تكويننا",
            () => new ZatcaVectorResult(
                Text: ZatcaChain.PreviousInvoiceHash(ZatcaFixtures.Slot(1)),
                Note: "هذه هي الفجوة الوحيدة بين سلسلتنا وسلسلة الهيئة، وهي عند العدّاد 1 وحده"));

        Add("chain.pih.second.document",
            "بصمة الفاتورة السابقة للمستند الثاني: base64 لبايتات البصمة السابقة كما هي",
            () => new ZatcaVectorResult(Text: ZatcaChain.PreviousInvoiceHash(ZatcaFixtures.Slot(2))));

        Add("chain.icv.is.inside.the.signed.bytes",
            "‼ تغيير العدّاد وحده يغيّر بصمة الفاتورة — أي أن العدّاد داخل البايتات لا بجوارها",
            () =>
            {
                byte[] one = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).SigningInputDigest.ToArray();
                byte[] two = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(3)).SigningInputDigest.ToArray();
                return new ZatcaVectorResult(
                    Text: one.AsSpan().SequenceEqual(two) ? "unchanged" : "changed",
                    Note: "«unchanged» هنا تعني سلسلة زخرفية: رابط خارج البايتات المُجزَّأة");
            });

        Add("chain.pih.is.inside.the.signed.bytes",
            "‼ تغيير البصمة السابقة وحدها يغيّر بصمة الفاتورة",
            () =>
            {
                byte[] a = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2, 0xAB)).SigningInputDigest.ToArray();
                byte[] b = Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2, 0xCD)).SigningInputDigest.ToArray();
                return new ZatcaVectorResult(Text: a.AsSpan().SequenceEqual(b) ? "unchanged" : "changed");
            });

        // ── (3) قاعدة الاستبعاد ──────────────────────────────────────────────
        Add("exclusion.removes.exactly.three.node.sets",
            "تحويل التوقيع يزيل ثلاث مجموعات عقد بالضبط: الامتدادات، وعنصر التوقيع، ومرجع QR",
            () =>
            {
                XElement tree = ZatcaDocumentRenderer.Parse(
                    Renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)).Body.Span);
                SigningTransformResult result = Renderer.Transform.Apply(tree);
                return new ZatcaVectorResult(
                    Text: result.ExcludedNodeSets.ToString(CultureInfo.InvariantCulture),
                    Note: "أي رقم غير 3 خطأ في القراءة أو خطأ في الإعداد");
            });

        // ── (4) رمز QR ───────────────────────────────────────────────────────
        Add("qr.phase1.tlv",
            "رمز المرحلة الأولى: خمسة وسوم بترتيبها، بلا بصمة ولا توقيع ولا مفتاح عام",
            () => new ZatcaVectorResult(Text: ZatcaQr.Phase1(
                ZatcaFixtures.SellerParty.NameAr,
                ZatcaFixtures.SellerParty.TaxRegistrationNumber!,
                ZatcaFixtures.IssuedAt,
                1350.00m,
                150.00m)));

        Add("qr.phase1.tag.lengths",
            "أطوال الوسوم الخمسة بالبايت — الاسم العربي يكلّف ضعف حروفه تقريباً",
            () =>
            {
                IReadOnlyList<QrTag> tags = ZatcaQr.Decode(ZatcaQr.Phase1(
                    ZatcaFixtures.SellerParty.NameAr,
                    ZatcaFixtures.SellerParty.TaxRegistrationNumber!,
                    ZatcaFixtures.IssuedAt, 1350.00m, 150.00m));
                return new ZatcaVectorResult(
                    Text: string.Join(",", tags.Select(t =>
                        string.Create(CultureInfo.InvariantCulture, $"{t.Tag}:{t.Value.Length}"))));
            });

        Add("qr.timestamp",
            "الطابع الزمني داخل الرمز: ثقافة ثابتة و UTC صريح، لا تقويم محلي",
            () => new ZatcaVectorResult(Text: ZatcaQr.Timestamp(ZatcaFixtures.IssuedAt)));

        // ── (5) توحيد قياسي لجزء مع بيئته ────────────────────────────────────
        Add("c14n.subtree.inherits.ancestor.namespaces",
            "‼ توحيد جزء من مستند يكتب على العنصر الأعلى كل تصريحات مساحات الأسماء السارية عليه",
            () =>
            {
                ZatcaCanonicalXml c14n = new();
                XNamespace a = "urn:example:a";
                XNamespace b = "urn:example:b";
                XElement inner = new(b + "Inner", new XElement(b + "Value", "قيمة"));
                XElement outer = new(a + "Outer",
                    new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "b", b.NamespaceName),
                    inner);
                _ = outer;
                return new ZatcaVectorResult(
                    Text: Utf8.GetString(c14n.CanonicaliseInScope(inner)),
                    Note: "لو حُسبت البصمة على نسخة مقتطعة لنقصت التصريحات ولاختلفت البصمة تماماً");
            });

        Add("c14n.subtree.detached.differs",
            "المقارنة المقابلة: النسخة المقتطعة تعطي بايتات أخرى — وهذا هو مصدر العطل",
            () =>
            {
                ZatcaCanonicalXml c14n = new();
                XNamespace a = "urn:example:a";
                XNamespace b = "urn:example:b";
                XElement inner = new(b + "Inner", new XElement(b + "Value", "قيمة"));
                XElement outer = new(a + "Outer",
                    new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "b", b.NamespaceName),
                    inner);
                _ = outer;
                return new ZatcaVectorResult(Text: Utf8.GetString(c14n.Canonicalise(new XElement(inner))));
            });

        Add("c14n.empty.element.is.expanded",
            "الوسم الفارغ يُكتب مفتوحاً ومغلقاً — سلوك C14N الذي يخالف أي تسلسل ساذج",
            () => new ZatcaVectorResult(
                Text: Utf8.GetString(new ZatcaCanonicalXml().Canonicalise(
                    XElement.Parse("<Root xmlns=\"urn:x\"><Empty/><Text>نصّ</Text></Root>")))));

        // ── (6) الخصائص الموقَّعة: الشكل بلا مفتاح ───────────────────────────
        Add("xades.signed.properties.canonical.shape",
            "شكل الخصائص الموقَّعة بقيم ثابتة مصطنعة — يُثبّت البنية والترميز بلا أي مادة مفتاح",
            () =>
            {
                XElement properties = SyntheticSignedProperties();
                return Bytes(new ZatcaCanonicalXml().CanonicaliseInScope(properties));
            });

        Add("digest.encoding.raw.vs.hex",
            "‼ الفرق بين ترميزَي البصمة: 44 محرفاً مقابل 88 — والخلط بينهما يُرفض عند الجهة وحدها",
            () =>
            {
                byte[] digest = SHA256.HashData(Utf8.GetBytes("سلاسل بابل"));
                string raw = ZatcaDigests.Render(digest, DigestEncoding.RawDigestBase64);
                string hex = ZatcaDigests.Render(digest, DigestEncoding.HexDigestBase64);
                return new ZatcaVectorResult(Text: raw + "|" + hex);
            });

        Add("digest.policy.default.is.asymmetric",
            "‼ الافتراضي غير متماثل عمداً: مرجع الفاتورة خام، والخصائص والشهادة ستّ‌عشرياً",
            () => new ZatcaVectorResult(Text: string.Create(CultureInfo.InvariantCulture,
                $"{ZatcaDigestPolicy.Default.InvoiceReference}|" +
                $"{ZatcaDigestPolicy.Default.SignedPropertiesReference}|" +
                $"{ZatcaDigestPolicy.Default.CertificateDigest}")));

        Add("binary.security.token.is.double.base64",
            "رمز الأمان الثنائي: base64 فوق base64 فوق DER — دورتا فكّ ترميز",
            () => new ZatcaVectorResult(
                Text: ZatcaDigests.BinarySecurityToken([0x30, 0x03, 0x02, 0x01, 0x05]),
                Note: "فكّ دورة واحدة يعطي نصّاً يبدو معقولاً ويفشل لاحقاً"));

        // ── (7) الأرقام والمسار ──────────────────────────────────────────────
        Add("amounts.scale.two",
            "المبالغ داخل المستند بخانتين، بثقافة ثابتة",
            () => new ZatcaVectorResult(Text: string.Join("|",
                ZatcaAmounts.Render(1350.00m, "t"),
                ZatcaAmounts.Render(1350.0000m, "t"),
                ZatcaAmounts.Render(0m, "t"),
                ZatcaAmounts.Render(1000000000000.50m, "t"))));

        Add("invoice.type.name.standard",
            "السمة name للفاتورة القياسية: تبدأ بـ01 أي مسار مقاصة",
            () => new ZatcaVectorResult(Text: ZatcaProfile.TypeNameOf(ComplianceFlow.Clearance, InvoiceTraits.None)));

        Add("invoice.type.name.simplified",
            "السمة name للفاتورة المبسّطة: تبدأ بـ02 أي مسار إبلاغ",
            () => new ZatcaVectorResult(Text: ZatcaProfile.TypeNameOf(ComplianceFlow.Reporting, InvoiceTraits.None)));

        Add("invoice.type.name.flags",
            "الأعلام الخمسة بترتيبها: طرف ثالث، صوري، تصدير، مجمَّع، فوترة ذاتية",
            () => new ZatcaVectorResult(Text: ZatcaProfile.TypeNameOf(
                ComplianceFlow.Reporting,
                InvoiceTraits.ThirdParty | InvoiceTraits.Export | InvoiceTraits.SelfBilled)));

        Add("invoice.type.codes",
            "رموز أنواع المستندات الثلاثة",
            () => new ZatcaVectorResult(Text: string.Join("|",
                ZatcaProfile.TypeCodeOf(ComplianceDocumentKind.Invoice),
                ZatcaProfile.TypeCodeOf(ComplianceDocumentKind.CreditNote),
                ZatcaProfile.TypeCodeOf(ComplianceDocumentKind.DebitNote))));

        Add("signing.time.format",
            "وقت التوقيع: ثانية بلا كسور، و UTC صريح",
            () => new ZatcaVectorResult(Text: XadesSignatureWriter.SigningTimestamp(ZatcaFixtures.IssuedAt)));

        Add("csr.rdn.order",
            "ترتيب معرّفات RDN داخل الاسم البديل — مفروض بقائمة معلنة لا بترتيب قاموس",
            () => new ZatcaVectorResult(Text: string.Join("|", ZatcaCertificateRequest.RdnOrder)));

        Add("csr.template.per.environment",
            "اسم قالب الشهادة لكل بيئة — بيئتان منفصلتان تماماً",
            () => new ZatcaVectorResult(Text: string.Join("|",
                ZatcaCertificateRequest.TemplateFor(ComplianceEnvironment.Simulation),
                ZatcaCertificateRequest.TemplateFor(ComplianceEnvironment.Production))));

        Add("idempotency.key.shape",
            "مفتاح الإحكام من جانبنا: ثابت عبر المحاولات لأنه مشتقّ من هوية المستند وموضعه",
            () => new ZatcaVectorResult(Text: string.Create(CultureInfo.InvariantCulture,
                $"{ZatcaFixtures.Unit.Value}:2:{ZatcaFixtures.StandardUuid:D}")));

        return vectors;
    }

    /// <summary>
    /// خصائص موقَّعة بقيم <b>مصطنعة وثابتة</b>: لا شهادة، ولا مفتاح، ولا وقت تشغيل.
    /// غرضها تثبيت <b>البنية والترميز ووراثة مساحات الأسماء</b> — وهي الأجزاء التي
    /// لا تعتمد على المفتاح، وهي بالضبط الأجزاء التي يقع فيها العطل.
    /// </summary>
    private static XElement SyntheticSignedProperties()
    {
        XNamespace ds = ZatcaProfile.Ds;
        XNamespace xades = ZatcaProfile.Xades;

        XElement properties = new(xades + "SignedProperties",
            new XAttribute("Id", ZatcaProfile.SignedPropertiesId),
            new XElement(xades + "SignedSignatureProperties",
                new XElement(xades + "SigningTime", XadesSignatureWriter.SigningTimestamp(ZatcaFixtures.IssuedAt)),
                new XElement(xades + "SigningCertificate",
                    new XElement(xades + "Cert",
                        new XElement(xades + "CertDigest",
                            new XElement(ds + "DigestMethod", new XAttribute("Algorithm", ZatcaProfile.DigestAlgorithm)),
                            new XElement(ds + "DigestValue", "0000000000000000000000000000000000000000000000000000000000000000")),
                        new XElement(xades + "IssuerSerial",
                            new XElement(ds + "X509IssuerName", "CN=babel-test-issuer, C=SA"),
                            new XElement(ds + "X509SerialNumber", "1234567890"))))));

        // بيئة أسلاف مصطنعة تحاكي موضع العنصر داخل المستند.
        XElement obj = new(ds + "Object",
            new XAttribute(XNamespace.Xmlns + "ds", ds.NamespaceName),
            new XElement(xades + "QualifyingProperties",
                new XAttribute(XNamespace.Xmlns + "xades", xades.NamespaceName),
                new XAttribute("Target", ZatcaProfile.SignatureElementId),
                properties));
        _ = obj;

        return properties;
    }

    private static ZatcaVectorResult Bytes(ReadOnlyMemory<byte> bytes) => Bytes(bytes.ToArray());

    private static ZatcaVectorResult Bytes(byte[] bytes) => new(
        Text: Utf8.GetString(bytes),
        Sha256: Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}

/// <summary>قراءة الملف الذهبي وكتابته ومقارنته. المقارنة تعيد <b>كل</b> الانحرافات لا أوّلها.</summary>
internal static class ZatcaGoldenFile
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public sealed record Drift(string Id, string Field, string Expected, string Actual);

    public static string Emit(IReadOnlyList<ZatcaVector> vectors)
    {
        JsonArray array = [];

        foreach (ZatcaVector vector in vectors)
        {
            ZatcaVectorResult result = vector.Execute();
            JsonObject entry = new()
            {
                ["id"] = vector.Id,
                ["ar"] = vector.DescriptionAr
            };

            if (result.Text is not null) entry["text"] = result.Text;
            if (result.BytesHex is not null) entry["bytes_hex"] = result.BytesHex;
            if (result.Sha256 is not null) entry["sha256"] = result.Sha256;
            if (result.Note is not null) entry["note"] = result.Note;

            array.Add(entry);
        }

        string vectorsJson = array.ToJsonString(Options);
        string manifest = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(vectorsJson))).ToLowerInvariant();

        JsonObject root = new()
        {
            ["_"] = "متجهات ذهبية لمسار الهيئة — لا تُحرَّر يدوياً. تُولَّد بـBABEL_ZATCA_GOLDEN_EMIT=1 وتُفحص في كل بناء.",
            ["_تحذير"] = "هذه المتجهات تُثبت الحتمية، لا القبول لدى الهيئة. بيئة البناء محجوبة عنها (403 مقيس).",
            ["format_version"] = ZatcaGoldenSet.FormatVersion,
            ["hash_algorithm"] = "SHA-256",
            ["encoding"] = "UTF-8 without BOM",
            ["vector_count"] = vectors.Count,
            ["manifest_sha256"] = manifest,
            ["vectors"] = JsonNode.Parse(vectorsJson)
        };

        return root.ToJsonString(Options) + "\n";
    }

    public static IReadOnlyList<Drift> Verify(string storedJson, IReadOnlyList<ZatcaVector> vectors)
    {
        List<Drift> drifts = [];
        JsonObject root = JsonNode.Parse(storedJson)!.AsObject();

        void Compare(string id, string field, string? expected, string? actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                drifts.Add(new Drift(id, field, expected ?? "(none)", actual ?? "(none)"));
            }
        }

        Compare("_meta", "format_version", root["format_version"]?.GetValue<string>(), ZatcaGoldenSet.FormatVersion);
        Compare("_meta", "vector_count",
            root["vector_count"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture),
            vectors.Count.ToString(CultureInfo.InvariantCulture));

        // البصمة المُودَعة تُعاد من الملف نفسه: تحرير يدوي يغيّر متجهاً ويترك البصمة
        // كما هي يُكشف هنا، لا عند أول إرسال حقيقي.
        JsonNode stored = root["vectors"]!;
        string recomputed = Convert.ToHexString(
            SHA256.HashData(Utf8.GetBytes(stored.ToJsonString(Options)))).ToLowerInvariant();
        Compare("_meta", "manifest_sha256", root["manifest_sha256"]?.GetValue<string>(), recomputed);

        Dictionary<string, JsonNode?> byId = stored.AsArray()
            .ToDictionary(node => node!["id"]!.GetValue<string>(), node => node, StringComparer.Ordinal);

        foreach (ZatcaVector vector in vectors)
        {
            if (!byId.TryGetValue(vector.Id, out JsonNode? node))
            {
                drifts.Add(new Drift(vector.Id, "missing", "(present in file)", "(absent)"));
                continue;
            }

            ZatcaVectorResult result = vector.Execute();
            Compare(vector.Id, "text", node!["text"]?.GetValue<string>(), result.Text);
            Compare(vector.Id, "sha256", node["sha256"]?.GetValue<string>(), result.Sha256);
        }

        return drifts;
    }

    /// <summary>يبحث صعوداً عن الملف. في worktree يكون ".git" ملفاً لا مجلداً، فلا يُعتمد عليه.</summary>
    public static string Path()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, "tests", "golden", ZatcaGoldenSet.FileName);
            if (File.Exists(candidate)) return candidate;

            string marker = System.IO.Path.Combine(directory.FullName, "Babel.slnx");
            if (File.Exists(marker))
            {
                return System.IO.Path.Combine(directory.FullName, "tests", "golden", ZatcaGoldenSet.FileName);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"تعذّر تحديد موضع tests/golden/{ZatcaGoldenSet.FileName} صعوداً من {AppContext.BaseDirectory}");
    }
}
