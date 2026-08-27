using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>
/// <b>بوابة القبول في وحدة المشتريات</b> — نفس شكلها في المبيعات، وللسبب نفسه.
/// <para>
/// النواة تبني ملفّ قدرات مغلقاً مُطابَقاً بمصفوفة الترحيل، وتُنتج
/// <see cref="AdmittedDocument"/> الذي لا يُبنى إلا بالمرور من القبول (‏ADR-0023). ونوعٌ
/// لا يطلبه أحد في توقيعه ليس حارساً: البوابة كانت <b>غير قابلة للوصل هنا أصلاً</b> —
/// لم يكن في الكتالوج نوع مستند مشتريات واحد، فلم يكن ثمّة ما يُقبَل.
/// </para>
/// <para>
/// <b>وغياب الملفّ رفضٌ لا فتح.</b> مستأجرٌ بلا ملفّ قدرات ليس مستأجراً «بلا قيود» بل
/// مستأجراً لم يُقرَّر بعد ما اشتراه. والفتح عند الغياب يجعل الحارس بلا أثر عملي: يكفي
/// ألّا يُحفظ ملفّ لتمرّ كل قدرة.
/// </para>
/// <para>
/// <b>ولماذا الطلب على مستوى الحقل:</b> الكتالوج يرخّص <b>حقولاً</b>، والقدرة تُمارَس
/// بحمل حقلها. والأسماء أدناه هي نفسها التي في <c>CapabilityCatalogue</c>، ويحرسها
/// اختبار يطابقهما.
/// </para>
/// </summary>
internal sealed class PurchasingAdmission
{
    /// <summary>نوع مستند فاتورة المورد في الكتالوج.</summary>
    public const string BillDocumentType = "purchasing.supplier_bill";

    /// <summary>حقل المورد — حقل أساسي قائم دائماً.</summary>
    public const string SupplierField = "supplier";

    /// <summary>حقل السطور — حقل أساسي قائم دائماً.</summary>
    public const string LinesField = "lines";

    /// <summary>حقل مركز التكلفة — حقل أساسي على الفاتورة المصروفية.</summary>
    public const string CostCenterField = "costCenter";

    /// <summary>حقل تصنيف المصروف — حقل أساسي على الفاتورة المصروفية.</summary>
    public const string ExpenseCategoryField = "expenseCategory";

    /// <summary>حقل الاستلام — ترخّصه قدرة <c>three_way_match</c>.</summary>
    public const string ReceiptField = "receipt";

    /// <summary>حقل تكلفة الاستيراد — ترخّصه قدرة <c>landed_cost</c>.</summary>
    public const string LandedCostField = "landedCost";

    private readonly ICapabilityProfileStore _profiles;

    /// <summary>ينشئ البوابة.</summary>
    /// <param name="profiles">مخزن ملفّات القدرات.</param>
    public PurchasingAdmission(ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles;
    }

    /// <summary>يعرض فاتورة مورد بحقولها على ملفّ المستأجر، فيُرجع مستنداً مقبولاً أو يرفض.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="presentFields">الحقول التي يحملها المستند فعلاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AdmittedDocument>> AdmitBillAsync(
        TenantId tenant,
        IReadOnlyCollection<string> presentFields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentFields);

        ValidatedCapabilityProfile? profile = await _profiles
            .FindAsync(tenant, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return Result<AdmittedDocument>.Failure(PurchasingErrors.CapabilityProfileMissing(tenant));
        }

        return profile.Admit(new DocumentSubmission(new DocumentTypeCode(BillDocumentType), presentFields));
    }

    /// <summary>
    /// يتأكّد أن المستند المقبول <b>هو</b> المستند الذي يُنفَّذ الآن.
    /// <para>
    /// بدون هذا الفحص يصير <see cref="AdmittedDocument"/> تذكرةً عامّة: قبولٌ نُشئ لفاتورة
    /// مصروف يُمرَّر إلى مسار مخزني، فيبدو الأمر مقبولاً وهو لم يُعرض قطّ.
    /// </para>
    /// </summary>
    /// <param name="admitted">المستند المقبول.</param>
    /// <param name="field">الحقل الذي يمارسه هذا المسار.</param>
    public static Result EnsureCovers(AdmittedDocument admitted, string field)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return admitted.Fields.Contains(field, StringComparer.Ordinal)
            && string.Equals(admitted.DocumentType.Value, BillDocumentType, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(PurchasingErrors.AdmissionDoesNotCoverField(
                admitted.DocumentType.Value ?? string.Empty, field));
    }
}
