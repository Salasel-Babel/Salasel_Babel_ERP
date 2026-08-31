using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.Entitlement;
using Babel.Ledger.Audit;
using Babel.Ledger.Posting;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.Tests;

/// <summary>
/// منفِّذ استحقاق يسمح دائماً. الاستحقاق نفسه مُختبَر في <c>Babel.Core.Tests</c>؛
/// هذه الاختبارات تفحص الدفتر، وخلط الاثنين يجعل فشل أحدهما يُقرأ فشلاً للآخر.
/// </summary>
internal sealed class AlwaysEntitled : IEntitlementEnforcer
{
    public ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success());
}

/// <summary>طباعة الإثبات: كل بند في مهمة الإثبات يُطبع بحكمه وبالدليل الذي أنتجه.</summary>
internal static class Proof
{
    public static void Pass(string title, string evidence)
        => Console.WriteLine($"PASS — {title}\n        الدليل: {evidence}");

    public static void Note(string text) => Console.WriteLine("        " + text);

    public static void Fail(string title, string evidence)
    {
        Console.WriteLine($"FAIL — {title}\n        الدليل: {evidence}");
        throw new InvalidOperationException($"FAIL — {title}: {evidence}");
    }

    public static void Require(bool condition, string title, string evidence)
    {
        if (condition)
        {
            Pass(title, evidence);
        }
        else
        {
            Fail(title, evidence);
        }
    }

    public static string Money(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}

/// <summary>بناء طلبات الترحيل للاختبارات — بمفردات الحدث لا بأرقام حسابات.</summary>
internal static class Requests
{
    /// <summary>المركز الافتراضي لأول مركز تكلفة في أي منشأة — <c>cc.001</c>.</summary>
    public const string DefaultCostCenter = "cc.001";

    public static PostingRequest RentInvoice(
        Guid tenant,
        string documentId,
        decimal net,
        decimal tax,
        DateOnly date,
        string property = LedgerTestEnvironment.OwnProperty,
        bool taxable = true)
        => new()
        {
            Tenant = new TenantId(tenant),
            IdempotencyKey = new IdempotencyKey("rent-invoice:" + documentId),
            Source = new SourceDocument(BabelModule.RealEstate, "RentInvoice", documentId),
            Trigger = PostingTrigger.OnApproval,
            DocumentDate = date,
            Narration = new LocalizedName("فاتورة إيجار دورية", "Periodic rent invoice"),
            Lines = [],
            Event = new PostingEventCode("realestate.rent_invoice.own_property"),
            Amounts =
            [
                new PostingAmount("net", SharedKernel.Money.Of(net, CurrencyCode.Sar)),
                new PostingAmount("tax", SharedKernel.Money.Of(tax, CurrencyCode.Sar)),
            ],
            Facts =
            [
                new PostingFact("unit.vat_treatment", taxable ? "standard" : "exempt"),
                new PostingFact("subledger.tenant", "TEN-" + documentId),
                new PostingFact("subledger.lease_contract", "LC-" + documentId),
            ],
            Dimensions =
            [
                // ‏**مركز التكلفة مُحلّ قبل بناء الطلب** (ADR-0026). وهذه الاختبارات تفحص
                // الدفتر لا البوّابة، فهي تُسلّم ما تُسلّمه البوّابة: رمزاً مُحلّاً. ومن
                // يبني طلباً بلا مركز يُرفض بـledger.posting.missing_cost_center، وذاك
                // مُثبَت في CostCenterIsNeverAbsentTests.
                new PostingDimension("cost_center", DefaultCostCenter),
                new PostingDimension("property", property),
                new PostingDimension("unit", "U-01"),
            ],
            Currency = CurrencyCode.Sar,
            Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
        };

    /// <summary>استحقاق الأجرة الشهري — الحدث الذي يمسّ دور <c>rental_revenue</c>.</summary>
    public static PostingRequest RentAccrual(
        Guid tenant,
        string documentId,
        decimal periodShare,
        DateOnly date,
        string property)
        => new()
        {
            Tenant = new TenantId(tenant),
            IdempotencyKey = new IdempotencyKey("rent-accrual:" + documentId),
            Source = new SourceDocument(BabelModule.RealEstate, "RentAccrual", documentId),
            Trigger = PostingTrigger.Periodic,
            DocumentDate = date,
            Narration = new LocalizedName("استحقاق أجرة شهري", "Monthly rent accrual"),
            Lines = [],
            Event = new PostingEventCode("realestate.rent.accrual.own_property"),
            Amounts = [new PostingAmount("period_share", SharedKernel.Money.Of(periodShare, CurrencyCode.Sar))],
            Facts = [new PostingFact("subledger.lease_contract", "LC-" + documentId)],
            Dimensions =
            [
                // ‏**مركز التكلفة مُحلّ قبل بناء الطلب** (ADR-0026). وهذه الاختبارات تفحص
                // الدفتر لا البوّابة، فهي تُسلّم ما تُسلّمه البوّابة: رمزاً مُحلّاً. ومن
                // يبني طلباً بلا مركز يُرفض بـledger.posting.missing_cost_center، وذاك
                // مُثبَت في CostCenterIsNeverAbsentTests.
                new PostingDimension("cost_center", DefaultCostCenter),
                new PostingDimension("property", property),
                new PostingDimension("unit", "U-01"),
            ],
            Currency = CurrencyCode.Sar,
            Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
        };
}

/// <summary>وصلة اختبار إلى الدفتر: الخدمات مركّبة يدوياً بلا حاوية.</summary>
internal sealed class LedgerHarness
{
    private static LedgerHarness? _shared;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private LedgerHarness(LedgerRuntime runtime)
    {
        Runtime = runtime;
        Posting = new PostingService(new AlwaysEntitled(), runtime);
        Auditing = new LedgerAuditService(new AlwaysEntitled(), runtime);
    }

    public LedgerRuntime Runtime { get; }

    public PostingService Posting { get; }

    public LedgerAuditService Auditing { get; }

    /// <summary>
    /// وصلة واحدة لكل عملية. إنشاء <c>LedgerRuntime</c> لكل اختبار يعني مجمّع اتصالات
    /// لكل اختبار، وPostgreSQL يرفض عند سقف <c>max_connections</c> برمز 53300 —
    /// وهو رفض بيئة اختبار لا عيب في المحرك، لكنه يُخفي ما نريد قياسه.
    /// </summary>
    public static async Task<LedgerHarness> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_shared is not null)
        {
            return _shared;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_shared is null)
            {
                await LedgerTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);
                _shared = new LedgerHarness(new LedgerRuntime(LedgerTestEnvironment.Options));
            }

            return _shared;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static LedgerHarness? _v1;

    /// <summary>
    /// وصلة ثانية تكتب القيود بـ<b>الشكل القانوني v1</b>.
    /// <para>
    /// وجودها ليس ترفاً: إثبات أن الثغرة كانت حقيقية يقتضي <b>كتابة سلسلة v1
    /// فعلية</b> والعبث بها وإظهار أن التحقق يقول «سليمة». ادّعاء ثغرة بلا سلسلة
    /// تُظهرها ادّعاء، لا دليل. ومجمّع اتصالاتها صغير عمداً: مجمّع لكل اختبار هو
    /// ما يُسقط PostgreSQL برمز 53300.
    /// </para>
    /// </summary>
    public static async Task<LedgerHarness> CreateV1Async(CancellationToken cancellationToken = default)
    {
        if (_v1 is not null)
        {
            return _v1;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_v1 is null)
            {
                await LedgerTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);
                _v1 = new LedgerHarness(new LedgerRuntime(new LedgerOptions
                {
                    OwnerConnectionString = LedgerTestEnvironment.Options.OwnerConnectionString,
                    AppConnectionString = LedgerTestEnvironment.Options.AppConnectionString + ";Maximum Pool Size=5",
                    AppRole = LedgerTestEnvironment.Options.AppRole,
                    CompanyCurrency = LedgerTestEnvironment.Options.CompanyCurrency,
                    CanonVersion = "v1",
                }));
            }

            return _v1;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static NpgsqlConnection OpenOwner()
    {
        NpgsqlConnection connection = new(LedgerTestEnvironment.Options.OwnerConnectionString);
        connection.Open();
        return connection;
    }

    public static NpgsqlConnection OpenApp()
    {
        NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        connection.Open();
        return connection;
    }
}
