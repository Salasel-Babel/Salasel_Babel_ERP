using System.Reflection;

namespace Babel.Compliance.Abstractions;

/// <summary>
/// كل تفصيلة تنظيمية أو بروتوكولية في هذا الحدّ لم تُقرأ من الوثيقة الرسمية للهيئة
/// تحمل هذه السمة. بوابة الهيئة وبيئة المحاكاة ومنتدى المطوّرين كلها محجوبة عن هذه
/// الشبكة، فلا يوجد مصدر رسمي يمكن الرجوع إليه أثناء بناء هذا الحدّ.
/// <para/>
/// Every regulatory or protocol specific in this boundary that was NOT read from the
/// authority's own published document carries this attribute. The authority's portal,
/// sandbox and developer forum are all unreachable from this network, so no official
/// source could be consulted while this boundary was built.
/// <para/>
/// <see cref="ProvisionalRegistry"/> يُخرج قائمة التحقق قبل البناء من هذه السمات.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property |
    AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Constructor,
    AllowMultiple = true, Inherited = false)]
public sealed class ProvisionalAttribute(string subject) : Attribute
{
    /// <summary>النص الملزم — لا يُغيَّر.</summary>
    public const string Notice = "غير مُتحقَّق منه — يُثبَّت من الوثيقة الرسمية قبل البناء";

    /// <summary>ما هو المؤقَّت بالضبط.</summary>
    public string Subject { get; } = subject;

    /// <summary>من أين جاء الشكل المؤقَّت، إن جاء من مكان أصلاً. لا يُذكر هنا أي مصدر رسمي.</summary>
    public string? DerivedFrom { get; init; }

    /// <summary>ماذا يكلّف تصحيحه لاحقاً.</summary>
    public ProvisionalRisk Risk { get; init; } = ProvisionalRisk.Reworkable;

    /// <summary>ما الذي يجب سؤاله/قراءته بالضبط لإغلاق هذا البند.</summary>
    public string? VerifyBy { get; init; }
}

/// <summary>تكلفة تصحيح البند المؤقَّت بعد أن يُبنى عليه.</summary>
public enum ProvisionalRisk
{
    /// <summary>تغيير قيمة نصّية أو ثابت. أثر محدود.</summary>
    Cosmetic,

    /// <summary>تغيير شكل بيانات داخل الحدّ. يُعاد بناء المُحوِّل وحده.</summary>
    Reworkable,

    /// <summary>تغيير يمسّ بنية الحدّ نفسه أو السلسلة أو دورة الشهادات. مكلف جداً بعد الإنتاج.</summary>
    Structural
}

/// <summary>
/// السمة الثانية، وهي ليست عن الهيئة بل عن قرارنا نحن: هذا العنصر موجود
/// <b>فقط</b> لأن شكلَي حيازة المفتاح لم يُحسم بينهما بعد. لو حُسم القرار
/// اليوم لحُذف هذا العنصر أو انكمش.
/// <para/>
/// This member exists ONLY because the key-custody decision is still open.
/// Committing to one shape would delete or shrink it. This is how the price of
/// deferring the decision is measured instead of asserted.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property |
    AttributeTargets.Method | AttributeTargets.Constructor,
    AllowMultiple = false, Inherited = false)]
public sealed class DualCustodyCostAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;

    public CustodyCostKind Kind { get; init; } = CustodyCostKind.ExtraSurface;

    /// <summary>الشكل الذي يجعل هذا العنصر ميّتاً (غير قابل للتنفيذ) — إن وُجد.</summary>
    public DeadUnderShape DeadUnder { get; init; } = DeadUnderShape.None;
}

/// <summary>أيّ شكل حيازة يجعل هذا العنصر كوداً ميّتاً. (تعداد مستقل لأن النوع القابل للعدم لا يصلح وسيطاً لسمة.)</summary>
public enum DeadUnderShape
{
    /// <summary>ليس ميّتاً تحت أيّ شكل — كلفته سطح إضافي فقط.</summary>
    None,

    /// <summary>ميّت تحت «المزوّد يحوز المفتاح».</summary>
    ProviderHeld,

    /// <summary>ميّت تحت «نحن نحوز المفتاح».</summary>
    SelfHeld
}

public enum CustodyCostKind
{
    /// <summary>نوع أو عضو إضافي في العقد ما كان ليوجد لو حُسم القرار.</summary>
    ExtraSurface,

    /// <summary>فرع تنفيذ لا يُنفَّذ أبداً تحت أحد الشكلين — كود ميّت نصف الوقت.</summary>
    DeadBranch,

    /// <summary>فحص وقت التشغيل يستبدل ضماناً كان مترجم اللغة سيعطيه مجاناً لو حُسم القرار.</summary>
    RuntimeGuard,

    /// <summary>خاصية أضعف: ما كان يمكن ضمانه بنيوياً صار مشروطاً بقدرات المزوّد.</summary>
    WeakenedGuarantee
}

/// <summary>بند واحد في طابور التحقق قبل البناء.</summary>
public sealed record ProvisionalItem(
    string Location,
    string Subject,
    ProvisionalRisk Risk,
    string? DerivedFrom,
    string? VerifyBy)
{
    public string Notice => ProvisionalAttribute.Notice;
}

/// <summary>بند واحد في فاتورة تعميم شكلَي الحيازة.</summary>
public sealed record CustodyCostItem(
    string Location,
    string Reason,
    CustodyCostKind Kind,
    DeadUnderShape DeadUnder);

/// <summary>
/// يمسح التجميعات ويستخرج طابور التحقق قبل البناء وفاتورة التعميم.
/// وجود هذا المُسجِّل هو ما يجعل «تكلفة التأجيل» رقماً يُطبع، لا رأياً يُقال.
/// </summary>
public static class ProvisionalRegistry
{
    public static IReadOnlyList<ProvisionalItem> Collect(params Assembly[] assemblies)
    {
        var items = new List<ProvisionalItem>();
        foreach (var (member, location) in EnumerateMembers(assemblies))
            foreach (var a in member.GetCustomAttributes<ProvisionalAttribute>(false))
                items.Add(new ProvisionalItem(location, a.Subject, a.Risk, a.DerivedFrom, a.VerifyBy));

        foreach (var (parameter, location) in EnumerateParameters(assemblies))
            foreach (var a in parameter.GetCustomAttributes<ProvisionalAttribute>(false))
                items.Add(new ProvisionalItem(location, a.Subject, a.Risk, a.DerivedFrom, a.VerifyBy));

        return [.. items
            .DistinctBy(i => (i.Location, i.Subject))
            .OrderByDescending(i => i.Risk)
            .ThenBy(i => i.Location, StringComparer.Ordinal)];
    }

    public static IReadOnlyList<CustodyCostItem> CollectCustodyCost(params Assembly[] assemblies)
    {
        var items = new List<CustodyCostItem>();
        foreach (var (member, location) in EnumerateMembers(assemblies))
            foreach (var a in member.GetCustomAttributes<DualCustodyCostAttribute>(false))
                items.Add(new CustodyCostItem(location, a.Reason, a.Kind, a.DeadUnder));
        return [.. items
            .DistinctBy(i => i.Location)
            .OrderBy(i => i.Kind)
            .ThenBy(i => i.Location, StringComparer.Ordinal)];
    }

    private static IEnumerable<(MemberInfo Member, string Location)> EnumerateMembers(Assembly[] assemblies)
    {
        foreach (var asm in assemblies)
            foreach (var type in SafeTypes(asm))
            {
                yield return (type, type.FullName ?? type.Name);
                foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                  BindingFlags.Static | BindingFlags.DeclaredOnly))
                    yield return (m, $"{type.FullName}.{m.Name}");
            }
    }

    private static IEnumerable<(ParameterInfo Parameter, string Location)> EnumerateParameters(Assembly[] assemblies)
    {
        foreach (var asm in assemblies)
            foreach (var type in SafeTypes(asm))
                foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                  BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m is not MethodBase mb) continue;
                    foreach (var p in mb.GetParameters())
                        yield return (p, $"{type.FullName}.{m.Name}({p.Name})");
                }
    }

    private static IEnumerable<Type> SafeTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    /// <summary>جدول نصّي جاهز للطباعة في سجل البناء أو في تقرير المراجعة.</summary>
    public static string Render(IReadOnlyList<ProvisionalItem> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("طابور التحقق قبل البناء — ").Append(items.Count).Append(" بنداً\n");
        sb.Append(ProvisionalAttribute.Notice).Append('\n');
        sb.Append(new string('-', 100)).Append('\n');
        foreach (var g in items.GroupBy(i => i.Risk).OrderByDescending(g => g.Key))
        {
            sb.Append('\n').Append("### ").Append(g.Key).Append(" (").Append(g.Count()).Append(")\n");
            foreach (var i in g)
            {
                sb.Append("  • ").Append(i.Subject).Append('\n');
                sb.Append("      الموضع : ").Append(i.Location).Append('\n');
                if (i.DerivedFrom is not null) sb.Append("      مشتق من: ").Append(i.DerivedFrom).Append('\n');
                if (i.VerifyBy is not null) sb.Append("      يُثبَّت بـ: ").Append(i.VerifyBy).Append('\n');
            }
        }
        return sb.ToString();
    }
}
