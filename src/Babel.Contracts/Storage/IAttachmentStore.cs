using Babel.SharedKernel;

namespace Babel.Contracts.Storage;

/// <summary>
/// <b>منفذ مخزن المرفقات — البايتات في المخزن، والمسار والوصف في القاعدة.</b>
/// <para>
/// <b>ولماذا يعيش المنفذ في <c>Babel.Contracts</c>:</b> المرفق يخصّ كل وحدة تقريباً —
/// صورة فاتورة مورد، ومستند تسليم، وعقد إيجار، وكشف بنك. و<c>ModuleMap.AllowedProjectReferences</c>
/// يعطي كل وحدة أفقية <c>{SharedKernel, Contracts, Core}</c> ولا شيء غيرها، و<b>القاعدة 3
/// تُفشل البناء</b> على أي مرجع خارجها. فمنفذٌ في مشروع تخزين مستقلّ يعني إمّا سطراً
/// جديداً في الخريطة لكل وحدة تريد مرفقاً — وهو فتحُ الخريطة الذي رُفض في ADR-0042 —
/// وإمّا وحدةً لا تستطيع أن تودِع مرفقاً أصلاً. <c>Babel.Contracts</c> هو الموضع المُعلَن
/// لهذا بعينه، وهو الشكل نفسه الذي يمرّ منه الترحيل و<c>IElectronicDocumentIntake</c>.
/// </para>
/// <para>
/// <b>والمنفذ لا يفترض نظام ملفّات.</b> لا مسار مطلق في أي توقيع، ولا <c>Stream</c> مفتوح
/// على القرص، ولا <c>FileInfo</c>. <see cref="StoredAttachment.ObjectKey"/> نصّ يفهمه
/// المحوّل وحده: مسارٌ نسبي عند محوّل نظام الملفّات، ومفتاح كائن عند مخزن كائنات.
/// </para>
/// <para>
/// <b>وكل عملية تأخذ المستأجر معامِلاً إلزامياً</b> — لا تُشتقّ من سياق ولا من الصفّ
/// المقروء. المعرّف المسرَّب مع مستأجر آخر لا يجد شيئاً، لأن المستأجر <b>جزء من
/// المفتاح</b> في الاستعلام وفي المسار على القرص معاً.
/// </para>
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// يودِع بايتات. يشمّ النوع من البايتات، ويقيس الحجم، ويجزّئ، ويكتب مرّة واحدة،
    /// ثم يسجّل الصفّ. <b>ولا يكتب فوق شيء أبداً</b>: إيداعٌ يحمل
    /// <see cref="AttachmentSubmission.Supersedes"/> يُنشئ إصداراً جديداً يشير إلى سلفه.
    /// </summary>
    /// <param name="submission">ما يُقدَّم — ومنه حقلان لا يُصدَّقان.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<StoredAttachment>> PutAsync(AttachmentSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>يجلب الوصف بلا بايتات. القراءة داخل المستأجر لا عبره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="id">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<StoredAttachment>> DescribeAsync(TenantId tenant, AttachmentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// يقرأ البايتات ومعها الوصف. <b>ويتحقّق من البصمة قبل أن يعيد بايتة واحدة</b>:
    /// ملفٌّ بُدِّل تحت المسار يُرفض هنا ولا يُسلَّم لقارئ.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="id">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<AttachmentContent>> OpenAsync(TenantId tenant, AttachmentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// يعيد قراءة البايتات ويقارن البصمة، ويعيد النتيجة <b>قيمةً لا استثناءً</b> —
    /// فالمقصود من هذا المسار أن يُشغَّل دورياً ويُبلَّغ عن الفارق، لا أن يسقط.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="id">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<AttachmentIntegrity>> VerifyAsync(TenantId tenant, AttachmentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// يجرد مرفقات مستأجر، مرشَّحةً على المستند المصدر ومصفَّحةً.
    /// <b>ولا بايتة تُقرأ هنا</b>: الجرد وصفٌ لا محتوى.
    /// </summary>
    /// <param name="query">السؤال — والمستأجر جزء منه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<AttachmentPage>> ListAsync(AttachmentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// يضع علامة سحب. <b>لا يحذف بايتة</b> — الاحتفاظ بسند القيد واجب نظامي، والسحب
    /// إعلانٌ عن حالة لا محو.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="id">المعرّف.</param>
    /// <param name="actor">من يسحب.</param>
    /// <param name="reasonKey">مفتاح السبب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<StoredAttachment>> WithdrawAsync(
        TenantId tenant,
        AttachmentId id,
        UserId actor,
        string reasonKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// تذكرة وصول قصيرة الأجل وموقّعة. <b>هي ما يُعطى لمتصفّح، لا المسار ولا المعرّف.</b>
/// </summary>
public sealed record AttachmentTicket
{
    /// <summary>الرمز الموقّع. نصّ آمن في المسار (‏base64url) بلا حشو.</summary>
    public required string Token { get; init; }

    /// <summary>المرفق الذي تفتحه.</summary>
    public required AttachmentId Id { get; init; }

    /// <summary>المستأجر الذي صُدرت له — <b>داخل البايتات الموقّعة</b>، لا بجانبها.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>لحظة الانتهاء.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>ما تحمله تذكرة بعد التحقّق من توقيعها ومن أنها لم تنتهِ.</summary>
public sealed record RedeemedTicket
{
    /// <summary>المرفق.</summary>
    public required AttachmentId Id { get; init; }

    /// <summary>المستأجر كما وقّعناه — <b>يُقارَن بمستأجر الجلسة ولا يحلّ محلّه</b>.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>حاملها الذي صدرت له.</summary>
    public required UserId Bearer { get; init; }

    /// <summary>لحظة الانتهاء.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// <b>منفذ التذاكر — إصدار وصولٍ محدود بزمن، وقراءته.</b>
/// <para>
/// وجودُه منفذاً مستقلاً مقصود: نقطة نهاية التنزيل تحتاج «هل يجوز لحامل هذا الرمز أن
/// يقرأ هذا المرفق الآن؟» ولا تحتاج المخزن نفسه لتجيب. والتذكرة <b>لا تُغني عن
/// نطاق المستأجر</b>: هي طبقة ثانية فوقه، ومستأجرها موقَّع داخلها ليُقارَن بمستأجر
/// الجلسة عند الاستهلاك.
/// </para>
/// </summary>
public interface IAttachmentTickets
{
    /// <summary>يصدر تذكرة قصيرة الأجل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="id">المرفق.</param>
    /// <param name="bearer">الحامل.</param>
    /// <param name="lifetime">العمر — يُرفض إن تجاوز السقف المُعلَن في الإعدادات.</param>
    Result<AttachmentTicket> Issue(TenantId tenant, AttachmentId id, UserId bearer, TimeSpan lifetime);

    /// <summary>يتحقّق من رمز ويعيد ما يحمله. التوقيع أولاً، ثم الانتهاء.</summary>
    /// <param name="token">الرمز.</param>
    Result<RedeemedTicket> Redeem(string token);
}
