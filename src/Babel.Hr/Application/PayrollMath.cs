using System.Security.Cryptography;

namespace Babel.Hr.Application;

/// <summary>
/// حسابُ القسيمة — <b>ولا رقم نظامي واحد في هذا الملفّ</b>.
/// <para>
/// كل نسبة وكل حدٍّ يصل إلى هنا <b>مقروءاً من صفّ إعدادات معتمد</b>، ولا افتراضي ولا
/// صفر صامت. وما هنا عملياتٌ حسابية بحتة: الضرب، والقصّ بين حدَّين مُعطَيَين، وخفضُ
/// المقياس إلى المقياس القانوني.
/// </para>
/// </summary>
internal static class PayrollMath
{
    /// <summary>المقياس القانوني للمبالغ في كل النطاق — أربع خانات.</summary>
    public const int MoneyScale = 4;

    /// <summary>
    /// يخفض ناتج <c>وعاء × نسبة</c> إلى المقياس القانوني.
    /// <para>
    /// <b>ولماذا الخفض مفروضٌ لا مختار:</b> الوعاء مبلغٌ بمقياس أربع، والنسبة كسرٌ
    /// بمقياس ثمانٍ، فالحاصل يبلغ اثنتي عشرة خانة — و<c>Money</c> يرفض ما زاد على
    /// أربع رفضاً صريحاً. أي أن «الاحتفاظ بالدقّة كاملة» ليس خياراً متاحاً في هذا
    /// النطاق أصلاً.
    /// </para>
    /// <para>
    /// <b>وقاعدة المنتصف «نصفٌ بعيداً عن الصفر» مطابقةٌ لقاعدة PostgreSQL</b> عند
    /// الكتابة في <c>numeric(19,4)</c> — و.NET يقرّب افتراضياً «نصفاً إلى الزوجي».
    /// والاختلاف بينهما عند نقاط المنتصف يجعل <b>المخزَّن يخالف المُجزَّأ</b>، وهو
    /// عطلٌ موصوف في <c>Babel.Canonicalization.Amounts</c> بنصّه.
    /// </para>
    /// <para>
    /// <b>وما لا يفعله هذا الملفّ — ويجب أن يُقال:</b> لا يقرّب إلى الهللة، ولا يوزّع
    /// فرق تقريب على سطر، ولا يختار بين «تقريب حصّة كل موظف ثم الجمع» و«الجمع ثم
    /// التقريب مرّة». تلك <b>سياسة محاسبية يملكها المالك</b> ولم تُحسم بعد، وهي
    /// مُسجَّلة في دَين التحقّق. وما هنا خفضُ تمثيلٍ لا سياسةُ تقريب.
    /// </para>
    /// </summary>
    /// <param name="value">القيمة قبل الخفض.</param>
    public static decimal ToCanonicalScale(decimal value)
        => decimal.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// يقصّ الوعاء بين حدَّين <b>مقروءَين من صفّ الإعدادات المعتمد</b>.
    /// <para>وحدٌّ أقصى قيمته صفر يعني «لا سقف»: صفٌّ لم يُملأ سقفه لا يجوز أن يقصّ
    /// الوعاء كلّه إلى صفر بصمت.</para>
    /// </summary>
    /// <param name="wage">الوعاء قبل القصّ.</param>
    /// <param name="floor">الحدّ الأدنى كما اعتُمد.</param>
    /// <param name="ceiling">الحدّ الأقصى كما اعتُمد، أو صفر فلا سقف.</param>
    public static decimal Clamp(decimal wage, decimal floor, decimal ceiling)
    {
        decimal clamped = wage < floor ? floor : wage;
        return ceiling > 0m && clamped > ceiling ? ceiling : clamped;
    }

    /// <summary>حصّة طرفٍ من الوعاء بنسبته المعتمدة، بالمقياس القانوني.</summary>
    /// <param name="contributoryWage">الوعاء بعد القصّ.</param>
    /// <param name="rate">النسبة كما اعتُمدت — كسرٌ عشري لا نسبة مئوية.</param>
    public static decimal Share(decimal contributoryWage, decimal rate)
        => ToCanonicalScale(contributoryWage * rate);
}

/// <summary>
/// مولّد الرمز المعتم للموظف.
/// <para>
/// <b>والمعرّف الذي يعبر إلى <c>ledger.*</c> هو هذا الرمز وحده.</b> ولا يُشتقّ من هوية
/// وطنية ولا من آيبان ولا من اسم ولا من تاريخ ميلاد ولا من تسلسل يُقرأ منه ترتيب
/// التوظيف: كل ما يدخل الدفتر يدخل <b>البايتات المُجزَّأة</b>، و<c>REVOKE UPDATE,
/// DELETE</c> يجعله غير قابل للإزالة، وعلاجُ المحو الموعود في ADR-0046 — تعميةٌ بمفتاح
/// يُتلَف — <b>لا يبلغ بايتات دخلت سلسلة تجزئة</b> لأن تغييرها يكسر السلسلة.
/// </para>
/// <para>
/// وانتبه أن <c>journal_line.description_ar_search</c> عمودٌ <b>مفهرس نصّياً</b>: فرقمٌ
/// شخصي لا يدخل الدفتر غيرَ ممحوٍّ فحسب، بل <b>قابلَ البحث</b> غير ممحوّ.
/// </para>
/// </summary>
internal static class EmployeeCodes
{
    /// <summary>بادئة الرمز — لتُقرأ طبيعته في سجلّ تدقيق بلا أن يُقرأ منه شيء عن صاحبه.</summary>
    public const string Prefix = "emp-";

    /// <summary>عدد بايتات العشوائية: مئة وثمانية وعشرون بتّاً.</summary>
    private const int EntropyBytes = 16;

    /// <summary>يولّد رمزاً معتماً جديداً من مولّد عشوائية معمّى.</summary>
    public static string Mint()
        => Prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(EntropyBytes));
}

/// <summary>
/// طرق التسوية المقبولة على مستندات الدفع في هذه الوحدة — <b>مجموعة ضيّقة عمداً</b>.
/// <para>
/// وهي <b>مؤهّلات دور</b> تقرؤها خريطة الأدوار، لا رموز حسابات: الوحدة تُسلّم المؤهّل
/// والمصفوفة وحدها تُحوّله. ومؤهّلٌ لا تعرفه الخريطة يقع على المؤهّل الافتراضي فيختار
/// حساباً آخر <b>بصمت</b> — ولذلك يُرفض هنا باسمه.
/// </para>
/// <para>
/// <b>وما لا يُقبل هنا، ولماذا:</b> المؤهّل <c>in_transit</c> موجودٌ في خريطة الأدوار
/// ويقصد حساباً وسيطاً «نقدية بالطريق». وقبولُه على سند صرف رواتب <b>يفترض جواب سؤال
/// مفتوح على المالك</b>: متى يقع قيد صرف الرواتب بالضبط — عند توليد ملفّ حماية الأجور،
/// أم عند تأكيد المصرف بالتنفيذ؟ ونصّ المصفوفة نفسه يحمل «أو» صريحة. فالمجموعة تبقى
/// عند الطريقتين اللتين لا يختلف عليهما جواب السؤال، ويُفتح الثالث حين يُغلق البند.
/// والمؤهّل <c>card_clearing</c> لا يخدم صرف رواتب أصلاً، و<c>trust_bank</c> بندٌ
/// عقاري معلَّق على قرار آخر.
/// </para>
/// </summary>
internal static class SettlementMethods
{
    /// <summary>نقداً من الصندوق.</summary>
    public const string Cash = "cash";

    /// <summary>تحويلاً من حساب مصرفي.</summary>
    public const string Bank = "bank";

    /// <summary>المجموعة المقبولة، مرتَّبة ترتيباً حرفياً ثابتاً.</summary>
    public static IReadOnlyList<string> Accepted { get; } = [Bank, Cash];

    /// <summary>هل هذا المؤهّل مقبول على مستند دفع في هذه الوحدة؟</summary>
    /// <param name="method">المؤهّل كما وصل.</param>
    public static bool IsAccepted(string? method)
        => method is not null && Accepted.Contains(method, StringComparer.Ordinal);
}
