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
    public const string Compliance = "Babel.Compliance";

    /// <summary>
    /// مكتبة التوحيد القياسي: أساس تحت الجميع، بلا حزمة خارجية واحدة وبلا معرفة بأي وحدة.
    /// بايتاتها المُجزَّأة لا يجوز أن تتحرّك بسبب ترقية اعتمادية، ولذلك لا اعتمادية لها أصلاً.
    /// </summary>
    public const string Canonicalization = "Babel.Canonicalization";

    /// <summary>
    /// مستوى التحكّم: سجل المستأجرين والتزويد وترحيل الأسطول والاتصالات والاستحقاق والقياس.
    /// يعمل <b>فوق</b> الأسطول لا داخل مستأجر، فلا مكان له في خريطة الوحدات ولا استحقاق له.
    /// ولا يعتمد على أي مشروع بابل — مجموعة مراجعه المسموحة فارغة أدناه.
    /// </summary>
    public const string ControlPlane = "Babel.ControlPlane";

    /// <summary>
    /// عقد حدّ الالتزام: أنواع فقط، بلا EF ولا Wolverine ولا Npgsql ولا HTTP.
    /// خاصية «بلا اعتمادية خارج مكتبة الأساس» مفروضة أيضاً باختبار في مجموعة اختبارات الالتزام.
    /// </summary>
    public const string ComplianceAbstractions = "Babel.Compliance.Abstractions";

    /// <summary>مزوّد وهمي كامل التنفيذ للاختبار: يعتمد على العقد وحده، ولا يعرف التنسيق.</summary>
    public const string ComplianceFakeProvider = "Babel.Compliance.FakeProvider";

    /// <summary>محوّل الصندوق الصادر: يعزل Wolverine كي يبقى تشغيل الالتزام ممكناً بدونها.</summary>
    public const string ComplianceWolverine = "Babel.Compliance.Wolverine";

    /// <summary>الوحدات الأفقية: لا تستدعي بعضها مباشرة، بل عبر Contracts أو الأحداث.</summary>
    public static IReadOnlyList<string> Horizontal { get; } =
    [
        "Babel.Sales",
        "Babel.Purchasing",
        Compliance,
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
    /// مشاريع مساندة: أساس تحت الجميع، أو عقد وحدة، أو محوّل مورّد. ليست وحدات منتج،
    /// فلا بطاقة وحدة لها ولا استحقاق ولا مدخل في <c>BabelModule</c> — ولا تدخل قائمة
    /// الوحدات الأفقية، لأن الوحدة الأفقية تُمنع من الاعتماد على أخواتها، وهذه يُعتمد عليها.
    /// </summary>
    public static IReadOnlyList<string> Supporting { get; } =
    [
        Canonicalization,
        ControlPlane,
        ComplianceAbstractions,
        ComplianceFakeProvider,
        ComplianceWolverine,
    ];

    /// <summary>كل مشاريع المنتج.</summary>
    public static IReadOnlyList<string> AllProjects { get; } =
        [SharedKernel, Contracts, Core, Ledger, .. Horizontal, .. Supporting, Api];

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

            // الأساس والعقود والمحوّلات: الاتجاه هنا أيضاً إلى الأسفل حصراً.
            [Canonicalization] = new HashSet<string>(StringComparer.Ordinal),
            [ControlPlane] = new HashSet<string>(StringComparer.Ordinal),
            [ComplianceAbstractions] = new HashSet<string>(StringComparer.Ordinal),
            [ComplianceFakeProvider] = new HashSet<string>([ComplianceAbstractions], StringComparer.Ordinal),
            [ComplianceWolverine] = new HashSet<string>([Compliance, ComplianceAbstractions], StringComparer.Ordinal),

            // الجذر التركيبي وحده يعرف الجميع.
            [Api] = new HashSet<string>(AllProjects.Where(static p => p != Api), StringComparer.Ordinal),
        };

        foreach (string horizontal in Horizontal)
        {
            // لا Ledger — الترحيل عبر العقد. ولا وحدة أفقية أخرى — التخاطب بالأحداث.
            HashSet<string> references = new([SharedKernel, Contracts, Core], StringComparer.Ordinal);

            // استثناء واحد معلن: وحدة الالتزام تعتمد على عقد حدّ الالتزام — وهو أنواع
            // بلا مورّد ولا استمرارية، لا وحدة أفقية. الغرض منه أن يبقى اسم المزوّد
            // خارج الوحدة نفسها؛ حذفه يعني عودة اسم المزوّد إلى قلب التنسيق.
            if (horizontal == Compliance)
            {
                references.Add(ComplianceAbstractions);
            }

            allowed[horizontal] = references;
        }

        return allowed;
    }
}
