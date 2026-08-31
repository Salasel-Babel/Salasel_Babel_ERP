using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Storage;

/// <summary>
/// <b>ربط المرفق بمستنده — نصفٌ منه ليس ربطاً.</b>
/// <para>
/// الفحص هنا لا في المحوّل الواحد: محوّلان يفحصان الشكل نفسه في موضعين ينحرفان عند أول
/// تعديل، فيقبل أحدهما ما يرفضه الآخر ويصير سلوك النظام تابعاً لأي محوّل رُكِّب — وهو
/// بالضبط ما لا يجوز أن يقوله منفذ.
/// </para>
/// <para>
/// <b>والرمز رمزٌ لا نصٌّ معروض:</b> يُرشَّح به الجرد ويُقارَن بالتساوي، فلا يحمل مسافة
/// ولا حرفاً كبيراً ولا محرفاً غير لاتيني — وشكلُه هو شكل رموز أنواع المستندات في
/// الكتالوج نفسه، لا شكلاً ثانياً يُخترع هنا.
/// </para>
/// </summary>
public static class SourceDocumentLink
{
    /// <summary>أقصى طول لرمز نوع المستند المصدر.</summary>
    public const int MaximumTypeLength = 64;

    /// <summary>
    /// يتحقّق من أن الربط كامل وشكله مقبول، أو غائب كاملاً.
    /// </summary>
    /// <param name="documentType">رمز نوع المستند، أو <c>null</c>.</param>
    /// <param name="documentId">معرّف المستند، أو <c>null</c>.</param>
    /// <returns>نجاحٌ بلا قيمة، أو خطأً باسمه.</returns>
    public static Result<bool> Check(string? documentType, Guid? documentId)
    {
        bool hasType = !string.IsNullOrEmpty(documentType);
        bool hasId = documentId is { } id && id != Guid.Empty;

        if (hasType != hasId)
        {
            return Result<bool>.Failure(AttachmentErrors.SourceDocumentIncomplete);
        }

        if (!hasType)
        {
            return Result<bool>.Success(false);
        }

        return IsWellFormed(documentType!)
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(AttachmentErrors.SourceDocumentTypeRefused(documentType!));
    }

    /// <summary>هل الرمز مكتوب بالشكل المقبول؟</summary>
    /// <param name="documentType">الرمز.</param>
    public static bool IsWellFormed(string documentType)
    {
        ArgumentNullException.ThrowIfNull(documentType);

        return documentType.Length is > 0 and <= MaximumTypeLength
            && documentType.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_');
    }

    /// <summary>يتحقّق من مدى الصفحة، ويرفض ما يتجاوز السقف <b>ولا يقصّه</b>.</summary>
    /// <param name="query">السؤال.</param>
    public static Result<bool> CheckPage(AttachmentQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // **يُرفض ولا يُقصّ**: القصّ الصامت يجعل المستدعي يظنّ أنه طلب ألفاً وقد طلب مئة،
        // فيقرأ صفحةً واحدة ويحسب أن الجرد انتهى — وهو الدرس نفسه الذي يحكم سقف التذكرة.
        return query.Skip >= 0 && query.Take is > 0 and <= AttachmentQuery.MaximumPageSize
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(
                AttachmentErrors.PageRefused(query.Skip, query.Take, AttachmentQuery.MaximumPageSize));
    }
}
