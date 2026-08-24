using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدة بيانات حقيقية وعدّادات
/// حقيقية، وتوازيها يجعل «فجوة في الترقيم» تعني «اختباران تسابقا» لا «المحرك مكسور».
/// </summary>
[CollectionDefinition("ledger", DisableParallelization = true)]
public sealed class LedgerTestGroup;

/// <summary>مشاهد محرك الترحيل على PostgreSQL حقيقية.</summary>
[Collection("ledger")]
public sealed class PostingEngineTests : IAsyncLifetime
{
    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · ترحيل متوازن عبر مسار الحدث
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Balanced_post_through_the_matrix_event()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        PostingRequest request = Requests.RentInvoice(
            LedgerTestEnvironment.TenantA, "INV-1001", 10_000.0000m, 1_500.0000m, new DateOnly(2026, 3, 10));

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(request, token);

        Proof.Require(result.IsSuccess, "ترحيل متوازن ينجح",
            result.IsSuccess ? $"القيد {result.Value.EntryNumber} بصمته {result.Value.EntryHash[..16]}…"
                             : string.Join(" | ", result.Errors.Select(static e => e.Code + ": " + e.MessageAr)));

        PostingReceipt receipt = result.Value;
        Proof.Require(receipt.LineCount == 3, "القالب ولّد ثلاثة سطور (مستأجرون · إيراد مؤجل · ضريبة مخرجات)",
            $"عدد السطور {receipt.LineCount.ToString(CultureInfo.InvariantCulture)}");

        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select account_code, debit_company, credit_company, role_code
              from ledger.journal_line where entry_id = $1 order by line_no
            """, connection);
        command.Parameters.AddWithValue(receipt.JournalEntryId);

        List<string> rows = [];
        decimal debit = 0m, credit = 0m;
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                rows.Add($"{reader.GetString(3)} ⇒ {reader.GetString(0)} مدين {Proof.Money(reader.GetDecimal(1))} دائن {Proof.Money(reader.GetDecimal(2))}");
                debit += reader.GetDecimal(1);
                credit += reader.GetDecimal(2);
            }
        }

        foreach (string row in rows)
        {
            Proof.Note(row);
        }

        Proof.Require(debit == credit && debit == 11_500.0000m, "مجموع المدين = مجموع الدائن بعملة الشركة",
            $"مدين {Proof.Money(debit)} دائن {Proof.Money(credit)}");

        // القالب لم يذكر رمز حساب واحد؛ الأدوار هي التي حُلّت.
        Proof.Require(rows.Exists(static r => r.StartsWith("ar_tenant_control ⇒ 1310", StringComparison.Ordinal)),
            "الدور ar_tenant_control حُلّ إلى حساب من خريطة المستأجر لا من الكود", rows[0]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · وحدتان، خريطتان، حسابان مختلفان من الحدث نفسه — بلا سطر كود
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_same_event_resolves_to_different_accounts_for_two_tenants()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        Result<PostingReceipt> a = await _harness.Posting.PostAsync(
            Requests.RentAccrual(LedgerTestEnvironment.TenantA, "ACC-A-1", 3_000.0000m,
                new DateOnly(2026, 3, 31), LedgerTestEnvironment.OwnProperty), token);

        Result<PostingReceipt> b = await _harness.Posting.PostAsync(
            Requests.RentAccrual(LedgerTestEnvironment.TenantB, "ACC-B-1", 3_000.0000m,
                new DateOnly(2026, 3, 31), LedgerTestEnvironment.OwnProperty), token);

        Proof.Require(a.IsSuccess && b.IsSuccess, "الحدث نفسه رُحّل لدى المستأجرين",
            a.IsSuccess && b.IsSuccess ? "كلاهما نجح"
                : string.Join(" | ", a.Errors.Concat(b.Errors).Select(static e => e.MessageAr)));

        string accountA = await RevenueAccountAsync(a.Value.JournalEntryId, token);
        string accountB = await RevenueAccountAsync(b.Value.JournalEntryId, token);

        Proof.Require(accountA == "4301" && accountB == "4305" && accountA != accountB,
            "الدور rental_revenue أنتج حسابين مختلفين من الحدث نفسه — بتعديل صفّ في خريطة، لا بكود",
            $"المستأجر أ ⇒ {accountA} · المستأجر ب ⇒ {accountB}");
    }

    private static async Task<string> RevenueAccountAsync(Guid entryId, CancellationToken token)
    {
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            "select account_code from ledger.journal_line where entry_id = $1 and role_code = 'rental_revenue'", connection);
        command.Parameters.AddWithValue(entryId);
        return (string)(await command.ExecuteScalarAsync(token))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · GR-RE-001 — الحجب يرفض محاولة حقيقية
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GR_RE_001_refuses_rental_revenue_on_a_managed_for_others_property()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // محاولة حقيقية: نفس الحدث، ونفس المبالغ، والفرق الوحيد أن العقار مُدار
        // لصالح الغير. في هذا النموذج الأجرة المحصَّلة **التزام تجاه المالك** لا
        // إيراد للشركة، والخطأ هنا يضخّم الإيراد ٢١ ضعفاً (07-real-estate.md §1.3).
        Result<PostingReceipt> result = await _harness.Posting.PostAsync(
            Requests.RentAccrual(LedgerTestEnvironment.TenantA, "ACC-BLOCKED-1", 30_000.0000m,
                new DateOnly(2026, 4, 30), LedgerTestEnvironment.ManagedProperty), token);

        bool blocked = result.IsFailure && result.Errors.Any(static e => e.Code == "ledger.posting.guard.GR-RE-001");
        Proof.Require(blocked, "GR-RE-001 ترفض الترحيل إلى إيراد الإيجار على عقار مُدار لصالح الغير",
            result.IsFailure ? result.Errors[0].MessageAr : "القيد رُحّل — والقاعدة لم تعمل");

        // والرفض مسجَّل: المرفوض هو ما يُثبت أن الرقابة عملت (فخ-08).
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select count(*) from ledger.process_event
             where company_id = $1 and outcome = 'refused' and reason_code = 'ledger.posting.guard.GR-RE-001'
            """, connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        long refusals = (long)(await command.ExecuteScalarAsync(token))!;
        Proof.Require(refusals >= 1, "الرفض مكتوب في سجل العمليات",
            $"عدد سجلات الرفض {refusals.ToString(CultureInfo.InvariantCulture)}");

        // والطبقة الثالثة: قاعدة البيانات نفسها ترفض التركيب حتى لو أخطأ الكود.
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();
        await using NpgsqlTransaction transaction = await owner.BeginTransactionAsync(token);
        Guid entryId = Guid.CreateVersion7();
        string sqlState = string.Empty;
        try
        {
            await InsertRawEntryAsync(owner, transaction, LedgerTestEnvironment.TenantA, "GRRE", entryId, 990_001, token);
            await InsertRawLineAsync(owner, transaction, entryId, 1, "1310", 30_000m, 0m, null, token);
            await InsertRawLineAsync(owner, transaction, entryId, 2, "4301", 0m, 30_000m,
                LedgerTestEnvironment.ManagedProperty, token, roleCode: "rental_revenue");
            await transaction.CommitAsync(token);
        }
        catch (PostgresException exception)
        {
            sqlState = exception.SqlState;
            Proof.Require(exception.MessageText.Contains("GR-RE-001", StringComparison.Ordinal),
                "الطبقة الثالثة — قاعدة البيانات ترفض التركيب نفسه بدور المالك",
                $"SQLSTATE {sqlState}: {exception.MessageText[..Math.Min(120, exception.MessageText.Length)]}");
        }

        Proof.Require(sqlState.Length > 0, "لا مسار كتابة يصل إلى إيراد إيجار على عقار مُدار",
            sqlState.Length > 0 ? "رُفض داخل PostgreSQL" : "التركيب مرّ — الطبقة الثالثة معطّلة");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · الإحكام مستقلّ عن الترتيب — إعادة التسليم خارج الترتيب لا تُسقط ريالاً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Idempotent_replay_survives_out_of_order_arrival()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // المشهد المقيس حرفياً: ثلاثة قيود من جهاز نقاط بيع دون اتصال، تصل
        // بترتيب 3 ثم 1 ثم 2، ثم تُعاد كلها. الحارس التصاعدي لكل حساب
        // (WHERE applied_seq < @seq) أسقط الوسط بصمت فضاعت 500 من 1500 (فخ-13).
        decimal[] values = [500.0000m, 500.0000m, 500.0000m];
        int[] arrival = [2, 0, 1];

        foreach (int index in arrival)
        {
            Result<PostingReceipt> result = await _harness.Posting.PostAsync(
                Requests.RentInvoice(LedgerTestEnvironment.TenantA, "POS-OOO-" + index.ToString(CultureInfo.InvariantCulture),
                    values[index], 0m, new DateOnly(2026, 5, 10), taxable: false), token);
            Proof.Require(result.IsSuccess, $"وصول خارج الترتيب #{index.ToString(CultureInfo.InvariantCulture)} رُحّل",
                result.IsSuccess ? "نجح" : result.Errors[0].MessageAr);
        }

        // إعادة التسليم كاملة وبترتيب ثالث مختلف — «مرة واحدة على الأقل» تعني هذا.
        int replayed = 0;
        foreach (int index in new[] { 1, 2, 0 })
        {
            Result<PostingReceipt> result = await _harness.Posting.PostAsync(
                Requests.RentInvoice(LedgerTestEnvironment.TenantA, "POS-OOO-" + index.ToString(CultureInfo.InvariantCulture),
                    values[index], 0m, new DateOnly(2026, 5, 10), taxable: false), token);
            if (result.IsSuccess && result.Value.WasAlreadyPosted)
            {
                replayed++;
            }
        }

        Proof.Require(replayed == 3, "الوصول الثاني بالمفتاح نفسه لا يفعل شيئاً ولا يُعدّ خطأ — مهما كان الترتيب",
            $"{replayed.ToString(CultureInfo.InvariantCulture)}/3 أُعيدت بلا كتابة");

        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select coalesce(sum(l.debit_company), 0), count(distinct e.entry_id)
              from ledger.journal_entry e join ledger.journal_line l on l.entry_id = e.entry_id
             where e.company_id = $1 and e.source_doc_type = 'RentInvoice'
               and e.source_doc_id like 'POS-OOO-%' and l.account_code = '1310'
            """, connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        decimal total = reader.GetDecimal(0);
        long entries = reader.GetInt64(1);

        Proof.Require(total == 1_500.0000m && entries == 3,
            "المجموع 1500.0000 كاملاً وثلاثة قيود — لا 1000.0000 ولا قيد ضائع",
            $"المجموع {Proof.Money(total)} في {entries.ToString(CultureInfo.InvariantCulture)} قيود");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · العكس لا الحذف — والأصل لا يُمسّ · ثم ترحيل ← عكس ← تصحيح ← إعادة ترحيل
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Reversal_leaves_the_original_untouched_and_a_corrected_repost_follows()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        Result<PostingReceipt> original = await _harness.Posting.PostAsync(
            Requests.RentInvoice(LedgerTestEnvironment.TenantA, "REV-1", 8_000.0000m, 1_200.0000m,
                new DateOnly(2026, 6, 10)), token);
        Proof.Require(original.IsSuccess, "القيد الأصلي رُحّل",
            original.IsSuccess ? $"رقمه {original.Value.EntryNumber}" : original.Errors[0].MessageAr);

        string before = await FingerprintAsync(original.Value.JournalEntryId, token);

        Result<PostingReceipt> reversal = await _harness.Posting.ReverseAsync(new ReversalRequest
        {
            Tenant = new TenantId(LedgerTestEnvironment.TenantA),
            EntryId = original.Value.JournalEntryId,
            Reason = new LocalizedName("خطأ في مبلغ الأجرة", "Wrong rent amount"),
            Actor = new UserId(new Guid("22222222-2222-4222-8222-222222222222")),
        }, token);

        Proof.Require(reversal.IsSuccess, "العكس رُحّل قيداً جديداً",
            reversal.IsSuccess ? $"رقمه {reversal.Value.EntryNumber}" : reversal.Errors[0].MessageAr);

        string after = await FingerprintAsync(original.Value.JournalEntryId, token);
        Proof.Require(before == after, "القيد الأصلي لم يُمسّ بحرف — لا علم «معكوس» ولا تعديل",
            $"بصمة الصف قبل وبعد: {before[..16]}… = {after[..16]}…");

        await using (NpgsqlConnection connection = LedgerHarness.OpenApp())
        {
            await using NpgsqlCommand command = new(
                """
                select r.status, r.reverses_entry_id, sum(rl.debit_company), sum(rl.credit_company)
                  from ledger.journal_entry r join ledger.journal_line rl on rl.entry_id = r.entry_id
                 where r.entry_id = $1 group by r.status, r.reverses_entry_id
                """, connection);
            command.Parameters.AddWithValue(reversal.Value.JournalEntryId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            Proof.Require(reader.GetString(0) == "REVERSAL" && reader.GetGuid(1) == original.Value.JournalEntryId
                          && reader.GetDecimal(2) == 9_200.0000m && reader.GetDecimal(3) == 9_200.0000m,
                "قيد العكس مرتبط بالأصل ومقلوب الجانبين ومتوازن",
                $"حالته {reader.GetString(0)} يعكس {reader.GetGuid(1)} بمبلغ {Proof.Money(reader.GetDecimal(2))}");
        }

        // إعادة الترحيل مصحَّحاً: الجيل الثاني، ومفتاح إحكام مختلف بلا التفاف.
        PostingRequest corrected = Requests.RentInvoice(
            LedgerTestEnvironment.TenantA, "REV-1", 9_000.0000m, 1_350.0000m, new DateOnly(2026, 6, 10))
            with
        { Generation = 2, IdempotencyKey = new IdempotencyKey("rent-invoice:REV-1:g2") };

        Result<PostingReceipt> repost = await _harness.Posting.PostAsync(corrected, token);
        Proof.Require(repost.IsSuccess && repost.Value.Generation == 2,
            "ترحيل ← عكس ← تصحيح ← إعادة ترحيل يعمل بلا التفاف على الإحكام",
            repost.IsSuccess ? $"الجيل {repost.Value.Generation.ToString(CultureInfo.InvariantCulture)} رقم {repost.Value.EntryNumber}"
                             : repost.Errors[0].MessageAr);

        // وزيادة الجيل بلا عكس سابق مرفوضة: هي الالتفاف نفسه.
        PostingRequest illegal = Requests.RentInvoice(
            LedgerTestEnvironment.TenantA, "REV-1", 9_000.0000m, 1_350.0000m, new DateOnly(2026, 6, 10))
            with
        { Generation = 3, IdempotencyKey = new IdempotencyKey("rent-invoice:REV-1:g3") };

        Result<PostingReceipt> refused = await _harness.Posting.PostAsync(illegal, token);
        Proof.Require(refused.IsFailure, "زيادة الجيل بلا عكس مشروع مرفوضة",
            refused.IsFailure ? refused.Errors[0].MessageAr : "مرّت — والجيل صار باباً خلفياً");
    }

    private static async Task<string> FingerprintAsync(Guid entryId, CancellationToken token)
    {
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select md5(string_agg(t.row_text, '|' order by t.row_text)) from (
                select e.entry_id::text || e.entry_no::text || e.status || e.memo_ar || e.posted_at::text as row_text
                  from ledger.journal_entry e where e.entry_id = $1
                union all
                select l.line_no::text || l.account_code || l.debit::text || l.credit::text
                  from ledger.journal_line l where l.entry_id = $1) t
            """, connection);
        command.Parameters.AddWithValue(entryId);
        return (string)(await command.ExecuteScalarAsync(token))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · رقابة الفترة — الرفض افتراضي، والاستثناء إذنٌ موثَّق يُسجَّل
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Closed_period_is_refused_by_default_and_allowed_only_by_a_recorded_permission()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        PostingRequest intoClosed = Requests.RentInvoice(
            LedgerTestEnvironment.TenantA, "CLOSED-1", 1_000.0000m, 0m, new DateOnly(2026, 1, 15), taxable: false);

        Result<PostingReceipt> refused = await _harness.Posting.PostAsync(intoClosed, token);
        Proof.Require(refused.IsFailure && refused.Errors.Any(static e => e.Code == "ledger.posting.closed_period"),
            "الترحيل في فترة مقفلة مرفوض افتراضاً",
            refused.IsFailure ? refused.Errors[0].MessageAr : "مرّ بلا إذن");

        PostingRequest authorised = intoClosed with
        {
            ClosedPeriodAuthorisation = new ClosedPeriodAuthorisation(
                "LEDGER.POST_INTO_CLOSED_PERIOD",
                new UserId(new Guid("33333333-3333-4333-8333-333333333333")),
                new LocalizedName("تصحيح مطالب به من المدقّق الخارجي", "Correction required by the external auditor")),
        };

        Result<PostingReceipt> allowed = await _harness.Posting.PostAsync(authorised, token);
        Proof.Require(allowed.IsSuccess, "الإذن الاستثنائي الموثَّق يسمح — وهو وحده",
            allowed.IsSuccess ? $"القيد {allowed.Value.EntryNumber} في الفترة {allowed.Value.PeriodCode}" : allowed.Errors[0].MessageAr);

        await using (NpgsqlConnection connection = LedgerHarness.OpenApp())
        {
            await using NpgsqlCommand command = new(
                """
                select count(*) from ledger.process_event
                 where company_id = $1 and kind = 'posting.closed_period'
                   and outcome = 'allowed_by_permission' and detail like '%LEDGER.POST_INTO_CLOSED_PERIOD%'
                """, connection);
            command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
            long recorded = (long)(await command.ExecuteScalarAsync(token))!;
            Proof.Require(recorded >= 1, "الإذن مكتوب في سجل التدقيق داخل معاملة القيد نفسها — إذنٌ بلا أثر ثغرة لا إذن",
                $"سجلات الإذن {recorded.ToString(CultureInfo.InvariantCulture)}");
        }

        PostingRequest intoPermanent = Requests.RentInvoice(
            LedgerTestEnvironment.TenantA, "PERM-1", 1_000.0000m, 0m, new DateOnly(2026, 2, 15), taxable: false)
            with
        {
            ClosedPeriodAuthorisation = new ClosedPeriodAuthorisation(
                "LEDGER.POST_INTO_CLOSED_PERIOD",
                new UserId(new Guid("33333333-3333-4333-8333-333333333333")),
                new LocalizedName("محاولة", "Attempt")),
        };

        Result<PostingReceipt> permanent = await _harness.Posting.PostAsync(intoPermanent, token);
        Proof.Require(permanent.IsFailure, "الفترة المقفلة نهائياً لا يفتحها إذن ولا غيره",
            permanent.IsFailure ? permanent.Errors[0].MessageAr : "مرّ — والقفل النهائي ليس نهائياً");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 · ميزان المراجعة يطابق السطور غير القابلة للتعديل بالضبط
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Trial_balance_ties_exactly_to_the_immutable_lines()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 5; i++)
        {
            await _harness.Posting.PostAsync(
                Requests.RentInvoice(LedgerTestEnvironment.TenantA, "TB-" + i.ToString(CultureInfo.InvariantCulture),
                    1_000.0000m * i, 150.0000m * i, new DateOnly(2026, 7, 10)), token);
        }

        Result<IReadOnlyList<TrialBalanceRow>> trial = await _harness.Auditing.TrialBalanceFromLinesAsync(
            new TenantId(LedgerTestEnvironment.TenantA), LedgerTestEnvironment.Book, "2026-07", token);

        decimal debit = trial.Value.Sum(static row => row.Debit);
        decimal credit = trial.Value.Sum(static row => row.Credit);

        // خمس فواتير: الصافي 1000·i والضريبة 150·i لـ i من 1 إلى 5
        // ⇒ الصافي 15,000.0000 والضريبة 2,250.0000 والمدين 17,250.0000.
        Proof.Require(debit == credit && debit == 17_250.0000m,
            "ميزان المراجعة متوازن ومبنيّ من السطور لا من الإسقاط",
            $"مدين {Proof.Money(debit)} = دائن {Proof.Money(credit)} على {trial.Value.Count.ToString(CultureInfo.InvariantCulture)} حسابات");

        // والإسقاط يطابق الحقيقة صفّاً بصفّ: انحرافه هو «الرقم الخاطئ الصامت».
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select count(*) from (
              select account_code, sum(debit) d, sum(credit) c
                from ledger.account_balance
               where company_id = $1 and book_id = $2 and period_code = '2026-07'
               group by account_code
            ) b full outer join (
              select l.account_code, sum(l.debit_company) d, sum(l.credit_company) c
                from ledger.journal_line l join ledger.journal_entry e on e.entry_id = l.entry_id
               where l.company_id = $1 and e.book_id = $2 and e.period_code = '2026-07'
               group by l.account_code
            ) t on t.account_code = b.account_code
            where b.account_code is null or t.account_code is null or b.d <> t.d or b.c <> t.c
            """, connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        command.Parameters.AddWithValue(LedgerTestEnvironment.Book);
        long mismatches = (long)(await command.ExecuteScalarAsync(token))!;

        Proof.Require(mismatches == 0, "إسقاط الأرصدة يطابق السطور بالضبط — صفر انحراف",
            $"صفوف مختلفة: {mismatches.ToString(CultureInfo.InvariantCulture)}");
    }

    internal static async Task InsertRawEntryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid company,
        string book,
        Guid entryId,
        long entryNo,
        CancellationToken token,
        string periodCode = "2026-03")
    {
        await using NpgsqlCommand command = new(
            """
            insert into ledger.journal_entry
                (entry_id, company_id, book_id, fiscal_year, entry_no, entry_date, period_code, posted_at,
                 status, actor, source_module, source_doc_type, source_doc_id, posting_trigger_code,
                 idempotency_key, currency)
            values ($1,$2,$3,2026,$4,'2026-03-15',$5, now(), 'POSTED', 'raw-sql',
                    'Ledger', 'RawProbe', $6, 'RAW', $6, 'SAR')
            """, connection, transaction);
        command.Parameters.AddWithValue(entryId);
        command.Parameters.AddWithValue(company);
        command.Parameters.AddWithValue(book);
        command.Parameters.AddWithValue(entryNo);
        command.Parameters.AddWithValue(periodCode);
        command.Parameters.AddWithValue(entryId.ToString("D", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(token);
    }

    internal static async Task InsertRawLineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid entryId,
        int lineNo,
        string account,
        decimal debit,
        decimal credit,
        string? propertyId,
        CancellationToken token,
        string roleCode = "")
    {
        await using NpgsqlCommand command = new(
            """
            insert into ledger.journal_line
                (line_id, entry_id, line_no, company_id, account_code, role_code, qualifier,
                 debit, credit, currency, fx_rate, debit_company, credit_company, property_id)
            select $1, $2, $3, e.company_id, $4, $5, '*', $6, $7, 'SAR', 1, $6, $7, $8
              from ledger.journal_entry e where e.entry_id = $2
            """,
            connection, transaction);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(entryId);
        command.Parameters.AddWithValue(lineNo);
        command.Parameters.AddWithValue(account);
        command.Parameters.AddWithValue(roleCode);
        command.Parameters.AddWithValue(debit);
        command.Parameters.AddWithValue(credit);
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)propertyId ?? DBNull.Value });
        await command.ExecuteNonQueryAsync(token);
    }
}
