using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>بندٌ مرجعي يُقاس عليه سطر المستخلص: رمزه ووحدته.</summary>
/// <param name="Id">معرّف البند.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Unit">وحدته كما كُتبت في العقد.</param>
internal sealed record MeasuredItem(Guid Id, string Code, string Unit);

/// <summary>
/// <b>بناء سطور المستخلص التراكمي — والأساس المطروح منه هو المُرحَّل وحده.</b>
/// <para>
/// فخ-44 بنصّه: «الإعفاء في دفتر مساعد يُشتقّ من المُرحَّل وحده؛ حقل حجزٍ يزيد عند
/// المسوّدة لا يُطرح من بند مفتوح». ومسوّدةٌ تُزيح الأساس تُنتج إيراداً مضاعفاً أو
/// ناقصاً <b>بلا استثناء ولا رسالة</b> — فالكمّية السابقة تُقرأ من مستخلصاتٍ حالتها
/// <c>POSTED</c> لا من آخر ما أُنشئ.
/// </para>
/// <para>
/// <b>ولا تحويل وحدات هنا:</b> السطر الذي تخالف وحدتُه وحدةَ بنده <b>يُرفض</b>. وقاعدة
/// التحويل يملكها المخزون، ونسخةٌ ثانية منها في هذه الوحدة تنحرف عن أصلها عند أول تعديل.
/// </para>
/// </summary>
internal static class CumulativeLines
{
    /// <summary>
    /// الكمّيات السابقة لكل بند، مقروءةً من سطور المستخلصات <b>المُرحَّلة</b> وحدها.
    /// </summary>
    /// <param name="database">جداول الوحدة.</param>
    /// <param name="tenantId">المستأجر.</param>
    /// <param name="ownerType">صنف المستند المالك.</param>
    /// <param name="postedCertificateIds">معرّفات المستخلصات المُرحَّلة على هذا العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Dictionary<Guid, decimal>> PreviousQuantitiesAsync(
        ProjectsDbContext database,
        Guid tenantId,
        string ownerType,
        IReadOnlyCollection<Guid> postedCertificateIds,
        CancellationToken cancellationToken)
    {
        if (postedCertificateIds.Count == 0)
        {
            return [];
        }

        List<CertificateLineRow> lines = await database.CertificateLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId
                          && row.OwnerType == ownerType
                          && postedCertificateIds.Contains(row.OwnerId)
                          && row.LineKind == CertificateLineKind.Work
                          && row.ItemId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> previous = [];

        foreach (CertificateLineRow line in lines)
        {
            Guid itemId = line.ItemId!.Value;

            // التراكمي **أعلى ما بلغه** مستخلصٌ مُرحَّل على هذا البند، لا مجموع السطور:
            // جمعُ الكمّيات التراكمية يُضاعف كل فترة سابقة.
            if (!previous.TryGetValue(itemId, out decimal seen) || line.CumulativeQuantity > seen)
            {
                previous[itemId] = line.CumulativeQuantity;
            }
        }

        return previous;
    }

    /// <summary>
    /// يبني صفوف السطور من مسوّداتها بعد فحص الوحدة وعدم النزول عن آخر مُرحَّل.
    /// </summary>
    /// <param name="tenantId">المستأجر.</param>
    /// <param name="ownerType">صنف المستند المالك.</param>
    /// <param name="ownerId">معرّف المستند.</param>
    /// <param name="drafts">مسوّدات السطور.</param>
    /// <param name="items">البنود المرجعية بمعرّفاتها.</param>
    /// <param name="previous">الكمّيات السابقة من المُرحَّل.</param>
    public static Result<List<CertificateLineRow>> Build(
        Guid tenantId,
        string ownerType,
        Guid ownerId,
        IReadOnlyList<CertificateLineDraft> drafts,
        IReadOnlyDictionary<Guid, MeasuredItem> items,
        IReadOnlyDictionary<Guid, decimal> previous)
    {
        List<CertificateLineRow> rows = [];
        int lineNo = 0;

        foreach (CertificateLineDraft draft in drafts)
        {
            lineNo++;

            if (draft.Amount.Amount < 0m)
            {
                return Result<List<CertificateLineRow>>.Failure(
                    ProjectsErrors.NegativeAmount(nameof(draft.Amount)));
            }

            if (!string.Equals(draft.LineKind, CertificateLineKind.Work, StringComparison.Ordinal))
            {
                // سطر غرامة أو خصم: بلا بند وبلا كمّية، ومبلغُه يُدخله المستخدم.
                rows.Add(new CertificateLineRow
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantId,
                    OwnerType = ownerType,
                    OwnerId = ownerId,
                    LineNo = lineNo,
                    LineKind = draft.LineKind,
                    ItemId = null,
                    DescriptionAr = draft.DescriptionAr,
                    Unit = draft.CumulativeQuantity.Unit,
                    CumulativeQuantity = 0m,
                    PreviousQuantity = 0m,
                    Amount = draft.Amount.Amount,
                });
                continue;
            }

            if (draft.ItemId is not { } itemId || !items.TryGetValue(itemId, out MeasuredItem? item))
            {
                return Result<List<CertificateLineRow>>.Failure(
                    ProjectsErrors.NotFound("boq_item", draft.ItemId ?? Guid.Empty));
            }

            if (!string.Equals(draft.CumulativeQuantity.Unit, item.Unit, StringComparison.Ordinal))
            {
                return Result<List<CertificateLineRow>>.Failure(
                    ProjectsErrors.UnitMismatch(item.Code, draft.CumulativeQuantity.Unit, item.Unit));
            }

            if (draft.CumulativeQuantity.Magnitude < 0m)
            {
                return Result<List<CertificateLineRow>>.Failure(
                    ProjectsErrors.NegativeAmount(nameof(draft.CumulativeQuantity)));
            }

            decimal before = previous.TryGetValue(itemId, out decimal seen) ? seen : 0m;

            if (draft.CumulativeQuantity.Magnitude < before)
            {
                return Result<List<CertificateLineRow>>.Failure(
                    ProjectsErrors.CumulativeQuantityWentDown(item.Code));
            }

            rows.Add(new CertificateLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                OwnerType = ownerType,
                OwnerId = ownerId,
                LineNo = lineNo,
                LineKind = CertificateLineKind.Work,
                ItemId = itemId,
                DescriptionAr = draft.DescriptionAr,
                Unit = item.Unit,
                CumulativeQuantity = draft.CumulativeQuantity.Magnitude,
                PreviousQuantity = before,
                Amount = 0m,
            });
        }

        return Result<List<CertificateLineRow>>.Success(rows);
    }
}
