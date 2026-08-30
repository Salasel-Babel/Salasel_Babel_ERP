using Babel.SharedKernel;

namespace Babel.Contracts.RealEstate;

/// <summary>
/// <b>منفذ تسجيل بُعد العقار في الدفتر</b> — يتكلّم بمفردات العقار لا بمفردات الحسابات.
/// <para>
/// <b>لماذا يوجد هذا المنفذ أصلاً:</b> قاعدة الحجب <c>GR-RE-001</c> تُقيَّم على الواقعة
/// <c>property.ownership_model</c>، ومصدرُها الوحيد المُعتمَد هو صفّ العقار في سجلّ
/// أبعاد الدفتر. ومقيس أن ذلك الجدول مُهاجَر منذ أساس الدفتر و<b>لا عبارة إدراج واحدة
/// عليه في <c>src/</c></b> — أي أن الوحدة العقارية لا تستطيع أن تُنشئ عقاراً يقبل
/// الدفتر أن يُرحّل عليه. وكل بديل عن ذلك أسوأ: أن تُصرّح الوحدة بالواقعة بنفسها
/// يعني <b>وحدةً تشهد لحارسٍ يحرسها</b>، وقد قيس أن مُخطِّط الترحيل يكتب قيمة السجلّ
/// <b>فوق</b> واقعة الوحدة فلا ينفع التصريح أصلاً.
/// </para>
/// <para>
/// <b>ولماذا موضعه العقد لا وحدة العقارات:</b> الوحدة الأفقية لا تعتمد على الدفتر
/// (القاعدة 3)، ومجموعة مراجعها <c>{SharedKernel, Contracts, Core}</c> بالضبط. فالموضع
/// الوحيد الذي تراه الوحدة ويراه الدفتر هو العقد — وهو الشكل نفسه المعتمد في
/// <see cref="Babel.Contracts.Subledger.IControlPointReader"/> و
/// <see cref="Babel.Contracts.Inventory.IInventoryValuation"/>.
/// </para>
/// <para>
/// <b>ولا تحديث ولا حذف في هذه الواجهة — وغيابهما بنيوي:</b> تغيير نموذج الملكية بعد
/// التسجيل <b>يُعيد تفسير قيودٍ سبق ترحيلها بأثر رجعي</b> (السطر الذي كان التزاماً
/// للمالك يصير إيراداً للشركة بلا قيد واحد يتحرّك). ونقلُ عقارٍ بين نموذجين واقعةٌ
/// تجارية لها مستندها، لا تعديلُ حقل. والطبقة الحاسمة على ذلك صلاحيات PostgreSQL:
/// دور التطبيق يُمنح <c>insert</c> وحده على <c>ledger.property_dimension</c>.
/// </para>
/// </summary>
public interface IPropertyDimensionRegistrar
{
    /// <summary>
    /// يسجّل عقاراً في سجلّ أبعاد الدفتر. <b>حصينٌ ضد التكرار</b>: تسجيلٌ ثانٍ بالقيم
    /// نفسها لا يفعل شيئاً ولا يُعدّ خطأ، وتسجيلٌ ثانٍ بنموذج ملكية مختلف <b>يُرفض</b>.
    /// </summary>
    /// <param name="tenant">المستأجر — وهو نطاق الشركة في الدفتر.</param>
    /// <param name="companyId">المنشأة التي يُسجَّل فيها العقار.</param>
    /// <param name="propertyId">معرّف العقار كما يظهر بُعداً على سطر القيد.</param>
    /// <param name="ownershipModel">نموذج الملكية — <c>own_property</c> أو <c>managed_for_others</c>.</param>
    /// <param name="name">اسم العقار: سجلُّه عربي وترجماته صفوف (ADR-0021).</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result> RegisterAsync(
        TenantId tenant,
        Guid companyId,
        string propertyId,
        string ownershipModel,
        TranslatedName name,
        CancellationToken cancellationToken = default);
}

/// <summary>نماذج الملكية التي يقبلها سجلّ أبعاد الدفتر — <b>وهي قيد تحقق في القاعدة لا اتفاق</b>.</summary>
public static class PropertyOwnershipModels
{
    /// <summary>ملكية ذاتية: العقار أصلٌ للمنشأة، والأجرة إيرادها.</summary>
    public const string OwnProperty = "own_property";

    /// <summary>إدارة أملاك الغير: الأجرة المحصَّلة <b>التزام تجاه المالك</b> لا إيراد للشركة.</summary>
    public const string ManagedForOthers = "managed_for_others";

    /// <summary>هل النصّ نموذجُ ملكيةٍ معروف؟ وما سواه يُرفض ولا يُخمَّن.</summary>
    /// <param name="value">القيمة المرشَّحة.</param>
    public static bool IsKnown(string? value)
        => string.Equals(value, OwnProperty, StringComparison.Ordinal)
        || string.Equals(value, ManagedForOthers, StringComparison.Ordinal);
}
