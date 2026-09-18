using System.Globalization;
using Babel.Core;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Hr;
using Babel.Hr.Application;
using Babel.Ledger;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// بذر الموارد البشرية — <b>دورةُ رواتبَ كاملة تُرى على ثماني شاشات</b>.
/// <para>
/// وكل صفٍّ يمرّ بخدمات الوحدة المعلَنة نفسها التي يناديها سطح HTTP: مكوّناتُ
/// أجرٍ بأوعيتها، ثمّ نِسَبُ تأمينات بإصدارٍ معتمَد، ثمّ موظّفون بعناصر أجرهم،
/// ثمّ مسيّرُ رواتبٍ يُسوَّد ثمّ <b>يُرحَّل</b> فتصير القسائم والمطابقةُ وقائع.
/// </para>
/// <para>
/// <b>ولا إدراج خام واحد</b>: مسيّرٌ يُكتب في الجدول مباشرةً ينتج قسائمَ لا
/// يقابلها قيدٌ في الدفتر، ودفتراً مساعداً للموظفين لا تطابقه نقطةُ ضبط. فتُظهر
/// شاشةُ المطابقة «متطابق» وهي لم تطابق شيئاً — وذلك أسوأ من فراغٍ يُرى.
/// </para>
/// <para>
/// <b>ولا مبلغَ نهاية خدمةٍ يُحسب هنا</b>: أساسُ المكافأة غير محسومٍ في هذا
/// المستودع، وشاشتُه تُعلن ذلك نقصاً. فبذرُ رقمٍ يبدو محسوباً كان سيغلق سؤالاً
/// لم يُجب — ويُسكِت لوحَ النقص الذي كُتب ليُرى.
/// </para>
/// </summary>
internal sealed class HrSeed : IDisposable
{
    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private readonly EmployeeService _employees;
    private readonly PayrollSettingsService _settingsService;
    private readonly PayrollRunService _runs;
    private readonly IEntitlementService _entitlements;
    private readonly ICompanyMoneyResolver _companyMoney;

    private CompanyMoney _money;
    private string _costCentre = string.Empty;
    private int _postedRuns;

    private HrSeed(Settings settings)
    {
        _settings = settings;

        ServiceCollection services = new();

        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        // الدفتر: منه IPostingService الذي تناديه بوّابة ترحيل الرواتب — فقسيمةٌ
        // تُرحَّل هنا تكتب قيدها في الدفتر نفسه، ونقطةُ ضبط الدفتر المساعد تُقرأ منه.
        services.AddBabelLedger(options =>
        {
            options.AppConnectionString = settings.Ledger.AppConnectionString;
            options.OwnerConnectionString = settings.Ledger.OwnerConnectionString;
            options.AppRole = settings.Ledger.AppRole;
        });

        services.AddBabelHr(options =>
        {
            options.ConnectionString = settings.HrOwner.ConnectionString;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        IServiceProvider scoped = _scope.ServiceProvider;
        _employees = scoped.GetRequiredService<EmployeeService>();
        _settingsService = scoped.GetRequiredService<PayrollSettingsService>();
        _runs = scoped.GetRequiredService<PayrollRunService>();
        _entitlements = scoped.GetRequiredService<IEntitlementService>();
        _companyMoney = scoped.GetRequiredService<ICompanyMoneyResolver>();
    }

    private TenantId Tenant => new(_settings.Company);

    private CurrencyCode Currency => _money.IsAssigned
        ? _money.Currency
        : throw new InvalidOperationException("عملة المنشأة تُحلّ في أول الدورة قبل أي مستند.");

    /// <summary>يبذر دورة الرواتب إن لم تكن مبذورة، ويُرجع عدد المسيّرات المُرحَّلة.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر دورة الرواتب عبر خدمات الوحدة / seeding the payroll cycle through the module services");

        if (await AlreadySeededAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            Say.Detail("الموارد البشرية مبذورة سلفاً — لا يُعاد البذر.");
            return 0;
        }

        using HrSeed seed = new(settings);
        await seed.EntitleAsync(cancellationToken).ConfigureAwait(false);
        await seed.CycleAsync(cancellationToken).ConfigureAwait(false);
        return seed._postedRuns;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private static async Task<bool> AlreadySeededAsync(Settings settings, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(settings.HrOwner.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // على أوّل ما تكتبه الدورة — مكوّنُ الأجر — لا على آخره: شوطٌ يسقط في
        // منتصفه يجب أن يُكشَف، لا أن يُعاد فيُرفض بازدواج رمزٍ لا بعطلٍ حقيقي.
        await using NpgsqlCommand command = new(
            """select count(*) from hr.pay_component where "TenantId" = $1""", connection);
        command.Parameters.AddWithValue(settings.Company);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) > 0;
    }

    private async Task EntitleAsync(CancellationToken cancellationToken)
    {
        Result<EntitlementSet> applied = await _entitlements
            .ApplyAsync(
                new EntitlementChangeRequest(
                    Tenant,
                    new Dictionary<BabelModule, EntitlementState> { [BabelModule.Hr] = EntitlementState.Entitled },
                    Seed.Actor,
                    "شراء وحدة الموارد البشرية للمنشأة التجريبية / hr entitled for the demo company"),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(applied, "شراء وحدة الموارد البشرية");
    }

    private async Task CycleAsync(CancellationToken cancellationToken)
    {
        Result<CompanyMoney> money = await _companyMoney.ResolveAsync(Tenant, cancellationToken).ConfigureAwait(false);
        Say.Require(money.IsSuccess, "عملة المنشأة من صفّ التأسيس", string.Join(" | ", money.Errors.Select(static e => e.ToString())));
        _money = money.Value;
        _costCentre = await CostCentreAsync(cancellationToken).ConfigureAwait(false);

        // ── ١ · مكوّناتُ الأجر — وأوعيتُها هي الفرقُ كلُّه ─────────────────────
        //
        // الوسمان `EntersContributoryWage` و`EntersEndOfServiceBase` ليسا حقلين
        // إداريين: الأول يقرّر ما يدخل وعاءَ اشتراك التأمينات، والثاني ما يدخل
        // وعاء المكافأة. فبدلُ سكنٍ يدخل الأول ولا يدخل الثاني — وذلك ما يجعل
        // شاشةَ مكوّنات الأجر ذاتَ معنى بدل أن تكون قائمةَ أسماء.
        await ComponentAsync("BASIC", "الراتب الأساسي", "Basic salary", "earning", true, true, cancellationToken).ConfigureAwait(false);
        await ComponentAsync("HOUSING", "بدل السكن", "Housing allowance", "earning", true, false, cancellationToken).ConfigureAwait(false);
        await ComponentAsync("TRANSPORT", "بدل النقل", "Transport allowance", "earning", false, false, cancellationToken).ConfigureAwait(false);
        await ComponentAsync("PHONE", "بدل الاتصالات", "Communications allowance", "earning", false, false, cancellationToken).ConfigureAwait(false);
        await ComponentAsync("ABSENCE", "خصم غياب", "Absence deduction", "deduction", false, false, cancellationToken).ConfigureAwait(false);
        Say.Detail("مكوّناتُ أجر: خمسة — أربعةُ استحقاقٍ وخصم، بأوعيةٍ مختلفة.");

        // ── ٢ · نِسَبُ التأمينات — إصدارٌ **معتمَد بمرجعه**، لا رقمٌ في الشيفرة ─
        //
        // والتصنيفان مختلفان لأن النسبة تختلف: السعوديُّ عليه اشتراكُ معاشات
        // وساند معاً، وغيرُه اشتراكُ الأخطار المهنية وحده. ورقمٌ واحد للاثنين
        // كان سيُظهر شاشةَ سدادٍ صحيحةَ الشكل خاطئةَ المبلغ.
        await SettingsAsync("SA", 0.2175m, 0.0975m, cancellationToken).ConfigureAwait(false);
        await SettingsAsync("NON-SA", 0.02m, 0m, cancellationToken).ConfigureAwait(false);
        Say.Detail("نِسَبُ تأمينات: إصداران معتمَدان بمرجعٍ وتاريخِ اعتماد.");

        // ── ٣ · الموظّفون وعناصرُ أجرهم ────────────────────────────────────────
        Guid a = await EmployeeAsync("سعد بن ناصر القحطاني", "Saad N. Al-Qahtani", "SA", "1012345678", "SA4420000001234567891234", 1988, 4, 12, 2_022, 3, 1, cancellationToken).ConfigureAwait(false);
        Guid b = await EmployeeAsync("فهد بن عبدالعزيز الدوسري", "Fahad A. Al-Dosari", "SA", "1087654321", "SA4420000009876543211234", 1992, 11, 3, 2_023, 7, 15, cancellationToken).ConfigureAwait(false);
        Guid c = await EmployeeAsync("راجيش كومار نائير", "Rajesh Kumar Nair", "NON-SA", "2233445566", "SA4420000005544332211234", 1985, 2, 20, 2_021, 9, 1, cancellationToken).ConfigureAwait(false);
        Guid d = await EmployeeAsync("محمد أنور حسين", "Mohammed Anwar Hussain", "NON-SA", "2299887766", "SA4420000001122334455667", 1990, 6, 8, 2_024, 1, 10, cancellationToken).ConfigureAwait(false);
        Say.Detail("موظّفون: أربعة بتصنيفَي اشتراكٍ مختلفين، وهوياتُهم مقنَّعة على السلك.");

        await ElementsAsync(a, 12_000m, 3_000m, 800m, 300m, cancellationToken).ConfigureAwait(false);
        await ElementsAsync(b, 9_500m, 2_375m, 800m, 300m, cancellationToken).ConfigureAwait(false);
        await ElementsAsync(c, 7_000m, 1_750m, 600m, 0m, cancellationToken).ConfigureAwait(false);
        await ElementsAsync(d, 5_500m, 1_375m, 600m, 0m, cancellationToken).ConfigureAwait(false);
        Say.Detail("عناصرُ أجر: أساسٌ وسكنٌ ونقلٌ لكلّ موظف — والاتصالاتُ لاثنين.");

        // ── ٤ · مسيّراتُ رواتبَ ثلاثة، تُسوَّد ثمّ تُرحَّل ─────────────────────
        //
        // وثلاثةٌ لا واحد: شاشةُ المسيّر تعرض سلسلةً زمنية، وشاشةُ المطابقة تُقارن
        // رصيدَ الدفتر المساعد بنقطة ضبطه — ومسيّرٌ واحد يجعل المقارنة سطراً لا
        // مطابقة.
        await PayrollAsync("PR-2026-06", 6, cancellationToken).ConfigureAwait(false);
        await PayrollAsync("PR-2026-07", 7, cancellationToken).ConfigureAwait(false);
        await PayrollAsync("PR-2026-08", 8, cancellationToken).ConfigureAwait(false);
        Say.Detail("مسيّراتُ رواتب: " + Say.Count(_postedRuns) + " مُرحَّلة — بقسائمها وقيودها في الدفتر.");
    }

    /// <summary>
    /// مركزُ التكلفة الذي تُرحَّل به الرواتب — <b>يُقرأ من سجلّ المنشأة لا يُخمَّن</b>.
    /// ورمزٌ مخترَع هنا يُرحَّل على مركزٍ لا وجود له، فيُرفض المسيّرُ في منتصفه.
    /// </summary>
    private async Task<string> CostCentreAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(_settings.Core.OwnerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            "select code from core.cost_center where company_id = $1 and state = 'active' order by code limit 1",
            connection);
        command.Parameters.AddWithValue(_settings.Company);
        object? code = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return code is string text && text.Length > 0
            ? text
            : throw new InvalidOperationException("لا مركز تكلفة في سجلّ المنشأة — والرواتب تُرحَّل على مركز.");
    }

    private async Task ComponentAsync(
        string code, string arabic, string english, string kind,
        bool contributory, bool endOfService, CancellationToken cancellationToken)
        => Ok(
            await _employees.AddPayComponentAsync(
                Tenant,
                Seed.Actor,
                new PayComponentDraft(code, Named(arabic, english), kind, contributory, endOfService),
                cancellationToken).ConfigureAwait(false),
            "إنشاء مكوّن الأجر " + code);

    private async Task SettingsAsync(string classCode, decimal employer, decimal employee, CancellationToken cancellationToken)
        => Ok(
            await _settingsService.DepositAsync(
                Tenant,
                Seed.Actor,
                new PayrollSettingsDraft(
                    classCode,
                    new DateOnly(_settings.FiscalYear, 1, 1),
                    employer,
                    employee,
                    Money.Of(1_500m, Currency),
                    Money.Of(45_000m, Currency),
                    "مدير الموارد البشرية",
                    new DateOnly(_settings.FiscalYear, 1, 1),
                    "لائحة التأمينات الاجتماعية — نسخة المنشأة التجريبية"),
                cancellationToken).ConfigureAwait(false),
            "إيداع نِسَب التأمينات للتصنيف " + classCode);

    private async Task<Guid> EmployeeAsync(
        string arabic, string english, string classCode, string nationalId, string iban,
        int birthYear, int birthMonth, int birthDay,
        int hiredYear, int hiredMonth, int hiredDay, CancellationToken cancellationToken)
    {
        Result<EmployeeView> created = await _employees
            .RegisterAsync(
                Tenant,
                Seed.Actor,
                new EmployeeDraft(
                    Named(arabic, english),
                    classCode,
                    _costCentre,
                    new DateOnly(hiredYear, hiredMonth, hiredDay),
                    new EmployeeIdentityDraft(nationalId, iban, new DateOnly(birthYear, birthMonth, birthDay))),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "تسجيل الموظف " + english);
        return created.Value.Id;
    }

    private async Task ElementsAsync(
        Guid employee, decimal basic, decimal housing, decimal transport, decimal phone, CancellationToken cancellationToken)
    {
        DateOnly from = new(_settings.FiscalYear, 1, 1);
        await ElementAsync(employee, "BASIC", from, basic, cancellationToken).ConfigureAwait(false);
        await ElementAsync(employee, "HOUSING", from, housing, cancellationToken).ConfigureAwait(false);
        await ElementAsync(employee, "TRANSPORT", from, transport, cancellationToken).ConfigureAwait(false);

        if (phone > 0m)
        {
            await ElementAsync(employee, "PHONE", from, phone, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ElementAsync(Guid employee, string component, DateOnly from, decimal amount, CancellationToken cancellationToken)
        => Ok(
            await _employees.AddPayElementAsync(
                Tenant, Seed.Actor, employee, new PayElementDraft(component, from, Money.Of(amount, Currency)), cancellationToken)
                .ConfigureAwait(false),
            "عنصر الأجر " + component);

    private async Task PayrollAsync(string number, int month, CancellationToken cancellationToken)
    {
        int lastDay = DateTime.DaysInMonth(_settings.FiscalYear, month);

        Result<PayrollRunView> drafted = await _runs
            .DraftAsync(
                Tenant,
                Seed.Actor,
                new PayrollRunDraft(
                    number,
                    _settings.FiscalYear.ToString("D4", CultureInfo.InvariantCulture) + "-" + month.ToString("D2", CultureInfo.InvariantCulture),
                    new DateOnly(_settings.FiscalYear, month, 1),
                    new DateOnly(_settings.FiscalYear, month, lastDay)),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(drafted, "تسويد مسيّر الرواتب " + number);

        Ok(
            await _runs.PostAsync(Tenant, Seed.Actor, drafted.Value.Id, cancellationToken).ConfigureAwait(false),
            "ترحيل مسيّر الرواتب " + number);

        _postedRuns++;
    }

    /// <summary>اسمٌ بسجلّه العربي وترجمته الإنجليزية — لا حقلٌ إنجليزيّ ثابت (ADR-0021).</summary>
    private static TranslatedName Named(string arabic, string english)
        => new(arabic, new Dictionary<string, string> { ["en"] = english });

    private static void Ok<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                what + " — رُفض: " + string.Join(" | ", result.Errors.Select(static error => error.ToString())));
        }
    }
}
