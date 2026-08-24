using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Babel.Compliance.Store;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// دفتر تغيّرات الالتزام: كل انتقال حالة يُكتب صفاً <b>يُضاف إليه فقط</b>، بفاعل وسبب
/// بالعربية والإنجليزية ورقم المحاولة. لا يوجد مسار يغيّر حالة مستند دون المرور من هنا.
/// <para/>
/// «لا تُبتلع أخطاء الهيئة بصمت في سجل فني» (04-zatca §7) يبدأ من هنا:
/// السبب مكتوب بلغة يقرؤها المحاسب، لا برمز HTTP.
/// </summary>
internal static class ComplianceJournal
{
    public static async Task TransitionAsync(
        IComplianceUnitOfWork uow,
        ComplianceRecord record,
        ComplianceStatus to,
        string actor,
        string reasonAr,
        string reasonEn,
        DateTimeOffset at,
        AttemptId? attempt,
        CancellationToken ct)
    {
        var from = record.Status;
        ComplianceStatusMachine.EnsureTransition(from, to);

        var existing = await uow.TransitionsAsync(record.DocumentId, ct);
        record.Status = to;
        if (ComplianceStatusMachine.IsSettled(to)) record.SettledAt = at;
        if (to == ComplianceStatus.Queued && record.QueuedAt is null) record.QueuedAt = at;

        await uow.UpdateAsync(record, ct);
        await uow.AppendTransitionAsync(new StatusTransition
        {
            TransitionId = Guid.CreateVersion7(),
            DocumentId = record.DocumentId,
            Seq = existing.Count + 1,
            From = from,
            To = to,
            At = at,
            Actor = actor,
            ReasonAr = reasonAr,
            ReasonEn = reasonEn,
            Attempt = attempt
        }, ct);
    }
}
