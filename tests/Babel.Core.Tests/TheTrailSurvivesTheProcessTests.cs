using System.Globalization;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>الأثر والقياس ينجوان من إعادة الإقلاع — وهو ما كان يسقط على الخادم الحقيقي.</b>
/// <para>
/// كان <c>AddBabelCoreShared</c> — <b>وهي تُنادى من مسار PostgreSQL نفسه</b> — يسجّل
/// <c>InMemoryAuditLog</c> و<c>InMemoryUsageStore</c>. فكل نشرة كانت تمحو «من فعل ماذا
/// ومتى» في نظامٍ محاسبي، وسقفُ الإنفاق كان يُتجاوَز بإعادة تشغيل، وخادمان خلف موزّع
/// كانا يريان سجلَّين مختلفين.
/// </para>
/// <para>
/// <b>ولماذا الحاوية تُهدَم وتُبنى ولا يكفي أن نكتب ونقرأ:</b> الكتابة والقراءة في
/// العملية نفسها <b>تمرّان على تنفيذ الذاكرة أيضاً</b> — وهو بالضبط ما لا نريد. فكل
/// إثبات هنا يبني الجذر التركيبي، ويكتب، ثم <b>يتخلّص من الحاوية كلّها</b> ويُفرغ
/// تجمّعات الاتصال، ثم يبني جذراً تركيبياً ثانياً لا يشترك مع الأول في بايتٍ واحد من
/// الحالة، ويقرأ منه. وإعادةُ التسجيل إلى الذاكرة تُسقط كل إثباتٍ من هذه — وهو الشاهد
/// السالب المُوثَّق في <c>docs/evidence/measurements.md</c>.
/// </para>
/// <para>
/// <b>وما لا يُثبَت هنا بالنيّة:</b> «يُلحَق ولا يُعدَّل ولا يُحذف» جملةٌ لا تعني شيئاً
/// إن كانت مبنيّةً على غياب دالّةٍ في واجهة. فالقسم الثالث يطلب التعديل والحذف
/// والاقتطاع <b>فعلاً</b> — بدور التطبيق وبدور المالك — ويقرأ رمز الرفض من PostgreSQL.
/// </para>
/// </summary>
public sealed class TheTrailSurvivesTheProcessTests
{
    private static readonly UserId Actor = new(new Guid("a0d17000-0000-4000-8000-00000000000a"));
    private static readonly UserId SecondActor = new(new Guid("a0d17000-0000-4000-8000-00000000000b"));

    /// <summary>لحظةٌ بدقّة الميكروثانية تماماً — <c>timestamptz</c> يقصّ ما دونها.</summary>
    private static readonly DateTimeOffset At =
        new DateTimeOffset(2026, 3, 14, 9, 15, 26, TimeSpan.Zero).AddTicks(5358980);

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · الأثر ينجو من هدم الحاوية وإعادة بنائها
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task قيدُ_التدقيق_يُقرأ_من_حاويةٍ_ثانية_بُنيت_بعد_هدم_الأولى()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        IAuditLog firstLog;

        // «العملية الأولى»: جذرٌ تركيبيّ كامل يكتب قيداً ثم يُهدم.
        await using (ServiceProvider first = NewComposition())
        {
            firstLog = first.GetRequiredService<IAuditLog>();

            await firstLog.RecordAsync(
                new AuditEntry(
                    tenant,
                    Actor,
                    At,
                    "entitlement.changed",
                    "Sales",
                    "NotEntitled -> Entitled; reason: تفعيل بعد توقيع العقد"),
                TestContext.Current.CancellationToken);

            // القراءة من الحاوية نفسها **لا تُثبت شيئاً وحدها** — تنفيذ الذاكرة يمرّ منها
            // أيضاً. وهي هنا كي يكون سقوطُ ما بعدها سقوطَ الاستمرارية لا سقوطَ الكتابة.
            Assert.Single(await firstLog.ReadAsync(tenant, TestContext.Current.CancellationToken));
        }

        // ‏**هدمٌ فعليّ**: الحاوية تخلّصت من مفرداتها، وتجمّعات الاتصال أُفرغت — فلا شيء
        // من الأول يعبر إلى الثاني إلا ما استقرّ في القاعدة.
        NpgsqlConnection.ClearAllPools();

        await using ServiceProvider second = NewComposition();
        IAuditLog secondLog = second.GetRequiredService<IAuditLog>();

        Assert.NotSame(firstLog, secondLog);

        IReadOnlyList<AuditEntry> read = await secondLog.ReadAsync(tenant, TestContext.Current.CancellationToken);

        AuditEntry entry = Assert.Single(read);
        Assert.Equal(tenant, entry.Tenant);
        Assert.Equal(Actor, entry.Actor);
        Assert.Equal(At, entry.OccurredAt);
        Assert.Equal("entitlement.changed", entry.Action);
        Assert.Equal("Sales", entry.Subject);
        Assert.Equal("NotEntitled -> Entitled; reason: تفعيل بعد توقيع العقد", entry.Details);

        CoreTestEnvironment.Note(
            "قُرئ بعد هدم الحاوية: " + entry.Action + " · " + entry.Subject
            + " · " + entry.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task القيود_تُقرأ_بترتيب_وقوعها_ولو_وقع_قيدان_في_الميكروثانية_نفسها()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using (ServiceProvider first = NewComposition())
        {
            IAuditLog log = first.GetRequiredService<IAuditLog>();

            // الثالث يقع **قبل** الأول زمناً، والأول والثاني في اللحظة نفسها بالضبط:
            // فالترتيب المطلوب هو (الثالث، الأول، الثاني) — لا ترتيب الكتابة.
            await log.RecordAsync(New(tenant, At, "core.company_founded", "أ"), TestContext.Current.CancellationToken);
            await log.RecordAsync(New(tenant, At, "core.company_founded", "ب"), TestContext.Current.CancellationToken);
            await log.RecordAsync(
                New(tenant, At.AddSeconds(-30), "core.company_founded", "ج"),
                TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();

        await using ServiceProvider second = NewComposition();
        IReadOnlyList<AuditEntry> read = await second.GetRequiredService<IAuditLog>()
            .ReadAsync(tenant, TestContext.Current.CancellationToken);

        Assert.Equal(["ج", "أ", "ب"], read.Select(static e => e.Subject));
        CoreTestEnvironment.Note("الترتيب المقروء: " + string.Join(" ← ", read.Select(static e => e.Subject)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · نطاق المستأجر مفروضٌ في الاستعلام — لا مستأجرٌ يقرأ أثر آخر
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task مستأجرٌ_لا_يقرأ_أثر_مستأجرٍ_آخر()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId mine = new(CoreTestEnvironment.NewCompany());
        TenantId theirs = new(CoreTestEnvironment.NewCompany());

        await using (ServiceProvider first = NewComposition())
        {
            IAuditLog log = first.GetRequiredService<IAuditLog>();
            await log.RecordAsync(New(mine, At, "access.membership_granted", "لي"), TestContext.Current.CancellationToken);
            await log.RecordAsync(
                New(theirs, At, "access.membership_granted", "لغيري"),
                TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();

        await using ServiceProvider second = NewComposition();
        IAuditLog reader = second.GetRequiredService<IAuditLog>();

        AuditEntry read = Assert.Single(await reader.ReadAsync(mine, TestContext.Current.CancellationToken));
        Assert.Equal("لي", read.Subject);
        Assert.Equal(mine, read.Tenant);

        AuditEntry other = Assert.Single(await reader.ReadAsync(theirs, TestContext.Current.CancellationToken));
        Assert.Equal("لغيري", other.Subject);

        // والصفّان موجودان **كلاهما** في القاعدة: لو كان أحدهما غير مكتوب لمرّ هذا
        // الإثبات وهو لا يقيس شيئاً — نطاقٌ يُرشّح لا شيء ليس نطاقاً.
        Assert.Equal(
            2,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.audit_entry where tenant_id in ('{mine.Value:D}', '{theirs.Value:D}')"));

        CoreTestEnvironment.Note("صفّان في القاعدة، وكلُّ مستأجرٍ يقرأ واحداً.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · يُلحَق ولا يُعدَّل ولا يُحذف — رفضٌ **مقيس** من PostgreSQL
    //
    //     وطبقتان لا واحدة: الصلاحيات تحمي من دور التطبيق، والمشغّل يحمي من
    //     كل فاعلٍ **ومنهم المالك**. فالقاعدة لها أبوابٌ غير مسار الطلب.
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("update core.audit_entry set subject = 'مُحرَّف' where tenant_id = '{0}'")]
    [InlineData("delete from core.audit_entry where tenant_id = '{0}'")]
    [InlineData("truncate core.audit_entry")]
    public async Task دور_التطبيق_لا_يعدّل_قيد_تدقيق_ولا_يحذفه_ولا_يقتطع_السجلّ(string statement)
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());
        await RecordOneAsync(tenant, "شاهدٌ لا يُمسّ");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.ApplicationAsync(
                string.Format(CultureInfo.InvariantCulture, statement, tenant.Value.ToString("D", CultureInfo.InvariantCulture))));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
        CoreTestEnvironment.Note("رفض PostgreSQL بالرمز " + refused.SqlState + ": " + refused.MessageText);

        // والصفّ باقٍ بنصّه: رفضٌ لا يترك أثراً هو رفضٌ فعلي لا رسالة.
        Assert.Equal(
            1,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.audit_entry where tenant_id = '{tenant.Value:D}' and subject = 'شاهدٌ لا يُمسّ'"));
    }

    [Theory]
    [InlineData("update core.audit_entry set subject = 'مُحرَّف' where tenant_id = '{0}'")]
    [InlineData("delete from core.audit_entry where tenant_id = '{0}'")]
    [InlineData("truncate core.audit_entry")]
    public async Task قيدُ_التدقيق_لا_يُمسّ_ولو_كان_الفاعل_هو_المالك(string statement)
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());
        await RecordOneAsync(tenant, "شاهدٌ لا يمسّه المالك");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.OwnerAsync(
                string.Format(CultureInfo.InvariantCulture, statement, tenant.Value.ToString("D", CultureInfo.InvariantCulture))));

        Assert.Contains("APPEND_ONLY_VIOLATION", refused.MessageText, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رفض المشغّل: " + refused.MessageText);

        Assert.Equal(
            1,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.audit_entry where tenant_id = '{tenant.Value:D}' and subject = 'شاهدٌ لا يمسّه المالك'"));
    }

    [Fact]
    public async Task مشغّلا_الإلحاق_ليسا_ضامرين_والإدخال_ما_زال_يمرّ()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        // الحارس الموجب: المشغّل يرفض التعديل والحذف **ولا يرفض الإلحاق**. ومشغّلٌ
        // يرفض كل شيء كان سيُمرّ إثباتات الرفض أعلاه وهو يمنع الالتقاط نفسه.
        await RecordOneAsync(tenant, "أوّل");
        await RecordOneAsync(tenant, "ثانٍ");

        Assert.Equal(
            2,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.audit_entry where tenant_id = '{tenant.Value:D}'"));

        // والمشغّلات الاثنا عشر حيّة — مقروءةً من pg_trigger لا من ملفّ هجرة.
        //
        // ‏**وكانت ستّة، فصارت اثني عشر حين انضمّت جداول المعامِلات الثلاثة إلى
        // الانضباط نفسه** (‏parameter_version · parameter_value · parameter_usage،
        // مشغّلان لكلٍّ منها في `CoreParameterAppendOnly.sql`). والرقم مكتوبٌ هنا
        // صراحةً لا محسوباً من قائمة: قائمةٌ تُحسب من الواقع تمرّ خضراء حين يسقط
        // مشغّلٌ كلَّه، وهذا الرقم يُحمِّر البناء عند أول سقوط.
        long triggers = await CoreTestEnvironment.CountAsync(
            "select count(*) from pg_trigger where not tgisinternal and tgname like '%_append_only' or "
            + "(not tgisinternal and tgname like '%_no_truncate')");
        Assert.Equal(12, triggers);
        CoreTestEnvironment.Note("مشغّلات الإلحاق الحيّة: " + triggers.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("audit_entry")]
    [InlineData("module_usage")]
    [InlineData("user_activity")]
    public async Task صلاحياتُ_دور_التطبيق_على_السجلّات_الثلاثة_هي_القراءة_والإلحاق_فقط(string table)
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        // ‏**المنحة تُقرأ من الكتالوج لا من ملفّ النصّ.** ملفُّ `CoreGrants.sql` يقول ما
        // نوى كاتبُه؛ و`information_schema.table_privileges` يقول ما هو قائمٌ فعلاً.
        // وإثباتُ الرفض أعلاه يمسك المنحة الزائدة، وهذا يمسك **النقصان**: منحةٌ سقطت
        // فصار الإلحاق نفسه ممنوعاً تمرّ من كل إثبات رفضٍ خضراء.
        await using NpgsqlConnection owner = new(CoreTestEnvironment.Options.OwnerConnectionString);
        await owner.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(
            """
            select coalesce(string_agg(privilege_type, ',' order by privilege_type), '')
            from information_schema.table_privileges
            where table_schema = 'core' and table_name = $1 and grantee = $2
            """,
            owner);

        command.Parameters.Add(new NpgsqlParameter { Value = table });
        command.Parameters.Add(new NpgsqlParameter { Value = CoreTestEnvironment.AppRole });

        object? value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        string granted = value as string ?? string.Empty;

        Assert.Equal("INSERT,SELECT", granted);
        CoreTestEnvironment.Note("صلاحيات دور التطبيق على core." + table + ": " + granted);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٤ · قياس الاستخدام — المحوران معاً ينجوان، ومحصوران بالمستأجر وبالشهر
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task قياسُ_الاستخدام_على_المحورين_يُقرأ_من_حاويةٍ_ثانية()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());
        BillingPeriod period = BillingPeriod.FromInstant(At);

        await using (ServiceProvider first = NewComposition())
        {
            IUsageMeter meter = first.GetRequiredService<IUsageMeter>();

            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(tenant, BabelModule.Sales, "Sales.Invoice.Issue", Actor, At, 1),
                TestContext.Current.CancellationToken);
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(tenant, BabelModule.Sales, "Sales.Invoice.Issue", SecondActor, At.AddHours(1), 4),
                TestContext.Current.CancellationToken);
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(tenant, BabelModule.Ledger, "Ledger.Entry.Post", Actor, At, 2),
                TestContext.Current.CancellationToken);

            await meter.RecordUserActivityAsync(
                new UserActivityEvent(tenant, Actor, BabelModule.Sales, "Sales.Invoice.Issue", At, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
            await meter.RecordUserActivityAsync(
                new UserActivityEvent(tenant, SecondActor, BabelModule.Sales, "Sales.Invoice.Read", At, EntitlementState.ReadOnly),
                TestContext.Current.CancellationToken);
            await meter.RecordUserActivityAsync(
                new UserActivityEvent(tenant, Actor, BabelModule.Ledger, "Ledger.Entry.Post", At, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();

        await using ServiceProvider second = NewComposition();
        IUsageReader reader = second.GetRequiredService<IUsageReader>();

        IReadOnlyDictionary<BabelModule, long> totals =
            await reader.GetModuleUsageAsync(tenant, period, TestContext.Current.CancellationToken);

        Assert.Equal(2, totals.Count);
        Assert.Equal(5, totals[BabelModule.Sales]);
        Assert.Equal(2, totals[BabelModule.Ledger]);

        IReadOnlyCollection<UserId> users =
            await reader.GetActiveUsersAsync(tenant, period, TestContext.Current.CancellationToken);

        Assert.Equal(2, users.Count);
        Assert.Contains(Actor, users);
        Assert.Contains(SecondActor, users);

        CoreTestEnvironment.Note(
            "بعد هدم الحاوية: المبيعات " + totals[BabelModule.Sales].ToString(CultureInfo.InvariantCulture)
            + " · الدفتر " + totals[BabelModule.Ledger].ToString(CultureInfo.InvariantCulture)
            + " · المستخدمون " + users.Count.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task القياس_محصورٌ_بشهر_الفوترة_وبالمستأجر_معاً()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId mine = new(CoreTestEnvironment.NewCompany());
        TenantId theirs = new(CoreTestEnvironment.NewCompany());

        // ثلاث لحظات: أول ثانيةٍ في الشهر، وآخر ميكروثانيةٍ فيه، وأول ثانيةٍ في التالي.
        DateTimeOffset firstInstant = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset lastInstant = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-10);
        DateTimeOffset nextMonth = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        await using (ServiceProvider first = NewComposition())
        {
            IUsageMeter meter = first.GetRequiredService<IUsageMeter>();

            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(mine, BabelModule.Hr, "Hr.Payslip.Issue", Actor, firstInstant, 7),
                TestContext.Current.CancellationToken);
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(mine, BabelModule.Hr, "Hr.Payslip.Issue", Actor, lastInstant, 3),
                TestContext.Current.CancellationToken);

            // خارج الشهر — والحدّ **نصفُ مفتوح**: أوّلُ لحظةٍ في الشهر التالي ليست منه.
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(mine, BabelModule.Hr, "Hr.Payslip.Issue", Actor, nextMonth, 100),
                TestContext.Current.CancellationToken);

            // مستأجرٌ آخر في الشهر نفسه.
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(theirs, BabelModule.Hr, "Hr.Payslip.Issue", SecondActor, firstInstant, 900),
                TestContext.Current.CancellationToken);

            await meter.RecordUserActivityAsync(
                new UserActivityEvent(mine, Actor, BabelModule.Hr, "Hr.Payslip.Issue", firstInstant, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
            await meter.RecordUserActivityAsync(
                new UserActivityEvent(mine, SecondActor, BabelModule.Hr, "Hr.Payslip.Issue", nextMonth, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
            await meter.RecordUserActivityAsync(
                new UserActivityEvent(theirs, SecondActor, BabelModule.Hr, "Hr.Payslip.Issue", firstInstant, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();

        await using ServiceProvider second = NewComposition();
        IUsageReader reader = second.GetRequiredService<IUsageReader>();

        BillingPeriod march = new(2026, 3);
        BillingPeriod april = new(2026, 4);

        Assert.Equal(
            10,
            (await reader.GetModuleUsageAsync(mine, march, TestContext.Current.CancellationToken))[BabelModule.Hr]);
        Assert.Equal(
            100,
            (await reader.GetModuleUsageAsync(mine, april, TestContext.Current.CancellationToken))[BabelModule.Hr]);
        Assert.Equal(
            900,
            (await reader.GetModuleUsageAsync(theirs, march, TestContext.Current.CancellationToken))[BabelModule.Hr]);

        Assert.Equal(
            [Actor],
            await reader.GetActiveUsersAsync(mine, march, TestContext.Current.CancellationToken));
        Assert.Equal(
            [SecondActor],
            await reader.GetActiveUsersAsync(mine, april, TestContext.Current.CancellationToken));

        CoreTestEnvironment.Note("آذار 10 · نيسان 100 · مستأجرٌ آخر 900 — والحدّ نصف مفتوح.");
    }

    [Theory]
    [InlineData("update core.module_usage set quantity = 0 where tenant_id = '{0}'")]
    [InlineData("delete from core.module_usage where tenant_id = '{0}'")]
    [InlineData("update core.user_activity set entitlement_state = 'not_entitled' where tenant_id = '{0}'")]
    [InlineData("delete from core.user_activity where tenant_id = '{0}'")]
    public async Task دور_التطبيق_لا_يُنقص_فاتورةً_بتعديل_قياسٍ_ولا_بحذفه(string statement)
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using (ServiceProvider first = NewComposition())
        {
            IUsageMeter meter = first.GetRequiredService<IUsageMeter>();
            await meter.RecordModuleUsageAsync(
                new ModuleUsageEvent(tenant, BabelModule.Pos, "Pos.Sale.Close", Actor, At, 6),
                TestContext.Current.CancellationToken);
            await meter.RecordUserActivityAsync(
                new UserActivityEvent(tenant, Actor, BabelModule.Pos, "Pos.Sale.Close", At, EntitlementState.Entitled),
                TestContext.Current.CancellationToken);
        }

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.ApplicationAsync(
                string.Format(CultureInfo.InvariantCulture, statement, tenant.Value.ToString("D", CultureInfo.InvariantCulture))));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
        CoreTestEnvironment.Note("رفض PostgreSQL بالرمز " + refused.SqlState + ": " + refused.MessageText);

        Assert.Equal(
            6,
            await CoreTestEnvironment.CountAsync(
                $"select coalesce(sum(quantity), 0) from core.module_usage where tenant_id = '{tenant.Value:D}'"));
    }

    [Fact]
    public async Task قياسٌ_مخزَّن_على_وحدةٍ_لا_يعرفها_التعداد_يُرفض_عند_القراءة_ولا_يُقرأ_صامتاً()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        // قيدُ المخطّط `module >= 1` بلا حدٍّ أعلى عمداً — كي لا يرفض وحدةً جديدة. فحارسُ
        // المجموعة المغلقة عند القراءة، والزرع هنا بدور المالك: بابٌ لا يمرّ بأي نوع.
        await CoreTestEnvironment.OwnerAsync(
            $"""
            insert into core.module_usage (tenant_id, module, operation, actor_id, occurred_at, quantity)
            values ('{tenant.Value:D}', 99, 'Ghost.Operation', '{Actor.Value:D}', '2026-03-14T09:15:26Z', 1)
            """);

        await using ServiceProvider provider = NewComposition();

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetRequiredService<IUsageReader>()
                .GetModuleUsageAsync(tenant, new BillingPeriod(2026, 3), TestContext.Current.CancellationToken));

        Assert.Contains("99", refused.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رُفضت القراءة: " + refused.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٥ · والجذر التركيبي يعطي الدائم لا الذاكرة — وهذا ما يمنع الارتداد
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void مسارُ_PostgreSQL_يعطي_سجلّاً_دائماً_ومخزنَ_استخدامٍ_دائماً()
    {
        using ServiceProvider provider = NewComposition();

        // النوع يُسأل من الحاوية لا من ملفّ التسجيل: «الخادم يستعمل الذاكرة» لا يجوز أن
        // يبقى شيئاً يُكتشَف في عرض. والأنواع internal فترى الاختباراتُ داخلَ النواة.
        Assert.IsType<Babel.Core.Persistence.PostgresAuditLog>(provider.GetRequiredService<IAuditLog>());
        Assert.IsType<Babel.Core.Persistence.PostgresUsageStore>(provider.GetRequiredService<IUsageStore>());
        Assert.IsType<Babel.Core.Persistence.PostgresUsageStore>(provider.GetRequiredService<IUsageMeter>());
        Assert.IsType<Babel.Core.Persistence.PostgresUsageStore>(provider.GetRequiredService<IUsageReader>());

        // والواجهات الثلاث مثيلٌ واحد، كما في نظيره في الذاكرة تماماً: مثيلان يعنيان
        // كاتباً وقارئاً لا يريان بعضهما — وهو العطل نفسه بشكلٍ آخر.
        Assert.Same(provider.GetRequiredService<IUsageStore>(), provider.GetRequiredService<IUsageMeter>());
        Assert.Same(provider.GetRequiredService<IUsageStore>(), provider.GetRequiredService<IUsageReader>());
    }

    [Fact]
    public void المسارُ_بلا_قاعدةٍ_يبقى_على_الذاكرة_كما_كان()
    {
        // ‏`AddBabelCore()` بلا إعدادات هو تحميل **اختبارات الوحدة** — ولا قاعدة له.
        // ونقلُ الذاكرة منه كان سيكسر كل مستهلكٍ لا قاعدة عنده، بلا أن يُصلح شيئاً.
        ServiceCollection services = new();
        services.AddBabelCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryAuditLog>(provider.GetRequiredService<IAuditLog>());
        Assert.IsType<InMemoryUsageStore>(provider.GetRequiredService<IUsageStore>());
        Assert.Same(provider.GetRequiredService<IUsageStore>(), provider.GetRequiredService<IUsageMeter>());
    }

    // ── أدوات ───────────────────────────────────────────────────────────────

    /// <summary>
    /// جذرٌ تركيبيّ كامل على مسار PostgreSQL — <b>هو نفسه ما يبنيه الخادم</b>.
    /// <para>
    /// والبناء عبر <c>AddBabelCore(options)</c> لا بإنشاء النوع الدائم يدوياً: إنشاءٌ
    /// يدوي كان سيمرّ خضراء ولو بقي التسجيل على الذاكرة — أي كان سيثبت أن النوع يعمل
    /// ولا يثبت أن الخادم يستعمله.
    /// </para>
    /// </summary>
    private static ServiceProvider NewComposition()
    {
        ServiceCollection services = new();
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = CoreTestEnvironment.Options.AppConnectionString;
            options.OwnerConnectionString = CoreTestEnvironment.Options.OwnerConnectionString;
            options.AppRole = CoreTestEnvironment.Options.AppRole;
        });

        return services.BuildServiceProvider();
    }

    private static AuditEntry New(TenantId tenant, DateTimeOffset at, string action, string subject) =>
        new(tenant, Actor, at, action, subject, null);

    private static async Task RecordOneAsync(TenantId tenant, string subject)
    {
        await using ServiceProvider provider = NewComposition();
        await provider.GetRequiredService<IAuditLog>()
            .RecordAsync(New(tenant, At, "entitlement.changed", subject), TestContext.Current.CancellationToken);
    }
}
