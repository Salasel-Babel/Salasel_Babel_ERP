using Babel.Ai.Lookup;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// <b>آلة حالة الدور الواحد — وهي فحصُ بوّابة لا تعليمةٌ في نصّ نظام.</b>
/// <para>
/// <b>التسريب الواقعي ليس جواباً واحداً بل التكرار:</b> وكيلٌ يبحث «محمد» ثم «محمد ع»
/// ثم «محمد عل» يُنصّف السجلّ بحثاً ثنائياً ويستخرج العدد الذي منعناه من الجواب. وجملةٌ
/// في نصّ النظام تقول «لا تفعل» ليست حاجزاً: النموذج احتماليّ، وحاجزٌ يصمد تسعاً
/// وتسعين مرّة من مئة ليس حاجزاً.
/// </para>
/// <para>
/// <b>فثلاث قواعد تُفحص قبل التنفيذ:</b> سقفٌ عدديّ للبحث في الدور؛ وبعد أي غموضٍ في
/// سجلٍّ لا يجوز بحثٌ ثانٍ في ذلك السجلّ إلا بعد <c>ask_question</c>؛ وبحثان مفتاح
/// أحدهما بادئةٌ <b>صارمة</b> للآخر يُرفضان سبراً.
/// </para>
/// <para>
/// والرفض يعود إلى النموذج <c>tool_result { is_error: true }</c> بنصّه العربي فيُصحّح،
/// ولا يُرمى استثناءً يقتل الدور.
/// </para>
/// </summary>
public sealed class AgentTurnState
{
    private readonly int _lookupBudget;
    private readonly HashSet<string> _awaitingQuestion = new(StringComparer.Ordinal);
    private readonly List<string> _lookupTexts = [];

    /// <summary>ينشئ حالةً لدورٍ واحد.</summary>
    /// <param name="lookupBudget">سقف نداءات البحث في هذا الدور.</param>
    public AgentTurnState(int lookupBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lookupBudget, 1);
        _lookupBudget = lookupBudget;
    }

    /// <summary>كم بحثاً جرى في هذا الدور.</summary>
    public int LookupsMade { get; private set; }

    /// <summary>
    /// كم دورةَ «نداء نموذج» جرت. تُزاد من الحلقة، وتُقرأ في القياس — <b>ولا يُقاس منها
    /// عددُ مرشّحين</b>: هي عدّاد دورات لا عدّاد صفوف.
    /// </summary>
    public int ModelCalls { get; private set; }

    /// <summary>السجلّات التي غمض فيها اسمٌ ولم يُسأل عنه بعد.</summary>
    public IReadOnlySet<string> RegistersAwaitingQuestion => _awaitingQuestion;

    /// <summary>يسجّل نداء نموذجٍ جرى.</summary>
    public void RecordModelCall() => ModelCalls++;

    /// <summary>
    /// يفحص نداء بحثٍ <b>قبل</b> إرساله. يعيد الخطأ عند الرفض، أو <c>null</c> عند الإذن —
    /// ولا يُسجّل شيئاً عند الرفض، فالمرفوض لم يقع.
    /// </summary>
    /// <param name="registerKey">مفتاح السجلّ المطلوب.</param>
    /// <param name="text">نصّ البحث كما كتبه النموذج.</param>
    public Error? RefuseLookup(string registerKey, string text)
    {
        ArgumentNullException.ThrowIfNull(registerKey);
        ArgumentNullException.ThrowIfNull(text);

        if (LookupsMade >= _lookupBudget)
        {
            return AgentErrors.LookupBudgetSpent(_lookupBudget);
        }

        if (_awaitingQuestion.Contains(registerKey))
        {
            return AgentErrors.AskBeforeLookingAgain(registerKey);
        }

        foreach (string earlier in _lookupTexts)
        {
            if (ArabicNameFold.OneFoldsToAStrictPrefixOfTheOther(earlier, text))
            {
                return AgentErrors.LookupProbing;
            }
        }

        return null;
    }

    /// <summary>يسجّل بحثاً <b>جرى فعلاً</b> ونتيجته.</summary>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    /// <param name="text">نصّ البحث.</param>
    /// <param name="ambiguous">هل كان الجواب غامضاً؟</param>
    public void RecordLookup(string registerKey, string text, bool ambiguous)
    {
        ArgumentNullException.ThrowIfNull(registerKey);
        ArgumentNullException.ThrowIfNull(text);

        LookupsMade++;
        _lookupTexts.Add(text);

        if (ambiguous)
        {
            _awaitingQuestion.Add(registerKey);
        }
    }

    /// <summary>يرفع الحجر عن سجلٍّ بعد أن سُئل عنه فعلاً.</summary>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    public void RecordQuestionAnswered(string registerKey)
    {
        ArgumentNullException.ThrowIfNull(registerKey);
        _awaitingQuestion.Remove(registerKey);
    }
}
