using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Provisioning;

public sealed record ProvisioningRequest(
    string IdempotencyKey,
    string TenantCode,
    BilingualName Name,
    string PlanCode,
    string AdminUserRef,
    BilingualName AdminName,
    string AdminEmail,
    string RequestedBy,
    int FiscalYear,
    Guid? TenantId = null,
    IsolationModel Isolation = IsolationModel.DatabasePerTenant,
    Residency Residency = Residency.Provider,
    int TargetSchemaVersion = TenantSchema.LatestVersion);

public sealed record ProvisioningResult(
    Guid RunId, Guid TenantId, bool Resumed, int StepsExecuted, int StepsSkipped,
    IReadOnlyList<ProvisioningStepState> Steps);

/// <summary>
/// تزويد مستأجر — عملية <b>مُحكَمة</b> بدفتر خطوات.
///
/// <para><b>المبدأ:</b> كل خطوة إمّا مُسجَّلة مكتملة فتُتخطّى، أو تُعاد كاملةً؛
/// وكل خطوة مكتوبة لتكون <b>إعادتها بلا أثر إضافي</b>. لذلك لا يضرّ أن تُقاطَع
/// التشغيلة بعد تنفيذ أثر خطوة وقبل تسجيله: إعادة التنفيذ تصل إلى النتيجة نفسها.</para>
///
/// <para>هذا هو الفرق بين «مُحكَم» و«يبدو مُحكَماً»: لا نراهن على أن الانهيار
/// يقع بين الخطوات.</para>
/// </summary>
public sealed class TenantProvisioner(
    ControlPlaneOptions options, TenantRegistry registry, EntitlementService entitlements)
{
    /// <summary>خطّاف المقاطعة — للإثباتات فقط؛ <c>null</c> في الإنتاج.</summary>
    public Func<string, InterruptPhase, Task>? Interrupt { get; set; }

    public static readonly IReadOnlyList<string> Steps =
    [
        "register_tenant",
        "create_database",
        "apply_schema",
        "grant_app_role",
        "seed_chart_of_accounts",
        "seed_roles",
        "seed_periods",
        "create_first_admin",
        "apply_entitlements",
        "activate"
    ];

    public async Task<ProvisioningResult> ProvisionAsync(ProvisioningRequest req,
        CancellationToken ct = default)
    {
        Db.Ident(req.TenantCode);
        var tenantId = req.TenantId ?? DeterministicTenantId(req.TenantCode);
        var dbName = options.TenantDatabaseName(req.TenantCode);
        Db.Ident(dbName);

        await using var control = await Db.OpenAsync(options.ControlConnectionString, ct);
        var journal = new ProvisioningJournal(control);
        var (runId, resumed) = await journal.OpenRunAsync(
            req.IdempotencyKey, tenantId, req.TenantCode, req.RequestedBy, ct);

        var executed = 0;
        var skipped = 0;

        async Task Step(string name, Func<Task> body)
        {
            var ordinal = Steps.ToList().IndexOf(name) + 1;
            if (!await journal.ClaimStepAsync(runId, ordinal, name, ct)) { skipped++; return; }

            await body();
            if (Interrupt is not null) await Interrupt(name, InterruptPhase.AfterEffect);

            await journal.CompleteStepAsync(runId, name, ct: ct);
            executed++;
            if (Interrupt is not null) await Interrupt(name, InterruptPhase.AfterCommit);
        }

        // 1 -------------------------------------------------------------------
        await Step("register_tenant", async () =>
            await registry.RegisterAsync(control, tenantId, req.TenantCode, req.Name,
                req.Isolation, req.Residency, null, ct));

        // 2 -------------------------------------------------------------------
        await Step("create_database", async () => await CreateDatabaseAsync(dbName, ct));

        // 3 -------------------------------------------------------------------
        await Step("apply_schema", async () =>
        {
            await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
            await TenantSchema.MigrateToAsync(tc, req.TargetSchemaVersion, ct);
            await WriteTenantMetaAsync(tc, tenantId, req, ct);
        });

        // 4 -------------------------------------------------------------------
        await Step("grant_app_role", async () => await GrantAppRoleAsync(dbName, ct));

        // 5 -------------------------------------------------------------------
        await Step("seed_chart_of_accounts", async () =>
        {
            await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
            await SeedData.SeedChartOfAccountsAsync(tc, ct);
        });

        // 6 -------------------------------------------------------------------
        await Step("seed_roles", async () =>
        {
            await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
            await SeedData.SeedRolesAsync(tc, ct);
        });

        // 7 -------------------------------------------------------------------
        await Step("seed_periods", async () =>
        {
            await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
            await SeedData.SeedPeriodsAsync(tc, req.FiscalYear, ct);
        });

        // 8 -------------------------------------------------------------------
        await Step("create_first_admin", async () =>
        {
            await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
            await Db.WriteAsync(tc, """
                insert into app.app_user
                    (user_ref, name_ar, name_en, email, role_code, state, created_at)
                values (@u, @ar, @en, @mail, 'OWNER', 'Active', @t)
                on conflict (user_ref) do update
                   set name_ar = excluded.name_ar, name_en = excluded.name_en,
                       email = excluded.email, role_code = excluded.role_code
                """, 1, p =>
                {
                    p.AddWithValue("u", req.AdminUserRef);
                    p.AddWithValue("ar", req.AdminName.Ar);
                    p.AddWithValue("en", req.AdminName.En);
                    p.AddWithValue("mail", req.AdminEmail);
                    p.AddWithValue("t", Canon.Now());
                }, null, ct);

            // القائمة الاسمية تُنسَخ إلى مستوى التحكّم: المحور الثاني للتسعير
            // يحتاجها، وهي بيانات فوترة لا بيانات محاسبية.
            await Db.WriteAsync(control, """
                insert into control.tenant_user
                    (tenant_id, user_ref, name_ar, name_en, state, created_at)
                values (@t, @u, @ar, @en, 'Active', @at)
                on conflict (tenant_id, user_ref) do update
                   set name_ar = excluded.name_ar, name_en = excluded.name_en
                """, 1, p =>
                {
                    p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                    p.AddWithValue("u", req.AdminUserRef);
                    p.AddWithValue("ar", req.AdminName.Ar);
                    p.AddWithValue("en", req.AdminName.En);
                    p.AddWithValue("at", Canon.Now());
                }, null, ct);
        });

        // 9 -------------------------------------------------------------------
        await Step("apply_entitlements", async () =>
            await entitlements.ApplyPlanAsync(tenantId, req.PlanCode,
                new ChangeAuthority(req.RequestedBy, $"provisioning:{req.IdempotencyKey}",
                    $"تفعيل الخطة «{req.PlanCode}» عند تزويد المستأجر"), ct));

        // 10 ------------------------------------------------------------------
        await Step("activate", async () =>
        {
            await registry.SetSchemaVersionAsync(control, tenantId, req.TargetSchemaVersion, null, ct);
            await registry.SetStatusAsync(control, tenantId, TenantStatus.Active, Canon.Now(), null, ct);
            await journal.CompleteRunAsync(runId, ct);
            await OperationLog.WriteAsync(control, tenantId, req.RequestedBy, "tenant.provision",
                OperationOutcome.Allowed, $"اكتمل تزويد المستأجر «{req.TenantCode}»",
                new { req.IdempotencyKey, database = dbName }, null, ct);
        });

        var steps = await journal.ReadStepsAsync(runId, ct);
        return new ProvisioningResult(runId, tenantId, resumed, executed, skipped, steps);
    }

    // =======================================================================

    /// <summary>
    /// معرّف مستأجر مشتقّ من رمزه اشتقاقاً حتمياً. الغرض: إعادة تشغيل التزويد
    /// بلا تمرير المعرّف تصل إلى نفس المستأجر لا إلى مستأجر ثانٍ.
    /// </summary>
    public static Guid DeterministicTenantId(string tenantCode)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("babel.tenant:" + tenantCode));
        var g = new byte[16];
        Array.Copy(bytes, g, 16);
        g[6] = (byte)((g[6] & 0x0F) | 0x80);   // نسخة 8 (‏UUIDv8 — مشتقّ من اسم)
        g[8] = (byte)((g[8] & 0x3F) | 0x80);
        return new Guid(g);
    }

    private async Task CreateDatabaseAsync(string dbName, CancellationToken ct)
    {
        await using var maint = await Db.OpenAsync(options.MaintenanceConnectionString, ct);
        var exists = await Db.ScalarAsync<long>(maint,
            "select count(*) from pg_database where datname = @d",
            p => p.AddWithValue("d", dbName), null, ct);
        if (exists > 0) return;

        try
        {
            // CREATE DATABASE لا يعمل داخل معاملة، ولا يقبل معاملات مرتبطة.
            await Db.ExecAsync(maint, $"create database {Db.Ident(dbName)} template template0 "
                                      + "encoding 'UTF8' lc_collate 'C' lc_ctype 'C'", null, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P04")
        {
            // 42P04 duplicate_database: نداء متزامن سبقنا. النتيجة مطلوبة، لا الفاعل.
        }
    }

    private static async Task WriteTenantMetaAsync(NpgsqlConnection tc, Guid tenantId,
        ProvisioningRequest req, CancellationToken ct)
    {
        await Db.WriteAsync(tc, """
            insert into app.tenant_meta
                (singleton, tenant_id, tenant_code, name_ar, name_en, created_at)
            values (true, @id, @code, @ar, @en, @t)
            on conflict (singleton) do update
               set tenant_id = excluded.tenant_id, tenant_code = excluded.tenant_code,
                   name_ar = excluded.name_ar, name_en = excluded.name_en
            """, 1, p =>
            {
                p.Add(Db.P("id", tenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("code", req.TenantCode);
                p.AddWithValue("ar", req.Name.Ar);
                p.AddWithValue("en", req.Name.En);
                p.AddWithValue("t", Canon.Now());
            }, null, ct);
    }

    /// <summary>
    /// صلاحيات دور التطبيق. ADR-0003: دفتر الأستاذ <b>يُضاف إليه فقط</b>،
    /// والحصانة مفروضة بالصلاحيات لا بالكود — فلا <c>UPDATE</c> ولا
    /// <c>DELETE</c> على القيود وسطورها، ولا <c>DELETE</c> في أي مكان.
    /// ودور التطبيق ليس superuser ولا مالك المخطط (فخ-30).
    /// </summary>
    private async Task GrantAppRoleAsync(string dbName, CancellationToken ct)
    {
        var role = Db.Ident(options.AppRole);
        await using var maint = await Db.OpenAsync(options.MaintenanceConnectionString, ct);
        await Db.ExecAsync(maint, $"grant connect on database {Db.Ident(dbName)} to {role}", null, ct);

        await using var tc = await Db.OpenAsync(options.TenantOwnerConnectionString(dbName), ct);
        await Db.ExecAsync(tc, $"""
            revoke create on schema public from public;
            grant usage on schema app, ledger to {role};

            grant select, insert, update on all tables in schema app to {role};
            grant select, insert, update on all tables in schema ledger to {role};

            -- دفتر الأستاذ: إضافة وقراءة فقط.
            revoke update, delete, truncate on ledger.journal_entry from {role};
            revoke update, delete, truncate on ledger.journal_line  from {role};
            revoke delete, truncate on all tables in schema ledger from {role};
            revoke delete, truncate on all tables in schema app     from {role};

            alter default privileges in schema app, ledger
                grant select, insert, update on tables to {role};
            """, null, ct);
    }
}
