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
    public static string PeriodOf(DateTimeOffset at) => $"{at.UtcDateTime:yyyy-MM}";

    public UsageEvent Normalised() => this with
    {
        OccurredAt = Canon.Instant(OccurredAt),
        Quantity = decimal.Round(Quantity, 4, MidpointRounding.ToEven)
    };
}

public sealed record RecordOutcome(int Accepted, int Duplicates, int Spooled)
{
    public int Total => Accepted + Duplicates + Spooled;
}
