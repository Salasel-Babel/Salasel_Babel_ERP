using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.SharedKernel;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// أخطاء ملفّ القدرات — رمز ثابت ورسالتان، والرسالة العربية <b>تسمّي القدرة وما ينقصها</b>.
/// <para>
/// رسالة «الملفّ غير صالح» بلا اسم لا تُصلح شيئاً: من يقرؤها لا يعرف أي مفتاح يحذف ولا أي
/// حدث ينقص، فيجرّب. والرسائل هنا تُقرأ لمحاسب، فالعربية هي الأصل والإنجليزية معها.
/// </para>
/// </summary>
public static class CapabilityProfileErrors
{
    /// <summary>ملفّ بلا نوع مستند واحد.</summary>
    public static Error Empty { get; } = new(
        "capability_profile.empty",
        "ملفّ القدرات لا يحمل نوع مستند واحداً. والملفّ الفارغ ليس «كل شيء مسموح» ولا «لا شيء مسموح» — "
        + "بل غموضٌ يُقرأ يوماً بأحد المعنيين، فيُرفض هنا.",
        "The capability profile carries not one document type. An empty profile is neither 'everything allowed' "
        + "nor 'nothing allowed' but an ambiguity that will one day be read as either, so it is refused here.");

    /// <summary>نوع مستند ليس في الكتالوج المغلق.</summary>
    /// <param name="documentType">الرمز كما ورد.</param>
    public static Error UnknownDocumentType(string documentType) => new(
        "capability_profile.document_type_unknown",
        $"نوع مستند غير معروف: «{documentType}». والمجموعة مغلقة عمداً — "
        + $"المعروف: {Known(CapabilityCatalogue.DocumentTypes.Select(static d => d.Code.Value))}.",
        $"Unknown document type: '{documentType}'. The set is closed by design; known types: "
        + $"{Known(CapabilityCatalogue.DocumentTypes.Select(static d => d.Code.Value))}.");

    /// <summary>قدرة ليست من قدرات نوع المستند.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="capability">رمز القدرة كما ورد.</param>
    /// <param name="definition">تعريف نوع المستند.</param>
    public static Error UnknownCapability(string documentType, string capability, DocumentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        string known = Known(definition.Capabilities.Select(static c => c.Code.Value));

        return new Error(
            "capability_profile.capability_unknown",
            $"قدرة غير معروفة على «{documentType}»: «{capability}». وقدرات هذا النوع: {known}.",
            $"Unknown capability on '{documentType}': '{capability}'. The capabilities of this type are: {known}.");
    }

    /// <summary>
    /// قدرة مُشغَّلة لا تخدمها المصفوفة. <b>هذا هو الخطأ الذي يوجد هذا التصميم كلّه من أجله.</b>
    /// </summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="capability">تعريف القدرة.</param>
    /// <param name="missing">الأحداث الناقصة.</param>
    public static Error CapabilityNotServedByMatrix(
        string documentType,
        CapabilityDefinition capability,
        ImmutableArray<PostingEventCode> missing)
    {
        ArgumentNullException.ThrowIfNull(capability);
        string codes = string.Join(" · ", missing.Select(static code => code.Value));

        return new Error(
            "capability_profile.capability_not_served_by_matrix",
            $"القدرة «{capability.NameAr}» ({capability.Code.Value}) على «{documentType}» مُشغَّلة، "
            + $"ولا تقابلها أحداث في مصفوفة الترحيل: {codes}. "
            + "وتشغيل قدرة بلا حدث يعني مستنداً يجمع أرقاماً لا يقابلها قيد — "
            + "يُكتشف بعد شهر دفترَ أستاذ مساعد لا يُطابَق. تُضاف بيانات الحدث إلى المصفوفة، ولا يُضعَّف هذا الفحص.",
            $"The capability '{capability.Code.Value}' on '{documentType}' is enabled "
            + $"but the posting matrix carries no event for it: {codes}. "
            + "Enabling a capability with no event means a document that collects figures no entry answers — "
            + "discovered a month later as a subledger that will not tie. Add the event data; never weaken this check.");
    }

    /// <summary>الحدث الأساسي لنوع المستند غير موجود في المصفوفة.</summary>
    /// <param name="definition">تعريف نوع المستند.</param>
    public static Error DocumentTypeNotServedByMatrix(DocumentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new Error(
            "capability_profile.document_type_not_served_by_matrix",
            $"نوع المستند «{definition.NameAr}» ({definition.Code.Value}) لا يقابله حدث في مصفوفة الترحيل: "
            + $"{definition.BaseEvent.Value}. ولا يُفتح لمستأجر مستندٌ لا يعرف الدفتر كيف يرحّله.",
            $"The document type '{definition.Code.Value}' has no event in the posting "
            + $"matrix: {definition.BaseEvent.Value}. A document the ledger cannot post is not opened for a tenant.");
    }

    /// <summary>قيمة افتراضية لحقل ليس في الشكل المشتق.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="field">اسم الحقل.</param>
    /// <param name="shape">الحقول المتاحة فعلاً.</param>
    public static Error DefaultFieldNotInShape(string documentType, string field, ImmutableArray<string> shape) => new(
        "capability_profile.default_field_not_in_shape",
        $"قيمة افتراضية لحقل «{field}» غير موجود على «{documentType}» بهذا الملفّ. "
        + $"والحقول المتاحة: {Known(shape)}. القيمة الافتراضية لحقل مُطفأ تبقى في البيانات بلا شاشة تقرؤها، "
        + "ثم تُقرأ يوم يُشغَّل الحقل بقيمة لم يقصدها أحد.",
        $"A default for the field '{field}' which this profile does not put on '{documentType}'. "
        + $"Available fields: {Known(shape)}. A default for a disabled field lingers in the data with no screen to "
        + "read it, then gets read the day the field is enabled — with a value nobody chose.");

    /// <summary>قيمة افتراضية مرفوضة شكلاً.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="field">اسم الحقل.</param>
    public static Error DefaultValueMalformed(string documentType, string field) => new(
        "capability_profile.default_value_malformed",
        $"قيمة افتراضية مرفوضة للحقل «{field}» على «{documentType}»: تُقبل قيمة غير فارغة، بلا محارف تحكّم، "
        + $"وطولها {ProfileLimits.MaximumDefaultLength} محرفاً على الأكثر.",
        $"A refused default for the field '{field}' on '{documentType}': a non-empty value is required, with no "
        + $"control characters, at most {ProfileLimits.MaximumDefaultLength} characters long.");

    /// <summary>سحب قدرة بلا إقرار صريح.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="withdrawn">القدرات المسحوبة.</param>
    public static Error WithdrawalRequiresAcknowledgement(string documentType, ImmutableArray<string> withdrawn) => new(
        "capability_profile.capability_withdrawal_requires_acknowledgement",
        $"سحبُ قدرة على «{documentType}» ({Known(withdrawn)}) يحتاج إقراراً صريحاً بسببه. "
        + "الاتجاه الخطر هو الإطفاء لا التشغيل: مستندٌ مفتوح يحمل حقل القدرة يصير غير مقبول، "
        + "وحدثُ المتابعة الذي يُخلي رصيد الدفتر المساعد يصير غير قابل للوقوع — فيبقى رصيد لا يُخلَّص. "
        + "أَقفِل المستندات المفتوحة أولاً، ثم اسحب القدرة بإقرار يُسجَّل في سجل التدقيق.",
        $"Withdrawing a capability on '{documentType}' ({Known(withdrawn)}) requires an explicit acknowledgement "
        + "with a reason. The dangerous direction is off, not on: an open document carrying the capability's field "
        + "becomes inadmissible, and the follow-on event that relieves the subledger balance becomes unreachable — "
        + "leaving a balance nothing can clear. Close the open documents first, then withdraw with an acknowledgement "
        + "recorded in the audit log.");

    /// <summary>مستأجر بلا ملفّ قدرات.</summary>
    public static Error ProfileNotFound { get; } = new(
        "capability_profile.not_found",
        "لا ملفّ قدرات لهذا المستأجر. ولا يُفترض ملفّ ضمني: مستأجر بلا ملفّ لا شاشة له، "
        + "وهذا أوضح من شاشةٍ تُخمَّن.",
        "This tenant has no capability profile. No implicit profile is assumed: a tenant without a profile has no "
        + "screen, and that is clearer than a guessed one.");

    /// <summary>نوع مستند ليس في ملفّ المستأجر.</summary>
    /// <param name="documentType">نوع المستند.</param>
    public static Error DocumentTypeNotInProfile(string documentType) => new(
        "document_admission.document_type_not_in_profile",
        $"نوع المستند «{documentType}» ليس في ملفّ قدرات هذا المستأجر — ولا يُفتح ضمناً.",
        $"The document type '{documentType}' is not in this tenant's capability profile, and is not opened implicitly.");

    /// <summary>حقل لا يعرفه الكتالوج لهذا النوع.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="field">اسم الحقل.</param>
    public static Error FieldUnknown(string documentType, string field) => new(
        "document_admission.field_unknown",
        $"حقل غير معروف على «{documentType}»: «{field}». والتجاهل الصامت يجعل المُرسِل يظنّ أنه أرسل ما لم يصل.",
        $"Unknown field on '{documentType}': '{field}'. Silently ignoring it makes the sender believe it sent "
        + "something that never arrived.");

    /// <summary>حقل ترخّصه قدرة مُطفأة على هذا المستأجر.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="field">اسم الحقل.</param>
    /// <param name="capability">القدرة التي ترخّصه.</param>
    public static Error CapabilityNotEnabled(string documentType, string field, CapabilityDefinition capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return new Error(
            "document_admission.capability_not_enabled",
            $"المستند يحمل الحقل «{field}» على «{documentType}»، وهو حقل القدرة «{capability.NameAr}» "
            + $"({capability.Code.Value}) وهي غير مُشغَّلة لهذا المستأجر. "
            + "وقدرةٌ يمكن ممارستها بإرسال الحقل رغم إطفائها ليست قدرة بل زينة.",
            $"The document carries the field '{field}' on '{documentType}', which belongs to the capability "
            + $"'{capability.Code.Value}' and that capability is not enabled for this "
            + "tenant. A capability that can still be exercised by sending the field anyway is decoration, not a capability.");
    }

    private static string Known(IEnumerable<string> values)
        => string.Join(" · ", values.Order(StringComparer.Ordinal));
}

/// <summary>حدود شكلية معلنة مرّة واحدة — يقرؤها التحقّق والعقد المنشور معاً.</summary>
public static class ProfileLimits
{
    /// <summary>أقصى طول لقيمة افتراضية.</summary>
    public const int MaximumDefaultLength = 64;

    /// <summary>أقصى طول لسبب سحب قدرة.</summary>
    public const int MaximumReasonLength = 512;

    /// <summary>أدنى طول لسبب سحب قدرة — «لا سبب» ليس سبباً.</summary>
    public const int MinimumReasonLength = 8;
}
