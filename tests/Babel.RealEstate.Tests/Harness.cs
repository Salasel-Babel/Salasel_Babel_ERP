using Babel.Contracts.Posting;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.RealEstate.Application;
using Babel.SharedKernel;
using Babel.Tests.Shared;
using Xunit;

namespace Babel.RealEstate.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك قاعدة بيانات حقيقية وعدّاد ترقيم
/// حقيقياً ودفتراً مساعداً واحداً، وتوازيها يجعل «انحراف في المطابقة» تعني «اختباران
/// تسابقا» لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("realestate", DisableParallelization = true)]
public sealed class RealEstateTestGroup;

/// <summary>
/// تركيب الاختبار بلا حاوية اعتماديات: كل منشئ عام، وكل خدمة تأخذ نفس
/// <see cref="RealEstateRuntime"/> فتشترك في سياق واحد — وهو ما يفعله النطاق في الإنتاج.
/// <para>
/// <b>ومنفذ تسجيل بُعد العقار يُبنى من التنفيذ الحقيقي في الدفتر لا من بديل</b>: المطلوب
/// إثباته هو أن الصفّ يُكتب في جدول الدفتر فعلاً، وأن الدفتر يقبل الكتابة عليه بصلاحيات
/// دور التطبيق. وبديلٌ في الذاكرة كان سيُثبت أن البديل يعمل.
/// </para>
/// </summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(RealEstateRuntime runtime, LedgerRuntime ledger)
    {
        Runtime = runtime;
        LedgerRuntime = ledger;
        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);
        Registrar = new Babel.Ledger.RealEstate.PropertyDimensionRegistrar(enforcer, ledger);
        Properties = new PropertyService(enforcer, runtime, Registrar);
        Parties = new PartyService(enforcer, runtime);
        Leases = new LeaseRegistrationService(enforcer, runtime);
        Invoices = new RentInvoiceService(enforcer, runtime, Posting);
        Receipts = new TenantReceiptService(enforcer, runtime, Posting);
        Arrears = new TenantArrearsService(
            enforcer, runtime, new LedgerControlPointReader(RealEstateTestEnvironment.Ledger.AppConnectionString));
    }

    public RealEstateRuntime Runtime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    /// <summary>التنفيذ الحقيقي للمنفذ — الكاتب الوحيد في سجلّ أبعاد العقار.</summary>
    public Babel.Contracts.RealEstate.IPropertyDimensionRegistrar Registrar { get; }

    public PropertyService Properties { get; }

    public PartyService Parties { get; }

    public LeaseRegistrationService Leases { get; }

    public RentInvoiceService Invoices { get; }

    public TenantReceiptService Receipts { get; }

    public TenantArrearsService Arrears { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000b1"));

    public static async Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
    {
        await RealEstateTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(RealEstateTestEnvironment.Ledger);
        }

        // المنشآت مؤسَّسة قبل أول ترحيل: البوّابة تسأل النواة عن مركز التكلفة، ومنشأةٌ
        // لم تُؤسَّس لا مركز لها أصلاً (ADR-0026).
        return new Harness(
            new RealEstateRuntime(
                RealEstateTestEnvironment.RealEstate,
                FoundedTenants.ResolverFor(RealEstateTestEnvironment.AllTenants)),
            _ledger);
    }

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    public void Dispose() => Runtime.Dispose();
}
