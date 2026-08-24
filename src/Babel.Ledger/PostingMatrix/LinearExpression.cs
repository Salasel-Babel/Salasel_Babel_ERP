using System.Globalization;

namespace Babel.Ledger.PostingMatrix;

/// <summary>
/// تعبير مبلغ السطر: <b>تركيبة خطية</b> من متغيرات <c>amounts</c> فقط.
/// <para>
/// القيود مفروضة عمداً (<c>data/posting-matrix/README.md</c>):
/// لا ضرب متغيرين ببعضهما، ولا قسمة على متغير، ولا نسبة ضريبة ولا نسبة محتجز
/// ولا حدّ مكتوب في التعبير — النسب في جداول إعدادات. حسابٌ يضرب مبلغين ليس
/// سطر ترحيل بل حسابٌ يخصّ المستند نفسه.
/// </para>
/// <para>
/// وما لا يُفهَم <b>يُرفض بصوت عالٍ</b>: تعبير خارج هذه القواعد يرمي، ولا يُقيَّم
/// إلى صفر. صفرٌ صامت في سطر قيد هو بالضبط «الرقم الخاطئ الصامت»
/// (<c>traps.md</c> §0).
/// </para>
/// <para>
/// كل الحساب <c>decimal</c>. لا <c>double</c> في أي موضع (Rule04).
/// </para>
/// </summary>
internal static class LinearExpression
{
    /// <summary>يقيّم التعبير على مفردات المبالغ المُسلَّمة.</summary>
    /// <param name="expression">التعبير كما هو في المصفوفة، مثل <c>net + tax</c>.</param>
    /// <param name="amounts">قيم المبالغ باسمها.</param>
    /// <param name="value">القيمة المحسوبة.</param>
    /// <param name="unknown">اسم أول متغيّر غير معروف، إن وُجد.</param>
    public static bool TryEvaluate(
        string expression,
        IReadOnlyDictionary<string, decimal> amounts,
        out decimal value,
        out string? unknown)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        value = 0m;
        unknown = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            unknown = "(تعبير فارغ)";
            return false;
        }

        decimal total = 0m;
        int sign = 1;
        int index = 0;
        bool expectTerm = true;

        while (index < expression.Length)
        {
            char c = expression[index];

            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            if (c is '+' or '-')
            {
                if (expectTerm && total == 0m && index == 0)
                {
                    sign = c == '-' ? -1 : 1;
                }
                else
                {
                    sign = c == '-' ? -1 : 1;
                }

                index++;
                expectTerm = true;
                continue;
            }

            if (!expectTerm)
            {
                unknown = expression;
                return false;
            }

            int start = index;
            while (index < expression.Length && expression[index] is not '+' and not '-')
            {
                index++;
            }

            string term = expression[start..index].Trim();
            if (!TryTerm(term, amounts, out decimal termValue, out unknown))
            {
                return false;
            }

            total += sign * termValue;
            expectTerm = false;
        }

        if (expectTerm)
        {
            unknown = expression;
            return false;
        }

        value = total;
        return true;
    }

    /// <summary>أسماء المتغيرات التي يقرؤها التعبير.</summary>
    public static IReadOnlyList<string> Variables(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        List<string> names = [];
        foreach (string term in expression.Split(['+', '-'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string head = term.Split(['*', '/'], StringSplitOptions.TrimEntries)[0];
            if (head.Length > 0 && !char.IsAsciiDigit(head[0]))
            {
                names.Add(head);
            }
        }

        return names;
    }

    private static bool TryTerm(
        string term,
        IReadOnlyDictionary<string, decimal> amounts,
        out decimal value,
        out string? unknown)
    {
        value = 0m;
        unknown = null;

        // ثابت عددي مجرّد.
        if (decimal.TryParse(term, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal literal))
        {
            value = literal;
            return true;
        }

        // متغيّر × ثابت أو متغيّر ÷ ثابت — والضرب في متغيّر آخر ممنوع بنيوياً.
        int star = term.IndexOf('*', StringComparison.Ordinal);
        int slash = term.IndexOf('/', StringComparison.Ordinal);

        if (star < 0 && slash < 0)
        {
            if (amounts.TryGetValue(term, out decimal amount))
            {
                value = amount;
                return true;
            }

            unknown = term;
            return false;
        }

        if (star >= 0 && slash >= 0)
        {
            unknown = term;
            return false;
        }

        int split = star >= 0 ? star : slash;
        string left = term[..split].Trim();
        string right = term[(split + 1)..].Trim();

        bool leftIsVariable = amounts.TryGetValue(left, out decimal leftValue);
        bool rightIsLiteral = decimal.TryParse(right, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal rightValue);

        if (star >= 0)
        {
            if (leftIsVariable && rightIsLiteral)
            {
                value = leftValue * rightValue;
                return true;
            }

            if (decimal.TryParse(left, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal leftLiteral)
                && amounts.TryGetValue(right, out decimal rightVariable))
            {
                value = leftLiteral * rightVariable;
                return true;
            }

            unknown = term;
            return false;
        }

        if (leftIsVariable && rightIsLiteral && rightValue != 0m)
        {
            value = leftValue / rightValue;
            return true;
        }

        unknown = term;
        return false;
    }
}
