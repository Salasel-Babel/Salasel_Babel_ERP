using System.Globalization;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Golden;

/// <summary>
/// مجموعة المتجهات الذهبية للإصدار <b>v2</b>.
///
/// <para>
/// المجموعة <b>مستقلّة تماماً</b> عن مجموعة v1 ولا تشاركها سطراً: مجموعة مشتركة
/// تعني أن تعديلاً «لتحسين» متجه v2 يحرّك متجه v1 معه، وهو بالضبط ما تمنعه
/// المواصفة. والملفان يُفحصان معاً في كل بناء.
/// </para>
/// <para>
/// وتغطّي كل ما تغطّيه مجموعة v1 (المبالغ، اللحظات، التوقيت الصيفي، النص العربي،
/// محارف التحكّم، التطويل وأشكال الألف، الفراغ مقابل الغياب، حقن حدود الحقول،
/// استقلال ترتيب الضبط) <b>زائداً</b> متجهاً لكل حقل دخل الحقيقة المُوقَّعة في v2:
/// الأبعاد كلها، ورمز الدور، والمبالغ بعملة الشركة، وسعر الصرف، والدفتر المساعد،
/// والفترة، والمصدر، ورابط العكس، والإذن الاستثنائي.
/// </para>
/// </summary>
public static class GoldenVectorSetV2
{
    // ===== ثوابت مشتركة، مجمّدة =====
    private const string Tenant = "acme";
    private const string Book = "MAIN";
    private const int Year = 2026;

    private static readonly Guid EntryId = Guid.Parse("0192f3c8-0000-7000-8000-000000000001");
    private static readonly Guid ReversedId = Guid.Parse("0192f3c8-0000-7000-8000-0000000000ff");
    private static readonly byte[] Gen = JournalEntrySchema.Genesis(Tenant, Book, Year);
    private static readonly DateTime Posted =
        new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc).AddTicks(1234560);
    private static readonly DateOnly EntryDate = new(2026, 5, 1);

    private const string MemoAr = "قيد إثبات إيراد مبيعات - فرع الرياض";

    // محارف غير مرئية، مكتوبة بترميز الهروب حتى تبقى الشيفرة قابلة للقراءة والبحث
    private const string Rlm = "\u200F";      // RIGHT-TO-LEFT MARK
    private const string Lrm = "\u200E";      // LEFT-TO-RIGHT MARK
    private const string Rle = "\u202B";      // RIGHT-TO-LEFT EMBEDDING
    private const string Rlo = "\u202E";      // RIGHT-TO-LEFT OVERRIDE
    private const string Alm = "\u061C";      // ARABIC LETTER MARK
    private const string Bom = "\uFEFF";      // ZERO WIDTH NO-BREAK SPACE
    private const string Zwj = "\u200D";      // ZERO WIDTH JOINER
    private const string Nbsp = "\u00A0";     // NO-BREAK SPACE
    private const string Tatweel = "\u0640";  // ARABIC TATWEEL

    private const string AlefPlain = "\u0627";      // alef
    private const string AlefHamzaAbove = "\u0623"; // alef hamza above
    private const string AlefHamzaBelow = "\u0625"; // alef hamza below
    private const string AlefMadda = "\u0622";      // alef madda

    private const string LamAlefLigature = "\uFEFB";             // lam-alef ligature (شكل عرض)
    private const string ArabicIndic100 = "\u0661\u0660\u0660"; // 100 بالأرقام العربية-الهندية

    // «أرباح» بشكليها المركّب والمفكّك. مكتوبان بالهروب لأنهما متطابقان بصرياً تماماً.
    private const string ArbahComposed = "\u0623\u0631\u0628\u0627\u062D";
    private const string ArbahDecomposed = "\u0627\u0654\u0631\u0628\u0627\u062D";

    /// <summary>أنواع الدفتر المساعد كما يكتبها محرك الترحيل.</summary>
    private static readonly string[] SubledgerKinds =
        ["none", "customer", "supplier", "employee", "asset", "treasury"];

    /// <summary>
    /// يبني قيداً مرجعياً v2 مع إمكانية تعديل أي حقل محلّ الاختبار.
    /// <b>كل حقل اختياري يُضبط صراحةً</b> — v2 يرفض الغياب الضمني.
    /// </summary>
    private static CanonicalDocument Entry(
        string memoAr = MemoAr,
        string memo = "revenue recognition",
        decimal amount = 1500.0000m,
        DateTime? postedAt = null,
        DateOnly? entryDate = null,
        string tenant = Tenant,
        string lineDescription = "النقدية",
        long entryNo = 42,
        string status = "POSTED",
        int lineCount = 2,
        string periodCode = "2026-05",
        string sourceModule = "RealEstate",
        string sourceDocType = "RentInvoice",
        string sourceDocId = "INV-1",
        string triggerCode = "on_approval",
        long generation = 1,
        string eventCode = "realestate.rent_invoice.own_property",
        Guid? reverses = null,
        string? reversalAr = null,
        string? reversalEn = null,
        string? permission = null,
        string? authoriser = null,
        string actor = "muhasib@acme.sa",
        string currency = "SAR",
        string roleCode = "cash_on_hand",
        string qualifier = "*",
        decimal fxRate = 1m,
        decimal? companyAmount = null,
        string? branch = null,
        string? costCenter = null,
        string? project = null,
        string? property = "P-OWN-001",
        string? unit = "U-01",
        string? warehouse = null,
        string? boq = null,
        string? taxCode = null,
        string subledgerKind = "none",
        string? subledgerParty = null,
        string lineDescriptionAr = "النقدية")
    {
        var b = JournalEntrySchema.V2.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("entry_no", CanonicalValue.Integer(entryNo))
            .Set("entry_date", CanonicalValue.Date(entryDate ?? EntryDate))
            .Set("period_code", CanonicalValue.Text(periodCode))
            .Set("posted_at", CanonicalValue.Instant(postedAt ?? Posted))
            .Set("status", CanonicalValue.Token(status))
            .Set("reverses_entry_id", CanonicalValue.UuidOrNull(reverses))
            .Set("reversal_reason_ar", CanonicalValue.TextOrNull(reversalAr))
            .Set("reversal_reason_en", CanonicalValue.TextOrNull(reversalEn))
            .Set("source_module", CanonicalValue.Text(sourceModule))
            .Set("source_doc_type", CanonicalValue.Text(sourceDocType))
            .Set("source_doc_id", CanonicalValue.Text(sourceDocId))
            .Set("posting_trigger_code", CanonicalValue.Text(triggerCode))
            .Set("posting_generation", CanonicalValue.Integer(generation))
            .Set("event_code", CanonicalValue.Text(eventCode))
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("currency", CanonicalValue.Token(currency))
            .Set("actor", CanonicalValue.Text(actor))
            .Set("closed_period_permission", CanonicalValue.TextOrNull(permission))
            .Set("closed_period_authoriser", CanonicalValue.TextOrNull(authoriser))
            .Set("memo", CanonicalValue.Text(memo))
            .Set("memo_ar", CanonicalValue.Text(memoAr));

        var company = companyAmount ?? amount;

        var items = new List<Action<CanonicalItemBuilder>>();
        if (lineCount == 2)
        {
            items.Add(i => Line(i, 1, "1010", roleCode, qualifier, amount, 0m, currency, fxRate, company, 0m,
                branch, costCenter, project, property, unit, warehouse, boq, taxCode,
                subledgerKind, subledgerParty, lineDescription, lineDescriptionAr));
            items.Add(i => Line(i, 2, "4010", "rental_revenue", "*", 0m, amount, currency, fxRate, 0m, company,
                null, null, null, property, unit, null, null, null,
                "none", null, "المبيعات", "المبيعات"));
        }
        else
        {
            for (var k = 1; k <= lineCount; k++)
            {
                var n = k;
                items.Add(i => Line(i, n, (1000 + n).ToString(CultureInfo.InvariantCulture),
                    roleCode, qualifier,
                    n % 2 == 1 ? 10.0000m : 0m, n % 2 == 1 ? 0m : 10.0000m,
                    currency, fxRate,
                    n % 2 == 1 ? 10.0000m : 0m, n % 2 == 1 ? 0m : 10.0000m,
                    branch, costCenter, project, property, unit, warehouse, boq, taxCode,
                    subledgerKind, subledgerParty,
                    $"سطر {n.ToString(CultureInfo.InvariantCulture)}",
                    $"سطر {n.ToString(CultureInfo.InvariantCulture)}"));
            }
        }

        b.SetGroup("lines", items);
        return b.Build();
    }

    /// <summary>سطر v2 كاملاً — <b>كل حقل مضبوط صراحةً</b>، والغياب يُكتب Null.</summary>
    private static void Line(
        CanonicalItemBuilder i,
        int lineNo,
        string account,
        string roleCode,
        string qualifier,
        decimal debit,
        decimal credit,
        string currency,
        decimal fxRate,
        decimal debitCompany,
        decimal creditCompany,
        string? branch,
        string? costCenter,
        string? project,
        string? property,
        string? unit,
        string? warehouse,
        string? boq,
        string? taxCode,
        string subledgerKind,
        string? subledgerParty,
        string description,
        string descriptionAr)
        => i.Set("line_no", CanonicalValue.Integer(lineNo))
            .Set("account_code", CanonicalValue.Text(account))
            .Set("role_code", CanonicalValue.Text(roleCode))
            .Set("qualifier", CanonicalValue.Text(qualifier))
            .Set("debit", CanonicalValue.Amount(debit))
            .Set("credit", CanonicalValue.Amount(credit))
            .Set("currency", CanonicalValue.Token(currency))
            .Set("fx_rate", CanonicalValue.Rate(fxRate))
            .Set("debit_company", CanonicalValue.Amount(debitCompany))
            .Set("credit_company", CanonicalValue.Amount(creditCompany))
            .Set("branch_id", CanonicalValue.TextOrNull(branch))
            .Set("cost_center_id", CanonicalValue.TextOrNull(costCenter))
            .Set("project_id", CanonicalValue.TextOrNull(project))
            .Set("property_id", CanonicalValue.TextOrNull(property))
            .Set("unit_id", CanonicalValue.TextOrNull(unit))
            .Set("warehouse_id", CanonicalValue.TextOrNull(warehouse))
            .Set("boq_item_id", CanonicalValue.TextOrNull(boq))
            .Set("tax_code", CanonicalValue.TextOrNull(taxCode))
            .Set("subledger_kind", CanonicalValue.Text(subledgerKind))
            .Set("subledger_party_id", CanonicalValue.TextOrNull(subledgerParty))
            .Set("description", CanonicalValue.Text(description))
            .Set("description_ar", CanonicalValue.Text(descriptionAr));

    private static ChainLink Link(CanonicalDocument d, long seq = 1) => Canonicalizer.Compute(d, seq, Gen);

    /// <summary>متجه «حقل غُطِّي في v2»: تغيير الحقل وحده يجب أن يغيّر البصمة.</summary>
    private static GoldenVector Covered(string field, string descriptionAr, Func<CanonicalDocument> changed)
        => new("covered." + field,
            descriptionAr,
            () => Golden.DifferentHash(
            [
                () => Link(Entry()),
                () => Link(changed())
            ], "تغيير هذا الحقل وحده كان يمرّ أخضر تحت v1"));

    /// <summary>المجموعة كاملة، بترتيب ثابت.</summary>
    public static IReadOnlyList<GoldenVector> All =>
    [
        // ══════════════════════ 1. الأساس والتكوين ══════════════════════
        new("genesis.scope.main",
            "بصمة التكوين لنطاق (مستأجر × دفتر × سنة مالية) — مستقلّة عن الإصدار عمداً",
            () => Golden.Value(Canonicalizer.Hex(Gen), "scope=" + JournalEntrySchema.ChainScope(Tenant, Book, Year))),

        new("genesis.is.version.independent",
            "‼ بصمة التكوين نفسها ل\u0640v1 وv2: السلسلة الواحدة قد تحمل الإصدارين، ولو تغيّر التكوين لانكسر السجل رقم 1 بترقية ثنائي",
            () => Golden.Value(Canonicalizer.Hex(JournalEntrySchema.Genesis(Tenant, Book, Year)),
                "يطابق قيمة المتجه نفسه في مجموعة v1")),

        new("genesis.scope.differs.by.tenant",
            "نطاقان مختلفان يعطيان بصمتي تكوين مختلفتين — السلسلة لكل دفتر لا للمنتج كله",
            () => Golden.Value(
                Canonicalizer.Hex(JournalEntrySchema.Genesis("other", Book, Year)),
                "لا يساوي بصمة تكوين acme")),

        new("baseline.entry.seq1",
            "القيد المرجعي v2، أول السلسلة — البايتات القانونية كاملة بكل الحقول المُغطّاة",
            () => Golden.Bytes(Entry(), 1, Gen)),

        new("baseline.entry.seq2.same.content",
            "نفس المحتوى برقم تسلسل مختلف يعطي بصمة مختلفة — التسلسل داخل البايتات لا بجوارها",
            () => Golden.Bytes(Entry(), 2, Gen)),

        new("baseline.entry.different.prev.hash",
            "نفس المحتوى وبصمة سابقة مختلفة يعطي بصمة مختلفة — الرابط مربوط تشفيرياً",
            () => Golden.Bytes(Entry(), 1, JournalEntrySchema.Genesis("other", Book, Year))),

        new("wire.magic.is.v2",
            "ترويسة الشكل السلكي ل\u0640v2",
            () => Golden.Value(CanonicalV2.Magic)),

        new("schema.fingerprint.v2",
            "بصمة إعلان مخطّط v2: الأسماء والأنواع والترتيب ومجموعة الاستثناء وعلم الضبط الصريح",
            () => Golden.Value(JournalEntrySchema.V2.Fingerprint,
                $"fields={JournalEntrySchema.V2.Fields.Count} exclusions={JournalEntrySchema.V2.Exclusions.Count}")),

        new("schema.fingerprint.v2.differs.from.v1",
            "بصمتا المخطّطين مختلفتان — وهذا هو المقصود: v2 يغطّي أكثر",
            () => Golden.Value(
                JournalEntrySchema.V2.Fingerprint == JournalEntrySchema.V1.Fingerprint ? "COLLISION" : "DISTINCT")),

        new("document.unbound.rejected",
            "لا يمكن الحصول على بايتات من مستند غير مرتبط بالسلسلة — نسيان التسلسل مستحيل بنيوياً",
            () => Golden.Reject(() => Canonicalizer.Canonicalize(Entry()))),

        new("chain.previous.hash.wrong.length",
            "بصمة سابقة بطول غير 32 بايت مرفوضة",
            () => Golden.Reject(() => Canonicalizer.Compute(Entry(), 1, new byte[31]))),

        new("chain.sequence.zero.rejected",
            "رقم تسلسل صفر مرفوض — السلسلة تبدأ من 1",
            () => Golden.Reject(() => Canonicalizer.Compute(Entry(), 0, Gen))),

        // ══════════════════════ 2. المبالغ ══════════════════════
        new("amount.100.five.source.forms",
            "مئة بخمسة أشكال مصدرية: 100m و100.0m و100.00m و100.0000m و100.00000m — بصمة واحدة",
            () => Golden.SameHash(
            [
                () => Link(Entry(amount: 100m)),
                () => Link(Entry(amount: 100.0m)),
                () => Link(Entry(amount: 100.00m)),
                () => Link(Entry(amount: 100.0000m)),
                () => Link(Entry(amount: 100.00000m))
            ], "بايت المقياس في decimal.GetBits يختلف بين هذه القيم؛ الشكل اللفظي واحد")),

        new("amount.100.from.exponent.literal",
            "1.0E2 مقروءة من نصّ تعطي نفس بصمة 100.0000m — الصيغة الأسّية لا تُكتب أبداً",
            () => Golden.SameHash(
            [
                () => Link(Entry(amount: 100.0000m)),
                () => Link(Entry(amount: decimal.Parse("1.0E2", NumberStyles.Float, CultureInfo.InvariantCulture))),
                () => Link(Entry(amount: decimal.Parse("+100.00", NumberStyles.Any, CultureInfo.InvariantCulture)))
            ])),

        new("amount.rendered.exactly.100.0000", "الشكل اللفظي الوحيد للمئة",
            () => Golden.Value(Amounts.Render(100m))),

        new("amount.negative", "مبلغ سالب: -2500.7500",
            () => Golden.Value(Amounts.Render(-2500.75m))),

        new("amount.zero", "الصفر: 0.0000 بلا إشارة",
            () => Golden.Value(Amounts.Render(0m))),

        new("amount.negative.zero.normalised",
            "الصفر السالب: PostgreSQL تُسقط إشارته عند الدورة (مقيس) — نُثبّت 0.0000 هنا أيضاً",
            () => Golden.Value(Amounts.Render(decimal.Negate(0.0000m)), "GetBits قبل الدورة يحمل بت الإشارة")),

        new("amount.max.numeric.19.4", "أكبر قيمة تسع في numeric(19,4)",
            () => Golden.Value(Amounts.Render(999_999_999_999_999.9999m))),

        new("amount.min.numeric.19.4", "أصغر قيمة تسع في numeric(19,4)",
            () => Golden.Value(Amounts.Render(-999_999_999_999_999.9999m))),

        new("amount.max.in.a.full.entry",
            "أكبر مبلغ داخل قيد كامل — البايتات كاملة",
            () => Golden.Bytes(Entry(amount: 999_999_999_999_999.9999m), 1, Gen)),

        new("amount.overflow.numeric.19.4.rejected",
            "قيمة تتجاوز numeric(19,4) مرفوضة — قيمة لا تُخزَّن لا يجوز أن تُجزَّأ",
            () => Golden.Reject(() => CanonicalValue.Amount(1_000_000_000_000_000.0000m))),

        new("amount.five.decimals.rejected.not.rounded",
            "خمس خانات عشرية تُرفض ولا تُقرَّب: .NET نصف-إلى-الزوجي وPostgreSQL نصف-بعيداً-عن-الصفر (مقيس)",
            () => Golden.Reject(() => CanonicalValue.Amount(0.00005m),
                "decimal.Round(0.00005m,4)=0.0000 بينما PG تخزّن 0.0001")),

        new("amount.literal.comma.rejected", "«100,00» انزلاق لغوي مرفوض",
            () => Golden.Reject(() => Amounts.ParseCanonical("100,00"))),

        new("amount.literal.scale2.rejected", "«100.00» شكل غير قانوني: المقياس 4 دائماً",
            () => Golden.Reject(() => Amounts.ParseCanonical("100.00"))),

        new("amount.literal.plus.sign.rejected", "«+100.0000» علامة الموجب مرفوضة",
            () => Golden.Reject(() => Amounts.ParseCanonical("+100.0000"))),

        new("amount.literal.exponent.rejected", "«1.0E2» صيغة أسّية مرفوضة",
            () => Golden.Reject(() => Amounts.ParseCanonical("1.0E2"))),

        new("amount.literal.arabic.indic.digits.rejected",
            "«\u0661\u0660\u0660.\u0660\u0660\u0660\u0660» بأرقام عربية-هندية: تبدو صحيحة وتُجزَّأ خطأ",
            () => Golden.Reject(() => Amounts.ParseCanonical(ArabicIndic100 + ".\u0660\u0660\u0660\u0660"))),

        new("amount.literal.negative.zero.rejected", "«-0.0000» شكل غير قانوني: للصفر شكل واحد",
            () => Golden.Reject(() => Amounts.ParseCanonical("-0.0000"))),

        new("amount.literal.leading.zero.rejected", "«0100.0000» صفر بادئ مرفوض",
            () => Golden.Reject(() => Amounts.ParseCanonical("0100.0000"))),

        new("amount.normalize.raises.scale.to.exactly.four",
            "التطبيع عند الحدّ يرفع مقياس 100.00m إلى 4 بالضبط، فيتطابق المخزَّن والمُجزَّأ",
            () => Golden.Value(
                ((decimal.GetBits(Amounts.Normalize(100.00m))[3] >> 16) & 0xFF)
                    .ToString(CultureInfo.InvariantCulture) + "|" + Amounts.Render(Amounts.Normalize(100.00m)),
                "مقياس decimal.GetBits قبل التطبيع = 2")),

        new("amount.normalize.clears.negative.zero.sign",
            "التطبيع يمحو بت إشارة الصفر السالب، تماماً كما تفعل PostgreSQL عند الدورة (مقيس)",
            () => Golden.Value(
                decimal.GetBits(Amounts.Normalize(decimal.Negate(0.0000m)))[3].ToString("X8", CultureInfo.InvariantCulture),
                "قبل التطبيع: 80010000")),

        // ══════════════════════ 3. سعر الصرف — النوع R الجديد ══════════════════════
        new("rate.one.canonical.form", "الشكل اللفظي الوحيد للواحد الصحيح كسعر صرف: مقياس 8",
            () => Golden.Value(Rates.Render(1m))),

        new("rate.eight.decimals.preserved",
            "‼ 3.75123456 سعر مشروع في numeric(19,8) — ولو مرّ بقواعد المبلغ (مقياس 4) لرُفض أو قُرِّب",
            () => Golden.Value(Rates.Render(3.75123456m))),

        new("rate.scale.raised.to.exactly.eight",
            "التطبيع يرفع مقياس 3.75m إلى 8 بالضبط",
            () => Golden.Value(Rates.Render(Rates.Normalize(3.75m)))),

        new("rate.zero.and.negative.zero.render.alike",
            "الصفر السالب لسعر الصرف يُطبَّع كما يفعل المبلغ",
            () => Golden.Value(Rates.Render(Rates.Normalize(decimal.Negate(0.00000000m))))),

        new("rate.max.numeric.19.8", "أكبر قيمة تسع في numeric(19,8)",
            () => Golden.Value(Rates.Render(99_999_999_999.99999999m))),

        new("rate.min.numeric.19.8", "أصغر قيمة تسع في numeric(19,8)",
            () => Golden.Value(Rates.Render(-99_999_999_999.99999999m))),

        new("rate.overflow.rejected", "سعر يتجاوز numeric(19,8) مرفوض",
            () => Golden.Reject(() => CanonicalValue.Rate(100_000_000_000.00000000m))),

        new("rate.nine.decimals.rejected.not.rounded",
            "تسع خانات عشرية تُرفض ولا تُقرَّب — نفس مبدأ المبلغ بأرقام العمود الفعلي",
            () => Golden.Reject(() => CanonicalValue.Rate(0.000000005m))),

        new("rate.literal.scale4.rejected", "«3.7500» ليس شكلاً قانونياً لسعر صرف: المقياس 8",
            () => Golden.Reject(() => Rates.ParseCanonical("3.7500"))),

        new("rate.literal.negative.zero.rejected", "«-0.00000000» مرفوض: للصفر شكل واحد",
            () => Golden.Reject(() => Rates.ParseCanonical("-0.00000000"))),

        new("rate.literal.arabic.indic.rejected", "سعر بأرقام عربية-هندية مرفوض",
            () => Golden.Reject(() => Rates.ParseCanonical("\u0663.\u0667\u0665\u0660\u0660\u0660\u0660\u0660\u0660"))),

        new("rate.wrong.kind.into.amount.field.rejected",
            "قيمة R في حقل معلَن D مرفوضة — النوع جزء من المخطّط لا زينة",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .SetGroup("lines", [i => i.Set("debit", CanonicalValue.Rate(1m))]))),

        new("rate.entry.with.foreign.currency",
            "قيد بعملة أجنبية: سعر صرف بثمان خانات ومبالغ بعملة الشركة مختلفة عن مبالغ الحركة",
            () => Golden.Bytes(
                Entry(currency: "USD", amount: 400.0000m, fxRate: 3.75123456m, companyAmount: 1_500.4938m), 1, Gen)),

        // ══════════════════════ 4. الأوقات ══════════════════════
        new("instant.microsecond.canonical", "لحظة بميكروثانية غير صفرية — ست خانات كسرية بالضبط",
            () => Golden.Value(Instants.Render(Posted))),

        new("instant.sub.microsecond.rejected",
            "دقّة دون الميكروثانية مرفوضة: Npgsql تقصّ عند الكتابة (مقيس)، فلن تُتحقَّق السلسلة بعد الدورة",
            () => Golden.Reject(() =>
                CanonicalValue.Instant(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567)),
                "ticks=...7 مكتوبة تعود ...0")),

        new("instant.sub.microsecond.truncated.accepted",
            "نفس اللحظة بعد القصّ تُقبل، وتساوي ما تعيده قاعدة البيانات",
            () => Golden.Value(Instants.Render(
                Instants.Truncate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567))))),

        new("instant.max.fraction.truncates.not.rounds",
            ".9999999 تُقصّ إلى .999999 ولا تُقرَّب إلى الثانية التالية — مطابق لسلوك Npgsql المقيس",
            () => Golden.Value(Instants.Render(
                Instants.Truncate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(9999999))))),

        new("instant.kind.unspecified.rejected",
            "DateTimeKind.Unspecified مرفوض — Npgsql 10 تقبله بصمت (مقيس)، وتفسيره يتبع منطقة الجهاز",
            () => Golden.Reject(() =>
                CanonicalValue.Instant(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)))),

        new("instant.kind.local.rejected", "DateTimeKind.Local مرفوض في قيمة مُجزَّأة",
            () => Golden.Reject(() =>
                CanonicalValue.Instant(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)))),

        new("instant.dst.spring.forward.boundary",
            "حدّ التوقيت الصيفي (الساعة المفقودة): 2026-03-29 01:00 UTC = 02:00 BST — يُحلّ إلى UTC واحد",
            () => Golden.Value(Instants.Render(
                Instants.Truncate(new DateTimeOffset(2026, 3, 29, 2, 0, 0, TimeSpan.FromHours(1)))))),

        new("instant.dst.fall.back.ambiguous.first",
            "الساعة المكرّرة، المرور الأول: 2026-10-25 01:30 +01:00",
            () => Golden.Value(Instants.Render(
                Instants.Truncate(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(1)))))),

        new("instant.dst.fall.back.ambiguous.second",
            "الساعة المكرّرة، المرور الثاني: 2026-10-25 01:30 +00:00 — لحظة مختلفة، بصمة مختلفة",
            () => Golden.Value(Instants.Render(
                Instants.Truncate(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero))))),

        new("instant.dst.two.passes.hash.differently",
            "المروران على «نفس» التوقيت المحلي يعطيان بصمتي قيد مختلفتين — لا لبس في UTC",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(postedAt: Instants.Truncate(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(1))))),
                () => Link(Entry(postedAt: Instants.Truncate(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero))))
            ])),

        new("date.leap.day", "يوم كبيسي: 2026 ليست كبيسة، و2028 كبيسة",
            () => Golden.Value(Instants.RenderDate(new DateOnly(2028, 2, 29)))),

        new("date.end.of.month", "آخر يوم في شهر",
            () => Golden.Value(Instants.RenderDate(new DateOnly(2026, 2, 28)))),

        new("date.entry.leap.day.entry", "قيد مؤرّخ بيوم كبيسي — البايتات كاملة",
            () => Golden.Bytes(Entry(entryDate: new DateOnly(2028, 2, 29)), 1, Gen)),

        // ══════════════════════ 5. النص العربي — التطبيع ══════════════════════
        new("text.nfc.composed.and.decomposed.same.hash",
            "«أرباح» مركّبة (U+0623) ومفكّكة (U+0627 U+0654) — بصمة واحدة بعد تنظيف الحدّ",
            () => Golden.SameHash(
            [
                () => Link(Entry(memoAr: ArbahComposed)),
                () => Link(Entry(memoAr: TextRules.CleanForInput(ArbahDecomposed)))
            ], "بايتات UTF-8 خام مختلفة: 10 مقابل 12")),

        new("text.nfd.rejected.by.hasher",
            "الشكل المفكّك يُرفض عند التجزئة — المُجزِّئ يتحقّق ولا يطبّع",
            () => Golden.Reject(() => CanonicalValue.Text(ArbahDecomposed),
                "التطبيع عند التجزئة يفكّ ارتباط البصمة بما هو مخزَّن")),

        new("text.nfc.composed.accepted.bytes",
            "الشكل المركّب مقبول — البايتات القانونية للقيد",
            () => Golden.Bytes(Entry(memoAr: ArbahComposed), 1, Gen)),

        // ══════════════════════ 6. محارف التحكّم الاتجاهي ══════════════════════
        new("text.rlm.u200f.rejected",
            "U+200F داخل بيان عربي: غير مرئي، يغيّر البصمة، وتشخيصه شبه مستحيل — مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text(MemoAr.Insert(4, Rlm)))),

        new("text.rlm.u200f.in.a.dimension.rejected",
            "‼ U+200F داخل معرّف عقار — والأبعاد صارت مُوقَّعة في v2، فالقاعدة تسري عليها كلها",
            () => Golden.Reject(() => CanonicalValue.Text("P-OWN" + Rlm + "-001"))),

        new("text.lrm.u200e.rejected", "U+200E مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text("Riyadh" + Lrm + " فرع"))),

        new("text.rle.u202b.rejected", "U+202B (تضمين اتجاهي) مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text(Rle + MemoAr))),

        new("text.rlo.u202e.rejected", "U+202E (تجاوز اتجاهي) مرفوض — وهو ناقل هجوم عرض معروف",
            () => Golden.Reject(() => CanonicalValue.Text(Rlo + "1000.00"))),

        new("text.alm.u061c.rejected", "U+061C (علامة الحرف العربي) مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Text(MemoAr + Alm))),

        new("text.bom.ufeff.rejected", "U+FEFF مرفوض حتى في وسط النص",
            () => Golden.Reject(() => CanonicalValue.Text("فرع" + Bom + "الرياض"))),

        new("text.zwj.u200d.rejected", "U+200D مرفوض في v2 أيضاً — القرار المُعلن لم يتغيّر",
            () => Golden.Reject(() => CanonicalValue.Text("لا" + Zwj + "م"))),

        new("text.nbsp.u00a0.rejected",
            "U+00A0 مرفوضة: تأتي من النسخ من Word، تصمد عبر PostgreSQL (مقيس)، ومطابقة بصرياً للمسافة",
            () => Golden.Reject(() => CanonicalValue.Text("فرع" + Nbsp + "الرياض"))),

        new("text.clean.for.input.strips.bidi",
            "تنظيف الحدّ يزيل U+200F ويعيد النص إلى شكله القانوني",
            () => Golden.SameHash(
            [
                () => Link(Entry(memoAr: MemoAr)),
                () => Link(Entry(memoAr: TextRules.CleanForInput(MemoAr.Insert(4, Rlm))))
            ])),

        new("text.bidi.changes.hash.if.allowed",
            "متجه ضبط: النصّان يختلفان فعلاً بعد التنظيف",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: MemoAr)),
                () => Link(Entry(memoAr: TextRules.CleanForInput(MemoAr + Nbsp + "فرع")))
            ])),

        // ══════════════════════ 7. الأرقام في النص ══════════════════════
        new("text.arabic.indic.digits.rejected",
            "أرقام عربية-هندية U+0660–U+0669 في حقل مُوقَّع: مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Text("قيد رقم " + ArabicIndic100))),

        new("text.eastern.arabic.indic.digits.rejected", "أرقام شرقية U+06F0–U+06F9: مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Text("قيد \u06F1\u06F0\u06F0"))),

        new("text.clean.folds.arabic.indic.digits", "تنظيف الحدّ يحوّل \u0661\u0660\u0660 إلى 100",
            () => Golden.Value(TextRules.CleanForInput("قيد رقم " + ArabicIndic100))),

        // ══════════════════════ 8. التطويل وأشكال الألف ══════════════════════
        new("text.tatweel.and.four.alef.variants.all.distinct",
            "التطويل وأشكال الألف الأربعة: خمس قيم موقَّعة مختلفة، وخمس بصمات مختلفة",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: AlefPlain + "لرياض")),
                () => Link(Entry(memoAr: AlefHamzaAbove + "لرياض")),
                () => Link(Entry(memoAr: AlefHamzaBelow + "لرياض")),
                () => Link(Entry(memoAr: AlefMadda + "لرياض")),
                () => Link(Entry(memoAr: AlefPlain + Tatweel + "لرياض"))
            ], "التطويل U+0640 محرف مشروع في القيمة الموقَّعة")),

        new("search.normalisation.folds.all.five.to.one.key",
            "‼ تطبيع البحث يطوي الخمسة إلى مفتاح واحد — ولذلك لا يُجزَّأ ولا يُكتب فوق حقل موقَّع",
            () => Golden.Value(string.Join("|",
                new[] { AlefPlain, AlefHamzaAbove, AlefHamzaBelow, AlefMadda, AlefPlain + Tatweel }
                    .Select(a => ArabicSearch.Normalize(a + "لرياض").Value)
                    .Distinct(StringComparer.Ordinal)),
                "مفتاح واحد فقط يجب أن يظهر هنا")),

        new("search.normalisation.taa.marbuta.and.alef.maqsura", "تطبيع البحث: ة -> ه و ى -> ي",
            () => Golden.Value(ArabicSearch.Normalize("مكتبة الرياض الكبرى").Value)),

        new("search.normalisation.tashkeel_removed", "تطبيع البحث يزيل التشكيل",
            () => Golden.Value(ArabicSearch.Normalize("مَكْتَبَة الرِّيَاض").Value)),

        new("search.normalisation.mixed.case.latin", "تطبيع البحث يخفض حالة اللاتيني ويطوي المسافات",
            () => Golden.Value(ArabicSearch.Normalize("  Riyadh   BRANCH  ").Value)),

        new("text.tashkeel.preserved.in.signed.value",
            "التشكيل محفوظ في القيمة الموقَّعة — القيمة الموقَّعة ليست مفتاح بحث",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: "مكتبة الرياض")),
                () => Link(Entry(memoAr: "مَكْتَبَة الرِّيَاض"))
            ])),

        // ══════════════════════ 9. أشكال العرض العربية ══════════════════════
        new("text.lam.alef.presentation.form.rejected",
            "U+FEFB (\uFEFB) يصمد أمام NFC ولا يفكّه إلا NFKC (مقيس) — مصدره النسخ من PDF. مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text(LamAlefLigature + " يوجد"))),

        new("text.clean.decomposes.presentation.form",
            "تنظيف الحدّ يفكّ U+FEFB إلى U+0644 U+0627 دون تدمير بقية النص",
            () => Golden.Value(TextRules.CleanForInput(LamAlefLigature + " يوجد رصيد"))),

        new("text.presentation.form.and.plain.same.hash.after.clean",
            "بعد التنظيف يتطابق الشكلان",
            () => Golden.SameHash(
            [
                () => Link(Entry(memoAr: "لا يوجد رصيد")),
                () => Link(Entry(memoAr: TextRules.CleanForInput(LamAlefLigature + " يوجد رصيد")))
            ])),

        // ══════════════════════ 10. الفراغ والغياب والأسطر ══════════════════════
        new("text.empty.string.vs.null.differ",
            "نصّ فارغ وغياب قيمة يعطيان بايتات مختلفة — النوع T بطول 0 مقابل النوع N",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(reversalAr: "")),
                () => Link(Entry(reversalAr: null))
            ], "الحقل هنا سبب العكس: عمود nullable فعلاً، فالغياب حقيقة لا خطأ بناء")),

        new("text.empty.string.bytes", "البايتات القانونية لقيد ببيان فارغ (النوع T بطول 0)",
            () => Golden.Bytes(Entry(memo: "", reversalAr: ""), 1, Gen)),

        new("text.null.bytes", "البايتات القانونية لقيد بغياب صريح في حقل اختياري (النوع N)",
            () => Golden.Bytes(Entry(memo: "", reversalAr: null), 1, Gen)),

        new("text.multiline.narration",
            "بيان متعدّد الأسطر ب\u0640LF — سابقة الطول تحفظ الأسطر داخل الحمولة بلا لبس",
            () => Golden.Bytes(Entry(memoAr: "قيد تسوية:\nالسطر الأول\nالسطر الثاني"), 1, Gen)),

        new("text.crlf.rejected",
            "CR مرفوض: نفس البيان على Windows وعلى Linux يجب ألا يعطي بصمتين",
            () => Golden.Reject(() => CanonicalValue.Text("قيد تسوية:\r\nالسطر الأول"))),

        new("text.crlf.cleaned.matches.lf", "بعد توحيد نهايات الأسطر تتطابق البصمة",
            () => Golden.SameHash(
            [
                () => Link(Entry(memoAr: "قيد تسوية:\nالسطر الأول")),
                () => Link(Entry(memoAr: TextRules.CleanForInput("قيد تسوية:\r\nالسطر الأول")))
            ])),

        new("text.line.separator.u2028.rejected",
            "U+2028 (فاصل سطر Unicode) مرفوض: نهاية سطر ثالثة تأتي من اللصق من HTML والمحرّرات",
            () => Golden.Reject(() => CanonicalValue.Text("سطر\u2028سطر"))),

        new("text.paragraph.separator.u2029.rejected", "U+2029 (فاصل فقرة Unicode) مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text("سطر\u2029سطر"))),

        new("text.soft.hyphen.u00ad.rejected",
            "U+00AD (الشرطة اللينة) مرفوضة: غير مرئية، وفئتها Cf",
            () => Golden.Reject(() => CanonicalValue.Text("Riy\u00ADadh"))),

        new("text.nul.rejected",
            "U+0000 مرفوض: PostgreSQL لا تخزّنه في text أصلاً (مقيس: خطأ 22021)",
            () => Golden.Reject(() => CanonicalValue.Text("قيد" + "\u0000" + "مبتور"))),

        new("text.tab.control.rejected", "TAB مرفوض داخل النص — وهو فاصل الشكل السلكي",
            () => Golden.Reject(() => CanonicalValue.Text("قيد\tمبتور"))),

        new("text.lone.surrogate.rejected",
            "بديل غير مقترن مرفوض بوضوح بدل أن ينفجر String.Normalize لاحقاً",
            () => Golden.Reject(() => CanonicalValue.Text("قيد\uD800مبتور"))),

        // ══════════════════════ 11. حقن حدود الحقول ══════════════════════
        new("injection.field.separator.in.narration",
            "بيان يحاكي سطر حقل — الفاصل TAB ممنوع في النص أصلاً، وسابقة الطول حزام ثانٍ",
            () => Golden.Bytes(Entry(memoAr: "خطر\nmemo_ar T 9 مزوَّر"), 1, Gen)),

        new("injection.two.documents.do.not.collide",
            "مستندان مختلفان أحدهما يحاول محاكاة بنية الآخر — بصمتان مختلفتان",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: "أ\nmemo T 1 ب")),
                () => Link(Entry(memoAr: "أ", memo: "ب"))
            ])),

        new("injection.end.marker.in.narration", "بيان يحتوي علامة النهاية — لا يُنهي المستند",
            () => Golden.Bytes(Entry(memoAr: "end C 0 "), 1, Gen)),

        new("injection.dimension.mimics.a.line.boundary",
            "‼ بُعد يحاكي حدّ سطر: الأبعاد صارت مُوقَّعة، وسابقة الطول تحميها كما تحمي البيان",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(property: "P-A", warehouse: "W-B")),
                () => Link(Entry(property: "P-A\nwarehouse_id", warehouse: "B"))
            ])),

        new("injection.source.doc.slash.does.not.collide",
            "‼ الثغرة القديمة: v1 كان يدمج نوع المستند ومعرّفه بشرطة مائلة، فكان (A/B,C) و(A,B/C) يعطيان البايتات نفسها. في v2 حقلان",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(sourceDocType: "A/B", sourceDocId: "C")),
                () => Link(Entry(sourceDocType: "A", sourceDocId: "B/C"))
            ])),

        // ══════════════════════ 12. مختلط عربي/إنجليزي ══════════════════════
        new("text.mixed.arabic.english.digits",
            "اسم مختلط عربي/إنجليزي/أرقام — الحالة التي تُحقن فيها محارف الاتجاه عملياً",
            () => Golden.Bytes(Entry(memoAr: "فرع الرياض - Riyadh Branch 2026 (VAT 15%)"), 1, Gen)),

        new("text.mixed.with.injected.lrm.rejected",
            "نفس الاسم وقد لصقته الواجهة مع U+200E — مرفوض عند الحدّ",
            () => Golden.Reject(() => CanonicalValue.Text("فرع الرياض - " + Lrm + "Riyadh Branch 2026"))),

        // ══════════════════════ 13. المجموعات والترتيب ══════════════════════
        new("lines.two.line.entry", "قيد بسطرين", () => Golden.Bytes(Entry(), 1, Gen)),

        new("lines.fifty.line.entry", "قيد بخمسين سطراً", () => Golden.Bytes(Entry(lineCount: 50), 1, Gen)),

        new("lines.order.is.significant",
            "ترتيب السطور جزء من الشكل القانوني — لا يُستنتج من قاموس ولا من انعكاس",
            () => Golden.DifferentHash(
            [
                () => Link(BuildTwoLines(1, "1010", 2, "4010")),
                () => Link(BuildTwoLines(2, "4010", 1, "1010"))
            ])),

        new("field.order.independent.of.set.order",
            "ترتيب استدعاءات Set لا يؤثر: المُخرَج بترتيب المخطّط دائماً",
            () => Golden.SameHash([() => Link(Entry()), () => Link(EntryReversedSetOrder())])),

        new("status.reversal.entry", "قيد عكس — الحالة والرابط والسبب كلها مُوقَّعة",
            () => Golden.Bytes(
                Entry(status: "REVERSAL", reverses: ReversedId,
                      reversalAr: "عكس لخطأ في الفترة", reversalEn: "reversed: wrong period"), 1, Gen)),

        new("token.lowercase.rejected", "الرموز [A-Z0-9_] فقط: «Posted» مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Token("Posted"))),

        // ══════════════════════ 14. الحقول التي دخلت في v2 ══════════════════════
        //  كل متجه هنا يثبت أن **تغيير هذا الحقل وحده** يغيّر البصمة، وهو بالضبط
        //  ما كان يمرّ أخضر تحت v1.
        Covered("period_code", "الفترة المحاسبية: نقل قيد بين شهرين يغيّر إقرارين وربحية شهرين",
            static () => Entry(periodCode: "2026-06")),
        Covered("source_module", "الوحدة المصدر — من أنتج القيد",
            static () => Entry(sourceModule: "Sales")),
        Covered("source_doc_type", "نوع المستند المصدر",
            static () => Entry(sourceDocType: "SalesInvoice")),
        Covered("source_doc_id", "معرّف المستند المصدر — فصل القيد عن مستنده",
            static () => Entry(sourceDocId: "INV-999")),
        Covered("posting_trigger_code", "رمز إطلاق الترحيل — جزء من مفتاح الإحكام",
            static () => Entry(triggerCode: "on_payment")),
        Covered("posting_generation", "جيل الترحيل — رفعه بلا عكس مشروع التفاف على مفتاح الإحكام",
            static () => Entry(generation: 2)),
        Covered("event_code", "رمز الحدث المحاسبي الذي وُلِّد منه القيد",
            static () => Entry(eventCode: "realestate.rent_invoice.managed_property")),
        Covered("reverses_entry_id", "رابط العكس: إعادة توجيهه يجعل الأصل يبدو قائماً وقد أُلغي",
            static () => Entry(reverses: ReversedId)),
        Covered("reversal_reason_ar", "سبب العكس بالعربية",
            static () => Entry(reversalAr: "سبب آخر")),
        Covered("reversal_reason_en", "سبب العكس بالإنجليزية",
            static () => Entry(reversalEn: "another reason")),
        Covered("closed_period_permission", "‼ الإذن الاستثنائي بالترحيل في فترة مقفلة — إذنٌ يُعاد كتابته ليس إذناً",
            static () => Entry(permission: "LEDGER.CLOSED_PERIOD_OVERRIDE")),
        Covered("closed_period_authoriser", "‼ من أذن (approved_by) — وهو أول ما يسأل عنه المدقّق",
            static () => Entry(authoriser: "cfo@acme.sa")),
        Covered("actor", "من أنشأ القيد (created_by)",
            static () => Entry(actor: "other@acme.sa")),
        Covered("entry_currency", "عملة القيد",
            static () => Entry(currency: "USD")),
        Covered("role_code", "‼ رمز الدور: لماذا وُلِّد السطر — وعليه تُفرض GR-RE-001 في قاعدة البيانات",
            static () => Entry(roleCode: "bank_current_account")),
        Covered("qualifier", "المؤهّل: مع الدور يُكوِّن مفتاح role_account_map",
            static () => Entry(qualifier: "SAR-MAIN")),
        Covered("fx_rate", "سعر الصرف: تغييره وحده يجعل مبالغ عملة الشركة غير مبرَّرة",
            static () => Entry(fxRate: 3.75000000m)),
        Covered("debit_company", "‼ المبلغ بعملة الشركة — وهو ما يُبنى منه ميزان المراجعة فعلاً",
            static () => Entry(companyAmount: 1_501.0000m)),
        Covered("branch_id", "الفرع",
            static () => Entry(branch: "BR-JED")),
        Covered("cost_center_id", "مركز التكلفة",
            static () => Entry(costCenter: "CC-01")),
        Covered("project_id", "‼ المشروع — نقل تكلفة بين مشروعين يقلب ربحية اثنين",
            static () => Entry(project: "PRJ-77")),
        Covered("property_id", "‼‼ العقار — الثغرة المُبلَّغ عنها حرفياً: نقل حركة بين عقارين",
            static () => Entry(property: "P-MANAGED-001")),
        Covered("unit_id", "الوحدة العقارية",
            static () => Entry(unit: "U-02")),
        Covered("warehouse_id", "‼ المستودع — نقل حركة مخزون بين مستودعين",
            static () => Entry(warehouse: "WH-02")),
        Covered("boq_item_id", "بند جدول الكميات",
            static () => Entry(boq: "BOQ-14")),
        Covered("tax_code", "رمز المعالجة الضريبية — الفتحة محجوزة ومُجزَّأة من اليوم الأول",
            static () => Entry(taxCode: "VAT-STD-15")),
        Covered("subledger_kind", "نوع الدفتر المساعد",
            static () => Entry(subledgerKind: "customer", subledgerParty: "CUST-1")),
        Covered("subledger_party_id", "‼ الطرف: نقل دَين من ذمّة إلى ذمّة",
            static () => Entry(subledgerKind: "customer", subledgerParty: "CUST-2")),
        Covered("line_description", "بيان السطر بالإنجليزية",
            static () => Entry(lineDescription: "Cash")),
        Covered("line_description_ar", "بيان السطر بالعربية",
            static () => Entry(lineDescriptionAr: "الصندوق")),

        new("covered.every.new.field.is.distinct",
            "‼ ثلاثون تغييراً، كل واحد في حقل واحد فقط — وثلاثون بصمة مختلفة، ولا تصادم واحد",
            () => Golden.DifferentHash(
            [
                () => Link(Entry()),
                () => Link(Entry(periodCode: "2026-06")),
                () => Link(Entry(sourceModule: "Sales")),
                () => Link(Entry(sourceDocType: "SalesInvoice")),
                () => Link(Entry(sourceDocId: "INV-999")),
                () => Link(Entry(triggerCode: "on_payment")),
                () => Link(Entry(generation: 2)),
                () => Link(Entry(eventCode: "realestate.rent_invoice.managed_property")),
                () => Link(Entry(reverses: ReversedId)),
                () => Link(Entry(reversalAr: "سبب آخر")),
                () => Link(Entry(reversalEn: "another reason")),
                () => Link(Entry(permission: "LEDGER.CLOSED_PERIOD_OVERRIDE")),
                () => Link(Entry(authoriser: "cfo@acme.sa")),
                () => Link(Entry(actor: "other@acme.sa")),
                () => Link(Entry(currency: "USD")),
                () => Link(Entry(roleCode: "bank_current_account")),
                () => Link(Entry(qualifier: "SAR-MAIN")),
                () => Link(Entry(fxRate: 3.75000000m)),
                () => Link(Entry(companyAmount: 1_501.0000m)),
                () => Link(Entry(branch: "BR-JED")),
                () => Link(Entry(costCenter: "CC-01")),
                () => Link(Entry(project: "PRJ-77")),
                () => Link(Entry(property: "P-MANAGED-001")),
                () => Link(Entry(unit: "U-02")),
                () => Link(Entry(warehouse: "WH-02")),
                () => Link(Entry(boq: "BOQ-14")),
                () => Link(Entry(taxCode: "VAT-STD-15")),
                () => Link(Entry(subledgerKind: "customer", subledgerParty: "CUST-1")),
                () => Link(Entry(lineDescription: "Cash")),
                () => Link(Entry(lineDescriptionAr: "الصندوق"))
            ])),

        // ══════════════════════ 15. الغياب مقابل القيمة الفارغة ══════════════════════
        new("dimension.null.vs.empty.string.differ",
            "‼ بُعد غائب (N بطول 0) وبُعد بقيمة نصّية فارغة (T بطول 0) — بايتات مختلفة، ولا تصادم",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(property: null)),
                () => Link(Entry(property: ""))
            ], "غياب البُعد حقيقة محاسبية، والنصّ الفارغ قيمة مُدخلة — وليسا الشيء نفسه")),

        new("dimension.null.bytes", "البايتات القانونية لقيد بلا عقار — النوع N صريحاً",
            () => Golden.Bytes(Entry(property: null), 1, Gen)),

        new("dimension.empty.string.bytes", "البايتات القانونية لقيد بعقار نصّه فارغ — النوع T بطول 0",
            () => Golden.Bytes(Entry(property: ""), 1, Gen)),

        new("optional.entry.field.not.set.rejected",
            "‼ حقل اختياري لم يُضبط في مستند v2 مرفوض: «نُسِي الحقل» و«الحقل غائب» لا يجوز أن يعطيا البايتات نفسها",
            () => Golden.Reject(() => EntryMissingOptional(),
                "في v1 كان هذا يمرّ صامتاً ويصير Null ضمناً")),

        new("optional.line.field.not.set.rejected",
            "‼ بُعد لم يُضبط داخل سطر مرفوض بنفس المنطق — وهو المسار الذي يُنسى فيه بُعدٌ جديد عملياً",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .SetGroup("lines", [i => i.Set("line_no", CanonicalValue.Integer(1))
                                          .Set("account_code", CanonicalValue.Text("1010"))
                                          .Set("role_code", CanonicalValue.Text("r"))
                                          .Set("qualifier", CanonicalValue.Text("*"))
                                          .Set("debit", CanonicalValue.Amount(1m))
                                          .Set("credit", CanonicalValue.Amount(0m))
                                          .Set("currency", CanonicalValue.Token("SAR"))
                                          .Set("fx_rate", CanonicalValue.Rate(1m))
                                          .Set("debit_company", CanonicalValue.Amount(1m))
                                          .Set("credit_company", CanonicalValue.Amount(0m))
                                          .Set("subledger_kind", CanonicalValue.Text("none"))]))),

        new("dimension.swap.between.two.dimensions.does.not.collide",
            "قيمة واحدة منقولة من بُعد إلى بُعد: مستندان مختلفان، بصمتان مختلفتان",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(project: "X", warehouse: null)),
                () => Link(Entry(project: null, warehouse: "X"))
            ])),

        // ══════════════════════ 16. الدفتر المساعد — كل الأنواع ══════════════════════
        new("subledger.every.kind.is.distinct",
            "أنواع الدفتر المساعد الستة تعطي ست بصمات مختلفة",
            () => Golden.DifferentHash(
                [.. SubledgerKinds.Select(static kind => new Func<ChainLink>(
                    () => Link(Entry(subledgerKind: kind, subledgerParty: kind == "none" ? null : "PARTY-1"))))])),

        new("subledger.kind.none.bytes", "دفتر مساعد: none — بلا طرف",
            () => Golden.Bytes(Entry(subledgerKind: "none", subledgerParty: null), 1, Gen)),
        new("subledger.kind.customer.bytes", "دفتر مساعد: عميل",
            () => Golden.Bytes(Entry(subledgerKind: "customer", subledgerParty: "CUST-1"), 1, Gen)),
        new("subledger.kind.supplier.bytes", "دفتر مساعد: مورّد",
            () => Golden.Bytes(Entry(subledgerKind: "supplier", subledgerParty: "SUP-1"), 1, Gen)),
        new("subledger.kind.employee.bytes", "دفتر مساعد: موظف",
            () => Golden.Bytes(Entry(subledgerKind: "employee", subledgerParty: "EMP-1"), 1, Gen)),
        new("subledger.kind.asset.bytes", "دفتر مساعد: أصل ثابت",
            () => Golden.Bytes(Entry(subledgerKind: "asset", subledgerParty: "AST-1"), 1, Gen)),
        new("subledger.kind.treasury.bytes", "دفتر مساعد: خزينة أو حساب بنكي",
            () => Golden.Bytes(Entry(subledgerKind: "treasury", subledgerParty: "BANK-1"), 1, Gen)),

        new("subledger.party.without.kind.still.hashes",
            "طرف بلا نوع: حالة بيانات غير متّسقة، ومع ذلك مُجزَّأة كما هي — البصمة تربط المخزَّن لا الصحيح",
            () => Golden.Bytes(Entry(subledgerKind: "none", subledgerParty: "PARTY-ORPHAN"), 1, Gen)),

        // ══════════════════════ 17. المخطّط ومجموعة الاستثناء ══════════════════════
        new("schema.excluded.field.rejected",
            "محاولة إدراج حقل مستثنى (بصمة السجل نفسه) مرفوضة",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("entry_hash", CanonicalValue.Text("x")))),

        new("schema.search.column.excluded.rejected",
            "‼ محاولة إدراج العمود المطبَّع للبحث مرفوضة",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("memo_ar_search", CanonicalValue.Text("x")))),

        new("schema.line.search.column.excluded.rejected",
            "‼ عمود بحث السطر مستثنى أيضاً — مصدره description_ar المُوقَّع",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("description_ar_search", CanonicalValue.Text("x")))),

        new("schema.legacy.source_ref.name.excluded.rejected",
            "الاسم المدموج القديم مستثنى صراحةً كي لا يعود من باب خلفي",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("source_ref", CanonicalValue.Text("A/B")))),

        new("schema.unknown.field.rejected", "حقل غير معرّف مرفوض — لا حقول ضمنية",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("whatever", CanonicalValue.Text("x")))),

        new("schema.missing.required.field.rejected",
            "حقل مطلوب ناقص مرفوض — لا قيم افتراضية ضمنية",
            () => Golden.Reject(() => JournalEntrySchema.V2.NewDocument()
                .Set("tenant_id", CanonicalValue.Text(Tenant)).Build())),

        new("schema.exclusion.set.listing",
            "مجموعة استثناء v2 كاملة، بأسمائها وأسبابها — مُشتقّة من أعمدة الجدولين لا موروثة",
            () => Golden.Value(string.Join("\n", JournalEntrySchema.V2.Exclusions
                .Select(e => $"{e.Name}|{e.Reason}")))),

        new("schema.field.listing",
            "قائمة حقول v2 بترتيبها وأنواعها — بما فيها حقول المجموعة",
            () => Golden.Value(string.Join("\n", JournalEntrySchema.V2.Fields.SelectMany(f =>
                f.GroupFields is null
                    ? new[] { $"{f.Name}|{f.Kind}|{f.Required}" }
                    : new[] { $"{f.Name}|{f.Kind}|{f.Required}" }
                        .Concat(f.GroupFields.Select(g => $"  {f.Name}/{g.Name}|{g.Kind}|{g.Required}")))))),

        new("schema.requires.explicit.optionals",
            "علم الضبط الصريح مرفوع في v2 ومخفوض في v1 — وهو داخل بصمة المخطّط",
            () => Golden.Value(
                $"v1={JournalEntrySchema.V1.RequireExplicitOptionals}|v2={JournalEntrySchema.V2.RequireExplicitOptionals}")),

        // ══════════════════════ 18. الإصدارات والتعايش ══════════════════════
        new("version.registry.lists.v1.and.v2",
            "سجلّ الإصدارات يحوي الاثنين — سجلات v1 تبقى قابلة للتحقق إلى الأبد بجوار سجلات v2",
            () => Golden.Value(string.Join(",", CanonRegistry.Versions.OrderBy(v => v, StringComparer.Ordinal)))),

        new("version.v1.resolves.to.the.v1.canonicaliser",
            "‼ التوزيع بالإصدار المخزَّن لا بثابت في الشيفرة",
            () => Golden.Value(CanonRegistry.Resolve("v1").Version + "|" + CanonRegistry.Resolve("v2").Version)),

        new("version.unknown.rejected", "إصدار غير معروف مرفوض بدل أن يُفترض",
            () => Golden.Reject(() => CanonRegistry.Resolve("v99"))),

        new("version.v1.bytes.did.not.move",
            "‼ البايتات القانونية v1 لقيد v1 مرجعي — تُحسب هنا بعد تسجيل v2، وقيمتها تساوي ما في ملف v1",
            () => Golden.Bytes(V1ReferenceEntry(), 1, Gen,
                "المتجه نفسه موجود في golden-vectors.v1.json تحت baseline.entry.seq1")),

        new("version.same.content.differs.between.v1.and.v2",
            "نفس القيد تحت الإصدارين يعطي بصمتين مختلفتين — الترويسة والتغطية مختلفتان، وهذا هو المقصود",
            () => Golden.DifferentHash(
            [
                () => Canonicalizer.Compute(V1ReferenceEntry(), 1, Gen),
                () => Link(Entry())
            ])),

        // ══════════════════════ 19. بيئة التشغيل ══════════════════════
        new("runtime.self.test.passes",
            "الفحص السلوكي لبيئة التشغيل: NFC يعمل فعلاً، لا كما يدّعي AppContext",
            () => Golden.Value(CanonicalRuntime.SelfTest().Ok ? "OK" : "BROKEN",
                "وضع العولمة الثابتة يجعل String.Normalize لا-شيء بصمت (مقيس)"))
    ];

    /// <summary>مستند v2 ينقصه حقل اختياري واحد — يجب أن يُرفض.</summary>
    private static CanonicalDocument EntryMissingOptional()
    {
        var b = JournalEntrySchema.V2.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(Tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("entry_no", CanonicalValue.Integer(42))
            .Set("entry_date", CanonicalValue.Date(EntryDate))
            .Set("period_code", CanonicalValue.Text("2026-05"))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("status", CanonicalValue.Token("POSTED"))
            // reverses_entry_id غير مضبوط عمداً — هذا هو موضع الاختبار
            .Set("reversal_reason_ar", CanonicalValue.Null())
            .Set("reversal_reason_en", CanonicalValue.Null())
            .Set("source_module", CanonicalValue.Text("RealEstate"))
            .Set("source_doc_type", CanonicalValue.Text("RentInvoice"))
            .Set("source_doc_id", CanonicalValue.Text("INV-1"))
            .Set("posting_trigger_code", CanonicalValue.Text("on_approval"))
            .Set("posting_generation", CanonicalValue.Integer(1))
            .Set("event_code", CanonicalValue.Text("e"))
            .Set("idempotency_key", CanonicalValue.Text("k"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .Set("actor", CanonicalValue.Text("a"))
            .Set("closed_period_permission", CanonicalValue.Null())
            .Set("closed_period_authoriser", CanonicalValue.Null())
            .Set("memo", CanonicalValue.Null())
            .Set("memo_ar", CanonicalValue.Null());

        b.SetGroup("lines", []);
        return b.Build();
    }

    /// <summary>القيد المرجعي كما تبنيه مجموعة v1 — لإثبات أن بايتات v1 لم تتحرّك.</summary>
    private static CanonicalDocument V1ReferenceEntry()
        => JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(Tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("entry_no", CanonicalValue.Integer(42))
            .Set("entry_date", CanonicalValue.Date(EntryDate))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("memo", CanonicalValue.Text("revenue recognition"))
            .Set("memo_ar", CanonicalValue.Text(MemoAr))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .SetGroup("lines",
            [
                i => i.Set("line_no", CanonicalValue.Integer(1))
                      .Set("account_code", CanonicalValue.Text("1010"))
                      .Set("debit", CanonicalValue.Amount(1500.0000m))
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("النقدية")),
                i => i.Set("line_no", CanonicalValue.Integer(2))
                      .Set("account_code", CanonicalValue.Text("4010"))
                      .Set("debit", CanonicalValue.Amount(0m))
                      .Set("credit", CanonicalValue.Amount(1500.0000m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("المبيعات"))
            ])
            .Build();

    private static CanonicalDocument BuildTwoLines(int a, string accA, int b, string accB)
    {
        var doc = JournalEntrySchema.V2.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(Tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("entry_no", CanonicalValue.Integer(42))
            .Set("entry_date", CanonicalValue.Date(EntryDate))
            .Set("period_code", CanonicalValue.Text("2026-05"))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("reverses_entry_id", CanonicalValue.Null())
            .Set("reversal_reason_ar", CanonicalValue.Null())
            .Set("reversal_reason_en", CanonicalValue.Null())
            .Set("source_module", CanonicalValue.Text("RealEstate"))
            .Set("source_doc_type", CanonicalValue.Text("RentInvoice"))
            .Set("source_doc_id", CanonicalValue.Text("INV-1"))
            .Set("posting_trigger_code", CanonicalValue.Text("on_approval"))
            .Set("posting_generation", CanonicalValue.Integer(1))
            .Set("event_code", CanonicalValue.Text("realestate.rent_invoice.own_property"))
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("closed_period_permission", CanonicalValue.Null())
            .Set("closed_period_authoriser", CanonicalValue.Null())
            .Set("memo", CanonicalValue.Text("order"))
            .Set("memo_ar", CanonicalValue.Text(MemoAr));

        doc.SetGroup("lines",
        [
            i => Line(i, a, accA, "cash_on_hand", "*", a == 1 ? 1500m : 0m, a == 1 ? 0m : 1500m,
                "SAR", 1m, a == 1 ? 1500m : 0m, a == 1 ? 0m : 1500m,
                null, null, null, "P-OWN-001", "U-01", null, null, null, "none", null, "سطر", "سطر"),
            i => Line(i, b, accB, "rental_revenue", "*", b == 1 ? 1500m : 0m, b == 1 ? 0m : 1500m,
                "SAR", 1m, b == 1 ? 1500m : 0m, b == 1 ? 0m : 1500m,
                null, null, null, "P-OWN-001", "U-01", null, null, null, "none", null, "سطر", "سطر")
        ]);

        return doc.Build();
    }

    /// <summary>نفس القيد المرجعي، بترتيب استدعاءات Set معكوس تماماً.</summary>
    private static CanonicalDocument EntryReversedSetOrder()
    {
        var b = JournalEntrySchema.V2.NewDocument();

        b.SetGroup("lines",
        [
            i => i.Set("description_ar", CanonicalValue.Text("النقدية"))
                  .Set("description", CanonicalValue.Text("النقدية"))
                  .Set("subledger_party_id", CanonicalValue.Null())
                  .Set("subledger_kind", CanonicalValue.Text("none"))
                  .Set("tax_code", CanonicalValue.Null())
                  .Set("boq_item_id", CanonicalValue.Null())
                  .Set("warehouse_id", CanonicalValue.Null())
                  .Set("unit_id", CanonicalValue.Text("U-01"))
                  .Set("property_id", CanonicalValue.Text("P-OWN-001"))
                  .Set("project_id", CanonicalValue.Null())
                  .Set("cost_center_id", CanonicalValue.Null())
                  .Set("branch_id", CanonicalValue.Null())
                  .Set("credit_company", CanonicalValue.Amount(0m))
                  .Set("debit_company", CanonicalValue.Amount(1500.0000m))
                  .Set("fx_rate", CanonicalValue.Rate(1m))
                  .Set("currency", CanonicalValue.Token("SAR"))
                  .Set("credit", CanonicalValue.Amount(0m))
                  .Set("debit", CanonicalValue.Amount(1500.0000m))
                  .Set("qualifier", CanonicalValue.Text("*"))
                  .Set("role_code", CanonicalValue.Text("cash_on_hand"))
                  .Set("account_code", CanonicalValue.Text("1010"))
                  .Set("line_no", CanonicalValue.Integer(1)),
            i => i.Set("description_ar", CanonicalValue.Text("المبيعات"))
                  .Set("description", CanonicalValue.Text("المبيعات"))
                  .Set("subledger_party_id", CanonicalValue.Null())
                  .Set("subledger_kind", CanonicalValue.Text("none"))
                  .Set("tax_code", CanonicalValue.Null())
                  .Set("boq_item_id", CanonicalValue.Null())
                  .Set("warehouse_id", CanonicalValue.Null())
                  .Set("unit_id", CanonicalValue.Text("U-01"))
                  .Set("property_id", CanonicalValue.Text("P-OWN-001"))
                  .Set("project_id", CanonicalValue.Null())
                  .Set("cost_center_id", CanonicalValue.Null())
                  .Set("branch_id", CanonicalValue.Null())
                  .Set("credit_company", CanonicalValue.Amount(1500.0000m))
                  .Set("debit_company", CanonicalValue.Amount(0m))
                  .Set("fx_rate", CanonicalValue.Rate(1m))
                  .Set("currency", CanonicalValue.Token("SAR"))
                  .Set("credit", CanonicalValue.Amount(1500.0000m))
                  .Set("debit", CanonicalValue.Amount(0m))
                  .Set("qualifier", CanonicalValue.Text("*"))
                  .Set("role_code", CanonicalValue.Text("rental_revenue"))
                  .Set("account_code", CanonicalValue.Text("4010"))
                  .Set("line_no", CanonicalValue.Integer(2))
        ]);

        b.Set("memo_ar", CanonicalValue.Text(MemoAr))
         .Set("memo", CanonicalValue.Text("revenue recognition"))
         .Set("closed_period_authoriser", CanonicalValue.Null())
         .Set("closed_period_permission", CanonicalValue.Null())
         .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
         .Set("currency", CanonicalValue.Token("SAR"))
         .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
         .Set("event_code", CanonicalValue.Text("realestate.rent_invoice.own_property"))
         .Set("posting_generation", CanonicalValue.Integer(1))
         .Set("posting_trigger_code", CanonicalValue.Text("on_approval"))
         .Set("source_doc_id", CanonicalValue.Text("INV-1"))
         .Set("source_doc_type", CanonicalValue.Text("RentInvoice"))
         .Set("source_module", CanonicalValue.Text("RealEstate"))
         .Set("reversal_reason_en", CanonicalValue.Null())
         .Set("reversal_reason_ar", CanonicalValue.Null())
         .Set("reverses_entry_id", CanonicalValue.Null())
         .Set("status", CanonicalValue.Token("POSTED"))
         .Set("posted_at", CanonicalValue.Instant(Posted))
         .Set("period_code", CanonicalValue.Text("2026-05"))
         .Set("entry_date", CanonicalValue.Date(EntryDate))
         .Set("entry_no", CanonicalValue.Integer(42))
         .Set("entry_id", CanonicalValue.Uuid(EntryId))
         .Set("fiscal_year", CanonicalValue.Integer(Year))
         .Set("book_id", CanonicalValue.Text(Book))
         .Set("tenant_id", CanonicalValue.Text(Tenant));

        return b.Build();
    }
}
