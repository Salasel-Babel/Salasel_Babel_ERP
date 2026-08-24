namespace Babel.SharedKernel;

/// <summary>
/// رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة.
/// المحارف اللاتينية شرط سلامة سلسلة التجزئة، لا تفضيل عرض (وثيقة المعمارية §8.3 ع-3).
/// </summary>
public readonly record struct CurrencyCode
{
    private readonly string? _code;

    /// <summary>ينشئ رمز عملة بعد التحقق من شكله.</summary>
    public CurrencyCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (code.Length != 3)
        {
            throw new ArgumentException("رمز العملة ثلاثة محارف بالضبط. / Currency code must be exactly three characters.", nameof(code));
        }

        foreach (char c in code)
        {
            if (c is < 'A' or > 'Z')
            {
                throw new ArgumentException(
                    "رمز العملة محارف ISO 4217 لاتينية كبيرة فقط. / Currency code must be upper-case ASCII letters.",
                    nameof(code));
            }
        }

        _code = code;
    }

    /// <summary>الريال السعودي — عملة الشركة الافتراضية.</summary>
    public static CurrencyCode Sar => new("SAR");

    /// <summary>القيمة النصية للرمز.</summary>
    public string Value => _code ?? throw new InvalidOperationException(
        "رمز عملة غير مهيّأ. / Uninitialised currency code.");

    /// <summary>هل الرمز مهيّأ؟</summary>
    public bool IsAssigned => _code is not null;

    /// <inheritdoc />
    public override string ToString() => _code ?? string.Empty;

    /// <summary>تحويل صريح من نص.</summary>
    public static explicit operator CurrencyCode(string code) => new(code);

    /// <summary>تحويل صريح من نص — بديل مسمّى لمحلّلات التسمية.</summary>
    public static CurrencyCode FromString(string code) => new(code);
}
