using System.Globalization;

namespace Babel.Canonicalization;

/// <summary>
/// اللحظات الزمنية — شكل لفظي واحد بالضبط:
/// <c>yyyy-MM-ddTHH:mm:ss.ffffffZ</c> (UTC، ست خانات كسرية = ميكروثانية).
///
/// <b>1. الميكروثانية مقابل الـ100 نانوثانية.</b> مقيس على PostgreSQL 16.13 + Npgsql 10.0.3:
/// <code>
///   كُتب DateTime بـ ticks = ...1234567  وقُرئ -> ...1234560
///   كُتب  ...+9 ticks                     وقُرئ -> +0
///   كُتب  .9999999                        وقُرئ -> .9999990
/// </code>
/// أي أن Npgsql <b>تقصّ</b> ولا تقرّب. ولذلك القصّ هنا هو <c>ticks - ticks % 10</c>
/// بالضبط، فتتطابق البايتات قبل الكتابة وبعد القراءة. لو قرّبنا، لانحرفنا عن السائق.
///
/// <b>2. <c>DateTimeKind.Unspecified</c> يجب أن يُرفض هنا، لأن السائق لا يرفضه.</b> مقيس:
/// <code>
///   insert timestamptz مع Kind=Unspecified -> ACCEPTED
///   insert timestamptz مع Kind=Local       -> ACCEPTED
/// </code>
/// (كانت Npgsql 6 ترمي على Unspecified؛ الإصدار 10 يقبل.) وقيمة Unspecified هي
/// بالضبط ما تنتجه <c>DateTime.Parse</c> لنصّ بلا إزاحة، وما تنتجه معظم المُحوِّلات.
/// تفسيرها يعتمد على منطقة الجهاز، فتنقلب البصمة بتغيير إعداد المنطقة الزمنية —
/// وهو بالضبط ما تحذّر منه قاعدة «الالتقاط مرّة واحدة». الرفض هنا هو الحماية الوحيدة.
///
/// <b>3. الالتقاط مرّة واحدة.</b> لحظة الإنشاء تُلتقط مرّة، تُقصّ فوراً، وتُعامَل
/// مُدخلاً غير قابل للتغيير. أي هجرة «تُصلح» طوابع زمنية تاريخية تدمّر السلسلة
/// بأثر رجعي. استخدم <see cref="CaptureNow"/> عند الإنشاء ولا شيء غيرها.
///
/// <b>4. التوقيت الصيفي.</b> لا يوجد شكل محلي في المواصفة إطلاقاً. أي إزاحة
/// تُحلّ إلى UTC قبل أي شيء، فلا وجود لساعة مكرَّرة ولا لساعة مفقودة.
/// (السعودية بلا توقيت صيفي، لكن البيانات المستوردة والموردين ليسوا كذلك.)
/// </summary>
public static class Instants
{
    /// <summary>عدد الـticks في الميكروثانية الواحدة: 10.</summary>
    public const long TicksPerMicrosecond = 10;

    private const string Format = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// يلتقط اللحظة الحالية مقصوصة إلى الميكروثانية بتوقيت UTC.
    /// <b>هذه هي الطريقة الوحيدة لالتقاط لحظة إنشاء.</b>
    /// </summary>
    public static DateTime CaptureNow() => Truncate(DateTime.UtcNow);

    /// <summary>
    /// يقصّ إلى الميكروثانية ويحوّل إلى UTC. يرفض <c>Unspecified</c>.
    /// يُستدعى عند الحدّ، ويُخزَّن ناتجه.
    /// </summary>
    public static DateTime Truncate(DateTime value, string? field = null)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new CanonicalizationException(CanonErrors.InstantKindUnspecified,
                "DateTimeKind.Unspecified مرفوض. Npgsql 10 تقبله بصمت لعمود timestamptz (مقيس)، " +
                "وتفسيره يعتمد على منطقة الجهاز الزمنية، فينقلب المُجزَّأ بتغيير إعداد. " +
                "حدّد Utc صراحة، أو استخدم DateTimeOffset.", -1, field);

        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TicksPerMicrosecond, DateTimeKind.Utc);
    }

    /// <summary>يحوّل إزاحة صريحة إلى UTC مقصوص. المسار المفضَّل لكل مدخلات الواجهة.</summary>
    public static DateTime Truncate(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTime(utc.Ticks - utc.Ticks % TicksPerMicrosecond, DateTimeKind.Utc);
    }

    /// <summary>يتحقّق أن اللحظة صالحة للتجزئة كما هي (UTC ومقصوصة)، أو يرمي. لا يعدّل.</summary>
    public static DateTime Require(DateTime value, string? field = null)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            throw new CanonicalizationException(CanonErrors.InstantKindUnspecified,
                "DateTimeKind.Unspecified مرفوض في قيمة مُجزَّأة. استخدم Instants.Truncate عند الحدّ.",
                -1, field);

        if (value.Kind == DateTimeKind.Local)
            throw new CanonicalizationException(CanonErrors.InstantKindUnspecified,
                "DateTimeKind.Local مرفوض في قيمة مُجزَّأة: خزّن UTC وحوّل عند العرض. " +
                "استخدم Instants.Truncate عند الحدّ.", -1, field);

        if (value.Ticks % TicksPerMicrosecond != 0)
            throw new CanonicalizationException(CanonErrors.InstantOutOfRange,
                $"اللحظة تحمل دقّة دون الميكروثانية (ticks={value.Ticks}). " +
                "PostgreSQL تخزّن بالميكروثانية وNpgsql تقصّ عند الكتابة (مقيس)، " +
                "فلن تُتحقَّق السلسلة بعد أول دورة ذهاب وإياب. اقصص بـ Instants.Truncate قبل التخزين.",
                -1, field);

        return value;
    }

    /// <summary>الشكل اللفظي القانوني: <c>2026-03-29T01:30:00.123456Z</c>.</summary>
    public static string Render(DateTime value, string? field = null)
    {
        Require(value, field);
        return value.ToString(Format, CultureInfo.InvariantCulture);
    }

    /// <summary>الشكل اللفظي القانوني للتاريخ المجرّد: <c>2026-02-29</c>.</summary>
    public static string RenderDate(DateOnly value)
        => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>يقرأ لحظة من الشكل القانوني وحده. يرفض أي شكل آخر.</summary>
    public static DateTime ParseCanonical(string text, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!DateTime.TryParseExact(text, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
            throw new CanonicalizationException(CanonErrors.InstantBadLiteral,
                $"«{text}» ليس بالشكل القانوني {Format}.", -1, field);

        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    /// <summary>يقرأ تاريخاً من الشكل القانوني وحده.</summary>
    public static DateOnly ParseCanonicalDate(string text, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var value))
            throw new CanonicalizationException(CanonErrors.InstantBadLiteral,
                $"«{text}» ليس بالشكل القانوني {DateFormat}.", -1, field);
        return value;
    }
}
