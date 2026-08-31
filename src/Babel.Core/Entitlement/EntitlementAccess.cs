namespace Babel.Core.Entitlement;

/// <summary>ما تطلبه نقطة الدخول من الاستحقاق.</summary>
public enum EntitlementAccess
{
    /// <summary>قراءة وتقارير. تعمل في <see cref="EntitlementState.Entitled"/> و<see cref="EntitlementState.ReadOnly"/>.</summary>
    Read = 1,

    /// <summary>إنشاء أو تعديل أو ترحيل. تعمل في <see cref="EntitlementState.Entitled"/> فقط.</summary>
    Write = 2,
}
