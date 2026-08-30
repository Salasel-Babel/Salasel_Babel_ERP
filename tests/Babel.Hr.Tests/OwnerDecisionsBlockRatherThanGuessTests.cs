using Babel.Hr.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// <b>ما لم يحسمه المالك يُرفض صراحةً، ولا يُخمَّن ولا يُملأ بافتراضي.</b>
/// <para>
/// وهذا الملفّ هو الطرف المُثبَت من القيد الحاكم في هذا التسليم: الوحدة تُسلَّم وجدولُ
/// النِّسَب <b>فارغاً</b>، ومسيّرٌ لفترةٍ لا يغطّيها صفٌّ معتمد يُرفض برمزٍ مستقرّ
/// ورسالةٍ <b>تسمّي البند المعلَّق</b> — لا بصفرٍ صامت ولا بنسبةٍ «مؤقّتة».
/// </para>
/// </summary>
[Collection("hr")]
public sealed class OwnerDecisionsBlockRatherThanGuessTests
{
    private static readonly DateOnly PeriodStart = new(2026, 4, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 4, 30);

    /// <summary>
    /// <b>الرفض الحاكم:</b> منشأةٌ لا صفَّ نِسَبٍ فيها ⇒ لا مسيّر، والرمز يسمّي البند.
    /// </summary>
    [Fact]
    public async Task مسيّرٌ_بلا_صفّ_نِسَبٍ_معتمد_يُرفض_برمزٍ_يسمّي_البند_المعلَّق()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);

        // منشأةٌ معزولة **لا يُودَع فيها صفّ نِسَبٍ أبداً** — وعزلُها هو ما يمنع هذا
        // الإثبات من أن يمرّ أو يسقط بترتيب التشغيل بدل بنائه.
        TenantId tenant = HrTestEnvironment.EmptyRatesTenant;
        string suffix = Scenario.Suffix();
        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);

        await Scenario.EmployeeAsync(
            harness, tenant, "class-" + suffix, component,
            "1000000000", "SA0000000000000000000000", "موظف بلا نِسَب",
            6_000.0000m, token).ConfigureAwait(true);

        Result<PayrollRunView> run = await harness.Runs
            .DraftAsync(
                tenant, Harness.Actor,
                new PayrollRunDraft("RUN-EMPTY-" + suffix, "2026-04", PeriodStart, PeriodEnd),
                token)
            .ConfigureAwait(true);

        Assert.True(run.IsFailure, "مسيّرٌ بُني بلا صفّ نِسَب — وهذا هو العطل الذي لا يظهر في ميزان مراجعة.");
        Assert.Equal("hr.payroll_settings_missing", run.Errors[0].Code);

        // والرسالة تسمّي البند المعلَّق بالعربية والإنجليزية — لا «خطأ في الإعدادات».
        Assert.Contains("م-14", run.Errors[0].MessageAr, StringComparison.Ordinal);
        Assert.Contains("م-14", run.Errors[0].MessageEn, StringComparison.Ordinal);
        Assert.Contains("verification-debt", run.Errors[0].MessageEn, StringComparison.Ordinal);

        // ولا مسيّر كُتب: الرفض يترك المنشأة على حالها لا نصفَ مسيّر.
        Result<PayrollRunView> reread = await harness.Runs
            .GetAsync(tenant, Harness.Actor, Guid.CreateVersion7(), token).ConfigureAwait(true);
        Assert.True(reread.IsFailure);
    }

    /// <summary>
    /// <b>ولا صفر صامت:</b> الجدول الفارغ يُقرأ قائمةً فارغة — وهي جوابٌ صحيح — ولا
    /// يُخترع فيه صفٌّ ضمني عند أول قراءة.
    /// </summary>
    [Fact]
    public async Task جدولُ_النِّسَب_يُقرأ_فارغاً_ولا_يُخترع_فيه_صفّ()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);

        Result<IReadOnlyList<PayrollSettingsView>> versions = await harness.Settings
            .ListAsync(HrTestEnvironment.EmptyRatesTenant, Harness.Actor, Harness.Currency, token)
            .ConfigureAwait(true);

        Assert.True(versions.IsSuccess, Harness.Reason(versions));
        Assert.Empty(versions.Value);
    }

    /// <summary>
    /// <b>وطرف الخزينة إلزامي على كل مستند دفع.</b> سطر التسوية معلَنٌ
    /// <c>subledger: "resolved"</c> والمحرك يطويه إلى <c>none</c> ثم يبحث عن الواقعة
    /// <c>subledger.none</c>؛ وحساب التسوية الافتراضي حسابٌ ضابط. فالرفض هنا يسبق
    /// المحرك ويقول السبب، بدل <c>ledger.posting.missing_subledger</c> بعد كتابة صفّ محاولة.
    /// </summary>
    [Fact]
    public async Task سندُ_دفعٍ_بلا_طرف_خزينة_يُرفض_قبل_أن_يبلغ_المحرك()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        string suffix = Scenario.Suffix();

        Result<SocialInsurancePaymentView> payment = await harness.SocialInsurance
            .DraftAsync(
                HrTestEnvironment.Tenant,
                Harness.Actor,
                new SocialInsurancePaymentDraft(
                    "GOSI-" + suffix, "2026-03", new DateOnly(2026, 4, 5),
                    Money.Of(1_500.0000m, Harness.Currency), "bank", TreasuryPartyId: string.Empty),
                token)
            .ConfigureAwait(true);

        Assert.True(payment.IsFailure, "سندُ دفعٍ بلا طرف خزينة قُبل — والمحرك كان سيرفضه بعد كتابة صفّ محاولة.");
        Assert.Equal("hr.treasury_party_missing", payment.Errors[0].Code);
    }

    /// <summary>
    /// <b>وطريقة تسوية لا تعرفها خريطة الأدوار تُرفض باسمها.</b> ومؤهّلٌ مجهول يقع على
    /// المؤهّل الافتراضي فيختار حساباً آخر <b>بصمت</b> — وذلك صنف العطل الذي لا يُظهره
    /// توازن.
    /// <para>
    /// والمؤهّل <c>in_transit</c> مرفوضٌ هنا <b>عمداً</b> رغم وجوده في الخريطة: قبولُه
    /// يفترض جواب سؤالٍ مفتوح على المالك عن لحظة وقوع قيد صرف الرواتب.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("in_transit")]
    [InlineData("card_clearing")]
    [InlineData("wire")]
    public async Task طريقةُ_تسويةٍ_خارج_المجموعة_المقبولة_تُرفض_باسمها(string method)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        string suffix = Scenario.Suffix();

        Result<SocialInsurancePaymentView> payment = await harness.SocialInsurance
            .DraftAsync(
                HrTestEnvironment.Tenant,
                Harness.Actor,
                new SocialInsurancePaymentDraft(
                    "GOSI-" + method + "-" + suffix, "2026-03", new DateOnly(2026, 4, 5),
                    Money.Of(1_500.0000m, Harness.Currency), method, "treasury.main"),
                token)
            .ConfigureAwait(true);

        Assert.True(payment.IsFailure, "طريقةُ تسويةٍ خارج المجموعة قُبلت — والمؤهّل المجهول يختار حساباً آخر بصمت.");
        Assert.Equal("hr.unknown_settlement_method", payment.Errors[0].Code);
    }

    /// <summary>
    /// <b>ومسيّرٌ ثانٍ للفترة الواحدة يُرفض في الخدمة لا في فهرس</b> — حتى يُجاب سؤال
    /// «هل يُسمح بأكثر من مسيّر مُرحَّل للفترة؟». وفهرسٌ اليوم يفترض جوابه في مفتاح على
    /// جدولٍ لا يُحذف منه شيء.
    /// </summary>
    [Fact]
    public async Task مسيّرٌ_ثانٍ_للفترة_يُرفض_في_الخدمة_ولا_فهرس_يفترض_الجواب()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.PeriodUniquenessTenant;
        string suffix = Scenario.Suffix();
        string classCode = "class-" + suffix;

        await harness.DepositTestRatesAsync(
            tenant, classCode, Scenario.TestEmployerRate, Scenario.TestEmployeeRate,
            floor: 0m, ceiling: 0m, effectiveFrom: new DateOnly(2026, 1, 1), cancellationToken: token)
            .ConfigureAwait(true);

        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);
        await Scenario.EmployeeAsync(
            harness, tenant, classCode, component,
            "2" + suffix.PadRight(9, '0')[..9], "SA9" + suffix.ToUpperInvariant() + "000000000000",
            "موظف الفترة المكرّرة", 3_000.0000m, token).ConfigureAwait(true);

        string period = "2026-05";
        DateOnly start = new(2026, 5, 1);
        DateOnly end = new(2026, 5, 31);

        Result<PayrollRunView> first = await harness.Runs
            .DraftAsync(tenant, Harness.Actor, new PayrollRunDraft("RUN-A-" + suffix, period, start, end), token)
            .ConfigureAwait(true);
        Assert.True(first.IsSuccess, Harness.Reason(first));

        Result<PayrollRunView> second = await harness.Runs
            .DraftAsync(tenant, Harness.Actor, new PayrollRunDraft("RUN-B-" + suffix, period, start, end), token)
            .ConfigureAwait(true);

        Assert.True(second.IsFailure);
        Assert.Equal("hr.period_already_has_a_run", second.Errors[0].Code);
        Assert.Contains(first.Value.Number, second.Errors[0].MessageAr, StringComparison.Ordinal);
    }
}
