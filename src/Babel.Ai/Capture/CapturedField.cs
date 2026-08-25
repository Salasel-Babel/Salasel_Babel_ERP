namespace Babel.Ai.Capture;

/// <summary>
/// قيمة ملتقَطة <b>ومعها مصدرها</b>. لا توجد في هذه الوحدة قيمة بلا مصدر.
/// <para>
/// النوع عام كي يستحيل أن توجد قيمة ملتقَطة <b>خارج</b> هذا الشكل: أي قيمة تدخل مسوّدة
/// الالتقاط تمرّ من هنا، فتحمل مصدرها بحكم نوعها لا بحكم انضباط الكاتب.
/// </para>
/// <para>
/// و<see cref="Value"/> هي <b>السجلّ كما كتبه المُصدِر</b>: اسم مورد يُخزَّن كما ورد،
/// بلا ترجمة ولا نقحرة ولا زوج عربي/إنجليزي — الإلزام على بنية الحقل لا على محارف ما
/// كُتب فيه (‏ADR-0021). و<see cref="OriginKey"/> مفتاح عرضٍ يُحلّ في الواجهة، لا نصّ.
/// </para>
/// </summary>
/// <typeparam name="T">نوع القيمة.</typeparam>
public sealed record CapturedField<T>
{
    /// <summary>القيمة كما وردت. سجلٌّ لا ترجمة.</summary>
    public required T Value { get; init; }

    /// <summary>مصدر القيمة.</summary>
    public required FieldProvenance Provenance { get; init; }

    /// <summary>مفتاح مورد يصف المنشأ المادّي — انظر <see cref="CaptureOriginKeys"/>.</summary>
    public required string OriginKey { get; init; }

    /// <summary>
    /// درجة الثقة بين صفر وواحد، وللمقروء والمُستنتَج <b>وحدهما</b>.
    /// <c>decimal</c> لا <c>double</c>: الرقم يُعرض ويُقارن بعتبة، والفاصلة العائمة
    /// تجعل مقارنتين متطابقتين تختلفان.
    /// </summary>
    public decimal? Confidence { get; init; }

    /// <summary>ما يجب أن يفعله الإنسان أمام هذا الحقل.</summary>
    public ProvenanceDuty Duty => FieldProvenanceInfo.DutyOf(Provenance);

    /// <summary>مفتاح المورد لتسمية المصدر.</summary>
    public string ProvenanceKey => FieldProvenanceInfo.ResourceKeyOf(Provenance);

    /// <summary>
    /// حقل مُصدَّق: من رمز موقَّع أو فاتورة إلكترونية. بلا درجة ثقة — مصدره لا يقيس ثقة.
    /// </summary>
    /// <param name="value">القيمة.</param>
    /// <param name="originKey">مفتاح المنشأ.</param>
    public static CapturedField<T> Attested(T value, string originKey) =>
        new() { Value = value, Provenance = FieldProvenance.Attested, OriginKey = originKey };

    /// <summary>حقل مقروء ضوئياً بدرجة ثقته.</summary>
    /// <param name="value">القيمة.</param>
    /// <param name="confidence">درجة الثقة بين صفر وواحد.</param>
    /// <param name="originKey">مفتاح المنشأ.</param>
    public static CapturedField<T> Read(T value, decimal confidence, string originKey = CaptureOriginKeys.Optical) =>
        new() { Value = value, Provenance = FieldProvenance.Read, OriginKey = originKey, Confidence = confidence };

    /// <summary>حقل مُستنتَج من نموذج بدرجة ثقته.</summary>
    /// <param name="value">القيمة.</param>
    /// <param name="confidence">درجة الثقة بين صفر وواحد.</param>
    /// <param name="originKey">مفتاح المنشأ.</param>
    public static CapturedField<T> Inferred(T value, decimal confidence, string originKey = CaptureOriginKeys.Model) =>
        new() { Value = value, Provenance = FieldProvenance.Inferred, OriginKey = originKey, Confidence = confidence };

    /// <summary>حقل من إعدادات المستأجر.</summary>
    /// <param name="value">القيمة.</param>
    /// <param name="originKey">مفتاح المنشأ.</param>
    public static CapturedField<T> Defaulted(T value, string originKey = CaptureOriginKeys.TenantSetting) =>
        new() { Value = value, Provenance = FieldProvenance.Defaulted, OriginKey = originKey };

    /// <summary>حقل أدخله إنسان — يملكه ولا يُراجَع.</summary>
    /// <param name="value">القيمة.</param>
    /// <param name="originKey">مفتاح المنشأ.</param>
    public static CapturedField<T> Typed(T value, string originKey = CaptureOriginKeys.Human) =>
        new() { Value = value, Provenance = FieldProvenance.Typed, OriginKey = originKey };
}
