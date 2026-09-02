using System.Globalization;
using Npgsql;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>الهجرة تُشغَّل على قاعدةٍ فيها بيانات — وهذا هو جواب ت-7.</b>
/// <para>
/// <b>ما كان ناقصاً بالضبط:</b> كل قواعد هذا المستودع تُنشأ من الصفر، فالمسار
/// المُختبَر فعلاً هو <c>EnsureCreated</c> — أي <b>الفرع الذي يعود فوراً</b> من
/// كتلة الهجرة لأن <c>to_regclass</c> تُرجع <c>null</c>. والهجرتان مقروءتان
/// ومعلَّلتان و<b>غير مُشغَّلتين</b>، وهي الحالة التي وُصفت في
/// <c>docs/evidence/verification-debt.md</c> بـ«لا يُكتب في أي مستند أن الترقية
/// مُثبَتة».
/// </para>
/// <para>
/// <b>وهذه المجموعة تبني الشكل القديم بيدها</b> — مفتاح رصيدٍ <b>ثلاثي</b> بلا
/// موقعٍ ولا وحدة، وحركاتٌ بلا وحدة ولا موقع — وتبذر فيه أرصدةً وحركاتٍ لمنشأتين
/// وثلاثة مستودعات، ثم تُشغّل الناشر ثلاث مرّات، ثم تُؤكّد خمسة:
/// </para>
/// <para>
/// ‏<b>١</b> لا كمّية واحدة تغيّرت ولا قيمة · <b>٢</b> الفهرس الفريد صار رباعياً
/// باسمه القديم · <b>٣</b> لكل نصّ مستودعٍ <b>رُصد</b> صفٌّ في الكتالوج بمنشأ
/// <c>OBSERVED</c> واسمُه رمزُه، ولكلٍّ موقعُ <c>DEFAULT</c> · <b>٤</b> التشغيل
/// الثاني لا يفعل شيئاً ولا يفشل ولا يُضاعف صفّاً · <b>٥</b> وموقعٌ يُكتب <b>بين
/// ترقيتين</b> يُلاحَظ في التالية — وهو المشهد الحقيقي لعميلٍ قائم.
/// </para>
/// <para>
/// <b>وقاعدتها قاعدتها وحدها</b>: تُنشأ باسمٍ خاصّ بهذه العملية وتُحذف عند الخروج،
/// فلا تمسّ قاعدة المخزون المشتركة التي تُبنى من الصفر — <b>ولا يجوز أن تمسّها</b>:
/// المقيس هنا هو بالضبط ما لا يقع هناك.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class TheMigrationRunsOnADatabaseThatHasDataTests : IAsyncLifetime
{
    /// <summary>منشأة هذا الإثبات وحده — بذرةٌ مكتوبة بيدٍ في قاعدةٍ خاصّة به.</summary>
    private static readonly Guid SeededTenant = new("7b1e0c33-0000-4000-8000-0000000000a1");

    /// <summary>ومنشأةٌ ثانية في القاعدة نفسها: الملء يُلاحظ لكل منشأة على حدة.</summary>
    private static readonly Guid NeighbourTenant = new("7b1e0c33-0000-4000-8000-0000000000a2");

    private string _connectionString = null!;

    public async ValueTask InitializeAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await using NpgsqlConnection admin = new(InventoryTestEnvironment.Maintenance);
        await admin.OpenAsync(token);

        await using (NpgsqlCommand drop = new(
            $"drop database if exists {InventoryTestEnvironment.SeededDatabase} with (force)", admin))
        {
            await drop.ExecuteNonQueryAsync(token);
        }

        await using (NpgsqlCommand create = new(
            $"create database {InventoryTestEnvironment.SeededDatabase}", admin))
        {
            await create.ExecuteNonQueryAsync(token);
        }

        _connectionString = "Host=127.0.0.1;Port=5432;Database="
            + InventoryTestEnvironment.SeededDatabase
            + ";Username=postgres;Include Error Detail=true";
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using NpgsqlConnection admin = new(InventoryTestEnvironment.Maintenance);
        await admin.OpenAsync(CancellationToken.None);

        await using NpgsqlCommand drop = new(
            $"drop database if exists {InventoryTestEnvironment.SeededDatabase} with (force)", admin);
        await drop.ExecuteNonQueryAsync(CancellationToken.None);
    }

    [Fact]
    public async Task الهجرتان_تصفان_ما_وُجد_ولا_تُعيدان_كتابة_رقمٍ_واحد()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await SeedOldShapeAsync(token);

        // ما كان قبل الترقية، مقروءاً من القاعدة نفسها لا مكتوباً هنا مرّتين.
        (decimal quantityBefore, decimal valueBefore) = await TotalsAsync(token);
        long movementsBefore = await ScalarAsync("select count(*) from inventory.stock_movement", token);
        long balancesBefore = await ScalarAsync("select count(*) from inventory.item_balance", token);

        Proof.Require(
            movementsBefore == 5 && balancesBefore == 4,
            "البذرة على الشكل القديم: خمس حركات وأربعة أرصدة، بلا موقعٍ ولا وحدة ولا كتالوج",
            "حركات=" + movementsBefore.ToString(CultureInfo.InvariantCulture)
            + " · أرصدة=" + balancesBefore.ToString(CultureInfo.InvariantCulture));

        // ── الترقية ─────────────────────────────────────────────────────────
        InventoryOptions options = new() { ConnectionString = _connectionString, CompanyCurrency = "SAR" };
        await InventorySchemaDeployer.DeployAsync(options, token);

        // ── ١ · ولا رقم واحد أُعيدت كتابته ──────────────────────────────────
        (decimal quantityAfter, decimal valueAfter) = await TotalsAsync(token);

        Proof.Require(
            quantityBefore == quantityAfter && valueBefore == valueAfter
            && movementsBefore == await ScalarAsync("select count(*) from inventory.stock_movement", token)
            && balancesBefore == await ScalarAsync("select count(*) from inventory.item_balance", token),
            "الترقية لم تُعِد كتابة كمّية واحدة ولا قيمة، ولا حذفت صفّاً ولا أضافت",
            "كمّية " + Number(quantityBefore) + " ⇒ " + Number(quantityAfter)
            + " · قيمة " + Proof.Money(valueBefore) + " ⇒ " + Proof.Money(valueAfter));

        // ── ٢ · المفتاح صار رباعياً باسمه القديم ────────────────────────────
        string key = await TextAsync(
            """
            select string_agg(a.attname, ',' order by k.ord)
              from pg_class c
              join pg_index i on i.indexrelid = c.oid
              join lateral unnest(i.indkey) with ordinality as k(attnum, ord) on true
              join pg_attribute a on a.attrelid = i.indrelid and a.attnum = k.attnum
             where c.relname = 'uq_inventory_item_balance'
            """,
            token);

        Proof.Require(
            key == "TenantId,ItemId,WarehouseId,LocationId",
            "والفهرس الفريد صار رباعياً وبقي اسمه — فلا فهرسٌ يتيم ولا نافذةٌ بلا حارس",
            key);

        // ── ٣ · الملء بالملاحظة: صفٌّ لكل نصٍّ وُجد، بمنشأ OBSERVED ─────────
        long warehouses = await ScalarAsync(
            "select count(*) from inventory.warehouse where \"Origin\" = 'OBSERVED'", token);

        string observed = await TextAsync(
            """
            select string_agg("TenantId" || '/' || "Code", ' · ' order by "TenantId", "Code")
              from inventory.warehouse
            """,
            token);

        Proof.Require(
            warehouses == 3
            && observed == SeededTenant + "/WH-JEDDAH · " + SeededTenant + "/WH-RIYADH · "
                         + NeighbourTenant + "/WH-RIYADH",
            "ولكل نصّ مستودعٍ **وُجد فعلاً** صفٌّ واحد بمنشأ OBSERVED — ومستودع الجارة مستودعها هي",
            observed);

        string names = await TextAsync(
            """
            select string_agg("Code" || '⇒' || "NameAr", ' · ' order by "Code")
              from inventory.warehouse where "TenantId" = $1
            """,
            token,
            SeededTenant);

        Proof.Require(
            names == "WH-JEDDAH⇒WH-JEDDAH · WH-RIYADH⇒WH-RIYADH",
            "والاسم هو الرمز نفسه — **لا اسم يُخترع**: الملء إفادةٌ عمّا وُجد لا تخمينٌ لما يُقصد",
            names);

        string locations = await TextAsync(
            """
            select string_agg("WarehouseCode" || '/' || "Code", ' · ' order by "WarehouseCode", "Code")
              from inventory.location where "TenantId" = $1
            """,
            token,
            SeededTenant);

        Proof.Require(
            locations == "WH-JEDDAH/DEFAULT · WH-RIYADH/DEFAULT",
            "و«DEFAULT» — الذي كتبته الهجرة 001 اصطلاحاً — صار موقعاً مُسجَّلاً في كل مستودعٍ وُجد، ولا موقع يُخترع حيث لم يُكتب",
            locations);

        long declared = await ScalarAsync(
            "select count(*) from inventory.location where \"Origin\" <> 'OBSERVED'", token);

        Proof.Require(
            declared == 0,
            "ولا صفّ واحد بمنشأ DECLARED: لم يكتب إنسانٌ شيئاً في هذه القاعدة",
            declared.ToString(CultureInfo.InvariantCulture));

        // ── ٤ · التشغيل الثاني لا يُضاعف صفّاً ولا يفشل ─────────────────────
        await InventorySchemaDeployer.DeployAsync(options, token);

        (decimal quantityTwice, decimal valueTwice) = await TotalsAsync(token);
        long warehousesTwice = await ScalarAsync("select count(*) from inventory.warehouse", token);
        long locationsTwice = await ScalarAsync("select count(*) from inventory.location", token);

        Proof.Require(
            quantityTwice == quantityBefore && valueTwice == valueBefore
            && warehousesTwice == 3 && locationsTwice == 3,
            "والتشغيل الثاني لا يفعل شيئاً ولا يفشل — ولا يُضاعف صفّاً واحداً في الكتالوج",
            "مستودعات=" + warehousesTwice.ToString(CultureInfo.InvariantCulture)
            + " · مواقع=" + locationsTwice.ToString(CultureInfo.InvariantCulture));

        // ── ٥ · وموقعٌ يظهر **بين ترقيتين** يُلاحَظ في التالية ───────────────
        // هذا هو المشهد الحقيقي لعميلٍ قائم: تُنزَّل الترقية، ثم تُكتب حركاتٌ في
        // مواقع جديدة، ثم تُنزَّل الترقية التالية. والملء يلاحظ **ما وُجد**، لا ما
        // وُجد يوم كُتب.
        await ExecuteAsync(
            $"""
            insert into inventory.item_balance
                ("Id","TenantId","ItemId","WarehouseId","LocationId","BaseUnit",
                 "Quantity","ValueAmount","UnitCost","HasCostBasis","UpdatedAt")
            values (gen_random_uuid(), '{SeededTenant}', 'ITEM-3', 'WH-RIYADH', 'A-01', 'EACH',
                    5.000000, 50.0000, 10.000000, true, now());
            """,
            token);

        await InventorySchemaDeployer.DeployAsync(options, token);

        string afterPlacement = await TextAsync(
            """
            select string_agg("WarehouseCode" || '/' || "Code" || '@' || "Origin", ' · '
                              order by "WarehouseCode", "Code")
              from inventory.location where "TenantId" = $1
            """,
            token,
            SeededTenant);

        Proof.Require(
            afterPlacement == "WH-JEDDAH/DEFAULT@OBSERVED · WH-RIYADH/A-01@OBSERVED · WH-RIYADH/DEFAULT@OBSERVED",
            "وموقعٌ كُتب بعد الترقية الأولى يُلاحَظ في الثانية، ولا يُضاعَف ما لوحظ قبله",
            afterPlacement);
    }

    /// <summary>
    /// يبني <b>الشكل الذي كان قبل الهجرة 001</b>: مفتاح رصيدٍ ثلاثي، وحركاتٌ بلا وحدة
    /// ولا موقع، ولا كتالوج مستودعاتٍ ولا مواقع. ثم يبذر فيه أرصدةً وحركاتٍ حقيقية.
    /// <para>
    /// <b>ولا يُستدعى هنا نموذج EF ولا EnsureCreated</b>: استعمالُ الشكل الحالي لبناء
    /// «الشكل القديم» يجعل الإثبات يقيس نفسه.
    /// </para>
    /// </summary>
    private async Task SeedOldShapeAsync(CancellationToken token)
    {
        await ExecuteAsync(
            """
            create schema if not exists inventory;

            create table inventory.stock_movement (
                "Id"                 uuid                  not null primary key,
                "TenantId"           uuid                  not null,
                "SourceModule"       character varying(32) not null,
                "DocumentType"       character varying(64) not null,
                "DocumentId"         character varying(64) not null,
                "TriggerCode"        character varying(32) not null,
                "Generation"         integer               not null,
                "EventCode"          character varying(128) not null,
                "ItemId"             character varying(64) not null,
                "WarehouseId"        character varying(64) not null,
                "ItemGroup"          character varying(64) not null,
                "Direction"          character varying(8)  not null,
                "Quantity"           numeric(19,6)         not null,
                "ValueAmount"        numeric(19,4)         not null,
                "UnitCost"           numeric(19,6)         not null,
                "Method"             character varying(32) not null,
                "DrewOnNegativeStock" boolean              not null,
                "AgainstKey"         character varying(128) not null default '',
                "QuantityAfter"      numeric(19,6)         not null,
                "ValueAfter"         numeric(19,4)         not null,
                "OccurredOn"         date                  not null,
                "RecordedAt"         timestamp with time zone not null,
                "ActorId"            character varying(64) not null
            );

            create unique index uq_inventory_movement_identity
                on inventory.stock_movement
                   ("TenantId","SourceModule","DocumentType","DocumentId","TriggerCode","Generation","EventCode");

            create index ix_inventory_movement_item
                on inventory.stock_movement ("TenantId","ItemId","WarehouseId");

            create table inventory.item_balance (
                "Id"           uuid                  not null primary key,
                "TenantId"     uuid                  not null,
                "ItemId"       character varying(64) not null,
                "WarehouseId"  character varying(64) not null,
                "Quantity"     numeric(19,6)         not null,
                "ValueAmount"  numeric(19,4)         not null,
                "UnitCost"     numeric(19,6)         not null,
                "HasCostBasis" boolean               not null,
                "UpdatedAt"    timestamp with time zone not null
            );

            create unique index uq_inventory_item_balance
                on inventory.item_balance ("TenantId","ItemId","WarehouseId");
            """,
            token);

        // البذرة: مستودعان لمنشأة، ومستودعٌ ثالث لجارتها يحمل الاسم نفسه — فالمفتاح
        // يحمل المنشأة، ونصٌّ متطابق عبر منشأتين مستودعان لا واحد.
        await ExecuteAsync(
            $"""
            insert into inventory.item_balance
                ("Id","TenantId","ItemId","WarehouseId","Quantity","ValueAmount","UnitCost","HasCostBasis","UpdatedAt")
            values
                (gen_random_uuid(), '{SeededTenant}',    'ITEM-1', 'WH-RIYADH', 100.000000, 1000.0000, 10.000000, true, now()),
                (gen_random_uuid(), '{SeededTenant}',    'ITEM-2', 'WH-RIYADH',  -3.500000,    0.0000,  0.000000, false, now()),
                (gen_random_uuid(), '{SeededTenant}',    'ITEM-1', 'WH-JEDDAH',  12.000000,  144.0000, 12.000000, true, now()),
                (gen_random_uuid(), '{NeighbourTenant}', 'ITEM-9', 'WH-RIYADH',   7.000000,   70.0000, 10.000000, true, now());

            insert into inventory.stock_movement
                ("Id","TenantId","SourceModule","DocumentType","DocumentId","TriggerCode","Generation","EventCode",
                 "ItemId","WarehouseId","ItemGroup","Direction","Quantity","ValueAmount","UnitCost","Method",
                 "DrewOnNegativeStock","QuantityAfter","ValueAfter","OccurredOn","RecordedAt","ActorId")
            values
                (gen_random_uuid(), '{SeededTenant}',    'Inventory', 'Seed', 'd1', 'OnApproval', 1, 'inventory.count_adjustment.posted',
                 'ITEM-1', 'WH-RIYADH', '*', 'IN',  100.000000, 1000.0000, 10.000000, 'moving_weighted_average', false, 100.000000, 1000.0000, date '2026-01-05', now(), 'seed'),
                (gen_random_uuid(), '{SeededTenant}',    'Inventory', 'Seed', 'd2', 'OnApproval', 1, 'inventory.count_adjustment.posted',
                 'ITEM-2', 'WH-RIYADH', '*', 'OUT',   3.500000,    0.0000,  0.000000, 'moving_weighted_average', true,   -3.500000,    0.0000, date '2026-01-06', now(), 'seed'),
                (gen_random_uuid(), '{SeededTenant}',    'Inventory', 'Seed', 'd3', 'OnApproval', 1, 'inventory.count_adjustment.posted',
                 'ITEM-1', 'WH-JEDDAH', '*', 'IN',   12.000000,  144.0000, 12.000000, 'moving_weighted_average', false,  12.000000,  144.0000, date '2026-01-07', now(), 'seed'),
                (gen_random_uuid(), '{SeededTenant}',    'Inventory', 'Seed', 'd4', 'OnApproval', 1, 'inventory.count_adjustment.posted',
                 'ITEM-1', 'WH-RIYADH', '*', 'IN',    1.000000,   10.0000, 10.000000, 'moving_weighted_average', false, 101.000000, 1010.0000, date '2026-01-08', now(), 'seed'),
                (gen_random_uuid(), '{NeighbourTenant}', 'Inventory', 'Seed', 'd5', 'OnApproval', 1, 'inventory.count_adjustment.posted',
                 'ITEM-9', 'WH-RIYADH', '*', 'IN',    7.000000,   70.0000, 10.000000, 'moving_weighted_average', false,   7.000000,   70.0000, date '2026-01-09', now(), 'seed');
            """,
            token);
    }

    /// <summary>
    /// مجموعا الكمّية والقيمة على الجدولين معاً — <b>ما يجب ألّا تمسّه هجرة</b>.
    /// </summary>
    private async Task<(decimal Quantity, decimal Value)> TotalsAsync(CancellationToken token)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(token);

        await using NpgsqlCommand command = new(
            """
            select coalesce((select sum("Quantity") from inventory.item_balance), 0)
                 + coalesce((select sum("Quantity") from inventory.stock_movement), 0),
                   coalesce((select sum("ValueAmount") from inventory.item_balance), 0)
                 + coalesce((select sum("ValueAmount") from inventory.stock_movement), 0)
            """,
            connection);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private async Task ExecuteAsync(string sql, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task<long> ScalarAsync(string sql, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(sql, connection);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private async Task<string> TextAsync(string sql, CancellationToken token, Guid? parameter = null)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(sql, connection);

        if (parameter is { } value)
        {
            command.Parameters.AddWithValue(value);
        }

        object? result = await command.ExecuteScalarAsync(token);
        return result as string ?? string.Empty;
    }

    private static string Number(decimal value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
}
