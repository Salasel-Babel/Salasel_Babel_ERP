using Babel.Core.Presets;

namespace Babel.Api.Wire;

/// <summary>طلب استبدال ثوابت الشركة كلّها.</summary>
/// <param name="Values">المفاتيح وقيمها. قيمةٌ فارغة تزيل الثابت، ومفتاحٌ غائب كذلك.</param>
internal sealed record ReplaceCompanyPresetsRequestDto(IReadOnlyList<NameValueDto> Values);

/// <summary>ثابت في الكتالوج كما يُنشر — ليرسم الإعداد حقوله من العقد لا من قائمة مكتوبة بيد.</summary>
/// <param name="Key">المفتاح.</param>
/// <param name="Kind">نوع القيمة.</param>
/// <param name="Choices">الأعضاء المسموحة حين يكون النوع اختياراً.</param>
internal sealed record PresetKeyDto(string Key, string Kind, IReadOnlyList<string> Choices);

/// <summary>سلسلة ترقيم كما تُنشر.</summary>
/// <param name="Code">رمز السلسلة.</param>
/// <param name="DefaultPrefix">البادئة الافتراضية.</param>
internal sealed record DocumentSeriesDto(string Code, string DefaultPrefix);

/// <summary>ثوابت الشركة كما تُقرأ: ما ضُبط، ومعه الكتالوج والسلاسل.</summary>
/// <param name="Values">الثوابت المضبوطة مرتَّبة بالمفتاح.</param>
/// <param name="Catalogue">الكتالوج المغلق.</param>
/// <param name="Series">سلاسل الترقيم ببادئاتها الافتراضية.</param>
internal sealed record CompanyPresetsDto(
    IReadOnlyList<NameValueDto> Values,
    IReadOnlyList<PresetKeyDto> Catalogue,
    IReadOnlyList<DocumentSeriesDto> Series);

/// <summary>طلب تخصيص رقم.</summary>
/// <param name="On">تاريخ المستند بصيغة yyyy-MM-dd — سنتُه هي سنة العدّاد.</param>
internal sealed record AllocateNumberRequestDto(string On);

/// <summary>رقم مخصَّص.</summary>
/// <param name="Series">السلسلة.</param>
/// <param name="FiscalYear">السنة.</param>
/// <param name="Sequence">التسلسل داخل السنة.</param>
/// <param name="Number">الرقم المركّب الذي يُرسل في المستند.</param>
internal sealed record AllocatedNumberDto(string Series, int FiscalYear, int Sequence, string Number);

/// <summary>نقل ثوابت الشركة بين السلك والنواة — شكلٌ لا حكم.</summary>
internal static class CompanyPresetWire
{
    /// <summary>يحوّل الطلب إلى أزواج كما وصلت؛ التصديق في النواة.</summary>
    /// <param name="dto">الطلب.</param>
    public static IReadOnlyList<KeyValuePair<string, string>> ToValues(ReplaceCompanyPresetsRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Values is null)
        {
            throw WireNumbers.Reject("wire.body.field_missing", "values", "القائمة إلزامية ولو فارغة.", "The list is mandatory, even when empty.");
        }

        return dto.Values
            .Select(static pair => new KeyValuePair<string, string>(pair.Name ?? string.Empty, pair.Value ?? string.Empty))
            .ToList();
    }

    /// <summary>يقرأ تاريخ طلب التخصيص.</summary>
    /// <param name="dto">الطلب.</param>
    public static DateOnly ToDate(AllocateNumberRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return WireMapping.ReadDate(dto.On, "on");
    }

    /// <summary>يُخرج الثوابت مع الكتالوج.</summary>
    /// <param name="presets">الثوابت.</param>
    public static CompanyPresetsDto ToDto(CompanyPresets presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        return new CompanyPresetsDto(
            presets.Values.Select(static pair => new NameValueDto(pair.Key, pair.Value)).ToList(),
            PresetCatalogue.Keys.Select(static key => new PresetKeyDto(key.Key, key.Kind.ToString(), key.Choices)).ToList(),
            PresetCatalogue.Series.Select(static series => new DocumentSeriesDto(series.Code, series.DefaultPrefix)).ToList());
    }

    /// <summary>يُخرج رقماً مخصَّصاً.</summary>
    /// <param name="allocated">الرقم.</param>
    public static AllocatedNumberDto ToDto(AllocatedNumber allocated)
    {
        ArgumentNullException.ThrowIfNull(allocated);
        return new AllocatedNumberDto(allocated.Series, allocated.FiscalYear, allocated.Sequence, allocated.Number);
    }
}
