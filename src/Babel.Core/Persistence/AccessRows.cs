namespace Babel.Core.Persistence;

/// <summary>
/// عضوية مستخدم في منشأة — <b>مصدرُ ما يبلغه اعتمادُه</b>.
/// <para>
/// <c>internal</c> كسائر صفوف الاستمرارية (القاعدة 5): ما يعبر حدّ الوحدة هو
/// <c>Babel.Core.Access.Membership</c>، لا كيان EF.
/// </para>
/// <para>
/// <b>ولا عمود «محذوف» هنا ولا مسار حذف</b>: سحبُ عضوية إزالةُ صفّ فعلاً — وهو الفرق
/// عن الدفتر عمداً. فالعضوية ليست سجلّاً محاسبياً بل <b>صلاحيةُ وصولٍ جارية</b>، وأثرُها
/// التاريخي محفوظ في سجلّ التدقيق بمن منحها ومتى. وصلاحيةٌ «موقوفة» تبقى صفّاً في جدول
/// وصولٍ هي بالضبط الشكل الذي يُنسى فيه أحدهم مُفعَّلاً.
/// </para>
/// </summary>
internal sealed class AccessMembershipRow
{
    /// <summary>المنشأة.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>المستخدم.</summary>
    public Guid UserId { get; set; }

    /// <summary>المستأجر الذي تنتمي إليه المنشأة.</summary>
    public Guid TenantId { get; set; }

    /// <summary>الدور: <c>reader</c> أو <c>contributor</c> أو <c>owner</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>الاسم العربي المعروض — السجلّ (ADR-0021).</summary>
    public string DisplayNameAr { get; set; } = string.Empty;

    /// <summary>لحظة المنح.</summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>من منح العضوية.</summary>
    public Guid GrantedBy { get; set; }
}

/// <summary>
/// اعتماد انتساب: دعوةٌ تُقبل <b>مرّة واحدة</b>.
/// <para>
/// <b>والصفّ يبقى بعد الاستهلاك ولا يُحذف</b>: صفٌّ محذوف لا يُفرَّق عن دعوةٍ لم توجد
/// قط، فيُقرأ الاستعمال الثاني «اعتماد مختلَق» بدل «دُعيت مرّة واستُعملت دعوتك» — وهما
/// جوابان يُبنى عليهما فعلان مختلفان تماماً عند من يقرؤهما.
/// </para>
/// </summary>
internal sealed class AccessEnrolmentRow
{
    /// <summary>بصمة الاعتماد — <b>وهي المفتاح</b>. ولا يوجد عمودٌ يحمل النصّ.</summary>
    public string Digest { get; set; } = string.Empty;

    /// <summary>المستأجر.</summary>
    public Guid TenantId { get; set; }

    /// <summary>المستخدم المدعوّ.</summary>
    public Guid UserId { get; set; }

    /// <summary>لحظة الإصدار.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>لحظة انقضاء الدعوة.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>لحظة الاستهلاك، أو <c>null</c> إن لم تُستعمل بعد.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>
/// عائلة جلسة: هوية تبقى ثابتة عبر كل دورات التجديد، وعليها <b>مفتاح الإبطال</b>.
/// <para>
/// ولذلك الإبطال هنا لا على الاعتماد المفرد: إبطالُ اعتمادٍ واحد يترك اعتماد تجديده
/// حيّاً فيُصدر بديلاً بعد ثوانٍ — أي أن «أبطلتُ الجلسة» تكون قد كذبت.
/// </para>
/// </summary>
internal sealed class AccessSessionRow
{
    /// <summary>معرّف العائلة.</summary>
    public Guid SessionId { get; set; }

    /// <summary>المستأجر.</summary>
    public Guid TenantId { get; set; }

    /// <summary>المستخدم.</summary>
    public Guid UserId { get; set; }

    /// <summary>لحظة الفتح.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>رقم الدورة الجارية. يبدأ من 1 ويزيد بواحد عند كل تدوير.</summary>
    public int Generation { get; set; }

    /// <summary>لحظة الإبطال، أو <c>null</c>.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>سبب الإبطال من المجموعة المغلقة، أو نصّ فارغ.</summary>
    public string RevokedReason { get; set; } = string.Empty;
}

/// <summary>
/// اعتمادٌ مُصدَر داخل عائلة: بصمته ونوعه ودورته وانقضاؤه.
/// <para>
/// <b>ولا عمود يحمل نصّ الاعتماد</b>، لا هنا ولا في أي جدول. من يقرأ نسخة احتياطية أو
/// سجلّ استعلامات لا يجد قيمةً تصلح للانتحال.
/// </para>
/// <para>
/// <b>والصفّ المستهلَك يبقى</b>: هو الشاهد الوحيد على إعادة الاستعمال. حذفُه يجعل اعتماد
/// تجديد مسروقاً يُقرأ «مختلَق» فيُرفض الطلب وحده وتبقى الجلسة حيّة في يد سارقها.
/// </para>
/// </summary>
internal sealed class AccessCredentialRow
{
    /// <summary>البصمة — وهي المفتاح.</summary>
    public string Digest { get; set; } = string.Empty;

    /// <summary>العائلة.</summary>
    public Guid SessionId { get; set; }

    /// <summary>النوع: <c>access</c> أو <c>refresh</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>دورة الإصدار.</summary>
    public int Generation { get; set; }

    /// <summary>لحظة الإصدار.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>لحظة الانقضاء.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>لحظة الاستهلاك — لاعتماد التجديد وحده، و<c>null</c> لما عداه.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
