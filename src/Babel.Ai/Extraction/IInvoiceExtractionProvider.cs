using Babel.Ai.Capture;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction;

/// <summary>
/// أين تُنفَّذ عملية الاستخراج فعلاً. <b>سؤال إقامة بيانات لا سؤال أداء.</b>
/// <para>
/// العميل قد يستضيف قاعدته (‏ADR-0010)، وإرسال صورة فاتورة إلى نموذج خارج المملكة قرارٌ
/// تنظيمي لا تفصيلة تركيب. ولذلك تُعلَن الإقامة <b>قدرةً يقرؤها المُنسِّق وقت التركيب</b>،
/// لا يكتشفها المستخدم بعد أن غادرت الصورة.
/// </para>
/// </summary>
public enum ExtractionResidency
{
    /// <summary>داخل العملية نفسها — لا تغادر البايتات الخادم.</summary>
    InProcess = 1,

    /// <summary>خدمة داخل شبكة العميل.</summary>
    CustomerNetwork = 2,

    /// <summary>خدمة داخل المملكة.</summary>
    InKingdom = 3,

    /// <summary>خدمة خارج المملكة — تحتاج قرار مالك مكتوباً.</summary>
    Offshore = 4,
}

/// <summary>
/// ما يستطيعه هذا المزوّد بالضبط، ويُقرأ <b>وقت التركيب لا وقت النداء</b> —
/// نفس شكل <c>ProviderCapabilities</c> في حدّ الالتزام وللسبب نفسه (‏ADR-0015).
/// </summary>
/// <param name="ProviderId">معرّف المزوّد. يُسجَّل على كل مسوّدة: يُعرف من قرأ ماذا.</param>
/// <param name="DisplayNameKey">مفتاح مورد لاسم المزوّد المعروض (‏ADR-0021).</param>
/// <param name="Residency">أين تُنفَّذ العملية.</param>
/// <param name="ReadsLineItems">هل يقرأ سطور الفاتورة أم الترويسة وحدها؟</param>
/// <param name="IsDeterministic">هل يُعيد المُخرَج نفسه للمدخل نفسه دائماً؟</param>
/// <param name="Timeout">المهلة المُعلنة.</param>
public sealed record ExtractionProviderCapabilities(
    string ProviderId,
    string DisplayNameKey,
    ExtractionResidency Residency,
    bool ReadsLineItems,
    bool IsDeterministic,
    TimeSpan Timeout)
{
    /// <summary>هل تغادر بايتات المستند حدود المنشأة؟ سؤال يُجاب قبل أول صورة لا بعدها.</summary>
    public bool DocumentBytesLeaveThePremises => Residency != ExtractionResidency.InProcess;
}

/// <summary>
/// طلب استخراج. يحمل بايتات المستند و<b>حمولة الرمز إن قُرئت</b> — وهما مساران
/// مختلفان تماماً: الرمز مُصدَّق والصورة مقروءة.
/// <para>
/// <b>ولا يبنيه مستدعٍ خارجي.</b> هذا نوعٌ يعبر إلى المزوّد وحده، و<c>InvoiceCaptureService</c>
/// هي التي تبنيه بعد أن تقرأ البايتات من <c>IAttachmentStore</c>. أمّا ما يدخل
/// الالتقاط من الخارج فهو <c>CaptureRequest</c>، و<b>لا بايتة فيه</b>: معرّف مرفق
/// وقناة وحمولة رمز. والفرق ليس شكلياً — نوعٌ يحمل بايتات عند نقطة الدخول هو نوعٌ
/// تعبر بايتاته السلك <c>base64</c> داخل جسم JSON، فتنتفخ الثلث وتهبط في سجلّ الطلب
/// ولا تُخزَّن في مكان.
/// </para>
/// </summary>
public sealed record ExtractionRequest
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>معرّف المستند الملتقَط داخل هذه الوحدة. ليس معرّف فاتورة.</summary>
    public required string DocumentId { get; init; }

    /// <summary>القناة التي وصل منها.</summary>
    public required CaptureChannel Channel { get; init; }

    /// <summary>نوع المحتوى، مثل <c>image/jpeg</c> أو <c>application/pdf</c>.</summary>
    public required string MediaType { get; init; }

    /// <summary>بايتات المستند.</summary>
    public ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// حمولة رمز الاستجابة السريعة كما مُسحت، أو <c>null</c>. <b>لا تُمرَّر إلى المزوّد
    /// بوصفها إجابة</b>: هي مسار مستقلّ يُفكّ عندنا، ولا يُطلب من النموذج تأكيدها.
    /// </summary>
    public string? QrPayload { get; init; }
}

/// <summary>
/// مُخرَج المزوّد <b>خاماً</b>: نصّ JSON ومعرّف من أنتجه.
/// <para>
/// <b>ولماذا نصّ لا نوع مُهيكَل:</b> لأن هذا هو ما يعيده نموذج فعلاً، ولأن النوع المُهيكَل
/// عند حدّ المزوّد <b>يُخفي</b> السؤال الذي يجب أن يُطرح: هل المُخرَج مطابق للمخطط؟
/// التهيكل يقع عند الحدّ، بعد التحقق، لا قبله.
/// </para>
/// </summary>
/// <param name="ProviderId">من أنتج المُخرَج.</param>
/// <param name="Json">المُخرَج كما ورد.</param>
public sealed record ExtractionOutput(string ProviderId, string Json);

/// <summary>
/// حدّ الاستخراج — <b>واجهة نملكها، مشتقّة من حالة استخدامنا لا من واجهة مورّد</b> (‏ADR-0015).
/// <para>
/// يعبّر عنها مستخرِجٌ داخل العملية وخدمةٌ بعيدة على السواء، <b>ولا يعرف المستدعي أيّهما
/// جاءه</b>: النداء غير متزامن دائماً، والفشل قيمة لا استثناء، والقدرات تُقرأ من
/// <see cref="Capabilities"/> لا تُستنتَج من سلوك النداء.
/// </para>
/// </summary>
public interface IInvoiceExtractionProvider
{
    /// <summary>قدرات هذا المزوّد.</summary>
    ExtractionProviderCapabilities Capabilities { get; }

    /// <summary>يستخرج من المستند. الفشل المتوقّع يُعاد قيمةً.</summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<ExtractionOutput>> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken = default);
}
