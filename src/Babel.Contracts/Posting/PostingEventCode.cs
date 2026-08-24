namespace Babel.Contracts.Posting;

/// <summary>
/// رمز الحدث التجاري في مصفوفة الترحيل، بصيغة <c>&lt;وحدة&gt;.&lt;كيان&gt;.&lt;فعل&gt;</c>
/// مثل <c>realestate.rent.accrual.own_property</c>.
/// <para>
/// هذا هو المفتاح الذي يفتح <b>قالب</b> القيد: أي الأدوار تُدين وأيها تُدان، وبأي مبالغ،
/// وتحت أي شروط. الوحدة تسمّي الحدث؛ ولا تسمّي حساباً ولا جانباً ولا مبلغ سطر
/// (‏<c>data/posting-matrix/README.md</c>).
/// </para>
/// </summary>
/// <param name="Value">الرمز كما هو في ملفات المصفوفة.</param>
public readonly record struct PostingEventCode(string Value)
{
    /// <summary>لا حدث — الطلب يحمل سطوره صراحةً.</summary>
    public static PostingEventCode None => new(string.Empty);

    /// <summary>هل الطلب يشير إلى قالب في المصفوفة؟</summary>
    public bool IsAssigned => !string.IsNullOrWhiteSpace(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
