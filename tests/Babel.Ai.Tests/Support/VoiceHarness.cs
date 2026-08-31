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
    /// <summary>مجموعات الوحدات الست كما تُسجّلها كلٌّ منها.</summary>
    public static IReadOnlyList<IVoiceIntentCatalogue> Catalogues { get; } =
    [
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
        "سلاسل بابل",
        new HashSet<string>(Registry.Intents.Select(static intent => intent.Id), StringComparer.Ordinal));

    private static VoiceIntentRegistry BuildOrThrow()
    {
        Result<VoiceIntentRegistry> built = VoiceIntentRegistry.Build(Catalogues, MatrixPostingVocabulary.Default);

        return built.IsSuccess
            ? built.Value
            : throw new InvalidOperationException(
                "سجلّ النيّات لم يُبنَ: " + string.Join(" · ", built.Errors.Select(static error => error.MessageAr)));
    }
}
