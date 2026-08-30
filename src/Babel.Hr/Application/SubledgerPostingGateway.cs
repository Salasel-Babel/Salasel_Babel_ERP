using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Hr.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Hr.Application;

/// <summary>
/// نية ترحيل مستند: ما تصفه الوحدة، بلا حساب ولا جانب ولا مبلغ سطر.
/// <para>
/// <b>ولاحظ ما تحمله واحداً لا جمعاً:</b> <see cref="PartyId"/> طرفٌ واحد،
/// و<see cref="ControlEffect"/> عددٌ واحد. وهذا ليس تبسيطاً بل <b>حدُّ مسار القالب
/// نفسه</b>: <c>PostingPlanner.FromEvent</c> يحلّ طرف الدفتر المساعد من واقعةٍ واحدة
/// لكل طلب (<c>facts["subledger.employee"]</c>) ويقرأ مركز التكلفة وكل الأبعاد من
/// قاموسٍ واحد على مستوى الطلب. فطلبٌ واحد <b>لا يستطيع بنيةً</b> أن يحمل طرفين ولا
/// مركزين — ومن هنا جاءت حبيبيّة القسيمة، لا من ذوقٍ في التصميم.
/// </para>
/// </summary>
internal sealed record PostingIntent
{
    public required TenantId Tenant { get; init; }

    /// <summary>نوع المستند — الحقل الأول في هوية الإحكام لدى المحرك.</summary>
    public required string DocumentType { get; init; }

    /// <summary>
    /// معرّف المستند. <b>وفي استحقاق الرواتب هو معرّف القسيمة لا معرّف المسيّر</b>:
    /// نداءٌ واحد على المسيّر يُصدر N قيداً لكلٍّ هويّته.
    /// </summary>
    public required Guid DocumentId { get; init; }

    public required PostingTrigger Trigger { get; init; }

    /// <summary>رمز الحدث — <b>حقل في هوية الإحكام لا وصفٌ للقيد</b>.</summary>
    public required PostingEventCode Event { get; init; }

    public required DateOnly DocumentDate { get; init; }

    /// <summary>
    /// بيان القيد ثنائي اللغة.
    /// <para>
    /// <b>ويُركَّب من رمز الحدث والفترة والرمز المعتم وحدها — ولا نصّ حرّ فيه.</b>
    /// حقلا <c>memo</c> و<c>memo_ar</c> داخل الشكل القانوني v2، أي <b>داخل البايتات
    /// المُجزَّأة</b>، و<c>REVOKE UPDATE, DELETE</c> يجعل ما دخلها غير قابل للإزالة.
    /// فاسمُ موظف أو رقم هويته في بيانٍ حرّ يدخل سلسلة تجزئة لا يبلغها أي علاج محو.
    /// </para>
    /// </summary>
    public required LocalizedName Narration { get; init; }

    public required IReadOnlyList<PostingAmount> Amounts { get; init; }

    public required IReadOnlyList<PostingFact> Facts { get; init; }

    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>
    /// الطرف في الدفتر المساعد — <b>الرمز المعتم</b>، أو فراغٌ لمستندٍ لا طرف له
    /// (سداد التأمينات: سطره على حساب الالتزام بلا دفتر مساعد).
    /// </summary>
    public required string PartyId { get; init; }

    /// <summary>
    /// الأثر المتوقَّع على نقطة الضبط بمنطق «مدين ناقص دائن»، <b>لسطور دفتر الموظف
    /// وحدها</b>. وهو ما تُطابَق به الحركة مستنداً بمستند.
    /// </summary>
    public required decimal ControlEffect { get; init; }

    public required CurrencyCode Currency { get; init; }

    public UserId Actor { get; init; } = UserId.SystemActor;

    public int Generation { get; init; } = 1;
}

/// <summary>
/// بوابة الترحيل: الطريق الوحيد الذي تصل به وحدة الموارد البشرية إلى دفتر الأستاذ.
/// <para>
/// <b>وهي النسخة الرابعة</b> من هذا الشكل في هذا المستودع — بعد <c>Babel.Sales</c>
/// (‏<c>sales:v2:</c>) و<c>Babel.Purchasing</c> (‏<c>purchasing:v2:</c>) و
/// <c>Babel.Inventory</c> (‏<c>inventory:v1:</c>) — <b>ولا حارس يقارن الأربع</b>.
/// وانتزاعها إلى <c>Babel.Contracts</c> إيداعٌ مستقلّ لا شرطٌ على الرواتب، لكن الزيادة
/// <b>تُسجَّل ولا تُبتلع</b>: البند ت-10 في <c>docs/evidence/verification-debt.md</c>.
/// </para>
/// <para>
/// <b>الإحكام لكل مستند ومستقلّ عن الترتيب.</b> هوية الإحكام سداسية (نوع المستند ·
/// معرّفه · رمز الإطلاق · الجيل · رمز الحدث) داخل المستأجر. لا عدّاد تصاعدي لكل طرف،
/// ولا مقارنة مع تسلسل مُطبَّق: ذلك الشكل قيس وهو يُسقط بصمت (فخ-13).
/// </para>
/// <para>
/// وسجلّ المحاولة يُكتب <b>قبل</b> النداء ويُغلق <b>بعده</b>، فالرفض يترك المستند على
/// حاله ومعه سبب مكتوب — حالة متّسقة تُعاد المحاولة منها، لا نصف كتابة.
/// </para>
/// </summary>
internal sealed class SubledgerPostingGateway(
    HrDbContext database,
    IPostingService posting,
    ICostCenterResolver costCenters)
{
    /// <summary>اسم بُعد مركز التكلفة كما تعرفه المصفوفة والمخطّط.</summary>
    private const string CostCenterDimension = "cost_center";

    /// <summary>بادئة المفتاح وإصدار ترميزه — الإصدار في المفتاح كي يُقرأ شكله من قيمته.</summary>
    private const string KeyPrefix = "hr:v1:";

    private readonly HrDbContext _database = database;
    private readonly IPostingService _posting = posting;
    private readonly ICostCenterResolver _costCenters = costCenters;

    /// <summary>
    /// مفتاح الحصانة المشتقّ من هوية الإحكام السداسية <b>كاملةً</b>، وكلُّ مكوّن فيه
    /// مسبوقٌ بطوله قبل التجزئة: الطول الصريح يجعل حدود المكوّنات غير قابلة للتزوير،
    /// فلا يحتاج إلى فاصل «آمن» يُفترض أنه لا يظهر في البيانات.
    /// </summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="documentId">معرّف المستند — <b>القسيمة</b> في استحقاق الرواتب.</param>
    /// <param name="trigger">رمز الإطلاق.</param>
    /// <param name="generation">جيل الترحيل.</param>
    /// <param name="eventCode">رمز الحدث — مكوّن أصيل في الهوية.</param>
    public static string IdempotencyKeyOf(
        string documentType,
        Guid documentId,
        PostingTrigger trigger,
        int generation,
        PostingEventCode eventCode)
    {
        StringBuilder canonical = new();
        Append(canonical, "hr");
        Append(canonical, documentType);
        Append(canonical, documentId.ToString("N", CultureInfo.InvariantCulture));
        Append(canonical, trigger.ToString());
        Append(canonical, generation.ToString(CultureInfo.InvariantCulture));
        Append(canonical, eventCode.Value ?? string.Empty);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return KeyPrefix + Convert.ToHexStringLower(digest);
    }

    /// <summary>يرحّل نيّةً واحدة، ويُرجع إيصال المحرك.</summary>
    /// <param name="intent">النية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<Result<PostingReceipt>> PostAsync(PostingIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!intent.Event.IsAssigned)
        {
            return Result<PostingReceipt>.Failure(HrErrors.MissingEventCode(intent.DocumentType, intent.DocumentId));
        }

        // ── مركز التكلفة يُحلّ **قبل** أن يُكتب صفّ محاولة أو يُبنى طلب ────────
        // ‏ADR-0026: المذكور على المستند إن كان عاملاً، والافتراضي إن لم يُذكر شيء.
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

        DocumentPostingRow? attempt = await _database.Postings
            .FirstOrDefaultAsync(
                row => row.TenantId == intent.Tenant.Value
                       && row.DocumentType == intent.DocumentType
                       && row.DocumentId == documentId
                       && row.TriggerCode == triggerCode
                       && row.Generation == intent.Generation
                       && row.EventCode == eventCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (attempt is { State: PostingAttemptState.Posted, EntryId: { } posted })
        {
            // وصول ثانٍ بالهوية نفسها لا يفعل شيئاً ولا يُعدّ خطأ — مهما كان ترتيب الوصول.
            return Result<PostingReceipt>.Success(
                new PostingReceipt(posted, attempt.EntryNumber, string.Empty, true, 0, string.Empty, attempt.Generation));
        }

        string key = IdempotencyKeyOf(intent.DocumentType, intent.DocumentId, intent.Trigger, intent.Generation, intent.Event);

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
                EventCode = eventCode,
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
            Source = new SourceDocument(BabelModule.Hr, intent.DocumentType, documentId),
            Trigger = intent.Trigger,
            DocumentDate = intent.DocumentDate,
            Narration = intent.Narration,

            // ‏**سطور فارغة عمداً — ولا مفرّ من ذلك.** المسار الصريح لا يبلغ الرواتب
            // أصلاً: مخطّط PostingLine.role في العقد المنشور مجموعةٌ مغلقة من أربع عشرة
            // قيمة ليس فيها دور رواتب واحد، وPostingRoleCodes.Of يعرف الأربع عشرة نفسها
            // ويرمي على ما سواها. فبلوغُ الرواتب منه يكسر مخطّطاً منشوراً.
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
            attempt.State = PostingAttemptState.Refused;
            attempt.FailureCode = first.Code;
            attempt.FailureMessageAr = Trim(first.MessageAr);
            attempt.FailureMessageEn = Trim(first.MessageEn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<PostingReceipt>.Failure(HrErrors.PostingRefused(result.Errors));
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

    /// <summary>مركز التكلفة المذكور على المستند، أو <c>null</c> فالافتراضي.</summary>
    private static string? Requested(IReadOnlyList<PostingDimension> dimensions)
    {
        string? value = dimensions
            .FirstOrDefault(static d => string.Equals(d.Name, CostCenterDimension, StringComparison.Ordinal))?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// الأبعاد ومعها مركز التكلفة <b>مُحلّاً</b> — مستبدَلاً إن ذُكر، ومُضافاً إن غاب.
    /// <para>
    /// والإضافة عند الغياب هي الفارق كلّه: مسار القالب يورّث بُعد الطلب إلى <b>كل</b>
    /// سطر يولّده، والقاعدة تفرض مركز تكلفة على كل سطر قيد
    /// (<c>ck_journal_line_cost_center_present</c>)، فبُعدٌ غائب يُنتج ستّة سطور بلا
    /// مركز يرفضها المخطِّط بـ<c>MissingCostCenter</c>.
    /// </para>
    /// </summary>
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

    private static void Append(StringBuilder canonical, string component)
        => canonical.Append(component.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(component);

    private static string Trim(string message) => message.Length <= 1000 ? message : message[..1000];
}
