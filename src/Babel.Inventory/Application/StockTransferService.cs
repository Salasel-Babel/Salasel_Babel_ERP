using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>
/// <b>النقل بين موقعين</b>: مسوّدةٌ ← تنفيذ. و<b>لا ترحيل</b>.
/// <para>
/// <b>ولماذا لا قيد — وهو أهمّ ما في هذا الملفّ:</b> النقل داخل المنشأة نفسها
/// <b>لا يُغيّر قيمة المخزون</b>. والصنف واحدٌ على الطرفين، فمجموعته واحدة، فمؤهّل
/// دور <c>inventory_control</c> واحدٌ على الطرفين، فالحساب الذي كان سيُجعل مديناً هو
/// الحساب الذي كان سيُجعل دائناً <b>بالمبلغ نفسه</b>. أي أن القيد المكتوب لا شيء —
/// مكتوباً في دفترٍ يُضاف إليه ولا يُحذف منه، وله رقمٌ متسلسل، ويدخل سلسلة البصمات.
/// وقيدٌ لا أثر له يُبقي على نفسه إلى الأبد ويُقرأ في كل تقرير حركة.
/// </para>
/// <para>
/// <b>والمصفوفة نفسها تقول هذا</b>: شرط <c>inventory.transfer.between_warehouses</c>
/// نصّه «حساب مخزون المستودع المصدر يختلف عن حساب مخزون المستودع الوجهة — وإلا فلا
/// قيد مالي إطلاقاً». والنقل بين موقعين <b>لا يبلغ ذلك الشرط أبداً</b>: المؤهّل مجموعةُ
/// الصنف، وهي لا تتغيّر بنقل مكان.
/// </para>
/// <para>
/// <b>ولا حدث لهذا المستند في المصفوفة — ولا يحتاج واحداً.</b> المصفوفة تُجيب «أيّ
/// حسابٍ لأيّ حدث»، وحدثٌ لا يُنتج قيداً لا حساب له فلا جواب لها فيه. <b>و«لا قيد»
/// مفروضٌ هنا بالبناء لا بالوصف</b>: هذا الملفّ لا يحمل مرجعاً واحداً إلى
/// <c>InventoryPostingGateway</c> ولا إلى <c>IPostingService</c> — فلا طريق منه إلى
/// الدفتر أصلاً — ويُثبته إثباتٌ يَعُدّ قيود المنشأة قبل النقل وبعده فيجدهما سواء.
/// وذلك أقوى من سطرٍ في ملفّ بيانات.
/// </para>
/// <para>
/// <b>وحركتان لا حركة واحدة:</b> صادرٌ من موضع المصدر بتكلفته المتحرّكة، ووارد إلى
/// موضع الوجهة <b>بالقيمة نفسها</b> التي خرجت. ورصيدُ كلّ موضع مفتاحٌ مستقلّ، فحركةٌ
/// واحدة «تنقل» كانت ستحتاج أن تُنقص مفتاحاً وتزيد آخر في صفٍّ واحد — وهو ما لا
/// يصفه عمود <c>Direction</c> ولا يُجمَع في تقرير حركة.
/// </para>
/// <para>
/// <b>والقيمة تُحسب ولا تُملى</b> (‏ADR-0039): المنقول يخرج بمتوسط تكلفة مصدره لحظة
/// النقل، ويدخل الوجهة بذلك المبلغ. فمجموع قيمة المخزون قبل النقل وبعده واحد
/// بالضبط، وهو ما يجعل الامتناع عن القيد صحيحاً لا مجرّد اختصار.
/// </para>
/// </summary>
public sealed class StockTransferService : IApplicationService
{
    /// <summary>نوع مستند النقل في هوية الحركة.</summary>
    internal const string TransferDocument = "InventoryStockTransfer";

    /// <summary>
    /// رمز حدث الصادر من المصدر — <b>ورمزان لا رمز واحد</b>.
    /// <para>
    /// هوية الحركة سداسية ورمزُ الحدث فيها (‏فخ-45): حركتان بالهوية نفسها تعني أن
    /// الثانية تُبتلع بصمت، فيخرج المنقول من المصدر ولا يدخل الوجهة أبداً — وهو عجزٌ
    /// لا يُظهره توازن ولا سلسلة.
    /// </para>
    /// </summary>
    internal const string IssuedEvent = "inventory.transfer.between_locations.issued";

    /// <summary>رمز حدث الوارد إلى الوجهة.</summary>
    internal const string ReceivedEvent = "inventory.transfer.between_locations.received";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;
    private readonly StockMovementService _stock;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="stock">دفتر المخزون المساعد — <b>وهو من يحسب قيمة المنقول</b>.</param>
    public StockTransferService(
        IEntitlementEnforcer enforcer, InventoryRuntime runtime, StockMovementService stock)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(stock);
        _enforcer = enforcer;
        _database = runtime.Database;
        _stock = stock;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يُنشئ مستند نقلٍ <b>مسوّدة</b>. لا حركة ولا رصيد يتغيّر.
    /// <para>
    /// <b>وموضعا النقل يُتحقَّق أنهما مسجَّلان وعاملان</b> — بخلاف مسار الحركة العام
    /// الذي يقبل رمزاً غير مسجَّل. والفرق مقصود: مسار الحركة العام قائمٌ منذ ما قبل
    /// السجلّ ويحمل رموزاً مكتوبة قبله، وإلزامُه بالتسجيل بأثر رجعي يُوقف مستأجراً
    /// عاملاً. أمّا النقل فبابٌ جديد لا حركة سابقة عليه، فيُولد مُلزماً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StockTransferView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        StockTransferDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StockTransfer.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockTransferView>.Failure(gate.Errors);
        }

        Result quantity = UnitConversion.Validate(draft.Quantity);
        if (quantity.IsFailure)
        {
            return Result<StockTransferView>.Failure(quantity.Errors);
        }

        if (UnitConversion.SameUnit(draft.FromWarehouseId, draft.ToWarehouseId)
            && UnitConversion.SameUnit(draft.FromLocationId, draft.ToLocationId))
        {
            return Result<StockTransferView>.Failure(
                InventoryErrors.TransferToSamePlace(draft.FromWarehouseId, draft.FromLocationId));
        }

        Result source = await EnsurePlaceUsableAsync(
            tenant, draft.FromWarehouseId, draft.FromLocationId, cancellationToken).ConfigureAwait(false);

        if (source.IsFailure)
        {
            return Result<StockTransferView>.Failure(source.Errors);
        }

        Result destination = await EnsurePlaceUsableAsync(
            tenant, draft.ToWarehouseId, draft.ToLocationId, cancellationToken).ConfigureAwait(false);

        if (destination.IsFailure)
        {
            return Result<StockTransferView>.Failure(destination.Errors);
        }

        if (await _database.Transfers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<StockTransferView>.Failure(InventoryErrors.DuplicateTransferNumber(draft.Number));
        }

        StockTransferRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            ItemCode = draft.ItemId,
            ItemGroup = draft.ItemGroup,
            FromWarehouseId = draft.FromWarehouseId,
            FromLocationId = draft.FromLocationId,
            ToWarehouseId = draft.ToWarehouseId,
            ToLocationId = draft.ToLocationId,
            Magnitude = draft.Quantity.Magnitude,
            UnitCode = draft.Quantity.Unit,
            ValueAmount = 0m,
            OccurredOn = draft.OccurredOn,
            State = StockTransferState.Draft,
            MovementGeneration = 1,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Transfers.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<StockTransferView>.Success(ViewOf(row));
    }

    /// <summary>يقرأ مستند نقلٍ واحداً. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="transferId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<StockTransferView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StockTransfer.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockTransferView>.Failure(gate.Errors);
        }

        StockTransferRow? row = await _database.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == transferId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<StockTransferView>.Failure(InventoryErrors.DocumentNotFound(TransferDocument, transferId))
            : Result<StockTransferView>.Success(ViewOf(row));
    }

    /// <summary>
    /// يقرأ مستندات النقل، <b>مرتَّبةً بالتاريخ ثم بالرقم ترتيباً حرفياً ثابتاً</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<StockTransferView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StockTransfer.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StockTransferView>>.Failure(gate.Errors);
        }

        List<StockTransferRow> rows = await _database.Transfers
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StockTransferView> views =
        [
            .. rows
                .OrderBy(static row => row.OccurredOn)
                .ThenBy(static row => row.Number, StringComparer.Ordinal)
                .Select(ViewOf),
        ];

        return Result<IReadOnlyList<StockTransferView>>.Success(views);
    }

    /// <summary>
    /// ينفّذ النقل: <b>صادرٌ من المصدر ثم واردٌ إلى الوجهة بالقيمة نفسها</b> — ولا قيد.
    /// <para>
    /// <b>حصينٌ ضد التكرار بهوية الحركة</b> لا بحالة المستند: نداءان متزامنان يجتازان
    /// فحص «مسوّدة» معاً، ويلتقيان عند الفهرس الفريد على هوية الحركة السداسية. فالوصول
    /// الثاني يُعيد الحركتين نفسيهما و<c>AlreadyMoved = true</c>، بلا حركة ثالثة.
    /// </para>
    /// <para>
    /// <b>والصادر أوّلاً ثم الوارد</b> — بالترتيب نفسه الذي يفرضه ADR-0041 على «الدفتر
    /// المساعد قبل القيد»: قيمة الوارد <b>هي</b> قيمة الصادر المحسوبة، فلا يوجد رقمٌ
    /// يُكتب به الوارد قبل أن يخرج الصادر. ولو انقلب الترتيب لدخل المنقولُ الوجهةَ
    /// بتكلفةٍ مُخترَعة.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="transferId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StockTransferView>> MoveAsync(
        TenantId tenant,
        UserId actor,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StockTransfer.Move", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockTransferView>.Failure(gate.Errors);
        }

        StockTransferRow? row = await _database.Transfers
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == transferId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<StockTransferView>.Failure(InventoryErrors.DocumentNotFound(TransferDocument, transferId));
        }

        string documentId = row.Id.ToString("D", CultureInfo.InvariantCulture);
        InventoryQuantity entered = new(row.Magnitude, row.UnitCode);

        // ‏١ · هل يغطّي رصيدُ المصدر ما يُنقَل؟ **والمقارنة بعد التحويل إلى وحدة واحدة**
        //      لا قبله: «اثنا عشر» و«واحد» رقمان لا يُقارَنان قبل أن يُعرف أنهما كرتونٌ
        //      وحبّة.
        //
        //      **ولماذا يُرفض هنا ما يُقبل في الصرف:** الصرف على رصيد سالب واقعةٌ
        //      يومية — بيعٌ قبل إدخال استلامه — فتُوسَم وتُقبل. أمّا النقل فيُحرّك
        //      بضاعةً بين رفّين **فعلياً**، ولا يُحمَل من رفٍّ ما ليس عليه. وقبولُه
        //      كان سينتج رصيداً سالباً في مكانٍ وموجباً في آخر لواقعةٍ لم تقع.
        Result<InventoryQuantity> resolved = await _stock.ResolveToBaseAsync(
            tenant, row.ItemCode, row.FromWarehouseId, row.FromLocationId, entered, cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result<StockTransferView>.Failure(resolved.Errors);
        }

        Result<StockBalanceView> available = await _stock
            .ReadStockAsync(tenant, actor, row.ItemCode, row.FromWarehouseId, row.FromLocationId, cancellationToken)
            .ConfigureAwait(false);

        if (available.IsFailure)
        {
            return Result<StockTransferView>.Failure(available.Errors);
        }

        bool alreadyMoved = string.Equals(row.State, StockTransferState.Moved, StringComparison.Ordinal);

        if (!alreadyMoved && available.Value.Quantity.Magnitude < resolved.Value.Magnitude)
        {
            return Result<StockTransferView>.Failure(InventoryErrors.TransferExceedsBalance(
                row.ItemCode,
                row.FromWarehouseId,
                row.FromLocationId,
                available.Value.Quantity.Magnitude,
                resolved.Value.Magnitude,
                resolved.Value.Unit));
        }

        // ‏٢ · الصادر — ومنه تأتي **القيمة**.
        Result<InventoryMovementCost> issued = await _stock.IssueAsync(
            new InventoryIssue
            {
                Tenant = tenant,
                Actor = actor,
                Source = SourceOf(documentId, row.MovementGeneration, IssuedEvent),
                Location = new InventoryItemLocation(
                    row.ItemCode, row.FromWarehouseId, row.FromLocationId, row.ItemGroup),
                Quantity = entered,
                OccurredOn = row.OccurredOn,
            },
            cancellationToken).ConfigureAwait(false);

        if (issued.IsFailure)
        {
            return Result<StockTransferView>.Failure(issued.Errors);
        }

        // ‏٣ · الوارد — **بالقيمة التي خرجت بها، لا بقيمة تُملى**. فمجموع قيمة المخزون
        //      قبل النقل وبعده واحدٌ بالضبط، وهو ما يجعل الامتناع عن القيد صحيحاً.
        Result<InventoryMovementCost> received = await _stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = actor,
                Source = SourceOf(documentId, row.MovementGeneration, ReceivedEvent),
                Location = new InventoryItemLocation(
                    row.ItemCode, row.ToWarehouseId, row.ToLocationId, row.ItemGroup),
                Quantity = entered,
                Cost = issued.Value.Cost,
                OccurredOn = row.OccurredOn,
            },
            cancellationToken).ConfigureAwait(false);

        if (received.IsFailure)
        {
            return Result<StockTransferView>.Failure(received.Errors);
        }

        row.State = StockTransferState.Moved;
        row.ValueAmount = issued.Value.Cost.Amount;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // ‏**و«نُفّذ سلفاً» حكمُ الحركة لا حكمُ حالة الصفّ**: الحالة تصير `MOVED` بعد
        // أي تنفيذ ناجح — الأول والثاني سواء — والفرق بينهما لا يُقرأ إلا من الحركة.
        return Result<StockTransferView>.Success(
            ViewOf(row) with { AlreadyMoved = issued.Value.WasAlreadyRecorded });
    }

    private static InventoryMovementSource SourceOf(string documentId, int generation, string eventCode) => new(
        BabelModule.Inventory,
        TransferDocument,
        documentId,
        PostingTrigger.OnApproval.ToString(),
        generation,
        eventCode);

    /// <summary>
    /// يتحقّق أن المستودع والموقع مسجَّلان وعاملان، وأن الموقع يقع في ذلك المستودع.
    /// <para>
    /// <b>والانتماء يُتحقَّق منه صراحةً:</b> موقعان بالرمز نفسه في مستودعين شيئان
    /// مختلفان، وقبولُ «‏A1» بلا سؤالٍ عن أبيه كان سينقل البضاعة إلى الرفّ الصحيح في
    /// المبنى الخطأ.
    /// </para>
    /// </summary>
    private async ValueTask<Result> EnsurePlaceUsableAsync(
        TenantId tenant, string warehouseCode, string locationCode, CancellationToken cancellationToken)
    {
        StoragePlaceRow? warehouse = await _database.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.Level == PlacementLevel.Warehouse
                       && row.Code == warehouseCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null)
        {
            return Result.Failure(InventoryErrors.PlaceNotFound(PlacementLevel.Warehouse, warehouseCode));
        }

        if (!warehouse.IsActive)
        {
            return Result.Failure(InventoryErrors.PlaceInactive(PlacementLevel.Warehouse, warehouseCode));
        }

        StoragePlaceRow? location = await _database.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.Level == PlacementLevel.Location
                       && row.Code == locationCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (location is null)
        {
            return Result.Failure(InventoryErrors.PlaceNotFound(PlacementLevel.Location, locationCode));
        }

        if (!UnitConversion.SameUnit(location.ParentCode, warehouseCode))
        {
            return Result.Failure(InventoryErrors.PlaceNotUnderParent(
                locationCode, location.ParentCode, warehouseCode));
        }

        return location.IsActive
            ? Result.Success()
            : Result.Failure(InventoryErrors.PlaceInactive(PlacementLevel.Location, locationCode));
    }

    private StockTransferView ViewOf(StockTransferRow row) => new(
        row.Id,
        row.Number,
        row.State,
        row.ItemCode,
        row.ItemGroup,
        row.FromWarehouseId,
        row.FromLocationId,
        row.ToWarehouseId,
        row.ToLocationId,
        new InventoryQuantity(row.Magnitude, row.UnitCode),
        Money.Of(row.ValueAmount, _currency),
        row.OccurredOn);
}
