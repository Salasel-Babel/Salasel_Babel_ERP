-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المبيعات 001 — هوية الترحيل تشمل رمز الحدث
-- ═══════════════════════════════════════════════════════════════════════════
--
-- كان مفتاح uq_sales_posting_identity خماسياً: (مستأجر · نوع المستند · معرّفه · رمز الإطلاق · الجيل)،
-- وكان استعلام «هل رُحّل من قبل؟» في البوابة على الحقول نفسها. فالمستند الواحد
-- الذي يُنتج حدثين محاسبيين مختلفين عند الإطلاق نفسه — فاتورة تعترف بالإيراد
-- وتُنزل المخزون بالتكلفة، وفاتورة مورد بشقّ بضاعة وشقّ مصروف — كان حدثه الثاني
-- يُعدّ «مُرحَّلاً سلفاً»: لا يُكتب، ولا يُرفع خطأ، والإيصال يعود حاملاً معرّف
-- **القيد الأول** فتخزّنه الوحدة في مكان القيد الثاني.
--
-- **لماذا هذه الهجرة آمنة على بيانات قائمة:** المفتاح الجديد أوسع من القديم بعمود،
-- وتوسيع مفتاح فريد لا يُنتج تصادماً جديداً أبداً — كل صفّ كان فريداً بخمسة أعمدة
-- يبقى فريداً بستة. ولا صفّ واحد يُعاد كتابته.
--
-- **وما لا تفعله:** لا تملأ رمز حدث فارغاً بأثر رجعي. رمز الحدث هوية، وتخمينه
-- يعني نسبة قيد إلى حدث لم يقع. فإن وُجد صفّ برمز فارغ تتوقّف الهجرة وتسمّي العدد.
--
-- والهجرة **معاملاتية**: كتلة do واحدة، فإما أن تتم كلها أو لا يتغيّر شيء.

do $migration$
declare
    v_blank bigint;
begin
    -- قاعدة لم يُنشأ فيها المخطّط بعد ليست حالة خطأ: EnsureCreated ينشئ الشكل
    -- الجديد كاملاً، ولا شيء هنا ليُرقّى.
    if to_regclass('sales.document_posting') is null then
        return;
    end if;

    select count(*) into v_blank
      from sales.document_posting
     where "EventCode" is null or length(btrim("EventCode")) = 0;

    if v_blank > 0 then
        raise exception
            'BLANK_EVENT_CODE_IN_POSTING_ATTEMPTS rows=% : يوجد % صفّ محاولة برمز حدث فارغ. رمز الحدث هوية لا وصف، وتخمينه بأثر رجعي ينسب قيداً إلى حدث لم يقع — العلاج تسمية الحدث الصحيح يدوياً أو حذف المحاولة غير المُرحَّلة، لا ملء تلقائي / posting attempt rows with a blank event code exist; the event code is identity, not description, so back-filling it would attribute an entry to an event that never happened',
            v_blank, v_blank
            using errcode = 'check_violation';
    end if;

    alter table sales.document_posting alter column "EventCode" set not null;

    -- الفهرس القديم يُسقط ويُعاد بستة أعمدة. الإسقاط والإنشاء عمليتا مالك ولا
    -- تمرّان على صلاحيات صفوف، ولا يوقظان مشغّلاً مؤجَّلاً: لا مشغّلات على هذا
    -- الجدول أصلاً.
    drop index if exists sales.uq_sales_posting_identity;

    create unique index uq_sales_posting_identity
        on sales.document_posting
           ("TenantId", "DocumentType", "DocumentId", "TriggerCode", "Generation", "EventCode");

    -- ورمزٌ فارغ يُعيد تركيب العطب داخل مفتاح موسَّع، فيُمنع في القاعدة نفسها.
    if not exists (
        select 1 from pg_constraint
         where conname = 'ck_sales_document_posting_event_code'
           and conrelid = 'sales.document_posting'::regclass)
    then
        alter table sales.document_posting
            add constraint ck_sales_document_posting_event_code check (length(btrim("EventCode")) > 0);
    end if;
end
$migration$;
