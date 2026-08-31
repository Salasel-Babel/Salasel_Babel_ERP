-- ═══════════════════════════════════════════════════════════════════════════
-- المرفق يحمل مستنده المصدر — عمودان ومفتاح جرد، لا جدول ربط
--
-- الباب السادس على سطح المرفقات هو الجرد: «ما مرفقات هذه الفاتورة؟». وبلا هذين
-- العمودين لا يُجاب ذلك السؤال إلا بجدول ربط في مخطّط آخر، أو بمسح كامل. وكلاهما
-- أسوأ: الجدول يعبر حدّ وحدة (القاعدة 5)، والمسح يجعل شاشةً عادية تقرأ المخزن كلّه.
--
-- والعمودان **يقبلان NULL**: النموّ إضافة محضة، والصفوف المُودَعة قبل هذه الهجرة
-- مرفقاتٌ بلا مستند مصدر — وهي حالة مشروعة تبقى مشروعة.
--
-- وقيدُ الاقتران يقوله المخطّط لا الشيفرة: نصفُ ربطٍ في صفٍّ يُقرأ لاحقاً «مرفقٌ بلا
-- مستند» أو «مستندٌ بلا معرّف»، وكلاهما كذبة صامتة لا استثناء.
--
-- ولا يمرّ شيء من هذا على مشغّل «يُضاف ولا يُعدَّل»: ALTER TABLE تعريفٌ لا تعديل صفّ،
-- والمشغّل على UPDATE و DELETE للصفوف وحدها. والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════

alter table storage.attachment add column if not exists "SourceDocumentType" varchar(64);
alter table storage.attachment add column if not exists "SourceDocumentId" uuid;

do $source$
begin
    if not exists (
        select 1 from pg_constraint where conname = 'ck_storage_attachment_source_document'
    ) then
        alter table storage.attachment
            add constraint ck_storage_attachment_source_document
            check (("SourceDocumentType" is null) = ("SourceDocumentId" is null));
    end if;
end
$source$;

create index if not exists ix_storage_attachment_source_document
    on storage.attachment ("TenantId", "SourceDocumentType", "SourceDocumentId");
