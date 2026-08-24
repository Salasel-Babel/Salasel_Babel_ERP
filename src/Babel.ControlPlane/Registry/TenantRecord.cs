namespace Babel.ControlPlane.Registry;

public enum TenantStatus { Provisioning, Active, Suspended, Archived }

/// <summary>
/// نموذج العزل — <b>عمود لا افتراض</b>. ADR-0009 مفتوح عمداً؛ ولذلك يوجد
/// الحقل في السجل ويوجد مُحلّل توجيه يقرؤه، ولا توجد سلسلة اتصال واحدة
/// مطبوعة في الشيفرة.
/// </summary>
public enum IsolationModel { DatabasePerTenant, SharedSchema }

/// <summary>موقع البيانات: عندنا أم لدى العميل. ADR-0010: مؤجَّل لا مستحيل.</summary>
public enum Residency { Provider, Customer }

public sealed record TenantRecord(
    Guid TenantId,
    string TenantCode,
    string NameAr,
    string NameEn,
    TenantStatus Status,
    IsolationModel Isolation,
    Residency Residency,
    string Host,
    int Port,
    string DatabaseName,
    int SchemaVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ArchivedAt,
    string? ArchiveReason,
    string? ArchiveActor)
{
    public bool IsReachable => Status is TenantStatus.Active or TenantStatus.Suspended;
}

/// <summary>يُرفع حين يُطلب مستأجر مؤرشف من مسار التطبيق.</summary>
public sealed class TenantArchivedException(string tenantCode)
    : Exception($"المستأجر «{tenantCode}» مؤرشف: بياناته محفوظة، والوصول التطبيقي مقطوع.")
{
    public string TenantCode { get; } = tenantCode;
}

public sealed class TenantNotFoundException(string tenantCode)
    : Exception($"لا يوجد مستأجر بالرمز «{tenantCode}» في سجل مستوى التحكّم.");
