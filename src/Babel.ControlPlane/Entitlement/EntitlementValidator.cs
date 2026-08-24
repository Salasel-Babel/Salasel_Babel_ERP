namespace Babel.ControlPlane.Entitlement;

/// <summary>
/// مُتحقِّق تماسك مجموعة الاستحقاق.
///
/// <para><b>القاعدة الواحدة:</b> حالة وحدة لا تتجاوز <b>أدنى</b> حالة بين
/// اعتمادياتها المباشرة. أي: <c>POS = Entitled</c> يستلزم
/// <c>INV = Entitled</c> و<c>AR = Entitled</c>؛ و<c>POS = ReadOnly</c> يستلزم
/// <c>INV ≥ ReadOnly</c>. و<c>NotEntitled</c> مسموح دائماً.</para>
///
/// <para>سبب صياغتها هكذا: خفض حزمة العميل هو الحالة الشائعة، لا الشراء
/// الأول. حين تسقط «المخزون» إلى قراءة فقط، فإن «نقاط البيع» <b>لا يمكن</b>
/// أن تبقى قابلة للكتابة — ستُنشئ حركات مخزون في وحدة موقوفة الإدخال.</para>
/// </summary>
public static class EntitlementValidator
{
    public static IReadOnlyList<EntitlementViolation> Validate(
        IReadOnlyDictionary<string, EntitlementState> set)
    {
        var violations = new List<EntitlementViolation>();

        // 1 · كل وحدة معروفة، وكل وحدة في الكتالوج لها حالة صريحة.
        foreach (var code in set.Keys.OrderBy(x => x, StringComparer.Ordinal))
            if (ModuleCatalog.All.All(m => m.Code != code))
                violations.Add(new(code,
                    $"وحدة غير معروفة في الكتالوج: «{code}»",
                    $"unknown module: '{code}'"));

        foreach (var m in ModuleCatalog.All.OrderBy(m => m.Code, StringComparer.Ordinal))
            if (!set.ContainsKey(m.Code))
                violations.Add(new(m.Code,
                    $"الوحدة «{m.Code}» بلا حالة صريحة — لا حالة ضمنية ولا NULL",
                    $"module '{m.Code}' has no explicit state"));

        if (violations.Count > 0) return violations;

        // 2 · قاعدة الاعتمادية.
        foreach (var m in ModuleCatalog.All.OrderBy(m => m.Code, StringComparer.Ordinal))
        {
            var mine = set[m.Code];
            if (mine == EntitlementState.NotEntitled) continue;

            foreach (var dep in m.DependsOn.OrderBy(x => x, StringComparer.Ordinal))
            {
                var depState = set.TryGetValue(dep, out var d) ? d : EntitlementState.NotEntitled;
                if (depState >= mine) continue;

                violations.Add(new(m.Code,
                    $"«{m.Code}» بحالة {Ar(mine)} بينما اعتماديتها «{dep}» بحالة {Ar(depState)} — "
                    + $"الوحدة لا تتجاوز أدنى حالات اعتمادياتها",
                    $"'{m.Code}' is {mine} but its dependency '{dep}' is {depState}"));
            }
        }

        return violations;
    }

    public static void Require(IReadOnlyDictionary<string, EntitlementState> set)
    {
        var v = Validate(set);
        if (v.Count > 0) throw new IncoherentEntitlementSetException(v);
    }

    /// <summary>
    /// أصغر مجموعة تصحيحات تجعل الطلب متماسكاً: ترفع الاعتماديات إلى حالة
    /// الوحدة الطالبة. تُعرَض على المشغّل، ولا تُطبَّق تلقائياً — رفع استحقاق
    /// بلا سند مكتوب هو بالضبط ما يمنعه سجل التدقيق.
    /// </summary>
    public static IReadOnlyList<EntitlementChange> SuggestRepairs(
        IReadOnlyDictionary<string, EntitlementState> set)
    {
        var work = new Dictionary<string, EntitlementState>(set, StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var m in ModuleCatalog.All.OrderBy(m => m.Code, StringComparer.Ordinal))
            {
                if (!work.TryGetValue(m.Code, out var mine) || mine == EntitlementState.NotEntitled)
                    continue;
                foreach (var dep in m.DependsOn)
                {
                    var cur = work.TryGetValue(dep, out var d) ? d : EntitlementState.NotEntitled;
                    if (cur >= mine) continue;
                    work[dep] = mine;
                    changed = true;
                }
            }
        }

        return [.. work.Where(kv => !set.TryGetValue(kv.Key, out var old) || old != kv.Value)
                       .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                       .Select(kv => new EntitlementChange(kv.Key, kv.Value))];
    }

    public static string Ar(EntitlementState s) => s switch
    {
        EntitlementState.Entitled => "مستحقّة",
        EntitlementState.ReadOnly => "قراءة فقط",
        _ => "غير مستحقّة"
    };
}
