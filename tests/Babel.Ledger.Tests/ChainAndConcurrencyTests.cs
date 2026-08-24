using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// الترقيم بلا فجوات تحت تزاحم حقيقي، وسلسلة البصمات أمام عابث بصلاحيات المالك.
/// </summary>
[Collection("ledger")]
public sealed class ChainAndConcurrencyTests : IAsyncLifetime
{
    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // الترقيم بلا فجوات: 16 كاتباً متزامناً، وتراجعات متعمَّدة بينهم
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Numbering_stays_gapless_under_sixteen_concurrent_writers_with_deliberate_rollbacks()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string book = "GAPLESS";
        const int writers = 16;
        const int perWriter = 8;

        Guid tenant = LedgerTestEnvironment.TenantB;
        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);

        int committed = 0;
        int rolledBack = 0;

        await Parallel.ForAsync(0, writers, token, async (writer, ct) =>
        {
            for (int i = 0; i < perWriter; i++)
            {
                // كل ثالث محاولة تُجهَض عمداً بعد أن **أخذت** رقماً تحت القفل.
                // هذا هو بالضبط ما يُهدر أرقاماً مع SEQUENCE: التسلسل غير معاملاتي
                // ولا يعود عند التراجع، والمدقّق يقرأ الرقم المفقود مستنداً محذوفاً
                // (فخ-12 · ADR-0008).
                if (i % 3 == 0)
                {
                    await RollBackDeliberatelyAsync(tenant, book, ct);
                    Interlocked.Increment(ref rolledBack);
                    continue;
                }

                PostingRequest request = Requests.RentInvoice(
                    tenant,
                    $"GAP-{writer.ToString(CultureInfo.InvariantCulture)}-{i.ToString(CultureInfo.InvariantCulture)}",
                    100.0000m, 15.0000m, new DateOnly(2026, 9, 10)) with
                { Book = book };

                Result<PostingReceipt> result = await _harness.Posting.PostAsync(request, ct);
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref committed);
                }
                else
                {
                    Proof.Fail("كاتب متزامن فشل بلا سبب متوقَّع", result.Errors[0].Code + ": " + result.Errors[0].MessageAr);
                }
            }
        });

        long min, max, count, distinct;
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using (NpgsqlCommand command = new(
            """
            select min(entry_no), max(entry_no), count(*), count(distinct entry_no)
              from ledger.journal_entry where company_id = $1 and book_id = $2
            """, connection))
        {
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue(book);

            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            min = reader.GetInt64(0);
            max = reader.GetInt64(1);
            count = reader.GetInt64(2);
            distinct = reader.GetInt64(3);
        }

        Proof.Note($"{writers.ToString(CultureInfo.InvariantCulture)} كاتباً · "
            + $"{committed.ToString(CultureInfo.InvariantCulture)} ترحيلاً مثبَّتاً · "
            + $"{rolledBack.ToString(CultureInfo.InvariantCulture)} تراجعاً متعمَّداً بعد أخذ الرقم");

        Proof.Require(min == 1 && max == count && count == distinct,
            "الترقيم متصل 1..N بلا فجوة ولا تكرار، رغم التراجعات المتعمَّدة",
            $"من {min.ToString(CultureInfo.InvariantCulture)} إلى {max.ToString(CultureInfo.InvariantCulture)} "
            + $"بعدد {count.ToString(CultureInfo.InvariantCulture)} ومميّزات {distinct.ToString(CultureInfo.InvariantCulture)}");

        // ونطاق السلسلة = نطاق الترقيم بالضبط، فلا تعريفان لـ«الدفتر».
        long links, firstSeq, lastSeq;
        await using (NpgsqlCommand chain = new(
            """
            select count(*), min(chain_seq), max(chain_seq)
              from ledger.chain_link where company_id = $1 and book_id = $2
            """, connection))
        {
            chain.Parameters.AddWithValue(tenant);
            chain.Parameters.AddWithValue(book);
            await using NpgsqlDataReader chainReader = await chain.ExecuteReaderAsync(token);
            await chainReader.ReadAsync(token);
            links = chainReader.GetInt64(0);
            firstSeq = chainReader.GetInt64(1);
            lastSeq = chainReader.GetInt64(2);
        }

        Proof.Require(links == count && firstSeq == 1 && lastSeq == max,
            "سلسلة البصمات متصلة على النطاق نفسه ولها العدد نفسه",
            $"حلقات {links.ToString(CultureInfo.InvariantCulture)} من "
            + $"{firstSeq.ToString(CultureInfo.InvariantCulture)} إلى {lastSeq.ToString(CultureInfo.InvariantCulture)}");

        Result<LedgerChainReport> verification = await _harness.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);

        Proof.Require(verification.Value.Ok, "السلسلة الناتجة عن التزاحم تُعاد التحقق منها سليمة",
            verification.Value.ToString());
    }

    /// <summary>يأخذ رقماً تحت القفل ثم يُجهض المعاملة — تراجع متعمَّد.</summary>
    private static async Task RollBackDeliberatelyAsync(Guid tenant, string book, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);

        await using (NpgsqlCommand command = new(
            """
            select next_entry_no from ledger.posting_counter
             where company_id = $1 and book_id = $2 and fiscal_year = $3 for update
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue(book);
            command.Parameters.AddWithValue(LedgerTestEnvironment.FiscalYear);
            await command.ExecuteScalarAsync(token);
        }

        await using (NpgsqlCommand command = new(
            """
            update ledger.posting_counter set next_entry_no = next_entry_no + 1, next_chain_seq = next_chain_seq + 1
             where company_id = $1 and book_id = $2 and fiscal_year = $3
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue(book);
            command.Parameters.AddWithValue(LedgerTestEnvironment.FiscalYear);
            await command.ExecuteNonQueryAsync(token);
        }

        await transaction.RollbackAsync(token);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // العبث على مستوى المالك — والسلسلة تسمّي أول تسلسل منحرف
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Chain_verification_names_the_first_divergent_sequence_after_a_balance_preserving_tamper()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string book = "TAMPER";
        Guid tenant = LedgerTestEnvironment.TenantA;
        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);

        for (int i = 1; i <= 6; i++)
        {
            PostingRequest request = Requests.RentInvoice(
                tenant, "TAM-" + i.ToString(CultureInfo.InvariantCulture),
                1_000.0000m * i, 150.0000m * i, new DateOnly(2026, 10, 10)) with
            { Book = book };

            Result<PostingReceipt> posted = await _harness.Posting.PostAsync(request, token);
            Proof.Require(posted.IsSuccess, $"قيد التمهيد {i.ToString(CultureInfo.InvariantCulture)} رُحّل",
                posted.IsSuccess ? "نجح" : posted.Errors[0].MessageAr);
        }

        Result<LedgerChainReport> before = await _harness.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);
        Proof.Require(before.Value.Ok && before.Value.Checked == 6, "السلسلة سليمة قبل العبث",
            before.Value.ToString());

        // العابث هنا هو **المالك**: يملك UPDATE، ويتعمّد ألا يكسر التوازن —
        // يبدّل حساب سطرين داخل القيد نفسه. ميزان المراجعة يبقى متوازناً،
        // ومجاميع القيد لا تتغيّر، وتقرير الحساب يتغيّر كلياً.
        await using (NpgsqlConnection owner = LedgerHarness.OpenOwner())
        {
            await using NpgsqlCommand command = new(
                """
                update ledger.journal_line l
                   set account_code = '2192'
                  from ledger.chain_link c
                 where c.entry_id = l.entry_id and c.company_id = $1 and c.book_id = $2
                   and c.chain_seq = 4 and l.account_code = '2171'
                """, owner);
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue(book);
            int affected = await command.ExecuteNonQueryAsync(token);
            Proof.Require(affected == 1, "العابث — بصلاحيات المالك — عدّل سطراً بلا كسر أي توازن",
                $"صفوف مُعدَّلة {affected.ToString(CultureInfo.InvariantCulture)} عند التسلسل 4");
        }

        // والتوازن ما زال سليماً: هذا بالضبط ما يجعل الفحص المحاسبي وحده أعمى.
        await using (NpgsqlConnection app = LedgerHarness.OpenApp())
        {
            await using NpgsqlCommand command = new(
                """
                select sum(l.debit_company) - sum(l.credit_company)
                  from ledger.journal_line l join ledger.chain_link c on c.entry_id = l.entry_id
                 where c.company_id = $1 and c.book_id = $2
                """, app);
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue(book);
            decimal difference = (decimal)(await command.ExecuteScalarAsync(token))!;
            Proof.Require(difference == 0m, "الدفتر ما زال متوازناً بعد العبث — ولو اكتفينا بالتوازن لما رأينا شيئاً",
                $"فرق المدين عن الدائن {Proof.Money(difference)}");
        }

        Result<LedgerChainReport> after = await _harness.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);

        Proof.Require(!after.Value.Ok && after.Value.FirstDivergentSequence == 4,
            "إعادة التحقق تسمّي أول تسلسل منحرف بالضبط",
            after.Value.ToString());

        Proof.Require(after.Value.Verdict == "CHAIN-CONTENT-TAMPERED",
            "الحكم يقول ما حدث: المحتوى تغيّر بعد الترحيل",
            after.Value.Verdict + " — " + after.Value.ReasonAr);
    }
}
