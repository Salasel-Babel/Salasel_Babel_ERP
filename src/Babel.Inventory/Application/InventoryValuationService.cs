using System.Data;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.Inventory.Subledger;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Inventory.Application;

/// <summary>
/// مطابقة دفتر المخزون المساعد بحسابه الضابط، وجاهزية الفترة للإقفال.
/// <para>
/// <b>المطابقة تُقرأ من ثلاثة طرق مستقلّة إلى الرقم نفسه</b>: مجموع الحركات، ومجموع
/// أرصدة الأصناف، ونقطة الضبط في الدفتر. اثنان يكفيان لكشف انحراف بين الوحدة والدفتر؛
/// والثالث يكشف انحراف <b>الوحدة عن نفسها</b> — رصيدٌ لا يساوي مجموع حركاته — وهو
/// عطلٌ لا يراه أي فحص يقارن طرفين فقط.
/// </para>
/// </summary>
public sealed class InventoryValuationService : IApplicationService
{
    /// <summary>
    /// نوع الدفتر المساعد كما تعرّفه بيانات المصفوفة على سطر <c>inventory_control</c>.
    /// <b>ليس رقم حساب</b>: أي حساب ضابط يُضاف لاحقاً لهذا الدفتر المساعد يدخل
    /// المطابقة من تلقاء نفسه.
    /// </summary>
    internal const string SubledgerKind = "item";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;
    private readonly IControlPointReader _controlPoint;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="controlPoint">قارئ نقطة الضبط — يصله الجذر التركيبي بالدفتر.</param>
    public InventoryValuationService(
        IEntitlementEnforcer enforcer, InventoryRuntime runtime, IControlPointReader controlPoint)
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
    /// يطابق دفتر المخزون بحسابه الضابط حتى تاريخ.
    /// <para>
    /// <b>والمقارنة بحبيبيّة واحدة على الطرفين</b>: نوع المستند ومعرّفه والصنف.
    /// حبيبيّتان مختلفتان تُنتجان انحرافاً على مستند سليم وصافياً قدره صفر — أي تقريراً
    /// يقول «هناك مشكلة» ولا يقول أين (فخ-48).
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<ControlReconciliationReport>> ReconcileAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Reconcile", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ControlReconciliationReport>.Failure(gate.Errors);
        }

        Result<ControlPointSnapshot> control = await _controlPoint
            .ReadAsync(tenant, SubledgerKind, asOf, cancellationToken).ConfigureAwait(false);

        if (control.IsFailure)
        {
            return Result<ControlReconciliationReport>.Failure(
                InventoryErrors.ControlPointUnavailable(control.Errors));
        }

        Dictionary<DocumentKey, decimal> subledger = await MovementEffectsAsync(tenant, asOf, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<DocumentKey, decimal> controlByDocument = [];
        foreach (ControlPointMovement movement in control.Value.Movements)
        {
            DocumentKey key = new(movement.DocumentType, movement.DocumentId, movement.PartyId);
            controlByDocument[key] = controlByDocument.GetValueOrDefault(key) + movement.Net;
        }

        List<ReconciliationDivergence> divergences = [];

        foreach (DocumentKey key in subledger.Keys.Concat(controlByDocument.Keys).Distinct().OrderBy(
                     static k => k.DocumentType + " " + k.DocumentId + " " + k.ItemId, StringComparer.Ordinal))
        {
            decimal mine = subledger.GetValueOrDefault(key);
            decimal theirs = controlByDocument.GetValueOrDefault(key);

            if (mine == theirs)
            {
                continue;
            }

            string reason = !subledger.ContainsKey(key)
                ? DivergenceReason.MissingInSubledger
                : !controlByDocument.ContainsKey(key)
                    ? DivergenceReason.MissingInControl
                    : DivergenceReason.AmountMismatch;

            divergences.Add(new ReconciliationDivergence(
                key.DocumentType,
                key.DocumentId,
                key.ItemId,
                Money.Of(mine, _currency),
                Money.Of(theirs, _currency),
                Money.Of(mine - theirs, _currency),
                reason));
        }

        decimal subledgerTotal = subledger.Values.Sum();
        decimal balanceTotal = await BalanceTotalAsync(tenant, cancellationToken).ConfigureAwait(false);

        return Result<ControlReconciliationReport>.Success(new ControlReconciliationReport(
            asOf,
            Money.Of(subledgerTotal, _currency),
            Money.Of(control.Value.Net, _currency),
            Money.Of(balanceTotal, _currency),
            Money.Of(subledgerTotal - control.Value.Net, _currency),
            subledgerTotal == control.Value.Net && divergences.Count == 0,
            divergences));
    }

    /// <summary>
    /// هل يُقفَل المخزون على هذه الفترة؟
    /// <para>
    /// <b>الرفض هنا جوابٌ مشروع لا عجز.</b> الفترة التي تُقفَل فوق كميةٍ سالبة أو فوق
    /// قيمةٍ بلا كمية تُثبّت في القوائم رقماً لا يقابله واقع في المستودع، ولا يُصحَّح
    /// بعدها إلا بقيدٍ يعترف بأن الإقفال كان خاطئاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="periodCode">رمز الفترة بصيغة <c>yyyy-MM</c>.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<CloseObstacle>>> CloseReadinessAsync(
        TenantId tenant,
        UserId actor,
        string periodCode,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.CloseReadiness", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<CloseObstacle>>.Failure(gate.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        List<CloseObstacle> obstacles = [];

        await using (NpgsqlCommand command = new(
            """
            select "ItemId","WarehouseId","Quantity","ValueAmount"
              from inventory.item_balance
             where "TenantId" = $1 and ("Quantity" < 0 or ("Quantity" <= 0 and "ValueAmount" <> 0))
             order by "ItemId","WarehouseId"
            """, Connection))
        {
            command.Parameters.AddWithValue(tenant.Value);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                decimal quantity = reader.GetDecimal(2);
                decimal value = reader.GetDecimal(3);

                obstacles.Add(new CloseObstacle(
                    reader.GetString(0),
                    reader.GetString(1),
                    quantity,
                    Money.Of(value, _currency),
                    quantity < 0m ? CloseObstacleReason.NegativeQuantity : CloseObstacleReason.ValueWithoutQuantity));
            }
        }

        if (obstacles.Count == 0)
        {
            return Result<IReadOnlyList<CloseObstacle>>.Success(obstacles);
        }

        string[] reasons = [.. obstacles.Select(static o => FormattableString.Invariant(
            $"  - {o.ItemId} @ {o.WarehouseId}: quantity {o.Quantity} / value {o.Value.Amount} / {o.ReasonCode}"))];

        return Result<IReadOnlyList<CloseObstacle>>.Failure(
            InventoryErrors.PeriodNotCloseable(periodCode, reasons));
    }

    /// <summary>
    /// أثر كل حركة على القيمة، مجموعاً بحبيبيّة نوع المستند ومعرّفه والصنف.
    /// الوارد موجب والصادر سالب — <b>وهو منطق «مدين ناقص دائن» نفسه</b> الذي تُقرأ به
    /// نقطة الضبط، لأن مراقبة المخزون حسابُ أصول طبيعتُه مدينة.
    /// </summary>
    private async ValueTask<Dictionary<DocumentKey, decimal>> MovementEffectsAsync(
        TenantId tenant, DateOnly asOf, CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<DocumentKey, decimal> effects = [];

        await using NpgsqlCommand command = new(
            """
            select "DocumentType","DocumentId","ItemId",
                   sum(case when "Direction" = 'IN' then "ValueAmount" else -"ValueAmount" end)
              from inventory.stock_movement
             where "TenantId" = $1 and "OccurredOn" <= $2
             group by "DocumentType","DocumentId","ItemId"
            """, Connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(asOf);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            effects[new DocumentKey(reader.GetString(0), reader.GetString(1), reader.GetString(2))] = reader.GetDecimal(3);
        }

        return effects;
    }

    private async ValueTask<decimal> BalanceTotalAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlCommand command = new(
            """ select coalesce(sum("ValueAmount"), 0) from inventory.item_balance where "TenantId" = $1 """,
            Connection);
        command.Parameters.AddWithValue(tenant.Value);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private NpgsqlConnection Connection => (NpgsqlConnection)_database.Database.GetDbConnection();

    private async ValueTask OpenAsync(CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private readonly record struct DocumentKey(string DocumentType, string DocumentId, string ItemId);
}
