using System.Text;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>تطبيع الأرقام — بقرار لا بالصدفة.</b>
/// <para>
/// ثلاثة أنظمة أرقام تصل هذا المستودع فعلاً: اللاتينية <c>0-9</c>، والعربية-الهندية
/// <c>٠-٩</c> (‏U+0660)، والديفاناغرية <c>०-९</c> (‏U+0966) — والأخيرة لأن الواجهة تحمل
/// لغةً بأرقام ديفاناغرية، فلصقٌ من لوحة مفاتيح هندية يصل الحدّ كما هو. ومعها الفارسية
/// الموسّعة <c>۰-۹</c> (‏U+06F0) لأنها تُنسَخ من نصوص عربية كثيرة.
/// </para>
/// <para>
/// <b>وما يفعله هذا الملف يُقاس بما يرفضه لا بما يقبله:</b> عددٌ يخلط نظامين —
/// <c>١٢3</c> — <b>يُرفض</b>. تطبيعُه إلى 123 يبدو لطفاً، وهو في الحقيقة إخفاءُ أن نصف
/// الرقم جاء من مصدر آخر. والقاعدة نفسها التي يطبّقها سطح HTTP على المال: <b>الرفض لا
/// التطبيع الصامت</b>.
/// </para>
/// <para>
/// وكذلك <b>فاصل الآلاف العربي</b> <c>U+066C</c> والفاصلة العشرية العربية <c>U+066B</c>:
/// كلاهما يُحوَّل صراحةً، لأن <c>decimal.TryParse</c> بثقافة ثابتة لا يعرفهما فيرفض رقماً
/// صحيحاً — أو أسوأ، يقرأ <c>١٬٥</c> رقماً آخر.
/// </para>
/// </summary>
public static class ArabicNumerals
{
    /// <summary>الفاصلة العشرية العربية <c>٫</c>.</summary>
    public const char ArabicDecimalSeparator = '٫';

    /// <summary>فاصل الآلاف العربي <c>٬</c>.</summary>
    public const char ArabicThousandsSeparator = '٬';

    /// <summary>العلامة العربية للنسبة المئوية <c>٪</c>.</summary>
    public const char ArabicPercentSign = '٪';

    private const int Latin = 0;
    private const int ArabicIndic = 0x0660;
    private const int ExtendedArabicIndic = 0x06F0;
    private const int Devanagari = 0x0966;

    private static readonly int[] Bases = [ArabicIndic, ExtendedArabicIndic, Devanagari];

    /// <summary>هل المحرف رقم في أي من الأنظمة الأربعة؟</summary>
    /// <param name="value">المحرف.</param>
    public static bool IsDigitInAnySystem(char value) => SystemOf(value) >= 0;

    /// <summary>
    /// نظام الرقم: <c>0</c> لاتيني، أو نقطة بداية النظام، أو <c>-1</c> إن لم يكن رقماً.
    /// </summary>
    /// <param name="value">المحرف.</param>
    public static int SystemOf(char value)
    {
        if (value is >= '0' and <= '9')
        {
            return Latin;
        }

        foreach (int start in Bases)
        {
            if (value >= start && value <= start + 9)
            {
                return start;
            }
        }

        return -1;
    }

    /// <summary>
    /// يطبّع كلمةً واحدة إلى أرقام لاتينية، <b>ويرفض خلط نظامين</b> داخلها.
    /// </summary>
    /// <param name="token">الكلمة كما وردت.</param>
    public static Result<string> NormaliseToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        StringBuilder output = new(token.Length);
        int seen = -1;

        foreach (char character in token)
        {
            switch (character)
            {
                case ArabicThousandsSeparator:
                case ',':
                case ' ':
                case '\u00A0':
                case '\u202F':
                case '_':
                    continue;

                case ArabicDecimalSeparator:
                    output.Append('.');
                    continue;
            }

            int system = SystemOf(character);
            if (system < 0)
            {
                output.Append(character);
                continue;
            }

            if (seen >= 0 && seen != system)
            {
                return Result<string>.Failure(VoiceErrors.MixedDigitSystems(token));
            }

            seen = system;
            output.Append(system == Latin ? character : (char)('0' + (character - system)));
        }

        return Result<string>.Success(output.ToString());
    }

    /// <summary>
    /// يطبّع نصّاً كاملاً كلمةً كلمة. <b>الفحص داخل الكلمة لا عبر النصّ</b>: جملةٌ تقول
    /// «رقم الفاتورة ١٢٣ والمبلغ 500» ليست خلطاً — كلّ عدد فيها من نظام واحد.
    /// </summary>
    /// <param name="text">النصّ.</param>
    public static Result<string> Normalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] tokens = text.Split(' ');
        List<Error> errors = [];

        for (int index = 0; index < tokens.Length; index++)
        {
            Result<string> normalised = NormaliseToken(tokens[index]);
            if (normalised.IsFailure)
            {
                errors.AddRange(normalised.Errors);
                continue;
            }

            tokens[index] = normalised.Value;
        }

        return errors.Count == 0
            ? Result<string>.Success(string.Join(' ', tokens))
            : Result<string>.Failure(errors);
    }
}
