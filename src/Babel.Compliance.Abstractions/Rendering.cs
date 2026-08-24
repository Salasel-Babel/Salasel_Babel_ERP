namespace Babel.Compliance.Abstractions;

/// <summary>
/// المستند بعد بنائه بالشكل الذي تفهمه الجهة، مع البايتات المقصودة بالتوقيع.
/// <para/>
/// <b>ثلاث مجموعات بايتات مختلفة، ولا يجوز الخلط بينها أبداً:</b>
/// <list type="number">
///   <item><see cref="Body"/> — المستند كاملاً كما بُني.</item>
///   <item><see cref="SigningInput"/> — البايتات بعد استبعاد المجموعات الثلاث وتوحيدها قياسياً.</item>
///   <item><see cref="DomainChainDigest"/> — بصمتنا نحن على الحقيقة المجالية، لغرض سلسلتنا ومطابقتنا.</item>
/// </list>
/// الخلط بين (2) و(3) هو أسرع طريق إلى سلسلة تتحقق محلياً وتُرفض عند الجهة.
/// </summary>
public sealed record RenderedDocument(
    ComplianceDocumentId DocumentId,
    [property: Provisional("معرّف ملف التعريف/الإصدار الذي يُصرَّح به داخل المستند",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "قيمة ProfileID المطلوبة في مواصفة الفاتورة الإلكترونية")]
    string ProfileId,
    ReadOnlyMemory<byte> Body,
    string MediaType,
    ReadOnlyMemory<byte> SigningInput,
    ReadOnlyMemory<byte> SigningInputDigest,
    ReadOnlyMemory<byte> DomainChainDigest,
    ChainSlot Chain)
{
    public string DomainChainDigestHex => Convert.ToHexString(DomainChainDigest.Span).ToLowerInvariant();
}

/// <summary>
/// يبني تمثيل المستند. تنفيذ واحد في الإنتاج، ولا مسار ثانٍ.
/// <para/>
/// <b>«اشترِ ولا تكتب» (02-architecture §12):</b> توليد UBL والتوحيد القياسي لـXML وترميز QR
/// كلها بنود «تبدو أسبوعاً وتصير ستة أشهر». التنفيذ المرافق هنا <b>مؤقَّت للتشغيل والاختبار</b>،
/// لا لأنه تقدير للجهد الحقيقي.
/// </summary>
public interface IDocumentRenderer
{
    RenderedDocument Render(ComplianceDocument document, ChainSlot chain);
}

/// <summary>
/// قاعدة الاستبعاد قبل التوحيد القياسي.
/// <para/>
/// <b>مُتحقَّق منه من تنفيذات مفتوحة المصدر (لا من الهيئة):</b> تحويل التوقيع يستبعد
/// <b>ثلاث مجموعات عقد بالضبط</b> — امتدادات UBL، وعنصر التوقيع، ومرجع المستند الإضافي
/// الذي معرّفه <c>QR</c>. أما <b>عدّاد الفاتورة والبصمة السابقة فليسا مستبعدين</b>،
/// وهذا بالذات ما يجعل السلسلة رابطة تشفيرياً.
/// <para/>
/// القيم محفوظة هنا كـ<b>إعداد</b> لا كثوابت مبعثرة في الكود: حين تصل المواصفة الرسمية
/// يتغيّر هذا السجل وحده.
/// </summary>
public sealed record SigningExclusionRule(
    IReadOnlyList<string> ExcludedElementNames,
    string AdditionalDocumentReferenceElement,
    string AdditionalDocumentReferenceIdElement,
    string ExcludedAdditionalDocumentReferenceId)
{
    /// <summary>
    /// الأسماء نفسها (مساحات الأسماء وبادئاتها) <b>غير مُتحقَّق منها من الهيئة</b>؛
    /// أما <b>عدد المجموعات المستبعدة وهويّتها المفاهيمية</b> فمُتحقَّق منهما من تنفيذات مفتوحة المصدر.
    /// </summary>
    [Provisional("الأسماء الحرفية للعناصر ومساحات أسمائها",
        DerivedFrom = "تنفيذات مفتوحة المصدر مستقلة — لا الهيئة. عدد المجموعات الثلاث وهويّتها مُتحقَّق منهما؛ الأسماء الحرفية لا",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "ملف XSLT الرسمي للتوقيع، أو مواصفة التوقيع المنشورة")]
    public static SigningExclusionRule Default { get; } = new(
        ExcludedElementNames: ["UBLExtensions", "Signature"],
        AdditionalDocumentReferenceElement: "AdditionalDocumentReference",
        AdditionalDocumentReferenceIdElement: "ID",
        ExcludedAdditionalDocumentReferenceId: "QR");

    /// <summary>ثلاث مجموعات بالضبط. أي رقم آخر خطأ في القراءة أو خطأ في الإعداد.</summary>
    public int ExcludedNodeSetCount => ExcludedElementNames.Count + 1;
}

/// <summary>
/// أسماء مراجع المستند الإضافية التي <b>تحمل</b> العدّاد والبصمة السابقة.
/// هذه العناصر <b>داخل</b> البايتات الموقَّعة وليست مستبعدة.
/// </summary>
public static class ChainCarryingReferences
{
    [Provisional("المعرّف الحرفي لمرجع عدّاد الفاتورة داخل المستند",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة بنية الفاتورة المنشورة")]
    public const string CounterReferenceId = "ICV";

    [Provisional("المعرّف الحرفي لمرجع بصمة الفاتورة السابقة داخل المستند",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة بنية الفاتورة المنشورة")]
    public const string PreviousHashReferenceId = "PIH";

    [Provisional("المعرّف الحرفي لمرجع رمز QR داخل المستند — وهو المُستبعَد الثالث",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "ملف XSLT الرسمي للتوقيع")]
    public const string QrReferenceId = "QR";
}
