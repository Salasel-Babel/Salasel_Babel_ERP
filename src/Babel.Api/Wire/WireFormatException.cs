using System.Text.Json;

namespace Babel.Api.Wire;

/// <summary>
/// رفض شكلي عند حدّ HTTP: القيمة الواصلة لا تطابق قواعد السلك.
/// <para>
/// يرث <see cref="JsonException"/> عمداً كي يستطيع محوّل <c>System.Text.Json</c> أن يرميه
/// من داخل عملية القراءة، فيصل إلى نقطة النهاية برمزه سليماً بدل أن يُبتلع في رسالة
/// عامة «حمولة JSON غير صالحة» لا تقول للعميل <b>أي</b> حقل رُفض ولا لماذا.
/// </para>
/// <para>
/// والرمز هو ما يُعتمد عليه برمجياً؛ الرسالتان للعرض. لا يُقرأ نصّ رسالة أبداً لاتخاذ قرار.
/// </para>
/// </summary>
internal sealed class WireFormatException : JsonException
{
    /// <summary>ينشئ رفضاً شكلياً برمزه ورسالتيه.</summary>
    /// <param name="code">الرمز الثابت.</param>
    /// <param name="messageAr">الرسالة العربية.</param>
    /// <param name="messageEn">الرسالة الإنجليزية.</param>
    /// <param name="field">اسم الحقل على السلك، إن عُرف.</param>
    public WireFormatException(string code, string messageAr, string messageEn, string? field = null)
        : base(messageEn)
    {
        Code = code;
        MessageAr = messageAr;
        MessageEn = messageEn;
        Field = field;
    }

    /// <summary>الرمز الثابت للرفض.</summary>
    public string Code { get; }

    /// <summary>الرسالة العربية.</summary>
    public string MessageAr { get; }

    /// <summary>الرسالة الإنجليزية.</summary>
    public string MessageEn { get; }

    /// <summary>الحقل المرفوض على السلك، إن عُرف.</summary>
    public string? Field { get; }

    /// <summary>يستخرج الرفض الشكلي من استثناء تسلسل، مهما كان عمق تغليفه.</summary>
    /// <param name="exception">الاستثناء المرصود.</param>
    public static WireFormatException? Unwrap(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is WireFormatException wire)
            {
                return wire;
            }
        }

        return null;
    }
}
