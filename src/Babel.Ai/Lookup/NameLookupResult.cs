using System.Globalization;
using System.Text;

namespace Babel.Ai.Lookup;

/// <summary>نتيجة البحث كما يراها النموذج: لا شيء · واحد · غامض.</summary>
public enum NameLookupOutcome
{
    /// <summary>لا اسم يطابق في هذه المنشأة.</summary>
    None = 1,

    /// <summary>اسمٌ واحد بالضبط — ومعه مِقبضه.</summary>
    Resolved = 2,

    /// <summary>أكثر من واحد. تُعرض ورقة سؤال، <b>ولا يُقال كم كانوا</b>.</summary>
    NeedsQuestion = 3,
}

/// <summary>
/// <b>الجواب — ثلاثة حقول لا رابع لها، ومجموعة المفاتيح واحدة في الحالات الثلاث.</b>
/// <para>
/// وما ليس عليه <b>عمداً</b>: لا <c>Count</c>، ولا <c>CandidateCount</c>، ولا <c>Score</c>،
/// ولا <c>Confidence</c>، ولا <c>TopMatch</c>، ولا <c>Names</c>. والغياب بنيويّ لا اتّفاقيّ:
/// النوع الذي يصل من المحوّل (<c>NameCandidateProbe</c>) لا يحمل عدداً أصلاً، والاستعلام
/// يقف عند صفّين — <b>فما لم يُحسب لا يُسرَّب</b>.
/// </para>
/// </summary>
public sealed record NameLookupResult
{
    private NameLookupResult(NameLookupOutcome outcome, string? handle, string? questionId)
    {
        Outcome = outcome;
        Handle = handle;
        QuestionId = questionId;
    }

    /// <summary>الحالة.</summary>
    public NameLookupOutcome Outcome { get; }

    /// <summary>مِقبض الكِيان — مضبوطٌ عند <see cref="NameLookupOutcome.Resolved"/> وحدها.</summary>
    public string? Handle { get; }

    /// <summary>معرّف الورقة — مضبوطٌ عند <see cref="NameLookupOutcome.NeedsQuestion"/> وحدها.</summary>
    public string? QuestionId { get; }

    /// <summary>لا مطابق.</summary>
    public static NameLookupResult None { get; } = new(NameLookupOutcome.None, null, null);

    /// <summary>مطابقٌ واحد.</summary>
    /// <param name="handle">مِقبض الكِيان.</param>
    public static NameLookupResult Resolved(string handle)
    {
        ArgumentException.ThrowIfNullOrEmpty(handle);
        return new NameLookupResult(NameLookupOutcome.Resolved, handle, null);
    }

    /// <summary>غموضٌ يُسأل عنه.</summary>
    /// <param name="questionId">معرّف الورقة — مِقبضٌ غرضه <see cref="LookupHandlePurpose.Question"/>.</param>
    public static NameLookupResult NeedsQuestion(string questionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(questionId);
        return new NameLookupResult(NameLookupOutcome.NeedsQuestion, null, questionId);
    }
}

/// <summary>
/// <b>الشكل السلكيّ الذي يعبر إلى النموذج — ويُكتب هنا لا في نقطة النهاية.</b>
/// <para>
/// السبب أن الخاصّية المطلوبة خاصّيةُ <b>بايتات</b> لا خاصّيةُ نوع: مجموعة المفاتيح
/// وترتيبها واحدة في الحالات الثلاث، والمِقبض ومعرّف الورقة طولهما ثابت
/// (<see cref="SignedLookupHandles.TokenLength"/>). فطولُ الجواب في حالة الغموض
/// <b>واحدٌ سواء كان المرشّحون اثنين أو خمسين</b>، وذلك يُثبَت على البايتات لا على النوع.
/// </para>
/// <para>
/// ومُسَلسِلٌ عامّ كان سيُغري بإضافة حقلٍ «للتشخيص» يُقاس منه العدد. هذا يكتب ثلاثة مفاتيح ولا رابع.
/// </para>
/// </summary>
public static class NameLookupWire
{
    /// <summary>يكتب الجواب بالشكل الذي يقرؤه النموذج.</summary>
    /// <param name="result">الجواب.</param>
    public static string Write(NameLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder json = new(200);
        json.Append(CultureInfo.InvariantCulture, $"{{\"outcome\":\"{Token(result.Outcome)}\"");
        json.Append(",\"handle\":").Append(Quoted(result.Handle));
        json.Append(",\"questionId\":").Append(Quoted(result.QuestionId));
        json.Append('}');
        return json.ToString();
    }

    /// <summary>الرمز النصّي للحالة كما يقرؤه النموذج.</summary>
    /// <param name="outcome">الحالة.</param>
    public static string Token(NameLookupOutcome outcome) => outcome switch
    {
        NameLookupOutcome.None => "none",
        NameLookupOutcome.Resolved => "resolved",
        NameLookupOutcome.NeedsQuestion => "needs_question",

        // قيمةٌ خارج المفردات المغلقة تُرفع ولا تُكتب «unknown» — والصمت هنا يعني
        // أن النموذج يقرأ حالةً لم يُقصد أن توجد.
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "حالة بحثٍ خارج المفردات المغلقة."),
    };

    /// <summary>المقابض base64url فلا محرف فيها يحتاج هرباً — ومع ذلك تُرفض ما ليس كذلك.</summary>
    private static string Quoted(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                throw new ArgumentException(
                    "المِقبض يحمل محرفاً خارج base64url فلا يُكتب في الشكل السلكيّ. "
                    + "/ the handle carries a character outside base64url.",
                    nameof(value));
            }
        }

        return "\"" + value + "\"";
    }
}
