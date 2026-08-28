-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقة الأولى للمرفقات: الصلاحيات — الحصانة يفرضها PostgreSQL لا التطبيق
--
-- المرفق سند إثبات لقيد، فيأخذ انضباط الدفتر نفسه: **يُضاف إليه ولا يُعدَّل**.
-- «الشيفرة لا تُحدِّث صفّ مرفق» جملة صحيحة اليوم ويُلتَفّ عليها غداً بسكربت صيانة
-- أو أداة إدارة أو بيانات اعتماد مسرّبة. السؤال العملي هو نفسه سؤال LedgerGrants.sql:
-- **ما الذي يبقى صحيحاً حين يفشل كل ما سبق؟**
--
-- الجواب: دور تطبيق ليس superuser وليس مالكاً، له INSERT و SELECT فقط، و UPDATE
-- و DELETE و TRUNCATE **مسحوبة صراحةً**. الرفض يأتي بالرمز 42501 قبل أي منطق.
--
-- اسم الدور يُقرأ من إعداد الجلسة babel.storage_app_role كي لا يُثبَّت اسم بيئة في
-- نصّ نشر. لا كلمات مرور هنا ولا في أي مكان في المستودع.
-- ═══════════════════════════════════════════════════════════════════════════
do $grants$
declare
    v_role text := coalesce(nullif(current_setting('babel.storage_app_role', true), ''), 'babel_storage_app');
begin
    if not exists (select 1 from pg_roles where rolname = v_role) then
        raise exception
            'APP_ROLE_MISSING role=% : دور التطبيق غير موجود — الحصانة بالصلاحيات لا تُركَّب بعد الحقيقة / the application role must exist before the storage schema is granted',
            v_role;
    end if;

    -- superuser يتجاوز كل شيء دائماً، فتصير كل الطبقة أدناه زينة (فخ-30 · ADR-0003).
    if exists (select 1 from pg_roles where rolname = v_role and rolsuper) then
        raise exception
            'APP_ROLE_IS_SUPERUSER role=% : دور التطبيق superuser — كل ضمانات الحصانة ساقطة / the application role is a superuser; every immutability guarantee is void',
            v_role;
    end if;

    execute format('revoke all on schema storage from public');
    execute format('grant usage on schema storage to %I', v_role);
    execute format('revoke all on all tables in schema storage from %I', v_role);

    -- ── جدول المرفقات: يُضاف إليه فقط ─────────────────────────────────────
    execute format('grant select, insert on storage.attachment to %I', v_role);
    execute format('revoke update, delete, truncate on storage.attachment from %I', v_role);

    -- ── علامات السحب: تُضاف ولا تُرفع ─────────────────────────────────────
    -- ولو مُلكت هنا صلاحية DELETE لصار «سحبٌ ثم تراجعٌ عن السحب» عمليةً بلا أثر،
    -- وهي بالضبط الرواية التي يُفترض أن يمنعها السجلّ.
    execute format('grant select, insert on storage.attachment_withdrawal to %I', v_role);
    execute format('revoke update, delete, truncate on storage.attachment_withdrawal from %I', v_role);

    execute format('grant usage, select on all sequences in schema storage to %I', v_role);
end
$grants$;
