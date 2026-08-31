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

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// يستخرج مصفوفة النيّات من ملفّ TypeScript. <b>والاستخراج نصّي بقصد</b>: بناءُ
    /// Node لقراءة الملفّ يجعل الحارس يعتمد على تثبيت حزم، فيُتخطّى في البوّابة التي
    /// لا تُثبّت — وهي البوّابة التي تُشغَّل أكثر.
    /// </summary>
    private static IReadOnlyList<VectorIntent> Mirror()
    {
        string source = File.ReadAllText(RepositoryRoot.At(CataloguePath));

        int start = source.IndexOf(Anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, "المرساة «" + Anchor + "» غير موجودة في " + CataloguePath);

        // ‏**من بعد المرساة لا من أوّلها**: المرساة نفسها تحمل «VoiceIntent[]».
        int open = source.IndexOf('[', start + Anchor.Length);
        int close = source.LastIndexOf("];", StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open, "مصفوفة النيّات غير مغلقة في " + CataloguePath);

        string json = source[open..(close + 1)];

        return JsonSerializer.Deserialize<IReadOnlyList<VectorIntent>>(json, Options)
            ?? throw new InvalidOperationException("مصفوفة النيّات في المتصفّح لا تُقرأ.");
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
    public void المتصفح_والخادم_يقرآن_ملف_المتجهات_نفسه()
    {
        // ‏ملفٌّ واحد لا ملفّان: هذا هو ما يجعل «التنفيذان متطابقان» جملةً تُقاس
        // لا جملةً تُقال (ADR-0030 خامساً).
        string browserTest = File.ReadAllText(RepositoryRoot.At("web/tests/voice-command.test.ts"));

        Assert.Contains("tests/Babel.Ai.Tests/golden/voice-intents.v1.json", browserTest, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryRoot.At(VoiceVectors.RelativePath)));
    }
}
