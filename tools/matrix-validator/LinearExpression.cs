using System.Globalization;

namespace SalaselBabel.MatrixValidator;

/// <summary>
/// A linear combination of named amount variables with decimal coefficients, plus a constant term.
/// Every posting-matrix amount is parsed into one of these so that "the lines balance by
/// construction" becomes an algebraic fact rather than an opinion: sum(debit) - sum(credit)
/// must reduce to the zero combination for every declared scenario.
///
/// تركيبة خطية من متغيرات المبالغ بمعاملات عشرية. توازن القيد يصبح حقيقة جبرية لا رأياً:
/// مجموع المدين ناقص مجموع الدائن يجب أن يؤول إلى الصفر في كل سيناريو معلن.
/// No float, no double — decimal only, per CONTRIBUTING §3.2.
/// </summary>
internal sealed class LinearExpression
{
    private readonly SortedDictionary<string, decimal> _terms = new(StringComparer.Ordinal);
    public decimal Constant { get; private set; }

    public IReadOnlyDictionary<string, decimal> Terms => _terms;

    public IEnumerable<string> Variables => _terms.Where(t => t.Value != 0m).Select(t => t.Key);

    public bool IsZero => Constant == 0m && _terms.Values.All(v => v == 0m);

    public static LinearExpression Zero() => new();

    public static LinearExpression Variable(string name)
    {
        var e = new LinearExpression();
        e._terms[name] = 1m;
        return e;
    }

    public static LinearExpression Number(decimal value) => new() { Constant = value };

    public LinearExpression Add(LinearExpression other)
    {
        var r = Clone();
        r.Constant += other.Constant;
        foreach (var (k, v) in other._terms)
            r._terms[k] = r._terms.TryGetValue(k, out var cur) ? cur + v : v;
        return r;
    }

    public LinearExpression Subtract(LinearExpression other) => Add(other.Scale(-1m));

    public LinearExpression Scale(decimal factor)
    {
        var r = new LinearExpression { Constant = Constant * factor };
        foreach (var (k, v) in _terms) r._terms[k] = v * factor;
        return r;
    }

    /// <summary>Replaces every occurrence of <paramref name="name"/> with <paramref name="replacement"/>.</summary>
    public LinearExpression Substitute(string name, LinearExpression replacement)
    {
        if (!_terms.TryGetValue(name, out var coeff) || coeff == 0m) return Clone();
        var r = Clone();
        r._terms.Remove(name);
        return r.Add(replacement.Scale(coeff));
    }

    public LinearExpression Clone()
    {
        var r = new LinearExpression { Constant = Constant };
        foreach (var (k, v) in _terms) r._terms[k] = v;
        return r;
    }

    public override string ToString()
    {
        var parts = _terms.Where(t => t.Value != 0m)
                          .Select(t => t.Value == 1m ? t.Key
                                     : t.Value == -1m ? "-" + t.Key
                                     : t.Value.ToString(CultureInfo.InvariantCulture) + "*" + t.Key)
                          .ToList();
        if (Constant != 0m || parts.Count == 0)
            parts.Add(Constant.ToString(CultureInfo.InvariantCulture));
        return string.Join(" + ", parts).Replace("+ -", "- ");
    }
}
