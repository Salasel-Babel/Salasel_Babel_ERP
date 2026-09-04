using Babel.RealEstate.Application;
using Babel.RealEstate.Subledger;
using Babel.SharedKernel;

namespace Babel.RealEstate.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة العقارات</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// <b>لماذا يوجد هذا الملفّ:</b> القاعدة 13 (البند ب) تمنع <c>Babel.Api</c> من ذكر أي نوع
/// في فضاء اسم داخلي لوحدة — و<c>Application</c> و<c>Persistence</c> منهما بالاسم. فالباب
/// الوحيد المشروع سطحٌ منشور مسمّى خارج فضاءات الداخل، بالشكل نفسه في المبيعات والمخزون.
/// </para>
/// <para>
/// <b>وما لا يفعله — عمداً:</b> لا يقرّر شيئاً محاسبياً، ولا يختار حدثاً، ولا يقرأ جدولاً.
/// كل دالّة هنا تترجم نوعاً منشوراً إلى مسوّدة الوحدة وتنادي خدمة التطبيق التي تحمل سمة
/// الاستحقاق وتنادي المنفِّذ أوّل شيء. وفحصٌ ثانٍ هنا كان سيكون آليةَ تصريحٍ موازية.
/// </para>
/// <para>
/// <b>والمال يعبر هذا الحدّ <c>decimal</c> لا <c>Money</c>:</b> ‏<c>Money</c> يحمل عملة،
/// وعملةُ المنشأة إعدادُ وحدةٍ لا معلومةُ نقل — فلو أخذ هذا السطح <c>Money</c> لاضطرّ
/// سطح HTTP أن <b>يختار عملة</b>، وهو قرار أعمال في طبقة نقل.
/// </para>
/// </summary>
public sealed class RealEstateSurface
{
    private readonly PropertyService _properties;
    private readonly PartyService _parties;
    private readonly LeaseRegistrationService _leases;
    private readonly RentInvoiceService _invoices;
    private readonly TenantReceiptService _receipts;
    private readonly TenantArrearsService _arrears;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="properties">خدمة العقارات والوحدات.</param>
    /// <param name="parties">خدمة الأطراف.</param>
    /// <param name="leases">خدمة قيود تسجيل العقود.</param>
    /// <param name="invoices">خدمة فواتير الإيجار.</param>
    /// <param name="receipts">خدمة التحصيل.</param>
    /// <param name="arrears">خدمة المتأخرات ومطابقتها.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public RealEstateSurface(
        PropertyService properties,
        PartyService parties,
        LeaseRegistrationService leases,
        RentInvoiceService invoices,
        TenantReceiptService receipts,
        TenantArrearsService arrears,
        RealEstateOptions options)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(arrears);
        ArgumentNullException.ThrowIfNull(options);

        _properties = properties;
        _parties = parties;
        _leases = leases;
        _invoices = invoices;
        _receipts = receipts;
        _arrears = arrears;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    /// <summary>يسجّل عقاراً <b>ويسجّل بُعده في الدفتر في العملية نفسها</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateProperty>> AddPropertyAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstatePropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyView> result = await _properties
            .CreatePropertyAsync(
                tenant, actor, companyId,
                new PropertyDraft(request.Code, request.Name, request.OwnershipModel, request.OwnerId),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<RealEstateProperty>.Failure(result.Errors)
            : Result<RealEstateProperty>.Success(Property(result.Value));
    }

    /// <summary>يقرأ عقاراً بنموذج ملكيته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="propertyId">العقار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateProperty>> ReadPropertyAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        Result<PropertyView> result = await _properties
            .ReadPropertyAsync(tenant, actor, companyId, propertyId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<RealEstateProperty>.Failure(result.Errors)
            : Result<RealEstateProperty>.Success(Property(result.Value));
    }

    /// <summary>يسجّل وحدةً داخل عقار.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="propertyId">العقار المالك.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateUnit>> AddUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid propertyId,
        RealEstateUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<UnitView> result = await _properties
            .CreateUnitAsync(
                tenant, actor, companyId, propertyId,
                new UnitDraft(request.Code, request.Name, request.Usage, request.VatTreatment),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<RealEstateUnit>.Failure(result.Errors)
            : Result<RealEstateUnit>.Success(Unit(result.Value));
    }

    /// <summary>يقرأ وحدةً بتصنيفها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="unitId">الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateUnit>> ReadUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result<UnitView> result = await _properties
            .ReadUnitAsync(tenant, actor, companyId, unitId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<RealEstateUnit>.Failure(result.Errors)
            : Result<RealEstateUnit>.Success(Unit(result.Value));
    }

    /// <summary>يسجّل مستأجراً.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateParty>> AddLesseeAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstatePartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PartyView> result = await _parties
            .CreateLesseeAsync(tenant, actor, companyId, Draft(request), cancellationToken).ConfigureAwait(false);

        return Party(result);
    }

    /// <summary>يقرأ مستأجراً.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="lesseeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateParty>> ReadLesseeAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid lesseeId,
        CancellationToken cancellationToken = default)
        => Party(await _parties.ReadLesseeAsync(tenant, actor, companyId, lesseeId, cancellationToken).ConfigureAwait(false));

    /// <summary>يسجّل مالك عقار.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateParty>> AddOwnerAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstatePartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PartyView> result = await _parties
            .CreateOwnerAsync(tenant, actor, companyId, Draft(request), cancellationToken).ConfigureAwait(false);

        return Party(result);
    }

    /// <summary>يقرأ مالك عقار.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="ownerId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateParty>> ReadOwnerAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
        => Party(await _parties.ReadOwnerAsync(tenant, actor, companyId, ownerId, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// يُسجّل عقد إيجار مُحرَّراً في منصّة إيجار بوصفه <b>مسوّدة قيد</b>. ولا بوّابة
    /// ترحيل عليه إطلاقاً، <b>ولا يُحرَّر العقد هنا</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateLease>> DraftLeaseRegistrationAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstateLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<InstalmentDraft> instalments =
        [
            .. request.Instalments.Select(instalment => new InstalmentDraft(
                instalment.PeriodFrom,
                instalment.PeriodTo,
                instalment.DueOn,
                Money.Of(instalment.Amount, _currency))),
        ];

        Result<LeaseView> result = await _leases
            .DraftAsync(
                tenant, actor, companyId,
                new LeaseDraft(
                    request.EjarContractNumber,
                    request.UnitId,
                    request.LesseeId,
                    request.StartsOn,
                    request.EndsOn,
                    Money.Of(request.TotalRent, _currency),
                    instalments),
                cancellationToken)
            .ConfigureAwait(false);

        return Lease(result);
    }

    /// <summary>يقرأ قيد تسجيل عقدٍ بحالته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateLease>> ReadLeaseAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
        => Lease(await _leases.ReadAsync(tenant, actor, companyId, leaseId, cancellationToken).ConfigureAwait(false));

    /// <summary>يقرأ جدول الدفعات بمعرّفات سطوره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<RealEstateScheduleLine>>> ReadScheduleAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ScheduleLineView>> result = await _leases
            .ReadScheduleAsync(tenant, actor, companyId, leaseId, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<IReadOnlyList<RealEstateScheduleLine>>.Failure(result.Errors);
        }

        IReadOnlyList<RealEstateScheduleLine> lines =
        [
            .. result.Value.Select(static line => new RealEstateScheduleLine(
                line.Id, line.Seq, line.PeriodFrom, line.PeriodTo, line.DueOn, line.Amount.Amount, line.IsInvoiced)),
        ];

        return Result<IReadOnlyList<RealEstateScheduleLine>>.Success(lines);
    }

    /// <summary>
    /// يعتمد القيد <b>للفوترة</b> فتُبنى فواتير الإيجار عليه. ولا يُرحّل قيداً محاسبياً،
    /// ولا يُنفِّذ عقداً. <b>وإعادةُ الاعتماد آمنة وتُعيد النتيجة نفسها.</b>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="leaseId">قيد التسجيل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateLease>> ApproveLeaseForBillingAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
        => Lease(await _leases.ApproveForBillingAsync(tenant, actor, companyId, leaseId, cancellationToken).ConfigureAwait(false));

    /// <summary>ينشئ فاتورة إيجار <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateRentInvoice>> DraftRentInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstateRentInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<RentInvoiceView> result = await _invoices
            .DraftAsync(
                tenant, actor, companyId,
                new RentInvoiceDraft(request.Number, request.LeaseId, request.IssuedOn, request.ScheduleLineIds, request.TaxRate),
                cancellationToken)
            .ConfigureAwait(false);

        return Invoice(result);
    }

    /// <summary>يقرأ فاتورة إيجار.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateRentInvoice>> ReadRentInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
        => Invoice(await _invoices.ReadAsync(tenant, actor, companyId, invoiceId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُرحّل فاتورة إيجار — حصيناً ضد التكرار بهوية المستند.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateRentInvoice>> PostRentInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
        => Invoice(await _invoices.PostAsync(tenant, actor, companyId, invoiceId, cancellationToken).ConfigureAwait(false));

    /// <summary>ينشئ سند قبض <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateReceipt>> DraftReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RealEstateReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<TenantReceiptView> result = await _receipts
            .DraftAsync(
                tenant, actor, companyId,
                new TenantReceiptDraft(
                    request.Number,
                    request.LesseeId,
                    request.ReceivedOn,
                    request.SettlementMethod,
                    request.TreasuryPartyId,
                    Money.Of(request.Received, _currency)),
                cancellationToken)
            .ConfigureAwait(false);

        return Receipt(result);
    }

    /// <summary>يقرأ سند قبض.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateReceipt>> ReadReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
        => Receipt(await _receipts.ReadAsync(tenant, actor, companyId, receiptId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُرحّل سند القبض.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateReceipt>> PostReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
        => Receipt(await _receipts.PostAsync(tenant, actor, companyId, receiptId, cancellationToken).ConfigureAwait(false));

    /// <summary>يخصّص سنداً ورد بلا مرجع — بقيدٍ مستقلّ لا عكس.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateReceipt>> AllocateReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        RealEstateAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Receipt(await _receipts
            .AllocateAsync(tenant, actor, companyId, receiptId, request.LesseeId, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>أعمار متأخرات المستأجرين ومطابقتها بنقطة ضبطها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="asOf">تاريخ التقرير.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<RealEstateArrears>> ReadArrearsAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<(ArrearsReport Aging, ControlReconciliationReport Reconciliation)> result = await _arrears
            .AgingAsync(tenant, actor, companyId, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<RealEstateArrears>.Failure(result.Errors);
        }

        (ArrearsReport aging, ControlReconciliationReport reconciliation) = result.Value;

        IReadOnlyList<RealEstateArrearsParty> parties =
        [
            .. aging.Parties.Select(static party => new RealEstateArrearsParty(
                party.PartyId, party.Code, party.Name, Bands(party.Buckets))),
        ];

        return Result<RealEstateArrears>.Success(new RealEstateArrears(
            aging.AsOf,
            parties,
            Bands(aging.Totals),
            reconciliation.ControlTotal.Amount,
            reconciliation.Divergence.Amount,
            reconciliation.IsReconciled));
    }

    private static PartyDraft Draft(RealEstatePartyRequest request)
        => new(request.Code, request.Name, request.VatNumber, request.TaxResidency);

    private static RealEstateArrearsBands Bands(ArrearsBuckets buckets) => new(
        buckets.NotDue.Amount,
        buckets.Days1To30.Amount,
        buckets.Days31To60.Amount,
        buckets.Days61To90.Amount,
        buckets.Over90.Amount,
        buckets.Total.Amount);

    private static RealEstateProperty Property(PropertyView view) => new(
        view.Id, view.Code, view.Name, view.OwnershipModel,
        view.OwnerId, view.OwnerShareNumerator, view.OwnerShareDenominator);

    private static RealEstateUnit Unit(UnitView view) => new(
        view.Id, view.PropertyId, view.Code, view.Name, view.Usage, view.VatTreatment);

    private static Result<RealEstateParty> Party(Result<PartyView> result)
        => result.IsFailure
            ? Result<RealEstateParty>.Failure(result.Errors)
            : Result<RealEstateParty>.Success(new RealEstateParty(
                result.Value.Id, result.Value.Role, result.Value.Code,
                result.Value.Name, result.Value.VatNumber, result.Value.TaxResidency));

    private static Result<RealEstateLease> Lease(Result<LeaseView> result)
        => result.IsFailure
            ? Result<RealEstateLease>.Failure(result.Errors)
            : Result<RealEstateLease>.Success(new RealEstateLease(
                result.Value.Id, result.Value.EjarContractNumber, result.Value.PropertyId, result.Value.UnitId,
                result.Value.LesseeId, result.Value.StartsOn, result.Value.EndsOn,
                result.Value.TotalRent.Amount, result.Value.State));

    private static Result<RealEstateRentInvoice> Invoice(Result<RentInvoiceView> result)
        => result.IsFailure
            ? Result<RealEstateRentInvoice>.Failure(result.Errors)
            : Result<RealEstateRentInvoice>.Success(new RealEstateRentInvoice(
                result.Value.Id,
                result.Value.Number,
                result.Value.State,
                result.Value.Net.Amount,
                result.Value.Tax.Amount,
                result.Value.Gross.Amount,
                result.Value.EventCode,
                result.Value.VatTreatment,
                result.Value.ExemptionReasonCode,

                // ‏**العلامة تُشتقّ ولا تُخزَّن**: وحدةٌ معفاة بلا رمز سبب حالةٌ قائمة
                // اليوم بحكم أن الرمز غير معروف، ونشرُها في الجواب يجعل الغياب مرئياً
                // في التقرير لا مدفوناً في تعليق (م-8).
                !string.Equals(result.Value.VatTreatment, RentMath.Standard, StringComparison.Ordinal)
                    && result.Value.ExemptionReasonCode.Length == 0,
                result.Value.EntryId,
                result.Value.AlreadyPosted));

    private static Result<RealEstateReceipt> Receipt(Result<TenantReceiptView> result)
        => result.IsFailure
            ? Result<RealEstateReceipt>.Failure(result.Errors)
            : Result<RealEstateReceipt>.Success(new RealEstateReceipt(
                result.Value.Id, result.Value.Number, result.Value.State, result.Value.Received.Amount,
                result.Value.EventCode, result.Value.EntryId, result.Value.IsAllocated,
                result.Value.AllocationEntryId, result.Value.AlreadyPosted));
}
