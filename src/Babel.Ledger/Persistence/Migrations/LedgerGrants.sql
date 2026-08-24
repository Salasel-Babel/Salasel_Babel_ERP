-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقة الأولى: الصلاحيات — الحصانة يفرضها PostgreSQL لا التطبيق
--
-- «القيد المُرحَّل لا يُعدَّل» جملة يكتبها كل نظام محاسبي وتفرضها طبقة التطبيق
-- عادةً. وطبقة التطبيق يُلتَفّ عليها: سكربت صيانة، أداة إدارة، بيانات اعتماد
-- مسرّبة، مطوّر متعجّل. السؤال العملي: **ما الذي يبقى صحيحاً حين يفشل كل ما سبق؟**
--
-- الجواب هنا: دور تطبيق **ليس superuser وليس مالكاً**، له INSERT و SELECT فقط
-- على جداول الدفتر، و UPDATE و DELETE **مسحوبتان صراحةً**. الرفض يأتي من
-- PostgreSQL بالرمز 42501 قبل أي منطق (ADR-0003).
--
-- اسم الدور يُقرأ من إعداد الجلسة babel.app_role كي لا يُثبَّت اسم بيئة في
-- ترحيل مخطّط. لا كلمات مرور هنا ولا في أي مكان في المستودع.
-- ═══════════════════════════════════════════════════════════════════════════
do $grants$
declare
    v_role text := coalesce(nullif(current_setting('babel.app_role', true), ''), 'babel_ledger_app');
begin
    if not exists (select 1 from pg_roles where rolname = v_role) then
        raise exception
            'APP_ROLE_MISSING role=% : دور التطبيق غير موجود — الحصانة بالصلاحيات لا تُركَّب بعد الحقيقة / the application role must exist before the ledger schema is granted',
            v_role;
    end if;

    -- الدور التطبيقي **لا يجوز** أن يكون superuser: superuser يتجاوز كل شيء
    -- دائماً، بما فيه RLS، فتصير كل الطبقة أدناه زينة (فخ-30 · ADR-0003).
    if exists (select 1 from pg_roles where rolname = v_role and rolsuper) then
        raise exception
            'APP_ROLE_IS_SUPERUSER role=% : دور التطبيق superuser — كل ضمانات الحصانة ساقطة / the application role is a superuser; every immutability guarantee is void',
            v_role;
    end if;

    execute format('revoke all on schema ledger from public');
    execute format('grant usage on schema ledger to %I', v_role);
    execute format('revoke all on all tables in schema ledger from %I', v_role);

    -- ── الدفتر: يُضاف إليه فقط ────────────────────────────────────────────
    execute format('grant select, insert on ledger.journal_entry, ledger.journal_line, ledger.chain_link to %I', v_role);
    execute format('revoke update, delete, truncate on ledger.journal_entry, ledger.journal_line, ledger.chain_link from %I', v_role);

    -- ── البيانات المرجعية: قراءة فقط. البذر بدور المالك. ─────────────────
    execute format('grant select on ledger.account, ledger.posting_role, ledger.role_account_map,
                                     ledger.fiscal_period, ledger.property_dimension to %I', v_role);
    execute format('revoke insert, update, delete, truncate on ledger.account, ledger.posting_role,
                                     ledger.role_account_map, ledger.property_dimension from %I', v_role);

    -- ── العدّاد والأرصدة قابلان للتحديث عمداً ────────────────────────────
    -- ليسا دفتراً: العدّاد يجب أن يتقدّم، والأرصدة يمكن إعادة اشتقاقها من
    -- السطور غير القابلة للتعديل في أي لحظة. لكن لا حذف ولا اقتطاع.
    execute format('grant select, insert, update on ledger.posting_counter, ledger.account_balance to %I', v_role);
    execute format('revoke delete, truncate on ledger.posting_counter, ledger.account_balance from %I', v_role);

    -- ── سجلّ العمليات: يُكتب فيه الرفض قبل إرجاعه للمستخدم ───────────────
    execute format('grant select, insert on ledger.process_event to %I', v_role);
    execute format('revoke update, delete, truncate on ledger.process_event from %I', v_role);
    execute format('grant usage, select on all sequences in schema ledger to %I', v_role);

    -- ── إغلاق الفترة يبقى بيد المالك ─────────────────────────────────────
    execute format('revoke insert, update, delete, truncate on ledger.fiscal_period from %I', v_role);

    -- ── الترحيل مكالمة خادم واحدة: تنفيذها مسموح، والصلاحيات داخلها
    --    تبقى صلاحيات المستدعي (SECURITY INVOKER) — أي أن الطبقة الأولى
    --    ليست معطَّلة داخل الإجراء، وهذا مقصود.
    execute format('grant execute on function ledger.post_entry to %I', v_role);
    execute format('grant execute on function ledger.assert_entry_balanced to %I', v_role);
    execute format('grant execute on function ledger.assert_line_allowed to %I', v_role);
end
$grants$;
