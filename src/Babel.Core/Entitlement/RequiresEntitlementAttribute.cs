using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// تُعلن ما تحتاجه نقطة الدخول من استحقاق.
/// <para>
/// السمة إعلان، والإنفاذ عند <see cref="IEntitlementEnforcer"/>. ما يجعل الإعلان ملزماً
/// هو Rule06: كل دالة عامة على نوع يحمل <see cref="Application.IApplicationService"/>
/// ولا تحمل هذه السمة (ولا يحملها نوعها) <b>تُفشل البناء</b>.
/// </para>
/// </summary>
/// <param name="module">الوحدة التي تنتمي إليها نقطة الدخول.</param>
/// <param name="access">مستوى الوصول المطلوب.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresEntitlementAttribute(BabelModule module, EntitlementAccess access) : Attribute
{
    /// <summary>الوحدة التي تنتمي إليها نقطة الدخول.</summary>
    public BabelModule Module { get; } = module;

    /// <summary>مستوى الوصول المطلوب.</summary>
    public EntitlementAccess Access { get; } = access;
}
