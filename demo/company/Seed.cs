using System.Collections.Immutable;
using System.Globalization;
using Babel.Core;
using Babel.Core.CapabilityProfile;
using Babel.Ledger;
using Babel.Purchasing;
using Babel.Purchasing.Application;
using Babel.Sales;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// الخطوة الثالثة: نشاط ثمانية أشهر — <b>كلّه عبر محرّك الترحيل، ولا إدراج خام واحد</b>.
/// <para>
/// <b>ولماذا هذا شرطٌ لا أسلوب:</b> بذرٌ يكتب في <c>ledger.journal_entry</c> مباشرةً
/// ينتج ميزاناً متوازناً وسلسلةَ بصمات <b>مكسورة</b>، ودفتراً مساعداً بلا نقطة ضبط
/// تقابله. أي أن العرض سيُظهر ثلاثة تقارير، اثنان منها كاذبان. والمرور بالمحرّك يجعل
/// العرض <b>صادقاً</b>: ما يراه صاحب القرار على الشاشة هو ما يفعله النظام عند العميل.
/// </para>
/// <para>
/// وهو أيضاً غير قابل للالتفاف حتى لو أراد كاتبه: دور التطبيق الذي تعمل به هذه الخطوة
/// لا يملك <c>INSERT</c> مباشراً يتجاوز <c>ledger.post_entry</c>، ولا <c>UPDATE</c>،
/// ولا <c>DELETE</c> (ADR-0003).
/// </para>
/// </summary>
internal sealed class Seed : IDisposable
{
    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private readonly CustomerService _customers;
    private readonly SalesInvoiceService _invoices;
    private readonly CreditNoteService _creditNotes;
    private readonly CustomerReceiptService _receipts;
    private readonly SupplierService _suppliers;
    private readonly SupplierBillService _bills;
    private readonly SupplierPaymentService _payments;

    private readonly Dictionary<string, Guid> _customerIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _supplierIds = new(StringComparer.Ordinal);
    private readonly List<Open> _openInvoices = [];
    private readonly List<Open> _openBills = [];

    private int _postedEntries;
    private int _sequence;

    private Seed(Settings settings)
    {
        _settings = settings;

        // التركيب بالطريق المعلَن نفسه الذي يسلكه الجذر التركيبي: ‏AddBabel<Module>.
        // ‏**ولا انعكاس ولا نوع داخلي**: محرّك الترحيل نوع internal في الدفتر، ولا يُبلَغ
        // إلا عبر IPostingService الذي تسجّله الوحدة. أي أن هذه الأداة لا تملك طريقاً
        // إلى الدفتر لا يملكه الخادم نفسه.
        ServiceCollection services = new();
        services.AddBabelCore();
        services.AddBabelLedger(options =>
        {
            options.AppConnectionString = settings.Ledger.AppConnectionString;
            options.OwnerConnectionString = settings.Ledger.OwnerConnectionString;
            options.AppRole = settings.Ledger.AppRole;
            options.CompanyCurrency = settings.Ledger.CompanyCurrency;
        });
        services.AddBabelSales(options =>
        {
            options.ConnectionString = settings.SalesOwner.ConnectionString;
            options.CompanyCurrency = settings.Ledger.CompanyCurrency;
        });
        services.AddBabelPurchasing(options =>
        {
            options.ConnectionString = settings.PurchasingOwner.ConnectionString;
            options.CompanyCurrency = settings.Ledger.CompanyCurrency;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        IServiceProvider scoped = _scope.ServiceProvider;
        _customers = scoped.GetRequiredService<CustomerService>();
        _invoices = scoped.GetRequiredService<SalesInvoiceService>();
        _creditNotes = scoped.GetRequiredService<CreditNoteService>();
        _receipts = scoped.GetRequiredService<CustomerReceiptService>();
        _suppliers = scoped.GetRequiredService<SupplierService>();
        _bills = scoped.GetRequiredService<SupplierBillService>();
        _payments = scoped.GetRequiredService<SupplierPaymentService>();
        Profiles = scoped.GetRequiredService<ICapabilityProfileStore>();
    }

    /// <summary>الفاعل: مستخدم تجريبي واحد، معرّفه ثابت كي يظهر هو نفسه في كل سجلّ رقابة.</summary>
    public static UserId Actor { get; } = new(new Guid("d3305e1e-0000-4000-8000-0000000000a1"));

    private ICapabilityProfileStore Profiles { get; }

    private TenantId Tenant => new(_settings.Company);

    private CurrencyCode Currency => CurrencyCode.FromString(_settings.Ledger.CompanyCurrency);

    /// <summary>يبذر نشاط المنشأة إن لم يكن مبذوراً، ثم يُرجع عدد القيود التي رُحّلت.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر نشاط المنشأة عبر محرّك الترحيل / seeding activity through the posting engine");

        if (await AlreadySeededAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            Say.Detail("البيانات مبذورة سلفاً — لا يُعاد البذر. الدفتر لا يُكتب فيه مرّتان بالمعنى نفسه.");
            return 0;
        }

        using Seed seed = new(settings);
        await seed.SaveProfileAsync(cancellationToken).ConfigureAwait(false);
        await seed.MasterDataAsync(cancellationToken).ConfigureAwait(false);
        await seed.MonthsAsync(cancellationToken).ConfigureAwait(false);

        Say.Detail("قيود رُحّلت: " + Say.Count(seed._postedEntries));
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
        await using NpgsqlConnection connection = new(settings.SalesOwner.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """select count(*) from sales.sales_invoice where "TenantId" = $1""", connection);
        command.Parameters.AddWithValue(settings.Company);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) > 0;
    }

    private async Task SaveProfileAsync(CancellationToken cancellationToken)
    {
        CapabilityProfileDraft draft = new(
            new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
            {
                ["sales.invoice"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["advance"] = true,
                        ["cost_of_sales"] = false,
                    },
                    ImmutableSortedDictionary<string, string>.Empty),
            });

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(draft, EmbeddedPostingEventDirectory.Default);

        Ok(profile, "ملفّ قدرات المنشأة التجريبية");
        await Profiles.SaveAsync(Tenant, profile.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task MasterDataAsync(CancellationToken cancellationToken)
    {
        foreach (DemoCustomer customer in Company.Customers)
        {
            Result<CustomerView> created = await _customers
                .CreateAsync(
                    Tenant,
                    Actor,
                    new CustomerDraft(
                        customer.Code,
                        new LocalizedName(customer.Arabic, customer.English),
                        Money.Of(customer.CreditLimit, Currency),
                        customer.TermsDays),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(created, "إنشاء العميل " + customer.Code);
            _customerIds[customer.Code] = created.Value.Id;
        }

        Say.Detail("عملاء: " + Say.Count(_customerIds.Count) + " بأسماء عربية وحدود ائتمان ومهل سداد مختلفة");

        foreach (DemoSupplier supplier in Company.Suppliers)
        {
            Result<SupplierView> created = await _suppliers
                .CreateAsync(
                    Tenant,
                    Actor,
                    new SupplierDraft(
                        supplier.Code,
                        new LocalizedName(supplier.Arabic, supplier.English),
                        Money.Of(200_000m, Currency),
                        supplier.TermsDays),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(created, "إنشاء المورد " + supplier.Code);
            _supplierIds[supplier.Code] = created.Value.Id;
        }

        Say.Detail("موردون: " + Say.Count(_supplierIds.Count));
    }

    /// <summary>
    /// ثمانية أشهر من النشاط. المولّد <b>حتمي ببذرة ثابتة</b>: تشغيلان على قاعدتين
    /// فارغتين يُنتجان الأرقام نفسها، فيُقارَن العرضان.
    /// </summary>
    private async Task MonthsAsync(CancellationToken cancellationToken)
    {
        Deterministic random = new(20260826);

        for (int month = 1; month <= 8; month++)
        {
            int invoicesThisMonth = 4 + (month % 3);

            for (int i = 0; i < invoicesThisMonth; i++)
            {
                DemoCustomer customer = Company.Customers[random.Next(Company.Customers.Count)];
                DemoItem item = Company.Items[random.Next(Company.Items.Count)];
                decimal quantity = 1m + random.Next(6);
                decimal discount = random.Next(5) == 0 ? 250m : 0m;

                DateOnly issued = Day(month, 2 + random.Next(24));
                string number = Number("INV", _settings.FiscalYear, month, ++_sequence);

                Result<SalesDocumentView> created = await _invoices
                    .CreateInvoiceAsync(
                        Tenant,
                        Actor,
                        new SalesDocumentDraft(
                            number,
                            _customerIds[customer.Code],
                            issued,
                            Company.Branch,
                            [
                                new SalesLineDraft(
                                    "*",
                                    new LocalizedName(item.Arabic, item.English),
                                    quantity,
                                    Money.Of(item.UnitPrice, Currency),
                                    Money.Of(discount, Currency),
                                    item.TaxClassification,
                                    string.Equals(item.TaxClassification, "standard", StringComparison.Ordinal) ? 0.15m : 0m),
                            ]),
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);

                Ok(created, "إصدار الفاتورة " + number);

                Result<SalesDocumentView> posted = await _invoices
                    .PostInvoiceAsync(Tenant, Actor, created.Value.Id, cancellationToken)
                    .ConfigureAwait(false);

                Ok(posted, "ترحيل الفاتورة " + number);
                _postedEntries++;

                _openInvoices.Add(new Open(
                    created.Value.Id, number, customer.Code, issued, created.Value.Totals.Gross.Amount));
            }

            await ReceiptsAsync(month, random, cancellationToken).ConfigureAwait(false);
            await CreditNoteAsync(month, cancellationToken).ConfigureAwait(false);
            await BillsAsync(month, random, cancellationToken).ConfigureAwait(false);
            await PaymentsAsync(month, cancellationToken).ConfigureAwait(false);
        }

        Say.Detail("أشهر النشاط: 8 (من يناير إلى أغسطس) — والشهران الأخيران يبقيان بذمم مفتوحة عمداً");
    }

    /// <summary>
    /// سندات القبض. وتُترك ذمم الشهرين الأخيرين مفتوحةً <b>عمداً</b>: تقرير أعمار
    /// كل شرائحه صفر لا يُظهر شيئاً، والذي يُظهر النظام هو التوزيع على الشرائح.
    /// </summary>
    private async Task ReceiptsAsync(int month, Deterministic random, CancellationToken cancellationToken)
    {
        List<Open> due = [.. _openInvoices.Where(open => open.Issued.Month <= Math.Max(1, month - 1) && open.Remaining > 0m)];

        for (int i = 0; i < 3 && due.Count > 0; i++)
        {
            Open invoice = due[random.Next(due.Count)];
            due.Remove(invoice);

            // تسديد جزئي أحياناً: ذمّةٌ تُسدَّد كاملةً دائماً تجعل الأعمار عمودين لا خمسة.
            decimal amount = random.Next(4) == 0
                ? decimal.Round(invoice.Remaining * 0.6m, 4)
                : invoice.Remaining;

            if (amount <= 0m)
            {
                continue;
            }

            string number = Number("RCP", _settings.FiscalYear, month, ++_sequence);
            DateOnly receivedOn = Day(month, 10 + random.Next(15));
            string method = random.Next(5) == 0 ? "cash" : "bank";

            Result<SalesDocumentView> created = await _receipts
                .RecordReceiptAsync(
                    Tenant,
                    Actor,
                    new CustomerReceiptDraft(
                        number,
                        _customerIds[invoice.PartyCode],
                        receivedOn,
                        method,
                        string.Equals(method, "cash", StringComparison.Ordinal) ? Company.Cash : Company.Bank,
                        Money.Of(amount, Currency),
                        Money.Of(0m, Currency),
                        [new AllocationDraft(invoice.Id, Money.Of(amount, Currency))]),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(created, "تسجيل سند القبض " + number);

            Result<SalesDocumentView> posted = await _receipts
                .PostReceiptAsync(Tenant, Actor, created.Value.Id, cancellationToken)
                .ConfigureAwait(false);

            Ok(posted, "ترحيل سند القبض " + number);
            _postedEntries++;
            invoice.Remaining -= amount;
        }
    }

    /// <summary>إشعار دائن كل شهرين: مرتجعٌ جزئي على أقدم فاتورة قائمة.</summary>
    private async Task CreditNoteAsync(int month, CancellationToken cancellationToken)
    {
        const decimal CreditNoteGross = 1_380m;

        Open? invoice = _openInvoices
            .FirstOrDefault(open => open.Issued.Month < month && open.Remaining >= CreditNoteGross);

        if (month % 3 != 0 || invoice is null)
        {
            return;
        }

        string number = Number("CRN", _settings.FiscalYear, month, ++_sequence);

        Result<SalesDocumentView> created = await _creditNotes
            .CreateAsync(
                Tenant,
                Actor,
                new CreditNoteDraft(
                    number,
                    invoice.Id,
                    Day(month, 20),
                    [
                        new SalesLineDraft(
                            "*",
                            new LocalizedName("مرتجع جزئي على الفاتورة " + invoice.Number, "Partial return on invoice " + invoice.Number),
                            1m,
                            Money.Of(1_200m, Currency),
                            Money.Of(0m, Currency),
                            "standard",
                            0.15m),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إصدار الإشعار الدائن " + number);

        Result<SalesDocumentView> posted = await _creditNotes
            .PostAsync(Tenant, Actor, created.Value.Id, cancellationToken)
            .ConfigureAwait(false);

        Ok(posted, "ترحيل الإشعار الدائن " + number);
        _postedEntries++;
        invoice.Remaining -= CreditNoteGross;
    }

    private async Task BillsAsync(int month, Deterministic random, CancellationToken cancellationToken)
    {
        for (int i = 0; i < 4; i++)
        {
            DemoSupplier supplier = Company.Suppliers[random.Next(Company.Suppliers.Count)];
            DemoItem expense = Company.Expenses[random.Next(Company.Expenses.Count)];
            decimal quantity = 1m + random.Next(3);

            string number = Number("EXP", _settings.FiscalYear, month, ++_sequence);
            DateOnly issued = Day(month, 3 + random.Next(22));

            Result<PurchasingDocumentView> created = await _bills
                .CreateExpenseBillAsync(
                    Tenant,
                    Actor,
                    new ExpenseBillDraft(
                        number,
                        _supplierIds[supplier.Code],
                        issued,
                        supplier.ExpenseCategory,
                        Company.CostCentre,
                        [
                            new PurchaseLineDraft(
                                "SRV-" + supplier.Code,
                                "*",
                                new LocalizedName(expense.Arabic, expense.English),
                                quantity,
                                Money.Of(expense.UnitPrice, Currency),
                                "standard",
                                0.15m),
                        ]),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(created, "إصدار فاتورة المورد " + number);

            Result<PurchasingDocumentView> posted = await _bills
                .PostBillAsync(Tenant, Actor, created.Value.Id, cancellationToken)
                .ConfigureAwait(false);

            Ok(posted, "ترحيل فاتورة المورد " + number);
            _postedEntries++;

            _openBills.Add(new Open(created.Value.Id, number, supplier.Code, issued, created.Value.Totals.Gross.Amount));
        }
    }

    private async Task PaymentsAsync(int month, CancellationToken cancellationToken)
    {
        List<Open> due =
        [
            .. _openBills
                .Where(open => open.Issued.Month <= Math.Max(1, month - 1) && open.Remaining > 0m)
                .Take(2),
        ];

        foreach (Open bill in due)
        {
            string number = Number("PAY", _settings.FiscalYear, month, ++_sequence);

            Result<PurchasingDocumentView> created = await _payments
                .RecordPaymentAsync(
                    Tenant,
                    Actor,
                    new SupplierPaymentDraft(
                        number,
                        _supplierIds[bill.PartyCode],
                        Day(month, 25),
                        "bank",
                        Company.Bank,
                        Money.Of(bill.Remaining, Currency),
                        Money.Of(0m, Currency),
                        [new PayableAllocationDraft(bill.Id, Money.Of(bill.Remaining, Currency))]),
                    cancellationToken)
                .ConfigureAwait(false);

            Ok(created, "تسجيل سند الصرف " + number);

            Result<PurchasingDocumentView> posted = await _payments
                .PostPaymentAsync(Tenant, Actor, created.Value.Id, cancellationToken)
                .ConfigureAwait(false);

            Ok(posted, "ترحيل سند الصرف " + number);
            _postedEntries++;
            bill.Remaining = 0m;
        }
    }

    private DateOnly Day(int month, int day)
        => new(_settings.FiscalYear, month, Math.Min(day, DateTime.DaysInMonth(_settings.FiscalYear, month)));

    private static string Number(string prefix, int year, int month, int sequence)
        => FormattableString.Invariant($"{prefix}-{year:0000}{month:00}-{sequence:0000}");

    private static void Ok<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                what + " — رُفض: " + string.Join(" | ", result.Errors.Select(static error => error.ToString())));
        }
    }

    /// <summary>
    /// مستند قائم <b>برصيده المتبقّي</b> — لا بإجماليه.
    /// <para>
    /// والفرق ليس تجميلاً: تتبّعُ الإجمالي وحده يجعل البذر يخصّص على فاتورة سُدّدت،
    /// فيرفضها محرّك التخصيص بـ<c>over_allocation</c> — وهو رفضٌ <b>صحيح</b> كشف عيباً
    /// في البذر لا في المحرّك. الرصيد المتبقّي هنا هو ما يجعل البذر يوافق الواقع.
    /// </para>
    /// </summary>
    private sealed class Open(Guid id, string number, string partyCode, DateOnly issued, decimal gross)
    {
        public Guid Id { get; } = id;

        public string Number { get; } = number;

        public string PartyCode { get; } = partyCode;

        public DateOnly Issued { get; } = issued;

        public decimal Remaining { get; set; } = gross;
    }

    /// <summary>
    /// مولّد حتمي صغير (‏xorshift). <b>ولا <c>Random</c> بلا بذرة</b>: عرضان لنفس
    /// البناء يجب أن يحملا نفس الأرقام، وإلا صار «الرقم تغيّر» سؤالاً بلا جواب.
    /// </summary>
    private sealed class Deterministic(uint seed)
    {
        private uint _state = seed == 0 ? 1u : seed;

        public int Next(int exclusiveBound)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveBound);
        }
    }
}
