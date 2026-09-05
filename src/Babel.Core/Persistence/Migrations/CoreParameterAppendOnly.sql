-- ═══════════════════════════════════════════════════════════════════════════
-- ‏**إصدارُ المعامِلات يُضاف ولا يُعدَّل — مفروضاً بالبناء لا بالنيّة.**
--
-- وهذا هو الفرق الحاكم بين هذه الجداول وبقيّة مخطّط النواة. ما فوقها بياناتُ
-- إعدادٍ يحرّرها إنسان بصلاحية — يُعاد تسمية مركز، ويُنقَل الافتراضي — أمّا هذه
-- فـ**شهادةٌ على ما كان سارياً**: مسيّرٌ رُحِّل بنسبة، وفاتورةٌ رُحِّلت بنسبة،
-- ومراجعٌ يسأل بعد سنتين «بأي رقمٍ حُسب هذا؟». وصفٌّ يُعدَّل يجعل الجواب رواية.
--
-- ‏**ولماذا مشغّل والصلاحيات موجودة أصلاً:** الصلاحيات تحمي من دور التطبيق وحده،
-- وهو الفاعل في مسار الطلب. والصفّ في قاعدة بيانات ولها أبوابٌ أخرى — سكربت
-- صيانة، تصحيحٌ يدوي بدور المالك. وتغييرُ نسبةٍ في صفٍّ قديم **يُعيد كتابة الماضي
-- في كل مستندٍ لم يحمل لقطته** — وهو بالضبط ما وُجدت هذه الخدمة لمنعه.
--
-- ‏**والاقتطاع مشمول**: ‏TRUNCATE لا يمرّ بمشغّل الصفوف إطلاقاً.
--
-- والدالّتان مكتوبتان في `CoreAppendOnlyTriggers.sql`، ولا تُكرَّران هنا: هجرةُ
-- سجلّ التدقيق تسبق هذه في الترتيب، فهما موجودتان حين يُنفَّذ هذا النصّ.
-- (‏ADR-0002 · ADR-0003)
-- ═══════════════════════════════════════════════════════════════════════════

do $parameters_append_only$
declare
    v_table text;
begin
    foreach v_table in array array['parameter_version', 'parameter_value', 'parameter_usage']
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
$parameters_append_only$;
