using System.Collections.Immutable;
using Babel.SharedKernel;

namespace Babel.Core.CompanySetup;

/// <summary>رمز مركز التكلفة. معرّف لا نصّ: لا يُترجَم ولا يتغيّر (ADR-0021).</summary>
/// <param name="Value">الرمز كما سكّه السجلّ.</param>
public readonly record struct CostCenterCode(string Value)
{
    /// <summary>أقصى طول للرمز — ويسع حقل <c>costCenterId</c> في نطاق الترحيل.</summary>
    public const int MaximumLength = 32;

    /// <summary>القيمة غير المُسنَدة.</summary>
    public static CostCenterCode None => new(string.Empty);

    /// <summary>هل الرمز مُسنَد؟</summary>
    public bool IsAssigned => !string.IsNullOrWhiteSpace(Value);

    /// <summary>
    /// هل النصّ رمزٌ سليم الشكل؟ أحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية —
    /// والقيد لاتيني لأن الرمز يعبر مسار HTTP وفهارس قاعدة البيانات، لا لأنه أفضل.
    /// </summary>
    /// <param name="candidate">النصّ المرشَّح.</param>
    public static bool IsWellFormed(string? candidate)
        => candidate is { Length: > 0 and <= MaximumLength }
            && candidate.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_');

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// حالة مركز التكلفة — <b>حالة عمل يضبطها إنسان بصلاحية، لا إشارة تقنية</b> (ADR-0006).
/// <para>
/// ولا عضو ثالث اسمه «محذوف»: العنصر الذي عليه حركة لا يُحذف أبداً، والنواة لا تستطيع
/// أن تسأل الدفتر هل عليه حركة (اتجاه الاعتماد إلى الأسفل)، فالجواب الصادق الوحيد هو
/// ألّا يوجد حذفٌ إطلاقاً.
/// </para>
/// </summary>
public enum CostCenterState
{
    /// <summary>عامل: يُختار على مستند جديد.</summary>
    Active = 0,

    /// <summary>
    /// موقوف عن الترحيل: لا يُختار على مستند جديد، <b>ويبقى مقروءاً ومُبوَّباً في التقارير
    /// إلى الأبد</b> — الدفتر إضافي بحكم ADR-0002، والتاريخ المُرحَّل عليه لا يُمسّ.
    /// </summary>
    Suspended = 1,
}

/// <summary>
/// مركز تكلفة واحد.
/// <para>
/// <b>الاسم العربي إلزامي</b> لأنه السجلّ لا ترجمةً أولى (ADR-0021)، و<b>الترجمات صفوف
/// لا أعمدة</b>: إضافة الأردية أو الهندية إدخالُ مدخل في الخريطة، لا هجرةُ مخطّط.
/// </para>
/// <para>
/// ولا مُنشئ عام: مراكز التكلفة تُسكّ في <see cref="CostCenterRegister"/> وحده، لأن
/// الرمز وحالة «الافتراضي» خاصّتان بالسجلّ لا بالعنصر.
/// </para>
/// </summary>
public sealed record CostCenter
{
    internal CostCenter(
        CostCenterCode code,
        TranslatedName name,
        CostCenterState state,
        string suspensionReason)
    {
        ArgumentNullException.ThrowIfNull(name);

        Code = code;
        Name = name;
        State = state;
        SuspensionReason = suspensionReason;
    }

    /// <summary>الرمز — الهوية الثابتة التي تحملها سطور القيود.</summary>
    public CostCenterCode Code { get; }

    /// <summary>
    /// الاسم: سجلٌّ عربي إلزامي وترجماتٌ صفوف. النوع نفسه هو ما يجعل إضافة لغة خامسة
    /// إدخالَ مدخل لا هجرةَ مخطّط، وهو مشترك مع كل كيان مُسمّى (ADR-0021).
    /// </summary>
    public TranslatedName Name { get; }

    /// <summary>الاسم العربي — إلزامي، وهو الارتداد المضمون عند غياب ترجمة.</summary>
    public string NameAr => Name.Arabic;

    /// <summary>الترجمات بوسم اللغة BCP-47، مرتَّبة ترتيباً حرفياً ثابتاً.</summary>
    public ImmutableSortedDictionary<string, string> Translations => Name.Translations;

    /// <summary>الحالة.</summary>
    public CostCenterState State { get; }

    /// <summary>سبب الإيقاف مكتوباً، أو نصّ فارغ إن كان عاملاً.</summary>
    public string SuspensionReason { get; }

    /// <summary>هل هو عامل؟</summary>
    public bool IsActive => State == CostCenterState.Active;

    /// <summary>
    /// الاسم بلغة العرض، <b>مرتدّاً إلى العربية لا إلى الفراغ ولا إلى المفتاح</b> (ADR-0021):
    /// عنوانٌ فارغ فوق عمود أرقام يجعل المحاسب يفترض العنوان الذي يتوقّعه فلا يُبلَّغ عن
    /// العطل أبداً.
    /// </summary>
    /// <param name="languageTag">وسم اللغة المطلوب، مثل <c>ur-PK</c>.</param>
    public string NameIn(string? languageTag) => Name.In(languageTag);

    /// <summary>
    /// الاسم بلغة العرض <b>مع إعلان الارتداد</b>. من يعرض الاسم في شاشة يحتاج أن يعرف
    /// أنه ارتدّ ليقول ذلك للقارئ — والارتداد الصامت هو العطل الذي لا يُبلَّغ عنه.
    /// </summary>
    /// <param name="languageTag">وسم اللغة المطلوب.</param>
    public NameResolution ResolveName(string? languageTag) => Name.Resolve(languageTag);
}
