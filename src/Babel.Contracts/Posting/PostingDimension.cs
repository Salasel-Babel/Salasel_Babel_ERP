namespace Babel.Contracts.Posting;

/// <summary>
/// بُعد تحليلي على سطر أو على الطلب كله: <c>branch</c> · <c>cost_center</c> · <c>project</c> ·
/// <c>property</c> · <c>unit</c>.
/// <para>
/// قائمة لا قاموس: الترتيب ثابت والتسلسل حتمي، وهذا شرط لأي شيء يدخل بايتات مُجزَّأة
/// أو يُقارن بين تشغيلين.
/// </para>
/// </summary>
/// <param name="Name">اسم البُعد.</param>
/// <param name="Value">قيمته.</param>
public sealed record PostingDimension(string Name, string Value);
