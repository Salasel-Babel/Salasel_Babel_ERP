using System.Collections.Immutable;
using Babel.Core.CompanySetup;

namespace Babel.Api.Wire;

/// <summary>طلب تأسيس المنشأة — يُقبل مرّة واحدة.</summary>
/// <param name="CompanyNameAr">اسم المنشأة بالعربية. إلزامي وهو السجلّ (ADR-0021).</param>
/// <param name="CostCenters">الجواب عن سؤال مراكز التكلفة: <c>One</c> أو <c>Multiple</c>.</param>
/// <param name="DecimalPlaces">عدد الخانات العشرية المعروضة. يُسنَد هنا ولا يُعدَّل بعدها.</param>
/// <param name="CompanyNameTranslations">ترجمات اسم المنشأة بوسم اللغة.</param>
/// <param name="FirstCostCenterNameAr">اسم أول مركز تكلفة. إلزامي مع <c>Multiple</c>، ومرفوض مع <c>One</c>.</param>
/// <param name="FirstCostCenterTranslations">ترجمات اسم أول مركز.</param>
internal sealed record InitialiseCompanySetupRequestDto(
    string CompanyNameAr,
    string CostCenters,
    int DecimalPlaces,
    IReadOnlyList<NameValueDto>? CompanyNameTranslations = null,
    string? FirstCostCenterNameAr = null,
    IReadOnlyList<NameValueDto>? FirstCostCenterTranslations = null);

/// <summary>طلب إضافة مركز تكلفة أو إعادة تسميته.</summary>
/// <param name="NameAr">الاسم العربي. إلزامي.</param>
/// <param name="NameTranslations">ترجمات الاسم بوسم اللغة.</param>
internal sealed record CostCenterNameRequestDto(string NameAr, IReadOnlyList<NameValueDto>? NameTranslations = null);

/// <summary>طلب إيقاف مركز تكلفة.</summary>
/// <param name="Reason">السبب المكتوب. إلزامي — «لا سبب» ليس سبباً.</param>
internal sealed record SuspendCostCenterRequestDto(string Reason);

/// <summary>مركز تكلفة على السلك.</summary>
/// <param name="Code">الرمز — الهوية الثابتة التي تحملها سطور القيود.</param>
/// <param name="NameAr">الاسم العربي — الارتداد المضمون حين لا ترجمة.</param>
/// <param name="NameTranslations">الترجمات بوسم اللغة، مرتَّبة.</param>
/// <param name="State">‏<c>Active</c> أو <c>Suspended</c>.</param>
/// <param name="IsDefault">هل هو المركز الافتراضي؟ واحدٌ فقط يحمل <c>true</c>، ودائماً.</param>
/// <param name="SuspensionReason">سبب الإيقاف، أو نصّ فارغ.</param>
internal sealed record CostCenterDto(
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string State,
    bool IsDefault,
    string SuspensionReason);

/// <summary>تأسيس المنشأة كما يُقرأ.</summary>
/// <param name="NameAr">اسم المنشأة بالعربية.</param>
/// <param name="NameTranslations">ترجمات الاسم.</param>
/// <param name="DecimalPlaces">عدد الخانات المعروضة — مُسنَد مرّة، غير قابل للتعديل.</param>
/// <param name="DefaultCostCenter">رمز المركز الافتراضي. غير فارغ أبداً.</param>
/// <param name="CostCenters">مراكز التكلفة كلّها — العاملة والموقوفة — مرتَّبة برمزها.</param>
internal sealed record CompanySetupDto(
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    int DecimalPlaces,
    string DefaultCostCenter,
    IReadOnlyList<CostCenterDto> CostCenters);

/// <summary>
/// النقل بين السلك ونواة التأسيس — <b>نقلٌ لا قرار</b>.
/// <para>
/// لا فحص هنا ولا حكم: الاسم الفارغ، والوسم المعطوب، وعدد الخانات خارج المدى، وجواب
/// «متعدّد» بلا اسم أول — كلها تُرجع من النواة برموزها. وما يقع هنا تحويل شكل، ورفضُ
/// التكرار عند الحدّ لأن «أي القيمتين» سؤال بلا جواب.
/// </para>
/// </summary>
internal static class CompanySetupWire
{
    /// <summary>يحوّل طلب التأسيس إلى مسوّدة.</summary>
    /// <param name="dto">الطلب.</param>
    public static CompanySetupDraft ToDraft(InitialiseCompanySetupRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new CompanySetupDraft(
            dto.CompanyNameAr,
            ToTranslations(dto.CompanyNameTranslations, "companyNameTranslations"),
            ToPlan(dto.CostCenters),
            dto.FirstCostCenterNameAr,
            ToTranslations(dto.FirstCostCenterTranslations, "firstCostCenterTranslations"),
            dto.DecimalPlaces);
    }

    /// <summary>يقرأ ترجمات اسم مركز تكلفة من طلب إضافة أو إعادة تسمية.</summary>
    /// <param name="dto">الطلب.</param>
    public static IReadOnlyDictionary<string, string> ToTranslations(CostCenterNameRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return ToTranslations(dto.NameTranslations, "nameTranslations");
    }

    /// <summary>يحوّل تأسيساً قائماً إلى شكله على السلك.</summary>
    /// <param name="setup">التأسيس.</param>
    public static CompanySetupDto ToDto(FoundedCompany setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        return new CompanySetupDto(
            setup.NameAr,
            [.. setup.Translations.Select(static entry => new NameValueDto(entry.Key, entry.Value))],
            setup.DisplayScale.Places,
            setup.CostCenters.Default.Value,
            [.. setup.CostCenters.All.Select(center => new CostCenterDto(
                center.Code.Value,
                center.NameAr,
                [.. center.Translations.Select(static entry => new NameValueDto(entry.Key, entry.Value))],
                center.State.ToString(),
                center.Code == setup.CostCenters.Default,
                center.SuspensionReason))]);
    }

    private static CostCenterPlan ToPlan(string? answer)
        => Enum.TryParse(answer, ignoreCase: false, out CostCenterPlan plan) && Enum.IsDefined(plan)
            ? plan
            : throw WireNumbers.Reject(
                "wire.body.malformed",
                "costCenters",
                $"جواب مراكز التكلفة «{answer}» غير معروف. المقبول: One أو Multiple، حرفياً وبحساسية حالة الأحرف.",
                $"The cost-centre answer '{answer}' is unknown. Accepted: One or Multiple, literally and case-sensitively.");

    private static ImmutableSortedDictionary<string, string> ToTranslations(
        IReadOnlyList<NameValueDto>? entries,
        string field)
    {
        ImmutableSortedDictionary<string, string>.Builder builder =
            ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (NameValueDto entry in entries ?? [])
        {
            if (builder.ContainsKey(entry.Name))
            {
                throw WireNumbers.Reject(
                    "wire.body.repeated",
                    field,
                    $"ترجمة مكرَّرة للوسم «{entry.Name}».",
                    $"A repeated translation for the tag '{entry.Name}'.");
            }

            builder[entry.Name] = entry.Value;
        }

        return builder.ToImmutable();
    }
}
