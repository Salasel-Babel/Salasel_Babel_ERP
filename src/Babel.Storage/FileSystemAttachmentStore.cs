using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Babel.Contracts.Storage;
using Babel.SharedKernel;
using Babel.Storage.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Storage;

/// <summary>
/// <b>محوّل نظام الملفّات</b> — البايتات تحت جذرٍ على القرص، والمسار والوصف في PostgreSQL.
/// <para>
/// <b>لماذا نظام الملفّات اليوم:</b> النشر يستهدف خادم لينكس واحداً (<c>deploy/</c>).
/// و<b>لماذا لا يفترض المنفذ ذلك:</b> لا مسار مطلق في أي توقيع، ولا <c>Stream</c> على
/// القرص يعبر الحدّ؛ مفتاح الكائن نصٌّ يفهمه هذا المحوّل وحده.
/// </para>
/// <para>
/// <b>وثلاث خصائص يحملها هذا المحوّل ولا يقولها اسمه:</b>
/// </para>
/// <list type="number">
///   <item><b>لا كتابة فوق ملفّ قائم أبداً.</b> كل كتابة <c>FileMode.CreateNew</c> على
///         مفتاح عشوائي من 256 بتّاً، والاصطدام يرمي ولا يستبدل.</item>
///   <item><b>لا حذف.</b> لا نداء <c>File.Delete</c> واحد في هذا الملفّ. سطر لم يكتمل
///         يترك ملفّاً يتيماً بلا صفّ — <b>وذلك مقصود</b>: ملفٌّ بلا صفّ لا يُقرأ أبداً
///         (القراءة تبدأ من الصفّ)، وحذفه من مسار كتابةٍ يعني أن مسار الكتابة يملك
///         الحذف. وأثرُه مساحةٌ تُكنس بعملٍ إداري بدور المالك، لا بالتطبيق.</item>
///   <item><b>المستأجر داخل المسار.</b> أول جزء من مفتاح الكائن هو المستأجر، فحتى لو
///         سُرّب مفتاح كائنٍ كاملاً بقي محصوراً في شجرة مستأجره — والقراءة مع ذلك
///         لا تبدأ من المسار بحال.</item>
/// </list>
/// </summary>
public sealed class FileSystemAttachmentStore : IAttachmentStore
{
    private readonly StorageOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ المحوّل.</summary>
    /// <param name="options">الإعدادات.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public FileSystemAttachmentStore(StorageOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<Result<StoredAttachment>> PutAsync(
        AttachmentSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // ── ١ · الحجم قبل أي شيء: يُفحص قبل الشمّ وقبل أي تخصيص ──────────────
        ReadOnlyMemory<byte> content = submission.Content;
        if (content.Length == 0)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.Empty);
        }

        if (content.Length > _options.MaximumBytes)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.TooLarge(content.Length, _options.MaximumBytes));
        }

        // ── ٢ · النوع من البايتات، ثم يُقارَن بالإعلان ولا يُؤخذ منه ──────────
        AttachmentMediaType? sniffed = ContentSniff.Of(content.Span);
        if (sniffed is not { } mediaType)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.UnrecognisedContent);
        }

        if (!ContentSniff.DeclarationAgrees(submission.DeclaredMediaType, mediaType))
        {
            return Result<StoredAttachment>.Failure(
                AttachmentErrors.DeclaredTypeMismatch(submission.DeclaredMediaType!, mediaType));
        }

        // ── ٣ · الاسم بيانات: يُطهَّر، ولا يشارك في بناء المسار ───────────────
        Result<string> fileName = SafeFileName.Sanitise(submission.DeclaredFileName, mediaType);
        if (fileName.IsFailure)
        {
            return Result<StoredAttachment>.Failure(fileName.Errors);
        }

        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);

        // ── ٣.٥ · ربط المستند المصدر: كاملٌ أو غائب، ولا نصف ربط ─────────────
        Result<bool> link = SourceDocumentLink.Check(submission.SourceDocumentType, submission.SourceDocumentId);
        if (link.IsFailure)
        {
            return Result<StoredAttachment>.Failure(link.Errors);
        }

        // ── ٤ · إن كان تصحيحاً: السلف موجود، وفي المستأجر نفسه، وقابل للتصحيح ──
        int version = 1;
        if (submission.Supersedes.IsAssigned)
        {
            Result<int> chain = await NextVersionAsync(database, submission.Tenant, submission.Supersedes, cancellationToken)
                .ConfigureAwait(false);

            if (chain.IsFailure)
            {
                return Result<StoredAttachment>.Failure(chain.Errors);
            }

            version = chain.Value;
        }

        // ── ٥ · البصمة، ثم الكتابة مرّة واحدة على مفتاح لا يُخمَّن ────────────
        string contentHash = Digest(content.Span);
        string objectKey = NewObjectKey(submission.Tenant, mediaType);
        string absolute = Resolve(objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        // ‏CreateNew: الاصطدام يرمي ولا يستبدل. ولا مسار في هذا الصنف يفتح ملفّاً
        // قائماً للكتابة، وهو ما يجعل «البايتات لا تتغيّر» صحيحاً في الشيفرة أيضاً
        // لا في القاعدة وحدها.
        await using (FileStream file = new(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            await file.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        AttachmentRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = submission.Tenant.Value,
            MediaType = AttachmentMediaTypes.NameOf(mediaType),
            ByteLength = content.Length,
            ContentHash = contentHash,
            ObjectKey = objectKey,
            FileName = fileName.Value,
            StoredAt = _clock.GetUtcNow(),
            StoredBy = submission.Actor.Value,
            Version = version,
            SupersedesId = submission.Supersedes.IsAssigned ? submission.Supersedes.Value : null,
            SourceDocumentType = link.Value ? submission.SourceDocumentType : null,
            SourceDocumentId = link.Value ? submission.SourceDocumentId : null,
        };

        database.Attachments.Add(row);

        // ── ٦ · والسباق تحسمه القاعدة، لا الفحص أعلاه ────────────────────────
        // الفحص في الخطوة ٤ يقرأ ثم يكتب، وبينهما نافذة: طلبان يصحّحان السلف نفسه
        // يمرّان كلاهما من الفحص. والفهرس الفريد الجزئي على SupersedesId يرفض الثاني —
        // **لكن الرفض يصل استثناءً لا قيمة**، وهو ما يحوّله هذا المسك إلى رفضٍ
        // بالرمز نفسه الذي كان الفحص سيعيده. و`PostgresException` تُمسَك **إلى جانب**
        // `DbUpdateException` لأن المشغّل المؤجَّل يرفض عند COMMIT لا عند العبارة
        // (‏docs/evidence/traps.md فخ-41).
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (submission.Supersedes.IsAssigned && IsUniqueViolation(exception))
        {
            return await RefuseForkAsync(database, submission, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception)
            when (submission.Supersedes.IsAssigned && exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await RefuseForkAsync(database, submission, cancellationToken).ConfigureAwait(false);
        }

        return Result<StoredAttachment>.Success(Describe(row, successor: null, withdrawal: null));
    }

    /// <inheritdoc />
    public async ValueTask<Result<StoredAttachment>> DescribeAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);
        return await FindAsync(database, tenant, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<AttachmentContent>> OpenAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);

        Result<StoredAttachment> found = await FindAsync(database, tenant, id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<AttachmentContent>.Failure(found.Errors);
        }

        StoredAttachment descriptor = found.Value;
        string absolute = Resolve(descriptor.ObjectKey);

        if (!File.Exists(absolute))
        {
            return Result<AttachmentContent>.Failure(AttachmentErrors.ContentMissing(id));
        }

        byte[] bytes = await File.ReadAllBytesAsync(absolute, cancellationToken).ConfigureAwait(false);
        string observed = Digest(bytes);

        // **البصمة تُفحص قبل التسليم، لا بعده.** مخزنٌ يعيد بايتات ثم يخبرك أنها
        // لا تطابق قد سلّمها بالفعل.
        if (!string.Equals(observed, descriptor.ContentHash, StringComparison.Ordinal))
        {
            return Result<AttachmentContent>.Failure(
                AttachmentErrors.HashMismatch(id, descriptor.ContentHash, observed));
        }

        return Result<AttachmentContent>.Success(new AttachmentContent
        {
            Descriptor = descriptor,
            Content = bytes,
        });
    }

    /// <inheritdoc />
    public async ValueTask<Result<AttachmentIntegrity>> VerifyAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);

        Result<StoredAttachment> found = await FindAsync(database, tenant, id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<AttachmentIntegrity>.Failure(found.Errors);
        }

        StoredAttachment descriptor = found.Value;
        string absolute = Resolve(descriptor.ObjectKey);

        if (!File.Exists(absolute))
        {
            return Result<AttachmentIntegrity>.Failure(AttachmentErrors.ContentMissing(id));
        }

        long timestamp = Stopwatch.GetTimestamp();
        long bytesRead = 0;

        // القراءة على دفعات: التحقّق يجب أن يعمل على مرفقٍ لا يتّسع في الذاكرة،
        // وأن يكون ثمنه قابلاً للقياس. وهو **خطّي في حجم المرفق** لا استعلام فهرس —
        // ومن يجدوله على مليون مرفق يجدول قراءة المخزن كلّه.
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (FileStream file = new(absolute, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                bytesRead += read;
            }
        }

        string observed = Convert.ToHexString(hash.GetCurrentHash()).ToLowerInvariant();

        return Result<AttachmentIntegrity>.Success(new AttachmentIntegrity
        {
            Id = id,
            Matches = string.Equals(observed, descriptor.ContentHash, StringComparison.Ordinal),
            RecordedHash = descriptor.ContentHash,
            ObservedHash = observed,
            BytesRead = bytesRead,
            Elapsed = Stopwatch.GetElapsedTime(timestamp),
        });
    }

    /// <inheritdoc />
    public async ValueTask<Result<AttachmentPage>> ListAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<bool> page = SourceDocumentLink.CheckPage(query);
        if (page.IsFailure)
        {
            return Result<AttachmentPage>.Failure(page.Errors);
        }

        Result<bool> link = SourceDocumentLink.Check(query.SourceDocumentType, query.SourceDocumentId);
        if (link.IsFailure)
        {
            return Result<AttachmentPage>.Failure(link.Errors);
        }

        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);

        // **المستأجر أول شرط، لا مرشّح يُضاف بعد**: جرد بلا مستأجر ليس جرداً ناقصاً
        // بل جردٌ عبر الحدّ، والفهرس نفسه يبدأ بالمستأجر كي يقول الشكلُ ذلك أيضاً.
        IQueryable<AttachmentRow> rows = database.Attachments
            .AsNoTracking()
            .Where(candidate => candidate.TenantId == query.Tenant.Value);

        if (link.Value)
        {
            rows = rows.Where(candidate =>
                candidate.SourceDocumentType == query.SourceDocumentType
                && candidate.SourceDocumentId == query.SourceDocumentId);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        // ‏Guid v7 يرتّب زمنياً، والترتيب التنازلي عليه هو «الأحدث أولاً» بلا عمود ثانٍ.
        // والترتيب **صريح دائماً**: صفحةٌ بلا ترتيب مُعلن تُعيد صفوفاً مكرّرة وأخرى
        // مفقودة بين طلبين — وهو عطلٌ لا يُرى إلا عند العميل.
        List<AttachmentRow> window = await rows
            .OrderByDescending(candidate => candidate.Id)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoredAttachment> items = [];
        foreach (AttachmentRow row in window)
        {
            Result<StoredAttachment> described =
                await FindAsync(database, query.Tenant, new AttachmentId(row.Id), cancellationToken).ConfigureAwait(false);

            if (described.IsFailure)
            {
                return Result<AttachmentPage>.Failure(described.Errors);
            }

            items.Add(described.Value);
        }

        return Result<AttachmentPage>.Success(new AttachmentPage
        {
            Items = items,
            Total = total,
            Skip = query.Skip,
            Take = query.Take,
        });
    }

    /// <inheritdoc />
    public async ValueTask<Result<StoredAttachment>> WithdrawAsync(
        TenantId tenant,
        AttachmentId id,
        UserId actor,
        string reasonKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);

        await using StorageDbContext database = StorageRuntime.Build(_options.AppConnectionString);

        Result<StoredAttachment> found = await FindAsync(database, tenant, id, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<StoredAttachment>.Failure(found.Errors);
        }

        if (found.Value.Withdrawal is not null)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.AlreadyWithdrawn(id));
        }

        // **صفٌّ جديد، لا تعديل.** ولا حذف لبايتة: الاحتفاظ بسند القيد واجب نظامي.
        AttachmentWithdrawalRow marker = new()
        {
            AttachmentId = id.Value,
            TenantId = tenant.Value,
            WithdrawnAt = _clock.GetUtcNow(),
            WithdrawnBy = actor.Value,
            ReasonKey = reasonKey,
        };

        database.Withdrawals.Add(marker);

        // نفس السباق، ونفس العلاج: مفتاح جدول السحب هو المرفق، فالسحب المتزامن
        // الثاني يصطدم بالمفتاح الأوّلي ويعود بالرمز الذي كان الفحص سيعيده.
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.AlreadyWithdrawn(id));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.AlreadyWithdrawn(id));
        }

        return await FindAsync(database, tenant, id, cancellationToken).ConfigureAwait(false);
    }

    // ── القراءة ─────────────────────────────────────────────────────────────

    /// <summary>
    /// يقرأ صفّاً <b>بمفتاح مركّب من المستأجر والمعرّف معاً</b>.
    /// <para>
    /// وهذا هو الجواب على «ماذا لو سُرّب معرّف؟»: المعرّف وحده لا يكوّن مفتاحاً في أي
    /// استعلام في هذا الصنف. مستأجرٌ آخر يقدّم المعرّف الصحيح يحصل على
    /// <c>storage.attachment_not_found</c> — <b>لا على 403</b>، لأن التمييز بين
    /// «غير موجود» و«ليس لك» يُخبر السائل بوجود ما لا يخصّه.
    /// </para>
    /// </summary>
    private static async ValueTask<Result<StoredAttachment>> FindAsync(
        StorageDbContext database,
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken)
    {
        AttachmentRow? row = await database.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == id.Value && candidate.TenantId == tenant.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<StoredAttachment>.Failure(AttachmentErrors.NotFound(id));
        }

        // الخلف والسحب يُقرآن ولا يُكتبان على الصفّ: الصفّ لا يُعدَّل.
        Guid? successor = await database.Attachments
            .AsNoTracking()
            .Where(candidate => candidate.SupersedesId == row.Id && candidate.TenantId == tenant.Value)
            .Select(static candidate => (Guid?)candidate.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        AttachmentWithdrawalRow? withdrawal = await database.Withdrawals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AttachmentId == row.Id && candidate.TenantId == tenant.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return Result<StoredAttachment>.Success(Describe(row, successor, withdrawal));
    }

    private static async ValueTask<Result<int>> NextVersionAsync(
        StorageDbContext database,
        TenantId tenant,
        AttachmentId predecessor,
        CancellationToken cancellationToken)
    {
        Result<StoredAttachment> found = await FindAsync(database, tenant, predecessor, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<int>.Failure(found.Errors);
        }

        if (found.Value.Withdrawal is not null)
        {
            return Result<int>.Failure(AttachmentErrors.AlreadyWithdrawn(predecessor));
        }

        if (found.Value.SupersededBy.IsAssigned)
        {
            return Result<int>.Failure(AttachmentErrors.AlreadySuperseded(predecessor, found.Value.SupersededBy));
        }

        return Result<int>.Success(found.Value.Version + 1);
    }

    private static StoredAttachment Describe(AttachmentRow row, Guid? successor, AttachmentWithdrawalRow? withdrawal) => new()
    {
        Id = new AttachmentId(row.Id),
        Tenant = new TenantId(row.TenantId),
        MediaType = MediaTypeOf(row.MediaType),
        ByteLength = row.ByteLength,
        ContentHash = row.ContentHash,
        ObjectKey = row.ObjectKey,
        FileName = row.FileName,
        StoredAt = row.StoredAt,
        StoredBy = new UserId(row.StoredBy),
        Version = row.Version,
        Supersedes = row.SupersedesId is { } predecessor ? new AttachmentId(predecessor) : AttachmentId.None,
        SourceDocumentType = row.SourceDocumentType,
        SourceDocumentId = row.SourceDocumentId,
        SupersededBy = successor is { } next ? new AttachmentId(next) : AttachmentId.None,
        Withdrawal = withdrawal is null
            ? null
            : new AttachmentWithdrawal(withdrawal.WithdrawnAt, new UserId(withdrawal.WithdrawnBy), withdrawal.ReasonKey),
    };

    private static AttachmentMediaType MediaTypeOf(string stored) => stored switch
    {
        "image/jpeg" => AttachmentMediaType.Jpeg,
        "image/png" => AttachmentMediaType.Png,
        "application/pdf" => AttachmentMediaType.Pdf,
        "image/tiff" => AttachmentMediaType.Tiff,
        "image/webp" => AttachmentMediaType.Webp,
        "image/heic" => AttachmentMediaType.Heic,
        _ => throw new InvalidOperationException(
            "نوع محتوى مخزَّن خارج المجموعة المغلقة: " + stored
            + " / a stored content type outside the closed set: " + stored),
    };

    // ── المسار ──────────────────────────────────────────────────────────────

    /// <summary>
    /// يولّد مفتاح كائن جديداً: <c>{المستأجر}/{بايتان}/{256 بتّاً عشوائياً}.{الامتداد}</c>.
    /// <para>
    /// <b>العشوائية معمّاة</b> (<see cref="RandomNumberGenerator"/>) لا مولّد عام: مفتاحٌ
    /// قابل للتخمين يجعل شجرة المخزن قابلة للمسح. و<b>لا يُشتقّ المفتاح من المعرّف
    /// ولا من البصمة ولا من الاسم</b>: اشتقاقه من البصمة كان سيجعل ملفّين متطابقين
    /// لمستأجرَين مختلفين ملفّاً واحداً — وهو تسريبٌ عبر الحدّ من حيث لا يُتوقَّع.
    /// </para>
    /// <para>
    /// والمجلدان الوسيطان من أول بايتَي المفتاح: لا يجوز أن يجتمع مليون ملفّ في مجلد واحد.
    /// </para>
    /// </summary>
    private static string NewObjectKey(TenantId tenant, AttachmentMediaType mediaType)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{tenant.Value:N}/{token[..2]}/{token[2..4]}/{token}.{AttachmentMediaTypes.ExtensionOf(mediaType)}");
    }

    /// <summary>
    /// يحلّ مفتاح كائن إلى مسار مطلق، <b>ويرفض أي مفتاح يخرج من الجذر</b>.
    /// <para>
    /// المفتاح يولّده هذا الصنف ولا يأتي من مستخدم، فالفحص هنا حزامٌ فوق حمّالة —
    /// لكنه الفحص الذي يبقى صحيحاً لو صار المفتاح يوماً يعبر حدّاً. ويقارَن
    /// <b>المسار الكامل بعد التطبيع</b>، لا النصّ قبله: ‏<c>..</c> يُبطلها التطبيع
    /// لا الترشيح.
    /// </para>
    /// </summary>
    private string Resolve(string objectKey)
    {
        string root = Path.GetFullPath(_options.RootPath);
        string absolute = Path.GetFullPath(Path.Combine(root, objectKey));

        if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "مفتاح كائن يخرج من جذر المخزن: " + objectKey
                + " / an object key escapes the store root: " + objectKey);
        }

        return absolute;
    }

    /// <summary>
    /// هل هذا الفشل تصادمَ تفرّد؟ ‏<c>DbUpdateException</c> غلافٌ، والرمز في جوفه.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException inner
        && inner.SqlState == PostgresErrorCodes.UniqueViolation;

    /// <summary>
    /// يبني رفض التفرّع بعد أن حسمته القاعدة، ويسمّي <b>الخلف الذي فاز</b> — يُقرأ
    /// من جديد لأن الفائز غير معروف للخاسر.
    /// <para>
    /// <b>ولا يُمسك تصادمٌ آخر بهذا الرمز:</b> الفهرس الفريد الوحيد الذي يستطيع طلبان
    /// متزامنان أن يصطدما عليه هو فهرس <c>SupersedesId</c>. أمّا <c>ObjectKey</c> فمن
    /// 256 بتّاً معمّى، وتصادمه ليس سباقاً بل عطلٌ في مولّد العشوائية — <b>ويُترك
    /// يرمي</b>، لأن ابتلاعه برمز مجالي يُخفي عطلاً في أساس المخزن كلّه.
    /// </para>
    /// </summary>
    private static async ValueTask<Result<StoredAttachment>> RefuseForkAsync(
        StorageDbContext database,
        AttachmentSubmission submission,
        CancellationToken cancellationToken)
    {
        Guid? winner = await database.Attachments
            .AsNoTracking()
            .Where(candidate => candidate.SupersedesId == submission.Supersedes.Value
                && candidate.TenantId == submission.Tenant.Value)
            .Select(static candidate => (Guid?)candidate.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<StoredAttachment>.Failure(AttachmentErrors.AlreadySuperseded(
            submission.Supersedes,
            winner is { } id ? new AttachmentId(id) : AttachmentId.None));
    }

    private static string Digest(ReadOnlySpan<byte> content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
