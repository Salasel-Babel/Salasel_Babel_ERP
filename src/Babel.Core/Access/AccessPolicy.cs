using System.Globalization;

namespace Babel.Core.Access;

/// <summary>
/// <b>مُدَد الاعتمادات الثلاث — سياسةُ أمنٍ تُضبَط في النشر، لا ثوابتُ شيفرة.</b>
/// <para>
/// <b>لماذا خرجت من <c>static readonly</c>:</b> هذه الأرقام هي أوّل ما يُشدَّد <b>لحظةَ
/// حادثة</b>. ومدّةُ اعتماد التجديد بالذات هي <b>المدّة التي يبقى فيها اعتمادٌ مسروق
/// صالحاً</b> — فأربعةَ عشرَ يوماً جوابٌ معقول في يومٍ عادي، وجوابٌ لا يُحتمل في اليوم
/// الذي يُكتشف فيه تسريب. ورقمٌ مكتوبٌ في شيفرة يعني أن الردّ على حادثةٍ يمرّ ببناءٍ
/// ونشرةٍ كاملة، وهو زمنٌ لا يملكه أحد ساعتها.
/// </para>
/// <para>
/// <b>وهذا ليس ارتداداً صامتاً</b>، والفارق مقصود ومكتوب: القيم أدناه <b>سياسةٌ معلَنة
/// موثَّقة ومحدودةٌ بسقف</b>، لا <b>تخمينٌ عن النشر</b>. الذي يُرفض غيابه هو ما لا
/// جوابَ آمن له خارج النشر — نصُّ اتصال، أو مفتاح توقيع. أمّا مدّةٌ لها جوابٌ صحيح
/// منشور، وسقفٌ يمنع أن تنحرف، فغيابُها يعني «أبقِ السياسة المعلَنة» ولا يعني شيئاً
/// مخفياً. <b>والقيمة الخارجة عن السقف تُرفض ولا تُقصّ</b>: القصُّ الصامت يجعل من ضبط
/// ثلاثين يوماً يظنّ أنه ضبطها.
/// </para>
/// </summary>
public sealed class AccessPolicy
{
    /// <summary>متغيّر عمر الاعتماد الفاعل بالدقائق.</summary>
    public const string AccessLifetimeVariable = "BABEL_ACCESS_LIFETIME_MINUTES";

    /// <summary>متغيّر عمر اعتماد التجديد بالساعات.</summary>
    public const string RefreshLifetimeVariable = "BABEL_ACCESS_REFRESH_HOURS";

    /// <summary>متغيّر مهلة قبول الدعوة بالساعات.</summary>
    public const string EnrolmentLifetimeVariable = "BABEL_ACCESS_ENROLMENT_HOURS";

    /// <summary>
    /// عمر الاعتماد الفاعل المُعلَن: خمس عشرة دقيقة. قصيرٌ عمداً — هو ما يُحمل في كل
    /// طلب فهو الأكثر تعرّضاً.
    /// </summary>
    public static TimeSpan DeclaredAccessLifetime { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// عمر اعتماد التجديد المُعلَن: أربعةَ عشرَ يوماً. طويلٌ لأنه لا يُحمل إلا مرّة كل
    /// دورة ويدور في كل مرّة — <b>وهو مع ذلك المدّة التي يبقى فيها المسروق صالحاً</b>.
    /// </summary>
    public static TimeSpan DeclaredRefreshLifetime { get; } = TimeSpan.FromDays(14);

    /// <summary>مهلة قبول الدعوة المُعلَنة: سبعة أيام. مهلةٌ لا اعتمادَ استعمال.</summary>
    public static TimeSpan DeclaredEnrolmentLifetime { get; } = TimeSpan.FromDays(7);

    /// <summary>سقف عمر الاعتماد الفاعل: ساعة. ما فوقها يُلغي معنى «قصيرٌ عمداً».</summary>
    public static TimeSpan MaximumAccessLifetime { get; } = TimeSpan.FromHours(1);

    /// <summary>سقف عمر اعتماد التجديد: ثلاثون يوماً.</summary>
    public static TimeSpan MaximumRefreshLifetime { get; } = TimeSpan.FromDays(30);

    /// <summary>سقف مهلة الدعوة: ثلاثون يوماً.</summary>
    public static TimeSpan MaximumEnrolmentLifetime { get; } = TimeSpan.FromDays(30);

    /// <summary>عمر الاعتماد الفاعل المُطبَّق فعلاً.</summary>
    public TimeSpan AccessLifetime { get; set; } = FromConfigured(
        Environment.GetEnvironmentVariable(AccessLifetimeVariable),
        TimeSpan.FromMinutes(1), DeclaredAccessLifetime, AccessLifetimeVariable);

    /// <summary>عمر اعتماد التجديد المُطبَّق فعلاً.</summary>
    public TimeSpan RefreshLifetime { get; set; } = FromConfigured(
        Environment.GetEnvironmentVariable(RefreshLifetimeVariable),
        TimeSpan.FromHours(1), DeclaredRefreshLifetime, RefreshLifetimeVariable);

    /// <summary>مهلة قبول الدعوة المُطبَّقة فعلاً.</summary>
    public TimeSpan EnrolmentLifetime { get; set; } = FromConfigured(
        Environment.GetEnvironmentVariable(EnrolmentLifetimeVariable),
        TimeSpan.FromHours(1), DeclaredEnrolmentLifetime, EnrolmentLifetimeVariable);

    /// <summary>
    /// يرفض كل مدّةٍ خارج سقفها، ويرفض سياسةً غير متماسكة (تجديدٌ أقصر من الفاعل).
    /// <b>يُنادى عند الإقلاع</b> — فسياسةٌ مضبوطةٌ خطأً تُكتشف قبل أوّل جلسة لا بعدها.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن خرجت مدّةٌ عن حدّها، أو تناقضت المدّتان.</exception>
    public void EnsureWithinCeiling()
    {
        Check(AccessLifetime, MaximumAccessLifetime, AccessLifetimeVariable, "عمر الاعتماد الفاعل", "the access credential lifetime");
        Check(RefreshLifetime, MaximumRefreshLifetime, RefreshLifetimeVariable, "عمر اعتماد التجديد", "the refresh credential lifetime");
        Check(EnrolmentLifetime, MaximumEnrolmentLifetime, EnrolmentLifetimeVariable, "مهلة قبول الدعوة", "the enrolment window");

        if (RefreshLifetime < AccessLifetime)
        {
            throw new InvalidOperationException(
                "access.policy_incoherent — عمر اعتماد التجديد أقصر من عمر الاعتماد الفاعل، وهي سياسةٌ "
                + "لا معنى لها: الجلسة تنتهي قبل أن يوجد ما يجدّدها. / "
                + "access.policy_incoherent — the refresh lifetime is shorter than the access lifetime.");
        }
    }

    private static void Check(TimeSpan value, TimeSpan ceiling, string variable, string subjectAr, string subjectEn)
    {
        if (value > TimeSpan.Zero && value <= ceiling)
        {
            return;
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"access.policy_out_of_range — {subjectAr} خارج مداه: المضبوط {value} والسقف {ceiling}. "
            + $"اضبط {variable} بعددٍ موجب لا يتجاوز السقف؛ ولا تُقصّ القيمة صمتاً، فمن ضبط ما فوق السقف "
            + $"يظنّ أنه ضبطه. / access.policy_out_of_range — {subjectEn} is outside its range: "
            + $"configured {value}, ceiling {ceiling}. Set {variable} within the ceiling."));
    }

    /// <summary>
    /// يحسم مدّةً من قيمةٍ مضبوطة بوحدةٍ معلَنة. <b>الغياب يعني «السياسة المعلَنة»</b>،
    /// و<b>قيمةٌ لا تُقرأ عدداً تُرفض</b> ولا تُبتلع: متغيّرٌ فيه خطأ مطبعي يجعل من
    /// شدّد السياسة يظنّ أنه شدّدها وهي على حالها — وذلك أخطر من ألّا يضبط شيئاً.
    /// <para>دالّة صافية عمداً كي تُختبَر بلا لمس بيئة العملية.</para>
    /// </summary>
    /// <param name="raw">القيمة كما وصلت.</param>
    /// <param name="unit">وحدةُ العدد — دقيقة أو ساعة.</param>
    /// <param name="declared">السياسة المُعلَنة عند الغياب.</param>
    /// <param name="variable">اسم المتغيّر — يُذكر في الرفض.</param>
    /// <exception cref="InvalidOperationException">إن كانت القيمة مضبوطة ولا تُقرأ عدداً صحيحاً.</exception>
    public static TimeSpan FromConfigured(string? raw, TimeSpan unit, TimeSpan declared, string variable)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return declared;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int units))
        {
            throw new InvalidOperationException(
                "access.policy_not_a_number — " + variable + " مضبوطٌ بقيمةٍ لا تُقرأ عدداً صحيحاً. "
                + "ولا تُبتلع: من ضبطها يظنّ أنه شدّد السياسة وهي على حالها. / "
                + "access.policy_not_a_number — " + variable + " is not an integer.");
        }

        return units * unit;
    }
}
