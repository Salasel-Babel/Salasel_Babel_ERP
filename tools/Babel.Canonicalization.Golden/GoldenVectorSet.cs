using System.Globalization;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Golden;

/// <summary>
/// مجموعة المتجهات الذهبية للإصدار v1.
///
/// كل متجه هنا يقابل مصيدة حقيقية عضّت هذا المشروع أو نظاماً إنتاجياً مماثلاً.
/// <b>المُدخلات مكتوبة بلغة C# لا بـJSON</b> عمداً: قيم <c>decimal</c> بمقاييس
/// مختلفة، ولحظات بدقّة الـtick، ومحارف تحكّم غير مرئية — كلها تفقد دقّتها إن
/// مرّت بأي تسلسل وسيط. الملف الذهبي يخزّن <b>المخرجات</b>: البايتات القانونية
/// كاملة بترميز hex، وبصمة SHA-256، ورمز الرفض المتوقّع.
/// </summary>
public static class GoldenVectorSet
{
    // ===== ثوابت مشتركة، مجمّدة =====
    private const string Tenant = "acme";
    private const string Book = "MAIN";
    private const int Year = 2026;

    private static readonly Guid EntryId = Guid.Parse("0192f3c8-0000-7000-8000-000000000001");
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

    private const string AlefPlain = "\u0627"; // ا
    private const string AlefHamzaAbove = "\u0623"; // أ
    private const string AlefHamzaBelow = "\u0625"; // إ
    private const string AlefMadda = "\u0622"; // آ

    private const string LamAlefLigature = "\uFEFB"; // ﻻ  شكل عرض
    private const string ArabicIndic100 = "\u0661\u0660\u0660"; // ١٠٠

    // «أرباح» بشكليها المركّب والمفكّك. مكتوبان بالهروب لأنهما متطابقان بصرياً تماماً.
    private const string ArbahComposed   = "\u0623\u0631\u0628\u0627\u062D";        // U+0623 ...
    private const string ArbahDecomposed = "\u0627\u0654\u0631\u0628\u0627\u062D"; // U+0627 U+0654 ...

    /// <summary>يبني قيداً مرجعياً مع إمكانية تعديل الحقول محلّ الاختبار.</summary>
    private static CanonicalDocument Entry(
        string? memoAr = MemoAr,
        string? memo = "revenue recognition",
        decimal amount = 1500.0000m,
        DateTime? postedAt = null,
        DateOnly? entryDate = null,
        string tenant = Tenant,
        string lineDescription = "النقدية",
        long entryNo = 42,
        string status = "POSTED",
        int lineCount = 2)
    {
        var b = JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("entry_no", CanonicalValue.Integer(entryNo))
            .Set("entry_date", CanonicalValue.Date(entryDate ?? EntryDate))
            .Set("posted_at", CanonicalValue.Instant(postedAt ?? Posted))
            .Set("status", CanonicalValue.Token(status))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("memo", CanonicalValue.TextOrNull(memo))
            .Set("memo_ar", CanonicalValue.TextOrNull(memoAr))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("currency", CanonicalValue.Token("SAR"));

        if (lineCount == 2)
        {
            b.SetGroup("lines",
            [
                i => i.Set("line_no", CanonicalValue.Integer(1))
                      .Set("account_code", CanonicalValue.Text("1010"))
                      .Set("debit", CanonicalValue.Amount(amount))
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text(lineDescription)),
                i => i.Set("line_no", CanonicalValue.Integer(2))
                      .Set("account_code", CanonicalValue.Text("4010"))
                      .Set("debit", CanonicalValue.Amount(0m))
                      .Set("credit", CanonicalValue.Amount(amount))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("المبيعات"))
            ]);
        }
        else
        {
            var items = new List<Action<CanonicalItemBuilder>>();
            for (var k = 1; k <= lineCount; k++)
            {
                var n = k;
                items.Add(i => i.Set("line_no", CanonicalValue.Integer(n))
                                .Set("account_code", CanonicalValue.Text((1000 + n).ToString(CultureInfo.InvariantCulture)))
                                .Set("debit", CanonicalValue.Amount(n % 2 == 1 ? 10.0000m : 0m))
                                .Set("credit", CanonicalValue.Amount(n % 2 == 1 ? 0m : 10.0000m))
                                .Set("cost_center", CanonicalValue.Null())
                                .Set("description", CanonicalValue.Text($"سطر {n.ToString(CultureInfo.InvariantCulture)}")));
            }
            b.SetGroup("lines", items);
        }

        return b.Build();
    }

    private static ChainLink Link(CanonicalDocument d, long seq = 1) => Canonicalizer.Compute(d, seq, Gen);

    /// <summary>المجموعة كاملة، بترتيب ثابت.</summary>
    public static IReadOnlyList<GoldenVector> All =>
    [
        // ══════════════════════ 1. الأساس والتكوين ══════════════════════
        new("genesis.scope.main",
            "بصمة التكوين لنطاق (مستأجر × دفتر × سنة مالية) — prev_hash للسجل رقم 1",
            () => Golden.Value(Canonicalizer.Hex(Gen), "scope=" + JournalEntrySchema.ChainScope(Tenant, Book, Year))),

        new("genesis.scope.differs.by.tenant",
            "نطاقان مختلفان يعطيان بصمتي تكوين مختلفتين — السلسلة لكل دفتر لا للمنتج كله",
            () => Golden.Value(
                Canonicalizer.Hex(JournalEntrySchema.Genesis("other", Book, Year)),
                "لا يساوي بصمة تكوين acme")),

        new("baseline.entry.seq1",
            "القيد المرجعي، أول السلسلة — البايتات القانونية كاملة، وفيها chain_seq و prev_hash",
            () => Golden.Bytes(Entry(), 1, Gen)),

        new("baseline.entry.seq2.same.content",
            "نفس المحتوى برقم تسلسل مختلف يعطي بصمة مختلفة — التسلسل داخل البايتات لا بجوارها",
            () => Golden.Bytes(Entry(), 2, Gen)),

        new("baseline.entry.different.prev.hash",
            "نفس المحتوى وبصمة سابقة مختلفة يعطي بصمة مختلفة — الرابط مربوط تشفيرياً",
            () => Golden.Bytes(Entry(), 1, JournalEntrySchema.Genesis("other", Book, Year))),

        new("schema.fingerprint.v1",
            "بصمة إعلان المخطّط: الأسماء والأنواع والترتيب ومجموعة الاستثناء. أي تعديل يُسقط البناء",
            () => Golden.Value(JournalEntrySchema.V1.Fingerprint,
                $"fields={JournalEntrySchema.V1.Fields.Count} exclusions={JournalEntrySchema.V1.Exclusions.Count}")),

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

        new("amount.rendered.exactly.100.0000",
            "الشكل اللفظي الوحيد للمئة",
            () => Golden.Value(Amounts.Render(100m))),

        new("amount.negative",
            "مبلغ سالب: -2500.7500",
            () => Golden.Value(Amounts.Render(-2500.75m))),

        new("amount.zero",
            "الصفر: 0.0000 بلا إشارة",
            () => Golden.Value(Amounts.Render(0m))),

        new("amount.negative.zero.normalised",
            "الصفر السالب: PostgreSQL تُسقط إشارته عند الدورة (مقيس) — نُثبّت 0.0000 هنا أيضاً",
            () => Golden.Value(Amounts.Render(decimal.Negate(0.0000m)), "GetBits قبل الدورة يحمل بت الإشارة")),

        new("amount.max.numeric.19.4",
            "أكبر قيمة تسع في numeric(19,4)",
            () => Golden.Value(Amounts.Render(999_999_999_999_999.9999m))),

        new("amount.min.numeric.19.4",
            "أصغر قيمة تسع في numeric(19,4)",
            () => Golden.Value(Amounts.Render(-999_999_999_999_999.9999m))),

        new("amount.overflow.numeric.19.4.rejected",
            "قيمة تتجاوز numeric(19,4) مرفوضة — قيمة لا تُخزَّن لا يجوز أن تُجزَّأ",
            () => Golden.Reject(() => CanonicalValue.Amount(1_000_000_000_000_000.0000m))),

        new("amount.five.decimals.rejected.not.rounded",
            "خمس خانات عشرية تُرفض ولا تُقرَّب: .NET نصف-إلى-الزوجي وPostgreSQL نصف-بعيداً-عن-الصفر (مقيس)",
            () => Golden.Reject(() => CanonicalValue.Amount(0.00005m),
                "decimal.Round(0.00005m,4)=0.0000 بينما PG تخزّن 0.0001")),

        new("amount.literal.comma.rejected",
            "«100,00» انزلاق لغوي مرفوض",
            () => Golden.Reject(() => Amounts.ParseCanonical("100,00"))),

        new("amount.literal.scale2.rejected",
            "«100.00» شكل غير قانوني: المقياس 4 دائماً",
            () => Golden.Reject(() => Amounts.ParseCanonical("100.00"))),

        new("amount.literal.plus.sign.rejected",
            "«+100.0000» علامة الموجب مرفوضة",
            () => Golden.Reject(() => Amounts.ParseCanonical("+100.0000"))),

        new("amount.literal.exponent.rejected",
            "«1.0E2» صيغة أسّية مرفوضة",
            () => Golden.Reject(() => Amounts.ParseCanonical("1.0E2"))),

        new("amount.literal.arabic.indic.digits.rejected",
            "«١٠٠.٠٠٠٠» بأرقام عربية-هندية: تبدو صحيحة وتُجزَّأ خطأ",
            () => Golden.Reject(() => Amounts.ParseCanonical(ArabicIndic100 + ".٠٠٠٠"))),

        new("amount.literal.negative.zero.rejected",
            "«-0.0000» شكل غير قانوني: للصفر شكل واحد",
            () => Golden.Reject(() => Amounts.ParseCanonical("-0.0000"))),

        new("amount.literal.leading.zero.rejected",
            "«0100.0000» صفر بادئ مرفوض",
            () => Golden.Reject(() => Amounts.ParseCanonical("0100.0000"))),

        // ══════════════════════ 3. الأوقات ══════════════════════
        new("instant.microsecond.canonical",
            "لحظة بميكروثانية غير صفرية — ست خانات كسرية بالضبط",
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

        new("instant.kind.local.rejected",
            "DateTimeKind.Local مرفوض في قيمة مُجزَّأة",
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

        new("date.leap.day",
            "يوم كبيسي: 2026 ليست كبيسة، و2028 كبيسة",
            () => Golden.Value(Instants.RenderDate(new DateOnly(2028, 2, 29)))),

        new("date.end.of.month",
            "آخر يوم في شهر",
            () => Golden.Value(Instants.RenderDate(new DateOnly(2026, 2, 28)))),

        new("date.entry.leap.day.entry",
            "قيد مؤرّخ بيوم كبيسي — البايتات كاملة",
            () => Golden.Bytes(Entry(entryDate: new DateOnly(2028, 2, 29)), 1, Gen)),

        // ══════════════════════ 4. النص العربي — التطبيع ══════════════════════
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

        // ══════════════════════ 5. محارف التحكّم الاتجاهي ══════════════════════
        new("text.rlm.u200f.rejected",
            "U+200F داخل بيان عربي: غير مرئي، يغيّر البصمة، وتشخيصه شبه مستحيل — مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text(MemoAr.Insert(4, Rlm)))),

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

        new("text.zwj.u200d.rejected", "U+200D مرفوض في v1 — قرار مُعلن، انظر SPEC",
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
            "لو سُمح بها لكانت تغيّر البصمة: النصّان بعد إزالة الفرق يتطابقان، وقبلها لا",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: MemoAr)),
                () => Link(Entry(memoAr: TextRules.CleanForInput(MemoAr + Nbsp + "فرع")))
            ], "متجه ضبط: النصّان يختلفان فعلاً")),

        // ══════════════════════ 6. الأرقام في النص ══════════════════════
        new("text.arabic.indic.digits.rejected",
            "أرقام عربية-هندية U+0660–U+0669 في حقل مُوقَّع: مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Text("قيد رقم " + ArabicIndic100))),

        new("text.eastern.arabic.indic.digits.rejected",
            "أرقام شرقية U+06F0–U+06F9: مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Text("قيد ۱۰۰"))),

        new("text.clean.folds.arabic.indic.digits",
            "تنظيف الحدّ يحوّل ١٠٠ إلى 100",
            () => Golden.Value(TextRules.CleanForInput("قيد رقم " + ArabicIndic100))),

        // ══════════════════════ 7. التطويل وأشكال الألف ══════════════════════
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

        new("search.normalisation.taa.marbuta.and.alef.maqsura",
            "تطبيع البحث: ة -> ه و ى -> ي",
            () => Golden.Value(ArabicSearch.Normalize("مكتبة الرياض الكبرى").Value)),

        new("search.normalisation.tashkeel_removed",
            "تطبيع البحث يزيل التشكيل",
            () => Golden.Value(ArabicSearch.Normalize("مَكْتَبَة الرِّيَاض").Value)),

        new("search.normalisation.mixed.case.latin",
            "تطبيع البحث يخفض حالة اللاتيني ويطوي المسافات",
            () => Golden.Value(ArabicSearch.Normalize("  Riyadh   BRANCH  ").Value)),

        new("text.tashkeel.preserved.in.signed.value",
            "التشكيل محفوظ في القيمة الموقَّعة — القيمة الموقَّعة ليست مفتاح بحث",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memoAr: "مكتبة الرياض")),
                () => Link(Entry(memoAr: "مَكْتَبَة الرِّيَاض"))
            ])),

        // ══════════════════════ 8. أشكال العرض العربية ══════════════════════
        new("text.lam.alef.presentation.form.rejected",
            "U+FEFB (ﻻ) يصمد أمام NFC ولا يفكّه إلا NFKC (مقيس) — مصدره النسخ من PDF. مرفوض",
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

        // ══════════════════════ 9. الفراغ والغياب والأسطر ══════════════════════
        new("text.empty.string.vs.null.differ",
            "نصّ فارغ وغياب قيمة يعطيان بايتات مختلفة — النوع T بطول 0 مقابل النوع N",
            () => Golden.DifferentHash(
            [
                () => Link(Entry(memo: "")),
                () => Link(Entry(memo: null))
            ])),

        new("text.empty.string.bytes",
            "البايتات القانونية لقيد ببيان فارغ",
            () => Golden.Bytes(Entry(memo: ""), 1, Gen)),

        new("text.null.bytes",
            "البايتات القانونية لقيد بلا بيان",
            () => Golden.Bytes(Entry(memo: null), 1, Gen)),

        new("text.multiline.narration",
            "بيان متعدّد الأسطر بـLF — سابقة الطول تحفظ الأسطر داخل الحمولة بلا لبس",
            () => Golden.Bytes(Entry(memoAr: "قيد تسوية:\nالسطر الأول\nالسطر الثاني"), 1, Gen)),

        new("text.crlf.rejected",
            "CR مرفوض: نفس البيان على Windows وعلى Linux يجب ألا يعطي بصمتين",
            () => Golden.Reject(() => CanonicalValue.Text("قيد تسوية:\r\nالسطر الأول"))),

        new("text.crlf.cleaned.matches.lf",
            "بعد توحيد نهايات الأسطر تتطابق البصمة",
            () => Golden.SameHash(
            [
                () => Link(Entry(memoAr: "قيد تسوية:\nالسطر الأول")),
                () => Link(Entry(memoAr: TextRules.CleanForInput("قيد تسوية:\r\nالسطر الأول")))
            ])),

        new("text.line.separator.u2028.rejected",
            "U+2028 (فاصل سطر Unicode) مرفوض: نهاية سطر ثالثة تأتي من اللصق من HTML والمحرّرات",
            () => Golden.Reject(() => CanonicalValue.Text("سطر\u2028سطر"))),

        new("text.paragraph.separator.u2029.rejected",
            "U+2029 (فاصل فقرة Unicode) مرفوض",
            () => Golden.Reject(() => CanonicalValue.Text("سطر\u2029سطر"))),

        new("text.soft.hyphen.u00ad.rejected",
            "U+00AD (الشرطة اللينة) مرفوضة: غير مرئية، وفئتها Cf",
            () => Golden.Reject(() => CanonicalValue.Text("Riy\u00ADadh"))),

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

        new("text.nul.rejected",
            "U+0000 مرفوض: PostgreSQL لا تخزّنه في text أصلاً (مقيس: خطأ 22021)",
            () => Golden.Reject(() => CanonicalValue.Text("قيد" + "\u0000" + "مبتور"))),

        new("text.tab.control.rejected",
            "TAB مرفوض داخل النص — وهو فاصل الشكل السلكي",
            () => Golden.Reject(() => CanonicalValue.Text("قيد\tمبتور"))),

        new("text.lone.surrogate.rejected",
            "بديل غير مقترن مرفوض بوضوح بدل أن ينفجر String.Normalize لاحقاً",
            () => Golden.Reject(() => CanonicalValue.Text("قيد\uD800مبتور"))),

        // ══════════════════════ 10. حقن حدود الحقول ══════════════════════
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

        new("injection.end.marker.in.narration",
            "بيان يحتوي علامة النهاية — لا يُنهي المستند",
            () => Golden.Bytes(Entry(memoAr: "end C 0 "), 1, Gen)),

        // ══════════════════════ 11. مختلط عربي/إنجليزي ══════════════════════
        new("text.mixed.arabic.english.digits",
            "اسم مختلط عربي/إنجليزي/أرقام — الحالة التي تُحقن فيها محارف الاتجاه عملياً",
            () => Golden.Bytes(Entry(memoAr: "فرع الرياض - Riyadh Branch 2026 (VAT 15%)"), 1, Gen)),

        new("text.mixed.with.injected.lrm.rejected",
            "نفس الاسم وقد لصقته الواجهة مع U+200E — مرفوض عند الحدّ",
            () => Golden.Reject(() => CanonicalValue.Text("فرع الرياض - " + Lrm + "Riyadh Branch 2026"))),

        // ══════════════════════ 12. المجموعات والترتيب ══════════════════════
        new("lines.two.line.entry", "قيد بسطرين", () => Golden.Bytes(Entry(), 1, Gen)),

        new("lines.fifty.line.entry", "قيد بخمسين سطراً",
            () => Golden.Bytes(Entry(lineCount: 50), 1, Gen)),

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

        new("status.cancelled.entry",
            "قيد ملغى — الإلغاء حالة موقَّعة لا حذف",
            () => Golden.Bytes(Entry(status: "CANCELLED"), 1, Gen)),

        new("token.lowercase.rejected",
            "الرموز [A-Z0-9_] فقط: «Posted» مرفوضة",
            () => Golden.Reject(() => CanonicalValue.Token("Posted"))),

        // ══════════════════════ 13. المخطّط ومجموعة الاستثناء ══════════════════════
        new("schema.excluded.field.rejected",
            "محاولة إدراج حقل مستثنى (بصمة السجل نفسه) مرفوضة",
            () => Golden.Reject(() => JournalEntrySchema.V1.NewDocument()
                .Set("entry_hash", CanonicalValue.Text("x")))),

        new("schema.search.column.excluded.rejected",
            "‼ محاولة إدراج العمود المطبَّع للبحث مرفوضة — هذه هي المصيدة ع-4 بعينها",
            () => Golden.Reject(() => JournalEntrySchema.V1.NewDocument()
                .Set("memo_ar_search", CanonicalValue.Text("x")))),

        new("schema.unknown.field.rejected",
            "حقل غير معرّف مرفوض — لا حقول ضمنية",
            () => Golden.Reject(() => JournalEntrySchema.V1.NewDocument()
                .Set("whatever", CanonicalValue.Text("x")))),

        new("schema.missing.required.field.rejected",
            "حقل مطلوب ناقص مرفوض — لا قيم افتراضية ضمنية",
            () => Golden.Reject(() => JournalEntrySchema.V1.NewDocument()
                .Set("tenant_id", CanonicalValue.Text(Tenant)).Build())),

        new("schema.exclusion.set.listing",
            "مجموعة الاستثناء كاملة، بأسمائها وأسبابها — جزء من المواصفة لا تعليق",
            () => Golden.Value(string.Join("\n", JournalEntrySchema.V1.Exclusions
                .Select(e => $"{e.Name}|{e.Reason}")))),

        // ══════════════════════ 14. الإصدارات ══════════════════════
        new("version.registry.contains.v1",
            "سجلّ الإصدارات يحوي v1 — سجلات v1 تبقى قابلة للتحقق بعد ظهور v2",
            () => Golden.Value(string.Join(",", CanonRegistry.Versions.OrderBy(v => v, StringComparer.Ordinal)))),

        new("version.unknown.rejected",
            "إصدار غير معروف مرفوض بدل أن يُفترض",
            () => Golden.Reject(() => CanonRegistry.Resolve("v99"))),

        // ══════════════════════ 15. بيئة التشغيل ══════════════════════
        new("runtime.self.test.passes",
            "الفحص السلوكي لبيئة التشغيل: NFC يعمل فعلاً، لا كما يدّعي AppContext",
            () => Golden.Value(CanonicalRuntime.SelfTest().Ok ? "OK" : "BROKEN",
                "وضع العولمة الثابتة يجعل String.Normalize لا-شيء بصمت (مقيس)"))
    ];

    private static CanonicalDocument BuildTwoLines(int a, string accA, int b, string accB)
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
            .Set("memo", CanonicalValue.Text("order"))
            .Set("memo_ar", CanonicalValue.Text(MemoAr))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("currency", CanonicalValue.Token("SAR"))
            .SetGroup("lines",
            [
                i => i.Set("line_no", CanonicalValue.Integer(a))
                      .Set("account_code", CanonicalValue.Text(accA))
                      .Set("debit", CanonicalValue.Amount(a == 1 ? 1500m : 0m))
                      .Set("credit", CanonicalValue.Amount(a == 1 ? 0m : 1500m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("سطر")),
                i => i.Set("line_no", CanonicalValue.Integer(b))
                      .Set("account_code", CanonicalValue.Text(accB))
                      .Set("debit", CanonicalValue.Amount(b == 1 ? 1500m : 0m))
                      .Set("credit", CanonicalValue.Amount(b == 1 ? 0m : 1500m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("سطر"))
            ])
            .Build();

    /// <summary>نفس القيد المرجعي، بترتيب استدعاءات Set معكوس تماماً.</summary>
    private static CanonicalDocument EntryReversedSetOrder()
        => JournalEntrySchema.V1.NewDocument()
            .SetGroup("lines",
            [
                i => i.Set("description", CanonicalValue.Text("النقدية"))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("debit", CanonicalValue.Amount(1500.0000m))
                      .Set("account_code", CanonicalValue.Text("1010"))
                      .Set("line_no", CanonicalValue.Integer(1)),
                i => i.Set("description", CanonicalValue.Text("المبيعات"))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("credit", CanonicalValue.Amount(1500.0000m))
                      .Set("debit", CanonicalValue.Amount(0m))
                      .Set("account_code", CanonicalValue.Text("4010"))
                      .Set("line_no", CanonicalValue.Integer(2))
            ])
            .Set("currency", CanonicalValue.Token("SAR"))
            .Set("idempotency_key", CanonicalValue.Text("pos-2026-05-01-000042"))
            .Set("source_ref", CanonicalValue.Null())
            .Set("memo_ar", CanonicalValue.Text(MemoAr))
            .Set("memo", CanonicalValue.Text("revenue recognition"))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("posted_at", CanonicalValue.Instant(Posted))
            .Set("entry_date", CanonicalValue.Date(EntryDate))
            .Set("entry_no", CanonicalValue.Integer(42))
            .Set("entry_id", CanonicalValue.Uuid(EntryId))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("tenant_id", CanonicalValue.Text(Tenant))
            .Build();
}
