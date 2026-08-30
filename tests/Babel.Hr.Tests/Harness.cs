using Babel.Contracts.Posting;
using Babel.Core.CompanySetup;
using Babel.Hr.Application;
using Babel.Hr.Subledger;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.SharedKernel;
using Babel.Tests.Shared;
using Xunit;

namespace Babel.Hr.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدتَي بيانات حقيقيتين وعدّاد
/// ترقيم حقيقياً ودفتراً مساعداً واحداً، وتوازيها يجعل «انحراف في المطابقة» تعني
/// «اختباران تسابقا» لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("hr", DisableParallelization = true)]
public sealed class HrTestGroup;

/// <summary>
/// تركيب الاختبار بلا حاوية اعتماديات — وهو <b>الجذر التركيبي مكتوباً بيده</b>:
/// وحدة الموارد البشرية تصف حدثاً، والدفتر يقرّر الحساب، ولا تعرف إحداهما جداول
/// الأخرى.
/// <para>
/// و<b>لا بديل عن أي منهما هنا</b>: ترحيلٌ حقيقي، ودفتر أستاذ حقيقي بمخطّطه وبياناته
/// المرجعية، وقارئ نقطة ضبط يقرأ <c>ledger.journal_line</c> نفسه. فالرقم الذي تُثبته
/// هذه المجموعة رقمٌ ينتجه المنتج.
/// </para>
/// </summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(HrRuntime hr, LedgerRuntime ledger)
    {
        HrRuntime = hr;
        LedgerRuntime = ledger;

        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);

        Employees = new EmployeeService(enforcer, hr);
        Settings = new PayrollSettingsService(enforcer, hr);
        Runs = new PayrollRunService(enforcer, hr, Posting, Settings);
        Payments = new PayrollPaymentService(enforcer, hr, Posting);
        SocialInsurance = new SocialInsurancePaymentService(enforcer, hr, Posting);
        Register = new EmployeeLedgerService(enforcer, hr);
        EndOfService = new EndOfServiceService(enforcer, hr, Posting);
        Reconciliation = new EmployeeReconciliationService(
            enforcer, hr, new LedgerControlPointReader(HrTestEnvironment.Ledger.AppConnectionString));
    }

    public HrRuntime HrRuntime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public EmployeeService Employees { get; }

    public PayrollSettingsService Settings { get; }

    public PayrollRunService Runs { get; }

    public PayrollPaymentService Payments { get; }

    public SocialInsurancePaymentService SocialInsurance { get; }

    public EmployeeLedgerService Register { get; }

    public EndOfServiceService EndOfService { get; }

    public EmployeeReconciliationService Reconciliation { get; }

    /// <summary>الفاعل — إنسانٌ واحد لكل هذه المجموعة، يدخل البايتات المُجزَّأة.</summary>
    public static UserId Actor { get; } = new(new Guid("9c2f0d44-1111-4111-8111-111111111111"));

    /// <summary>عملة المنشأة في هذه المجموعة.</summary>
    public static CurrencyCode Currency => CurrencyCode.Sar;

    /// <summary>يبني التركيبة بعد أن تكون البيئة جاهزة.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
    {
        await HrTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        // ‏**دفترٌ واحد لكل العملية، ووحدةُ موارد بشرية لكل تركيبة.** ‏LedgerRuntime
        // يحمل عدّاد الترقيم ورأس السلسلة، فبناؤه مرّتين في عملية واحدة كان سيجعل
        // اختبارين يتنازعان الرأس نفسه — وهو ما يظهر «سلسلة مكسورة» لا «تصميم خاطئ».
        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(HrTestEnvironment.Ledger);
        }

        ICostCenterResolver centres = FoundedTenants.ResolverFor(HrTestEnvironment.AllTenants);
        return new Harness(new HrRuntime(HrTestEnvironment.Hr, centres), _ledger!);
    }

    /// <summary>
    /// يودِع صفّ نِسَبٍ معتمداً <b>من داخل الاختبار</b>، بقيمٍ يمرّرها المُستدعي.
    /// <para>
    /// <b>ولا قيمة افتراضية هنا ولا في أي موضع من هذه المجموعة.</b> كل نسبة تُكتب في
    /// نصّ الاختبار الذي يستعملها ومعها مرجعٌ يقول صراحةً إنها **قيمة اختبار مختلقة لا
    /// قيمة نظامية**. والسبب مقيس: نسبةٌ تُكتب «مؤقّتاً» في تجهيز اختبار تُنسخ إلى
    /// إنتاج بعد شهرين، وقد وقع هذا في هذا المستودع من قبل. والقيم النظامية الحقيقية
    /// **غير متحقَّق منها** (البند م-14)، ولا يجوز أن يظهر واحدٌ منها هنا.
    /// </para>
    /// </summary>
    /// <param name="tenant">المنشأة.</param>
    /// <param name="classCode">تصنيف الاشتراك.</param>
    /// <param name="employerRate">نسبة المنشأة — قيمة اختبار مختلقة.</param>
    /// <param name="employeeRate">نسبة الموظف — قيمة اختبار مختلقة.</param>
    /// <param name="floor">أدنى أجر خاضع — قيمة اختبار مختلقة.</param>
    /// <param name="ceiling">أقصى أجر خاضع، أو صفر فلا سقف — قيمة اختبار مختلقة.</param>
    /// <param name="effectiveFrom">تاريخ السريان.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task<PayrollSettingsView> DepositTestRatesAsync(
        TenantId tenant,
        string classCode,
        decimal employerRate,
        decimal employeeRate,
        decimal floor,
        decimal ceiling,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken = default)
    {
        Result<PayrollSettingsView> deposited = await Settings
            .DepositAsync(
                tenant,
                Actor,
                new PayrollSettingsDraft(
                    classCode,
                    effectiveFrom,
                    employerRate,
                    employeeRate,
                    Money.Of(floor, Currency),
                    Money.Of(ceiling, Currency),
                    "accountant.under.test",
                    effectiveFrom,
                    "TEST-ONLY — قيمة مختلقة لهذا الاختبار وحده، وليست نسبة نظامية. "
                    + "النِّسَب النظامية غير متحقَّق منها (م-14) ولا تُكتب في شيفرة ولا في اختبار."),
                cancellationToken)
            .ConfigureAwait(false);

        Assert.True(deposited.IsSuccess, Reason(deposited));
        return deposited.Value;
    }

    /// <summary>سبب الفشل مقروءاً برموزه — رسالةُ فشلٍ بلا رمز تُرسل القارئ إلى تخمين.</summary>
    /// <param name="result">النتيجة.</param>
    public static string Reason<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? "نجاح"
            : string.Join(
                " | ", result.Errors.Select(static error => error.Code + ": " + error.MessageAr));
    }

    /// <inheritdoc />
    public void Dispose() => HrRuntime.Dispose();
}
