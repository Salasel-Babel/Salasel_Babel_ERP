using System.Globalization;
using Babel.Ai.Attestation;
using Babel.Ai.Extraction;
using Babel.Ai.Promotion;
using Babel.Ai.Reconciliation;
using Babel.Ai.Suggestions;
using Babel.Contracts.Capture;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Ai.Capture;

/// <summary>تصحيح بشري لحقل واحد: المفتاح والقيمة كما كتبها الإنسان.</summary>
/// <param name="Field">مفتاح الحقل.</param>
/// <param name="Value">القيمة الجديدة نصّاً — تُقرأ بثقافة ثابتة.</param>
public sealed record FieldCorrection(string Field, string Value);

/// <summary>
/// <b>خدمة التقاط فواتير الموردين.</b> تلتقط، وتُصدِّق ما يحمله الرمز، وتطابق حسابياً،
/// وتقترح حدثاً من مفردات مغلقة — <b>ولا تُرحِّل شيئاً ولا تستطيع</b>.
/// <para>
/// وحدة <c>Babel.Ai</c> لا تعرف <c>Babel.Contracts.Posting</c> إطلاقاً: لا محرك ترحيل،
/// ولا طلب ترحيل، ولا سطر. ولا تعرف وحدة أفقية أخرى. فالطريق الوحيد إلى مستند حقيقي
/// هو <see cref="ICapturedInvoiceReceiver"/>، وينفّذه <b>مالك المستند بخدماته المعتادة</b>.
/// </para>
/// </summary>
public sealed class InvoiceCaptureService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IInvoiceExtractionProvider _extractor;
    private readonly IAttestedQrReader _qr;
    private readonly IPostingVocabulary _vocabulary;
    private readonly ICapturedDraftStore _store;
    private readonly ICapturedInvoiceReceiver _receiver;
    private readonly AiOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="extractor">مزوّد الاستخراج.</param>
    /// <param name="qr">قارئ الرمز المُصدَّق.</param>
    /// <param name="vocabulary">المفردات المغلقة.</param>
    /// <param name="store">مخزن المسوّدات.</param>
    /// <param name="receiver">منفذ الترقية إلى الوحدة المالكة.</param>
    /// <param name="options">إعدادات الوحدة.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public InvoiceCaptureService(
        IEntitlementEnforcer enforcer,
        IInvoiceExtractionProvider extractor,
        IAttestedQrReader qr,
        IPostingVocabulary vocabulary,
        ICapturedDraftStore store,
        ICapturedInvoiceReceiver receiver,
        AiOptions options,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(qr);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _enforcer = enforcer;
        _extractor = extractor;
        _qr = qr;
        _vocabulary = vocabulary;
        _store = store;
        _receiver = receiver;
        _options = options;
        _clock = clock;
    }

    /// <summary>
    /// يلتقط مستنداً ويعيد مسوّدةً مطابَقة حسابياً.
    /// <para>
    /// الترتيب مقصود: <b>الرمز أولاً ثم النموذج</b>. ما يحمله الرمز مُصدَّق ولا يُطلب من
    /// النموذج تأكيده؛ والنموذج يُسأل عمّا لا يحمله الرمز — السطور.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">طلب الالتقاط.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Write)]
    public async ValueTask<Result<CapturedInvoiceDraft>> CaptureAsync(
        TenantId tenant,
        UserId actor,
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Write, "Ai.Capture.Capture", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(gate.Errors);
        }

        // ── 1 · الرمز: مُصدَّق أو رفض. ورمزٌ معطوب لا ينحدر بصمت إلى «قراءة ضوئية» ──
        AttestedInvoiceFacts? attested = null;
        if (!string.IsNullOrWhiteSpace(request.QrPayload))
        {
            Result<AttestedInvoiceFacts> read = _qr.Read(request.QrPayload);
            if (read.IsFailure)
            {
                return Result<CapturedInvoiceDraft>.Failure(read.Errors);
            }

            attested = read.Value;
        }

        // ── 2 · المزوّد، ثم المخطط عند الحدّ ──────────────────────────────────
        Result<ExtractionOutput> output = await _extractor.ExtractAsync(request, cancellationToken).ConfigureAwait(false);
        if (output.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(output.Errors);
        }

        Result<ExtractedInvoice> validated = ExtractionSchema.Validate(output.Value.Json);
        if (validated.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(validated.Errors);
        }

        ExtractedInvoice extracted = validated.Value;

        // ── 3 · الاقتراح يمرّ بالمفردات المغلقة قبل أن يراه أحد ────────────────
        PostingSuggestion? suggestion = null;
        if (extracted.Suggestion is { } proposed)
        {
            PostingSuggestion candidate = new()
            {
                EventCode = proposed.EventCode,
                RoleCode = proposed.RoleCode,
                Confidence = proposed.Confidence,
                Rationale = proposed.Rationale,
            };

            Result guardResult = SuggestionGuard.Validate(candidate, _vocabulary);
            if (guardResult.IsFailure)
            {
                return Result<CapturedInvoiceDraft>.Failure(guardResult.Errors);
            }

            suggestion = candidate.Confidence >= _options.MinimumSuggestionConfidence ? candidate : null;
        }

        CapturedInvoiceDraft draft = Build(tenant, request, output.Value.ProviderId, extracted, attested, suggestion);
        draft = Settle(draft);

        await _store.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        return Result<CapturedInvoiceDraft>.Success(draft);
    }

    /// <summary>يجلب مسوّدة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draftId">معرّف المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Read)]
    public async ValueTask<Result<CapturedInvoiceDraft>> FindAsync(
        TenantId tenant,
        UserId actor,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Read, "Ai.Capture.Find", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(gate.Errors);
        }

        CapturedInvoiceDraft? draft = await _store.FindAsync(tenant, draftId, cancellationToken).ConfigureAwait(false);
        return draft is null
            ? Result<CapturedInvoiceDraft>.Failure(CaptureErrors.DraftNotFound(draftId))
            : Result<CapturedInvoiceDraft>.Success(draft);
    }

    /// <summary>يعدّد مسوّدات المستأجر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<CapturedInvoiceDraft>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Read, "Ai.Capture.List", cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailure
            ? Result<IReadOnlyList<CapturedInvoiceDraft>>.Failure(gate.Errors)
            : Result<IReadOnlyList<CapturedInvoiceDraft>>.Success(
                await _store.ListAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يصحّح حقولاً بيد إنسان، فيصير مصدرها <c>typed</c>، ثم يُعاد ضبط المطابقة.
    /// <para>
    /// <b>والحقل المُصدَّق لا يُعاد كتابته.</b> إنسانٌ يكتب فوق إجمالي وقّعه المُصدِر يزيل
    /// أقوى ما في المسوّدة من ضمانة، ولا يفعل ذلك عن قصد بل لأن الشاشة سمحت.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draftId">معرّف المسوّدة.</param>
    /// <param name="corrections">التصحيحات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Write)]
    public async ValueTask<Result<CapturedInvoiceDraft>> CorrectAsync(
        TenantId tenant,
        UserId actor,
        Guid draftId,
        IReadOnlyList<FieldCorrection> corrections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corrections);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Write, "Ai.Capture.Correct", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(gate.Errors);
        }

        CapturedInvoiceDraft? found = await _store.FindAsync(tenant, draftId, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return Result<CapturedInvoiceDraft>.Failure(CaptureErrors.DraftNotFound(draftId));
        }

        CapturedInvoiceDraft draft = found;
        List<Error> errors = [];

        foreach (FieldCorrection correction in corrections)
        {
            draft = Apply(draft, correction, errors);
        }

        if (errors.Count > 0)
        {
            return Result<CapturedInvoiceDraft>.Failure(errors);
        }

        draft = Settle(draft);
        await _store.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        return Result<CapturedInvoiceDraft>.Success(draft);
    }

    /// <summary>يرفض مسوّدة بقرار بشري.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draftId">معرّف المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Write)]
    public async ValueTask<Result<CapturedInvoiceDraft>> RejectAsync(
        TenantId tenant,
        UserId actor,
        Guid draftId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Write, "Ai.Capture.Reject", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CapturedInvoiceDraft>.Failure(gate.Errors);
        }

        CapturedInvoiceDraft? draft = await _store.FindAsync(tenant, draftId, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return Result<CapturedInvoiceDraft>.Failure(CaptureErrors.DraftNotFound(draftId));
        }

        CapturedInvoiceDraft rejected = draft with { State = DraftState.Rejected };
        await _store.SaveAsync(rejected, cancellationToken).ConfigureAwait(false);
        return Result<CapturedInvoiceDraft>.Success(rejected);
    }

    /// <summary>
    /// يُرقّي مسوّدةً إلى مستند حقيقي عبر <see cref="ICapturedInvoiceReceiver"/>.
    /// <para>
    /// ثلاثة شروط لا رابع لها: مطابقة حسابية بلا ملاحظات، ورمز حدث من المصفوفة،
    /// و<b>تأكيد بشري على كل حقل لا تكفي فيه اللمحة</b>.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل — إنسان، وهو من يعتمد.</param>
    /// <param name="draftId">معرّف المسوّدة.</param>
    /// <param name="confirmation">التأكيد البشري.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Ai, EntitlementAccess.Write)]
    public async ValueTask<Result<PromotedDocumentReference>> PromoteAsync(
        TenantId tenant,
        UserId actor,
        Guid draftId,
        PromotionConfirmation confirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Ai, EntitlementAccess.Write, "Ai.Capture.Promote", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PromotedDocumentReference>.Failure(gate.Errors);
        }

        CapturedInvoiceDraft? draft = await _store.FindAsync(tenant, draftId, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return Result<PromotedDocumentReference>.Failure(CaptureErrors.DraftNotFound(draftId));
        }

        if (draft.State != DraftState.Reconciled)
        {
            return Result<PromotedDocumentReference>.Failure(draft.Findings.Count > 0
                ? CaptureErrors.DraftHasOpenFindings(draft.Findings.Count)
                : CaptureErrors.NotPromotable(draft.State));
        }

        if (draft.Suggestion is null)
        {
            return Result<PromotedDocumentReference>.Failure(CaptureErrors.NoSuggestion);
        }

        List<Error> missing = [.. draft
            .FieldsNeedingHumanJudgement()
            .Where(field => !confirmation.ConfirmedFields.Contains(field))
            .Select(CaptureErrors.FieldNotConfirmed)];

        if (missing.Count > 0)
        {
            return Result<PromotedDocumentReference>.Failure(missing);
        }

        PromotionOrder order = new()
        {
            Tenant = tenant,
            DraftId = draft.DraftId,
            PromotedBy = actor,
            SupplierName = draft.SellerName.Value,
            SupplierVatNumber = draft.SellerVatNumber.Value,
            InvoiceNumber = draft.InvoiceNumber.Value,
            IssuedOn = draft.IssuedOn.Value,
            Currency = draft.Currency.Value,
            Net = draft.Net.Value,
            TaxRate = draft.TaxRate.Value,
            TaxTotal = draft.TaxTotal.Value,
            GrossTotal = draft.GrossTotal.Value,
            EventCode = draft.Suggestion.EventCode,
            RoleCode = draft.Suggestion.RoleCode,

            // تصنيف المصروف يعبر **كما كتبه الإنسان** عند التأكيد، ولا يُشتقّ من اقتراح
            // ولا من سطر. والمستقبِل يقرأ مصدره من الخريطة أدناه ويرفض ما ليس مكتوباً بيد.
            ExpenseCategory = confirmation.ExpenseCategory,
            Lines = [.. draft.Lines.Select(static line => new PromotionLine(
                line.LineNo, line.Description.Value, line.Quantity.Value, line.UnitPrice.Value, line.LineNet.Value))],
            Provenance = ProvenanceOf(draft, confirmation),
        };

        Result<PromotedDocumentReference> received = await _receiver.ReceiveAsync(order, cancellationToken).ConfigureAwait(false);
        if (received.IsFailure)
        {
            return received;
        }

        await _store.SaveAsync(draft with { State = DraftState.Promoted }, cancellationToken).ConfigureAwait(false);
        return received;
    }

    // ── البناء والضبط ───────────────────────────────────────────────────────

    private CapturedInvoiceDraft Build(
        TenantId tenant,
        ExtractionRequest request,
        string providerId,
        ExtractedInvoice extracted,
        AttestedInvoiceFacts? attested,
        PostingSuggestion? suggestion)
    {
        string originKey = attested is { CarriesSignature: true } ? CaptureOriginKeys.SignedQr : CaptureOriginKeys.UnsignedQr;

        CapturedField<string> sellerName = attested is null
            ? CapturedField<string>.Read(extracted.SellerName.Value, extracted.SellerName.Confidence)
            : CapturedField<string>.Attested(attested.SellerName, originKey);

        CapturedField<string> vatNumber = attested is null
            ? CapturedField<string>.Read(extracted.SellerVatNumber.Value, extracted.SellerVatNumber.Confidence)
            : CapturedField<string>.Attested(attested.SellerVatNumber, originKey);

        CapturedField<DateOnly> issuedOn = attested is null
            ? CapturedField<DateOnly>.Read(extracted.IssuedOn.Value, extracted.IssuedOn.Confidence)
            : CapturedField<DateOnly>.Attested(DateOnly.FromDateTime(attested.IssuedAt.UtcDateTime), originKey);

        CapturedField<decimal> taxTotal = attested is null
            ? CapturedField<decimal>.Read(extracted.TaxTotal.Value, extracted.TaxTotal.Confidence)
            : CapturedField<decimal>.Attested(attested.TaxTotal, originKey);

        CapturedField<decimal> grossTotal = attested is null
            ? CapturedField<decimal>.Read(extracted.GrossTotal.Value, extracted.GrossTotal.Confidence)
            : CapturedField<decimal>.Attested(attested.GrossTotal, originKey);

        CapturedField<CurrencyCode> currency = extracted.Currency is { } read
            ? CapturedField<CurrencyCode>.Read(read.Value, read.Confidence)
            : CapturedField<CurrencyCode>.Defaulted(CurrencyCode.FromString(_options.CompanyCurrency));

        CapturedField<decimal> taxRate = extracted.TaxRate is { } rate
            ? CapturedField<decimal>.Read(rate.Value, rate.Confidence)
            : CapturedField<decimal>.Defaulted(_options.StatutoryTaxRate);

        return new CapturedInvoiceDraft
        {
            DraftId = Guid.CreateVersion7(),
            Tenant = tenant,
            Channel = request.Channel,
            CapturedAt = _clock.GetUtcNow(),
            ExtractionProviderId = providerId,
            SellerName = sellerName,
            SellerVatNumber = vatNumber,
            InvoiceNumber = CapturedField<string>.Read(extracted.InvoiceNumber.Value, extracted.InvoiceNumber.Confidence),
            IssuedOn = issuedOn,
            Currency = currency,
            Net = CapturedField<decimal>.Read(extracted.Net.Value, extracted.Net.Confidence),
            TaxRate = taxRate,
            TaxTotal = taxTotal,
            GrossTotal = grossTotal,
            Lines = [.. extracted.Lines.Select(static line => new CapturedInvoiceLine
            {
                LineNo = line.LineNo,
                Description = CapturedField<string>.Read(line.Description.Value, line.Description.Confidence),
                Quantity = CapturedField<decimal>.Read(line.Quantity.Value, line.Quantity.Confidence),
                UnitPrice = CapturedField<decimal>.Read(line.UnitPrice.Value, line.UnitPrice.Confidence),
                LineNet = CapturedField<decimal>.Read(line.LineNet.Value, line.LineNet.Confidence),
            })],
            State = DraftState.Captured,
            Suggestion = suggestion,
        };
    }

    /// <summary>يُعيد المطابقة ويشتقّ الحالة منها. الحالة نتيجة لا إعلان.</summary>
    private static CapturedInvoiceDraft Settle(CapturedInvoiceDraft draft)
    {
        IReadOnlyList<ReconciliationFinding> findings = DraftReconciler.Reconcile(draft);

        return draft with
        {
            Findings = findings,
            State = findings.Any(static finding => finding.Severity == FindingSeverity.Blocking)
                ? DraftState.Disputed
                : DraftState.Reconciled,
        };
    }

    /// <summary>
    /// خريطة المصادر التي تعبر مع الأمر.
    /// <para>
    /// وتصنيف المصروف يدخلها <b>حين يُكتب فقط</b>، ومصدره <c>Typed</c> بحكم موضعه: هو
    /// قادم من نوع التأكيد البشري، ولا يوجد في هذه الوحدة مسارٌ آخر يضعه. فالوحدة
    /// المالكة تستطيع أن ترفض أي تصنيف لم يكتبه إنسان، وذلك الرفضُ <b>ممكنٌ لأن المصدر
    /// عبر معه</b>.
    /// </para>
    /// </summary>
    private static Dictionary<string, FieldProvenance> ProvenanceOf(
        CapturedInvoiceDraft draft,
        PromotionConfirmation confirmation)
    {
        Dictionary<string, FieldProvenance> map = new(StringComparer.Ordinal)
        {
            [CapturedInvoiceDraft.SellerNameField] = draft.SellerName.Provenance,
            [CapturedInvoiceDraft.SellerVatNumberField] = draft.SellerVatNumber.Provenance,
            [CapturedInvoiceDraft.InvoiceNumberField] = draft.InvoiceNumber.Provenance,
            [CapturedInvoiceDraft.IssuedOnField] = draft.IssuedOn.Provenance,
            [CapturedInvoiceDraft.NetField] = draft.Net.Provenance,
            [CapturedInvoiceDraft.TaxRateField] = draft.TaxRate.Provenance,
            [CapturedInvoiceDraft.TaxTotalField] = draft.TaxTotal.Provenance,
            [CapturedInvoiceDraft.GrossTotalField] = draft.GrossTotal.Provenance,
        };

        if (!string.IsNullOrWhiteSpace(confirmation.ExpenseCategory))
        {
            map[PromotionFields.ExpenseCategory] = FieldProvenance.Typed;
        }

        return map;
    }

    private static CapturedInvoiceDraft Apply(CapturedInvoiceDraft draft, FieldCorrection correction, List<Error> errors)
    {
        switch (correction.Field)
        {
            case CapturedInvoiceDraft.SellerNameField:
                return Guarded(draft, draft.SellerName, errors, correction, value => draft with
                {
                    SellerName = CapturedField<string>.Typed(value),
                });

            case CapturedInvoiceDraft.SellerVatNumberField:
                return Guarded(draft, draft.SellerVatNumber, errors, correction, value => draft with
                {
                    SellerVatNumber = CapturedField<string>.Typed(value),
                });

            case CapturedInvoiceDraft.InvoiceNumberField:
                return Guarded(draft, draft.InvoiceNumber, errors, correction, value => draft with
                {
                    InvoiceNumber = CapturedField<string>.Typed(value),
                });

            case CapturedInvoiceDraft.NetField:
                return Numeric(draft, draft.Net, errors, correction, value => draft with
                {
                    Net = CapturedField<decimal>.Typed(value),
                });

            case CapturedInvoiceDraft.TaxRateField:
                return Numeric(draft, draft.TaxRate, errors, correction, value => draft with
                {
                    TaxRate = CapturedField<decimal>.Typed(value),
                });

            case CapturedInvoiceDraft.TaxTotalField:
                return Numeric(draft, draft.TaxTotal, errors, correction, value => draft with
                {
                    TaxTotal = CapturedField<decimal>.Typed(value),
                });

            case CapturedInvoiceDraft.GrossTotalField:
                return Numeric(draft, draft.GrossTotal, errors, correction, value => draft with
                {
                    GrossTotal = CapturedField<decimal>.Typed(value),
                });

            case CapturedInvoiceDraft.IssuedOnField:
                if (Attested(draft.IssuedOn.Provenance, correction.Field, errors))
                {
                    return draft;
                }

                if (!DateOnly.TryParseExact(correction.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                {
                    errors.Add(CaptureErrors.DateNotIso(correction.Field, correction.Value));
                    return draft;
                }

                return draft with { IssuedOn = CapturedField<DateOnly>.Typed(date) };

            default:
                errors.Add(CaptureErrors.UnknownField("correction", correction.Field));
                return draft;
        }
    }

    private static CapturedInvoiceDraft Guarded<T>(
        CapturedInvoiceDraft draft,
        CapturedField<T> current,
        List<Error> errors,
        FieldCorrection correction,
        Func<string, CapturedInvoiceDraft> apply) =>
        Attested(current.Provenance, correction.Field, errors) ? draft : apply(correction.Value);

    private static CapturedInvoiceDraft Numeric(
        CapturedInvoiceDraft draft,
        CapturedField<decimal> current,
        List<Error> errors,
        FieldCorrection correction,
        Func<decimal, CapturedInvoiceDraft> apply)
    {
        if (Attested(current.Provenance, correction.Field, errors))
        {
            return draft;
        }

        if (!decimal.TryParse(
                correction.Value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal parsed))
        {
            errors.Add(CaptureErrors.NotADecimal(correction.Field, correction.Value));
            return draft;
        }

        return apply(parsed);
    }

    private static bool Attested(FieldProvenance provenance, string field, List<Error> errors)
    {
        if (provenance != FieldProvenance.Attested)
        {
            return false;
        }

        errors.Add(CaptureErrors.AttestedFieldCannotBeRetyped(field));
        return true;
    }
}
