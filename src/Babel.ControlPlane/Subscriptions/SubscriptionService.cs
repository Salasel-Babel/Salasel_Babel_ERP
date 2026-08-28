using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Subscriptions;

/// <summary>
/// حالة وحدة في اشتراك مستأجر، <b>باسم حالتها نصّاً لا بقيمة تعداد</b>.
/// <para>
/// والنصّ مقصود: هذا النوع يعبر إلى الجذر التركيبي، وهو الطرف الذي يترجم بين
/// المستويين. فلو عبر تعدادٌ لصار على الطرف الآخر أن يفرّع عليه — أي أن يكتب نسخةً
/// ثانية من جدول القرار خارج حدّ الاستحقاق، وهو ما تمنعه القاعدة 6 بحقّ.
/// </para>
/// </summary>
/// <param name="Code">رمز الوحدة في كتالوج مستوى التحكّم.</param>
/// <param name="NameAr">اسمها بالعربية.</param>
/// <param name="NameEn">اسمها بالإنجليزية.</param>
/// <param name="State">اسم حالتها كما هو في التعداد حرفاً بحرف.</param>
/// <param name="PostsJournal">هل يبلغ عملُها الدفتر؟ — وهو ما يجعل أرضيتها قراءةً لا نزعاً.</param>
public sealed record SubscriptionModule(string Code, string NameAr, string NameEn, string State, bool PostsJournal);

/// <summary>
/// اشتراك مستأجر كما يُقرأ من مستوى التحكّم: الخطّة، والحالة، والوحدات، وتاريخ التجديد.
/// <para>
/// <b>وكل مبلغ فيه نصّ</b> (<see cref="Canon.Amount"/>): المال يعبر نصّاً على الطرفين،
/// فلا يقع تحويلٌ إلى فاصلة عائمة بين قراءة الصفّ وكتابته على السلك.
/// </para>
/// </summary>
/// <param name="TenantId">معرّف المستأجر.</param>
/// <param name="TenantCode">رمزه القصير.</param>
/// <param name="NameAr">اسمه بالعربية.</param>
/// <param name="NameEn">اسمه بالإنجليزية.</param>
/// <param name="TenantStatus">حالته في سجل الأسطول، باسمها نصّاً.</param>
/// <param name="SubscriptionId">معرّف الاشتراك الجاري.</param>
/// <param name="PlanCode">رمز الخطّة.</param>
/// <param name="PlanNameAr">اسم الخطّة بالعربية.</param>
/// <param name="PlanNameEn">اسم الخطّة بالإنجليزية.</param>
/// <param name="MonthlyPrice">السعر الشهري نصّاً بأربع خانات.</param>
/// <param name="PerUserPrice">سعر المستخدم الواحد بعد المُضمَّن، نصّاً.</param>
/// <param name="IncludedUsers">عدد المستخدمين المُضمَّنين.</param>
/// <param name="Currency">عملة التسعير.</param>
/// <param name="StartedOn">تاريخ بدء الاشتراك الجاري.</param>
/// <param name="EndsOn">تاريخ انتهائه إن وُجد.</param>
/// <param name="State">حالته: <c>Active</c> أو <c>Lapsed</c> أو <c>Cancelled</c>.</param>
/// <param name="RenewsOn">تاريخ التجديد التالي، أو <c>null</c> لاشتراك ليس فعّالاً.</param>
/// <param name="Modules">الوحدات وحالاتها، مرتّبةً برمزها ترتيباً حرفياً ثابتاً.</param>
public sealed record SubscriptionRecord(
    Guid TenantId,
    string TenantCode,
    string NameAr,
    string NameEn,
    string TenantStatus,
    Guid SubscriptionId,
    string PlanCode,
    string PlanNameAr,
    string PlanNameEn,
    string MonthlyPrice,
    string PerUserPrice,
    int IncludedUsers,
    string Currency,
    DateOnly StartedOn,
    DateOnly? EndsOn,
    string State,
    DateOnly? RenewsOn,
    IReadOnlyList<SubscriptionModule> Modules);

/// <summary>يُرفع حين يُطلب اشتراك مستأجر لا اشتراك له في سجل مستوى التحكّم.</summary>
/// <param name="tenantId">معرّف المستأجر المطلوب.</param>
public sealed class SubscriptionNotFoundException(Guid tenantId)
    : Exception($"لا اشتراك للمستأجر «{tenantId}» في سجل مستوى التحكّم.")
{
    /// <summary>المستأجر الذي لا اشتراك له.</summary>
    public Guid TenantId { get; } = tenantId;
}

/// <summary>
/// <b>دورة حياة الاشتراك</b>: يُفتَح، ويُقرأ، وتُغيَّر خطّته، وينقطع، ويُستأنف.
/// <para>
/// <b>ولماذا وُجدت هذه الخدمة:</b> ‏<c>control.subscription</c> موجود منذ الموجة الأولى
/// ولا يقرؤه شيء ولا يكتبه إلا <c>PlanCatalog.SubscribeAsync</c> عند التزويد — أي أن
/// «انقطع اشتراك المستأجر» كانت جملةً عن جدول الاستحقاق وحده، وحالةُ الاشتراك نفسها
/// عمودٌ ساكن. وهذه الخدمة تجعل الاثنين يتحرّكان معاً: <b>حالةُ الاشتراك وحالةُ الوحدات
/// تُكتبان في الفعل الواحد</b>، فلا يبقى مستأجرٌ اشتراكه <c>Lapsed</c> ووحداته فاعلة.
/// </para>
/// <para>
/// <b>ولا جدول قرارٍ هنا:</b> الخفض والاستئناف يقعان في <see cref="EntitlementService"/>
/// (‏<c>LapseAsync</c> و<c>ApplyPlanAsync</c> و<c>DowngradeToPlanAsync</c>) — موضعُهما
/// الواحد الذي يحرسه ADR-0036. وما هنا كتابةُ صفّ الاشتراك ثم نداءٌ إليها.
/// </para>
/// <para>
/// <b>ولا يعرف هذا الملف شيئاً عن مستوى المستأجر</b> — لا منشأة، ولا عضوية، ولا جلسة.
/// أسماءُ حالات الوحدات تخرج منه <b>نصوصاً</b>، والجذر التركيبي وحده يترجمها.
/// </para>
/// </summary>
/// <param name="options">إعدادات مستوى التحكّم.</param>
/// <param name="registry">سجل المستأجرين.</param>
/// <param name="entitlements">خدمة الاستحقاق — الموضع الوحيد الذي يقرّر فيه الخفض.</param>
public sealed class SubscriptionService(
    ControlPlaneOptions options, TenantRegistry registry, EntitlementService entitlements)
{
    /// <summary>إعدادات مستوى التحكّم.</summary>
    public ControlPlaneOptions Options { get; } = options;

    /// <summary>
    /// <b>خطّة الدخول</b> — الخطّة الوحيدة التي يُفتح بها اشتراكٌ من باب تسجيلٍ مجهول
    /// الهويّة.
    /// <para>
    /// وثبوتُها شرطُ أمنٍ لا تبسيط: بابٌ بلا اعتماد يختار منه الطالب خطّته هو بابٌ
    /// يمنح الحزمة الشاملة لمن كتب اسمها في جسم الطلب. فاختيار الخطّة فعلٌ يقع
    /// <b>بعد</b> الاشتراك وباعتماد، لا عند الباب المفتوح.
    /// </para>
    /// </summary>
    public const string EntryPlanCode = "ESSENTIAL";

    /// <summary>يقرأ اشتراك مستأجر، أو <c>null</c> إن لم يكن له صفّ اشتراك.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك الجاري، أو <c>null</c>.</returns>
    public async Task<SubscriptionRecord?> FindAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
        return await ReadAsync(c, tenantId, ct);
    }

    /// <summary>
    /// يفتح اشتراك مستأجر جديد على خطّة الدخول — <b>ومُحكَم بالمعرّف</b>: النداء الثاني
    /// بالمعرّفات نفسها لا يُنشئ مستأجراً ثانياً ولا اشتراكاً ثانياً.
    /// <para>
    /// والإحكام هنا <b>بنيوي لا مُخترَع</b>: تسجيل المستأجر مُحكَم بـ<c>tenant_code</c>،
    /// والاشتراك مُحكَم بـ(المستأجر، الخطّة، تاريخ البدء)، وتطبيق الخطّة يكتب التغييرات
    /// الفعّالة وحدها. فإعادةُ النداء تصل إلى الحالة نفسها بلا صفّ زائد.
    /// </para>
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر — يشتقّه المُنادي من مفتاح الطلب.</param>
    /// <param name="tenantCode">رمزه القصير.</param>
    /// <param name="name">اسمه بالعربية والإنجليزية.</param>
    /// <param name="actor">من طلب الفتح.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك بعد الفتح.</returns>
    public async Task<SubscriptionRecord> OpenAsync(
        Guid tenantId, string tenantCode, BilingualName name, string actor, CancellationToken ct = default)
    {
        var startedOn = DateOnly.FromDateTime(Canon.Now().UtcDateTime);

        await using (var c = await Db.OpenAsync(Options.ControlConnectionString, ct))
        {
            await registry.RegisterAsync(c, tenantId, tenantCode, name, ct: ct);
            await PlanCatalog.SubscribeAsync(c, tenantId, EntryPlanCode, startedOn, ct);
            await TenantRegistry.SetStatusAsync(c, tenantId, TenantStatus.Active, Canon.Now(), ct: ct);
        }

        await entitlements.ApplyPlanAsync(
            tenantId,
            EntryPlanCode,
            new ChangeAuthority(actor, "signup:" + tenantCode, "فتحُ اشتراك جديد على خطّة الدخول"),
            ct);

        return await RequireAsync(tenantId, ct);
    }

    /// <summary>
    /// يغيّر خطّة المستأجر: ما تغطّيه الخطّة الجديدة يصير مستحقّاً، وما خرج منها يهبط
    /// إلى <b>أرضيته</b> لا إلى العدم (ADR-0034).
    /// <para>
    /// <b>وصفّ الاشتراك القديم لا يُعدَّل ولا يُحذف:</b> يُغلَق بتاريخ ويُفتح صفٌّ جديد.
    /// فتاريخ الاشتراك يبقى مقروءاً، ولا يصير «على أي خطّة كان في مارس؟» سؤالاً بلا جواب.
    /// </para>
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="planCode">رمز الخطّة الجديدة.</param>
    /// <param name="authority">السند: من، وبأي صلاحية، ولماذا.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك بعد التغيير.</returns>
    public async Task<SubscriptionRecord> ChangePlanAsync(
        Guid tenantId, string planCode, ChangeAuthority authority, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        PlanCatalog.Require(planCode);

        var current = await RequireAsync(tenantId, ct);
        var on = DateOnly.FromDateTime(Canon.Now().UtcDateTime);

        if (!string.Equals(current.PlanCode, planCode, StringComparison.Ordinal))
        {
            await using var c = await Db.OpenAsync(Options.ControlConnectionString, ct);
            await CloseAsync(c, current.SubscriptionId, "Cancelled", on, ct);
            await PlanCatalog.SubscribeAsync(c, tenantId, planCode, on, ct);
        }

        await entitlements.DowngradeToPlanAsync(tenantId, planCode, authority, ct);
        return await RequireAsync(tenantId, ct);
    }

    /// <summary>
    /// <b>انقطاع الاشتراك.</b> يُغلق الصفّ بحالة <c>Lapsed</c>، وينزل بكل وحدة إلى
    /// أرضيتها — قراءةً وتقارير وتصديراً كاملة، بلا مستند جديد وبلا ترحيل.
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="authority">السند.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك بعد الانقطاع.</returns>
    public async Task<SubscriptionRecord> LapseAsync(
        Guid tenantId, ChangeAuthority authority, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(authority);

        var current = await RequireAsync(tenantId, ct);
        var on = DateOnly.FromDateTime(Canon.Now().UtcDateTime);

        await using (var c = await Db.OpenAsync(Options.ControlConnectionString, ct))
        {
            await CloseAsync(c, current.SubscriptionId, "Lapsed", on, ct);
        }

        await entitlements.LapseAsync(tenantId, authority, ct);
        return await RequireAsync(tenantId, ct);
    }

    /// <summary>
    /// <b>استئناف الاشتراك.</b> يفتح صفّاً فعّالاً جديداً على الخطّة نفسها، ويُعيد
    /// وحداتها إلى الاستحقاق.
    /// </summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="authority">السند.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك بعد الاستئناف.</returns>
    public async Task<SubscriptionRecord> ResumeAsync(
        Guid tenantId, ChangeAuthority authority, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(authority);

        var current = await RequireAsync(tenantId, ct);
        var on = DateOnly.FromDateTime(Canon.Now().UtcDateTime);

        await using (var c = await Db.OpenAsync(Options.ControlConnectionString, ct))
        {
            // ‏`SubscribeAsync` مُحكَم بـ(المستأجر، الخطّة، تاريخ البدء)، فاستئنافٌ في
            // **يوم الانقطاع نفسه** يجد الصفّ الذي أُغلق للتوّ ويُعيده كما هو — أي
            // يُرجع «تمّ» على فعلٍ لم يقع. ولذلك يُفتح الصفّ المُعاد صراحةً: الحالة
            // إلى فعّالة والنهاية تُمحى، وهو ما يجعل الاستئناف صحيحاً في اليوم نفسه
            // كما هو صحيح بعد شهر.
            var id = await PlanCatalog.SubscribeAsync(c, tenantId, current.PlanCode, on, ct);
            await ReopenAsync(c, id, ct);
        }

        await entitlements.ApplyPlanAsync(tenantId, current.PlanCode, authority, ct);
        return await RequireAsync(tenantId, ct);
    }

    /// <summary>يقرأ اشتراك مستأجر ويرمي إن لم يوجد.</summary>
    /// <param name="tenantId">معرّف المستأجر.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الاشتراك الجاري.</returns>
    /// <exception cref="SubscriptionNotFoundException">لا صفّ اشتراك لهذا المستأجر.</exception>
    public async Task<SubscriptionRecord> RequireAsync(Guid tenantId, CancellationToken ct = default) =>
        await FindAsync(tenantId, ct) ?? throw new SubscriptionNotFoundException(tenantId);

    private static async Task CloseAsync(
        NpgsqlConnection c, Guid subscriptionId, string state, DateOnly on, CancellationToken ct) =>
        await Db.WriteAsync(c, """
            update control.subscription
               set state = @s, ends_on = @on
             where subscription_id = @id
            """, 1, p =>
            {
                p.AddWithValue("s", state);
                p.Add(Db.P("on", on, NpgsqlDbType.Date));
                p.Add(Db.P("id", subscriptionId, NpgsqlDbType.Uuid));
            }, null, ct);

    private static async Task ReopenAsync(NpgsqlConnection c, Guid subscriptionId, CancellationToken ct) =>
        await Db.WriteAsync(c, """
            update control.subscription
               set state = 'Active', ends_on = null
             where subscription_id = @id
            """, 1, p => p.Add(Db.P("id", subscriptionId, NpgsqlDbType.Uuid)), null, ct);

    private async Task<SubscriptionRecord?> ReadAsync(NpgsqlConnection c, Guid tenantId, CancellationToken ct)
    {
        // الاشتراك الجاري: آخر صفّ بتاريخ بدء، ثمّ بمعرّفه — ترتيبٌ كلّي ثابت، فلا
        // يتغيّر الجواب بين تشغيلين على صفّين بدآ في اليوم نفسه.
        var rows = await Db.QueryAsync(c, """
            select s.subscription_id, s.plan_code, s.started_on, s.ends_on, s.state,
                   t.tenant_code, t.name_ar, t.name_en, t.status
              from control.subscription s
              join control.tenant t on t.tenant_id = s.tenant_id
             where s.tenant_id = @t
             order by s.started_on desc, s.subscription_id::text desc
             limit 1
            """,
            r => (
                Id: r.GetGuid(0),
                Plan: r.GetString(1),
                Started: r.GetFieldValue<DateOnly>(2),
                Ends: r.IsDBNull(3) ? (DateOnly?)null : r.GetFieldValue<DateOnly>(3),
                State: r.GetString(4),
                Code: r.GetString(5),
                Ar: r.GetString(6),
                En: r.GetString(7),
                Status: r.GetString(8)),
            p => p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid)), null, ct);

        if (rows.Count == 0)
        {
            return null;
        }

        var row = rows[0];
        var plan = PlanCatalog.Require(row.Plan);
        var currency = await Db.ScalarAsync<string>(c,
            "select currency from control.plan where plan_code = @p",
            p => p.AddWithValue("p", row.Plan), null, ct) ?? "SAR";

        var set = await entitlements.GetSetAsync(tenantId, ct);

        var modules = ModuleCatalog.All
            .OrderBy(m => m.Code, StringComparer.Ordinal)
            .Select(m => new SubscriptionModule(
                m.Code, m.NameAr, m.NameEn,
                set.TryGetValue(m.Code, out var state) ? state.ToString() : NeverPurchased,
                m.PostsJournal))
            .ToList();

        return new SubscriptionRecord(
            tenantId, row.Code, row.Ar, row.En, row.Status,
            row.Id, plan.Code, plan.NameAr, plan.NameEn,
            Canon.Amount(plan.MonthlyPrice), Canon.Amount(plan.PerUserPrice), plan.IncludedUsers, currency,
            row.Started, row.Ends, row.State,
            RenewalOf(row.State, row.Started),
            modules);
    }

    /// <summary>
    /// اسم حالة الوحدة التي <b>لم تُشترَ قط</b> — نصّاً لا قيمةَ تعداد، للسبب المكتوب
    /// على <see cref="SubscriptionModule"/>: هذا الملف خارج حدّ الاستحقاق فلا يفرّع عليه.
    /// وهو مقروءٌ من التعداد نفسه عند صفر، لا مكتوباً بيد فينحرف عنه.
    /// </summary>
    private static string NeverPurchased { get; } = default(EntitlementState).ToString();

    /// <summary>
    /// تاريخ التجديد التالي: الذكرى الشهرية لتاريخ البدء بعد اليوم.
    /// <para>
    /// <b>ولا تجديد لاشتراك ليس فعّالاً</b>: <c>null</c> لا تاريخٌ مستقبلي — فتاريخٌ
    /// يُعرض على اشتراك منقطع يُقرأ وعداً بأن الخدمة ستعود من تلقاء نفسها، وهي لا تعود.
    /// </para>
    /// <para>
    /// واليوم الذي لا وجود له في الشهر التالي (‏31 في فبراير) يُقصّ إلى آخر يوم فيه —
    /// وهو ما يفعله التقويم الميلادي نفسه، لا اختراعٌ هنا.
    /// </para>
    /// </summary>
    private static DateOnly? RenewalOf(string state, DateOnly startedOn)
    {
        if (!string.Equals(state, "Active", StringComparison.Ordinal))
        {
            return null;
        }

        var today = DateOnly.FromDateTime(Canon.Now().UtcDateTime);
        var next = startedOn;
        var months = 0;

        while (next <= today && months < MaximumRenewalSearchMonths)
        {
            months++;
            next = startedOn.AddMonths(months);
        }

        return next;
    }

    /// <summary>
    /// سقفُ بحثٍ مُعلَن على حلقة التجديد: مئة سنة بالأشهر.
    /// <para>وحلقةٌ بلا سقف على تاريخٍ يأتي من قاعدة بيانات هي حلقةٌ لا نهائية تنتظر
    /// صفّاً واحداً معطوباً — والسقف يجعلها تخرج بجوابٍ بدل أن تُعلّق طلباً.</para>
    /// </summary>
    private const int MaximumRenewalSearchMonths = 1200;

    /// <summary>الخطط المعروضة، مرتّبةً برمزها — يقرؤها السطح فلا يكتب قائمةً ثانية.</summary>
    /// <returns>كل خطّة برمزها واسمَيها وسعرها نصّاً ووحداتها.</returns>
    public static IReadOnlyList<(string Code, string NameAr, string NameEn, string MonthlyPrice,
        string PerUserPrice, int IncludedUsers, IReadOnlyList<string> Modules)> Plans() =>
        [.. PlanCatalog.All
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .Select(p => (
                p.Code, p.NameAr, p.NameEn,
                Canon.Amount(p.MonthlyPrice), Canon.Amount(p.PerUserPrice), p.IncludedUsers,
                (IReadOnlyList<string>)[.. p.Modules.OrderBy(m => m, StringComparer.Ordinal)]))];

    /// <summary>رموز الخطط المعروفة مفصولةً — تُقرأ من الكتالوج فلا تُكتب قائمةً ثانية.</summary>
    /// <returns>الرموز مرتّبةً ومفصولةً بنقطة وسطى.</returns>
    public static string KnownPlans() =>
        string.Join(" · ", PlanCatalog.All.Select(p => p.Code).OrderBy(c => c, StringComparer.Ordinal));
}
