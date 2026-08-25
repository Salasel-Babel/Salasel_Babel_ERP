using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>
/// <b>مقياس العرض — عدد الخانات العشرية الذي يُثبَّت عند أول تأسيس ولا يُعدَّل بعده.</b>
/// <para>
/// قرار المالك بنصّه: «ما يهمّ هو توحيدها لمنشأة واحدة — عدد الخانات يُسنَد عند أول
/// إعداد للنظام ولا يقبل التعديل بعد ذلك». والسبب محاسبي لا هندسي: رقمٌ يُقرأ بخانتين
/// في شاشة وبأربع في أخرى مشكلةُ مطابقة، لا تفضيلُ عرض.
/// </para>
/// <para>
/// <b>وما لا يحكمه هذا المقياس — وهو أهمّ ما فيه:</b> لا يحكم التخزين، ولا يحكم الحساب.
/// التخزين يبقى <c>NUMERIC(19,4)</c> و<see cref="Money.CanonicalScale"/> بحكم ADR-0002،
/// والبايتات المُجزَّأة تُكتب دائماً بأربع خانات مهما كان هذا المقياس. أما الحساب فلا
/// يستشير هذا النوع في أي موضع: ضريبة 15٪ على صافٍ فردي تُنتج أربع خانات بطبيعتها،
/// ومنشأةٌ اختارت خانتين لا تصير فواتيرها مستحيلة لأجل ذلك.
/// </para>
/// <para>
/// فما يحكمه بالضبط شيئان: <see cref="AcceptsTypedAmount(decimal)"/> — ما <b>يكتبه
/// إنسان</b> — و<see cref="Render(decimal)"/> — ما <b>يُعرض له</b>. والتفصيل الكامل
/// بحججه في <c>docs/decisions/ADR-0025</c>.
/// </para>
/// </summary>
public readonly record struct DisplayScale
{
    /// <summary>أدنى عدد خانات مقبول: صفر — منشأة تمسك دفاترها بالريال الصحيح.</summary>
    public const int Minimum = 0;

    /// <summary>
    /// أقصى عدد خانات مقبول، وهو مقياس التخزين القانوني نفسه. عرضٌ بخانات أكثر من
    /// التخزين يعرض أصفاراً مخترَعة يظنّها القارئ دقّة.
    /// </summary>
    public const int Maximum = Money.CanonicalScale;

    /// <summary>
    /// أنماط التنسيق الثابتة، مفهرسة بعدد الخانات. مكتوبةٌ حرفياً لا مبنيّةً بسلسلة
    /// مُستكمَلة: السلسلة المُستكمَلة تقرأ ثقافة العملية (القاعدة 10).
    /// </summary>
    private static readonly string[] FormatsByPlaces = ["0", "0.0", "0.00", "0.000", "0.0000"];

    private readonly int _places;
    private readonly bool _assigned;

    private DisplayScale(int places)
    {
        _places = places;
        _assigned = true;
    }

    /// <summary>عدد الخانات العشرية المعروضة. قراءتها على قيمة غير مُسنَدة خطأ برمجي.</summary>
    public int Places => _assigned
        ? _places
        : throw new InvalidOperationException("مقياس عرض غير مهيّأ. / Uninitialised display scale.");

    /// <summary>هل المقياس مُسنَد؟</summary>
    public bool IsAssigned => _assigned;

    /// <summary>
    /// يبني مقياساً أو يرفض العدد بخطأ مسمّى. لا مُنشئ عام: الطريق الوحيد إلى قيمة
    /// من هذا النوع يمرّ من هنا.
    /// </summary>
    /// <param name="places">عدد الخانات المطلوب.</param>
    public static Result<DisplayScale> Of(int places)
        => places is < Minimum or > Maximum
            ? Result<DisplayScale>.Failure(CompanySetupErrors.DecimalPlacesOutOfRange(places))
            : Result<DisplayScale>.Success(new DisplayScale(places));

    /// <summary>
    /// هل يجوز أن <b>يكتب إنسان</b> هذا المبلغ في هذه المنشأة؟
    /// <para>
    /// الحكم على الأرقام المعنوية لا على بايت المقياس: <c>5.0000</c> مقبول في منشأة
    /// بخانتين لأنه <c>5.00</c> بعينه، و<c>4.9995</c> مرفوض لأنه ليس أي قيمة تُعرض
    /// بخانتين. والرفض هنا وحده — لا في الحساب ولا في التخزين: إنسانٌ يُدخل رقماً لا
    /// تستطيع شاشته أن تُظهره لا يستطيع مراجعة ما أدخل.
    /// </para>
    /// </summary>
    /// <param name="amount">المبلغ كما كُتب.</param>
    public bool AcceptsTypedAmount(decimal amount)
        => decimal.Round(amount, Places, MidpointRounding.AwayFromZero) == amount;

    /// <summary>
    /// يعرض مبلغاً بهذا المقياس، <b>ويقول صراحةً إن كان العرض يفقد شيئاً</b>.
    /// <para>
    /// والإفصاح هو البند الحامل: تقريبٌ صامت من <c>4.9995</c> إلى <c>5.00</c> يجعل
    /// الشاشة تقول ما لا يقوله الدفتر، فيكفّ الدفتر عن مطابقة حسابه. ولذلك يحمل الناتج
    /// النصّ القانوني بأربع خانات دائماً إلى جانب النصّ المعروض.
    /// </para>
    /// </summary>
    /// <param name="amount">المبلغ المخزَّن.</param>
    public RenderedAmount Render(decimal amount)
    {
        decimal rounded = decimal.Round(amount, Places, MidpointRounding.AwayFromZero);

        return new RenderedAmount(
            rounded.ToString(FormatsByPlaces[Places], CultureInfo.InvariantCulture),
            rounded == amount,
            amount.ToString(FormatsByPlaces[Maximum], CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override string ToString()
        => _assigned ? _places.ToString(CultureInfo.InvariantCulture) : "؟";
}

/// <summary>
/// مبلغٌ معروضاً: النصّ بمقياس المنشأة، وحكمُ الدقّة، والنصّ القانوني بأربع خانات.
/// </summary>
/// <param name="Text">النصّ كما يُعرض، بعدد خانات المنشأة بالضبط.</param>
/// <param name="IsExact">
/// هل يساوي المعروضُ المخزَّنَ تماماً؟ <c>false</c> تعني أن على العارض أن يُعلن التقريب —
/// ولا تعني أبداً أن المخزَّن تغيّر.
/// </param>
/// <param name="CanonicalText">النصّ القانوني بأربع خانات — ما يُطابَق به الدفتر.</param>
public sealed record RenderedAmount(string Text, bool IsExact, string CanonicalText);
