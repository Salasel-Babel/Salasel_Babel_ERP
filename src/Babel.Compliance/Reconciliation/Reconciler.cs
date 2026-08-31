using System.Globalization;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Store;

namespace Babel.Compliance.Reconciliation;

/// <summary>
/// <b>المطابقة ميزة من الدرجة الأولى.</b> تقارن ثلاث مجموعات على نافذة زمنية:
/// <list type="number">
///   <item>ما رحّله <b>الدفتر</b> من مستندات خاضعة.</item>
///   <item>ما بناه <b>الالتزام</b> من مستندات بعدّاد وسلسلة.</item>
///   <item>ما أقرّت به <b>الجهة</b> فعلاً.</item>
/// </list>
/// وتخرج بفروق مسمّاة، كل فرق منها يحمل ما يكفي إنساناً للحسم.
/// <para/>
/// <b>هذه هي الجهة التي تكتشف الإرسال المكرّر بعد وقوعه</b>، والتي تُبقي «سلسلة بلا فجوات»
/// ادعاءً مُتحقَّقاً منه بدل أن يكون أملاً.
/// </summary>
public sealed class Reconciler(
    IComplianceStore store,
    ILedgerTaxableDocumentSource ledger,
    ComplianceSettings settings,
    TimeProvider clock)
{
    public async Task<ReconciliationReport> RunAsync(
        TenantId tenant, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var now = ComplianceCanonical.PgInstant(clock.GetUtcNow());
        var posted = await ledger.ListAsync(tenant, from, to, ct);

        var compliance = await store.InTransactionAsync(
            (uow, token) => uow.ListAsync(
                new ComplianceQuery(tenant, IssuedFrom: from, IssuedTo: to, Limit: int.MaxValue), token), ct);

        var findings = new List<ReconciliationFinding>();

        findings.AddRange(FindPostedButNeverBuilt(tenant, posted, compliance, now));
        findings.AddRange(FindAcknowledgedButNotPosted(tenant, posted, compliance, now));
        findings.AddRange(FindAmountMismatches(tenant, posted, compliance, now));
        findings.AddRange(FindStuckDocuments(tenant, compliance, now));
        findings.AddRange(FindDuplicateAcceptances(tenant, compliance, now));
        findings.AddRange(await FindChainProblemsAsync(tenant, compliance, now, ct));
        findings.AddRange(FindMissingStampedCopies(tenant, compliance, now));

        if (findings.Count > 0)
            await store.InTransactionAsync((uow, token) => uow.AddFindingsAsync(findings, token), ct);

        // كل وحدة إصدار عليها كسر سلسلة تُوقَف عن الإصدار — تنبيه بأعلى درجة (04-zatca §7).
        foreach (var unit in findings.Where(f => f.Kind == FindingKind.ChainBroken)
                                     .Select(f => f.IssuingUnit)
                                     .OfType<IssuingUnitId>()
                                     .Distinct())
        {
            await store.InTransactionAsync((uow, token) => uow.HaltChainAsync(
                tenant, unit,
                "كُسرت سلسلة التجزئة على هذه الوحدة — الإصدار موقوف حتى المعالجة",
                "the hash chain on this issuing unit is broken — issuance is halted until resolved", token), ct);
        }

        return new ReconciliationReport(tenant, from, to, now, Totals(posted, compliance), findings);
    }

    // -------------------------------------------------------------------

    private static IEnumerable<ReconciliationFinding> FindPostedButNeverBuilt(
        TenantId tenant, IReadOnlyList<PostedTaxableDocument> posted,
        IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        var byEntry = compliance.ToLookup(r => r.JournalEntry.Value);
        foreach (var p in posted.Where(p => !byEntry.Contains(p.JournalEntry.Value)))
            yield return new ReconciliationFinding
            {
                FindingId = Guid.CreateVersion7(),
                Tenant = tenant,
                Kind = FindingKind.PostedButNeverBuilt,
                Severity = FindingSeverity.Critical,
                DetectedAt = now,
                IssuingUnit = p.IssuingUnit,
                JournalEntry = p.JournalEntry,
                ExpectedAmount = p.TaxTotal,
                SummaryAr = $"قيد مُرحَّل لمستند خاضع «{p.DocumentNumber}» بلا سجل التزام إطلاقاً — لم يُبنَ ولم يُرسل.",
                SummaryEn = $"posted taxable document '{p.DocumentNumber}' has no compliance record at all — never built, never sent.",
                NextStepAr = "يُبنى مستند الالتزام لهذا القيد فوراً؛ وإن تعذّر، يُحدَّد سبب عدم استدعاء المُنسِّق عند الترحيل.",
                NextStepEn = "build the compliance document for this entry now; if impossible, find why the orchestrator was not invoked at posting time."
            };
    }

    private static IEnumerable<ReconciliationFinding> FindAcknowledgedButNotPosted(
        TenantId tenant, IReadOnlyList<PostedTaxableDocument> posted,
        IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        var postedEntries = posted.Select(p => p.JournalEntry.Value).ToHashSet();
        foreach (var r in compliance.Where(r => r.IsAccepted && !postedEntries.Contains(r.JournalEntry.Value)))
            yield return new ReconciliationFinding
            {
                FindingId = Guid.CreateVersion7(),
                Tenant = tenant,
                Kind = FindingKind.AcknowledgedButNotPosted,
                Severity = FindingSeverity.Blocking,
                DetectedAt = now,
                DocumentId = r.DocumentId,
                IssuingUnit = r.IssuingUnit,
                Counter = r.Counter,
                JournalEntry = r.JournalEntry,
                ObservedAmount = r.TaxTotal,
                SummaryAr = $"الجهة تُقرّ بمستند «{r.DocumentNumber}» ولا يقابله قيد مُرحَّل في النافذة. " +
                            "لا ينبغي أن يحدث هذا في قناة أحادية الاتجاه.",
                SummaryEn = $"the authority acknowledges document '{r.DocumentNumber}' but no posted entry matches it in the window. " +
                            "This should be impossible in a one-way channel.",
                NextStepAr = "يُفحص هل حُذف القيد أو رُحّل خارج النافذة، أو صدر المستند من مسار لا يمرّ بمحرك الترحيل.",
                NextStepEn = "check whether the entry was deleted, posted outside the window, or issued through a path that bypasses the posting engine."
            };
    }

    private static IEnumerable<ReconciliationFinding> FindAmountMismatches(
        TenantId tenant, IReadOnlyList<PostedTaxableDocument> posted,
        IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        var byEntry = posted.ToDictionary(p => p.JournalEntry.Value);
        foreach (var r in compliance)
        {
            if (!byEntry.TryGetValue(r.JournalEntry.Value, out var p)) continue;
            // مقارنة decimal دقيقة. لا تقريب، ولا حدّ تسامح.
            if (p.TaxTotal == r.TaxTotal && p.GrossTotal == r.GrossTotal && p.NetTotal == r.NetTotal) continue;

            yield return new ReconciliationFinding
            {
                FindingId = Guid.CreateVersion7(),
                Tenant = tenant,
                Kind = FindingKind.AmountMismatch,
                Severity = FindingSeverity.Critical,
                DetectedAt = now,
                DocumentId = r.DocumentId,
                IssuingUnit = r.IssuingUnit,
                Counter = r.Counter,
                JournalEntry = r.JournalEntry,
                ExpectedAmount = p.TaxTotal,
                ObservedAmount = r.TaxTotal,
                SummaryAr = $"اختلاف مبالغ بين القيد والمستند «{r.DocumentNumber}»: " +
                            $"ضريبة القيد {ComplianceCanonical.Money(p.TaxTotal)} والمستند {ComplianceCanonical.Money(r.TaxTotal)}.",
                SummaryEn = $"amount mismatch between entry and document '{r.DocumentNumber}': " +
                            $"entry tax {ComplianceCanonical.Money(p.TaxTotal)} vs document {ComplianceCanonical.Money(r.TaxTotal)}.",
                NextStepAr = "يُصحَّح بمستند تصحيحي (إشعار)، لا بتعديل المستند المرسل ولا بحذف القيد.",
                NextStepEn = "correct with a credit/debit note; never by editing the sent document or deleting the entry."
            };
        }
    }

    private IEnumerable<ReconciliationFinding> FindStuckDocuments(
        TenantId tenant, IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        foreach (var r in compliance)
        {
            switch (r.Status)
            {
                case ComplianceStatus.Built:
                    yield return Stuck(tenant, r, now, FindingKind.BuiltButNeverQueued, FindingSeverity.Critical,
                        "بُني المستند ولم يدخل الطابور إطلاقاً — خلل في المُنسِّق نفسه.",
                        "built but never queued — a defect in the orchestrator itself.",
                        "يُعاد إدراجه في الطابور، ويُفحص لماذا لم يُدرج داخل معاملة البناء.",
                        "requeue it and find why the enqueue did not happen inside the build transaction.");
                    break;

                case ComplianceStatus.Ambiguous:
                    yield return Stuck(tenant, r, now, FindingKind.UnresolvedAmbiguity, FindingSeverity.Critical,
                        "مهلة غامضة لم تُحسم: غادر الطلب ولم يصل جواب، فلا يُعرف هل قبلته الجهة.",
                        "unresolved ambiguity: the request left and no answer came back; acceptance is unknown.",
                        "لا يُعاد الإرسال يدوياً. يُستعلم عن الحالة إن أمكن، وإلا فقرار بشري موثَّق بعد مراجعة سجل المحاولات.",
                        "do not resubmit by hand. Probe the status if possible; otherwise a documented human decision after reviewing the attempt log.");
                    break;

                case ComplianceStatus.NeedsHumanReview:
                    yield return Stuck(tenant, r, now, FindingKind.UnresolvedAmbiguity, FindingSeverity.Blocking,
                        $"بند في الطابور البشري: {r.HumanReviewReasonAr}",
                        $"human queue item: {r.HumanReviewReasonEn}",
                        "يُتَّخذ قرار بشري موثَّق: تأكيد القبول، أو إصدار مستند تصحيحي، أو إعادة إرسال بعلم تام بخطر التكرار.",
                        "take a documented human decision: confirm acceptance, issue a corrective document, or resubmit in full knowledge of the duplication risk.");
                    break;

                case ComplianceStatus.Queued or ComplianceStatus.Submitting
                    when r.QueuedAt is { } q && now - q > settings.QueueAgeAlarm:
                    yield return Stuck(tenant, r, now, FindingKind.QueuedTooLong, FindingSeverity.Warning,
                        string.Create(CultureInfo.InvariantCulture,
                            $"في الطابور منذ {(now - q).TotalHours:0.0} ساعة — يتجاوز عتبة التنبيه المضبوطة للمستأجر."),
                        string.Create(CultureInfo.InvariantCulture,
                            $"queued for {(now - q).TotalHours:0.0} hours — beyond the tenant's configured alarm threshold."),
                        "يُفحص الاتصال بالجهة وعمق الطابور. هذا هو المؤشر الذي يجب أن يراه المدير المالي قبل انقضاء النافذة النظامية.",
                        "check connectivity and queue depth. This is the indicator the finance manager must see before the statutory window closes.");
                    break;
            }
        }
    }

    private static ReconciliationFinding Stuck(
        TenantId tenant, ComplianceRecord r, DateTimeOffset now, FindingKind kind, FindingSeverity severity,
        string summaryAr, string summaryEn, string nextAr, string nextEn) => new()
    {
        FindingId = Guid.CreateVersion7(),
        Tenant = tenant,
        Kind = kind,
        Severity = severity,
        DetectedAt = now,
        DocumentId = r.DocumentId,
        IssuingUnit = r.IssuingUnit,
        Counter = r.Counter,
        JournalEntry = r.JournalEntry,
        ObservedAmount = r.TaxTotal,
        SummaryAr = $"[{r.DocumentNumber}] {summaryAr}",
        SummaryEn = $"[{r.DocumentNumber}] {summaryEn}",
        NextStepAr = nextAr,
        NextStepEn = nextEn
    };

    private static IEnumerable<ReconciliationFinding> FindDuplicateAcceptances(
        TenantId tenant, IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        // مستند واحد قُبل أكثر من مرة بمراجع مزوّد مختلفة = تكرار حقيقي لدى الجهة.
        foreach (var g in compliance.Where(r => r.IsAccepted)
                     .GroupBy(r => (r.IssuingUnit.Value, r.Counter))
                     .Where(g => g.Count() > 1))
        {
            var first = g.First();
            yield return new ReconciliationFinding
            {
                FindingId = Guid.CreateVersion7(),
                Tenant = tenant,
                Kind = FindingKind.DuplicateAcceptance,
                Severity = FindingSeverity.Blocking,
                DetectedAt = now,
                DocumentId = first.DocumentId,
                IssuingUnit = first.IssuingUnit,
                Counter = first.Counter,
                JournalEntry = first.JournalEntry,
                ObservedAmount = g.Sum(x => x.TaxTotal),
                SummaryAr = $"العدّاد {first.Counter} على الوحدة «{first.IssuingUnit}» مقبول {g.Count()} مرات — قبول مكرّر.",
                SummaryEn = $"counter {first.Counter} on unit '{first.IssuingUnit}' is accepted {g.Count()} times — duplicate acceptance.",
                NextStepAr = "يُحدَّد أيّ إرسال هو المعتمد لدى الجهة، ويُعالَج الباقي بمستند تصحيحي. لا يُحذف شيء.",
                NextStepEn = "determine which submission the authority holds as canonical and correct the rest with a corrective document. Delete nothing."
            };
        }
    }

    private async Task<IReadOnlyList<ReconciliationFinding>> FindChainProblemsAsync(
        TenantId tenant, IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now, CancellationToken ct)
    {
        var findings = new List<ReconciliationFinding>();

        foreach (var unitGroup in compliance.GroupBy(r => r.IssuingUnit.Value))
        {
            var unit = new IssuingUnitId(unitGroup.Key);
            var ordered = unitGroup.OrderBy(r => r.Counter).ToList();
            if (ordered.Count == 0) continue;

            // نبدأ من بصمة أول مستند في النافذة، لا من التكوين — النافذة قد لا تبدأ من العدّاد 1.
            var expectedCounter = ordered[0].Counter;
            var expectedPrev = ordered[0].PreviousHash;

            if (expectedCounter == 1)
            {
                var genesis = ComplianceCanonical.Genesis(tenant, unit);
                if (!expectedPrev.AsSpan().SequenceEqual(genesis))
                    findings.Add(Chain(tenant, ordered[0], now,
                        "أول مستند في السلسلة لا يشير إلى بصمة التكوين الصحيحة.",
                        "the first document does not link to the correct genesis hash."));
            }

            foreach (var r in ordered)
            {
                if (r.Counter != expectedCounter)
                {
                    // «الفجوة تُؤكَّد إيجاباً لا تُستنتج من الغياب» — وكل قيمة عدّاد محسوبة، بما فيها الملغى.
                    findings.Add(new ReconciliationFinding
                    {
                        FindingId = Guid.CreateVersion7(),
                        Tenant = tenant,
                        Kind = FindingKind.CounterGap,
                        Severity = FindingSeverity.Blocking,
                        DetectedAt = now,
                        DocumentId = r.DocumentId,
                        IssuingUnit = unit,
                        Counter = r.Counter,
                        SummaryAr = $"فجوة عدّاد على الوحدة «{unit}»: العدّاد المتوقَّع {expectedCounter} والموجود {r.Counter}.",
                        SummaryEn = $"counter gap on unit '{unit}': expected {expectedCounter}, found {r.Counter}.",
                        NextStepAr = "لا يُملأ الفراغ بمستند جديد. يُبحث عن المستند صاحب الرقم المفقود في السجل والنسخ الاحتياطية، " +
                                     "ويُوثَّق سببه — المدقّق يطلب تفسيراً لكل رقم، والملغى أيضاً محسوب.",
                        NextStepEn = "do not backfill. Locate the missing number in the record and in backups and document the cause — " +
                                     "every counter value must be accounted for, cancelled ones included."
                    });
                    expectedCounter = r.Counter;
                }

                if (!r.PreviousHash.AsSpan().SequenceEqual(expectedPrev))
                    findings.Add(Chain(tenant, r, now,
                        $"رابط السلسلة مكسور عند العدّاد {r.Counter}: البصمة السابقة المخزَّنة لا تطابق بصمة سابقه.",
                        $"broken chain link at counter {r.Counter}: the stored previous hash does not match its predecessor."));

                expectedPrev = r.DocumentHash;
                expectedCounter++;
            }

            var head = await store.InTransactionAsync((uow, t) => uow.GetChainHeadAsync(tenant, unit, t), ct);
            if (head is not null && head.NextCounter != expectedCounter && !head.IsHalted)
                findings.Add(new ReconciliationFinding
                {
                    FindingId = Guid.CreateVersion7(),
                    Tenant = tenant,
                    Kind = FindingKind.CounterGap,
                    Severity = FindingSeverity.Warning,
                    DetectedAt = now,
                    IssuingUnit = unit,
                    Counter = head.NextCounter,
                    SummaryAr = $"رأس السلسلة للوحدة «{unit}» يقول إن العدّاد التالي {head.NextCounter} " +
                                $"بينما آخر مستند في النافذة ينتهي عند {expectedCounter - 1}.",
                    SummaryEn = $"chain head for unit '{unit}' says next counter is {head.NextCounter} " +
                                $"while the last document in the window ends at {expectedCounter - 1}.",
                    NextStepAr = "متوقَّع إن كانت النافذة لا تغطي كل المستندات؛ يُتحقَّق بتوسيع النافذة قبل اعتباره خللاً.",
                    NextStepEn = "expected when the window does not cover every document; widen the window before treating it as a defect."
                });
        }

        return findings;
    }

    private static ReconciliationFinding Chain(
        TenantId tenant, ComplianceRecord r, DateTimeOffset now, string ar, string en) => new()
    {
        FindingId = Guid.CreateVersion7(),
        Tenant = tenant,
        Kind = FindingKind.ChainBroken,
        Severity = FindingSeverity.Blocking,
        DetectedAt = now,
        DocumentId = r.DocumentId,
        IssuingUnit = r.IssuingUnit,
        Counter = r.Counter,
        SummaryAr = ar,
        SummaryEn = en,
        NextStepAr = "الإصدار يتوقف على هذه الوحدة فوراً. لا يُعاد بناء السلسلة ولا تُعاد كتابة بصمة — " +
                     "يُحقَّق في مصدر التغيير أولاً.",
        NextStepEn = "issuance on this unit halts immediately. Do not rebuild the chain and do not rewrite any hash — " +
                     "investigate the source of the change first."
    };

    private static IEnumerable<ReconciliationFinding> FindMissingStampedCopies(
        TenantId tenant, IReadOnlyList<ComplianceRecord> compliance, DateTimeOffset now)
    {
        foreach (var r in compliance.Where(r =>
                     r.Flow == ComplianceFlow.Clearance && r.IsAccepted &&
                     (r.StampedDocument is null || r.StampedDocument.Length == 0)))
            yield return new ReconciliationFinding
            {
                FindingId = Guid.CreateVersion7(),
                Tenant = tenant,
                Kind = FindingKind.StampedCopyMissing,
                Severity = FindingSeverity.Warning,
                DetectedAt = now,
                DocumentId = r.DocumentId,
                IssuingUnit = r.IssuingUnit,
                Counter = r.Counter,
                SummaryAr = $"تمّت مقاصة المستند «{r.DocumentNumber}» ولم تُخزَّن النسخة المختومة العائدة من الجهة.",
                SummaryEn = $"document '{r.DocumentNumber}' cleared but the authority's stamped copy was not stored.",
                NextStepAr = "النسخة المعتمدة هي التي تعيدها الجهة. تُجلب وتُخزَّن كما هي، ولا تُشتقّ من قاعدة البيانات.",
                NextStepEn = "the authority's returned copy is the authoritative one. Fetch and store it verbatim; never re-derive it."
            };
    }

    private static ReconciliationTotals Totals(
        IReadOnlyList<PostedTaxableDocument> posted, IReadOnlyList<ComplianceRecord> compliance) =>
        new(
            LedgerDocuments: posted.Count,
            ComplianceDocuments: compliance.Count,
            Accepted: compliance.Count(r => r.IsAccepted),
            Pending: compliance.Count(r => !r.IsSettled),
            Rejected: compliance.Count(r => r.Status == ComplianceStatus.Rejected),
            Unresolved: compliance.Count(r => r.Status is ComplianceStatus.Ambiguous or ComplianceStatus.NeedsHumanReview),
            LedgerTaxTotal: posted.Aggregate(0m, (a, p) => a + p.TaxTotal),
            AcceptedTaxTotal: compliance.Where(r => r.IsAccepted).Aggregate(0m, (a, r) => a + r.TaxTotal),
            QuarantinedTaxTotal: compliance.Where(r => !r.IsAccepted).Aggregate(0m, (a, r) => a + r.TaxTotal));
}
