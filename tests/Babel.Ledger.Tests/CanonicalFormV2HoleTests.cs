using System.Globalization;
using Babel.Canonicalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>الثغرة، مُثبَتة ثم مُغلقة — على PostgreSQL حقيقية وبصلاحيات المالك.</b>
///
/// <para>
/// الشكل القانوني v1 كان يغطّي من السطر ستّة حقول فقط: رقمه، والحساب، والمدين،
/// والدائن، و<c>cost_center</c>، والوصف. أي أن مالك قاعدة البيانات كان يستطيع أن
/// ينقل حركة من عقار إلى عقار — فتنقلب ربحية عقارين، ويتغيّر كشف مالك —
/// و<c>VerifyChain</c> يقول «سليمة».
/// </para>
/// <para>
/// كل سيناريو هنا يُنفَّذ <b>مرّتين</b>: على سلسلة كُتبت بـv1 وعلى سلسلة كُتبت بـv2،
/// بنفس عبارة <c>UPDATE</c> بالحرف، وبنفس البيانات. وكل عبارة تُبقي
/// <c>مجموع المدين = مجموع الدائن</c> فلا يطلق أي فحص محاسبي — وهذا بالضبط ما
/// يجعل الفحص المحاسبي وحده أعمى.
/// </para>
/// <para>
/// والمشغّلات لا تحمي هنا: <c>trg_journal_line_balanced</c> و
/// <c>trg_journal_line_allowed</c> كلاهما <c>after insert</c>، فلا يريان
/// <c>UPDATE</c> أصلاً. الحماية الوحيدة الممكنة هي أن يكون الحقل <b>داخل البايتات
/// المُجزَّأة</b>.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class CanonicalFormV2HoleTests : IAsyncLifetime
{
    private LedgerHarness _v2 = null!;
    private LedgerHarness _v1 = null!;

    public async ValueTask InitializeAsync()
    {
        _v2 = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        _v1 = await LedgerHarness.CreateV1Async(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>سيناريو عبث: اسمه، وعبارته، والتسلسل الذي يقع عنده.</summary>
    private sealed record Tamper(string Name, string DescriptionAr, string Sql, long Sequence);

    private static readonly Tamper[] Scenarios =
    [
        new("property_id",
            "نقل الحركة من عقار مملوك إلى عقار مُدار لصالح الغير — ربحية عقارين تنقلب وكشف مالك يتغيّر",
            """
            update ledger.journal_line l set property_id = 'P-MANAGED-001'
              from ledger.chain_link c
             where c.entry_id = l.entry_id and c.company_id = $1 and c.book_id = $2
               and c.chain_seq = $3 and l.property_id = 'P-OWN-001'
            """,
            2),

        new("warehouse_id",
            "نقل حركة مخزون إلى مستودع آخر — رصيد مستودعين ينقلب والجرد لا يطابق",
            """
            update ledger.journal_line l set warehouse_id = 'WH-99'
              from ledger.chain_link c
             where c.entry_id = l.entry_id and c.company_id = $1 and c.book_id = $2
               and c.chain_seq = $3 and l.line_no = 1
            """,
            3),

        new("project_id",
            "نقل تكلفة إلى مشروع آخر — ربحية مشروعين تنقلب",
            """
            update ledger.journal_line l set project_id = 'PRJ-99'
              from ledger.chain_link c
             where c.entry_id = l.entry_id and c.company_id = $1 and c.book_id = $2
               and c.chain_seq = $3 and l.line_no = 1
            """,
            1),

        new("role_code",
            "إعادة كتابة الدور الذي وُلِّد منه السطر — وعليه تُفرض GR-RE-001 في قاعدة البيانات",
            """
            update ledger.journal_line l set role_code = 'bank_current_account'
              from ledger.chain_link c
             where c.entry_id = l.entry_id and c.company_id = $1 and c.book_id = $2
               and c.chain_seq = $3 and l.line_no = 1
            """,
            4),

        // المبلغ بعملة الشركة: سطر مدين واحد وسطر دائن واحد يزيد كلٌّ منهما بريال،
        // فيبقى مجموع المدين = مجموع الدائن، ويبقى كل سطر أحادي الجانب
        // (ck_journal_line_company_side)، ولا ينكسر شيء يمكن لفحص محاسبي رؤيته.
        new("company_amount",
            "تضخيم المبلغ بعملة الشركة على جانبي القيد معاً — ميزان المراجعة يتغيّر والتوازن لا ينكسر",
            """
            with target as (
                select l.entry_id from ledger.journal_line l
                  join ledger.chain_link c on c.entry_id = l.entry_id
                 where c.company_id = $1 and c.book_id = $2 and c.chain_seq = $3
                 limit 1),
                 d as (select line_id from ledger.journal_line
                        where entry_id = (select entry_id from target) and debit_company > 0
                        order by line_no limit 1),
                 k as (select line_id from ledger.journal_line
                        where entry_id = (select entry_id from target) and credit_company > 0
                        order by line_no limit 1)
            update ledger.journal_line l
               set debit_company  = l.debit_company
                                  + case when l.line_id in (select line_id from d) then 1 else 0 end,
                   credit_company = l.credit_company
                                  + case when l.line_id in (select line_id from k) then 1 else 0 end
             where l.line_id in (select line_id from d union all select line_id from k)
            """,
            5),
    ];

    // ═══════════════════════════════════════════════════════════════════════
    //  1) الثغرة كانت حقيقية: خمسة عبثات، وسلسلة v1 خضراء بعد كل واحدة
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Under_v1_all_five_owner_level_tampers_leave_the_chain_green()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantA;

        foreach (Tamper scenario in Scenarios)
        {
            string book = "HOLE1-" + scenario.Name.ToUpperInvariant();
            await SeedAsync(_v1, tenant, book, token);

            await AssertStoredCanonVersionAsync(tenant, book, "v1", token);

            Result<LedgerChainReport> before = await _v1.Auditing.VerifyChainAsync(
                new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);
            Proof.Require(before.Value.Ok, $"[v1/{scenario.Name}] السلسلة سليمة قبل العبث", before.Value.ToString());

            int affected = await TamperAsync(scenario, tenant, book, token);
            Proof.Require(affected > 0, $"[v1/{scenario.Name}] العابث — بصلاحيات المالك — عدّل {affected} صفاً",
                scenario.DescriptionAr);

            await AssertStillBalancedAsync(tenant, book, scenario.Name, token);

            Result<LedgerChainReport> after = await _v1.Auditing.VerifyChainAsync(
                new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);

            Proof.Require(after.Value.Ok,
                $"‼ [v1/{scenario.Name}] السلسلة ما زالت **خضراء** بعد العبث — هذه هي الثغرة",
                after.Value.ToString());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  2) الثغرة مغلقة: نفس العبث، وv2 يمسك كل واحد ويسمّي تسلسله
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Under_v2_every_one_of_the_five_is_caught_and_the_sequence_is_named()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantA;

        foreach (Tamper scenario in Scenarios)
        {
            string book = "HOLE2-" + scenario.Name.ToUpperInvariant();
            await SeedAsync(_v2, tenant, book, token);

            await AssertStoredCanonVersionAsync(tenant, book, "v2", token);

            Result<LedgerChainReport> before = await _v2.Auditing.VerifyChainAsync(
                new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);
            Proof.Require(before.Value.Ok && before.Value.Checked == 5,
                $"[v2/{scenario.Name}] السلسلة سليمة قبل العبث", before.Value.ToString());

            int affected = await TamperAsync(scenario, tenant, book, token);
            Proof.Require(affected > 0, $"[v2/{scenario.Name}] نفس العبث بنفس العبارة، {affected} صفاً",
                scenario.DescriptionAr);

            await AssertStillBalancedAsync(tenant, book, scenario.Name, token);

            Result<LedgerChainReport> after = await _v2.Auditing.VerifyChainAsync(
                new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);

            Proof.Require(!after.Value.Ok,
                $"✓ [v2/{scenario.Name}] العبث **مكشوف**", after.Value.ToString());

            Proof.Require(after.Value.Verdict == ChainVerdicts.ContentTampered,
                $"[v2/{scenario.Name}] الحكم يقول ما حدث: المحتوى تغيّر بعد الترحيل",
                after.Value.Verdict + " — " + after.Value.ReasonAr);

            Proof.Require(after.Value.FirstDivergentSequence == scenario.Sequence,
                $"[v2/{scenario.Name}] إعادة التحقق تسمّي أول تسلسل منحرف بالضبط",
                $"المتوقّع {scenario.Sequence.ToString(CultureInfo.InvariantCulture)} "
                + $"والمُبلَّغ {after.Value.FirstDivergentSequence?.ToString(CultureInfo.InvariantCulture) ?? "لا شيء"}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  3) التعايش: دفتر واحد فيه سجلات v1 ثم سجلات v2
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_single_book_carrying_v1_then_v2_records_verifies_end_to_end()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantA;
        const string book = "MIXED";

        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);

        for (int i = 1; i <= 3; i++)
        {
            await PostAsync(_v1, tenant, book, "MIX-V1-" + i.ToString(CultureInfo.InvariantCulture), i, token);
        }

        for (int i = 1; i <= 3; i++)
        {
            await PostAsync(_v2, tenant, book, "MIX-V2-" + i.ToString(CultureInfo.InvariantCulture), i, token);
        }

        List<string> versions = await StoredVersionsAsync(tenant, book, token);
        Proof.Require(versions.SequenceEqual(["v1", "v1", "v1", "v2", "v2", "v2"]),
            "الدفتر الواحد يحمل الإصدارين بالترتيب — العمود canon_version هو ما يفصل",
            string.Join(",", versions));

        Result<LedgerChainReport> verification = await _v2.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);

        Proof.Require(verification.Value.Ok && verification.Value.Checked == 6,
            "سلسلة واحدة فيها v1 و v2 تُعاد التحقق منها كاملة — التوزيع بالإصدار المخزَّن",
            verification.Value.ToString());
    }

    /// <summary>العبث بسجل v1 داخل الدفتر المختلط يبقى غير مكشوف؛ وبسجل v2 يُكشف.</summary>
    [Fact]
    public async Task In_a_mixed_book_only_the_v2_half_detects_a_dimension_rewrite()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantA;
        const string book = "MIXED-TAMPER";

        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);
        for (int i = 1; i <= 3; i++)
        {
            await PostAsync(_v1, tenant, book, "MT-V1-" + i.ToString(CultureInfo.InvariantCulture), i, token);
        }

        for (int i = 1; i <= 3; i++)
        {
            await PostAsync(_v2, tenant, book, "MT-V2-" + i.ToString(CultureInfo.InvariantCulture), i, token);
        }

        // العبث بسجل v1 (التسلسل 2): البصمة لا تشمل العقار، فلا شيء يُكشف.
        int v1Rows = await TamperAsync(Scenarios[0] with { Sequence = 2 }, tenant, book, token);
        Result<LedgerChainReport> afterV1 = await _v2.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);
        Proof.Require(afterV1.Value.Ok,
            $"‼ العبث بسجل v1 ({v1Rows.ToString(CultureInfo.InvariantCulture)} صفاً) يبقى غير مكشوف — "
            + "سجل كُتب تحت v1 يُتحقَّق منه بقواعد v1، ولا يُعاد تجزئته بإصدار أحدث",
            afterV1.Value.ToString());

        // ونفس العبث بسجل v2 (التسلسل 5) يُكشف فوراً.
        int v2Rows = await TamperAsync(Scenarios[0] with { Sequence = 5 }, tenant, book, token);
        Result<LedgerChainReport> afterV2 = await _v2.Auditing.VerifyChainAsync(
            new TenantId(tenant), book, LedgerTestEnvironment.FiscalYear, token);
        Proof.Require(!afterV2.Value.Ok && afterV2.Value.FirstDivergentSequence == 5,
            $"✓ نفس العبث بسجل v2 ({v2Rows.ToString(CultureInfo.InvariantCulture)} صفاً) يُكشف ويُسمّى تسلسله",
            afterV2.Value.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  4) الافتراضي هو v2
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task New_entries_are_written_under_v2_by_default()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantB;
        const string book = "DEFAULT-V2";

        await SeedAsync(_v2, tenant, book, token);
        List<string> versions = await StoredVersionsAsync(tenant, book, token);

        Proof.Require(versions.All(v => v == "v2") && versions.Count == 5,
            "كل قيد جديد يُكتب بـv2 بلا أي إعداد",
            string.Join(",", versions));

        Proof.Require(LedgerTestEnvironment.Options.CanonVersion == "v2",
            "الافتراضي في LedgerOptions هو v2",
            LedgerTestEnvironment.Options.CanonVersion);
    }

    /// <summary>
    /// البايتات المخزَّنة تحمل ترويسة الإصدار المخزَّن — لا العمود يكذب ولا البايتات.
    /// </summary>
    [Fact]
    public async Task The_stored_bytes_carry_the_wire_header_of_the_stored_version()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid tenant = LedgerTestEnvironment.TenantB;
        const string book = "HEADER";

        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);
        await PostAsync(_v1, tenant, book, "HDR-V1", 1, token);
        await PostAsync(_v2, tenant, book, "HDR-V2", 2, token);

        await using NpgsqlConnection app = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select canon_version, substring(encode(canonical_bytes, 'escape') from 1 for 14)
              from ledger.chain_link where company_id = $1 and book_id = $2 order by chain_seq
            """, app);
        command.Parameters.AddWithValue(tenant);
        command.Parameters.AddWithValue(book);

        List<string> pairs = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            pairs.Add(reader.GetString(0) + "|" + reader.GetString(1));
        }

        Proof.Require(pairs.SequenceEqual(["v1|babel.canon/v1", "v2|babel.canon/v2"]),
            "عمود canon_version وترويسة البايتات يقولان الشيء نفسه في كل سجل",
            string.Join("  ·  ", pairs));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  مساعدات
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task SeedAsync(LedgerHarness harness, Guid tenant, string book, CancellationToken token)
    {
        await LedgerTestEnvironment.EnsureCounterAsync(tenant, book, token);
        for (int i = 1; i <= 5; i++)
        {
            await PostAsync(harness, tenant, book, book + "-" + i.ToString(CultureInfo.InvariantCulture), i, token);
        }
    }

    private static async Task PostAsync(
        LedgerHarness harness, Guid tenant, string book, string documentId, int index, CancellationToken token)
    {
        PostingRequest request = Requests.RentInvoice(
            tenant, documentId,
            1_000.0000m * index, 150.0000m * index,
            new DateOnly(2026, 10, 10)) with
        { Book = book };

        Result<PostingReceipt> posted = await harness.Posting.PostAsync(request, token);
        if (posted.IsFailure)
        {
            Proof.Fail($"تعذّر ترحيل {documentId}", posted.Errors[0].Code + ": " + posted.Errors[0].MessageAr);
        }
    }

    private static async Task<int> TamperAsync(Tamper scenario, Guid tenant, string book, CancellationToken token)
    {
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();
        await using NpgsqlCommand command = new(scenario.Sql, owner);
        command.Parameters.AddWithValue(tenant);
        command.Parameters.AddWithValue(book);
        command.Parameters.AddWithValue(scenario.Sequence);
        return await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>التوازن بعملة الشركة ما زال سليماً — ولو اكتفينا به لما رأينا شيئاً.</summary>
    private static async Task AssertStillBalancedAsync(Guid tenant, string book, string name, CancellationToken token)
    {
        await using NpgsqlConnection app = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select sum(l.debit_company) - sum(l.credit_company)
              from ledger.journal_line l join ledger.chain_link c on c.entry_id = l.entry_id
             where c.company_id = $1 and c.book_id = $2
            """, app);
        command.Parameters.AddWithValue(tenant);
        command.Parameters.AddWithValue(book);
        decimal difference = (decimal)(await command.ExecuteScalarAsync(token))!;

        Proof.Require(difference == 0m,
            $"[{name}] الدفتر ما زال متوازناً بعد العبث — فلا فحص محاسبي يطلق",
            $"فرق المدين عن الدائن {Proof.Money(difference)}");
    }

    private static async Task AssertStoredCanonVersionAsync(
        Guid tenant, string book, string expected, CancellationToken token)
    {
        List<string> versions = await StoredVersionsAsync(tenant, book, token);
        Proof.Require(versions.Count > 0 && versions.All(v => v == expected),
            $"السلسلة مكتوبة كلها بـ{expected}", string.Join(",", versions));
    }

    private static async Task<List<string>> StoredVersionsAsync(Guid tenant, string book, CancellationToken token)
    {
        await using NpgsqlConnection app = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select canon_version from ledger.chain_link
             where company_id = $1 and book_id = $2 order by chain_seq
            """, app);
        command.Parameters.AddWithValue(tenant);
        command.Parameters.AddWithValue(book);

        List<string> versions = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }
}
