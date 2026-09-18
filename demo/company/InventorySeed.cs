using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Core;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Inventory;
using Babel.Inventory.Application;
using Babel.Ledger;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BabelDemoCompany;

/// <summary>
/// بذر المخزون — <b>دورةٌ كاملة تُرى على عشر شاشات، لا صفوفٌ خام في جدول</b>.
/// <para>
/// وكل صفٍّ هنا يمرّ بخدمات الوحدة المعلَنة نفسها التي يناديها سطح HTTP: وحداتُ
/// قياسٍ ومعاملاتُ تحويلٍ بينها، ثمّ أصناف، ثمّ هرمُ تسكينٍ من مستودعٍ إلى موقع،
/// ثمّ مستنداتُ حركةٍ تُرحَّل فتصير أرصدةً ذاتَ تكلفةٍ مرجّحة، ثمّ نقلٌ بين
/// موقعين، ثمّ صنفٌ يُعطَّل فتصير شاشة حاله واقعةً لا جدولاً فارغاً.
/// </para>
/// <para>
/// <b>ولا إدراج خام واحد</b>، للسبب نفسه الذي في <see cref="Seed"/>: بذرٌ يكتب في
/// جداول المخزون مباشرةً ينتج أرصدةً لا يقابلها قيدٌ في الدفتر، وتكلفةً مرجّحة لم
/// يحسبها أحد. فتُظهر الشاشاتُ أرقاماً صحيحةَ الشكل كاذبةَ المصدر — وهو أسوأ من
/// جدولٍ فارغ، لأن الفارغ يُرى والكاذب يُصدَّق.
/// </para>
/// <para>
/// <b>والاستحقاق يُشترى أوّلاً:</b> خدماتُ المخزون كلُّها خلف
/// <c>RequiresEntitlement(Inventory, Write)</c>، فبلا شرائه تُرفض أولُ كتابة
/// برسالةٍ صريحة — وهو رفضٌ صحيح لا عقبة.
/// </para>
/// </summary>
internal sealed class InventorySeed : IDisposable
{
    private readonly Settings _settings;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private readonly UnitOfMeasureService _units;
    private readonly ItemCatalogueService _items;
    private readonly StoragePlaceService _places;
    private readonly StockDocumentService _documents;
    private readonly StockTransferService _transfers;
    private readonly IEntitlementService _entitlements;
    private readonly ICompanyMoneyResolver _companyMoney;

    private CompanyMoney _money;
    private int _postedDocuments;

    private InventorySeed(Settings settings)
    {
        _settings = settings;

        ServiceCollection services = new();

        // النواة: منها حالُّ عملة المنشأة ومنفِّذ الاستحقاق — وبمخزنَي PostgreSQL
        // لا بمخزن ذاكرة، كالخادم بالضبط.
        services.AddBabelCore(options =>
        {
            options.AppConnectionString = settings.Core.AppConnectionString;
            options.OwnerConnectionString = settings.Core.OwnerConnectionString;
            options.AppRole = settings.Core.AppRole;
        });

        // الدفتر: منه IPostingService الذي تناديه بوّابة ترحيل المخزون، فكلُّ مستندٍ
        // يُرحَّل هنا يكتب قيده في الدفتر نفسه لا في دفترٍ ثانٍ.
        services.AddBabelLedger(options =>
        {
            options.AppConnectionString = settings.Ledger.AppConnectionString;
            options.OwnerConnectionString = settings.Ledger.OwnerConnectionString;
            options.AppRole = settings.Ledger.AppRole;
        });

        services.AddBabelInventory(options =>
        {
            options.ConnectionString = settings.InventoryOwner.ConnectionString;
        });

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        IServiceProvider scoped = _scope.ServiceProvider;
        _units = scoped.GetRequiredService<UnitOfMeasureService>();
        _items = scoped.GetRequiredService<ItemCatalogueService>();
        _places = scoped.GetRequiredService<StoragePlaceService>();
        _documents = scoped.GetRequiredService<StockDocumentService>();
        _transfers = scoped.GetRequiredService<StockTransferService>();
        _entitlements = scoped.GetRequiredService<IEntitlementService>();
        _companyMoney = scoped.GetRequiredService<ICompanyMoneyResolver>();
    }

    private TenantId Tenant => new(_settings.Company);

    private CurrencyCode Currency => _money.IsAssigned
        ? _money.Currency
        : throw new InvalidOperationException("عملة المنشأة تُحلّ في أول الدورة قبل أي مستند.");

    /// <summary>يبذر دورة المخزون إن لم تكن مبذورة، ويُرجع عدد المستندات المُرحَّلة.</summary>
    /// <param name="settings">الإعدادات.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Say.Step("بذر دورة المخزون عبر خدمات الوحدة / seeding the inventory cycle through the module services");

        // حارسُ إعادةٍ **مستقلّ** عن حرّاس الأقسام الأخرى: نشرٌ يُعاد بعد فشلٍ في
        // منتصفه يجب أن يبلغ ما لم يُبذر بعدُ لا أن يُصرَف عنه لأن جدولاً آخر امتلأ.
        if (await AlreadySeededAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            Say.Detail("المخزون مبذور سلفاً — لا يُعاد البذر.");
            return 0;
        }

        using InventorySeed seed = new(settings);
        await seed.EntitleAsync(cancellationToken).ConfigureAwait(false);
        await seed.CycleAsync(cancellationToken).ConfigureAwait(false);
        return seed._postedDocuments;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private static async Task<bool> AlreadySeededAsync(Settings settings, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(settings.InventoryOwner.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        // ‏**والفحصُ على أوّل ما تكتبه الدورة لا على آخره.** وحدةُ القياس أولُ صفٍّ
        // يُكتب، والصنفُ بعدها بخطوتين — فحارسٌ على `item` يقول «لم يُبذر» عن شوطٍ
        // سقط في منتصفه بعد كتابة الوحدات، فيُعاد فيُرفض بازدواج رمزٍ لا بعطلٍ حقيقي.
        await using NpgsqlCommand command = new(
            """select count(*) from inventory.unit_of_measure where "TenantId" = $1""", connection);
        command.Parameters.AddWithValue(settings.Company);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) > 0;
    }

    private async Task EntitleAsync(CancellationToken cancellationToken)
    {
        Result<EntitlementSet> applied = await _entitlements
            .ApplyAsync(
                new EntitlementChangeRequest(
                    Tenant,
                    new Dictionary<BabelModule, EntitlementState> { [BabelModule.Inventory] = EntitlementState.Entitled },
                    Seed.Actor,
                    "شراء وحدة المخزون للمنشأة التجريبية / inventory entitled for the demo company"),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(applied, "شراء وحدة المخزون");
    }

    private async Task CycleAsync(CancellationToken cancellationToken)
    {
        Result<CompanyMoney> money = await _companyMoney.ResolveAsync(Tenant, cancellationToken).ConfigureAwait(false);
        Say.Require(money.IsSuccess, "عملة المنشأة من صفّ التأسيس", string.Join(" | ", money.Errors.Select(static e => e.ToString())));
        _money = money.Value;

        // ── ١ · وحداتُ القياس ومعاملاتُ التحويل ────────────────────────────────
        //
        // ثلاثُ وحدات من صنفَي كمّيةٍ مختلفين، ومعاملٌ واحد بينها — فشاشة الوحدات
        // تعرض تحويلاً حقيقياً لا جدولاً بوحدةٍ واحدة لا تُحوَّل إلى شيء.
        await UnitAsync("PCS", "حبّة", "Piece", "COUNT", cancellationToken).ConfigureAwait(false);
        await UnitAsync("BOX", "كرتون", "Carton", "COUNT", cancellationToken).ConfigureAwait(false);
        await UnitAsync("KG", "كيلوغرام", "Kilogram", "WEIGHT", cancellationToken).ConfigureAwait(false);
        Say.Detail("وحداتُ قياس: ثلاث من صنفَي كمّية — العدّ والوزن.");

        // كرتونٌ = اثنتا عشرة حبّة. والمعاملُ بين وحدتَي **صنفِ الكمّية نفسه** فقط:
        // تحويلُ كتلةٍ إلى عددٍ ليس معاملاً بل كذبة.
        Ok(await _units.CreateConversionAsync(
                Tenant, Seed.Actor, new UnitConversionDraft("BOX", "PCS", 12, 1), cancellationToken).ConfigureAwait(false),
            "معامل التحويل كرتون ← حبّة");
        Say.Detail("معاملُ تحويل: كرتون = ١٢ حبّة.");

        // ── ٢ · الأصناف ────────────────────────────────────────────────────────
        _ = await ItemAsync("ITM-001", "أسمنت مقاوم 50kg", "Sulphate-resistant cement 50kg", "MATERIAL", "KG", cancellationToken).ConfigureAwait(false);
        _ = await ItemAsync("ITM-002", "حديد تسليح 16mm", "Rebar 16mm", "MATERIAL", "KG", cancellationToken).ConfigureAwait(false);
        _ = await ItemAsync(
            "ITM-003", "بلاط بورسلان 60×60", "Porcelain tile 60×60", "MATERIAL", "BOX", cancellationToken,
            new ItemUnitDraft("PCS", 1, 12)).ConfigureAwait(false);
        _ = await ItemAsync("ITM-004", "كابل كهربائي 3×2.5mm", "Electrical cable 3×2.5mm", "MATERIAL", "PCS", cancellationToken).ConfigureAwait(false);
        _ = await ItemAsync("ITM-005", "دهان أساس أبيض 20L", "White primer 20L", "MATERIAL", "PCS", cancellationToken).ConfigureAwait(false);
        Guid toolSet = await ItemAsync("ITM-006", "عدّة يدوية — طقم", "Hand tool set", "SUPPLY", "PCS", cancellationToken).ConfigureAwait(false);
        Say.Detail("أصناف: ستّة بأسماء عربية وإنجليزية ووحداتِ أساسٍ مختلفة.");

        // ── ٣ · هرمُ التسكين: مستودعان، وموقعان تحت كلٍّ ───────────────────────
        Guid main = await PlaceAsync("WAREHOUSE", null, "WH-MAIN", "المستودع الرئيسي — الدمام", "Main store — Dammam", cancellationToken).ConfigureAwait(false);
        Guid site = await PlaceAsync("WAREHOUSE", null, "WH-SITE", "مستودع الموقع — الخبر", "Site store — Khobar", cancellationToken).ConfigureAwait(false);
        await PlaceAsync("LOCATION", main, "LOC-A1", "الرفّ أ-١", "Rack A1", cancellationToken).ConfigureAwait(false);
        await PlaceAsync("LOCATION", main, "LOC-A2", "الرفّ أ-٢", "Rack A2", cancellationToken).ConfigureAwait(false);
        await PlaceAsync("LOCATION", site, "LOC-B1", "الساحة ب-١", "Yard B1", cancellationToken).ConfigureAwait(false);
        Say.Detail("تسكين: مستودعان وثلاثةُ مواقع تحتهما.");

        // ── ٤ · الواردات — وهي وحدها تحمل تكلفة (ADR-0039) ─────────────────────
        //
        // وارداتٌ على أشهرٍ مختلفة بتكاليفَ مختلفة للصنف نفسه، **عمداً**: بذلك
        // تصير التكلفةُ المرجّحة في شاشة التقييم رقماً محسوباً لا رقماً واحداً
        // مكرَّراً، ويُرى أثرُ الترجيح على الشاشة.
        await ReceiveAsync("STK-IN-0001", "ITM-001", "WH-MAIN", "LOC-A1", 2_000m, "KG", 24_000m, 3, 12, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0002", "ITM-001", "WH-MAIN", "LOC-A1", 1_500m, "KG", 19_500m, 5, 8, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0003", "ITM-002", "WH-MAIN", "LOC-A2", 8_000m, "KG", 96_000m, 3, 20, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0004", "ITM-003", "WH-MAIN", "LOC-A2", 180m, "BOX", 27_000m, 4, 6, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0005", "ITM-004", "WH-SITE", "LOC-B1", 400m, "PCS", 18_000m, 4, 18, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0006", "ITM-005", "WH-SITE", "LOC-B1", 60m, "PCS", 9_600m, 5, 3, cancellationToken).ConfigureAwait(false);
        await ReceiveAsync("STK-IN-0007", "ITM-006", "WH-MAIN", "LOC-A1", 25m, "PCS", 8_750m, 6, 11, cancellationToken).ConfigureAwait(false);

        // ── ٥ · الصادرات — بلا تكلفةٍ مُملاة، تُحسب في الدفتر المساعد ──────────
        await IssueAsync("STK-OUT-0001", "ITM-001", "WH-MAIN", "LOC-A1", 900m, "KG", 6, 4, cancellationToken).ConfigureAwait(false);
        await IssueAsync("STK-OUT-0002", "ITM-002", "WH-MAIN", "LOC-A2", 2_400m, "KG", 6, 19, cancellationToken).ConfigureAwait(false);
        await IssueAsync("STK-OUT-0003", "ITM-003", "WH-MAIN", "LOC-A2", 45m, "BOX", 7, 9, cancellationToken).ConfigureAwait(false);
        await IssueAsync("STK-OUT-0004", "ITM-004", "WH-SITE", "LOC-B1", 120m, "PCS", 7, 22, cancellationToken).ConfigureAwait(false);
        Say.Detail("حركاتٌ مُرحَّلة: " + Say.Count(_postedDocuments) + " — واردٌ بتكلفته وصادرٌ تُحسب تكلفته.");

        // ── ٦ · نقلٌ بين موقعين — حركةُ مكانٍ لا حركةَ قيمة ────────────────────
        Ok(await _transfers.CreateAsync(
                Tenant,
                Seed.Actor,
                new StockTransferDraft(
                    "STK-TRF-0001",
                    "ITM-001",
                    "MATERIAL",
                    "WH-MAIN",
                    "LOC-A1",
                    "WH-SITE",
                    "LOC-B1",
                    new InventoryQuantity(500m, "KG"),
                    new DateOnly(_settings.FiscalYear, 7, 15)),
                cancellationToken).ConfigureAwait(false),
            "مسوّدة النقل STK-TRF-0001");

        Say.Detail("نقلٌ واحد بين موقعين — مسوّدةً تنتظر التحريك، فتُرى الشاشةُ بحالتَيها.");

        // ── ٧ · صنفٌ مُعطَّل — فشاشةُ حال الصنف تعرض واقعةً لا جدولاً فارغاً ───
        //
        // و«العدّة اليدوية» لا الأسمنت: التعطيلُ يقع على صنفٍ **له رصيد** وحركةٌ
        // سابقة، فتُرى الشاشةُ كما تُرى عند العميل — لا على صنفٍ وُلد ميتاً.
        Ok(await _items.DeactivateAsync(Tenant, Seed.Actor, toolSet, cancellationToken).ConfigureAwait(false),
            "تعطيل الصنف ITM-006");

        Say.Detail("صنفٌ واحد مُعطَّل — شاشةُ حال الصنف تعرض دورةَ حياةٍ حقيقية لا جدولاً فارغاً.");
    }

    private async Task UnitAsync(string code, string arabic, string english, string quantityClass, CancellationToken cancellationToken)
        => Ok(
            await _units.CreateAsync(
                Tenant, Seed.Actor, new UnitOfMeasureDraft(code, new LocalizedName(arabic, english), quantityClass), cancellationToken)
                .ConfigureAwait(false),
            "إنشاء وحدة القياس " + code);

    /// <summary>
    /// يسجّل صنفاً. و<paramref name="alternates"/> للوحدات <b>البديلة وحدها</b>:
    /// وحدةُ الأساس ليست منها، لأن كلَّ وحدةٍ في القائمة يُفحَص لها معامل تحويلٍ
    /// إلى الأساس — ولا معاملَ من وحدةٍ إلى نفسها، فإدراجُ الأساس يُرفض بـ
    /// <c>inventory.unit_not_convertible</c>.
    /// </summary>
    private async Task<Guid> ItemAsync(
        string code,
        string arabic,
        string english,
        string group,
        string baseUnit,
        CancellationToken cancellationToken,
        params ItemUnitDraft[] alternates)
    {
        Result<ItemView> created = await _items
            .CreateAsync(
                Tenant,
                Seed.Actor,
                new ItemDraft(code, new LocalizedName(arabic, english), group, baseUnit, alternates),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إنشاء الصنف " + code);
        return created.Value.Id;
    }

    private async Task<Guid> PlaceAsync(
        string level, Guid? parent, string code, string arabic, string english, CancellationToken cancellationToken)
    {
        Result<StoragePlaceView> created = await _places
            .CreateAsync(Tenant, Seed.Actor, level, parent, new StoragePlaceDraft(code, new LocalizedName(arabic, english)), cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "إنشاء موضع التسكين " + code);
        return created.Value.Id;
    }

    /// <summary>واردٌ يُنشأ ثمّ يُرحَّل — فلا يبقى مسوّدةً لا تظهر في رصيد.</summary>
    private Task ReceiveAsync(
        string number, string item, string warehouse, string location,
        decimal quantity, string unit, decimal cost, int month, int day, CancellationToken cancellationToken)
        => DocumentAsync(number, "IN", item, warehouse, location, quantity, unit, cost, month, day, cancellationToken);

    /// <summary>صادرٌ يُنشأ ثمّ يُرحَّل. <b>وتكلفتُه صفرٌ على السلك</b> — تُحسب في الوحدة.</summary>
    private Task IssueAsync(
        string number, string item, string warehouse, string location,
        decimal quantity, string unit, int month, int day, CancellationToken cancellationToken)
        => DocumentAsync(number, "OUT", item, warehouse, location, quantity, unit, 0m, month, day, cancellationToken);

    private async Task DocumentAsync(
        string number, string direction, string item, string warehouse, string location,
        decimal quantity, string unit, decimal cost, int month, int day, CancellationToken cancellationToken)
    {
        Result<StockDocumentView> created = await _documents
            .CreateAsync(
                Tenant,
                Seed.Actor,
                new StockDocumentDraft(
                    number,
                    direction,
                    item,
                    warehouse,
                    location,
                    "MATERIAL",
                    new InventoryQuantity(quantity, unit),
                    Money.Of(cost, Currency),
                    new DateOnly(_settings.FiscalYear, month, day)),
                cancellationToken)
            .ConfigureAwait(false);

        Ok(created, "مسوّدة حركة المخزون " + number);

        Ok(
            await _documents.PostAsync(Tenant, Seed.Actor, created.Value.Id, cancellationToken).ConfigureAwait(false),
            "ترحيل حركة المخزون " + number);

        _postedDocuments++;
    }

    private static void Ok<T>(Result<T> result, string what)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                what + " — رُفض: " + string.Join(" | ", result.Errors.Select(static error => error.ToString())));
        }
    }
}
