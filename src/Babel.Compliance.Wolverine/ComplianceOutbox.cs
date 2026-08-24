using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Store;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Babel.Compliance.Wolverine;

/// <summary>
/// إشعار إيقاظ. <b>ليس أمراً بالإرسال</b> — الصف في جدول <c>work_item</c> هو المرجع
/// الوحيد لما يجوز إرساله، وهذا الظرف مجرد جرس.
/// </summary>
public sealed record ComplianceDocumentQueued(Guid DocumentId, string Tenant);

/// <summary>
/// <b>لماذا طابور خاص بالالتزام إلى جانب الصندوق الصادر لـWolverine؟</b>
/// <para/>
/// لأن الصندوق الصادر يضمن <b>تسليم الرسالة</b>، ونحن نحتاج ضمان <b>عدم تكرار الإرسال
/// إلى الجهة</b> — وهما شيئان مختلفان. الحالة التي يقرؤها حارس الحصانة (المحاولات،
/// والغموض، وعدد محاولات الحسم) يجب أن تكون في معاملة قاعدة البيانات نفسها التي
/// تكتب المستند، لا في ظرف رسالة.
/// <para/>
/// فالتقسيم:
/// <list type="bullet">
///   <item><b>جدول <c>work_item</c></b> — المرجع فيما يجوز إرساله ومتى. يُكتب داخل معاملة البناء.</item>
///   <item><b>ظرف Wolverine</b> — جرس إيقاظ يوزّع العمل على العُقد. تسليمه «مرة على الأقل»
///         <b>غير ضار</b>: كل إيقاظ يمرّ بالحارس أولاً، فالإيقاظ المكرّر لا يُنتج إرسالاً مكرّراً.</item>
/// </list>
/// وهذا هو بالضبط ما يجعل ضمان «مرة على الأقل» كافياً هنا رغم أن الإرسال ليس حصيناً.
/// </summary>
public sealed class ComplianceOutboxPublisher(IMessageBus bus)
{
    public Task WakeAsync(ComplianceRecord record) =>
        bus.PublishAsync(new ComplianceDocumentQueued(record.DocumentId.Value, record.Tenant.Value)).AsTask();
}

/// <summary>
/// معالج Wolverine. جسمه سطر واحد: يستدعي نفس <see cref="ReportingWorker"/> الذي
/// يستدعيه الاختبار. <b>لا منطق إرسال يعيش في المعالج</b> — وإلا صار للإرسال مساران.
/// </summary>
public sealed class ComplianceDocumentQueuedHandler(
    IComplianceStore store,
    ReportingWorker worker,
    ILogger<ComplianceDocumentQueuedHandler> logger)
{
    public async Task Handle(ComplianceDocumentQueued message, CancellationToken ct)
    {
        var id = new ComplianceDocumentId(message.DocumentId);

        var item = await store.InTransactionAsync(async (uow, token) =>
        {
            var due = await uow.DueWorkAsync(DateTimeOffset.UtcNow, 50, token);
            return due.FirstOrDefault(w => w.DocumentId == id);
        }, ct);

        if (item is null)
        {
            // إيقاظ مكرّر أو مبكر. هذا متوقَّع تحت «مرة على الأقل» وليس خطأ.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("إيقاظ بلا عمل مستحق للمستند {DocumentId} — متوقَّع تحت التسليم «مرة على الأقل»", id);
            }

            return;
        }

        await worker.ProcessAsync(item, ct);
    }
}
