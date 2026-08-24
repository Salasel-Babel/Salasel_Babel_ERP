using System.Globalization;
using System.Text;

namespace SalaselBabel.MatrixValidator;

/// <summary>
/// Parses a posting-matrix amount expression into a <see cref="LinearExpression"/>.
///
/// Grammar (deliberately tiny — an amount rule a reviewer cannot read is a defect):
///   expr    := term (('+' | '-') term)*
///   term    := factor (('*' | '/') factor)*      — multiplication/division by CONSTANTS only
///   factor  := '-'? ( number | identifier | '(' expr ')' )
///
/// Non-linear products (variable * variable) are rejected: an amount that multiplies two
/// document amounts together is never a posting line, it is a computation that belongs in
/// the document.
/// قواعد صغيرة عمداً: قاعدة مبلغ لا يستطيع المراجع قراءتها عيبٌ في ذاتها.
/// </summary>
public static class ExpressionParser
{
    public static LinearExpression Parse(string input)
    {
        var p = new Parser(input);
        var e = p.ParseExpression();
        p.SkipWhitespace();
        if (!p.AtEnd) throw new ExpressionException($"unexpected '{p.Rest}' in expression \"{input}\"");
        return e;
    }

    private sealed class Parser(string s)
    {
        private int _i;

        public bool AtEnd => _i >= s.Length;
        public string Rest => s[Math.Min(_i, s.Length)..];

        public void SkipWhitespace() { while (_i < s.Length && char.IsWhiteSpace(s[_i])) _i++; }

        public LinearExpression ParseExpression()
        {
            var left = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) return left;
                var op = s[_i];
                if (op != '+' && op != '-') return left;
                _i++;
                var right = ParseTerm();
                left = op == '+' ? left.Add(right) : left.Subtract(right);
            }
        }

        private LinearExpression ParseTerm()
        {
            var left = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) return left;
                var op = s[_i];
                if (op != '*' && op != '/') return left;
                _i++;
                var right = ParseFactor();
                var leftIsConst = left.Terms.Values.All(v => v == 0m);
                var rightIsConst = right.Terms.Values.All(v => v == 0m);
                if (op == '*')
                {
                    if (rightIsConst) left = left.Scale(right.Constant);
                    else if (leftIsConst) left = right.Scale(left.Constant);
                    else throw new ExpressionException(
                        $"a posting amount may not multiply two variables together in \"{s}\"");
                }
                else
                {
                    if (!rightIsConst) throw new ExpressionException(
                        $"a posting amount may only be divided by a constant in \"{s}\"");
                    if (right.Constant == 0m) throw new ExpressionException($"division by zero in \"{s}\"");
                    left = left.Scale(1m / right.Constant);
                }
            }
        }

        private LinearExpression ParseFactor()
        {
            SkipWhitespace();
            if (AtEnd) throw new ExpressionException($"expression ended unexpectedly: \"{s}\"");

            if (s[_i] == '-') { _i++; return ParseFactor().Scale(-1m); }
            if (s[_i] == '+') { _i++; return ParseFactor(); }

            if (s[_i] == '(')
            {
                _i++;
                var inner = ParseExpression();
                SkipWhitespace();
                if (AtEnd || s[_i] != ')') throw new ExpressionException($"missing ')' in \"{s}\"");
                _i++;
                return inner;
            }

            if (char.IsDigit(s[_i]) || s[_i] == '.')
            {
                var start = _i;
                while (_i < s.Length && (char.IsDigit(s[_i]) || s[_i] == '.')) _i++;
                var lit = s[start.._i];
                if (!decimal.TryParse(lit, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    throw new ExpressionException($"'{lit}' is not a decimal literal in \"{s}\"");
                return LinearExpression.Number(d);
            }

            if (char.IsLetter(s[_i]) || s[_i] == '_')
            {
                var sb = new StringBuilder();
                while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] == '_')) sb.Append(s[_i++]);
                return LinearExpression.Variable(sb.ToString());
            }

            throw new ExpressionException($"unexpected character '{s[_i]}' in \"{s}\"");
        }
    }
}

public sealed class ExpressionException(string message) : Exception(message);
