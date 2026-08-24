using Babel.SharedKernel;

namespace Babel.Core.Tenancy;

/// <summary>
/// سياق الطلب الجاري: من يطلب، ولأي مستأجر.
/// مصدره طبقة المصادقة، لا وسيط من العميل — وسيط من العميل يعني تجاوز عزل المستأجرين.
/// </summary>
public interface ITenantContext
{
    /// <summary>المستأجر الحالي.</summary>
    TenantId Tenant { get; }

    /// <summary>المستخدم الحالي.</summary>
    UserId User { get; }
}
