using System.Globalization;

namespace Babel.SharedKernel;

/// <summary>
/// شهر الفوترة. مستقل عن <see cref="PeriodId"/> عمداً: الفترة المالية شأن محاسبي،
/// وشهر الفوترة شأن اشتراك. خلطهما يجعل تغيير السنة المالية يغيّر الفواتير.
/// </summary>
public readonly record struct BillingPeriod : IComparable<BillingPeriod>
{
    /// <summary>ينشئ شهر فوترة.</summary>
    public BillingPeriod(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        Year = year;
        Month = month;
    }

    /// <summary>السنة الميلادية.</summary>
    public int Year { get; }

    /// <summary>الشهر الميلادي (1..12).</summary>
    public int Month { get; }

    /// <summary>شهر الفوترة الذي تقع فيه اللحظة المعطاة بتوقيت UTC.</summary>
    public static BillingPeriod FromInstant(DateTimeOffset instant)
    {
        DateTimeOffset utc = instant.ToUniversalTime();
        return new BillingPeriod(utc.Year, utc.Month);
    }

    /// <inheritdoc />
    public int CompareTo(BillingPeriod other)
    {
        int byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    /// <summary>أصغر من.</summary>
    public static bool operator <(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) < 0;

    /// <summary>أصغر من أو يساوي.</summary>
    public static bool operator <=(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) <= 0;

    /// <summary>أكبر من.</summary>
    public static bool operator >(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) > 0;

    /// <summary>أكبر من أو يساوي.</summary>
    public static bool operator >=(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:D4}-{Month:D2}");
}
