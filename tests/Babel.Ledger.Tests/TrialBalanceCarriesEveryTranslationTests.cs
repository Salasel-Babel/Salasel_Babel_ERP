using System.Diagnostics;
using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>ميزان المراجعة يحمل السجلّ العربي وكل ترجمة موجودة — لا زوجاً ثابتاً.</b>
/// <para>
/// كان الصفّ يحمل <c>NameAr</c> و<c>NameEn</c>، فالمحاسب الأردي أو الهندي يرى السجلّ
/// ومعه ترجمة <b>إنجليزية</b> لا ترجمةً بلغته — وهو نصّ الحدّ الذي رفعته
/// <c>TrialBalanceTable.tsx</c> في الواجهة. وما يُفحص هنا أن الحدّ سقط: خمس لغات على
/// حسابٍ واحد، تصل كلّها إلى الصفّ، بلا عمود ولا هجرة ولا إصدار.
/// </para>
/// <para>
/// وكل حالة هنا تبذر ترجماتها بنفسها وتحذفها في <c>finally</c> (CONTRIBUTING §3 بند 8):
/// البيانات المرجعية للمجموعة كلّها مشتركة، فترجمةٌ تُترك تُغيّر ما تقرؤه اختبارات أخرى.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class TrialBalanceCarriesEveryTranslationTests : IAsyncLifetime
{
    private const string Book = "XLATE";
    private const string Period = "2026-06";

    /// <summary>الحساب الذي يُحلّ إليه دور فرق التقريب — وعليه تُعلَّق اللغات.</summary>
    private const string ProbeAccount = "5401";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task خمس_لغات_تصل_ميزان_المراجعة_بلا_عمود_ولا_هجرة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        (string Tag, string Name)[] added =
        [
            ("ur", "نقدی"),
            ("hi", "नकद"),
            ("am", "ጥሬ ገንዘብ"),
            ("tl", "Salapi"),
        ];

        await SeedAsync(added, token);

        try
        {
            await PostAsync("XL-1", token);

            TrialBalanceRow cash = await ReadProbeAsync(token);

            Proof.Require(
                cash.Name.Arabic.Length > 0,
                "السجلّ العربي موجود وغير فارغ",
                cash.Name.Arabic);

            // خمس: الإنجليزية المبذورة أصلاً، والأربع المضافة هنا.
            Proof.Require(
                cash.Name.TranslationCount == 5,
                "خمس ترجمات على حساب واحد — والخامسة صفٌّ لا عمود",
                string.Join(
                    " · ",
                    cash.Name.Translations.Select(static pair => pair.Key + "=" + pair.Value)));

            foreach ((string tag, string name) in added)
            {
                Proof.Require(
                    cash.Name.In(tag) == name,
                    "الاسم بلغة « " + tag + " » يصل كما أُدخل",
                    cash.Name.In(tag));
            }

            // ولغةٌ لا ترجمة لها ترتدّ إلى السجلّ — **ويُعلَن الارتداد**.
            NameResolution absent = cash.Name.Resolve("fr-CA");

            Proof.Require(
                absent.IsFallback && absent.Text == cash.Name.Arabic && absent.LanguageTag == "ar",
                "لغةٌ بلا ترجمة ترتدّ إلى السجلّ العربي ويُعلَن أنها ارتدّت",
                absent.LanguageTag + " · ارتداد " + absent.IsFallback.ToString());

            // ورمز الحساب معرّف لا نصّ: لا يُترجَم ولا يتغيّر بتغيّر اللغة.
            Proof.Require(
                cash.AccountCode == ProbeAccount,
                "رمز الحساب معرّف لا نصّ — لا يُترجَم",
                cash.AccountCode);
        }
        finally
        {
            await ClearAsync([.. added.Select(static pair => pair.Tag)], token);
        }
    }

    /// <summary>
    /// كلفة الانضمام إلى جدول الترجمات — <b>الرقم الذي وسمه ADR-0021 §4 «غير مقيس»</b>.
    /// <para>
    /// والقياس هنا مقارنة لا عتبة: الاستعلام نفسه بالاستعلام الفرعي وبدونه، على البيانات
    /// نفسها وفي العملية نفسها. عتبةٌ مطلقة على زمن جدار في آلة بناء مشتركة حارسٌ متذبذب،
    /// وهو أسوأ من غيابه — فالحكم هنا على <b>مرتبة</b> الفرق لا على مللي ثانية بعينها.
    /// </para>
    /// </summary>
    [Fact]
    public async Task كلفة_الانضمام_إلى_جدول_الترجمات_مقيسة_لا_مُقدَّرة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        (string Tag, string Name)[] added = [("ur", "نقدی"), ("hi", "नकद"), ("am", "ጥሬ ገንዘብ")];
        await SeedAsync(added, token);

        try
        {
            await PostAsync("XL-COST", token);

            const string withTranslations =
                """
                select l.account_code, a.name_ar,
                       coalesce(sum(l.debit_company), 0), coalesce(sum(l.credit_company), 0),
                       grouping(l.account_code),
                       (select jsonb_object_agg(t.language_tag, t.name)
                          from ledger.name_translation t
                         where t.company_id = $1 and t.entity_kind = 'account'
                           and t.entity_key = l.account_code)
                  from ledger.journal_line l
                  join ledger.journal_entry e on e.entry_id = l.entry_id
                  join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
                 where l.company_id = $1 and e.book_id = $2
                 group by grouping sets ((l.account_code, a.name_ar), ())
                 order by grouping(l.account_code), l.account_code
                """;

            const string without =
                """
                select l.account_code, a.name_ar,
                       coalesce(sum(l.debit_company), 0), coalesce(sum(l.credit_company), 0),
                       grouping(l.account_code)
                  from ledger.journal_line l
                  join ledger.journal_entry e on e.entry_id = l.entry_id
                  join ledger.account a on a.company_id = l.company_id and a.account_code = l.account_code
                 where l.company_id = $1 and e.book_id = $2
                 group by grouping sets ((l.account_code, a.name_ar), ())
                 order by grouping(l.account_code), l.account_code
                """;

            await using NpgsqlConnection connection = LedgerHarness.OpenOwner();

            // إحماء: أول تنفيذ يدفع ثمن تخطيط الاستعلام وملء الذاكرة المؤقّتة، وقياسه
            // يقيس البرد لا الكلفة.
            await TimeAsync(connection, without, token);
            await TimeAsync(connection, withTranslations, token);

            double bare = 0;
            double joined = 0;
            const int rounds = 12;

            for (int i = 0; i < rounds; i++)
            {
                bare += await TimeAsync(connection, without, token);
                joined += await TimeAsync(connection, withTranslations, token);
            }

            bare /= rounds;
            joined /= rounds;

            string measured = string.Format(
                CultureInfo.InvariantCulture,
                "زمن الاستعلام بلا ترجمات: {0:F3} ملّي ثانية · معه: {1:F3} ملّي ثانية · النسبة {2:F2}× — "
                + "متوسّط {3} جولة بعد إحماء جولتين، على PostgreSQL محلّي وبيانات هذه المجموعة.",
                bare,
                joined,
                joined / bare,
                rounds);

            Proof.Note(measured);

            Proof.Require(
                joined < bare * 10,
                "الاستعلام الفرعي على المفتاح الأساسي لا يُغيّر مرتبة الزمن",
                string.Format(CultureInfo.InvariantCulture, "{0:F2}×", joined / bare));
        }
        finally
        {
            await ClearAsync([.. added.Select(static pair => pair.Tag)], token);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static async Task<double> TimeAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        long start = Stopwatch.GetTimestamp();

        await using (NpgsqlCommand command = new(sql, connection))
        {
            command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
            command.Parameters.AddWithValue(Book);

            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                // القراءة كاملةً: قياسُ زمنٍ لا تُستهلك فيه النتيجة يقيس الإرسال لا العمل.
            }
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static async Task SeedAsync((string Tag, string Name)[] translations, CancellationToken token)
    {
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();

        foreach ((string tag, string name) in translations)
        {
            await using NpgsqlCommand command = new(
                """
                insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                values ($1, 'account', $2, $3, $4)
                on conflict (company_id, entity_kind, entity_key, language_tag) do update set name = excluded.name
                """, owner);
            command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
            command.Parameters.AddWithValue(ProbeAccount);
            command.Parameters.AddWithValue(tag);
            command.Parameters.AddWithValue(name);
            await command.ExecuteNonQueryAsync(token);
        }
    }

    private static async Task ClearAsync(string[] tags, CancellationToken token)
    {
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();
        await using NpgsqlCommand command = new(
            """
            delete from ledger.name_translation
             where company_id = $1 and entity_kind = 'account' and entity_key = $2 and language_tag = any($3)
            """, owner);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        command.Parameters.AddWithValue(ProbeAccount);
        command.Parameters.AddWithValue(tags);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task PostAsync(string document, CancellationToken token)
    {
        await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantA, Book, token);

        Result<PostingReceipt> posted = await _harness.Posting.PostAsync(
            new PostingRequest
            {
                Tenant = new TenantId(LedgerTestEnvironment.TenantA),
                IdempotencyKey = new IdempotencyKey("Xlate:" + document),
                Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", document),
                Trigger = PostingTrigger.OnApproval,
                DocumentDate = new DateOnly(2026, 6, 15),
                Book = Book,
                Event = new PostingEventCode("ledger.manual_voucher.posted"),
                Narration = new LocalizedName("قيد فحص الترجمات", "Translation probe voucher"),
                Lines =
                [
                    new PostingLine
                    {
                        Role = PostingRole.RoundingDifference,
                        Side = PostingSide.Debit,
                        Amount = SharedKernel.Money.Of(250.0000m, CurrencyCode.Sar),
                        Scope = new PostingScope("BR-01", null, null),
                    },
                    new PostingLine
                    {
                        Role = PostingRole.RoundingDifference,
                        Side = PostingSide.Credit,
                        Amount = SharedKernel.Money.Of(250.0000m, CurrencyCode.Sar),
                        Scope = new PostingScope("BR-01", null, null),
                    },
                ],
            },
            token);

        Proof.Require(
            posted.IsSuccess,
            "قيد فحص الترجمات رُحّل",
            posted.IsSuccess
                ? posted.Value.EntryNumber.ToString(CultureInfo.InvariantCulture)
                : string.Join(" | ", posted.Errors.Select(static e => e.Code)));
    }

    private async Task<TrialBalanceRow> ReadProbeAsync(CancellationToken token)
    {
        Result<TrialBalanceReport> report = await _harness.Auditing.TrialBalanceFromLinesAsync(
            new TenantId(LedgerTestEnvironment.TenantA), LedgerTestEnvironment.Auditor, Book, Period, token);

        Proof.Require(
            report.IsSuccess,
            "قراءة ميزان دفتر الترجمات",
            report.IsSuccess ? "نجحت" : string.Join(" | ", report.Errors.Select(static e => e.Code)));

        TrialBalanceRow? probe = report.Value.Rows.FirstOrDefault(row => row.AccountCode == ProbeAccount);

        Proof.Require(probe is not null, "صفّ الحساب المرصود موجود في الميزان", ProbeAccount);

        return probe!;
    }
}
