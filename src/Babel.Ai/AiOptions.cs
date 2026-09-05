namespace Babel.Ai;

/// <summary>
/// إعدادات وحدة الالتقاط. ما يُملأ منها في المسوّدة يحمل المصدر <c>defaulted</c>
/// ويُعرض للإنسان — قيمةٌ مفترَضة تُعرض أخفّ ضرراً من قيمة مفترَضة تُخفى.
/// </summary>
public sealed class AiOptions
{
    /// <summary>عملة الشركة حين لا تُطبع العملة على المستند.</summary>
    public string CompanyCurrency { get; set; } = "SAR";

    // ‏**والنسبة النظامية لم تعد هنا.** كانت `StatutoryTaxRate = 0.15m` قيمةً ابتدائية
    // في هذا النوع: رقمٌ من جهةٍ خارجية يعيش في شيفرة، بلا تاريخ سريان، وبلا مصدرٍ
    // مُسمّى، وبلا سبيلٍ لصاحب المصلحة أن يغيّره. وموضعُه الصحيح صفٌّ في خدمة المعامِلات
    // — `tax.value_added` · `standard_rate` — بتاريخ سريانه وحالة اعتماده ومصدره،
    // وبتجاوزٍ لكلّ منشأة. ووحدةُ الالتقاط تقرؤه عبر `IParameterSource`.
    // (‏docs/decisions/ADR-جديد-a-parameter-is-a-dated-version-of-a-whole-set.md)

    /// <summary>
    /// أدنى ثقة يُقبل عندها اقتراح النموذج أصلاً. ما دونها لا يُعرض اقتراح —
    /// <b>ولا يُرحَّل شيء بناءً عليها بحال</b>: العتبة تخصّ العرض لا الاعتماد.
    /// </summary>
    public decimal MinimumSuggestionConfidence { get; set; } = 0.50m;
}
