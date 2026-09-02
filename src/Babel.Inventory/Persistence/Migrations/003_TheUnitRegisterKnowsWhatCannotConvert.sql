-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المخزون 003 — سجلّ الوحدات يعرف **ما لا يُحوَّل**
-- ═══════════════════════════════════════════════════════════════════════════
--
-- **ما يُضاف:**
--   ١ · `inventory.unit_of_measure` — وحدة القياس ككيان أوّل: رمزها واسمها
--       و**صنف كمّيتها**.
--   ٢ · `inventory.unit_of_measure_name_translation` — الترجمة صفٌّ لا عمود.
--   ٣ · `inventory.unit_conversion` — معامل تحويل بين وحدتين **على مستوى المنشأة**.
--
-- **ولماذا صنف الكمّية هو البند كلّه:** معامل التحويل بين وحدتين من صنفٍ واحد
-- **واقعةٌ فيزيائية** — الكيلوغرام ألف غرام دائماً. وبين صنفين مختلفين **ليس معاملاً
-- بل كثافة**: «كم كيلوغراماً في اللتر؟» جوابه يختلف بين الماء والزيت والرصاص، ويختلف
-- للمادّة الواحدة بالحرارة. فبلا هذا العمود لا يملك النظام ما يفرّق به بين الاثنين،
-- ويصير «كيلوغرام ← متر» معاملاً يكتبه أحدهم بحسن نيّة ولا يعترض عليه شيء.
--
-- **وما لا يُضاف:**
--   · **لا مفتاح خارجي** من `stock_movement` ولا من `item_unit` إلى هذا السجلّ، ولا
--     قيد تحقّق يشترط أن تكون الوحدة مسجَّلة. والسبب هو سبب سجلّ التسكين حرفاً بحرف:
--     كل حركة سبقت هذا الفرع تحمل `EACH` أو ما كتبه المستدعي، وإلزامُ التسجيل بأثر
--     رجعي إمّا يبذر صفوفاً تدّعي تسجيلاً لم يقع، أو يُعيد كتابة واقعةٍ مضت.
--   · **ولا صفّ يُبذر.** ولا حتى `EACH`: بذرُها يعني أن النظام يُقرّر أنها «عدد»
--     نيابةً عن المستأجر، وهي وحدةٌ قد يكون استعملها لغير ذلك.
--
-- **ولا كمّية تُعاد كتابتها، ولا معامل قائم يُمَسّ.** معاملات `item_unit` تبقى كما هي:
-- هي **خاصّية تعبئةٍ لصنف**، وهذا الجدول **واقعةٌ فيزيائية للمنشأة**، ولا يُدمَجان.
--
-- والهجرة مكتوبة لتُعاد بلا أثر.

create schema if not exists inventory;

-- ── ١ · وحدة القياس ككيان أوّل ──────────────────────────────────────────────
create table if not exists inventory.unit_of_measure (
    "Id"        uuid                     not null primary key,
    "TenantId"  uuid                     not null,
    "Code"      character varying(32)    not null,
    "NameAr"    character varying(256)   not null,
    "Class"     character varying(16)    not null,
    "IsActive"  boolean                  not null,
    "CreatedAt" timestamp with time zone not null,
    -- صنف الكمّية **مغلق**: قيمةٌ حرّة تجعل صنفين مكتوبين بحرفين مختلفين يبدوان
    -- مختلفين وهما واحد، فيُرفض تحويلٌ مشروع أو يُقبل تحويلٌ مستحيل.
    constraint ck_inventory_unit_class
        check ("Class" in ('COUNT','WEIGHT','VOLUME','LENGTH','AREA'))
);

create unique index if not exists uq_inventory_unit_of_measure
    on inventory.unit_of_measure ("TenantId", "Code");

-- ── ٢ · الترجمة صفٌّ لا عمود (‏ADR-0021 · القاعدة 14) ──────────────────────
create table if not exists inventory.unit_of_measure_name_translation (
    "Id"       uuid                   not null primary key,
    "TenantId" uuid                   not null,
    "UnitCode" character varying(32)  not null,
    "Locale"   character varying(16)  not null,
    "Text"     character varying(256) not null
);

create unique index if not exists uq_inventory_unit_of_measure_name_translation
    on inventory.unit_of_measure_name_translation ("TenantId", "UnitCode", "Locale");

-- ── ٣ · معامل التحويل على مستوى المنشأة ────────────────────────────────────
-- بسطٌ ومقام صحيحان لا عددٌ عشري: «الحبّة ثلث علبة» لا يُكتب عشرياً بلا خسارة،
-- والخسارة في كمّيةٍ تُضرب في تكلفة الوحدة تصل إلى المال.
create table if not exists inventory.unit_conversion (
    "Id"          uuid                     not null primary key,
    "TenantId"    uuid                     not null,
    "FromUnit"    character varying(32)    not null,
    "ToUnit"      character varying(32)    not null,
    "Numerator"   bigint                   not null,
    "Denominator" bigint                   not null,
    "CreatedAt"   timestamp with time zone not null,
    constraint ck_inventory_unit_conversion_ratio_positive
        check ("Numerator" > 0 and "Denominator" > 0),
    -- وحدةٌ إلى نفسها معاملُها واحدٌ على واحد بالتعريف؛ وصفٌّ يقول غير ذلك تعريفان
    -- متناقضان لشيء واحد.
    constraint ck_inventory_unit_conversion_distinct
        check ("FromUnit" <> "ToUnit")
);

create unique index if not exists uq_inventory_unit_conversion
    on inventory.unit_conversion ("TenantId", "FromUnit", "ToUnit");
