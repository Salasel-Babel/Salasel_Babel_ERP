namespace Babel.Contracts.Posting;

/// <summary>
/// النطاق التحليلي للسطر: الفرع والمشروع ومركز التكلفة.
/// معرّفات مبهمة عمداً — Babel.Ledger يتحقق منها، والوحدة لا تعرف شجرتها.
/// </summary>
/// <param name="BranchId">معرّف الفرع، أو <c>null</c>.</param>
/// <param name="CostCenterId">معرّف مركز التكلفة، أو <c>null</c>.</param>
/// <param name="ProjectId">معرّف المشروع، أو <c>null</c>.</param>
public readonly record struct PostingScope(string? BranchId, string? CostCenterId, string? ProjectId)
{
    /// <summary>لا نطاق تحليلي.</summary>
    public static PostingScope None => default;
}
