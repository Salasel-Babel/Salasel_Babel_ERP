using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// <b>ملفّ قدرات مستأجر، صالحاً بحكم وجوده.</b>
/// <para>
/// لا مُنشئ عام، ولا مُهيّئ خصائص، ولا مصنع ثانٍ: الطريق الوحيد إلى قيمة من هذا النوع هو
/// <see cref="Create(CapabilityProfileDraft, IPostingEventDirectory)"/>، وهي تُطابق كل قدرة
/// مُشغَّلة بمصفوفة الترحيل قبل أن تُرجع شيئاً. ولذلك <b>لا يوجد في الشجرة مسارٌ يُخزَّن فيه
/// ملفٌّ لم يُطابَق</b>: المخزن نفسه لا يقبل إلا هذا النوع، فلا يعتمد الفحص على انضباط
/// المستدعي — وهو بالضبط صنف العطل الذي يتكرر هنا: فحصٌ يؤدّيه مستدعٍ واحد.
/// </para>
/// <para>
/// وسبب الحكم <b>عند الحفظ</b> لا عند أول مستند: القدرة التي لا يخدمها حدث تُنتج مستنداً
/// يجمع أرقاماً لا يقابلها قيد، ولا يظهر ذلك إلا بعد شهر — دفترَ أستاذ مساعد لا يُطابَق.
/// وهي من عائلة العطلين اللذين مرّ بهما الدفتر: رمز حدث <b>فارغ</b> جعل واقعتين محاسبيتين
/// هويةً واحدة، ورمز حدث <b>مخترَع</b> جعل واقعة واحدة هويتين.
/// </para>
/// </summary>
public sealed class ValidatedCapabilityProfile
{
    private readonly ImmutableSortedDictionary<string, DocumentShape> _shapes;

    private ValidatedCapabilityProfile(ImmutableSortedDictionary<string, DocumentShape> shapes) => _shapes = shapes;

    /// <summary>أشكال المستندات المشتقّة، مرتَّبة بنوع المستند ترتيباً حرفياً ثابتاً.</summary>
    public ImmutableArray<DocumentShape> Shapes => [.. _shapes.Values];

    /// <summary>أنواع المستندات في هذا الملفّ.</summary>
    public ImmutableArray<DocumentTypeCode> DocumentTypes => [.. _shapes.Keys.Select(static key => new DocumentTypeCode(key))];

    /// <summary>
    /// يبني ملفّاً صالحاً من مسودّة، أو يُرجع <b>كل</b> أسباب الرفض مجتمعة.
    /// <para>
    /// وكل الأسباب لا أوّلها عمداً: من يصلح مفتاحاً ليكتشف التالي يظنّ أن العدد اثنان
    /// وهو خمسة، فيصلح خمس مرات ويُحمّل خمس مرات.
    /// </para>
    /// </summary>
    /// <param name="draft">المسودّة الواصلة.</param>
    /// <param name="directory">فهرس أحداث المصفوفة الذي يُطابَق به.</param>
    public static Result<ValidatedCapabilityProfile> Create(
        CapabilityProfileDraft draft,
        IPostingEventDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(directory);

        List<Error> errors = [];

        if (draft.Documents.Count == 0)
        {
            errors.Add(CapabilityProfileErrors.Empty);
        }

        ImmutableSortedDictionary<string, DocumentShape>.Builder shapes =
            ImmutableSortedDictionary.CreateBuilder<string, DocumentShape>(StringComparer.Ordinal);

        foreach (string documentType in draft.Documents.Keys.Order(StringComparer.Ordinal))
        {
            DocumentTypeDefinition? definition = CapabilityCatalogue.Find(new DocumentTypeCode(documentType));

            if (definition is null)
            {
                errors.Add(CapabilityProfileErrors.UnknownDocumentType(documentType));
                continue;
            }

            if (!directory.Contains(definition.BaseEvent))
            {
                errors.Add(CapabilityProfileErrors.DocumentTypeNotServedByMatrix(definition));
            }

            DocumentShape? shape = BuildShape(draft.Documents[documentType], definition, directory, errors);

            if (shape is not null)
            {
                shapes[documentType] = shape;
            }
        }

        return errors.Count > 0
            ? Result<ValidatedCapabilityProfile>.Failure(errors)
            : Result<ValidatedCapabilityProfile>.Success(new ValidatedCapabilityProfile(shapes.ToImmutable()));
    }

    /// <summary>الشكل المشتقّ لنوع مستند، أو <c>null</c> إن لم يكن في هذا الملفّ.</summary>
    /// <param name="documentType">نوع المستند.</param>
    public DocumentShape? ShapeOf(DocumentTypeCode documentType)
        => _shapes.TryGetValue(documentType.Value ?? string.Empty, out DocumentShape? shape) ? shape : null;

    /// <summary>هل القدرة مُشغَّلة على هذا النوع لهذا المستأجر؟</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="capability">رمز القدرة.</param>
    public bool IsEnabled(DocumentTypeCode documentType, CapabilityCode capability)
        => ShapeOf(documentType)?.EnabledCapabilities.Contains(capability) == true;

    /// <summary>
    /// <b>يقبل مستنداً أو يرفضه.</b> حقلٌ ترخّصه قدرة مُطفأة يُرفض به المستند كلّه —
    /// لأن قدرةً يمكن ممارستها بإرسال الحقل رغم إطفائها ليست قدرة بل زينة.
    /// </summary>
    /// <param name="submission">المستند المقدَّم.</param>
    public Result<AdmittedDocument> Admit(DocumentSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        DocumentShape? shape = ShapeOf(submission.DocumentType);

        if (shape is null)
        {
            return Result<AdmittedDocument>.Failure(
                CapabilityProfileErrors.DocumentTypeNotInProfile(submission.DocumentType.Value ?? string.Empty));
        }

        DocumentTypeDefinition definition = CapabilityCatalogue.Find(submission.DocumentType)!;
        List<Error> errors = [];

        foreach (string field in submission.PresentFields.Order(StringComparer.Ordinal))
        {
            if (shape.Fields.Contains(field, StringComparer.Ordinal))
            {
                continue;
            }

            CapabilityDefinition? owner = definition.Capabilities
                .FirstOrDefault(capability => capability.Fields.Contains(field, StringComparer.Ordinal));

            errors.Add(owner is null
                ? CapabilityProfileErrors.FieldUnknown(submission.DocumentType.Value ?? string.Empty, field)
                : CapabilityProfileErrors.CapabilityNotEnabled(
                    submission.DocumentType.Value ?? string.Empty, field, owner));
        }

        return errors.Count > 0
            ? Result<AdmittedDocument>.Failure(errors)
            : Result<AdmittedDocument>.Success(new AdmittedDocument(
                submission.DocumentType,
                [.. submission.PresentFields.Order(StringComparer.Ordinal)]));
    }

    /// <summary>
    /// القدرات التي يسحبها هذا الملفّ مقارنةً بسابقه — أي المُشغَّلة هناك والمُطفأة هنا،
    /// ومعها أنواع المستندات التي اختفت كلياً.
    /// </summary>
    /// <param name="previous">الملفّ السابق.</param>
    public ImmutableArray<CapabilityWithdrawal> WithdrawalsAgainst(ValidatedCapabilityProfile previous)
    {
        ArgumentNullException.ThrowIfNull(previous);

        List<CapabilityWithdrawal> withdrawals = [];

        foreach (DocumentShape before in previous.Shapes)
        {
            DocumentShape? after = ShapeOf(before.DocumentType);

            ImmutableArray<CapabilityCode> lost = after is null
                ? before.EnabledCapabilities
                : [.. before.EnabledCapabilities.Where(code => !after.EnabledCapabilities.Contains(code))];

            if (lost.Length > 0 || after is null)
            {
                withdrawals.Add(new CapabilityWithdrawal(before.DocumentType, lost, DocumentTypeRemoved: after is null));
            }
        }

        return [.. withdrawals.OrderBy(static withdrawal => withdrawal.DocumentType.Value, StringComparer.Ordinal)];
    }

    private static DocumentShape? BuildShape(
        DocumentProfileDraft draft,
        DocumentTypeDefinition definition,
        IPostingEventDirectory directory,
        List<Error> errors)
    {
        string documentType = definition.Code.Value;
        int before = errors.Count;

        List<CapabilityCode> enabled = [];

        foreach (string capabilityCode in draft.Capabilities.Keys.Order(StringComparer.Ordinal))
        {
            CapabilityDefinition? capability = definition.Find(new CapabilityCode(capabilityCode));

            if (capability is null)
            {
                errors.Add(CapabilityProfileErrors.UnknownCapability(documentType, capabilityCode, definition));
                continue;
            }

            if (!draft.Capabilities[capabilityCode])
            {
                continue;
            }

            ImmutableArray<PostingEventCode> missing =
                [.. capability.RequiredEvents.Where(code => !directory.Contains(code))];

            if (missing.Length > 0)
            {
                errors.Add(CapabilityProfileErrors.CapabilityNotServedByMatrix(documentType, capability, missing));
                continue;
            }

            enabled.Add(capability.Code);
        }

        ImmutableArray<string> fields =
        [
            .. definition.BaseFields
                .Concat(definition.Capabilities
                    .Where(capability => enabled.Contains(capability.Code))
                    .SelectMany(static capability => capability.Fields))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        ImmutableSortedDictionary<string, string>.Builder defaults =
            ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (string field in draft.Defaults.Keys.Order(StringComparer.Ordinal))
        {
            string value = draft.Defaults[field];

            if (!fields.Contains(field, StringComparer.Ordinal))
            {
                errors.Add(CapabilityProfileErrors.DefaultFieldNotInShape(documentType, field, fields));
                continue;
            }

            if (!IsAcceptableDefault(value))
            {
                errors.Add(CapabilityProfileErrors.DefaultValueMalformed(documentType, field));
                continue;
            }

            defaults[field] = value;
        }

        return errors.Count > before
            ? null
            : new DocumentShape(
                definition.Code,
                definition.NameAr,
                definition.NameKey,
                definition.Module,
                [.. definition.Capabilities.Select(static capability => capability.Code)],
                [.. enabled.OrderBy(static code => code.Value, StringComparer.Ordinal)],
                fields,
                defaults.ToImmutable());
    }

    private static bool IsAcceptableDefault(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= ProfileLimits.MaximumDefaultLength
            && !value.Any(char.IsControl);
}

/// <summary>سحبُ قدرات على نوع مستند — الاتجاه الخطر في تغيير الملفّ.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="Capabilities">القدرات المسحوبة.</param>
/// <param name="DocumentTypeRemoved">هل اختفى نوع المستند كلياً من الملفّ؟</param>
public sealed record CapabilityWithdrawal(
    DocumentTypeCode DocumentType,
    ImmutableArray<CapabilityCode> Capabilities,
    bool DocumentTypeRemoved);
