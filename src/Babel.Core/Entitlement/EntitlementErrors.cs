using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>رموز أخطاء الاستحقاق. الرمز ثابت والرسالة للعرض.</summary>
public static class EntitlementErrors
{
    /// <summary>الوحدة غير مشتراة.</summary>
    public static Error NotEntitled(BabelModule module) => new(
        "entitlement.not_entitled",
        string.Create(CultureInfo.InvariantCulture, $"الوحدة «{module}» غير مشمولة بالاشتراك."),
        string.Create(CultureInfo.InvariantCulture, $"Module '{module}' is not part of the subscription."));

    /// <summary>الوحدة للقراءة فقط: الاشتراك انقضى.</summary>
    public static Error ReadOnly(BabelModule module) => new(
        "entitlement.read_only",
        string.Create(CultureInfo.InvariantCulture,
            $"الوحدة «{module}» للقراءة فقط: البيانات والتقارير متاحة، ولا يمكن إنشاء مستندات أو ترحيل قيود جديدة."),
        string.Create(CultureInfo.InvariantCulture,
            $"Module '{module}' is read-only: data and reports remain available, but no new documents or postings."));

    /// <summary>وحدة إلزامية أُطفئت.</summary>
    public static Error MandatoryModuleDisabled(BabelModule module) => new(
        "entitlement.mandatory_disabled",
        string.Create(CultureInfo.InvariantCulture, $"الوحدة «{module}» إلزامية ولا يمكن إطفاؤها."),
        string.Create(CultureInfo.InvariantCulture, $"Module '{module}' is mandatory and cannot be disabled."));

    /// <summary>وحدة مفعّلة أقوى من وحدة تعتمد عليها.</summary>
    public static Error UnsatisfiedRequirement(BabelModule module, EntitlementState state, BabelModule requirement, EntitlementState requirementState) => new(
        "entitlement.unsatisfied_requirement",
        string.Create(CultureInfo.InvariantCulture,
            $"الوحدة «{module}» بحالة «{state}» تتطلب «{requirement}» بالحالة نفسها على الأقل، وحالتها «{requirementState}»."),
        string.Create(CultureInfo.InvariantCulture,
            $"Module '{module}' at '{state}' requires '{requirement}' at least at the same level, but it is '{requirementState}'."));

    /// <summary>مجموعة الاستحقاق لا تغطي كل الوحدات.</summary>
    public static Error IncompleteSet(BabelModule module) => new(
        "entitlement.incomplete_set",
        string.Create(CultureInfo.InvariantCulture, $"مجموعة الاستحقاق لا تذكر الوحدة «{module}»."),
        string.Create(CultureInfo.InvariantCulture, $"The entitlement set does not mention module '{module}'."));
}
