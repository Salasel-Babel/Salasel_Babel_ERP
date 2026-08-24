using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// عقد الترحيل: الطريق الوحيد الذي تصل به أي وحدة إلى دفتر الأستاذ.
/// <para>
/// «لا وحدة تكتب في جداول Ledger مباشرة — الكتابة عبر محرك الترحيل فقط»
/// (CONTRIBUTING §3 بند 1 · وثيقة المعمارية §13). القاعدة مفروضة بثلاث طبقات:
/// (1) لا مرجع مشروع من أي وحدة أفقية إلى Babel.Ledger،
/// (2) أنواع استمرارية الدفتر <c>internal</c>،
/// (3) صلاحيات PostgreSQL: <c>INSERT</c> و<c>SELECT</c> فقط للدور التطبيقي (وثيقة المعمارية §3.2).
/// </para>
/// </summary>
public sealed record PostingRequest
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>
    /// مفتاح الحصانة ضد التكرار. القاعدة المعمارية 4: مستقل عن الترتيب،
    /// لأن مزامنة نقاط البيع دون اتصال تُسلّم خارج الترتيب بطبيعتها.
    /// </summary>
    public required IdempotencyKey IdempotencyKey { get; init; }

    /// <summary>المستند المصدر.</summary>
    public required SourceDocument Source { get; init; }

    /// <summary>الحدث الذي أطلق الترحيل.</summary>
    public required PostingTrigger Trigger { get; init; }

    /// <summary>تاريخ المستند الميلادي. الفترة المالية تُشتق منه داخل الدفتر.</summary>
    public required DateOnly DocumentDate { get; init; }

    /// <summary>بيان القيد ثنائي اللغة.</summary>
    public required LocalizedName Narration { get; init; }

    /// <summary>
    /// سطور الطلب بأدوارها ومبالغها — <b>وهي وحدها ما يختار المسار</b>.
    /// <para>
    /// فارغة ⇒ مسار القالب: القالب في المصفوفة يولّد السطور، والوحدة تُسلّم
    /// <see cref="Amounts"/> و<see cref="Facts"/> فقط. غير فارغة ⇒ المسار الصريح
    /// (قيد يومية يدوي). و<see cref="Event"/> <b>إلزام في الحالتين</b>.
    /// </para>
    /// </summary>
    public required IReadOnlyList<PostingLine> Lines { get; init; }

    /// <summary>
    /// الحدث في مصفوفة الترحيل — <b>حقل إلزامي غير فارغ، على المسارين معاً</b>.
    /// <para>
    /// على مسار القالب يقرأ المحرك قالب الحدث ويولّد السطور منه: يحلّ كل دور عبر خريطة
    /// المستأجر (مع المؤهلات)، ويقيّم السطور المشروطة، ويحسب المبالغ من التعابير الخطية،
    /// ويتحقق من الأبعاد الإلزامية. وعلى المسار الصريح لا يولّد الرمز سطراً — لكنه يبقى
    /// إلزامياً لأنه <b>جزء من هوية الترحيل</b>: الرمز يعطي القيد هويّته والسطور تعطيه
    /// محتواه، ورمزٌ فارغ يبتلع الحدث الثاني بصمت (‏ADR-0016 · ADR-0017).
    /// </para>
    /// </summary>
    public PostingEventCode Event { get; init; } = PostingEventCode.None;

    /// <summary>مفردات المبالغ التي يقرؤها قالب الحدث. لا معنى لها في المسار الصريح.</summary>
    public IReadOnlyList<PostingAmount> Amounts { get; init; } = [];

    /// <summary>وقائع السياق التي تُقيَّم عليها الشروط وقواعد الحجب.</summary>
    public IReadOnlyList<PostingFact> Facts { get; init; } = [];

    /// <summary>الأبعاد التحليلية على مستوى الطلب. السطر قد يضيف عليها.</summary>
    public IReadOnlyList<PostingDimension> Dimensions { get; init; } = [];

    /// <summary>الدفتر داخل الشركة. نطاق الترقيم ونطاق السلسلة معاً (‏ADR-0007 · ADR-0008).</summary>
    public string Book { get; init; } = "MAIN";

    /// <summary>عملة القيد. الافتراضي عملة الشركة.</summary>
    public CurrencyCode Currency { get; init; }

    /// <summary>
    /// سعر صرف عملة القيد إلى عملة الشركة. <c>decimal</c> بمقياس 8 ولا شيء غيره
    /// (Rule04 — الاسم نفسه يحمل الكلمة <c>Rate</c> فيلتقطه الفحص).
    /// <para>
    /// <b>والتوازن يُفحص بعملة الشركة لا بعملة الحركة:</b> قيدٌ متوازن بالدولار
    /// وغير متوازن بالريال قيدٌ غير متوازن — والمشغّل المؤجَّل عند COMMIT يفحص
    /// أعمدة الشركة تحديداً.
    /// </para>
    /// </summary>
    public decimal ExchangeRate { get; init; } = 1m;

    /// <summary>
    /// جيل الترحيل. يبدأ من 1 ولا يزيد إلا بعد <b>عكس مشروع</b>، فيُتيح إعادة ترحيل
    /// المستند نفسه مصحَّحاً بمفتاح إحكام مختلف. الزيادة بلا عكس سابق مرفوضة.
    /// </summary>
    public int Generation { get; init; } = 1;

    /// <summary>الفاعل الذي طلب الترحيل. يدخل البايتات المُجزَّأة.</summary>
    public UserId Actor { get; init; } = UserId.SystemActor;

    /// <summary>إذن استثنائي بالترحيل في فترة مقفلة. <c>null</c> = لا استثناء، وهو الأصل.</summary>
    public ClosedPeriodAuthorisation? ClosedPeriodAuthorisation { get; init; }
}
