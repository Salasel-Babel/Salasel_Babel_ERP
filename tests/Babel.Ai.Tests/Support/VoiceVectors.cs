using System.Text.Json;
using System.Text.Json.Serialization;

namespace Babel.Ai.Tests.Support;

/// <summary>نيّة كما يصفها ملفّ المتجهات.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Section">القسم.</param>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="Kind">الصنف.</param>
/// <param name="Status">الحال.</param>
/// <param name="LedgerEffect">أثر الدفتر.</param>
/// <param name="EventCode">رمز الحدث.</param>
/// <param name="OperationId">العملية المنشورة التي تبلغها — ولا تبلغ ترحيلاً أبداً.</param>
/// <param name="RequiresConfirmation">هل تحتاج تأكيداً؟</param>
/// <param name="ReadsPersonalData">هل تقرأ بياناً شخصياً؟</param>
/// <param name="NameAr">الاسم العربي — وهو السجلّ لا ترجمته (‏ADR-0021)، ولا نصف إنجليزيّ بجانبه.</param>
/// <param name="Phrases">عبارات الإطلاق.</param>
/// <param name="Slots">الشرائح.</param>
internal sealed record VectorIntent(
    string Id,
    string Section,
    string Module,
    string Kind,
    string Status,
    string LedgerEffect,
    string? EventCode,
    string? OperationId,
    bool RequiresConfirmation,
    bool ReadsPersonalData,
    string NameAr,
    IReadOnlyList<string> Phrases,
    IReadOnlyList<VectorSlot> Slots);

/// <summary>شريحة كما يصفها ملفّ المتجهات.</summary>
/// <param name="Name">الاسم.</param>
/// <param name="Kind">الصنف.</param>
/// <param name="NameAr">اسمها العربي.</param>
/// <param name="Required">هل هي لازمة؟</param>
/// <param name="Cues">الدلائل.</param>
/// <param name="Choices">القائمة المغلقة.</param>
internal sealed record VectorSlot(
    string Name,
    string Kind,
    string NameAr,
    bool Required,
    IReadOnlyList<string> Cues,
    IReadOnlyList<string> Choices);

/// <summary>جملةٌ تُقرأ بنجاح، ومعها ما يجب أن يُستخرج منها.</summary>
/// <param name="Transcript">التفريغ.</param>
/// <param name="Intent">النيّة المتوقَّعة.</param>
/// <param name="Slots">الشرائح المتوقَّعة بقيمها النصّية.</param>
/// <param name="Units">وحدات الشرائح الكمّية.</param>
/// <param name="Dropped">
/// <b>ما قُصّ عند كاسر مقطع</b> لكل شريحة. يُقاس كي لا يكون القصّ صامتاً: متجهٌ يقول
/// «قُصّ هذا بعينه» يُحمِّر إن قُصّ غيرُه أو إن لم يُقصّ شيء.
/// </param>
internal sealed record VectorUtterance(
    string Transcript,
    string Intent,
    IReadOnlyDictionary<string, string> Slots,
    IReadOnlyDictionary<string, string>? Units,
    IReadOnlyDictionary<string, string>? Dropped = null);

/// <summary>جملةٌ تُفهَم نيّتُها وتنقصها شريحة.</summary>
/// <param name="Transcript">التفريغ.</param>
/// <param name="Intent">النيّة.</param>
/// <param name="Missing">الشرائح الناقصة.</param>
/// <param name="Faults">أعطال القراءة المتوقَّعة.</param>
/// <param name="WithoutToday">
/// تُقرأ الجملة <b>بلا حقنِ تاريخ اليوم</b>. وهي الطريقة الوحيدة لإثبات القاعدة
/// المكتوبة: <b>بلا حقنِ تاريخِ اليوم لا يُملأ حقلُ تاريخٍ إطلاقاً</b> — فحقلُ التاريخ
/// في تقريرٍ عمريّ ليس زينةً بل هو تاريخ القطع، واختراعُه يُنتج تقريراً صحيح الشكل
/// عن يومٍ لم يُطلَب.
/// </param>
/// <param name="Slots">
/// قيمُ الشرائح التي <b>امتلأت</b> رغم النقص — فجملةٌ ينقصها حقلٌ واحد يجب أن تقرأ
/// بقيّتَها صحيحةً، وإلّا كان النقصُ يخفي خطأً في الباقي.
/// </param>
/// <param name="Dropped">ما قُصّ عند كاسر مقطع لكل شريحة.</param>
internal sealed record VectorMissing(
    string Transcript,
    string Intent,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string>? Faults,
    IReadOnlyDictionary<string, string>? Slots = null,
    IReadOnlyDictionary<string, string>? Dropped = null,
    bool WithoutToday = false);

/// <summary>جملةٌ تُرفض قراءتُها أصلاً.</summary>
/// <param name="Transcript">التفريغ.</param>
/// <param name="Code">رمز الرفض.</param>
internal sealed record VectorRefusal(string Transcript, string Code);

/// <summary>ربطُ شريحةٍ كما يصفه ملفّ المتجهات.</summary>
/// <param name="SlotName">الشريحة.</param>
/// <param name="Source">مصدر القيمة.</param>
internal sealed record VectorBinding(string SlotName, string Source);

/// <summary>خطوةٌ كما يصفها ملفّ المتجهات.</summary>
/// <param name="StepId">معرّفها.</param>
/// <param name="IntentId">النيّة التي تسمّيها — <b>ولا عمليةَ هنا بحال</b>.</param>
/// <param name="Condition">شرطها.</param>
/// <param name="PurposeAr">غرضها بالعربية.</param>
/// <param name="Bindings">روابط شرائحها.</param>
/// <param name="ScreenAsksForAr">ما تطلبه شاشتُها ولا يطلبه الصوت.</param>
internal sealed record VectorPlanStep(
    string StepId,
    string IntentId,
    string Condition,
    string PurposeAr,
    IReadOnlyList<VectorBinding> Bindings,
    IReadOnlyList<string> ScreenAsksForAr);

/// <summary>خطّةٌ كما يصفها ملفّ المتجهات.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Section">القسم.</param>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="NameAr">الاسم العربي — وهو السجلّ لا ترجمته.</param>
/// <param name="TriggerPhrases">الطلب.</param>
/// <param name="ConditionPhrases">الشرط.</param>
/// <param name="Steps">الخطوات.</param>
internal sealed record VectorPlan(
    string Id,
    string Section,
    string Module,
    string NameAr,
    IReadOnlyList<string> TriggerPhrases,
    IReadOnlyList<string> ConditionPhrases,
    IReadOnlyList<VectorPlanStep> Steps);

/// <summary>ملفّ المتجهات كاملاً.</summary>
/// <param name="Today">تاريخ اليوم المُحقَن.</param>
/// <param name="StatutoryTaxRate">النسبة النظامية.</param>
/// <param name="CompanyNameAr">اسم المنشأة المفتوحة.</param>
/// <param name="Intents">النيّات.</param>
/// <param name="Plans">الخطط.</param>
/// <param name="Utterances">جمل تُقرأ.</param>
/// <param name="Missing">جمل ينقصها شيء.</param>
/// <param name="Refusals">جمل تُرفض.</param>
internal sealed record VoiceVectorFile(
    string Today,
    string StatutoryTaxRate,
    string CompanyNameAr,
    IReadOnlyList<VectorIntent> Intents,
    IReadOnlyList<VectorPlan> Plans,
    IReadOnlyList<VectorUtterance> Utterances,
    IReadOnlyList<VectorMissing> Missing,
    IReadOnlyList<VectorRefusal> Refusals);

/// <summary>
/// <b>ملفّ متجهات واحد يقرؤه تنفيذان.</b>
/// <para>
/// نفس مبدأ <c>arabic-spoken-numbers.v1.json</c> ونفس سببه (‏ADR-0030 خامساً): تنفيذٌ
/// في الخادم وتنفيذٌ في المتصفّح، وملفّان للمتجهات يعنيان أنّ انحرافهما لا يُكتشف إلا
/// على شاشة صاحب المصلحة. فالمتجهات ملفٌّ واحد، والانحراف يُحمِّر بوّابةً لا شاشة.
/// </para>
/// </summary>
internal static class VoiceVectors
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>المسار داخل المستودع — يقرؤه الطرفان.</summary>
    public const string RelativePath = "tests/Babel.Ai.Tests/golden/voice-intents.v1.json";

    /// <summary>الملفّ مقروءاً.</summary>
    public static VoiceVectorFile File { get; } = Load();

    private static VoiceVectorFile Load()
    {
        string json = System.IO.File.ReadAllText(RepositoryRoot.At(RelativePath));

        return JsonSerializer.Deserialize<VoiceVectorFile>(json, Options)
            ?? throw new InvalidOperationException("ملفّ متجهات المسار المنطوق لا يُقرأ.");
    }
}
