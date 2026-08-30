-- ═══════════════════════════════════════════════════════════════════════════
-- مدّةٌ سارية واحدة لكل وحدة — قيدٌ في قاعدة البيانات لا فحصٌ في الواجهة
--
-- ‏**لماذا لا يعبّر عنه فهرس فريد:** «مدّة واحدة» شرط **تقاطع مدى** لا شرط تساوٍ.
-- عقدان على الوحدة نفسها بتاريخي بداية مختلفين لهما مفتاحان مختلفان، فيمرّان من أي
-- فهرس فريد مهما اتّسع — بينما مدّتاهما متداخلتان والوحدة مؤجَّرة مرّتين.
--
-- ‏**ولماذا في القاعدة لا في الخدمة:** فحصٌ في الخدمة يقرأ ثم يكتب، وبين القراءة
-- والكتابة يمرّ نداءٌ آخر. نداءان متزامنان على الوحدة نفسها يجتازان الفحص معاً
-- ويكتبان عقدين متداخلين — وهو الشكل نفسه الذي تمنعه القاعدة الأولى للكتابة في
-- ‏`docs/evidence/README.md §3`.
--
-- ‏**والتبعية معلَنة لا مضمرة:** ‏`btree_gist` امتدادٌ بلا سابقة في هذا المستودع
-- (مقيس: صفر مطابقة لـ`create extension` في `src/` قبل هذا الملفّ). وهو **موثوق**
-- منذ PostgreSQL 13 فيركّبه مالك القاعدة بلا امتياز خارق؛ ومع ذلك يبقى تركيبه فعل
-- مالك، وبيئة استضافة تمنعه تُبطل هذا القيد — وذلك بندٌ في سجلّ دَين التحقّق لا
-- مفاجأة يوم النشر.
--
-- والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════
create extension if not exists btree_gist;

do $one_live_term$
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
            where ("State" = 'ACTIVE');
    end if;
end
$one_live_term$;
