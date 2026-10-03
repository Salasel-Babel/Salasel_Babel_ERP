using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Babel.SharedKernel;

namespace Babel.Core.Presets;

/// <summary>نوع قيمة الثابت — وهو ما يقرّر كيف تُصدَّق.</summary>
public enum PresetKind
{
    /// <summary>نصّ حرّ قصير: معرّف فرع أو مستودع أو طرف.</summary>
    Text,

    /// <summary>اختيار من قائمة مغلقة.</summary>
    Choice,

    /// <summary>نسبة ككسر عشري بين 0 و1.</summary>
    Rate,

    /// <summary>‏<c>true</c> أو <c>false</c>.</summary>
    Boolean,

    /// <summary>بادئة ترقيم: حروف لاتينية كبيرة وأرقام.</summary>
    Prefix,
}

/// <summary>ثابت واحد في الكتالوج المغلق.</summary>
/// <param name="Key">المفتاح.</param>
/// <param name="Kind">نوع القيمة.</param>
/// <param name="Choices">الأعضاء المسموحة حين يكون النوع اختياراً، وإلا فارغة.</param>
public sealed record PresetKey(string Key, PresetKind Kind, ImmutableArray<string> Choices);

/// <summary>سلسلة ترقيم لنوع مستند، ببادئتها الافتراضية.</summary>
/// <param name="Code">رمز السلسلة كما يُرسل على السلك.</param>
/// <param name="DefaultPrefix">البادئة إن لم تُضبط في الثوابت.</param>
public sealed record DocumentSeries(string Code, string DefaultPrefix);

/// <summary>رقمٌ خُصّص من عدّاد السلسلة.</summary>
/// <param name="Series">السلسلة.</param>
/// <param name="FiscalYear">السنة التي يعدّ العدّاد داخلها.</param>
/// <param name="Sequence">التسلسل داخل السنة، يبدأ من 1.</param>
/// <param name="Number">الرقم المركّب الذي يُكتب على المستند.</param>
public sealed record AllocatedNumber(string Series, int FiscalYear, int Sequence, string Number);

/// <summary>حدود الثوابت، معلنة مرّة واحدة ويقرؤها العقد المنشور والنواة معاً.</summary>
public static class CompanyPresetLimits
{
    /// <summary>أقصى طول لمفتاح ثابت.</summary>
    public const int MaximumKeyLength = 64;

    /// <summary>أقصى طول لقيمة ثابت.</summary>
    public const int MaximumValueLength = 128;

    /// <summary>أقصى طول لبادئة ترقيم.</summary>
    public const int MaximumPrefixLength = 12;

    /// <summary>أقصى عدد ثوابت في إيداع واحد — وهو حجم الكتالوج نفسه.</summary>
    public const int MaximumEntries = 64;
}

/// <summary>
/// الكتالوج المغلق لثوابت الشركة وسلاسل الترقيم.
/// <para>
/// <b>مغلقٌ عمداً:</b> مفتاحٌ خارج القائمة يُرفض، لأن الشاشة التي تقرأه لن تكون موجودة.
/// ما يُضاف هنا يُضاف معه ما يقرؤه.
/// </para>
/// </summary>
public static class PresetCatalogue
{
    private static readonly ImmutableArray<string> TaxClassifications = ["standard", "zero", "exempt"];
    private static readonly ImmutableArray<string> SettlementMethods = ["cash", "bank", "card_clearing"];

    /// <summary>سلاسل الترقيم ببادئاتها الافتراضية، بترتيب العمل.</summary>
    public static ImmutableArray<DocumentSeries> Series { get; } =
    [
        new("sales_invoice", "INV"),
        new("customer_receipt", "RCT"),
        new("credit_note", "CN"),
        new("supplier_bill", "BILL"),
        new("supplier_payment", "PAY"),
        new("purchase_order", "PO"),
        new("goods_receipt", "GRN"),
        new("purchase_return", "PRN"),
        new("stock_movement", "MOV"),
        new("stock_transfer", "TRF"),
        new("payroll_run", "RUN"),
        new("payroll_payment", "PRL"),
        new("employee_advance", "ADV"),
        new("social_insurance", "GOSI"),
        new("eos_provision", "EOSP"),
        new("eos_settlement", "EOSS"),
    ];

    /// <summary>الثوابت كلّها: القيم التشغيلية ثم بادئة لكل سلسلة.</summary>
    public static ImmutableArray<PresetKey> Keys { get; } =
    [
        new("branch.default", PresetKind.Text, []),
        new("tax.classification", PresetKind.Choice, TaxClassifications),
        new("tax.rate", PresetKind.Rate, []),
        new("tax.recoverable", PresetKind.Boolean, []),
        new("expense.category", PresetKind.Text, []),
        new("settlement.method", PresetKind.Choice, SettlementMethods),
        new("treasury.cash", PresetKind.Text, []),
        new("treasury.bank", PresetKind.Text, []),
        new("warehouse.default", PresetKind.Text, []),
        new("location.default", PresetKind.Text, []),
        new("hr.class", PresetKind.Text, []),
        new("hr.settlement.method", PresetKind.Text, []),
        new("hr.treasury", PresetKind.Text, []),
        .. Series.Select(static series => new PresetKey("number." + series.Code, PresetKind.Prefix, [])),
    ];

    private static readonly ImmutableDictionary<string, PresetKey> ByKey =
        Keys.ToImmutableDictionary(static key => key.Key, StringComparer.Ordinal);

    private static readonly ImmutableDictionary<string, DocumentSeries> SeriesByCode =
        Series.ToImmutableDictionary(static series => series.Code, StringComparer.Ordinal);

    /// <summary>يجد ثابتاً بمفتاحه.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="found">الثابت إن وُجد.</param>
    public static bool TryFind(string key, out PresetKey found) => ByKey.TryGetValue(key, out found!);

    /// <summary>يجد سلسلة برمزها.</summary>
    /// <param name="code">الرمز.</param>
    /// <param name="found">السلسلة إن وُجدت.</param>
    public static bool TryFindSeries(string code, out DocumentSeries found) => SeriesByCode.TryGetValue(code, out found!);
}

/// <summary>
/// ثوابت الشركة المُصدَّقة — ما يُحسم مرّةً في الإعداد ثم يختفي من شاشات الإدخال.
/// <para>
/// <b>الفكرة التي يحملها هذا النوع:</b> الفاتورة تسأل عن الفرع والتصنيف الضريبي ونسبته وطريقة
/// التسوية وخزينتها — وكلّها لا تتغيّر من فاتورة إلى فاتورة في منشأةٍ غير ميدانية. فتُضبط هنا
/// مرّة، وتبقى في الشاشة المتغيّرات القليلة: العميل والصنف والكمية والسعر.
/// </para>
/// <para>
/// والنوع لا يُبنى إلا من <see cref="Validate"/>: مفتاحٌ خارج الكتالوج أو قيمةٌ تخالف نوعها
/// لا تصل إلى المخزن ولا إلى الشاشة.
/// </para>
/// </summary>
public sealed partial class CompanyPresets
{
    private readonly ImmutableDictionary<string, string> _values;

    private CompanyPresets(ImmutableDictionary<string, string> values) => _values = values;

    /// <summary>ثوابت فارغة — منشأة لم تضبط شيئاً بعد، فكلُّ حقلٍ يُسأل عنه.</summary>
    public static CompanyPresets Empty { get; } = new(ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>القيم كما ضُبطت، مرتَّبة بالمفتاح.</summary>
    public IEnumerable<KeyValuePair<string, string>> Values => _values.OrderBy(static pair => pair.Key, StringComparer.Ordinal);

    /// <summary>عدد الثوابت المضبوطة.</summary>
    public int Count => _values.Count;

    /// <summary>يقرأ ثابتاً، أو <c>null</c> إن لم يُضبط.</summary>
    /// <param name="key">المفتاح.</param>
    public string? this[string key] => _values.TryGetValue(key, out string? value) ? value : null;

    /// <summary>بادئة سلسلة: ما ضُبط، وإلا الافتراضي من الكتالوج.</summary>
    /// <param name="series">السلسلة.</param>
    public string PrefixOf(DocumentSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return this["number." + series.Code] ?? series.DefaultPrefix;
    }

    /// <summary>يركّب الرقم المكتوب على المستند: البادئة، فالسنة، فالتسلسل بأربع خانات على الأقل.</summary>
    /// <param name="series">السلسلة.</param>
    /// <param name="fiscalYear">السنة.</param>
    /// <param name="sequence">التسلسل.</param>
    public AllocatedNumber Compose(DocumentSeries series, int fiscalYear, int sequence)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        string number = string.Create(
            CultureInfo.InvariantCulture,
            $"{PrefixOf(series)}-{fiscalYear:0000}-{sequence:0000}");

        return new AllocatedNumber(series.Code, fiscalYear, sequence, number);
    }

    /// <summary>
    /// يُعيد بناء الثوابت من صفوف المخزن. يرمي على الصفّ المخالف بدل أن يُسقطه صامتاً:
    /// صفٌّ لا يمرّ من التصديق لم يكن ليُكتب، فوجوده عطلُ قاعدة لا حالةُ عمل.
    /// </summary>
    /// <param name="values">الصفوف.</param>
    public static CompanyPresets Rehydrate(IEnumerable<KeyValuePair<string, string>> values)
    {
        Result<CompanyPresets> result = Validate(values);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException("صفوف ثوابت مخالفة في المخزن: " + string.Join(" · ", result.Errors.Select(static e => e.Code)));
    }

    /// <summary>يصدّق القيم الواصلة ويبني الثوابت، أو يُرجع أخطاءها كلّها دفعةً واحدة.</summary>
    /// <param name="values">المفاتيح وقيمها كما وصلت.</param>
    public static Result<CompanyPresets> Validate(IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        List<Error> errors = [];
        ImmutableDictionary<string, string>.Builder builder =
            ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach ((string rawKey, string rawValue) in values)
        {
            string key = (rawKey ?? string.Empty).Trim();
            string value = (rawValue ?? string.Empty).Trim();

            if (!PresetCatalogue.TryFind(key, out PresetKey preset))
            {
                errors.Add(CompanyPresetErrors.UnknownKey(key));
                continue;
            }

            if (builder.ContainsKey(key))
            {
                errors.Add(CompanyPresetErrors.DuplicateKey(key));
                continue;
            }

            /* قيمةٌ فارغة تعني «أزل الثابت»، لا «اضبطه فارغاً». */
            if (value.Length == 0)
            {
                continue;
            }

            Error? problem = Check(preset, value);

            if (problem is not null)
            {
                errors.Add(problem);
                continue;
            }

            builder[key] = value;
        }

        if (builder.Count > CompanyPresetLimits.MaximumEntries)
        {
            errors.Add(CompanyPresetErrors.TooMany);
        }

        return errors.Count > 0
            ? Result<CompanyPresets>.Failure(errors)
            : Result<CompanyPresets>.Success(new CompanyPresets(builder.ToImmutable()));
    }

    private static Error? Check(PresetKey preset, string value)
    {
        if (value.Length > CompanyPresetLimits.MaximumValueLength || value.Any(char.IsControl))
        {
            return CompanyPresetErrors.ValueMalformed(preset.Key);
        }

        return preset.Kind switch
        {
            PresetKind.Text => null,
            PresetKind.Choice => preset.Choices.Contains(value, StringComparer.Ordinal)
                ? null
                : CompanyPresetErrors.ValueNotAChoice(preset.Key, preset.Choices),
            PresetKind.Rate => RatePattern().IsMatch(value) ? null : CompanyPresetErrors.ValueMalformed(preset.Key),
            PresetKind.Boolean => value is "true" or "false" ? null : CompanyPresetErrors.ValueMalformed(preset.Key),
            PresetKind.Prefix => value.Length <= CompanyPresetLimits.MaximumPrefixLength && PrefixPattern().IsMatch(value)
                ? null
                : CompanyPresetErrors.ValueMalformed(preset.Key),
            _ => CompanyPresetErrors.ValueMalformed(preset.Key),
        };
    }

    [GeneratedRegex(@"^(0(\.[0-9]{1,6})?|1(\.0{1,6})?)$")]
    private static partial Regex RatePattern();

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{0,11}$")]
    private static partial Regex PrefixPattern();
}

/// <summary>أخطاء ثوابت الشركة، برموز ثابتة هي ما تعتمد عليه الشيفرة.</summary>
public static class CompanyPresetErrors
{
    /// <summary>مفتاحٌ خارج الكتالوج.</summary>
    /// <param name="key">المفتاح كما وصل.</param>
    public static Error UnknownKey(string key) => new(
        "company_presets.unknown_key",
        $"المفتاح «{key}» ليس في كتالوج الثوابت. الكتالوج مغلق: ما لا تقرؤه شاشةٌ لا يُخزَّن.",
        $"The key '{key}' is not in the preset catalogue. The catalogue is closed: what no screen reads is not stored.");

    /// <summary>المفتاح نفسه مرّتين في إيداع واحد.</summary>
    /// <param name="key">المفتاح.</param>
    public static Error DuplicateKey(string key) => new(
        "company_presets.duplicate_key",
        $"المفتاح «{key}» ورد مرّتين في الإيداع نفسه، ولا يُعرف أيّ القيمتين قُصدت.",
        $"The key '{key}' appears twice in the same deposit, and neither value can be taken as the intended one.");

    /// <summary>قيمةٌ تخالف نوع مفتاحها.</summary>
    /// <param name="key">المفتاح.</param>
    public static Error ValueMalformed(string key) => new(
        "company_presets.value_malformed",
        $"قيمة «{key}» لا توافق نوعها: نسبةٌ ككسر عشري بين 0 و1، أو true/false، أو بادئة من حروف لاتينية كبيرة وأرقام، أو نصّ لا يتجاوز "
            + CompanyPresetLimits.MaximumValueLength + " حرفاً بلا محارف تحكّم.",
        $"The value of '{key}' does not match its kind: a rate as a decimal fraction between 0 and 1, true/false, a prefix of upper-case ASCII letters and digits, or text of at most "
            + CompanyPresetLimits.MaximumValueLength + " characters with no control characters.");

    /// <summary>قيمةٌ خارج القائمة المغلقة.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="choices">الأعضاء المسموحة.</param>
    public static Error ValueNotAChoice(string key, ImmutableArray<string> choices) => new(
        "company_presets.value_not_a_choice",
        $"قيمة «{key}» يجب أن تكون واحدة من: {string.Join("، ", choices)}.",
        $"The value of '{key}' must be one of: {string.Join(", ", choices)}.");

    /// <summary>عدد ثوابت يتجاوز الكتالوج.</summary>
    public static Error TooMany { get; } = new(
        "company_presets.too_many",
        "عدد الثوابت يتجاوز حجم الكتالوج.",
        "The number of presets exceeds the catalogue's size.");

    /// <summary>سلسلة ترقيم غير معروفة.</summary>
    /// <param name="series">الرمز كما وصل.</param>
    public static Error UnknownSeries(string series) => new(
        "company_presets.unknown_series",
        $"سلسلة الترقيم «{series}» غير معروفة. السلاسل: {string.Join("، ", PresetCatalogue.Series.Select(static s => s.Code))}.",
        $"The numbering series '{series}' is unknown. Series: {string.Join(", ", PresetCatalogue.Series.Select(static s => s.Code))}.");
}
