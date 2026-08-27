using System.Globalization;
using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Promotion;
using Babel.Ai.Reconciliation;
using Babel.Ai.Tests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Capture;

/// <summary>
/// <b>المسار كاملاً بلا شبكة:</b> رمز موقَّع، ثم استخراج، ثم مطابقة حسابية، ثم تأكيد
/// بشري لكل حقل لا تكفي فيه اللمحة، ثم ترقية عبر الوحدة المالكة.
/// <para>
/// وكل اختبار هنا <b>يبني بيئته كاملةً</b> ويمرّ وحده: مخزن جديد ومستأجر جديد ومزوّد
/// مبذور — لا حالة يتركها اختبار لجاره.
/// </para>
/// </summary>
public sealed class CaptureEndToEndTests(ITestOutputHelper output)
{
    /// <summary>رمز إلغاء الاختبار — يُمرَّر إلى كل نداء غير متزامن.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── ما يصل مُصدَّقاً وما يصل مقروءاً ────────────────────────────────────

    /// <summary>
    /// <b>الرمز أولاً والنموذج ثانياً.</b> على فاتورة ملتزمة تصل خمسة حقول
    /// <c>attested</c> من الرمز، وتصل السطور ورقم الفاتورة <c>read</c>، وتُملأ العملة
    /// والنسبة <c>defaulted</c>، ويأتي الحدث <c>inferred</c>.
    /// </summary>
    [Fact]
    public async Task On_a_compliant_invoice_the_QR_fields_arrive_attested_and_the_lines_arrive_read()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct);

        Assert.True(captured.IsSuccess, Report(captured));
        CapturedInvoiceDraft draft = captured.Value;

        Print(draft);

        Assert.Equal(FieldProvenance.Attested, draft.SellerName.Provenance);
        Assert.Equal(FieldProvenance.Attested, draft.SellerVatNumber.Provenance);
        Assert.Equal(FieldProvenance.Attested, draft.IssuedOn.Provenance);
        Assert.Equal(FieldProvenance.Attested, draft.TaxTotal.Provenance);
        Assert.Equal(FieldProvenance.Attested, draft.GrossTotal.Provenance);
        Assert.Equal(CaptureOriginKeys.SignedQr, draft.GrossTotal.OriginKey);

        Assert.Equal(FieldProvenance.Read, draft.InvoiceNumber.Provenance);
        Assert.Equal(FieldProvenance.Read, draft.Net.Provenance);
        Assert.All(draft.Lines, line => Assert.Equal(FieldProvenance.Read, line.LineNet.Provenance));

        Assert.Equal(FieldProvenance.Defaulted, draft.Currency.Provenance);
        Assert.Equal(FieldProvenance.Defaulted, draft.TaxRate.Provenance);

        Assert.NotNull(draft.Suggestion);
        Assert.Equal(CaptureHarness.EventCode, draft.Suggestion.EventCode);

        // الحقل المُصدَّق لا يحمل درجة ثقة: مصدره لا يقيس ثقة، ورقمٌ هناك ادّعاء كاذب.
        Assert.Null(draft.GrossTotal.Confidence);
        Assert.NotNull(draft.Net.Confidence);

        // والواجب البشري مشتقّ من المصدر لا من الشاشة.
        Assert.Equal(ProvenanceDuty.Glance, draft.GrossTotal.Duty);
        Assert.Equal(ProvenanceDuty.Review, draft.Net.Duty);
        Assert.Equal(DraftState.Reconciled, draft.State);
        Assert.Empty(draft.Findings);
    }

    /// <summary>بلا رمز: كل حقل يصير <c>read</c> — ولا شيء يُعرض مُصدَّقاً وهو مقروء.</summary>
    [Fact]
    public async Task Without_a_QR_nothing_is_shown_as_attested()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(qrPayload: null), Ct);

        Assert.True(captured.IsSuccess, Report(captured));
        Print(captured.Value);

        Assert.Equal(FieldProvenance.Read, captured.Value.SellerName.Provenance);
        Assert.Equal(FieldProvenance.Read, captured.Value.GrossTotal.Provenance);
        Assert.DoesNotContain(
            captured.Value.FieldsNeedingHumanJudgement(),
            field => field == CapturedInvoiceDraft.TaxRateField);
    }

    /// <summary>
    /// رمز المرحلة الأولى مُصدَّق بمعنى «كتبه المُصدِر» لا بمعنى «وقّعه»، والفرق يُعرض
    /// في منشأ الحقل ولا يُطمس تحت كلمة واحدة.
    /// </summary>
    [Fact]
    public async Task A_phase_one_QR_is_marked_as_unsigned_even_though_it_is_attested()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase1Qr(1150.00m, 150.00m)), Ct);

        Assert.True(captured.IsSuccess, Report(captured));
        output.WriteLine("منشأ الإجمالي: " + captured.Value.GrossTotal.OriginKey);

        Assert.Equal(FieldProvenance.Attested, captured.Value.GrossTotal.Provenance);
        Assert.Equal(CaptureOriginKeys.UnsignedQr, captured.Value.GrossTotal.OriginKey);
    }

    /// <summary>رمز معطوب <b>يوقف الالتقاط</b> ولا ينحدر بصمت إلى قراءة ضوئية.</summary>
    [Fact]
    public async Task A_corrupt_QR_stops_the_capture_instead_of_silently_degrading_to_optical_reading()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request("هذا ليس رمزاً"), Ct);

        output.WriteLine(Report(captured));
        Assert.True(captured.IsFailure);
        Assert.Single(captured.Errors, e => e.Code == "ai.capture.qr_unreadable");
        Assert.Empty((await harness.Service.ListAsync(harness.Tenant, harness.Actor, Ct)).Value);
    }

    // ── الحساب قبل الإنسان ─────────────────────────────────────────────────

    /// <summary>
    /// <b>المثال الذي يسمّي الرقم المختلِف:</b> صافٍ قُرئ 1050.00 والسطور 1000.00
    /// والإجمالي مُصدَّق 1150.00. ثلاث ملاحظات، وكلها تشير إلى <c>net</c> — الطرف
    /// <b>الأضعف مصدراً</b> — لا إلى الإجمالي الذي وقّعه المُصدِر.
    /// </summary>
    [Fact]
    public async Task When_the_figures_disagree_the_finding_names_the_weakest_sourced_figure_and_the_difference()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice(net: 1050.00m));

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct);

        Assert.True(captured.IsSuccess, Report(captured));
        CapturedInvoiceDraft draft = captured.Value;

        foreach (ReconciliationFinding finding in draft.Findings)
        {
            output.WriteLine(finding.Code);
            output.WriteLine("  الحقل المشتبه به : " + finding.SuspectField);
            output.WriteLine("  المتوقَّع         : " + finding.Expected.ToString("0.00", CultureInfo.InvariantCulture));
            output.WriteLine("  المرصود          : " + finding.Observed.ToString("0.00", CultureInfo.InvariantCulture));
            output.WriteLine("  الفرق            : " + finding.Divergence.ToString("0.00", CultureInfo.InvariantCulture));
            output.WriteLine("  " + finding.Message.Arabic);
        }

        Assert.Equal(DraftState.Disputed, draft.State);
        Assert.Equal(3, draft.Findings.Count);
        Assert.All(draft.Findings, finding => Assert.Equal(CapturedInvoiceDraft.NetField, finding.SuspectField));

        ReconciliationFinding sum = draft.Findings.Single(static f => f.Code == "capture.line_sum_disagrees_with_net");
        Assert.Equal(1000.00m, sum.Expected);
        Assert.Equal(1050.00m, sum.Observed);
        Assert.Equal(50.00m, sum.Divergence);
        Assert.Contains("50.00", sum.Message.Arabic, StringComparison.Ordinal);

        // ومسوّدة لا يتّسق حسابها لا تُرقّى، حتى لو أكّد الإنسان كل حقل فيها.
        Result<PromotedDocumentReference> refused = await harness.Service.PromoteAsync(
            harness.Tenant, harness.Actor, draft.DraftId, CaptureHarness.ConfirmAll(draft), Ct);

        output.WriteLine("الترقية: " + Report(refused));
        Assert.True(refused.IsFailure);
        Assert.Single(refused.Errors, e => e.Code == "ai.capture.draft_has_open_findings");
    }

    /// <summary>
    /// والحلقة تُغلق: الإنسان يكتب الرقم الذي سمّته الملاحظة، فيصير مصدره <c>typed</c>،
    /// وتُعاد المطابقة، وتُفتح الترقية.
    /// </summary>
    [Fact]
    public async Task The_human_types_the_figure_the_finding_named_and_the_draft_becomes_promotable()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice(net: 1050.00m));

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        Assert.Equal(DraftState.Disputed, draft.State);

        Result<CapturedInvoiceDraft> corrected = await harness.Service.CorrectAsync(
            harness.Tenant, harness.Actor, draft.DraftId, [new FieldCorrection(CapturedInvoiceDraft.NetField, "1000.00")], Ct);

        Assert.True(corrected.IsSuccess, Report(corrected));
        output.WriteLine("بعد التصحيح: الحالة " + corrected.Value.State.ToString()
            + " · مصدر الصافي " + corrected.Value.Net.Provenance.ToString());

        Assert.Equal(DraftState.Reconciled, corrected.Value.State);
        Assert.Equal(FieldProvenance.Typed, corrected.Value.Net.Provenance);
        Assert.Equal(ProvenanceDuty.Own, corrected.Value.Net.Duty);
        Assert.Empty(corrected.Value.Findings);
    }

    /// <summary>
    /// <b>ولا يُعاد كتابة حقل مُصدَّق.</b> الكتابة فوق إجمالٍ وقّعه المُصدِر تُزيل أقوى
    /// ضمانة على المسوّدة، ولا تقع عن قصد بل لأن الشاشة سمحت.
    /// </summary>
    [Fact]
    public async Task An_attested_field_cannot_be_retyped_by_a_human()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        Result<CapturedInvoiceDraft> refused = await harness.Service.CorrectAsync(
            harness.Tenant, harness.Actor, draft.DraftId,
            [new FieldCorrection(CapturedInvoiceDraft.GrossTotalField, "9999.00")], Ct);

        output.WriteLine(Report(refused));
        Assert.True(refused.IsFailure);
        Assert.Single(refused.Errors, e => e.Code == "ai.capture.attested_field_cannot_be_retyped");

        // والمسوّدة المحفوظة لم تتغيّر: الرفض بلا أثر.
        Assert.Equal(1150.00m, (await harness.Store.FindAsync(harness.Tenant, draft.DraftId, Ct))!.GrossTotal.Value);
    }

    // ── الترقية ────────────────────────────────────────────────────────────

    /// <summary>
    /// الترقية تُسلّم <b>أمراً</b> للوحدة المالكة، لا تكتب شيئاً. والأمر يحمل رمز الحدث
    /// ومصدر كل حقل، ولا يحمل سطر ترحيل ولا رمز حساب.
    /// </summary>
    [Fact]
    public async Task Promotion_hands_an_order_to_the_owning_module_and_writes_nothing_itself()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        Result<PromotedDocumentReference> promoted = await harness.Service.PromoteAsync(
            harness.Tenant, harness.Actor, draft.DraftId, CaptureHarness.ConfirmAll(draft), Ct);

        Assert.True(promoted.IsSuccess, Report(promoted));
        PromotionOrder order = Assert.Single(harness.Receiver.Received);

        output.WriteLine("المستند الناتج: " + promoted.Value.Module.ToString() + " · " + promoted.Value.DocumentType + " · " + promoted.Value.DocumentId);
        output.WriteLine("الحدث         : " + order.EventCode);
        output.WriteLine("الدور         : " + order.RoleCode);
        foreach (KeyValuePair<string, FieldProvenance> pair in order.Provenance.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            output.WriteLine("  " + pair.Key + " ← " + pair.Value.ToString());
        }

        Assert.Equal(BabelModule.Purchasing, promoted.Value.Module);
        Assert.Equal(CaptureHarness.EventCode, order.EventCode);
        Assert.Equal(1150.00m, order.GrossTotal);
        Assert.Equal(FieldProvenance.Attested, order.Provenance[CapturedInvoiceDraft.GrossTotalField]);
        Assert.Equal(FieldProvenance.Read, order.Provenance[CapturedInvoiceDraft.NetField]);
        Assert.Equal(DraftState.Promoted, (await harness.Store.FindAsync(harness.Tenant, draft.DraftId, Ct))!.State);
    }

    /// <summary>
    /// <b>حقلٌ يوجب مراجعة ولم يُؤكَّد يمنع الترقية</b> — وهذا هو ما يجعل التمييز بين
    /// المصادر عاملاً لا زينة على الشاشة.
    /// </summary>
    [Fact]
    public async Task A_field_needing_review_that_was_not_confirmed_blocks_the_promotion_by_name()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        HashSet<string> partial = new(draft.FieldsNeedingHumanJudgement(), StringComparer.Ordinal);
        partial.Remove(CapturedInvoiceDraft.NetField);

        Result<PromotedDocumentReference> refused = await harness.Service.PromoteAsync(
            harness.Tenant, harness.Actor, draft.DraftId, new PromotionConfirmation(partial), Ct);

        output.WriteLine("الحقول التي توجب حكماً بشرياً: " + string.Join(" · ", draft.FieldsNeedingHumanJudgement()));
        output.WriteLine(Report(refused));

        Assert.True(refused.IsFailure);
        Error error = Assert.Single(refused.Errors, e => e.Code == "ai.capture.field_not_confirmed");
        Assert.Contains(CapturedInvoiceDraft.NetField, error.MessageAr, StringComparison.Ordinal);
        Assert.Empty(harness.Receiver.Received);
    }

    /// <summary>مسوّدة بلا حدث لا تُرقّى: رمز الحدث إلزام على مسارَي الترحيل معاً.</summary>
    [Fact]
    public async Task A_draft_without_an_event_code_cannot_be_promoted()
    {
        ComposedExtraction without = CaptureHarness.ConsistentInvoice() with { SuggestedEventCode = string.Empty };
        CaptureHarness harness = CaptureHarness.Create(without);

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        Result<PromotedDocumentReference> refused = await harness.Service.PromoteAsync(
            harness.Tenant, harness.Actor, draft.DraftId, CaptureHarness.ConfirmAll(draft), Ct);

        output.WriteLine(Report(refused));
        Assert.True(refused.IsFailure);
        Assert.Single(refused.Errors, e => e.Code == "ai.capture.no_suggestion");
    }

    /// <summary>رفض الوحدة المالكة يُعاد كما هو، والمسوّدة تبقى قابلة للترقية لا «مُرقّاة».</summary>
    [Fact]
    public async Task A_refusal_from_the_owning_module_leaves_the_draft_unpromoted()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice(), new RefusingReceiver());

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct)).Value;

        Result<PromotedDocumentReference> refused = await harness.Service.PromoteAsync(
            harness.Tenant, harness.Actor, draft.DraftId, CaptureHarness.ConfirmAll(draft), Ct);

        output.WriteLine(Report(refused));
        Assert.True(refused.IsFailure);
        Assert.Equal(DraftState.Reconciled, (await harness.Store.FindAsync(harness.Tenant, draft.DraftId, Ct))!.State);
    }

    /// <summary>اقتراح يحمل رمز حساب <b>يُسقط الالتقاط كله</b>، ولا تُحفظ مسوّدة.</summary>
    [Fact]
    public async Task A_suggestion_carrying_a_ledger_code_fails_the_whole_capture()
    {
        ComposedExtraction poisoned = CaptureHarness.ConsistentInvoice() with { SuggestedEventCode = "purchasing.1210" };
        CaptureHarness harness = CaptureHarness.Create(poisoned);

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(CaptureHarness.Phase2Qr(1150.00m, 150.00m)), Ct);

        output.WriteLine(Report(captured));
        Assert.True(captured.IsFailure);
        Assert.Single(captured.Errors, e => e.Code == "ai.capture.suggestion_names_a_ledger_code");
        Assert.Empty((await harness.Service.ListAsync(harness.Tenant, harness.Actor, Ct)).Value);
    }

    /// <summary>مزوّد بلا جواب يقول ذلك — ولا يخترع مسوّدة من فراغ.</summary>
    [Fact]
    public async Task A_provider_with_no_answer_says_so_instead_of_inventing_a_draft()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        ExtractionRequest unknown = harness.Request(null) with { DocumentId = "CAP-UNKNOWN" };
        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(harness.Tenant, harness.Actor, unknown, Ct);

        output.WriteLine(Report(captured));
        Assert.True(captured.IsFailure);
        Assert.Single(captured.Errors, e => e.Code == "ai.capture.provider_has_no_answer");
    }

    /// <summary>المسوّدة تُعزَل بمستأجرها: المستأجر جزء من المفتاح لا مرشّح استعلام.</summary>
    [Fact]
    public async Task A_draft_is_invisible_to_another_tenant()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        CapturedInvoiceDraft draft = (await harness.Service.CaptureAsync(
            harness.Tenant, harness.Actor, harness.Request(null), Ct)).Value;

        TenantId other = new(Guid.CreateVersion7());
        Result<CapturedInvoiceDraft> found = await harness.Service.FindAsync(other, harness.Actor, draft.DraftId, Ct);

        output.WriteLine(Report(found));
        Assert.True(found.IsFailure);
        Assert.Single(found.Errors, e => e.Code == "ai.capture.draft_not_found");
    }

    private void Print(CapturedInvoiceDraft draft)
    {
        output.WriteLine("المسوّدة " + draft.DraftId.ToString("D", CultureInfo.InvariantCulture) + " · الحالة " + draft.State.ToString());
        output.WriteLine("المزوّد  : " + draft.ExtractionProviderId);
        Line("seller_name", draft.SellerName.Provenance, draft.SellerName.OriginKey, draft.SellerName.Confidence, draft.SellerName.Value);
        Line("seller_vat_number", draft.SellerVatNumber.Provenance, draft.SellerVatNumber.OriginKey, draft.SellerVatNumber.Confidence, draft.SellerVatNumber.Value);
        Line("invoice_number", draft.InvoiceNumber.Provenance, draft.InvoiceNumber.OriginKey, draft.InvoiceNumber.Confidence, draft.InvoiceNumber.Value);
        Line("issued_on", draft.IssuedOn.Provenance, draft.IssuedOn.OriginKey, draft.IssuedOn.Confidence, draft.IssuedOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Line("currency", draft.Currency.Provenance, draft.Currency.OriginKey, draft.Currency.Confidence, draft.Currency.Value.ToString());
        Line("net", draft.Net.Provenance, draft.Net.OriginKey, draft.Net.Confidence, draft.Net.Value.ToString("0.00", CultureInfo.InvariantCulture));
        Line("tax_rate", draft.TaxRate.Provenance, draft.TaxRate.OriginKey, draft.TaxRate.Confidence, draft.TaxRate.Value.ToString("0.####", CultureInfo.InvariantCulture));
        Line("tax_total", draft.TaxTotal.Provenance, draft.TaxTotal.OriginKey, draft.TaxTotal.Confidence, draft.TaxTotal.Value.ToString("0.00", CultureInfo.InvariantCulture));
        Line("gross_total", draft.GrossTotal.Provenance, draft.GrossTotal.OriginKey, draft.GrossTotal.Confidence, draft.GrossTotal.Value.ToString("0.00", CultureInfo.InvariantCulture));

        if (draft.Suggestion is not null)
        {
            output.WriteLine("  suggested_event   ← Inferred   · " + draft.Suggestion.EventCode
                + " · ثقة " + draft.Suggestion.Confidence.ToString("0.00", CultureInfo.InvariantCulture));
        }
    }

    private void Line(string field, FieldProvenance provenance, string origin, decimal? confidence, string value)
    {
        string score = confidence is null ? "—" : confidence.Value.ToString("0.00", CultureInfo.InvariantCulture);
        output.WriteLine("  " + field.PadRight(18) + "← " + provenance.ToString().PadRight(10)
            + " · ثقة " + score + " · " + origin + " · " + value);
    }

    private static string Report<T>(Result<T> result) =>
        result.IsSuccess ? "نجح" : string.Join('\n', result.Errors.Select(static e => e.ToString()));
}
