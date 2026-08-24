namespace BabelPosOffline.Support;

/// <summary>سلوك الجهاز عند بلوغ سقف نافذة الإبلاغ — <b>قرار عمل، لا ثابت في الكود</b>.</summary>
public enum CeilingBehaviour
{
    /// <summary>الأكثر تحفّظاً: يتوقّف البيع. الافتراضي في هذه التجربة.</summary>
    StopTrading,
    /// <summary>يستمر البيع مع إنذار مستمر وتسجيل كل عملية بعد السقف بعلامة صريحة.</summary>
    ContinueWithAlarm
}

/// <summary>سلوك الجهاز عند نفاد مدى الأرقام المحجوز وهو غير متصل.</summary>
public enum RangeExhaustionBehaviour
{
    StopTrading,
    ContinueWithAlarm   // يستهلك مدى الطوارئ الاحتياطي إن وُجد، وإلا يتوقّف رغماً عنه
}

/// <summary>سياسة المرتجع اليتيم (مرتجع بلا بيع أصلي مُزامَن).</summary>
public enum OrphanReturnPolicy
{
    /// <summary>الأكثر تحفّظاً: يُحجز في طابور استثناءات ولا يُرحَّل. الافتراضي.</summary>
    Quarantine,
    /// <summary>يُرحَّل مع إنذار (يتطلّب قراراً من العمل — انظر الأسئلة المفتوحة).</summary>
    PostWithAlarm
}

public sealed record PosSettings
{
    // ── سقف نافذة الإبلاغ ──────────────────────────────────────────────────
    /// <summary>
    /// نافذة الإبلاغ للفاتورة المبسّطة. <b>غير مُتحقَّق منه</b> — موقع الهيئة محجوب عن هذه
    /// الشبكة. يُستخدم 24 ساعة كسقف عمل، وهو <b>قابل للضبط</b> لأنه رقم تنظيمي لا هندسي.
    /// UNVERIFIED: the authority's site is blocked from this network. 24h is used as a
    /// working ceiling and is configurable because it is a regulatory number, not an
    /// engineering one.
    /// </summary>
    public TimeSpan ReportingCeiling { get; init; } = TimeSpan.FromHours(24);

    /// <summary>إنذار مبكّر: عند 60٪ من السقف (14.4 ساعة) يبدأ التحذير المرئي.</summary>
    public double WarnAtFraction { get; init; } = 0.60;

    /// <summary>إنذار حرج: عند 85٪ من السقف (20.4 ساعة).</summary>
    public double CriticalAtFraction { get; init; } = 0.85;

    public CeilingBehaviour AtCeiling { get; init; } = CeilingBehaviour.StopTrading;

    // ── مدى الأرقام المحجوز ────────────────────────────────────────────────
    /// <summary>حجم المدى: 500–1500 عملية يومياً ⇒ 5,000 بهامش ≈ 3–10 أيام تشغيل.</summary>
    public long RangeSize { get; init; } = 5_000;

    /// <summary>يطلب مدى جديداً عند تبقّي هذه النسبة (20٪ = 1,000 رقم ≈ يوم كامل احتياطي).</summary>
    public double RangeRefillAtRemainingFraction { get; init; } = 0.20;

    public RangeExhaustionBehaviour AtRangeExhaustion { get; init; } = RangeExhaustionBehaviour.StopTrading;

    // ── المزامنة ───────────────────────────────────────────────────────────
    public int MaxBatchSize { get; init; } = 100;
    public int MinBatchSize { get; init; } = 10;

    // ── التعارضات ──────────────────────────────────────────────────────────
    public OrphanReturnPolicy OrphanReturn { get; init; } = OrphanReturnPolicy.Quarantine;

    /// <summary>فرق السعر الذي يُصعَّد لقرار بشري بدل تسجيله كانحراف فقط.</summary>
    public decimal PriceVarianceEscalateAbove { get; init; } = 5.0000m;

    /// <summary>انزياح ساعة الجهاز عن الخادم الذي يُصعَّد بعده لقرار بشري.</summary>
    public TimeSpan ClockSkewEscalateAbove { get; init; } = TimeSpan.FromMinutes(15);

    public static PosSettings Default => new();
}
