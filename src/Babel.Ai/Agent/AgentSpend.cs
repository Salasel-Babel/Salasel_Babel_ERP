using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// إعداد الإنفاق لمنشأةٍ بعينها.
/// <para>
/// <b>ولماذا حقلان لا واحد:</b> المالك يشغّل النظام على مفتاحه افتراضياً، والمنشأة قد
/// تأتي بمفتاحها. فمن جاء بمفتاحه يُقاس إنفاقه ولا يُسقَف بسقف المالك — لأنه يدفعه.
/// ومن يعمل على مفتاح المالك يُسقَف. <b>ولا يُخمَّن أيّهما</b>: الغياب يعني «مفتاح المالك
/// وسقفه»، وهو <b>معلَن</b> في الافتراضي لا مستنتَج.
/// </para>
/// </summary>
/// <param name="ApiKeyVariable">
/// <b>اسم</b> متغيّر البيئة الحامل لمفتاح المنشأة، أو <c>null</c> فمفتاح المالك.
/// ولا يحمل هذا الحقل مفتاحاً أبداً — نفس قاعدة <c>GitHubModelsOptions.TokenVariable</c>.
/// </param>
/// <param name="TokenCeiling">سقف الرموز في النافذة، أو <c>null</c> فسقف المالك الافتراضي.</param>
public sealed record AgentTenantBilling(string? ApiKeyVariable, long? TokenCeiling)
{
    /// <summary>الافتراضي: مفتاح المالك وسقفه.</summary>
    public static AgentTenantBilling OwnerKey { get; } = new(null, null);

    /// <summary>هل تعمل هذه المنشأة على مفتاحها؟</summary>
    public bool BringsItsOwnKey => ApiKeyVariable is not null;
}

/// <summary>
/// من أين يُعرَف إعداد إنفاق المنشأة. <b>منفذ</b>: مصدره مستوى التحكّم أو الإعداد،
/// ولا تعرفه هذه الوحدة.
/// </summary>
public interface IAgentTenantBillingSource
{
    /// <summary>يقرأ إعداد المنشأة.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<AgentTenantBilling> ReadAsync(TenantId tenant, CancellationToken cancellationToken);
}

/// <summary>المُعطى الافتراضي: الكلّ على مفتاح المالك وسقفه.</summary>
public sealed class OwnerKeyBillingSource : IAgentTenantBillingSource
{
    /// <inheritdoc />
    public Task<AgentTenantBilling> ReadAsync(TenantId tenant, CancellationToken cancellationToken) =>
        Task.FromResult(AgentTenantBilling.OwnerKey);
}

/// <summary>إنفاق منشأةٍ في نافذةٍ واحدة.</summary>
/// <param name="Tenant">المنشأة.</param>
/// <param name="WindowStartedAt">بداية النافذة.</param>
/// <param name="Usage">المجموع.</param>
/// <param name="Turns">عدد الأدوار المُحاسَبة.</param>
public sealed record AgentTenantSpend(
    TenantId Tenant,
    DateTimeOffset WindowStartedAt,
    AgentModelUsage Usage,
    int Turns);

/// <summary>
/// <b>دفتر الإنفاق — يُقاس لكل منشأة على حدة، ويُرفض عند السقف بجملةٍ مسمّاة.</b>
/// <para>
/// <b>والوحدة رموزٌ لا ريالات</b>، وذلك قرارٌ لا كسل: الرمز واقعةٌ يُعيدها المزوّد في
/// <c>usage</c> ونحن نقيسها؛ والريال يحتاج جدول أسعارٍ ليس في هذا المستودع، وسعرٌ
/// يُكتب في الشيفرة يتجمّد بينما يتحرّك عند المزوّد. و<c>AgentErrors.PriceListMissing</c>
/// تقول ذلك صراحةً بدل أن يُخترع رقم.
/// </para>
/// </summary>
public interface IAgentSpendLedger
{
    /// <summary>يأذن بالدور أو يرفض عند السقف.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="ceiling">السقف بالرموز، أو <c>null</c> فلا سقف (منشأةٌ بمفتاحها).</param>
    /// <param name="window">نافذة المحاسبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<Result> AdmitAsync(TenantId tenant, long? ceiling, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>يسجّل ما استُهلك فعلاً.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="usage">القياس.</param>
    /// <param name="window">نافذة المحاسبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task RecordAsync(TenantId tenant, AgentModelUsage usage, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>يقرأ إنفاق المنشأة في نافذتها الجارية.</summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="window">نافذة المحاسبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<AgentTenantSpend> ReadAsync(TenantId tenant, TimeSpan window, CancellationToken cancellationToken);
}

/// <summary>
/// دفترٌ في الذاكرة — للتشغيل بعقدةٍ واحدة وللاختبار، على منوال
/// <c>InMemoryCapturedDraftStore</c>. ويُستبدل بدفترٍ مُستديم بسطرٍ في التركيب.
/// </summary>
public sealed class InMemoryAgentSpendLedger : IAgentSpendLedger
{
    private readonly ConcurrentDictionary<Guid, AgentTenantSpend> _spend = new();
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الدفتر.</summary>
    /// <param name="clock">مصدر الوقت — لا <c>DateTimeOffset.UtcNow</c> مباشرةً.</param>
    public InMemoryAgentSpendLedger(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<Result> AdmitAsync(
        TenantId tenant,
        long? ceiling,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (ceiling is null)
        {
            return Task.FromResult(Result.Success());
        }

        AgentTenantSpend current = Current(tenant, window);

        return Task.FromResult(current.Usage.Billable >= ceiling.Value
            ? Result.Failure(AgentErrors.SpendCeilingReached(tenant, ceiling.Value))
            : Result.Success());
    }

    /// <inheritdoc />
    public Task RecordAsync(
        TenantId tenant,
        AgentModelUsage usage,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);

        _spend.AddOrUpdate(
            tenant.Value,
            _ => new AgentTenantSpend(tenant, _clock.GetUtcNow(), usage, 1),
            (_, existing) =>
            {
                AgentTenantSpend fresh = Rolled(existing, tenant, window);
                return fresh with { Usage = fresh.Usage.Plus(usage), Turns = fresh.Turns + 1 };
            });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AgentTenantSpend> ReadAsync(TenantId tenant, TimeSpan window, CancellationToken cancellationToken) =>
        Task.FromResult(Current(tenant, window));

    private AgentTenantSpend Current(TenantId tenant, TimeSpan window) =>
        _spend.TryGetValue(tenant.Value, out AgentTenantSpend? existing)
            ? Rolled(existing, tenant, window)
            : new AgentTenantSpend(tenant, _clock.GetUtcNow(), AgentModelUsage.Zero, 0);

    /// <summary>نافذةٌ انقضت تُطوى إلى صفر — ولا تُجمَّع أبداً بلا حدّ.</summary>
    private AgentTenantSpend Rolled(AgentTenantSpend existing, TenantId tenant, TimeSpan window) =>
        _clock.GetUtcNow() - existing.WindowStartedAt >= window
            ? new AgentTenantSpend(tenant, _clock.GetUtcNow(), AgentModelUsage.Zero, 0)
            : existing;
}
