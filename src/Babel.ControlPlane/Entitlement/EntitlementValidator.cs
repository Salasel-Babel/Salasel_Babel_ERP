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
    /// <summary>
    /// يفحص تماسك مجموعة استحقاق كاملةً ويُرجِع <b>كل</b> المخالفات لا أوّلها.
    /// </summary>
    /// <param name="set">المجموعة المطلوبة: الحالة لكل وحدة.</param>
    /// <returns>المخالفات؛ قائمة فارغة تعني مجموعة متماسكة.</returns>
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

    /// <summary>
    /// يرفض المجموعة غير المتماسكة <b>كاملةً</b>. لا إصلاح صامت: مجموعة تُصلَح
    /// تلقائياً تُنتج اشتراكاً لم يطلبه أحد ولم يوافق عليه أحد.
    /// </summary>
    /// <param name="set">المجموعة المطلوبة.</param>
    /// <exception cref="IncoherentEntitlementSetException">المجموعة غير متماسكة.</exception>
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

    // ── الأرضية: الانتقال، لا المجموعة ──────────────────────────────────────

    /// <summary>
    /// يفحص <b>انتقالاً</b> من مجموعة قائمة إلى مطلوبة: تماسك المطلوبة كاملةً،
    /// <b>ومعه قاعدة الأرضية</b>.
    ///
    /// <para><b>لماذا الانتقال لا المجموعة:</b> «‏CORE = NotEntitled» جملةٌ
    /// مشروعة تماماً عن مستأجر لم يشترِ شيئاً بعد، وجملةٌ كارثية عن مستأجر
    /// رحّل ألف قيد. الفرق <b>ليس في المجموعة المطلوبة</b> بل في التي سبقتها،
    /// فمُتحقِّقٌ يقرأ المطلوبة وحدها <b>لا يستطيع</b> أن يفرّق بينهما — وهذا
    /// بالضبط ما كان يسمح بانتزاع دفتر عميل عبر عملية مدعومة.</para>
    ///
    /// <para><b>القاعدة:</b> وحدةٌ حالتها القائمة فوق <c>NotEntitled</c> لا تنزل
    /// تحت أرضيتها (<see cref="ModuleCatalog.FloorOf"/>). والصعود حرّ، والوحدة
    /// التي لم تُشترَ قط حرّة.</para>
    /// </summary>
    /// <param name="current">المجموعة القائمة.</param>
    /// <param name="next">المجموعة المطلوبة.</param>
    /// <returns>كل المخالفات — مخالفات التماسك ومخالفات الأرضية معاً.</returns>
    public static IReadOnlyList<EntitlementViolation> ValidateTransition(
        IReadOnlyDictionary<string, EntitlementState> current,
        IReadOnlyDictionary<string, EntitlementState> next)
    {
        var violations = new List<EntitlementViolation>(Validate(next));
        if (violations.Count > 0) return violations;

        foreach (var m in ModuleCatalog.All.OrderBy(m => m.Code, StringComparer.Ordinal))
        {
            var was = current.TryGetValue(m.Code, out var c) ? c : EntitlementState.NotEntitled;
            var floor = ModuleCatalog.LowestReachableFrom(m.Code, was);
            var to = next.TryGetValue(m.Code, out var n) ? n : EntitlementState.NotEntitled;
            if (to >= floor) continue;

            violations.Add(new(m.Code,
                $"«{m.Code}» ({m.NameAr}) بحالة {Ar(was)}، وطُلب إنزالها إلى {Ar(to)} — "
                + $"وهي وحدة تُرحّل قيوداً فلا تنزل تحت {Ar(floor)}: "
                + "السجل المحاسبي يبقى مقروءاً ومُصدَّراً، والإدخال والترحيل وحدهما يتوقّفان",
                $"'{m.Code}' is {was} and was asked to drop to {to}; it posts journal entries, "
                + $"so it may not fall below {floor}: the accounting record stays readable "
                + "and exportable, only entry and posting stop"));
        }

        return violations;
    }

    /// <summary>يرفض الانتقال المخالف كاملاً — بأرضيته وتماسكه معاً.</summary>
    /// <param name="current">المجموعة القائمة.</param>
    /// <param name="next">المجموعة المطلوبة.</param>
    /// <exception cref="IncoherentEntitlementSetException">الانتقال مخالف.</exception>
    public static void RequireTransition(
        IReadOnlyDictionary<string, EntitlementState> current,
        IReadOnlyDictionary<string, EntitlementState> next)
    {
        var v = ValidateTransition(current, next);
        if (v.Count > 0) throw new IncoherentEntitlementSetException(v);
    }

    /// <summary>
    /// المجموعة الناتجة عن <b>انقطاع سداد أو خفض حزمة</b>: ما تغطّيه الحزمة
    /// الجديدة يبقى كما هو، وما لا تغطّيه <b>يهبط إلى أرضيته</b> — لا إلى العدم.
    ///
    /// <para>ثم تُقصّ النتيجة على رسم الاعتماديات: وحدةٌ هبطت اعتماديتها لا يجوز
    /// أن تبقى فوقها (نقاط بيع فاعلة فوق مخزون موقوف الإدخال تبيع بلا حركة
    /// مخزون). والقصّ <b>نزولاً دائماً</b>، فلا يمنح أحداً استحقاقاً لم يشترِه.</para>
    /// </summary>
    /// <param name="current">المجموعة القائمة.</param>
    /// <param name="covered">الوحدات التي ما تزال الحزمة تغطّيها.</param>
    /// <returns>المجموعة الناتجة — متماسكة، ولا تنزل تحت الأرضيات.</returns>
    public static IReadOnlyDictionary<string, EntitlementState> Degrade(
        IReadOnlyDictionary<string, EntitlementState> current,
        IReadOnlySet<string> covered)
    {
        var next = new Dictionary<string, EntitlementState>(StringComparer.Ordinal);
        foreach (var m in ModuleCatalog.All)
        {
            var was = current.TryGetValue(m.Code, out var c) ? c : EntitlementState.NotEntitled;
            next[m.Code] = covered.Contains(m.Code)
                ? was
                : ModuleCatalog.LowestReachableFrom(m.Code, was);
        }

        // قصّ نزولي حتى الثبات: لا وحدة فوق أدنى اعتمادياتها.
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var m in ModuleCatalog.All.OrderBy(m => m.Code, StringComparer.Ordinal))
                foreach (var dep in m.DependsOn)
                {
                    var cap = next[dep];
                    if (next[m.Code] <= cap) continue;
                    next[m.Code] = cap;
                    changed = true;
                }
        }

        return next;
    }

    /// <summary>اسم الحالة بالعربية للعرض والرسائل.</summary>
    /// <param name="s">الحالة.</param>
    /// <returns>الاسم العربي.</returns>
    public static string Ar(EntitlementState s) => s switch
    {
        EntitlementState.Entitled => "مستحقّة",
        EntitlementState.ReadOnly => "قراءة فقط",
        _ => "غير مستحقّة"
    };
}
