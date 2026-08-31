using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>نية ترحيل مستند: ما تصفه الوحدة، بلا حساب ولا جانب ولا مبلغ سطر.</summary>
internal sealed record PostingIntent
{
    public required TenantId Tenant { get; init; }

    /// <summary>نوع المستند — الحقل الأول في هوية الإحكام لدى المحرك.</summary>
    public required string DocumentType { get; init; }

    public required Guid DocumentId { get; init; }

    public required PostingTrigger Trigger { get; init; }

    /// <summary>
    /// رمز الحدث — <b>حقل في هوية الإحكام لا وصفٌ للقيد</b>. المستند الواحد يُنتج
    /// حدثين مختلفين عند الإطلاق نفسه في حالات يومية (فاتورة تعترف بالإيراد وتُنزل
    /// المخزون بالتكلفة)، وبدون هذا الحقل في الهوية يُبتلع الثاني بصمت.
    /// </summary>
    public required PostingEventCode Event { get; init; }

    public required DateOnly DocumentDate { get; init; }

    public required LocalizedName Narration { get; init; }

    public required IReadOnlyList<PostingAmount> Amounts { get; init; }

    public required IReadOnlyList<PostingFact> Facts { get; init; }

    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>الطرف في الدفتر المساعد — يُحفظ في سجلّ المحاولة كي تُسمّيه المطابقة.</summary>
    public required string PartyId { get; init; }

    /// <summary>الأثر المتوقَّع على نقطة الضبط بمنطق «دائن ناقص مدين» — الذمم الدائنة موجبة.</summary>
    public required decimal ControlEffect { get; init; }

    public required CurrencyCode Currency { get; init; }

    public UserId Actor { get; init; } = UserId.SystemActor;

    public int Generation { get; init; } = 1;
}

/// <summary>
/// بوابة الترحيل: الطريق الوحيد الذي تصل به وحدة المشتريات إلى دفتر الأستاذ.
/// <para>
/// <b>الإحكام لكل مستند ومستقلّ عن الترتيب.</b> هوية الإحكام هي الخماسية
/// (نوع المستند · معرّفه · رمز الإطلاق · الجيل · <b>رمز الحدث</b>)، وهذا هو ما تحمله
/// البوابة بالضبط. لا عدّاد تصاعدي لكل عميل، ولا مقارنة <c>&lt;</c> مع تسلسل مُطبَّق:
/// ذلك الشكل قيس وهو يُسقط بصمت 500 ريال من 1,500 عند وصول خارج الترتيب (فخ-13).
/// </para>
/// <para>
/// <b>ولماذا رمز الحدث هنا لا في الدفتر وحده:</b> هذه البوابة تحتفظ بجدول محاولات
/// خاصّ بها، وتُقصِّر الطريق <b>قبل</b> بلوغ الدفتر أصلاً. فإصلاح هوية الدفتر وحده
/// لا يُغيّر شيئاً: التنفيذ لا يبلغه. وقيس أن الحدث الثاني كان يعود بإيصال
/// <c>WasAlreadyPosted = true</c> يحمل معرّف <b>القيد الأول</b>، فتخزّن الوحدة معرّف
/// قيد الإيراد في مكان قيد التكلفة (ADR-0017).
/// </para>
/// <para>
/// وسجلّ المحاولة يُكتب <b>قبل</b> النداء ويُغلق <b>بعده</b>، فالرفض يترك المستند
/// على حاله ومعه سبب مكتوب — حالة متّسقة تُعاد المحاولة منها، لا نصف كتابة.
/// </para>
/// </summary>
internal sealed class SubledgerPostingGateway(
    PurchasingDbContext database,
    IPostingService posting,
    ICostCenterResolver costCenters)
{
    /// <summary>اسم بُعد مركز التكلفة كما تعرفه المصفوفة والمخطّط.</summary>
    private const string CostCenterDimension = "cost_center";

    /// <summary>بادئة المفتاح وإصدار ترميزه — الإصدار في المفتاح كي يُقرأ شكله من قيمته.</summary>
    private const string KeyPrefix = "purchasing:v2:";

    private readonly PurchasingDbContext _database = database;
    private readonly IPostingService _posting = posting;
    private readonly ICostCenterResolver _costCenters = costCenters;

    /// <summary>
    /// مفتاح الحصانة المشتقّ من هوية الإحكام الخماسية <b>كاملةً</b>.
    /// <para>
    /// <b>لماذا بصمة لا سلسلة نصية مقروءة:</b> عقد <see cref="IdempotencyKey"/> يحدّ
    /// المفتاح بـ128 محرفاً، ونوع المستند وحده يبلغ 64 محرفاً ورمز الحدث 128 — فالسلسلة
    /// المقروءة تتجاوز الحدّ في أسوأ حالاتها المشروعة. والأخطر أن السلسلة المبنية
    /// بالوصل على فاصل قد يحتويه أحد المكوّنات هي <b>عطب تصادم بذاتها</b>، وقد لُدغ
    /// هذا المستودع به من قبل في <c>source_ref</c> المدموج حيث أنتج
    /// <c>("A/B","C")</c> و<c>("A","B/C")</c> البايتات نفسها.
    /// </para>
    /// <para>
    /// ولذلك يُرمَّز كل مكوّن <b>مسبوقاً بطوله</b> قبل التجزئة: الطول الصريح يجعل حدود
    /// المكوّنات غير قابلة للتزوير مهما كان محتواها — ولا حاجة معه إلى فاصل «آمن»
    /// يُفترض أنه لا يظهر في البيانات. والبصمة تجعل الناتج ثابت العرض دائماً.
    /// والقراءة البشرية لم تُفقد: صفّ <c>document_posting</c> يحمل حقول الهوية الخمسة
    /// كلها صريحةً، وكذلك رأس القيد في الدفتر.
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
        Append(canonical, "purchasing");
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

        // رمزٌ فارغ يُعيد تركيب العطب نفسه: حدثان بلا رمز هويةٌ واحدة. والمحرك يرفضه
        // بـ‏ledger.posting.missing_event_code، والبوابة ترفضه هنا بالمنطق نفسه كي لا
        // يُكتب صفّ محاولة بهوية ناقصة أصلاً.
        if (!intent.Event.IsAssigned)
        {
            return Result<PostingReceipt>.Failure(PurchasingErrors.MissingEventCode(intent.DocumentType, intent.DocumentId));
        }

        // ── مركز التكلفة يُحلّ **قبل** أن يُكتب صفّ محاولة أو يُبنى طلب ────────
        // ‏ADR-0026: المذكور على المستند إن كان عاملاً، والافتراضي إن لم يُذكر شيء. والحلّ
        // هنا لا في الدفتر: البوّابة هي الموضع الذي يعرف المنشأة ويستطيع أن يسأل النواة،
        // والدفتر يكتب ما وصله. وموضعه قبل صفّ المحاولة كي لا يُكتب أثرٌ لطلبٍ لن يُبنى.
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

        // ‏**رمز الحدث في الشرط.** بدونه كان هذا الاستعلام نفسه — لا الفهرس الفريد —
        // هو ما يُسقط الحدث الثاني ويُرجع إيصال الأول. الفهرس لم يكن يُنتهَك أصلاً
        // لأن التنفيذ لم يكن يصل إليه.
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
            Source = new SourceDocument(BabelModule.Purchasing, intent.DocumentType, documentId),
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
            attempt.State = PostingAttemptState.Refused;
            attempt.FailureCode = first.Code;
            attempt.FailureMessageAr = Trim(first.MessageAr);
            attempt.FailureMessageEn = Trim(first.MessageEn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<PostingReceipt>.Failure(PurchasingErrors.PostingRefused(result.Errors));
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

    /// <summary>
    /// الأبعاد ومعها مركز التكلفة <b>مُحلّاً</b> — مستبدَلاً إن ذُكر، ومُضافاً إن غاب.
    /// <para>
    /// والإضافة عند الغياب هي الفارق كلّه: مسار القالب يقرأ مركز سطوره من هذا البُعد
    /// وحده، فبُعدٌ غائب كان يُنتج سطراً بلا مركز — يمرّ صامتاً قبل هذا التغيير، ويرفضه
    /// المخطّط بعده.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// يُلحق مكوّناً مسبوقاً بطوله. الطول الصريح يجعل حدّ المكوّن غير قابل للتزوير:
    /// لا محتوى يستطيع أن يدّعي أنه نهاية مكوّن وبداية آخر.
    /// </summary>
    private static void Append(StringBuilder canonical, string component)
        => canonical.Append(component.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(component);

    private static string Trim(string message) => message.Length <= 1000 ? message : message[..1000];
}
