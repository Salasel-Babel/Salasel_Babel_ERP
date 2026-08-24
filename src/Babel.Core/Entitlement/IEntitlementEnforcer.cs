using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// حدّ الإنفاذ. تستدعيه كل نقطة دخول عامة قبل أي عمل.
/// <para>
/// موضعه عند الخدمة لا عند الواجهة: إخفاء عنصر من القائمة لا يمنع نداء HTTP.
/// وهو أيضاً موضع قياس الاستخدام — بحيث لا يمكن أن يمرّ استدعاء مستحَق دون أن يُقاس.
/// </para>
/// </summary>
public interface IEntitlementEnforcer
{
    /// <summary>يتحقق من الاستحقاق ويسجّل الاستخدام. الفشل يُعاد قيمةً لا استثناءً.</summary>
    ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default);
}
