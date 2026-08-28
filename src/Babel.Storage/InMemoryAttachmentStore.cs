using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Babel.Contracts.Storage;
using Babel.SharedKernel;

namespace Babel.Storage;

/// <summary>
/// <b>المحوّل الثاني: في الذاكرة.</b>
/// <para>
/// <b>ولماذا يوجد اثنان:</b> منفذٌ له تنفيذ واحد ليس منفذاً بل اسمٌ آخر لذلك التنفيذ.
/// وجود محوّل ثانٍ لا يعرف نظام ملفّات ولا PostgreSQL هو <b>ما يُثبت</b> أن
/// <see cref="IAttachmentStore"/> لا يفترض أيّاً منهما: لا مسار في توقيع، ولا
/// <c>Stream</c> على قرص، ولا اتصال قاعدة بيانات.
/// </para>
/// <para>
/// <b>وما يحفظه من سلوك المحوّل الحقيقي حرفياً</b> — لأن من يُبدّل بينهما يعتمد عليه:
/// الشمّ من البايتات، وتطهير الاسم، والسقف، وبصمة SHA-256، والمستأجر جزءاً من المفتاح،
/// والتصحيح إصداراً جديداً، والسحب علامةً لا محواً. <b>وما لا يحفظه</b>: الدوام —
/// وذلك مُعلَن في اسمه، فلا يُركَّب في إنتاج.
/// </para>
/// </summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly ConcurrentDictionary<(Guid Tenant, Guid Attachment), Entry> _entries = new();
    private readonly StorageOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ محوّلاً في الذاكرة بإعدادات افتراضية.</summary>
    public InMemoryAttachmentStore()
        : this(new StorageOptions(), TimeProvider.System)
    {
    }

    /// <summary>ينشئ محوّلاً في الذاكرة.</summary>
    /// <param name="options">الإعدادات — يُقرأ منها السقف وحده.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public InMemoryAttachmentStore(StorageOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _clock = clock;
    }

    private sealed record Entry(StoredAttachment Descriptor, byte[] Content)
    {
        public AttachmentId SupersededBy { get; set; }

        public AttachmentWithdrawal? Withdrawal { get; set; }
    }

    /// <inheritdoc />
    public ValueTask<Result<StoredAttachment>> PutAsync(
        AttachmentSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlyMemory<byte> content = submission.Content;

        if (content.Length == 0)
        {
            return Refuse(AttachmentErrors.Empty);
        }

        if (content.Length > _options.MaximumBytes)
        {
            return Refuse(AttachmentErrors.TooLarge(content.Length, _options.MaximumBytes));
        }

        if (ContentSniff.Of(content.Span) is not { } mediaType)
        {
            return Refuse(AttachmentErrors.UnrecognisedContent);
        }

        if (!ContentSniff.DeclarationAgrees(submission.DeclaredMediaType, mediaType))
        {
            return Refuse(AttachmentErrors.DeclaredTypeMismatch(submission.DeclaredMediaType!, mediaType));
        }

        Result<string> fileName = SafeFileName.Sanitise(submission.DeclaredFileName, mediaType);
        if (fileName.IsFailure)
        {
            return ValueTask.FromResult(Result<StoredAttachment>.Failure(fileName.Errors));
        }

        int version = 1;
        Entry? predecessor = null;

        if (submission.Supersedes.IsAssigned)
        {
            if (!_entries.TryGetValue((submission.Tenant.Value, submission.Supersedes.Value), out predecessor))
            {
                return Refuse(AttachmentErrors.NotFound(submission.Supersedes));
            }

            if (predecessor.Withdrawal is not null)
            {
                return Refuse(AttachmentErrors.AlreadyWithdrawn(submission.Supersedes));
            }

            if (predecessor.SupersededBy.IsAssigned)
            {
                return Refuse(AttachmentErrors.AlreadySuperseded(submission.Supersedes, predecessor.SupersededBy));
            }

            version = predecessor.Descriptor.Version + 1;
        }

        AttachmentId id = new(Guid.CreateVersion7());
        byte[] bytes = content.ToArray();

        StoredAttachment descriptor = new()
        {
            Id = id,
            Tenant = submission.Tenant,
            MediaType = mediaType,
            ByteLength = bytes.Length,
            ContentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            ObjectKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{submission.Tenant.Value:N}/{id.Value:N}.{AttachmentMediaTypes.ExtensionOf(mediaType)}"),
            FileName = fileName.Value,
            StoredAt = _clock.GetUtcNow(),
            StoredBy = submission.Actor,
            Version = version,
            Supersedes = submission.Supersedes,
        };

        _entries[(submission.Tenant.Value, id.Value)] = new Entry(descriptor, bytes);

        if (predecessor is not null)
        {
            predecessor.SupersededBy = id;
        }

        return ValueTask.FromResult(Result<StoredAttachment>.Success(descriptor));
    }

    /// <inheritdoc />
    public ValueTask<Result<StoredAttachment>> DescribeAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(Find(tenant, id) is { } entry
            ? Result<StoredAttachment>.Success(Describe(entry))
            : Result<StoredAttachment>.Failure(AttachmentErrors.NotFound(id)));
    }

    /// <inheritdoc />
    public ValueTask<Result<AttachmentContent>> OpenAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(Find(tenant, id) is { } entry
            ? Result<AttachmentContent>.Success(new AttachmentContent
            {
                Descriptor = Describe(entry),
                Content = entry.Content,
            })
            : Result<AttachmentContent>.Failure(AttachmentErrors.NotFound(id)));
    }

    /// <inheritdoc />
    public ValueTask<Result<AttachmentIntegrity>> VerifyAsync(
        TenantId tenant,
        AttachmentId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Find(tenant, id) is not { } entry)
        {
            return ValueTask.FromResult(Result<AttachmentIntegrity>.Failure(AttachmentErrors.NotFound(id)));
        }

        string observed = Convert.ToHexString(SHA256.HashData(entry.Content)).ToLowerInvariant();

        return ValueTask.FromResult(Result<AttachmentIntegrity>.Success(new AttachmentIntegrity
        {
            Id = id,
            Matches = string.Equals(observed, entry.Descriptor.ContentHash, StringComparison.Ordinal),
            RecordedHash = entry.Descriptor.ContentHash,
            ObservedHash = observed,
            BytesRead = entry.Content.Length,
            Elapsed = TimeSpan.Zero,
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<StoredAttachment>> WithdrawAsync(
        TenantId tenant,
        AttachmentId id,
        UserId actor,
        string reasonKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (Find(tenant, id) is not { } entry)
        {
            return Refuse(AttachmentErrors.NotFound(id));
        }

        if (entry.Withdrawal is not null)
        {
            return Refuse(AttachmentErrors.AlreadyWithdrawn(id));
        }

        entry.Withdrawal = new AttachmentWithdrawal(_clock.GetUtcNow(), actor, reasonKey);
        return ValueTask.FromResult(Result<StoredAttachment>.Success(Describe(entry)));
    }

    private Entry? Find(TenantId tenant, AttachmentId id) =>
        _entries.TryGetValue((tenant.Value, id.Value), out Entry? entry) ? entry : null;

    private static StoredAttachment Describe(Entry entry) => entry.Descriptor with
    {
        SupersededBy = entry.SupersededBy,
        Withdrawal = entry.Withdrawal,
    };

    private static ValueTask<Result<StoredAttachment>> Refuse(Error error) =>
        ValueTask.FromResult(Result<StoredAttachment>.Failure(error));
}
