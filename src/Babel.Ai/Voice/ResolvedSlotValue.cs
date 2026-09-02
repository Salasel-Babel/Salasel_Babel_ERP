using Babel.Contracts.Capture;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>قيمةُ شريحةٍ في أمرٍ اجتاز البوابة — والطرفُ فيها مِقبضٌ لا اسم.</b>
/// <para>
/// وهذا هو الفرق كلّه عن <see cref="SpokenSlotValue"/>: ذاك يحمل <c>Heard</c> ليرى الإنسان
/// <b>لماذا</b> على الشاشة؛ وهذا يعبر إلى الوحدة المالكة، <b>فلا نصّ فيه لطرفٍ إطلاقاً</b>.
/// لا <c>Text</c> ولا <c>Heard</c> ولا <c>LabelAr</c> على حالة الكِيان — وغيابُها بنيويّ
/// يقيسه <c>NoDraftIsBuiltFromASpokenName</c> بالانعكاس، لا اتّفاقٌ يُنسى.
/// </para>
/// <para>
/// <b>ولماذا لا يُمرَّر الاسم «للعرض»:</b> حقلٌ يحمل اسماً يُقرأ، وما يُقرأ يُكتب. وأولُ
/// من يبني جسمَ مسوّدةٍ من هذا النوع سيجد <c>Text</c> جاهزاً في موضع <c>customerId</c> إن
/// وُجد. فلا يوجد.
/// </para>
/// </summary>
public sealed record ResolvedSlotValue
{
    private ResolvedSlotValue(string name, string? text, string? unit, string? handle, FieldProvenance provenance)
    {
        Name = name;
        Text = text;
        Unit = unit;
        Handle = handle;
        Provenance = provenance;
    }

    /// <summary>اسم الشريحة.</summary>
    public string Name { get; }

    /// <summary>القيمة نصّاً — <b>لغير الأطراف وحدها</b>، و<c>null</c> لشريحة الطرف.</summary>
    public string? Text { get; }

    /// <summary>رمز الوحدة حين تكون الشريحة كمّية.</summary>
    public string? Unit { get; }

    /// <summary>
    /// المِقبض المعتم — <b>لشريحة الطرف وحدها</b>، و<c>null</c> لما عداها. طولُه ثابت،
    /// ولا يُقرأ منه غرضٌ ولا منشأةٌ ولا صفّ، ومِقبضان لصفٍّ واحد لا يتساويان.
    /// </summary>
    public string? Handle { get; }

    /// <summary>المصدر.</summary>
    public FieldProvenance Provenance { get; }

    /// <summary>هل هذه شريحة طرفٍ مُحلَّة؟</summary>
    public bool IsEntity => Handle is not null;

    /// <summary>قيمةٌ ليست طرفاً — تُبنى من قراءةٍ ممتلئة.</summary>
    /// <param name="value">القيمة كما قُرئت.</param>
    public static ResolvedSlotValue OfValue(SpokenSlotValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ResolvedSlotValue(value.Name, value.Text, value.Unit, null, value.Provenance);
    }

    /// <summary>طرفٌ حُلّ في السجلّ — <b>مِقبضه وحده</b>.</summary>
    /// <param name="name">اسم الشريحة.</param>
    /// <param name="handle">المِقبض.</param>
    public static ResolvedSlotValue OfEntity(string name, string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        return new ResolvedSlotValue(name, null, null, handle, FieldProvenance.Spoken);
    }
}
