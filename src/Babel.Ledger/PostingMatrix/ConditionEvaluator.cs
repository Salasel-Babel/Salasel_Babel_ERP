using System.Globalization;

namespace Babel.Ledger.PostingMatrix;

/// <summary>نتيجة تقييم شرط: قيمة، أو تعذّر مُعلَّل.</summary>
internal readonly record struct ConditionOutcome(bool Evaluated, bool Value, string? Reason)
{
    public static ConditionOutcome True => new(true, true, null);

    public static ConditionOutcome False => new(true, false, null);

    public static ConditionOutcome Of(bool value) => new(true, value, null);

    public static ConditionOutcome Undecidable(string reason) => new(false, false, reason);
}

/// <summary>
/// مقيّم شروط المصفوفة (<c>when</c> على السطر، و<c>condition</c> في قواعد الحجب).
/// <para>
/// <b>القاعدة الحاكمة:</b> شرط لا يمكن تقييمه <b>يوقف الترحيل</b> ولا يُعامل معاملة
/// «خطأ ⇒ السطر لا ينشأ». الفرق بين الاثنين هو الفرق بين قيدٍ مرفوض بصوت عالٍ
/// وقيدٍ ناقص سطر ضريبة يمرّ صامتاً (<c>traps.md</c> §0).
/// </para>
/// <para>
/// ما يُقيَّم هنا:
/// <list type="bullet">
///   <item><c>path == 'literal'</c> · <c>path != 'literal'</c> · <c>path == true|false</c></item>
///   <item>مقارنات عددية بين تعبيرين خطيّين: <c>&gt;</c> <c>&lt;</c> <c>&gt;=</c> <c>&lt;=</c></item>
///   <item>الربط بـ<c>and</c> و<c>or</c></item>
///   <item><c>always</c> — صحيح دائماً</item>
/// </list>
/// وما سوى ذلك (مثل <c>document.has_any_line_with(...)</c> أو <c>abs(...)</c>) <b>لا يُخمَّن</b>:
/// على الوحدة أن تُصرّح بنتيجته صراحةً كواقعة <c>condition.&lt;اسم الشرط&gt;</c>،
/// وإلا رُفض الطلب.
/// </para>
/// </summary>
internal static class ConditionEvaluator
{
    /// <summary>يقيّم شرطاً مُسمّى: الواقعة الصريحة أولاً، ثم التعبير.</summary>
    public static ConditionOutcome Evaluate(
        string conditionName,
        string expression,
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyDictionary<string, decimal> amounts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        // الواقعة الصريحة تسبق التعبير دائماً: هي مخرج الوحدة حين يكون الشرط
        // منطقاً في المستند لا مقارنةً على سياق.
        if (!string.IsNullOrEmpty(conditionName)
            && facts.TryGetValue("condition." + conditionName, out string? declared))
        {
            return ParseBoolean(declared) is { } value
                ? ConditionOutcome.Of(value)
                : ConditionOutcome.Undecidable(
                    $"الواقعة condition.{conditionName} قيمتها «{declared}» وليست true أو false.");
        }

        return EvaluateExpression(expression, facts, amounts);
    }

    /// <summary>يقيّم تعبيراً مباشرة (قواعد الحجب لا تحمل اسم شرط).</summary>
    public static ConditionOutcome EvaluateExpression(
        string expression,
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyDictionary<string, decimal> amounts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(amounts);

        string text = (expression ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return ConditionOutcome.True;
        }

        if (string.Equals(text, "always", StringComparison.Ordinal))
        {
            return ConditionOutcome.True;
        }

        int or = FindOperator(text, " or ");
        if (or >= 0)
        {
            ConditionOutcome left = EvaluateExpression(text[..or], facts, amounts);
            ConditionOutcome right = EvaluateExpression(text[(or + 4)..], facts, amounts);
            if (!left.Evaluated)
            {
                return left;
            }

            return !right.Evaluated ? right : ConditionOutcome.Of(left.Value || right.Value);
        }

        int and = FindOperator(text, " and ");
        if (and >= 0)
        {
            ConditionOutcome left = EvaluateExpression(text[..and], facts, amounts);
            ConditionOutcome right = EvaluateExpression(text[(and + 5)..], facts, amounts);
            if (!left.Evaluated)
            {
                return left;
            }

            return !right.Evaluated ? right : ConditionOutcome.Of(left.Value && right.Value);
        }

        // دالة داخل التعبير ⇒ خارج ما تعرفه هذه الطبقة. لا تخمين.
        if (text.Contains('(', StringComparison.Ordinal))
        {
            return ConditionOutcome.Undecidable(
                $"التعبير «{text}» يحوي استدعاء دالة لا يقيّمه المحرك. "
                + "على الوحدة أن تُصرّح بنتيجته واقعةً باسم condition.<اسم الشرط>.");
        }

        foreach ((string token, int width) in Comparators)
        {
            int at = text.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            // ‏>= و<= يجب أن يُلتقطا قبل > و< — والترتيب في Comparators يضمن ذلك.
            string left = text[..at].Trim();
            string right = text[(at + width)..].Trim();
            return Compare(token, left, right, facts, amounts);
        }

        return ConditionOutcome.Undecidable($"التعبير «{text}» ليس مقارنة يعرفها المحرك.");
    }

    private static readonly (string Token, int Width)[] Comparators =
    [
        ("==", 2), ("!=", 2), (">=", 2), ("<=", 2), (">", 1), ("<", 1),
    ];

    private static ConditionOutcome Compare(
        string comparator,
        string left,
        string right,
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyDictionary<string, decimal> amounts)
    {
        if (comparator is "==" or "!=")
        {
            string? literal = Literal(right);
            if (literal is not null)
            {
                if (!TryFact(left, facts, out string? actual))
                {
                    return ConditionOutcome.Undecidable(
                        $"الواقعة «{Path(left)}» غير مُسلَّمة، والشرط يقارنها بـ«{literal}». "
                        + "الدفتر لا يقرأ جداول الوحدات (القاعدة 5)، فالوحدة هي التي تُصرّح بوقائع حدثها.");
                }

                bool equal = string.Equals(actual, literal, StringComparison.Ordinal);
                return ConditionOutcome.Of(comparator == "==" ? equal : !equal);
            }
        }

        // مقارنة عددية بين تعبيرين خطيّين على المبالغ أو على وقائع عددية.
        if (!TryNumeric(left, facts, amounts, out decimal leftValue, out string? leftReason))
        {
            return ConditionOutcome.Undecidable(leftReason!);
        }

        if (!TryNumeric(right, facts, amounts, out decimal rightValue, out string? rightReason))
        {
            return ConditionOutcome.Undecidable(rightReason!);
        }

        return comparator switch
        {
            "==" => ConditionOutcome.Of(leftValue == rightValue),
            "!=" => ConditionOutcome.Of(leftValue != rightValue),
            ">=" => ConditionOutcome.Of(leftValue >= rightValue),
            "<=" => ConditionOutcome.Of(leftValue <= rightValue),
            ">" => ConditionOutcome.Of(leftValue > rightValue),
            "<" => ConditionOutcome.Of(leftValue < rightValue),
            _ => ConditionOutcome.Undecidable($"مقارنة غير معروفة: {comparator}"),
        };
    }

    private static bool TryNumeric(
        string expression,
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyDictionary<string, decimal> amounts,
        out decimal value,
        out string? reason)
    {
        reason = null;

        Dictionary<string, decimal> scope = new(amounts, StringComparer.Ordinal);
        foreach (string name in LinearExpression.Variables(expression))
        {
            if (scope.ContainsKey(name))
            {
                continue;
            }

            if (TryFact(name, facts, out string? raw)
                && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
            {
                scope[name] = parsed;
            }
        }

        if (LinearExpression.TryEvaluate(expression, scope, out value, out string? unknown))
        {
            return true;
        }

        reason = $"تعذّر تقييم «{expression}»: المتغيّر «{unknown}» ليس مبلغاً معرَّفاً ولا واقعة عددية مُسلَّمة.";
        return false;
    }

    private static bool TryFact(string path, IReadOnlyDictionary<string, string> facts, out string? value)
    {
        string key = Path(path);
        if (facts.TryGetValue(key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>يُسقط بادئة <c>context.</c> التي تكتبها قواعد الحجب.</summary>
    private static string Path(string path)
    {
        string trimmed = path.Trim();
        return trimmed.StartsWith("context.", StringComparison.Ordinal) ? trimmed[8..] : trimmed;
    }

    private static string? Literal(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            return trimmed[1..^1];
        }

        return trimmed is "true" or "false" ? trimmed : null;
    }

    private static bool? ParseBoolean(string text) => text switch
    {
        "true" or "True" or "TRUE" or "1" or "نعم" => true,
        "false" or "False" or "FALSE" or "0" or "لا" => false,
        _ => null,
    };

    private static int FindOperator(string text, string token)
    {
        int at = text.IndexOf(token, StringComparison.Ordinal);
        return at;
    }
}
