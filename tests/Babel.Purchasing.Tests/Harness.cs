using Babel.Contracts.Posting;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.Purchasing.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدة بيانات حقيقية وعدّاد ترقيم
/// حقيقياً ودفتراً مساعداً واحداً، وتوازيها يجعل «انحراف في المطابقة» تعني «اختباران
/// تسابقا» لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("payables", DisableParallelization = true)]
public sealed class PayablesTestGroup;

/// <summary>تركيب الاختبار بلا حاوية اعتماديات: كل الخدمات على نفس موارد الوحدة.</summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(PurchasingRuntime runtime, LedgerRuntime ledger)
    {
        Runtime = runtime;
        LedgerRuntime = ledger;
        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);
        Suppliers = new SupplierService(enforcer, runtime);
        Orders = new PurchaseOrderService(enforcer, runtime);
        Receipts = new GoodsReceiptService(enforcer, runtime, Posting);
        Bills = new SupplierBillService(enforcer, runtime, Posting);
        Payments = new SupplierPaymentService(enforcer, runtime, Posting);
        Payables = new PayablesService(
            enforcer, runtime, new LedgerControlPointReader(PurchasingTestEnvironment.Ledger.AppConnectionString));
        Gateway = new SubledgerPostingGateway(runtime.Database, Posting, runtime.CostCenters);
    }

    public PurchasingRuntime Runtime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public SupplierService Suppliers { get; }

    public PurchaseOrderService Orders { get; }

    public GoodsReceiptService Receipts { get; }

    public SupplierBillService Bills { get; }

    public SupplierPaymentService Payments { get; }

    public PayablesService Payables { get; }

    /// <summary>
    /// بوابة الترحيل نفسها — الطريق الذي تسلكه كل خدمة في الوحدة.
    /// إثبات هوية الإحكام يمرّ من هنا لا بإدراج خام: الإدراج الخام يصيب الفهرس
    /// ويترك استعلام «هل رُحّل من قبل؟» بلا شاهد.
    /// </summary>
    public SubledgerPostingGateway Gateway { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000b1"));

    public static async Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
    {
        await PurchasingTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(PurchasingTestEnvironment.Ledger);
        }

        // المنشآت مؤسَّسة قبل أول ترحيل: البوّابة تسأل النواة عن مركز التكلفة، ومنشأةٌ
        // لم تُؤسَّس لا مركز لها أصلاً (ADR-0026).
        return new Harness(
            new PurchasingRuntime(
                PurchasingTestEnvironment.Purchasing,
                FoundedTenants.ResolverFor(PurchasingTestEnvironment.AllTenants)),
            _ledger);
    }

    public async Task<Guid> SupplierAsync(string code, int termsDays = 30)
    {
        Result<SupplierView> created = await Suppliers
            .CreateAsync(
                PurchasingTestEnvironment.Tenant,
                Actor,
                new SupplierDraft(
                    code,
                    new LocalizedName("مورد " + code, "Supplier " + code),
                    Money.Of(0m, CurrencyCode.Sar),
                    termsDays),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Errors[0].ToString());
        }

        return created.Value.Id;
    }

    public static PurchaseLineDraft Line(string itemId, decimal quantity, decimal unitPrice, decimal taxRate = 0.15m, bool recoverable = true)
        => new(
            itemId,
            "*",
            new LocalizedName("صنف اختبار", "Test item"),
            quantity,
            Money.Of(unitPrice, CurrencyCode.Sar),
            "standard",
            taxRate,
            recoverable);

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    public void Dispose() => Runtime.Dispose();
}
