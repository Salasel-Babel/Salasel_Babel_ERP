using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.CapabilityProfile;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Sales.Tests;

/// <summary>
/// <b>بوابة القبول موصولة — لا مبنيّة وحدها.</b>
/// <para>
/// النواة تبني ملفّ قدرات مغلقاً مُطابَقاً بالمصفوفة، وتُنتج <c>AdmittedDocument</c>
/// الذي لا يُبنى إلا بالمرور من القبول (ADR-0023). ونوعٌ لا يطلبه أحد في توقيعه ليس
/// حارساً: قبل هذا التغيير لم تكن أي وحدة تستدعي البوابة، فكان الحقل الذي ترخّصه قدرة
/// مُطفأة يمرّ كأن الملفّ لا وجود له.
/// </para>
/// </summary>
[Collection("receivables")]
public sealed class CapabilityAdmissionTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Next(string prefix)
        => prefix + "-ADM-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · مستأجران · شيفرة واحدة · مصفوفة واحدة · صفّا ملفّ مختلفان
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏**هذا هو الإثبات.** المستأجران مبذوران في الدفتر بالكامل — الحسابات نفسها،
    // وخريطة الأدوار نفسها، والفترات نفسها — ولا يفترقان إلا في صفّ ملفّ القدرات.
    // فلو كان الرفض عند المُطفأ ناتجاً عن نقص في الحساب أو في المصفوفة لسقط عند
    // المُشغَّل أيضاً. والمستند المُقدَّم واحد حرفياً: العميل نفسه بالرمز نفسه،
    // والمبالغ نفسها، والاستدعاءات نفسها بالترتيب نفسه.
    [Fact]
    public async Task The_same_advance_is_refused_for_a_tenant_that_disabled_the_capability_and_accepted_for_one_that_enabled_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        TenantId enabled = SalesTestEnvironment.AdvanceEnabledTenant;
        TenantId disabled = SalesTestEnvironment.AdvanceDisabledTenant;

        // صفّا البيانات — وهما كل الفارق بين الحالتين.
        await _harness.SaveProfileAsync(enabled, advance: true, costOfSales: true, token);
        await _harness.SaveProfileAsync(disabled, advance: false, costOfSales: true, token);

        Attempt allowed = await AttemptAdvanceAsync(enabled, token);
        Attempt refused = await AttemptAdvanceAsync(disabled, token);

        // والقدرة الأخرى مُشغَّلة عند الاثنين: الرفض قدرةٌ بعينها لا وحدةٌ كاملة.
        Result<PostingReceipt> costEnabled = await CostOfSalesAsync(enabled, allowed.InvoiceId, token);
        Result<PostingReceipt> costDisabled = await CostOfSalesAsync(disabled, refused.InvoiceId, token);

        Proof.Require(
            allowed.Applied.IsSuccess
            && refused.Recorded.IsFailure
            && refused.Recorded.Errors[0].Code == "document_admission.capability_not_enabled"
            && refused.Recorded.Errors[0].MessageAr.Contains("advanceApplied", StringComparison.Ordinal)
            && costEnabled.IsSuccess
            && costDisabled.IsSuccess,
            "الدفعة المقدمة نفسها تمرّ عند مستأجر شغّل القدرة وتُرفض عند مستأجر أطفأها، والقدرة الأخرى تمرّ عند الاثنين",
            "المُشغَّل: الاستنفاد=" + Describe(allowed.Applied)
            + " · المُطفأ: التسجيل=" + Describe(refused.Recorded)
            + " · تكلفة المبيعات عند المُشغَّل=" + Describe(costEnabled)
            + " وعند المُطفأ=" + Describe(costDisabled));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · مستأجر بلا ملفّ قدرات: رفض لا فتح
    // ═══════════════════════════════════════════════════════════════════════
    //
    // الباب الذي يُلتفّ منه على البوابة كلّها لو فُتح: يكفي ألّا يُحفظ ملفّ لتمرّ
    // كل قدرة مُطفأة. والمستأجر هنا مبذور في الدفتر تماماً — فالرفض من الملفّ لا من نقص.
    [Fact]
    public async Task A_tenant_with_no_capability_profile_at_all_is_refused_not_opened()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // تجهيزة نظيفة بلا أي ملفّ محفوظ: المخزن هنا ملك هذا البند وحده.
        using Harness bare = await Harness.CreateWithoutProfilesAsync(token);

        Result<SalesDocumentView> recorded = await bare.Receipts.RecordAdvanceAsync(
            SalesTestEnvironment.AdvanceEnabledTenant,
            Harness.Actor,
            new CustomerAdvanceDraft(
                Next("ADV"), Guid.CreateVersion7(), March, "bank", "BANK-01", Harness.Sar(100m), Harness.Sar(0m), false),
            token);

        Result<PostingReceipt> cost = await bare.Invoices.PostCostOfSalesAsync(
            SalesTestEnvironment.AdvanceEnabledTenant,
            Harness.Actor,
            Guid.CreateVersion7(),
            new CostOfSalesDraft("ITEM-X", "WH-01", "*", 10m),
            token);

        ValidatedCapabilityProfile? stored = await bare.Profiles
            .FindAsync(SalesTestEnvironment.AdvanceEnabledTenant, token);

        Proof.Require(
            stored is null
            && recorded.IsFailure
            && recorded.Errors[0].Code == "sales.capability_profile_missing"
            && cost.IsFailure
            && cost.Errors[0].Code == "sales.capability_profile_missing",
            "غياب الملفّ رفضٌ لا فتح — على المسارين معاً",
            "الملفّ المخزَّن=" + (stored is null ? "(لا شيء)" : "موجود!")
            + " · تسجيل الدفعة=" + Describe(recorded)
            + " · تكلفة المبيعات=" + Describe(cost));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · تذكرة مستند آخر ليست تذكرة هذا المستند
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏<c>AdmittedDocument</c> في التوقيع يمنع «نسيان الفحص»، ولا يمنع وحده تمرير
    // قبولٍ نُشئ لشيء آخر. وهذا البند يُثبت أن الفحص الثاني — «هل يغطّي هذا القبول
    // الحقل الذي أمارسه؟» — ليس زخرفاً: قبولٌ حقيقي تماماً، صادر عن ملفّ صالح، يُرفض
    // لأنه لا يحمل الحقل.
    [Fact]
    public async Task An_admission_issued_for_another_field_does_not_open_this_path()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = SalesTestEnvironment.AdvanceEnabledTenant;

        await _harness.SaveProfileAsync(tenant, advance: true, costOfSales: true, token);

        ValidatedCapabilityProfile profile = (await _harness.Profiles.FindAsync(tenant, token))!;

        // قبولان حقيقيان من الملفّ نفسه: أحدهما يحمل حقل الدفعة والآخر يحمل المستودع.
        Result<AdmittedDocument> advanceTicket = profile.Admit(new DocumentSubmission(
            new DocumentTypeCode("sales.invoice"), ["customer", "lines", "advanceApplied"]));

        Result<AdmittedDocument> warehouseTicket = profile.Admit(new DocumentSubmission(
            new DocumentTypeCode("sales.invoice"), ["customer", "lines", "warehouse"]));

        Result covers = SalesAdmissionProbe.EnsureCovers(warehouseTicket.Value, "advanceApplied");
        Result coversItself = SalesAdmissionProbe.EnsureCovers(advanceTicket.Value, "advanceApplied");

        Proof.Require(
            advanceTicket.IsSuccess
            && warehouseTicket.IsSuccess
            && covers.IsFailure
            && covers.Errors[0].Code == "sales.admission_does_not_cover_field"
            && coversItself.IsSuccess,
            "قبولٌ صادرٌ لحقل آخر لا يفتح هذا المسار، والقبول الصحيح يفتحه",
            "تذكرة المستودع على حقل الدفعة=" + (covers.IsFailure ? covers.Errors[0].Code : "(قُبلت!)")
            + " · تذكرة الدفعة على حقل الدفعة=" + (coversItself.IsSuccess ? "قُبلت" : "رُفضت!"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · لا باب غير محروس — انتقل إلى اختبارات المعمارية
    // ═══════════════════════════════════════════════════════════════════════
    //
    // ‏**الحارس البنيوي** — الماسح الذي يقرأ اللغة الوسيطة ويسأل «أي نقطة دخول تبلغ
    // حدثاً تفتحه قدرة ولا تبلغ القبول؟» — كان هنا، وكان يقرأ تجميعة المبيعات وحدها
    // لأنها التجميعة الوحيدة التي يراها هذا المشروع. فكانت جملته «لا باب غير محروس»
    // جملةً عن وحدة واحدة، ولم يكن فيها ما يمتدّ إلى المشتريات عند ربط بوابتها.
    //
    // وقد انتقل بلا نقصان إلى:
    //   tests/Babel.ArchitectureTests/Support/AdmissionScan.cs
    //   tests/Babel.ArchitectureTests/CapabilityAdmissionIsReachedFromEveryGatedEntryPoint.cs
    //
    // وهناك يقرأ **المبيعات والمشتريات معاً**، ومعه شاهدٌ موجب: خدمةُ تطبيقٍ تخالف عمداً
    // في تجميعة الاختبار، ويجب أن يُبلّغ عنها — فلا يُلتبس «صفر مخالفات» بـ«لم يُفحص شيء».
    // والبنود الثلاثة أعلاه تبقى هنا لأنها تحتاج قاعدة بيانات ودفتراً حقيقيين.

    private sealed record Attempt(Result<SalesDocumentView> Recorded, Result<PostingReceipt> Applied, Guid InvoiceId);

    /// <summary>يبني الحالة نفسها حرفياً لكل مستأجر، ويُعيد نتيجة كل خطوة.</summary>
    private async Task<Attempt> AttemptAdvanceAsync(TenantId tenant, CancellationToken token)
    {
        string code = Next("CUS");

        Result<CustomerView> customer = await _harness.Customers.CreateAsync(
            tenant,
            Harness.Actor,
            new CustomerDraft(code, new LocalizedName("عميل " + code, "Customer " + code), Harness.Sar(0m), 30),
            token);
        Assert.True(customer.IsSuccess, Describe(customer));

        Result<SalesDocumentView> invoice = await _harness.Invoices.CreateInvoiceAsync(
            tenant,
            Harness.Actor,
            new SalesDocumentDraft(Next("INV"), customer.Value.Id, March, "BR-01", [Harness.Line(1m, 800m)]),
            null,
            token);
        Assert.True(invoice.IsSuccess, Describe(invoice));

        Result<SalesDocumentView> posted = await _harness.Invoices
            .PostInvoiceAsync(tenant, Harness.Actor, invoice.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted));

        Result<SalesDocumentView> recorded = await _harness.Receipts.RecordAdvanceAsync(
            tenant,
            Harness.Actor,
            new CustomerAdvanceDraft(
                Next("ADV"), customer.Value.Id, March, "bank", "BANK-01", Harness.Sar(300m), Harness.Sar(0m), false),
            token);

        if (recorded.IsFailure)
        {
            return new Attempt(recorded, Result<PostingReceipt>.Failure(recorded.Errors), invoice.Value.Id);
        }

        Result<SalesDocumentView> postedAdvance = await _harness.Receipts
            .PostAdvanceAsync(tenant, Harness.Actor, recorded.Value.Id, token);
        Assert.True(postedAdvance.IsSuccess, Describe(postedAdvance));

        Result<PostingReceipt> applied = await _harness.Receipts.ApplyAdvanceAsync(
            tenant, Harness.Actor, recorded.Value.Id, invoice.Value.Id, Harness.Sar(300m), token);

        return new Attempt(recorded, applied, invoice.Value.Id);
    }

    private Task<Result<PostingReceipt>> CostOfSalesAsync(TenantId tenant, Guid invoiceId, CancellationToken token)
        => _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoiceId,
            new CostOfSalesDraft(Next("ITEM"), "WH-01", "*", 120m),
            token).AsTask();

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.Code));
}

/// <summary>
/// مسبار يبلغ <c>SalesAdmission</c> الداخلية — الاختبار يفحص ما يفحصه الإنتاج نفسه،
/// لا نسخةً منه.
/// </summary>
internal static class SalesAdmissionProbe
{
    public static Result EnsureCovers(AdmittedDocument admitted, string field)
        => SalesAdmission.EnsureCovers(admitted, field);
}
