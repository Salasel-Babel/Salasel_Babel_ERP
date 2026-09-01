-- ═══════════════════════════════════════════════════════════════════════════
-- كل سجلٍّ يحمل مفتاحَي بحثه وفهرسيهما — والوحدة المالكة هي من تربطهما
--
-- ‏**لماذا دالّة لا نصّ لكل جدول:** جداول الأطراف تعيش في وحداتٍ خمس، و«كل وحدة تملك
-- جداولها» (القاعدة 5). فلو كتب هذا الملفّ `alter table sales.customer` لصارت وحدة
-- الذكاء تعرف جدول المبيعات بالاسم. هنا **أداةٌ لا قرار**: الوحدة المالكة تنادي
-- `babel.attach_name_register('sales','customer','NameAr','{TenantId}')` في ناشر
-- مخطّطها هي، ولا يظهر في هذا المشروع اسم وحدةٍ ولا اسم جدول.
--
-- ‏**ونطاق العزل عمودٌ مُمرَّر لا عمودٌ مفترض.** المقيس على هذه الشجرة: `sales.customer`
-- و`purchasing.supplier` و`hr.employee` و`inventory.item` تحمل `TenantId` **ولا تحمل
-- `CompanyId` إطلاقاً** — بينما `realestate.*` و`ledger.*` تحملان الاثنين. فتوقيعٌ
-- يفترض `company_id` كان سيسقط عند أول جدولٍ من الأربعة، ودالّةٌ تتخطّى العمود الغائب
-- بصمت كانت ستبني فهرساً بلا نطاق. القائمة تُمرَّر، وكل اسمٍ فيها يُتحقَّق من وجوده.
--
-- ‏**والعمود مولَّد مخزَّن، فإضافته على جدولٍ كبير تُعيد كتابته.** ذلك ثمنٌ يُدفع مرّة
-- عند النشر، ويُذكر هنا كي لا يُكتشف على قاعدة إنتاج.
--
-- والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════

create or replace function babel.require_column(target regclass, column_name text)
returns void
language plpgsql
as $require_column$
begin
    if not exists (
        select 1 from pg_attribute
         where attrelid = target and attname = column_name and attnum > 0 and not attisdropped)
    then
        raise exception
            'العمود «%» غير موجود في «%»، فلا يُبنى عليه سجلّ أسماء. '
            '/ column "%" does not exist on "%".',
            column_name, target::text, column_name, target::text
            using errcode = 'undefined_column';
    end if;
end
$require_column$;

create or replace function babel.attach_name_register(
    register_schema text,
    register_table  text,
    name_column     text,
    scope_columns   text[])
returns void
language plpgsql
as $attach$
declare
    qualified      regclass;
    search_index   text;
    tight_index    text;
    scope_column   text;
    scope_list     text;
begin
    if scope_columns is null or array_length(scope_columns, 1) is null then
        raise exception
            'سجلّ أسماء بلا عمود نطاق واحد. جدولٌ يُطابَق بلا نطاق يُعيد صفوف منشأةٍ أخرى — '
            'ولا يُبنى فهرسٌ على ذلك. / a name register with no scope column would match across tenants.'
            using errcode = 'invalid_parameter_value';
    end if;

    qualified := to_regclass(format('%I.%I', register_schema, register_table));
    if qualified is null then
        raise exception
            'الجدول «%.%» غير موجود، فلا يُربَط به سجلّ أسماء. '
            '/ table "%.%" does not exist.',
            register_schema, register_table, register_schema, register_table
            using errcode = 'undefined_table';
    end if;

    -- الاسم أولاً، ثم كل عمود نطاق: عمودٌ غائب يُرفَع بصوته ولا يُتخطّى.
    perform babel.require_column(qualified, name_column);
    foreach scope_column in array scope_columns loop
        perform babel.require_column(qualified, scope_column);
    end loop;

    perform babel.require_extension(
        'pg_trgm',
        'فهرس التشابه على ' || register_schema || '.' || register_table);

    search_index := format('ix_%s_%s_search', register_schema, register_table);
    tight_index  := format('ix_%s_%s_search_tight', register_schema, register_table);

    -- ‏**حدّ المعرّف يُفحص ولا يُترك للقصّ الصامت.** ‏PostgreSQL يقصّ عند 63 بايتاً بلا
    -- كلمة، واسمان يُقصّان إلى نصٍّ واحد هما فهرسٌ واحد — نفس درس `TestRunScope`.
    if octet_length(search_index) > 63 or octet_length(tight_index) > 63 then
        raise exception
            'اسم الفهرس «%» أو «%» يتجاوز حدّ المعرّف (‏63 بايتاً) فيُقصّ بصمت. '
            'قصّر اسم المخطّط أو الجدول. / index name exceeds the 63-byte identifier limit.',
            search_index, tight_index
            using errcode = 'name_too_long';
    end if;

    if not exists (
        select 1 from pg_attribute
         where attrelid = qualified and attname = 'search_key' and attnum > 0 and not attisdropped)
    then
        execute format(
            'alter table %s add column search_key text generated always as (babel.fold_arabic(%I)) stored',
            qualified, name_column);
    end if;

    if not exists (
        select 1 from pg_attribute
         where attrelid = qualified and attname = 'search_key_tight' and attnum > 0 and not attisdropped)
    then
        execute format(
            'alter table %s add column search_key_tight text generated always as (babel.fold_arabic_tight(%I)) stored',
            qualified, name_column);
    end if;

    execute format(
        'create index if not exists %I on %s using gin (search_key gin_trgm_ops)',
        search_index, qualified);

    select string_agg(format('%I', column_name), ', ' order by ordinality)
      into scope_list
      from unnest(scope_columns) with ordinality as columns(column_name, ordinality);

    execute format(
        'create index if not exists %I on %s (%s, search_key_tight)',
        tight_index, qualified, scope_list);

    -- ‏**والفهرس يُقرأ بعد إنشائه.** نشرٌ يمضي بلا فهرس أسوأ من نشرٍ يقف: المطابقة
    -- تبقى صحيحة وتصير مسحاً كاملاً، فيظهر العطل بطئاً تحت الحمل لا خطأً عند النشر.
    if to_regclass(format('%I.%I', register_schema, search_index)) is null
       or to_regclass(format('%I.%I', register_schema, tight_index)) is null
    then
        raise exception
            'فهرسا سجلّ الأسماء على «%.%» لم يوجدا بعد إنشائهما — لا يُتابَع النشر. '
            '/ the name-register indexes on "%.%" are absent after creation.',
            register_schema, register_table, register_schema, register_table
            using errcode = 'internal_error';
    end if;
end
$attach$;

comment on function babel.attach_name_register(text, text, text, text[]) is
    'يربط بجدولٍ مملوكٍ لوحدةٍ أخرى عمودَي بحث مولَّدين وفهرسيهما. أداةٌ تناديها الوحدة '
    'المالكة في ناشر مخطّطها — ولا تسمّي وحدةُ الذكاء جدولاً.';
