-- ═══════════════════════════════════════════════════════════════════════════
-- ‏**الإلحاق مفروضٌ بالبناء لا بالنيّة** — سجلّ التدقيق وسجلّا القياس.
--
-- ولماذا مشغّل والصلاحيات موجودة أصلاً: الصلاحيات تحمي من **دور التطبيق**، وهو
-- الفاعل في مسار الطلب وحده. أمّا الصفّ فهو في قاعدة بيانات ولها أبوابٌ أخرى —
-- سكربت صيانة، أداة إدارة، تصحيحٌ يدوي بدور المالك في الثانية صباحاً. وسجلُّ
-- التدقيق في نظام محاسبي شاهدٌ على من فعل ماذا: قيدٌ كُتب لا يُمحى، **ولا يُمحى
-- بيد المالك أيضاً**. وهي حجّة مشغّل ثبات مقياس العرض نفسها لا نمطٌ ثانٍ
-- (‏CoreTriggers.sql · ADR-0002 · ADR-0003).
--
-- ‏**والاقتطاع مشمول.** ‏TRUNCATE لا يمرّ بمشغّل الصفوف إطلاقاً: يُفرَّغ الجدول
-- ولا يرى المشغّل صفّاً واحداً. فمشغّل الجملة على TRUNCATE ليس تشدّداً بل هو
-- الفرق بين «لا يُحذف صفّ» و«لا يُمحى الجدول».
--
-- ‏**وما يبقى للمالك:** المالك يملك DDL، فيستطيع إسقاط هذه المشغّلات في نافذة
-- صيانة معلنة. وذلك مقصود: الحصانة تجعل المحو **فعلاً يُقصَد ويُرى في تاريخ
-- المخطّط**، لا سطراً يمرّ في سكربت. ولا تدّعي هذه الوثيقة أكثر من ذلك.
-- ═══════════════════════════════════════════════════════════════════════════

create or replace function core.append_only_row()
returns trigger
language plpgsql
as $fn$
begin
    raise exception
        'APPEND_ONLY_VIOLATION table=%.% op=% : سجلٌّ يُلحَق ولا يُعدَّل ولا يُحذف / this table is append-only: no UPDATE and no DELETE',
        tg_table_schema, tg_table_name, tg_op;
end
$fn$;

create or replace function core.append_only_statement()
returns trigger
language plpgsql
as $fn$
begin
    raise exception
        'APPEND_ONLY_VIOLATION table=%.% op=% : سجلٌّ يُلحَق ولا يُقتطَع / this table is append-only: no TRUNCATE',
        tg_table_schema, tg_table_name, tg_op;
end
$fn$;

do $append_only$
declare
    v_table text;
begin
    foreach v_table in array array['audit_entry', 'module_usage', 'user_activity']
    loop
        execute format('drop trigger if exists trg_%s_append_only on core.%I', v_table, v_table);
        execute format(
            'create trigger trg_%s_append_only
                 before update or delete on core.%I
                 for each row execute function core.append_only_row()',
            v_table, v_table);

        execute format('drop trigger if exists trg_%s_no_truncate on core.%I', v_table, v_table);
        execute format(
            'create trigger trg_%s_no_truncate
                 before truncate on core.%I
                 for each statement execute function core.append_only_statement()',
            v_table, v_table);
    end loop;
end
$append_only$;
