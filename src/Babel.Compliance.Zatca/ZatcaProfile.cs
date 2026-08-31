using System.Xml.Linq;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca;

/// <summary>
/// <b>كل تفصيلة بروتوكولية تخصّ الهيئة تعيش هنا، ولا تتكرّر في سطر واحد آخر من هذا المشروع.</b>
/// <para/>
/// السبب ليس أناقة: بيئة البناء <b>محجوبة عن الهيئة بالكامل</b> — نطاقات
/// <c>zatca.gov.sa</c> و<c>gw-fatoora.zatca.gov.sa</c> تُعيد <c>403</c> من طبقة الخروج
/// (مقيس في هذا الفرع، ومسجَّل في <c>docs/evidence/verification-debt.md §1.1</c>).
/// فكل قيمة أدناه <b>مستعادة من قراءة المواصفة ومن تنفيذات مفتوحة المصدر، لا من الهيئة</b>.
/// حين تُفتح الشبكة، تُصحَّح <b>هذه الوثيقة وحدها</b>، ولا يُبحث عن القيمة في اثني عشر ملفاً.
/// <para/>
/// <b>ما تُثبته المتجهات الذهبية المرافقة، وما لا تُثبته:</b>
/// <list type="bullet">
///   <item>تُثبت أن التنفيذ <b>حتمي</b> وأن بايتاته لا تتحرّك بترقية ولا بثقافة ولا بترتيب.</item>
///   <item><b>لا تُثبت</b> أن الهيئة تقبلها. متجه ذهبي على ترميز خاطئ يُجمّد الخطأ بدقّة بايتية.</item>
/// </list>
/// </summary>
public static class ZatcaProfile
{
    // ── مساحات الأسماء ───────────────────────────────────────────────────────

    /// <summary>مساحة أسماء فاتورة UBL 2.1.</summary>
    public static readonly XNamespace Invoice =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    public static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public static readonly XNamespace Ext =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public static readonly XNamespace Sig =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonSignatureComponents-2";

    public static readonly XNamespace Sac =
        "urn:oasis:names:specification:ubl:schema:xsd:SignatureAggregateComponents-2";

    public static readonly XNamespace Sbc =
        "urn:oasis:names:specification:ubl:schema:xsd:SignatureBasicComponents-2";

    public static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public static readonly XNamespace Xades = "http://uri.etsi.org/01903/v1.3.2#";

    // ── معرّفات داخل المستند ─────────────────────────────────────────────────

    /// <summary>قيمة <c>cbc:ProfileID</c>. <b>المسارَان يتشاركانها</b> — الفرق في نوع المستند لا هنا.</summary>
    [Provisional("قيمة ProfileID المُصرَّح بها داخل المستند",
        DerivedFrom = "قراءة مواصفة الفاتورة الإلكترونية وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "جدول الحقول الإلزامية في مواصفة الفاتورة الإلكترونية السارية")]
    public const string ProfileId = "reporting:1.0";

    [Provisional("المعرّف الحرفي لعنصر التوقيع داخل cac:Signature",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة الختم التشفيري المنشورة")]
    public const string SignatureId = "urn:oasis:names:specification:ubl:signature:Invoice";

    [Provisional("قيمة SignatureMethod المُصرَّح بها في cac:Signature",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة الختم التشفيري المنشورة")]
    public const string SignatureMethod = "urn:oasis:names:specification:ubl:dsig:enveloped:xades";

    /// <summary>معرّف عنصر الخصائص الموقَّعة. <b>يدخل البايتات الموقَّعة، فتغييره يغيّر كل توقيع.</b></summary>
    public const string SignedPropertiesId = "xadesSignedProperties";

    public const string SignatureElementId = "signature";

    public const string SignatureValueId = "signatureValue";

    // ── الخوارزميات ──────────────────────────────────────────────────────────

    /// <summary>
    /// خوارزمية التوحيد القياسي المُصرَّح بها. <b>هذا الاسم يُكتب في المستند</b>؛
    /// وما يُنفَّذ فعلاً موصوف في <see cref="Canonicalization.ZatcaCanonicalXml"/> ومعه
    /// شرح كامل للفارق بين ما يُصرَّح به وما تنفّذه المنصّة، والحارس الذي يجعلهما متطابقين.
    /// </summary>
    [Provisional("خوارزمية التوحيد القياسي المطلوبة (C14N 1.1 مقابل C14N 1.0)",
        DerivedFrom = "قراءة مواصفة التوقيع — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "قيمة CanonicalizationMethod/@Algorithm في ملف XSLT الرسمي أو مواصفة التوقيع")]
    public const string CanonicalizationAlgorithm = "http://www.w3.org/2006/12/xml-c14n11";

    public const string DigestAlgorithm = "http://www.w3.org/2001/04/xmlenc#sha256";

    [Provisional("معرّف خوارزمية التوقيع المُصرَّح به داخل SignedInfo",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة الختم التشفيري المنشورة")]
    public const string SignatureAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";

    public const string TransformEnveloped = "http://www.w3.org/2000/09/xmldsig#enveloped-signature";

    public const string TransformXPath = "http://www.w3.org/TR/1999/REC-xpath-19991116";

    public const string SignedPropertiesReferenceType = "http://www.w3.org/2000/09/xmldsig#SignatureProperties";

    /// <summary>المنحنى. <b>مقيس على .NET 10.0.111: يعمل على المنصّة القياسية بلا BouncyCastle.</b></summary>
    public const string CurveFriendlyName = "secP256k1";

    /// <summary>اسم الخوارزمية كما يعبر عقد <see cref="ILocalKeyCustodian"/>.</summary>
    public const string CustodianSignatureAlgorithm = "ECDSA-secp256k1";

    public const string CustodianHashAlgorithm = "SHA-256";

    // ── قاعدة الاستبعاد ──────────────────────────────────────────────────────

    /// <summary>
    /// المجموعات الثلاث المستبعدة من بايتات التوقيع، بأسمائها الحرفية في UBL.
    /// <b>عدّاد الفاتورة (ICV) وبصمة الفاتورة السابقة (PIH) ليسا منها</b> — وهذا بالذات
    /// ما يجعل السلسلة رابطة تشفيرياً بدل أن تكون عمودين مجاورين يعيد مالك القاعدة كتابتهما
    /// (‏<c>docs/evidence/traps.md#fakh-decorative-chain-link-outside-the-hash</c>).
    /// </summary>
    public static SigningExclusionRule ExclusionRule { get; } = new(
        ExcludedElementNames: ["UBLExtensions", "Signature"],
        AdditionalDocumentReferenceElement: "AdditionalDocumentReference",
        AdditionalDocumentReferenceIdElement: "ID",
        ExcludedAdditionalDocumentReferenceId: ChainCarryingReferences.QrReferenceId);

    // ── رموز أنواع المستندات ─────────────────────────────────────────────────

    /// <summary>
    /// رمز نوع المستند في <c>cbc:InvoiceTypeCode</c>. القيم من قائمة UN/EDIFACT 1001.
    /// </summary>
    [Provisional("رموز أنواع المستندات المقبولة (388/381/383) وشروط كلٍّ منها",
        DerivedFrom = "قائمة UN/EDIFACT 1001 وقراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "جدول أنواع المستندات في مواصفة الفاتورة الإلكترونية")]
    public static string TypeCodeOf(ComplianceDocumentKind kind) => kind switch
    {
        ComplianceDocumentKind.Invoice => "388",
        ComplianceDocumentKind.CreditNote => "381",
        ComplianceDocumentKind.DebitNote => "383",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "نوع مستند غير معروف / unknown document kind")
    };

    /// <summary>
    /// السمة <c>name</c> على <c>cbc:InvoiceTypeCode</c>: سبع خانات.
    /// الخانتان الأوليان تفصلان <b>المسارين</b>: <c>01</c> قياسية (تُصفَّى قبل الإصدار)،
    /// و<c>02</c> مبسّطة (يُبلَّغ عنها بعد الإصدار). والخانات الخمس الباقية أعلام.
    /// <para/>
    /// <b>هذه هي النقطة التي يقع فيها فخ-37 حرفياً</b>: خانتان في سلسلة نصية تقرّران
    /// اتجاه المسار كله. ولذلك لا تُكتب هنا حرفياً في أي موضع آخر، ويشتقّها
    /// <see cref="Documents.ZatcaFlowPolicy"/> من نفس المصدر الذي يشتقّ منه المسار.
    /// </summary>
    [Provisional("ترتيب الخانات السبع في السمة name ومعنى كل علم فيها",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "جدول رمز نوع الفاتورة والأعلام في مواصفة الفاتورة الإلكترونية")]
    public static string TypeNameOf(ComplianceFlow flow, InvoiceTraits flags) =>
        (flow == ComplianceFlow.Clearance ? "01" : "02")
        + (flags.HasFlag(InvoiceTraits.ThirdParty) ? '1' : '0')
        + (flags.HasFlag(InvoiceTraits.Nominal) ? '1' : '0')
        + (flags.HasFlag(InvoiceTraits.Export) ? '1' : '0')
        + (flags.HasFlag(InvoiceTraits.Summary) ? '1' : '0')
        + (flags.HasFlag(InvoiceTraits.SelfBilled) ? '1' : '0');

    // ── المقياس المالي على السلك ─────────────────────────────────────────────

    /// <summary>
    /// عدد الخانات العشرية التي تُكتب بها المبالغ داخل المستند.
    /// <para/>
    /// <b>وهذا ليس المقياس القانوني للنظام.</b> المقياس القانوني هنا أربع خانات
    /// (‏<c>numeric(19,4)</c>)، والمستند يُكتب بخانتين. الفارق يُعالَج
    /// <b>بالرفض لا بالتقريب</b> في <see cref="Documents.ZatcaAmounts"/>: التقريب قرار
    /// محاسبي يقع قبل هذا الحدّ، لا داخل مُولِّد المستند.
    /// </summary>
    [Provisional("عدد الخانات العشرية المطلوبة للمبالغ داخل المستند",
        DerivedFrom = "قراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "قواعد التنسيق العددي في مواصفة الفاتورة الإلكترونية")]
    public const int DocumentAmountScale = 2;
}

/// <summary>
/// الأعلام الخمسة في السمة <c>name</c>. <b>ليست إعداداً عاماً</b> — تُشتقّ من المستند
/// نفسه أو تُمرَّر صراحةً، ولا تُخمَّن.
/// </summary>
[Flags]
[Provisional("وجود هذه الأعلام الخمسة بالذات وترتيبها ومعنى كل واحد",
    DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
    Risk = ProvisionalRisk.Reworkable,
    VerifyBy = "جدول أعلام نوع الفاتورة في مواصفة الفاتورة الإلكترونية")]
public enum InvoiceTraits
{
    None = 0,
    ThirdParty = 1,
    Nominal = 2,
    Export = 4,
    Summary = 8,
    SelfBilled = 16
}
