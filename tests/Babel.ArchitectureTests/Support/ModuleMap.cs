using Babel.SharedKernel;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// خريطة الاعتماد المسموح بها — إعلان واحد تقرأه كل قواعد الاتجاه.
/// <para>
/// الاتجاه دائماً إلى الأسفل: SharedKernel ← Contracts ← Core ← Ledger، والوحدات الأفقية
/// فوقها لا تعتمد على بعضها ولا على الدفتر. مشروع واحد فقط يعرف الجميع: Babel.Api.
/// </para>
/// <para>
/// إضافة سطر هنا قرار معماري واعٍ يمرّ بمراجعة — وهذا هو المقصود بالضبط.
/// </para>
/// </summary>
internal static class ModuleMap
{
    public const string SharedKernel = "Babel.SharedKernel";
    public const string Contracts = "Babel.Contracts";
    public const string Core = "Babel.Core";

    /// <summary>
    /// مكتبة التوحيد القياسي — <b>ليست وحدة منتج</b> بل مكتبة بلا اعتماديات إطلاقاً،
    /// وهي الطريق الوحيد إلى دالة التجزئة. لا تدخل <see cref="BabelModule"/> ولا
    /// الاستحقاق ولا القياس: لا يشتريها عميل ولا تُطفأ. حدودها مفروضة في Rule10.
    /// </summary>
    public const string Canonicalization = "Babel.Canonicalization";

    public const string Ledger = "Babel.Ledger";
    public const string Api = "Babel.Api";

    /// <summary>الوحدات الأفقية: لا تستدعي بعضها مباشرة، بل عبر Contracts أو الأحداث.</summary>
    public static IReadOnlyList<string> Horizontal { get; } =
    [
        "Babel.Sales",
        "Babel.Purchasing",
        "Babel.Compliance",
        "Babel.Inventory",
        "Babel.Pos",
        "Babel.Hr",
        "Babel.Projects",
        "Babel.RealEstate",
        "Babel.Assets",
        "Babel.Portals",
        "Babel.Ai",
    ];

    /// <summary>كل مشاريع المنتج.</summary>
    public static IReadOnlyList<string> AllProjects { get; } =
        [SharedKernel, Contracts, Canonicalization, Core, Ledger, .. Horizontal, Api];

    /// <summary>اسم مشروع الوحدة.</summary>
    public static string ProjectOf(BabelModule module) => "Babel." + module;

    /// <summary>المراجع المسموح بها لكل مشروع. ما ليس في القائمة ممنوع.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedProjectReferences { get; } = Build();

    private static Dictionary<string, IReadOnlySet<string>> Build()
    {
        Dictionary<string, IReadOnlySet<string>> allowed = new(StringComparer.Ordinal)
        {
            [SharedKernel] = new HashSet<string>(StringComparer.Ordinal),
            [Contracts] = new HashSet<string>([SharedKernel], StringComparer.Ordinal),
            [Core] = new HashSet<string>([SharedKernel, Contracts], StringComparer.Ordinal),

            // المكتبة لا تعتمد على شيء إطلاقاً — ولا حتى على النواة المشتركة.
            // البايتات المُجزَّأة يجب ألا تتحرّك أبداً بسبب ترقية اعتمادية.
            [Canonicalization] = new HashSet<string>(StringComparer.Ordinal),

            // الدفتر وحده يجوز أن يعتمد على المُوحِّد: هو الجهة الوحيدة التي تُجزّئ قيداً.
            [Ledger] = new HashSet<string>([SharedKernel, Contracts, Core, Canonicalization], StringComparer.Ordinal),

            // الجذر التركيبي وحده يعرف الجميع.
            [Api] = new HashSet<string>(AllProjects.Where(static p => p != Api), StringComparer.Ordinal),
        };

        foreach (string horizontal in Horizontal)
        {
            // لا Ledger — الترحيل عبر العقد. ولا وحدة أفقية أخرى — التخاطب بالأحداث.
            allowed[horizontal] = new HashSet<string>([SharedKernel, Contracts, Core], StringComparer.Ordinal);
        }

        return allowed;
    }
}
