using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>
/// هوية الترحيل في <b>بوابة الوحدة</b> — لا في الدفتر وحده.
/// <para>
/// البوابة تحتفظ بجدول محاولات خاصّ بها وتُقصِّر الطريق <b>قبل</b> بلوغ الدفتر أصلاً.
/// فما دامت هويتها خماسية بلا رمز حدث، فإصلاح الدفتر لا يُغيّر شيئاً: الحدث الثاني
/// للمستند الواحد عند الإطلاق الواحد يُبتلع في طبقة الوحدة، ويعود إيصالاً يحمل
/// <c>WasAlreadyPosted = true</c> ومعرّف <b>القيد الأول</b>.
/// </para>
/// </summary>
[Collection("receivables")]
public sealed class PostingIdentityIncludesEventCodeTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 12);
    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Next(string prefix)
        => prefix + "-EVT-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · مستند واحد · إطلاق واحد · جيل واحد · حدثان مختلفان ⇒ قيدان في الدفتر
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Two_different_events_of_one_document_at_one_trigger_both_reach_the_ledger()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.GatewayTenant;
        string code = Next("CUS");

        Guid document = Guid.CreateVersion7();
        SubledgerPostingGateway gateway = _harness.Gateway;

        Result<PostingReceipt> revenue = await gateway.PostAsync(RevenueIntent(tenant, document, code), token);
        Assert.True(revenue.IsSuccess, Describe(revenue.Errors));

        Result<PostingReceipt> cost = await gateway.PostAsync(CostIntent(tenant, document), token);
        Assert.True(cost.IsSuccess, Describe(cost.Errors));

        long entries = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString,
            tenant,
            SalesInvoiceService.InvoiceDocument,
            document.ToString("D", CultureInfo.InvariantCulture),
            token);

        decimal revenueDebit = await DebitOfEntryAsync(revenue.Value.JournalEntryId, token);
        decimal costDebit = await DebitOfEntryAsync(cost.Value.JournalEntryId, token);

        Proof.Note(
            "إيصال الحدث الثاني: WasAlreadyPosted=" + cost.Value.WasAlreadyPosted
            + " · LineCount=" + cost.Value.LineCount.ToString(CultureInfo.InvariantCulture)
            + " · معرّفه يساوي معرّف الأول؟ " + (cost.Value.JournalEntryId == revenue.Value.JournalEntryId));

        Proof.Require(
            entries == 2
            && !cost.Value.WasAlreadyPosted
            && cost.Value.JournalEntryId != revenue.Value.JournalEntryId
            && revenueDebit == 11500.0000m
            && costDebit == 6400.0000m,
            "حدثان مختلفان لمستند واحد عند إطلاق واحد يصلان الدفتر قيدين مستقلَّين بمبلغيهما",
            "قيود المستند=" + entries.ToString(CultureInfo.InvariantCulture)
            + " · مدين قيد الإيراد=" + Proof.Money(revenueDebit)
            + " · مدين قيد التكلفة=" + Proof.Money(costDebit));
    }


    // ═══════════════════════════════════════════════════════════════════════
    // 2 · الإحكام لم يُكسر: الحدث نفسه مرّتين ⇒ قيد واحد بمبلغ غير مضاعف
    // ═══════════════════════════════════════════════════════════════════════
    //
    // توسيع مفتاح الهوية يُصلح الضياع الصامت، ويستطيع أن يُنتج عطباً مقابلاً أسوأ
    // في محاسبة: ازدواج صامت. هذا البند يُثبت أن ذلك لم يقع.
    [Fact]
    public async Task The_same_event_arriving_twice_still_writes_exactly_one_entry()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.GatewayTenant;
        string party = Next("CUS");
        Guid document = Guid.CreateVersion7();

        Result<PostingReceipt> first = await _harness.Gateway.PostAsync(RevenueIntent(tenant, document, party), token);
        Assert.True(first.IsSuccess, Describe(first.Errors));

        Result<PostingReceipt> again = await _harness.Gateway.PostAsync(RevenueIntent(tenant, document, party), token);
        Assert.True(again.IsSuccess, Describe(again.Errors));

        long entries = await LedgerProbe.EntryCountAsync(
            SalesTestEnvironment.Ledger.AppConnectionString,
            tenant,
            SalesInvoiceService.InvoiceDocument,
            document.ToString("D", CultureInfo.InvariantCulture),
            token);

        decimal debit = await DebitOfEntryAsync(first.Value.JournalEntryId, token);
        long attempts = await AttemptRowsAsync(tenant, document, token);

        Proof.Require(
            entries == 1
            && again.Value.WasAlreadyPosted
            && again.Value.LineCount == 0
            && again.Value.JournalEntryId == first.Value.JournalEntryId
            && debit == 11500.0000m
            && attempts == 1,
            "الوصول الثاني بالهوية نفسها لا يكتب شيئاً، والمبلغ لم يتضاعف",
            "قيود المستند=" + entries.ToString(CultureInfo.InvariantCulture)
            + " · صفوف المحاولة=" + attempts.ToString(CultureInfo.InvariantCulture)
            + " · مدين القيد=" + Proof.Money(debit)
            + " · WasAlreadyPosted=" + again.Value.WasAlreadyPosted);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · نية بلا رمز حدث تُرفض في البوابة قبل أن تُكتب محاولة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_intent_without_an_event_code_is_refused_before_any_attempt_row_is_written()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.GatewayTenant;
        string party = Next("CUS");
        Guid document = Guid.CreateVersion7();

        PostingIntent blank = RevenueIntent(tenant, document, party) with { Event = PostingEventCode.None };

        Result<PostingReceipt> refused = await _harness.Gateway.PostAsync(blank, token);
        long attempts = await AttemptRowsAsync(tenant, document, token);

        Proof.Require(
            refused.IsFailure
            && refused.Errors[0].Code == "sales.posting.missing_event_code"
            && attempts == 0,
            "نية بلا رمز حدث مرفوضة، ولا صفّ محاولة يُكتب بهوية ناقصة",
            "الرمز=" + (refused.IsFailure ? refused.Errors[0].Code : "(نجح!)")
            + " · صفوف المحاولة=" + attempts.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · الحرّاس الثلاثة في القاعدة ليسوا زخرفاً — انتهاك حقيقي يُلتقط
    // ═══════════════════════════════════════════════════════════════════════
    //
    // المجموعة المفحوصة غير فارغة بالبناء: الاختبار يبني صفّه بنفسه من صفّ قائم،
    // ويؤكّد وجوده قبل أن ينتهك. قاعدة اجتازت بلا صفوف تُثبت أن لا شيء فيها.
    [Fact]
    public async Task The_identity_index_and_the_event_code_guards_catch_a_real_violation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.GatewayTenant;
        string party = Next("CUS");
        Guid document = Guid.CreateVersion7();

        Result<PostingReceipt> seed = await _harness.Gateway.PostAsync(RevenueIntent(tenant, document, party), token);
        Assert.True(seed.IsSuccess, Describe(seed.Errors));

        await using NpgsqlConnection connection = new(SalesTestEnvironment.Sales.ConnectionString);
        await connection.OpenAsync(token);

        string indexDefinition = await ScalarTextAsync(
            connection,
            "select indexdef from pg_indexes where schemaname = 'sales' and indexname = 'uq_sales_posting_identity'",
            token);

        bool notNull = await ScalarBoolAsync(
            connection,
            """
            select a.attnotnull
              from pg_attribute a
             where a.attrelid = 'sales.document_posting'::regclass
               and a.attname = 'EventCode'
            """,
            token);

        long population = await ScalarLongAsync(
            connection, "select count(*) from sales.document_posting", token);

        string duplicate = await ViolationAsync(connection, Duplicate(document), token);
        string blank = await ViolationAsync(connection, Blank(document), token);
        string nulled = await ViolationAsync(connection, Nulled(document), token);

        Proof.Require(
            population > 0
            && indexDefinition.Contains("EventCode", StringComparison.Ordinal)
            && notNull
            && duplicate == "23505"
            && blank == "23514"
            && nulled == "23502",
            "الفهرس والقيدان يلتقطون انتهاكاً حقيقياً، والمجموعة المفحوصة غير فارغة",
            "صفوف الجدول=" + population.ToString(CultureInfo.InvariantCulture)
            + " · تكرار الهوية=" + duplicate
            + " · رمز فارغ=" + blank
            + " · رمز معدوم=" + nulled
            + " · الفهرس الحيّ=" + indexDefinition);
    }

    /// <summary>ينسخ صفّ المستند بمعرّف جديد فقط — هوية مكرّرة حرفياً.</summary>
    private static string Duplicate(Guid document) => Clone(document, null, false);

    /// <summary>نسخة برمز حدث فارغ — تُعيد تركيب العطب داخل مفتاح موسَّع.</summary>
    private static string Blank(Guid document) => Clone(document, "''", false);

    /// <summary>نسخة برمز حدث معدوم — يجب أن يرفضها NOT NULL.</summary>
    private static string Nulled(Guid document) => Clone(document, null, true);

    private static string Clone(Guid document, string? eventCode, bool nulled)
    {
        string assignment = nulled
            ? @", ""EventCode"" = null"
            : eventCode is null ? string.Empty : @", ""EventCode"" = " + eventCode;

        return $"""
            create temporary table probe on commit drop as
                select * from sales.document_posting
                 where "DocumentId" = '{document:D}' limit 1;
            update probe set "Id" = gen_random_uuid(){assignment};
            insert into sales.document_posting select * from probe;
            """;
    }

    /// <summary>ينفّذ انتهاكاً داخل معاملة تُلغى دائماً، ويُعيد SQLSTATE الملتقَط.</summary>
    private static async Task<string> ViolationAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        try
        {
            await using NpgsqlCommand command = new(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(token);
            return "(لم يُرفض)";
        }
        catch (PostgresException failure)
        {
            return failure.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    private static async Task<long> AttemptRowsAsync(TenantId tenant, Guid document, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(SalesTestEnvironment.Sales.ConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(
            """
            select count(*) from sales.document_posting
             where "TenantId" = $1 and "DocumentId" = $2
            """, connection);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(document.ToString("D", CultureInfo.InvariantCulture));
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private static async Task<string> ScalarTextAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (await command.ExecuteScalarAsync(token)) as string ?? "(لا فهرس)";
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (await command.ExecuteScalarAsync(token)) is true;
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }


    // ═══════════════════════════════════════════════════════════════════════
    // 5 · الترقية على قاعدة قائمة مملوءة — لا على قاعدة فارغة
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏EnsureCreated ينشئ الشكل الصحيح في قاعدة فارغة **ولا يفعل شيئاً في قاعدة
    // قائمة**. فقاعدة عميل مُنشأة قبل هذا التغيير لن ترى المفتاح الموسَّع ولا قيد
    // التحقق ما لم يُشغَّل نصّ الترقية. هذا البند يبني قاعدة بالشكل القديم بالضبط،
    // يملؤها بصفوف، ثم يستدعي الناشر ويقرأ النتيجة من كتالوج PostgreSQL.
    [Fact]
    public async Task The_deployer_upgrades_an_existing_populated_database_without_losing_a_row()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string Probe = "babel_arap_sales_upgrade_probe";

        string admin = SalesTestEnvironment.Maintenance;
        string probeConnection = $"Host=127.0.0.1;Port=5432;Database={Probe};Username=postgres;Include Error Detail=true";

        await using (NpgsqlConnection maintenance = new(admin))
        {
            await maintenance.OpenAsync(token);
            await ExecuteAsync(maintenance, $"drop database if exists {Probe} with (force)", token);
            await ExecuteAsync(maintenance, $"create database {Probe}", token);
        }

        try
        {
            SalesOptions options = new() { ConnectionString = probeConnection, CompanyCurrency = "SAR" };

            // (أ) الشكل الحالي، ثم **إرجاعه إلى شكل ما قبل الإصلاح بالضبط**:
            //     مفتاح خماسي بلا رمز حدث، ولا قيد تحقّق على الرمز.
            await SalesSchemaDeployer.DeployAsync(options, token);

            await using NpgsqlConnection probe = new(probeConnection);
            await probe.OpenAsync(token);

            await ExecuteAsync(
                probe,
                """
                alter table sales.document_posting drop constraint ck_sales_document_posting_event_code;
                drop index sales.uq_sales_posting_identity;
                create unique index uq_sales_posting_identity on sales.document_posting
                    ("TenantId", "DocumentType", "DocumentId", "TriggerCode", "Generation");
                """,
                token);

            // (ب) صفوف حقيقية بالشكل القديم — قاعدة مملوءة لا فارغة.
            for (int index = 1; index <= 3; index++)
            {
                await ExecuteAsync(
                    probe,
                    $"""
                    insert into sales.document_posting
                        ("Id", "TenantId", "DocumentType", "DocumentId", "TriggerCode", "Generation",
                         "IdempotencyKey", "EventCode", "PartyId", "DocumentDate", "State",
                         "ControlEffect", "EntryNumber", "FailureCode", "FailureMessageAr",
                         "FailureMessageEn", "AttemptCount", "LastAttemptAt")
                    values (gen_random_uuid(), gen_random_uuid(), 'Probe', '{index}', 'OnApproval', 1,
                            'probe:{index}', 'sales.invoice.posted', 'P', date '2026-03-01', 'POSTED',
                            0, {index}, '', '', '', 1, now())
                    """,
                    token);
            }

            long before = await CountAsync(probe, "select count(*) from sales.document_posting", token);
            string indexBefore = await IndexOfAsync(probe, token);

            // (ج) الناشر يُستدعى على القاعدة القائمة — وهذا بالضبط ما يفعله النشر.
            await SalesSchemaDeployer.DeployAsync(options, token);

            long after = await CountAsync(probe, "select count(*) from sales.document_posting", token);
            string indexAfter = await IndexOfAsync(probe, token);
            long constraints = await CountAsync(
                probe,
                "select count(*) from pg_constraint where conname = 'ck_sales_document_posting_event_code'",
                token);

            Proof.Require(
                before == 3
                && after == 3
                && !indexBefore.Contains("EventCode", StringComparison.Ordinal)
                && indexAfter.Contains("EventCode", StringComparison.Ordinal)
                && constraints == 1,
                "الترقية تعمل على قاعدة قائمة مملوءة: الصفوف كما هي، والمفتاح والقيد صارا حيَّين",
                "الصفوف قبل=" + before.ToString(CultureInfo.InvariantCulture)
                + " وبعد=" + after.ToString(CultureInfo.InvariantCulture)
                + " · المفتاح قبل=" + indexBefore
                + " · المفتاح بعد=" + indexAfter
                + " · قيود التحقق=" + constraints.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            await using NpgsqlConnection maintenance = new(admin);
            await maintenance.OpenAsync(token);
            await ExecuteAsync(maintenance, $"drop database if exists {Probe} with (force)", token);
        }
    }

    private static async Task<string> IndexOfAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using NpgsqlCommand command = new(
            "select indexdef from pg_indexes where schemaname = 'sales' and indexname = 'uq_sales_posting_identity'", connection);
        return (await command.ExecuteScalarAsync(token)) as string ?? "(لا فهرس)";
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }


    // ═══════════════════════════════════════════════════════════════════════
    // 6 · مفتاح الحصانة يفصل ما تفصله الهوية، ولا يخلط ما يفصله فاصل
    // ═══════════════════════════════════════════════════════════════════════
    //
    // مفتاح مبني بالوصل على فاصل قد يحتويه أحد المكوّنات هو عطب تصادم بذاته، وقد
    // لُدغ هذا المستودع به في source_ref المدموج حيث أنتج ("A/B","C") و("A","B/C")
    // البايتات نفسها. الترميز بسابقة طول يقطع ذلك من جذره.
    [Fact]
    public void The_idempotency_key_separates_what_identity_separates_and_never_collides_on_a_delimiter()
    {
        Guid document = new("0199a0f0-0000-7000-8000-000000000001");

        string revenue = SubledgerPostingGateway.IdempotencyKeyOf(
            "SalesInvoice", document, PostingTrigger.OnApproval, 1, new PostingEventCode("sales.invoice.posted"));

        string cost = SubledgerPostingGateway.IdempotencyKeyOf(
            "SalesInvoice", document, PostingTrigger.OnApproval, 1, new PostingEventCode("sales.invoice.cost_of_sales"));

        string revenueAgain = SubledgerPostingGateway.IdempotencyKeyOf(
            "SalesInvoice", document, PostingTrigger.OnApproval, 1, new PostingEventCode("sales.invoice.posted"));

        // الحدّ المزوَّر: النوع ينتهي بفاصل والحدث يبدأ به، والعكس. سلسلةٌ موصولة
        // بفاصل تُنتج البايتات نفسها للحالتين؛ الترميز بالطول لا يستطيع ذلك.
        string leftHeavy = SubledgerPostingGateway.IdempotencyKeyOf(
            "A:B", document, PostingTrigger.OnApproval, 1, new PostingEventCode("C"));

        string rightHeavy = SubledgerPostingGateway.IdempotencyKeyOf(
            "A", document, PostingTrigger.OnApproval, 1, new PostingEventCode("B:C"));

        // والحدّ الأقصى المشروع في العقد: نوع 64 محرفاً وحدث 128 — والمفتاح يجب أن
        // يبقى مقبولاً في IdempotencyKey الذي يحدّه بـ128.
        string longest = SubledgerPostingGateway.IdempotencyKeyOf(
            new string('T', 64), document, PostingTrigger.OnApproval, 99, new PostingEventCode(new string('e', 128)));

        IdempotencyKey accepted = new(longest);

        Proof.Require(
            revenue != cost
            && string.Equals(revenue, revenueAgain, StringComparison.Ordinal)
            && leftHeavy != rightHeavy
            && longest.Length <= 128
            && accepted.Value.Length == longest.Length,
            "المفتاح يفصل الحدثين، ويثبت لنفس الهوية، ولا يتصادم على فاصل، ويسع أطول مكوّنات مشروعة",
            "طول المفتاح=" + longest.Length.ToString(CultureInfo.InvariantCulture)
            + " · مفتاح الحدث الأول=" + revenue
            + " · مفتاح الحدث الثاني=" + cost);
    }

    private static PostingIntent RevenueIntent(TenantId tenant, Guid document, string customerCode) => new()
    {
        Tenant = tenant,
        DocumentType = SalesInvoiceService.InvoiceDocument,
        DocumentId = document,
        Trigger = PostingTrigger.OnApproval,
        Event = new PostingEventCode("sales.invoice.posted"),
        DocumentDate = March,
        Narration = new LocalizedName("إيراد فاتورة اختبار الهوية", "Revenue of the identity test invoice"),
        Amounts =
        [
            new PostingAmount("net", Harness.Sar(10000m)),
            new PostingAmount("tax", Harness.Sar(1500m)),
        ],
        Facts =
        [
            new PostingFact("condition.is_taxable_supply", "true"),
            new PostingFact("subledger.customer", customerCode),
            new PostingFact("line.item_group", "*"),
        ],
        Dimensions = [new PostingDimension("branch", "BR-01")],
        PartyId = customerCode,
        ControlEffect = 11500m,
        Currency = CurrencyCode.Sar,
        Actor = Harness.Actor,
    };

    private static PostingIntent CostIntent(TenantId tenant, Guid document) => new()
    {
        Tenant = tenant,
        DocumentType = SalesInvoiceService.InvoiceDocument,
        DocumentId = document,
        Trigger = PostingTrigger.OnApproval,
        Event = new PostingEventCode("sales.invoice.cost_of_sales"),
        DocumentDate = March,
        Narration = new LocalizedName("تكلفة فاتورة اختبار الهوية", "Cost of the identity test invoice"),
        Amounts = [new PostingAmount("cost", Harness.Sar(6400m))],
        Facts =
        [
            new PostingFact("subledger.item", "ITEM-EVT"),
            new PostingFact("line.item_group", "*"),
        ],
        Dimensions =
        [
            new PostingDimension("branch", "BR-01"),
            new PostingDimension("warehouse", "WH-01"),
        ],
        PartyId = "ITEM-EVT",
        ControlEffect = 0m,
        Currency = CurrencyCode.Sar,
        Actor = Harness.Actor,
    };

    private static async Task<decimal> DebitOfEntryAsync(Guid entryId, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(SalesTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(
            "select coalesce(sum(debit_company), 0) from ledger.journal_line where entry_id = $1", connection);
        command.Parameters.AddWithValue(entryId);
        return (decimal)(await command.ExecuteScalarAsync(token))!;
    }

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" | ", errors.Select(static error => error.ToString()));
}
