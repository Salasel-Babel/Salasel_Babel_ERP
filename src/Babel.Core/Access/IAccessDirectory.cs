using Babel.SharedKernel;

namespace Babel.Core.Access;

/// <summary>حكم استهلاك اعتماد انتساب.</summary>
public enum EnrolmentOutcome
{
    /// <summary>لا يقابله شيء.</summary>
    Rejected = 0,

    /// <summary>انقضت مهلته.</summary>
    Expired = 1,

    /// <summary>استُعمل من قبل.</summary>
    AlreadyConsumed = 2,

    /// <summary>قُبل واستُهلك الآن.</summary>
    Accepted = 3,
}

/// <summary>نتيجة استهلاك اعتماد انتساب.</summary>
/// <param name="Outcome">الحكم.</param>
/// <param name="Tenant">المستأجر — ذو معنى عند <see cref="EnrolmentOutcome.Accepted"/> وحدها.</param>
/// <param name="User">المستخدم — كذلك.</param>
public sealed record EnrolmentClaim(EnrolmentOutcome Outcome, TenantId Tenant, UserId User);

/// <summary>حكم تدوير اعتماد تجديد.</summary>
public enum RotationOutcome
{
    /// <summary>لا يقابله شيء.</summary>
    Rejected = 0,

    /// <summary>انقضى.</summary>
    Expired = 1,

    /// <summary><b>قُدِّم مرّتين</b> — وقد أُبطلت العائلة كلّها بسبب هذا الحكم نفسه.</summary>
    Replayed = 2,

    /// <summary>عائلته مُبطَلة.</summary>
    SessionRevoked = 3,

    /// <summary>دار: استُهلك القديم وأُصدر الجديد في المعاملة نفسها.</summary>
    Rotated = 4,
}

/// <summary>نتيجة تدوير اعتماد تجديد.</summary>
/// <param name="Outcome">الحكم.</param>
/// <param name="SessionId">العائلة.</param>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Generation">رقم الدورة الجديدة عند <see cref="RotationOutcome.Rotated"/>.</param>
public sealed record RotationResult(
    RotationOutcome Outcome,
    Guid SessionId,
    TenantId Tenant,
    UserId User,
    int Generation);

/// <summary>حكم حلّ اعتماد فاعل.</summary>
public enum AccessOutcome
{
    /// <summary>لا يقابله شيء.</summary>
    Rejected = 0,

    /// <summary>انقضى.</summary>
    Expired = 1,

    /// <summary>عائلته مُبطَلة — <b>والإبطال يُقرأ هنا فوراً، لا عند الانقضاء</b>.</summary>
    Revoked = 2,

    /// <summary>حيّ.</summary>
    Live = 3,
}

/// <summary>نتيجة حلّ اعتماد فاعل.</summary>
/// <param name="Outcome">الحكم.</param>
/// <param name="SessionId">العائلة.</param>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="ExpiresAt">لحظة انقضاء الاعتماد.</param>
public sealed record AccessLookup(
    AccessOutcome Outcome,
    Guid SessionId,
    TenantId Tenant,
    UserId User,
    DateTimeOffset ExpiresAt);

/// <summary>
/// دليل المصادقة والعضويات — <b>ولا يصله نصّ اعتماد واحد أبداً</b>.
/// <para>
/// كل دالّة هنا تأخذ <b>بصمة</b>. والتجزئة تقع في <see cref="AccessService"/> قبل النداء،
/// فلا يوجد في هذا الحدّ ولا فيما تحته موضعٌ يستطيع أن يكتب اعتماداً قابلاً للاستعمال —
/// لا في جدول، ولا في سجلّ استعلامات، ولا في نسخة احتياطية، ولا في أثر تشخيص.
/// </para>
/// <para>
/// <b>وثلاث من هذه الدوالّ ذرّية بالضرورة لا بالاتفاق:</b> استهلاك الانتساب، وتدوير
/// التجديد، ومنح العضوية. فطلبان متزامنان باعتماد تجديد واحد يجب أن يفوز أحدهما ويُقرأ
/// الآخر <b>إعادةَ استعمال</b> — وهي بالضبط الحالة التي يقع فيها اعتمادٌ مسروق. وتنفيذٌ
/// يقرأ ثم يكتب في نداءين يُنتج «يفوز الاثنان» بلا أن يشتكي شيء.
/// </para>
/// </summary>
public interface IAccessDirectory
{
    /// <summary>يمنح عضوية ويودع بصمة اعتماد انتسابها في فعل واحد. <c>false</c> إن كانت العضوية قائمة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="membership">العضوية.</param>
    /// <param name="grantedBy">من منحها.</param>
    /// <param name="enrolmentDigest">بصمة اعتماد الانتساب.</param>
    /// <param name="enrolmentExpiresAt">لحظة انقضاء الدعوة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<bool> TryGrantAsync(
        TenantId tenant,
        Membership membership,
        UserId grantedBy,
        string enrolmentDigest,
        DateTimeOffset enrolmentExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>عضوية واحدة، أو <c>null</c>.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<Membership?> FindMembershipAsync(Guid company, UserId user, CancellationToken cancellationToken = default);

    /// <summary>أعضاء منشأة واحدة، مرتَّبين بمعرّف المستخدم ترتيباً حرفياً ثابتاً.</summary>
    /// <param name="company">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<IReadOnlyList<Membership>> ListMembershipsAsync(Guid company, CancellationToken cancellationToken = default);

    /// <summary>عضويات مستخدم داخل مستأجره — <b>وهي مصدر ما تبلغه جلسته</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<IReadOnlyList<Membership>> MembershipsOfAsync(TenantId tenant, UserId user, CancellationToken cancellationToken = default);

    /// <summary>يستهلك اعتماد انتساب ذرّياً: يقبله مرّة واحدة ولا مرّتين.</summary>
    /// <param name="enrolmentDigest">البصمة المُقدَّمة.</param>
    /// <param name="now">اللحظة الجارية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<EnrolmentClaim> ConsumeEnrolmentAsync(string enrolmentDigest, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>يفتح عائلة جلسة جديدة بدورتها الأولى.</summary>
    /// <param name="sessionId">معرّف العائلة.</param>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="user">المستخدم.</param>
    /// <param name="accessDigest">بصمة الاعتماد الفاعل.</param>
    /// <param name="accessExpiresAt">انقضاؤه.</param>
    /// <param name="refreshDigest">بصمة اعتماد التجديد.</param>
    /// <param name="refreshExpiresAt">انقضاؤه.</param>
    /// <param name="now">اللحظة الجارية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task OpenSessionAsync(
        Guid sessionId,
        TenantId tenant,
        UserId user,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string refreshDigest,
        DateTimeOffset refreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// يدوّر اعتماد تجديد ذرّياً. وإن كان مستهلَكاً سلفاً <b>أبطل العائلة كلّها</b> وأعاد
    /// <see cref="RotationOutcome.Replayed"/>.
    /// </summary>
    /// <param name="refreshDigest">بصمة اعتماد التجديد المُقدَّم.</param>
    /// <param name="accessDigest">بصمة الاعتماد الفاعل الجديد.</param>
    /// <param name="accessExpiresAt">انقضاؤه.</param>
    /// <param name="nextRefreshDigest">بصمة اعتماد التجديد الجديد.</param>
    /// <param name="nextRefreshExpiresAt">انقضاؤه.</param>
    /// <param name="now">اللحظة الجارية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<RotationResult> RotateAsync(
        string refreshDigest,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string nextRefreshDigest,
        DateTimeOffset nextRefreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>يحلّ اعتماداً فاعلاً — ويقرأ الإبطال في الاستعلام نفسه.</summary>
    /// <param name="accessDigest">البصمة المُقدَّمة.</param>
    /// <param name="now">اللحظة الجارية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<AccessLookup> LookupAccessAsync(string accessDigest, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>يُبطل عائلة جلسة. الإبطال أول مرّة يفوز، والثاني يقرأ الأول.</summary>
    /// <param name="sessionId">العائلة.</param>
    /// <param name="reason">السبب من <see cref="RevocationReasons"/>.</param>
    /// <param name="now">اللحظة الجارية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<SessionRevocation?> RevokeSessionAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
