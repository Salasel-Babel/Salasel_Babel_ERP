using System.Collections.Immutable;

namespace Babel.Core.CapabilityProfile;

/// <summary>مستند مقدَّم للقبول: نوعه، وأسماء الحقول التي يحملها فعلاً.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="PresentFields">أسماء الحقول الموجودة على المستند.</param>
public sealed record DocumentSubmission(DocumentTypeCode DocumentType, IReadOnlyCollection<string> PresentFields);

/// <summary>
/// مستند <b>قُبل</b> مقابل ملفّ قدرات مستأجر بعينه.
/// <para>
/// <b>لا مُنشئ عام ولا مصنع ثانٍ:</b> الطريق الوحيد إلى قيمة من هذا النوع هو
/// <see cref="ValidatedCapabilityProfile.Admit(DocumentSubmission)"/>. ومن يطلب هذا النوع
/// في توقيعه يكون قد <b>فرض</b> مرور القبول بنيوياً، لا اتفاقاً يُنسى في المستدعي الثاني.
/// </para>
/// </summary>
public sealed class AdmittedDocument
{
    internal AdmittedDocument(DocumentTypeCode documentType, ImmutableArray<string> fields)
    {
        DocumentType = documentType;
        Fields = fields;
    }

    /// <summary>نوع المستند المقبول.</summary>
    public DocumentTypeCode DocumentType { get; }

    /// <summary>حقول المستند مرتَّبة ترتيباً حرفياً ثابتاً.</summary>
    public ImmutableArray<string> Fields { get; }
}
