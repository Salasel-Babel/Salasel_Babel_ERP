using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Documents;

/// <summary>
/// تصنيف المستند إلى مسار. <b>هذا النوع هو الموضع الوحيد الذي يقرّر الاتجاه</b>،
/// و<see cref="ZatcaProfile.TypeNameOf"/> يشتقّ خانتَي السمة <c>name</c> من
/// <b>نفس القيمة</b> — فيستحيل بنيوياً أن يخرج مستند مكتوب «مبسّط» في جسمه ومُرسَل
/// في مسار المقاصة، أو العكس.
/// <para/>
/// <b>لماذا هذا الاحتياط بالذات:</b> المساران متعاكسان في اتجاه الاعتماد الزمني —
/// المقاصة <b>تسبق</b> تسليم المستند للمشتري، والإبلاغ <b>يلحقه</b>. وخلطهما يُنتج
/// إمّا إيقاف بيع بانتظار ردّ شبكي كان يجب ألّا يكون حاجزاً، وإمّا تسليم مستند قبل
/// اعتماده (‏<c>docs/evidence/traps.md#fakh-clearance-versus-reporting-are-opposite-paths</c>).
/// والخلط لا يقع بقرار، بل يقع حين يوجد <b>مصدران</b> للحقيقة: حقل في المستند وقرار
/// في المُنسِّق. فالعلاج بنيوي: مصدر واحد.
/// </summary>
public sealed class ZatcaFlowPolicy : IFlowPolicy
{
    /// <summary>
    /// المعيار: <b>وجود رقم تسجيل ضريبي للمشتري</b> يجعل المستند قياسياً (مسار مقاصة)،
    /// وغيابه يجعله مبسّطاً (مسار إبلاغ).
    /// <para/>
    /// <b>هذا المعيار غير مُتحقَّق منه.</b> التعريف الحقيقي للفاتورة المبسّطة — وحدوده،
    /// وسقوفه المالية إن وُجدت، واستثناءاته — يجب أن يُقرأ من المواصفة السارية قبل أول
    /// عميل. وما يصمد بلا تحقّق هو <b>البنية</b> لا <b>الشرط</b>: أن المسارين مختلفان،
    /// وأن قرار الاختيار يقع في موضع واحد.
    /// </summary>
    [Provisional("معيار تصنيف المستند إلى فاتورة قياسية أو مبسّطة، وسقوفه واستثناءاته",
        DerivedFrom = "قراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "تعريف الفاتورة المبسّطة وشروطها في مواصفة الفاتورة الإلكترونية السارية")]
    public ComplianceFlow FlowFor(ComplianceDocumentKind kind, PartyRef? buyer, DocumentTotals totals) =>
        string.IsNullOrWhiteSpace(buyer?.TaxRegistrationNumber)
            ? ComplianceFlow.Reporting
            : ComplianceFlow.Clearance;

    /// <summary>
    /// القناة التي يملكها هذا المسار. تُقرأ عند التركيب لا في كل نداء، وغيابها
    /// عطل تركيب لا عطل تشغيل.
    /// </summary>
    public static bool RequiresBlockingResponse(ComplianceFlow flow) => flow == ComplianceFlow.Clearance;
}
