-- ═══════════════════════════════════════════════════════════════════════════
-- هجرة المشتريات 002 — المورد يحمل رقم تسجيله الضريبي
-- ═══════════════════════════════════════════════════════════════════════════
--
-- رمز فاتورة المورد (‏ZATCA QR) يحمل رقم تسجيل البائع الضريبي **مُصدَّقاً**: كتبه
-- المُصدِر داخل الرمز، لا قارئٌ ضوئي. وجدول الموردين لم يكن يحمل هذا الحقل، فكان
-- المُصدَّق يصل ولا يُطابقه شيء. والأثر ليس بطئاً في الإدخال وحده: فاتورة مُصدَّقة
-- تُسند إلى المورد الخطأ **بلا أن يعترض شيء**، فيُنتج المعرّف المُصدَّق مظهرَ تحقّق
-- بلا تحقّق — وذلك أسوأ من غياب المعرّف.
--
-- **اصطلاح الخواء — نصّ فارغ لا قيمة معدومة:** العمود ‏`not null default ''`.
-- والسبب أن الفهرس أدناه **جزئيّ** بشرط `<> ''`؛ ولو جاز الخواء بقيمتين لصار عندنا
-- صفوف تُظنّ داخل الفهرس وهي خارجه (‏`null <> ''` تساوي `null` لا `true`). اصطلاح
-- واحد للخواء، ومشروط به الفهرس حرفياً.
--
-- **والموردون القائمون يبقون بلا رقم، ولا يُخترع لهم شيء:** رقم التسجيل الضريبي
-- معرّفٌ خارجي تصدره الهيئة، وتخمينه بأثر رجعي ينسب مورداً إلى تسجيل قد لا يكون له.
-- الملء يقع لاحقاً بيد المستخدم عبر `SupplierService.SetVatNumberAsync`، أو لا يقع:
-- المورد دون حدّ التسجيل، والمورد غير المقيم، كلاهما بلا رقم بحقّ.
--
-- **ولماذا الفهرس ليس فريداً:** الافتراض «المستأجر لا يسجّل مورداً مرّتين بالرقم
-- نفسه» **غير صحيح في السعودية**: المجموعة الضريبية الواحدة تحمل رقم تسجيل واحداً
-- لعدّة منشآت، فشراءٌ من منشأتين في المجموعة نفسها يُنتج مورّدين مشروعين برقم واحد؛
-- ومورّدٌ أُعيد إنشاؤه بعد اندماج يترك صفّاً موقوفاً بالرقم نفسه. وفهرس فريد هنا
-- يمنع **تسجيل الواقع**، فيلتفّ عليه المستخدم بتشويه الرقم — ويُفقَد المعرّف الذي
-- بُني هذا كلّه لأجله. الحارس ينتقل إلى موضع السؤال: البحث برقم ضريبي يُرجع مورداً
-- واحداً أو يرفض بالغموض ويُسمّي المرشّحين، ولا يختار أبداً.
--
-- **وقيد الشكل في القاعدة لا في الشيفرة وحدها:** خمس عشرة خانة، تبدأ بـ3 وتنتهي
-- بـ3. والصنف `[0-9]` مقصود ولا يجوز أن يصير `\d`: الأخير `[[:digit:]]` وقد يقبل
-- الأرقام العربية-الهندية والديفاناغارية حسب الإعدادات المحلية — وذلك بالضبط باب
-- الدخول الصامت الذي يُغلقه هذا العمود.
--
-- والهجرة **معاملاتية** ومكتوبة لتُعاد بلا أثر.

do $migration$
declare
    v_malformed bigint;
begin
    -- قاعدة لم يُنشأ فيها المخطّط بعد ليست حالة خطأ: EnsureCreated ينشئ الشكل
    -- الجديد كاملاً، ولا شيء هنا ليُرقّى.
    if to_regclass('purchasing.supplier') is null then
        return;
    end if;

    alter table purchasing.supplier
        add column if not exists "VatNumber" character varying(15) not null default '';

    -- عمودٌ أُضيف في تشغيل سابق ثم عُدِّل بيدٍ خارجية: يُعاد إلى اصطلاح الخواء الواحد.
    update purchasing.supplier set "VatNumber" = '' where "VatNumber" is null;
    alter table purchasing.supplier alter column "VatNumber" set default '';
    alter table purchasing.supplier alter column "VatNumber" set not null;

    -- ولا يُركَّب قيد على بيانات تخرقه: يُسمّى العدد ويتوقّف كل شيء. تركيب قيد
    -- `not valid` هنا يعني قاعدةً تبدو محروسة وليست كذلك.
    select count(*) into v_malformed
      from purchasing.supplier
     where "VatNumber" <> '' and "VatNumber" !~ '^3[0-9]{13}3$';

    if v_malformed > 0 then
        raise exception
            'MALFORMED_SUPPLIER_VAT_NUMBER rows=% : يوجد % صفّ مورد برقم تسجيل ضريبي مخالف للشكل (خمس عشرة خانة تبدأ بـ3 وتنتهي بـ3). الرقم معرّف خارجي تُصدره الهيئة، وتصحيحه بأثر رجعي تخمين — العلاج تصحيح الصفوف يدوياً أو تفريغ الحقل، لا تليين القيد / supplier rows carry a VAT registration number that does not match the required shape; the number is an externally issued identifier and back-filling or reshaping it would be a guess',
            v_malformed, v_malformed
            using errcode = 'check_violation';
    end if;

    if not exists (
        select 1 from pg_constraint
         where conname = 'ck_purchasing_supplier_vat_number'
           and conrelid = 'purchasing.supplier'::regclass)
    then
        alter table purchasing.supplier
            add constraint ck_purchasing_supplier_vat_number
            check ("VatNumber" = '' or "VatNumber" ~ '^3[0-9]{13}3$');
    end if;

    -- فهرس بحث جزئيّ: الغالبية بلا رقم، ولا يُبحث عن الفراغ أصلاً. والشرط يطابق
    -- اصطلاح الخواء في العمود حرفياً.
    create index if not exists ix_purchasing_supplier_vat_number
        on purchasing.supplier ("TenantId", "VatNumber")
        where "VatNumber" <> '';
end
$migration$;
