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
/// <param name="entitlements">خدمة الاستحقاق التي يستشيرها الحارس.</param>
public sealed class EntitlementGuard(EntitlementService entitlements)
{
    private readonly OperationLog _log = new(entitlements.Options.ControlConnectionString);

    /// <summary>خدمة الاستحقاق التي يستشيرها الحارس.</summary>
    public EntitlementService Entitlements { get; } = entitlements;

    /// <summary>حالة استحقاق وحدة لمستأجر، بلا إنفاذ — للعرض والاستعلام.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="moduleCode">رمز الوحدة.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الحالة؛ و<c>NotEntitled</c> لوحدة لم تُشترَ قط.</returns>
    public async Task<EntitlementState> StateAsync(Guid tenantId, string moduleCode,
        CancellationToken ct = default)
    {
        var set = await Entitlements.GetSetAsync(tenantId, ct);
        return set.TryGetValue(moduleCode, out var s) ? s : EntitlementState.NotEntitled;
    }

    /// <summary>
    /// يُنفِذ استحقاق <b>الكتابة</b>: يُقبل <c>Entitled</c> وحدها، ويرفض
    /// <c>ReadOnly</c> و<c>NotEntitled</c> بعد كتابة سطر رفض.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="moduleCode">رمز الوحدة.</param>
    /// <param name="actor">من يحاول.</param>
    /// <param name="operation">اسم العملية — يُكتب في السِرد.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الحالة عند السماح.</returns>
    /// <exception cref="EntitlementDeniedException">الكتابة غير مسموحة في هذه الحالة.</exception>
    public Task<EntitlementState> RequireWriteAsync(TenantRecord tenant, string moduleCode,
        string actor, string operation, CancellationToken ct = default) =>
        RequireAsync(tenant, moduleCode, actor, operation, AccessIntent.Write, ct);

    /// <summary>
    /// يُنفِذ استحقاق <b>القراءة</b>: يُقبل <c>Entitled</c> و<c>ReadOnly</c> معاً —
    /// عميل انقطع سداده يظلّ قادراً على إخراج سجلاته المحاسبية.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="moduleCode">رمز الوحدة.</param>
    /// <param name="actor">من يحاول.</param>
    /// <param name="operation">اسم العملية — يُكتب في السِرد.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الحالة عند السماح.</returns>
    /// <exception cref="EntitlementDeniedException">الوحدة غير مستحقّة إطلاقاً.</exception>
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

        // القرار من موضعه الوحيد — لا نسخة ثانية منه هنا ولا في أي وحدة.
        if (EntitlementRules.Allows(state, intent)) return state;

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
