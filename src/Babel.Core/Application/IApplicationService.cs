namespace Babel.Core.Application;

/// <summary>
/// علامة خدمة تطبيق: أي نوع يحمل نقاط دخول عامة يستدعيها العالم الخارجي
/// (HTTP، معالج رسالة، مهمة مجدولة).
/// <para>
/// وجود هذه العلامة هو ما يجعل «لا شيء يتجاوز الاستحقاق» قابلاً للفرض:
/// Rule06 يعدّ كل دالة عامة على كل نوع يحمل هذه العلامة، ويُفشل البناء على أي دالة
/// بلا <see cref="Entitlement.RequiresEntitlementAttribute"/>.
/// </para>
/// <para>
/// إخفاء المنتج من القائمة ليس إنفاذاً — نداء HTTP لا يمرّ بالقائمة. الإنفاذ عند حدّ الخدمة.
/// </para>
/// </summary>
public interface IApplicationService;
