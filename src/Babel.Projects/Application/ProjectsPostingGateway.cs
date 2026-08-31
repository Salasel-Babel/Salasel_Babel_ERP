using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>نية ترحيل مستند مقاولات: ما تصفه الوحدة، بلا حساب ولا جانب ولا مبلغ سطر.</summary>
internal sealed record PostingIntent
{
    public required TenantId Tenant { get; init; }

    /// <summary>نوع المستند — الحقل الأول في هوية الإحكام.</summary>
    public required string DocumentType { get; init; }

    public required Guid DocumentId { get; init; }

    public required PostingTrigger Trigger { get; init; }

    /// <summary>رمز الحدث — <b>حقل في هوية الإحكام لا وصفٌ للقيد</b> (ADR-0016 · ADR-0017).</summary>
    public required PostingEventCode Event { get; init; }

    public required DateOnly DocumentDate { get; init; }

    public required LocalizedName Narration { get; init; }

    public required IReadOnlyList<PostingAmount> Amounts { get; init; }

    public required IReadOnlyList<PostingFact> Facts { get; init; }

    /// <summary>
    /// أبعاد الطلب. <b>ولا يُرسَل فيها <c>boq_item</c> على مستند</b>: مسار القالب
    /// يستنسخ أبعاد الطلب على <b>كل</b> سطور القيد، فقيمةٌ واحدة تُختَم على سطور
    /// المستند لا سطور البند. وتفصيل البند يبقى في <c>certificate_line</c>.
    /// </summary>
    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>الطرف في الدفتر المساعد — يُحفظ في سجلّ المحاولة كي تُسمّيه المطابقة.</summary>
    public required string PartyId { get; init; }

    /// <summary>نوع الدفتر المساعد الذي يتحرّك بهذا المستند — به تُقسَّم المطابقة على دفترين.</summary>
    public required string SubledgerKind { get; init; }

    /// <summary>الأثر المتوقَّع على نقطة الضبط بمنطق «مدين ناقص دائن».</summary>
    public required decimal ControlEffect { get; init; }

    public required CurrencyCode Currency { get; init; }

    public UserId Actor { get; init; } = UserId.SystemActor;

    public int Generation { get; init; } = 1;
}

/// <summary>
/// بوابة الترحيل: الطريق الوحيد الذي تصل به وحدة المقاولات إلى دفتر الأستاذ.
/// <para>
/// <b>الشكل هو شكل <c>Babel.Sales.SubledgerPostingGateway</c> حرفاً</b>، ولسببٍ واحد:
/// هوية الإحكام قاعدةٌ واحدة، ونسختان منها تنحرفان عند أول تعديل. والاختلاف الوحيد
/// عن أصلها عمودٌ ثالثٌ في صفّ المحاولة — <c>SubledgerKind</c> — لأن هذه الوحدة
/// <b>تُحرّك دفترين مساعدين</b> لا دفتراً واحداً (<c>customer</c> و<c>subcontractor</c>)،
/// ومطابقةٌ تجمع أثرَي دفترين في رقم واحد لا تُطابق شيئاً.
/// </para>
/// <para>
/// <b>وحقن مركز التكلفة قبل كل شيء.</b> القيد <c>ck_journal_line_cost_center_present</c>
/// يرفض على مستوى قاعدة البيانات، ويرفضه <c>PostingPlanner</c> قبله بـ
/// <c>ledger.posting.missing_cost_center</c>، ومسار القالب يقرأ المركز من <b>أبعاد
/// الطلب وحدها</b>. فبلا هذا الحقن تُرفض <b>كل</b> ترحيلة في هذه الوحدة — ولا تصميم
/// من التصاميم الثلاثة التي سبقت هذا البناء وضعه.
/// </para>
/// <para>
/// وسجلّ المحاولة يُكتب <b>قبل</b> النداء ويُغلق <b>بعده</b>، فالرفض يترك المستند على
/// حاله ومعه سبب مكتوب — حالة متّسقة تُعاد المحاولة منها، لا نصف كتابة.
/// </para>
/// </summary>
internal sealed class ProjectsPostingGateway(
    ProjectsDbContext database,
    IPostingService posting,
    ICostCenterResolver costCenters)
{
    /// <summary>اسم بُعد مركز التكلفة كما تعرفه المصفوفة والمخطّط.</summary>
    internal const string CostCenterDimension = "cost_center";

    /// <summary>اسم بُعد المشروع كما تعرفه المصفوفة والمخطّط.</summary>
    internal const string ProjectDimension = "project";

    /// <summary>بادئة المفتاح وإصدار ترميزه — الإصدار في المفتاح كي يُقرأ شكله من قيمته.</summary>
    private const string KeyPrefix = "projects:v1:";

    private readonly ProjectsDbContext _database = database;
    private readonly IPostingService _posting = posting;
    private readonly ICostCenterResolver _costCenters = costCenters;

    /// <summary>
    /// مفتاح الحصانة المشتقّ من هوية الإحكام <b>كاملةً</b>: خمسة مكوّنات، كلٌّ منها
    /// <b>مسبوقٌ بطوله</b> قبل التجزئة.
    /// <para>
    /// والطول الصريح لا الفاصل: مفتاحٌ مبنيٌّ بالوصل على فاصل قد يحتويه أحد المكوّنات
    /// هو عطب تصادم بذاته، ولُدغ هذا المستودع به من قبل في <c>source_ref</c> المدموج
    /// حيث أنتج <c>("A/B","C")</c> و<c>("A","B/C")</c> البايتات نفسها. والبصمة تجعل
    /// الناتج ثابت العرض تحت حدّ <c>IdempotencyKey</c> البالغ 128 محرفاً.
    /// </para>
    /// </summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="documentId">معرّف المستند.</param>
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
        Append(canonical, "projects");
        Append(canonical, documentType);
        Append(canonical, documentId.ToString("N", CultureInfo.InvariantCulture));
        Append(canonical, trigger.ToString());
        Append(canonical, generation.ToString(CultureInfo.InvariantCulture));
        Append(canonical, eventCode.Value ?? string.Empty);

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return KeyPrefix + Convert.ToHexStringLower(digest);
    }

    public async Task<Result<PostingReceipt>> PostAsync(PostingIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        // رمزٌ فارغ يُعيد تركيب العطب نفسه: حدثان بلا رمز هويةٌ واحدة (فخ-45).
        if (!intent.Event.IsAssigned)
        {
            return Result<PostingReceipt>.Failure(
                ProjectsErrors.MissingEventCode(intent.DocumentType, intent.DocumentId));
        }

        // مركز التكلفة يُحلّ **قبل** أن يُكتب صفّ محاولة أو يُبنى طلب — كي لا يُكتب أثرٌ
        // لطلبٍ لن يُبنى، ولأن الوحدة لا تعرف شجرة المراكز ولا الافتراضي (ADR-0026).
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

        // **رمز الحدث في الشرط.** بدونه يكون هذا الاستعلام — لا الفهرس الفريد — هو ما
        // يُسقط الحدث الثاني ويُرجع إيصال الأول، والفهرس لا يُنتهَك أصلاً لأن التنفيذ
        // لا يصل إليه.
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
            // والحكم من هنا، من **بوّابة الترحيل**، لا من مقارنة حالة على المستند.
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
                SubledgerKind = intent.SubledgerKind,
                DocumentDate = intent.DocumentDate,
            };
            _database.Postings.Add(attempt);
        }

        attempt.State = PostingAttemptState.Attempting;
        attempt.DocumentDate = intent.DocumentDate;
        attempt.SubledgerKind = intent.SubledgerKind;
        attempt.ControlEffect = intent.ControlEffect;
        attempt.AttemptCount++;
        attempt.LastAttemptAt = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        PostingRequest request = new()
        {
            Tenant = intent.Tenant,
            IdempotencyKey = new IdempotencyKey(key),
            Source = new SourceDocument(BabelModule.Projects, intent.DocumentType, documentId),
            Trigger = intent.Trigger,
            DocumentDate = intent.DocumentDate,
            Narration = intent.Narration,

            // ‏**مسار القالب حصراً**: القائمة فارغة عمداً. المسار الصريح يقرأ جسراً يصل
            // إلى أربعة عشر دوراً فقط من ستّة وسبعين، ولا يبلغ retention_receivable ولا
            // subcontractor_advance ولا subcontractor_cost ولا ap_subcontractor_control
            // ولا contract_revenue؛ ويقرأ مجموعةً مغلقة **منشورة** لأنواع الدفاتر
            // المساعدة ليس فيها المقاول من الباطن. فسلوكه يحوّل «لا تغيير على العقد»
            // إلى توسيع معدودٍ منشور بلا مقابل.
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
            return Result<PostingReceipt>.Failure(ProjectsErrors.PostingRefused(result.Errors));
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
    /// <param name="dimensions">أبعاد المستند.</param>
    private static string? Requested(IReadOnlyList<PostingDimension> dimensions)
    {
        string? value = dimensions
            .FirstOrDefault(static d => string.Equals(d.Name, CostCenterDimension, StringComparison.Ordinal))?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>الأبعاد ومعها مركز التكلفة <b>مُحلّاً</b> — مستبدَلاً إن ذُكر، ومُضافاً إن غاب.</summary>
    /// <param name="dimensions">أبعاد المستند كما وصفتها الوحدة.</param>
    /// <param name="centre">المركز المُحلّ.</param>
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

    /// <summary>يُلحق مكوّناً مسبوقاً بطوله، فلا محتوى يدّعي أنه نهاية مكوّن وبداية آخر.</summary>
    private static void Append(StringBuilder canonical, string component)
        => canonical.Append(component.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(component);

    private static string Trim(string message) => message.Length <= 1000 ? message : message[..1000];
}
