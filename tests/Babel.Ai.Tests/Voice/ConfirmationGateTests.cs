using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>البوابة — القاعدة التي لا استثناء فيها.</b>
/// <para>
/// <b>كل</b> نيّةٍ تُغيّر الحال يُثبَت عليها هنا أنها <b>لا تستطيع</b> أن تُنفَّذ بلا
/// تأكيد، ثم يُثبَت أنها تُنفَّذ به. والفرق بين الإثباتَين هو الفرق بين حارسٍ موجود
/// وحارسٍ يعمل.
/// </para>
/// <para>
/// والقائمة تُشتقّ من السجلّ لا تُكتب بيد: نيّةٌ تُضاف غداً <b>تدخل هذا الإثبات
/// تلقائياً</b>، فلا تُشحن نيّةٌ مُغيِّرةٌ للحال بلا إثبات تأكيد لأن أحداً نسي سطراً.
/// </para>
/// </summary>
public sealed class ConfirmationGateTests
{
    /// <summary>لكل نيّةٍ مُغيِّرة جملةٌ كاملة من ملفّ المتجهات.</summary>
    public static TheoryData<string> StateChanging()
    {
        TheoryData<string> data = [];

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            VoiceIntent intent = VoiceHarness.Registry.Find(vector.Intent)!;
            if (intent.Kind == VoiceIntentKind.StateChange && intent.Status == VoiceIntentStatus.Published)
            {
                data.Add(vector.Transcript);
            }
        }

        return data;
    }

    /// <summary>ونيّات الاستعلام كلها.</summary>
    public static TheoryData<string> Queries()
    {
        TheoryData<string> data = [];

        foreach (VectorUtterance vector in VoiceVectors.File.Utterances)
        {
            if (VoiceHarness.Registry.Find(vector.Intent)!.Kind == VoiceIntentKind.Query)
            {
                data.Add(vector.Transcript);
            }
        }

        return data;
    }

    /// <summary>
    /// <b>يقرأ ثم يحلّ</b> — والخطوتان معاً هما ما يصل البوّابة في المنتج. وقراءةٌ بلا
    /// حلٍّ تترك كل طرفٍ <see cref="SlotReading.Pending"/>، والبوّابة ترفضه بالاسم؛ وذلك
    /// مُثبَتٌ وحده في <c>ARefusedSlotIsNeverOverwritten</c> و<c>TheThirdPartyIsNeverChosen</c>.
    /// </summary>
    private static VoiceResolution Resolve(string transcript) => VoiceHarness.ReadAndResolve(transcript);

    [Fact]
    public void القائمتان_ليستا_ضامرتين()
    {
        // حارس لا فراغ: مجموعةٌ فارغة تجعل «كل نيّة تحتاج تأكيداً» جملةً لا تفشل أبداً.
        Assert.True(StateChanging().Count >= 11, StateChanging().Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Queries().Count >= 5, Queries().Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(StateChanging))]
    public void لا_عملية_تغير_الحال_تمر_بلا_تأكيد(string transcript)
    {
        VoiceResolution resolution = Resolve(transcript);
        Assert.True(resolution.IsComplete, "الجملة ناقصة، فالإثبات لا يقيس التأكيد: " + transcript);

        Result<VoiceDispatch> withoutConfirmation =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, confirmationToken: null);

        Assert.True(withoutConfirmation.IsFailure, "مرّت بلا تأكيد: " + resolution.Intent.Id);
        Assert.Contains(withoutConfirmation.Errors, error => error.Code == "ai.voice.confirmation_required");
        Assert.Contains(
            withoutConfirmation.Errors,
            error => error.MessageAr.StartsWith(VoiceRefusals.NeedsConfirmationAr, StringComparison.Ordinal));

        // والملخّص يدعو إلى التأكيد نصّاً — يُقرأ ويُعرض معاً.
        Assert.Contains(VoiceReadback.ConfirmCallAr, resolution.ReadbackAr, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(StateChanging))]
    public void التأكيد_الصحيح_وحده_يفتح_البوابة(string transcript)
    {
        VoiceResolution resolution = Resolve(transcript);

        Result<VoiceDispatch> allowed =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Errors[0].MessageAr : string.Empty);
        Assert.True(allowed.Value.ConfirmedByHuman);
        Assert.Equal(VoiceHarness.Caller.CompanyId, allowed.Value.CompanyId);
    }

    [Theory]
    [MemberData(nameof(StateChanging))]
    public void تأكيد_أمر_آخر_يُرفض_فلا_يُنفَّذ_ما_لم_يُقرأ(string transcript)
    {
        VoiceResolution resolution = Resolve(transcript);

        Result<VoiceDispatch> stale = VoiceConfirmationGate.Authorise(
            resolution, VoiceHarness.Caller, resolution.ConfirmationToken + "|تغيّر");

        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == "ai.voice.confirmation_mismatch");
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void الاستعلام_يمر_بلا_تأكيد_ولا_يُوسَم_مؤكَّداً(string transcript)
    {
        VoiceResolution resolution = Resolve(transcript);

        Result<VoiceDispatch> allowed =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, confirmationToken: null);

        Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Errors[0].MessageAr : string.Empty);
        Assert.False(allowed.Value.ConfirmedByHuman);
        Assert.DoesNotContain(VoiceReadback.ConfirmCallAr, resolution.ReadbackAr, StringComparison.Ordinal);
    }

    [Fact]
    public void ما_لا_يملكه_المتكلم_يُرفض_قبل_أن_يُقرأ_عليه_ملخصه()
    {
        VoiceResolution resolution = Resolve(
            "سجل سند صرف للمورد شركة الخليج بمبلغ ألف ريال نقد اليوم");

        VoiceCaller narrow = VoiceHarness.Caller with
        {
            PermittedIntentIds = new HashSet<string>(StringComparer.Ordinal) { "accounting.customer_balance.query" },
        };

        Result<VoiceDispatch> refused =
            VoiceConfirmationGate.Authorise(resolution, narrow, resolution.ConfirmationToken);

        Assert.True(refused.IsFailure);
        Error only = Assert.Single(refused.Errors);
        Assert.Equal("ai.voice.not_permitted", only.Code);
        Assert.StartsWith(VoiceRefusals.NotPermittedAr, only.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>دليلُ الشركة هو الإشارة، لا الاسمُ المُحلَّل.</b> كان القارئ يجمع اسماً بقاعدة
    /// الجمع نفسها ثم يُقارَن بتساوي نصّين باسم الشركة المفتوحة — حكمٌ على الهوية
    /// بالتخمين. فحُذف التحليل، وصار وجودُ الدليل وحده رفضاً.
    /// </summary>
    [Fact]
    public void دليل_شركة_داخل_أمر_آخر_يُرفض_ولا_يُحلَّل_له_اسم()
    {
        VoiceResolution resolution = Resolve(
            "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم في شركة الفروع");

        Assert.True(resolution.CompanyCueHeard);

        Result<VoiceDispatch> refused =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(refused.IsFailure);
        Error company = Assert.Single(
            refused.Errors, error => error.Code == "ai.voice.company_not_switched");

        // ‏**ولا يُسمّى ما نُطق**: تسميتُه تقتضي تحليل اسمٍ من الكلام.
        Assert.DoesNotContain("الفروع", company.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وحتى اسمُ الشركة المفتوحة نفسِه يُرفض</b> — وهو ثمنٌ مقصود ومُسمّى: قبولُه
    /// يقتضي مقارنةَ نصٍّ بنصّ، وهي بعينها العملية المحذوفة. والرفض يسمّي الشاشة.
    /// </summary>
    [Fact]
    public void دليل_شركة_ولو_كانت_المفتوحة_يُرفض_لأن_المقارنة_نفسها_محذوفة()
    {
        VoiceResolution resolution = Resolve(
            "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم في شركة سلاسل بابل");

        Assert.True(resolution.CompanyCueHeard);

        Result<VoiceDispatch> refused =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == "ai.voice.company_not_switched");
    }

    [Fact]
    public void النية_التي_تنتظر_قرار_المالك_تُفهَم_ولا_تُنفَّذ_ولو_اكتملت_وأُكِّدت()
    {
        VoiceResolution resolution = Resolve(
            "تسكين القطع الصنف اسمنت كمية خمسة أكياس المستودع الرئيسي من الرف واحد الى الرف اثنين");

        Assert.True(resolution.IsComplete);
        Assert.Equal(VoiceIntentStatus.AwaitingOwnerDecision, resolution.Intent.Status);

        Result<VoiceDispatch> refused =
            VoiceConfirmationGate.Authorise(resolution, VoiceHarness.Caller, resolution.ConfirmationToken);

        Assert.True(refused.IsFailure);
        Error only = Assert.Single(refused.Errors);
        Assert.Equal("ai.voice.owner_decision_pending", only.Code);
        Assert.Contains("القرار المطلوب من مالك المنتج", only.MessageAr, StringComparison.Ordinal);
    }

    [Fact]
    public void كلمة_التأكيد_المنطوقة_مغلقة_ولا_تُقارَب_بأقرب_شبيه()
    {
        Assert.True(VoiceConfirmationGate.IsSpokenConfirmation("تأكيد"));
        Assert.True(VoiceConfirmationGate.IsSpokenConfirmation("تمام اعتمد"));
        Assert.False(VoiceConfirmationGate.IsSpokenConfirmation("تقريباً"));
        Assert.False(VoiceConfirmationGate.IsSpokenConfirmation(""));

        Assert.True(VoiceConfirmationGate.IsSpokenCancellation("إلغاء"));
        Assert.False(VoiceConfirmationGate.IsSpokenCancellation("تأكيد"));
    }
}
