using System.Collections.Immutable;
using Babel.Core.CapabilityProfile;

namespace Babel.Api.Wire;

/// <summary>مفتاح قدرة واحد على السلك: الرمز وحالته.</summary>
/// <param name="Capability">رمز القدرة من المجموعة المغلقة.</param>
/// <param name="Enabled">مُشغَّلة أم لا.</param>
internal sealed record CapabilitySwitchDto(string Capability, bool Enabled);

/// <summary>ملفّ نوع مستند واحد كما يصل من العميل.</summary>
/// <param name="DocumentType">رمز نوع المستند من المجموعة المغلقة.</param>
/// <param name="Capabilities">مفاتيح القدرات.</param>
/// <param name="Defaults">القيم الافتراضية، ومفاتيحها حقول من شكل المستند حصراً.</param>
internal sealed record DocumentProfileDto(
    string DocumentType,
    IReadOnlyList<CapabilitySwitchDto> Capabilities,
    IReadOnlyList<NameValueDto>? Defaults = null);

/// <summary>طلب حفظ ملفّ القدرات.</summary>
/// <param name="Documents">أنواع المستندات.</param>
/// <param name="WithdrawalReason">
/// سبب سحب قدرة. إلزامي متى أطفأ الطلب قدرةً كانت مُشغَّلة، ومهمَل فيما عدا ذلك.
/// </param>
internal sealed record PutCapabilityProfileRequestDto(
    IReadOnlyList<DocumentProfileDto> Documents,
    string? WithdrawalReason = null);

/// <summary>شكل مستند مُشتقّاً — ما تبني عليه الشاشة.</summary>
/// <param name="DocumentType">رمز نوع المستند.</param>
/// <param name="NameAr">الاسم العربي — إلزامي، وهو الارتداد المضمون حين لا ترجمة (ADR-0021).</param>
/// <param name="NameKey">مفتاح الترجمة إلى أيّ عدد من اللغات. الإنجليزية واحدة منها لا نصف اثنتين.</param>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="AvailableCapabilities">كل قدرات هذا النوع في الكتالوج المغلق.</param>
/// <param name="EnabledCapabilities">المُشغَّل منها لهذا المستأجر.</param>
/// <param name="Fields">حقول المستند بهذا الملفّ.</param>
/// <param name="Defaults">القيم الافتراضية.</param>
internal sealed record DocumentShapeDto(
    string DocumentType,
    string NameAr,
    string NameKey,
    string Module,
    IReadOnlyList<string> AvailableCapabilities,
    IReadOnlyList<string> EnabledCapabilities,
    IReadOnlyList<string> Fields,
    IReadOnlyList<NameValueDto> Defaults);

/// <summary>ملفّ القدرات كاملاً بأشكاله المشتقّة.</summary>
/// <param name="Documents">الأشكال مرتَّبة بنوع المستند.</param>
internal sealed record CapabilityProfileDto(IReadOnlyList<DocumentShapeDto> Documents);

/// <summary>مستند يُعرض على الملفّ ليُقبل أو يُرفض: أسماء حقوله لا قيمها.</summary>
/// <param name="Fields">أسماء الحقول الموجودة على المستند.</param>
internal sealed record AdmitDocumentRequestDto(IReadOnlyList<string> Fields);

/// <summary>حكم القبول.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="Admitted">مقبول دائماً في الاستجابة الناجحة — والرفض يخرج مشكلةً لا حكماً.</param>
/// <param name="Fields">الحقول المقبولة مرتَّبة.</param>
internal sealed record DocumentAdmissionDto(string DocumentType, bool Admitted, IReadOnlyList<string> Fields);

/// <summary>
/// النقل بين السلك وبين نواة القدرات — <b>نقلٌ لا قرار</b>.
/// <para>
/// لا فحص هنا ولا حكم: التكرار والمفتاح المجهول والقيمة المرفوضة كلها تُرجع من النواة
/// برموزها. وما يقع هنا هو ترتيب ثابت وتحويل شكل، لا أكثر.
/// </para>
/// </summary>
internal static class CapabilityProfileWire
{
    /// <summary>يحوّل طلب الحفظ إلى مسودّة، ويرفض التكرار عند الحدّ لأن «أي القيمتين» سؤال بلا جواب.</summary>
    /// <param name="dto">الطلب.</param>
    public static CapabilityProfileDraft ToDraft(PutCapabilityProfileRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Dictionary<string, DocumentProfileDraft> documents = new(StringComparer.Ordinal);

        foreach (DocumentProfileDto document in dto.Documents)
        {
            Dictionary<string, bool> capabilities = new(StringComparer.Ordinal);

            foreach (CapabilitySwitchDto entry in document.Capabilities)
            {
                if (!capabilities.TryAdd(entry.Capability, entry.Enabled))
                {
                    throw WireNumbers.Reject(
                        "wire.body.repeated",
                        "capabilities",
                        $"مفتاح قدرة مكرَّر: «{entry.Capability}».",
                        $"A repeated capability switch: '{entry.Capability}'.");
                }
            }

            Dictionary<string, string> defaults = new(StringComparer.Ordinal);

            foreach (NameValueDto entry in document.Defaults ?? [])
            {
                if (!defaults.TryAdd(entry.Name, entry.Value))
                {
                    throw WireNumbers.Reject(
                        "wire.body.repeated",
                        "defaults",
                        $"قيمة افتراضية مكرَّرة للحقل «{entry.Name}».",
                        $"A repeated default for the field '{entry.Name}'.");
                }
            }

            if (!documents.TryAdd(document.DocumentType, new DocumentProfileDraft(capabilities, defaults)))
            {
                throw WireNumbers.Reject(
                    "wire.body.repeated",
                    "documents",
                    $"نوع مستند مكرَّر: «{document.DocumentType}».",
                    $"A repeated document type: '{document.DocumentType}'.");
            }
        }

        return new CapabilityProfileDraft(documents);
    }

    /// <summary>يحوّل ملفّاً صالحاً إلى شكله على السلك.</summary>
    /// <param name="profile">الملفّ.</param>
    public static CapabilityProfileDto ToDto(ValidatedCapabilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CapabilityProfileDto([.. profile.Shapes.Select(ToDto)]);
    }

    /// <summary>يحوّل شكل مستند إلى شكله على السلك.</summary>
    /// <param name="shape">الشكل المشتقّ.</param>
    public static DocumentShapeDto ToDto(DocumentShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return new DocumentShapeDto(
            shape.DocumentType.Value,
            shape.NameAr,
            shape.NameKey,
            shape.Module.ToString(),
            [.. shape.AvailableCapabilities.Select(static code => code.Value)],
            [.. shape.EnabledCapabilities.Select(static code => code.Value)],
            [.. shape.Fields],
            [.. shape.Defaults.Select(static entry => new NameValueDto(entry.Key, entry.Value))]);
    }

    /// <summary>يحوّل حكم القبول إلى شكله على السلك.</summary>
    /// <param name="admitted">المستند المقبول.</param>
    public static DocumentAdmissionDto ToDto(AdmittedDocument admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        return new DocumentAdmissionDto(admitted.DocumentType.Value, Admitted: true, [.. admitted.Fields]);
    }

    /// <summary>أسماء الحقول كما وصلت، بلا تكرار — والتكرار يُرفض لا يُطوى.</summary>
    /// <param name="dto">الطلب.</param>
    public static ImmutableArray<string> ToFields(AdmitDocumentRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HashSet<string> fields = new(StringComparer.Ordinal);

        foreach (string field in dto.Fields)
        {
            if (!fields.Add(field))
            {
                throw WireNumbers.Reject(
                    "wire.body.repeated",
                    "fields",
                    $"حقل مكرَّر: «{field}».",
                    $"A repeated field: '{field}'.");
            }
        }

        return [.. fields.Order(StringComparer.Ordinal)];
    }
}
