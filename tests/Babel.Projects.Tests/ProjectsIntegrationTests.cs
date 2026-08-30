using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Contracts.Subledger;
using Babel.Projects.Application;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Babel.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Babel.Projects.Tests;

/// <summary>
/// <b>إثباتات وحدة المقاولات على دفتر أستاذ حقيقي.</b>
/// <para>
/// وكل بند هنا يُثبت أحد شيئين: إمّا أن ما بُني <b>يعمل</b> — والدليل قيدٌ في دفتر
/// حقيقي وحسابٌ ضابط تحرّك بالمبلغ بالضبط — وإمّا أن ما <b>لم يُبنَ يُرفض باسمه</b>
/// برمزٍ مستقرّ ورسالةٍ تسمّي البند المعلَّق. والثاني ليس نقصاً في الاختبار: هو
/// السلوك المطلوب حرفياً، لأن البديل قيدٌ متوازن بأرقامٍ لم يقلها محاسب.
/// </para>
/// <para>
/// <b>ولا نسبة محتجز ولا وعاء ولا قاعدة استرداد ولا سياسة تقريب ولا نسبة ضريبة في هذا
/// الملفّ</b> — ولا في أي ملفّ آخر من هذا التسليم. اختبارٌ يكتب أحدها يُثبّت رقماً لم
/// يقله أحد، ثم يصير الرقم «مُختبَراً» فيُبنى عليه.
/// </para>
/// </summary>
[Collection("projects")]
public sealed class ProjectsIntegrationTests
{
    private static readonly DateOnly March = new(ProjectsTestEnvironment.FiscalYear, 3, 20);

    // ═══════════════════════════════════════════════════════════════════════
    // ‏1 · المسار الذي يعمل: دفعة المقاول المقدمة تُرحَّل، ومرّتين بلا قيدٍ ثانٍ
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>الترحيل الوحيد الذي يقع في هذه الوحدة اليوم، وحصانتُه من بوّابتها.</b>
    /// <para>
    /// ويُثبت أربعة أشياء في نداءٍ واحد: أن <b>مركز التكلفة حُقن</b> — بلا حقنه يرفض
    /// المُخطِّط كل سطر ويرفض قيدُ قاعدة البيانات ما نجا منه؛ وأن <b>واقعة طرف
    /// الخزينة</b> باسمها المضلّل <c>subledger.none</c> وصلت، وإلا رُفض سطر التسوية
    /// بـ<c>MissingSubledger</c>؛ وأن <b>مؤهّل طريقة التسوية</b> وصل، وإلا رُفض
    /// بـ<c>MissingQualifier</c>؛ وأن الوصول الثاني بالهوية نفسها يُرجع <b>معرّف القيد
    /// الأول</b> ولا يُنشئ ثانياً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Subcontractor_advance_posts_once_and_a_second_call_returns_the_same_entry()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        Guid projectId = await harness.ProjectAsync("PRJ-ADV");
        Guid subcontractorId = await harness.SubcontractorAsync("SUB-ADV");
        Guid subcontractId = await harness.SubcontractAsync("SC-ADV", projectId, subcontractorId);

        Result<ProjectsDocumentView> draft = await harness.Advances.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new SubcontractorAdvanceDraft(
                "ADV-001",
                subcontractId,
                March,
                Harness.Sar(1000m),
                "bank",
                "BANK-01",
                GuaranteeId: null),
            token);

        Assert.True(draft.IsSuccess, Describe(draft.Errors));
        Assert.Equal(ProjectsDocumentState.Draft, draft.Value.State);
        Assert.Null(draft.Value.EntryId);

        Result<ProjectsDocumentView> first = await harness.Advances.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(first.IsSuccess, Describe(first.Errors));

        Result<ProjectsDocumentView> second = await harness.Advances.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(second.IsSuccess, Describe(second.Errors));

        Proof.Require(
            first.Value.EntryId is not null
            && !first.Value.AlreadyPosted
            && second.Value.AlreadyPosted
            && second.Value.EntryId == first.Value.EntryId,
            "الترحيل الأول يُنشئ قيداً، والثاني يردّ القيد نفسه موسوماً بـalreadyPosted",
            "الأول: قيد=" + first.Value.EntryId + " · سلفاً=" + first.Value.AlreadyPosted
            + " | الثاني: قيد=" + second.Value.EntryId + " · سلفاً=" + second.Value.AlreadyPosted);

        // وصفٌّ واحد في سجلّ المحاولات لا صفّان: الهوية واحدة.
        int attempts = await harness.Runtime.Database.Postings
            .CountAsync(
                row => row.TenantId == ProjectsTestEnvironment.Tenant.Value
                       && row.DocumentType == SubcontractorAdvanceService.AdvanceDocument
                       && row.DocumentId == draft.Value.Id.ToString("D", CultureInfo.InvariantCulture),
                token);

        // وحركة واحدة في سجلّ الدفعات المقدمة: الرصيد يُشتقّ منها ولا عمود يُنقَص.
        int movements = await harness.Runtime.Database.AdvanceMovements
            .CountAsync(
                row => row.TenantId == ProjectsTestEnvironment.Tenant.Value
                       && row.DocumentId == draft.Value.Id.ToString("D", CultureInfo.InvariantCulture),
                token);

        Proof.Require(
            attempts == 1 && movements == 1,
            "نداءان بالهوية نفسها يتركان صفّ محاولةٍ واحداً وحركةً واحدة",
            "محاولات=" + attempts.ToString(CultureInfo.InvariantCulture)
            + " · حركات=" + movements.ToString(CultureInfo.InvariantCulture));

        // ونقطة الضبط في دفتر أستاذ **حقيقي** تحرّكت بالمبلغ بالضبط. والقراءة مُضيَّقة
        // على ما كتبته هذه الوحدة، لا على نوع الدفتر وحده.
        LedgerControlPointReader control = new(ProjectsTestEnvironment.Ledger.AppConnectionString);

        Result<ControlPointSnapshot> snapshot = await control.ReadAsync(
            ProjectsTestEnvironment.Tenant,
            SubcontractorAdvanceService.SubcontractorSubledger,
            March,
            BabelModule.Projects,
            token);

        Assert.True(snapshot.IsSuccess, Describe(snapshot.Errors));

        // ‏**والحركة تُقاس بمستندها لا بمجموع المستأجر**: بنودُ هذه المجموعة تتشارك
        // مستأجراً واحداً عمداً — كي تكون المطابقة مطابقةَ دفترٍ حقيقي لا دفترٍ معزول
        // لكل بند — فمجموعُه يحمل أثر بنودٍ أخرى. والمقياس الصادق هو حركة هذا المستند.
        ControlPointMovement? movement = snapshot.Value.Movements.FirstOrDefault(
            row => string.Equals(
                row.DocumentId,
                draft.Value.Id.ToString("D", CultureInfo.InvariantCulture),
                StringComparison.Ordinal));

        Proof.Require(
            movement is not null
            && movement.Net == 1000.0000m
            && string.Equals(
                movement.PartyId,
                subcontractorId.ToString("D", CultureInfo.InvariantCulture),
                StringComparison.Ordinal),
            "الحساب الضابط للمقاولين في دفتر حقيقي تحرّك بمبلغ الدفعة بالضبط، ومنسوباً إلى طرفه",
            "حركة المستند = " + (movement is null ? "غائبة" : Proof.Money(movement.Net))
            + " · الطرف = " + (movement?.PartyId ?? "—"));
    }

    /// <summary>
    /// <b>وكشف المقاولين يُطابق نقطة ضبطه بالضبط — صفرٌ لا «قريب من الصفر».</b>
    /// </summary>
    [Fact]
    public async Task The_subcontractor_statement_reconciles_with_its_control_point()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        Guid projectId = await harness.ProjectAsync("PRJ-REC");
        Guid subcontractorId = await harness.SubcontractorAsync("SUB-REC");
        Guid subcontractId = await harness.SubcontractAsync("SC-REC", projectId, subcontractorId);

        Result<ProjectsDocumentView> draft = await harness.Advances.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new SubcontractorAdvanceDraft(
                "ADV-REC", subcontractId, March, Harness.Sar(250m), "cash", "CASH-01", null),
            token);

        Assert.True(draft.IsSuccess, Describe(draft.Errors));

        Result<ProjectsDocumentView> posted = await harness.Advances.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        Result<SubcontractorStatement> statement = await harness.Reconciliation
            .ReadSubcontractorStatementAsync(ProjectsTestEnvironment.Tenant, Harness.Actor, March, token);

        Assert.True(statement.IsSuccess, Describe(statement.Errors));

        Proof.Require(
            statement.Value.IsReconciled && statement.Value.Divergence.Amount == 0m,
            "كشف المقاولين يساوي رصيد الحساب الضابط بالضبط",
            "الدفتر المساعد=" + Proof.Money(statement.Value.SubledgerTotal.Amount)
            + " · نقطة الضبط=" + Proof.Money(statement.Value.ControlTotal.Amount)
            + " · الانحراف=" + Proof.Money(statement.Value.Divergence.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ‏2 · ما لم يُبنَ يُرفض باسمه — والبند المعلَّق يُسمّى في الرسالة
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>مستخلص عميل لعقدٍ بلا صفّ إعداداتٍ معتمد يُرفض رفضاً صريحاً.</b>
    /// <para>
    /// والمسوّدة تُحفظ: الكمّيات المُقاسة واقعةٌ يسجّلها المهندس، والرفض يقع عند
    /// <b>الترحيل</b> حيث يُطلب رقمٌ لا أساس له بعد.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_client_certificate_is_drafted_and_its_posting_is_refused_naming_the_pending_items()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        (Guid contractId, Guid itemId) = await ContractWithOneItemAsync(harness, "PRJ-CERT", "C-CERT");

        Result<CertificateView> draft = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-001",
                contractId,
                1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1),
                March,
                [
                    new CertificateLineDraft(
                        itemId,
                        CertificateLineKind.Work,
                        "أعمال الفترة",
                        new ProjectQuantity(4m, "m3"),
                        Harness.Sar(0m)),
                ]),
            token);

        Assert.True(draft.IsSuccess, Describe(draft.Errors));

        Result<CertificateView> posted = await harness.ClientCertificates.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(posted.IsFailure);
        Error refusal = posted.Errors[0];

        Proof.Require(
            string.Equals(refusal.Code, "projects.contract_policy.pending", StringComparison.Ordinal)
            && draft.Value.PendingPolicy.Count == PendingPolicyItems.All.Count
            && refusal.MessageAr.Length > 0
            && refusal.MessageEn.Length > 0,
            "المسوّدة تُحفظ، والترحيل يُرفض برمزٍ مستقرّ ورسالةٍ بلغتين تسمّي البنود المعلَّقة",
            "الرمز=" + refusal.Code
            + " · البنود المعلَّقة=" + draft.Value.PendingPolicy.Count.ToString(CultureInfo.InvariantCulture)
            + " · " + string.Join(" · ", draft.Value.PendingPolicy.Select(static item => item.Code)));

        // ولا صفّ محاولة ترحيل كُتب: الرفض يقع **قبل** البوّابة، فلا يُلوَّث سجلّ
        // المحاولات بمحاولاتٍ لم تقع.
        int attempts = await harness.Runtime.Database.Postings
            .CountAsync(
                row => row.TenantId == ProjectsTestEnvironment.Tenant.Value
                       && row.DocumentType == ClientCertificateService.CertificateDocument,
                token);

        Assert.Equal(0, attempts);
    }

    /// <summary>
    /// <b>والحجب الثاني مقصود: قرارٌ اعتُمد ولا حاسبَ له بعد.</b>
    /// <para>
    /// نصّ القرار في جدول البنود مبهمٌ على الشيفرة عمداً — من يكتبه محاسب، ومن يبني
    /// حاسبه مهندس بتوقيع ذلك المحاسب. فامتلاء الجدول يرفع الحجب الأول ولا يفتح حساباً
    /// لم يُكتب. <b>والنصّ المكتوب هنا محايدٌ عمداً</b> ولا يسمّي وعاءً ولا قاعدة:
    /// اختبارٌ يسمّيهما يُثبّت جواباً لم يقله أحد.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_approved_resolution_with_no_calculator_still_refuses_by_its_own_name()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        (Guid contractId, Guid itemId) = await ContractWithOneItemAsync(harness, "PRJ-POL", "C-POL");

        foreach (PendingPolicyItem item in PendingPolicyItems.All)
        {
            harness.Runtime.Database.ContractPolicies.Add(new ContractPolicyRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = ProjectsTestEnvironment.Tenant.Value,
                ContractId = contractId,
                ItemCode = item.Code,
                Resolution = "بانتظار صياغة المحاسب",
                ApprovedBy = "محاسب الاختبار",
                ApprovedOn = March,
            });
        }

        await harness.Runtime.Database.SaveChangesAsync(token);

        Result<CertificateView> draft = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-POL",
                contractId,
                1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1),
                March,
                [
                    new CertificateLineDraft(
                        itemId, CertificateLineKind.Work, "أعمال", new ProjectQuantity(1m, "m3"), Harness.Sar(0m)),
                ]),
            token);

        Assert.True(draft.IsSuccess, Describe(draft.Errors));
        Assert.Empty(draft.Value.PendingPolicy);

        Result<CertificateView> posted = await harness.ClientCertificates.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(posted.IsFailure);

        Proof.Require(
            string.Equals(
                posted.Errors[0].Code,
                "projects.contract_policy.resolution_not_implemented",
                StringComparison.Ordinal),
            "امتلاء جدول البنود يرفع الحجب الأول ويترك الثاني: الحاسب يتبع القرار ولا يسبقه",
            "البنود المعلَّقة بعد الاعتماد=" + draft.Value.PendingPolicy.Count.ToString(CultureInfo.InvariantCulture)
            + " · رمز الرفض=" + posted.Errors[0].Code);
    }

    /// <summary>
    /// <b>مستخلص باطنٍ يحمل سطر غرامة يُرفض باسمه — ولا يُخصم من قيمة الأعمال.</b>
    /// <para>
    /// والسطر <b>يبقى مخزَّناً</b>: الغرامة واقعةٌ حدثت، وحذفها لأن القالب لا يحملها
    /// إخفاءٌ لا حلّ.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_subcontractor_certificate_carrying_a_penalty_line_is_refused_and_the_line_is_kept()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        Guid projectId = await harness.ProjectAsync("PRJ-PEN");
        Guid subcontractorId = await harness.SubcontractorAsync("SUB-PEN");
        Guid subcontractId = await harness.SubcontractAsync("SC-PEN", projectId, subcontractorId);

        Result<IReadOnlyList<SubcontractLineView>> lines = await harness.Subcontractors
            .ListSubcontractLinesAsync(ProjectsTestEnvironment.Tenant, Harness.Actor, subcontractId, token);

        Assert.True(lines.IsSuccess, Describe(lines.Errors));

        Result<CertificateView> draft = await harness.SubcontractorCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "SIPC-PEN",
                subcontractId,
                1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1),
                March,
                [
                    new CertificateLineDraft(
                        lines.Value[0].Id,
                        CertificateLineKind.Work,
                        "أعمال المقاول",
                        new ProjectQuantity(3m, "m3"),
                        Harness.Sar(0m)),
                    new CertificateLineDraft(
                        null,
                        CertificateLineKind.Penalty,
                        "غرامة تأخير",
                        new ProjectQuantity(0m, "m3"),
                        Harness.Sar(50m)),
                ]),
            token);

        Assert.True(draft.IsSuccess, Describe(draft.Errors));

        Result<CertificateView> posted = await harness.SubcontractorCertificates.PostAsync(
            ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(posted.IsFailure);

        Result<CertificateView> reread = await harness.SubcontractorCertificates
            .GetAsync(ProjectsTestEnvironment.Tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(reread.IsSuccess, Describe(reread.Errors));

        int penalties = reread.Value.Lines
            .Count(static line => !string.Equals(line.LineKind, CertificateLineKind.Work, StringComparison.Ordinal));

        Proof.Require(
            string.Equals(posted.Errors[0].Code, "projects.penalty_line_has_no_template", StringComparison.Ordinal)
            && penalties == 1,
            "سطر الغرامة يُخزَّن ويُرفض الترحيل باسمه — ولا يُخصم من قيمة الأعمال بصمت",
            "رمز الرفض=" + posted.Errors[0].Code
            + " · سطور الغرامة المحفوظة=" + penalties.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ‏3 · الاشتقاق التراكمي والوحدات
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>مسوّدةٌ لا تُزيح الأساس التراكمي — والأساس من المُرحَّل وحده (فخ-44).</b>
    /// <para>
    /// وهذا هو الاختبار الذي كان سيمرّ صامتاً لو اشتُقّ الأساس من آخر ما أُنشئ: مستخلصٌ
    /// ثانٍ يقرأ كمّيةً سابقة من مسوّدةٍ لم يُرحَّل قيدُها، فيُنتج إيراداً ناقصاً بلا
    /// استثناء ولا رسالة.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_draft_never_moves_the_cumulative_base_because_the_base_is_the_posted_one()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        (Guid contractId, Guid itemId) = await ContractWithOneItemAsync(harness, "PRJ-CUM", "C-CUM");

        Result<CertificateView> first = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-CUM-1", contractId, 1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1), March,
                [
                    new CertificateLineDraft(
                        itemId, CertificateLineKind.Work, "الفترة الأولى", new ProjectQuantity(4m, "m3"), Harness.Sar(0m)),
                ]),
            token);

        Assert.True(first.IsSuccess, Describe(first.Errors));

        Result<CertificateView> second = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-CUM-2", contractId, 2,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 4, 1),
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 4, 30),
                [
                    new CertificateLineDraft(
                        itemId, CertificateLineKind.Work, "الفترة الثانية", new ProjectQuantity(7m, "m3"), Harness.Sar(0m)),
                ]),
            token);

        Assert.True(second.IsSuccess, Describe(second.Errors));

        Proof.Require(
            first.Value.Lines[0].PreviousQuantity.Magnitude == 0m
            && second.Value.Lines[0].PreviousQuantity.Magnitude == 0m
            && second.Value.Lines[0].CumulativeQuantity.Magnitude == 7m,
            "الكمّية السابقة تُقرأ من المُرحَّل وحده، فمسوّدةٌ سابقة لا تُزيح الأساس",
            "الأولى: سابق=" + first.Value.Lines[0].PreviousQuantity.Magnitude.ToString(CultureInfo.InvariantCulture)
            + " · الثانية: سابق=" + second.Value.Lines[0].PreviousQuantity.Magnitude.ToString(CultureInfo.InvariantCulture)
            + " تراكمي=" + second.Value.Lines[0].CumulativeQuantity.Magnitude.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <b>سطرٌ بوحدةٍ تخالف وحدة بنده يُرفض ولا يُحوَّل.</b>
    /// <para>
    /// قاعدة التحويل يملكها المخزون، ونسخةٌ ثانية منها هنا تنحرف عن أصلها عند أول
    /// تعديل. و«عشرة» بلا وحدةٍ صحيحة ليست معلومة: عشرة أمتار مكعّبة أم عشرة أطنان؟
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_line_whose_unit_differs_from_its_item_is_refused_never_converted()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        (Guid contractId, Guid itemId) = await ContractWithOneItemAsync(harness, "PRJ-UNIT", "C-UNIT");

        Result<CertificateView> draft = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-UNIT", contractId, 1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1), March,
                [
                    new CertificateLineDraft(
                        itemId, CertificateLineKind.Work, "أعمال", new ProjectQuantity(4m, "ton"), Harness.Sar(0m)),
                ]),
            token);

        Assert.True(draft.IsFailure);

        Proof.Require(
            string.Equals(draft.Errors[0].Code, "projects.unit_mismatch", StringComparison.Ordinal),
            "الوحدة المخالفة تُرفض باسمها ولا تُحوَّل داخل هذه الوحدة",
            "رمز الرفض=" + draft.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ‏4 · القبول: غياب الملفّ رفضٌ لا فتح، والقدرة تفرّق مستأجرين متطابقين
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>مستأجرٌ بلا ملفّ قدرات يُرفض</b> — لا لأنه بلا قيود بل لأنه لم يُقرَّر بعد ما اشتراه.
    /// </summary>
    [Fact]
    public async Task A_tenant_with_no_capability_profile_is_refused_rather_than_opened()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateWithoutProfilesAsync(token);

        (Guid contractId, Guid itemId) = await ContractWithOneItemAsync(harness, "PRJ-NOPROF", "C-NOPROF");

        Result<CertificateView> draft = await harness.ClientCertificates.DraftAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new CertificateDraft(
                "IPC-NOPROF", contractId, 1,
                new DateOnly(ProjectsTestEnvironment.FiscalYear, 3, 1), March,
                [
                    new CertificateLineDraft(
                        itemId, CertificateLineKind.Work, "أعمال", new ProjectQuantity(1m, "m3"), Harness.Sar(0m)),
                ]),
            token);

        Assert.True(draft.IsFailure);

        Proof.Require(
            string.Equals(draft.Errors[0].Code, "projects.capability_profile_missing", StringComparison.Ordinal),
            "غياب ملفّ القدرات رفضٌ صريح لا فتحٌ صامت",
            "رمز الرفض=" + draft.Errors[0].Code);
    }

    /// <summary>
    /// <b>القدرة تفرّق مستأجرين لا يفترقان في شيء آخر.</b>
    /// <para>
    /// دفتر أستاذ مبذور بالكامل للاثنين، وشيفرة واحدة، ومصفوفة واحدة — <b>والفارق صفّ
    /// ملفّ قدرات</b>. ولو كان الرفض عند المُطفأ ناتجاً عن نقصٍ في الحساب أو في المصفوفة
    /// لسقط عند المُشغَّل أيضاً، فوجود الطرفين هو ما يجعل الإثبات إثباتاً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_retention_capability_separates_two_otherwise_identical_tenants()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateWithoutProfilesAsync(token);

        await harness.SaveProfileAsync(
            ProjectsTestEnvironment.RetentionEnabledTenant, advance: true, retention: true, token);
        await harness.SaveProfileAsync(
            ProjectsTestEnvironment.RetentionDisabledTenant, advance: true, retention: false, token);

        Guid absent = Guid.CreateVersion7();

        Result<ProjectsDocumentView> enabled = await harness.Retention.PostCollectionAsync(
            ProjectsTestEnvironment.RetentionEnabledTenant, Harness.Actor, absent, token);

        Result<ProjectsDocumentView> disabled = await harness.Retention.PostCollectionAsync(
            ProjectsTestEnvironment.RetentionDisabledTenant, Harness.Actor, absent, token);

        Assert.True(enabled.IsFailure);
        Assert.True(disabled.IsFailure);

        // المُشغَّل يعبر القبول ويسقط بعده على «لا مستند» — وهو رفضٌ من مسارٍ لاحق.
        // والمُطفأ يسقط **عند القبول نفسه** ولا يبلغ المستند أصلاً.
        Proof.Require(
            string.Equals(enabled.Errors[0].Code, "projects.not_found", StringComparison.Ordinal)
            && disabled.Errors[0].Code.StartsWith("document_admission.", StringComparison.Ordinal),
            "المسار نفسه يعبر القبول عند مستأجرٍ شغّل القدرة ويُرفض عند مستأجرٍ أطفأها",
            "المُشغَّل=" + enabled.Errors[0].Code + " · المُطفأ=" + disabled.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ‏5 · ما يتبع البند المعلَّق: لا محتجز يُفرَج عنه ولا يُحصَّل
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>لا إفراج عن رصيدٍ لم يُثبته قيد</b> — وسجلّ المحتجزات فارغ ما دام أول مستخلص محجوباً.
    /// </summary>
    [Fact]
    public async Task No_retention_is_released_because_no_movement_was_ever_posted()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(token);

        Result<RetentionRegister> register = await harness.Retention
            .ReadRegisterAsync(ProjectsTestEnvironment.Tenant, Harness.Actor, March, token);

        Assert.True(register.IsSuccess, Describe(register.Errors));

        Result<ProjectsDocumentView> release = await harness.Retention.DraftReleaseAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new RetentionReleaseDraft("REL-001", Guid.CreateVersion7(), March, Harness.Sar(10m), "مدير المشروع"),
            token);

        Assert.True(release.IsFailure);

        Proof.Require(
            string.Equals(release.Errors[0].Code, "projects.retention_movement_not_found", StringComparison.Ordinal)
            && register.Value.Rows.Count == 0
            && register.Value.ReceivableTotal.Amount == 0m
            && register.Value.PayableTotal.Amount == 0m,
            "سجلّ المحتجزات مشتقٌّ من المُرحَّل، فلا حركة تُفرَج ما دام أول مستخلص محجوباً",
            "صفوف السجلّ=" + register.Value.Rows.Count.ToString(CultureInfo.InvariantCulture)
            + " · رمز رفض الإفراج=" + release.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ‏6 · هوية الإحكام: رمز الحدث حقلٌ فيها لا وصفٌ للقيد
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>رمز الحدث مكوّنٌ في مفتاح الحصانة</b> — فحدثان للمستند نفسه عند الإطلاق نفسه
    /// مفتاحان لا مفتاح واحد.
    /// <para>
    /// ولولا ذلك لابتُلع الحدث الثاني بصمت وعادت الوحدة بإيصال القيد الأول، فخُزّن معرّف
    /// قيدٍ في مكان قيدٍ آخر (ADR-0016 · ADR-0017 · فخ-45).
    /// </para>
    /// </summary>
    [Fact]
    public void The_event_code_is_a_component_of_the_idempotency_key()
    {
        Guid document = new("11111111-2222-4333-8444-555555555555");

        string first = ProjectsPostingGateway.IdempotencyKeyOf(
            SubcontractorAdvanceService.AdvanceDocument,
            document,
            PostingTrigger.OnSettlement,
            1,
            new PostingEventCode("projects.subcontractor_advance.paid"));

        string second = ProjectsPostingGateway.IdempotencyKeyOf(
            SubcontractorAdvanceService.AdvanceDocument,
            document,
            PostingTrigger.OnSettlement,
            1,
            new PostingEventCode("projects.retention.released"));

        string repeat = ProjectsPostingGateway.IdempotencyKeyOf(
            SubcontractorAdvanceService.AdvanceDocument,
            document,
            PostingTrigger.OnSettlement,
            1,
            new PostingEventCode("projects.subcontractor_advance.paid"));

        Proof.Require(
            !string.Equals(first, second, StringComparison.Ordinal)
            && string.Equals(first, repeat, StringComparison.Ordinal)
            && first.StartsWith("projects:v1:", StringComparison.Ordinal),
            "رمز الحدث يغيّر المفتاح، والهوية نفسها تُنتجه ثابتاً",
            "الأول=" + first[..24] + "… · الثاني=" + second[..24] + "…");
    }

    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// عقدٌ ببندٍ واحد في جدول كمياته.
    /// <para>
    /// <b>ونسبة المحتجز صفر لأن العقد لا ينصّ على محتجز</b> — لا لأن الاختبار يختار
    /// نسبة. وكتابةُ نسبةٍ هنا كانت ستُثبّت رقماً لم يقله محاسب ثم يصير «مُختبَراً».
    /// </para>
    /// </summary>
    private static async Task<(Guid ContractId, Guid ItemId)> ContractWithOneItemAsync(
        Harness harness,
        string projectCode,
        string contractNumber)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Guid projectId = await harness.ProjectAsync(projectCode);

        Result<ContractView> contract = await harness.Registry.CreateContractAsync(
            ProjectsTestEnvironment.Tenant,
            Harness.Actor,
            new ContractDraft(
                contractNumber,
                projectId,
                "CUST-01",
                new DateOnly(2026, 1, 10),
                RetentionRate: 0m,
                GuaranteeMonths: 12,
                Items:
                [
                    new BoqItemDraft(
                        "B-1", "حفر وردم", new ProjectQuantity(100m, "m3"), Money.Of(1m, CurrencyCode.Sar)),
                ]),
            token);

        if (contract.IsFailure)
        {
            throw new InvalidOperationException(contract.Errors[0].ToString());
        }

        Result<IReadOnlyList<BoqItemView>> items = await harness.Registry
            .ListBoqItemsAsync(ProjectsTestEnvironment.Tenant, Harness.Actor, contract.Value.Id, token);

        return items.IsFailure
            ? throw new InvalidOperationException(items.Errors[0].ToString())
            : (contract.Value.Id, items.Value[0].Id);
    }

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" | ", errors.Select(static error => error.ToString()));
}
