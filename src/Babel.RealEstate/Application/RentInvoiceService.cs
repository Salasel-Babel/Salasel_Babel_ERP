using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Contracts.RealEstate;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Application;

/// <summary>
/// فاتورة الإيجار الدورية: مسوّدةً ثم ترحيلاً.
/// <para>
/// <b>والوحدة تختار الحدث من نموذج الملكية المُسجَّل لا من حقلٍ في الطلب.</b> فلا يستطيع
/// عميل HTTP أن يطلب «فاتورة ملكية ذاتية» على عقارٍ مُدار — وذلك بالضبط الخطأ الذي
/// تحرسه GR-RE-001 ويضخّم إيراد قائمة الدخل واحداً وعشرين ضعفاً في المثال المكتوب.
/// والفرق بين النموذجين يظهر في <b>دائن الفاتورة</b>: إيرادُ إيجار مؤجَّل للشركة في
/// الملكية الذاتية، وأماناتُ مالكٍ في الإدارة.
/// </para>
/// <para>
/// <b>ولا نسبة ضريبة مكتوبة في هذه الوحدة</b>: النسبة تصل مع الطلب، والوحدة تقرّر
/// <b>هل تنطبق</b> من معاملة الوحدة الضريبية المُدخَلة لا من نوع العقار (م-3).
/// وحين تكون الوحدة معفاة يبقى <c>ExemptionReasonCode</c> <b>عموداً فارغاً بعلامة
/// ظاهرة</b> حتى يُعرف الرمز من القائمة الرسمية — وحقلٌ إلزامي بقيمة مُختلَقة أسوأ من
/// حقلٍ فارغ (م-8).
/// </para>
/// </summary>
public sealed class RentInvoiceService : IApplicationService
{
    /// <summary>نوع المستند في هوية الترحيل.</summary>
    internal const string DocumentType = "realestate.rent_invoice";

    private const string OwnPropertyEvent = "realestate.rent_invoice.own_property";
    private const string ManagedPropertyEvent = "realestate.rent_invoice.managed_property";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;
    private readonly RealEstatePostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل عبر العقد.</param>
    public RentInvoiceService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new RealEstatePostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>ينشئ فاتورة إيجار <b>مسوّدة</b>. لا قيد ولا أثر في الدفتر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<RentInvoiceView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        RentInvoiceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.RentInvoice.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<RentInvoiceView>.Failure(gate.Errors);
        }

        if (draft.ScheduleLineIds.Count == 0)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.InvoiceHasNoLines);
        }

        LeaseContractRow? lease = await _database.Leases
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == draft.LeaseId,
                cancellationToken)
            .ConfigureAwait(false);

        if (lease is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.LeaseNotFound(draft.LeaseId));
        }

        if (!string.Equals(lease.State, LeaseState.Billable, StringComparison.Ordinal))
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.LeaseIsNotApprovedForBilling(lease.Id));
        }

        UnitRow? unit = await _database.Units
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == lease.UnitId, cancellationToken)
            .ConfigureAwait(false);

        if (unit is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.UnitNotFound(lease.UnitId));
        }

        PropertyRow? property = await _database.Properties
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == lease.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.PropertyNotFound(lease.PropertyId));
        }

        if (await _database.RentInvoices
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Number == draft.Number,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.DuplicateCode(draft.Number));
        }

        List<PaymentScheduleLineRow> schedule = await _database.ScheduleLines
            .Where(row => row.TenantId == tenant.Value && row.LeaseId == lease.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PaymentScheduleLineRow> chosen = [];
        foreach (Guid lineId in draft.ScheduleLineIds)
        {
            PaymentScheduleLineRow? line = schedule.Find(candidate => candidate.Id == lineId);
            if (line is null)
            {
                return Result<RentInvoiceView>.Failure(RealEstateErrors.ScheduleLineNotFound(lease.Id, lineId));
            }

            if (line.IsInvoiced)
            {
                return Result<RentInvoiceView>.Failure(RealEstateErrors.ScheduleLineAlreadyInvoiced(lineId));
            }

            chosen.Add(line);
        }

        // ‏**الحدث من السجلّ لا من الطلب.**
        bool managed = string.Equals(
            property.OwnershipModel, PropertyOwnershipModels.ManagedForOthers, StringComparison.Ordinal);

        // ── الخضوع للضريبة يُحسم هنا، ونموذج الملكية جزءٌ منه ────────────────────
        // في الملكية الذاتية يكفي أن تكون معاملة الوحدة standard. وفي الإدارة يشترط
        // القالب أن يكون **المالك مسجَّلاً ضريبياً** كذلك، لأن الفاتورة صادرة لحسابه.
        // ولو حُسبت الضريبة بمعاملة الوحدة وحدها لخرجت فاتورةٌ مدينُها الصافي والضريبة
        // ودائنُها الصافي وحده — قيدٌ غير متوازن يسقط عند COMMIT بلا سبب مفهوم.
        bool taxable = string.Equals(unit.VatTreatment, RentMath.Standard, StringComparison.Ordinal);

        if (managed)
        {
            Result<Guid> owner = await SingleOwnerAsync(tenant, companyId, property.Id, cancellationToken).ConfigureAwait(false);
            if (owner.IsFailure)
            {
                return Result<RentInvoiceView>.Failure(owner.Errors);
            }

            PartyRow? ownerRow = await _database.Parties
                .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == owner.Value, cancellationToken)
                .ConfigureAwait(false);

            if (ownerRow is null)
            {
                return Result<RentInvoiceView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Owner, owner.Value));
            }

            taxable = taxable && ownerRow.VatNumber.Length > 0;
        }

        RentInvoiceRow invoice = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            Number = draft.Number,
            LeaseId = lease.Id,
            PropertyId = lease.PropertyId,
            UnitId = lease.UnitId,
            LesseeId = lease.LesseeId,
            IssuedOn = draft.IssuedOn,
            VatTreatment = unit.VatTreatment,
            ExemptionReasonCode = string.Empty,
            EventCode = managed ? ManagedPropertyEvent : OwnPropertyEvent,
            State = RealEstateDocumentState.Draft,
        };

        decimal net = 0m;
        decimal tax = 0m;

        foreach (PaymentScheduleLineRow line in chosen.OrderBy(row => row.Seq))
        {
            decimal lineNet = line.Amount;
            decimal lineTax = RentMath.Tax(lineNet, draft.TaxRate, taxable);

            _database.RentInvoiceLines.Add(new RentInvoiceLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                InvoiceId = invoice.Id,
                ScheduleLineId = line.Id,
                PeriodFrom = line.PeriodFrom,
                PeriodTo = line.PeriodTo,
                Net = lineNet,
                Tax = lineTax,
            });

            line.IsInvoiced = true;
            net += lineNet;
            tax += lineTax;
        }

        invoice.NetTotal = net;
        invoice.TaxTotal = tax;
        invoice.GrossTotal = net + tax;

        _database.RentInvoices.Add(invoice);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<RentInvoiceView>.Success(View(invoice, alreadyPosted: false));
    }

    /// <summary>يقرأ فاتورة بحالتها ومجاميعها ومعرّف قيدها إن رُحّلت.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<RentInvoiceView>> ReadAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.RentInvoice.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<RentInvoiceView>.Failure(gate.Errors);
        }

        RentInvoiceRow? row = await _database.RentInvoices
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == invoiceId,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<RentInvoiceView>.Failure(RealEstateErrors.DocumentNotFound(DocumentType, invoiceId))
            : Result<RentInvoiceView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>
    /// يُرحّل فاتورة مسوّدة. <b>والحكم حكمُ البوّابة لا مقارنةَ حالة</b>: الوصول الثاني
    /// بالهوية نفسها يُرجع المستند ذاته و<c>alreadyPosted = true</c> ومعرّف القيد نفسه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<RentInvoiceView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.RentInvoice.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<RentInvoiceView>.Failure(gate.Errors);
        }

        RentInvoiceRow? invoice = await _database.RentInvoices
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == invoiceId,
                cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.DocumentNotFound(DocumentType, invoiceId));
        }

        PartyRow? lessee = await _database.Parties
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoice.LesseeId, cancellationToken)
            .ConfigureAwait(false);

        if (lessee is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Lessee, invoice.LesseeId));
        }

        PropertyRow? property = await _database.Properties
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoice.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        UnitRow? unit = await _database.Units
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoice.UnitId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null || unit is null)
        {
            return Result<RentInvoiceView>.Failure(RealEstateErrors.PropertyNotFound(invoice.PropertyId));
        }

        bool managed = string.Equals(invoice.EventCode, ManagedPropertyEvent, StringComparison.Ordinal);
        string ownerParty = string.Empty;
        bool ownerVatRegistered = false;

        if (managed)
        {
            Result<Guid> owner = await SingleOwnerAsync(tenant, companyId, property.Id, cancellationToken).ConfigureAwait(false);
            if (owner.IsFailure)
            {
                return Result<RentInvoiceView>.Failure(owner.Errors);
            }

            PartyRow? ownerRow = await _database.Parties
                .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == owner.Value, cancellationToken)
                .ConfigureAwait(false);

            if (ownerRow is null)
            {
                return Result<RentInvoiceView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Owner, owner.Value));
            }

            ownerParty = ownerRow.Code;

            // ‏«مسجَّل ضريبياً» واقعةٌ عن الطرف لا اجتهاد: رقمُ تسجيلٍ مكتوب أو لا شيء.
            ownerVatRegistered = ownerRow.VatNumber.Length > 0;
        }

        List<PostingFact> facts =
        [
            // ‏**الوحدة لا تُصرّح بـproperty.ownership_model إطلاقاً**: المُخطِّط يكتب
            // قيمة سجلّ الدفتر **فوق** أي واقعة تُرسلها الوحدة، فالتصريح لا ينفع —
            // ولو نفع في مسارٍ آخر لكان وحدةً تكذب على حارسٍ يحرسها. الطريق الوحيد
            // هو الصفّ الذي كتبه منفذ التسجيل عند إنشاء العقار.
            new PostingFact("unit.vat_treatment", unit.VatTreatment),
            new PostingFact("subledger.tenant", lessee.Code),
            new PostingFact("subledger.lease_contract", invoice.LeaseId.ToString("D", CultureInfo.InvariantCulture)),

            // قاعدة الحجب GR-VAT-001 تُقيَّم على هذه الواقعة، وقاعدةٌ لا تُقيَّم لا
            // تُتجاوَز — فغيابها يرفض الفاتورة كلها في النموذج المُدار.
            new PostingFact("document_type", DocumentType),
        ];

        if (managed)
        {
            facts.Add(new PostingFact("owner.vat_registered", ownerVatRegistered ? "true" : "false"));
            facts.Add(new PostingFact("subledger.property_owner", ownerParty));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = DocumentType,
            DocumentId = invoice.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(invoice.EventCode),
            DocumentDate = invoice.IssuedOn,
            Narration = new LocalizedName(
                "فاتورة إيجار " + invoice.Number,
                "Rent invoice " + invoice.Number),
            Amounts =
            [
                new PostingAmount("net", Money.Of(invoice.NetTotal, _currency)),
                new PostingAmount("tax", Money.Of(invoice.TaxTotal, _currency)),
            ],
            Facts = facts,
            Dimensions =
            [
                new PostingDimension("property", property.Code),
                new PostingDimension("unit", unit.Code),
            ],
            PartyId = lessee.Code,
            ControlEffect = invoice.GrossTotal,
            Currency = _currency,
            Actor = actor,
        };

        Result<PostingReceipt> receipt = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (receipt.IsFailure)
        {
            return Result<RentInvoiceView>.Failure(receipt.Errors);
        }

        invoice.State = RealEstateDocumentState.Posted;
        invoice.EntryId = receipt.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<RentInvoiceView>.Success(View(invoice, receipt.Value.WasAlreadyPosted));
    }

    /// <summary>
    /// مالكُ العقار الواحد، أو رفضٌ يسمّي البند حين يكون أكثر من مالك.
    /// <para>
    /// المفتاح رباعي من اليوم فالشكل يحتمل الحصص بلا هجرة، <b>والقسمة نفسها</b> قرار
    /// مالك مفتوح (ق-ع-18): تقسيم سطور النموذج المُدار يضيف بُعد المالك إلى مفتاح
    /// الفوترة ويُدخل سياسة تقريب على كل قسمة. فيُرفض بصوت عالٍ ولا يُخترع.
    /// </para>
    /// </summary>
    private async Task<Result<Guid>> SingleOwnerAsync(
        TenantId tenant,
        Guid companyId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        List<PropertyOwnerShareRow> shares = await _database.OwnerShares
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.PropertyId == propertyId)
            .OrderBy(row => row.OwnerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return shares.Count switch
        {
            0 => Result<Guid>.Failure(RealEstateErrors.ManagedPropertyNeedsAnOwner),
            1 => Result<Guid>.Success(shares[0].OwnerId),
            _ => Result<Guid>.Failure(RealEstateErrors.OwnerShareSplitNotDecided),
        };
    }

    private RentInvoiceView View(RentInvoiceRow row, bool alreadyPosted) => new(
        row.Id,
        row.Number,
        row.State,
        Money.Of(row.NetTotal, _currency),
        Money.Of(row.TaxTotal, _currency),
        Money.Of(row.GrossTotal, _currency),
        row.EventCode,
        row.VatTreatment,
        row.ExemptionReasonCode,
        row.EntryId,
        alreadyPosted);
}
