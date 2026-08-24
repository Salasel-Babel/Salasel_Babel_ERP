using System.Text;
using Babel.Canonicalization;

namespace Babel.Ledger.Posting;

/// <summary>
/// الشكل القانوني مقطوعاً في ثلاث قطع عند المواضع التي <b>لا يمكن معرفتها قبل أخذ
/// قفل العدّاد</b>: <c>chain_seq</c> و<c>prev_hash</c> و<c>entry_no</c>.
/// <para>
/// المشكلة الحقيقية: الترحيل يجب أن يكون <b>مكالمة خادم واحدة</b> (فارق 127× مقيس،
/// فخ-14)، ورقم القيد ورقم التسلسل لا يُعرفان إلا تحت القفل داخل الخادم. فإمّا أن
/// تُحسب البايتات في الخادم — وعندها يقترب من التجزئة مُنسِّق لا نعرف ثقافته —
/// وإمّا أن تُحسب في C# — وعندها تلزم رحلة ذهاب وإياب إضافية داخل القفل.
/// </para>
/// <para>
/// والمخرج: تُحسب البايتات <b>كاملة</b> في C# عبر المكتبة المختومة، ثم تُقطع عند
/// حدود تلك السطور الثلاثة بالضبط. الخادم يعيد تركيب السطور الثلاثة وحدها من
/// <c>bigint::text</c> و<c>encode(bytea,'hex')</c> — وكلاهما لا يعرف ثقافةً ولا
/// فاصل آلاف. أي أن **لا مُنسِّق واعياً باللغة يقترب من البايتات في أي طرف**
/// (SPEC §8.1).
/// </para>
/// <para>
/// والقطع تُشتقّ من <b>ناتج المُوحِّد نفسه</b> لا من قالب نصّي مكتوب بيد: تُحسب
/// بايتات كاملة بقيم نائبة، ثم يُمشى على الشكل السلكي بسابقات الأطوال وتُقصّ عند
/// حدود السطور. ولذلك يستحيل أن ينحرف القالب عن المواصفة دون أن ينحرف المُوحِّد
/// معه. واختبار التكافؤ البايتي يثبّت ذلك على عشرات التركيبات.
/// </para>
/// </summary>
internal sealed record CanonicalSplit
{
    private static readonly byte[] PlaceholderPreviousHash = new byte[32];

    private CanonicalSplit(byte[] prefix, byte[] head, byte[] tail)
    {
        Prefix = prefix;
        Head = head;
        Tail = tail;
    }

    /// <summary>‏<c>babel.canon/v1</c> وسطر <c>kind</c> — كل ما يسبق <c>chain_seq</c>.</summary>
    public byte[] Prefix { get; }

    /// <summary>ما بين سطر <c>prev_hash</c> وسطر <c>entry_no</c>.</summary>
    public byte[] Head { get; }

    /// <summary>ما بعد سطر <c>entry_no</c> حتى سطر <c>end</c> ضمناً.</summary>
    public byte[] Tail { get; }

    /// <summary>يقطع مستنداً عند المواضع الثلاثة.</summary>
    public static CanonicalSplit Of(CanonicalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // قيم نائبة: المواضع وحدها هي المطلوبة، لا القيم.
        ChainLink placeholder = Canonicalizer.Compute(document, 1L, PlaceholderPreviousHash);
        byte[] bytes = placeholder.CanonicalBytes;

        (int Start, int End) chainSeq = LineSpan(bytes, "chain_seq");
        (int Start, int End) previousHash = LineSpan(bytes, "prev_hash");
        (int Start, int End) entryNo = LineSpan(bytes, "entry_no");

        if (chainSeq.End != previousHash.Start)
        {
            throw new InvalidOperationException(
                "‏prev_hash لا يلي chain_seq مباشرة في الشكل السلكي — المواصفة تغيّرت والقطع لم يتغيّر معها.");
        }

        return new CanonicalSplit(
            bytes[..chainSeq.Start],
            bytes[previousHash.End..entryNo.Start],
            bytes[entryNo.End..]);
    }

    /// <summary>
    /// يعيد تركيب البايتات كما يفعل <c>ledger.post_entry</c> — للاختبار وحده.
    /// وجود هذا التركيب هنا هو ما يجعل اختبار التكافؤ البايتي ممكناً أصلاً.
    /// </summary>
    public byte[] Reassemble(long chainSequence, byte[] previousHash, long entryNumber)
    {
        ArgumentNullException.ThrowIfNull(previousHash);

        string sequence = chainSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string number = entryNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string hex = Convert.ToHexString(previousHash).ToLowerInvariant();

        UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        byte[] middle = utf8.GetBytes(
            $"chain_seq\tI\t{sequence.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{sequence}\n"
            + $"prev_hash\tB\t64\t{hex}\n");
        byte[] numberLine = utf8.GetBytes(
            $"entry_no\tI\t{number.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{number}\n");

        byte[] result = new byte[Prefix.Length + middle.Length + Head.Length + numberLine.Length + Tail.Length];
        int at = 0;
        Prefix.CopyTo(result, at); at += Prefix.Length;
        middle.CopyTo(result, at); at += middle.Length;
        Head.CopyTo(result, at); at += Head.Length;
        numberLine.CopyTo(result, at); at += numberLine.Length;
        Tail.CopyTo(result, at);
        return result;
    }

    /// <summary>
    /// موضع سطر حقل بالاسم، بالمشي على الشكل السلكي <b>بسابقات الأطوال</b>.
    /// المشي بالبحث عن <c>\n</c> خاطئ: بيان متعدّد الأسطر يحمل <c>LF</c> داخل حمولته،
    /// وهو مسموح في المواصفة (SPEC §5.4) — وسابقة الطول هي بالضبط ما يجعل الحدود
    /// غير قابلة للتزوير.
    /// </summary>
    private static (int Start, int End) LineSpan(byte[] bytes, string field)
    {
        int at = IndexOfNewline(bytes, 0) + 1; // تخطّي ترويسة babel.canon/v1

        while (at < bytes.Length)
        {
            int start = at;
            int firstTab = IndexOfTab(bytes, at);
            string name = Encoding.UTF8.GetString(bytes, at, firstTab - at);

            int secondTab = IndexOfTab(bytes, firstTab + 1);
            int thirdTab = IndexOfTab(bytes, secondTab + 1);
            int length = int.Parse(
                Encoding.ASCII.GetString(bytes, secondTab + 1, thirdTab - secondTab - 1),
                System.Globalization.CultureInfo.InvariantCulture);

            int end = thirdTab + 1 + length + 1; // الحمولة ثم سطر جديد

            if (string.Equals(name, field, StringComparison.Ordinal))
            {
                return (start, end);
            }

            at = end;
        }

        throw new InvalidOperationException($"لا سطر باسم «{field}» في البايتات القانونية.");
    }

    private static int IndexOfTab(byte[] bytes, int from)
    {
        int at = Array.IndexOf(bytes, (byte)'\t', from);
        return at < 0 ? throw new InvalidOperationException("الشكل السلكي بلا فاصل جدولة.") : at;
    }

    private static int IndexOfNewline(byte[] bytes, int from)
    {
        int at = Array.IndexOf(bytes, (byte)'\n', from);
        return at < 0 ? throw new InvalidOperationException("الشكل السلكي بلا نهاية سطر.") : at;
    }
}
