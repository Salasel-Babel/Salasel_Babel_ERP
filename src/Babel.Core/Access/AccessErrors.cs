using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.Access;

/// <summary>
/// أخطاء سطح المصادقة والانتساب، برموز ثابتة هي ما تعتمد عليه الشيفرة.
/// <para>
/// <b>وثلاثة رموز تفترق عمداً حيث كان يُغري رمزٌ واحد:</b> اعتمادٌ مختلَق، واعتمادٌ
/// انقضى، واعتمادٌ أُبطل. فمن انقضت جلسته يدخل من جديد؛ ومن أُبطلت جلسته يعرف أن شيئاً
/// وقع — ربما لم يقع منه — فيغيّر سلوكه؛ ومن اختلق اعتماداً <b>لا يتعلّم منه شيئاً</b>.
/// ورمزٌ واحد لثلاثتها كان سيجعل المستخدم الشرعي يفتح تذكرة دعم عن جلسة انتهت.
/// </para>
/// </summary>
public static class AccessErrors
{
    /// <summary>اعتماد مُقدَّم لا يقابله شيء. لا يفرَّق عن أي اعتماد آخر غير مقبول.</summary>
    public static Error CredentialRejected { get; } = new(
        "access.credential_rejected",
        "الاعتماد غير مقبول.",
        "The credential was rejected.");

    /// <summary>اعتماد انتساب انقضت مهلته.</summary>
    public static Error EnrolmentExpired { get; } = new(
        "access.enrolment_expired",
        "انقضت مهلة هذه الدعوة. اطلب من صاحب المنشأة دعوةً جديدة — ولم يُنتزَع منك شيء.",
        "This invitation has expired. Ask the company's owner for a new one — nothing was taken from you.");

    /// <summary>اعتماد انتساب استُعمل من قبل.</summary>
    public static Error EnrolmentConsumed { get; } = new(
        "access.enrolment_consumed",
        "استُعملت هذه الدعوة من قبل. الدعوة تُقبل مرّة واحدة، فإن لم تكن أنت من استعملها فأخبر صاحب المنشأة الآن.",
        "This invitation was already used. An invitation is accepted exactly once; if it was not you who used it, tell the company's owner now.");

    /// <summary>اعتماد تجديد انقضى.</summary>
    public static Error RefreshExpired { get; } = new(
        "access.refresh_expired",
        "انقضى اعتماد التجديد. ادخل من جديد — ولم يُبطَل شيء ولم يتغيّر شيء في البيانات.",
        "The refresh credential has expired. Sign in again — nothing was revoked and nothing in the data changed.");

    /// <summary>
    /// <b>اعتماد تجديد قُدِّم مرّتين.</b> والجواب إسقاط العائلة كلّها، لا خدمة الطلب الثاني.
    /// </summary>
    public static Error RefreshReplayed { get; } = new(
        "access.refresh_replayed",
        "قُدِّم اعتماد التجديد هذا مرّتين. واعتمادٌ يدور ثم يعود اعتمادٌ في يد اثنين — أحدهما ليس صاحبه. "
        + "فأُبطلت الجلسة كلّها الآن، ولا يُخدَم الطلب الثاني. ادخل من جديد، وبياناتك كما هي.",
        "This refresh credential was presented twice. A rotating credential that comes back is a credential in two "
        + "hands — one of them is not its owner's. The whole session has been revoked now and the second request is "
        + "not served. Sign in again; your data is untouched.");

    /// <summary>جلسة أُبطلت.</summary>
    public static Error SessionRevoked { get; } = new(
        "access.session_revoked",
        "أُبطلت هذه الجلسة. ادخل من جديد — والإبطال يقع فوراً ولا يُنتظر به انقضاء.",
        "This session has been revoked. Sign in again — revocation takes effect immediately and never waits for an expiry.");

    /// <summary>الاعتماد المُقدَّم أطول من أي اعتماد يُصدره هذا السطح.</summary>
    public static Error CredentialTooLong { get; } = new(
        "access.credential_too_long",
        string.Create(
            CultureInfo.InvariantCulture,
            $"الاعتماد المُقدَّم يتجاوز {AccessLimits.MaximumPresentedLength} محرفاً، وهو أطول من أي اعتماد يُصدره هذا السطح. ويُرفض قبل أي تجزئة وقبل أي بحث."),
        string.Create(
            CultureInfo.InvariantCulture,
            $"The presented credential exceeds {AccessLimits.MaximumPresentedLength} characters — longer than anything this surface issues. It is refused before any hashing and before any lookup."));

    /// <summary>اسم العضو العربي مفقود.</summary>
    public static Error MemberNameMissing { get; } = new(
        "membership.name_missing",
        "اسم العضو بالعربية إلزامي: هو ما يظهر في سجلّ التدقيق وفي قائمة الأعضاء، والعربية هي السجلّ.",
        "The member's Arabic name is mandatory: it is what appears in the audit log and in the member list, and Arabic is the record.");

    /// <summary>اسم العضو أطول من الحدّ.</summary>
    public static Error MemberNameTooLong { get; } = new(
        "membership.name_too_long",
        string.Create(CultureInfo.InvariantCulture, $"اسم العضو يتجاوز {AccessLimits.MaximumNameLength} محرفاً."),
        string.Create(CultureInfo.InvariantCulture, $"The member's name exceeds {AccessLimits.MaximumNameLength} characters."));

    /// <summary>دور غير معروف وصل على السلك.</summary>
    /// <param name="role">النصّ الواصل.</param>
    public static Error RoleUnknown(string role) => new(
        "membership.role_unknown",
        $"الدور «{role}» ليس من الأدوار المعروفة. المعروف: {string.Join(" · ", MembershipRoles.All)}.",
        $"The role '{role}' is not one of the known roles. Known: {string.Join(", ", MembershipRoles.All)}.");

    /// <summary>عضويةٌ قائمة لهذا المستخدم في هذه المنشأة.</summary>
    public static Error MembershipAlreadyGranted { get; } = new(
        "membership.already_granted",
        "لهذا المستخدم عضويةٌ في هذه المنشأة فعلاً. وتغيير الدور فعلٌ آخر يُطلب باسمه، لا دعوةٌ ثانية "
        + "تُنتج اعتماد انتساب جديداً لمن يملك جلسة.",
        "This user already has a membership in this company. Changing a role is a different act asked for by its own "
        + "name, not a second invitation minting an enrolment credential for someone who already holds a session.");

    /// <summary>الداعي ليس مالكاً.</summary>
    public static Error InviterIsNotAnOwner { get; } = new(
        "membership.inviter_is_not_an_owner",
        "دعوةُ عضوٍ إلى منشأة يفعلها مالكٌ فيها. ومن يستطيع أن يدعو يستطيع أن يمنح نفسه ما شاء عبر عضوٍ يدعوه، "
        + "فالحدّ عند الدعوة لا عند ما بعدها.",
        "Inviting a member into a company is an owner's act. Whoever can invite can grant themselves anything through "
        + "the member they invite, so the limit sits at the invitation, not after it.");
}
