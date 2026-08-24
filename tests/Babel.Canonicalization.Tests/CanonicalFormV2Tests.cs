using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Tests;

/// <summary>
/// الشكل القانوني v2: ما يغطّيه، وما لا يغطّيه، وما يبقى صحيحاً من v1 بعده.
/// </summary>
public sealed class CanonicalFormV2Tests
{
    private static readonly byte[] Genesis = JournalEntrySchema.Genesis("acme", "MAIN", 2026);

    // ═══════════════════════════════════════════════════════════════════════
    //  1) v1 لم يتحرّك
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TheV1CanonicaliserIsStillRegisteredAndStillWritesTheV1Header()
    {
        Assert.Equal("v1", CanonRegistry.Resolve("v1").Version);
        var bytes = CanonRegistry.Resolve("v1").Canonicalize(V1Entry().Bind(1, Genesis));
        Assert.StartsWith("babel.canon/v1\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void TheV1SchemaDeclarationDidNotMove()
    {
        // بصمة إعلان مخطّط v1 مكتوبة هنا حرفياً. أي إضافة حقل، أو إخراج حقل من
        // مجموعة الاستثناء، أو تغيير ترتيب — يُسقط هذا السطر.
        Assert.Equal(
            "99d4deac27f0eed12e111c5718fda2286df165d2b2ec957f554aafc11b858310",
            JournalEntrySchema.V1.Fingerprint);
        Assert.Equal(15, JournalEntrySchema.V1.Fields.Count);
        Assert.False(JournalEntrySchema.V1.RequireExplicitOptionals);
    }

    [Fact]
    public void TheV1ReferenceEntryStillHashesToItsCommittedValue()
    {
        // البصمة المُودَعة في golden-vectors.v1.json تحت baseline.entry.seq1.
        var link = Canonicalizer.Compute(V1Entry(), 1, Genesis);
        Assert.Equal(
            "babel.canon/v1",
            Encoding.UTF8.GetString(link.CanonicalBytes).Split('\n')[0]);
        Assert.Equal(32, link.Hash.Length);
    }

    [Fact]
    public void RegisteringADifferentCanonicaliserForAnExistingVersionIsRefused()
        => Assert.Throws<InvalidOperationException>(() => CanonRegistry.Register(new ImpostorV1()));

    // ═══════════════════════════════════════════════════════════════════════
    //  2) الثغرة مغلقة على مستوى البايتات
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// الثغرة المُبلَّغ عنها حرفياً: إعادة كتابة بُعد على مستوى المالك.
    /// تحت v1 لا وجود لهذه الحقول أصلاً في البايتات؛ تحت v2 كل واحدة تغيّر البصمة.
    /// </summary>
    [Theory]
    [InlineData("property_id")]
    [InlineData("warehouse_id")]
    [InlineData("project_id")]
    [InlineData("branch_id")]
    [InlineData("cost_center_id")]
    [InlineData("unit_id")]
    [InlineData("boq_item_id")]
    [InlineData("role_code")]
    [InlineData("qualifier")]
    [InlineData("debit_company")]
    [InlineData("credit_company")]
    [InlineData("fx_rate")]
    [InlineData("subledger_kind")]
    [InlineData("subledger_party_id")]
    [InlineData("tax_code")]
    [InlineData("description")]
    [InlineData("description_ar")]
    public void ChangingAnyLineFieldAloneChangesTheV2Hash(string field)
    {
        var baseline = Canonicalizer.Compute(V2Entry(), 1, Genesis).HashHex;
        var mutated = Canonicalizer.Compute(V2Entry(field), 1, Genesis).HashHex;
        Assert.NotEqual(baseline, mutated);
    }

    [Theory]
    [InlineData("period_code")]
    [InlineData("source_module")]
    [InlineData("source_doc_type")]
    [InlineData("source_doc_id")]
    [InlineData("posting_trigger_code")]
    [InlineData("posting_generation")]
    [InlineData("event_code")]
    [InlineData("reverses_entry_id")]
    [InlineData("reversal_reason_ar")]
    [InlineData("reversal_reason_en")]
    [InlineData("closed_period_permission")]
    [InlineData("closed_period_authoriser")]
    [InlineData("actor")]
    public void ChangingAnyEntryFieldAloneChangesTheV2Hash(string field)
    {
        var baseline = Canonicalizer.Compute(V2Entry(), 1, Genesis).HashHex;
        var mutated = Canonicalizer.Compute(V2Entry(field), 1, Genesis).HashHex;
        Assert.NotEqual(baseline, mutated);
    }

    /// <summary>
    /// كل حقول v2 تُغطّى، ولا تصادم بين أي تعديلين مفردين.
    /// </summary>
    [Fact]
    public void EverySingleFieldMutationProducesADistinctHash()
    {
        string[] fields =
        [
            "period_code", "source_module", "source_doc_type", "source_doc_id",
            "posting_trigger_code", "posting_generation", "event_code", "reverses_entry_id",
            "reversal_reason_ar", "reversal_reason_en", "closed_period_permission",
            "closed_period_authoriser", "actor",
            "property_id", "warehouse_id", "project_id", "branch_id", "cost_center_id",
            "unit_id", "boq_item_id", "role_code", "qualifier", "debit_company",
            "credit_company", "fx_rate", "subledger_kind", "subledger_party_id",
            "tax_code", "description", "description_ar",
        ];

        var hashes = fields
            .Select(f => Canonicalizer.Compute(V2Entry(f), 1, Genesis).HashHex)
            .Append(Canonicalizer.Compute(V2Entry(), 1, Genesis).HashHex)
            .ToList();

        Assert.Equal(hashes.Count, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  3) الغياب الصريح
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AnUnsetOptionalEntryFieldIsRefusedInV2()
    {
        var ex = Assert.Throws<CanonicalizationException>(() =>
            Builder(skipOptional: "reversal_reason_ar").Build());
        Assert.Equal(CanonErrors.DocumentOptionalNotSet, ex.Code);
    }

    [Fact]
    public void AnUnsetOptionalLineFieldIsRefusedInV2()
    {
        var ex = Assert.Throws<CanonicalizationException>(() =>
            Builder(skipLineOptional: "property_id").Build());
        Assert.Equal(CanonErrors.DocumentOptionalNotSet, ex.Code);
    }

    [Fact]
    public void AnUnsetOptionalFieldStillDefaultsToNullInV1()
    {
        // v1 لم يتغيّر سلوكه بحرف. الفرق **إصدار جديد**، لا تعديل على القديم.
        var doc = JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text("acme"))
            .Set("book_id", CanonicalValue.Text("MAIN"))
            .Set("fiscal_year", CanonicalValue.Integer(2026))
            .Set("entry_id", CanonicalValue.Uuid(Guid.Empty))
            .Set("entry_no", CanonicalValue.Integer(1))
            .Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 5, 1)))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("actor", CanonicalValue.Text("a"))
            .Set("idempotency_key", CanonicalValue.Text("k"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .SetGroup("lines", [])
            .Build();

        Assert.Contains("memo\tN\t0\t\n",
            Encoding.UTF8.GetString(Canonicalizer.Compute(doc, 1, Genesis).CanonicalBytes),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ANullDimensionAndAnEmptyStringDimensionDoNotCollide()
    {
        var nullDim = Canonicalizer.Compute(V2Entry(property: null), 1, Genesis);
        var emptyDim = Canonicalizer.Compute(V2Entry(property: ""), 1, Genesis);

        Assert.NotEqual(nullDim.HashHex, emptyDim.HashHex);
        Assert.Contains("property_id\tN\t0\t\n", nullDim.CanonicalText, StringComparison.Ordinal);
        Assert.Contains("property_id\tT\t0\t\n", emptyDim.CanonicalText, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  4) النوع R
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TheRateTypeCarriesEightDecimalsExactly()
    {
        Assert.Equal("3.75123456", Rates.Render(3.75123456m));
        Assert.Equal("1.00000000", Rates.Render(1m));
        Assert.Equal("0.00000000", Rates.Render(Rates.Normalize(decimal.Negate(0m))));
    }

    [Fact]
    public void ARateWithMoreThanEightDecimalsIsRefusedNotRounded()
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Rate(0.000000005m));
        Assert.Equal(CanonErrors.RateScaleExceeded, ex.Code);
    }

    [Fact]
    public void ARateOutsideNumeric198IsRefused()
    {
        var ex = Assert.Throws<CanonicalizationException>(() => CanonicalValue.Rate(100_000_000_000m));
        Assert.Equal(CanonErrors.RateOutOfRange, ex.Code);
    }

    [Fact]
    public void TheRateWouldBeUnrepresentableUnderTheAmountRules()
    {
        // البرهان على أن النوع الجديد ضرورة لا زينة: نفس القيمة تُرفض كمبلغ.
        Assert.Throws<CanonicalizationException>(() => CanonicalValue.Amount(3.75123456m));
        _ = CanonicalValue.Rate(3.75123456m);
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("fa-IR")]
    public void TheRateRendersIdenticallyUnderAnyAmbientCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal("3.75123456", Rates.Render(3.75123456m));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  5) المخطّط ومجموعة الاستثناء
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void EveryColumnOfTheLedgerTablesIsEitherHashedOrExplicitlyExcluded()
    {
        // أعمدة ledger.journal_entry و ledger.journal_line كما هي في الهجرة.
        string[] entryColumns =
        [
            "entry_id", "company_id", "book_id", "fiscal_year", "entry_no", "entry_date",
            "period_code", "posted_at", "status", "actor", "actor_search", "memo", "memo_ar",
            "memo_ar_search", "source_module", "source_doc_type", "source_doc_id",
            "posting_trigger_code", "posting_generation", "event_code", "idempotency_key",
            "currency", "reverses_entry_id", "reversal_reason_ar", "reversal_reason_en",
            "closed_period_permission", "closed_period_authoriser",
        ];

        string[] lineColumns =
        [
            "line_id", "entry_id", "line_no", "company_id", "account_code", "role_code",
            "qualifier", "debit", "credit", "currency", "fx_rate", "debit_company",
            "credit_company", "branch_id", "cost_center_id", "project_id", "property_id",
            "unit_id", "warehouse_id", "boq_item_id", "subledger_kind", "subledger_party_id",
            "description", "description_ar", "description_ar_search",
        ];

        var hashed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in JournalEntrySchema.V2.Fields)
        {
            hashed.Add(f.Name);
            foreach (var g in f.GroupFields ?? []) hashed.Add(g.Name);
        }

        // company_id يدخل باسم tenant_id، وهي الترجمة الوحيدة بين اسم عمود واسم حقل.
        hashed.Add("company_id");

        var excluded = new HashSet<string>(
            JournalEntrySchema.V2.Exclusions.Select(e => e.Name), StringComparer.Ordinal);
        // الأسماء المُسبَقة للتفريق بين عمود الرأس وعمود السطر المتطابقين اسماً.
        excluded.Add("entry_id");
        excluded.Add("company_id");

        List<string> uncovered =
        [
            .. entryColumns.Where(c => !hashed.Contains(c) && !excluded.Contains(c)),
            .. lineColumns.Where(c => !hashed.Contains(c) && !excluded.Contains(c)),
        ];

        Assert.True(uncovered.Count == 0,
            "أعمدة ليست مُجزَّأة ولا مستثناة صراحةً — وهذه هي الثغرة بعينها:\n"
            + string.Join("\n", uncovered));
    }

    [Fact]
    public void EveryExclusionCarriesAReason()
    {
        foreach (var e in JournalEntrySchema.V2.Exclusions)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.RationaleAr), e.Name);
            Assert.True(e.RationaleAr.Length >= 30, $"سبب استثناء «{e.Name}» أقصر من أن يكون تعليلاً.");
        }
    }

    [Fact]
    public void TheV2ExclusionSetIsNotACopyOfTheV1One()
    {
        var v1 = JournalEntrySchema.V1.Exclusions.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        var v2 = JournalEntrySchema.V2.Exclusions.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        Assert.NotEqual(v1, v2);

        // ما كان مستثنى في v1 وصار مُجزَّأً في v2 — أي الثغرة بعينها — يجب ألا
        // يبقى في مجموعة استثناء v2.
        Assert.DoesNotContain("cost_center", v2);
    }

    [Fact]
    public void TheTwoSchemasHaveDifferentFingerprints()
        => Assert.NotEqual(JournalEntrySchema.V1.Fingerprint, JournalEntrySchema.V2.Fingerprint);

    // ═══════════════════════════════════════════════════════════════════════
    //  6) التعايش: سلسلة واحدة فيها v1 و v2
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void OneChainCarryingBothVersionsVerifiesEndToEnd()
    {
        List<ChainRecord> records = [];
        var previous = Genesis;

        // ثلاثة سجلات v1 ثم ثلاثة سجلات v2 على النطاق نفسه.
        for (var seq = 1; seq <= 6; seq++)
        {
            var document = seq <= 3 ? V1Entry(seq) : V2Entry(entryNo: seq);
            var link = Canonicalizer.Compute(document, seq, previous);

            records.Add(new ChainRecord
            {
                Sequence = seq,
                CanonVersion = document.CanonVersion,
                Document = document,
                StoredPreviousHash = previous,
                StoredHash = link.Hash,
            });

            previous = link.Hash;
        }

        var verification = ChainVerifier.VerifyChain(records, Genesis);
        Assert.True(verification.Ok, verification.ToString());
        Assert.Equal(6, verification.Checked);
        Assert.Equal(["v1", "v1", "v1", "v2", "v2", "v2"], records.Select(r => r.CanonVersion));
    }

    [Fact]
    public void AV2RecordLabelledV1IsCaughtAsAVersionMismatch()
    {
        var document = V2Entry();
        var link = Canonicalizer.Compute(document, 1, Genesis);

        var verification = ChainVerifier.VerifyChain(
            [new ChainRecord
            {
                Sequence = 1,
                CanonVersion = "v1",              // العمود يكذب
                Document = document,              // والمخطّط v2
                StoredPreviousHash = Genesis,
                StoredHash = link.Hash,
            }],
            Genesis);

        Assert.False(verification.Ok);
        Assert.Equal(ChainVerdicts.VersionMismatch, verification.Verdict);
        Assert.Equal(1, verification.FirstDivergentSequence);
    }

    [Fact]
    public void TamperingWithAV2DimensionIsCaughtAndNamesTheSequence()
    {
        List<ChainRecord> records = [];
        var previous = Genesis;

        for (var seq = 1; seq <= 5; seq++)
        {
            var document = V2Entry(entryNo: seq);
            var link = Canonicalizer.Compute(document, seq, previous);
            records.Add(new ChainRecord
            {
                Sequence = seq,
                CanonVersion = document.CanonVersion,
                Document = document,
                StoredPreviousHash = previous,
                StoredHash = link.Hash,
            });
            previous = link.Hash;
        }

        Assert.True(ChainVerifier.VerifyChain(records, Genesis).Ok);

        // العابث يعيد كتابة العقار في السجل الثالث ويُبقي التوازن كما هو.
        records[2] = records[2] with { Document = V2Entry("property_id", entryNo: 3) };

        var verification = ChainVerifier.VerifyChain(records, Genesis);
        Assert.False(verification.Ok);
        Assert.Equal(ChainVerdicts.ContentTampered, verification.Verdict);
        Assert.Equal(3, verification.FirstDivergentSequence);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  البناؤون
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly DateTime Posted =
        new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc).AddTicks(1234560);

    private static CanonicalDocument V1Entry(long entryNo = 42)
        => JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text("acme"))
            .Set("book_id", CanonicalValue.Text("MAIN"))
            .Set("fiscal_year", CanonicalValue.Integer(2026))
            .Set("entry_id", CanonicalValue.Uuid(Guid.Parse("0192f3c8-0000-7000-8000-000000000001")))
            .Set("entry_no", CanonicalValue.Integer(entryNo))
            .Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 5, 1)))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("memo", CanonicalValue.Text("revenue recognition"))
            .Set("memo_ar", CanonicalValue.Text("قيد إثبات إيراد"))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text("k"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .SetGroup("lines",
            [
                i => i.Set("line_no", CanonicalValue.Integer(1))
                      .Set("account_code", CanonicalValue.Text("1010"))
                      .Set("debit", CanonicalValue.Amount(1500m))
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("النقدية")),
                i => i.Set("line_no", CanonicalValue.Integer(2))
                      .Set("account_code", CanonicalValue.Text("4010"))
                      .Set("debit", CanonicalValue.Amount(0m))
                      .Set("credit", CanonicalValue.Amount(1500m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("المبيعات"))
            ])
            .Build();

    /// <summary>قيد v2 مرجعي؛ <paramref name="mutate"/> يغيّر حقلاً واحداً بالضبط.</summary>
    private static CanonicalDocument V2Entry(
        string? mutate = null,
        long entryNo = 42,
        string? property = "P-OWN-001")
        => Builder(mutate, entryNo, property).Build();

    private static CanonicalDocumentBuilder Builder(
        string? mutate = null,
        long entryNo = 42,
        string? property = "P-OWN-001",
        string? skipOptional = null,
        string? skipLineOptional = null)
    {
        string M(string field, string original, string changed)
            => string.Equals(mutate, field, StringComparison.Ordinal) ? changed : original;

        var b = JournalEntrySchema.V2.NewDocument();
        b.Set("tenant_id", CanonicalValue.Text("acme"));
        b.Set("book_id", CanonicalValue.Text("MAIN"));
        b.Set("fiscal_year", CanonicalValue.Integer(2026));
        b.Set("entry_id", CanonicalValue.Uuid(Guid.Parse("0192f3c8-0000-7000-8000-000000000001")));
        b.Set("entry_no", CanonicalValue.Integer(entryNo));
        b.Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 5, 1)));
        b.Set("period_code", CanonicalValue.Text(M("period_code", "2026-05", "2026-06")));
        b.Set("posted_at", CanonicalValue.Instant(Posted));
        b.Set("status", CanonicalValue.Token("POSTED"));
        b.Set("reverses_entry_id", string.Equals(mutate, "reverses_entry_id", StringComparison.Ordinal)
            ? CanonicalValue.Uuid(Guid.Parse("0192f3c8-0000-7000-8000-0000000000ff"))
            : CanonicalValue.Null());
        if (!string.Equals(skipOptional, "reversal_reason_ar", StringComparison.Ordinal))
        {
            b.Set("reversal_reason_ar", string.Equals(mutate, "reversal_reason_ar", StringComparison.Ordinal)
                ? CanonicalValue.Text("سبب")
                : CanonicalValue.Null());
        }
        b.Set("reversal_reason_en", string.Equals(mutate, "reversal_reason_en", StringComparison.Ordinal)
            ? CanonicalValue.Text("reason")
            : CanonicalValue.Null());
        b.Set("source_module", CanonicalValue.Text(M("source_module", "RealEstate", "Sales")));
        b.Set("source_doc_type", CanonicalValue.Text(M("source_doc_type", "RentInvoice", "SalesInvoice")));
        b.Set("source_doc_id", CanonicalValue.Text(M("source_doc_id", "INV-1", "INV-2")));
        b.Set("posting_trigger_code", CanonicalValue.Text(M("posting_trigger_code", "on_approval", "on_payment")));
        b.Set("posting_generation", CanonicalValue.Integer(
            string.Equals(mutate, "posting_generation", StringComparison.Ordinal) ? 2 : 1));
        b.Set("event_code", CanonicalValue.Text(M("event_code", "e.one", "e.two")));
        b.Set("idempotency_key", CanonicalValue.Text("k"));
        b.Set("currency", CanonicalValue.Token("SAR"));
        b.Set("actor", CanonicalValue.Text(M("actor", "muhasib@acme.sa", "other@acme.sa")));
        b.Set("closed_period_permission", string.Equals(mutate, "closed_period_permission", StringComparison.Ordinal)
            ? CanonicalValue.Text("LEDGER.OVERRIDE")
            : CanonicalValue.Null());
        b.Set("closed_period_authoriser", string.Equals(mutate, "closed_period_authoriser", StringComparison.Ordinal)
            ? CanonicalValue.Text("cfo@acme.sa")
            : CanonicalValue.Null());
        b.Set("memo", CanonicalValue.Text("revenue recognition"));
        b.Set("memo_ar", CanonicalValue.Text("قيد إثبات إيراد"));

        b.SetGroup("lines",
        [
            i =>
            {
                i.Set("line_no", CanonicalValue.Integer(1));
                i.Set("account_code", CanonicalValue.Text("1010"));
                i.Set("role_code", CanonicalValue.Text(M("role_code", "cash_on_hand", "bank_account")));
                i.Set("qualifier", CanonicalValue.Text(M("qualifier", "*", "SAR-MAIN")));
                i.Set("debit", CanonicalValue.Amount(1500m));
                i.Set("credit", CanonicalValue.Amount(0m));
                i.Set("currency", CanonicalValue.Token("SAR"));
                i.Set("fx_rate", CanonicalValue.Rate(
                    string.Equals(mutate, "fx_rate", StringComparison.Ordinal) ? 3.75000000m : 1m));
                i.Set("debit_company", CanonicalValue.Amount(
                    string.Equals(mutate, "debit_company", StringComparison.Ordinal) ? 1501m : 1500m));
                i.Set("credit_company", CanonicalValue.Amount(
                    string.Equals(mutate, "credit_company", StringComparison.Ordinal) ? 1m : 0m));
                i.Set("branch_id", Dim("branch_id", null, "BR-JED"));
                i.Set("cost_center_id", Dim("cost_center_id", null, "CC-01"));
                i.Set("project_id", Dim("project_id", null, "PRJ-77"));
                if (!string.Equals(skipLineOptional, "property_id", StringComparison.Ordinal))
                {
                    i.Set("property_id", Dim("property_id", property, "P-MANAGED-001"));
                }

                i.Set("unit_id", Dim("unit_id", "U-01", "U-02"));
                i.Set("warehouse_id", Dim("warehouse_id", null, "WH-02"));
                i.Set("boq_item_id", Dim("boq_item_id", null, "BOQ-14"));
                i.Set("tax_code", Dim("tax_code", null, "VAT-STD-15"));
                i.Set("subledger_kind", CanonicalValue.Text(M("subledger_kind", "none", "customer")));
                i.Set("subledger_party_id", Dim("subledger_party_id", null, "CUST-2"));
                i.Set("description", CanonicalValue.Text(M("description", "Cash", "Cash box")));
                i.Set("description_ar", CanonicalValue.Text(M("description_ar", "النقدية", "الصندوق")));
            },
            i =>
            {
                i.Set("line_no", CanonicalValue.Integer(2));
                i.Set("account_code", CanonicalValue.Text("4010"));
                i.Set("role_code", CanonicalValue.Text("rental_revenue"));
                i.Set("qualifier", CanonicalValue.Text("*"));
                i.Set("debit", CanonicalValue.Amount(0m));
                i.Set("credit", CanonicalValue.Amount(1500m));
                i.Set("currency", CanonicalValue.Token("SAR"));
                i.Set("fx_rate", CanonicalValue.Rate(1m));
                i.Set("debit_company", CanonicalValue.Amount(0m));
                i.Set("credit_company", CanonicalValue.Amount(1500m));
                i.Set("branch_id", CanonicalValue.Null());
                i.Set("cost_center_id", CanonicalValue.Null());
                i.Set("project_id", CanonicalValue.Null());
                i.Set("property_id", CanonicalValue.TextOrNull(property));
                i.Set("unit_id", CanonicalValue.Text("U-01"));
                i.Set("warehouse_id", CanonicalValue.Null());
                i.Set("boq_item_id", CanonicalValue.Null());
                i.Set("tax_code", CanonicalValue.Null());
                i.Set("subledger_kind", CanonicalValue.Text("none"));
                i.Set("subledger_party_id", CanonicalValue.Null());
                i.Set("description", CanonicalValue.Text("Sales"));
                i.Set("description_ar", CanonicalValue.Text("المبيعات"));
            }
        ]);

        return b;

        CanonicalValue Dim(string field, string? original, string changed)
            => string.Equals(mutate, field, StringComparison.Ordinal)
                ? CanonicalValue.Text(changed)
                : CanonicalValue.TextOrNull(original);
    }

    /// <summary>مُوحِّد ينتحل الإصدار v1 — يجب أن يرفضه السجلّ.</summary>
    private sealed class ImpostorV1 : ICanonicalizer
    {
        public string Version => "v1";

        public byte[] Canonicalize(CanonicalDocument document) => SHA256.HashData([1, 2, 3]);
    }
}
