-- ═══════════════════════════════════════════════════════════════════════════
-- ما لا يعبّر عنه نموذج EF: ثباتُ مقياس العرض، ورابطُ المركز بمنشأته.
--
-- **لماذا مشغّل لا اتفاق في الكود.** ثبات مقياس العرض اليوم «غيابُ باب»: لا يوجد
-- في الشجرة توقيعٌ واحد يحمل DisplayScale إلى منشأة قائمة (ADR-0026 · انظر
-- ICompanySetupStore). وذلك صحيح ما دام الطريق الوحيد إلى الصفّ هو هذه الشجرة.
-- لكن الصفّ الآن في قاعدة بيانات، ولقاعدة البيانات أبوابٌ أخرى: سكربت صيانة،
-- أداة إدارة، تصحيحٌ يدوي في الثانية صباحاً. فما كان غيابَ بابٍ في الكود يصير
-- باباً مفتوحاً في القاعدة ما لم يُقفل هنا (ADR-0003: الحصانة يفرضها PostgreSQL).
--
-- ورقمٌ يُقرأ بخانتين في شاشة وبأربع في أخرى مشكلةُ مطابقة لا تفضيلُ عرض — وهو
-- بالضبط ما يجعل هذا القفل محاسبياً لا هندسياً (ADR-0025).
-- ═══════════════════════════════════════════════════════════════════════════

create or replace function core.company_setup_is_immutable()
returns trigger
language plpgsql
as $fn$
begin
    if new.company_id is distinct from old.company_id then
        raise exception
            'COMPANY_SETUP_IDENTITY_IMMUTABLE : هوية المنشأة لا تُنقل / a company setup row never changes its company';
    end if;

    if new.decimal_places is distinct from old.decimal_places then
        raise exception
            'DISPLAY_SCALE_IMMUTABLE company=% from=% to=% : مقياس العرض يُسنَد عند التأسيس ولا يُعدَّل بعده / the display scale is assigned at founding and never changes',
            old.company_id, old.decimal_places, new.decimal_places;
    end if;

    if new.founded_at is distinct from old.founded_at then
        raise exception
            'FOUNDING_INSTANT_IMMUTABLE company=% : لحظة التأسيس وقعت مرّة / the founding instant happened once';
    end if;

    return new;
end
$fn$;

drop trigger if exists trg_company_setup_immutable on core.company_setup;

create trigger trg_company_setup_immutable
    before update on core.company_setup
    for each row execute function core.company_setup_is_immutable();

-- ── مركزُ تكلفة بلا منشأة مؤسَّسة لا معنى له ────────────────────────────────
-- والقيد هنا لا في نموذج EF كي يبقى النموذج وصفاً للأعمدة، ويبقى ما يفرضه
-- المحرّك مقروءاً في موضع واحد.
alter table core.cost_center
    drop constraint if exists fk_cost_center_company;

alter table core.cost_center
    add constraint fk_cost_center_company
    foreign key (company_id) references core.company_setup (company_id);
