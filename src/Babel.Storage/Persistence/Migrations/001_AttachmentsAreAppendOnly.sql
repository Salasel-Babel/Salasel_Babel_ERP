-- ═══════════════════════════════════════════════════════════════════════════
-- الطبقة الثانية للمرفقات: مشغّل رفض — يعمل على **كل** دور، والمالك منهم
--
-- الطبقة الأولى (StorageGrants.sql) تنزع UPDATE و DELETE من دور التطبيق. وهي
-- كافية ضدّ التطبيق ومَن سرق بيانات اعتماده، **وغير كافية ضدّ اتصال بدور المالك**:
-- سكربت صيانة، أو هجرة كُتبت بعجلة، أو psql مفتوح. وذلك بالضبط المسار الذي يُعدَّل
-- منه سند إثبات دون أن يعترض شيء.
--
-- فهنا مشغّل يرفض UPDATE و DELETE على جدولَي المخزن **مهما كان الدور**. وهو ليس
-- مثالياً ولا يُدَّعى ذلك: من يملك المخطّط يستطيع أن يُسقط المشغّل. لكن الفارق ليس
-- شكلياً — الإسقاط **فعلٌ صريح باسمه** يظهر في سجلّ الخادم وفي أي مراجعة، بينما
-- ‏UPDATE بلا مشغّل لا يترك أثراً على الإطلاق. والغرض من هذه الطبقة أن يصير التعديل
-- **قراراً يُتّخذ**، لا حادثاً يقع.
--
-- والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════

create or replace function storage.refuse_mutation() returns trigger
language plpgsql as $fn$
begin
    raise exception
        'ATTACHMENT_IS_APPEND_ONLY table=%.% operation=% : سجلّ المرفقات يُضاف إليه ولا يُعدَّل ولا يُحذف — التصحيح إصدار جديد يشير إلى سلفه، والإزالة علامة سحب / the attachment register is append-only: a correction is a new version that references its predecessor, and a removal is a withdrawal marker',
        tg_table_schema, tg_table_name, tg_op
        using errcode = 'restrict_violation';
end
$fn$;

drop trigger if exists trg_attachment_append_only on storage.attachment;
create trigger trg_attachment_append_only
    before update or delete on storage.attachment
    for each row execute function storage.refuse_mutation();

drop trigger if exists trg_attachment_withdrawal_append_only on storage.attachment_withdrawal;
create trigger trg_attachment_withdrawal_append_only
    before update or delete on storage.attachment_withdrawal
    for each row execute function storage.refuse_mutation();

-- ═══════════════════════════════════════════════════════════════════════════
-- ولا علامة سحب لمرفق غير موجود، ولا إصدارٌ يشير إلى سلف غير موجود.
--
-- المفتاح الخارجي هنا **داخل المخطّط نفسه** — لا يعبر حدّ وحدة (القاعدة 5) — و
-- ‏on delete no action مقصود: الحذف ممنوع أصلاً، والقيد يقول ذلك مرّة أخرى.
-- ═══════════════════════════════════════════════════════════════════════════
do $constraints$
begin
    if not exists (
        select 1 from pg_constraint where conname = 'fk_storage_withdrawal_attachment'
    ) then
        alter table storage.attachment_withdrawal
            add constraint fk_storage_withdrawal_attachment
            foreign key ("AttachmentId") references storage.attachment ("Id")
            on delete no action on update no action;
    end if;

    if not exists (
        select 1 from pg_constraint where conname = 'fk_storage_attachment_supersedes'
    ) then
        alter table storage.attachment
            add constraint fk_storage_attachment_supersedes
            foreign key ("SupersedesId") references storage.attachment ("Id")
            on delete no action on update no action;
    end if;
end
$constraints$;
