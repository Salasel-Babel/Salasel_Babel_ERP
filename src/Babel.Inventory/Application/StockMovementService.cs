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
/// <b>ومفتاح الرصيد أربعة أبعاد:</b> المنشأة والصنف والمستودع <b>والموقع</b>. ووحدةُ
/// الأساس مكتوبة على الرصيد وعلى كل حركة، فالكمّية في هذا الملف لا تُقرأ مجرّدة أبداً.
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

        Result quantity = UnitConversion.Validate(receipt.Quantity);
        if (quantity.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(quantity.Errors);
        }

        return await WriteAsync(
            receipt.Tenant,
            receipt.Actor,
            receipt.Source,
            receipt.Location,
            receipt.Quantity,
            receipt.OccurredOn,
            MovementDirection.In,
            (position, magnitude) => WeightedAverageCost.Receive(position, magnitude, receipt.Cost.Amount),
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

        Result quantity = UnitConversion.Validate(issue.Quantity);
        if (quantity.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(quantity.Errors);
        }

        return await WriteAsync(
            issue.Tenant,
            issue.Actor,
            issue.Source,
            issue.Location,
            issue.Quantity,
            issue.OccurredOn,
            MovementDirection.Out,
            static (position, magnitude) => WeightedAverageCost.Issue(position, magnitude),
            requiresCostBasis: true,
            againstKey: string.Empty,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يسجّل مرتجعاً <b>بتكلفة حركته الأصلية</b>.
    /// <para>
    /// «بنفس تكلفة قيد البيع الأصلي لا بتكلفة اليوم» — نصّ
    /// <c>sales.credit_note.cost_of_sales</c> في المصفوفة. ولذلك يحمل الطلب هوية الحركة
    /// الأصلية: بحثٌ بالصنف وحده كان سيختار «آخر حركة» وهو اختيارٌ لا يقرّره أحد.
    /// </para>
    /// <para>
    /// <b>واتجاه المرتجع هو عكس اتجاه أصله — يُقرأ من الحركة الأصلية ولا يُفترض.</b>
    /// مرتجعٌ على <b>صرف</b> بضاعةٌ تعود إلى المستودع، ومرتجعٌ على <b>استلام</b> بضاعةٌ
    /// تخرج منه إلى المورد. وكان هذا المسار يكتب الوارد في الحالتين، فكان مرتجع
    /// المشتريات <b>يزيد</b> الرصيد بينما يُنقص حسابه الضابط — قيدٌ متوازن، وذمّةٌ
    /// صحيحة، ومستودعٌ يكذب بضِعف قيمة المرتجع.
    /// </para>
    /// <para>
    /// <b>والقيمة تُطبَّق كما حُسبت في الاتجاهين</b> عبر <see cref="WeightedAverageCost.Annul"/>
    /// لا عبر متوسط اليوم: المرتجع إبطالٌ جزئي لواقعةٍ قُيِّمت من قبل، ولو أُعيد تقييمه
    /// بمتوسط اللحظة لبقي في الرصيد فارقُ حركة المتوسط بين اللحظتين — قيمةٌ لا يقابلها
    /// شيء في المستودع، ولا يقابلها شيء على المورد.
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

        Result validated = UnitConversion.Validate(movement.Quantity);
        if (validated.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(validated.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        Result<RecordedMovement> original = await ReadMovementAsync(
            movement.Tenant, movement.OriginalMovement, null, cancellationToken).ConfigureAwait(false);

        if (original.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(original.Errors);
        }

        RecordedMovement issue = original.Value;

        // ── الوحدة تُطابَق قبل أي حساب ───────────────────────────────────────────
        // «رُدّ عشرة» على صرفٍ مُمسَك بالحبّة تعني عشر حبّات؛ وعلى صرفٍ مُمسَك بالكرتون
        // تعني شيئاً آخر تماماً. والمرتجع يُقيَّم بتكلفة وحدة الصرف الأصلي، فخلطُ
        // الوحدتين هنا يُنتج قيمةً مضروبةً في العدد الخطأ — بقيدٍ متوازن.
        if (!UnitConversion.SameUnit(movement.Quantity.Unit, issue.BaseUnit))
        {
            return Result<InventoryMovementCost>.Failure(
                InventoryErrors.UnitNotConvertible(issue.ItemId, movement.Quantity.Unit, issue.BaseUnit));
        }

        // ── «هل هذا المرتجع مُسجَّل سلفاً؟» يُسأل **قبل** «هل الردّ زائد؟» ────────
        // لأن صفّ هذا المرتجع نفسه داخل مجموع ما رُدّ على الصرف. فلو سُئل الثاني
        // أوّلاً لأعلنت **الإعادةُ بالهوية نفسها** ردّاً زائداً: ردٌّ كامل لعشر وحدات
        // يُعاد فيُقرأ 10 + 10 > 10 ⇒ `inventory.return_exceeds_issue` — رفضٌ لواقعة
        // وقعت مرّة واحدة، ويكسر القاعدة المعمارية 4 (الإحكام مستقلّ عن الترتيب).
        // وليس هذا احتمالاً نظرياً: مسار الإشعار الدائن يُعيد المحاولة كلّما سقط
        // ترحيلٌ بعد كتابة الحركة، وهو المسار الذي يُبنى عليه الآن.
        decimal cost = 0m;

        if ((await ReadMovementAsync(movement.Tenant, movement.Source, null, cancellationToken)
                .ConfigureAwait(false)).IsFailure)
        {
            decimal returned = await ReturnedAgainstAsync(movement.Tenant, movement.OriginalMovement, cancellationToken)
                .ConfigureAwait(false);

            if (returned + movement.Quantity.Magnitude > issue.Quantity)
            {
                return Result<InventoryMovementCost>.Failure(
                    InventoryErrors.ReturnExceedsIssue(issue.Quantity, returned, movement.Quantity.Magnitude));
            }

            // ردٌّ كامل للصرف الواحد يستعيد **قيمته بالضبط**، لا حاصل ضرب يعيد بناءها
            // بتقريبٍ ثانٍ. وردٌّ جزئي يُقيَّم بتكلفة وحدة ذلك الصرف نفسه.
            cost = returned == 0m && movement.Quantity.Magnitude == issue.Quantity
                ? issue.ValueAmount
                : decimal.Round(
                    issue.UnitCost * movement.Quantity.Magnitude, WeightedAverageCost.ValueScale, MidpointRounding.ToEven);
        }

        // وعلى مسار الإعادة تبقى `cost` صفراً ولا تُستعمل: `WriteAsync` يقرأ الحركة
        // المُسجَّلة ويعود بها قبل أن يبلغ دالّة الأثر أصلاً.

        // ── الاتجاه من الأصل، لا من افتراض ──────────────────────────────────────
        // ولا `MovementDirection.In` مكتوبةً هنا: ردٌّ على صادرٍ وارد، وردٌّ على واردٍ
        // صادر. والافتراض الصامت هو ما جعل مرتجع المشتريات يزيد المخزون.
        bool inbound = string.Equals(issue.Direction, MovementDirection.Out, StringComparison.Ordinal);

        return await WriteAsync(
            movement.Tenant,
            movement.Actor,
            movement.Source,
            new InventoryItemLocation(issue.ItemId, issue.WarehouseId, issue.LocationId, issue.ItemGroup),
            movement.Quantity,
            movement.OccurredOn,
            inbound ? MovementDirection.In : MovementDirection.Out,
            (position, magnitude) => WeightedAverageCost.Annul(position, magnitude, cost, inbound),
            requiresCostBasis: false,
            againstKey: MovementKey.Of(movement.OriginalMovement),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <b>يُلغي حركة مُسجَّلة بكاملها وبقيمتها هي</b> — وهو ما يقابل عكسَ قيدها في الدفتر.
    /// <para>
    /// والكمّية والقيمة تُقرآن من الحركة المُلغاة ولا يُسلّمهما المستدعي: العكس إبطالُ
    /// واقعةٍ قُيِّمت من قبل، لا واقعةٌ جديدة تُقيَّم اليوم. ولو أُعيد الحساب بمتوسط اليوم
    /// لبقي في الرصيد فارقُ حركة المتوسط بين اللحظتين — قيمةٌ لا يقابلها شيء.
    /// </para>
    /// <para>
    /// <b>وهوية حركة العكس هي هوية قيد العكس حرفاً بحرف</b> (‏ADR-0016 · ADR-0039 §4):
    /// هوية الأصل ورمزُ إطلاقها مسبوقاً بـ<c>REVERSAL:</c>. فيقع الطرفان — الحركة
    /// والقيد — تحت مفتاح المستند نفسه، ويصير صافي المطابقة صفراً <b>بالبناء</b>.
    /// </para>
    /// </summary>
    /// <param name="movement">طلب الإلغاء.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<InventoryMovementCost>> ReverseMovementAsync(
        InventoryMovementReversal movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        Result gate = await _enforcer
            .EnsureAsync(movement.Tenant, movement.Actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.ReverseMovement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(gate.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        Result<RecordedMovement> original = await ReadMovementAsync(
            movement.Tenant, movement.ReversedMovement, null, cancellationToken).ConfigureAwait(false);

        if (original.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(InventoryErrors.OriginalMovementNotFound(
                movement.ReversedMovement.DocumentType,
                movement.ReversedMovement.DocumentId,
                movement.ReversedMovement.EventCode));
        }

        RecordedMovement annulled = original.Value;

        // ── «هل كُتب هذا العكس سلفاً؟» يُسأل **قبل** «هل رُدّ على الأصل؟» ─────────
        // لأن حركة العكس نفسها تحمل مفتاح الأصل في `AgainstKey`، فهي داخل مجموع ما
        // رُدّ عليه. فلو سُئل الثاني أوّلاً لأعلنت **الإعادة بالهوية نفسها** ردّاً
        // قائماً ورُفضت — رفضٌ لواقعة وقعت مرّة واحدة، وكسرٌ للقاعدة المعمارية 4.
        // وليس هذا احتمالاً نظرياً: مسار العكس يُعاد كلّما سقط عكسُ القيد بعد كتابة
        // الحركة، وهو المسار الذي يُبنى عليه الآن.
        if ((await ReadMovementAsync(movement.Tenant, movement.Source, null, cancellationToken)
                .ConfigureAwait(false)).IsFailure)
        {
            decimal returned = await ReturnedAgainstAsync(
                movement.Tenant, movement.ReversedMovement, cancellationToken).ConfigureAwait(false);

            if (returned > 0m)
            {
                return Result<InventoryMovementCost>.Failure(InventoryErrors.MovementAlreadyReturned(
                    movement.ReversedMovement.DocumentType,
                    movement.ReversedMovement.DocumentId,
                    movement.ReversedMovement.EventCode,
                    returned));
            }
        }

        bool inbound = !UnitConversion.SameUnit(annulled.Direction, MovementDirection.In);
        decimal value = annulled.ValueAmount;

        return await WriteAsync(
            movement.Tenant,
            movement.Actor,
            movement.Source,
            new InventoryItemLocation(annulled.ItemId, annulled.WarehouseId, annulled.LocationId, annulled.ItemGroup),
            new InventoryQuantity(annulled.Quantity, annulled.BaseUnit),
            movement.OccurredOn,
            inbound ? MovementDirection.In : MovementDirection.Out,
            (position, magnitude) => WeightedAverageCost.Annul(position, magnitude, value, inbound),
            requiresCostBasis: false,
            againstKey: MovementKey.Of(movement.ReversedMovement),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يقرأ حركةً مُسجَّلة بهويتها — موضعها وكمّيتها بوحدة أساسها وقيمتها.
    /// <para>
    /// وهي القراءة التي تجعل وحدةً أخرى تردّ بضاعةً <b>بوحدة صرفها</b> بدل أن تخترع
    /// لها وحدة. نقطة قراءة: تعمل عند «للقراءة فقط».
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="source">هوية الحركة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<InventoryMovementCost>> ReadMovementAsync(
        TenantId tenant,
        UserId actor,
        InventoryMovementSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.ReadMovement", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(gate.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        Result<RecordedMovement> found = await ReadMovementAsync(tenant, source, null, cancellationToken)
            .ConfigureAwait(false);

        if (found.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(InventoryErrors.OriginalMovementNotFound(
                source.DocumentType, source.DocumentId, source.EventCode));
        }

        RecordedMovement movement = found.Value;

        return Result<InventoryMovementCost>.Success(new InventoryMovementCost(
            Money.Of(movement.ValueAmount, _currency),
            movement.Method,
            new InventoryItemLocation(movement.ItemId, movement.WarehouseId, movement.LocationId, movement.ItemGroup),
            new InventoryQuantity(movement.Quantity, movement.BaseUnit),
            new InventoryQuantity(movement.QuantityAfter, movement.BaseUnit),
            Money.Of(movement.ValueAfter, _currency),
            movement.DrewOnNegativeStock,
            WasAlreadyRecorded: true));
    }

    /// <summary>يقرأ رصيد صنف في موقعٍ من مستودع. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">الصنف.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="locationId">الموقع داخل المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<StockBalanceView>> ReadStockAsync(
        TenantId tenant,
        UserId actor,
        string itemId,
        string warehouseId,
        string locationId,
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
        StockPosition position = await ReadPositionAsync(
            tenant, itemId, warehouseId, locationId, forUpdate: false, null, cancellationToken).ConfigureAwait(false);

        return Result<StockBalanceView>.Success(new StockBalanceView(
            itemId,
            warehouseId,
            locationId,
            new InventoryQuantity(position.Quantity, position.BaseUnit),
            Money.Of(position.Value, _currency),
            position.UnitCost,
            position.HasCostBasis));
    }

    /// <summary>
    /// يقرأ أرصدة المنشأة كلّها، مرتَّبةً بالصنف ثم المستودع ثم الموقع.
    /// <para>
    /// <b>وترتيبٌ حرفي معلَن لا ترتيبُ إدخال</b>: قائمةٌ يتغيّر ترتيبها بين نداءين تجعل
    /// كل مقارنة بين تقريرين عملاً يدوياً (القاعدة 10).
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<StockBalanceView>>> ListStockAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.ListStock", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StockBalanceView>>.Failure(gate.Errors);
        }

        await OpenAsync(cancellationToken).ConfigureAwait(false);

        List<StockBalanceView> balances = [];

        await using NpgsqlCommand command = new(
            """
            select "ItemId","WarehouseId","LocationId","BaseUnit","Quantity","ValueAmount","UnitCost","HasCostBasis"
              from inventory.item_balance
             where "TenantId" = $1
             order by "ItemId","WarehouseId","LocationId"
            """, Connection);

        command.Parameters.AddWithValue(tenant.Value);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            balances.Add(new StockBalanceView(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                new InventoryQuantity(reader.GetDecimal(4), reader.GetString(3)),
                Money.Of(reader.GetDecimal(5), _currency),
                reader.GetDecimal(6),
                reader.GetBoolean(7)));
        }

        return Result<IReadOnlyList<StockBalanceView>>.Success(balances);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  المسار الوحيد الذي يكتب حركة — الوارد والصادر والمرتجع والعكس كلّها تمرّ منه.
    // ────────────────────────────────────────────────────────────────────────
    private async ValueTask<Result<InventoryMovementCost>> WriteAsync(
        TenantId tenant,
        UserId actor,
        InventoryMovementSource source,
        InventoryItemLocation location,
        InventoryQuantity entered,
        DateOnly occurredOn,
        string direction,
        Func<StockPosition, decimal, StockEffect> apply,
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

            // ‏**والمقارنة بما سُلّم لا بما استقرّ**: طلبٌ سُجّل بكرتون واحد يُعاد
            // بـ«اثنتي عشرة حبّة» ليس إعادةً بل طلبٌ آخر يصادف أن أثره واحد اليوم.
            // ومقارنةُ الكميتين بعد التحويل كانت ستقبله بصمت، فيضيع أن المستدعي
            // غيّر ما يقوله المستند.
            if (recorded.EnteredMagnitude != entered.Magnitude
                || !UnitConversion.SameUnit(recorded.EnteredUnit, entered.Unit))
            {
                return Result<InventoryMovementCost>.Failure(InventoryErrors.QuantityConflict(
                    source.DocumentType,
                    source.DocumentId,
                    source.EventCode,
                    recorded.EnteredMagnitude,
                    entered.Magnitude));
            }

            return Result<InventoryMovementCost>.Success(new InventoryMovementCost(
                Money.Of(recorded.ValueAmount, _currency),
                recorded.Method,
                new InventoryItemLocation(recorded.ItemId, recorded.WarehouseId, recorded.LocationId, recorded.ItemGroup),
                new InventoryQuantity(recorded.Quantity, recorded.BaseUnit),
                new InventoryQuantity(recorded.QuantityAfter, recorded.BaseUnit),
                Money.Of(recorded.ValueAfter, _currency),
                recorded.DrewOnNegativeStock,
                WasAlreadyRecorded: true));
        }

        // ‏٢ · قفل صفّ الرصيد. `FOR UPDATE` على صفٍّ غير موجود يُرجع صفر صفوف بلا
        //      خطأ، والإنشاء يقع في الخطوة 5 بـ`ON CONFLICT` — فلا سباق إنشاء.
        StockPosition position = await ReadPositionAsync(
            tenant, location.ItemId, location.WarehouseId, location.LocationId, forUpdate: true, transaction, cancellationToken)
            .ConfigureAwait(false);

        // ‏٣ · الوحدة تُحلّ **قبل** أي حساب: بأي وحدة يُمسَك هذا الرصيد، وبأي معامل
        //      تدخله الوحدة المُسلَّمة. والخلط بلا معامل يُرفض باسمه ولا يُقرَّب.
        Result<ResolvedQuantity> resolved = await ResolveAsync(
            tenant, location.ItemId, entered, position.BaseUnit, transaction, cancellationToken).ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result<InventoryMovementCost>.Failure(resolved.Errors);
        }

        string baseUnit = resolved.Value.BaseUnit;
        decimal magnitude = resolved.Value.Magnitude;

        if (requiresCostBasis && !position.HasCostBasis)
        {
            return Result<InventoryMovementCost>.Failure(
                InventoryErrors.NoCostBasis(location.ItemId, location.WarehouseId));
        }

        StockEffect effect = apply(position, magnitude);

        // ‏٤ · الحركة تُكتب أولاً: الفهرس الفريد على الهوية هو ما يحسم السباق، لا
        //      استعلامُ الخطوة 1 وحده (‏فخ-46: الهوية تُحسم عند الموضع الذي ينفّذ).
        await using (NpgsqlCommand insert = new(
            """
            insert into inventory.stock_movement
                ("Id","TenantId","SourceModule","DocumentType","DocumentId","TriggerCode","Generation",
                 "EventCode","ItemId","WarehouseId","LocationId","ItemGroup","Direction","Quantity","BaseUnit",
                 "EnteredUnit","EnteredMagnitude","ValueAmount","UnitCost","Method","DrewOnNegativeStock",
                 "QuantityAfter","ValueAfter","OccurredOn","RecordedAt","ActorId","AgainstKey")
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25,$26,$27)
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
            insert.Parameters.AddWithValue(location.LocationId);
            insert.Parameters.AddWithValue(location.ItemGroup);
            insert.Parameters.AddWithValue(direction);
            insert.Parameters.AddWithValue(magnitude);
            insert.Parameters.AddWithValue(baseUnit);
            insert.Parameters.AddWithValue(entered.Unit);
            insert.Parameters.AddWithValue(entered.Magnitude);
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

        // ‏٥ · الرصيد: `INSERT … ON CONFLICT DO UPDATE` — لا `UPDATE` مجرّد أبداً.
        //      والصفّ الواحد يُكتب بعبارة واحدة، فلا ترتيب أقفال يُتفاوض عليه (‏فخ-10 · فخ-11).
        //      و`BaseUnit` **لا يُحدَّث عند التعارض**: أساس الرصيد يُثبَّت بأول حركة،
        //      ورصيدٌ يتغيّر أساسه بعد أن كُتبت عليه حركات لا يُجمَع أصلاً.
        await using (NpgsqlCommand upsert = new(
            """
            insert into inventory.item_balance
                ("Id","TenantId","ItemId","WarehouseId","LocationId","BaseUnit","Quantity","ValueAmount",
                 "UnitCost","HasCostBasis","UpdatedAt")
            values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            on conflict ("TenantId","ItemId","WarehouseId","LocationId") do update
               set "Quantity" = $7,
                   "ValueAmount" = $8,
                   "UnitCost" = $9,
                   "HasCostBasis" = $10,
                   "UpdatedAt" = $11
            """, Connection, transaction))
        {
            upsert.Parameters.AddWithValue(Guid.CreateVersion7());
            upsert.Parameters.AddWithValue(tenant.Value);
            upsert.Parameters.AddWithValue(location.ItemId);
            upsert.Parameters.AddWithValue(location.WarehouseId);
            upsert.Parameters.AddWithValue(location.LocationId);
            upsert.Parameters.AddWithValue(baseUnit);
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
            location,
            new InventoryQuantity(magnitude, baseUnit),
            new InventoryQuantity(effect.After.Quantity, baseUnit),
            Money.Of(effect.After.Value, _currency),
            effect.DrewOnNegativeStock,
            WasAlreadyRecorded: false));
    }

    /// <summary>
    /// يحلّ الكمّية المُسلَّمة إلى <b>وحدة أساس الرصيد</b>.
    /// <para>
    /// وأساس الرصيد يُقرأ من الصفّ إن وُجد، وإلا من كتالوج الأصناف، وإلا فهو الوحدة
    /// المُسلَّمة نفسها. <b>وصنفٌ غير مسجَّل في الكتالوج يعمل</b> — لأن المخزون كان
    /// يعمل قبل الكتالوج، وإلزامُ التسجيل بأثر رجعي يُوقف مستأجراً عاملاً. لكن الخلط
    /// بين وحدتين على مثل هذا الصنف <b>لا يعمل</b>: لا معامل، فلا تحويل، فرفضٌ باسمه.
    /// </para>
    /// </summary>
    private async ValueTask<Result<ResolvedQuantity>> ResolveAsync(
        TenantId tenant,
        string itemId,
        InventoryQuantity entered,
        string balanceBaseUnit,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string catalogueBase = await ItemBaseUnitAsync(tenant, itemId, transaction, cancellationToken).ConfigureAwait(false);

        if (balanceBaseUnit.Length > 0 && catalogueBase.Length > 0
            && !UnitConversion.SameUnit(balanceBaseUnit, catalogueBase))
        {
            return Result<ResolvedQuantity>.Failure(
                InventoryErrors.BaseUnitMismatch(itemId, balanceBaseUnit, catalogueBase));
        }

        string baseUnit = balanceBaseUnit.Length > 0
            ? balanceBaseUnit
            : catalogueBase.Length > 0 ? catalogueBase : entered.Unit;

        if (UnitConversion.SameUnit(entered.Unit, baseUnit))
        {
            return Result<ResolvedQuantity>.Success(new ResolvedQuantity(baseUnit, entered.Magnitude));
        }

        // معاملات التحويل مُعرَّفة **إلى وحدة أساس الصنف** وحدها. فرصيدٌ أساسه غير
        // أساس الكتالوج لا طريق منه إلى وحدة أخرى — والرفض هنا أصدق من سلسلة تحويلات
        // يخترعها الكود.
        UnitRatio? ratio = UnitConversion.SameUnit(baseUnit, catalogueBase)
            ? await UnitRatioAsync(tenant, itemId, entered.Unit, transaction, cancellationToken).ConfigureAwait(false)
            : null;

        if (ratio is not { } factor)
        {
            return Result<ResolvedQuantity>.Failure(
                InventoryErrors.UnitNotConvertible(itemId, entered.Unit, baseUnit));
        }

        Result<decimal> converted = UnitConversion.ToBase(entered.Magnitude, factor);

        return converted.IsFailure
            ? Result<ResolvedQuantity>.Failure(converted.Errors)
            : Result<ResolvedQuantity>.Success(new ResolvedQuantity(baseUnit, converted.Value));
    }

    private async ValueTask<string> ItemBaseUnitAsync(
        TenantId tenant, string itemId, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(
            """ select "BaseUnit" from inventory.item where "TenantId" = $1 and "Code" = $2 """,
            Connection,
            transaction);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(itemId);

        object? found = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return found as string ?? string.Empty;
    }

    private async ValueTask<UnitRatio?> UnitRatioAsync(
        TenantId tenant,
        string itemId,
        string unit,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(
            """
            select "Numerator","Denominator" from inventory.item_unit
             where "TenantId" = $1 and "ItemCode" = $2 and "UnitCode" = $3
            """, Connection, transaction);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(itemId);
        command.Parameters.AddWithValue(unit);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new UnitRatio(reader.GetInt64(0), reader.GetInt64(1))
            : null;
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
        string locationId,
        bool forUpdate,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string sql = """
            select "Quantity","ValueAmount","UnitCost","HasCostBasis","BaseUnit"
              from inventory.item_balance
             where "TenantId" = $1 and "ItemId" = $2 and "WarehouseId" = $3 and "LocationId" = $4
            """ + (forUpdate ? " for update" : string.Empty);

        await using NpgsqlCommand command = new(sql, Connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(itemId);
        command.Parameters.AddWithValue(warehouseId);
        command.Parameters.AddWithValue(locationId);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return WeightedAverageCost.Empty;
        }

        return new StockPosition(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetBoolean(3),
            reader.GetString(4));
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
                   "DrewOnNegativeStock","QuantityAfter","ValueAfter","Direction","LocationId","BaseUnit",
                   "EnteredUnit","EnteredMagnitude"
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
            reader.GetDecimal(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetDecimal(14)));
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

    /// <summary>الكمّية بعد الحلّ: وحدة الأساس، والمقدار بها.</summary>
    private readonly record struct ResolvedQuantity(string BaseUnit, decimal Magnitude);

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
        decimal ValueAfter,
        string Direction,
        string LocationId,
        string BaseUnit,
        string EnteredUnit,
        decimal EnteredMagnitude);
}

/// <summary>رصيد صنف في موقعٍ من مستودع كما تراه جهة خارج الوحدة.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع داخل المستودع.</param>
/// <param name="Quantity">الكمية بوحدة أساسها — قد تكون سالبة.</param>
/// <param name="Value">القيمة.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة المتحرّك.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا الموقع مرّةً بتكلفة؟</param>
public sealed record StockBalanceView(
    string ItemId,
    string WarehouseId,
    string LocationId,
    InventoryQuantity Quantity,
    Money Value,
    decimal UnitCost,
    bool HasCostBasis);
