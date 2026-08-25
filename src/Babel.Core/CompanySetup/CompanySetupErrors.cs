using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>حدود تأسيس المنشأة، معلنة مرّة واحدة ويقرؤها العقد المنشور والنواة معاً.</summary>
public static class CompanySetupLimits
{
    /// <summary>أقصى طول لاسم منشأة أو مركز تكلفة.</summary>
    public const int MaximumNameLength = 200;

    /// <summary>أقصى طول لوسم لغة BCP-47.</summary>
    public const int MaximumLanguageTagLength = 16;

    /// <summary>أقصى عدد ترجمات لاسم واحد.</summary>
    public const int MaximumTranslations = 32;

    /// <summary>أقصى عدد مراكز تكلفة لمنشأة واحدة.</summary>
    public const int MaximumCostCenters = 1000;

    /// <summary>أدنى طول لسبب الإيقاف — «لا سبب» ليس سبباً.</summary>
    public const int MinimumReasonLength = 8;

    /// <summary>أقصى طول لسبب الإيقاف.</summary>
    public const int MaximumReasonLength = 512;
}

/// <summary>
/// أخطاء تأسيس المنشأة ومراكز تكلفتها، برموز ثابتة هي ما تعتمد عليه الشيفرة.
/// </summary>
public static class CompanySetupErrors
{
    /// <summary>اسم المنشأة العربي مفقود.</summary>
    public static Error CompanyNameMissing { get; } = new(
        "company_setup.name_missing",
        "اسم المنشأة بالعربية إلزامي: هو السجلّ لا ترجمةً أولى، وهو ما يصير مركز التكلفة "
        + "الافتراضي حين لا تحتاج المنشأة مراكز متعدّدة.",
        "The company's Arabic name is mandatory: it is the record rather than a first translation, and it becomes "
        + "the default cost centre when the company does not need several.");

    /// <summary>لم تُؤسَّس المنشأة بعد.</summary>
    public static Error NotFound { get; } = new(
        "company_setup.not_found",
        "لم تُؤسَّس هذه المنشأة بعد. لا يوجد لها مقياس عرض ولا مركز تكلفة.",
        "This company has not been set up yet. It has no display scale and no cost centre.");

    /// <summary>محاولة تأسيس ثانية.</summary>
    public static Error AlreadyInitialised { get; } = new(
        "company_setup.already_initialised",
        "هذه المنشأة مؤسَّسة من قبل. عدد الخانات العشرية يُسنَد عند أول تأسيس ولا يُعدَّل بعده — "
        + "وتوحيده داخل دفاتر المنشأة الواحدة أهمّ من أي قيمة بعينها.",
        "This company is already set up. The number of decimal places is assigned at first setup and is never "
        + "editable afterwards — its consistency inside one entity's books matters more than any particular value.");

    /// <summary>اسم أول مركز تكلفة مطلوب حين تكون الإجابة «متعدّد».</summary>
    public static Error FirstCostCenterNameRequired { get; } = new(
        "company_setup.first_cost_center_name_required",
        "اخترتَ مراكز تكلفة متعدّدة، فاسم أول مركز إلزامي: العمود لا يُقبل فارغاً، "
        + "ولا يُخترَع له اسم نيابةً عنك حين تكون المراكز أكثر من واحد.",
        "You chose several cost centres, so the first centre's name is mandatory: the column is never accepted "
        + "empty, and no name is invented on your behalf once there is more than one centre.");

    /// <summary>اسم أول مركز تكلفة أُرسل مع إجابة «واحد».</summary>
    public static Error FirstCostCenterNameNotExpected { get; } = new(
        "company_setup.first_cost_center_name_not_expected",
        "اخترتَ مركز تكلفة واحداً، فاسمه هو اسم المنشأة نفسه ولا يُرسَل معه اسم آخر — "
        + "وإرساله يجعل الاسم المعروض غير الاسم الذي قُصد.",
        "You chose a single cost centre, so its name is the company's own name and no other name is sent with it — "
        + "sending one makes the displayed name differ from the intended one.");

    /// <summary>عدد خانات خارج المدى.</summary>
    /// <param name="places">العدد الواصل.</param>
    public static Error DecimalPlacesOutOfRange(int places) => new(
        "company_setup.decimal_places_out_of_range",
        string.Create(
            CultureInfo.InvariantCulture,
            $"عدد الخانات العشرية {places} خارج المدى المقبول {DisplayScale.Minimum}–{DisplayScale.Maximum}. "
            + $"والحدّ الأعلى هو مقياس التخزين نفسه: عرضٌ بخانات أكثر من التخزين يُظهر أصفاراً مخترَعة يظنّها القارئ دقّة."),
        string.Create(
            CultureInfo.InvariantCulture,
            $"The number of decimal places {places} is outside the accepted range {DisplayScale.Minimum}-{DisplayScale.Maximum}. "
            + $"The upper bound is the storage scale itself: displaying more places than are stored shows invented zeros that read as precision."));

    /// <summary>اسم مركز تكلفة مفقود.</summary>
    public static Error CostCenterNameMissing { get; } = new(
        "cost_center.name_missing",
        "اسم مركز التكلفة بالعربية إلزامي.",
        "The cost centre's Arabic name is mandatory.");

    /// <summary>اسم أطول من الحدّ.</summary>
    /// <param name="field">الحقل المعنيّ.</param>
    public static Error NameTooLong(string field) => new(
        "company_setup.name_too_long",
        string.Create(CultureInfo.InvariantCulture, $"الاسم في «{field}» يتجاوز {CompanySetupLimits.MaximumNameLength} محرفاً."),
        string.Create(CultureInfo.InvariantCulture, $"The name in '{field}' exceeds {CompanySetupLimits.MaximumNameLength} characters."));

    /// <summary>وسم لغة غير سليم الشكل.</summary>
    /// <param name="tag">الوسم الواصل.</param>
    public static Error LanguageTagMalformed(string tag) => new(
        "company_setup.language_tag_malformed",
        $"وسم اللغة «{tag}» ليس وسم BCP-47 سليم الشكل. والوسم معرّف لا نصّ معروض، فيُكتب لاتينياً.",
        $"The language tag '{tag}' is not a well-formed BCP-47 tag. A tag is an identifier, not displayed text, so it is written in ASCII.");

    /// <summary>ترجمة بلا نصّ.</summary>
    /// <param name="tag">الوسم المعنيّ.</param>
    public static Error TranslationEmpty(string tag) => new(
        "company_setup.translation_empty",
        $"الترجمة للوسم «{tag}» فارغة. ترجمة فارغة أسوأ من غيابها: الغياب يرتدّ إلى العربية، والفراغ يُعرض فراغاً.",
        $"The translation for tag '{tag}' is empty. An empty translation is worse than a missing one: a missing one falls back to Arabic, an empty one displays as blank.");

    /// <summary>عدد الترجمات فوق الحدّ.</summary>
    public static Error TooManyTranslations { get; } = new(
        "company_setup.too_many_translations",
        string.Create(CultureInfo.InvariantCulture, $"عدد الترجمات يتجاوز {CompanySetupLimits.MaximumTranslations}."),
        string.Create(CultureInfo.InvariantCulture, $"The number of translations exceeds {CompanySetupLimits.MaximumTranslations}."));

    /// <summary>مركز تكلفة غير موجود.</summary>
    /// <param name="code">الرمز المطلوب.</param>
    public static Error CostCenterNotFound(string code) => new(
        "cost_center.not_found",
        $"لا يوجد مركز تكلفة بالرمز «{code}» في هذه المنشأة.",
        $"No cost centre with the code '{code}' exists in this company.");

    /// <summary>اسم مركز تكلفة مكرَّر.</summary>
    /// <param name="nameAr">الاسم المكرَّر.</param>
    public static Error CostCenterNameRepeated(string nameAr) => new(
        "cost_center.name_repeated",
        $"يوجد مركز تكلفة باسم «{nameAr}» فعلاً. اسمان متطابقان على تقرير واحد يجعلان السطرين غير قابلين للتمييز.",
        $"A cost centre named '{nameAr}' already exists. Two identical names on one report make the two rows indistinguishable.");

    /// <summary>محاولة إيقاف المركز الافتراضي.</summary>
    /// <param name="code">رمز المركز الافتراضي.</param>
    public static Error DefaultCostCenterCannotBeSuspended(string code) => new(
        "cost_center.default_cannot_be_suspended",
        $"«{code}» هو مركز التكلفة الافتراضي لهذه المنشأة، ولا يُوقَف ولا يُحذف. "
        + "المنشأة لا تخلو من مركز تكلفة أبداً؛ فإن أردتَ إيقافه فانقل الافتراضي إلى مركز عامل آخر أولاً.",
        $"'{code}' is this company's default cost centre; it is neither suspended nor deleted. "
        + "A company is never without a cost centre; to suspend it, move the default to another active centre first.");

    /// <summary>المركز موقوف فعلاً.</summary>
    /// <param name="code">الرمز.</param>
    public static Error CostCenterAlreadySuspended(string code) => new(
        "cost_center.already_suspended",
        $"مركز التكلفة «{code}» موقوف فعلاً.",
        $"The cost centre '{code}' is already suspended.");

    /// <summary>المركز عامل فعلاً.</summary>
    /// <param name="code">الرمز.</param>
    public static Error CostCenterAlreadyActive(string code) => new(
        "cost_center.already_active",
        $"مركز التكلفة «{code}» عامل فعلاً.",
        $"The cost centre '{code}' is already active.");

    /// <summary>الافتراضي الجديد موقوف.</summary>
    /// <param name="code">الرمز.</param>
    public static Error DefaultMustBeActive(string code) => new(
        "cost_center.default_must_be_active",
        $"مركز التكلفة «{code}» موقوف، فلا يصلح افتراضياً: الافتراضي هو ما يُرحَّل عليه حين لا يختار المستخدم شيئاً.",
        $"The cost centre '{code}' is suspended and cannot be the default: the default is what gets posted to when the user picks nothing.");

    /// <summary>سبب الإيقاف مفقود أو أقصر من الحدّ.</summary>
    public static Error SuspensionReasonRequired { get; } = new(
        "cost_center.suspension_reason_required",
        string.Create(
            CultureInfo.InvariantCulture,
            $"إيقاف مركز تكلفة يحتاج سبباً مكتوباً لا يقلّ عن {CompanySetupLimits.MinimumReasonLength} محارف "
            + $"ولا يزيد على {CompanySetupLimits.MaximumReasonLength} — الإيقاف حالة عمل يضبطها إنسان، ويُسجَّل بسببه ومن فعلها."),
        string.Create(
            CultureInfo.InvariantCulture,
            $"Suspending a cost centre requires a written reason of at least {CompanySetupLimits.MinimumReasonLength} and at most "
            + $"{CompanySetupLimits.MaximumReasonLength} characters — suspension is a business state a person sets, and it is recorded with its reason and its actor."));

    /// <summary>عدد مراكز التكلفة فوق الحدّ.</summary>
    public static Error TooManyCostCenters { get; } = new(
        "cost_center.too_many",
        string.Create(CultureInfo.InvariantCulture, $"عدد مراكز التكلفة يتجاوز {CompanySetupLimits.MaximumCostCenters}."),
        string.Create(CultureInfo.InvariantCulture, $"The number of cost centres exceeds {CompanySetupLimits.MaximumCostCenters}."));
}
