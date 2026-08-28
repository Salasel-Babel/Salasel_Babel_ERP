namespace Babel.Api.Wire;

/// <summary>
/// طلب فتح جلسة: اعتماد الانتساب وحده.
/// <para>
/// <b>ولا حقل مستأجر فيه ولا حقل مستخدم:</b> الهوية تُشتقّ من الاعتماد كما تُشتقّ في كل
/// مسار آخر على هذا السطح. وحقلٌ يقول «أنا فلان» في جسم طلبِ دخول ليس مصادقةً بل ادّعاء.
/// </para>
/// </summary>
/// <param name="EnrolmentCredential">النصّ المُسلَّم مرّة واحدة عند الدعوة.</param>
internal sealed record OpenSessionRequestDto(string EnrolmentCredential);

/// <summary>طلب تجديد جلسة: اعتماد التجديد وحده.</summary>
/// <param name="RefreshCredential">اعتماد التجديد الجاري. يُستهلك بهذا النداء ولا يُقبل ثانيةً.</param>
internal sealed record RenewSessionRequestDto(string RefreshCredential);

/// <summary>عضوية صاحب الجلسة في منشأة واحدة.</summary>
/// <param name="CompanyId">المنشأة كما تُكتب في المسار.</param>
/// <param name="Role">الدور: <c>Reader</c> أو <c>Contributor</c> أو <c>Owner</c>.</param>
internal sealed record AccessMembershipDto(string CompanyId, string Role);

/// <summary>
/// جلسة مفتوحة كما تُسلَّم لصاحبها — <b>ومرّة واحدة</b>.
/// <para>
/// <c>accessCredential</c> و<c>refreshCredential</c> يخرجان من الخادم في هذه الاستجابة
/// وحدها ولا يُخزَّنان في أي جدول: المُودَع بصمتهما. فمن فقد الاستجابة فقد الاعتماد،
/// ولا يوجد في الخادم من يستطيع أن يعيده إليه — وهذا هو المقصود.
/// </para>
/// </summary>
/// <param name="SessionId">معرّف العائلة. يبقى ثابتاً عبر كل تجديد، وهو ما يُبطَل.</param>
/// <param name="TenantId">المستأجر خلف الجلسة.</param>
/// <param name="UserId">المستخدم خلف الجلسة.</param>
/// <param name="Generation">رقم الدورة. يبدأ من 1 ويزيد بواحد عند كل تجديد.</param>
/// <param name="AccessCredential">الاعتماد الفاعل — يُقدَّم في ترويسة <c>Authorization: Bearer</c>.</param>
/// <param name="AccessExpiresAt">لحظة انقضائه بصيغة ISO 8601 بتوقيت UTC.</param>
/// <param name="RefreshCredential">اعتماد التجديد — يُقدَّم مرّة واحدة، ثم يصير تقديمه سرقة.</param>
/// <param name="RefreshExpiresAt">لحظة انقضائه.</param>
/// <param name="WriteReachesNothing">
/// ‏<c>true</c> حين تكون كل عضويات صاحب الجلسة <c>Reader</c> — أي أن هذه الجلسة لا تكتب
/// في أي منشأة. تقرؤها الواجهة فتبني شاشة قراءة بدل أن تعرض أزراراً يرفضها الخادم.
/// </param>
/// <param name="Memberships">عضويات صاحب الجلسة، مرتَّبة بمعرّف المنشأة ترتيباً حرفياً ثابتاً.</param>
internal sealed record AccessSessionDto(
    string SessionId,
    string TenantId,
    string UserId,
    int Generation,
    string AccessCredential,
    string AccessExpiresAt,
    string RefreshCredential,
    string RefreshExpiresAt,
    bool WriteReachesNothing,
    IReadOnlyList<AccessMembershipDto> Memberships);

/// <summary>إبطال جلسة: ما أُبطل، ومتى، ولماذا برمزٍ من مجموعة مغلقة.</summary>
/// <param name="SessionId">الجلسة المُبطَلة.</param>
/// <param name="RevokedAt">لحظة الإبطال.</param>
/// <param name="Reason">
/// ‏<c>signed_out</c> حين يطلبه صاحبها، و<c>refresh_replayed</c> حين يُسقطها كشفُ إعادة
/// استعمال اعتماد تجديد. رمزٌ يقرؤه العميل، لا نصّاً يفسّره.
/// </param>
internal sealed record SessionRevocationDto(string SessionId, string RevokedAt, string Reason);

/// <summary>طلب دعوة عضو إلى منشأة.</summary>
/// <param name="DisplayNameAr">اسم المدعوّ بالعربية — السجلّ (ADR-0021).</param>
/// <param name="Role">الدور المطلوب من المجموعة المغلقة.</param>
internal sealed record GrantMembershipRequestDto(string DisplayNameAr, string Role);

/// <summary>عضو في منشأة كما يُعرض في قائمة الأعضاء. <b>ولا اعتماد فيه.</b></summary>
/// <param name="UserId">معرّف المستخدم كما سكّه الخادم.</param>
/// <param name="DisplayNameAr">الاسم العربي.</param>
/// <param name="Role">الدور.</param>
/// <param name="GrantedAt">لحظة منح العضوية.</param>
internal sealed record MembershipDto(string UserId, string DisplayNameAr, string Role, string GrantedAt);

/// <summary>أعضاء منشأة.</summary>
/// <param name="CompanyId">المنشأة.</param>
/// <param name="MemberCount">عددهم.</param>
/// <param name="Members">القائمة مرتَّبة بمعرّف المستخدم ترتيباً حرفياً ثابتاً.</param>
internal sealed record MembershipListDto(string CompanyId, int MemberCount, IReadOnlyList<MembershipDto> Members);

/// <summary>
/// عضوية مُنحت للتوّ، ومعها <b>اعتماد انتسابها</b>.
/// <para>
/// وهذه هي الاستجابة الوحيدة التي يخرج فيها اعتماد انتساب، ويخرج فيها <b>مرّة واحدة</b>:
/// المُودَع بصمته. فمن دعا عضواً يسلّمه هذا النصّ بنفسه، ولا يوجد في الخادم من يعيده.
/// </para>
/// </summary>
/// <param name="CompanyId">المنشأة.</param>
/// <param name="Member">العضوية الممنوحة.</param>
/// <param name="EnrolmentCredential">اعتماد الانتساب — يُقبل مرّة واحدة ثم يُستهلك.</param>
/// <param name="EnrolmentExpiresAt">لحظة انقضاء الدعوة.</param>
internal sealed record GrantedMembershipDto(
    string CompanyId,
    MembershipDto Member,
    string EnrolmentCredential,
    string EnrolmentExpiresAt);
