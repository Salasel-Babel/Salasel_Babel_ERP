using System.Collections.Immutable;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// جواب المنشأة عن سؤال مراكز التكلفة — <b>يُسأل مرّة واحدة عند التأسيس</b>.
/// </summary>
public enum CostCenterPlan
{
    /// <summary>
    /// مركز واحد. لا يُسأل المستخدم عن اسمه: اسم المنشأة نفسه يصير اسم المركز الافتراضي،
    /// ولا يرى صاحبُ هذا الجواب كلمة «مركز تكلفة» في أي شاشة بعدها.
    /// </summary>
    One = 0,

    /// <summary>
    /// مراكز متعدّدة. اسم أول مركز <b>إلزامي</b>: من أعلن أن لديه أكثر من واحد لا يُخترَع
    /// له اسم نيابةً عنه.
    /// </summary>
    Multiple = 1,
}

/// <summary>
/// ما يصل من العميل عند التأسيس. <b>مسوّدة لا تأسيس</b>: لا تقول شيئاً عن صلاحية ما تحمله،
/// ولا تُخزَّن، والطريق الوحيد إلى منشأة مؤسَّسة هو
/// <see cref="FoundedCompany.Found(TenantId, CompanySetupDraft)"/>.
/// </summary>
/// <param name="CompanyNameAr">اسم المنشأة بالعربية — إلزامي، وهو السجلّ (ADR-0021).</param>
/// <param name="CompanyNameTranslations">ترجمات اسم المنشأة بوسم اللغة، إن وُجدت.</param>
/// <param name="CostCenters">الجواب عن سؤال مراكز التكلفة.</param>
/// <param name="FirstCostCenterNameAr">
/// اسم أول مركز تكلفة. إلزامي مع <see cref="CostCenterPlan.Multiple"/>، ومرفوض مع
/// <see cref="CostCenterPlan.One"/> — لأن اسمه هناك اسم المنشأة بعينه.
/// </param>
/// <param name="FirstCostCenterTranslations">ترجمات اسم أول مركز، إن وُجدت.</param>
/// <param name="DecimalPlaces">عدد الخانات العشرية المعروضة. يُسنَد هنا ولا يُعدَّل بعدها أبداً.</param>
public sealed record CompanySetupDraft(
    string CompanyNameAr,
    IReadOnlyDictionary<string, string>? CompanyNameTranslations,
    CostCenterPlan CostCenters,
    string? FirstCostCenterNameAr,
    IReadOnlyDictionary<string, string>? FirstCostCenterTranslations,
    int DecimalPlaces);

/// <summary>
/// <b>منشأة مؤسَّسة، صالحةً بحكم وجودها.</b>
/// <para>
/// لا مُنشئ عام، ولا مُهيّئ خصائص، ولا مصنع ثانٍ: الطريق الوحيد هو
/// <see cref="Found(TenantId, CompanySetupDraft)"/>. وقيمةٌ من هذا النوع تعني — بلا فحص
/// إضافي عند أي مستدعٍ — أن للمنشأة اسماً عربياً، ومقياس عرض مُسنَداً، و<b>مركز تكلفة
/// واحداً على الأقل بافتراضيٍّ عامل</b>.
/// </para>
/// <para>
/// <b>وثباتُ مقياس العرض بنيويٌّ أيضاً:</b> الطريقة الوحيدة لاشتقاق قيمة أخرى من هذه هي
/// <see cref="WithCostCenters(CostCenterRegister)"/>، وهي <b>تنسخ المقياس ولا تقبله</b> —
/// فلا يوجد في الشجرة توقيعٌ واحد يستطيع أن يحمل مقياساً ثانياً إلى منشأة قائمة، ولا
/// في المخزن كذلك (انظر <see cref="ICompanySetupStore"/>).
/// </para>
/// </summary>
public sealed class FoundedCompany
{
    private FoundedCompany(
        TenantId company,
        TranslatedName name,
        DisplayScale displayScale,
        CostCenterRegister costCenters)
    {
        Company = company;
        Name = name;
        DisplayScale = displayScale;
        CostCenters = costCenters;
    }

    /// <summary>المنشأة.</summary>
    public TenantId Company { get; }

    /// <summary>
    /// اسم المنشأة: سجلٌّ عربي إلزامي وترجماتٌ صفوف — النوع نفسه المشترك مع كل كيان
    /// مُسمّى، فلا تُعاد كتابة قاعدة الارتداد في كل موضع (ADR-0021).
    /// </summary>
    public TranslatedName Name { get; }

    /// <summary>اسم المنشأة بالعربية — إلزامي، وهو الارتداد المضمون.</summary>
    public string NameAr => Name.Arabic;

    /// <summary>ترجمات الاسم بوسم اللغة، مرتَّبة ترتيباً حرفياً ثابتاً.</summary>
    public ImmutableSortedDictionary<string, string> Translations => Name.Translations;

    /// <summary>مقياس العرض. مُسنَد عند التأسيس، ولا يتغيّر بعده.</summary>
    public DisplayScale DisplayScale { get; }

    /// <summary>سجلّ مراكز التكلفة. غير فارغ بحكم بنائه.</summary>
    public CostCenterRegister CostCenters { get; }

    /// <summary>
    /// يؤسّس منشأة من مسوّدة، أو يُرجع <b>كل</b> أسباب الرفض مجتمعة — لا أوّلها: من يصلح
    /// حقلاً ليكتشف التالي يظنّ أن العدد واحد وهو ثلاثة.
    /// </summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="draft">المسوّدة الواصلة.</param>
    public static Result<FoundedCompany> Found(TenantId company, CompanySetupDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        List<Error> errors = [];

        string nameAr = draft.CompanyNameAr?.Trim() ?? string.Empty;

        if (nameAr.Length == 0)
        {
            errors.Add(CompanySetupErrors.CompanyNameMissing);
        }
        else if (nameAr.Length > CompanySetupLimits.MaximumNameLength)
        {
            errors.Add(CompanySetupErrors.NameTooLong("companyNameAr"));
        }

        ImmutableSortedDictionary<string, string> translations =
            CostCenterRegister.NormaliseTranslations(draft.CompanyNameTranslations, errors);

        string firstCostCenter = draft.FirstCostCenterNameAr?.Trim() ?? string.Empty;

        // ── سؤال المالك، مطبَّقاً حرفياً ────────────────────────────────────────
        // «لا» ⇒ اسم المنشأة يصير المركز الافتراضي · «متعدّد» ⇒ اسم أول مركز إلزامي.
        switch (draft.CostCenters)
        {
            case CostCenterPlan.One when firstCostCenter.Length > 0:
                errors.Add(CompanySetupErrors.FirstCostCenterNameNotExpected);
                break;

            case CostCenterPlan.One:
                firstCostCenter = nameAr;
                break;

            case CostCenterPlan.Multiple when firstCostCenter.Length == 0:
                errors.Add(CompanySetupErrors.FirstCostCenterNameRequired);
                break;

            default:
                break;
        }

        Result<DisplayScale> scale = DisplayScale.Of(draft.DecimalPlaces);

        if (scale.IsFailure)
        {
            errors.AddRange(scale.Errors);
        }

        IReadOnlyDictionary<string, string>? costCenterTranslations = draft.CostCenters == CostCenterPlan.One
            ? draft.CompanyNameTranslations
            : draft.FirstCostCenterTranslations;

        Result<CostCenterRegister> register = firstCostCenter.Length == 0
            ? Result<CostCenterRegister>.Failure(CompanySetupErrors.CostCenterNameMissing)
            : CostCenterRegister.Open(firstCostCenter, costCenterTranslations);

        // اسمٌ مفقود سُجّل خطؤه أعلاه؛ فلا يُكرَّر خطؤه من جهة السجلّ.
        if (register.IsFailure && firstCostCenter.Length > 0)
        {
            errors.AddRange(register.Errors);
        }

        return errors.Count > 0
            ? Result<FoundedCompany>.Failure(errors)
            : Result<FoundedCompany>.Success(
                new FoundedCompany(company, new TranslatedName(nameAr, translations), scale.Value, register.Value));
    }

    /// <summary>
    /// يشتقّ منشأة بسجلّ مراكز تكلفة جديد. <b>المقياس يُنسَخ ولا يُقبل</b> — وذلك هو
    /// إنفاذ ثباته، لا التوثيق.
    /// </summary>
    /// <param name="costCenters">السجلّ الجديد.</param>
    public FoundedCompany WithCostCenters(CostCenterRegister costCenters)
    {
        ArgumentNullException.ThrowIfNull(costCenters);
        return new FoundedCompany(Company, Name, DisplayScale, costCenters);
    }

    /// <summary>اسم المنشأة بلغة العرض، مرتدّاً إلى العربية (ADR-0021).</summary>
    /// <param name="languageTag">وسم اللغة المطلوب.</param>
    public string NameIn(string? languageTag) => Name.In(languageTag);

    /// <summary>اسم المنشأة بلغة العرض <b>مع إعلان الارتداد</b>.</summary>
    /// <param name="languageTag">وسم اللغة المطلوب.</param>
    public NameResolution ResolveName(string? languageTag) => Name.Resolve(languageTag);
}
