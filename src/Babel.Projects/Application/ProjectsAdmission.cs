using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;

namespace Babel.Projects.Application;

/// <summary>
/// <b>بوابة القبول في وحدة المقاولات</b> — بالشكل المُودَع في <c>SalesAdmission</c> حرفاً.
/// <para>
/// النواة تبني ملفّ قدرات مغلقاً مُطابَقاً بمصفوفة الترحيل، وتُنتج
/// <see cref="AdmittedDocument"/> الذي لا يُبنى إلا بالمرور من القبول (ADR-0023). ونوعٌ
/// لا يطلبه أحد في توقيعه ليس حارساً — فالمسار الذي يمارس قدرة <b>يمرّ من هنا</b>،
/// ويحرس ذلك ماسحُ اللغة الوسيطة على هذه التجميعة كما يحرسه على المبيعات والمشتريات.
/// </para>
/// <para>
/// <b>وغياب الملفّ رفضٌ لا فتح.</b> مستأجرٌ بلا ملفّ قدرات ليس مستأجراً «بلا قيود»، بل
/// مستأجراً لم يُقرَّر بعد ما اشتراه. والفتح عند الغياب يجعل الحارس كلّه بلا أثر عملي.
/// </para>
/// <para>
/// <b>ولا تخترع الوحدة أسماء حقول:</b> الأسماء أدناه هي نفسها التي في
/// <c>CapabilityCatalogue</c> على نوع المستند <c>projects.client_certificate</c> —
/// الأساسيان <c>contract</c> و<c>workValue</c>، و<c>advanceRecovery</c> ترخّصه قدرة
/// <c>advance</c>، و<c>retention</c> ترخّصه قدرة <c>retention</c>.
/// </para>
/// </summary>
internal sealed class ProjectsAdmission
{
    /// <summary>نوع مستند مستخلص العميل في الكتالوج.</summary>
    public const string CertificateDocumentType = "projects.client_certificate";

    /// <summary>حقل العقد — حقل أساسي قائم دائماً.</summary>
    public const string ContractField = "contract";

    /// <summary>حقل قيمة الأعمال — حقل أساسي قائم دائماً.</summary>
    public const string WorkValueField = "workValue";

    /// <summary>حقل استرداد الدفعة المقدمة — ترخّصه قدرة <c>advance</c>.</summary>
    public const string AdvanceRecoveryField = "advanceRecovery";

    /// <summary>حقل المحتجز — ترخّصه قدرة <c>retention</c>.</summary>
    public const string RetentionField = "retention";

    private readonly ICapabilityProfileStore _profiles;

    /// <summary>ينشئ البوابة.</summary>
    /// <param name="profiles">مخزن ملفّات القدرات.</param>
    public ProjectsAdmission(ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles;
    }

    /// <summary>
    /// يعرض مستخلصاً بحقوله على ملفّ المستأجر، فيُرجع مستنداً <b>مقبولاً</b> أو يرفض.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="presentFields">الحقول التي يحملها المستند فعلاً.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AdmittedDocument>> AdmitCertificateAsync(
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
            return Result<AdmittedDocument>.Failure(ProjectsErrors.CapabilityProfileMissing(tenant));
        }

        return profile.Admit(new DocumentSubmission(new DocumentTypeCode(CertificateDocumentType), presentFields));
    }

    /// <summary>
    /// يتأكّد أن المستند المقبول <b>هو</b> المستند الذي يُنفَّذ الآن.
    /// <para>
    /// بدون هذا الفحص يصير <see cref="AdmittedDocument"/> تذكرةً عامّة: قبولٌ نُشئ
    /// لمستخلصٍ بلا قدرات يُمرَّر إلى مسار يمارس قدرة، فيبدو الأمر مقبولاً وهو لم يُعرض قطّ.
    /// </para>
    /// </summary>
    /// <param name="admitted">المستند المقبول.</param>
    /// <param name="field">الحقل الذي يمارسه هذا المسار.</param>
    public static Result EnsureCovers(AdmittedDocument admitted, string field)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return admitted.Fields.Contains(field, StringComparer.Ordinal)
            && string.Equals(admitted.DocumentType.Value, CertificateDocumentType, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(ProjectsErrors.AdmissionDoesNotCoverField(
                admitted.DocumentType.Value ?? string.Empty, field));
    }
}
