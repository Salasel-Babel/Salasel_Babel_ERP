using Babel.SharedKernel;

namespace Babel.Contracts.Subledger;

/// <summary>
/// حدّ قراءة <b>نقطة الضبط</b> في دفتر الأستاذ — <b>معلَنٌ هنا مرّة واحدة لكل الدفاتر
/// المساعدة</b>.
/// <para>
/// الوحدة لا تسمّي حساباً ولا تقرأ جداول الدفتر (القاعدتان 1 و2)، لكن الدفتر المساعد
/// بلا مطابقة مع نقطة ضبطه دفترٌ يجرف بصمت. ولذلك يُعلَن <b>منفذ</b> يتكلّم بمفردات
/// الدفاتر المساعدة (‏<c>customer</c> · <c>supplier</c> · <c>item</c>) لا بمفردات
/// الحسابات، ويصله الجذر التركيبي بالدفتر.
/// </para>
/// <para>
/// <b>ولماذا موضعه العقد لا الوحدات:</b> كان هذا العقد نفسه مكتوباً <b>ثلاث مرّات</b>
/// في <c>Babel.Sales.Subledger</c> و<c>Babel.Purchasing.Subledger</c>
/// و<c>Babel.Inventory.Subledger</c>، وتنفيذه المطابق حرفاً بحرف <b>خمس مرّات</b>
/// (‏ثلاث تجهيزات اختبار وصنفان في أداة العرض). وقاعدةٌ واحدة مكتوبة في مواضع بلا
/// حارس يربطها هي <c>docs/evidence/traps.md#fakh-81</c> بعينها: تُحرَّر نسخةٌ وتُنسى
/// الأخرى، فيُجيب موضعان عن السؤال نفسه إجابتين. والوحدات الأفقية لا يعتمد بعضها على
/// بعض (القاعدة 3)، فالموضع الوحيد الذي تراه ثلاثتها هو العقد — وهو الشكل نفسه
/// المعتمد في <see cref="Babel.Contracts.Inventory.IInventoryValuation"/>.
/// </para>
/// <para>
/// والحارس على ذلك في
/// <c>tests/Babel.ArchitectureTests/ControlPointPortIsDeclaredOnce.cs</c>.
/// </para>
/// </summary>
public interface IControlPointReader
{
    /// <summary>
    /// يقرأ صافي حركة نقطة الضبط لنوع دفتر مساعد حتى تاريخ، مفصّلة بالمستند وبالطرف.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="subledgerKind">نوع الدفتر المساعد كما تعرّفه بيانات الدفتر.</param>
    /// <param name="asOf">التاريخ الذي تُقرأ الحركة حتى نهايته.</param>
    /// <param name="writtenBy">
    /// <b>مُرشِّح الوحدة المُرحِّلة — اختياري، وغيابه يعني «كل ما حرّك نقطة الضبط».</b>
    /// <para>
    /// <b>ولماذا وُجد:</b> نقطة الضبط الواحدة يُحرّكها أكثر من وحدة. ودفتر
    /// <c>customer</c> بعينه تكتب فيه المبيعات فواتيرها وسنداتها، <b>وتكتب فيه
    /// المقاولات مستخلصاتها</b> — ثلاثة سطور بـ<c>subledger: customer</c> في قالب
    /// <c>projects.client_certificate.posted</c> وحده. ومطابقةٌ تقرأ نقطة الضبط
    /// <b>بنوع الدفتر وحده</b> ثم تُقارن مجموعها بمستنداتها هي تُصدر انحرافاً على
    /// مستأجرٍ سليم: كل حركة ليست في جدولها تُقرأ «مفقودة من الدفتر المساعد».
    /// </para>
    /// <para>
    /// <b>ولماذا الوحدة لا نوع المستند — وهذا فرقٌ قِيس ولم يُقدَّر:</b> التضييق بأنواع
    /// مستندات الوحدة يُخفي <b>القيد اليدوي على الحساب الضابط</b>، وهو أشيع سببٍ حقيقي
    /// لانحراف الدفاتر المساعدة: نوعُه ليس في جرد أي وحدة، فيخرج من القراءة
    /// <b>بصمت</b> وتُعلن المطابقة صفراً على حسابٍ منحرف. أمّا الوحدة فهي حقلٌ يحمله
    /// كل قيد (<c>source_module</c>)، ويحمله القيد اليدوي أيضاً — فيبقى مرئياً
    /// لمطابقة الوحدة التي كُتب باسمها، ويخرج <b>وحده</b> ما كتبته وحدةٌ أخرى.
    /// (‏وقد أمسك هذا الفرقَ اختبارٌ قائم: مطابقةٌ ضُيِّقت بالأنواع أسقطت حقناً
    /// مقصوداً بـ777.0000 من تقريرها.)
    /// </para>
    /// <para>
    /// <b>ولا يجوز أن يكون هذا المُرشِّح علاجاً وحده:</b> تضييق قارئ على ما كتبه
    /// <b>ينقل</b> الثقب ولا يسدّه — الحركة التي خرجت من مطابقةٍ يجب أن تدخل
    /// مطابقةً ثانية يكتبها مَن حرّك الحساب الضابط (ADR-0041).
    /// </para>
    /// </param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<ControlPointSnapshot>> ReadAsync(
        TenantId tenant,
        string subledgerKind,
        DateOnly asOf,
        BabelModule? writtenBy = null,
        CancellationToken cancellationToken = default);
}

/// <summary>لقطة نقطة الضبط: الصافي وتفصيله بالمستند.</summary>
/// <param name="Net">صافي الحركة بمنطق «مدين ناقص دائن» بعملة الشركة.</param>
/// <param name="Movements">حركة كل مستند على حدة.</param>
public sealed record ControlPointSnapshot(decimal Net, IReadOnlyList<ControlPointMovement> Movements);

/// <summary>حركة مستند واحد على نقطة الضبط.</summary>
/// <param name="DocumentType">نوع المستند كما أرسلته الوحدة المُرحِّلة.</param>
/// <param name="DocumentId">معرّف المستند.</param>
/// <param name="PartyId">الطرف في الدفتر المساعد: العميل أو المورد أو الصنف.</param>
/// <param name="Net">صافي «مدين ناقص دائن» لسطور هذا المستند على نقطة الضبط.</param>
public sealed record ControlPointMovement(string DocumentType, string DocumentId, string PartyId, decimal Net);
