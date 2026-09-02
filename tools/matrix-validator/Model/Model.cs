using System.Text.Json.Serialization;

namespace SalaselBabel.MatrixValidator.Model;

// ---------------------------------------------------------------------------
// Chart of accounts (CSV)
// ---------------------------------------------------------------------------

internal sealed class Account
{
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string ParentCode { get; init; } = string.Empty;
    public int Level { get; init; }
    public required string AccountType { get; init; }
    public required string NaturalSide { get; init; }
    public bool IsPostable { get; init; }
    public bool IsContra { get; init; }
    public string StatementSection { get; init; } = string.Empty;
    public string SubledgerType { get; init; } = "none";
    public IReadOnlyList<string> RequiredDimensions { get; init; } = Array.Empty<string>();
    public string CurrencyMode { get; init; } = "any";
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsProtected { get; init; }
    public required string Status { get; init; }
    public string SourceRef { get; init; } = string.Empty;
    public string CaveatAr { get; init; } = string.Empty;
    public string CaveatEn { get; init; } = string.Empty;
    public int SourceLine { get; init; }
}

internal sealed class Dimension
{
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public int SourceLine { get; init; }
}

internal sealed class SubledgerType
{
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public int SourceLine { get; init; }
}

internal sealed class AccountRole
{
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string ExpectedAccountType { get; init; } = string.Empty;
    public string ExpectedSide { get; init; } = string.Empty;
    public string Status { get; init; } = "drafted";
    public int SourceLine { get; init; }
}

internal sealed class RoleMapping
{
    public required string TenantId { get; init; }
    public required string RoleCode { get; init; }
    public required string Qualifier { get; init; }
    public required string AccountCode { get; init; }
    public string Status { get; init; } = "drafted";
    public int SourceLine { get; init; }
}

// ---------------------------------------------------------------------------
// Posting matrix (JSON)
// ---------------------------------------------------------------------------

internal sealed class EventFile
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "";
    [JsonPropertyName("module")] public string Module { get; set; } = "";
    [JsonPropertyName("source_refs")] public List<string> SourceRefs { get; set; } = new();
    [JsonPropertyName("events")] public List<PostingEvent> Events { get; set; } = new();
}

internal sealed class Bilingual
{
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";

    /// <summary>شرحٌ اختياري: <c>null</c> يعني «لم يُكتب»، و<c>""</c> يعني «كُتب فارغاً» وهو خطأ.</summary>
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
}

internal sealed class Caveat
{
    [JsonPropertyName("ref")] public string Ref { get; set; } = "";
    [JsonPropertyName("text_ar")] public string TextAr { get; set; } = "";
    [JsonPropertyName("text_en")] public string? TextEn { get; set; }
}

internal sealed class AmountVariable
{
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
    [JsonPropertyName("derivation_ar")] public string DerivationAr { get; set; } = "";
    [JsonPropertyName("derivation_en")] public string? DerivationEn { get; set; }
}

internal sealed class ConditionDef
{
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
    [JsonPropertyName("expression")] public string Expression { get; set; } = "";
}

internal sealed class Scenario
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
    [JsonPropertyName("true_conditions")] public List<string> TrueConditions { get; set; } = new();
    [JsonPropertyName("zero_amounts")] public List<string> ZeroAmounts { get; set; } = new();
    [JsonPropertyName("identities")] public Dictionary<string, string> Identities { get; set; } = new();
}

internal sealed class SweepSpec
{
    [JsonPropertyName("selector")] public string Selector { get; set; } = "";
    [JsonPropertyName("classes")] public List<string> Classes { get; set; } = new();
    [JsonPropertyName("postable_only")] public bool PostableOnly { get; set; }
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
}

internal sealed class PostingLine
{
    [JsonPropertyName("line_no")] public int LineNo { get; set; }
    [JsonPropertyName("line_kind")] public string LineKind { get; set; } = "role";
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("qualifier_source")] public string? QualifierSource { get; set; }
    [JsonPropertyName("side")] public string Side { get; set; } = "";
    [JsonPropertyName("amount")] public string Amount { get; set; } = "";
    [JsonPropertyName("dimensions")] public List<string> Dimensions { get; set; } = new();
    [JsonPropertyName("subledger")] public string? Subledger { get; set; }
    [JsonPropertyName("when")] public System.Text.Json.JsonElement? When { get; set; }
    [JsonPropertyName("sweep")] public SweepSpec? Sweep { get; set; }
    [JsonPropertyName("note_ar")] public string NoteAr { get; set; } = "";
    [JsonPropertyName("note_en")] public string? NoteEn { get; set; }

    public IReadOnlyList<string> WhenConditions()
    {
        if (When is not { } w) return Array.Empty<string>();
        return w.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => new[] { w.GetString()! },
            System.Text.Json.JsonValueKind.Array => w.EnumerateArray().Select(x => x.GetString()!).ToArray(),
            _ => Array.Empty<string>()
        };
    }
}

internal sealed class PostingEvent
{
    [JsonPropertyName("event_code")] public string EventCode { get; set; } = "";
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string? NameEn { get; set; }
    [JsonPropertyName("module")] public string Module { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("source_ref")] public string SourceRef { get; set; } = "";
    [JsonPropertyName("trigger")] public Bilingual? Trigger { get; set; }
    [JsonPropertyName("precondition")] public Bilingual? Precondition { get; set; }
    [JsonPropertyName("reversal")] public Bilingual? Reversal { get; set; }
    [JsonPropertyName("posts_entry")] public bool PostsEntry { get; set; } = true;
    [JsonPropertyName("caveats")] public List<Caveat> Caveats { get; set; } = new();
    [JsonPropertyName("amounts")] public Dictionary<string, AmountVariable> Amounts { get; set; } = new();
    [JsonPropertyName("identities")] public Dictionary<string, string> Identities { get; set; } = new();
    [JsonPropertyName("conditions")] public Dictionary<string, ConditionDef> Conditions { get; set; } = new();
    [JsonPropertyName("scenarios")] public List<Scenario> Scenarios { get; set; } = new();
    [JsonPropertyName("lines")] public List<PostingLine> Lines { get; set; } = new();

    [JsonIgnore] public string SourceFile { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Guard rules (JSON)
// ---------------------------------------------------------------------------

internal sealed class GuardRuleFile
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "";
    [JsonPropertyName("rules")] public List<GuardRule> Rules { get; set; } = new();
}

internal sealed class GuardRuleTarget
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("property")] public string? Property { get; set; }
}

internal sealed class GuardRule
{
    [JsonPropertyName("rule_id")] public string RuleId { get; set; } = "";
    [JsonPropertyName("name_ar")] public string NameAr { get; set; } = "";
    [JsonPropertyName("name_en")] public string NameEn { get; set; } = "";
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("applies_to")] public GuardRuleTarget? AppliesTo { get; set; }
    [JsonPropertyName("condition")] public string Condition { get; set; } = "";
    [JsonPropertyName("message_ar")] public string MessageAr { get; set; } = "";
    [JsonPropertyName("message_en")] public string MessageEn { get; set; } = "";
}
