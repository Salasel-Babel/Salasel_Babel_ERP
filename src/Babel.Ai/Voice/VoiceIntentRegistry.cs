using System.Text.RegularExpressions;
using Babel.Ai.Suggestions;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>سجلّ النيّات — تُسهم به الوحدات، ولا يعرف هذا المشروع واحدةً منها بالاسم.</b>
/// <para>
/// كان المسار المنطوق يخدم تدفّقاً واحداً — التقاط فاتورة مورد — وكانت قواعده مكتوبة
/// <b>داخل</b> وحدة الذكاء. وإضافةُ نيّةٍ للمخزون أو للعقارات كانت تعني تعديل هذا
/// المشروع، أي أن كل قسمٍ جديد يمرّ من عنق زجاجةٍ واحد. وهو — وهذا الأهمّ — <b>ممنوع
/// بنيوياً</b>: الوحدات الأفقية لا يعرف بعضها بعضاً (القاعدة 3)، ووحدةُ الذكاء واحدة
/// منها؛ فلا تستطيع أن ترجع إلى المخزون لتعرف وحداته ولا إلى الموارد البشرية لتعرف
/// موظفيها.
/// </para>
/// <para>
/// <b>فانقلب الاتجاه:</b> النيّة نوعٌ في <c>Babel.Contracts</c>، والوحدة تُعلن نيّاتها
/// وتسجّلها عند تسجيل نفسها، وهذا السجلّ يجمع ما وجده في الحاوية. إضافةُ نيّةٍ اليوم
/// <b>لا تمسّ هذا المشروع بسطر</b>.
/// </para>
/// <para>
/// <b>وما يفعله السجلّ عند البناء يُقاس بما يرفضه:</b> معرّفاً مكرّراً، ورمزَ حدثٍ ليس
/// في مصفوفة الترحيل، وترحيلاً بلا رمز حدث، ورمزاً يحمل مقطعاً رقمياً (أي رقم حساب
/// متسلّلاً — القاعدة 2)، ونيّةً «تنتظر قراراً» بلا اسمِ القرار. وكلّها تُسقط <b>البناء</b>
/// لا النُّطق: سجلٌّ نصفُه صالح يعمل تسعاً وتسعين مرّة ثم يُرحّل مرّةً إلى حدثٍ لا وجود له.
/// </para>
/// </summary>
public sealed partial class VoiceIntentRegistry
{
    private readonly Dictionary<string, VoiceIntent> _byId;

    private VoiceIntentRegistry(Dictionary<string, VoiceIntent> byId)
    {
        _byId = byId;
        Intents = [.. byId.Values.OrderBy(static intent => intent.Id, StringComparer.Ordinal)];
    }

    /// <summary>كل النيّات مرتَّبةً بمعرّفاتها.</summary>
    public IReadOnlyList<VoiceIntent> Intents { get; }

    /// <summary>عدد النيّات — يقرؤه حارس اللافراغ.</summary>
    public int Count => Intents.Count;

    /// <summary>شكل معرّف النيّة: مقاطع لاتينية صغيرة تفصلها نقاط، ومقطعان على الأقل.</summary>
    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdShape();

    /// <summary>
    /// يبني السجلّ من مجموعات الوحدات، ويتحقّق من كلٍّ منها بالمفردات المغلقة.
    /// </summary>
    /// <param name="catalogues">ما وجده الجذر التركيبي في الحاوية.</param>
    /// <param name="vocabulary">مفردات مصفوفة الترحيل — رموز الأحداث والأدوار وحدها.</param>
    public static Result<VoiceIntentRegistry> Build(
        IEnumerable<IVoiceIntentCatalogue> catalogues,
        IPostingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(catalogues);
        ArgumentNullException.ThrowIfNull(vocabulary);

        Dictionary<string, VoiceIntent> byId = new(StringComparer.Ordinal);
        List<Error> errors = [];

        foreach (IVoiceIntentCatalogue catalogue in catalogues)
        {
            foreach (VoiceIntent intent in catalogue.Intents)
            {
                if (!byId.TryAdd(intent.Id, intent))
                {
                    errors.Add(VoiceCatalogueErrors.DuplicateIntentId(intent.Id));
                    continue;
                }

                Check(errors, intent, vocabulary);
            }
        }

        if (byId.Count == 0)
        {
            errors.Add(VoiceCatalogueErrors.CatalogueEmpty);
        }

        return errors.Count == 0
            ? Result<VoiceIntentRegistry>.Success(new VoiceIntentRegistry(byId))
            : Result<VoiceIntentRegistry>.Failure(errors);
    }

    /// <summary>نيّةٌ بمعرّفها، أو لا شيء.</summary>
    /// <param name="intentId">المعرّف.</param>
    public VoiceIntent? Find(string intentId) =>
        intentId is not null && _byId.TryGetValue(intentId, out VoiceIntent? intent) ? intent : null;

    /// <summary>نيّات قسمٍ بعينه، مرتَّبة.</summary>
    /// <param name="section">القسم.</param>
    public IReadOnlyList<VoiceIntent> InSection(VoiceSection section) =>
        [.. Intents.Where(intent => intent.Section == section)];

    private static void Check(List<Error> errors, VoiceIntent intent, IPostingVocabulary vocabulary)
    {
        if (!IdShape().IsMatch(intent.Id) || SuggestionGuard.CarriesNumericSegment(intent.Id))
        {
            errors.Add(VoiceCatalogueErrors.MalformedIntentId(intent.Id));
        }

        if (intent.Phrases.Count == 0)
        {
            errors.Add(VoiceCatalogueErrors.NoPhrases(intent.Id));
        }

        if (intent.LedgerEffect == VoiceLedgerEffect.Posts)
        {
            if (string.IsNullOrWhiteSpace(intent.EventCode))
            {
                errors.Add(VoiceCatalogueErrors.EventCodeMissing(intent.Id));
            }
            else if (SuggestionGuard.CarriesNumericSegment(intent.EventCode))
            {
                errors.Add(VoiceCatalogueErrors.NamesALedgerCode(intent.Id, intent.EventCode));
            }
            else if (!vocabulary.KnowsEvent(intent.EventCode))
            {
                errors.Add(VoiceCatalogueErrors.EventCodeUnknown(intent.Id, intent.EventCode));
            }
        }
        else if (!string.IsNullOrWhiteSpace(intent.EventCode))
        {
            errors.Add(VoiceCatalogueErrors.EventCodeNotExpected(intent.Id, intent.EventCode));
        }

        // ‏**والقاعدة 2 تُفحص على أسماء الشرائح أيضاً**: شريحةٌ اسمها `account_code` تجعل
        // الكلام يملي حساباً، وهو الباب نفسه الذي أغلقه حارس الاقتراح على مُخرَج النموذج.
        foreach (VoiceSlot slot in intent.Slots)
        {
            if (SuggestionGuard.LedgerCodeFieldNames.Contains(slot.Name))
            {
                errors.Add(VoiceCatalogueErrors.NamesALedgerCode(intent.Id, slot.Name));
            }
        }

        bool awaiting = intent.Status == VoiceIntentStatus.AwaitingOwnerDecision;
        bool stated = !string.IsNullOrWhiteSpace(intent.OwnerDecisionAr);

        if (awaiting && !stated)
        {
            errors.Add(VoiceCatalogueErrors.OwnerDecisionNotStated(intent.Id));
        }
        else if (!awaiting && stated)
        {
            errors.Add(VoiceCatalogueErrors.OwnerDecisionNotExpected(intent.Id));
        }
    }
}
