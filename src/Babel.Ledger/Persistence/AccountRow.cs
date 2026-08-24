namespace Babel.Ledger.Persistence;

/// <summary>صف الحساب في دليل الحسابات. <c>internal</c> — لا يعبر حدّ الدفتر.</summary>
internal sealed class AccountRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsPostable { get; set; }

    public bool IsActive { get; set; }
}
