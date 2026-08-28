using System.Globalization;
using Babel.Api.Endpoints;
using Babel.Storage.Surface;

namespace Babel.Api.Wire;

/// <summary>
/// نقل المرفقات بين السطح المنشور والسلك — <b>موضع واحد</b>.
/// <para>
/// ولا حساب هنا ولا قرار: أسماء الحقول وصيغة اللحظة وبناء المسار وحدها. ومسارٌ يُبنى
/// في معالجين ينحرف في أحدهما، فيصير عميلٌ يقرأ عنواناً لا يوصل.
/// </para>
/// </summary>
internal static class AttachmentMapping
{
    /// <summary>يبني مسار المرفق نفسه من مساره المُعلن، لا من نصّ مكتوب بيد.</summary>
    /// <param name="companyId">الشركة.</param>
    /// <param name="attachmentId">المرفق.</param>
    public static string SelfPath(Guid companyId, Guid attachmentId) =>
        Fill(ApiRoutes.Attachment, companyId, attachmentId);

    /// <summary>يبني مسار تنزيل البايتات — ويحتاج تذكرة موقّعة في سلسلة استعلامه.</summary>
    /// <param name="companyId">الشركة.</param>
    /// <param name="attachmentId">المرفق.</param>
    public static string ContentPath(Guid companyId, Guid attachmentId) =>
        Fill(ApiRoutes.AttachmentContent, companyId, attachmentId);

    /// <summary>ينقل وصف مرفق إلى السلك.</summary>
    /// <param name="record">الوصف كما خرج من السطح المنشور.</param>
    /// <param name="companyId">الشركة — منها يُبنى المسار.</param>
    public static AttachmentDto ToDto(AttachmentRecord record, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AttachmentDto(
            Id(record.Id),
            record.MediaType,
            record.ByteLength,
            record.ContentHash,
            record.FileName,
            ContentPath(companyId, record.Id),
            Instant(record.StoredAt),
            Id(record.StoredBy),
            record.Version,
            record.Supersedes is { } predecessor ? Id(predecessor) : null,
            record.SupersededBy is { } successor ? Id(successor) : null,
            record.SourceDocumentType,
            record.SourceDocumentId is { } source ? Id(source) : null,
            record.Withdrawal is null
                ? null
                : new AttachmentWithdrawalDto(
                    Instant(record.Withdrawal.WithdrawnAt),
                    Id(record.Withdrawal.WithdrawnBy),
                    record.Withdrawal.ReasonKey));
    }

    /// <summary>ينقل صفحة جرد إلى السلك.</summary>
    /// <param name="inventory">الصفحة كما خرجت من السطح المنشور.</param>
    /// <param name="companyId">الشركة.</param>
    public static AttachmentPageDto ToDto(AttachmentInventory inventory, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return new AttachmentPageDto(
            [.. inventory.Items.Select(item => ToDto(item, companyId))],
            inventory.Total,
            inventory.Skip,
            inventory.Take);
    }

    /// <summary>ينقل تذكرة تنزيل إلى السلك، ومعها المسار الكامل الذي تُستهلك عليه.</summary>
    /// <param name="ticket">التذكرة.</param>
    /// <param name="companyId">الشركة.</param>
    public static AttachmentTicketDto ToDto(AttachmentAccessTicket ticket, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new AttachmentTicketDto(
            ticket.Token,
            Id(ticket.AttachmentId),
            Instant(ticket.ExpiresAt),
            ContentPath(companyId, ticket.AttachmentId) + "?ticket=" + ticket.Token);
    }

    private static string Fill(string template, Guid companyId, Guid attachmentId) => template
        .Replace("{companyId}", Id(companyId), StringComparison.Ordinal)
        .Replace("{attachmentId}", Id(attachmentId), StringComparison.Ordinal);

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>اللحظة بصيغة ISO 8601 الدوّارة بتوقيت UTC وبثقافة ثابتة — كما في سطح المصادقة.</summary>
    private static string Instant(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
