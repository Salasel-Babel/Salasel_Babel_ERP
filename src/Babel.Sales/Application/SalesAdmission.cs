using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;

namespace Babel.Sales.Application;

/// <summary>
/// <b>بوابة القبول في وحدة المبيعات — الموضع الذي كان مفقوداً.</b>
/// <para>
/// النواة تبني ملفّ قدرات مغلقاً مُطابَقاً بمصفوفة الترحيل، وتُنتج
/// <see cref="AdmittedDocument"/> الذي لا يُبنى إلا بالمرور من القبول (ADR-0023). لكن
/// نوعاً لا يطلبه أحد في توقيعه ليس حارساً: البوابة كانت قائمة <b>ولا يستدعيها شيء</b>،
/// فالحقل الذي ترخّصه قدرة مُطفأة كان يمرّ كأن الملفّ لا وجود له.
/// </para>
/// <para>
/// <b>وغياب الملفّ رفضٌ لا فتح.</b> مستأجرٌ بلا ملفّ قدرات ليس مستأجراً «بلا قيود»، بل
/// مستأجراً لم يُقرَّر بعد ما اشتراه. والفتح عند غياب الملفّ يجعل الحارس كلّه بلا أثر
/// عملي: يكفي ألّا يُحفظ ملفّ لتمرّ كل قدرة — وهو باب واحد غير محروس يُلتفّ منه على
/// البوابة كلّها خلال شهر.
/// </para>
/// <para>
/// <b>ولماذا الطلب على مستوى الحقل لا على مستوى «العملية»:</b> الكتالوج يرخّص
/// <b>حقولاً</b>، والقدرة تُمارَس بحمل حقلها. فالوحدة تُصرّح بالحقول التي يحملها ما
/// تفعله فعلاً، والملفّ يقبل أو يرفض. ولا تخترع الوحدة أسماء حقول: الأسماء أدناه هي
/// نفسها التي في <c>CapabilityCatalogue</c>، ويحرسها اختبار يطابقهما.
/// </para>
/// </summary>
internal sealed class SalesAdmission
{
    /// <summary>نوع مستند فاتورة المبيعات في الكتالوج.</summary>
    public const string InvoiceDocumentType = "sales.invoice";

    /// <summary>حقل العميل — حقل أساسي قائم دائماً.</summary>
    public const string CustomerField = "customer";

    /// <summary>حقل السطور — حقل أساسي قائم دائماً.</summary>
    public const string LinesField = "lines";

    /// <summary>حقل استنفاد الدفعة المقدمة — ترخّصه قدرة <c>advance</c>.</summary>
    public const string AdvanceAppliedField = "advanceApplied";

    /// <summary>حقل المستودع — ترخّصه قدرة <c>cost_of_sales</c>.</summary>
    public const string WarehouseField = "warehouse";

    private readonly ICapabilityProfileStore _profiles;

    /// <summary>ينشئ البوابة.</summary>
    /// <param name="profiles">مخزن ملفّات القدرات.</param>
    public SalesAdmission(ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles;
    }

    /// <summary>
    /// يعرض فاتورة بحقولها على ملفّ المستأجر، فيُرجع مستنداً <b>مقبولاً</b> أو يرفض.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="presentFields">الحقول التي يحملها المستند فعلاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AdmittedDocument>> AdmitInvoiceAsync(
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
            return Result<AdmittedDocument>.Failure(SalesErrors.CapabilityProfileMissing(tenant));
        }

        return profile.Admit(new DocumentSubmission(new DocumentTypeCode(InvoiceDocumentType), presentFields));
    }

    /// <summary>
    /// يتأكّد أن المستند المقبول <b>هو</b> المستند الذي يُنفَّذ الآن.
    /// <para>
    /// بدون هذا الفحص يصير <see cref="AdmittedDocument"/> تذكرةً عامّة: قبولٌ نُشئ لفاتورة
    /// بلا قدرات يُمرَّر إلى مسار يمارس قدرة، فيبدو الأمر مقبولاً وهو لم يُعرض قطّ.
    /// </para>
    /// </summary>
    /// <param name="admitted">المستند المقبول.</param>
    /// <param name="field">الحقل الذي يمارسه هذا المسار.</param>
    public static Result EnsureCovers(AdmittedDocument admitted, string field)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return admitted.Fields.Contains(field, StringComparer.Ordinal)
            && string.Equals(admitted.DocumentType.Value, InvoiceDocumentType, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(SalesErrors.AdmissionDoesNotCoverField(
                admitted.DocumentType.Value ?? string.Empty, field));
    }
}
