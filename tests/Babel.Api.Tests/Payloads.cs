using System.Globalization;
using System.Text.Json;

namespace Babel.Api.Tests;

/// <summary>
/// حمولات الطلبات — <b>نصّاً خامّاً</b> لا كائنات.
/// <para>
/// وهذا مقصود: نصف ما تفحصه هذه المجموعة لا يستطيع أي مُسلسِل سليم أن يُنتجه — مبلغ وصل
/// رمزاً رقمياً، وصيغة أسّية، وأرقام عربية-هندية، وحقل لا وجود له في العقد. اختبارٌ يبني
/// طلبه بكائن مطبوع يفحص المُسلسِل الذي كتبناه نحن، لا الخادم الذي يواجه عميلاً غريباً.
/// </para>
/// </summary>
internal static class Payloads
{
    /// <summary>
    /// حدث القيد اليدوي في مصفوفة الترحيل.
    /// <para>
    /// ‏<c>event</c> حقل <b>إلزامي</b> في العقد المنشور، وعلى المسارين معاً: الرمز يعطي القيد
    /// هويّته والسطور تعطيه محتواه (‏ADR-0016 · ADR-0018). وهذه الحمولات تسلك المسار الصريح —
    /// سطور مذكورة — فحدثها هو الحدث المعرَّف للقيد اليدوي في <c>data/posting-matrix/events</c>.
    /// </para>
    /// </summary>
    public const string ManualVoucherEvent = "ledger.manual_voucher.posted";

    /// <summary>الدور الذي يُحلّ إلى حساب بنكي — يحتاج طرفاً في الدفتر المساعد.</summary>
    public const string SettlementRole = "Settlement";

    /// <summary>الدور الذي يُحلّ إلى إيراد مبيعات — يحتاج بُعد الفرع.</summary>
    public const string RevenueRole = "NetAmount";

    /// <summary>القيمة التي تُظهر التلف: تحويلها إلى فاصلة عائمة ثنائية يجعلها 1000000000000.4012.</summary>
    public const string LossyUnderDouble = "1000000000000.4013";

    /// <summary>
    /// قيد متوازن بسطرين: مدين تسوية بنكية ودائن إيراد مبيعات.
    /// </summary>
    /// <param name="idempotencyKey">مفتاح الحصانة.</param>
    /// <param name="amount">المبلغ كما يُكتب على السلك — نصّاً، أو رمزاً خامّاً لاختبارات الرفض.</param>
    /// <param name="documentDate">تاريخ المستند.</param>
    /// <param name="rawAmountToken">إن ضُبط، يُكتب المبلغ كما هو بلا اقتباس — لحقن رمز رقمي.</param>
    /// <param name="module">الوحدة المصدر.</param>
    /// <param name="creditRole">دور السطر الدائن.</param>
    /// <param name="extraField">حقل إضافي يُحقن في جذر الطلب — لاختبار رفض المجهول.</param>
    /// <param name="event">رمز الحدث. ‏<c>null</c> يحذف الحقل كلّه — لاختبار رفض الطلب بلا هوية.</param>
    /// <param name="costCenterId">
    /// مركز التكلفة على السطر الدائن. <c>null</c> يحذف الحقل — وهو <b>الحالة الغالبة</b>
    /// في الحمولات هنا عمداً: حذفُه يعني «المركز الافتراضي لهذه المنشأة»، وهو افتراض
    /// معلن في العقد لا صمت (‏ADR-0026).
    /// </param>
    public static string BalancedEntry(
        string idempotencyKey,
        string amount = "1250.5000",
        string documentDate = "2026-08-15",
        string? rawAmountToken = null,
        string module = "Ledger",
        string creditRole = RevenueRole,
        string? extraField = null,
        string? @event = ManualVoucherEvent,
        string? costCenterId = null)
    {
        string debit = rawAmountToken ?? Quote(amount);
        string credit = rawAmountToken ?? Quote(amount);

        return $$"""
        {
          {{(extraField is null ? string.Empty : extraField + ",")}}
          {{(@event is null ? string.Empty : "\"event\": " + Quote(@event) + ",")}}
          "idempotencyKey": {{Quote(idempotencyKey)}},
          "source": { "module": {{Quote(module)}}, "documentType": "ManualJournal", "documentId": {{Quote(idempotencyKey)}} },
          "trigger": "OnApproval",
          "documentDate": {{Quote(documentDate)}},
          "narration": { "ar": "قيد يدوي عبر سطح HTTP", "en": "Manual journal through the HTTP surface" },
          "book": "MAIN",
          "currency": "SAR",
          "exchangeRate": "1",
          "generation": 1,
          "lines": [
            {
              "role": {{Quote(SettlementRole)}},
              "side": "Debit",
              "amount": {{debit}},
              "qualifier": "bank",
              "subledger": { "kind": "Treasury", "partyId": "BANK-0001" },
              "narration": { "ar": "تحصيل بنكي", "en": "Bank receipt" }
            },
            {
              "role": {{Quote(creditRole)}},
              "side": "Credit",
              "amount": {{credit}},
              "scope": { "branchId": "BR-01"{{(costCenterId is null ? string.Empty : ", \"costCenterId\": " + Quote(costCenterId))}} },
              "narration": { "ar": "إيراد", "en": "Revenue" }
            }
          ]
        }
        """;
    }

    /// <summary>قيد غير متوازن عمداً — لإثبات أن الرفض المحاسبي يصل برمزه لا بنصّه.</summary>
    /// <param name="idempotencyKey">مفتاح الحصانة.</param>
    public static string UnbalancedEntry(string idempotencyKey) => $$"""
        {
          "event": {{Quote(ManualVoucherEvent)}},
          "idempotencyKey": {{Quote(idempotencyKey)}},
          "source": { "module": "Ledger", "documentType": "ManualJournal", "documentId": {{Quote(idempotencyKey)}} },
          "trigger": "OnApproval",
          "documentDate": "2026-08-15",
          "narration": { "ar": "قيد غير متوازن", "en": "Unbalanced entry" },
          "lines": [
            {
              "role": "Settlement", "side": "Debit", "amount": "100.0000", "qualifier": "bank",
              "subledger": { "kind": "Treasury", "partyId": "BANK-0001" }
            },
            {
              "role": "NetAmount", "side": "Credit", "amount": "90.0000",
              "scope": { "branchId": "BR-01" }
            }
          ]
        }
        """;

    /// <summary>قيد بدور مكتوب بحالة أحرف مختلفة — مسبار الثقافة.</summary>
    /// <param name="idempotencyKey">مفتاح الحصانة.</param>
    /// <param name="roleSpelling">هجاء الدور كما يُرسله العميل.</param>
    public static string EntryWithRoleSpelling(string idempotencyKey, string roleSpelling) =>
        BalancedEntry(idempotencyKey, creditRole: roleSpelling);

    /// <summary>طلب عكس.</summary>
    /// <param name="reversalDate">تاريخ العكس، أو <c>null</c>.</param>
    public static string Reversal(string? reversalDate = "2026-08-20") => $$"""
        {
          "reason": { "ar": "تصحيح خطأ في المستند المصدر", "en": "Correcting an error in the source document" }
          {{(reversalDate is null ? string.Empty : ", \"reversalDate\": " + Quote(reversalDate))}}
        }
        """;

    /// <summary>يقتبس نصّاً بصيغة JSON.</summary>
    /// <param name="value">النصّ.</param>
    public static string Quote(string value) => JsonSerializer.Serialize(value);

    /// <summary>مفتاح حصانة فريد لكل تشغيل — الاختبارات لا تتقاسم حالة.</summary>
    /// <param name="prefix">بادئة وصفية.</param>
    public static string Key(string prefix) =>
        prefix + "-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..16];
}
