using System.Globalization;
using Babel.ControlPlane.Support;

namespace Babel.ControlPlane.Metering;

/// <summary>
/// حدث قياس واحد على أحد محورَي التسعير.
///
/// <para><b>لماذا يُبنى هذا الآن لا لاحقاً:</b> ما لا يُلتقط اليوم لا يُستعاد
/// غداً. يمكن تغيير صيغة الفاتورة بأثر رجعي؛ ولا يمكن اختراع استعمال شهر
/// مضى. القياس هو البند الوحيد في مستوى التحكّم الذي يفوت وقته فعلاً.</para>
///
/// <para><c>IdempotencyKey</c> <b>يورّده المنتِج</b> — لا يُولَّد هنا. مفتاح
/// مُولَّد داخلياً ليس مفتاح إحكام بل رقماً عشوائياً جديداً في كل إعادة محاولة.</para>
/// </summary>
public sealed record UsageEvent(
    Guid TenantId,
    string IdempotencyKey,
    string PeriodCode,
    string ModuleCode,
    string? UserRef,
    string EventKind,
    decimal Quantity,
    DateTimeOffset OccurredAt,
    string Source)
{
    /// <summary>
    /// رمز فترة الفوترة (‏<c>YYYY-MM</c>) الميلادي من لحظة.
    ///
    /// <para><b>التقويم والثقافة مثبَّتان صراحةً، وهذا ليس تجميلاً.</b> التنسيق
    /// بلا ثقافة يقرأ ثقافة العملية <b>وتقويمها</b>. والمنتج عربي يعمل بثقافة
    /// <c>ar-SA</c> وبـ<c>InvariantGlobalization=false</c> إلزاماً، والتقويم
    /// الافتراضي لتلك الثقافة في .NET هو <b>أم القرى الهجري</b> — فيصير رمز
    /// الفترة <c>1448-03</c> بدل <c>2026-08</c> (‏مقيس). وقياساً عليه
    /// <c>fa-IR</c> ⇒ <c>1405-06</c> و<c>th-TH</c> ⇒ <c>2569-08</c>.
    /// </para>
    ///
    /// <para><b>العطل صامت وكامل:</b> لا استثناء ولا سطر سجل. الأحداث تُكتب تحت
    /// رمز فترة هجري، واستعلام الفوترة عن <c>2026-08</c> يُرجِع صفراً، فتُصدَر
    /// فاتورة الشهر <b>خالية من كل الاستعمال</b> — أي خسارة إيراد لا يكشفها إلا
    /// تدقيق يدوي. وهو يقع على خادم الإنتاج العربي ولا يقع على جهاز المطوّر.</para>
    /// </summary>
    /// <param name="at">اللحظة.</param>
    /// <returns>رمز الفترة الميلادي بصيغة <c>YYYY-MM</c>.</returns>
    public static string PeriodOf(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>نسخة مُوحَّدة قياسياً: اللحظة مقصوصة إلى الميكروثانية والكمّية بمقياس 4.</summary>
    /// <returns>الحدث بعد التوحيد.</returns>
    public UsageEvent Normalised() => this with
    {
        OccurredAt = Canon.Instant(OccurredAt),
        Quantity = decimal.Round(Quantity, 4, MidpointRounding.ToEven)
    };
}

/// <summary>حصيلة تسجيل دفعة أحداث قياس.</summary>
/// <param name="Accepted">أُدرِجت جديدةً.</param>
/// <param name="Duplicates">مكرَّرة صدّها مفتاح الإحكام — وهي دليل عدم الازدواج تحت إعادة المحاولة.</param>
/// <param name="Spooled">تعذّر الوصول إلى قاعدة التحكّم فثُبِّتت على القرص، ولم تُسقَط.</param>
public sealed record RecordOutcome(int Accepted, int Duplicates, int Spooled)
{
    /// <summary>مجموع ما استُقبل — لا يضيع حدث بين الحالات الثلاث.</summary>
    public int Total => Accepted + Duplicates + Spooled;
}
