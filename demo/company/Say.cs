using System.Globalization;

namespace BabelDemoCompany;

/// <summary>
/// الطباعة: خطوةٌ، وتفصيلٌ، وحكمٌ بدليله.
/// <para>
/// وكل حكم يُطبع <b>ومعه الرقم الذي أنتجه</b>، لا كلمة «تمّ»: تشغيلٌ يقول «تمّ» ولا
/// يُظهر ميزاناً متوازناً لا يُثبت شيئاً لمن يقرأ سجلّ النشر بعد شهر.
/// </para>
/// </summary>
internal static class Say
{
    /// <summary>عنوان خطوة.</summary>
    /// <param name="text">النصّ.</param>
    public static void Step(string text) => Console.WriteLine("\n── " + text);

    /// <summary>سطر تفصيل تحت خطوة.</summary>
    /// <param name="text">النصّ.</param>
    public static void Detail(string text) => Console.WriteLine("   · " + text);

    /// <summary>حكمٌ بدليله. يرمي عند السقوط: بذرٌ نصفه صحيح أسوأ من بذر فاشل.</summary>
    /// <param name="condition">الحكم.</param>
    /// <param name="title">ما يُدّعى.</param>
    /// <param name="evidence">الرقم أو النصّ الذي أنتج الحكم.</param>
    public static void Require(bool condition, string title, string evidence)
    {
        Console.WriteLine((condition ? "   ✔ " : "   ✘ ") + title);
        Console.WriteLine("     الدليل: " + evidence);

        if (!condition)
        {
            throw new InvalidOperationException("سقط الإثبات — " + title + " · " + evidence);
        }
    }

    /// <summary>مبلغ بأربع خانات وبثقافة ثابتة — لا فاصلة عشرية تتغيّر بلغة الخادم.</summary>
    /// <param name="value">القيمة.</param>
    public static string Money(decimal value) => value.ToString("N4", CultureInfo.InvariantCulture);

    /// <summary>عدد بثقافة ثابتة.</summary>
    /// <param name="value">القيمة.</param>
    public static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
