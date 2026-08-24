using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Babel.Api.Hosting;

/// <summary>
/// إعدادات التسلسل على السلك — <b>واحدة لكل السطح</b>.
/// <para>
/// إعدادان مختلفان بين نقطتي نهاية يعني عقدين مختلفين تحت اسم واحد، وهو أسوأ من عقدين
/// معلنين. ولذلك النسخة واحدة وساكنة ويقرؤها المُسلسِل والمولّد معاً.
/// </para>
/// <para>
/// وكل بند هنا قرار، لا افتراض:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>UnmappedMemberHandling = Disallow</c>: حقل لا نعرفه يُفشل الطلب كله. التجاهل
///     الصامت يعني أن عميلاً يرسل <c>tenantId</c> أو <c>accountCode</c> ويظنّ أنه أثّر،
///     ولا يعلم أنه لم يصل — وهو صمت في مسار مالي.
///   </description></item>
///   <item><description>
///     <c>PropertyNameCaseInsensitive = false</c> (الافتراضي، ومثبَّت هنا صراحةً): المطابقة
///     غير الحسّاسة تُدخل تحويل حالة الأحرف في مسار التحليل، وهو الباب الذي يدخل منه
///     <c>tr-TR</c> (فخ-39).
///   </description></item>
///   <item><description>
///     <c>NumberHandling = Strict</c>: لا رقم يُقرأ من نصّ ولا نصّ يُقرأ من رقم ضمنياً.
///     المال يُفرض نصّاً بمحوّله الخاص، وهذا يمنع أي طريق التفاف على ذلك.
///   </description></item>
///   <item><description>
///     <c>AllowTrailingCommas = false</c> و<c>ReadCommentHandling = Disallow</c>: حمولة
///     صارمة، فما يُقبل هنا يُقبل عند أي عميل مطابق للمواصفة.
///   </description></item>
///   <item><description>
///     <c>DefaultIgnoreCondition = Never</c>: كل حقل معلن يُكتب ولو كان <c>null</c>.
///     حذف الفارغ يُنتج عقدين: واحد للحالة السعيدة وآخر للحالة الفارغة، ويُجبر العميل
///     على التمييز بين «غائب» و«فارغ» بلا تعريف.
///   </description></item>
/// </list>
/// </summary>
internal static class ApiJson
{
    /// <summary>الإعدادات الوحيدة للسطح كله.</summary>
    public static JsonSerializerOptions Options { get; } = Build();

    private static JsonSerializerOptions Build()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            // كل حقل معلن يُكتب، ولو كان null. حذف الحقل الفارغ يجعل العميل يميّز بين
            // «غائب» و«فارغ» بلا تعريف، ويجعل العقد المنشور يعد بحقل لا يجده — وقد وقع
            // ذلك فعلاً هنا: firstDivergentSequence اختفى من حكم السلسلة السليمة.
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            MaxDepth = 32,
            WriteIndented = false,

            // العربية تُكتب كما هي لا مهروبة: الترميز UTF-8 والمُخرَج يُقرأ في متصفّح
            // وفي سجلّ وفي مراجعة. والهروب لا يزيد أماناً في سياق JSON نفسه.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // المُحلِّل الافتراضي يُضبط صراحةً قبل التجميد: MakeReadOnly يرفض نسخة بلا مُحلِّل،
        // ورفضُه يقع عند **أول طلب** لا عند الإقلاع — أي عطل يظهر عند العميل لا في النشر.
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
        options.MakeReadOnly();
        return options;
    }
}
