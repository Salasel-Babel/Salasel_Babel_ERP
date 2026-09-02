using Babel.Ai.Suggestions;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Tests.Support;

/// <summary>
/// <b>السجلّ كما يبنيه الجذر التركيبي بالضبط</b> — من مجموعات الوحدات الست، وبمفردات
/// المصفوفة المضمَّنة نفسها.
/// <para>
/// <b>ولا يُبنى هنا سجلٌّ مُصطنَع للإثبات:</b> إثباتٌ على نيّاتٍ كُتبت في ملفّ الاختبار
/// يُثبت أن المحرّك يعمل على ما يعرفه، لا أن المنتج يعمل. والنيّات التي تُقرأ هنا هي
/// نفسها التي تصل المستخدم.
/// </para>
/// </summary>
internal static class VoiceHarness
{
    /// <summary>
    /// مجموعات الوحدات <b>السبع</b> كما تُسجّلها كلٌّ منها. والسابعة هي الدفتر:
    /// يُسهم بنيّةٍ واحدة هي <b>امتناع</b> — قيدُ يوميةٍ يُملى ولا يُنفَّذ، لأن السطح
    /// المنشور لا يحمل له بابَ مسوّدة.
    /// </summary>
    public static IReadOnlyList<IVoiceIntentCatalogue> Catalogues { get; } =
    [
        new Ledger.Voice.LedgerVoiceIntents(),
        new Purchasing.Voice.PurchasingVoiceIntents(),
        new Sales.Voice.SalesVoiceIntents(),
        new Projects.Voice.ProjectsVoiceIntents(),
        new Hr.Voice.HrVoiceIntents(),
        new Inventory.Voice.InventoryVoiceIntents(),
        new RealEstate.Voice.RealEstateVoiceIntents(),
    ];

    /// <summary>السجلّ المبنيّ — يسقط الإثبات إن رفض البناء، ويسمّي السبب.</summary>
    public static VoiceIntentRegistry Registry { get; } = BuildOrThrow();

    /// <summary>تاريخٌ مُحقَن كي تكون القراءة حتمية. لا ساعةَ جهاز في أي إثبات.</summary>
    public const string Today = "2026-08-31";

    /// <summary>خيارات القراءة المُحقونة.</summary>
    public static VoiceReadingOptions Options { get; } = new(Today, "0.15");

    /// <summary>متكلّمٌ يملك كل النيّات — تُضيَّق الصلاحية في الإثبات الذي يقيسها وحده.</summary>
    public static VoiceCaller Caller { get; } = new(
        Guid.Parse("0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70"),
        new HashSet<string>(Registry.Intents.Select(static intent => intent.Id), StringComparer.Ordinal));

    /// <summary>الجلسة التي تُسأل فيها السجلّات — منشأتها وشركتها ثابتتان في الإثباتات.</summary>
    public static Ai.Lookup.LookupSession Session { get; } = new(
        new TenantId(Guid.Parse("7e1c0a5e-0000-4000-8000-000000000001")),
        Guid.Parse("0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70"),
        Guid.Parse("3f2b1c9d-0000-4000-8000-000000000001"));

    /// <summary>
    /// البحث في السجلّات كما أعلنها ملفّ المتجهات — <b>مِفصلٌ لا مُطابِق</b>
    /// (انظر <see cref="ScriptedNameRegister"/>).
    /// </summary>
    public static Ai.Lookup.NameRegisterLookup Lookup { get; } = BuildLookup();

    /// <summary>
    /// <b>يقرأ ثم يحلّ</b> — وهو المسار الكامل كما يقع في المنتج: القارئ يحمل المقطع،
    /// والسجلّ يقرّر، والبوّابة تقرأ ما قرّره.
    /// </summary>
    /// <param name="transcript">التفريغ.</param>
    public static VoiceResolution ReadAndResolve(string transcript)
    {
        Result<VoiceResolution> read = SpokenCommandReader.Read(transcript, Registry, Options);

        if (read.IsFailure)
        {
            throw new InvalidOperationException(
                "لم تُقرأ الجملة «" + transcript + "»: "
                + string.Join(" · ", read.Errors.Select(static error => error.MessageAr)));
        }

        Result<VoiceResolution> resolved = SpokenNameResolver
            .ResolveAsync(read.Value, Lookup, Session)
            .GetAwaiter()
            .GetResult();

        return resolved.IsSuccess
            ? resolved.Value
            : throw new InvalidOperationException(
                "لم تُحلّ أسماء «" + transcript + "»: "
                + string.Join(" · ", resolved.Errors.Select(static error => error.MessageAr)));
    }

    private static Ai.Lookup.NameRegisterLookup BuildLookup()
    {
        List<Contracts.Lookup.INameCandidateSource> sources = [];

        foreach ((string key, IReadOnlyList<string> spans) in VoiceVectors.File.Registers)
        {
            sources.Add(new ScriptedNameRegister(key, spans));
        }

        return new Ai.Lookup.NameRegisterLookup(
            sources,
            new Ai.Lookup.SignedLookupHandles(
                System.Text.Encoding.UTF8.GetBytes("مفتاح إثباتٍ طوله أكثر من اثنتين وثلاثين بايتاً بلا شكّ"),
                new Ai.Lookup.LookupOptions(),
                TimeProvider.System),
            new Ai.Lookup.LookupOptions());
    }

    private static VoiceIntentRegistry BuildOrThrow()
    {
        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build(Catalogues, MatrixPostingVocabulary.Default);

        return built.IsSuccess
            ? built.Value
            : throw new InvalidOperationException(
                "سجلّ النيّات لم يُبنَ: " + string.Join(" · ", built.Errors.Select(static error => error.MessageAr)));
    }
}
