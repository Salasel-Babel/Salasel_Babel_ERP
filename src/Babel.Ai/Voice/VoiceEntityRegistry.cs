using System.Text.RegularExpressions;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>سجلّ الأسماء المعروفة — تُسهم به الوحدات، ولا يعرف هذا المشروع واحدةً منها بالاسم.</b>
/// <para>
/// <b>وما يفعله في جملةٍ واحدة:</b> يجيب على سؤالٍ واحد — «أيُّ اسمٍ مسجَّل هو
/// <b>بادئةُ</b> هذه النافذة من الكلام؟» — فينتهي اسمُ الطرف حيث ينتهي صفُّه في السجلّ،
/// لا حيث تصادف أن جاءت أداةُ عطفٍ يعرفها القارئ.
/// </para>
/// <para>
/// <b>والتعادلُ رفضٌ لا قرعة:</b> إن سُجّل «المسار» و«المسار الأمثل» معاً، فأطولُهما
/// ليس قاعدةً بل تخمينٌ يلبس ثوبها — والمقياس نفسه الذي يرفض نيّتين متعادلتين يرفض
/// هنا. أمّا اسمان بطولين مختلفين فأحدهما بادئةُ الآخر، والأطولُ <b>أخصّ</b> لا
/// أرجح — وهو الاختيار نفسه الذي تختاره مطابقةُ النيّات.
/// </para>
/// <para>
/// <b>ولا يُقارَب اسمٌ بأقرب شبيه.</b> تقريبٌ في اسم طرفٍ يُنتج مستنداً صحيح الشكل على
/// طرفٍ آخر، ولا يراه أحد. ومن لم يُسجَّل يُرفض باسمه.
/// </para>
/// </summary>
public sealed partial class VoiceEntityRegistry
{
    private readonly Dictionary<VoiceEntityKind, List<string[]>> _folded;
    private readonly Dictionary<VoiceEntityKind, List<string>> _names;

    private VoiceEntityRegistry(
        Dictionary<VoiceEntityKind, List<string[]>> folded,
        Dictionary<VoiceEntityKind, List<string>> names)
    {
        _folded = folded;
        _names = names;
    }

    /// <summary>سجلٌّ لا يعرف اسماً واحداً — وهو الحال حين لا تُحقن أدلّة.</summary>
    public static VoiceEntityRegistry Empty { get; } = new([], []);

    /// <summary>عدد الأسماء المعروفة كلَّها — يقرؤه حارس اللافراغ.</summary>
    public int Count => _names.Values.Sum(static list => list.Count);

    /// <summary>شكلٌ يحمل مقطعاً رقمياً — رقم حسابٍ متسلّل (القاعدة 2).</summary>
    [GeneratedRegex(@"(^|[^0-9])[0-9]{3,}([^0-9]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericSegment();

    /// <summary>
    /// يبني السجلّ من أدلّة الوحدات، ويرفض ما يخالف عند <b>البناء</b> لا عند النُّطق.
    /// </summary>
    /// <param name="directories">ما وجده الجذر التركيبي في الحاوية.</param>
    public static Result<VoiceEntityRegistry> Build(IEnumerable<IVoiceEntityDirectory> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);

        Dictionary<VoiceEntityKind, List<string[]>> folded = [];
        Dictionary<VoiceEntityKind, List<string>> names = [];
        List<Error> errors = [];

        foreach (IVoiceEntityDirectory directory in directories)
        {
            if (directory.Kind == VoiceEntityKind.None)
            {
                errors.Add(VoiceEntityErrors.DirectoryNamesNoRegister(directory.Module.ToString()));
                continue;
            }

            foreach (string name in directory.Names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(VoiceEntityErrors.NameEmpty(directory.Kind.ToString()));
                    continue;
                }

                // ‏**القاعدة 2 مفروضة هنا لا موصوفة**: اسمٌ يحمل مقطعاً رقمياً هو رقم
                // حسابٍ يعبر في ثوب اسم، ولو مرّةً واحدة.
                if (NumericSegment().IsMatch(name))
                {
                    errors.Add(VoiceEntityErrors.NameCarriesALedgerCode(directory.Kind.ToString(), name));
                    continue;
                }

                string[] parts = [.. VoiceText.Words(name).Select(VoiceText.Fold)];
                if (parts.Length == 0)
                {
                    errors.Add(VoiceEntityErrors.NameEmpty(directory.Kind.ToString()));
                    continue;
                }

                if (!folded.TryGetValue(directory.Kind, out List<string[]>? bucket))
                {
                    bucket = [];
                    folded[directory.Kind] = bucket;
                    names[directory.Kind] = [];
                }

                bucket.Add(parts);
                names[directory.Kind].Add(VoiceText.Strip(name));
            }
        }

        return errors.Count > 0
            ? Result<VoiceEntityRegistry>.Failure(errors)
            : Result<VoiceEntityRegistry>.Success(new VoiceEntityRegistry(folded, names));
    }

    /// <summary>هل يعرف هذا السجلّ اسماً واحداً من هذا الصنف؟</summary>
    /// <param name="kind">الصنف.</param>
    public bool Knows(VoiceEntityKind kind) =>
        kind != VoiceEntityKind.None && _folded.TryGetValue(kind, out List<string[]>? bucket) && bucket.Count > 0;

    /// <summary>
    /// <b>أطولُ اسمٍ مسجَّل هو بادئةُ هذه النافذة</b> — وهو حدُّ الاسم.
    /// </summary>
    /// <param name="kind">الصنف.</param>
    /// <param name="window">النافذة: الكلمات من دليل الشريحة إلى أوّل حدّ.</param>
    /// <returns>
    /// المطابقة، أو لا شيء حين لا يبدأ شيءٌ مسجَّل هذه النافذة. و<c>Tied</c> صحيحة حين
    /// طابق اسمان مختلفان بالطول نفسه — <b>فيُرفض ولا يُقترع</b>.
    /// </returns>
    public VoiceEntityMatch? LongestPrefix(VoiceEntityKind kind, IReadOnlyList<string> window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!_folded.TryGetValue(kind, out List<string[]>? bucket))
        {
            return null;
        }

        List<string> registered = _names[kind];
        int best = 0;
        int at = -1;
        bool tied = false;

        for (int index = 0; index < bucket.Count; index++)
        {
            string[] candidate = bucket[index];
            if (candidate.Length > window.Count || candidate.Length < best)
            {
                continue;
            }

            bool hit = true;
            for (int offset = 0; offset < candidate.Length; offset++)
            {
                if (!string.Equals(VoiceText.Fold(window[offset]), candidate[offset], StringComparison.Ordinal))
                {
                    hit = false;
                    break;
                }
            }

            if (!hit)
            {
                continue;
            }

            if (candidate.Length > best)
            {
                best = candidate.Length;
                at = index;
                tied = false;
            }
            else if (!string.Equals(registered[index], registered[at], StringComparison.Ordinal))
            {
                tied = true;
            }
        }

        return at < 0 ? null : new VoiceEntityMatch(registered[at], best, tied);
    }
}

/// <summary>اسمٌ مسجَّل طابق بادئةَ النافذة.</summary>
/// <param name="Name">الاسم بإملائه المسجَّل — وهو ما يُحفَظ ويُعرض.</param>
/// <param name="Words">عدد كلماته في النافذة.</param>
/// <param name="Tied">هل طابق اسمٌ آخر بالطول نفسه؟ <b>والتعادل رفضٌ لا قرعة</b>.</param>
public sealed record VoiceEntityMatch(string Name, int Words, bool Tied);
