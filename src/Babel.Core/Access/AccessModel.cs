using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.Access;

/// <summary>
/// حدود سطح المصادقة، معلنة مرّة واحدة ويقرؤها العقد المنشور والنواة والمخزن معاً.
/// </summary>
public static class AccessLimits
{
    /// <summary>طول الاعتماد المُصدَر بالبايتات قبل الترميز. 32 بايتاً = 256 بت عشوائية.</summary>
    public const int CredentialBytes = 32;

    /// <summary>طول البصمة السداسية عشرية الصغيرة — ‏SHA-256 مُرمَّزاً.</summary>
    public const int DigestLength = 64;

    /// <summary>أقصى طول لاعتماد يُقدَّم — حدٌّ يمنع تجزئة حمولة كبيرة قبل أي بحث.</summary>
    public const int MaximumPresentedLength = 512;

    /// <summary>أقصى طول لاسم عربي على عضوية.</summary>
    public const int MaximumNameLength = 200;

    /// <summary>عمر الاعتماد الفاعل. قصيرٌ عمداً: هو ما يُحمل في كل طلب فهو الأكثر تعرّضاً.</summary>
    public static TimeSpan AccessLifetime { get; } = TimeSpan.FromMinutes(15);

    /// <summary>عمر اعتماد التجديد. طويلٌ لأنه لا يُحمل إلا مرّة كل دورة، ويدور في كل مرّة.</summary>
    public static TimeSpan RefreshLifetime { get; } = TimeSpan.FromDays(14);

    /// <summary>عمر اعتماد الانتساب — مهلة قبول دعوة، لا اعتماد استعمال.</summary>
    public static TimeSpan EnrolmentLifetime { get; } = TimeSpan.FromDays(7);
}

/// <summary>
/// دور العضوية: ما يستطيعه <b>هذا الإنسان</b> في <b>هذه المنشأة</b>.
/// <para>
/// <b>وهو سؤال آخر غير سؤال الاستحقاق، ولذلك جدولٌ آخر ولا نسخة من ذاك.</b> الاستحقاق
/// يسأل «هل دُفع ثمن هذه الوحدة؟» ويجيب عن المستأجر كلّه؛ والدور يسأل «من هذا المستخدم في
/// هذه المنشأة؟». وخلطهما يجعل تجديد اشتراكٍ يمنح كاتباً صلاحية مالك، أو يجعل قارئاً
/// يُقرأ «اشتراكك منقطع» فيتّصل بالمحاسبة بلا سبب (ADR-0034 · ADR-0036).
/// </para>
/// </summary>
public enum MembershipRole
{
    /// <summary>قراءة وتقارير فقط. لا مستند جديد ولا ترحيل ولا دعوة عضو.</summary>
    Reader = 1,

    /// <summary>يقرأ ويكتب مستندات المنشأة.</summary>
    Contributor = 2,

    /// <summary>يقرأ ويكتب ويدعو أعضاء آخرين.</summary>
    Owner = 3,
}

/// <summary>
/// عضوية مستخدم في منشأة: من هو، وبأي دور، ومتى مُنح.
/// </summary>
/// <param name="Company">المنشأة.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Role">الدور.</param>
/// <param name="DisplayNameAr">الاسم العربي المعروض — السجلّ (ADR-0021).</param>
/// <param name="GrantedAt">لحظة المنح.</param>
public sealed record Membership(
    Guid Company,
    UserId User,
    MembershipRole Role,
    string DisplayNameAr,
    DateTimeOffset GrantedAt);

/// <summary>
/// اعتمادٌ مُصدَر: نصّه يُسلَّم <b>مرّة واحدة</b> ولا يُخزَّن، وبصمته وحدها هي ما يُودَع.
/// <para>
/// <b>ولذلك لا يوجد في هذا النوع طريق للعودة من البصمة إلى النصّ</b>: من يقرأ قاعدة
/// البيانات — نسخةً احتياطية أو سجلّ استعلامات أو لقطةَ دعم — لا ينتحل بها أحداً.
/// </para>
/// </summary>
/// <param name="Value">النصّ المُسلَّم للعميل. يعيش في الذاكرة وفي الاستجابة، ولا شيء غير ذلك.</param>
/// <param name="ExpiresAt">لحظة الانقضاء المعلنة.</param>
public sealed record IssuedCredential(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// جلسة مفتوحة كما تُسلَّم لصاحبها: هويتها، واعتماداها، وما تبلغه.
/// </summary>
/// <param name="SessionId">معرّف العائلة — يبقى ثابتاً عبر كل دورات التجديد.</param>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Generation">رقم الدورة. يبدأ من 1 ويزيد بواحد عند كل تجديد.</param>
/// <param name="Access">الاعتماد الفاعل.</param>
/// <param name="Refresh">اعتماد التجديد — ويُستهلك عند أول استعمال.</param>
/// <param name="Memberships">عضويات صاحب الجلسة، مرتَّبة بمعرّف المنشأة ترتيباً حرفياً.</param>
public sealed record OpenedSession(
    Guid SessionId,
    TenantId Tenant,
    UserId User,
    int Generation,
    IssuedCredential Access,
    IssuedCredential Refresh,
    IReadOnlyList<Membership> Memberships)
{
    /// <summary>
    /// جلسةٌ لا تبلغ الكتابة في أي منشأة — أي أن كل عضويات صاحبها <see cref="MembershipRole.Reader"/>.
    /// </summary>
    public bool WriteReachesNothing =>
        Memberships.Count > 0 && Memberships.All(static membership => membership.Role == MembershipRole.Reader);
}

/// <summary>
/// هوية محلولة من اعتماد فاعل مُقدَّم — <b>ما يحتاجه حدّ HTTP ولا شيء غيره</b>.
/// </summary>
/// <param name="SessionId">العائلة التي ينتمي إليها الاعتماد.</param>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Companies">المنشآت التي يبلغها.</param>
/// <param name="ReadOnlyCompanies">المنشآت التي دور صاحب الاعتماد فيها قراءةٌ فقط.</param>
/// <param name="ExpiresAt">لحظة انقضاء الاعتماد الفاعل.</param>
public sealed record ResolvedAccess(
    Guid SessionId,
    TenantId Tenant,
    UserId User,
    IReadOnlySet<Guid> Companies,
    IReadOnlySet<Guid> ReadOnlyCompanies,
    DateTimeOffset ExpiresAt);

/// <summary>سبب إبطال الجلسة — <b>مجموعة مغلقة</b> يقرؤها العميل رمزاً لا نصّاً.</summary>
public static class RevocationReasons
{
    /// <summary>طلبه صاحب الجلسة صراحةً.</summary>
    public const string SignedOut = "signed_out";

    /// <summary>‏<b>اعتماد تجديد قُدِّم مرّتين</b> — والعائلة كلّها تسقط، لا الطلب الثاني وحده.</summary>
    public const string RefreshReplayed = "refresh_replayed";

    /// <summary>كل الأسباب، مرتَّبة — يقرؤها مولّد العقد فلا تُكتب قائمة ثانية بيد.</summary>
    public static IReadOnlyList<string> All { get; } = [RefreshReplayed, SignedOut];
}

/// <summary>نتيجة إبطال جلسة.</summary>
/// <param name="SessionId">الجلسة المُبطَلة.</param>
/// <param name="RevokedAt">لحظة الإبطال.</param>
/// <param name="Reason">السبب من <see cref="RevocationReasons"/>.</param>
public sealed record SessionRevocation(Guid SessionId, DateTimeOffset RevokedAt, string Reason);

/// <summary>دعوة عضو: عضويته، والاعتماد الذي يفتح به جلسته الأولى.</summary>
/// <param name="Membership">العضوية الممنوحة.</param>
/// <param name="Enrolment">اعتماد الانتساب — يُسلَّم مرّة واحدة ويُستهلك عند أول استعمال.</param>
public sealed record GrantedMembership(Membership Membership, IssuedCredential Enrolment);

/// <summary>أسماء الأدوار كما تُكتب على السلك وفي المخطّط — مشتقّة من التعداد لا مكتوبة ثانيةً.</summary>
public static class MembershipRoles
{
    /// <summary>أعضاء التعداد بأسمائها، مرتَّبة بترتيب القدرة.</summary>
    public static IReadOnlyList<string> All { get; } =
        [.. Enum.GetValues<MembershipRole>().OrderBy(static role => (int)role).Select(static role => role.ToString())];

    /// <summary>الاسم كما يُخزَّن في العمود: حروف لاتينية صغيرة.</summary>
    /// <param name="role">الدور.</param>
    public static string ToColumn(MembershipRole role) =>
        role.ToString().ToLowerInvariant();

    /// <summary>يقرأ الدور من قيمة العمود. قيمةٌ غير معروفة عطلٌ في المخطّط لا حالة عمل.</summary>
    /// <param name="column">قيمة العمود.</param>
    public static MembershipRole FromColumn(string column) =>
        Enum.TryParse(column, ignoreCase: true, out MembershipRole role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"دورُ عضويةٍ غير معروف في المخطّط: «{column}». / Unknown membership role in the schema: '{column}'."));
}
