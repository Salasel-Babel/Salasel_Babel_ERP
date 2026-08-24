using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Sales.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>نية ترحيل مستند: ما تصفه الوحدة، بلا حساب ولا جانب ولا مبلغ سطر.</summary>
internal sealed record PostingIntent
{
    public required TenantId Tenant { get; init; }

    /// <summary>نوع المستند — الحقل الأول في هوية الإحكام لدى المحرك.</summary>
    public required string DocumentType { get; init; }

    public required Guid DocumentId { get; init; }

    public required PostingTrigger Trigger { get; init; }

    public required PostingEventCode Event { get; init; }

    public required DateOnly DocumentDate { get; init; }

    public required LocalizedName Narration { get; init; }

    public required IReadOnlyList<PostingAmount> Amounts { get; init; }

    public required IReadOnlyList<PostingFact> Facts { get; init; }

    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>الطرف في الدفتر المساعد — يُحفظ في سجلّ المحاولة كي تُسمّيه المطابقة.</summary>
    public required string PartyId { get; init; }

    /// <summary>الأثر المتوقَّع على نقطة الضبط بمنطق «مدين ناقص دائن».</summary>
    public required decimal ControlEffect { get; init; }

    public required CurrencyCode Currency { get; init; }

    public UserId Actor { get; init; } = UserId.SystemActor;

    public int Generation { get; init; } = 1;
}

/// <summary>
/// بوابة الترحيل: الطريق الوحيد الذي تصل به وحدة المبيعات إلى دفتر الأستاذ.
/// <para>
/// <b>الإحكام لكل مستند ومستقلّ عن الترتيب.</b> هوية الإحكام لدى المحرك هي الرباعية
/// (نوع المستند · معرّفه · رمز الإطلاق · الجيل)، وهذا هو ما تحمله البوابة بالضبط.
/// لا عدّاد تصاعدي لكل عميل، ولا مقارنة <c>&lt;</c> مع تسلسل مُطبَّق: ذلك الشكل قيس
/// وهو يُسقط بصمت 500 ريال من 1,500 عند وصول خارج الترتيب (فخ-13).
/// </para>
/// <para>
/// وسجلّ المحاولة يُكتب <b>قبل</b> النداء ويُغلق <b>بعده</b>، فالرفض يترك المستند
/// على حاله ومعه سبب مكتوب — حالة متّسقة تُعاد المحاولة منها، لا نصف كتابة.
/// </para>
/// </summary>
internal sealed class SubledgerPostingGateway(SalesDbContext database, IPostingService posting)
{
    private readonly SalesDbContext _database = database;
    private readonly IPostingService _posting = posting;

    public static string IdempotencyKeyOf(string documentType, Guid documentId, PostingTrigger trigger, int generation)
        => string.Concat(
            "sales:",
            documentType,
            ":",
            documentId.ToString("N", CultureInfo.InvariantCulture),
            ":",
            trigger.ToString(),
            ":g",
            generation.ToString(CultureInfo.InvariantCulture));

    public async Task<Result<PostingReceipt>> PostAsync(PostingIntent intent, CancellationToken cancellationToken)
    {
        string documentId = intent.DocumentId.ToString("D", CultureInfo.InvariantCulture);
        string triggerCode = intent.Trigger.ToString();

        DocumentPostingRow? attempt = await _database.Postings
            .FirstOrDefaultAsync(
                row => row.TenantId == intent.Tenant.Value
                       && row.DocumentType == intent.DocumentType
                       && row.DocumentId == documentId
                       && row.TriggerCode == triggerCode
                       && row.Generation == intent.Generation,
                cancellationToken)
            .ConfigureAwait(false);

        if (attempt is { State: PostingAttemptState.Posted, EntryId: { } posted })
        {
            // وصول ثانٍ بالهوية نفسها لا يفعل شيئاً ولا يُعدّ خطأ — مهما كان ترتيب الوصول.
            return Result<PostingReceipt>.Success(
                new PostingReceipt(posted, attempt.EntryNumber, string.Empty, true, 0, string.Empty, attempt.Generation));
        }

        string key = IdempotencyKeyOf(intent.DocumentType, intent.DocumentId, intent.Trigger, intent.Generation);

        if (attempt is null)
        {
            attempt = new DocumentPostingRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = intent.Tenant.Value,
                DocumentType = intent.DocumentType,
                DocumentId = documentId,
                TriggerCode = triggerCode,
                Generation = intent.Generation,
                IdempotencyKey = key,
                EventCode = intent.Event.Value,
                PartyId = intent.PartyId,
                DocumentDate = intent.DocumentDate,
            };
            _database.Postings.Add(attempt);
        }

        attempt.State = PostingAttemptState.Attempting;
        attempt.DocumentDate = intent.DocumentDate;
        attempt.ControlEffect = intent.ControlEffect;
        attempt.AttemptCount++;
        attempt.LastAttemptAt = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        PostingRequest request = new()
        {
            Tenant = intent.Tenant,
            IdempotencyKey = new IdempotencyKey(key),
            Source = new SourceDocument(BabelModule.Sales, intent.DocumentType, documentId),
            Trigger = intent.Trigger,
            DocumentDate = intent.DocumentDate,
            Narration = intent.Narration,
            Lines = [],
            Event = intent.Event,
            Amounts = intent.Amounts,
            Facts = intent.Facts,
            Dimensions = intent.Dimensions,
            Currency = intent.Currency,
            Generation = intent.Generation,
            Actor = intent.Actor,
        };

        Result<PostingReceipt> result = await _posting.PostAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            Error first = result.Errors[0];
            attempt.State = PostingAttemptState.Refused;
            attempt.FailureCode = first.Code;
            attempt.FailureMessageAr = Trim(first.MessageAr);
            attempt.FailureMessageEn = Trim(first.MessageEn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<PostingReceipt>.Failure(SalesErrors.PostingRefused(result.Errors));
        }

        attempt.State = PostingAttemptState.Posted;
        attempt.EntryId = result.Value.JournalEntryId;
        attempt.EntryNumber = result.Value.EntryNumber;
        attempt.FailureCode = string.Empty;
        attempt.FailureMessageAr = string.Empty;
        attempt.FailureMessageEn = string.Empty;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static string Trim(string message) => message.Length <= 1000 ? message : message[..1000];
}
