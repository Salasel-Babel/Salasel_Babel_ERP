using System.Security.Cryptography;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Store;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// بناء مستند الالتزام. <b>معاملة واحدة تملك كل شيء</b>: حجز خانة السلسلة، وتوليد
/// التمثيل، والختم، وتجميد البايتات، وكتابة السجل، وتقديم رأس السلسلة، وإدراج عنصر
/// الصندوق الصادر — إن كان المسار إبلاغاً.
/// <para/>
/// <b>ثلاث خصائص تُثبَّت هنا وتحمي مسار الحصانة كله لاحقاً:</b>
/// <list type="number">
///   <item>العدّاد يُخصَّص <b>مرة واحدة</b>. لا محاولة إرسال تحرقه، فلا تُنتج مهلة غامضة فجوة.</item>
///   <item>البايتات تُجمَّد وتُخزَّن. لا يُعاد توليد مصنوع مختوم لاحقاً.</item>
///   <item>البصمة تُحسب مرة وتُخزَّن، وهي مفتاح الحصانة الوحيد الذي نملكه.</item>
/// </list>
/// </summary>
public sealed class ComplianceDocumentFactory(
    IComplianceStore store,
    IDocumentRenderer renderer,
    IComplianceProvider provider,
    IIssuingUnitRegistry registry,
    TimeProvider clock)
{
    /// <summary>
    /// يبني المستند ويضعه في الطابور. <b>لا نداء شبكي هنا إطلاقاً</b> —
    /// حتى تحت شكل «المزوّد يحوز المفتاح»، لأن الختم عندها يقع داخل نداء الإرسال لا قبله.
    /// </summary>
    public async Task<ComplianceRecord> BuildAndQueueAsync(ComplianceDocument input, CancellationToken ct)
    {
        var document = ComplianceDocumentNormaliser.Normalise(input);

        var registration = await registry.GetAsync(document.Tenant, document.IssuingUnit, ct)
            ?? throw new IssuingUnitNotReadyException(
                $"وحدة الإصدار «{document.IssuingUnit}» غير مسجّلة لدى المستأجر «{document.Tenant}» " +
                "/ issuing unit is not registered");

        if (!registration.CanIssue)
            throw new IssuingUnitNotReadyException(
                $"وحدة الإصدار «{document.IssuingUnit}» في المرحلة {registration.Stage} ولا تستطيع الإصدار " +
                $"/ issuing unit is in stage {registration.Stage} and cannot issue");

        EnsureChannelExists(document.Flow);

        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());

        return await store.InTransactionAsync(async (uow, token) =>
        {
            // (1) خانة السلسلة تحت قفل الصف — لا SEQUENCE، ولا فجوات.
            var slot = await uow.AllocateChainSlotAsync(document.Tenant, document.IssuingUnit, token);

            // (2) التمثيل: الجسم، والبايتات المقصودة بالتوقيع بعد الاستبعاد، وبصمتنا المجالية.
            var rendered = renderer.Render(document, slot);

            // (3) الختم — الموضع الوحيد الذي يفترق فيه شكلا الحيازة، والمُنسِّق لا يعرف أيّهما.
            var sealingContext = new SealingContext(
                document.Tenant, document.IssuingUnit, registration.Credential, registration.Environment);
            var payload = await provider.Sealer.SealAsync(sealingContext, rendered, token);

            var record = new ComplianceRecord
            {
                DocumentId = document.DocumentId,
                DocumentUuid = document.DocumentUuid,
                Tenant = document.Tenant,
                IssuingUnit = document.IssuingUnit,
                Environment = registration.Environment,
                Kind = document.Kind,
                Flow = document.Flow,
                DocumentNumber = document.DocumentNumber,
                JournalEntry = document.JournalEntry,
                IssuedAt = document.IssuedAt,
                Counter = slot.Counter,
                PreviousHash = slot.PreviousHash.ToArray(),
                DocumentHash = rendered.DomainChainDigest.ToArray(),
                FrozenPayload = payload.Bytes.ToArray(),
                SealState = payload.State,
                SubmissionFingerprint = payload.FingerprintHex,
                RenderedBody = rendered.Body.ToArray(),
                NetTotal = document.Totals.NetTotal,
                TaxTotal = document.Totals.TaxTotal,
                GrossTotal = document.Totals.GrossTotal,
                CurrencyCode = document.CurrencyCode,
                Status = ComplianceStatus.Built
            };

            await uow.InsertAsync(record, token);
            await uow.AdvanceChainHeadAsync(
                document.Tenant, document.IssuingUnit, slot.Counter, record.DocumentHash, token);

            await ComplianceJournal.TransitionAsync(uow, record, ComplianceStatus.Queued,
                actor: "compliance.factory",
                reasonAr: $"بُني المستند بعدّاد {slot.Counter} وجُمِّدت بايتاته ({payload.State})",
                reasonEn: $"built at counter {slot.Counter}; payload frozen ({payload.State})",
                at: now, attempt: null, ct: token);

            // (4) الصندوق الصادر — للإبلاغ وحده. المقاصة لا تمرّ من الطابور أبداً.
            if (document.Flow == ComplianceFlow.Reporting)
            {
                await uow.EnqueueAsync(new ComplianceWorkItem
                {
                    WorkItemId = Guid.CreateVersion7(),
                    DocumentId = record.DocumentId,
                    Tenant = record.Tenant,
                    Kind = ComplianceWorkKind.ReportDocument,
                    NotBefore = now,
                    EnqueuedAt = now
                }, token);
            }

            return record;
        }, ct);
    }

    private void EnsureChannelExists(ComplianceFlow flow)
    {
        var caps = provider.Capabilities;
        switch (flow)
        {
            case ComplianceFlow.Clearance when !caps.SupportsClearance || provider.Clearance is null:
                throw new NotSupportedException(
                    $"المزوّد «{caps.ProviderId}» لا يدعم مسار المقاصة / provider does not support clearance");
            case ComplianceFlow.Reporting when !caps.SupportsReporting || provider.Reporting is null:
                throw new NotSupportedException(
                    $"المزوّد «{caps.ProviderId}» لا يدعم مسار الإبلاغ / provider does not support reporting");
        }
    }

    /// <summary>بصمة محتوى — تُحسب بالطريقة نفسها في كل موضع، فلا يوجد مسار ثانٍ.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
