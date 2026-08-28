using System.Globalization;
using Babel.Api.Ports;
using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Subscriptions;
using Babel.ControlPlane.Support;

namespace Babel.Api.Fleet;

/// <summary>
/// <b>محوّل الأسطول</b> — الموضع الوحيد في المستودع الذي يعرف الطرفين.
/// <para>
/// وهو في الجذر التركيبي لأن ذلك هو الشكل الصحيح الوحيد: <c>Babel.ControlPlane</c>
/// مُعلَن في <c>ModuleMap</c> بمجموعة مراجع <b>فارغة</b>، فلا يعرف <c>TenantId</c> ولا
/// <c>UserId</c> ولا <c>Result</c> ولا وحدة منتَج واحدة — ولا يستطيع أن يعرفها بلا
/// كسر القاعدة 3 بإحدى طرق ADR-0036 §2 الثلاث المرفوضة. ولو بدا أن الحلّ يستلزم أن
/// يعرف مستوى التحكّم نوعاً من مستوى المستأجر، فذلك <b>دليل على أن التصميم خطأ لا على
/// أن الحاجز خطأ</b>.
/// </para>
/// <para>
/// <b>وما يعبر هذا المحوّل نصوصٌ ومعرّفات</b>: أسماء حالات، ورموز وحدات، وتواريخ
/// <c>yyyy-MM-dd</c>، ومبالغ نصّاً. ولا تعداد من مستوى التحكّم يبلغ نقطة نهاية، ولا
/// نوع من مستوى المستأجر يبلغ مستوى التحكّم.
/// </para>
/// <para>
/// <b>والدور الذي يقرأ به دورٌ ثالث</b> — <c>UseSurfaceRole</c> — لا مستخدم الإدارة:
/// خادمٌ يخدم الإنترنت باتصال إدارة يستطيع أن يُسقط سجلّ الأسطول الذي يقرؤه، وهو نفس
/// مبدأ ADR-0003 منقولاً إلى مستوى التحكّم.
/// </para>
/// </summary>
internal sealed class ControlPlaneFleetDirectory : IFleetDirectory
{
    private readonly SubscriptionService _subscriptions;

    /// <summary>ينشئ المحوّل فوق إعدادات مستوى التحكّم المقروءة من البيئة.</summary>
    /// <param name="options">إعدادات مستوى التحكّم — كل قيمة فيها من متغيّر بيئة.</param>
    public ControlPlaneFleetDirectory(ControlPlaneOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        TenantRegistry registry = new(options);
        _subscriptions = new SubscriptionService(options, registry, new EntitlementService(options, registry));
    }

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownPlans { get; } =
        [.. SubscriptionService.Plans().Select(static plan => plan.Code)];

    /// <inheritdoc />
    public async Task<FleetSubscription?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        SubscriptionRecord? record = await _subscriptions.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return record is null ? null : Project(record);
    }

    /// <inheritdoc />
    public async Task<FleetSubscription> OpenAsync(
        Guid tenantId, string tenantCode, string nameAr, string nameEn, CancellationToken cancellationToken = default) =>
        Project(await _subscriptions
            .OpenAsync(tenantId, tenantCode, BilingualName.Of(nameAr, nameEn), SignupActor, cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<FleetSubscription> ChangePlanAsync(
        Guid tenantId, string planCode, string actor, string authority, string reasonAr,
        CancellationToken cancellationToken = default) =>
        Project(await _subscriptions
            .ChangePlanAsync(tenantId, planCode, new ChangeAuthority(actor, authority, reasonAr), cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<FleetSubscription> LapseAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default) =>
        Project(await _subscriptions
            .LapseAsync(tenantId, new ChangeAuthority(actor, authority, reasonAr), cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<FleetSubscription> ResumeAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default) =>
        Project(await _subscriptions
            .ResumeAsync(tenantId, new ChangeAuthority(actor, authority, reasonAr), cancellationToken)
            .ConfigureAwait(false));

    /// <summary>
    /// الفاعل المكتوب على سطر تدقيق التسجيل الأول.
    /// <para>
    /// وهو <b>ليس اسم إنسان ولا معرّف مستأجر</b>: من يفتح الباب مجهولٌ بحكم كون الباب
    /// مفتوحاً، وكتابةُ اسمٍ يدّعيه في جسم الطلب على سطر تدقيق تجعل السطر يكذب. فالسند
    /// يحمل رمز المستأجر المشتقّ، والفاعل يقول ما وقع فعلاً: تسجيلٌ من الباب المفتوح.
    /// </para>
    /// </summary>
    private const string SignupActor = "signup";

    private static FleetSubscription Project(SubscriptionRecord record) => new(
        record.TenantId,
        record.TenantCode,
        record.NameAr,
        record.NameEn,
        record.TenantStatus,
        record.SubscriptionId.ToString("D", CultureInfo.InvariantCulture),
        record.PlanCode,
        record.PlanNameAr,
        record.PlanNameEn,
        record.MonthlyPrice,
        record.PerUserPrice,
        record.IncludedUsers,
        record.Currency,
        Date(record.StartedOn),
        record.EndsOn is { } ends ? Date(ends) : null,
        record.State,
        record.RenewsOn is { } renews ? Date(renews) : null,
        [.. record.Modules.Select(static module =>
            new FleetModule(module.Code, module.NameAr, module.NameEn, module.State, module.PostsJournal))]);

    /// <summary>
    /// تاريخٌ على السلك: <c>yyyy-MM-dd</c> بثقافة ثابتة.
    /// <para>وهي الصيغة نفسها التي يقرأ بها السطح تواريخ الاستعلامات — صيغةٌ واحدة
    /// لتاريخ واحد، فلا يوجد تاريخ يُكتب بشكل ويُقرأ بآخر (فخ-38).</para>
    /// </summary>
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// أسطولٌ غير مُهيَّأ — <b>يقول ذلك بصوته</b>.
/// <para>
/// وهو الشكل نفسه الذي يأخذه <c>UnavailableJournalEntryReader</c>: العقد منشور،
/// والباب مسجَّل، والجواب رمزٌ ثابت لا انهيار. وخادمٌ بلا مستوى تحكّم حالةٌ مشروعة —
/// نشرٌ للتطوير، أو بيئة عرض — و<b>سائر السطح يعمل فيها كما هو</b>.
/// </para>
/// </summary>
internal sealed class UnavailableFleetDirectory : IFleetDirectory
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownPlans { get; } = [];

    /// <inheritdoc />
    public Task<FleetSubscription?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    /// <inheritdoc />
    public Task<FleetSubscription> OpenAsync(
        Guid tenantId, string tenantCode, string nameAr, string nameEn, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    /// <inheritdoc />
    public Task<FleetSubscription> ChangePlanAsync(
        Guid tenantId, string planCode, string actor, string authority, string reasonAr,
        CancellationToken cancellationToken = default) =>
        throw Unavailable();

    /// <inheritdoc />
    public Task<FleetSubscription> LapseAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    /// <inheritdoc />
    public Task<FleetSubscription> ResumeAsync(
        Guid tenantId, string actor, string authority, string reasonAr, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    private static InvalidOperationException Unavailable() => new(
        "نداءٌ على منفذ الأسطول وهو غير مُهيَّأ. ونقاط النهاية تسأل IsAvailable قبل النداء، "
        + "فبلوغُ هذا السطر خطأ برمجي لا حالة تشغيل. / "
        + "A call on the fleet port while it is not configured. Endpoints ask IsAvailable first, "
        + "so reaching this line is a programming error, not an operating state.");
}
