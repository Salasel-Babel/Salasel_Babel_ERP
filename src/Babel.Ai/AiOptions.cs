namespace Babel.Ai;

/// <summary>
/// إعدادات وحدة الالتقاط. ما يُملأ منها في المسوّدة يحمل المصدر <c>defaulted</c>
/// ويُعرض للإنسان — قيمةٌ مفترَضة تُعرض أخفّ ضرراً من قيمة مفترَضة تُخفى.
/// </summary>
public sealed class AiOptions
{
    /// <summary>عملة الشركة حين لا تُطبع العملة على المستند.</summary>
    public string CompanyCurrency { get; set; } = "SAR";

    /// <summary>
    /// النسبة النظامية لضريبة القيمة المضافة حين لا تُطبع على المستند.
    /// <c>decimal</c> لا <c>double</c>: قيس في هذا المستودع انحراف ضريبة عند الخانة
    /// العشرية الرابعة من <c>double</c> واحد.
    /// </summary>
    public decimal StatutoryTaxRate { get; set; } = 0.15m;

    /// <summary>
    /// أدنى ثقة يُقبل عندها اقتراح النموذج أصلاً. ما دونها لا يُعرض اقتراح —
    /// <b>ولا يُرحَّل شيء بناءً عليها بحال</b>: العتبة تخصّ العرض لا الاعتماد.
    /// </summary>
    public decimal MinimumSuggestionConfidence { get; set; } = 0.50m;
}
