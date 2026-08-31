namespace SalaselBabel.MatrixValidator.Rules;

internal enum Severity { Error, Warning }

internal sealed record Finding(
    string RuleId,
    Severity Severity,
    string Where,
    string MessageAr,
    string MessageEn)
{
    public override string ToString() =>
        $"[{RuleId}] {(Severity == Severity.Error ? "ERROR" : "WARN ")} {Where}\n"
      + $"        {MessageEn}\n"
      + $"        {MessageAr}";
}

internal sealed record RuleDescription(string Id, string TitleAr, string TitleEn);
