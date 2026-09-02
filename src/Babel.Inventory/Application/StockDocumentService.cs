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
/// <b>والمكان يُتحقَّق منه عند الإنشاء لا عند الترحيل</b>: (مستودع، موقع) زوجٌ مسجَّل في
/// كتالوج الوحدة وعامل، وإلّا فلا صفّ يُكتب. ورمزٌ لا يعرفه الكتالوج كان يفتح <b>رصيداً
/// خامساً</b> يُطابَق تماماً ويحمل قيمةً حقيقية لا يعرف أحدٌ أين هي.
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
    private readonly WarehouseCatalogueService _places;
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
    /// <param name="places">
    /// كتالوج المستودعات والمواقع — <b>يُسأل عند إنشاء المسوّدة وحده</b>. ومستودعٌ ليس
    /// فيه يُرفض قبل أن يُكتب صفّ، فلا يُفتح رصيدٌ خامس على خطأ إملائي.
    /// </param>
    public StockDocumentService(
        IEntitlementEnforcer enforcer,
        InventoryRuntime runtime,
        IPostingService posting,
        StockMovementService stock,
        WarehouseCatalogueService places)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(places);

        _enforcer = enforcer;
        _database = runtime.Database;
        _stock = stock;
        _places = places;
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

        // ‏**المكان يُتحقَّق منه هنا — عند إنشاء المسوّدة، لا عند الترحيل ولا في القاعدة.**
        // مستودعٌ لا يعرفه الكتالوج يفتح رصيداً خامساً يُطابَق تماماً ويحمل قيمةً حقيقية
        // لا يعرف أحدٌ أين هي: المطابقة تجمع الحركات والأرصدة على المفتاح نفسه، فيتوازن
        // الخطأ مع نفسه ولا يُظهره فحصٌ يقارن طرفين. والرفض قبل الكتابة لا بعدها: مستندٌ
        // كُتب ثم رُفض ترحيله يبقى عالقاً بلا مخرج.
        Result place = await _places
            .EnsurePlaceIsRegisteredAsync(tenant, draft.WarehouseId, draft.LocationId, cancellationToken)
            .ConfigureAwait(false);

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
