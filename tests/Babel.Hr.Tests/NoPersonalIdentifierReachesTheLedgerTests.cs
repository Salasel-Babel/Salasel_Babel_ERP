using Babel.Hr.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// <b>لا معرّف شخصي يعبر إلى <c>ledger.*</c> إطلاقاً.</b>
/// <para>
/// لا هوية وطنية، ولا آيبان، ولا اسم — لا في معرّف الطرف، ولا في البيان، ولا في وصف
/// السطر. والسبب بنيوي لا احتياطي: هذه الحقول كلّها داخل الشكل القانوني v2، أي
/// <b>داخل البايتات المُجزَّأة</b>، و<c>REVOKE UPDATE, DELETE</c> على دور التطبيق يجعل
/// ما دخلها غير قابل للإزالة، وعلاجُ المحو الموعود في ADR-0046 — تعميةٌ بمفتاح
/// يُتلَف — <b>لا يبلغ بايتات دخلت سلسلة تجزئة</b> لأن تغييرها يكسر السلسلة.
/// </para>
/// <para>
/// وانتبه أن <c>description_ar_search</c> و<c>memo_ar_search</c> عمودان <b>مفهرسان
/// نصّياً</b>: فرقمٌ شخصي لا يدخل غيرَ ممحوٍّ فحسب، بل <b>قابلَ البحث</b> غير ممحوّ.
/// </para>
/// </summary>
[Collection("hr")]
public sealed class NoPersonalIdentifierReachesTheLedgerTests
{
    [Fact]
    public async Task لا_هوية_ولا_آيبان_ولا_اسم_يظهر_في_قيدٍ_ولا_في_سطرٍ_ولا_في_بيان()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.PrivacyTenant;
        string suffix = Scenario.Suffix();
        string classCode = "class-" + suffix;

        await harness.DepositTestRatesAsync(
            tenant, classCode, Scenario.TestEmployerRate, Scenario.TestEmployeeRate,
            floor: 0m, ceiling: 0m, effectiveFrom: new DateOnly(2026, 1, 1), cancellationToken: token)
            .ConfigureAwait(true);

        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);

        // ثلاث قيمٍ **مميّزة يستحيل أن تظهر مصادفةً** — فبحثٌ يجدها يجدها لأنها كُتبت.
        string nationalId = "1" + suffix.PadRight(9, '0')[..9];
        string iban = "SA44" + suffix.ToUpperInvariant() + "PRIVATE0000";
        string nameAr = "فلان الفلاني " + suffix;

        EmployeeView employee = await Scenario
            .EmployeeAsync(harness, tenant, classCode, component, nationalId, iban, nameAr, 8_000.0000m, token)
            .ConfigureAwait(true);

        Result<PayrollRunView> run = await harness.Runs
            .DraftAsync(
                tenant, Harness.Actor,
                new PayrollRunDraft("RUN-PRIV-" + suffix, "2026-06", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
                token)
            .ConfigureAwait(true);
        Assert.True(run.IsSuccess, Harness.Reason(run));

        Result<IReadOnlyList<PayslipView>> posted = await harness.Runs
            .PostAsync(tenant, Harness.Actor, run.Value.Id, token).ConfigureAwait(true);
        Assert.True(posted.IsSuccess, Harness.Reason(posted));

        // ── الطرف في الدفتر هو الرمز المعتم وحده ────────────────────────────────
        Assert.StartsWith("emp-", employee.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(nationalId, employee.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(suffix, employee.Code, StringComparison.Ordinal);

        // ── ولا واحدة من الثلاث في أي حقلٍ نصّي داخل الدفتر ─────────────────────
        Assert.False(
            await Scenario.LedgerMentionsAsync(nationalId, token).ConfigureAwait(true),
            "رقم الهوية عبر إلى الدفتر — وما دخل البايتات المُجزَّأة لا يُمحى.");
        Assert.False(
            await Scenario.LedgerMentionsAsync(iban, token).ConfigureAwait(true),
            "الآيبان عبر إلى الدفتر — وهو يدخل عموداً مفهرساً نصّياً، فيصير قابلَ البحث غير ممحوّ.");
        Assert.False(
            await Scenario.LedgerMentionsAsync(nameAr, token).ConfigureAwait(true),
            "اسم الموظف عبر إلى الدفتر — والبيان يُركَّب من الفترة والرمز المعتم وحدهما.");

        // ── والشاهد الموجب: الكاشف يرى ما هو موجود فعلاً ────────────────────────
        // بلا هذا البند كان «لا يظهر شيء» يحتمل «الكاشف لا يقرأ شيئاً».
        Assert.True(
            await Scenario.LedgerMentionsAsync(employee.Code, token).ConfigureAwait(true),
            "الكاشف لم يجد حتى الرمز المعتم — أي أنه لا يقرأ الدفتر، فالبنود أعلاه لا تُثبت شيئاً.");
    }

    /// <summary>
    /// <b>والهوية لا تعود من السطح إلا مقنَّعة</b>: آخر أربعة محارف وحدها، وما قبلها
    /// نجومٌ <b>بعدد ثابت</b> — فعددٌ يساوي طول الأصل كان سيُسرّب الطول، وطولُ الآيبان
    /// يُميّز بلد إصداره.
    /// </summary>
    [Fact]
    public async Task قراءةُ_الموظف_لا_تُعيد_الهوية_ولا_الآيبان_إلا_مقنَّعين()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.MaskedReadTenant;
        string suffix = Scenario.Suffix();
        string component = await Scenario.BasicComponentAsync(harness, tenant, suffix, token).ConfigureAwait(true);

        string nationalId = "1" + suffix.PadRight(9, '0')[..9];
        string iban = "SA77" + suffix.ToUpperInvariant() + "MASKED00000";

        EmployeeView created = await Scenario
            .EmployeeAsync(
                harness, tenant, "class-" + suffix, component, nationalId, iban,
                "موظف القناع " + suffix, 3_000.0000m, token)
            .ConfigureAwait(true);

        Result<EmployeeView> read = await harness.Employees
            .GetAsync(tenant, Harness.Actor, created.Id, token).ConfigureAwait(true);
        Assert.True(read.IsSuccess, Harness.Reason(read));

        Assert.DoesNotContain(nationalId, read.Value.Identity.NationalIdMask, StringComparison.Ordinal);
        Assert.DoesNotContain(iban, read.Value.Identity.IbanMask, StringComparison.Ordinal);
        Assert.EndsWith(nationalId[^4..], read.Value.Identity.NationalIdMask, StringComparison.Ordinal);
        Assert.EndsWith(iban[^4..], read.Value.Identity.IbanMask, StringComparison.Ordinal);

        // والقناعان متساويا الطول رغم اختلاف طولَي الأصلين — فلا يُقرأ منهما الطول.
        Assert.Equal(read.Value.Identity.NationalIdMask.Length, read.Value.Identity.IbanMask.Length);
        Assert.NotEqual(nationalId.Length, iban.Length);
    }
}
