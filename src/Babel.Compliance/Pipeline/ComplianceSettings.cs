using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// إعدادات الالتزام لكل مستأجر. <b>لا نسب ولا حدود ولا مُهَل ثابتة في الكود</b>
/// (CONTRIBUTING §3 بند 6) — وهنا تحديداً لأن كل رقم زمني تنظيمي في هذا المجال
/// <b>غير مُتحقَّق منه</b> ولا يجوز تجميده في مُترجَم.
/// </summary>
public sealed record ComplianceSettings
{
    /// <summary>مهلة نداء المقاصة الحاجز. تتجاوزها = غموض، لا فشل.</summary>
    public TimeSpan ClearanceTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>مهلة نداء الإبلاغ من العامل الخلفي.</summary>
    public TimeSpan ReportingTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// مهلة إيجار المحاولة: صف <c>InFlight</c> أقدم من هذه المدة يعني أن العملية
    /// سقطت في منتصف النداء. <b>هذا غموض، لا محاولة قائمة.</b>
    /// </summary>
    public TimeSpan AttemptLease { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// النافذة التي يجب أن يتم فيها الإبلاغ. <b>القيمة الافتراضية هنا ليست القيمة النظامية</b>؛
    /// هي مجرد قيمة تشغيلية حتى تُثبَّت من الوثيقة الرسمية، وتُضبط لكل مستأجر.
    /// </summary>
    [Provisional("نافذة الإبلاغ النظامية ومتى تبدأ ومتى تنتهي",
        DerivedFrom = "docs/analysis/04-zatca-integration.md §3 يذكر ٢٤ ساعة، وهي وثيقة تخطيط داخلية لا مصدر رسمي",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "اللائحة السارية ومواصفة الإبلاغ المنشورة")]
    public TimeSpan ReportingWindow { get; init; } = TimeSpan.FromHours(24);

    /// <summary>متى يصير التأخير في الطابور نتيجة مطابقة يراها المدير المالي.</summary>
    public TimeSpan QueueAgeAlarm { get; init; } = TimeSpan.FromHours(4);

    /// <summary>سياسة إعادة المحاولة للأعطال التي لم يغادر فيها الطلب.</summary>
    public RetryPolicy Retry { get; init; } = RetryPolicy.Default;

    /// <summary>
    /// <b>سقف محاولات الحسم بعد الغموض.</b> تجاوزه يعني الطابور البشري، لا محاولة إضافية.
    /// الرقم صغير عمداً: كل محاولة حسم بإعادة إرسال تحمل خطر إنشاء تكرار حقيقي.
    /// </summary>
    public int MaxResolutionAttempts { get; init; } = 3;

    /// <summary>
    /// هل يُسمح بإعادة إرسال ببايتات مطابقة كوسيلة حسم حين لا يوجد استعلام حالة؟
    /// <b>الافتراضي: لا.</b> يُفعَّل فقط حين يُصرّح المزوّد بكشف التكرار تعاقدياً،
    /// وحين تكون البايتات مستقرة بنيوياً (شكل «نحن نحوز» وحده).
    /// </summary>
    public bool AllowIdenticalResubmitAsResolution { get; init; }

    /// <summary>سياسة العزل المالي.</summary>
    public FiscalInclusionPolicy Quarantine { get; init; } = FiscalInclusionPolicy.Default;
}

/// <summary>
/// إعادة المحاولة بتباعد أُسّي مع اهتزاز. <b>لا تُطبَّق على الغموض إطلاقاً</b> —
/// الغموض ليس عطلاً يُعاد معه المحاولة، بل حالة تُحسم.
/// </summary>
public sealed record RetryPolicy(
    int MaxAttempts,
    TimeSpan BaseDelay,
    double Multiplier,
    TimeSpan MaxDelay,
    double JitterFraction)
{
    public static RetryPolicy Default { get; } =
        new(MaxAttempts: 6, BaseDelay: TimeSpan.FromSeconds(2), Multiplier: 2.0,
            MaxDelay: TimeSpan.FromMinutes(15), JitterFraction: 0.20);

    public TimeSpan DelayFor(int attemptNo, Random? random = null)
    {
        if (attemptNo < 1) attemptNo = 1;
        var raw = BaseDelay.TotalMilliseconds * Math.Pow(Multiplier, attemptNo - 1);
        var capped = Math.Min(raw, MaxDelay.TotalMilliseconds);
        var rng = random ?? Random.Shared;
        var jitter = capped * JitterFraction * (rng.NextDouble() * 2 - 1);
        return TimeSpan.FromMilliseconds(Math.Max(0, capped + jitter));
    }
}

/// <summary>
/// <b>القيد مُرحَّل دائماً. العزل يقع على الاستهلاك، لا على الوجود.</b>
/// الاعتراف بالإيراد يدور على انتقال السيطرة، لا على ختم جهة ضريبية.
/// <para/>
/// <b>قرار محاسبي يجب تأكيده مع المحاسب القانوني</b> — وبالذات سؤالان:
/// هل تُعزل الفاتورة المبسطة (مسار الإبلاغ) عن الإقرار وهي <b>صادرة قانوناً</b> فعلاً؟
/// وهل يبقى المستند المرفوض معزولاً إلى الأبد أم يُعالَج بمستند تصحيحي؟
/// </summary>
public sealed record FiscalInclusionPolicy(
    bool QuarantineClearanceUntilAccepted,
    bool QuarantineReportingUntilAcknowledged,
    bool QuarantineRejectedForever)
{
    public static FiscalInclusionPolicy Default { get; } = new(
        QuarantineClearanceUntilAccepted: true,
        QuarantineReportingUntilAcknowledged: true,
        QuarantineRejectedForever: true);
}
