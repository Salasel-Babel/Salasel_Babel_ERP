using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Chain;

/// <summary>
/// عدّاد الفاتورة (‏ICV) وبصمة الفاتورة السابقة (‏PIH) — <b>سلسلة، لا حقلان</b>.
/// <para/>
/// <b>الشكل هو نفسه شكل سلسلة الدفتر في هذا المنتج</b> (‏ADR-0007): الرقم التسلسلي
/// والبصمة السابقة يدخلان <b>داخل</b> البايتات المُجزَّأة لا بجوارها، فيصير الرابط
/// تشفيرياً بدل أن يكون عمودين يعيد مالك قاعدة البيانات كتابتهما وتبقى كل بصمة صحيحة
/// (‏<c>docs/evidence/traps.md#fakh-decorative-chain-link-outside-the-hash</c>).
/// والعدّاد يُحجز بصفّ عدّاد تحت <c>SELECT … FOR UPDATE</c> لا بـ<c>SEQUENCE</c>
/// (‏ADR-0008): التسلسل غير المعاملاتي يُهدر رقماً عند التراجع، والفجوة في ترقيم مُلزَم
/// تُقرأ عند التدقيق «سجل محذوف».
/// <para/>
/// <b>ونقطة الالتقاء التي يجب أن تُقرأ بعناية:</b> حجز العدّاد وتقديم رأس السلسلة يقعان في
/// <c>Babel.Compliance</c>، داخل <b>معاملة البناء نفسها</b>، لا في هذا المزوّد. فالمزوّد
/// لا يملك عدّاداً ولا رأس سلسلة، ولا يستطيع أن يُحدث فجوة ولا أن يُعيد استعمال رقم.
/// وهذا ليس تقسيم عمل بل <b>خاصية</b>: محاولة إرسال فاشلة لا تحرق عدّاداً، لأن العدّاد
/// خُصِّص مرة واحدة قبل أي نداء شبكي.
/// </summary>
public static class ZatcaChain
{
    /// <summary>
    /// قيمة PIH للمستند الأول على وحدة الإصدار.
    /// <para/>
    /// <b>هذه القيمة ثابتة ومنشورة، ولا تُشتقّ من بيانات المستأجر</b> — وهنا تقع
    /// <b>الفجوة الوحيدة</b> بين سلسلتنا وسلسلة الهيئة: بصمة التكوين عندنا مشتقّة من
    /// (المستأجر × وحدة الإصدار) كي لا توجد سلسلة عالمية واحدة، أما الهيئة فتبدأ من
    /// بذرة واحدة لكل وحدة إصدار في العالم. فالمستند رقم 1 يكتب <b>بذرة الهيئة</b> في
    /// جسمه، بينما يحتفظ سجلّنا ببصمة التكوين الخاصة به.
    /// <para/>
    /// الفجوة <b>عند العدّاد 1 وحده</b>. وما بعده: PIH هو بصمة المستند السابق نفسها،
    /// فالسلسلتان تتطابقان.
    /// </summary>
    [Provisional("قيمة بذرة PIH للمستند الأول، وما إذا كانت ثابتة عالمياً أم لكل وحدة إصدار",
        DerivedFrom = "قراءة المواصفة وتنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة الختم التشفيري: القيمة الابتدائية لبصمة الفاتورة السابقة")]
    public const string GenesisPreviousInvoiceHash =
        "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==";

    /// <summary>العدّاد الأول. أي قيمة أقل عطل بناء لا حالة تشغيل.</summary>
    public const long FirstCounter = 1;

    /// <summary>
    /// قيمة PIH التي تُكتب في المستند لهذه الخانة.
    /// <para/>
    /// <b>لا تُجزّئ شيئاً</b>: البصمة السابقة محسوبة سلفاً في السلسلة، وإعادة تجزئتها هنا
    /// هي التجزئة المزدوجة نفسها في موضع ثالث
    /// (‏<c>docs/evidence/traps.md#fakh-double-hashing</c>).
    /// </summary>
    public static string PreviousInvoiceHash(ChainSlot slot)
    {
        if (slot.Counter < FirstCounter)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot), slot.Counter,
                "عدّاد الفاتورة يبدأ من 1. قيمة أقل تعني خانة سلسلة لم تُحجز. / the invoice counter starts at 1.");
        }

        return slot.Counter == FirstCounter
            ? GenesisPreviousInvoiceHash
            : Convert.ToBase64String(slot.PreviousHash.Span);
    }
}
