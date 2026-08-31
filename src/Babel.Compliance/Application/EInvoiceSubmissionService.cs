using Babel.Compliance.Abstractions;
using Babel.Compliance.Intake;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Store;
using Babel.Contracts.Compliance;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Compliance.Application;

/// <summary>
/// <b>نقطة دخول الوحدة الحقيقية: من فاتورة مُرحَّلة إلى مستند التزام مُقاصّ أو مُبلَّغ.</b>
/// <para>
/// كانت هذه الخدمة هيكلاً يعيد <c>compliance.not_implemented</c> بينما يعمل المسار كاملاً
/// في <c>Pipeline/</c> ولا يستدعيه شيء — أي نوعٌ عام يبدو أنه الطريق إلى الإرسال وليس
/// كذلك. وهي الآن الطريق فعلاً: تُفوّض إلى <see cref="ComplianceService"/> نفسه الذي
/// تُشغّله الاختبارات، بلا مسار ثانٍ للإرسال
/// (‏<c>docs/evidence/traps.md#fakh-authoritative-entry-point-that-leads-nowhere</c>).
/// </para>
/// <para>
/// The module's real entry point. It used to be a skeleton returning
/// <c>compliance.not_implemented</c> while the whole pipeline sat unreachable in another
/// folder; it now delegates to that same pipeline.
/// </para>
/// <para>
/// <b>ولا تكتب هذه الخدمة في الدفتر ولا تقرأ منه رقماً.</b> القيد مُرحَّل قبل أن تُستدعى،
/// وإشارته تصل في الحقيقة نفسها. ولذلك لا اعتماد هنا على <c>IPostingService</c> —
/// وجوده سابقاً كان يوحي بأن الالتزام يُرحّل، وهو ما تمنعه القاعدتان 1 و12.
/// </para>
/// </summary>
public sealed class EInvoiceSubmissionService : IApplicationService, IElectronicDocumentIntake
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly ComplianceService _compliance;
    private readonly IComplianceStore _store;
    private readonly IFlowPolicy _flowPolicy;

    /// <summary>ينشئ الخدمة.</summary>
    public EInvoiceSubmissionService(
        IEntitlementEnforcer enforcer,
        ComplianceService compliance,
        IComplianceStore store,
        IFlowPolicy flowPolicy)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(compliance);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(flowPolicy);
        _enforcer = enforcer;
        _compliance = compliance;
        _store = store;
        _flowPolicy = flowPolicy;
    }

    /// <summary>
    /// <b>نقطة دخول كتابة.</b> تعمل عند <see cref="EntitlementState.Entitled"/> فقط.
    /// <para>
    /// <b>اشتراك منقضٍ يُوقف الإبلاغ النظامي</b> — والرفض يعود نصّاً بالعربية والإنجليزية
    /// إلى وحدة المصدر، لا يُبتلع. وهذا مقصود ومُعلَن، لا أثر جانبي
    /// (‏<c>docs/evidence/traps.md#fakh-a-lapsed-subscription-silently-stops-a-legal-obligation</c>).
    /// </para>
    /// </summary>
    [RequiresEntitlement(BabelModule.Compliance, EntitlementAccess.Write)]
    public async ValueTask<Result<ElectronicDocumentOutcome>> SubmitPostedDocumentAsync(
        UserId actor,
        TaxableDocumentPosted document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        Result gate = await _enforcer
            .EnsureAsync(document.Tenant, actor, BabelModule.Compliance, EntitlementAccess.Write,
                "Compliance.SubmitPostedDocumentAsync", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ElectronicDocumentOutcome>.Failure(gate.Errors);
        }

        Result<ComplianceDocument> translated = PostedDocumentTranslator.Translate(document, _flowPolicy);
        if (translated.IsFailure)
        {
            return Result<ElectronicDocumentOutcome>.Failure(translated.Errors);
        }

        ComplianceDocument compliance = translated.Value;

        // الحصانة: الهوية مشتقّة من مستند المصدر، فالنداء الثاني يجد سجله قائماً.
        // ولا يُعاد الإرسال من هنا — إعادة الإرسال قرار للمُنسِّق أو لإنسان، لا للمستدعي.
        ComplianceRecord? existing = await _store
            .InTransactionAsync((uow, ct) => uow.GetAsync(compliance.DocumentId, ct), cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<ElectronicDocumentOutcome>.Success(Describe(existing));
        }

        try
        {
            if (compliance.Flow == ComplianceFlow.Clearance)
            {
                ClearanceResult cleared = await _compliance
                    .ClearAsync(compliance, cancellationToken).ConfigureAwait(false);

                return Result<ElectronicDocumentOutcome>.Success(new ElectronicDocumentOutcome(
                    cleared.DocumentId.Value,
                    cleared.DocumentMayBeDelivered,
                    cleared.StatusAr,
                    cleared.StatusEn,
                    cleared.GuidanceAr,
                    cleared.GuidanceEn));
            }

            ReportingReceipt queued = await _compliance
                .QueueForReportingAsync(compliance, cancellationToken).ConfigureAwait(false);

            return Result<ElectronicDocumentOutcome>.Success(new ElectronicDocumentOutcome(
                queued.DocumentId.Value,
                // مسار الإبلاغ: المستند صادر قانوناً ويُسلَّم الآن؛ الجهة تُبلَّغ بعده.
                MayBeDelivered: true,
                ComplianceStatusText.Ar(ComplianceStatus.Queued),
                ComplianceStatusText.En(ComplianceStatus.Queued),
                queued.MessageAr,
                queued.MessageEn));
        }
        catch (IssuingUnitNotReadyException)
        {
            return Result<ElectronicDocumentOutcome>.Failure(IntakeErrors.IssuingUnitNotReady);
        }
        catch (NotSupportedException)
        {
            return Result<ElectronicDocumentOutcome>.Failure(IntakeErrors.FlowNotSupported);
        }
    }

    /// <summary>
    /// <b>نقطة دخول قراءة.</b> تعمل عند <see cref="EntitlementState.ReadOnly"/> أيضاً —
    /// اشتراك منقضٍ لا ينزع سجلاً نظامياً (ADR-0034).
    /// </summary>
    [RequiresEntitlement(BabelModule.Compliance, EntitlementAccess.Read)]
    public async ValueTask<Result<ComplianceView>> ReadSubmissionAsync(
        SharedKernel.TenantId tenant,
        UserId actor,
        string sourceDocumentType,
        string sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Compliance, EntitlementAccess.Read,
                "Compliance.ReadSubmissionAsync", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ComplianceView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(sourceDocumentType) || string.IsNullOrWhiteSpace(sourceDocumentId))
        {
            return Result<ComplianceView>.Failure(IntakeErrors.SourceIdentityMissing);
        }

        ComplianceDocumentId id = PostedDocumentTranslator
            .DocumentIdOf(tenant, sourceDocumentType, sourceDocumentId);

        ComplianceView? view = await _compliance.ViewAsync(id, cancellationToken).ConfigureAwait(false);

        return view is null
            ? Result<ComplianceView>.Failure(NotFound)
            : Result<ComplianceView>.Success(view);
    }

    private static readonly Error NotFound = new(
        "compliance.document_not_found",
        "لا مستند التزام لمستند المصدر هذا. إمّا لم يُسلَّم إلى الالتزام بعد، وإمّا رُفض قبل البناء.",
        "no compliance document exists for this source document: either it was never handed to compliance, or it was refused before being built.");

    private static ElectronicDocumentOutcome Describe(ComplianceRecord record) => new(
        record.DocumentId.Value,
        record.Flow == ComplianceFlow.Reporting || record.IsAccepted,
        ComplianceStatusText.Ar(record.Status),
        ComplianceStatusText.En(record.Status),
        "سُلِّم هذا المستند إلى الالتزام من قبل؛ هذه حالته القائمة ولم يُبنَ مستند ثانٍ ولم يُعَد إرساله.",
        "this document was already handed to compliance; this is its current state. No second document was built and nothing was resubmitted.");
}
