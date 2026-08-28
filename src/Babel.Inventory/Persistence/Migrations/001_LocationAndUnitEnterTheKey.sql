-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المخزون 001 — الموقع يدخل المفتاح، والوحدة تدخل الكمّية
-- ═══════════════════════════════════════════════════════════════════════════
--
-- **ما يتغيّر:**
--   ١ · مفتاح الرصيد صار أربعة أبعاد: (منشأة · صنف · مستودع · **موقع**).
--   ٢ · كل كمّية صارت تحمل وحدتها: وحدةُ أساس الرصيد، والوحدة التي سُلّمت بها.
--   ٣ · جداول جديدة: كتالوج الأصناف، ومعاملات وحداته، ومستندات حركة المخزون،
--       وسجلّ محاولات ترحيلها.
--
-- **لماذا الآن ولماذا لا يُؤجَّل:** إضافة بُعدٍ إلى مفتاح رصيدٍ **بعد** أن تُكتب
-- عليه حركات هجرةٌ تُعيد توزيع كل رصيد على مواقع لا يعرفها أحد — أي أنها تُعيد
-- كتابة واقعةٍ مضت، وهو ما يمنعه ADR-0002 على الدفتر المساعد بالضبط. والبُعد يدخل
-- اليوم **بقيمة واحدة** فلا يتغيّر رقم واحد، ويتفرّع غداً بمستندات تسكين جديدة.
--
-- **واصطلاح الملء صريح، وكلٌّ منه إفادةٌ عن واقعة وقعت لا تخمين:**
--   · الموقع القائم = `DEFAULT` — لأن المستودع كان كلّه موقعاً واحداً فعلاً.
--   · الوحدة القائمة = `EACH` — لأن كل مسار في المنتج قبل هذه الهجرة كان يُسلّم
--     عدداً مجرّداً يُعامَل بالعدّ. ولو تُركت فارغة لصار الرصيد يُقرأ «بوحدة مجهولة»،
--     وهي حالة لا تُصلَح لاحقاً لأن أحداً لن يعرف ما كانت.
--
-- **ولا كمّية واحدة تُعاد كتابتها، ولا قيمة تُعاد حسابها.** ما يُضاف وصفٌ لواقعة،
-- لا تعديلٌ لها.
--
-- **والفهرس الفريد يُستبدل بترتيب لا يترك نافذة بلا حارس:** يُنشأ الرباعي أوّلاً
-- ثم يُسقَط الثلاثي. والعكس كان سيفتح لحظةً يُقبل فيها صفّان لمفتاح واحد.
--
-- والهجرة **معاملاتية** ومكتوبة لتُعاد بلا أثر.

do $migration$
declare
    v_conflicts bigint;
begin
    -- قاعدة لم يُنشأ فيها المخطّط بعد ليست حالة خطأ: EnsureCreated ينشئ الشكل
    -- الجديد كاملاً، ولا شيء هنا ليُرقّى.
    if to_regclass('inventory.item_balance') is null then
        return;
    end if;

    -- ── ١ · أعمدة الرصيد ────────────────────────────────────────────────────
    alter table inventory.item_balance
        add column if not exists "LocationId" character varying(64) not null default '',
        add column if not exists "BaseUnit"   character varying(32) not null default '';

    update inventory.item_balance set "LocationId" = 'DEFAULT' where "LocationId" = '';
    update inventory.item_balance set "BaseUnit"   = 'EACH'    where "BaseUnit"   = '';

    -- ── ٢ · أعمدة الحركة ────────────────────────────────────────────────────
    alter table inventory.stock_movement
        add column if not exists "LocationId"       character varying(64) not null default '',
        add column if not exists "BaseUnit"         character varying(32) not null default '',
        add column if not exists "EnteredUnit"      character varying(32) not null default '',
        add column if not exists "EnteredMagnitude" numeric(19,6)         not null default 0;

    update inventory.stock_movement set "LocationId"  = 'DEFAULT' where "LocationId"  = '';
    update inventory.stock_movement set "BaseUnit"    = 'EACH'    where "BaseUnit"    = '';
    update inventory.stock_movement set "EnteredUnit" = 'EACH'    where "EnteredUnit" = '';

    -- المقدار المُسلَّم يساوي المقدار المُسجَّل ما دامت الوحدتان واحدة — وهي كذلك
    -- على كل صفّ سبق هذه الهجرة. نسخٌ لا تخمين.
    update inventory.stock_movement set "EnteredMagnitude" = "Quantity" where "EnteredMagnitude" = 0;

    -- ── ٣ · المفتاح الفريد للرصيد: الرباعي أوّلاً ثم إسقاط الثلاثي ──────────
    -- ولا يُركَّب فهرس فريد على بيانات تخرقه: يُسمّى العدد ويتوقّف كل شيء.
    select count(*) into v_conflicts
      from (
        select 1
          from inventory.item_balance
         group by "TenantId", "ItemId", "WarehouseId", "LocationId"
        having count(*) > 1) as duplicated;

    if v_conflicts > 0 then
        raise exception
            'DUPLICATE_ITEM_BALANCE_KEY rows=% : يوجد % مفتاح رصيد مكرّر بعد إضافة الموقع — لا يجوز تركيب فهرس فريد فوقه، والعلاج دمج الصفوف بمستند لا بحذفها / % duplicate item-balance keys exist after adding the location dimension; a unique index must not be forced over them',
            v_conflicts, v_conflicts, v_conflicts
            using errcode = 'unique_violation';
    end if;

    create unique index if not exists uq_inventory_item_balance_v2
        on inventory.item_balance ("TenantId", "ItemId", "WarehouseId", "LocationId");

    drop index if exists inventory.uq_inventory_item_balance;

    alter index inventory.uq_inventory_item_balance_v2
        rename to uq_inventory_item_balance;

    -- ── ٤ · فهرس الحركة يتّسع بالموقع ───────────────────────────────────────
    drop index if exists inventory.ix_inventory_movement_item;
    create index if not exists ix_inventory_movement_item
        on inventory.stock_movement ("TenantId", "ItemId", "WarehouseId", "LocationId");
end
$migration$;

-- ── ٥ · الجداول الجديدة ─────────────────────────────────────────────────────
-- تُنشأ بـ`if not exists` خارج الكتلة أعلاه: قاعدةٌ جديدة تماماً ينشئها
-- ‏EnsureCreated بالشكل نفسه، وقاعدةٌ قائمة تحصل عليها من هنا.

create schema if not exists inventory;

create table if not exists inventory.item (
    "Id"         uuid                  not null primary key,
    "TenantId"   uuid                  not null,
    "Code"       character varying(64) not null,
    "NameAr"     character varying(256) not null,
    "ItemGroup"  character varying(64) not null,
    "BaseUnit"   character varying(32) not null,
    "CreatedAt"  timestamp with time zone not null
);

create unique index if not exists uq_inventory_item_code
    on inventory.item ("TenantId", "Code");

-- الترجمة **صفٌّ لا عمود** (‏ADR-0021 · القاعدة 14): اللغة الثالثة تدخل بصفٍّ لا
-- بهجرة مخطّط، و«لا ترجمة» تُقرأ من غياب الصفّ لا من نصٍّ فارغ في عمود.
create table if not exists inventory.item_name_translation (
    "Id"       uuid                   not null primary key,
    "TenantId" uuid                   not null,
    "ItemCode" character varying(64)  not null,
    "Locale"   character varying(16)  not null,
    "Text"     character varying(256) not null
);

create unique index if not exists uq_inventory_item_name_translation
    on inventory.item_name_translation ("TenantId", "ItemCode", "Locale");

create table if not exists inventory.item_unit (
    "Id"          uuid                  not null primary key,
    "TenantId"    uuid                  not null,
    "ItemCode"    character varying(64) not null,
    "UnitCode"    character varying(32) not null,
    "Numerator"   bigint                not null,
    "Denominator" bigint                not null,
    constraint ck_inventory_item_unit_ratio_positive check ("Numerator" > 0 and "Denominator" > 0)
);

create unique index if not exists uq_inventory_item_unit
    on inventory.item_unit ("TenantId", "ItemCode", "UnitCode");

create table if not exists inventory.stock_document (
    "Id"                uuid                  not null primary key,
    "TenantId"          uuid                  not null,
    "Number"            character varying(64) not null,
    "Direction"         character varying(8)  not null,
    "ItemCode"          character varying(64) not null,
    "WarehouseId"       character varying(64) not null,
    "LocationId"        character varying(64) not null,
    "ItemGroup"         character varying(64) not null,
    "Magnitude"         numeric(19,6)         not null,
    "UnitCode"          character varying(32) not null,
    "CostAmount"        numeric(19,4)         not null,
    "OccurredOn"        date                  not null,
    "State"             character varying(16) not null,
    "PostedEntryId"     uuid                  null,
    "PostingGeneration" integer               not null,
    "CreatedAt"         timestamp with time zone not null,
    constraint ck_inventory_stock_document_magnitude_positive check ("Magnitude" > 0)
);

create unique index if not exists uq_inventory_stock_document_number
    on inventory.stock_document ("TenantId", "Number");

create table if not exists inventory.document_posting (
    "Id"                uuid                   not null primary key,
    "TenantId"          uuid                   not null,
    "DocumentType"      character varying(64)  not null,
    "DocumentId"        character varying(64)  not null,
    "TriggerCode"       character varying(32)  not null,
    "Generation"        integer                not null,
    "EventCode"         character varying(128) not null,
    "IdempotencyKey"    character varying(128) not null,
    "PartyId"           character varying(64)  not null,
    "DocumentDate"      date                   not null,
    "State"             character varying(16)  not null,
    "EntryId"           uuid                   null,
    "EntryNumber"       bigint                 not null,
    "AttemptCount"      integer                not null,
    "LastAttemptAt"     timestamp with time zone not null,
    "FailureCode"       character varying(128)  not null,
    "FailureMessageAr"  character varying(1000) not null,
    "FailureMessageEn"  character varying(1000) not null
);

create unique index if not exists uq_inventory_document_posting_identity
    on inventory.document_posting
       ("TenantId", "DocumentType", "DocumentId", "TriggerCode", "Generation", "EventCode");
