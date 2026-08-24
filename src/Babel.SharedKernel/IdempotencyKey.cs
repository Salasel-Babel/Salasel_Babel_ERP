namespace Babel.SharedKernel;

/// <summary>
/// مفتاح حصانة يوفّره العميل. القاعدة المعمارية 4: الحصانة ضد التكرار لكل قيد ومستقلة عن الترتيب.
/// الحارس التسلسلي المتزايد ممنوع لأن مزامنة نقاط البيع دون اتصال تُسلّم خارج الترتيب بطبيعتها
/// (وثيقة المعمارية §6 القاعدة 4).
/// محارف ASCII فقط: المفتاح يدخل مفتاحاً أساسياً ويُجزَّأ.
/// </summary>
public readonly record struct IdempotencyKey
{
    private readonly string? _value;

    /// <summary>ينشئ مفتاح حصانة بعد التحقق من شكله.</summary>
    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > 128)
        {
            throw new ArgumentException("مفتاح الحصانة أطول من 128 محرفاً. / Idempotency key exceeds 128 characters.", nameof(value));
        }

        foreach (char c in value)
        {
            bool allowed = c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-' or '_' or ':' or '.';
            if (!allowed)
            {
                throw new ArgumentException(
                    "مفتاح الحصانة محارف ASCII آمنة فقط [0-9A-Za-z-_:.]. / Idempotency key must be safe ASCII only.",
                    nameof(value));
            }
        }

        _value = value;
    }

    /// <summary>القيمة النصية للمفتاح.</summary>
    public string Value => _value ?? throw new InvalidOperationException(
        "مفتاح حصانة غير مهيّأ. / Uninitialised idempotency key.");

    /// <summary>هل المفتاح مهيّأ؟</summary>
    public bool IsAssigned => _value is not null;

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
