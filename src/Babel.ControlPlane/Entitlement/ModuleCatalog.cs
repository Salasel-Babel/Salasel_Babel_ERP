using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Entitlement;

/// <summary>
/// وحدة قابلة للبيع.
/// <para><c>PostsJournal</c> هو التمييز الذي يجعل ADR-0014 قابلاً للتطبيق:
/// وحدة <b>لا تُرحّل قيوداً</b> (أداة تقارير) يمكن إلغاء تركيبها فعلاً بلا
/// ضرر — وهو الاستثناء الوحيد المذكور في «ولا ينقضه» هناك.</para>
///
/// <para><c>Floor</c> هو <b>أدنى حالة تبلغها الوحدة نزولاً بعد أن تكون قد
/// اشتُريت</b>. وهو <b>مُعلَن صفّاً صفّاً لا مُشتقّ ضمناً</b> كي يكون خطأ
/// المؤلّف القادم <b>مرئياً</b>: الحارس المعماري يقارن الإعلان بـ
/// <c>PostsJournal</c> ويُفشل البناء عند اختلافهما (القاعدة 6).</para>
/// </summary>
/// <param name="Code">رمز الوحدة.</param>
/// <param name="NameAr">اسمها بالعربية.</param>
/// <param name="NameEn">اسمها بالإنجليزية.</param>
/// <param name="PostsJournal">هل تُرحّل قيوداً إلى الأستاذ العام؟</param>
/// <param name="Floor">أدنى حالة تبلغها نزولاً بعد الشراء.</param>
/// <param name="SortOrder">ترتيب العرض.</param>
/// <param name="DependsOn">اعتمادياتها المباشرة.</param>
public sealed record ModuleDefinition(
    string Code, string NameAr, string NameEn, bool PostsJournal,
    EntitlementState Floor, int SortOrder,
    IReadOnlyList<string> DependsOn);

/// <summary>
/// كتالوج الوحدات ورسم اعتمادياتها. الرسم <b>بيان</b> لا شيفرة متفرّقة،
/// حتى يستطيع مُتحقِّق واحد أن يرفض مجموعة استحقاق غير متماسكة.
/// </summary>
public static class ModuleCatalog
{
    /// <summary>رمز وحدة الأستاذ العام — جذر رسم الاعتماديات.</summary>
    public const string Core = "CORE";

    /// <summary>
    /// كل الوحدات القابلة للبيع، مرتّبةً ترتيباً كلّياً ثابتاً برمزها.
    ///
    /// <para><b>العمود الخامس هو الأرضية.</b> ثماني وحدات تُرحّل قيوداً، فأرضيتها
    /// <c>ReadOnly</c>: تُخفَّض ولا تُنتزَع مهما كانت الخطة ومهما انقطع السداد.
    /// وواحدة لا تُرحّل قيوداً (‏<c>REP</c>) أرضيتها <c>NotEntitled</c>: تُنتزَع
    /// فعلاً، وهي الاستثناء المذكور في ADR-0014.</para>
    /// </summary>
    public static readonly IReadOnlyList<ModuleDefinition> All =
    [
        new("AP",  "المشتريات والذمم الدائنة", "Purchasing & payables", true,  EntitlementState.ReadOnly, 30, [Core]),
        new("AR",  "المبيعات والذمم المدينة",  "Sales & receivables",   true,  EntitlementState.ReadOnly, 20, [Core]),
        new("CORE","الأستاذ العام",            "General ledger",        true,  EntitlementState.ReadOnly, 10, []),
        new("FA",  "الأصول الثابتة",           "Fixed assets",          true,  EntitlementState.ReadOnly, 70, [Core, "AP"]),
        new("INV", "المخزون",                  "Inventory",             true,  EntitlementState.ReadOnly, 40, [Core, "AP"]),
        new("PAY", "الرواتب",                  "Payroll",               true,  EntitlementState.ReadOnly, 80, [Core]),
        new("POS", "نقاط البيع",               "Point of sale",         true,  EntitlementState.ReadOnly, 50, ["INV", "AR"]),
        new("PRJ", "المشاريع",                 "Projects",              true,  EntitlementState.ReadOnly, 60, ["INV", "AR"]),
        // الاستثناء المذكور في ADR-0014: لا تُرحّل قيوداً ⇒ تُلغى تركيباً فعلاً.
        new("REP", "التقارير التحليلية",        "Analytical reporting",  false, EntitlementState.NotEntitled, 90, [Core]),
    ];

    /// <summary>
    /// أدنى حالة تبلغها الوحدة <b>نزولاً بعد أن تكون قد اشتُريت</b>.
    /// <para>وهي لا تقيّد الصعود ولا الحالة الابتدائية: وحدةٌ لم تُشترَ قط تبقى
    /// <c>NotEntitled</c> ومخفيّة، والأرضية تلزم <b>الخفض</b> وحده.</para>
    /// </summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <returns>أدنى حالة مسموحة نزولاً.</returns>
    public static EntitlementState FloorOf(string code) => Require(code).Floor;

    /// <summary>هل هذه وحدة تُخفَّض ولا تُنتزَع؟</summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <returns><c>true</c> إن كانت أرضيتها فوق <c>NotEntitled</c>.</returns>
    public static bool MayOnlyBeDegraded(string code) =>
        FloorOf(code) > EntitlementState.NotEntitled;

    /// <summary>
    /// أدنى حالة مسموحة نزولاً <b>من حالة قائمة بعينها</b>: الأرضية تلزم متى
    /// كانت الوحدة فوق <c>NotEntitled</c> فعلاً، ولا تلزم قبل الشراء الأول.
    /// </summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <param name="current">حالتها القائمة.</param>
    /// <returns>الحدّ الأدنى الملزِم للحالة التالية.</returns>
    public static EntitlementState LowestReachableFrom(string code, EntitlementState current) =>
        current <= EntitlementState.NotEntitled
            ? EntitlementState.NotEntitled
            : (EntitlementState)Math.Min((int)current, (int)FloorOf(code));

    /// <summary>يُرجِع وحدة برمزها، ويرمي على رمز غير معروف.</summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <returns>تعريف الوحدة.</returns>
    /// <exception cref="ArgumentException">الرمز غير معروف.</exception>
    public static ModuleDefinition Require(string code) =>
        All.FirstOrDefault(m => m.Code == code)
        ?? throw new ArgumentException($"وحدة غير معروفة: «{code}»", nameof(code));

    /// <summary>الإغلاق المتعدّي للاعتماديات (‏POS ⇒ INV ⇒ AP ⇒ CORE).</summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <returns>كل ما تعتمد عليه مباشرةً وبالوساطة، مرتّباً.</returns>
    public static IReadOnlyList<string> TransitiveDependencies(string code)
    {
        var seen = new SortedSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(Require(code).DependsOn);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!seen.Add(cur)) continue;
            foreach (var d in Require(cur).DependsOn) stack.Push(d);
        }
        return [.. seen];
    }

    /// <summary>من يعتمد على هذه الوحدة مباشرةً — يقرؤه فحص الأرشفة والخفض.</summary>
    /// <param name="code">رمز الوحدة.</param>
    /// <returns>الوحدات المعتمِدة عليها مباشرةً، مرتّبةً.</returns>
    public static IReadOnlyList<string> Dependents(string code) =>
        [.. All.Where(m => m.DependsOn.Contains(code)).Select(m => m.Code)
               .OrderBy(x => x, StringComparer.Ordinal)];

    /// <summary>
    /// يكشف الحلقات في الرسم. رسم اعتماديات به حلقة يجعل كل تحقّق بعده بلا
    /// معنى، ويجب أن يُكتشف عند الإقلاع لا عند أول عميل.
    /// </summary>
    /// <returns>وصف كل حلقة مكتشَفة؛ قائمة فارغة تعني رساً سليماً.</returns>
    public static IReadOnlyList<string> DetectCycles()
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var cycles = new List<string>();
        var path = new List<string>();

        void Visit(string code)
        {
            state.TryGetValue(code, out var s);
            if (s == 1)
            {
                var at = path.IndexOf(code);
                cycles.Add(string.Join(" → ", path.Skip(at < 0 ? 0 : at).Append(code)));
                return;
            }
            if (s == 2) return;
            state[code] = 1;
            path.Add(code);
            foreach (var d in Require(code).DependsOn) Visit(d);
            path.RemoveAt(path.Count - 1);
            state[code] = 2;
        }

        foreach (var m in All.OrderBy(m => m.Code, StringComparer.Ordinal)) Visit(m.Code);
        return cycles;
    }

    /// <summary>يزرع الكتالوج في قاعدة التحكّم بصفوف مرتّبة (فخ-10).</summary>
    public static async Task SeedAsync(NpgsqlConnection c, CancellationToken ct = default)
    {
        var cycles = DetectCycles();
        if (cycles.Count > 0)
            throw new InvalidOperationException(
                "رسم اعتماديات الوحدات يحتوي حلقة: " + string.Join(" ; ", cycles));

        var mods = All.OrderBy(m => m.Code, StringComparer.Ordinal).ToList();
        var values = string.Join(", ", mods.Select((_, i) => $"(@c{i}, @ar{i}, @en{i}, @j{i}, @o{i})"));
        await Db.WriteAsync(c, $"""
            insert into control.module (module_code, name_ar, name_en, posts_journal, sort_order)
            values {values}
            on conflict (module_code) do update
               set name_ar = excluded.name_ar, name_en = excluded.name_en,
                   posts_journal = excluded.posts_journal, sort_order = excluded.sort_order
            """, mods.Count, p =>
            {
                for (var i = 0; i < mods.Count; i++)
                {
                    p.AddWithValue($"c{i}", mods[i].Code);
                    p.AddWithValue($"ar{i}", mods[i].NameAr);
                    p.AddWithValue($"en{i}", mods[i].NameEn);
                    p.AddWithValue($"j{i}", mods[i].PostsJournal);
                    p.AddWithValue($"o{i}", mods[i].SortOrder);
                }
            }, null, ct);

        var deps = mods.SelectMany(m => m.DependsOn.Select(d => (m.Code, Dep: d)))
                       .OrderBy(x => x.Code, StringComparer.Ordinal)
                       .ThenBy(x => x.Dep, StringComparer.Ordinal).ToList();
        if (deps.Count == 0) return;

        var dvalues = string.Join(", ", deps.Select((_, i) => $"(@m{i}, @d{i})"));
        await Db.WriteIdempotentManyAsync(c, $"""
            insert into control.module_dependency (module_code, depends_on)
            values {dvalues}
            on conflict (module_code, depends_on) do nothing
            """, deps.Count, p =>
            {
                for (var i = 0; i < deps.Count; i++)
                {
                    p.AddWithValue($"m{i}", deps[i].Code);
                    p.AddWithValue($"d{i}", deps[i].Dep);
                }
            }, null, ct);
    }
}
