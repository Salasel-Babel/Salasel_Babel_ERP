namespace Babel.Compliance.Abstractions;

/// <summary>
/// ما يستطيعه هذا المزوّد بالضبط. <b>يُقرأ وقت التركيب لا وقت التشغيل</b>:
/// المُنسِّق يستعمل هذه القدرات ليختار <b>استراتيجية</b>، لا ليتفرّع في كل نداء.
/// <para/>
/// أربعة من هذه الحقول موجودة <b>فقط</b> لأن شكل الحيازة غير محسوم — انظر السمات.
/// </summary>
public sealed record ProviderCapabilities(
    string ProviderId,
    string DisplayNameAr,
    string DisplayNameEn,
    KeyCustody Custody,
    bool SupportsClearance,
    bool SupportsReporting,
    StatusProbeSupport StatusQuery,
    bool ReturnsStampedDocument,
    bool RendersDocument,
    TimeSpan ClearanceTimeout,
    TimeSpan ReportingTimeout,
    bool DeduplicatesBySubmissionFingerprint = false,
    bool GuaranteesByteStableRetransmission = false)
{
    /// <summary>
    /// <b>أهم سطر في هذا السجل.</b> تحت شكل «نحن نحوز» تكون البايتات مجمَّدة عندنا،
    /// فكل إعادة إرسال مطابقة بايتياً — خاصية بنيوية لا تحتاج وعداً من أحد.
    /// وتحت شكل «المزوّد يحوز» يعيد المزوّد الختم في كل محاولة، وتوقيع ECDSA عشوائي،
    /// فالمطابقة البايتية <b>وعد تعاقدي</b> لا خاصية بنيوية — ويجب أن يُكتب في العقد.
    /// </summary>
    [DualCustodyCost(
        "خاصية أُنزلت إلى مرتبة «قدرة يُصرّح بها المزوّد» لأن القرار مؤجَّل. " +
        "لو حُسم «نحن نحوز» لصارت ثابتاً بنيوياً لا يحتاج تصريحاً ولا فحصاً وقت التشغيل، " +
        "ولاستطاع حارس الحصانة أن يبني عليها بدل أن يشترطها. " +
        "هذه هي التكلفة الحقيقية للتعميم: ليست أسطر كود، بل ضمان أضعف.",
        Kind = CustodyCostKind.WeakenedGuarantee)]
    public bool ByteStableRetriesAreStructural => Custody == KeyCustody.SelfHeld;

    /// <summary>
    /// هل يمكن حسم المهلة الغامضة آلياً أصلاً؟ إن كان الجواب لا،
    /// فكل مهلة غامضة تنتهي إلى طابور بشري. هذا رقم يُقاس ويُعرض على المالك.
    /// </summary>
    public bool AmbiguityCanBeResolvedAutomatically =>
        StatusQuery != StatusProbeSupport.NotSupported || DeduplicatesBySubmissionFingerprint;
}

/// <summary>
/// المزوّد كوحدة تركيب واحدة. القنوات <b>قابلة للغياب</b> عمداً:
/// مزوّد إبلاغ فقط شيء واقعي، ومزوّد بلا استعلام حالة هو <b>الحالة المتوقَّعة</b>.
/// </summary>
public interface IComplianceProvider
{
    ProviderCapabilities Capabilities { get; }

    IDocumentSealer Sealer { get; }

    IOnboardingChannel Onboarding { get; }

    IClearanceChannel? Clearance { get; }

    IReportingChannel? Reporting { get; }

    IComplianceStatusQuery? StatusQuery { get; }
}
