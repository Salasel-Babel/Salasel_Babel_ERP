using Babel.Contracts.Parameters;

namespace Babel.Core.Parameters;

/// <summary>مفتاحٌ داخل مجموعةٍ وصنفُ قيمته. الشكل في الشيفرة، والقيمة في البيانات.</summary>
/// <param name="Key">المفتاح.</param>
/// <param name="Kind">صنف القيمة — وهو ما يقرّر حارسها.</param>
public sealed record ParameterKeyDefinition(string Key, ParameterValueKind Kind);

/// <summary>مجموعةُ معامِلاتٍ تسري كوحدة واحدة.</summary>
/// <param name="Code">رمز المجموعة.</param>
/// <param name="Keys">مفاتيحها كلّها — والإيداع يطلبها كلّها معاً.</param>
public sealed record ParameterSetDefinition(string Code, IReadOnlyList<ParameterKeyDefinition> Keys);

/// <summary>
/// <b>فهرس مجموعات المعامِلات — الشكلُ معلَنٌ في الشيفرة، والقيمُ بياناتٌ لا شيفرة.</b>
/// <para>
/// <b>وما في هذا الملفّ ليس رقماً واحداً.</b> فيه أسماءُ مجموعاتٍ وأسماءُ مفاتيحَ
/// وأصنافُ قيم — أي <b>ما يجعل الرقم مفهوماً</b> لا الرقم. وأمّا الأرقام فتعيش صفوفاً
/// في <c>core.parameter_version</c>، وافتراضُ المنصّة منها يُشحن في
/// <c>data/parameters/platform-defaults.json</c> ويدخل بالنشر لا بالترجمة.
/// </para>
/// <para>
/// <b>ولماذا وحدةُ الإيداع مجموعةٌ لا قيمةٌ مفردة:</b> لأن قيم المجموعة الواحدة
/// <b>يسري بعضها ببعض</b> — نسبةُ المنشأة ونسبةُ الموظف وحدَّا الأجر الخاضع في مجموعةٍ
/// واحدة، وصفٌّ مستقلٌّ لكلّ رقمٍ منها يسمح بخليطٍ من إصدارين <b>لم يعتمده أحد</b>:
/// نسبةٌ من إصدار 2026 وحدٌّ أعلى من إصدار 2024، والمجموع رقمٌ لا يوجد في أي قرار.
/// </para>
/// </summary>
public static class ParameterCatalogue
{
    /// <summary>مجموعةُ ضريبة القيمة المضافة.</summary>
    public const string ValueAddedTax = "tax.value_added";

    /// <summary>
    /// النسبة الأساسية لضريبة القيمة المضافة، <b>كسراً عشرياً لا مئوية</b>.
    /// <para>
    /// وهي المعامِل الذي <b>كان</b> يعيش في <c>src/Babel.Ai/AiOptions.cs</c> قيمةً
    /// ابتدائية في نوع إعدادات. وموضعُه الصحيح صفٌّ بتاريخ سريان وحالةِ اعتمادٍ ومصدر
    /// — كما هو مقرَّر في معالجة الخطر خ-12 وفي §7 بند ٥ من
    /// <c>docs/decisions/قرارات-على-المالك.md</c>.
    /// </para>
    /// </summary>
    public const string ValueAddedTaxStandardRate = "standard_rate";

    /// <summary>كل المجموعات المعرَّفة، بترتيبٍ رتيب.</summary>
    public static IReadOnlyList<ParameterSetDefinition> All { get; } =
    [
        new ParameterSetDefinition(
            ValueAddedTax,
            [new ParameterKeyDefinition(ValueAddedTaxStandardRate, ParameterValueKind.Rate)]),
    ];

    /// <summary>تعريفُ مجموعةٍ برمزها، أو <c>null</c> إن لم تكن معرَّفة.</summary>
    /// <param name="code">الرمز.</param>
    public static ParameterSetDefinition? Find(string? code)
        => All.FirstOrDefault(set => string.Equals(set.Code, code, StringComparison.Ordinal));

    /// <summary>صنفُ قيمةِ مفتاحٍ داخل مجموعة، أو <c>null</c>.</summary>
    /// <param name="setCode">رمز المجموعة.</param>
    /// <param name="key">المفتاح.</param>
    public static ParameterValueKind? KindOf(string? setCode, string? key)
        => Find(setCode)?.Keys.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.Ordinal))?.Kind;
}
