-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المخزون 002 — سجلّ التسكين يولد **واصفاً** لا مُبطِلاً، ومستند نقلٍ معه
-- ═══════════════════════════════════════════════════════════════════════════
--
-- **ما يُضاف:**
--   ١ · `inventory.storage_place` — هرم التسكين الثلاثي في جدولٍ واحد بمستوىً صريح:
--       مستودع ← موقع ← رفّ.
--   ٢ · `inventory.storage_place_name_translation` — الترجمة صفٌّ لا عمود.
--   ٣ · `inventory.stock_transfer` — مستند نقلٍ بين موقعين.
--
-- **وما لا يُضاف — وهو الأهمّ:**
--
--   · **لا مفتاح خارجي من الحركة ولا من الرصيد إلى هذا السجلّ**، ولا قيد تحقّق
--     يشترط أن يكون رمز المستودع أو الموقع مسجَّلاً فيه. والسبب أن الحركات القائمة
--     تحمل `DEFAULT` وما شابهه، كُتبت قبل أن يوجد سجلّ أصلاً (هجرة 001). ومفتاحٌ
--     خارجيّ اليوم كان يعني أحد أمرين: إمّا أن تُبذر صفوفٌ تدّعي أن أحداً سجّل
--     `DEFAULT` وهو لم يفعل، أو أن تُعاد كتابة كل حركة مضت لتوافق سجلّاً وُلد بعدها
--     — وهي بالضبط إعادة كتابة الواقعة التي يمنعها ADR-0002 على الدفتر المساعد.
--
--   · **ولا صفّ يُبذر.** السجلّ يولد فارغاً، ورمزٌ غير مسجَّل يبقى عاملاً ويُوسَم
--     عند القراءة بأنه غير مسجَّل. والوسم إفادةٌ صادقة؛ والبذرة ادّعاءُ تسجيلٍ لم يقع.
--
-- **ولا كمّية تُعاد كتابتها، ولا قيمة تُعاد حسابها، ولا مفتاح رصيدٍ يتّسع.** الرفّ
-- **لا يدخل مفتاح الرصيد**: توسيعُ المفتاح ببُعدٍ خامس يقتضي إعادة توزيع كل رصيد
-- قائم على أرففٍ لا يعرفها أحد — وهو الثمن الذي دُفع مرّةً في هجرة 001 للموقع، ولا
-- يُدفع ثانيةً لبُعدٍ لا يحمل معنىً محاسبياً (‏ADR تسكين المخزون).
--
-- والهجرة مكتوبة لتُعاد بلا أثر: كل شيء فيها `if not exists`.

create schema if not exists inventory;

-- ── ١ · هرم التسكين ─────────────────────────────────────────────────────────
-- جدولٌ واحد بمستوىً صريح، لا ثلاثة جداول بخمسة أعمدة مكرّرة ثلاث مرّات.
-- والمستوى **في المفتاح الفريد**: رمز «A1» يجوز أن يكون مستودعاً ورفّاً معاً.
create table if not exists inventory.storage_place (
    "Id"         uuid                     not null primary key,
    "TenantId"   uuid                     not null,
    "Level"      character varying(16)    not null,
    "Code"       character varying(64)    not null,
    "ParentCode" character varying(64)    not null,
    "NameAr"     character varying(256)   not null,
    "IsActive"   boolean                  not null,
    "CreatedAt"  timestamp with time zone not null,
    constraint ck_inventory_storage_place_level
        check ("Level" in ('WAREHOUSE','LOCATION','BIN')),
    -- المستودع بلا أب، وما دونه بأب. صفٌّ يخالف ذلك هرمٌ مكسور لا يُقرأ.
    constraint ck_inventory_storage_place_parent
        check (("Level" = 'WAREHOUSE') = (btrim("ParentCode") = ''))
);

create unique index if not exists uq_inventory_storage_place
    on inventory.storage_place ("TenantId", "Level", "Code");

create index if not exists ix_inventory_storage_place_parent
    on inventory.storage_place ("TenantId", "Level", "ParentCode");

-- ── ٢ · الترجمة صفٌّ لا عمود (‏ADR-0021 · القاعدة 14) ───────────────────────
create table if not exists inventory.storage_place_name_translation (
    "Id"       uuid                   not null primary key,
    "TenantId" uuid                   not null,
    "Level"    character varying(16)  not null,
    "Code"     character varying(64)  not null,
    "Locale"   character varying(16)  not null,
    "Text"     character varying(256) not null
);

create unique index if not exists uq_inventory_storage_place_name_translation
    on inventory.storage_place_name_translation ("TenantId", "Level", "Code", "Locale");

-- ── ٣ · مستند النقل بين موقعين ──────────────────────────────────────────────
-- ولا عمود `PostedEntryId` فيه ولا `PostingGeneration`: **هذا المستند لا يُرحَّل**.
-- عمودٌ لمعرّف قيدٍ لا يُكتب أبداً يجعل كل قارئ يسأل متى يُملأ.
create table if not exists inventory.stock_transfer (
    "Id"                 uuid                     not null primary key,
    "TenantId"           uuid                     not null,
    "Number"             character varying(64)    not null,
    "ItemCode"           character varying(64)    not null,
    "ItemGroup"          character varying(64)    not null,
    "FromWarehouseId"    character varying(64)    not null,
    "FromLocationId"     character varying(64)    not null,
    "ToWarehouseId"      character varying(64)    not null,
    "ToLocationId"       character varying(64)    not null,
    "Magnitude"          numeric(19,6)            not null,
    "UnitCode"           character varying(32)    not null,
    "ValueAmount"        numeric(19,4)            not null,
    "OccurredOn"         date                     not null,
    "State"              character varying(16)    not null,
    "MovementGeneration" integer                  not null,
    "CreatedAt"          timestamp with time zone not null,
    constraint ck_inventory_stock_transfer_magnitude_positive
        check ("Magnitude" > 0),
    -- نقلٌ إلى الموضع نفسه ليس نقلاً: حركتان تُلغيان بعضهما وتُحدّثان صفّ رصيدٍ
    -- واحد مرّتين في معاملة واحدة.
    constraint ck_inventory_stock_transfer_distinct
        check (("FromWarehouseId", "FromLocationId") <> ("ToWarehouseId", "ToLocationId"))
);

create unique index if not exists uq_inventory_stock_transfer_number
    on inventory.stock_transfer ("TenantId", "Number");
