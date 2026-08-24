namespace Babel.SharedKernel;

/// <summary>
/// اسم ثنائي اللغة. العربية أساسية لا ترجمة ثانية (وثيقة المعمارية §16).
/// كل مسمّى بيانات تأسيسية يحمل <c>name_ar</c> و<c>name_en</c> (CONTRIBUTING §3 بند 5) —
/// ولذلك النوع يفرض وجود الاثنين بدل جدول ترجمات منفصل يُنسى ملؤه.
/// </summary>
public readonly record struct LocalizedName
{
    private readonly string? _ar;
    private readonly string? _en;

    /// <summary>ينشئ اسماً ثنائي اللغة. الطرفان إلزاميان.</summary>
    public LocalizedName(string arabic, string english)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(english);
        _ar = arabic;
        _en = english;
    }

    /// <summary>الاسم العربي.</summary>
    public string Arabic => _ar ?? throw new InvalidOperationException("اسم غير مهيّأ. / Uninitialised name.");

    /// <summary>الاسم الإنجليزي.</summary>
    public string English => _en ?? throw new InvalidOperationException("اسم غير مهيّأ. / Uninitialised name.");

    /// <summary>هل الاسم مهيّأ؟</summary>
    public bool IsAssigned => _ar is not null && _en is not null;

    /// <inheritdoc />
    public override string ToString() => _ar ?? string.Empty;
}
