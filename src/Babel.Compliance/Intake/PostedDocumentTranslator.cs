using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Contracts.Compliance;
using Babel.SharedKernel;

namespace Babel.Compliance.Intake;

/// <summary>
/// <b>الترجمة من حقيقة وحدة المصدر إلى مستند حدّ الالتزام — وهي الموضع الوحيد الذي
/// يقع فيه هذا التحويل.</b>
/// <para>
/// وهي <b>ترجمة نقية</b>: لا مخزن، ولا ساعة، ولا شبكة، ولا حالة. مدخلها حقيقة ومخرجها
/// مستند أو رفض، ولذلك تُختبر بلا تركيب. كل ما يحتاج حالة (الحصانة، السلسلة، الختم)
/// يقع بعدها في <c>ComplianceDocumentFactory</c>.
/// </para>
/// <para>
/// Translation from the originating module's fact into a compliance document — the single
/// place this conversion happens. It is pure: no store, no clock, no network, no state.
/// </para>
/// <para>
/// <b>ولا تقرّر هذه الترجمة المسار.</b> المسار يأتي من <see cref="IFlowPolicy"/> وحدها،
/// فيبقى قرار «مقاصة أم إبلاغ» بمصدر حقيقة واحد.
/// </para>
/// </summary>
public static class PostedDocumentTranslator
{
    /// <summary>غرض اشتقاق هوية المستند داخل حدّ الالتزام.</summary>
    public const string DocumentIdPurpose = "document-id";

    /// <summary>غرض اشتقاق معرّف المستند الظاهر في تمثيله المُرسَل.</summary>
    public const string DocumentUuidPurpose = "document-uuid";

    /// <summary>
    /// هوية المستند في حدّ الالتزام كما تُشتقّ من هوية مستند المصدر. <b>دالة، لا توليد</b> —
    /// وهذا هو مفتاح الحصانة كله.
    /// </summary>
    public static ComplianceDocumentId DocumentIdOf(
        SharedKernel.TenantId tenant, string sourceDocumentType, string sourceDocumentId) =>
        new(ComplianceCanonical.DerivedId(
            DocumentIdPurpose, TenantOf(tenant), sourceDocumentType, sourceDocumentId));

    /// <summary>معرّف المستأجر كما يراه حدّ الالتزام: نصّ ثابت الشكل، لا Guid.</summary>
    public static Abstractions.TenantId TenantOf(SharedKernel.TenantId tenant) =>
        new(tenant.Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// يترجم الحقيقة إلى مستند، أو يرفض برمز ثابت ورسالتين.
    /// <b>الرفض هنا يقع قبل أي كتابة</b>: لا سجل، ولا خانة سلسلة، ولا عدّاد محروق.
    /// </summary>
    /// <param name="posted">الحقيقة المُرحَّلة.</param>
    /// <param name="flowPolicy">سياسة اختيار المسار — المصدر الوحيد لقرار المسار.</param>
    public static Result<ComplianceDocument> Translate(TaxableDocumentPosted posted, IFlowPolicy flowPolicy)
    {
        ArgumentNullException.ThrowIfNull(posted);
        ArgumentNullException.ThrowIfNull(flowPolicy);

        List<Error> errors = [];

        if (!posted.Tenant.IsAssigned) errors.Add(IntakeErrors.TenantMissing);
        if (posted.JournalEntry == Guid.Empty) errors.Add(IntakeErrors.NotPosted);
        if (string.IsNullOrWhiteSpace(posted.IssuingUnit)) errors.Add(IntakeErrors.IssuingUnitMissing);
        if (string.IsNullOrWhiteSpace(posted.DocumentNumber)) errors.Add(IntakeErrors.DocumentNumberMissing);

        if (string.IsNullOrWhiteSpace(posted.SourceDocumentType) || string.IsNullOrWhiteSpace(posted.SourceDocumentId))
        {
            errors.Add(IntakeErrors.SourceIdentityMissing);
        }

        if (posted.Lines is null || posted.Lines.Count == 0) errors.Add(IntakeErrors.NoLines);

        errors.AddRange(CorrectionErrors(posted));

        // العملة: عملة الإجمالي هي عملة المستند، وكل مبلغ آخر يجب أن يوافقها.
        CurrencyCode currency = posted.GrossTotal.Currency;
        if (!CurrencyIsUniform(posted, currency)) errors.Add(IntakeErrors.CurrencyMismatch);

        if (posted.NetTotal.Amount + posted.TaxTotal.Amount != posted.GrossTotal.Amount)
        {
            errors.Add(IntakeErrors.TotalsInconsistent);
        }

        if (posted.Lines is { Count: > 0 } && !LinesSumToTotals(posted))
        {
            errors.Add(IntakeErrors.LinesDoNotSum);
        }

        if (errors.Count > 0) return Result<ComplianceDocument>.Failure(errors);

        Abstractions.TenantId tenant = TenantOf(posted.Tenant);
        ComplianceDocumentKind kind = KindOf(posted.Kind);

        PartyRef seller = PartyOf(posted.Seller)!;
        PartyRef? buyer = PartyOf(posted.Buyer);
        DocumentTotals totals = new(posted.NetTotal.Amount, posted.TaxTotal.Amount, posted.GrossTotal.Amount);

        // المسار من السياسة وحدها — لا من حقل في الحدث ولا من تفريع هنا.
        ComplianceFlow flow = flowPolicy.FlowFor(kind, buyer, totals);

        ComplianceDocumentId? original = posted.Kind == TaxableDocumentKind.Invoice
            ? null
            : new ComplianceDocumentId(ComplianceCanonical.DerivedId(
                DocumentIdPurpose, tenant, posted.OriginalSourceDocumentType!, posted.OriginalSourceDocumentId!));

        return Result<ComplianceDocument>.Success(new ComplianceDocument(
            DocumentId: DocumentIdOf(posted.Tenant, posted.SourceDocumentType, posted.SourceDocumentId),
            DocumentUuid: ComplianceCanonical.DerivedId(
                DocumentUuidPurpose, tenant, posted.SourceDocumentType, posted.SourceDocumentId),
            Tenant: tenant,
            IssuingUnit: new IssuingUnitId(posted.IssuingUnit),
            Kind: kind,
            Flow: flow,
            DocumentNumber: posted.DocumentNumber,
            IssuedAt: posted.IssuedAt,
            CurrencyCode: currency.Value,
            Seller: seller,
            Buyer: buyer,
            Lines: [.. posted.Lines!.OrderBy(static l => l.LineNo).Select(LineOf)],
            Totals: totals,
            JournalEntry: new JournalEntryRef(posted.JournalEntry),
            OriginalDocument: original,
            CorrectionReasonAr: posted.CorrectionReasonAr,
            CorrectionReasonEn: posted.CorrectionReasonEn));
    }

    private static IEnumerable<Error> CorrectionErrors(TaxableDocumentPosted posted)
    {
        bool isCorrection = posted.Kind is TaxableDocumentKind.CreditNote or TaxableDocumentKind.DebitNote;

        bool namesOriginal =
            !string.IsNullOrWhiteSpace(posted.OriginalSourceDocumentType)
            && !string.IsNullOrWhiteSpace(posted.OriginalSourceDocumentId);

        bool givesReason =
            !string.IsNullOrWhiteSpace(posted.CorrectionReasonAr)
            && !string.IsNullOrWhiteSpace(posted.CorrectionReasonEn);

        if (isCorrection && !(namesOriginal && givesReason))
        {
            yield return IntakeErrors.CorrectionIncomplete;
        }

        bool carriesAnyCorrectionData =
            !string.IsNullOrWhiteSpace(posted.OriginalSourceDocumentType)
            || !string.IsNullOrWhiteSpace(posted.OriginalSourceDocumentId)
            || !string.IsNullOrWhiteSpace(posted.CorrectionReasonAr)
            || !string.IsNullOrWhiteSpace(posted.CorrectionReasonEn);

        if (!isCorrection && carriesAnyCorrectionData)
        {
            yield return IntakeErrors.CorrectionOnPlainInvoice;
        }
    }

    private static bool CurrencyIsUniform(TaxableDocumentPosted posted, CurrencyCode currency)
    {
        if (posted.NetTotal.Currency != currency || posted.TaxTotal.Currency != currency) return false;

        foreach (TaxableDocumentLine line in posted.Lines ?? [])
        {
            if (line.UnitPrice.Currency != currency
                || line.NetAmount.Currency != currency
                || line.TaxAmount.Currency != currency
                || line.GrossAmount.Currency != currency)
            {
                return false;
            }
        }

        return true;
    }

    private static bool LinesSumToTotals(TaxableDocumentPosted posted)
    {
        decimal net = 0m, tax = 0m, gross = 0m;

        foreach (TaxableDocumentLine line in posted.Lines)
        {
            net += line.NetAmount.Amount;
            tax += line.TaxAmount.Amount;
            gross += line.GrossAmount.Amount;
        }

        return net == posted.NetTotal.Amount
            && tax == posted.TaxTotal.Amount
            && gross == posted.GrossTotal.Amount;
    }

    private static ComplianceDocumentKind KindOf(TaxableDocumentKind kind) => kind switch
    {
        TaxableDocumentKind.CreditNote => ComplianceDocumentKind.CreditNote,
        TaxableDocumentKind.DebitNote => ComplianceDocumentKind.DebitNote,
        _ => ComplianceDocumentKind.Invoice
    };

    private static PartyRef? PartyOf(TaxableDocumentParty? party) => party is null
        ? null
        : new PartyRef(party.Name.Arabic, party.Name.English, party.TaxRegistrationNumber, party.AddressAr, party.AddressEn);

    private static DocumentLine LineOf(TaxableDocumentLine line) => new(
        line.LineNo,
        line.DescriptionAr,
        line.DescriptionEn,
        line.Quantity,
        line.UnitPrice.Amount,
        line.NetAmount.Amount,
        line.TaxRate,
        line.TaxAmount.Amount,
        line.GrossAmount.Amount);
}
