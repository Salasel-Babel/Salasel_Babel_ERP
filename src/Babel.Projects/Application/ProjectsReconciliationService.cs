using System.Globalization;
using Babel.Contracts.Subledger;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// <b>مطابقة هذه الوحدة بدفاترها المساعدة</b> — ومن يُحرّك الحساب الضابط يكتب دفتره
/// المساعد ويطابقه في المسار نفسه (ADR-0041).
/// <para>
/// وكشف المقاولين هو المطابقة المُعلَنة نصّاً في بيانات الدفاتر المساعدة: «كشف
/// المقاولين = رصيد الحساب». وهو يُظهر نقطة الضبط <b>عبر منفذها المُعلَن</b> لا تقريراً
/// يُحتسب جانباً.
/// </para>
/// <para>
/// <b>والقراءة مُضيَّقة على مستندات هذه الوحدة.</b> نقطة الضبط الواحدة يُحرّكها أكثر من
/// وحدة، فمطابقةٌ تقرأ الدفتر <b>بالنوع وحده</b> ثم تقارنه بمستنداتها هي تُصدر انحرافاً
/// على مستأجرٍ سليم. وهذا هو نفسه سبب المُرشِّح المُضاف إلى منفذ نقطة الضبط، وهو
/// التغيير الوحيد المفروض على العقود في هذا التسليم.
/// </para>
/// </summary>
public sealed class ProjectsReconciliationService : IApplicationService
{
    /// <summary>
    /// الوحدة التي كتبت الحركة — <b>وهي ما تُقرأ به نقطة الضبط</b>، لا نوع الدفتر وحده.
    /// <para>
    /// نقطة الضبط الواحدة يُحرّكها أكثر من وحدة، فمطابقةٌ تقرأ الدفتر بالنوع وحده ثم
    /// تقارنه بمستنداتها هي تُصدر انحرافاً على مستأجرٍ سليم. والتضييق <b>بالوحدة لا
    /// بأنواع المستندات</b> كي يبقى القيد اليدوي المكتوب باسم هذه الوحدة مرئياً لها.
    /// </para>
    /// </summary>
    internal const BabelModule OwningModule = BabelModule.Projects;

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly IControlPointReader _controlPoint;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="controlPoint">قارئ نقطة الضبط — يصله الجذر التركيبي بالدفتر.</param>
    public ProjectsReconciliationService(
        IEntitlementEnforcer enforcer,
        ProjectsRuntime runtime,
        IControlPointReader controlPoint)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(controlPoint);
        _enforcer = enforcer;
        _database = runtime.Database;
        _controlPoint = controlPoint;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// كشف المقاولين حتى تاريخ، ومعه مطابقة الدفتر المساعد بنقطة ضبطه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<SubcontractorStatement>> ReadSubcontractorStatementAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.SubcontractorStatement.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SubcontractorStatement>.Failure(gate.Errors);
        }

        Result<ControlPointSnapshot> snapshot = await _controlPoint
            .ReadAsync(
                tenant,
                SubcontractorAdvanceService.SubcontractorSubledger,
                asOf,
                OwningModule,
                cancellationToken)
            .ConfigureAwait(false);

        if (snapshot.IsFailure)
        {
            return Result<SubcontractorStatement>.Failure(ProjectsErrors.ControlPointUnavailable(snapshot.Errors));
        }

        // الدفتر المساعد كما تعرفه هذه الوحدة: صفوف محاولات الترحيل **المُرحَّلة**
        // على دفتر المقاول وحده. والصفّ يحمل الطرف والأثر معاً، فهو المصدر الذي
        // تُبنى عليه المطابقة لا استعلامٌ ثانٍ يُعيد اشتقاقهما.
        List<DocumentPostingRow> postings = await _database.Postings
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.SubledgerKind == SubcontractorAdvanceService.SubcontractorSubledger
                          && row.State == PostingAttemptState.Posted
                          && row.DocumentDate <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SubcontractorRow> parties = await _database.Subcontractors
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .OrderBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<NameTranslationRow> translations = await _database.NameTranslations
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.EntityKind == SubcontractorRegistryService.SubcontractorEntityKind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SubcontractorStatementRow> rows = [];

        foreach (SubcontractorRow party in parties)
        {
            string partyId = party.Id.ToString("D", CultureInfo.InvariantCulture);

            decimal effect = postings
                .Where(row => string.Equals(row.PartyId, partyId, StringComparison.Ordinal))
                .Sum(static row => row.ControlEffect);

            TranslatedName name = new(
                party.NameAr,
                translations
                    .Where(row => row.EntityId == party.Id)
                    .ToDictionary(static row => row.LanguageTag, static row => row.Name, StringComparer.Ordinal));

            rows.Add(new SubcontractorStatementRow(party.Id, party.Code, name, Money.Of(effect, _currency)));
        }

        decimal subledgerTotal = postings.Sum(static row => row.ControlEffect);
        decimal controlTotal = snapshot.Value.Net;
        decimal divergence = subledgerTotal - controlTotal;

        return Result<SubcontractorStatement>.Success(new SubcontractorStatement(
            asOf,
            rows,
            Money.Of(subledgerTotal, _currency),
            Money.Of(controlTotal, _currency),
            Money.Of(divergence, _currency),

            // صفرٌ بالضبط لا «قريب من الصفر»: الفارق بريال واحد فارقٌ يُسمّى.
            divergence == 0m));
    }
}
