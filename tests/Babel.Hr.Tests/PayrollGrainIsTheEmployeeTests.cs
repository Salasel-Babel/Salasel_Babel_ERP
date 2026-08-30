using System.Globalization;
using Babel.Hr.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// <b>الإثبات الحاكم في هذه الوحدة: حبيبيّة الترحيل هي الموظف.</b>
/// <para>
/// نداءٌ واحد على المسيّر يُصدر <b>قيداً لكل قسيمة</b>، لكلٍّ طرفه في
/// <c>subledger_party_id</c> ومركز تكلفته على سطوره كلّها.
/// </para>
/// <para>
/// <b>ولماذا يُثبَت هذا بفحص صفوف الدفتر لا بفحص جواب الخدمة:</b> العطل الذي يمنعه هذا
/// الملفّ — قيدٌ واحد للمسيّر يكتب ذمّة الجميع على طرفٍ واحد — <b>متوازن تماماً،
/// وسلسلة بصماته سليمة، وميزان مراجعته صحيح</b>. فلا يُظهره توازنٌ ولا تجزئة ولا رقمٌ
/// مجمَّع: يُظهره <b>عدّ الأطراف المتمايزة في <c>ledger.journal_line</c></b> وحده.
/// </para>
/// </summary>
[Collection("hr")]
public sealed class PayrollGrainIsTheEmployeeTests
{
    /// <summary>
    /// فترة إثبات الحبيبيّة — مفتوحة في بذر الدفتر.
    /// <para>
    /// <b>ولكل اختبار في هذه المجموعة فترتُه هو.</b> والمنشأة لا تقبل مسيّرين لفترة
    /// واحدة (البند مفتوح على المالك)، ومسحُ العزل يشغّل كل دالّة وحدها — فاشتراكُ
    /// اختبارين في فترة يجعلهما يمرّان معاً ويسقط ثانيهما مجتمعَين، وهو «أخضر بترتيب
    /// التشغيل لا ببنائه».
    /// </para>
    /// </summary>
    private const string GrainPeriod = "2026-03";

    private static readonly DateOnly GrainStart = new(2026, 3, 1);
    private static readonly DateOnly GrainEnd = new(2026, 3, 31);

    /// <summary>فترة إثبات الحصانة — غير فترة الحبيبيّة عمداً.</summary>
    private const string IdempotencyPeriod = "2026-07";

    private static readonly DateOnly IdempotencyStart = new(2026, 7, 1);
    private static readonly DateOnly IdempotencyEnd = new(2026, 7, 31);

    [Fact]
    public async Task نداءُ_الترحيل_الواحد_يُصدر_قيداً_لكل_قسيمة_بطرفها_هي()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.Tenant;
        string suffix = Scenario.Suffix();
        string classCode = "class-" + suffix;

        // ── صفّ نِسَبٍ معتمد يُودَع من داخل الاختبار، بقيمٍ **مختلقة معلَنة** ────
        // ولا رقم نظامي واحد هنا: النِّسَب الحقيقية غير متحقَّق منها (م-14).
        await harness.DepositTestRatesAsync(
            tenant, classCode,
            employerRate: 0.10000000m, employeeRate: 0.05000000m,
            floor: 0m, ceiling: 0m, effectiveFrom: new DateOnly(2026, 1, 1), cancellationToken: token).ConfigureAwait(true);

        Result<PayComponentView> basic = await harness.Employees
            .AddPayComponentAsync(
                tenant, Harness.Actor,
                new PayComponentDraft("basic-" + suffix, new TranslatedName("الراتب الأساسي"), "earning", true, true),
                token)
            .ConfigureAwait(true);
        Assert.True(basic.IsSuccess, Harness.Reason(basic));

        // ثلاثة موظفين بأجور مختلفة: **أجورٌ مختلفة هي ما يجعل الخلط مرئياً**. لو
        // تساوت لكان قيدٌ واحد بمجموعها وثلاثة قيود متساوية يُنتجان الأرقام نفسها.
        decimal[] wages = [10_000.0000m, 7_000.0000m, 4_000.0000m];
        List<EmployeeView> employees = [];

        for (int i = 0; i < wages.Length; i++)
        {
            EmployeeView employee = await RegisterAsync(harness, tenant, classCode, suffix, i, token).ConfigureAwait(true);
            employees.Add(employee);

            Result<PayElementView> element = await harness.Employees
                .AddPayElementAsync(
                    tenant, Harness.Actor, employee.Id,
                    new PayElementDraft(basic.Value.Code, new DateOnly(2026, 1, 1), Money.Of(wages[i], Harness.Currency)),
                    token)
                .ConfigureAwait(true);
            Assert.True(element.IsSuccess, Harness.Reason(element));
        }

        Result<PayrollRunView> run = await harness.Runs
            .DraftAsync(
                tenant, Harness.Actor,
                new PayrollRunDraft("RUN-" + suffix, GrainPeriod, GrainStart, GrainEnd),
                token)
            .ConfigureAwait(true);
        Assert.True(run.IsSuccess, Harness.Reason(run));
        Assert.Equal(3, run.Value.PayslipCount);

        Result<IReadOnlyList<PayslipView>> posted = await harness.Runs
            .PostAsync(tenant, Harness.Actor, run.Value.Id, token)
            .ConfigureAwait(true);
        Assert.True(posted.IsSuccess, Harness.Reason(posted));

        // ── ١ · ثلاث قسائم، وثلاثة معرّفات قيود **متمايزة** ─────────────────────
        Assert.Equal(3, posted.Value.Count);
        Assert.Equal(3, posted.Value.Select(static slip => slip.EntryId).Distinct().Count());
        Assert.All(posted.Value, static slip => Assert.NotNull(slip.EntryId));

        // ── ٢ · وفي الدفتر نفسه: ثلاثة قيود، لا واحد ────────────────────────────
        List<Guid> entries = [.. posted.Value.Select(static slip => slip.EntryId!.Value)];
        Assert.Equal(3, await CountEntriesAsync(entries, token).ConfigureAwait(true));

        // ── ٣ · **وثلاثة أطراف متمايزة** — وهذا هو الإثبات كلّه ─────────────────
        // قيدٌ واحد للمسيّر كان سيُنتج طرفاً واحداً هنا، وهو متوازن تماماً.
        HashSet<string> parties = await PartiesAsync(entries, token).ConfigureAwait(true);
        Assert.Equal(3, parties.Count);
        Assert.Equal([.. employees.Select(static e => e.Code).Order(StringComparer.Ordinal)], [.. parties.Order(StringComparer.Ordinal)]);

        // ── ٤ · ومركز التكلفة على **كل** سطر لا على سطرَي المصروف وحدهما ────────
        // القالب يورّث بُعد الطلب إلى كل سطر يولّده، والقاعدة تفرضه بقيد تحقّق.
        Assert.Equal(0, await CountLinesWithoutCostCenterAsync(entries, token).ConfigureAwait(true));

        // ── ٥ · وكل قيد بمبلغ قسيمته هو، لا بمجموع المسيّر ──────────────────────
        foreach (PayslipView slip in posted.Value)
        {
            decimal debit = await EntryDebitAsync(slip.EntryId!.Value, token).ConfigureAwait(true);
            decimal expected = slip.Amounts.GrossEntitlements.Amount + slip.Amounts.EmployerSocialInsurance.Amount;
            Assert.Equal(expected, debit);
        }
    }

    /// <summary>
    /// <b>الوصول الثاني بالهوية نفسها لا يُنشئ قيداً ثانياً</b> — مهما كان ترتيب
    /// الوصول، ولأن الهوية السداسية تحمل معرّف <b>القسيمة</b> فالحصانة لكل قسيمة على
    /// حدة لا للمسيّر جملةً.
    /// </summary>
    [Fact]
    public async Task إعادةُ_الترحيل_تُرجع_القيود_نفسها_موسومةً_ولا_تكتب_ثانيةً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = HrTestEnvironment.IdempotencyTenant;
        string suffix = Scenario.Suffix();
        string classCode = "class-" + suffix;

        await harness.DepositTestRatesAsync(
            tenant, classCode,
            employerRate: 0.10000000m, employeeRate: 0.05000000m,
            floor: 0m, ceiling: 0m, effectiveFrom: new DateOnly(2026, 1, 1), cancellationToken: token).ConfigureAwait(true);

        Result<PayComponentView> basic = await harness.Employees
            .AddPayComponentAsync(
                tenant, Harness.Actor,
                new PayComponentDraft("basic-" + suffix, new TranslatedName("الراتب الأساسي"), "earning", true, true),
                token)
            .ConfigureAwait(true);
        Assert.True(basic.IsSuccess, Harness.Reason(basic));

        EmployeeView employee = await RegisterAsync(harness, tenant, classCode, suffix, 0, token).ConfigureAwait(true);
        await harness.Employees
            .AddPayElementAsync(
                tenant, Harness.Actor, employee.Id,
                new PayElementDraft(basic.Value.Code, new DateOnly(2026, 1, 1), Money.Of(5_000.0000m, Harness.Currency)),
                token)
            .ConfigureAwait(true);

        Result<PayrollRunView> run = await harness.Runs
            .DraftAsync(tenant, Harness.Actor, new PayrollRunDraft("RUN-" + suffix, IdempotencyPeriod, IdempotencyStart, IdempotencyEnd), token)
            .ConfigureAwait(true);
        Assert.True(run.IsSuccess, Harness.Reason(run));

        Result<IReadOnlyList<PayslipView>> first = await harness.Runs
            .PostAsync(tenant, Harness.Actor, run.Value.Id, token).ConfigureAwait(true);
        Assert.True(first.IsSuccess, Harness.Reason(first));
        Assert.All(first.Value, static slip => Assert.False(slip.AlreadyPosted));

        Result<IReadOnlyList<PayslipView>> second = await harness.Runs
            .PostAsync(tenant, Harness.Actor, run.Value.Id, token).ConfigureAwait(true);
        Assert.True(second.IsSuccess, Harness.Reason(second));
        Assert.All(second.Value, static slip => Assert.True(slip.AlreadyPosted));

        Assert.Equal(
            [.. first.Value.Select(static slip => slip.EntryId)],
            [.. second.Value.Select(static slip => slip.EntryId)]);

        Assert.Equal(
            first.Value.Count,
            await CountEntriesAsync([.. first.Value.Select(static slip => slip.EntryId!.Value)], token).ConfigureAwait(true));
    }

    private static async Task<EmployeeView> RegisterAsync(
        Harness harness, TenantId tenant, string classCode, string suffix, int index, CancellationToken token)
    {
        string ordinal = index.ToString(CultureInfo.InvariantCulture);

        Result<EmployeeView> employee = await harness.Employees
            .RegisterAsync(
                tenant,
                Harness.Actor,
                new EmployeeDraft(
                    new TranslatedName("موظف الاختبار " + ordinal),
                    classCode,
                    CostCenterId: string.Empty,
                    HiredOn: new DateOnly(2025, 1, 1),
                    new EmployeeIdentityDraft(
                        "1" + suffix.PadRight(9, '0')[..9] + ordinal,
                        "SA" + suffix.ToUpperInvariant() + "0000000000000" + ordinal,
                        new DateOnly(1990, 1, 1))),
                token)
            .ConfigureAwait(false);

        Assert.True(employee.IsSuccess, Harness.Reason(employee));
        return employee.Value;
    }

    // ── قراءةُ الدفتر نفسه — لا جوابَ الخدمة ─────────────────────────────────────

    internal static async Task<int> CountEntriesAsync(IReadOnlyList<Guid> entries, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            "select count(distinct entry_id) from ledger.journal_entry where entry_id = any($1)", connection);
        command.Parameters.AddWithValue(entries.ToArray());
        return Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    internal static async Task<HashSet<string>> PartiesAsync(IReadOnlyList<Guid> entries, CancellationToken token)
    {
        HashSet<string> parties = new(StringComparer.Ordinal);

        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select distinct subledger_party_id
              from ledger.journal_line
             where entry_id = any($1)
               and subledger_kind = 'employee'
               and subledger_party_id is not null
            """, connection);
        command.Parameters.AddWithValue(entries.ToArray());

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            parties.Add(reader.GetString(0));
        }

        return parties;
    }

    private static async Task<int> CountLinesWithoutCostCenterAsync(IReadOnlyList<Guid> entries, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select count(*) from ledger.journal_line
             where entry_id = any($1)
               and (cost_center_id is null or btrim(cost_center_id) = '')
            """, connection);
        command.Parameters.AddWithValue(entries.ToArray());
        return Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> EntryDebitAsync(Guid entry, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(HrTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            "select coalesce(sum(debit_company), 0) from ledger.journal_line where entry_id = $1", connection);
        command.Parameters.AddWithValue(entry);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }
}
