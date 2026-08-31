namespace Babel.Ledger.Accounts;

/// <summary>
/// رقم حساب في دليل الحسابات.
/// <para>
/// <b>هذا النوع <c>internal</c>، وهو جوهر القاعدة 2.</b> لا وحدة خارج الدفتر تستطيع
/// أن تسمّي حساباً — لا لأنها اتفقت على ذلك، بل لأن النوع غير مرئي لها أصلاً.
/// الوحدة تصف حدثاً تجارياً بـ<c>PostingRole</c>، ومصفوفة الترحيل هنا تحوّل الدور إلى رقم.
/// </para>
/// <para>
/// الفائدة العملية: تعديل قاعدة ترحيل يصبح تعديل صف في جدول، لا تعديل كود في وحدة المبيعات
/// (03-accounting-core.md §4).
/// </para>
/// </summary>
internal readonly record struct AccountCode
{
    private readonly string? _value;

    public AccountCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        foreach (char c in value)
        {
            // أرقام ASCII فقط: الأرقام العربية-الهندية تبدو صحيحة وتُجزَّأ خطأ
            // (وثيقة المعمارية §8.3 ع-3).
            if (c is < '0' or > '9')
            {
                throw new ArgumentException(
                    "رقم الحساب أرقام ASCII فقط. / Account code must be ASCII digits only.",
                    nameof(value));
            }
        }

        _value = value;
    }

    public string Value => _value ?? throw new InvalidOperationException("رقم حساب غير مهيّأ. / Uninitialised account code.");

    public override string ToString() => _value ?? string.Empty;
}
