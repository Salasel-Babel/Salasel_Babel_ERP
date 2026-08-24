using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// أدوات مشتركة بين الإثباتات: تهيئة قاعدة التحكّم، وصنع أساطيل مُحاكاة،
/// والتنظيف. <b>تُشغَّل على قاعدة مخصّصة للتجارب فقط</b> — كل شيء اسمه يبدأ
/// بـ<c>babel_cp_</c> يُحذف عند التنظيف.
/// </summary>
public static class Harness
{
    public const string TestPrefix = "babel_cp_";

    public static ControlPlaneOptions Options() => new()
    {
        ControlDatabase = Environment.GetEnvironmentVariable("BABEL_CP_CONTROL_DB_NAME")
                          ?? "babel_cp_control",
        TenantDatabasePrefix = TestPrefix + "t_",
        AppRole = Environment.GetEnvironmentVariable("BABEL_CP_APP_ROLE") ?? "babel_cp_app"
    };

    public static async Task ResetAsync(ControlPlaneOptions o)
    {
        await DropAllTestDatabasesAsync(o);
        await using (var maint = await Db.OpenAsync(o.MaintenanceConnectionString))
        {
            await TerminateAsync(maint, o.ControlDatabase);
            await Db.ExecAsync(maint, $"drop database if exists {Db.Ident(o.ControlDatabase)}");
        }
        await ControlSchema.EnsureAsync(o);

        await using var c = await Db.OpenAsync(o.ControlConnectionString);
        await ModuleCatalog.SeedAsync(c);
        await PlanCatalog.SeedAsync(c);
    }

    public static async Task DropAllTestDatabasesAsync(ControlPlaneOptions o)
    {
        await using var maint = await Db.OpenAsync(o.MaintenanceConnectionString);
        var dbs = await Db.QueryAsync(maint,
            "select datname from pg_database where datname like @p order by datname asc",
            r => r.GetString(0), p => p.AddWithValue("p", o.TenantDatabasePrefix + "%"));
        foreach (var d in dbs)
        {
            await Db.ExecAsync(maint, $"grant connect on database {Db.Ident(d)} to public");
            await TerminateAsync(maint, d);
            await Db.ExecAsync(maint, $"drop database if exists {Db.Ident(d)}");
        }
    }

    public static async Task TerminateAsync(NpgsqlConnection maint, string dbName) =>
        await Db.ExecAsync(maint, $"""
            select pg_terminate_backend(pid) from pg_stat_activity
             where datname = '{dbName}' and pid <> pg_backend_pid()
            """);

    /// <summary>
    /// أسطول مُحاكى: قواعد بيانات حقيقية بمخطط حقيقي، مُسجَّلة في السجل.
    /// الاختصار الوحيد هو تخطّي البذور والاستحقاقات — لأن ما يُقاس هنا هو
    /// الترحيل والاتصالات، لا التزويد (وهو مُثبَت في القسم أ).
    /// </summary>
    public static async Task<List<TenantRecord>> SimulateFleetAsync(ControlPlaneOptions o,
        TenantRegistry registry, string codePrefix, int count, int schemaVersion,
        int parallelism = 4, bool light = false)
    {
        var codes = Enumerable.Range(0, count).Select(i => $"{codePrefix}{i:D3}").ToList();

        await Parallel.ForEachAsync(codes,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (code, ct) =>
        {
            var dbName = o.TenantDatabaseName(code);
            await using (var maint = await Db.OpenAsync(o.MaintenanceConnectionString, ct))
            {
                var exists = await Db.ScalarAsync<long>(maint,
                    "select count(*) from pg_database where datname = @d",
                    p => p.AddWithValue("d", dbName), null, ct);
                if (exists == 0)
                    try
                    {
                        await Db.ExecAsync(maint,
                            $"create database {Db.Ident(dbName)} template template0 encoding 'UTF8' "
                            + "lc_collate 'C' lc_ctype 'C'", null, ct);
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42P04") { }

                await Db.ExecAsync(maint,
                    $"grant connect on database {Db.Ident(dbName)} to {Db.Ident(o.AppRole)}", null, ct);
            }

            await using (var tc = await Db.OpenAsync(o.TenantOwnerConnectionString(dbName), ct))
            {
                if (light)
                {
                    // أسطول خفيف: قواعد حقيقية بجدول واحد. يُستعمل في قياس
                    // الاتصالات وحده — وهو قياس لا علاقة له بحجم المخطط.
                    await Db.ExecAsync(tc, $"""
                        create schema if not exists app;
                        create table if not exists app.probe (
                            k text primary key, name_ar text not null, name_en text not null);
                        insert into app.probe (k, name_ar, name_en) values ('1','فحص','probe')
                            on conflict (k) do nothing;
                        grant usage on schema app to {Db.Ident(o.AppRole)};
                        grant select on app.probe to {Db.Ident(o.AppRole)};
                        """, null, ct);
                }
                else
                {
                    await TenantSchema.MigrateToAsync(tc, schemaVersion, ct);
                    await Db.ExecAsync(tc, $"""
                        grant usage on schema app, ledger to {Db.Ident(o.AppRole)};
                        grant select, insert, update on all tables in schema app to {Db.Ident(o.AppRole)};
                        grant select, insert, update on all tables in schema ledger to {Db.Ident(o.AppRole)};
                        revoke update, delete, truncate on ledger.journal_entry from {Db.Ident(o.AppRole)};
                        revoke update, delete, truncate on ledger.journal_line from {Db.Ident(o.AppRole)};
                        """, null, ct);
                }
            }

            await using var cc = await Db.OpenAsync(o.ControlConnectionString, ct);
            var id = Provisioning.TenantProvisioner.DeterministicTenantId(code);
            await registry.RegisterAsync(cc, id, code,
                BilingualName.Of($"مستأجر تجريبي {code}", $"simulated tenant {code}"),
                ct: ct);
            await registry.SetSchemaVersionAsync(cc, id, schemaVersion, null, ct);
            await registry.SetStatusAsync(cc, id, TenantStatus.Active, Canon.Now(), null, ct);
        });

        var all = await registry.ListAsync(TenantStatus.Active);
        return [.. all.Where(t => t.TenantCode.StartsWith(codePrefix, StringComparison.Ordinal))];
    }

    /// <summary>عدد اتصالات الخادم الفعلية — القياس الحقيقي لا التقدير.</summary>
    public static async Task<(int Total, int OurTenants)> ServerConnectionsAsync(
        ControlPlaneOptions o, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(o.MaintenanceConnectionString, ct);
        var total = (int)(await Db.ScalarAsync<long>(c,
            "select count(*) from pg_stat_activity where backend_type = 'client backend'",
            null, null, ct));
        var ours = (int)(await Db.ScalarAsync<long>(c, """
            select count(*) from pg_stat_activity
             where backend_type = 'client backend' and datname like @p
            """, p => p.AddWithValue("p", o.TenantDatabasePrefix + "%"), null, ct));
        return (total, ours);
    }

    public static async Task<int> MaxConnectionsAsync(ControlPlaneOptions o)
    {
        await using var c = await Db.OpenAsync(o.MaintenanceConnectionString);
        return int.Parse((await Db.ScalarAsync<string>(c, "show max_connections"))!);
    }

    public static string Pct(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return "n/a";
        var idx = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)].ToString("F2");
    }
}
