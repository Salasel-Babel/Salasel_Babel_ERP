namespace Babel.SharedKernel;

/// <summary>معرّف الفترة المالية. الإقفال والفتح شأن Babel.Ledger وحده.</summary>
public readonly record struct PeriodId(Guid Value)
{
    /// <summary>قيمة غير مخصّصة.</summary>
    public static PeriodId None => new(Guid.Empty);

    /// <summary>هل المعرّف مخصّص فعلاً؟</summary>
    public bool IsAssigned => Value != Guid.Empty;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
