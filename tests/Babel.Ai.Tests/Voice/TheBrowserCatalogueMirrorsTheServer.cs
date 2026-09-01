using System.Text.Json;
using Babel.Ai.Suggestions;
using Babel.Ai.Tests.Support;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>مرآة المتصفّح — تُقاس، لا يُوثَق بها.</b>
/// <para>
/// المسار المنطوق يعمل <b>بلا شبكة</b>: القارئ في المتصفّح يفهم الأمر ويملأ الشاشة
/// والمستخدم يتكلّم، والخادم يُنفّذ ما يُؤكَّد. وثمنُ ذلك سجلٌّ في مكانين — ومكانان
/// ينحرفان.
/// </para>
/// <para>
/// <b>وهذا الحارس هو ما يجعل الانحراف يُحمِّر بوّابةً لا شاشة.</b> يقرأ ملفّ
/// <c>web/src/voice/catalogue.ts</c> نفسه — لا وصفاً له — ويطابقه بالسجلّ الذي بنته
/// الوحدات الست. ونيّةٌ تُضاف في وحدةٍ ولا تصل الواجهة تسقط هنا، لا في يد مستخدم
/// يقول أمراً «موجوداً في النظام» ويسمع «لم أفهم».
/// </para>
/// <para>
/// وهو امتدادٌ لحارسٍ قائم على المبدأ نفسه: <c>ProviderBoundaryTests</c> يقرأ
/// <c>web/src/voice/intent.ts</c> ليطابق رموز أحداثه بالمصفوفة — <b>لأن رمزاً
/// مخترَعاً في ملفّ TypeScript لا يراه أي حارس آخر</b> (‏ADR-0016).
/// </para>
/// </summary>
public sealed class TheBrowserCatalogueMirrorsTheServer
{
    private const string CataloguePath = "web/src/voice/catalogue.ts";
    private const string Anchor = "export const VOICE_INTENTS: readonly VoiceIntent[] = ";
    private const string PlanAnchor = "export const VOICE_PLANS: readonly VoicePlan[] = ";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// يستخرج مصفوفة النيّات من ملفّ TypeScript. <b>والاستخراج نصّي بقصد</b>: بناءُ
    /// Node لقراءة الملفّ يجعل الحارس يعتمد على تثبيت حزم، فيُتخطّى في البوّابة التي
    /// لا تُثبّت — وهي البوّابة التي تُشغَّل أكثر.
    /// </summary>
    private static IReadOnlyList<VectorIntent> Mirror() =>
        Extract<VectorIntent>(Anchor, "النيّات");

    /// <summary>
    /// <b>ومرآةُ الخطط تُقاس بالمرساة نفسها</b>: الخطط نسخةٌ ثانية من بيانات، ونسخةٌ
    /// ثانية بلا حارس تنحرف — وهو بعينه العطل الذي وُجد هذا الملفّ ليمنعه.
    /// </summary>
    private static IReadOnlyList<VectorPlan> PlanMirror() =>
        Extract<VectorPlan>(PlanAnchor, "الخطط");

    /// <summary>
    /// يستخرج مصفوفةً من ملفّ TypeScript. <b>والاستخراج نصّي بقصد</b>: بناءُ Node
    /// لقراءة الملفّ يجعل الحارس يعتمد على تثبيت حزم، فيُتخطّى في البوّابة التي لا
    /// تُثبّت — وهي البوّابة التي تُشغَّل أكثر.
    /// </summary>
    private static IReadOnlyList<T> Extract<T>(string anchor, string whatAr)
    {
        string source = File.ReadAllText(RepositoryRoot.At(CataloguePath));

        int start = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, "المرساة «" + anchor + "» غير موجودة في " + CataloguePath);

        // ‏**من بعد المرساة لا من أوّلها**: المرساة نفسها تحمل «[]».
        int open = source.IndexOf('[', start + anchor.Length);
        int close = source.IndexOf("\n  ];", open, StringComparison.Ordinal) + 4;
        Assert.True(open >= 0 && close > open, "مصفوفة " + whatAr + " غير مغلقة في " + CataloguePath);

        return JsonSerializer.Deserialize<IReadOnlyList<T>>(source[open..close], Options)
            ?? throw new InvalidOperationException("مصفوفة " + whatAr + " في المتصفّح لا تُقرأ.");
    }

    [Fact]
    public void مرآة_المتصفح_ليست_ضامرة()
    {
        // حارس لا فراغ: استخراجٌ توقّف عن المطابقة يجعل كل ما تحته يمرّ على مصفوفة فارغة.
        Assert.True(Mirror().Count >= 40, "النيّات المُلتقَطة من الواجهة: " + Mirror().Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void كل_نية_في_الخادم_لها_نظير_مطابق_في_المتصفح()
    {
        IReadOnlyList<VectorIntent> mirror = Mirror();

        Assert.Equal(
            VoiceHarness.Registry.Intents.Select(static intent => intent.Id),
            mirror.Select(static intent => intent.Id));

        foreach (VectorIntent mirrored in mirror)
        {
            Contracts.Voice.VoiceIntent? intent = VoiceHarness.Registry.Find(mirrored.Id);
            Assert.NotNull(intent);

            Assert.Equal(intent.Section.ToString(), mirrored.Section);
            Assert.Equal(intent.Kind.ToString(), mirrored.Kind);
            Assert.Equal(intent.Status.ToString(), mirrored.Status);
            Assert.Equal(intent.LedgerEffect.ToString(), mirrored.LedgerEffect);
            Assert.Equal(intent.EventCode, mirrored.EventCode);

            // ‏**والعملية المنشورة تُطابَق أيضاً**: المتصفّح هو الذي ينادي الباب، فمرآةٌ
            // تحمل عمليةً غير التي أعلنتها الوحدة تُنشئ مستنداً في مكانٍ آخر.
            Assert.Equal(intent.OperationId, mirrored.OperationId);
            Assert.Equal(intent.RequiresConfirmation, mirrored.RequiresConfirmation);
            Assert.Equal(intent.ReadsPersonalData, mirrored.ReadsPersonalData);
            Assert.Equal(intent.NameAr, mirrored.NameAr);
            Assert.Equal(intent.Phrases, mirrored.Phrases);
            Assert.Equal(
                intent.Slots.Select(static slot => slot.Name),
                mirrored.Slots.Select(static slot => slot.Name));
        }
    }

    [Fact]
    public void كل_رمز_حدث_تنطق_به_المرآة_موجود_في_مصفوفة_الترحيل()
    {
        int posting = 0;

        foreach (VectorIntent mirrored in Mirror())
        {
            if (mirrored.EventCode is null)
            {
                continue;
            }

            posting++;
            Assert.False(SuggestionGuard.CarriesNumericSegment(mirrored.EventCode), mirrored.EventCode);
            Assert.True(
                MatrixPostingVocabulary.Default.KnowsEvent(mirrored.EventCode),
                "رمز حدث في " + CataloguePath + " ليس في مصفوفة الترحيل: " + mirrored.EventCode);
        }

        Assert.True(posting >= 18, "رموز الأحداث المُلتقَطة: " + posting.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void لا_عملية_ترحيل_واحدة_في_مرآة_المتصفح()
    {
        // ‏**والمرآة تُفحَص بالقاعدة نفسها لا بالثقة**: المتصفّح هو ما ينادي الباب فعلاً،
        // فسطرٌ يُحرَّر هنا بيدٍ ويكتب «postSalesInvoice» يجعل جملةً منطوقة تُرحّل —
        // ولا يراه أيُّ حارسٍ في الخادم إلا هذا.
        int checked_ = 0;

        foreach (VectorIntent mirrored in Mirror())
        {
            if (mirrored.OperationId is null)
            {
                continue;
            }

            checked_++;
            Assert.Null(Babel.Ai.Voice.VoiceOperationGuard.Refuse(mirrored.OperationId));
        }

        Assert.True(checked_ >= 40, "العمليات المُلتقَطة من الواجهة: " + checked_.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void مرآة_الخطط_ليست_ضامرة()
    {
        // حارس لا فراغ: استخراجٌ توقّف عن المطابقة يجعل كل ما تحته يمرّ على مصفوفة فارغة.
        Assert.NotEmpty(PlanMirror());
        Assert.Equal(VoiceHarness.Plans.Count, PlanMirror().Count);
    }

    [Fact]
    public void كل_خطة_في_الخادم_لها_نظير_مطابق_في_المتصفح()
    {
        IReadOnlyList<VectorPlan> mirror = PlanMirror();

        Assert.Equal(
            VoiceHarness.Plans.Plans.Select(static plan => plan.Id),
            mirror.Select(static plan => plan.Id));

        foreach (VectorPlan mirrored in mirror)
        {
            Contracts.Voice.VoicePlan? plan = VoiceHarness.Plans.Find(mirrored.Id);
            Assert.NotNull(plan);

            Assert.Equal(plan.Section.ToString(), mirrored.Section);
            Assert.Equal(plan.Module.ToString(), mirrored.Module);
            Assert.Equal(plan.NameAr, mirrored.NameAr);
            Assert.Equal(plan.TriggerPhrases, mirrored.TriggerPhrases);
            Assert.Equal(plan.ConditionPhrases, mirrored.ConditionPhrases);

            Assert.Equal(
                plan.Steps.Select(static step => step.StepId),
                mirrored.Steps.Select(static step => step.StepId));

            for (int index = 0; index < plan.Steps.Count; index++)
            {
                Contracts.Voice.VoicePlanStep step = plan.Steps[index];
                VectorPlanStep mirroredStep = mirrored.Steps[index];

                // ‏**والنيّة تُطابَق حرفاً**: مرآةٌ تحمل نيّةً غير التي أعلنتها الوحدة
                // تُنشئ مستنداً آخر — وهي الخانة الوحيدة التي تقرّر ما تبلغه الخطوة.
                Assert.Equal(step.IntentId, mirroredStep.IntentId);
                Assert.Equal(step.Condition.ToString(), mirroredStep.Condition);
                Assert.Equal(step.PurposeAr, mirroredStep.PurposeAr);
                Assert.Equal(step.ScreenAsksForAr, mirroredStep.ScreenAsksForAr);
                Assert.Equal(
                    step.Bindings.Select(static binding => binding.SlotName + "=" + binding.Source),
                    mirroredStep.Bindings.Select(static binding => binding.SlotName + "=" + binding.Source));
            }
        }
    }

    [Fact]
    public void لا_اسم_عملية_واحد_في_بيانات_الخطط_في_المتصفح()
    {
        // ‏**الخطوة تسمّي نيّةً ولا تسمّي باباً** — ولا خانةَ في بياناتها لاسم عملية
        // أصلاً. فيُقاس غيابُ الخانة نفسها لا حسنُ النيّة في ملئها.
        string source = File.ReadAllText(RepositoryRoot.At(CataloguePath));
        int start = source.IndexOf(PlanAnchor, StringComparison.Ordinal);
        int close = source.IndexOf("\n  ];", start, StringComparison.Ordinal);
        string plans = source[start..close];

        Assert.DoesNotContain("operationId", plans, StringComparison.Ordinal);
        Assert.DoesNotContain("eventCode", plans, StringComparison.Ordinal);
    }

    [Fact]
    public void المتصفح_والخادم_يقرآن_ملف_المتجهات_نفسه()
    {
        // ‏ملفٌّ واحد لا ملفّان: هذا هو ما يجعل «التنفيذان متطابقان» جملةً تُقاس
        // لا جملةً تُقال (ADR-0030 خامساً).
        string browserTest = File.ReadAllText(RepositoryRoot.At("web/tests/voice-command.test.ts"));

        Assert.Contains("tests/Babel.Ai.Tests/golden/voice-intents.v1.json", browserTest, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryRoot.At(VoiceVectors.RelativePath)));
    }
}
