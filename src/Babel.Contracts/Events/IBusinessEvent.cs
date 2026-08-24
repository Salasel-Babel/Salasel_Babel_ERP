using Babel.SharedKernel;

namespace Babel.Contracts.Events;

/// <summary>
/// حدث أعمال منشور. الوحدات الأفقية لا تستدعي بعضها مباشرة؛ تتخاطب بالأحداث
/// أو بواجهات معلنة (وثيقة المعمارية §13 — قواعد الحدود).
/// <para>الحدث يصف <b>ما حدث تجارياً</b>. لا يحمل حسابات ولا قيوداً ولا أرصدة.</para>
/// </summary>
public interface IBusinessEvent
{
    /// <summary>المستأجر.</summary>
    TenantId Tenant { get; }

    /// <summary>الوحدة التي نشرت الحدث.</summary>
    BabelModule Origin { get; }

    /// <summary>لحظة وقوع الحدث. تُلتقط مرة واحدة وتُعامل مدخلاً غير قابل للتغيير (وثيقة المعمارية §8.2 مصيدة 5).</summary>
    DateTimeOffset OccurredAt { get; }
}
