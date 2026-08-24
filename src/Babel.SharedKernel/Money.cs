using System.Globalization;

namespace Babel.SharedKernel;

/// <summary>
/// مبلغ نقدي: <see cref="decimal"/> دائماً ومقياس أربعة خانات دائماً.
/// <para>
/// لماذا المقياس 4 جزء من النوع لا من قاعدة البيانات: <c>numeric(19,4)</c> يعيد <c>100.0000m</c>
/// حيث كُتب <c>100.00m</c>. القيمة نفسها، لكن بايت المقياس في <c>decimal.GetBits()</c> يختلف،
/// فتختلف البصمة (وثيقة المعمارية §8.2 مصيدة 2). تثبيت المقياس هنا يمنع مساراً كاملاً من الأعطال
/// التي لا تظهر إلا بعد أول دورة ذهاب وإياب مع قاعدة البيانات.
/// </para>
/// <para>
/// ولماذا لا يوجد <c>double</c>: CONTRIBUTING §3 بند 2، ومفروض ببناء في
/// tests/Babel.ArchitectureTests/Rule04_MoneyIsDecimal.cs.
/// </para>
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>المقياس القانوني للمبالغ في كل النطاق.</summary>
    public const int CanonicalScale = 4;

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>المبلغ بمقياس أربع خانات عشرية بالضبط.</summary>
    public decimal Amount { get; }

    /// <summary>عملة المبلغ.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>صفر بالعملة المعطاة.</summary>
    public static Money Zero(CurrencyCode currency) => new(0.0000m, currency);

    /// <summary>
    /// ينشئ مبلغاً. يُرفض أي مدخل بأكثر من أربع خانات عشرية — عمداً:
    /// التقريب سياسة محاسبية تخصّ السطر والمستند، ولا يجوز أن يقع صامتاً داخل نوع قيمة.
    /// </summary>
    public static Money Of(decimal amount, CurrencyCode currency)
    {
        if (!currency.IsAssigned)
        {
            throw new ArgumentException("عملة غير مهيّأة. / Currency is not assigned.", nameof(currency));
        }

        if (ScaleOf(amount) > CanonicalScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "المبلغ يتجاوز أربع خانات عشرية. التقريب قرار محاسبي صريح، لا سلوك ضمني. / "
                + "Amount exceeds four decimal places; rounding must be an explicit accounting decision.");
        }

        return new Money(Rescale(amount), currency);
    }

    /// <summary>جمع مبلغين بالعملة نفسها.</summary>
    public static Money operator +(Money left, Money right) => left.Add(right);

    /// <summary>طرح مبلغين بالعملة نفسها.</summary>
    public static Money operator -(Money left, Money right) => left.Subtract(right);

    /// <summary>سالب المبلغ.</summary>
    public static Money operator -(Money value) => value.Negate();

    /// <summary>جمع مبلغين بالعملة نفسها.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Rescale(Amount + other.Amount), Currency);
    }

    /// <summary>طرح مبلغين بالعملة نفسها.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Rescale(Amount - other.Amount), Currency);
    }

    /// <summary>سالب المبلغ.</summary>
    public Money Negate() => new(Rescale(-Amount), Currency);

    /// <summary>هل المبلغ صفر؟</summary>
    public bool IsZero => Amount == 0m;

    /// <inheritdoc />
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    /// <summary>أصغر من.</summary>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>أصغر من أو يساوي.</summary>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>أكبر من.</summary>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>أكبر من أو يساوي.</summary>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// التمثيل القانوني: مقياس ثابت ولغة ثابتة. هذا هو ما يدخل دالة التوحيد القياسي،
    /// ولا يقترب منه أي <c>ToString()</c> واعٍ باللغة (وثيقة المعمارية §8.2 مصيدة 3).
    /// </summary>
    public string ToCanonicalString() => Amount.ToString("0.0000", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{ToCanonicalString()} {Currency}");

    private void EnsureSameCurrency(Money other)
    {
        if (!Currency.Equals(other.Currency))
        {
            throw new InvalidOperationException(
                $"لا تُجمع عملتان مختلفتان دون سعر صرف صريح: {Currency} و {other.Currency}. / "
                + $"Cannot combine {Currency} and {other.Currency} without an explicit exchange rate.");
        }
    }

    private static int ScaleOf(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static decimal Rescale(decimal value) => value + 0.0000m;
}
