using Babel.Tests.Shared;
using System.Collections.Immutable;
using Babel.Contracts.Capture;
using Babel.Contracts.Posting;
using Babel.Core.CapabilityProfile;
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
        Profiles = new InMemoryCapabilityProfileStore();
        Valuation = new UnitCostOfOne();
        Posting = new PostingService(enforcer, ledger);
        Suppliers = new SupplierService(enforcer, runtime);
        Orders = new PurchaseOrderService(enforcer, runtime);
        Receipts = new GoodsReceiptService(enforcer, runtime, Posting, Profiles, Valuation);
        Bills = new SupplierBillService(enforcer, runtime, Posting, Profiles, Valuation);
        Payments = new SupplierPaymentService(enforcer, runtime, Posting, Profiles);
        Promotion = new PurchasingCapturedInvoiceReceiver(Suppliers, Bills);
        Payables = new PayablesService(
            enforcer, runtime, new LedgerControlPointReader(PurchasingTestEnvironment.Ledger.AppConnectionString));
        Gateway = new SubledgerPostingGateway(runtime.Database, Posting, runtime.CostCenters);
    }

    public PurchasingRuntime Runtime { get; }

    /// <summary>مخزن ملفّات القدرات — بوابة القبول تقرأ منه (‏ADR-0023).</summary>
    /// <summary>
    /// حدّ التقييم البديل لهذه التجهيزة — انظر <see cref="UnitCostOfOne"/>.
    /// <para>
    /// المطابقة الحقيقية بين الاستلام ودفتر المخزون المساعد مُثبَتة على مخزون حقيقي
    /// في <c>Babel.Inventory.Tests</c>؛ وهنا يُقاس ما تقيسه المشتريات: الذمّة الدائنة
    /// وهوية الترحيل والمطابقة الثلاثية.
    /// </para>
    /// </summary>
    public UnitCostOfOne Valuation { get; }

    public InMemoryCapabilityProfileStore Profiles { get; }

    /// <summary>
    /// مستقبِل الفاتورة الملتقَطة — <b>تنفيذ المنفذ الذي يعيش في العقود</b>.
    /// وهو مبنيّ على خدمتَي الوحدة نفسيهما، لا على منفذ ثانٍ إلى القاعدة.
    /// </summary>
    public ICapturedInvoiceReceiver Promotion { get; }

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
        return await WithProfilesAsync(seedProfiles: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>تجهيزة بلا أي ملفّ قدرات محفوظ — غياب الملفّ رفضٌ لا فتح.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Harness> CreateWithoutProfilesAsync(CancellationToken cancellationToken = default)
    {
        await PurchasingTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(PurchasingTestEnvironment.Ledger);
        }

        return await WithProfilesAsync(seedProfiles: false, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Harness> WithProfilesAsync(bool seedProfiles, CancellationToken cancellationToken)
    {
        Harness harness = new(
            new PurchasingRuntime(
                PurchasingTestEnvironment.Purchasing,
                FoundedTenants.ResolverFor(PurchasingTestEnvironment.AllTenants)),
            _ledger!);

        // المستأجرون القدماء بكل القدرات مُشغَّلة: هذه التجهيزة تُعيد إنتاج ما كان قائماً
        // قبل ربط البوابة، فلا يتحوّل ربطُ حارسٍ إلى تغييرٍ في معنى الاختبارات القائمة.
        // والبنود التي تُثبت الحارس تكتب ملفّاتها بنفسها.
        foreach (TenantId tenant in seedProfiles ? PurchasingTestEnvironment.AllTenants : [])
        {
            await harness.SaveProfileAsync(tenant, threeWayMatch: true, landedCost: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return harness;
    }

    /// <summary>
    /// يكتب ملفّ قدرات المستأجر لنوع <c>purchasing.supplier_bill</c>.
    /// <para>
    /// ويمرّ بـ<see cref="ValidatedCapabilityProfile.Create"/> نفسها التي يمرّ بها الإنتاج:
    /// ملفٌّ لم يُطابَق بالمصفوفة لا يدخل المخزن أصلاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="threeWayMatch">قدرة المطابقة الثلاثية.</param>
    /// <param name="landedCost">قدرة تكاليف الاستيراد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task SaveProfileAsync(
        TenantId tenant,
        bool threeWayMatch,
        bool landedCost,
        CancellationToken cancellationToken = default)
    {
        CapabilityProfileDraft draft = new(
            new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
            {
                ["purchasing.supplier_bill"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["three_way_match"] = threeWayMatch,
                        ["landed_cost"] = landedCost,
                    },
                    ImmutableSortedDictionary<string, string>.Empty),
            });

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(draft, EmbeddedPostingEventDirectory.Default);

        if (profile.IsFailure)
        {
            throw new InvalidOperationException(
                "تعذّر بناء ملفّ قدرات صالح: " + string.Join(" | ", profile.Errors.Select(static e => e.ToString())));
        }

        await Profiles.SaveAsync(tenant, profile.Value, cancellationToken).ConfigureAwait(false);
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
            Babel.Contracts.Inventory.InventoryUnits.Each,
            Money.Of(unitPrice, CurrencyCode.Sar),
            "standard",
            taxRate,
            recoverable);

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    public void Dispose() => Runtime.Dispose();
}
