using Babel.Tests.Shared;
using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.Core.CapabilityProfile;
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
        Profiles = new InMemoryCapabilityProfileStore();
        Customers = new CustomerService(enforcer, runtime);
        Valuation = new UnitCostOfOne();
        Invoices = new SalesInvoiceService(enforcer, runtime, Posting, Profiles, Valuation);
        CreditNotes = new CreditNoteService(enforcer, runtime, Posting);
        Receipts = new CustomerReceiptService(enforcer, runtime, Posting, Profiles);
        Receivables = new ReceivablesService(
            enforcer, runtime, new LedgerControlPointReader(SalesTestEnvironment.Ledger.AppConnectionString));
        Gateway = new SubledgerPostingGateway(runtime.Database, Posting, runtime.CostCenters);
    }

    /// <summary>
    /// مخزن ملفّات القدرات — <b>لهذه التجهيزة وحدها</b>، فكل بند يملك ملفّاته ولا يقرأ
    /// ما كتبه غيره.
    /// </summary>
    public InMemoryCapabilityProfileStore Profiles { get; }

    /// <summary>حدّ التقييم البديل لهذه التجهيزة — انظر <see cref="UnitCostOfOne"/>.</summary>
    public UnitCostOfOne Valuation { get; }

    public SalesRuntime Runtime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public CustomerService Customers { get; }

    public SalesInvoiceService Invoices { get; }

    public CreditNoteService CreditNotes { get; }

    public CustomerReceiptService Receipts { get; }

    public ReceivablesService Receivables { get; }

    /// <summary>
    /// بوابة الترحيل نفسها — الطريق الذي تسلكه كل خدمة في الوحدة.
    /// إثبات هوية الإحكام يمرّ من هنا لا بإدراج خام: الإدراج الخام يصيب الفهرس
    /// ويترك استعلام «هل رُحّل من قبل؟» بلا شاهد.
    /// </summary>
    public SubledgerPostingGateway Gateway { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000a1"));

    /// <summary>
    /// تجهيزة <b>بلا ملفّ قدرات محفوظ لأي مستأجر</b> — لإثبات أن غياب الملفّ رفضٌ لا فتح.
    /// </summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static Task<Harness> CreateWithoutProfilesAsync(CancellationToken cancellationToken = default)
        => BuildAsync(seedProfiles: false, cancellationToken);

    public static Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
        => BuildAsync(seedProfiles: true, cancellationToken);

    private static async Task<Harness> BuildAsync(bool seedProfiles, CancellationToken cancellationToken)
    {
        await SalesTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(SalesTestEnvironment.Ledger);
        }

        // المنشآت مؤسَّسة قبل أول ترحيل: البوّابة تسأل النواة عن مركز التكلفة، ومنشأةٌ
        // لم تُؤسَّس لا مركز لها أصلاً (ADR-0026).
        Harness harness = new(
            new SalesRuntime(SalesTestEnvironment.Sales, FoundedTenants.ResolverFor(SalesTestEnvironment.AllTenants)),
            _ledger);

        // المستأجرون الثلاثة القدماء بكل القدرات مُشغَّلة: هذه التجهيزة تُعيد إنتاج
        // ما كان قائماً قبل ربط البوابة، فلا يتحوّل ربطُ حارسٍ إلى تغييرٍ في معنى
        // الاختبارات القائمة. والبند الذي يُثبت الحارس يكتب ملفّيه بنفسه.
        foreach (TenantId tenant in seedProfiles
                     ? new[]
                     {
                         SalesTestEnvironment.Tenant,
                         SalesTestEnvironment.InjectedTenant,
                         SalesTestEnvironment.GatewayTenant,
                     }
                     : [])
        {
            await harness.SaveProfileAsync(tenant, advance: true, costOfSales: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return harness;
    }

    /// <summary>
    /// يكتب ملفّ قدرات المستأجر لنوع <c>sales.invoice</c> — قدرتان تُشغَّلان أو تُطفآن.
    /// <para>
    /// ويمرّ بـ<see cref="ValidatedCapabilityProfile.Create"/> نفسها التي يمرّ بها
    /// الإنتاج: ملفٌّ لم يُطابَق بالمصفوفة لا يدخل المخزن أصلاً، فلا يُثبت الاختبار
    /// شيئاً على ملفّ لا يمكن أن يوجد.
    /// </para>
    /// </summary>
    public async Task SaveProfileAsync(
        TenantId tenant,
        bool advance,
        bool costOfSales,
        CancellationToken cancellationToken = default)
    {
        CapabilityProfileDraft draft = new(
            new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
            {
                ["sales.invoice"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["advance"] = advance,
                        ["cost_of_sales"] = costOfSales,
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
