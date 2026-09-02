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
/// دورة مستند حركة المخزون القائم بذاته: <b>مسوّدة ← قراءة ← ترحيل</b>.
/// <para>
/// وهي حركات المخزون التي <b>لا مستند لها في وحدة أخرى</b>: تسوية جرد، ورصيد افتتاحي،
/// وإعدام. أمّا استلام المشتريات وصرف المبيعات فمستنداتٌ في وحدتيهما، وحركتُهما أثرٌ
/// لها — ونشرُ بابٍ ثانٍ لها هنا كان سيجعل الحركة تُكتب مرّتين بهويتين.
/// </para>
/// <para>
/// <b>والحدث واحد بسيناريوَين</b>: <c>inventory.count_adjustment.posted</c>، وشرطاه
/// <c>is_shortage</c> و<c>is_surplus</c> هما بالضبط اتجاها هذا المستند. ولا حدث جديد
/// اختُرع، و<c>PostingPlanner</c> يرفض رمزاً ليس في المصفوفة وهو محقّ.
/// </para>
/// <para>
/// <b>والموضع المُعطَّل يُرفض عند الإنشاء <u>وعند الترحيل</u> كليهما.</b> ونداءان لا
/// نداءٌ واحد، لأن بينهما زمناً: مسوّدةٌ أُنشئت والموضع عامل تُرحَّل بعد تعطيله، فتدخل
/// البضاعةُ مكاناً أُعلن خارج الخدمة — <b>مقيس على قاعدةٍ حقيقية</b> قبل هذا الإصلاح:
/// مسوّدة ثم تعطيل ثم ترحيل ⇐ <c>POSTED</c>، ومسوّدةٌ جديدة على الموضع المُعطَّل ⇐
/// تُقبل وتُرحَّل، والرصيد <c>19</c> بقيمة <c>1900.0000</c> في موقعٍ <c>IsActive=false</c>.
/// و<c>InventoryErrors.PlaceInactive</c> يقول بنصّه «فلا تُسكَّن فيه بضاعة ولا تُنقَل
/// إليه» — وكان النصفُ الأول منه غيرَ مُنفَّذ: <c>StockTransferService</c> وحده يفرضه،
/// وهو لا يُسكِّن بل ينقل.
/// </para>
/// <para>
/// <b>ولا يُفرَض التسجيل، إنّما تُفرَض الحالة.</b> رمزٌ لا صفَّ له في السجلّ يمرّ كما
/// كان يمرّ — «السجلّ يصف ولا يُبطل»، وإلزامُ التسجيل بأثر رجعي يوقف مستأجراً عاملاً،
/// وذلك إثباتٌ قائم في <c>PlacementIsAHierarchyAndTransferIsNotAnEntryTests</c>. أمّا
/// صفٌّ <b>موجود</b> ومكتوبٌ عليه «مُعطَّل» فهو قرارٌ اتّخذه إنسان، ولا يُتجاوز بصمت.
/// </para>
/// <para>
/// <b>والترحيل يكتب الدفتر المساعد أوّلاً ثم القيد</b> (‏ADR-0041): رفضٌ من المخزون —
/// كصرفٍ بلا أساس تكلفة — يترك الدفتر نظيفاً؛ ولو وقع القيد أوّلاً لترك حساباً ضابطاً
/// تحرّك بلا حركةٍ تقابله، وهو انحرافٌ صامت.
/// </para>
/// </summary>
public sealed class StockDocumentService : IApplicationService
{
    /// <summary>نوع مستند حركة المخزون في هوية الإحكام.</summary>
    internal const string StockDocument = "InventoryStockDocument";

    /// <summary>رمز حدث تسوية الجرد في المصفوفة — بسيناريوَي العجز والزيادة.</summary>
    internal const string AdjustmentEvent = "inventory.count_adjustment.posted";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;
    private readonly StockMovementService _stock;
    private readonly InventoryPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل — الطريق الوحيد إلى دفتر الأستاذ.</param>
    /// <param name="stock">
    /// دفتر المخزون المساعد — <b>وهو من يحسب تكلفة الصادر</b>. ولا تُملى عليه قيمة صرف
    /// من هنا ولا من غيره (‏ADR-0039).
    /// </param>
    public StockDocumentService(
        IEntitlementEnforcer enforcer,
        InventoryRuntime runtime,
        IPostingService posting,
        StockMovementService stock)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(stock);

        _enforcer = enforcer;
        _database = runtime.Database;
        _stock = stock;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new InventoryPostingGateway(_database, posting, runtime.CostCenters);
    }

    /// <summary>يُنشئ مستند حركة <b>مسوّدة</b>. لا حركة ولا قيد: الترحيل خطوة مستقلّة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StockDocumentView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        StockDocumentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StockDocument.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockDocumentView>.Failure(gate.Errors);
        }

        Result quantity = UnitConversion.Validate(draft.Quantity);
        if (quantity.IsFailure)
        {
            return Result<StockDocumentView>.Failure(quantity.Errors);
        }

        bool inbound = string.Equals(draft.Direction, MovementDirection.In, StringComparison.Ordinal);

        if (!inbound && !string.Equals(draft.Direction, MovementDirection.Out, StringComparison.Ordinal))
        {
            return Result<StockDocumentView>.Failure(
                InventoryErrors.NotInState(draft.Number, draft.Direction, MovementDirection.In + " | " + MovementDirection.Out));
        }

        // الوارد يحمل تكلفته؛ والصادر **لا يحملها ولا يُقبل أن يحملها**: تكلفته تُحسب
        // في الدفتر المساعد لحظة الترحيل، ورقمٌ يُسلَّم هنا كان سيكون المبلغ المُملى
        // نفسه الذي أُزيل من وحدة المبيعات (‏ADR-0039 §1).
        if (inbound && draft.Cost.Amount <= 0m)
        {
            return Result<StockDocumentView>.Failure(InventoryErrors.ReceiptCostNotPositive(draft.Cost.Amount));
        }

        Result place = await EnsurePlaceIsNotDeactivatedAsync(
            tenant, draft.WarehouseId, draft.LocationId, cancellationToken).ConfigureAwait(false);

        if (place.IsFailure)
        {
            return Result<StockDocumentView>.Failure(place.Errors);
        }

        if (await _database.Documents
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<StockDocumentView>.Failure(InventoryErrors.DuplicateDocumentNumber(draft.Number));
        }

        StockDocumentRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            Direction = draft.Direction,
            ItemCode = draft.ItemId,
            WarehouseId = draft.WarehouseId,
            LocationId = draft.LocationId,
            ItemGroup = draft.ItemGroup,
            Magnitude = draft.Quantity.Magnitude,
            UnitCode = draft.Quantity.Unit,
            CostAmount = inbound ? draft.Cost.Amount : 0m,
            OccurredOn = draft.OccurredOn,
            State = StockDocumentState.Draft,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Documents.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<StockDocumentView>.Success(ViewOf(row));
    }

    /// <summary>يقرأ مستند حركة واحداً. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="documentId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<StockDocumentView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StockDocument.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockDocumentView>.Failure(gate.Errors);
        }

        StockDocumentRow? row = await _database.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == documentId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<StockDocumentView>.Failure(InventoryErrors.DocumentNotFound(StockDocument, documentId))
            : Result<StockDocumentView>.Success(ViewOf(row));
    }

    /// <summary>
    /// يقرأ مستندات المنشأة، <b>مرتَّبةً بالتاريخ ثم بالرقم ترتيباً حرفياً ثابتاً</b>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<StockDocumentView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StockDocument.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StockDocumentView>>.Failure(gate.Errors);
        }

        List<StockDocumentRow> rows = await _database.Documents
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StockDocumentView> views =
        [
            .. rows
                .OrderBy(static row => row.OccurredOn)
                .ThenBy(static row => row.Number, StringComparer.Ordinal)
                .Select(ViewOf),
        ];

        return Result<IReadOnlyList<StockDocumentView>>.Success(views);
    }

    /// <summary>
    /// يرحّل مستند حركة مسوّدة: <b>حركة في الدفتر المساعد ثم قيدٌ في الدفتر</b>.
    /// <para>
    /// حصينٌ ضد التكرار بهوية الترحيل: الوصول الثاني بالهوية نفسها يُعيد المستند ذاته
    /// و<c>AlreadyPosted = true</c> ومعرّف القيد نفسه، ولا يكتب حركةً ثانية.
    /// <b>والحكم حكمُ البوّابة</b> لا مقارنةَ حالةٍ قُرئت قبل النداء: نداءان متزامنان
    /// يجتازان فحص «مسوّدة» معاً ويلتقيان عند هوية الإحكام الواحدة.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="documentId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StockDocumentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StockDocument.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StockDocumentView>.Failure(gate.Errors);
        }

        StockDocumentRow? row = await _database.Documents
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == documentId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<StockDocumentView>.Failure(InventoryErrors.DocumentNotFound(StockDocument, documentId));
        }

        // ‏**والموضع يُسأل عنه هنا أيضاً، لا عند الإنشاء وحده.** بين الإنشاء والترحيل
        // زمنٌ يتّسع لقرار تعطيل، والترحيل هو اللحظة التي تدخل فيها البضاعة فعلاً:
        // ‏`item_balance` يُكتب عند الترحيل لا عند الإنشاء. فحارسٌ عند الإنشاء وحده
        // يَعِد بما لا يملك — **مقيس**: مسوّدة، ثم تعطيل، ثم ترحيل ⇐ `POSTED`.
        Result place = await EnsurePlaceIsNotDeactivatedAsync(
            tenant, row.WarehouseId, row.LocationId, cancellationToken).ConfigureAwait(false);

        if (place.IsFailure)
        {
            return Result<StockDocumentView>.Failure(place.Errors);
        }

        bool inbound = string.Equals(row.Direction, MovementDirection.In, StringComparison.Ordinal);

        InventoryMovementSource source = new(
            BabelModule.Inventory,
            StockDocument,
            row.Id.ToString("D", CultureInfo.InvariantCulture),
            PostingTrigger.OnApproval.ToString(),
            row.PostingGeneration,
            AdjustmentEvent);

        InventoryItemLocation location = new(row.ItemCode, row.WarehouseId, row.LocationId, row.ItemGroup);
        InventoryQuantity quantity = new(row.Magnitude, row.UnitCode);

        // ‏١ · الدفتر المساعد أوّلاً — ومنه تأتي **القيمة** على الصادر (‏ADR-0039 · ADR-0041).
        Result<InventoryMovementCost> moved = inbound
            ? await _stock.ReceiveAsync(
                new InventoryReceipt
                {
                    Tenant = tenant,
                    Actor = actor,
                    Source = source,
                    Location = location,
                    Quantity = quantity,
                    Cost = Money.Of(row.CostAmount, _currency),
                    OccurredOn = row.OccurredOn,
                },
                cancellationToken).ConfigureAwait(false)
            : await _stock.IssueAsync(
                new InventoryIssue
                {
                    Tenant = tenant,
                    Actor = actor,
                    Source = source,
                    Location = location,
                    Quantity = quantity,
                    OccurredOn = row.OccurredOn,
                },
                cancellationToken).ConfigureAwait(false);

        if (moved.IsFailure)
        {
            return Result<StockDocumentView>.Failure(moved.Errors);
        }

        // ‏٢ · القيد. والمبلغ هو **قيمة الحركة كما حسبها الدفتر المساعد**، لا رقمٌ من هنا.
        InventoryPostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = StockDocument,
            DocumentId = row.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(AdjustmentEvent),
            DocumentDate = row.OccurredOn,
            Narration = new LocalizedName("حركة مخزون " + row.Number, "Stock movement " + row.Number),
            Amounts = [new PostingAmount("diff", moved.Value.Cost)],
            Facts =
            [
                new PostingFact("condition.is_shortage", inbound ? "false" : "true"),
                new PostingFact("condition.is_surplus", inbound ? "true" : "false"),
                new PostingFact("subledger.item", row.ItemCode),
                new PostingFact("line.item_group", row.ItemGroup),
            ],
            Dimensions = [new PostingDimension("warehouse", row.WarehouseId)],
            PartyId = row.ItemCode,
            Currency = _currency,
            Actor = actor,
            Generation = row.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<StockDocumentView>.Failure(posted.Errors);
        }

        row.State = StockDocumentState.Posted;
        row.PostedEntryId = posted.Value.JournalEntryId;
        row.CostAmount = moved.Value.Cost.Amount;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<StockDocumentView>.Success(ViewOf(row) with { AlreadyPosted = posted.Value.WasAlreadyPosted });
    }

    /// <summary>
    /// <b>أموضعُ هذا المستند مُعلَنٌ خارج الخدمة؟</b> — سؤالٌ عن <b>الحالة</b> لا عن الوجود.
    /// <para>
    /// <b>ورمزٌ لا صفَّ له يمرّ.</b> السجلّ في هذه الوحدة <b>يصف ولا يُبطل</b>: حركاتٌ
    /// كُتبت قبل وجوده تحمل رموزاً بلا صفوف، وقراءةُ الأرصدة تُخرجها موسومةً
    /// <c>WarehouseRegistered=false</c> ولا تحذفها. فاشتراطُ التسجيل هنا كان سيوقف
    /// مستأجراً عاملاً بأثرٍ رجعيّ، وهو انقلابٌ على قرارٍ قائم لا إغلاقُ ثغرة.
    /// </para>
    /// <para>
    /// <b>أمّا صفٌّ موجودٌ عليه «مُعطَّل» فقرارُ إنسان</b>، و<c>PlaceInactive</c> يقول
    /// بنصّه «فلا تُسكَّن فيه بضاعة». وتجاوزُه هنا كان يجعل ذلك النصَّ كاذباً: البضاعة
    /// تُسكَّن، والرصيد يظهر في موضعٍ الشاشةُ تعرضه خارج الخدمة، ولا فحصَ توازنٍ يراه
    /// لأنه يتوازن مع نفسه.
    /// </para>
    /// <para>
    /// <b>والرفّ (<c>BIN</c>) ليس هنا</b>: المستند يحمل مستودعاً وموقعاً لا غير، وهو
    /// مستوى الرصيد المُقيَّم (‏ADR-0070). فلا يُسأل عن مستوىً لا يذكره المستند.
    /// </para>
    /// </summary>
    private async ValueTask<Result> EnsurePlaceIsNotDeactivatedAsync(
        TenantId tenant,
        string warehouseCode,
        string locationCode,
        CancellationToken cancellationToken)
    {
        StoragePlaceRow? warehouse = await _database.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.Level == PlacementLevel.Warehouse
                       && row.Code == warehouseCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is not null && !warehouse.IsActive)
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

        return location is null || location.IsActive
            ? Result.Success()
            : Result.Failure(InventoryErrors.PlaceInactive(PlacementLevel.Location, locationCode));
    }

    private StockDocumentView ViewOf(StockDocumentRow row) => new(
        row.Id,
        row.Number,
        row.State,
        row.Direction,
        row.ItemCode,
        row.WarehouseId,
        row.LocationId,
        row.ItemGroup,
        new InventoryQuantity(row.Magnitude, row.UnitCode),
        Money.Of(row.CostAmount, _currency),
        row.OccurredOn,
        row.PostedEntryId);
}
