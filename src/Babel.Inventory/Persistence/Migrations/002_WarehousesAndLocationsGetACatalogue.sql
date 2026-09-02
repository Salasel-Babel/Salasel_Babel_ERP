-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المخزون 002 — المستودع والموقع يصيران كياناً، والملء إفادةٌ لا اختراع
-- ═══════════════════════════════════════════════════════════════════════════
--
-- **ما يتغيّر:** أربعة جداول جديدة لا غير — كتالوج المستودعات، وكتالوج المواقع
-- داخلها، وترجمتا اسميهما **صفوفاً لا أعمدة** (‏ADR-0021 · القاعدة 14).
--
-- **ولا عمود واحد يُضاف إلى `stock_movement` ولا إلى `item_balance`، ولا صفّ
-- واحد فيهما يُقرأ لغير العدّ.** ما مضى واقعةٌ سُجّلت، وهذه الهجرة **تصف** ما
-- كُتب فيها ولا تمسّه.
--
-- **ولماذا لا مفتاح خارجي إلى الحركة ولا إلى الرصيد — وهو السطر الأهمّ هنا:**
-- عمودا `"WarehouseId"` و`"LocationId"` مُلئا شهوراً بنصوصٍ حرّة بلا كتالوج ولا
-- تحقّق. والمفتاح الخارجي يُصادق **الجدول كلّه** لحظة إنشائه، فخطأ إملائي واحد
-- في صفٍّ تاريخي يُحوّل الهجرة إلى إخفاق **لا شيء يُصلحه بـ`UPDATE`** على دفترٍ
-- يُضاف إليه فقط. فالوجود يُفرَض عند **إنشاء المسوّدة** في `StockDocumentService`،
-- لا في القاعدة ولا عند الترحيل. ولو أُريد القيد يوماً فبـ`NOT VALID` بلا
-- مصادقةٍ رجعية أبداً.
--
-- **والملء بالملاحظة، ولا اسم يُخترع:** يُدرَج صفُّ مستودعٍ لكل نصٍّ **مختلف**
-- ظهر فعلاً في `item_balance` أو `stock_movement`، وصفُّ موقعٍ لكل زوج
-- (مستودع، موقع) ظهر فيهما. والاسم العربي = الرمز نفسه، والمؤهّل نصٌّ فارغ،
-- و`"Origin" = 'OBSERVED'` يقول للشاشة إنّ هذا الاسم **صدى نصٍّ وُجد في
-- البيانات** لا اسمٌ كتبه إنسان. وما يكتبه إنسان يُولد بـ`'DECLARED'`.
-- وهي حركة الهجرة 001 نفسها حين كتبت `DEFAULT` و`EACH`: إفادةٌ عن واقعةٍ وقعت.
--
-- **و`DEFAULT` يصير موقعاً مُسجَّلاً في كل مستودع**، فما كتبته الهجرة 001 اصطلاحاً
-- يصير شيئاً موجوداً يُقرأ ويُعطَّل ويُسمّى.
--
-- والهجرة **معاملاتية**، ومكتوبة لتُعاد بلا أثر: كل إدراج مشروطٌ بغياب الصفّ.

create schema if not exists inventory;

-- ── ١ · الجداول ─────────────────────────────────────────────────────────────
-- تُنشأ بـ`if not exists` خارج أي كتلة شرطية: قاعدةٌ جديدة تماماً ينشئها
-- ‏EnsureCreated بالشكل نفسه، وقاعدةٌ قائمة تحصل عليها من هنا.

create table if not exists inventory.warehouse (
    "Id"        uuid                  not null primary key,
    "TenantId"  uuid                  not null,
    "Code"      character varying(64) not null,
    "NameAr"    character varying(256) not null,
    "Qualifier" character varying(64) not null default '',
    "Origin"    character varying(16) not null,
    "IsActive"  boolean               not null default true,
    "CreatedAt" timestamp with time zone not null
);

create unique index if not exists uq_inventory_warehouse_code
    on inventory.warehouse ("TenantId", "Code");

create table if not exists inventory.location (
    "Id"            uuid                  not null primary key,
    "TenantId"      uuid                  not null,
    "WarehouseCode" character varying(64) not null,
    "Code"          character varying(64) not null,
    "NameAr"        character varying(256) not null,
    "Origin"        character varying(16) not null,
    "IsActive"      boolean               not null default true,
    "CreatedAt"     timestamp with time zone not null
);

-- **المفتاح زوجٌ لا رمزٌ مفرد:** رمز موقعٍ واحد في مستودعين موقعان لا موقع،
-- وهو بالضبط ما يقوله مفتاح الرصيد الرباعي.
create unique index if not exists uq_inventory_location
    on inventory.location ("TenantId", "WarehouseCode", "Code");

create table if not exists inventory.warehouse_name_translation (
    "Id"            uuid                   not null primary key,
    "TenantId"      uuid                   not null,
    "WarehouseCode" character varying(64)  not null,
    "LanguageTag"   character varying(35)  not null,
    "Text"          character varying(256) not null
);

create unique index if not exists uq_inventory_warehouse_name_translation
    on inventory.warehouse_name_translation ("TenantId", "WarehouseCode", "LanguageTag");

create table if not exists inventory.location_name_translation (
    "Id"            uuid                   not null primary key,
    "TenantId"      uuid                   not null,
    "WarehouseCode" character varying(64)  not null,
    "LocationCode"  character varying(64)  not null,
    "LanguageTag"   character varying(35)  not null,
    "Text"          character varying(256) not null
);

create unique index if not exists uq_inventory_location_name_translation
    on inventory.location_name_translation
       ("TenantId", "WarehouseCode", "LocationCode", "LanguageTag");

-- ── ٢ · الملء بالملاحظة ─────────────────────────────────────────────────────
do $migration$
begin
    -- قاعدة لم يُنشأ فيها دفتر المخزون بعد ليست حالة خطأ: لا واقعة تُوصَف.
    if to_regclass('inventory.item_balance') is null
        or to_regclass('inventory.stock_movement') is null then
        return;
    end if;

    insert into inventory.warehouse
        ("Id", "TenantId", "Code", "NameAr", "Qualifier", "Origin", "IsActive", "CreatedAt")
    select
        gen_random_uuid(), observed."TenantId", observed."WarehouseId", observed."WarehouseId",
        '', 'OBSERVED', true, now()
      from (
            select distinct "TenantId", "WarehouseId" from inventory.item_balance
            union
            select distinct "TenantId", "WarehouseId" from inventory.stock_movement
           ) as observed
     where observed."WarehouseId" <> ''
       and not exists (
            select 1
              from inventory.warehouse existing
             where existing."TenantId" = observed."TenantId"
               and existing."Code"     = observed."WarehouseId");

    insert into inventory.location
        ("Id", "TenantId", "WarehouseCode", "Code", "NameAr", "Origin", "IsActive", "CreatedAt")
    select
        gen_random_uuid(), observed."TenantId", observed."WarehouseId", observed."LocationId",
        observed."LocationId", 'OBSERVED', true, now()
      from (
            select distinct "TenantId", "WarehouseId", "LocationId" from inventory.item_balance
            union
            select distinct "TenantId", "WarehouseId", "LocationId" from inventory.stock_movement
           ) as observed
     where observed."WarehouseId" <> ''
       and observed."LocationId"  <> ''
       and not exists (
            select 1
              from inventory.location existing
             where existing."TenantId"      = observed."TenantId"
               and existing."WarehouseCode" = observed."WarehouseId"
               and existing."Code"          = observed."LocationId");
end
$migration$;
