namespace Babel.Compliance.Abstractions;

/// <summary>
/// <b>التصنيف الثلاثي هو أهم تعداد في هذا الحدّ كله.</b> الصندوق الصادر يعطي «مرة على الأقل»،
/// والإرسال ليس حصيناً بطبيعته؛ والفرق بين «لم يصل» و«لا أدري» هو الفرق بين إعادة محاولة آمنة
/// وإرسال مكرّر لا يمكن التراجع عنه.
/// </summary>
public enum FaultClass
{
    /// <summary>
    /// عطل <b>قبل</b> أن يغادر الطلب: رفض اتصال، فشل DNS، فشل مصافحة TLS، خطأ تسلسل محلي.
    /// إعادة المحاولة آمنة تماماً.
    /// </summary>
    TransientNotSent,

    /// <summary>
    /// رفض نهائي مفهوم: بيانات غير صالحة، شهادة منتهية، صلاحية مرفوضة.
    /// إعادة المحاولة بنفس الحمولة عبث.
    /// </summary>
    Permanent,

    /// <summary>
    /// <b>الحالة التي تُسقط الأنظمة:</b> الطلب غادر، والجواب لم يصل — مهلة، أو قطع اتصال بعد
    /// الإرسال، أو رد بلا جسم. <b>لا يمكن معرفة هل نُفِّذ أم لا.</b>
    /// إعادة المحاولة العمياء هنا محظورة، ويتحوّل المسار إلى مسار حسم لا إلى مسار إرسال.
    /// </summary>
    Ambiguous
}

public enum NoticeSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>
/// ملاحظة من الجهة أو من المزوّد. <b>لا تُبتلع في سجل فني</b> — تُعرض للمستخدم بلغته.
/// </summary>
public sealed record ComplianceNotice(
    [property: Provisional("قائمة رموز الأخطاء والملاحظات ودلالة كل رمز",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "جدول رموز الاستجابة في مواصفة الواجهة")]
    string Code,
    string MessageAr,
    string MessageEn,
    NoticeSeverity Severity)
{
    public static ComplianceNotice Info(string code, string ar, string en) => new(code, ar, en, NoticeSeverity.Information);
    public static ComplianceNotice Warn(string code, string ar, string en) => new(code, ar, en, NoticeSeverity.Warning);
    public static ComplianceNotice Err(string code, string ar, string en) => new(code, ar, en, NoticeSeverity.Error);
}

public sealed record ComplianceFault(
    FaultClass Class,
    string Code,
    string MessageAr,
    string MessageEn,
    string? ProviderReference = null)
{
    public static ComplianceFault NotSent(string code, string ar, string en) =>
        new(FaultClass.TransientNotSent, code, ar, en);

    public static ComplianceFault Permanent(string code, string ar, string en) =>
        new(FaultClass.Permanent, code, ar, en);

    /// <summary>لا تُنشأ إلا حين يكون الجواب مجهولاً فعلاً. استعمالها في غير موضعها يعطّل الحماية كلها.</summary>
    public static ComplianceFault Ambiguous(string code, string ar, string en, string? providerRef = null) =>
        new(FaultClass.Ambiguous, code, ar, en, providerRef);
}

/// <summary>الاستثناء الوحيد الذي يعبر هذا الحدّ. يحمل التصنيف دائماً.</summary>
public sealed class ComplianceTransportException(ComplianceFault fault, Exception? inner = null)
    : Exception($"[{fault.Class}] {fault.Code}: {fault.MessageEn}", inner)
{
    public ComplianceFault Fault { get; } = fault;
}
