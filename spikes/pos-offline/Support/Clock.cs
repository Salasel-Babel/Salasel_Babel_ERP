using System.Diagnostics;

namespace BabelPosOffline.Support;

/// <summary>
/// ساعة الجهاز: ساعة حائط <b>غير موثوقة</b> + ساعة رتيبة (monotonic) موثوقة داخل
/// دورة التشغيل الواحدة فقط. الاثنتان مطلوبتان: ساعة الحائط لأنها ما يُوثَّق في المستند،
/// والرتيبة لأنها الوحيدة التي لا يستطيع الكاشير تغييرها من إعدادات الجهاز.
/// </summary>
public sealed class DeviceClock
{
    private readonly Stopwatch _mono = Stopwatch.StartNew();
    private long _monoOffsetMs;

    /// <summary>انزياح مُحقَن لمحاكاة ساعة خاطئة / injected offset simulating a wrong clock.</summary>
    public TimeSpan Offset { get; private set; } = TimeSpan.Zero;

    /// <summary>معرّف دورة التشغيل: يتغيّر عند كل إقلاع، ويبطل مقارنة القيم الرتيبة عبره.</summary>
    public string BootId { get; } = Guid.CreateVersion7().ToString("N");

    public DateTime WallUtcNow => Canonical.PgInstant(DateTime.UtcNow + Offset);

    public long MonotonicMs => _mono.ElapsedMilliseconds + _monoOffsetMs;

    /// <summary>
    /// مرور زمن حقيقي: يتقدّم <b>كلا</b> المصدرين. (محاكاة اختبارية لساعات طويلة.)
    /// </summary>
    public void Advance(TimeSpan d) { Offset += d; _monoOffsetMs += (long)d.TotalMilliseconds; }

    /// <summary>
    /// يقفز بساعة <b>الحائط وحدها</b> (للأمام أو للخلف) كما يفعل كاشير يضبط التاريخ يدوياً.
    /// الساعة الرتيبة لا تتأثر — وهذا هو جوهر الكشف.
    /// </summary>
    public void Step(TimeSpan delta) => Offset += delta;

    public void SetOffset(TimeSpan o) => Offset = o;
}

public enum AgeConfidence
{
    /// <summary>الساعة الرتيبة تغطي كامل المدة داخل دورة تشغيل واحدة — قياس موثوق.</summary>
    Monotonic,
    /// <summary>دورات تشغيل متعدّدة: مجموع أزمنة التشغيل حدّ أدنى، وزمن الإطفاء مجهول.</summary>
    AccumulatedLowerBound,
    /// <summary>لا مرساة رتيبة صالحة ولا ساعة موثوقة — العمر <b>غير معلوم</b>.</summary>
    Unknown
}

public readonly record struct BacklogAge(TimeSpan Age, AgeConfidence Confidence, TimeSpan WallEstimate, TimeSpan MonotonicEstimate)
{
    public override string ToString() =>
        (Confidence == AgeConfidence.Unknown ? "age=UNKNOWN (treated as at the ceiling)" : $"age={Age.TotalHours:F2}h")
        + $" conf={Confidence} (wall={WallEstimate.TotalHours:F2}h, mono={MonotonicEstimate.TotalHours:F2}h)";
}

/// <summary>
/// تقدير عمر أقدم عملية غير مُزامَنة — وهو <b>الرقم الذي يقرّر سلوك السقف</b>.
///
/// النتيجة المهمة: <b>جهاز بساعة خاطئة لا يستطيع قياس سقف الـ24 ساعة بساعة الحائط.</b>
/// لذلك نأخذ <c>max(ساعة الحائط, الساعة الرتيبة)</c>: إن قفزت الساعة للخلف صحّحت الرتيبةُ
/// النقصَ، وإن قفزت للأمام أخذنا التقدير الأكبر فتوقّفنا مبكّراً — وكلا الاتجاهين محافظ
/// في الاتجاه الصحيح. وإن انعدمت المرساة الرتيبة (إعادة إقلاع) وكانت الساعة مشكوكاً فيها،
/// يُعلَن العمر <b>مجهولاً</b> وتُطبَّق سياسة السقف كأننا عنده — لا كأننا في أمان.
/// </summary>
public static class AgeEstimator
{
    public static BacklogAge Estimate(
        DateTime oldestPendingDeviceClock,
        long oldestPendingMonotonicMs,
        string oldestPendingBootId,
        DeviceClock clock,
        long accumulatedUptimeMsSince,
        bool clockSuspect)
    {
        var wall = clock.WallUtcNow - oldestPendingDeviceClock;
        if (wall < TimeSpan.Zero) wall = TimeSpan.Zero;   // ساعة قفزت للخلف: التقدير عديم المعنى

        TimeSpan mono;
        AgeConfidence conf;
        if (oldestPendingBootId == clock.BootId)
        {
            mono = TimeSpan.FromMilliseconds(clock.MonotonicMs - oldestPendingMonotonicMs);
            conf = AgeConfidence.Monotonic;
        }
        else if (clockSuspect)
        {
            // دورة إقلاع أخرى + ساعة مشكوك فيها: لا حدّ أعلى على مدّة الإطفاء، ولا ساعة
            // يُوثق بها. زمن الإطفاء نفسه غير مُقاس أصلاً — والتراكم حدّ أدنى لا أكثر.
            mono = TimeSpan.FromMilliseconds(accumulatedUptimeMsSince + clock.MonotonicMs);
            conf = AgeConfidence.Unknown;
        }
        else
        {
            mono = TimeSpan.FromMilliseconds(accumulatedUptimeMsSince + clock.MonotonicMs);
            conf = AgeConfidence.AccumulatedLowerBound;
        }

        var age = wall > mono ? wall : mono;
        if (conf == AgeConfidence.Unknown) age = TimeSpan.MaxValue;   // محافظ: عامله كأنه عند السقف
        return new BacklogAge(age, conf, wall, mono);
    }
}
