-- ═══════════════════════════════════════════════════════════════════════════
-- صلاحيات مخطّط النواة — والفرق عن صلاحيات الدفتر مقصود ومُعلَن.
--
-- الدفتر يُضاف إليه ولا يُعدَّل، فدور التطبيق هناك بلا UPDATE ولا DELETE. أمّا هذا
-- المخطّط فبياناتُ **إعدادٍ يحرّرها إنسان بصلاحية**: يُعاد تسمية مركز، ويُوقَف
-- آخر، ويُنقَل الافتراضي. فمنعُ UPDATE هنا يمنع الميزة نفسها لا يحمي شيئاً.
--
-- وما يبقى ممنوعاً هنا ممنوعٌ لسبب يُقرأ:
--
--   · DELETE على core.cost_center — **مسحوبة**. غياب الحذف ثابتةُ النوع نفسها:
--     لا دالّة حذف على CostCenterRegister أصلاً، والمركز الذي يخرج من الاستعمال
--     يُوقَف فيبقى تاريخه المُرحَّل مقروءاً ومُبوَّباً إلى الأبد (ADR-0002 · ADR-0006).
--     ولو ملك التطبيق الحذف لصار بوسعه أن يجعل سطور قيود مُرحَّلة تشير إلى مركز
--     لا وجود له — أي أن يُفسد تبويب تقريرٍ عن سنة مضت بجملة واحدة.
--
--   · DELETE على core.company_setup — **مسحوبة**. حذفُ التأسيس يُرجع المنشأة إلى
--     الحالة التي يعالجها هذا القرار كلّه: خادمٌ لا يعرف مقياس عرضها ولا مركزها.
--
--   · وثباتُ مقياس العرض لا يُترك للصلاحيات أصلاً: مشغّلٌ في CoreTriggers.sql
--     يرفض تغييره ولو كان الفاعل هو المالك نفسه.
--
--   · DELETE على core.name_translation — **ممنوحة**، وهذا هو الفرق الحقيقي عن
--     ledger.name_translation: هناك الترجمات بياناتٌ مرجعية يبذرها المالك، وهنا
--     اسمُ مركزٍ يكتبه المستخدم — وإعادةُ تسميةٍ تُسقط لغةً كانت مكتوبة. فبلا
--     DELETE تبقى الترجمة القديمة معلّقة على اسم جديد، وهو أسوأ من غيابها.
--
-- اسم الدور يُقرأ من إعداد الجلسة babel.core_app_role كي لا يُثبَّت اسم بيئة في
-- ترحيل مخطّط. لا كلمات مرور هنا ولا في أي مكان في المستودع.
-- ═══════════════════════════════════════════════════════════════════════════
do $grants$
declare
    v_role text := coalesce(nullif(current_setting('babel.core_app_role', true), ''), 'babel_core_app');
begin
    if not exists (select 1 from pg_roles where rolname = v_role) then
        raise exception
            'APP_ROLE_MISSING role=% : دور التطبيق غير موجود — الحصانة بالصلاحيات لا تُركَّب بعد الحقيقة / the application role must exist before the core schema is granted',
            v_role;
    end if;

    -- الدور التطبيقي **لا يجوز** أن يكون superuser: superuser يتجاوز كل شيء
    -- دائماً، فتصير كل الطبقة أدناه زينة (فخ-30 · ADR-0003).
    if exists (select 1 from pg_roles where rolname = v_role and rolsuper) then
        raise exception
            'APP_ROLE_IS_SUPERUSER role=% : دور التطبيق superuser — كل ضمانات الحصانة ساقطة / the application role is a superuser; every immutability guarantee is void',
            v_role;
    end if;

    execute format('revoke all on schema core from public');
    execute format('grant usage on schema core to %I', v_role);
    execute format('revoke all on all tables in schema core from %I', v_role);

    -- ── التأسيس: يُقرأ ويُنشأ ويُحدَّث (نقلُ الافتراضي)، ولا يُحذف ─────────
    execute format('grant select, insert, update on core.company_setup to %I', v_role);
    execute format('revoke delete, truncate on core.company_setup from %I', v_role);

    -- ── مراكز التكلفة: تُقرأ وتُضاف وتُعدَّل، ولا تُحذف أبداً ──────────────
    execute format('grant select, insert, update on core.cost_center to %I', v_role);
    execute format('revoke delete, truncate on core.cost_center from %I', v_role);

    -- ── الترجمات: بيانات مستخدم هنا، فحقّها كامل عدا الاقتطاع ─────────────
    execute format('grant select, insert, update, delete on core.name_translation to %I', v_role);
    execute format('revoke truncate on core.name_translation from %I', v_role);

    -- ── ملفّ القدرات: يُستبدل كلٌّ واحد، فالحذف جزء من الاستبدال ──────────
    -- الملفّ مورد واحد يُقرأ ويُستبدل (ADR-0023)، وقدرةٌ تُطفأ تختفي من الصفوف.
    execute format('grant select, insert, update, delete on core.capability_profile_document,
                                     core.capability_profile_capability,
                                     core.capability_profile_default to %I', v_role);
    execute format('revoke truncate on core.capability_profile_document,
                                     core.capability_profile_capability,
                                     core.capability_profile_default from %I', v_role);

    -- ── وجدول تاريخ الهجرات يُقرأ ولا يُكتب: الهجرة ملك المالك ────────────
    execute format('grant select on core."__EFMigrationsHistory" to %I', v_role);
    execute format('revoke insert, update, delete, truncate on core."__EFMigrationsHistory" from %I', v_role);
end
$grants$;
