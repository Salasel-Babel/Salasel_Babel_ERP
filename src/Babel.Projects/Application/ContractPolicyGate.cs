using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// <b>بوّابة البنود المعلَّقة — الموضع الوحيد الذي يُسأل فيه «هل حُسم ما يلزم لهذا العقد؟».</b>
/// <para>
/// وهي موضعٌ واحد لا فحصٌ مكرَّر في كل مسار: فحصٌ يؤدّيه مستدعٍ ويُنسى في الثاني هو
/// شكل العطل الذي يتكرّر في هذا المستودع. فمن يضيف مساراً مالياً جديداً يستشير هذه
/// البوّابة، ومن يضيف بنداً معلَّقاً خامساً يضيفه إلى <see cref="PendingPolicyItems"/>
/// فيدخل الرفض تلقائياً في كل مسارٍ يستشيرها.
/// </para>
/// <para>
/// <b>وهي ترفض اليوم في الحالتين، ولكلٍّ رمزها:</b> عقدٌ ينقصه صفٌّ معتمد لبندٍ مطلوب
/// يُرفض بـ<c>projects.contract_policy.pending</c> مسمّياً البنود؛ وعقدٌ اكتملت صفوفه
/// يُرفض بـ<c>projects.contract_policy.resolution_not_implemented</c> لأن <b>الحاسب
/// يتبع القرار ولا يسبقه</b>. والجدول يُبنى فارغاً ولا باب على السطح يكتب فيه.
/// </para>
/// </summary>
internal static class ContractPolicyGate
{
    /// <summary>
    /// البنود المعلَّقة على عقد — بترتيبها الثابت، فارغةً إن اكتملت صفوفه المعتمدة.
    /// </summary>
    /// <param name="database">جداول الوحدة.</param>
    /// <param name="tenantId">المستأجر.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<IReadOnlyList<PendingPolicyItem>> PendingAsync(
        ProjectsDbContext database,
        Guid tenantId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        List<string> approved = await database.ContractPolicies
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.ContractId == contractId)
            .Select(row => row.ItemCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> settled = new(approved, StringComparer.Ordinal);

        return [.. PendingPolicyItems.All.Where(item => !settled.Contains(item.Code))];
    }

    /// <summary>
    /// يمنع ترحيل مستخلص عقدٍ لم تُحسم بنوده — <b>ويُسمّي ما نقص</b>.
    /// </summary>
    /// <param name="database">جداول الوحدة.</param>
    /// <param name="tenantId">المستأجر.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Result> EnsureSettledAsync(
        ProjectsDbContext database,
        Guid tenantId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        List<ContractPolicyRow> rows = await database.ContractPolicies
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.ContractId == contractId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, ContractPolicyRow> byItem = rows
            .ToDictionary(static row => row.ItemCode, StringComparer.Ordinal);

        List<PendingPolicyItem> missing =
            [.. PendingPolicyItems.All.Where(item => !byItem.ContainsKey(item.Code))];

        if (missing.Count > 0)
        {
            return Result.Failure(ProjectsErrors.ContractPolicyPending(contractId, missing));
        }

        // اكتملت الصفوف المعتمدة — ويبقى الحجب الثاني. ونصّ القرار مبهمٌ على الشيفرة
        // عمداً: من يكتبه محاسب، ومن يبني حاسبه مهندس بتوقيع ذلك المحاسب. والبند
        // المُبلَّغ هو **الأول بترتيب القائمة الثابت** لا أوّل صفٍّ اتّفق، كي تكون
        // الرسالة نفسها في كل تشغيل.
        PendingPolicyItem first = PendingPolicyItems.All[0];
        ContractPolicyRow resolved = byItem[first.Code];

        return Result.Failure(
            ProjectsErrors.PolicyResolutionNotImplemented(resolved.ItemCode, resolved.Resolution));
    }
}
