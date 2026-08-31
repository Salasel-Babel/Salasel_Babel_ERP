using System.Globalization;
using Babel.Core.CapabilityProfile;
using Babel.Core.CompanySetup;
using Babel.Core.Persistence;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>تأسيس المنشأة يبقى بعد أن تموت العملية — وهو ما كان يسقط العرض.</b>
/// <para>
/// كان المخزن <c>InMemoryCompanySetupStore</c>: حالتُه عمرُ العملية. وكل مسار كتابة
/// في النظام يسأل <see cref="ICostCenterResolver"/> عن مركز التكلفة <b>قبل</b> أن يبني
/// طلباً (ADR-0026 · ADR-0029)، فخادمٌ أُقلع للتوّ كان يردّ كل ترحيل بـ
/// <c>company_setup.not_found</c> بينما تعمل شاشات القراءة كلّها — ميزانٌ يُقرأ وفاتورة
/// لا تُكتب.
/// </para>
/// <para>
/// <b>وما يُثبَت هنا ليس «الصفّ يُكتب ويُقرأ»</b> — ذلك أسهل ما في الأمر. المُثبَت أن
/// <b>التحميل لا يستطيع أن يلتفّ على الثابتة</b>: صفٌّ مخالف زُرع بدور المالك لا يُنتج
/// كائناً مخالفاً، بل لا يُنتج كائناً. طبقةُ استمرارية تستطيع أن تُرجع
/// <see cref="FoundedCompany"/> بسجلّ فارغ أو بافتراضيٍّ موقوف تكون قد أزالت الثابتة
/// صامتةً، ويبقى النوع يَعِد بما لا يفي به.
/// </para>
/// </summary>
public sealed class CompanySetupSurvivesTheProcessTests
{
    private static readonly UserId Actor = new(new Guid("c0e0c0e0-0000-4000-8000-0000000000a1"));

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · ما بُني في «عملية» لا يُفقد في التالية
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task التأسيس_يُقرأ_من_مخزن_ثانٍ_لم_يشهد_كتابته()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        // «العملية الأولى»: مثيل مخزن يؤسّس ثم يُنسى — كما تفعل حاوية الترحيل.
        PostgresCompanySetupStore writer = NewStore();
        FoundedCompany founded = Found(company, "مؤسسة نخيل الشرقية للتجارة والمقاولات", 2);

        Assert.True(
            await writer.TryFoundAsync(founded, TestContext.Current.CancellationToken),
            "التأسيس الأول يجب أن يُقبل.");

        // «العملية الثانية»: مثيل جديد تماماً، لا يشترك مع الأول في بايت واحد من الحالة.
        PostgresCompanySetupStore reader = NewStore();
        FoundedCompany? read = await reader.FindAsync(new TenantId(company), TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal("مؤسسة نخيل الشرقية للتجارة والمقاولات", read.NameAr);
        Assert.Equal(2, read.DisplayScale.Places);
        Assert.Equal("cc.001", read.CostCenters.Default.Value);
        Assert.True(read.CostCenters.DefaultCenter.IsActive);

        CoreTestEnvironment.Note(
            "قُرئ من مخزن ثانٍ: «" + read.NameAr + "» · المقياس " + read.DisplayScale
            + " · الافتراضي " + read.CostCenters.Default);
    }

    [Fact]
    public async Task التأسيس_الثاني_لا_ينجح_مرّتين_ولا_يستبدل_المقياس()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        PostgresCompanySetupStore store = NewStore();

        Assert.True(await store.TryFoundAsync(
            Found(company, "منشأة أولى", 2), TestContext.Current.CancellationToken));

        // حمولة مختلفة تماماً — وبالأخصّ مقياسٌ آخر. «مؤسَّسة من قبل» جوابٌ لا عطل.
        Assert.False(
            await store.TryFoundAsync(Found(company, "اسم آخر تماماً", 4), TestContext.Current.CancellationToken),
            "التأسيس الثاني يجب أن يُرفض ذرّياً لا أن يستبدل.");

        FoundedCompany? read = await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal("منشأة أولى", read.NameAr);
        Assert.Equal(2, read.DisplayScale.Places);
    }

    [Fact]
    public async Task مراكز_التكلفة_والترجمات_تُستبدَل_ثم_تُقرأ_كما_كُتبت()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        PostgresCompanySetupStore store = NewStore();
        FoundedCompany founded = Found(company, "منشأة بفروع", 2);
        Assert.True(await store.TryFoundAsync(founded, TestContext.Current.CancellationToken));

        Result<CostCenterRegister> added = founded.CostCenters.Add(
            "فرع الدمام",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "Dammam Branch", ["ur"] = "دمام برانچ" });
        Assert.True(added.IsSuccess, string.Join(" | ", added.Errors.Select(static e => e.ToString())));

        Result<CostCenterRegister> suspended = added.Value.Suspend(new CostCenterCode("cc.002"), "أُغلق الفرع نهاية الربع الثاني");
        Assert.True(suspended.IsSuccess, string.Join(" | ", suspended.Errors.Select(static e => e.ToString())));

        Assert.True(await store.TryReplaceCostCentersAsync(
            new TenantId(company), suspended.Value, TestContext.Current.CancellationToken));

        FoundedCompany? read = await NewStore().FindAsync(new TenantId(company), TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(2, read.CostCenters.Count);
        CostCenter branch = read.CostCenters.Find(new CostCenterCode("cc.002"))!;
        Assert.False(branch.IsActive);
        Assert.Equal("أُغلق الفرع نهاية الربع الثاني", branch.SuspensionReason);

        // والترجمات صفوف: لغتان على كيان واحد، وإضافةُ ثالثة إدخالُ صفّ لا هجرةُ مخطّط.
        Assert.Equal("Dammam Branch", branch.NameIn("en"));
        Assert.Equal("دمام برانچ", branch.NameIn("ur"));
        Assert.Equal("فرع الدمام", branch.NameIn("hi"));

        long rows = await CoreTestEnvironment.CountAsync(
            $"select count(*) from core.name_translation where company_id = '{company:D}'");
        Assert.Equal(2, rows);
        CoreTestEnvironment.Note("صفوف الترجمة لهذه المنشأة: " + rows.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · التحميل لا يلتفّ على الثابتة — صفٌّ مخالف يُرفض، لا يُرحَّب به
    //
    //     وكل حالة هنا تزرع مخالفةً **حقيقية** بدور المالك ثم تقرأ: حارسٌ يمسح
    //     مجموعةً لا تستطيع بنيتها أن تحوي مخالفة يمرّ ولا يُثبت شيئاً.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task صفٌّ_بافتراضيٍّ_موقوف_لا_يُنتج_منشأة()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        PostgresCompanySetupStore store = NewStore();
        Assert.True(await store.TryFoundAsync(
            Found(company, "منشأة سليمة", 2), TestContext.Current.CancellationToken));

        // تُقرأ سليمةً أولاً: لو كانت تسقط قبل الزرع لما أثبت السقوطُ بعده شيئاً.
        Assert.NotNull(await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        // الزرع بدور المالك: تعديلٌ يدوي في الثانية صباحاً، بابٌ لا يمرّ بأي نوع في الشجرة.
        await CoreTestEnvironment.OwnerAsync(
            $"""
            update core.cost_center
               set state = 'suspended', suspension_reason = 'زُرع عمداً لإثبات أن التحميل يرفض'
             where company_id = '{company:D}' and code = 'cc.001'
            """);

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        Assert.Contains("cc.001", refused.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رُفض التحميل: " + refused.Message);
    }

    [Fact]
    public async Task صفٌّ_بافتراضيٍّ_غير_موجود_لا_يُنتج_منشأة()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        PostgresCompanySetupStore store = NewStore();
        Assert.True(await store.TryFoundAsync(
            Found(company, "منشأة سليمة", 3), TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        await CoreTestEnvironment.OwnerAsync(
            $"update core.company_setup set default_cost_center = 'cc.999' where company_id = '{company:D}'");

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        Assert.Contains("cc.999", refused.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رُفض التحميل: " + refused.Message);
    }

    [Fact]
    public async Task صفوفٌ_بلا_مركز_واحد_لا_تُنتج_منشأة()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        PostgresCompanySetupStore store = NewStore();
        Assert.True(await store.TryFoundAsync(
            Found(company, "منشأة سليمة", 0), TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        // الحذف بدور المالك — ودور التطبيق لا يملكه أصلاً (انظر الحالة أدناه).
        await CoreTestEnvironment.OwnerAsync($"delete from core.cost_center where company_id = '{company:D}'");

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        Assert.Contains("فارغ", refused.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رُفض التحميل: " + refused.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · وما لا يستطيعه دور التطبيق — الرفض من PostgreSQL قبل أي منطق
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task دور_التطبيق_لا_يحذف_مركز_تكلفة()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        Assert.True(await NewStore().TryFoundAsync(
            Found(company, "منشأة لإثبات الصلاحيات", 2), TestContext.Current.CancellationToken));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.ApplicationAsync(
                $"delete from core.cost_center where company_id = '{company:D}'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
        CoreTestEnvironment.Note("رفض PostgreSQL بالرمز " + refused.SqlState + ": " + refused.MessageText);

        // والصفّ باقٍ: رفضٌ لا يترك أثراً هو رفضٌ فعلي لا رسالة.
        Assert.Equal(
            1,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.cost_center where company_id = '{company:D}'"));
    }

    [Fact]
    public async Task دور_التطبيق_لا_يملك_DDL_ولا_يُسقط_مشغّل_ثبات_المقياس()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.ApplicationAsync(
                "drop trigger trg_company_setup_immutable on core.company_setup"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
        CoreTestEnvironment.Note("رفض PostgreSQL بالرمز " + refused.SqlState + ": " + refused.MessageText);

        // والمشغّل ما زال حيّاً — مقروءاً من pg_trigger لا من ملفّ هجرة.
        Assert.Equal(
            1,
            await CoreTestEnvironment.CountAsync(
                "select count(*) from pg_trigger where not tgisinternal and tgname = 'trg_company_setup_immutable'"));
    }

    [Fact]
    public async Task مقياس_العرض_لا_يتغيّر_ولو_كان_الفاعل_هو_المالك()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        Assert.True(await NewStore().TryFoundAsync(
            Found(company, "منشأة بخانتين", 2), TestContext.Current.CancellationToken));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await CoreTestEnvironment.OwnerAsync(
                $"update core.company_setup set decimal_places = 4 where company_id = '{company:D}'"));

        Assert.Contains("DISPLAY_SCALE_IMMUTABLE", refused.MessageText, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رفض المشغّل: " + refused.MessageText);

        // وأن الحارس ليس ضامراً: التحديث الذي **لا** يمسّ المقياس يمرّ.
        Assert.Equal(
            1,
            await CoreTestEnvironment.OwnerAsync(
                $"update core.company_setup set name_ar = name_ar where company_id = '{company:D}'"));

        Assert.Equal(
            1,
            await CoreTestEnvironment.CountAsync(
                $"select count(*) from core.company_setup where company_id = '{company:D}' and decimal_places = 2"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٤ · ملفّ القدرات كذلك: يُحفظ ويُقرأ، ويُطابَق بالمصفوفة عند كل قراءة
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ملفّ_القدرات_يُقرأ_من_مخزن_ثانٍ_بالقرار_نفسه()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        CapabilityProfileDraft draft = new(new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
        {
            ["sales.invoice"] = new DocumentProfileDraft(
                new Dictionary<string, bool>(StringComparer.Ordinal) { ["advance"] = true },
                new Dictionary<string, string>(StringComparer.Ordinal)),
        });

        Result<ValidatedCapabilityProfile> built =
            ValidatedCapabilityProfile.Create(draft, EmbeddedPostingEventDirectory.Default);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Errors.Select(static e => e.ToString())));

        PostgresCapabilityProfileStore writer = NewProfileStore();
        await writer.SaveAsync(new TenantId(company), built.Value, TestContext.Current.CancellationToken);

        ValidatedCapabilityProfile? read = await NewProfileStore()
            .FindAsync(new TenantId(company), TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(
            built.Value.Shapes.Single().Fields,
            read.Shapes.Single().Fields);
        Assert.True(read.IsEnabled(new DocumentTypeCode("sales.invoice"), new CapabilityCode("advance")));

        CoreTestEnvironment.Note("حقول الشكل بعد القراءة: " + string.Join(" · ", read.Shapes.Single().Fields));
    }

    [Fact]
    public async Task ملفٌّ_مخزَّن_بقدرةٍ_لا_يعرفها_الكتالوج_يُرفض_عند_التحميل_ولا_يُقرأ_صامتاً()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        Guid company = CoreTestEnvironment.NewCompany();

        await CoreTestEnvironment.OwnerAsync(
            $"""
            insert into core.capability_profile_document (company_id, document_type)
            values ('{company:D}', 'sales.invoice');
            insert into core.capability_profile_capability (company_id, document_type, capability, enabled)
            values ('{company:D}', 'sales.invoice', 'قدرة.مخترعة', true);
            """);

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await NewProfileStore().FindAsync(new TenantId(company), TestContext.Current.CancellationToken));

        Assert.Contains("قدرة.مخترعة", refused.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رُفض التحميل: " + refused.Message);
    }

    private static PostgresCompanySetupStore NewStore()
        => new PostgresCompanySetupStore(CoreTestEnvironment.Options, TimeProvider.System);

    private static PostgresCapabilityProfileStore NewProfileStore()
        => new PostgresCapabilityProfileStore(CoreTestEnvironment.Options, EmbeddedPostingEventDirectory.Default);

    private static FoundedCompany Found(Guid company, string nameAr, int places)
    {
        Result<FoundedCompany> founded = FoundedCompany.Found(
            new TenantId(company),
            new CompanySetupDraft(nameAr, null, CostCenterPlan.One, null, null, places));

        Assert.True(founded.IsSuccess, string.Join(" | ", founded.Errors.Select(static e => e.ToString())));
        return founded.Value;
    }
}
