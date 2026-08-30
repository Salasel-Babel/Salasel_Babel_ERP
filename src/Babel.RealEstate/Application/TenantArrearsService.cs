using Babel.Contracts.Subledger;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.RealEstate.Subledger;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Application;

/// <summary>
/// أعمار متأخرات المستأجرين، <b>ومطابقتها بنقطة ضبطها في الدفتر</b>.
/// <para>
/// <b>ووجود هذا التقرير أصلاً نتيجةٌ لقرارٍ محاسبي:</b> القيد يُثبت عند الفوترة لا عند
/// التحصيل، فالذمّة موجودة في الدفتر ويمكن تقادمها. ولو اعتُمدت السياسة البديلة — لا
/// قيد إلا عند التحصيل — لصار هذا التقرير فارغاً من معناه ووجب سحبه، وهو بندٌ معلَّق
/// على قرار المالك (ق-ع-3 في سجلّ العقاري).
/// </para>
/// <para>
/// <b>والمطابقة تُنشر مع الأعمار في الجواب نفسه</b>، لا في مورد ثانٍ: تقريرٌ لا يُفتح
/// لا يكشف انحرافاً، والانحراف الذي لا يُرى هو بالضبط ما وُضع له سجلّ الأدلة.
/// </para>
/// </summary>
public sealed class TenantArrearsService : IApplicationService
{
    /// <summary>نوع الدفتر المساعد كما تعرّفه بيانات الدفتر.</summary>
    internal const string SubledgerKindCode = "tenant";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;
    private readonly IControlPointReader _controlPoint;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="controlPoint">قارئ نقطة الضبط — يصله الجذر التركيبي بالدفتر.</param>
    public TenantArrearsService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime, IControlPointReader controlPoint)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(controlPoint);
        _enforcer = enforcer;
        _database = runtime.Database;
        _controlPoint = controlPoint;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>أعمار المتأخرات ومطابقتها حتى تاريخ.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<(ArrearsReport Aging, ControlReconciliationReport Reconciliation)>> AgingAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.TenantArrears.Aging", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<(ArrearsReport, ControlReconciliationReport)>.Failure(gate.Errors);
        }

        ArrearsReport aging = await BuildAgingAsync(tenant, companyId, asOf, cancellationToken).ConfigureAwait(false);

        Result<ControlPointSnapshot> snapshot = await _controlPoint
            .ReadAsync(tenant, SubledgerKindCode, asOf, cancellationToken).ConfigureAwait(false);

        if (snapshot.IsFailure)
        {
            return Result<(ArrearsReport, ControlReconciliationReport)>.Failure(snapshot.Errors);
        }

        decimal control = snapshot.Value.Net;
        decimal subledger = aging.Totals.Total.Amount;

        List<ReconciliationDivergence> divergences = [];

        // ‏**محاولة عالقة انحرافٌ يُسمّى ولا يُبتلع**: سُجّلت النية ولم يُعرف مصيرها،
        // فالمستند قد يكون في الدفتر ولا تعرف الوحدة، وهو أخطر حالات المطابقة.
        List<DocumentPostingRow> stuck = await _database.Postings
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.DocumentDate <= asOf
                          && row.State == PostingAttemptState.Attempting)
            .OrderBy(row => row.DocumentType).ThenBy(row => row.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (DocumentPostingRow row in stuck)
        {
            divergences.Add(new ReconciliationDivergence(
                row.DocumentType,
                row.DocumentId,
                row.PartyId,
                Money.Of(row.ControlEffect, _currency),
                Money.Zero(_currency),
                Money.Of(row.ControlEffect, _currency),
                DivergenceReason.PostingUnresolved));
        }

        ControlReconciliationReport reconciliation = new(
            asOf,
            Money.Of(subledger, _currency),
            Money.Of(control, _currency),
            Money.Of(subledger - control, _currency),
            subledger == control,
            divergences);

        return Result<(ArrearsReport, ControlReconciliationReport)>.Success((aging, reconciliation));
    }

    private async Task<ArrearsReport> BuildAgingAsync(
        TenantId tenant,
        Guid companyId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        List<RentInvoiceRow> invoices = await _database.RentInvoices
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.CompanyId == companyId
                          && row.State == RealEstateDocumentState.Posted
                          && row.IssuedOn <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TenantReceiptRow> receipts = await _database.TenantReceipts
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.CompanyId == companyId
                          && row.State == RealEstateDocumentState.Posted
                          && row.ReceivedOn <= asOf
                          && row.LesseeId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PartyRow> lessees = await _database.Parties
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.PartyRole == PartyRoles.Lessee)
            .OrderBy(row => row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PaymentScheduleLineRow> schedule = await _database.ScheduleLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, DateOnly> dueByInvoice = new();
        List<RentInvoiceLineRow> lines = await _database.RentInvoiceLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (RentInvoiceRow invoice in invoices)
        {
            // ‏**تاريخ الاستحقاق من جدول الدفعات لا من تاريخ الإصدار**: الفاتورة قد
            // تُصدَر قبل استحقاق قسطها بأسابيع، وعدُّها متأخرة يوم إصدارها يُنتج تقريراً
            // يشتكي من عملاء منتظمين.
            DateOnly due = invoice.IssuedOn;
            foreach (RentInvoiceLineRow line in lines.Where(row => row.InvoiceId == invoice.Id))
            {
                PaymentScheduleLineRow? scheduled = schedule.Find(row => row.Id == line.ScheduleLineId);
                if (scheduled is not null && scheduled.DueOn > due)
                {
                    due = scheduled.DueOn;
                }
            }

            dueByInvoice[invoice.Id] = due;
        }

        List<PartyArrears> parties = [];
        decimal[] totals = new decimal[5];

        foreach (PartyRow lessee in lessees)
        {
            decimal[] buckets = new decimal[5];

            foreach (RentInvoiceRow invoice in invoices.Where(row => row.LesseeId == lessee.Id))
            {
                int bucket = BucketOf(dueByInvoice[invoice.Id], asOf);
                buckets[bucket] += invoice.GrossTotal;
            }

            // ‏**التحصيل يُسقط من الأقدم**: هو ما يفعله المحاسب، وهو ما يجعل الشريحة
            // الأخيرة تصف ديناً حقيقياً لا رقماً يتضخّم مع كل دفعة.
            decimal paid = receipts.Where(row => row.LesseeId == lessee.Id).Sum(row => row.Received);
            for (int index = 4; index >= 0 && paid > 0m; index--)
            {
                decimal applied = Math.Min(paid, buckets[index]);
                buckets[index] -= applied;
                paid -= applied;
            }

            decimal total = buckets.Sum();
            if (total == 0m)
            {
                continue;
            }

            for (int index = 0; index < buckets.Length; index++)
            {
                totals[index] += buckets[index];
            }

            parties.Add(new PartyArrears(
                lessee.Id,
                lessee.Code,
                new TranslatedName(lessee.NameAr),
                Buckets(buckets)));
        }

        return new ArrearsReport(asOf, parties, Buckets(totals));
    }

    /// <summary>الشريحة التي يقع فيها تاريخ استحقاق بالنسبة إلى تاريخ التقرير.</summary>
    private static int BucketOf(DateOnly dueOn, DateOnly asOf)
    {
        int days = asOf.DayNumber - dueOn.DayNumber;
        return days switch
        {
            <= 0 => 0,
            <= 30 => 1,
            <= 60 => 2,
            <= 90 => 3,
            _ => 4,
        };
    }

    private ArrearsBuckets Buckets(decimal[] values) => new(
        Money.Of(values[0], _currency),
        Money.Of(values[1], _currency),
        Money.Of(values[2], _currency),
        Money.Of(values[3], _currency),
        Money.Of(values[4], _currency),
        Money.Of(values.Sum(), _currency));
}
