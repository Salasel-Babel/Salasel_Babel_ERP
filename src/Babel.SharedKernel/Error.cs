namespace Babel.SharedKernel;

/// <summary>
/// خطأ مجالي برمز ثابت ورسالتين. الرمز هو ما تعتمد عليه الشيفرة؛ الرسالتان للعرض.
/// ثنائية اللغة هنا ليست ترفاً: رسالة الخطأ تظهر للمحاسب، والمحاسب يقرأ بالعربية.
/// </summary>
public sealed record Error
{
    /// <summary>ينشئ خطأ مجالياً.</summary>
    public Error(string code, string messageAr, string messageEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageEn);
        Code = code;
        MessageAr = messageAr;
        MessageEn = messageEn;
    }

    /// <summary>رمز الخطأ الثابت — نقطة الاعتماد البرمجية.</summary>
    public string Code { get; }

    /// <summary>الرسالة بالعربية.</summary>
    public string MessageAr { get; }

    /// <summary>الرسالة بالإنجليزية.</summary>
    public string MessageEn { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {MessageAr} / {MessageEn}";
}
