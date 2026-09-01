using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>سجلّ الخطط — يُقاس بما يرفضه، ويرفضه عند <u>البناء</u> لا عند النُّطق.</b>
/// <para>
/// وكلُّ إثباتٍ هنا <b>طفرة</b>: تُشوَّه الخطّة الحقيقية تشويهاً واحداً بعينه، ويُقاس
/// أن البناء يسقط <b>بالرمز المسمّى</b>. فحارسٌ لا يُثبَت أنه يُحمِّر حين يجب حارسٌ
/// يبدو حارساً — وهو أسوأ من غيابه، لأنه يشتري ثقةً بلا ثمن.
/// </para>
/// </summary>
public sealed class VoicePlanRegistryTests
{
    private static readonly VoicePlan Real = new SalesVoicePlansMirror().Plans[0];

    /// <summary>نسخةٌ من مجموعة المبيعات كي تُشوَّه بلا مساس بالمنتج.</summary>
    private sealed class SalesVoicePlansMirror : IVoicePlanCatalogue
    {
        public BabelModule Module => BabelModule.Sales;

        public IReadOnlyList<VoicePlan> Plans { get; } = VoiceHarness.PlanCatalogues[0].Plans;
    }

    private sealed class OnePlan(VoicePlan plan) : IVoicePlanCatalogue
    {
        public BabelModule Module => BabelModule.Sales;

        public IReadOnlyList<VoicePlan> Plans { get; } = [plan];
    }

    private static Result<VoicePlanRegistry> Build(VoicePlan plan) =>
        VoicePlanRegistry.Build([new OnePlan(plan)], VoiceHarness.Registry);

    private static void Rejects(VoicePlan plan, string code)
    {
        Result<VoicePlanRegistry> built = Build(plan);

        Assert.True(built.IsFailure, "الطفرة بُنيت ولم تُرفض — والحارس المُدّعى غيرُ موجود.");
        Assert.Contains(built.Errors, error => error.Code == code);
    }

    [Fact]
    public void الخطة_الحقيقية_تُبنى_ولا_تُشترى_الخُضرة_بخطة_مصطنعة()
    {
        // حارس لا فراغ: الطفرات كلّها تُقاس على هذه الخطّة، فإن لم تُبنَ فكلُّ ما تحتها
        // يقيس رفضاً كان سيقع على أي حال.
        Assert.True(Build(Real).IsSuccess);
        Assert.Equal(2, Real.Steps.Count);
        Assert.NotEmpty(Real.TriggerPhrases);
        Assert.NotEmpty(Real.ConditionPhrases);
    }

    /* ══ الطفرة الكبرى: هل تُغلق الخطّة بابَ الترحيل فعلاً؟ ══════════════════ */

    [Fact]
    public void خطوةٌ_تسمّي_نيّةً_ليست_في_السجل_تُسقط_البناء()
    {
        // ‏**وهذه هي الطفرة التي تُثبت أن الباب مغلق.** لا تستطيع خطوةٌ أن تسمّي
        // «postCustomerReceipt» — ليس في نوعها حقلٌ لعملية أصلاً. فأقصى ما يستطيعه
        // مهرِّبٌ هو أن يخترع **اسمَ نيّة**؛ وهذا يُسقط البناء باسمه.
        VoicePlan mutated = Real with
        {
            Steps =
            [
                Real.Steps[0],
                Real.Steps[1] with { IntentId = "accounting.customer_receipt.posting" },
            ],
        };

        Rejects(mutated, "ai.voice.catalogue.plan_step_unknown");
    }

    [Fact]
    public void خطةٌ_بمستندين_يُرحَّلان_تُسقط_البناء()
    {
        // ‏**عطلُ «عدّة نعم» بعينه**: من قال «نعم» مرّتين يقولها الثالثة بلا أن يقرأ.
        // وفاتورةُ المبيعات وسندُ القبض كلاهما يُرحَّل — فدفعةٌ مؤكَّدة بالصوت.
        VoicePlan mutated = Real with
        {
            Steps =
            [
                Real.Steps[0] with
                {
                    IntentId = "accounting.sales_invoice.draft",
                    Bindings =
                    [
                        new VoiceSlotBinding("customer", VoiceSlotSource.FromUtterance),
                        new VoiceSlotBinding("amount", VoiceSlotSource.FromUtterance),
                        new VoiceSlotBinding("issuedOn", VoiceSlotSource.FromUtterance),
                    ],
                },
                Real.Steps[1],
            ],
        };

        Rejects(mutated, "ai.voice.catalogue.plan_posts_more_than_once");
    }

    [Fact]
    public void خطوةٌ_تسمّي_نيّةً_تنتظر_قرار_المالك_تُسقط_البناء()
    {
        VoiceIntent awaiting = VoiceHarness.Registry.Intents
            .First(static intent => intent.Status == VoiceIntentStatus.AwaitingOwnerDecision
                                 && intent.Section == VoiceSection.Accounting);

        VoicePlan mutated = Real with
        {
            Steps = [Real.Steps[0] with { IntentId = awaiting.Id, Bindings = [] }, Real.Steps[1]],
        };

        Result<VoicePlanRegistry> built = Build(mutated);
        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.plan_step_awaiting_owner");
    }

    [Fact]
    public void خطوةٌ_تقرأ_بياناً_شخصياً_في_الوسط_تُسقط_البناء()
    {
        // آخرَ الخطّة جوابٌ يقف عنده الكلام؛ ووسطَها جوابٌ يُقرأ داخل ملخّصٍ أكبر.
        VoiceIntent personal = VoiceHarness.Registry.Intents
            .First(static intent => intent.ReadsPersonalData);

        VoicePlan mutated = Real with
        {
            Section = personal.Section,
            Steps =
            [
                Real.Steps[0] with { IntentId = personal.Id, Bindings = [] },
                Real.Steps[1],
            ],
        };

        Result<VoicePlanRegistry> built = Build(mutated);
        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.plan_personal_data_mid_plan");
    }

    /* ══ طفرات البيانات ════════════════════════════════════════════════════ */

    [Fact]
    public void ربطٌ_يسمّي_شريحةً_لا_تُعلنها_النية_يُسقط_البناء()
    {
        VoicePlan mutated = Real with
        {
            Steps =
            [
                Real.Steps[0] with
                {
                    Bindings = [new VoiceSlotBinding("creditLimit", VoiceSlotSource.AskedOfHuman)],
                },
                Real.Steps[1],
            ],
        };

        // ‏**وهذه هي القاعدة الصعبة مقيسةً**: «حدّ الائتمان» حقلُ شاشةٍ لا شريحة منطوقة،
        // ومحاولةُ سؤاله بالصوت تُسقط البناء بدل أن تصير نموذجاً ثانياً بلا حارس.
        Rejects(mutated, "ai.voice.catalogue.plan_binding_unknown_slot");
    }

    [Fact]
    public void شريحةٌ_لازمة_بلا_ربط_تُسقط_البناء()
    {
        VoicePlan mutated = Real with { Steps = [Real.Steps[0] with { Bindings = [] }, Real.Steps[1]] };

        Rejects(mutated, "ai.voice.catalogue.plan_required_slot_not_bound");
    }

    [Fact]
    public void خطوةٌ_في_قسمٍ_آخر_تُسقط_البناء()
    {
        VoicePlan mutated = Real with { Section = VoiceSection.Inventory };

        Rejects(mutated, "ai.voice.catalogue.plan_step_leaves_section");
    }

    [Fact]
    public void خطةٌ_بلا_شرطٍ_أو_بلا_طلبٍ_تُسقط_البناء()
    {
        Rejects(Real with { ConditionPhrases = [] }, "ai.voice.catalogue.plan_no_phrases");
        Rejects(Real with { TriggerPhrases = [] }, "ai.voice.catalogue.plan_no_phrases");
    }

    [Fact]
    public void خطةٌ_بلا_خطوة_تُسقط_البناء()
    {
        Rejects(Real with { Steps = [] }, "ai.voice.catalogue.plan_empty");
    }

    [Fact]
    public void معرّفٌ_مكرّر_ومعرّفٌ_مشوَّه_يُسقطان_البناء()
    {
        Result<VoicePlanRegistry> duplicated = VoicePlanRegistry.Build(
            [new OnePlan(Real), new OnePlan(Real)], VoiceHarness.Registry);

        Assert.True(duplicated.IsFailure);
        Assert.Contains(duplicated.Errors, error => error.Code == "ai.voice.catalogue.plan_duplicate_id");

        // حرفٌ كبير، ومقطعٌ واحد بلا نقطة، ورقمُ حسابٍ متسلّل (القاعدة 2) — ثلاثتها تُسقط.
        Rejects(Real with { Id = "Accounting.Plan" }, "ai.voice.catalogue.plan_malformed_id");
        Rejects(Real with { Id = "accounting" }, "ai.voice.catalogue.plan_malformed_id");
        Rejects(Real with { Id = "accounting.4100" }, "ai.voice.catalogue.plan_malformed_id");
    }

    [Fact]
    public void خطواتٌ_فوق_السقف_تُسقط_البناء()
    {
        VoicePlanStep filler = Real.Steps[0];

        VoicePlan mutated = Real with
        {
            Steps = [filler, filler, filler, filler, filler, Real.Steps[1]],
        };

        Result<VoicePlanRegistry> built = Build(mutated);
        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.plan_too_many_steps");
    }

    /* ══ ما يقوله السجلّ الحقيقي ════════════════════════════════════════════ */

    [Fact]
    public void كل_خطوةٍ_في_كل_خطة_مبنيّة_تسمّي_نيّةً_منشورة_لا_تبلغ_ترحيلاً()
    {
        int steps = 0;

        foreach (VoicePlan plan in VoiceHarness.Plans.Plans)
        {
            foreach (VoicePlanStep step in plan.Steps)
            {
                steps++;
                VoiceIntent? intent = VoiceHarness.Registry.Find(step.IntentId);

                Assert.NotNull(intent);
                Assert.Equal(VoiceIntentStatus.Published, intent.Status);

                // ‏**والعملية تُقرأ من النيّة لا من الخطوة** — وهي قد اجتازت الحارس عند
                // بناء السجلّ. فيُقاس هنا أنها ما زالت تجتازه.
                Assert.Null(VoiceOperationGuard.Refuse(intent.OperationId));
            }
        }

        Assert.True(steps >= 2, "خطوات الخطط المقيسة: " + steps.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void لا_خطة_مبنيّة_تحمل_أكثر_من_مستندٍ_واحد_يُرحَّل()
    {
        foreach (VoicePlan plan in VoiceHarness.Plans.Plans)
        {
            int posting = plan.Steps.Count(step =>
                VoiceHarness.Registry.Find(step.IntentId)?.LedgerEffect == VoiceLedgerEffect.Posts);

            Assert.True(posting <= VoicePlanGuard.PostingStepLimit, plan.Id);
        }
    }
}
