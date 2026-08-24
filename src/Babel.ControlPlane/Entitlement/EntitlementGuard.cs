using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;

namespace Babel.ControlPlane.Entitlement;

/// <summary>
/// الحارس عند <b>حدّ الخدمة</b>. كل مسار كتابة في كل وحدة ينادي
/// <see cref="RequireWriteAsync"/> قبل أن يفعل شيئاً، وكل مسار قراءة ينادي
/// <see cref="RequireReadAsync"/>.
///
/// <para><b>لماذا هنا لا في الواجهة:</b> إخفاء عنصر قائمة لا يمنع نداء
/// <c>POST /api/…</c>. الواجهة تُخفي لتحسين التجربة؛ الحارس يمنع.</para>
///
/// <para>كل رفض يكتب سطراً في سِرد العمليات <b>قبل</b> الرمي — فخ-08: ما
/// يُطلب في التحقيق هو المحاولة المرفوضة، وهي بحكم البناء لا تُنتج حدث نطاق.</para>
/// </summary>
public sealed class EntitlementGuard(EntitlementService entitlements)
{
    private readonly OperationLog _log = new(entitlements.Options.ControlConnectionString);

    public EntitlementService Entitlements { get; } = entitlements;

    public async Task<EntitlementState> StateAsync(Guid tenantId, string moduleCode,
        CancellationToken ct = default)
    {
        var set = await Entitlements.GetSetAsync(tenantId, ct);
        return set.TryGetValue(moduleCode, out var s) ? s : EntitlementState.NotEntitled;
    }

    public Task<EntitlementState> RequireWriteAsync(TenantRecord tenant, string moduleCode,
        string actor, string operation, CancellationToken ct = default) =>
        RequireAsync(tenant, moduleCode, actor, operation, AccessIntent.Write, ct);

    public Task<EntitlementState> RequireReadAsync(TenantRecord tenant, string moduleCode,
        string actor, string operation, CancellationToken ct = default) =>
        RequireAsync(tenant, moduleCode, actor, operation, AccessIntent.Read, ct);

    private async Task<EntitlementState> RequireAsync(TenantRecord tenant, string moduleCode,
        string actor, string operation, AccessIntent intent, CancellationToken ct)
    {
        ModuleCatalog.Require(moduleCode);

        if (tenant.Status == TenantStatus.Archived)
        {
            await _log.WriteAsync(tenant.TenantId, actor, operation, OperationOutcome.Refused,
                "المستأجر مؤرشف — الوصول التطبيقي مقطوع والبيانات محفوظة",
                new { module = moduleCode, intent = intent.ToString() }, ct);
            throw new TenantArchivedException(tenant.TenantCode);
        }

        var state = await StateAsync(tenant.TenantId, moduleCode, ct);

        var allowed = intent switch
        {
            AccessIntent.Read => state is EntitlementState.Entitled or EntitlementState.ReadOnly,
            AccessIntent.Write => state is EntitlementState.Entitled,
            _ => false
        };

        if (allowed) return state;

        // السطر يُكتب قبل الرمي، لا بعده، ولا في معالِج الاستثناء.
        await _log.WriteAsync(tenant.TenantId, actor, operation, OperationOutcome.Refused,
            intent == AccessIntent.Write && state == EntitlementState.ReadOnly
                ? $"الوحدة «{moduleCode}» بحالة قراءة فقط: الإدخال والترحيل موقوفان، والقراءة والتصدير متاحان"
                : $"الوحدة «{moduleCode}» بحالة {EntitlementValidator.Ar(state)} — العملية مرفوضة",
            new { module = moduleCode, state = state.ToString(), intent = intent.ToString() }, ct);

        throw new EntitlementDeniedException(tenant.TenantCode, moduleCode, state, intent);
    }

    /// <summary>
    /// الوحدات الظاهرة في القائمة: <c>NotEntitled</c> مخفيّة تماماً.
    /// هذه دالة عرض — وليست هي الإنفاذ.
    /// </summary>
    public async Task<IReadOnlyList<(ModuleDefinition Module, EntitlementState State)>>
        VisibleModulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var set = await Entitlements.GetSetAsync(tenantId, ct);
        return [.. ModuleCatalog.All
            .Where(m => set.TryGetValue(m.Code, out var s) && s != EntitlementState.NotEntitled)
            .OrderBy(m => m.SortOrder)
            .Select(m => (m, set[m.Code]))];
    }
}
