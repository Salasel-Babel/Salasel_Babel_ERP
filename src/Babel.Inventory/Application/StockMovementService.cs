using System.Data;
using Babel.Contracts.Inventory;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Babel.Inventory.Application;

/// <summary>
/// دفتر المخزون المساعد: حركات الكمية والقيمة، ومتوسط التكلفة المتحرّك، و<b>حساب تكلفة
/// كل صادر</b>.
/// <para>
/// <b>هذه الخدمة هي المُنتِج الوحيد لـ<see cref="InventoryMovementCost"/> في المنتج
/// كلّه.</b> قبلها كان مستدعي <c>PostCostOfSalesAsync</c> في وحدة المبيعات يُسلّم
/// المبلغ بنفسه — فكانت المصفوفة تُسمّي واقعةً («تكلفة الأصناف المباعة بطريقة التكلفة
/// المعتمدة») ولا يحسبها شيء. الحارس على ذلك في
/// <c>tests/Babel.ArchitectureTests/InventoryValuationIsTheOnlySourceOfCostOfSales.cs</c>.
/// </para>
/// <para>
/// <b>ولا رقم حساب في هذا الملف كله</b> (القاعدة 2): الوحدة تمسك كميةً وقيمة، والدفتر
/// يمسك الحساب الضابط، والمطابقة بينهما وظيفةٌ مُعلنة في <c>Subledger/</c>.
/// </para>
/// <para>
/// <b>والكتابة كلّها بعبارات صريحة لا بتتبّع تغييرات:</b> رصيد جارٍ يُحدَّث بـ
/// <c>INSERT … ON CONFLICT DO UPDATE</c> وحده، وعدد الصفوف يُؤكَّد بعد كل عبارة —
/// ‏PostgreSQL يعتبر «أصبتُ صفر صفوف» نجاحاً (‏فخ-09 · فخ-05).
/// </para>
/// </summary>
public sealed class StockMovementService : IApplicationService, IInventoryValuation
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public StockMovementService(IEntitlementEnforcer enforcer, InventoryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يسجّل وارداً بتكلفته الفعلية.</summary>
    /// <param name="receipt">الوارد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<InventoryMovementCost>> ReceiveAsync(
        InventoryReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        Result gate = await _enforcer
            .EnsureAsync(receipt.Tenant, receipt.Actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Receive", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(gate.Errors);
        }

        if (receipt.Quantity <= 0m)
        {
            return Result<InventoryMovementCost>.Failure(InventoryErrors.QuantityNotPositive(receipt.Quantity));
        }

        return await WriteAsync(
            receipt.Tenant,
            receipt.Actor,
            receipt.Source,
            receipt.Location,
            receipt.Quantity,
            receipt.OccurredOn,
            MovementDirection.In,
            position => WeightedAverageCost.Receive(position, receipt.Quantity, receipt.Cost.Amount),
            requiresCostBasis: false,
            againstKey: string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يسجّل صادراً <b>ويحسب تكلفته</b>.</summary>
    /// <param name="issue">الصادر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<InventoryMovementCost>> IssueAsync(
        InventoryIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        Result gate = await _enforcer
            .EnsureAsync(issue.Tenant, issue.Actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Issue", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(gate.Errors);
        }

        if (issue.Quantity <= 0m)
        {
            return Result<InventoryMovementCost>.Failure(InventoryErrors.QuantityNotPositive(issue.Quantity));
        }

        return await WriteAsync(
            issue.Tenant,
            issue.Actor,
            issue.Source,
            issue.Location,
            issue.Quantity,
            issue.OccurredOn,
            MovementDirection.Out,
            position => WeightedAverageCost.Issue(position, issue.Quantity),
            requiresCostBasis: true,
            againstKey: string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يسجّل مرتجعاً <b>بتكلفة صرفه الأصلي</b>.
    /// <para>
    /// «بنفس تكلفة قيد البيع الأصلي لا بتكلفة اليوم» — نصّ
    /// <c>sales.credit_note.cost_of_sales</c> في المصفوفة. ولذلك يحمل الطلب هوية الصرف
    /// الأصلي: بحثٌ بالصنف وحده كان سيختار «آخر صرف» وهو اختيارٌ لا يقرّره أحد.
    /// </para>
    /// </summary>
    /// <param name="movement">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<InventoryMovementCost>> ReturnAsync(
        InventoryReturn movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        Result gate = await _enforcer
            .EnsureAsync(movement.Tenant, movement.Actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Return", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(gate.Errors);
        }

        if (movement.Quantity <= 0m)
        {
            return Result<InventoryMovementCost>.Failure(InventoryErrors.QuantityNotPositive(movement.Quantity));
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        Result<RecordedMovement> original = await ReadMovementAsync(
            movement.Tenant, movement.OriginalIssue, null, cancellationToken).ConfigureAwait(false);

        if (original.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(original.Errors);
        }

        RecordedMovement issue = original.Value;
        decimal returned = await ReturnedAgainstAsync(movement.Tenant, movement.OriginalIssue, cancellationToken)
            .ConfigureAwait(false);

        if (returned + movement.Quantity > issue.Quantity)
        {
            return Result<InventoryMovementCost>.Failure(
                InventoryErrors.ReturnExceedsIssue(issue.Quantity, returned, movement.Quantity));
        }

        // ردٌّ كامل للصرف الواحد يستعيد **قيمته بالضبط**، لا حاصل ضرب يعيد بناءها
        // بتقريبٍ ثانٍ. وردٌّ جزئي يُقيَّم بتكلفة وحدة ذلك الصرف نفسه.
        decimal cost = returned == 0m && movement.Quantity == issue.Quantity
            ? issue.ValueAmount
            : decimal.Round(issue.UnitCost * movement.Quantity, WeightedAverageCost.ValueScale, MidpointRounding.ToEven);

        return await WriteAsync(
            movement.Tenant,
            movement.Actor,
            movement.Source,
            new InventoryItemLocation(issue.ItemId, issue.WarehouseId, issue.ItemGroup),
            movement.Quantity,
            movement.OccurredOn,
            MovementDirection.In,
            position => WeightedAverageCost.Receive(position, movement.Quantity, cost),
            requiresCostBasis: false,
            againstKey: MovementKey.Of(movement.OriginalIssue),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يقرأ رصيد صنف في مستودع. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">الصنف.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<StockBalanceView>> ReadStockAsync(
        TenantId tenant,
        UserId actor,
        string itemId,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.ReadStock", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockBalanceView>.Failure(gate.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);
        StockPosition position = await ReadPositionAsync(tenant, itemId, warehouseId, forUpdate: false, null, cancellationToken)
            .ConfigureAwait(false);

        return Result<StockBalanceView>.Success(new StockBalanceView(
            itemId,
            warehouseId,
            position.Quantity,
            Money.Of(position.Value, _currency),
            position.UnitCost,
            position.HasCostBasis));
    }

    // ────────────────────────────────────────────────────────────────────────
    //  المسار الوحيد الذي يكتب حركة — الوارد والصادر والمرتجع كلّها تمرّ منه.
    // ────────────────────────────────────────────────────────────────────────
    private async ValueTask<Result<InventoryMovementCost>> WriteAsync(
        TenantId tenant,
        UserId actor,
        InventoryMovementSource source,
        InventoryItemLocation location,
        decimal quantity,
        DateOnly occurredOn,
        string direction,
        Func<StockPosition, StockEffect> apply,
        bool requiresCostBasis,
        string againstKey,
        CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);

        await using NpgsqlTransaction transaction = await Connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        // ‏١ · هل سُجّلت هذه الهوية من قبل؟ الوصول الثاني بالهوية نفسها لا يفعل شيئاً
        //      ولا يُعدّ خطأ — **مهما كان ترتيب الوصول** (القاعدة المعمارية 4).
        Result<RecordedMovement> existing = await ReadMovementAsync(tenant, source, transaction, cancellationToken)
            .ConfigureAwait(false);

        if (existing.IsSuccess)
        {
            RecordedMovement recorded = existing.Value;

            if (!string.Equals(recorded.ItemId, location.ItemId, StringComparison.Ordinal))
            {
                return Result<InventoryMovementCost>.Failure(InventoryErrors.IdentityConflict(
                    source.DocumentType, source.DocumentId, source.EventCode, recorded.ItemId, location.ItemId));
            }

            if (recorded.Quantity != quantity)
            {
                return Result<InventoryMovementCost>.Failure(InventoryErrors.QuantityConflict(
                    source.DocumentType, source.DocumentId, source.EventCode, recorded.Quantity, quantity));
            }

            return Result<InventoryMovementCost>.Success(new InventoryMovementCost(
                Money.Of(recorded.ValueAmount, _currency),
                recorded.Method,
                recorded.QuantityAfter,
                Money.Of(recorded.ValueAfter, _currency),
                recorded.DrewOnNegativeStock,
                WasAlreadyRecorded: true));
        }

        // ‏٢ · قفل صفّ الرصيد. `FOR UPDATE` على صفٍّ غير موجود يُرجع صفر صفوف بلا
        //      خطأ، والإنشاء يقع في الخطوة 5 بـ`ON CONFLICT` — فلا سباق إنشاء.
        StockPosition position = await ReadPositionAsync(
            tenant, location.ItemId, location.WarehouseId, forUpdate: true, transaction, cancellationToken)
            .ConfigureAwait(false);

        if (requiresCostBasis && !position.HasCostBasis)
        {
            return Result<InventoryMovementCost>.Failure(
                InventoryErrors.NoCostBasis(location.ItemId, location.WarehouseId));
        }

        StockEffect effect = apply(position);

        // ‏٣ · الحركة تُكتب أولاً: الفهرس الفريد على الهوية هو ما يحسم السباق، لا
        //      استعلامُ الخطوة 1 وحده (‏فخ-46: الهوية تُحسم عند الموضع الذي ينفّذ).
        await using (NpgsqlCommand insert = new(
            """
            insert into inventory.stock_movement
                ("Id","TenantId","SourceModule","DocumentType","DocumentId","TriggerCode","Generation",
                 "EventCode","ItemId","WarehouseId","ItemGroup","Direction","Quantity","ValueAmount",
                 "UnitCost","Method","DrewOnNegativeStock","QuantityAfter","ValueAfter","OccurredOn",
                 "RecordedAt","ActorId","AgainstKey")
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23)
            """, Connection, transaction))
        {
            insert.Parameters.AddWithValue(Guid.CreateVersion7());
            insert.Parameters.AddWithValue(tenant.Value);
            insert.Parameters.AddWithValue(source.Module.ToString());
            insert.Parameters.AddWithValue(source.DocumentType);
            insert.Parameters.AddWithValue(source.DocumentId);
            insert.Parameters.AddWithValue(source.TriggerCode);
            insert.Parameters.AddWithValue(source.Generation);
            insert.Parameters.AddWithValue(source.EventCode);
            insert.Parameters.AddWithValue(location.ItemId);
            insert.Parameters.AddWithValue(location.WarehouseId);
            insert.Parameters.AddWithValue(location.ItemGroup);
            insert.Parameters.AddWithValue(direction);
            insert.Parameters.AddWithValue(quantity);
            insert.Parameters.AddWithValue(effect.Value);
            insert.Parameters.AddWithValue(effect.UnitCost);
            insert.Parameters.AddWithValue(WeightedAverageCost.MethodCode);
            insert.Parameters.AddWithValue(effect.DrewOnNegativeStock);
            insert.Parameters.AddWithValue(effect.After.Quantity);
            insert.Parameters.AddWithValue(effect.After.Value);
            insert.Parameters.AddWithValue(occurredOn);
            insert.Parameters.AddWithValue(DateTime.UtcNow);
            insert.Parameters.AddWithValue(actor.Value.ToString());
            insert.Parameters.AddWithValue(againstKey);

            int written = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (written != 1)
            {
                return Result<InventoryMovementCost>.Failure(
                    InventoryErrors.UnexpectedRowCount("insert stock_movement", 1, written));
            }
        }

        // ‏٤ · الرصيد: `INSERT … ON CONFLICT DO UPDATE` — لا `UPDATE` مجرّد أبداً.
        //      والصفّ الواحد يُكتب بعبارة واحدة، فلا ترتيب أقفال يُتفاوض عليه (‏فخ-10 · فخ-11).
        await using (NpgsqlCommand upsert = new(
            """
            insert into inventory.item_balance
                ("Id","TenantId","ItemId","WarehouseId","Quantity","ValueAmount","UnitCost","HasCostBasis","UpdatedAt")
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9)
            on conflict ("TenantId","ItemId","WarehouseId") do update
               set "Quantity" = $5,
                   "ValueAmount" = $6,
                   "UnitCost" = $7,
                   "HasCostBasis" = $8,
                   "UpdatedAt" = $9
            """, Connection, transaction))
        {
            upsert.Parameters.AddWithValue(Guid.CreateVersion7());
            upsert.Parameters.AddWithValue(tenant.Value);
            upsert.Parameters.AddWithValue(location.ItemId);
            upsert.Parameters.AddWithValue(location.WarehouseId);
            upsert.Parameters.AddWithValue(effect.After.Quantity);
            upsert.Parameters.AddWithValue(effect.After.Value);
            upsert.Parameters.AddWithValue(effect.After.UnitCost);
            upsert.Parameters.AddWithValue(effect.After.HasCostBasis);
            upsert.Parameters.AddWithValue(DateTime.UtcNow);

            int written = await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (written != 1)
            {
                return Result<InventoryMovementCost>.Failure(
                    InventoryErrors.UnexpectedRowCount("upsert item_balance", 1, written));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result<InventoryMovementCost>.Success(new InventoryMovementCost(
            Money.Of(effect.Value, _currency),
            WeightedAverageCost.MethodCode,
            effect.After.Quantity,
            Money.Of(effect.After.Value, _currency),
            effect.DrewOnNegativeStock,
            WasAlreadyRecorded: false));
    }

    private NpgsqlConnection Connection => (NpgsqlConnection)_database.Database.GetDbConnection();

    private async ValueTask OpenAsync(CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<StockPosition> ReadPositionAsync(
        TenantId tenant,
        string itemId,
        string warehouseId,
        bool forUpdate,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string sql = """
            select "Quantity","ValueAmount","UnitCost","HasCostBasis"
              from inventory.item_balance
             where "TenantId" = $1 and "ItemId" = $2 and "WarehouseId" = $3
            """ + (forUpdate ? " for update" : string.Empty);

        await using NpgsqlCommand command = new(sql, Connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(itemId);
        command.Parameters.AddWithValue(warehouseId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return WeightedAverageCost.Empty;
        }

        return new StockPosition(
            reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetBoolean(3));
    }

    private async ValueTask<Result<RecordedMovement>> ReadMovementAsync(
        TenantId tenant,
        InventoryMovementSource source,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(
            """
            select "ItemId","WarehouseId","ItemGroup","Quantity","ValueAmount","UnitCost","Method",
                   "DrewOnNegativeStock","QuantityAfter","ValueAfter"
              from inventory.stock_movement
             where "TenantId" = $1 and "SourceModule" = $2 and "DocumentType" = $3
               and "DocumentId" = $4 and "TriggerCode" = $5 and "Generation" = $6 and "EventCode" = $7
            """, Connection, transaction);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(source.Module.ToString());
        command.Parameters.AddWithValue(source.DocumentType);
        command.Parameters.AddWithValue(source.DocumentId);
        command.Parameters.AddWithValue(source.TriggerCode);
        command.Parameters.AddWithValue(source.Generation);
        command.Parameters.AddWithValue(source.EventCode);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<RecordedMovement>.Failure(InventoryErrors.OriginalIssueNotFound(
                source.DocumentType, source.DocumentId, source.EventCode));
        }

        return Result<RecordedMovement>.Success(new RecordedMovement(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9)));
    }

    /// <summary>ما رُدَّ حتى الآن على صرفٍ بعينه — يُقرأ من الحركات لا من عدّاد.</summary>
    private async ValueTask<decimal> ReturnedAgainstAsync(
        TenantId tenant, InventoryMovementSource issue, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(
            """
            select coalesce(sum("Quantity"), 0)
              from inventory.stock_movement
             where "TenantId" = $1 and "AgainstKey" = $2
            """, Connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(MovementKey.Of(issue));

        return (decimal)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private sealed record RecordedMovement(
        string ItemId,
        string WarehouseId,
        string ItemGroup,
        decimal Quantity,
        decimal ValueAmount,
        decimal UnitCost,
        string Method,
        bool DrewOnNegativeStock,
        decimal QuantityAfter,
        decimal ValueAfter);
}

/// <summary>رصيد صنف في مستودع كما تراه جهة خارج الوحدة.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="Quantity">الكمية — قد تكون سالبة.</param>
/// <param name="Value">القيمة.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا المستودع مرّةً بتكلفة؟</param>
public sealed record StockBalanceView(
    string ItemId,
    string WarehouseId,
    decimal Quantity,
    Money Value,
    decimal UnitCost,
    bool HasCostBasis);
