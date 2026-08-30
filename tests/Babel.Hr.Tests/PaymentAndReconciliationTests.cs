using System.Globalization;
using Babel.Hr.Application;
using Babel.Hr.Subledger;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// الدورة الكاملة: استحقاقٌ ثم صرف ثم سدادُ تأمينات، ثم <b>مطابقة الدفتر المساعد
/// بنقطة ضبطه مستنداً بمستند</b>.
/// <para>
/// <b>وهذه المطابقة ممكنة أصلاً لأن الطرفين متساويا الحبيبيّة</b>: قيدٌ لكل قسيمة يعني
/// حركةً واحدة في نقطة الضبط لكل قسيمة وصفَّ محاولةٍ واحداً في جدول الوحدة لكل قسيمة.
/// ولو رُحِّل المسيّر قيداً واحداً لصار الطرفان بحبيبيّتين مختلفتين ولاستحال هذا الباب.
/// </para>
/// </summary>
[Collection("hr")]
public sealed class PaymentAndReconciliationTests
{
    private const string Period = "2026-08";

    private static readonly DateOnly PeriodStart = new(2026, 8, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 8, 31);

    [Fact]
    public async Task الدورةُ_الكاملة_تُطابَق_مستنداً_بمستند_ولا_تنشر_رصيداً_واحداً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.CycleTenant;
        string suffix = Scenario.Suffix();
        string classCode = "class-" + suffix;

        await harness.DepositTestRatesAsync(
            tenant, classCode, Scenario.TestEmployerRate, Scenario.TestEmployeeRate,
            floor: 0m, ceiling: 0m, effectiveFrom: new DateOnly(2026, 1, 1), cancellationToken: token)
            .ConfigureAwait(true);

        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);

        EmployeeView first = await Scenario.EmployeeAsync(
            harness, tenant, classCode, component,
            "3" + suffix.PadRight(9, '0')[..9] + "1", "SA31" + suffix.ToUpperInvariant() + "0000000001",
            "موظف الدورة الأول", 9_000.0000m, token).ConfigureAwait(true);

        EmployeeView second = await Scenario.EmployeeAsync(
            harness, tenant, classCode, component,
            "3" + suffix.PadRight(9, '0')[..9] + "2", "SA32" + suffix.ToUpperInvariant() + "0000000002",
            "موظف الدورة الثاني", 6_000.0000m, token).ConfigureAwait(true);

        // ── جزاءٌ معتمد على الأول، فيدخل قسيمته ويظهر في دفتره المساعد ──────────
        Result<EmployeeDeductionView> penalty = await harness.Register
            .RecordDeductionAsync(
                tenant, Harness.Actor,
                new EmployeeDeductionDraft(
                    first.Id, Period, "late_attendance", Money.Of(200.0000m, Harness.Currency),
                    "manager.under.test", new DateOnly(2026, 8, 20)),
                token)
            .ConfigureAwait(true);
        Assert.True(penalty.IsSuccess, Harness.Reason(penalty));

        Result<PayrollRunView> run = await harness.Runs
            .DraftAsync(
                tenant, Harness.Actor,
                new PayrollRunDraft("RUN-CYCLE-" + suffix, Period, PeriodStart, PeriodEnd), token)
            .ConfigureAwait(true);
        Assert.True(run.IsSuccess, Harness.Reason(run));

        // المتطابقة المعلَنة في المصفوفة، مفحوصةً على صفّ القسيمة نفسه.
        Result<IReadOnlyList<PayslipView>> slips = await harness.Runs
            .ListPayslipsAsync(tenant, Harness.Actor, run.Value.Id, token).ConfigureAwait(true);
        Assert.True(slips.IsSuccess, Harness.Reason(slips));
        Assert.Equal(2, slips.Value.Count);

        foreach (PayslipView slip in slips.Value)
        {
            Assert.Equal(
                slip.Amounts.GrossEntitlements.Amount
                - slip.Amounts.EmployeeSocialInsurance.Amount
                - slip.Amounts.AdvanceInstalment.Amount
                - slip.Amounts.Deductions.Amount,
                slip.Amounts.NetPayable.Amount);
        }

        // والجزاء دخل قسيمة صاحبه وحده.
        PayslipView charged = slips.Value.Single(s => string.Equals(s.EmployeeCode, first.Code, StringComparison.Ordinal));
        PayslipView clean = slips.Value.Single(s => string.Equals(s.EmployeeCode, second.Code, StringComparison.Ordinal));
        Assert.Equal(200.0000m, charged.Amounts.Deductions.Amount);
        Assert.Equal(0.0000m, clean.Amounts.Deductions.Amount);

        Result<IReadOnlyList<PayslipView>> accrued = await harness.Runs
            .PostAsync(tenant, Harness.Actor, run.Value.Id, token).ConfigureAwait(true);
        Assert.True(accrued.IsSuccess, Harness.Reason(accrued));

        // ── الصرف: سندٌ بسطرٍ لكل قسيمة، وقيدٌ لكل سطر، ومعه طرف الخزينة ────────
        Result<PayrollPaymentView> payment = await harness.Payments
            .DraftAsync(
                tenant, Harness.Actor,
                new PayrollPaymentDraft(
                    "PAY-" + suffix, run.Value.Id, new DateOnly(2026, 8, 31), "bank", "treasury.main"),
                token)
            .ConfigureAwait(true);
        Assert.True(payment.IsSuccess, Harness.Reason(payment));
        Assert.Equal(2, payment.Value.Lines.Count);

        Result<PayrollPaymentView> paid = await harness.Payments
            .PostAsync(tenant, Harness.Actor, payment.Value.Id, token).ConfigureAwait(true);
        Assert.True(paid.IsSuccess, Harness.Reason(paid));
        Assert.All(paid.Value.Lines, static line => Assert.NotNull(line.EntryId));
        Assert.Equal(2, paid.Value.Lines.Select(static line => line.EntryId).Distinct().Count());

        // ── سداد التأمينات: **قيدٌ واحد للفترة، وهو الوحيد الذي يجوز فيه ذلك** ──
        decimal accruedInsurance = run.Value.Amounts.EmployerSocialInsurance.Amount
                                   + run.Value.Amounts.EmployeeSocialInsurance.Amount;

        Result<SocialInsurancePaymentView> insurance = await harness.SocialInsurance
            .DraftAsync(
                tenant, Harness.Actor,
                new SocialInsurancePaymentDraft(
                    "GOSI-" + suffix, Period, new DateOnly(2026, 9, 5),
                    Money.Of(accruedInsurance, Harness.Currency), "bank", "treasury.main"),
                token)
            .ConfigureAwait(true);
        Assert.True(insurance.IsSuccess, Harness.Reason(insurance));

        Result<SocialInsurancePaymentView> settled = await harness.SocialInsurance
            .PostAsync(tenant, Harness.Actor, insurance.Value.Id, token).ConfigureAwait(true);
        Assert.True(settled.IsSuccess, Harness.Reason(settled));
        Assert.NotNull(settled.Value.EntryId);

        // وما استُحقّ في الفترة يُعرض إلى جانب المسدَّد **للمقارنة لا للإملاء**.
        Assert.Equal(accruedInsurance, settled.Value.AccruedForPeriod.Amount);

        // ولا سطرَ دفتر مساعد على قيده: سطره الأول على حساب الالتزام بلا دفتر مساعد.
        Assert.Empty(await PayrollGrainIsTheEmployeeTests
            .PartiesAsync([settled.Value.EntryId!.Value], token).ConfigureAwait(true));

        // ── المطابقة: مستنداً بمستند، بلا انحراف واحد ───────────────────────────
        Result<EmployeeReconciliationReport> report = await harness.Reconciliation
            .ReconcileAsync(tenant, Harness.Actor, new DateOnly(2026, 9, 30), token).ConfigureAwait(true);
        Assert.True(report.IsSuccess, Harness.Reason(report));

        Assert.True(
            report.Value.IsReconciled,
            "انحرافٌ في دفتر الموظف: " + string.Join(
                " | ",
                report.Value.Divergences.Select(static d =>
                    d.DocumentType + "/" + d.DocumentId + " " + d.ReasonCode + " "
                    + d.Divergence.Amount.ToString("0.0000", CultureInfo.InvariantCulture))));

        // ‏**واللافراغ من الطرف الآخر**: خمسة مستندات تطابق طرفاها — قسيمتان، وسطرا
        // صرف، وسندُ تأمينات أثره صفرٌ على دفتر الموظف بحكم بنائه. و«صفر انحراف» بلا
        // هذا العدد كان يحتمل «لم يُفحص شيء».
        Assert.Equal(5, report.Value.MatchedDocuments);
    }

    /// <summary>
    /// <b>مخالصة نهاية الخدمة بسيناريوهاتها الثلاثة</b> — والسيناريو <b>مُسمّى في
    /// الجواب</b> لا مستنتَجاً من فرق مبلغين عند القارئ.
    /// <para>
    /// والوحدة لا تقيس المخصص ولا المستحقّ: كلاهما يصل من معتمِد المستند ومعه مرجع
    /// أساسه. وما تحسبه هي رصيدُ المخصص من حركاته المُرحَّلة، ثم العجز والزيادة —
    /// اشتقاقٌ حسابي من رقمين.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1_000.0000, 1_000.0000, "exact", 0.0000, 0.0000)]
    [InlineData(1_000.0000, 1_500.0000, "short", 500.0000, 0.0000)]
    [InlineData(1_500.0000, 1_000.0000, "excess", 0.0000, 500.0000)]
    public async Task المخالصةُ_تُسمّي_سيناريوها_وتشتقّ_العجز_والزيادة_من_رصيد_المخصص(
        decimal provision, decimal due, string scenario, decimal shortfall, decimal excess)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.EndOfServiceTenant;
        string suffix = Scenario.Suffix();
        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);

        EmployeeView employee = await Scenario.EmployeeAsync(
            harness, tenant, "class-eos-" + suffix, component,
            "4" + suffix.PadRight(9, '0')[..9], "SA41" + suffix.ToUpperInvariant() + "0000000000",
            "موظف المخالصة " + suffix, 5_000.0000m, token).ConfigureAwait(true);

        // ── استحقاق المخصص: **مبلغٌ يُدخله معتمِد المستند ومعه مرجع أساسه** ─────
        Result<EndOfServiceProvisionView> accrual = await harness.EndOfService
            .DraftProvisionAsync(
                tenant, Harness.Actor,
                new EndOfServiceProvisionDraft(
                    "EOSP-" + suffix,
                    "2026-09",
                    new DateOnly(2026, 9, 30),
                    "TEST-ONLY — أساس قياس مختلق لهذا الاختبار وحده؛ وطريقة القياس النظامية غير محسومة.",
                    "accountant.under.test",
                    [new ProvisionShareDraft(employee.EmploymentId, Money.Of(provision, Harness.Currency))]),
                token)
            .ConfigureAwait(true);
        Assert.True(accrual.IsSuccess, Harness.Reason(accrual));

        Result<EndOfServiceProvisionView> postedAccrual = await harness.EndOfService
            .PostProvisionAsync(tenant, Harness.Actor, accrual.Value.Id, token).ConfigureAwait(true);
        Assert.True(postedAccrual.IsSuccess, Harness.Reason(postedAccrual));
        Assert.All(postedAccrual.Value.Movements, static movement => Assert.NotNull(movement.EntryId));

        // ── الإنهاء **مورداً فرعياً** — وهو ما يفتح المخالصة ────────────────────
        Result<EmployeeView> terminated = await harness.Employees
            .TerminateAsync(tenant, Harness.Actor, employee.Id, new DateOnly(2026, 10, 15), "resignation", token)
            .ConfigureAwait(true);
        Assert.True(terminated.IsSuccess, Harness.Reason(terminated));

        Result<EndOfServiceSettlementView> settlement = await harness.EndOfService
            .DraftSettlementAsync(
                tenant, Harness.Actor,
                new EndOfServiceSettlementDraft(
                    "EOSS-" + suffix,
                    employee.EmploymentId,
                    new DateOnly(2026, 10, 15),
                    Money.Of(due, Harness.Currency),
                    "TEST-ONLY — حسابُ مخالصةٍ مختلق؛ ومعادلة المكافأة النظامية غير متحقَّق منها.",
                    "bank",
                    "treasury.main"),
                token)
            .ConfigureAwait(true);
        Assert.True(settlement.IsSuccess, Harness.Reason(settlement));

        Assert.Equal(scenario, settlement.Value.ScenarioCode);
        Assert.Equal(provision, settlement.Value.ProvisionBalance.Amount);
        Assert.Equal(shortfall, settlement.Value.Shortfall.Amount);
        Assert.Equal(excess, settlement.Value.Excess.Amount);

        // والمتطابقة المعلَنة في المصفوفة: provision_utilised = amount_paid − shortfall + excess
        Assert.Equal(
            settlement.Value.AmountPaid.Amount - settlement.Value.Shortfall.Amount + settlement.Value.Excess.Amount,
            settlement.Value.ProvisionUtilised.Amount);

        Result<EndOfServiceSettlementView> posted = await harness.EndOfService
            .PostSettlementAsync(tenant, Harness.Actor, settlement.Value.Id, token).ConfigureAwait(true);
        Assert.True(posted.IsSuccess, Harness.Reason(posted));
        Assert.NotNull(posted.Value.EntryId);

        // والقيد يحمل طرف الموظف على سطر المخصص — وهو ما يجعل الرصيد قابلاً للمطابقة.
        HashSet<string> parties = await PayrollGrainIsTheEmployeeTests
            .PartiesAsync([posted.Value.EntryId!.Value], token).ConfigureAwait(true);
        Assert.Equal([employee.Code], [.. parties]);

        // وسطرُ التسوية يحمل طرف الخزينة **واقعةً نصّية** لا استدعاءً لوحدةٍ أخرى.
        Assert.Contains(
            "treasury.main",
            await TreasuryPartiesAsync(posted.Value.EntryId!.Value, token).ConfigureAwait(true),
            StringComparer.Ordinal);
    }

    /// <summary>أطراف السطور التي ليست على دفتر الموظف — ومنها طرف الخزينة.</summary>
    private static async Task<List<string>> TreasuryPartiesAsync(Guid entry, CancellationToken token)
    {
        List<string> parties = [];

        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select subledger_party_id
              from ledger.journal_line
             where entry_id = $1
               and subledger_kind <> 'employee'
               and subledger_party_id is not null
            """, connection);
        command.Parameters.AddWithValue(entry);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            parties.Add(reader.GetString(0));
        }

        return parties;
    }
}
