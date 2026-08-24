using System.Globalization;
using System.Text;

namespace Babel.ControlPlane.Support;

/// <summary>
/// التوحيد القياسي المشترك — دالة واحدة لكل نوع (فخ-16، فخ-17، فخ-18).
/// مستوى التحكّم لا يوقّع قيوداً، لكنه <b>يُنتج أرقام فوترة</b> وطوابع زمنية
/// تُقارَن عبر الأنظمة، فتُطبَّق القاعدة نفسها بلا استثناء.
/// </summary>
public static class Canon
{
    /// <summary>
    /// قصّ إلى الميكروثانية قبل الحفظ وقبل أي مقارنة: <c>timestamptz</c> في
    /// PostgreSQL دقّته ميكروثانية، ونبضة .NET أدقّ منها — فالقيمة المُعادة
    /// من القاعدة <b>لا تساوي</b> المكتوبة ما لم تُقصّ (فخ-16).
    /// </summary>
    /// <param name="t">اللحظة.</param>
    /// <returns>اللحظة مقصوصةً إلى الميكروثانية بتوقيت UTC.</returns>
    public static DateTimeOffset Instant(DateTimeOffset t)
    {
        var utc = t.ToUniversalTime();
        var ticks = utc.Ticks - utc.Ticks % 10; // 1 µs = 10 ticks
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    /// <summary>اللحظة الآن، مقصوصةً إلى الميكروثانية.</summary>
    /// <returns>اللحظة الحالية بدقّة القاعدة.</returns>
    public static DateTimeOffset Now() => Instant(DateTimeOffset.UtcNow);

    /// <summary>
    /// تمثيل نصّي وحيد للمبالغ: مقياس 4 وثقافة ثابتة. أي <c>ToString()</c>
    /// واعٍ باللغة قرب رقم مالي = خطأ فوترة صامت (فخ-17، فخ-18).
    /// </summary>
    /// <param name="value">المبلغ.</param>
    /// <returns>نصّ بمقياس 4 وثقافة ثابتة.</returns>
    public static string Amount(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.ToEven)
               .ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>
    /// تطبيع نصّ عربي عند <b>الحدّ</b> مرة واحدة: NFC، ورفض المحارف الاتجاهية
    /// غير المرئية (فخ-23، فخ-24). مستوى التحكّم يستقبل أسماء مستأجرين ووحدات
    /// بالعربية، وهي تدخل تقارير الفوترة.
    /// </summary>
    /// <param name="raw">النصّ الخام.</param>
    /// <param name="field">اسم الحقل — يظهر في رسالة الرفض.</param>
    /// <returns>النصّ بعد NFC والتشذيب.</returns>
    /// <exception cref="ArgumentException">النصّ يحوي محرف تحكّم اتجاهي غير مرئي.</exception>
    public static string Text(string raw, string field)
    {
        foreach (var ch in raw)
            if (ch is '‎' or '‏' or '‪' or '‫' or '‬'
                or '‭' or '‮' or '⁦' or '⁧' or '⁨' or '⁩')
                throw new ArgumentException(
                    $"«{field}» يحتوي محرف تحكّم اتجاهي غير مرئي (U+{(int)ch:X4}) — مرفوض عند الحدّ",
                    nameof(raw));
        return raw.Normalize(NormalizationForm.FormC).Trim();
    }
}

/// <summary>
/// اسم ثنائي اللغة. <b>كل</b> كيان بيانات أساسية في هذا المشروع يحمل
/// <c>name_ar</c> و<c>name_en</c> — لا استثناء ولا حقل واحد «متعدد اللغات».
/// </summary>
public readonly record struct BilingualName(string Ar, string En)
{
    /// <summary>يبني اسماً ثنائي اللغة بعد التطبيع، ويرفض الفارغ من أي من اللغتين.</summary>
    /// <param name="ar">الاسم العربي.</param>
    /// <param name="en">الاسم الإنجليزي.</param>
    /// <returns>الاسم بعد التطبيع.</returns>
    /// <exception cref="ArgumentException">أحد الاسمين فارغ أو يحمل محرف تحكّم اتجاهي.</exception>
    public static BilingualName Of(string ar, string en)
    {
        var a = Canon.Text(ar, "name_ar");
        var e = Canon.Text(en, "name_en");
        if (a.Length == 0) throw new ArgumentException("name_ar فارغ", nameof(ar));
        if (e.Length == 0) throw new ArgumentException("name_en فارغ", nameof(en));
        return new BilingualName(a, e);
    }

    /// <summary>تمثيل نصّي للعرض والتشخيص.</summary>
    /// <returns>«العربي / الإنجليزي».</returns>
    public override string ToString() => $"{Ar} / {En}";
}
