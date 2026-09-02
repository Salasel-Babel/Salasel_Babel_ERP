namespace Babel.Contracts.Lookup;

/// <summary>
/// عدد المرشّحين كما يُسمح للسجلّ أن يعرفه: <b>لا شيء · واحد · أكثر من واحد</b>.
/// <para>
/// <b>ولا قيمة رابعة، وهذا هو الحارس نفسه.</b> السؤال «كم اسماً يشبه هذا؟» تعدادٌ للسجلّ:
/// من يسأل «محمد» ثم «محمد ع» ثم «محمد عل» ويقرأ عدداً في كل مرّة يكون قد بحث في دفتر
/// العملاء بحثاً ثنائياً. والوقاية ليست في حذف الحقل من الاستجابة — بل في ألّا يُحسب العدد
/// أصلاً: الاستعلام يقف عند صفّين (‏<c>limit 2</c>)، وهذا النوع <b>لا يملك حقلاً يحمل عدداً</b>،
/// فما لا يوجد لا يُسرَّب سهواً.
/// </para>
/// </summary>
public enum NameCandidateCardinality
{
    /// <summary>لا مرشّح واحد في هذه المنشأة.</summary>
    None = 1,

    /// <summary>مرشّح واحد بالضبط — وهو وحده ما يُحلّ بلا سؤال.</summary>
    One = 2,

    /// <summary>أكثر من واحد. <b>ولا يُقال كم</b>، ولا يُختار أعلاهم تشابهاً.</summary>
    Many = 3,
}

/// <summary>
/// جواب السبر على سجلّ أسماء. <b>ثلاث حالات وقيمةٌ واحدة</b>، ولا منشئ عامّ:
/// تُبنى بثلاث دوالّ مصنعية لا رابع لها، فلا يستطيع محوّلٌ أن يُعبّر عن «سبعة مرشّحين»
/// حتى لو أراد.
/// </summary>
public sealed record NameCandidateProbe
{
    private NameCandidateProbe(NameCandidateCardinality cardinality, Guid only)
    {
        Cardinality = cardinality;
        Only = only;
    }

    /// <summary>لا مرشّح.</summary>
    public static NameCandidateProbe None { get; } = new(NameCandidateCardinality.None, Guid.Empty);

    /// <summary>أكثر من واحد — بلا عدد.</summary>
    public static NameCandidateProbe Many { get; } = new(NameCandidateCardinality.Many, Guid.Empty);

    /// <summary>مرشّح واحد بعينه.</summary>
    /// <param name="id">معرّف الصفّ داخل الوحدة المالكة.</param>
    /// <exception cref="ArgumentException">إن كان المعرّف فارغاً — «واحدٌ» بلا هوية ليس واحداً.</exception>
    public static NameCandidateProbe One(Guid id)
        => id == Guid.Empty
            ? throw new ArgumentException(
                "مرشّح واحد بمعرّف فارغ ليس مرشّحاً. / a single candidate with an empty identifier is not a candidate.",
                nameof(id))
            : new NameCandidateProbe(NameCandidateCardinality.One, id);

    /// <summary>الحالة.</summary>
    public NameCandidateCardinality Cardinality { get; }

    /// <summary>
    /// معرّف المرشّح الوحيد، أو <see cref="Guid.Empty"/> في الحالتين الأخريين.
    /// <b>ولا يعبر هذا المعرّف إلى النموذج بحال</b>: يُسكب في مِقبضٍ موقَّع أوّلاً.
    /// </summary>
    public Guid Only { get; }
}
