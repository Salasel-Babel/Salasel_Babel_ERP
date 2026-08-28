using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>نية ترحيل مستند مخزون: ما تصفه الوحدة، بلا حساب ولا جانب ولا مبلغ سطر.</summary>
internal sealed record InventoryPostingIntent
{
    public required TenantId Tenant { get; init; }

    public required string DocumentType { get; init; }

    public required Guid DocumentId { get; init; }

    public required PostingTrigger Trigger { get; init; }

    /// <summary>رمز الحدث — <b>حقل في هوية الإحكام لا وصفٌ للقيد</b> (‏ADR-0016).</summary>
    public required PostingEventCode Event { get; init; }

    public required DateOnly DocumentDate { get; init; }

    public required LocalizedName Narration { get; init; }

    public required IReadOnlyList<PostingAmount> Amounts { get; init; }

    public required IReadOnlyList<PostingFact> Facts { get; init; }

    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>الطرف في الدفتر المساعد — الصنف هنا، ويُحفظ كي تُسمّيه المطابقة.</summary>
    public required string PartyId { get; init; }

    public required CurrencyCode Currency { get; init; }

    public UserId Actor { get; init; } = UserId.SystemActor;

    public int Generation { get; init; } = 1;
}

/// <summary>
/// بوابة الترحيل: الطريق الوحيد الذي تصل به وحدة المخزون إلى دفتر الأستاذ.
/// <para>
/// <b>الإحكام لكل مستند ومستقلّ عن الترتيب.</b> هوية الإحكام هي السداسية (المنشأة ·
/// نوع المستند · معرّفه · رمز الإطلاق · الجيل · <b>رمز الحدث</b>)، وهي نفسها هوية
/// حركة المخزون حرفاً بحرف — فالواقعة واحدة تُروى مرّتين، ولا تُعدّ بحبيبيّتين
/// مختلفتين (‏ADR-0039 §4 · فخ-44 · فخ-48).
/// </para>
/// <para>
/// <b>وصفّ المحاولة يُكتب قبل النداء ويُغلق بعده</b>، فالرفض يترك المستند على حاله
/// ومعه سببٌ مكتوب — حالة متّسقة تُعاد المحاولة منها، لا نصف كتابة.
/// </para>
/// <para>
/// ⚠️ <b>وهذا ثالث نسخة من هذا الشكل</b> — بعد المبيعات والمشتريات — وهو دَينٌ مُعلَن
/// من صنف <c>docs/evidence/traps.md#fakh-81</c>: قاعدةٌ واحدة مكتوبة في ثلاثة مواضع
/// بلا حارسٍ يربطها. مُسجَّل في <c>docs/evidence/verification-debt.md</c>.
/// </para>
/// </summary>
internal sealed class InventoryPostingGateway(
    InventoryDbContext database,
    IPostingService posting,
    ICostCenterResolver costCenters)
{
    /// <summary>اسم بُعد مركز التكلفة كما تعرفه المصفوفة والمخطّط.</summary>
    private const string CostCenterDimension = "cost_center";

    /// <summary>بادئة المفتاح وإصدار ترميزه — الإصدار في المفتاح كي يُقرأ شكله من قيمته.</summary>
    private const string KeyPrefix = "inventory:v1:";

    private readonly InventoryDbContext _database = database;
    private readonly IPostingService _posting = posting;
    private readonly ICostCenterResolver _costCenters = costCenters;

    /// <summary>
    /// مفتاح الحصانة المشتقّ من هوية الإحكام كاملةً، <b>بترميزٍ مسبوقٍ بالطول ثم بصمة</b>:
    /// السلسلة الموصولة على فاصل قد يحتويه أحد المكوّنات عطبُ تصادم بذاته.
    /// </summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="documentId">معرّف المستند.</param>
    /// <param name="trigger">رمز الإطلاق.</param>
    /// <param name="generation">جيل الترحيل.</param>
    /// <param name="eventCode">رمز الحدث.</param>
    public static string IdempotencyKeyOf(
        string documentType,
        Guid documentId,
        PostingTrigger trigger,
        int generation,
        PostingEventCode eventCode)
    {
        StringBuilder canonical = new();
        Append(canonical, "inventory");
        Append(canonical, documentType);
        Append(canonical, documentId.ToString("N", CultureInfo.InvariantCulture));
        Append(canonical, trigger.ToString());
        Append(canonical, generation.ToString(CultureInfo.InvariantCulture));
        Append(canonical, eventCode.Value ?? string.Empty);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return KeyPrefix + Convert.ToHexStringLower(digest);
    }

    /// <summary>يرحّل نيّة ويُعيد إيصالها — و<b>«رُحّل سلفاً» حكمُ هذه البوّابة</b>.</summary>
    /// <param name="intent">النية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<Result<PostingReceipt>> PostAsync(
        InventoryPostingIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        // رمزٌ فارغ يُعيد تركيب العطب نفسه: حدثان بلا رمز هويةٌ واحدة (‏فخ-45).
        if (!intent.Event.IsAssigned)
        {
            return Result<PostingReceipt>.Failure(
                InventoryErrors.MissingEventCode(intent.DocumentType, intent.DocumentId));
        }

        // مركز التكلفة يُحلّ **قبل** أن يُكتب صفّ محاولة أو يُبنى طلب (‏ADR-0026).
        Result<string> centre = await _costCenters
            .ResolveAsync(intent.Tenant, Requested(intent.Dimensions), cancellationToken)
            .ConfigureAwait(false);

        if (centre.IsFailure)
        {
            return Result<PostingReceipt>.Failure(centre.Errors);
        }

        string documentId = intent.DocumentId.ToString("D", CultureInfo.InvariantCulture);
        string triggerCode = intent.Trigger.ToString();
        string eventCode = intent.Event.Value;

        InventoryPostingRow? attempt = await _database.Postings
            .FirstOrDefaultAsync(
                row => row.TenantId == intent.Tenant.Value
                       && row.DocumentType == intent.DocumentType
                       && row.DocumentId == documentId
                       && row.TriggerCode == triggerCode
                       && row.Generation == intent.Generation
                       && row.EventCode == eventCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (attempt is { State: InventoryPostingAttemptState.Posted, EntryId: { } posted })
        {
            // وصول ثانٍ بالهوية نفسها لا يفعل شيئاً ولا يُعدّ خطأ — مهما كان ترتيب الوصول.
            return Result<PostingReceipt>.Success(
                new PostingReceipt(posted, attempt.EntryNumber, string.Empty, true, 0, string.Empty, attempt.Generation));
        }

        string key = IdempotencyKeyOf(intent.DocumentType, intent.DocumentId, intent.Trigger, intent.Generation, intent.Event);

        if (attempt is null)
        {
            attempt = new InventoryPostingRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = intent.Tenant.Value,
                DocumentType = intent.DocumentType,
                DocumentId = documentId,
                TriggerCode = triggerCode,
                Generation = intent.Generation,
                IdempotencyKey = key,
                EventCode = eventCode,
                PartyId = intent.PartyId,
                DocumentDate = intent.DocumentDate,
            };
            _database.Postings.Add(attempt);
        }

        attempt.State = InventoryPostingAttemptState.Attempting;
        attempt.DocumentDate = intent.DocumentDate;
        attempt.AttemptCount++;
        attempt.LastAttemptAt = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        PostingRequest request = new()
        {
            Tenant = intent.Tenant,
            IdempotencyKey = new IdempotencyKey(key),
            Source = new SourceDocument(BabelModule.Inventory, intent.DocumentType, documentId),
            Trigger = intent.Trigger,
            DocumentDate = intent.DocumentDate,
            Narration = intent.Narration,
            Lines = [],
            Event = intent.Event,
            Amounts = intent.Amounts,
            Facts = intent.Facts,
            Dimensions = WithResolvedCostCenter(intent.Dimensions, centre.Value),
            Currency = intent.Currency,
            Generation = intent.Generation,
            Actor = intent.Actor,
        };

        Result<PostingReceipt> result = await _posting.PostAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            Error first = result.Errors[0];
            attempt.State = InventoryPostingAttemptState.Refused;
            attempt.FailureCode = first.Code;
            attempt.FailureMessageAr = Trim(first.MessageAr);
            attempt.FailureMessageEn = Trim(first.MessageEn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<PostingReceipt>.Failure(InventoryErrors.PostingRefused(result.Errors));
        }

        attempt.State = InventoryPostingAttemptState.Posted;
        attempt.EntryId = result.Value.JournalEntryId;
        attempt.EntryNumber = result.Value.EntryNumber;
        attempt.FailureCode = string.Empty;
        attempt.FailureMessageAr = string.Empty;
        attempt.FailureMessageEn = string.Empty;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static string? Requested(IReadOnlyList<PostingDimension> dimensions)
    {
        string? value = dimensions
            .FirstOrDefault(static d => string.Equals(d.Name, CostCenterDimension, StringComparison.Ordinal))?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<PostingDimension> WithResolvedCostCenter(
        IReadOnlyList<PostingDimension> dimensions,
        string centre)
    {
        List<PostingDimension> resolved = [];
        bool replaced = false;

        foreach (PostingDimension dimension in dimensions)
        {
            if (string.Equals(dimension.Name, CostCenterDimension, StringComparison.Ordinal))
            {
                resolved.Add(new PostingDimension(CostCenterDimension, centre));
                replaced = true;
                continue;
            }

            resolved.Add(dimension);
        }

        if (!replaced)
        {
            resolved.Add(new PostingDimension(CostCenterDimension, centre));
        }

        return resolved;
    }

    /// <summary>يُلحق مكوّناً مسبوقاً بطوله — فلا محتوى يدّعي أنه نهاية مكوّن وبداية آخر.</summary>
    private static void Append(StringBuilder canonical, string component)
        => canonical.Append(component.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(component);

    private static string Trim(string message) => message.Length <= 1000 ? message : message[..1000];
}
