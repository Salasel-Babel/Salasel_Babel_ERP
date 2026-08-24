namespace SalaselBabel.MatrixValidator.Rules;

public enum Severity { Error, Warning }

public sealed record Finding(
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

public sealed record RuleDescription(string Id, string TitleAr, string TitleEn);
