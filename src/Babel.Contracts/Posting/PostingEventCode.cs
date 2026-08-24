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
    /// <summary>
    /// <b>القيمة غير المُسنَدة — لا «مسار بلا حدث».</b>
    /// <para>
    /// كانت تعني «الطلب يحمل سطوره صراحةً فلا حاجة لحدث»، وذلك هو المعنى الذي ابتلع حدثاً
    /// محاسبياً بصمت: رمزٌ فارغ يجعل حدثين مختلفين من المستند نفسه عند الإطلاق نفسه هويةً
    /// واحدة (‏ADR-0016). واليوم رمز الحدث <b>إلزامي على المسارين</b>، وهذه القيمة حالة
    /// «لم يُسنَد بعد» يرفضها <c>PostingPlanner</c> وقيد التحقق في قاعدة البيانات
    /// و<c>ledger.post_entry</c> نفسها.
    /// </para>
    /// </summary>
    public static PostingEventCode None => new(string.Empty);

    /// <summary>هل الطلب يشير إلى قالب في المصفوفة؟</summary>
    public bool IsAssigned => !string.IsNullOrWhiteSpace(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
