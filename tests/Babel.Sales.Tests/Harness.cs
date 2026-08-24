using Babel.Contracts.Posting;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدة بيانات حقيقية وعدّاد
/// ترقيم حقيقياً ودفتراً مساعداً واحداً، وتوازيها يجعل «انحراف في المطابقة» تعني
/// «اختباران تسابقا» لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("receivables", DisableParallelization = true)]
public sealed class ReceivablesTestGroup;

/// <summary>
/// تركيب الاختبار بلا حاوية اعتماديات: كل منشئ عام، وكل خدمة تأخذ نفس
/// <see cref="SalesRuntime"/> فتشترك في سياق واحد — وهو ما يفعله النطاق في الإنتاج.
/// </summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(SalesRuntime runtime, LedgerRuntime ledger)
    {
        Runtime = runtime;
        LedgerRuntime = ledger;
        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);
        Customers = new CustomerService(enforcer, runtime);
        Invoices = new SalesInvoiceService(enforcer, runtime, Posting);
        CreditNotes = new CreditNoteService(enforcer, runtime, Posting);
        Receipts = new CustomerReceiptService(enforcer, runtime, Posting);
        Receivables = new ReceivablesService(
            enforcer, runtime, new LedgerControlPointReader(SalesTestEnvironment.Ledger.AppConnectionString));
    }

    public SalesRuntime Runtime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public CustomerService Customers { get; }

    public SalesInvoiceService Invoices { get; }

    public CreditNoteService CreditNotes { get; }

    public CustomerReceiptService Receipts { get; }

    public ReceivablesService Receivables { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000a1"));

    public static async Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
    {
        await SalesTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(SalesTestEnvironment.Ledger);
        }

        return new Harness(new SalesRuntime(SalesTestEnvironment.Sales), _ledger);
    }

    public async Task<Guid> CustomerAsync(string code, decimal creditLimit = 0m, int termsDays = 30)
    {
        Result<CustomerView> created = await Customers
            .CreateAsync(
                SalesTestEnvironment.Tenant,
                Actor,
                new CustomerDraft(
                    code,
                    new LocalizedName("عميل " + code, "Customer " + code),
                    Money.Of(creditLimit, CurrencyCode.Sar),
                    termsDays),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Errors[0].ToString());
        }

        return created.Value.Id;
    }

    public static SalesLineDraft Line(decimal quantity, decimal unitPrice, decimal taxRate = 0.15m, decimal discount = 0m)
        => new(
            "*",
            new LocalizedName("صنف اختبار", "Test item"),
            quantity,
            Money.Of(unitPrice, CurrencyCode.Sar),
            Money.Of(discount, CurrencyCode.Sar),
            "standard",
            taxRate);

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    public void Dispose() => Runtime.Dispose();
}
