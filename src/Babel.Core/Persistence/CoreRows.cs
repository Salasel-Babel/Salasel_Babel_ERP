namespace Babel.Core.Persistence;

/// <summary>
/// صفّ تأسيس المنشأة: اسمها العربي، ومقياس عرضها، ورمز مركزها الافتراضي.
/// <para>
/// <c>internal</c>: أنواع الاستمرارية لا تعبر حدّ الوحدة أبداً — ما يعبر واجهاتٌ معلنة
/// وعقود، لا كيانات EF (القاعدة 5).
/// </para>
/// <para>
/// <b>ولا عمود لغة ثانية هنا</b> (ADR-0021 · ADR-0027): العربي عمودٌ لأنه <b>السجلّ</b>،
/// وكل ما سواه صفٌّ في <see cref="CoreNameTranslationRow"/>.
/// </para>
/// </summary>
internal sealed class CompanySetupRow
{
    /// <summary>المنشأة — وهي المفتاح، فالتأسيس واحدٌ لكل منشأة بحكم المخطّط لا بحكم مستدعٍ منضبط.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>الاسم العربي — إلزامي وغير فارغ.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>عدد الخانات العشرية المعروضة. مُسنَد عند التأسيس، ويمنع مشغّلٌ تغييره بعده.</summary>
    public int DecimalPlaces { get; set; }

    /// <summary>رمز المركز الافتراضي.</summary>
    public string DefaultCostCenter { get; set; } = string.Empty;

    /// <summary>لحظة التأسيس.</summary>
    public DateTimeOffset FoundedAt { get; set; }
}

/// <summary>
/// صفّ مركز تكلفة.
/// <para>
/// <b>ولا عمود «محذوف» ولا مسار حذف:</b> المركز الذي يخرج من الاستعمال يُوقَف
/// (ADR-0006)، وصلاحية <c>DELETE</c> على هذا الجدول مسحوبة من دور التطبيق صراحةً —
/// فغياب الحذف مفروضٌ في PostgreSQL كما هو مفروضٌ في النوع.
/// </para>
/// </summary>
internal sealed class CostCenterRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>الرمز — الهوية الثابتة التي تحملها سطور القيود.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربي — إلزامي وغير فارغ.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>الحالة: <c>active</c> أو <c>suspended</c>.</summary>
    public string State { get; set; } = CostCenterStates.Active;

    /// <summary>سبب الإيقاف مكتوباً، أو نصّ فارغ إن كان عاملاً.</summary>
    public string SuspensionReason { get; set; } = string.Empty;
}

/// <summary>حالات مركز التكلفة كما تُكتب في المخطّط. مجموعة مغلقة يقابلها قيد تحقّق.</summary>
internal static class CostCenterStates
{
    /// <summary>عامل.</summary>
    public const string Active = "active";

    /// <summary>موقوف عن الترحيل.</summary>
    public const string Suspended = "suspended";
}

/// <summary>
/// <b>ترجمة اسم — صفٌّ لا عمود.</b> نظير <c>ledger.name_translation</c> داخل النواة.
/// <para>
/// ‏ADR-0021 بند 2 مُنفَّذاً: العربي عمودٌ على الكيان لأنه السجلّ، وكل ما سواه صفٌّ هنا
/// بمفتاح (منشأة × نوع كيان × مفتاح كيان × لغة). فإضافة الأردية أو الهندية <b>إدخالُ
/// صفوف لا هجرةُ مخطّط ولا إصدار برمجي</b>.
/// </para>
/// </summary>
internal sealed class CoreNameTranslationRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>نوع الكيان — مجموعة مغلقة يفرضها قيد في قاعدة البيانات.</summary>
    public string EntityKind { get; set; } = string.Empty;

    /// <summary>المفتاح الطبيعي للكيان: معرّف المنشأة نصّاً، أو رمز مركز التكلفة.</summary>
    public string EntityKey { get; set; } = string.Empty;

    /// <summary>وسم اللغة BCP-47. والعربية مرفوضة هنا: هي السجلّ لا ترجمةً له.</summary>
    public string LanguageTag { get; set; } = string.Empty;

    /// <summary>النصّ المترجَم. غير فارغ — والغياب يُعبَّر عنه بغياب الصفّ.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>أنواع الكيانات المُترجَمة في النواة. مجموعة مغلقة يقابلها قيد في المخطّط.</summary>
internal static class CoreTranslationKinds
{
    /// <summary>اسم المنشأة. المفتاح معرّفها نصّاً.</summary>
    public const string Company = "company";

    /// <summary>اسم مركز تكلفة. المفتاح رمزه.</summary>
    public const string CostCenter = "cost_center";
}

/// <summary>
/// نوع مستند في ملفّ قدرات منشأة. وجود الصفّ يعني «هذا النوع في الملفّ».
/// </summary>
internal sealed class CapabilityProfileDocumentRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>رمز نوع المستند.</summary>
    public string DocumentType { get; set; } = string.Empty;
}

/// <summary>
/// قدرةٌ على نوع مستند، وقرار المنشأة فيها.
/// <para>
/// <b>المخزَّن قرارُ المستأجر لا الشكل المشتقّ منه:</b> الشكل دالّةٌ في العقد المنشور
/// ويتغيّر بتغيّره، والقرار «هذه القدرة مُشغَّلة» هو ما يبقى صحيحاً عبر الإصدارات.
/// </para>
/// </summary>
internal sealed class CapabilityProfileCapabilityRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>رمز نوع المستند.</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>رمز القدرة.</summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>هل هي مُشغَّلة؟</summary>
    public bool Enabled { get; set; }
}

/// <summary>قيمة افتراضية لحقل على نوع مستند.</summary>
internal sealed class CapabilityProfileDefaultRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>رمز نوع المستند.</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>اسم الحقل.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>القيمة الافتراضية.</summary>
    public string Value { get; set; } = string.Empty;
}
