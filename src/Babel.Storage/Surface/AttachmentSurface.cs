using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Storage.Surface;

/// <summary>
/// <b>السطح المنشور للمرفقات</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// <b>والشكل مأخوذ حرفياً من ADR-0044:</b> صنفٌ واحد يُنادى، وأنواع نقل، ولا سياق قاعدة
/// بيانات ولا صفّ استمرارية ولا مفتاح كائن. والسبب هو السبب نفسه: طبقة HTTP لا تعرف
/// <c>FileSystemAttachmentStore</c> ولا <c>StorageDbContext</c> — تعرف هذا الصنف وحده،
/// فيبقى تبديل المحوّل (قرصٌ اليوم، مخزنٌ كائنيّ غداً) بلا حرف واحد في السطح.
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الصنف — عمداً:</b> لا يشمّ، ولا يجزّئ، ولا يطهّر اسماً، ولا يقرّر
/// سقفاً. كل ذلك في المنفذ ومحوّله حيث يُختبَر على PostgreSQL حقيقية؛ وتكراره هنا كان
/// سيصنع فحصين ينحرف أحدهما فيقبل السطح ما يرفضه المخزن أو العكس.
/// </para>
/// <para>
/// <b>وثلاث طبقات مستأجر لا واحدة</b> (‏ADR-0046 §5): المستأجر معامِلٌ إلزامي في كل
/// دالّة هنا · والتذكرة تحمل مستأجرها <b>داخل</b> بايتاتها الموقّعة · والمخزن يقرأ
/// بمفتاح مركّب من المستأجر والمعرّف معاً. وتذكرةٌ من مستأجر آخر تسقط عند الطبقة
/// الثانية بـ<c>storage.attachment_not_found</c> — <b>لا بمنعٍ</b>: المنع يُخبر السائل
/// بوجود ما لا يخصّه.
/// </para>
/// </summary>
public sealed class AttachmentSurface
{
    private readonly IAttachmentStore _store;
    private readonly IAttachmentTickets _tickets;
    private readonly StorageOptions _options;

    /// <summary>ينشئ السطح فوق المنفذ ومُصدِر التذاكر.</summary>
    /// <param name="store">منفذ المخزن.</param>
    /// <param name="tickets">منفذ التذاكر.</param>
    /// <param name="options">الإعدادات — ومنها السقف وسقف عمر التذكرة.</param>
    public AttachmentSurface(IAttachmentStore store, IAttachmentTickets tickets, StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _tickets = tickets;
        _options = options;
    }

    /// <summary>
    /// السقف المطلق لحجم مرفق واحد بالبايت — <b>يُقرأ عند الحدّ قبل أن يُقرأ جسمٌ</b>،
    /// كي يكون الرفض 413 برسالتيه لا استثناءً من الخادم.
    /// </summary>
    public long MaximumBytes => _options.MaximumBytes;

    /// <summary>سقف عمر تذكرة التنزيل. طلبٌ يتجاوزه <b>يُرفض ولا يُقصّ</b>.</summary>
    public TimeSpan TicketLifetimeCap => _options.TicketLifetimeCap;

    /// <summary>يودِع مرفقاً جديداً — إصدارُه الأول، بلا سلف.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="deposit">ما يُقدَّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<AttachmentRecord>> DepositAsync(
        TenantId tenant,
        UserId actor,
        AttachmentDeposit deposit,
        CancellationToken cancellationToken = default) =>
        PutAsync(tenant, actor, deposit, AttachmentId.None, cancellationToken);

    /// <summary>
    /// يودِع <b>إصداراً جديداً</b> يشير إلى سلفه. لا <c>PUT</c> ولا كتابة فوق:
    /// السجلّ يُضاف إليه، والتصحيح صفٌّ يشير إلى ما قبله.
    /// <para>
    /// <b>والسلسلة خطّية تفرضها القاعدة لا هذه الدالّة</b>: فهرس فريد جزئي على السلف
    /// يرفض الثاني، فيصل الرفض <c>storage.attachment_already_superseded</c> لا استثناءً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="predecessor">السلف الذي يُصحَّح.</param>
    /// <param name="deposit">ما يُقدَّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<AttachmentRecord>> ReviseAsync(
        TenantId tenant,
        UserId actor,
        Guid predecessor,
        AttachmentDeposit deposit,
        CancellationToken cancellationToken = default) =>
        PutAsync(tenant, actor, deposit, new AttachmentId(predecessor), cancellationToken);

    /// <summary>يقرأ الوصف وحده — <b>بلا بايتة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="attachment">المرفق.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AttachmentRecord>> DescribeAsync(
        TenantId tenant,
        Guid attachment,
        CancellationToken cancellationToken = default)
    {
        Result<StoredAttachment> found = await _store
            .DescribeAsync(tenant, new AttachmentId(attachment), cancellationToken)
            .ConfigureAwait(false);

        return found.IsFailure
            ? Result<AttachmentRecord>.Failure(found.Errors)
            : Result<AttachmentRecord>.Success(Record(found.Value));
    }

    /// <summary>يجرد مرفقات المستأجر، مرشَّحةً على المستند المصدر ومصفَّحةً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="sourceDocumentType">رمز نوع المستند المصدر، أو <c>null</c>.</param>
    /// <param name="sourceDocumentId">معرّف المستند المصدر، أو <c>null</c>.</param>
    /// <param name="skip">عدد الصفوف المتخطّاة.</param>
    /// <param name="take">حجم الصفحة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AttachmentInventory>> ListAsync(
        TenantId tenant,
        string? sourceDocumentType,
        Guid? sourceDocumentId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        Result<AttachmentPage> page = await _store
            .ListAsync(
                new AttachmentQuery
                {
                    Tenant = tenant,
                    SourceDocumentType = sourceDocumentType,
                    SourceDocumentId = sourceDocumentId,
                    Skip = skip,
                    Take = take,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return page.IsFailure
            ? Result<AttachmentInventory>.Failure(page.Errors)
            : Result<AttachmentInventory>.Success(new AttachmentInventory(
                [.. page.Value.Items.Select(Record)],
                page.Value.Total,
                page.Value.Skip,
                page.Value.Take));
    }

    /// <summary>
    /// يضع علامة سحب — <b>لا حذف</b>. صفٌّ في جدول ثانٍ، والبايتات والبصمة باقيتان.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل — إنسان يسحب.</param>
    /// <param name="attachment">المرفق.</param>
    /// <param name="reasonKey">مفتاح السبب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AttachmentRecord>> WithdrawAsync(
        TenantId tenant,
        UserId actor,
        Guid attachment,
        string reasonKey,
        CancellationToken cancellationToken = default)
    {
        Result<StoredAttachment> withdrawn = await _store
            .WithdrawAsync(tenant, new AttachmentId(attachment), actor, reasonKey, cancellationToken)
            .ConfigureAwait(false);

        return withdrawn.IsFailure
            ? Result<AttachmentRecord>.Failure(withdrawn.Errors)
            : Result<AttachmentRecord>.Success(Record(withdrawn.Value));
    }

    /// <summary>
    /// يسكّ تذكرة تنزيل قصيرة الأجل.
    /// <para>
    /// <b>والوجود يُتحقَّق منه أولاً داخل المستأجر</b>: تذكرةٌ تُسَكّ لمعرّفٍ لا وجود له
    /// تُنتج بابين يقولان قولين — سكٌّ ناجح ثم تنزيلٌ يردّ 404 — فيتعلّم السائل من
    /// الفرق بينهما شيئاً عن مستأجرٍ آخر.
    /// </para>
    /// </summary>
    /// <param name="tenant">مستأجر الجلسة.</param>
    /// <param name="bearer">الحامل الذي تصدر له.</param>
    /// <param name="attachment">المرفق.</param>
    /// <param name="lifetime">العمر المطلوب — يُرفض إن تجاوز السقف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AttachmentAccessTicket>> IssueTicketAsync(
        TenantId tenant,
        UserId bearer,
        Guid attachment,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        AttachmentId id = new(attachment);

        Result<StoredAttachment> found = await _store.DescribeAsync(tenant, id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<AttachmentAccessTicket>.Failure(found.Errors);
        }

        Result<AttachmentTicket> issued = _tickets.Issue(tenant, id, bearer, lifetime);

        return issued.IsFailure
            ? Result<AttachmentAccessTicket>.Failure(issued.Errors)
            : Result<AttachmentAccessTicket>.Success(
                new AttachmentAccessTicket(issued.Value.Token, issued.Value.Id.Value, issued.Value.ExpiresAt));
    }

    /// <summary>
    /// يستهلك تذكرة ويعيد البايتات ووصفها — <b>بعد أن يفحص المخزنُ البصمةَ قبل التسليم</b>.
    /// <para>
    /// والترتيب هو المهمّ، وهو نصّ ADR-0046 §8: يُتحقَّق من التوقيع، ثم من الانتهاء، ثم
    /// <b>يُقارَن مستأجر التذكرة بمستأجر الجلسة</b>، ثم يُنادى المخزن بمستأجر <b>الجلسة</b>
    /// لا بمستأجر التذكرة. فلو سُرّبت تذكرة كاملة واستُعملت في جلسة أخرى سقطت هنا؛ ولو
    /// سقطت هذه المقارنة سهواً سقط النداء عند المخزن لأن المستأجر جزء من المفتاح هناك.
    /// </para>
    /// </summary>
    /// <param name="sessionTenant">مستأجر الجلسة — وهو ما يُنادى به المخزن.</param>
    /// <param name="token">الرمز الموقّع.</param>
    /// <param name="attachment">المرفق كما ورد في المسار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<AttachmentBytes>> OpenAsync(
        TenantId sessionTenant,
        string token,
        Guid attachment,
        CancellationToken cancellationToken = default)
    {
        Result<RedeemedTicket> redeemed = _tickets.Redeem(token);
        if (redeemed.IsFailure)
        {
            return Result<AttachmentBytes>.Failure(redeemed.Errors);
        }

        // **مستأجرٌ آخر، أو مرفقٌ آخر: «غير موجود» لا «ممنوع».** والرمزان يفترقان في
        // ما يُخبران به السائل: «ممنوع» تُثبت أن الملفّ موجود عند غيره.
        if (redeemed.Value.Tenant != sessionTenant || redeemed.Value.Id.Value != attachment)
        {
            return Result<AttachmentBytes>.Failure(AttachmentErrors.NotFound(new AttachmentId(attachment)));
        }

        Result<AttachmentContent> content = await _store
            .OpenAsync(sessionTenant, redeemed.Value.Id, cancellationToken)
            .ConfigureAwait(false);

        return content.IsFailure
            ? Result<AttachmentBytes>.Failure(content.Errors)
            : Result<AttachmentBytes>.Success(
                new AttachmentBytes(Record(content.Value.Descriptor), content.Value.Content));
    }

    private async ValueTask<Result<AttachmentRecord>> PutAsync(
        TenantId tenant,
        UserId actor,
        AttachmentDeposit deposit,
        AttachmentId supersedes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deposit);

        Result<StoredAttachment> stored = await _store
            .PutAsync(
                new AttachmentSubmission
                {
                    Tenant = tenant,
                    Actor = actor,
                    Content = deposit.Content,
                    DeclaredFileName = deposit.DeclaredFileName,
                    DeclaredMediaType = deposit.DeclaredMediaType,
                    SourceDocumentType = deposit.SourceDocumentType,
                    SourceDocumentId = deposit.SourceDocumentId,
                    Supersedes = supersedes,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return stored.IsFailure
            ? Result<AttachmentRecord>.Failure(stored.Errors)
            : Result<AttachmentRecord>.Success(Record(stored.Value));
    }

    private static AttachmentRecord Record(StoredAttachment stored) => new(
        stored.Id.Value,
        AttachmentMediaTypes.NameOf(stored.MediaType),
        stored.ByteLength,
        stored.ContentHash,
        stored.FileName,
        stored.StoredAt,
        stored.StoredBy.Value,
        stored.Version,
        stored.Supersedes.IsAssigned ? stored.Supersedes.Value : null,
        stored.SupersededBy.IsAssigned ? stored.SupersededBy.Value : null,
        stored.SourceDocumentType,
        stored.SourceDocumentId,
        stored.Withdrawal is null
            ? null
            : new AttachmentWithdrawalRecord(
                stored.Withdrawal.WithdrawnAt,
                stored.Withdrawal.WithdrawnBy.Value,
                stored.Withdrawal.ReasonKey));
}
