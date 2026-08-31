using Babel.Ai.Suggestions;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>مجموعةُ إثباتٍ تحمل نيّةً واحدة — تُحقن في السجلّ لقياس ما يرفضه.</summary>
/// <param name="Module">الوحدة.</param>
/// <param name="Intents">النيّات.</param>
internal sealed record ProbeCatalogue(BabelModule Module, IReadOnlyList<VoiceIntent> Intents) : IVoiceIntentCatalogue;

/// <summary>
/// <b>السجلّ — ما يقبله وما يُسقط البناء لأجله.</b>
/// <para>
/// وكلّ إثباتٍ هنا يقيس <b>ما يرفضه</b> لا ما يمرّره: سجلٌّ يقبل كل شيء ليس سجلّاً بل
/// قائمة. والقبولُ وحده يُثبَت مرّةً على النيّات الحقيقية العشرين، لا على نيّةٍ
/// مُصطنَعة تُكتب لتمرّ.
/// </para>
/// </summary>
public sealed class VoiceIntentRegistryTests
{
    private static VoiceIntent Probe(
        string id,
        VoiceIntentKind kind = VoiceIntentKind.Query,
        VoiceLedgerEffect effect = VoiceLedgerEffect.None,
        string? eventCode = null,
        VoiceIntentStatus status = VoiceIntentStatus.Published,
        string? ownerDecision = null,
        IReadOnlyList<VoiceSlot>? slots = null,
        IReadOnlyList<string>? phrases = null)
        => new(
            id,
            VoiceSection.Accounting,
            BabelModule.Sales,
            kind,
            status,
            effect,
            eventCode,
            "نيّة إثبات",
            "Probe intent",
            phrases ?? ["عبارة إثبات"],
            slots ?? [],
            false,
            ownerDecision,
            ownerDecision is null ? null : "probe");

    private static Result<VoiceIntentRegistry> BuildWith(params VoiceIntent[] intents) =>
        VoiceIntentRegistry.Build(
            [new ProbeCatalogue(BabelModule.Sales, intents)],
            MatrixPostingVocabulary.Default);

    // ── ما يقبله: النيّات الحقيقية وحدها ──────────────────────────────────

    [Fact]
    public void السجل_يُبنى_من_مجموعات_الوحدات_الست_ويحمل_الأقسام_الخمسة()
    {
        VoiceIntentRegistry registry = VoiceHarness.Registry;

        Assert.Equal(6, VoiceHarness.Catalogues.Count);
        Assert.True(registry.Count >= 18, "عدد النيّات " + registry.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (VoiceSection section in Enum.GetValues<VoiceSection>())
        {
            IReadOnlyList<VoiceIntent> inSection = registry.InSection(section);
            Assert.True(
                inSection.Count >= 3,
                "القسم " + section + " فيه " + inSection.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " نيّة — والخطة تعد بثلاثٍ إلى ستّ لكل قسم.");
            Assert.True(inSection.Count <= 6, "القسم " + section + " تجاوز الستّ.");
        }
    }

    [Fact]
    public void كل_رمز_حدث_في_السجل_موجود_في_مصفوفة_الترحيل()
    {
        int posting = 0;

        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            if (intent.LedgerEffect != VoiceLedgerEffect.Posts)
            {
                Assert.Null(intent.EventCode);
                continue;
            }

            posting++;
            Assert.NotNull(intent.EventCode);
            Assert.True(
                MatrixPostingVocabulary.Default.KnowsEvent(intent.EventCode!),
                "رمز حدث خارج المصفوفة: " + intent.EventCode);
        }

        // حارس لا فراغ: سجلٌّ بلا نيّةٍ تُرحّل يجعل الفحص أعلاه يمرّ على لا شيء.
        Assert.True(posting >= 9, "النيّات المُرحِّلة " + posting.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void كل_نية_تغير_الحال_تحتاج_تأكيداً_ولا_استثناء()
    {
        int changing = 0;

        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            if (intent.Kind == VoiceIntentKind.StateChange)
            {
                changing++;
                Assert.True(intent.RequiresConfirmation, intent.Id + " تُغيّر الحال ولا تطلب تأكيداً.");
            }
            else
            {
                Assert.False(intent.RequiresConfirmation, intent.Id + " استعلامٌ ويطلب تأكيداً.");
            }
        }

        Assert.True(changing >= 12, "النيّات المُغيِّرة " + changing.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void لا_نية_تسمي_حساباً_لا_في_معرفها_ولا_في_رمز_حدثها_ولا_في_اسم_شريحة()
    {
        foreach (VoiceIntent intent in VoiceHarness.Registry.Intents)
        {
            Assert.False(SuggestionGuard.CarriesNumericSegment(intent.Id), intent.Id);

            if (intent.EventCode is not null)
            {
                Assert.False(SuggestionGuard.CarriesNumericSegment(intent.EventCode), intent.EventCode);
            }

            foreach (VoiceSlot slot in intent.Slots)
            {
                Assert.DoesNotContain(slot.Name, SuggestionGuard.LedgerCodeFieldNames, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    // ── ما يرفضه ───────────────────────────────────────────────────────────

    [Fact]
    public void معرف_مكرر_بين_وحدتين_يُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build(
            [
                new ProbeCatalogue(BabelModule.Sales, [Probe("probe.one")]),
                new ProbeCatalogue(BabelModule.Purchasing, [Probe("probe.one")]),
            ],
            MatrixPostingVocabulary.Default);

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.duplicate_intent");
    }

    [Fact]
    public void رمز_حدث_ليس_في_المصفوفة_يُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = BuildWith(
            Probe("probe.invented", VoiceIntentKind.StateChange, VoiceLedgerEffect.Posts, "sales.invented.posted"));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.event_code_unknown");
    }

    [Fact]
    public void ترحيل_بلا_رمز_حدث_يُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = BuildWith(
            Probe("probe.silent", VoiceIntentKind.StateChange, VoiceLedgerEffect.Posts));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.event_code_missing");
    }

    [Fact]
    public void رمز_حدث_على_مسار_لا_يُرحل_يُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = BuildWith(
            Probe("probe.pretend", VoiceIntentKind.StateChange, VoiceLedgerEffect.None, "sales.receipt.posted"));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.event_code_not_expected");
    }

    [Fact]
    public void رمز_حدث_بمقطع_رقمي_يُسقط_البناء_لأنه_رقم_حساب_متسلل()
    {
        Result<VoiceIntentRegistry> built = BuildWith(
            Probe("probe.leak", VoiceIntentKind.StateChange, VoiceLedgerEffect.Posts, "purchasing.1210"));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.names_a_ledger_code");
    }

    [Fact]
    public void شريحة_اسمها_يسمي_حساباً_تُسقط_البناء()
    {
        VoiceSlot slot = new("account_code", VoiceSlotKind.Text, "الحساب", "The code", true, ["حساب"], []);
        Result<VoiceIntentRegistry> built = BuildWith(Probe("probe.slot", slots: [slot]));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.names_a_ledger_code");
    }

    [Fact]
    public void نية_تنتظر_قراراً_بلا_اسم_القرار_تُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = BuildWith(
            Probe("probe.waiting", status: VoiceIntentStatus.AwaitingOwnerDecision));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.owner_decision_not_stated");
    }

    [Fact]
    public void نية_بلا_عبارة_إطلاق_تُسقط_البناء()
    {
        Result<VoiceIntentRegistry> built = BuildWith(Probe("probe.mute", phrases: []));

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.no_phrases");
    }

    [Fact]
    public void سجل_فارغ_يُسقط_البناء_ولا_يمر_صامتاً()
    {
        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build([], MatrixPostingVocabulary.Default);

        Assert.True(built.IsFailure);
        Assert.Contains(built.Errors, error => error.Code == "ai.voice.catalogue.empty");
    }

    [Fact]
    public void مشروع_الذكاء_لا_يعرف_وحدةً_واحدة_بالاسم()
    {
        // الحدّ نفسه الذي يجعل السجلّ ممكناً: النيّات تصل من الحاوية، ولا يظهر اسم
        // وحدةٍ في تجميعة الذكاء. ولو ظهر لسقطت القاعدة 3 قبل هذا الإثبات — وهذا
        // الإثبات يقول **أين** تسقط، فلا يُبحث عنها بين مئتَي نوع.
        string[] modules =
        [
            "Babel.Sales", "Babel.Purchasing", "Babel.Inventory",
            "Babel.Hr", "Babel.Projects", "Babel.RealEstate", "Babel.Ledger",
        ];

        string[] referenced =
        [
            .. typeof(AiModuleInfo).Assembly
                .GetReferencedAssemblies()
                .Select(static reference => reference.Name ?? string.Empty),
        ];

        foreach (string module in modules)
        {
            Assert.DoesNotContain(module, referenced, StringComparer.Ordinal);
        }
    }
}
