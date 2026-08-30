using System.Globalization;
using Babel.Contracts.Subledger;
using Babel.Core;
using Babel.Core.Audit;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.Ledger;
using Babel.Ledger.Audit;
using Babel.Purchasing;
using Babel.Sales;
using Babel.Sales.Application;
using PayablesService = Babel.Purchasing.Application.PayablesService;
using PurchasingAging = Babel.Purchasing.Application.AgingReport;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// الخطوة الرابعة: الإثبات — <b>ثلاثة أحكام بأرقامها، لا كلمة «تمّ»</b>.
/// <para>
/// وكلّها تُقرأ بدور التطبيق نفسه الذي لا يملك <c>UPDATE</c> ولا <c>DELETE</c>: أي أن
/// أداة التحقّق لا تستطيع أن تُصلح ما تكتشفه، وهذا هو المطلوب بالضبط.
/// </para>
/// </summary>
internal static class Verify
{
    /// <summary>يُشغّل الإثباتات الثلاثة ويرمي عند أول سقوط.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("الإثبات / proof");

        using LedgerRuntime ledger = new(settings.Ledger);
        InMemoryUsageStore usage = new();
        InMemoryAuditLog audit = new();
        InMemoryEntitlementService entitlements = new(audit, TimeProvider.System);
        // مخزن التأسيس **هو مخزن الخادم نفسه**: PostgreSQL، بالطريق المعلَن
        // AddBabelCore(options) لا بنوع داخلي. ولذلك تقرأ خطوة الإثبات ما بذرته خطوة
        // البذر في **عملية أخرى** — وهو بعينه ما يفعله الخادم بعد إعادة الإقلاع.
        ServiceCollection coreServices = new();
        coreServices.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        using ServiceProvider coreProvider = coreServices.BuildServiceProvider();
        ICompanySetupStore setupStore = coreProvider.GetRequiredService<ICompanySetupStore>();
        EntitlementEnforcer enforcer = new(entitlements, usage, TimeProvider.System);
        LedgerAuditService auditor = new(enforcer, ledger);

        TenantId tenant = new(settings.Company);

        // ── التأسيس أولاً — لأن الإثبات يبني الوحدتين، والوحدتان تحملان الحالّ ──────
        // مخزن التأسيس في هذه الموجة عمرُه عمرُ العملية (InMemoryCompanySetupStore)، وخطوة
        // «الإثبات» تُستدعى وحدها بـ`verify` كما تُستدعى ضمن `all` — فتؤسّس في مخزنها كما
        // يؤسّس البذر في مخزنه، **من الإعلان نفسه**. ولا حالّ صوري هنا: هذا
        // CostCenterResolver الإنتاجي فوق سجلٍّ فيه المنشأة فعلاً، فلو رحّل أحدٌ يوماً من
        // هذا المسار لأجابه بالمركز الصحيح بدل أن يمرّ بكذبةٍ مكتوبة «لا تُستدعى هنا».
        CompanySetupService setup = new(setupStore, enforcer, audit, TimeProvider.System);
        FoundedCompany founded = await Founding
            .EnsureAsync(setup, tenant, Seed.Actor, cancellationToken)
            .ConfigureAwait(false);

        Say.Detail(
            "تأسيس المنشأة مقروءاً: «" + founded.NameAr + "» · المركز الافتراضي "
            + founded.CostCenters.Default);

        // ── ١ · ميزان المراجعة يتوازن ──────────────────────────────────────
        // من **السطور غير القابلة للتعديل** لا من جدول الأرصدة: جدول الأرصدة إسقاط،
        // والسطور هي الحقيقة (ADR-0004 · فخ-06).
        Result<TrialBalanceReport> trial = await auditor
            .TrialBalanceFromLinesAsync(tenant, Seed.Actor, Settings.Book, null, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(trial.IsSuccess, "ميزان المراجعة يُقرأ", Describe(trial));

        TrialBalanceReport report = trial.Value;

        Say.Require(
            report.Balanced && report.TotalDebit == report.TotalCredit,
            "ميزان المراجعة متوازن — مجموع المدين = مجموع الدائن",
            "مدين=" + Say.Money(report.TotalDebit)
            + " · دائن=" + Say.Money(report.TotalCredit)
            + " · الفرق=" + Say.Money(report.TotalDebit - report.TotalCredit)
            + " · صفوف=" + Say.Count(report.Rows.Count));

        Say.Require(
            report.Rows.Count >= 8,
            "الميزان يحمل حسابات تكفي لأن يبدو نشاطاً لا نموذجاً",
            Say.Count(report.Rows.Count) + " حساباً بحركة");

        foreach (TrialBalanceRow row in report.Rows.Take(40))
        {
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"       {row.AccountCode,-8} {Say.Money(row.Debit),18} {Say.Money(row.Credit),18}  {row.Name.Arabic}"));
        }

        // ── ٢ · سلسلة البصمات تتحقّق من التكوين إلى الرأس ──────────────────
        Result<LedgerChainReport> chain = await auditor
            .VerifyChainAsync(tenant, Seed.Actor, Settings.Book, settings.FiscalYear, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(chain.IsSuccess, "سلسلة البصمات تُقرأ", Describe(chain));

        Say.Require(
            chain.Value.Ok,
            "سلسلة البصمات سليمة من بصمة التكوين حتى الرأس",
            "الحكم=" + chain.Value.Verdict
            + " · فُحص=" + Say.Count(chain.Value.Checked)
            + " · أول انحراف=" + (chain.Value.FirstDivergentSequence?.ToString(CultureInfo.InvariantCulture) ?? "لا شيء")
            + " · " + chain.Value.ReasonAr);

        Say.Require(
            chain.Value.Checked >= 40,
            "السلسلة المفحوصة ليست ضامرة — الحكم على عدد حقيقي من القيود",
            Say.Count(chain.Value.Checked) + " قيداً في السلسلة");

        // ── ٣ · أعمار الذمم: مدينة ودائنة ───────────────────────────────────
        // الحالّ نفسه للوحدتين: قاعدة الحلّ واحدة في النواة، ونسخةٌ ثانية منها هي
        // الشيء الذي وُجدت ICostCenterResolver لمنعه (ADR-0026).
        CostCenterResolver costCentres = new(setupStore);

        using SalesRuntime sales = new(settings.SalesOwner, costCentres);
        using PurchasingRuntime purchasing = new(settings.PurchasingOwner, costCentres);

        DateOnly asOf = new(settings.FiscalYear, 8, 31);

        ReceivablesService receivables = new(
            enforcer, sales, new ControlPoint(settings.Ledger.AppConnectionString));
        Result<AgingReport> ar = await receivables
            .AgingAsync(tenant, Seed.Actor, asOf, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(ar.IsSuccess, "تقرير أعمار الذمم المدينة يُقرأ", Describe(ar));
        PrintAging("الذمم المدينة (العملاء)", ar.Value);

        PayablesService payables = new(
            enforcer, purchasing, new ControlPoint(settings.Ledger.AppConnectionString));
        Result<PurchasingAging> ap = await payables
            .AgingAsync(tenant, Seed.Actor, asOf, cancellationToken)
            .ConfigureAwait(false);

        Say.Require(ap.IsSuccess, "تقرير أعمار الذمم الدائنة يُقرأ", DescribePurchasing(ap));
        PrintPayableAging("الذمم الدائنة (الموردون)", ap.Value);

        Say.Require(
            ar.Value.Totals.Total.Amount > 0m,
            "الذمم المدينة القائمة ليست صفراً — العرض يُظهر أعماراً لا جدولاً فارغاً",
            "الإجمالي=" + Say.Money(ar.Value.Totals.Total.Amount));
    }

    private static void PrintAging(string title, AgingReport report)
    {
        AgingBuckets totals = report.Totals;
        Console.WriteLine("     " + title + " كما في " + report.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"       أطراف={report.Parties.Count} · لم يستحق={Say.Money(totals.NotDue.Amount)}"
            + $" · 1–30={Say.Money(totals.Days1To30.Amount)} · 31–60={Say.Money(totals.Days31To60.Amount)}"
            + $" · 61–90={Say.Money(totals.Days61To90.Amount)} · +90={Say.Money(totals.Over90.Amount)}"
            + $" · الإجمالي={Say.Money(totals.Total.Amount)}"));
    }

    private static void PrintPayableAging(string title, PurchasingAging report)
    {
        Babel.Purchasing.Application.AgingBuckets totals = report.Totals;
        Console.WriteLine("     " + title + " كما في " + report.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"       أطراف={report.Parties.Count} · لم يستحق={Say.Money(totals.NotDue.Amount)}"
            + $" · 1–30={Say.Money(totals.Days1To30.Amount)} · 31–60={Say.Money(totals.Days31To60.Amount)}"
            + $" · 61–90={Say.Money(totals.Days61To90.Amount)} · +90={Say.Money(totals.Over90.Amount)}"
            + $" · الإجمالي={Say.Money(totals.Total.Amount)}"));
    }

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));

    private static string DescribePurchasing<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));

    /// <summary>
    /// قارئ نقطة الضبط — <b>واحدٌ للدفاتر المساعدة كلّها</b>.
    /// <para>
    /// كان صنفين متطابقين لأن العقد كان معلَناً في كل وحدة على حدة. وبعد أن صار
    /// <see cref="Babel.Contracts.Subledger.IControlPointReader"/> معلَناً في العقود
    /// مرّةً واحدة، صار المحوّل واحداً كذلك — وهو ما كان يجب أن يكون
    /// (<c>docs/evidence/traps.md#fakh-81</c>).
    /// </para>
    /// <para>
    /// <b>ولا يسمّي حساباً واحداً</b>: الاستعلام على <c>subledger_kind</c>، فأي حساب
    /// ضابط يُضاف لاحقاً لهذا الدفتر المساعد يدخل المطابقة من تلقاء نفسه (القاعدة 2).
    /// </para>
    /// </summary>
    private sealed class ControlPoint(string connectionString) : IControlPointReader
    {
        public async ValueTask<Result<ControlPointSnapshot>> ReadAsync(
            TenantId tenant,
            string subledgerKind,
            DateOnly asOf,
            BabelModule? writtenBy = null,
            CancellationToken cancellationToken = default)
        {
            // أداة العرض تقرأ نقطة الضبط كاملةً — لا وحدة لها هي تُقصيها.
            _ = writtenBy;

            List<ControlPointMovement> movements = [];
            decimal net = 0m;

            await foreach ((string type, string id, string party, decimal value)
                           in ReadMovementsAsync(connectionString, tenant, subledgerKind, asOf, cancellationToken)
                               .ConfigureAwait(false))
            {
                net += value;
                movements.Add(new ControlPointMovement(type, id, party, value));
            }

            return Result<ControlPointSnapshot>.Success(new ControlPointSnapshot(net, movements));
        }
    }

    private static async IAsyncEnumerable<(string Type, string Id, string Party, decimal Net)> ReadMovementsAsync(
        string connectionString,
        TenantId tenant,
        string subledgerKind,
        DateOnly asOf,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select e.source_doc_type,
                   e.source_doc_id,
                   coalesce(l.subledger_party_id, ''),
                   sum(l.debit_company - l.credit_company)
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where l.company_id = $1
               and l.subledger_kind = $2
               and e.entry_date <= $3
             group by e.source_doc_type, e.source_doc_id, coalesce(l.subledger_party_id, '')
             order by e.source_doc_type, e.source_doc_id
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(subledgerKind);
        command.Parameters.AddWithValue(asOf);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3));
        }
    }
}
