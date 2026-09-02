namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// قيمٌ <b>مصطنعة القيمة صحيحة الشكل</b> — على مثال <c>tools/secret-scan/positive-control.txt</c>.
/// لا واحدة منها تخصّ إنساناً؛ وكلّها تُطابق الشكل الذي يجب أن يُرفض.
/// </summary>
internal static class BoundaryFixtures
{
    /// <summary>هوية: عشر خانات تبدأ بـ1.</summary>
    public const string NationalId = "1092837465";

    /// <summary>إقامة: عشر خانات تبدأ بـ2.</summary>
    public const string ResidencyId = "2451097386";

    /// <summary>آيبان سعودي متّصل — الشكل الذي كان الحارس القديم يلتقطه وحده.</summary>
    public const string Iban = "SA0380000000608010167519";

    /// <summary>الآيبان نفسه كما يكتبه الناس فعلاً: مجموعاتٌ رباعية.</summary>
    public const string IbanGrouped = "SA03 8000 0000 6080 1016 7519";

    /// <summary>رقم تسجيل ضريبي: خمس عشرة خانة بين 3 و3.</summary>
    public const string Vat = "300123456789003";

    /// <summary>سجلّ تجاري: عشر خانات لا تبدأ بـ1 ولا 2 ولا 05.</summary>
    public const string CommercialRegister = "4030123456";

    /// <summary>جوّال محلّي.</summary>
    public const string Phone = "0512345678";

    /// <summary>الجوّال نفسه بالصيغة الدولية.</summary>
    public const string PhoneInternational = "+966512345678";

    /// <summary>سلسلة رقمية طويلة لا يطابقها شكلٌ مُسمّى — اثنتا عشرة خانة.</summary>
    public const string DigitRun = "123456789012";

    /// <summary>
    /// الشاهد الموجب: واحدٌ من كل شكلٍ من الأشكال الستّة المُسمّاة في التصميم، وكلٌّ منها
    /// يليه <b>حرفٌ عربي</b> لا خانة — كي لا يلمّ اللمُّ عددين متجاورين فيختلط العدّ.
    /// </summary>
    public const string OneOfEachShape =
        "الهوية " + NationalId
        + " والآيبان " + Iban
        + " والرقم الضريبي " + Vat
        + " والسجل التجاري " + CommercialRegister
        + " والجوال " + Phone
        + " والمرجع " + DigitRun + " انتهى";
}
