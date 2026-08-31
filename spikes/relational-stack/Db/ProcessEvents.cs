using System.Text.Json;
using System.Text.Json.Serialization;

namespace BabelRelationalSpike.Db;

// ---------------------------------------------------------------------------
// The "process narrative": drafts, approvals, POS shifts, ZATCA submissions and
// their retries, lease lifecycle, and AI suggestions both ACCEPTED and REJECTED.
// سرد مراحل العمليات: المسودات والاعتمادات ومناوبات نقاط البيع وإرسال فواتير
// هيئة الزكاة والضريبة والجمارك ودورة حياة العقود واقتراحات الذكاء الاصطناعي.
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DraftSaved), "DraftSaved")]
[JsonDerivedType(typeof(ApprovalRequested), "ApprovalRequested")]
[JsonDerivedType(typeof(ApprovalGranted), "ApprovalGranted")]
[JsonDerivedType(typeof(ApprovalRejected), "ApprovalRejected")]
[JsonDerivedType(typeof(ShiftOpened), "ShiftOpened")]
[JsonDerivedType(typeof(ShiftClosed), "ShiftClosed")]
[JsonDerivedType(typeof(ZatcaSubmitted), "ZatcaSubmitted")]
[JsonDerivedType(typeof(ZatcaRejectedByPortal), "ZatcaRejectedByPortal")]
[JsonDerivedType(typeof(ZatcaRetryScheduled), "ZatcaRetryScheduled")]
[JsonDerivedType(typeof(ZatcaCleared), "ZatcaCleared")]
[JsonDerivedType(typeof(LeaseSigned), "LeaseSigned")]
[JsonDerivedType(typeof(LeaseTerminated), "LeaseTerminated")]
[JsonDerivedType(typeof(AiSuggestionOffered), "AiSuggestionOffered")]
[JsonDerivedType(typeof(AiSuggestionAccepted), "AiSuggestionAccepted")]
[JsonDerivedType(typeof(AiSuggestionRejected), "AiSuggestionRejected")]
public abstract record ProcessPayload
{
    /// <summary>Denormalised status so a GIN containment query can find it cheaply.</summary>
    public string Status { get; init; } = "";
}

public record DraftSaved(string DocumentNo, decimal Total) : ProcessPayload;
public record ApprovalRequested(string DocumentNo, string RequestedFrom) : ProcessPayload;
public record ApprovalGranted(string DocumentNo, string Approver, string NoteAr) : ProcessPayload;
public record ApprovalRejected(string DocumentNo, string Approver, string ReasonAr) : ProcessPayload;

public record ShiftOpened(string Terminal, string Cashier, decimal OpeningFloat) : ProcessPayload;
public record ShiftClosed(string Terminal, decimal CountedCash, decimal Variance) : ProcessPayload;

public record ZatcaSubmitted(string InvoiceUuid, string Hash, int Attempt) : ProcessPayload;
public record ZatcaRejectedByPortal(string InvoiceUuid, string ErrorCode, string MessageAr, int Attempt) : ProcessPayload;
public record ZatcaRetryScheduled(string InvoiceUuid, DateTime NextAttemptUtc, int Attempt) : ProcessPayload;
public record ZatcaCleared(string InvoiceUuid, string ClearanceUuid, int Attempt) : ProcessPayload;

public record LeaseSigned(string ContractNo, decimal AnnualRent, string TenantNameAr) : ProcessPayload;
public record LeaseTerminated(string ContractNo, string ReasonAr) : ProcessPayload;

public record AiSuggestionOffered(string SuggestionId, string Kind, string ProposedAccount, double Confidence) : ProcessPayload;
public record AiSuggestionAccepted(string SuggestionId, string AcceptedBy) : ProcessPayload;
public record AiSuggestionRejected(string SuggestionId, string RejectedBy, string ReasonAr) : ProcessPayload;

public static class PayloadJson
{
    /// <summary>
    /// AllowOutOfOrderMetadataProperties matters here: PostgreSQL's jsonb type
    /// re-orders object keys (shortest key first), so the "$type" discriminator
    /// almost never comes back first. Without this option System.Text.Json
    /// throws on read. Marten hides this for you; here you must know it.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        WriteIndented = false
    };

    public static string Write(ProcessPayload p) => JsonSerializer.Serialize(p, Options);
    public static ProcessPayload Read(string json) => JsonSerializer.Deserialize<ProcessPayload>(json, Options)!;
}
