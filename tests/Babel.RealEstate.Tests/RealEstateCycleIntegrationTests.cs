using System.Globalization;
using Babel.Contracts.RealEstate;
using Babel.RealEstate.Application;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.RealEstate.Tests;

/// <summary>
/// <b>دورة عقارية واحدة كاملة، مُثبَتة على PostgreSQL حقيقية ودفتر أستاذ حقيقي.</b>
/// <para>
/// العقار ⇒ الوحدة ⇒ المستأجر ⇒ العقد ⇒ جدول الدفعات ⇒ التفعيل ⇒ الفاتورة ⇒ ترحيلها ⇒
/// التحصيل ⇒ ترحيله ⇒ أعمار المتأخرات ومطابقتها بنقطة ضبطها. وكل بند يُطبع بحكمه
/// وبالدليل الذي أنتجه.
/// </para>
/// <para>
/// <b>ولا نسبة نظامية في هذا الملفّ.</b> نسبة الضريبة المستعملة في الإثبات هي
/// <c>1.0000</c> — <b>قيمة مسبار</b> تجعل الضريبة تساوي الصافي بالضبط فيُقرأ أثر النسبة
/// في القيد بلا التباس، وهي عمداً ليست رقماً يمكن أن يُقرأ ادّعاءً عن أي نظام ضريبي.
/// والنسبة تعبر من الطلب في كل الأحوال ولا تُكتب في شيفرة الوحدة.
/// </para>
/// </summary>
[Collection("realestate")]
public sealed class RealEstateCycleIntegrationTests
{
    /// <summary>
    /// نسبة مسبار لا نسبة سارية — انظر شرح الصنف. اختيارها <c>1</c> يجعل كل ريال ضريبة
    /// يقابل ريال صافٍ، فأي خلط بين المبلغين يظهر فوراً.
    /// </summary>
    private const decimal ProbeTaxRate = 1.0000m;

    private const string TenantsControl = "1310";
    private const string DeferredRentalIncome = "2171";
    private const string OutputVat = "2131";
    private const string OwnerTrustPayable = "2191";
    private const string VatForOwner = "2193";
    private const string UnallocatedCollections = "2192";
    private const string BankAccount = "1201";
    private const string RentalRevenue = "4301";

    [Fact]
    public async Task OwnPropertyCycleRunsEndToEndAndReconcilesToItsControlPoint()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = RealEstateTestEnvironment.Tenant;

        // ── ١ · المستأجر والعقار والوحدة ──────────────────────────────────────
        PartyView lessee = await LesseeAsync(harness, tenant, "LSE-OWN-1", token).ConfigureAwait(true);

        Result<PropertyView> property = await harness.Properties
            .CreatePropertyAsync(
                tenant, Harness.Actor, tenant.Value,
                new PropertyDraft(
                    "PRP-OWN-1",
                    new TranslatedName("برج الملكية الذاتية", new Dictionary<string, string> { ["en"] = "Own Property Tower" }),
                    PropertyOwnershipModels.OwnProperty,
                    OwnerId: null),
                token)
            .ConfigureAwait(true);

        Proof.Require(property.IsSuccess, "إنشاء العقار ينجح ويسجّل بُعده", Describe(property));

        string? registered = await OwnershipInLedgerAsync(tenant.Value, "PRP-OWN-1", token).ConfigureAwait(true);
        Proof.Require(
            registered == PropertyOwnershipModels.OwnProperty,
            "صفّ العقار مكتوبٌ في ledger.property_dimension في العملية نفسها",
            "ownership_model = " + (registered ?? "«غائب»"));

        Result<UnitView> unit = await harness.Properties
            .CreateUnitAsync(
                tenant, Harness.Actor, tenant.Value, property.Value.Id,
                new UnitDraft("UNT-OWN-1", new TranslatedName("مكتب ١"), "commercial", "standard"),
                token)
            .ConfigureAwait(true);

        Proof.Require(unit.IsSuccess, "إنشاء الوحدة تحت عقارها ينجح", Describe(unit));

        // ── ٢ · العقد وجدول دفعاته ────────────────────────────────────────────
        // ‏**والأقساط مصرَّحة**: النظام لا يوزّع قيمة العقد — التوزيع سياسةُ تقريبٍ
        // يملكها المالك — بل يفحص أن مجموعها يساوي القيمة بالضبط.
        List<InstalmentDraft> instalments =
        [
            new(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), new DateOnly(2026, 3, 1), Harness.Sar(3000m)),
            new(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), new DateOnly(2026, 4, 1), Harness.Sar(3000m)),
        ];

        Result<LeaseView> mismatched = await harness.Leases
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new LeaseDraft("LSE-OWN-BAD", unit.Value.Id, lessee.Id,
                    new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 30), Harness.Sar(5999m), instalments),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            mismatched.IsFailure && mismatched.Errors[0].Code == "realestate.instalments_do_not_sum_to_the_contract",
            "أقساطٌ لا تجمع قيمة العقد تُرفض ولا تُصلَح — سياسة التقريب قرار مالك",
            Describe(mismatched));

        Result<LeaseView> lease = await harness.Leases
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new LeaseDraft("LSE-OWN-1", unit.Value.Id, lessee.Id,
                    new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 30), Harness.Sar(6000m), instalments),
                token)
            .ConfigureAwait(true);

        Proof.Require(lease.IsSuccess, "إنشاء العقد مسوّدةً ينجح", Describe(lease));

        Result<IReadOnlyList<ScheduleLineView>> schedule = await harness.Leases
            .ReadScheduleAsync(tenant, Harness.Actor, tenant.Value, lease.Value.Id, token).ConfigureAwait(true);

        Proof.Require(
            schedule.IsSuccess && schedule.Value.Count == 2,
            "جدول الدفعات يُنشر بمعرّفات سطوره — وهي مدخل الفوترة",
            "عدد الأقساط = " + schedule.Value.Count.ToString(CultureInfo.InvariantCulture));

        Result<RentInvoiceView> beforeActivation = await harness.Invoices
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new RentInvoiceDraft("RIV-OWN-0", lease.Value.Id, new DateOnly(2026, 3, 1),
                    [schedule.Value[0].Id], ProbeTaxRate),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            beforeActivation.IsFailure && beforeActivation.Errors[0].Code == "realestate.lease_is_not_active",
            "الفوترة على عقدٍ لم يُفعَّل تُرفض",
            Describe(beforeActivation));

        Result<LeaseView> activated = await harness.Leases
            .ActivateAsync(tenant, Harness.Actor, tenant.Value, lease.Value.Id, token).ConfigureAwait(true);

        Proof.Require(
            activated.IsSuccess && activated.Value.State == "ACTIVE",
            "التفعيل فعلٌ مستقلّ يجعل الجدول قابلاً للفوترة ولا يُرحّل قيداً",
            Describe(activated));

        // ── ٣ · الفاتورة وترحيلها ─────────────────────────────────────────────
        Result<RentInvoiceView> invoice = await harness.Invoices
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new RentInvoiceDraft("RIV-OWN-1", lease.Value.Id, new DateOnly(2026, 3, 1),
                    [schedule.Value[0].Id], ProbeTaxRate),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            invoice.IsSuccess
            && invoice.Value.EventCode == "realestate.rent_invoice.own_property"
            && invoice.Value.Net.Amount == 3000m
            && invoice.Value.Tax.Amount == 3000m,
            "الوحدة تختار حدث الملكية الذاتية من السجلّ، وتحسب الضريبة من نسبةٍ وصلت مع الطلب",
            Describe(invoice) + " · صافٍ " + Proof.Money(invoice.Value.Net.Amount)
            + " · ضريبة " + Proof.Money(invoice.Value.Tax.Amount));

        Result<RentInvoiceView> posted = await harness.Invoices
            .PostAsync(tenant, Harness.Actor, tenant.Value, invoice.Value.Id, token).ConfigureAwait(true);

        Proof.Require(posted.IsSuccess && posted.Value.EntryId is not null, "ترحيل الفاتورة ينجح", Describe(posted));

        Dictionary<string, (decimal Debit, decimal Credit)> lines =
            await EntryLinesAsync(tenant.Value, posted.Value.EntryId!.Value, token).ConfigureAwait(true);

        Proof.Require(
            lines[TenantsControl].Debit == 6000m
            && lines[DeferredRentalIncome].Credit == 3000m
            && lines[OutputVat].Credit == 3000m
            && !lines.ContainsKey(OwnerTrustPayable)
            && !lines.ContainsKey(RentalRevenue),
            "قيد الملكية الذاتية: مدين ذمم المستأجرين، ودائن إيراد إيجار مؤجَّل وضريبة مخرجات — ولا أمانات ملاك",
            Format(lines));

        Result<RentInvoiceView> again = await harness.Invoices
            .PostAsync(tenant, Harness.Actor, tenant.Value, invoice.Value.Id, token).ConfigureAwait(true);

        Proof.Require(
            again.IsSuccess && again.Value.AlreadyPosted && again.Value.EntryId == posted.Value.EntryId,
            "الوصول الثاني بالهوية نفسها لا يُنشئ قيداً ثانياً ويُرجع معرّف القيد نفسه",
            "alreadyPosted = " + again.Value.AlreadyPosted.ToString(CultureInfo.InvariantCulture)
            + " · القيد " + again.Value.EntryId!.Value.ToString("D", CultureInfo.InvariantCulture));

        Proof.Require(
            await EntryCountAsync(tenant.Value, "realestate.rent_invoice", invoice.Value.Id, token).ConfigureAwait(true) == 1,
            "قيدٌ واحد في الدفتر لهذا المستند مهما تكرّر النداء",
            "عدد القيود = 1");

        Result<RentInvoiceView> twice = await harness.Invoices
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new RentInvoiceDraft("RIV-OWN-2", lease.Value.Id, new DateOnly(2026, 3, 1),
                    [schedule.Value[0].Id], ProbeTaxRate),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            twice.IsFailure && twice.Errors[0].Code == "realestate.schedule_line_already_invoiced",
            "القسط الواحد لا يُفوتَر مرّتين",
            Describe(twice));

        // ── ٤ · التحصيل ───────────────────────────────────────────────────────
        Result<TenantReceiptView> receipt = await harness.Receipts
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new TenantReceiptDraft("RCP-OWN-1", lessee.Id, new DateOnly(2026, 3, 10), "bank", "BNK-1", Harness.Sar(2000m)),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            receipt.IsSuccess && receipt.Value.EventCode == "realestate.collection.received",
            "سندٌ بمرجعٍ معلوم يختار حدث التحصيل المخصَّص",
            Describe(receipt));

        Result<TenantReceiptView> postedReceipt = await harness.Receipts
            .PostAsync(tenant, Harness.Actor, tenant.Value, receipt.Value.Id, token).ConfigureAwait(true);

        Proof.Require(postedReceipt.IsSuccess, "ترحيل سند القبض ينجح", Describe(postedReceipt));

        Dictionary<string, (decimal Debit, decimal Credit)> collection =
            await EntryLinesAsync(tenant.Value, postedReceipt.Value.EntryId!.Value, token).ConfigureAwait(true);

        Proof.Require(
            collection[BankAccount].Debit == 2000m && collection[TenantsControl].Credit == 2000m,
            "قيد التحصيل: مدين البنك ودائن ذمم المستأجرين",
            Format(collection));

        // ── ٥ · الأعمار والمطابقة ─────────────────────────────────────────────
        Result<(ArrearsReport Aging, Babel.RealEstate.Subledger.ControlReconciliationReport Reconciliation)> arrears =
            await harness.Arrears
                .AgingAsync(tenant, Harness.Actor, tenant.Value, new DateOnly(2026, 4, 15), token)
                .ConfigureAwait(true);

        Proof.Require(arrears.IsSuccess, "قراءة الأعمار تنجح", Describe(arrears));

        Proof.Require(
            arrears.Value.Aging.Totals.Total.Amount == 4000m,
            "المتأخرات = المفوتر ناقص المحصَّل بالضبط",
            Proof.Money(arrears.Value.Aging.Totals.Total.Amount));

        Proof.Require(
            arrears.Value.Reconciliation.IsReconciled
            && arrears.Value.Reconciliation.Divergence.Amount == 0m,
            "الدفتر المساعد للمستأجرين يطابق نقطة ضبطه بالضبط — لا «قريباً من الصفر»",
            "نقطة الضبط " + Proof.Money(arrears.Value.Reconciliation.ControlTotal.Amount)
            + " · الدفتر المساعد " + Proof.Money(arrears.Value.Reconciliation.SubledgerTotal.Amount));
    }

    [Fact]
    public async Task TheRegisteredOwnershipModelDecidesWhichAccountTheRentIsCreditedTo()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = RealEstateTestEnvironment.ManagedTenant;

        PartyView lessee = await LesseeAsync(harness, tenant, "LSE-MNG-1", token).ConfigureAwait(true);

        Result<PartyView> owner = await harness.Parties
            .CreateOwnerAsync(
                tenant, Harness.Actor, tenant.Value,
                new PartyDraft("OWN-MNG-1", new TranslatedName("مالك مسجَّل"), "300000000000003", "resident"),
                token)
            .ConfigureAwait(true);

        Proof.Require(owner.IsSuccess, "تسجيل المالك ينجح", Describe(owner));

        Result<PropertyView> orphan = await harness.Properties
            .CreatePropertyAsync(
                tenant, Harness.Actor, tenant.Value,
                new PropertyDraft("PRP-MNG-BAD", new TranslatedName("عقار مُدار بلا مالك"),
                    PropertyOwnershipModels.ManagedForOthers, OwnerId: null),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            orphan.IsFailure && orphan.Errors[0].Code == "realestate.managed_property_needs_an_owner",
            "عقارٌ مُدار بلا مالك يُرفض عند الإنشاء لا عند الترحيل",
            Describe(orphan));

        Result<PropertyView> property = await harness.Properties
            .CreatePropertyAsync(
                tenant, Harness.Actor, tenant.Value,
                new PropertyDraft("PRP-MNG-1", new TranslatedName("برج مُدار"),
                    PropertyOwnershipModels.ManagedForOthers, owner.Value.Id),
                token)
            .ConfigureAwait(true);

        Proof.Require(property.IsSuccess, "إنشاء العقار المُدار بمالكه ينجح", Describe(property));

        Proof.Require(
            property.Value.OwnerShareNumerator == 1 && property.Value.OwnerShareDenominator == 1,
            "الحصّة كسرٌ ببسطٍ ومقام من اليوم — والمفتاح يحتمل الحصص بلا هجرة",
            property.Value.OwnerShareNumerator.ToString(CultureInfo.InvariantCulture)
            + "/" + property.Value.OwnerShareDenominator.ToString(CultureInfo.InvariantCulture));

        Result<UnitView> unit = await harness.Properties
            .CreateUnitAsync(
                tenant, Harness.Actor, tenant.Value, property.Value.Id,
                new UnitDraft("UNT-MNG-1", new TranslatedName("محل ١"), "commercial", "standard"),
                token)
            .ConfigureAwait(true);

        Proof.Require(unit.IsSuccess, "إنشاء الوحدة ينجح", Describe(unit));

        List<InstalmentDraft> instalments =
        [
            new(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), new DateOnly(2026, 5, 1), Harness.Sar(4000m)),
        ];

        Result<LeaseView> lease = await harness.Leases
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new LeaseDraft("LSE-MNG-1", unit.Value.Id, lessee.Id,
                    new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), Harness.Sar(4000m), instalments),
                token)
            .ConfigureAwait(true);

        Proof.Require(lease.IsSuccess, "إنشاء العقد ينجح", Describe(lease));

        await harness.Leases.ActivateAsync(tenant, Harness.Actor, tenant.Value, lease.Value.Id, token).ConfigureAwait(true);

        Result<IReadOnlyList<ScheduleLineView>> schedule = await harness.Leases
            .ReadScheduleAsync(tenant, Harness.Actor, tenant.Value, lease.Value.Id, token).ConfigureAwait(true);

        Result<RentInvoiceView> invoice = await harness.Invoices
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new RentInvoiceDraft("RIV-MNG-1", lease.Value.Id, new DateOnly(2026, 5, 1),
                    [schedule.Value[0].Id], ProbeTaxRate),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            invoice.IsSuccess && invoice.Value.EventCode == "realestate.rent_invoice.managed_property",
            "الوحدة تختار حدث الإدارة من نموذج الملكية المُسجَّل — لا من حقلٍ في الطلب",
            Describe(invoice));

        Result<RentInvoiceView> posted = await harness.Invoices
            .PostAsync(tenant, Harness.Actor, tenant.Value, invoice.Value.Id, token).ConfigureAwait(true);

        Proof.Require(posted.IsSuccess, "ترحيل الفاتورة المُدارة ينجح", Describe(posted));

        Dictionary<string, (decimal Debit, decimal Credit)> lines =
            await EntryLinesAsync(tenant.Value, posted.Value.EntryId!.Value, token).ConfigureAwait(true);

        Proof.Require(
            lines[TenantsControl].Debit == 8000m
            && lines[OwnerTrustPayable].Credit == 4000m
            && lines[VatForOwner].Credit == 4000m
            && !lines.ContainsKey(DeferredRentalIncome)
            && !lines.ContainsKey(RentalRevenue)
            && !lines.ContainsKey(OutputVat),
            "نموذج الإدارة يقلب دائن الفاتورة إلى أمانات الملاك — ولا إيراد إيجار ولا ضريبة مخرجات للشركة",
            Format(lines));
    }

    [Fact]
    public async Task TheLedgerRegisterIsTheAuthorityOnTheOwnershipModelAndItNeverChanges()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = RealEstateTestEnvironment.GatewayTenant;

        TranslatedName name = new("عقار الحصانة");

        Result first = await harness.Registrar
            .RegisterAsync(tenant, tenant.Value, "PRP-GATE-1", PropertyOwnershipModels.OwnProperty, name, token)
            .ConfigureAwait(true);

        Proof.Require(first.IsSuccess, "التسجيل الأول ينجح", Describe(first));

        Result repeated = await harness.Registrar
            .RegisterAsync(tenant, tenant.Value, "PRP-GATE-1", PropertyOwnershipModels.OwnProperty, name, token)
            .ConfigureAwait(true);

        Proof.Require(repeated.IsSuccess, "تسجيلٌ ثانٍ بالقيم نفسها لا يفعل شيئاً ولا يُعدّ خطأ", Describe(repeated));

        Result moved = await harness.Registrar
            .RegisterAsync(tenant, tenant.Value, "PRP-GATE-1", PropertyOwnershipModels.ManagedForOthers, name, token)
            .ConfigureAwait(true);

        Proof.Require(
            moved.IsFailure && moved.Errors[0].Code == "ledger.property_dimension.ownership_model_is_immutable",
            "تغيير نموذج الملكية بعد التسجيل مرفوض — وهو نقلُ عقارٍ لا تعديلُ حقل",
            Describe(moved));

        Result unknown = await harness.Registrar
            .RegisterAsync(tenant, tenant.Value, "PRP-GATE-2", "guaranteed_rent", name, token)
            .ConfigureAwait(true);

        Proof.Require(
            unknown.IsFailure && unknown.Errors[0].Code == "ledger.property_dimension.unknown_ownership_model",
            "نموذج ملكية ثالث لا وجود له في البيانات يُرفض ولا يُخمَّن",
            Describe(unknown));
    }

    [Fact]
    public async Task AnUnallocatedCollectionIsAllocatedByASecondEntryNotByAReversal()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = RealEstateTestEnvironment.GatewayTenant;

        PartyView lessee = await LesseeAsync(harness, tenant, "LSE-GATE-1", token).ConfigureAwait(true);

        Result<TenantReceiptView> receipt = await harness.Receipts
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new TenantReceiptDraft("RCP-GATE-1", LesseeId: null, new DateOnly(2026, 6, 5), "bank", "BNK-9", Harness.Sar(1500m)),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            receipt.IsSuccess && receipt.Value.EventCode == "realestate.collection.unallocated",
            "مبلغٌ بلا مرجع يختار حدث التحصيل غير المخصَّص — ولا يُنسب إلى أحد بالتخمين",
            Describe(receipt));

        Result<TenantReceiptView> posted = await harness.Receipts
            .PostAsync(tenant, Harness.Actor, tenant.Value, receipt.Value.Id, token).ConfigureAwait(true);

        Proof.Require(posted.IsSuccess, "ترحيل التحصيل غير المخصَّص ينجح", Describe(posted));

        Dictionary<string, (decimal Debit, decimal Credit)> collection =
            await EntryLinesAsync(tenant.Value, posted.Value.EntryId!.Value, token).ConfigureAwait(true);

        Proof.Require(
            collection[BankAccount].Debit == 1500m && collection[UnallocatedCollections].Credit == 1500m,
            "قيد التحصيل غير المخصَّص: مدين البنك ودائن التحصيلات غير المخصَّصة",
            Format(collection));

        Result<TenantReceiptView> allocated = await harness.Receipts
            .AllocateAsync(tenant, Harness.Actor, tenant.Value, receipt.Value.Id, lessee.Id, token).ConfigureAwait(true);

        Proof.Require(
            allocated.IsSuccess && allocated.Value.AllocationEntryId is not null
            && allocated.Value.AllocationEntryId != posted.Value.EntryId,
            "التخصيص **قيدٌ مستقلّ** لا عكسٌ للقيد السابق — والمال وصل فعلاً فلا تُمحى واقعة وقعت",
            "قيد التحصيل " + posted.Value.EntryId!.Value.ToString("D", CultureInfo.InvariantCulture)
            + " · قيد التخصيص " + allocated.Value.AllocationEntryId!.Value.ToString("D", CultureInfo.InvariantCulture));

        Dictionary<string, (decimal Debit, decimal Credit)> allocation =
            await EntryLinesAsync(tenant.Value, allocated.Value.AllocationEntryId!.Value, token).ConfigureAwait(true);

        Proof.Require(
            allocation[UnallocatedCollections].Debit == 1500m && allocation[TenantsControl].Credit == 1500m,
            "قيد التخصيص ينقل من التحصيلات غير المخصَّصة إلى ذمم المستأجرين",
            Format(allocation));

        Proof.Require(
            await EntryCountAsync(tenant.Value, "realestate.tenant_receipt", receipt.Value.Id, token).ConfigureAwait(true) == 2,
            "قيدان على المستند نفسه لا يتصادمان — لأن رمز الحدث داخل هوية الترحيل",
            "عدد القيود = 2");

        Result<TenantReceiptView> twice = await harness.Receipts
            .AllocateAsync(tenant, Harness.Actor, tenant.Value, receipt.Value.Id, lessee.Id, token).ConfigureAwait(true);

        Proof.Require(
            twice.IsFailure && twice.Errors[0].Code == "realestate.receipt_is_already_allocated",
            "التخصيص يقع مرّة",
            Describe(twice));
    }

    [Fact]
    public async Task TwoLiveTermsOnTheSameUnitAreRefusedByTheDatabaseNotByTheService()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token).ConfigureAwait(true);
        TenantId tenant = RealEstateTestEnvironment.UnregisteredTenant;

        PartyView lessee = await LesseeAsync(harness, tenant, "LSE-EXC-1", token).ConfigureAwait(true);

        Result<PropertyView> property = await harness.Properties
            .CreatePropertyAsync(
                tenant, Harness.Actor, tenant.Value,
                new PropertyDraft("PRP-EXC-1", new TranslatedName("برج التداخل"),
                    PropertyOwnershipModels.OwnProperty, OwnerId: null),
                token)
            .ConfigureAwait(true);

        Result<UnitView> unit = await harness.Properties
            .CreateUnitAsync(
                tenant, Harness.Actor, tenant.Value, property.Value.Id,
                new UnitDraft("UNT-EXC-1", new TranslatedName("شقة ١"), "residential", "exempt"),
                token)
            .ConfigureAwait(true);

        Result<LeaseView> first = await harness.Leases
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new LeaseDraft("LSE-EXC-1", unit.Value.Id, lessee.Id,
                    new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), Harness.Sar(900m),
                    [new InstalmentDraft(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), Harness.Sar(900m))]),
                token)
            .ConfigureAwait(true);

        await harness.Leases.ActivateAsync(tenant, Harness.Actor, tenant.Value, first.Value.Id, token).ConfigureAwait(true);

        Result<LeaseView> overlapping = await harness.Leases
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new LeaseDraft("LSE-EXC-2", unit.Value.Id, lessee.Id,
                    new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), Harness.Sar(1200m),
                    [new InstalmentDraft(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 9, 1), Harness.Sar(1200m))]),
                token)
            .ConfigureAwait(true);

        Proof.Require(overlapping.IsSuccess, "المسوّدة المتداخلة تُقبل — القيد على المدّة السارية وحدها", Describe(overlapping));

        Result<LeaseView> refused = await harness.Leases
            .ActivateAsync(tenant, Harness.Actor, tenant.Value, overlapping.Value.Id, token).ConfigureAwait(true);

        Proof.Require(
            refused.IsFailure && refused.Errors[0].Code == "realestate.lease_term_overlaps",
            "تفعيل مدّة متداخلة يُرفض من **قيد الاستبعاد الزمني في قاعدة البيانات** برمز ثابت ورسالتين",
            Describe(refused));

        // ── الوحدة المعفاة: لا ضريبة، وعلامةٌ ظاهرة على رمز سبب الإعفاء الغائب ──
        Result<IReadOnlyList<ScheduleLineView>> schedule = await harness.Leases
            .ReadScheduleAsync(tenant, Harness.Actor, tenant.Value, first.Value.Id, token).ConfigureAwait(true);

        Result<RentInvoiceView> invoice = await harness.Invoices
            .DraftAsync(
                tenant, Harness.Actor, tenant.Value,
                new RentInvoiceDraft("RIV-EXC-1", first.Value.Id, new DateOnly(2026, 7, 1),
                    [schedule.Value[0].Id], ProbeTaxRate),
                token)
            .ConfigureAwait(true);

        Proof.Require(
            invoice.IsSuccess && invoice.Value.Tax.Amount == 0m && invoice.Value.ExemptionReasonCode.Length == 0,
            "الوحدة المعفاة لا تحمل ضريبة مهما كانت النسبة في الطلب، ورمز سبب الإعفاء يبقى فارغاً حتى يُعرف",
            "ضريبة " + Proof.Money(invoice.Value.Tax.Amount) + " · رمز الإعفاء «" + invoice.Value.ExemptionReasonCode + "»");
    }

    // ── أدوات القراءة من الدفتر ───────────────────────────────────────────────

    private static async Task<PartyView> LesseeAsync(Harness harness, TenantId tenant, string code, CancellationToken token)
    {
        Result<PartyView> created = await harness.Parties
            .CreateLesseeAsync(
                tenant, Harness.Actor, tenant.Value,
                new PartyDraft(code, new TranslatedName("مستأجر " + code), string.Empty, "resident"),
                token)
            .ConfigureAwait(false);

        return created.IsSuccess
            ? created.Value
            : throw new InvalidOperationException(created.Errors[0].ToString());
    }

    private static async Task<string?> OwnershipInLedgerAsync(Guid companyId, string propertyId, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(RealEstateTestEnvironment.Ledger.OwnerConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            "select ownership_model from ledger.property_dimension where company_id = $1 and property_id = $2",
            connection);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(propertyId);
        return (string?)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, (decimal Debit, decimal Credit)>> EntryLinesAsync(
        Guid companyId,
        Guid entryId,
        CancellationToken token)
    {
        Dictionary<string, (decimal Debit, decimal Credit)> lines = new(StringComparer.Ordinal);

        await using NpgsqlConnection connection = new(RealEstateTestEnvironment.Ledger.OwnerConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select account_code, sum(debit_company), sum(credit_company)
              from ledger.journal_line
             where company_id = $1 and entry_id = $2
             group by account_code
            """, connection);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(entryId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            lines[reader.GetString(0)] = (reader.GetDecimal(1), reader.GetDecimal(2));
        }

        return lines;
    }

    private static async Task<long> EntryCountAsync(
        Guid companyId,
        string documentType,
        Guid documentId,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = new(RealEstateTestEnvironment.Ledger.OwnerConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """
            select count(*) from ledger.journal_entry
             where company_id = $1 and source_doc_type = $2 and source_doc_id = $3
            """, connection);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(documentType);
        command.Parameters.AddWithValue(documentId.ToString("D", CultureInfo.InvariantCulture));
        return Convert.ToInt64(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static string Format(Dictionary<string, (decimal Debit, decimal Credit)> lines)
        => string.Join(" · ", lines
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => entry.Key + " مدين " + Proof.Money(entry.Value.Debit) + " دائن " + Proof.Money(entry.Value.Credit)));

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.Code));

    private static string Describe(Result result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.Code));
}
