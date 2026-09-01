using System.Text.RegularExpressions;
using Babel.Ai.Suggestions;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>سجلّ الخطط — تُسهم به الوحدات، ويُبنى بعد سجلّ النيّات لا معه.</b>
/// <para>
/// <b>ولماذا سجلٌّ ثانٍ لا فحصٌ داخل <see cref="VoiceIntentRegistry.Build"/>:</b> الخطّة
/// تُفحص <b>بالنيّات</b> — خطوتُها تسمّي نيّةً يجب أن توجد وأن تكون منشورة وأن يُعرف
/// أثرُها على الدفتر. وفحصُها أثناء جمع النيّات يجعل النتيجة <b>تابعةً لترتيب
/// المجموعات في الحاوية</b>: خطّةُ المبيعات تسمّي نيّةً في مجموعةٍ لم تُزَر بعد فتُرفض،
/// ولو انعكس الترتيب لَقُبلت. وحارسٌ جوابُه يتغيّر بترتيب التسجيل ليس حارساً.
/// فمرحلتان: تُبنى النيّات كاملةً، ثم تُقاس الخطط عليها.
/// </para>
/// <para>
/// <b>والبناء يُسقط التركيب</b> — لا نُطقاً واحداً — كما يفعل سجلّ النيّات وللسبب نفسه.
/// </para>
/// </summary>
public sealed partial class VoicePlanRegistry
{
    private readonly Dictionary<string, VoicePlan> _byId;

    private VoicePlanRegistry(Dictionary<string, VoicePlan> byId)
    {
        _byId = byId;
        Plans = [.. byId.Values.OrderBy(static plan => plan.Id, StringComparer.Ordinal)];
    }

    /// <summary>كل الخطط مرتَّبةً بمعرّفاتها.</summary>
    public IReadOnlyList<VoicePlan> Plans { get; }

    /// <summary>عددها — يقرؤه حارس اللافراغ.</summary>
    public int Count => Plans.Count;

    /// <summary>شكل معرّف الخطّة — نفس شكل معرّف النيّة.</summary>
    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdShape();

    /// <summary>
    /// يبني السجلّ من مجموعات الوحدات، مقيساً على سجلّ النيّات المبنيّ.
    /// </summary>
    /// <param name="catalogues">ما وجده الجذر التركيبي في الحاوية.</param>
    /// <param name="intents">سجلّ النيّات — <b>مبنيّاً بالفعل</b>.</param>
    public static Result<VoicePlanRegistry> Build(
        IEnumerable<IVoicePlanCatalogue> catalogues,
        VoiceIntentRegistry intents)
    {
        ArgumentNullException.ThrowIfNull(catalogues);
        ArgumentNullException.ThrowIfNull(intents);

        Dictionary<string, VoicePlan> byId = new(StringComparer.Ordinal);
        List<Error> errors = [];

        foreach (IVoicePlanCatalogue catalogue in catalogues)
        {
            if (catalogue.Plans.Count == 0)
            {
                errors.Add(VoicePlanErrors.CatalogueEmpty);
                continue;
            }

            foreach (VoicePlan plan in catalogue.Plans)
            {
                if (!byId.TryAdd(plan.Id, plan))
                {
                    errors.Add(VoicePlanErrors.DuplicatePlanId(plan.Id));
                    continue;
                }

                if (!IdShape().IsMatch(plan.Id) || SuggestionGuard.CarriesNumericSegment(plan.Id))
                {
                    errors.Add(VoicePlanErrors.MalformedPlanId(plan.Id));
                }

                errors.AddRange(VoicePlanGuard.Refuse(plan, intents.Find));
            }
        }

        return errors.Count == 0
            ? Result<VoicePlanRegistry>.Success(new VoicePlanRegistry(byId))
            : Result<VoicePlanRegistry>.Failure(errors);
    }

    /// <summary>خطّةٌ بمعرّفها، أو لا شيء.</summary>
    /// <param name="planId">المعرّف.</param>
    public VoicePlan? Find(string planId) =>
        planId is not null && _byId.TryGetValue(planId, out VoicePlan? plan) ? plan : null;

    /// <summary>خطط قسمٍ بعينه، مرتَّبة.</summary>
    /// <param name="section">القسم.</param>
    public IReadOnlyList<VoicePlan> InSection(VoiceSection section) =>
        [.. Plans.Where(plan => plan.Section == section)];
}
