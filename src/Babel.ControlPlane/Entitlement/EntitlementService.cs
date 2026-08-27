using System.Collections.Concurrent;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Entitlement;

/// <summary>
/// السند الذي يجيز تغيير استحقاق: من، ومتى، و<b>بأي صلاحية</b>. الحقل الثالث
/// إلزامي وغير قابل للفراغ في قاعدة البيانات نفسها — لأن الاستحقاق يحكم
/// <b>أي بيانات مالية يجوز إنشاؤها</b>، فتغييره حدث تدقيقي لا إعداد واجهة.
/// </summary>
/// <param name="Actor">من طلب التغيير.</param>
/// <param name="Authority">السند: رقم عقد، أو حدث سداد، أو تذكرة، أو قرار مُوثَّق.</param>
/// <param name="ReasonAr">السبب بالعربية.</param>
public sealed record ChangeAuthority(string Actor, string Authority, string ReasonAr)
{
    /// <summary>يتحقّق من اكتمال السند. <b>لا تغيير استحقاق بلا فاعل وسند وسبب.</b></summary>
    /// <exception cref="ArgumentException">أحد الحقول الثلاثة فارغ.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Actor)) throw new ArgumentException("الفاعل مطلوب");
        if (string.IsNullOrWhiteSpace(Authority))
            throw new ArgumentException(
                "السند مطلوب: رقم عقد، أو حدث سداد، أو تذكرة دعم، أو قرار مُوثَّق. "
                + "لا تغيير استحقاق بلا سند.");
        if (string.IsNullOrWhiteSpace(ReasonAr)) throw new ArgumentException("السبب مطلوب");
    }
}

/// <summary>
/// خدمة الاستحقاق: تقرأ مجموعة الاستحقاق لمستأجر وتُغيّرها. كل تغيير يمرّ
/// بالمُتحقِّق ويُكتب في سجل التدقيق بمن وبمتى وبأي سند.
/// </summary>
/// <param name="options">إعدادات مستوى التحكّم.</param>
/// <param name="registry">سجل المستأجرين.</param>
public sealed class EntitlementService(ControlPlaneOptions options, TenantRegistry registry)
{
    private readonly OperationLog _log = new(options.ControlConnectionString);
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset At,
        IReadOnlyDictionary<string, EntitlementState> Set)> _cache = new();

    /// <summary>مهلة الذاكرة المؤقتة قصيرة عمداً: خفض الاستحقاق يجب أن يسري بسرعة.</summary>
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>إعدادات مستوى التحكّم.</summary>
    public ControlPlaneOptions Options { get; } = options;

    /// <summary>سجل المستأجرين.</summary>
    public TenantRegistry Registry { get; } = registry;

    /// <summary>يُبطل ذاكرة استحقاق مستأجر فوراً.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    public void InvalidateCache(Guid tenantId) => _cache.TryRemove(tenantId, out _);

    /// <summary>يُبطل ذاكرة الاستحقاق لكل المستأجرين.</summary>
    public void InvalidateAll() => _cache.Clear();

    // -----------------------------------------------------------------------

    /// <summary>مجموعة استحقاق المستأجر، عبر ذاكرة قصيرة الأجل.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الحالة لكل وحدة.</returns>
    public async Task<IReadOnlyDictionary<string, EntitlementState>> GetSetAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(tenantId, out var hit) && Canon.Now() - hit.At < CacheTtl)
            return hit.Set;

        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        var set = await ReadSetAsync(c, tenantId, null, ct);
        _cache[tenantId] = (Canon.Now(), set);
        return set;
    }

    private static async Task<IReadOnlyDictionary<string, EntitlementState>> ReadSetAsync(
        NpgsqlConnection c, Guid tenantId, NpgsqlTransaction? tx, CancellationToken ct)
    {
        var rows = await Db.QueryAsync(c, """
            select module_code, state
              from control.tenant_module_entitlement
             where tenant_id = @t
             order by module_code asc
            """,
            r => (Code: r.GetString(0), State: Enum.Parse<EntitlementState>(r.GetString(1))),
            p => p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)), tx, ct);

        // غياب الصفّ = NotEntitled. لا حالة ضمنية ثالثة.
        var set = new Dictionary<string, EntitlementState>(StringComparer.Ordinal);
        foreach (var m in ModuleCatalog.All) set[m.Code] = EntitlementState.NotEntitled;
        foreach (var r in rows) set[r.Code] = r.State;
        return set;
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// يطبّق تغييرات الاستحقاق كوحدة واحدة: يقرأ الحالة القائمة، يبني الحالة
    /// الناتجة، <b>يتحقّق من تماسكها كاملة</b>، ثم يكتب الاستحقاقات وسطور
    /// التدقيق في نفس المعاملة. رفض التماسك يُسجَّل في سِرد العمليات
    /// <b>قبل</b> الرمي (فخ-08).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, EntitlementState>> ApplyAsync(
        Guid tenantId, IReadOnlyList<EntitlementChange> changes, ChangeAuthority authority,
        CancellationToken ct = default)
    {
        authority.Validate();
        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        var tenant = await Db.QueryAsync(c,
            "select tenant_code, status from control.tenant where tenant_id = @t",
            r => (Code: r.GetString(0), Status: r.GetString(1)),
            p => p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)), tx, ct);
        if (tenant.Count == 0) throw new TenantNotFoundException(tenantId.ToString());

        if (tenant[0].Status == nameof(TenantStatus.Archived))
        {
            await OperationLog.WriteAsync(c, tenantId, authority.Actor, "entitlement.apply",
                OperationOutcome.Refused, "المستأجر مؤرشف — لا يُقبل تغيير استحقاق",
                new { tenant[0].Code }, tx, ct);
            await tx.CommitAsync(ct);
            throw new TenantArchivedException(tenant[0].Code);
        }

        var current = await ReadSetAsync(c, tenantId, tx, ct);
        var next = new Dictionary<string, EntitlementState>(current, StringComparer.Ordinal);

        // ترتيب كلّي ثابت على التغييرات: هي أيضاً كتابة متعددة الصفوف (فخ-10).
        var ordered = changes.OrderBy(x => x.ModuleCode, StringComparer.Ordinal).ToList();
        foreach (var ch in ordered)
        {
            ModuleCatalog.Require(ch.ModuleCode);
            next[ch.ModuleCode] = ch.NewState;
        }

        // الانتقال لا المجموعة: «‏CORE = NotEntitled» مشروعة عن مستأجر جديد
        // وكارثية عن مستأجر رحّل قيوداً، والفرق في الحالة السابقة وحدها.
        var violations = EntitlementValidator.ValidateTransition(current, next);
        if (violations.Count > 0)
        {
            await OperationLog.WriteAsync(c, tenantId, authority.Actor, "entitlement.apply",
                OperationOutcome.Refused,
                "مجموعة استحقاق غير متماسكة: " + string.Join(" ؛ ", violations.Select(v => v.MessageAr)),
                new
                {
                    requested = ordered.Select(x => new { x.ModuleCode, state = x.NewState.ToString() }),
                    violations = violations.Select(v => new { v.ModuleCode, v.MessageEn }),
                    repairs = EntitlementValidator.SuggestRepairs(next)
                        .Select(x => new { x.ModuleCode, state = x.NewState.ToString() })
                }, tx, ct);
            await tx.CommitAsync(ct);   // السطر التدقيقي يُثبَّت حتى مع الرفض
            throw new IncoherentEntitlementSetException(violations);
        }

        var now = Canon.Now();
        var effective = ordered.Where(ch => current[ch.ModuleCode] != ch.NewState).ToList();

        foreach (var ch in effective)
        {
            await Db.WriteAsync(c, """
                insert into control.tenant_module_entitlement
                    (tenant_id, module_code, state, effective_from, updated_at)
                values (@t, @m, @s, @now, @now)
                on conflict (tenant_id, module_code) do update
                   set state = excluded.state,
                       effective_from = excluded.effective_from,
                       updated_at = excluded.updated_at
                """, 1, p =>
                {
                    p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                    p.AddWithValue("m", ch.ModuleCode);
                    p.AddWithValue("s", ch.NewState.ToString());
                    p.AddWithValue("now", now);
                }, tx, ct);

            await Db.WriteAsync(c, """
                insert into control.entitlement_audit
                    (tenant_id, module_code, old_state, new_state, actor, authority, reason_ar, occurred_at)
                values (@t, @m, @old, @new, @actor, @auth, @reason, @now)
                """, 1, p =>
                {
                    p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                    p.AddWithValue("m", ch.ModuleCode);
                    p.AddWithValue("old", current[ch.ModuleCode].ToString());
                    p.AddWithValue("new", ch.NewState.ToString());
                    p.AddWithValue("actor", authority.Actor);
                    p.AddWithValue("auth", authority.Authority);
                    p.AddWithValue("reason", authority.ReasonAr);
                    p.AddWithValue("now", now);
                }, tx, ct);
        }

        await OperationLog.WriteAsync(c, tenantId, authority.Actor, "entitlement.apply",
            OperationOutcome.Allowed,
            $"طُبِّق {effective.Count} تغيير استحقاق بسند «{authority.Authority}»",
            new { changed = effective.Select(x => new { x.ModuleCode, state = x.NewState.ToString() }) },
            tx, ct);

        await tx.CommitAsync(ct);
        InvalidateCache(tenantId);
        return next;
    }

    /// <summary>يُطبّق خطة على مستأجر: كل وحدات الخطة <c>Entitled</c>، وما عداها كما هو.</summary>
    public async Task ApplyPlanAsync(Guid tenantId, string planCode, ChangeAuthority authority,
        CancellationToken ct = default)
    {
        List<string> modules;
        await using (var c = await Db.OpenAsync(Options.ControlConnectionString, ct))
            modules = await Db.QueryAsync(c,
                "select module_code from control.plan_module where plan_code = @p order by module_code asc",
                r => r.GetString(0), p => p.AddWithValue("p", planCode), null, ct);

        if (modules.Count == 0)
            throw new ArgumentException($"الخطة «{planCode}» بلا وحدات أو غير موجودة", nameof(planCode));

        // الإغلاق المتعدّي: الخطة التي تبيع POS تبيع ضمناً INV وAR وAP وCORE.
        var full = new SortedSet<string>(modules, StringComparer.Ordinal);
        foreach (var m in modules)
            foreach (var d in ModuleCatalog.TransitiveDependencies(m)) full.Add(d);

        await ApplyAsync(tenantId,
            [.. full.Select(m => new EntitlementChange(m, EntitlementState.Entitled))],
            authority, ct);
    }

    /// <summary>
    /// <b>انقطاع السداد.</b> ينزل بكل وحدة إلى <b>أرضيتها</b> لا إلى العدم:
    /// الوحدات التي رحّلت قيوداً تصير <c>ReadOnly</c> — قراءةً وتقارير وتصديراً
    /// كاملة، بلا مستند جديد وبلا ترحيل — والوحدة التي لا تُرحّل قيوداً تُنزَع.
    ///
    /// <para><b>ولا يُترَك هذا لاجتهاد المشغّل.</b> «اقطع الاشتراك» أمرٌ يُنفَّذ
    /// كثيراً وتحت ضغط تجاري، ولو كان تنفيذه مجموعةَ تغييرات يكتبها إنسان
    /// لكتب أحدهم يوماً <c>NotEntitled</c> على الدفتر. الأرضية تمنعه، وهذه
    /// الدالّة تجعل الطريق الصحيح هو الطريق القصير.</para>
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="authority">السند: من، وبأي صلاحية، ولماذا.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>المجموعة بعد الخفض.</returns>
    public Task<IReadOnlyDictionary<string, EntitlementState>> LapseAsync(
        Guid tenantId, ChangeAuthority authority, CancellationToken ct = default) =>
        DegradeToAsync(tenantId, new HashSet<string>(StringComparer.Ordinal), authority, ct);

    /// <summary>
    /// <b>خفض الحزمة.</b> ما تغطّيه الحزمة الجديدة (بإغلاقها المتعدّي) يصير
    /// <c>Entitled</c>، وما خرج منها يهبط إلى <b>أرضيته</b> لا إلى العدم.
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="planCode">رمز الحزمة الجديدة.</param>
    /// <param name="authority">السند.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>المجموعة بعد الخفض.</returns>
    public async Task<IReadOnlyDictionary<string, EntitlementState>> DowngradeToPlanAsync(
        Guid tenantId, string planCode, ChangeAuthority authority, CancellationToken ct = default)
    {
        var plan = PlanCatalog.Require(planCode);
        var covered = new HashSet<string>(plan.Modules, StringComparer.Ordinal);
        foreach (var m in plan.Modules)
            foreach (var d in ModuleCatalog.TransitiveDependencies(m)) covered.Add(d);

        return await DegradeToAsync(tenantId, covered, authority, ct, covered);
    }

    private async Task<IReadOnlyDictionary<string, EntitlementState>> DegradeToAsync(
        Guid tenantId, IReadOnlySet<string> covered, ChangeAuthority authority,
        CancellationToken ct, IReadOnlySet<string>? raiseTo = null)
    {
        var current = await GetSetAsync(tenantId, ct);
        var next = new Dictionary<string, EntitlementState>(
            EntitlementValidator.Degrade(current, covered), StringComparer.Ordinal);

        if (raiseTo is not null)
            foreach (var code in raiseTo) next[code] = EntitlementState.Entitled;

        var changes = next.Where(kv => current[kv.Key] != kv.Value)
            .Select(kv => new EntitlementChange(kv.Key, kv.Value))
            .ToList();

        return await ApplyAsync(tenantId, changes, authority, ct);
    }

    /// <summary>يقرأ سجل تدقيق الاستحقاق لمستأجر: كل تغيير بمن ومتى وبأي سند.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>أسطر التدقيق مرتّبةً.</returns>
    public async Task<List<(string Module, string Old, string New, string Actor, string Authority,
        DateTimeOffset At)>> ReadAuditAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        return await Db.QueryAsync(c, """
            select module_code, coalesce(old_state,''), new_state, actor, authority, occurred_at
              from control.entitlement_audit
             where tenant_id = @t
             order by audit_id asc
            """,
            r => (r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                  r.GetFieldValue<DateTimeOffset>(5)),
            p => p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)), null, ct);
    }
}
