using System.Text.Json;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Entitlement;

public sealed record ArchiveCheck(
    string Code, string NameAr, string NameEn, bool Passed, string DetailAr, object Detail);

public sealed record ArchiveDecision(
    Guid RequestId, bool Approved, IReadOnlyList<ArchiveCheck> Checks, string SummaryAr)
{
    public IReadOnlyList<ArchiveCheck> Failed => [.. Checks.Where(c => !c.Passed)];
}

public sealed record ArchiveApproval(string ApprovedBy, string ApprovalRef);

/// <summary>
/// أرشفة وحدة — <b>لا إلغاء تركيب</b> (ADR-0014).
///
/// <para>وحدة رحّلت قيوداً لا يمكن إلغاء تركيبها، لأن القيود غير قابلة للحذف
/// بصلاحيات قاعدة البيانات نفسها (ADR-0003، مقيس: SQLSTATE 42501)، ولأن
/// حذفها يكسر سلسلة البصمات ويترك فجوة يقرؤها المدقّق «سجل محذوف»
/// (ADR-0007، فخ-21). فـ«إلغاء التركيب» ليس ميزة ناقصة بل <b>مفهوم متناقض</b>.</para>
///
/// <para>الأرشفة مشروطة بفحص آلي سابق. <b>الفحص يرفض، ولا يُحذّر.</b></para>
/// </summary>
public sealed class ModuleArchiveService(
    ControlPlaneOptions options, EntitlementService entitlements)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// يُجري الفحص السابق للأرشفة ويسجّل قراره — <b>مقبولاً كان أو مرفوضاً</b>.
    /// الرفض المُسجَّل هو نصف قيمة هذه الآلية (فخ-08).
    /// </summary>
    public async Task<ArchiveDecision> RequestArchiveAsync(TenantRecord tenant, string moduleCode,
        string requestedBy, ArchiveApproval? approval, CancellationToken ct = default)
    {
        var module = ModuleCatalog.Require(moduleCode);
        var checks = new List<ArchiveCheck>();

        // ---- 0 · وحدة لا تُرحّل قيوداً: الاستثناء المُوثَّق في ADR-0014 -----
        if (!module.PostsJournal)
            checks.Add(new("module.no_journal", "وحدة لا تُرحّل قيوداً",
                "module posts no journal entries", true,
                "هذه الوحدة لا تُنتج قيوداً؛ إلغاء تركيبها ممكن فعلاً — وهو الاستثناء الوحيد في ADR-0014",
                new { module.PostsJournal }));

        await using var tc = await Db.OpenAsync(
            options.TenantOwnerConnectionString(tenant.DatabaseName), ct);

        // ---- 1 · كل حساب أستاذ عام تابع للوحدة برصيد صفر --------------------
        var nonZero = await Db.QueryAsync(tc, """
            select a.account_code, a.name_ar,
                   coalesce(sum(b.debit), 0) - coalesce(sum(b.credit), 0) as net
              from ledger.account a
              left join ledger.account_balance b on b.account_code = a.account_code
             where a.subledger = @m
             group by a.account_code, a.name_ar
            having coalesce(sum(b.debit), 0) - coalesce(sum(b.credit), 0) <> 0
             order by a.account_code asc
            """,
            r => (Code: r.GetString(0), Name: r.GetString(1), Net: r.GetDecimal(2)),
            p => p.AddWithValue("m", moduleCode), null, ct);

        checks.Add(new("gl.zero_balance", "أرصدة حسابات الوحدة صفر",
            "module subledger accounts are at zero", nonZero.Count == 0,
            nonZero.Count == 0
                ? "كل حسابات الأستاذ المساعد التابعة للوحدة برصيد صفر"
                : "حسابات برصيد غير صفري: " + string.Join("، ",
                    nonZero.Select(x => $"{x.Code} ({Canon.Amount(x.Net)})")),
            new { accounts = nonZero.Select(x => new { x.Code, x.Name, net = Canon.Amount(x.Net) }) }));

        // ---- 2 · لا مستندات مفتوحة ------------------------------------------
        var openDocs = await Db.QueryAsync(tc, """
            select doc_no, state from app.document
             where module_code = @m and state in ('Draft','Open')
             order by doc_no asc
            """,
            r => (No: r.GetString(0), State: r.GetString(1)),
            p => p.AddWithValue("m", moduleCode), null, ct);

        checks.Add(new("docs.none_open", "لا مستندات مفتوحة", "no open documents",
            openDocs.Count == 0,
            openDocs.Count == 0 ? "لا مستند مفتوح للوحدة"
                : $"عدد المستندات المفتوحة {openDocs.Count}",
            new { documents = openDocs.Select(d => new { d.No, d.State }) }));

        // ---- 3 · كل فترة تحتوي قيوداً للوحدة مُقفَلة --------------------------
        var openPeriods = await Db.QueryAsync(tc, """
            select distinct e.period_code
              from ledger.journal_entry e
              join ledger.period p on p.period_code = e.period_code
             where e.module_code = @m and p.state <> 'Closed'
             order by 1 asc
            """, r => r.GetString(0), p => p.AddWithValue("m", moduleCode), null, ct);

        checks.Add(new("periods.closed", "الفترات الحاوية مُقفَلة",
            "containing periods are closed", openPeriods.Count == 0,
            openPeriods.Count == 0 ? "كل فترة تحتوي قيوداً لهذه الوحدة مُقفَلة"
                : "فترات مفتوحة: " + string.Join("، ", openPeriods),
            new { periods = openPeriods }));

        // ---- 4 · لا وحدة أخرى ما تزال تعتمد عليها بحالة فعّالة ---------------
        var set = await entitlements.GetSetAsync(tenant.TenantId, ct);
        var activeDependents = ModuleCatalog.Dependents(moduleCode)
            .Where(d => set.TryGetValue(d, out var s) && s != EntitlementState.NotEntitled)
            .ToList();

        checks.Add(new("deps.no_active_dependents", "لا وحدات تابعة فعّالة",
            "no active dependent modules", activeDependents.Count == 0,
            activeDependents.Count == 0 ? "لا وحدة فعّالة تعتمد على هذه الوحدة"
                : "وحدات تابعة ما تزال فعّالة: " + string.Join("، ", activeDependents),
            new { dependents = activeDependents }));

        // ---- 5 · اعتماد مُسمّى مُسجَّل ---------------------------------------
        var hasApproval = approval is not null
            && !string.IsNullOrWhiteSpace(approval.ApprovedBy)
            && !string.IsNullOrWhiteSpace(approval.ApprovalRef);

        checks.Add(new("approval.named", "اعتماد مُسمّى مُسجَّل", "named approval recorded",
            hasApproval,
            hasApproval ? $"اعتمدها «{approval!.ApprovedBy}» بالمرجع «{approval.ApprovalRef}»"
                : "لا اعتماد مُسمّى: الأرشفة قرار تشغيلي يحمل اسماً ومرجعاً",
            new { approvedBy = approval?.ApprovedBy, approvalRef = approval?.ApprovalRef }));

        // ---- القرار ---------------------------------------------------------
        var approved = checks.All(x => x.Passed);
        var requestId = Guid.CreateVersion7();
        var now = Canon.Now();

        await using var cc = await Db.OpenAsync(options.ControlConnectionString, ct);
        await using var tx = await cc.BeginTransactionAsync(ct);

        await Db.WriteAsync(cc, """
            insert into control.module_archive_request
                (request_id, tenant_id, module_code, requested_by, requested_at,
                 approved_by, approval_ref, decision, decided_at)
            values (@id, @t, @m, @by, @at, @apby, @apref, @dec, @at)
            """, 1, p =>
            {
                p.Add(Db.P("id", requestId, NpgsqlDbType.Uuid));
                p.Add(Db.P("t", tenant.TenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("m", moduleCode);
                p.AddWithValue("by", requestedBy);
                p.AddWithValue("at", now);
                p.Add(Db.P("apby", approval?.ApprovedBy, NpgsqlDbType.Text));
                p.Add(Db.P("apref", approval?.ApprovalRef, NpgsqlDbType.Text));
                p.AddWithValue("dec", approved ? "Approved" : "Refused");
            }, tx, ct);

        foreach (var chk in checks.OrderBy(x => x.Code, StringComparer.Ordinal))
            await Db.WriteAsync(cc, """
                insert into control.module_archive_check
                    (request_id, check_code, name_ar, name_en, passed, detail)
                values (@r, @c, @ar, @en, @p, @d)
                """, 1, p =>
                {
                    p.Add(Db.P("r", requestId, NpgsqlDbType.Uuid));
                    p.AddWithValue("c", chk.Code);
                    p.AddWithValue("ar", chk.NameAr);
                    p.AddWithValue("en", chk.NameEn);
                    p.AddWithValue("p", chk.Passed);
                    p.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb)
                    { Value = JsonSerializer.Serialize(chk.Detail, Json) });
                }, tx, ct);

        var summary = approved
            ? $"اجتاز الفحص السابق للأرشفة ({checks.Count} فحصاً) — الوحدة «{moduleCode}» تُؤرشَف"
            : "رُفضت الأرشفة: " + string.Join(" ؛ ", checks.Where(x => !x.Passed).Select(x => x.DetailAr));

        await OperationLog.WriteAsync(cc, tenant.TenantId, requestedBy, "module.archive",
            approved ? OperationOutcome.Allowed : OperationOutcome.Refused, summary,
            new { module = moduleCode, checks = checks.Select(x => new { x.Code, x.Passed }) },
            tx, ct);

        await tx.CommitAsync(ct);

        if (!approved) return new ArchiveDecision(requestId, false, checks, summary);

        // الأرشفة نفسها = خفض الاستحقاق إلى قراءة فقط. البيانات باقية ومقروءة
        // ومصدَّرة؛ الإدخال والترحيل فقط هما ما يتوقّف.
        await entitlements.ApplyAsync(tenant.TenantId,
            [new EntitlementChange(moduleCode, EntitlementState.ReadOnly)],
            new ChangeAuthority(requestedBy, approval!.ApprovalRef,
                $"أرشفة الوحدة «{moduleCode}» بعد اجتياز الفحص السابق للأرشفة"), ct);

        return new ArchiveDecision(requestId, true, checks, summary);
    }
}
