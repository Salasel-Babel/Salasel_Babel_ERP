namespace Babel.Core.Persistence;

/// <summary>
/// صفّ قيد تدقيق — <b>مُلحَق ولا يُعدَّل ولا يُحذف</b>.
/// <para>
/// <c>internal</c> كسائر صفوف الاستمرارية (القاعدة 5): ما يعبر حدّ الوحدة هو
/// <c>Babel.Core.Audit.AuditEntry</c>، لا كيان EF.
/// </para>
/// <para>
/// <b>ولا عمود «محذوف» ولا عمود «مُعدَّل»:</b> الحصانة هنا ليست غياب مسارٍ في الشجرة بل
/// صلاحيةٌ مسحوبة في <c>CoreGrants.sql</c> ومُشغّلٌ في <c>CoreAppendOnlyTriggers.sql</c>
/// يرفض <c>UPDATE</c> و<c>DELETE</c> و<c>TRUNCATE</c> <b>ولو كان الفاعل هو المالك</b>
/// (ADR-0002 · ADR-0003). فسجلّ التدقيق في نظامٍ محاسبي شاهدٌ على من فعل ماذا، وشاهدٌ
/// يُعدَّل ليس شاهداً.
/// </para>
/// </summary>
internal sealed class AuditEntryRow
{
    /// <summary>
    /// رقم تسلسلي يُولّده المحرّك — وهو المفتاح.
    /// <para>
    /// <b>ولماذا لا يكون المفتاح (المستأجر، اللحظة، الفاعل):</b> قيدان في الميكروثانية
    /// نفسها من الفاعل نفسه على الموضوع نفسه <b>ممكنان</b> — وهما قيدان لا قيد. ومفتاحٌ
    /// طبيعيّ كهذا كان سيبتلع الثاني بصمت بـ<c>on conflict do nothing</c> أو يرمي
    /// ‏23505 فيُسقط الطلب. والتسلسل كذلك هو ما يفصل قيدين وقعا في اللحظة نفسها عند
    /// القراءة، فالترتيب يبقى معرَّفاً تماماً لا «أيّهما شاء المُخطِّط».
    /// </para>
    /// </summary>
    public long SequenceNo { get; set; }

    /// <summary>المستأجر — ونطاقه مفروضٌ في كل استعلام قراءة.</summary>
    public Guid TenantId { get; set; }

    /// <summary>الفاعل.</summary>
    public Guid ActorId { get; set; }

    /// <summary>لحظة الوقوع بتوقيت UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>رمز الإجراء الثابت، مثل <c>entitlement.changed</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// موضوع الإجراء — <c>text</c> بلا حدّ طول وبلا قيد «غير فارغ».
    /// <para>
    /// <b>وهذا مقصود:</b> الموضوع نصٌّ مُركَّب من بيانات المستأجر (اسمُ منشأة، قائمةُ
    /// أنواع مستندات مفصولة). وحدُّ طولٍ يجعل <c>INSERT</c> يرمي 22001 وقيدُ «غير فارغ»
    /// يجعله يرمي 23514 — وكلاهما <b>يُسقط القيد</b>. وقيدٌ لم يُلتقَط لا يُستعاد لاحقاً،
    /// فرفضُ الالتقاط أسوأ من قبول نصٍّ فارغ. (ملفُّ قدراتٍ بلا أنواع مستندات يُنتج
    /// موضوعاً فارغاً فعلاً — انظر <c>CapabilityProfileService</c>.)
    /// </para>
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>تفصيل نصّي اختياري.</summary>
    public string? Details { get; set; }
}

/// <summary>
/// صفّ استخدامٍ على محور الوحدة — سجلٌّ مُلحَق لا عدّاد.
/// <para>
/// <b>ولماذا سجلٌّ مُلحَق:</b> لأن <c>InMemoryUsageStore</c> ليس عدّاداً بنافذة زمنية بل
/// كيسُ أحداثٍ يُجمَع عند القراءة على (المستأجر × شهر الفوترة). فالنظير الدائم لكيس
/// أحداث هو جدولُ أحداث، لا صفُّ عدّادٍ يُزاد بـ<c>update</c>. ولو صار عدّاداً لضاع
/// «مَن ومتى وأيّ عملية»، ولصار إعادةُ حساب شهرٍ مضى بتعريفٍ آخر مستحيلة —
/// و<b>لا استعلام يستخرج ما لم يُكتب</b>.
/// </para>
/// </summary>
internal sealed class ModuleUsageRow
{
    /// <summary>الرقم التسلسلي — المفتاح، للسبب نفسه في <see cref="AuditEntryRow"/>.</summary>
    public long SequenceNo { get; set; }

    /// <summary>المستأجر.</summary>
    public Guid TenantId { get; set; }

    /// <summary>الوحدة بقيمتها العددية — وهي هوية الوحدة المعلنة في <c>BabelModule</c>.</summary>
    public int Module { get; set; }

    /// <summary>اسم العملية، مثل <c>Core.FoundedCompany.Initialise</c>.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>من نفّذ العملية.</summary>
    public Guid ActorId { get; set; }

    /// <summary>لحظة الوقوع بتوقيت UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>الكمية المقيسة — عدد صحيح لا كسر، فلا <c>double</c> يمرّ هنا (القاعدة 4).</summary>
    public long Quantity { get; set; }
}

/// <summary>
/// صفّ نشاطٍ على محور المستخدم — المحور الثاني للتسعير، وسجلٌّ مُلحَق كذلك.
/// <para>
/// وحالةُ الاستحقاق <b>تُلتقَط ولا تُحتسَب</b> (انظر <c>UserActivityEvent</c>): وجودُ
/// العمود هو ما يجعل أيّ تعريفٍ لـ«المستخدم الفعّال» قابلاً للحساب بأثرٍ رجعي.
/// </para>
/// </summary>
internal sealed class UserActivityRow
{
    /// <summary>الرقم التسلسلي — المفتاح.</summary>
    public long SequenceNo { get; set; }

    /// <summary>المستأجر.</summary>
    public Guid TenantId { get; set; }

    /// <summary>المستخدم.</summary>
    public Guid UserId { get; set; }

    /// <summary>الوحدة بقيمتها العددية.</summary>
    public int Module { get; set; }

    /// <summary>اسم النشاط.</summary>
    public string Activity { get; set; } = string.Empty;

    /// <summary>لحظة الوقوع بتوقيت UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>حالة الاستحقاق التي وقع النشاط تحتها، من المجموعة المغلقة الثلاثية.</summary>
    public string EntitlementState { get; set; } = string.Empty;
}

/// <summary>حالات الاستحقاق كما تُكتب في المخطّط. مجموعة مغلقة يقابلها قيد تحقّق.</summary>
internal static class EntitlementStates
{
    /// <summary>لم تُشترَ الوحدة قط.</summary>
    public const string NotEntitled = "not_entitled";

    /// <summary>اشتُريت ثم انقضى الاشتراك.</summary>
    public const string ReadOnly = "read_only";

    /// <summary>مشتراة وفاعلة.</summary>
    public const string Entitled = "entitled";

    /// <summary>يحوّل الحالة إلى نصّ العمود.</summary>
    /// <param name="state">الحالة.</param>
    public static string ToColumn(Babel.Core.Entitlement.EntitlementState state) => state switch
    {
        Babel.Core.Entitlement.EntitlementState.NotEntitled => NotEntitled,
        Babel.Core.Entitlement.EntitlementState.ReadOnly => ReadOnly,
        Babel.Core.Entitlement.EntitlementState.Entitled => Entitled,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "حالة استحقاق خارج المجموعة المغلقة."),
    };

    /// <summary>يقرأ نصّ العمود حالةً — ونصٌّ مجهول يُرفض ولا يُقرأ افتراضاً.</summary>
    /// <param name="column">نصّ العمود.</param>
    public static Babel.Core.Entitlement.EntitlementState FromColumn(string column) => column switch
    {
        NotEntitled => Babel.Core.Entitlement.EntitlementState.NotEntitled,
        ReadOnly => Babel.Core.Entitlement.EntitlementState.ReadOnly,
        Entitled => Babel.Core.Entitlement.EntitlementState.Entitled,
        _ => throw new InvalidOperationException("حالة استحقاق مخزَّنة لا يعرفها النوع: «" + column + "»."),
    };
}
