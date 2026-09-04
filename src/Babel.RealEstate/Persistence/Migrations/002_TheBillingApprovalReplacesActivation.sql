-- ═══════════════════════════════════════════════════════════════════════════
-- ‏«سارٍ» تخرج من الجدول: القيد يُعتمد للفوترة ولا يصير نافذاً
--
-- ‏**لماذا نصٌّ ثانٍ لا تعديلٌ في الأول:** النصّ الأول يحرس نفسه بـ
-- ‏`if not exists (conname = …)`، فتعديلُه في مكانه **لا يُغيّر شيئاً في قاعدة
-- قائمة**: القيد موجود بالاسم نفسه، فيُتخطّى الإنشاء بصمت ويبقى شرطه القديم
-- ‏`State = 'ACTIVE'` — بينما الشيفرة لم تعد تكتب `ACTIVE` أبداً. والنتيجة قيدُ
-- استبعادٍ **لا يحرس صفّاً واحداً**، وهو يبدو قائماً في `pg_constraint`. فالإسقاط
-- الصريح ثم إعادة البناء هو ما يجعل الترقية تقع فعلاً.
--
-- ‏**وما تغيّر في المعنى:** لم تكن `ACTIVE` تعني «مُرحَّل» ولا «مكتمل» — كانت تعني
-- ‏**«العقد سارٍ»**، وهو حكمٌ لا يملكه هذا النظام: عقد الإيجار يُحرَّر في منصّة
-- إيجار الحكومية وتُقرَّر آثاره هناك. وما يملكه هذا النظام هو **إذنُ الفوترة**:
-- من لحظة الاعتماد تُبنى فواتير الإيجار على هذا القيد. فصارت القيمة `BILLABLE`.
--
-- ‏**والقيد الزمني يبقى على معناه الجديد:** وحدةٌ واحدة لا تُفوتَر مرّتين على اليوم
-- نفسه. وهو ما زال شرطَ **تقاطع مدى** لا شرط تساوٍ، فلا يعبّر عنه فهرس فريد.
--
-- والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════
create extension if not exists btree_gist;

-- ‏٠ · العمود يُعاد تسميته: «رقم العقد» صار «رقم عقد إيجار» — مرجعٌ خارجي لا رقمٌ
--      يولّده هذا النظام. و‏`EnsureCreated` ينشئ الاسم الجديد في قاعدة فارغة ولا
--      يمسّ قاعدةً قائمة، فالشرط أدناه هو ما يجعل الترقية تقع مرّةً واحدة.
do $rename_reference$
begin
    if exists (
        select 1 from information_schema.columns
         where table_schema = 'realestate' and table_name = 'lease_contract'
           and column_name = 'ContractNo')
    then
        alter table realestate.lease_contract rename column "ContractNo" to "EjarContractNumber";
    end if;
end
$rename_reference$;

-- ‏١ · يُسقط القيد أياً كان شرطه — فالاسم واحد والشرط هو ما تغيّر.
alter table realestate.lease_contract
    drop constraint if exists ex_realestate_lease_term_does_not_overlap;

-- ‏٢ · تُنقل الصفوف القائمة إلى القيمة الجديدة قبل إعادة بناء القيد.
update realestate.lease_contract set "State" = 'BILLABLE' where "State" = 'ACTIVE';

-- ‏٣ · يُعاد بناؤه على الشرط الجديد.
do $one_billable_term$
begin
    if not exists (
        select 1 from pg_constraint
         where conname = 'ex_realestate_lease_term_does_not_overlap'
           and conrelid = 'realestate.lease_contract'::regclass)
    then
        alter table realestate.lease_contract
            add constraint ex_realestate_lease_term_does_not_overlap
            exclude using gist (
                "TenantId" with =,
                "CompanyId" with =,
                "UnitId" with =,
                daterange("StartsOn", "EndsOn", '[]') with &&
            )
            where ("State" = 'BILLABLE');
    end if;
end
$one_billable_term$;
