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

    /// <summary>
    /// مشاريع <b>بنية تحتية</b>: ليست وحدات منتَج مُرخَّصة — لا تظهر في
    /// <see cref="BabelModule"/>، ولا تحمل بطاقة <c>ModuleInfo</c>، ولا تُباع على حدة.
    /// <para>
    /// وقاعدتها الملزمة: <b>لا تعتمد على أي مشروع بابل، ولا يعتمد عليها أي مشروع
    /// إلا الجذر التركيبي.</b> ولذلك مجموعة مراجعها المسموحة فارغة أدناه.
    /// </para>
    /// <list type="bullet">
    /// <item><c>Babel.Canonicalization</c> — مكتبة التوحيد القياسي؛ صفر اعتماديات
    /// شرطٌ فيها حتى لا تتحرّك البايتات المُجزَّأة بترقية حزمة.</item>
    /// <item><c>Babel.ControlPlane</c> — مستوى التحكّم؛ يعمل <b>فوق</b> الأسطول
    /// (‏سجل المستأجرين والتزويد وترحيل الأسطول والاتصالات والاستحقاق والقياس)
    /// لا داخل مستأجر، فلا مكان له في خريطة الوحدات.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> Infrastructure { get; } =
    [
        "Babel.Canonicalization",
        "Babel.ControlPlane",
    ];

    /// <summary>كل مشاريع المنتج.</summary>
    public static IReadOnlyList<string> AllProjects { get; } =
        [SharedKernel, Contracts, Core, Ledger, .. Horizontal, .. Infrastructure, Api];

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
            [Ledger] = new HashSet<string>([SharedKernel, Contracts, Core], StringComparer.Ordinal),

            // الجذر التركيبي وحده يعرف الجميع.
            [Api] = new HashSet<string>(AllProjects.Where(static p => p != Api), StringComparer.Ordinal),
        };

        // بنية تحتية: لا مرجع إلى أي مشروع بابل — والقائمة الفارغة هي الإنفاذ.
        foreach (string infrastructure in Infrastructure)
        {
            allowed[infrastructure] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (string horizontal in Horizontal)
        {
            // لا Ledger — الترحيل عبر العقد. ولا وحدة أفقية أخرى — التخاطب بالأحداث.
            allowed[horizontal] = new HashSet<string>([SharedKernel, Contracts, Core], StringComparer.Ordinal);
        }

        return allowed;
    }
}
