using System.Globalization;
using Babel.Contracts.Parameters;
using Babel.Core.Parameters;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// <b>المعامِل إصدارٌ مؤرَّخُ السريان لمجموعةٍ كاملة، بحالة اعتمادٍ ثلاثية.</b>
/// <para>
/// وكلُّ إثباتٍ هنا على PostgreSQL <b>حقيقية</b>، ودورِ تطبيقٍ غير مالك: نصفُ ما يُثبَت
/// لا يقع إلّا هناك — قيدُ «افتراضُ المنصّة لا يحمل معتمِداً»، وحارسُ «النسبة كسرٌ لا
/// مئوية» في المخطّط، وذرّيةُ الفهرس الفريد، ورفضُ التعديل بالمشغّل ولو كان الفاعل هو
/// المالك. اختبارٌ بمخزن ذاكرة يمرّ وكلٌّ منها مكسور.
/// </para>
/// </summary>
public sealed class ParameterVersionsTests
{
    private static readonly UserId Actor = new(new Guid("c0de0000-0000-4000-8000-00000000000a"));
    private static readonly DateTimeOffset At = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · الافتراض المشحون يعمل — وهو موسومٌ «غير مُعتمَد» ولا يحمل اسم إنسان
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task افتراضُ_المنصّة_يُقرأ_بلا_إيداعٍ_ويحمل_حالة_غير_مُعتمَد_ولا_يحمل_معتمِداً()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        IParameterSource source = provider.GetRequiredService<IParameterSource>();

        Result<ParameterSnapshot> read = await source.ResolveAsync(
            tenant, ParameterCatalogue.ValueAddedTax, new DateOnly(2026, 5, 1), TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess, Because(read));
        ParameterSnapshot snapshot = read.Value;

        Assert.Equal(ParameterScope.Platform, snapshot.Scope);
        Assert.Equal(ParameterApproval.PlatformDefault, snapshot.Approval);
        Assert.False(ParameterApprovalInfo.CarriesAHumanApprover(snapshot.Approval));

        // ‏**القيمة تُؤخذ من الافتراض المشحون ولا تُخترع هنا** — والاختبار لا يكتب رقماً:
        // يقارن ما قُرئ من القاعدة بما في ملفّ البيانات المضمَّن.
        decimal shipped = PlatformDefaults.All
            .Single(version => version.SetCode == ParameterCatalogue.ValueAddedTax)
            .Values.Single(value => value.Key == ParameterCatalogue.ValueAddedTaxStandardRate).Value;

        Assert.Equal(shipped, snapshot.Find(ParameterCatalogue.ValueAddedTaxStandardRate));
        CoreTestEnvironment.Note("النسبة المشحونة كما قُرئت من القاعدة: " + Number(shipped));

        // ولا اسم إنسانٍ على صفّ المنصّة — وهو نصّ HrRows.cs عند ApprovedBy حرفاً.
        long named = await CoreTestEnvironment.CountAsync(
            "select count(*) from core.parameter_version where scope = 'platform' and length(btrim(approved_by)) > 0");
        Assert.Equal(0, named);

        // ولا صفّ مشحون بتوقيع محاسب — أبداً.
        long signed = await CoreTestEnvironment.CountAsync(
            "select count(*) from core.parameter_version where scope = 'platform' and approval = 'auditor_signed'");
        Assert.Equal(0, signed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · تجاوزُ المستأجر يسري من تاريخه — وما قبله يبقى على الافتراض
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task تجاوزُ_المستأجر_يسري_من_تاريخه_ولا_يمسّ_ما_قبله()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();
        IParameterSource source = provider.GetRequiredService<IParameterSource>();

        Result<ParameterVersionView> deposited = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 0.10m), TestContext.Current.CancellationToken);

        Assert.True(deposited.IsSuccess, Because(deposited));

        Result<ParameterSnapshot> before = await source.ResolveAsync(
            tenant, ParameterCatalogue.ValueAddedTax, new DateOnly(2026, 5, 31), TestContext.Current.CancellationToken);
        Result<ParameterSnapshot> after = await source.ResolveAsync(
            tenant, ParameterCatalogue.ValueAddedTax, new DateOnly(2026, 6, 1), TestContext.Current.CancellationToken);

        Assert.True(before.IsSuccess && after.IsSuccess, Because(before) + Because(after));
        Assert.Equal(ParameterScope.Platform, before.Value.Scope);
        Assert.Equal(ParameterScope.Tenant, after.Value.Scope);
        Assert.Equal(0.10m, after.Value.Find(ParameterCatalogue.ValueAddedTaxStandardRate));
        Assert.NotEqual(before.Value.VersionId, after.Value.VersionId);

        CoreTestEnvironment.Note(
            "قبل السريان: " + ParameterApprovalInfo.TokenOf(before.Value.Scope)
            + " · بعده: " + ParameterApprovalInfo.TokenOf(after.Value.Scope));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · الحارس الذي يمنع تضاعف الوعاء خمس عشرة مرّة — في الخدمة وفي المخطّط
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task نسبةٌ_كُتبت_مئويةً_تُرفض_باسمها_في_الخدمة_وبالقيد_في_المخطّط()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();

        Result<ParameterVersionView> refused = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 15m), TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Equal("core.parameter_rate_looks_like_a_percentage", refused.Errors[0].Code);
        CoreTestEnvironment.Note("رفضُ الخدمة: " + refused.Errors[0].Code);

        // ‏**والشاهد الموجب على القيد نفسه:** يُحقن الصفّ بدور **المالك** متجاوزاً
        // الخدمة كلَّها. فلو كان الحارس في الشيفرة وحدها لمرّ هذا الإدراج.
        Guid version = Guid.CreateVersion7();
        await CoreTestEnvironment.OwnerAsync(
            "insert into core.parameter_version (version_id, tenant_id, set_code, scope, effective_from, approval, "
            + "approved_by, approved_on, source_ref, deposited_at) values ('"
            + version.ToString("D", CultureInfo.InvariantCulture) + "', '"
            + tenant.Value.ToString("D", CultureInfo.InvariantCulture)
            + "', 'tax.value_added', 'tenant', date '2026-07-01', 'tenant_approved', 'اختبار', date '2026-07-01', "
            + "'شاهدٌ موجب على قيد النسبة', now())");

        PostgresException blocked = await Assert.ThrowsAsync<PostgresException>(() =>
            CoreTestEnvironment.OwnerAsync(
                "insert into core.parameter_value (version_id, key, kind, value) values ('"
                + version.ToString("D", CultureInfo.InvariantCulture)
                + "', 'standard_rate', 'rate', 15)"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, blocked.SqlState);
        Assert.Contains("ck_parameter_value_rate_is_a_fraction", blocked.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رفضُ المخطّط: " + blocked.SqlState + " · " + blocked.ConstraintName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٤ · الإيداع يرفض التكرار على (المستوى · المفتاح · تاريخ السريان)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task إيداعٌ_ثانٍ_بالمستوى_والمجموعة_وتاريخ_السريان_نفسها_يُرفض()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();

        Result<ParameterVersionView> first = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 0.10m), TestContext.Current.CancellationToken);
        Result<ParameterVersionView> second = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 0.12m), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess, Because(first));
        Assert.True(second.IsFailure);
        Assert.Equal("core.parameter_version_duplicate", second.Errors[0].Code);
        CoreTestEnvironment.Note("رفضُ التكرار: " + second.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٥ · المجموعة تُودَع كاملةً — والإيداع الجزئي يُرفض باسمه
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task إيداعٌ_بمفتاحٍ_ليس_من_المجموعة_يُرفض_ويسمّي_الزائد()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();

        ParameterVersionDraft draft = new(
            ParameterCatalogue.ValueAddedTax,
            new DateOnly(2026, 6, 1),
            ParameterApproval.TenantApproved,
            "مديرة المالية",
            new DateOnly(2026, 5, 20),
            "قرار مجلس الإدارة رقم 12",
            new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [ParameterCatalogue.ValueAddedTaxStandardRate] = 0.10m,
                ["reduced_rate"] = 0.05m,
            });

        Result<ParameterVersionView> refused =
            await settings.DepositAsync(tenant, Actor, draft, TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Equal("core.parameter_keys_incomplete", refused.Errors[0].Code);
        Assert.Contains("reduced_rate", refused.Errors[0].MessageAr, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رفضُ المفتاح الزائد: " + refused.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٦ · نطاق المستأجر مفروضٌ في الاستعلام — لا في مِصفاةٍ بعده
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task منشأةٌ_لا_ترى_إصدارَ_غيرها_ولا_استعمالَه()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId mine = new(CoreTestEnvironment.NewCompany());
        TenantId other = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();
        IParameterUsageRecorder recorder = provider.GetRequiredService<IParameterUsageRecorder>();

        Result<ParameterVersionView> theirs = await settings.DepositAsync(
            other, Actor, Draft(new DateOnly(2026, 6, 1), 0.05m), TestContext.Current.CancellationToken);
        Assert.True(theirs.IsSuccess, Because(theirs));

        await recorder.RecordAsync(
            other,
            new ParameterUsage(theirs.Value.Id, BabelModule.Purchasing, "SUPPLIER_BILL", Guid.CreateVersion7(), new DateOnly(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        Result<IReadOnlyList<ParameterVersionView>> listed =
            await settings.ListAsync(mine, Actor, TestContext.Current.CancellationToken);
        Result<IReadOnlyList<ParameterReviewView>> review =
            await settings.ReviewAsync(mine, Actor, TestContext.Current.CancellationToken);

        Assert.True(listed.IsSuccess && review.IsSuccess, Because(listed) + Because(review));
        Assert.DoesNotContain(listed.Value, version => version.Id == theirs.Value.Id);
        Assert.DoesNotContain(review.Value, entry => entry.Version.Id == theirs.Value.Id);
        Assert.All(review.Value, entry => Assert.Empty(entry.Usages));

        CoreTestEnvironment.Note(
            "ما تراه المنشأة الأولى: " + listed.Value.Count.ToString(CultureInfo.InvariantCulture)
            + " إصداراً، ولا واحد منها إصدارُ الثانية.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٧ · قائمةُ المراجعة: كلُّ إصدارٍ غير موقَّع ومَن استعمله — والموقَّع يخرج منها
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task قائمةُ_المراجعة_تجمع_الإصدارَ_غيرَ_الموقَّع_بمستنداته_وتُخرج_الموقَّع()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();
        IParameterUsageRecorder recorder = provider.GetRequiredService<IParameterUsageRecorder>();

        Result<ParameterVersionView> unsigned = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 0.10m), TestContext.Current.CancellationToken);
        Assert.True(unsigned.IsSuccess, Because(unsigned));

        ParameterVersionDraft signedDraft = Draft(new DateOnly(2026, 7, 1), 0.11m) with
        {
            Approval = ParameterApproval.AuditorSigned,
            ApprovedBy = "المحاسب القانوني — مكتب مرخَّص",
        };

        Result<ParameterVersionView> signed =
            await settings.DepositAsync(tenant, Actor, signedDraft, TestContext.Current.CancellationToken);
        Assert.True(signed.IsSuccess, Because(signed));

        Guid document = Guid.CreateVersion7();
        ParameterUsage usage = new(
            unsigned.Value.Id, BabelModule.Purchasing, "SUPPLIER_BILL", document, new DateOnly(2026, 6, 9));

        await recorder.RecordAsync(tenant, usage, TestContext.Current.CancellationToken);

        // ‏**آمنُ التكرار**: الترحيل الثاني للمستند نفسه لا يكتب صفّاً ثانياً.
        await recorder.RecordAsync(tenant, usage, TestContext.Current.CancellationToken);

        Result<IReadOnlyList<ParameterReviewView>> review =
            await settings.ReviewAsync(tenant, Actor, TestContext.Current.CancellationToken);

        Assert.True(review.IsSuccess, Because(review));
        Assert.DoesNotContain(review.Value, entry => entry.Version.Id == signed.Value.Id);

        ParameterReviewView entryForUnsigned = Assert.Single(
            review.Value, entry => entry.Version.Id == unsigned.Value.Id);
        ParameterUsageView only = Assert.Single(entryForUnsigned.Usages);
        Assert.Equal(document, only.DocumentId);
        Assert.Equal(BabelModule.Purchasing, only.Module);

        // وافتراضُ المنصّة غير موقَّع كذلك، فهو في القائمة ولم يستعمله مستند.
        Assert.Contains(review.Value, entry => entry.Version.Scope == ParameterScope.Platform);

        CoreTestEnvironment.Note(
            "صفوفُ المراجعة: " + review.Value.Count.ToString(CultureInfo.InvariantCulture)
            + " · مستنداتُ الإصدار غير الموقَّع: "
            + entryForUnsigned.Usages.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٨ · يُضاف ولا يُعدَّل — ولو كان الفاعل هو المالك
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task تعديلُ_قيمةِ_إصدارٍ_مضى_مرفوضٌ_بالمشغّل_ولو_بدور_المالك()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();

        Result<ParameterVersionView> deposited = await settings.DepositAsync(
            tenant, Actor, Draft(new DateOnly(2026, 6, 1), 0.10m), TestContext.Current.CancellationToken);
        Assert.True(deposited.IsSuccess, Because(deposited));

        string id = deposited.Value.Id.ToString("D", CultureInfo.InvariantCulture);

        PostgresException update = await Assert.ThrowsAsync<PostgresException>(() =>
            CoreTestEnvironment.OwnerAsync(
                "update core.parameter_value set value = 0.20 where version_id = '" + id + "'"));
        PostgresException delete = await Assert.ThrowsAsync<PostgresException>(() =>
            CoreTestEnvironment.OwnerAsync("delete from core.parameter_version where version_id = '" + id + "'"));

        Assert.Contains("APPEND_ONLY_VIOLATION", update.Message, StringComparison.Ordinal);
        Assert.Contains("APPEND_ONLY_VIOLATION", delete.Message, StringComparison.Ordinal);
        CoreTestEnvironment.Note("رفضُ التعديل والحذف بدور المالك: APPEND_ONLY_VIOLATION في الاثنين.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٩ · اللقطة تُكتب وتُقرأ بلا فقدان — وهي ما يُخزَّن على المستند
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task اللقطةُ_تعبر_الشكلَ_القانوني_ذهاباً_وإياباً_بلا_فقدان()
    {
        await CoreTestEnvironment.EnsureAsync(TestContext.Current.CancellationToken);
        TenantId tenant = new(CoreTestEnvironment.NewCompany());

        await using ServiceProvider provider = NewComposition();
        ParameterSettingsService settings = provider.GetRequiredService<ParameterSettingsService>();
        IParameterSource source = provider.GetRequiredService<IParameterSource>();

        ParameterVersionDraft draft = Draft(new DateOnly(2026, 6, 1), 0.07125m) with
        {
            SourceRef = "مرجعٌ فيه فاصلٌ رأسي | وفاصلةٌ منقوطة ؛ وعلامةُ يساوٍ = وسطرٌ جديد",
        };

        Assert.True((await settings.DepositAsync(tenant, Actor, draft, TestContext.Current.CancellationToken)).IsSuccess);

        Result<ParameterSnapshot> read = await source.ResolveAsync(
            tenant, ParameterCatalogue.ValueAddedTax, new DateOnly(2026, 6, 1), TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess, Because(read));

        string canonical = read.Value.Canonical();
        ParameterSnapshot back = ParameterSnapshot.Parse(canonical);

        Assert.Equal(read.Value.VersionId, back.VersionId);
        Assert.Equal(read.Value.SourceRef, back.SourceRef);
        Assert.Equal(read.Value.EffectiveFrom, back.EffectiveFrom);
        Assert.Equal(read.Value.Approval, back.Approval);
        Assert.Equal(
            read.Value.Find(ParameterCatalogue.ValueAddedTaxStandardRate),
            back.Find(ParameterCatalogue.ValueAddedTaxStandardRate));

        CoreTestEnvironment.Note("طول الشكل القانوني: "
            + canonical.Length.ToString(CultureInfo.InvariantCulture) + " محرفاً.");
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static ParameterVersionDraft Draft(DateOnly effectiveFrom, decimal rate) => new(
        ParameterCatalogue.ValueAddedTax,
        effectiveFrom,
        ParameterApproval.TenantApproved,
        "مديرة المالية",
        effectiveFrom.AddDays(-10),
        "قرار مجلس الإدارة رقم 12 — نسخةٌ محفوظة في المرفقات",
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [ParameterCatalogue.ValueAddedTaxStandardRate] = rate,
        });

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Because<T>(Result<T> result)
        => result.IsSuccess ? string.Empty : string.Join(" · ", result.Errors.Select(static error => error.ToString()));

    private static ServiceProvider NewComposition()
    {
        ServiceCollection services = new();
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = CoreTestEnvironment.Options.AppConnectionString;
            options.OwnerConnectionString = CoreTestEnvironment.Options.OwnerConnectionString;
            options.AppRole = CoreTestEnvironment.Options.AppRole;
        });

        services.AddSingleton<TimeProvider>(new FixedTimeProvider(At));
        return services.BuildServiceProvider();
    }
}
