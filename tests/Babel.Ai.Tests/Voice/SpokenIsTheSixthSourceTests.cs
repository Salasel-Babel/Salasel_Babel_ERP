using Babel.Ai.Capture;
using Babel.Ai.Promotion;
using Babel.Ai.Reconciliation;
using Babel.Ai.Tests.Support;
using Babel.Contracts.Capture;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>«منطوق» مصدرٌ سادس في النموذج القائم، لا مفهوم موازٍ.</b>
/// <para>
/// والمقصود بذلك عملياً شيء واحد: أن كل ما يحمله نموذج المصادر من ضمانات ينطبق على
/// المنطوق بلا سطر إضافي — الواجب البشري، ودخول مجموعة التأكيد، ومنع الترقية قبله.
/// وهذا بالضبط ما تقيسه هذه المجموعة: <b>لا شيء يُنطَق يصير حقيقة محاسبية</b> (‏ADR-0024).
/// </para>
/// </summary>
public sealed class SpokenIsTheSixthSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void المنطوق_واجبه_مراجعة_ويحمل_ثقة()
    {
        Assert.Equal(ProvenanceDuty.Review, FieldProvenanceInfo.DutyOf(FieldProvenance.Spoken));
        Assert.True(FieldProvenanceInfo.CarriesConfidence(FieldProvenance.Spoken));
        Assert.Equal("ai.capture.provenance.spoken", FieldProvenanceInfo.ResourceKeyOf(FieldProvenance.Spoken));
    }

    /// <summary>
    /// رتبة الثقة: أضعف من القراءة الضوئية وأقوى من اقتراح النموذج.
    /// <para>
    /// وليست مساوية للقراءة عمداً: القراءة تُخطئ في المحرف، والتفريغ يُخطئ في
    /// <b>الرقم كلّه</b> — «خمسة عشر» و«خمسين» تفريغان متجاوران وفرقهما 35.
    /// </para>
    /// </summary>
    [Fact]
    public void رتبة_المنطوق_بين_المقروء_والمُستنتَج()
    {
        Assert.True(DraftReconciler.TrustRank(FieldProvenance.Spoken) < DraftReconciler.TrustRank(FieldProvenance.Read));
        Assert.True(DraftReconciler.TrustRank(FieldProvenance.Spoken) > DraftReconciler.TrustRank(FieldProvenance.Inferred));
        Assert.True(DraftReconciler.TrustRank(FieldProvenance.Spoken) < DraftReconciler.TrustRank(FieldProvenance.Attested));
    }

    [Fact]
    public void حقلٌ_منطوق_يدخل_مجموعة_ما_يحتاج_قراراً_بشرياً()
    {
        CapturedField<decimal> spoken = CapturedField<decimal>.Spoken(1500.00m, 0.85m);

        Assert.Equal(FieldProvenance.Spoken, spoken.Provenance);
        Assert.Equal(ProvenanceDuty.Review, spoken.Duty);
        Assert.Equal(CaptureOriginKeys.SpokenOnDevice, spoken.OriginKey);
        Assert.Equal(0.85m, spoken.Confidence);
    }

    /// <summary>
    /// <b>الاختبار الذي يهمّ:</b> مسوّدة حقلُها منطوق <b>لا تُرقّى</b> قبل تأكيد ذلك
    /// الحقل بعينه — حتى لو كان حسابها متّسقاً تماماً وكل ما سواه مُصدَّقاً.
    /// </summary>
    [Fact]
    public async Task مسوّدة_فيها_حقل_منطوق_لا_تُرقّى_قبل_تأكيده()
    {
        CaptureHarness harness = CaptureHarness.Create(CaptureHarness.ConsistentInvoice());

        Result<CapturedInvoiceDraft> captured = await harness.Service.CaptureAsync(
            harness.Tenant,
            harness.Actor,
            harness.Request(CaptureHarness.Phase1Qr(1150.00m, 150.00m)),
            Ct);

        Assert.True(captured.IsSuccess);

        // رقم الفاتورة يصل هنا منطوقاً لا مقروءاً — وهو المسار الذي يفتحه الإدخال الصوتي.
        CapturedInvoiceDraft spokenDraft = captured.Value with
        {
            InvoiceNumber = CapturedField<string>.Spoken("INV-4417", 0.72m),
        };

        await harness.Store.SaveAsync(spokenDraft, Ct);

        Assert.Contains(CapturedInvoiceDraft.InvoiceNumberField, spokenDraft.FieldsNeedingHumanJudgement());

        // تأكيدٌ يشمل كل شيء **إلا** الحقل المنطوق.
        IReadOnlyList<string> allButSpoken =
            [.. spokenDraft.FieldsNeedingHumanJudgement().Where(field => field != CapturedInvoiceDraft.InvoiceNumberField)];

        Result<PromotedDocumentReference> refused = await harness.Service.PromoteAsync(
            harness.Tenant,
            harness.Actor,
            spokenDraft.DraftId,
            new PromotionConfirmation(new HashSet<string>(allButSpoken, StringComparer.Ordinal)),
            Ct);

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "ai.capture.field_not_confirmed");
        Assert.Contains(refused.Errors, error => error.MessageAr.Contains(CapturedInvoiceDraft.InvoiceNumberField, StringComparison.Ordinal));
        Assert.Empty(harness.Receiver.Received);

        // وبعد تأكيده — وبيد إنسان — يمرّ. فالحارس ليس جداراً بل شرط تأكيد.
        Result<PromotedDocumentReference> promoted = await harness.Service.PromoteAsync(
            harness.Tenant,
            harness.Actor,
            spokenDraft.DraftId,
            new PromotionConfirmation(new HashSet<string>(spokenDraft.FieldsNeedingHumanJudgement(), StringComparer.Ordinal)),
            Ct);

        Assert.True(promoted.IsSuccess, promoted.IsFailure ? promoted.Errors[0].MessageAr : string.Empty);
        Assert.Single(harness.Receiver.Received);
    }

    /// <summary>
    /// حارسٌ على التعداد نفسه: كل مصدر معلَن له واجب ومفتاح مورد. ومصدرٌ يُضاف بلا
    /// إدخاله في الجدولين يرمي وقت التشغيل على شاشة مستخدم لا وقت البناء.
    /// </summary>
    [Fact]
    public void كل_مصدر_في_التعداد_له_واجب_ومفتاح()
    {
        FieldProvenance[] all = Enum.GetValues<FieldProvenance>();

        Assert.Equal(6, all.Length);

        foreach (FieldProvenance provenance in all)
        {
            ProvenanceDuty duty = FieldProvenanceInfo.DutyOf(provenance);
            Assert.False(string.IsNullOrWhiteSpace(FieldProvenanceInfo.ResourceKeyOf(provenance)));
            Assert.False(string.IsNullOrWhiteSpace(FieldProvenanceInfo.ResourceKeyOf(duty)));
            Assert.True(DraftReconciler.TrustRank(provenance) > 0);
        }
    }
}
