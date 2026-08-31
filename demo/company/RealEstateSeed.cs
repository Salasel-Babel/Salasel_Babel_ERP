using System.Globalization;
using Babel.Contracts.RealEstate;
using Babel.Core;
using Babel.Core.Entitlement;
using Babel.Ledger;
using Babel.RealEstate;
using Babel.RealEstate.Application;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// بذر العقارات — <b>دورةٌ واحدة كاملة تُرى على الشاشة، لا صفوفٌ خام في جدول</b>.
/// <para>
/// وكل صفٍّ هنا يمرّ بخدمات الوحدة المعلَنة نفسها التي يناديها سطح HTTP: عقارٌ يُنشأ
/// فيُسجَّل بُعده في الدفتر في العملية نفسها، ووحداتٌ تحته، ومستأجرون، وعقدٌ يُفعَّل
/// فيصير جدوله قابلاً للفوترة، وفواتير تُرحَّل عبر محرّك الترحيل، وسندُ قبضٍ جزئي —
/// فتصير شاشة المتأخرات تُظهر <b>واقعةً</b> لا جدولاً فارغاً.
/// </para>
/// <para>
/// <b>ولا إدراج خام واحد</b>، للسبب نفسه الذي في <see cref="Seed"/>: بذرٌ يكتب في الجداول
/// مباشرةً يُنتج شاشةً جميلة ودفتراً لا يعرفها، ونقطةَ ضبطٍ لا يطابقها دفترٌ مساعد.
/// </para>
/// <para>
/// <b>والاستحقاق يُشترى صراحةً:</b> العقارات وحدة <b>اختيارية</b>، فحالتها الافتراضية على
/// كل منشأة <c>NotEntitled</c> (‏<see cref="ModuleDependencyGraph"/>). وشراؤها هنا فعلٌ
/// مكتوب بسببه، لا افتراضٌ صامت — والخادم يشتريها من إعداده هو
/// (<c>Babel__Entitlements__&lt;الشركة&gt;__RealEstate</c> في <c>deploy/compose.yml</c>).
/// </para>
/// </summary>
internal sealed class RealEstateSeed : IDisposable
{
    /// <summary>
    /// نسبة الضريبة المستعملة في هذا البذر التجريبي.
    /// <para>
    /// <b>وهي قيمة عرضٍ لا رقمٌ نظامي</b>: الوحدة لا تكتب نسبةً في شيفرتها إطلاقاً
    /// (ADR-0052 §6)، والنسبة تصل مع الطلب — ومَن يُصدر الطلب هنا هو طبقة العرض،
    /// وهي دَينٌ مُعلَن بحدوده في <see href="../../docs/decisions/ADR-0037-demo-layer-is-declared-debt-not-an-exempted-path.md">ADR-0037</see>.
    /// وهي النسبة نفسها التي يستعملها بذر المبيعات للبند القياسي، حرفاً بحرف.
    /// </para>
    /// </summary>
    private const decimal DemoStandardTaxRate = 0.15m;

    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private readonly PropertyService _properties;
    private readonly PartyService _parties;
    private readonly LeaseContractService _leases;
    private readonly RentInvoiceService _invoices;
    private readonly TenantReceiptService _receipts;
    private readonly TenantArrearsService _arrears;
    private readonly IEntitlementService _entitlements;

    private int _postedEntries;

    private RealEstateSeed(Settings settings)
    {
        _settings = settings;

        ServiceCollection services = new();

        // النواة: منها حالُّ مركز التكلفة الذي تسأله بوّابة الترحيل قبل أن تبني طلباً،
        // ومنها منفِّذ الاستحقاق. وبمخزنَي PostgreSQL لا بمخزن ذاكرة — كالخادم بالضبط.
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        // الدفتر: منه IPostingService، ومنه **الكاتب الوحيد** في سجلّ أبعاد العقار
        // (‏IPropertyDimensionRegistrar)، ومنه قارئ نقطة الضبط للمطابقة.
        services.AddBabelLedger(options =>
        {
            options.AppConnectionString = settings.Ledger.AppConnectionString;
            options.OwnerConnectionString = settings.Ledger.OwnerConnectionString;
            options.AppRole = settings.Ledger.AppRole;
            options.CompanyCurrency = settings.Ledger.CompanyCurrency;
        });

        services.AddBabelRealEstate(options =>
        {
            options.ConnectionString = settings.RealEstateOwner.ConnectionString;
            options.CompanyCurrency = settings.Ledger.CompanyCurrency;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        IServiceProvider scoped = _scope.ServiceProvider;
        _properties = scoped.GetRequiredService<PropertyService>();
        _parties = scoped.GetRequiredService<PartyService>();
        _leases = scoped.GetRequiredService<LeaseContractService>();
        _invoices = scoped.GetRequiredService<RentInvoiceService>();
        _receipts = scoped.GetRequiredService<TenantReceiptService>();
        _arrears = scoped.GetRequiredService<TenantArrearsService>();
        _entitlements = scoped.GetRequiredService<IEntitlementService>();
    }

    private TenantId Tenant => new(_settings.Company);

    private CurrencyCode Currency => CurrencyCode.FromString(_settings.Ledger.CompanyCurrency);

    /// <summary>يبذر دورة العقارات إن لم تكن مبذورة، ويُرجع عدد القيود التي رُحّلت.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر دورة العقارات عبر خدمات الوحدة / seeding the real-estate cycle through the module services");

        // حارسُ إعادةٍ **مستقلّ** عن حارس بذر المبيعات: نشرٌ يُعاد بعد فشلٍ في منتصفه
        // يجب أن يبلغ ما لم يُبذر بعدُ، لا أن يُصرَف عنه لأن جدولاً آخر امتلأ.
        if (await AlreadySeededAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            Say.Detail("العقارات مبذورة سلفاً — لا يُعاد البذر.");
            return 0;
        }

        using RealEstateSeed seed = new(settings);
        await seed.EntitleAsync(cancellationToken).ConfigureAwait(false);
        await seed.CycleAsync(cancellationToken).ConfigureAwait(false);
        return seed._postedEntries;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private static async Task<bool> AlreadySeededAsync(Settings settings, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(settings.RealEstateOwner.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """select count(*) from realestate.lease_contract where "TenantId" = $1""", connection);
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
                    new Dictionary<BabelModule, EntitlementState> { [BabelModule.RealEstate] = EntitlementState.Entitled },
                    Seed.Actor,
                    "شراء وحدة العقارات للمنشأة التجريبية / real-estate entitled for the demo company"),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(applied, "شراء وحدة العقارات");
        Say.Detail("الاستحقاق: العقارات Entitled — وهي وحدة اختيارية، فالافتراضي NotEntitled ولا يُتجاوز صمتاً.");
    }

    private async Task CycleAsync(CancellationToken cancellationToken)
    {
        // ── ١ · الأطراف: مالكٌ ومستأجران ───────────────────────────────────────
        Guid owner = (await CreateOwnerAsync(
            "OWN-01", "ورثة عبدالله الراجحي", "Abdullah Al-Rajhi Estate", "300000000000003", cancellationToken)
            .ConfigureAwait(false)).Id;

        Guid firstLessee = (await CreateLesseeAsync(
            "LSE-01", "شركة نجم الشمال للتجارة", "North Star Trading Co.", "310000000000003", cancellationToken)
            .ConfigureAwait(false)).Id;

        Guid secondLessee = (await CreateLesseeAsync(
            "LSE-02", "مؤسسة الواحة للمقاولات", "Al-Waha Contracting Est.", string.Empty, cancellationToken)
            .ConfigureAwait(false)).Id;

        Say.Detail("أطراف: مالكٌ واحد ومستأجران — والمالك طرفٌ في دفتره المساعد لا عمود على العقار (ADR-0052 §5).");

        // ── ٢ · عقاران بنموذجَي الملكية معاً ───────────────────────────────────
        // ‏**والنموذجان مقصودان**: الفرق بينهما يظهر في **دائن الفاتورة** — إيرادُ إيجار
        // مؤجَّل للشركة في الملكية الذاتية، وأماناتُ مالكٍ في الإدارة. وعرضٌ بنموذج واحد
        // يُخفي أهمّ ما تقرّره هذه الوحدة.
        Guid ownTower = (await CreatePropertyAsync(
            "PRP-01", "برج بابل التجاري", "Babel Commercial Tower",
            PropertyOwnershipModels.OwnProperty, null, cancellationToken).ConfigureAwait(false)).Id;

        Guid managedCompound = (await CreatePropertyAsync(
            "PRP-02", "مجمّع الياسمين السكني", "Al-Yasmin Residential Compound",
            PropertyOwnershipModels.ManagedForOthers, owner, cancellationToken).ConfigureAwait(false)).Id;

        Guid officeOne = (await CreateUnitAsync(
            ownTower, "UNT-101", "مكتب ١٠١", "Office 101", "commercial", "standard", cancellationToken)
            .ConfigureAwait(false)).Id;

        Guid officeTwo = (await CreateUnitAsync(
            ownTower, "UNT-102", "مكتب ١٠٢", "Office 102", "commercial", "standard", cancellationToken)
            .ConfigureAwait(false)).Id;

        await CreateUnitAsync(
            ownTower, "UNT-201", "شقة ٢٠١", "Flat 201", "residential", "exempt", cancellationToken)
            .ConfigureAwait(false);

        await CreateUnitAsync(
            managedCompound, "UNT-A1", "فيلا أ١", "Villa A1", "residential", "exempt", cancellationToken)
            .ConfigureAwait(false);

        Say.Detail("عقاران وأربع وحدات — والتصنيف الضريبي والاستعمال **مُدخَلان لا مشتقّان** (م-3).");

        // ── ٣ · عقدان يُفعَّلان، وجدولاهما مصرَّحان لا موزَّعان ─────────────────
        // ‏**والأقساط تصل مصرَّحةً**: توزيع قيمة العقد يستلزم سياسة تقريب هي قرار مالك
        // مفتوح، والوحدة تفحص أن مجموعها يساوي القيمة بالضبط وترفض بخلافه.
        Guid firstLease = await ActivateLeaseAsync(
            "LSE-CT-2026-01", officeOne, firstLessee,
            new DateOnly(_settings.FiscalYear, 3, 1), new DateOnly(_settings.FiscalYear, 8, 31),
            15_000m, 6, cancellationToken).ConfigureAwait(false);

        Guid secondLease = await ActivateLeaseAsync(
            "LSE-CT-2026-02", officeTwo, secondLessee,
            new DateOnly(_settings.FiscalYear, 5, 1), new DateOnly(_settings.FiscalYear, 10, 31),
            7_000m, 6, cancellationToken).ConfigureAwait(false);

        // ── ٤ · الفواتير تُرحَّل ────────────────────────────────────────────────
        await InvoiceAsync(firstLease, "RIV-2026-0001", 4, cancellationToken).ConfigureAwait(false);
        await InvoiceAsync(secondLease, "RIV-2026-0101", 2, cancellationToken).ConfigureAwait(false);

        // ── ٥ · تحصيلٌ جزئي، وتحصيلٌ مجهول المرجع يُخصَّص بقيدٍ مستقلّ ──────────
        await ReceiptAsync(
            "RCP-2026-0001", firstLessee, new DateOnly(_settings.FiscalYear, 4, 5), 20_000m, cancellationToken)
            .ConfigureAwait(false);

        Guid unallocated = await ReceiptAsync(
            "RCP-2026-0002", null, new DateOnly(_settings.FiscalYear, 6, 10), 5_000m, cancellationToken)
            .ConfigureAwait(false);

        Result<TenantReceiptView> allocated = await _receipts
            .AllocateAsync(Tenant, Seed.Actor, _settings.Company, unallocated, secondLessee, cancellationToken)
            .ConfigureAwait(false);

        Ok(allocated, "تخصيص السند RCP-2026-0002");
        _postedEntries++;

        Say.Detail("قيود العقارات المُرحَّلة: " + Say.Count(_postedEntries));

        // ── ٦ · وما تعرضه الشاشة: أعمارٌ غير صفرية ومطابقةٌ لا تنحرف ────────────
        Result<(ArrearsReport Aging, Babel.RealEstate.Subledger.ControlReconciliationReport Reconciliation)> arrears =
            await _arrears
                .AgingAsync(Tenant, Seed.Actor, _settings.Company, new DateOnly(_settings.FiscalYear, 8, 31), cancellationToken)
                .ConfigureAwait(false);

        Ok(arrears, "قراءة أعمار متأخرات المستأجرين");

        Say.Require(
            arrears.Value.Aging.Totals.Total.Amount > 0m && arrears.Value.Aging.Parties.Count >= 2,
            "شاشة المتأخرات تجد ما تعرضه: مستأجرون بأرصدة قائمة لا جدولٌ فارغ",
            "أطراف=" + Say.Count(arrears.Value.Aging.Parties.Count)
            + " · الإجمالي=" + Say.Money(arrears.Value.Aging.Totals.Total.Amount));

        Say.Require(
            arrears.Value.Reconciliation.IsReconciled && arrears.Value.Reconciliation.Divergence.Amount == 0m,
            "الدفتر المساعد للمستأجرين يطابق نقطة ضبطه في الدفتر بالضبط",
            "نقطة الضبط=" + Say.Money(arrears.Value.Reconciliation.ControlTotal.Amount)
            + " · الدفتر المساعد=" + Say.Money(arrears.Value.Reconciliation.SubledgerTotal.Amount)
            + " · الانحراف=" + Say.Money(arrears.Value.Reconciliation.Divergence.Amount));
    }

    private async Task<PartyView> CreateOwnerAsync(
        string code, string arabic, string english, string vat, CancellationToken cancellationToken)
    {
        Result<PartyView> created = await _parties
            .CreateOwnerAsync(Tenant, Seed.Actor, _settings.Company, Draft(code, arabic, english, vat), cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "تسجيل المالك " + code);
        return created.Value;
    }

    private async Task<PartyView> CreateLesseeAsync(
        string code, string arabic, string english, string vat, CancellationToken cancellationToken)
    {
        Result<PartyView> created = await _parties
            .CreateLesseeAsync(Tenant, Seed.Actor, _settings.Company, Draft(code, arabic, english, vat), cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "تسجيل المستأجر " + code);
        return created.Value;
    }

    private async Task<PropertyView> CreatePropertyAsync(
        string code, string arabic, string english, string model, Guid? owner, CancellationToken cancellationToken)
    {
        Result<PropertyView> created = await _properties
            .CreatePropertyAsync(
                Tenant, Seed.Actor, _settings.Company,
                new PropertyDraft(code, Name(arabic, english), model, owner),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إنشاء العقار " + code + " (" + model + ")");
        return created.Value;
    }

    private async Task<UnitView> CreateUnitAsync(
        Guid propertyId,
        string code,
        string arabic,
        string english,
        string usage,
        string vatTreatment,
        CancellationToken cancellationToken)
    {
        Result<UnitView> created = await _properties
            .CreateUnitAsync(
                Tenant, Seed.Actor, _settings.Company, propertyId,
                new UnitDraft(code, Name(arabic, english), usage, vatTreatment),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إنشاء الوحدة " + code);
        return created.Value;
    }

    /// <summary>ينشئ عقداً بأقساطٍ شهرية متساوية ثم يُفعّله — والتفعيل لا يُرحّل قيداً.</summary>
    private async Task<Guid> ActivateLeaseAsync(
        string contractNo,
        Guid unitId,
        Guid lesseeId,
        DateOnly startsOn,
        DateOnly endsOn,
        decimal monthlyRent,
        int months,
        CancellationToken cancellationToken)
    {
        List<InstalmentDraft> instalments = [];
        for (int index = 0; index < months; index++)
        {
            DateOnly from = startsOn.AddMonths(index);
            DateOnly to = from.AddMonths(1).AddDays(-1);
            instalments.Add(new InstalmentDraft(from, to, from, Money.Of(monthlyRent, Currency)));
        }

        Result<LeaseView> lease = await _leases
            .DraftAsync(
                Tenant, Seed.Actor, _settings.Company,
                new LeaseDraft(
                    contractNo, unitId, lesseeId, startsOn, endsOn,
                    Money.Of(monthlyRent * months, Currency), instalments),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(lease, "إنشاء العقد " + contractNo);

        Result<LeaseView> activated = await _leases
            .ActivateAsync(Tenant, Seed.Actor, _settings.Company, lease.Value.Id, cancellationToken)
            .ConfigureAwait(false);

        Ok(activated, "تفعيل العقد " + contractNo);
        Say.Detail(
            "عقد " + contractNo + ": " + Say.Count(months) + " قسطاً × " + Say.Money(monthlyRent)
            + " = " + Say.Money(monthlyRent * months) + " — والمجموع يساوي قيمة العقد بالضبط");

        return lease.Value.Id;
    }

    /// <summary>يُصدر فواتير لأوائل أقساط العقد ويُرحّلها، فاتورةً لكل قسط.</summary>
    private async Task InvoiceAsync(
        Guid leaseId, string firstNumber, int count, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ScheduleLineView>> schedule = await _leases
            .ReadScheduleAsync(Tenant, Seed.Actor, _settings.Company, leaseId, cancellationToken)
            .ConfigureAwait(false);

        Ok(schedule, "قراءة جدول دفعات العقد");

        string prefix = firstNumber[..^4];
        int sequence = int.Parse(firstNumber[^4..], CultureInfo.InvariantCulture);

        for (int index = 0; index < count && index < schedule.Value.Count; index++)
        {
            ScheduleLineView line = schedule.Value[index];
            string number = prefix + (sequence + index).ToString("0000", CultureInfo.InvariantCulture);

            Result<RentInvoiceView> invoice = await _invoices
                .DraftAsync(
                    Tenant, Seed.Actor, _settings.Company,
                    new RentInvoiceDraft(number, leaseId, line.DueOn, [line.Id], DemoStandardTaxRate),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(invoice, "إصدار فاتورة الإيجار " + number);

            Result<RentInvoiceView> posted = await _invoices
                .PostAsync(Tenant, Seed.Actor, _settings.Company, invoice.Value.Id, cancellationToken)
                .ConfigureAwait(false);

            Ok(posted, "ترحيل فاتورة الإيجار " + number);
            _postedEntries++;
        }
    }

    private async Task<Guid> ReceiptAsync(
        string number, Guid? lesseeId, DateOnly receivedOn, decimal amount, CancellationToken cancellationToken)
    {
        Result<TenantReceiptView> receipt = await _receipts
            .DraftAsync(
                Tenant, Seed.Actor, _settings.Company,
                new TenantReceiptDraft(number, lesseeId, receivedOn, "bank", "BNK-1", Money.Of(amount, Currency)),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(receipt, "إصدار سند القبض " + number);

        Result<TenantReceiptView> posted = await _receipts
            .PostAsync(Tenant, Seed.Actor, _settings.Company, receipt.Value.Id, cancellationToken)
            .ConfigureAwait(false);

        Ok(posted, "ترحيل سند القبض " + number);
        _postedEntries++;
        return receipt.Value.Id;
    }

    private static PartyDraft Draft(string code, string arabic, string english, string vat)
        => new(code, Name(arabic, english), vat, "resident");

    private static TranslatedName Name(string arabic, string english)
        => new(arabic, new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = english });

    /// <summary>يرمي عند أول رفض برمزه ورسالته — بذرٌ نصفه صحيح أسوأ من بذر فاشل.</summary>
    private static void Ok<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                what + " — رُفض: " + string.Join(" | ", result.Errors.Select(static error => error.ToString())));
        }
    }
}
