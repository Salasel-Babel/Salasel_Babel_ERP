using System.Diagnostics;
using System.Text.Json;
using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// (D) Per-tenant flexible documents without Marten: EF Core 10 JSON column
///     mapping (ToJson) over Npgsql jsonb, plus an untyped jsonb escape hatch
///     for the things the platform does not model at all.
/// </summary>
public static class ProofD_TenantDocs
{
    private const int SyntheticTenants = 200_000;

    public static async Task RunAsync(IServiceProvider services, ProofRecorder rec)
    {
        rec.Section("(D) per-tenant flexible documents, no schema migration per customer");

        // ---- D1 : store and read a per-tenant settings document -----------
        var acme = new TenantSettings
        {
            TenantId = "acme",
            UpdatedAt = DateTime.UtcNow,
            Settings = new SettingsDoc
            {
                Locale = "ar-SA",
                Currency = "SAR",
                FiscalYearStartMonth = 1,
                CompanyNameAr = "شركة سلاسل بابل للتقنية",
                Zatca = new ZatcaSettings
                {
                    Environment = "simulation", VatNumber = "300000000000003",
                    PhaseTwoEnabled = true, MaxRetries = 5
                },
                CustomFields =
                [
                    new CustomFieldDef { Key = "project_code", LabelAr = "رمز المشروع", LabelEn = "Project code", DataType = "text", Required = true },
                    new CustomFieldDef { Key = "cost_center",  LabelAr = "مركز التكلفة", LabelEn = "Cost centre", DataType = "text", Required = false }
                ],
                ReportTemplates =
                [
                    new ReportTemplateDef { Code = "TB", NameAr = "ميزان المراجعة", Layout = "landscape-a4" }
                ]
            }
        };

        var globex = new TenantSettings
        {
            TenantId = "globex",
            UpdatedAt = DateTime.UtcNow,
            Settings = new SettingsDoc
            {
                Locale = "en-SA", Currency = "SAR", FiscalYearStartMonth = 7,
                CompanyNameAr = "مؤسسة جلوبكس",
                Zatca = new ZatcaSettings { Environment = "production", VatNumber = "310000000000003", PhaseTwoEnabled = true, MaxRetries = 9 },
                CustomFields = [ new CustomFieldDef { Key = "fleet_no", LabelAr = "رقم المركبة", LabelEn = "Fleet no", DataType = "number", Required = true } ],
                ReportTemplates = [ new ReportTemplateDef { Code = "PL", NameAr = "قائمة الدخل", Layout = "portrait-a4" } ]
            }
        };

        await using (var ctx = Contexts.Create())
        {
            ctx.TenantSettings.AddRange(acme, globex);
            await ctx.SaveChangesAsync();
        }

        await using (var read = Contexts.Create())
        {
            var back = await read.TenantSettings.AsNoTracking().SingleAsync(t => t.TenantId == "acme");
            var ok = back.Settings.CompanyNameAr == "شركة سلاسل بابل للتقنية"
                     && back.Settings.Zatca.Environment == "simulation"
                     && back.Settings.Zatca.MaxRetries == 5
                     && back.Settings.CustomFields.Count == 2
                     && back.Settings.CustomFields[0].LabelAr == "رمز المشروع"
                     && back.Settings.ReportTemplates.Single().Code == "TB";
            var stored = await Sql.ScalarAsync<string>(Config.Admin,
                "select jsonb_pretty(settings) from app.tenant_settings where tenant_id = 'acme'");
            rec.Check("D1", "EF Core 10 maps a whole POCO graph to one jsonb column (ToJson)", ok,
                $"round-tripped: locale={back.Settings.Locale} currency={back.Settings.Currency} " +
                $"fiscalStart={back.Settings.FiscalYearStartMonth}\n" +
                $"nested Zatca: env={back.Settings.Zatca.Environment} phase2={back.Settings.Zatca.PhaseTwoEnabled}\n" +
                $"custom fields: {string.Join(", ", back.Settings.CustomFields.Select(c => $"{c.Key}({c.LabelAr})"))}\n" +
                $"column contents:\n{stored}");
        }

        // ---- D2 : query INTO the document, served by an index -------------
        await LoadSyntheticAsync(rec);

        var capture = new SqlCapture();
        List<string> matched;
        await using (var ctx = Contexts.Create(interceptor: capture))
        {
            var sw = Stopwatch.StartNew();
            matched = await ctx.TenantSettings.AsNoTracking()
                .Where(t => t.Settings.Zatca.Environment == "production")
                .Select(t => t.TenantId)
                .Take(5)
                .ToListAsync();
            sw.Stop();
            rec.Evidence($"EF Core LINQ into a NESTED json property returned in {sw.ElapsedMilliseconds} ms " +
                         $"over {SyntheticTenants:N0}+ documents");
        }

        var plan = await capture.ExplainAsync(Config.App);
        var usesIndex = plan.Contains("ix_tenant_settings", StringComparison.OrdinalIgnoreCase);
        rec.Check("D2", "EF Core query into a NESTED jsonb value uses an expression index",
            usesIndex && matched.Count > 0,
            $"EF Core generated SQL:\n  {capture.CommandText?.Replace("\n", "\n  ")}\n" +
            $"EXPLAIN (ANALYZE, BUFFERS):\n  {plan.Replace("\n", "\n  ")}");

        // containment over the whole document, served by the GIN index
        var ginPlan = await ExplainRawAsync(Config.App,
            """select count(*) from app.tenant_settings where settings @> '{"Currency":"USD"}'""");
        rec.Evidence("whole-document containment through the GIN index:\n  " + ginPlan.Replace("\n", "\n  "));

        // ---- D3 : update a NESTED value -----------------------------------
        var capture2 = new SqlCapture();
        await using (var ctx = Contexts.Create(interceptor: capture2))
        {
            var t = await ctx.TenantSettings.SingleAsync(x => x.TenantId == "acme");
            t.Settings.Zatca.Environment = "production";     // sandbox -> simulation -> production
            t.Settings.Zatca.MaxRetries = 7;
            t.Settings.CustomFields.Add(new CustomFieldDef
            {
                Key = "branch_code", LabelAr = "رمز الفرع", LabelEn = "Branch code",
                DataType = "text", Required = false
            });
            t.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // the server-side alternative: change one scalar without shipping the document
        await Sql.ExecAsync(Config.App, """
            update app.tenant_settings
               set settings = jsonb_set(settings, '{Zatca,MaxRetries}', '9'::jsonb, true),
                   updated_at = now()
             where tenant_id = 'globex'
            """);

        await using (var verify = Contexts.Create())
        {
            var a = await verify.TenantSettings.AsNoTracking().SingleAsync(t => t.TenantId == "acme");
            var g = await verify.TenantSettings.AsNoTracking().SingleAsync(t => t.TenantId == "globex");
            var untouched = g.Settings.CustomFields.Single().Key == "fleet_no";
            var ok = a.Settings.Zatca.Environment == "production"
                     && a.Settings.Zatca.MaxRetries == 7
                     && a.Settings.CustomFields.Count == 3
                     && a.Settings.CustomFields.Any(c => c.Key == "branch_code")
                     && a.Settings.Locale == "ar-SA"           // untouched siblings survive
                     && g.Settings.Zatca.MaxRetries == 9
                     && untouched;

            var updateSql = capture2.History.LastOrDefault(h => h.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
                            ?? "(no UPDATE captured)";
            rec.Check("D3", "a NESTED value is updated, in EF Core and server-side with jsonb_set", ok,
                $"EF Core UPDATE it actually sent:\n  {updateSql.Replace("\n", "\n  ")}\n" +
                "HONEST FINDING: EF Core 10 + Npgsql rewrites the WHOLE jsonb column\n" +
                "(SET settings = @p0); it does not emit a partial jsonb_set. For a big\n" +
                "document under frequent small edits, use ExecuteUpdate/raw jsonb_set as\n" +
                "shown for globex below.\n" +
                $"acme  : Zatca.Environment={a.Settings.Zatca.Environment}, Zatca.MaxRetries={a.Settings.Zatca.MaxRetries}, " +
                $"customFields={a.Settings.CustomFields.Count} (added '{a.Settings.CustomFields.Last().Key}')\n" +
                $"globex: patched server-side with jsonb_set -> MaxRetries={g.Settings.Zatca.MaxRetries}, " +
                $"its own custom field '{g.Settings.CustomFields.Single().Key}' untouched");
        }

        // ---- D4 : an untyped escape hatch, no migration, no shared shape ---
        await using (var ctx = Contexts.Create())
        {
            ctx.TenantDocuments.AddRange(
                new TenantDocument
                {
                    TenantId = "acme", DocType = "form-definition", DocKey = "purchase-order",
                    UpdatedAt = DateTime.UtcNow,
                    Doc = """
                    {"locale":"ar-SA","title":"أمر شراء","sections":[{"key":"header","fields":[
                      {"key":"supplier","type":"lookup","required":true},
                      {"key":"delivery_date","type":"date","required":true}]}]}
                    """
                },
                new TenantDocument
                {
                    TenantId = "globex", DocType = "form-definition", DocKey = "purchase-order",
                    UpdatedAt = DateTime.UtcNow,
                    Doc = """
                    {"locale":"en-SA","title":"Purchase order","approvalLadder":[
                      {"level":1,"limit":50000},{"level":2,"limit":250000}],
                      "sections":[{"key":"header","fields":[{"key":"supplier","type":"lookup","required":true}]}]}
                    """
                });
            await ctx.SaveChangesAsync();
        }

        await using (var read = Contexts.Create())
        {
            var arabicForms = await read.TenantDocuments.AsNoTracking()
                .Where(d => EF.Functions.JsonContains(d.Doc, """{"locale":"ar-SA"}"""))
                .Select(d => new { d.TenantId, d.DocKey })
                .ToListAsync();

            var globexDoc = await read.TenantDocuments.AsNoTracking()
                .SingleAsync(d => d.TenantId == "globex" && d.DocKey == "purchase-order");
            using var json = JsonDocument.Parse(globexDoc.Doc);
            var ladder = json.RootElement.GetProperty("approvalLadder").GetArrayLength();

            var columns = await Sql.ScalarAsync<long>(Config.Admin, """
                select count(*) from information_schema.columns
                where table_schema = 'app' and table_name = 'tenant_document'
                """);

            rec.Check("D4", "two tenants, two different document SHAPES, zero schema migrations",
                arabicForms.Count == 1 && arabicForms[0].TenantId == "acme" && ladder == 2 && columns == 5,
                $"acme's purchase-order form has no approvalLadder; globex's has {ladder} levels\n" +
                $"JsonContains filter found: {string.Join(", ", arabicForms.Select(f => f.TenantId + "/" + f.DocKey))}\n" +
                $"app.tenant_document still has exactly {columns} columns - no per-customer DDL was run");
        }
    }

    private static async Task LoadSyntheticAsync(ProofRecorder rec)
    {
        var existing = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from app.tenant_settings");
        if (existing >= SyntheticTenants) return;

        var sw = Stopwatch.StartNew();
        await using var conn = await Sql.OpenAsync(Config.App);
        await using (var writer = await conn.BeginBinaryImportAsync(
            "copy app.tenant_settings (tenant_id, settings, updated_at) from stdin (format binary)"))
        {
            var rnd = new Random(4242);
            for (var i = 0; i < SyntheticTenants; i++)
            {
                // 0.1% are on the production ZATCA portal - selective enough for an index
                var env = rnd.Next(0, 1000) == 0 ? "production" : (rnd.Next(0, 2) == 0 ? "sandbox" : "simulation");
                var cur = rnd.Next(0, 500) == 0 ? "USD" : "SAR";
                var doc = $$"""
                    {"Locale":"ar-SA","Currency":"{{cur}}","FiscalYearStartMonth":1,
                     "CompanyNameAr":"منشأة {{i}}",
                     "Zatca":{"Environment":"{{env}}","VatNumber":"3000000000000{{i % 10}}","PhaseTwoEnabled":true,"MaxRetries":5},
                     "CustomFields":[],"ReportTemplates":[]}
                    """;
                await writer.StartRowAsync();
                await writer.WriteAsync($"synthetic-{i:D6}", NpgsqlDbType.Text);
                await writer.WriteAsync(doc, NpgsqlDbType.Jsonb);
                await writer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz);
            }
            await writer.CompleteAsync();
        }
        sw.Stop();
        await Sql.ExecAsync(Config.Admin, "vacuum analyze app.tenant_settings");
        rec.Evidence($"loaded {SyntheticTenants:N0} synthetic tenant settings documents in {sw.ElapsedMilliseconds} ms " +
                     "(volume only exists so the planner has a realistic reason to pick an index)");
    }

    private static async Task<string> ExplainRawAsync(string cs, string sql)
    {
        await using var conn = await Sql.OpenAsync(cs);
        await using var cmd = new NpgsqlCommand("explain (analyze, buffers) " + sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await r.ReadAsync()) lines.Add(r.GetString(0));
        return string.Join("\n", lines);
    }
}
