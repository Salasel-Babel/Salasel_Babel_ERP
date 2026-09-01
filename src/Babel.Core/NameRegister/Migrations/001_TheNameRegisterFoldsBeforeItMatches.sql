-- ═══════════════════════════════════════════════════════════════════════════
-- سجلّ الأسماء يُطوى قبل أن يُطابَق — ودالّةٌ واحدة تحكم العمود والاستعلام معاً
--
-- ‏**لماذا لا يكفي pg_trgm وحده:** مقيس على هذا الجهاز (‏PostgreSQL 16.13 · pg_trgm 1.6)
-- أن `similarity('أحمد','احمد') = 0.250` — **دون العتبة الافتراضية 0.3**. أي أن اسماً
-- بهمزةٍ واحدة لا يُعثر عليه إطلاقاً، والعطل يظهر «لا نتائج» لا رسالةَ خطأ. وبعد الطيّ
-- يصير 1.000. والقياس كاملاً في تعليق `ArabicNameFold`.
--
-- ‏**ولماذا لا unaccent:** مقيس أنّها لا تفعل شيئاً للعربية —
-- `unaccent('أحمد') = 'أحمد'` و`unaccent('محمّد') = 'محمّد'`، بلا تغيير.
--
-- ‏**ولماذا لا to_tsvector('arabic', …):** مقيس أنّها **تخالف نفسها** عبر اختلافٍ رسميّ
-- واحد: «محمد القحطاني» تُجذَّر `'قحطان'` بينما «محمّد القحطانى» تُجذَّر `'قحطاني'`.
-- ومُجذِّرٌ يخلط «شركة» بـ`'شرك'` و«المسارات» بـ`'مسار'` أداةٌ لمتون الوثائق لا لسجلّ أسماء.
--
-- ‏**ولا `lower()` في الطيّ.** مقيس أنّها مُعلَنة `IMMUTABLE` في `pg_proc`
-- (`provolatile = 'i'`) وهي مع ذلك تابعة لترتيب المقارنة، فيقبلها PostgreSQL في عمودٍ
-- مولَّد مخزَّن بلا اعتراض — ويصير محتوى العمود تابعاً لترتيب قاعدةٍ بعينها. الخفض هنا
-- بـ`translate` على ASCII وحدها، فلا يعرف ترتيباً أصلاً.
--
-- ‏**والعمود مولَّد مخزَّن (`GENERATED … STORED`)، فتغييرُ أي قاعدة طيّ يستلزم هجرةً
-- تُعيد بناء العمود.** لا يكفي `create or replace function`: الصفوف القائمة تحتفظ بقيمها
-- المحسوبة قديماً، فيصير نصف السجلّ مطويّاً بقاعدةٍ ونصفه بأخرى — وهو أسوأ من ألّا يُطوى.
--
-- والنصّ مكتوب ليُعاد تشغيله بلا أثر.
-- ═══════════════════════════════════════════════════════════════════════════

create schema if not exists babel;

-- ── ١ · الامتداد يُفحص قبل أن يُستعمل، ويُوقف النشر إن غاب ───────────────────
--
-- ‏**السابقة:** ADR-0060 «تزويد قاعدة الوحدة جزءٌ من نشرها، وامتدادٌ مفقود يوقف النشر
-- ولا يُخترع له بديل»، وهي مكتوبة عن `btree_gist` في وحدة العقارات. والفرق هنا أن
-- الغياب **لا يُسقط أي استعلام**: بلا `pg_trgm` تبقى المطابقة على المفتاح الضيّق وحده،
-- فتعمل «عبدالله/عبد الله» ولا تعمل «المسار الامثل/المسار الأمثل» — نظامٌ يجد بعض
-- الأسماء ولا يجد بعضها، **بلا رسالةٍ واحدة**. ولذلك يُرفَع الصوت هنا لا هناك.
create or replace function babel.require_extension(extension_name text, what_is_lost text)
returns void
language plpgsql
as $require$
begin
    if exists (select 1 from pg_extension where extname = extension_name) then
        return;
    end if;

    if not exists (select 1 from pg_available_extensions where name = extension_name) then
        raise exception
            'الامتداد «%» غير متاح على هذا الخادم فلا يُنشأ ما يعتمد عليه. والمفقود: %. '
            'يُركَّب بيد مالك القاعدة أو يُنشَر على خادمٍ يتيحه — ولا يُتابَع النشر بدونه. '
            '/ extension "%" is not available on this server; what is lost: %.',
            extension_name, what_is_lost, extension_name, what_is_lost
            using errcode = 'feature_not_supported';
    end if;

    begin
        execute format('create extension if not exists %I', extension_name);
    exception when insufficient_privilege then
        raise exception
            'الامتداد «%» متاح ولكن هذا الدور لا يملك تركيبه. والمفقود: %. '
            'يُركَّب باتصال المالك في حاوية الترحيل (‏ADR-0060) — ولا يُتابَع النشر بدونه. '
            '/ extension "%" is available but this role may not install it; what is lost: %.',
            extension_name, what_is_lost, extension_name, what_is_lost
            using errcode = 'insufficient_privilege';
    end;
end
$require$;

select babel.require_extension(
    'pg_trgm',
    'مطابقة الأسماء بالتشابه الثلاثي. بدونها يُطابَق المفتاح الضيّق وحده، '
    || 'فتُوجد «عبد الله» ولا تُوجد «المسار الأمثل» — سجلٌّ يجد بعض الأسماء بلا أن يقول إنه لم يجد الباقي');

-- ── ٢ · الطيّ — تعريفٌ واحد، وهو الذي يحكم العمود المخزَّن ونصّ الاستعلام معاً ──
--
-- الترتيب ملزم: التطبيع، ثم الحذف (تطويل · تشكيل · علامات قرآنية · غير مرئي)، ثم
-- توحيد الرسم والأرقام وخفض ASCII، ثم طيّ الفراغ.
--
-- ‏**وصنفا المحارف مكتوبان بمهارب `\uXXXX` لا بالمحارف نفسها.** محرفٌ غير مرئي مكتوب
-- بذاته في مصدرٍ هو محرفٌ **لا يراه المراجع ولا الفرق**: يُحذف بلمسة مفتاح فلا يظهر في
-- ‏`git diff` إلا فراغاً، ويصير الطيّ يمرّر ما كان يحذفه بلا أن يشتكي أحد.
--
-- ‏**وصنف الفراغ مُعدَّد صراحةً ولا يُكتب `\s`:** ‏`\s` هنا هو الفراغ اللاتيني وحده،
-- والمرآة بلغة C# تعرف U+00A0 و U+2000–U+200A — فتعريفان بلغتين يُنتجان مفتاحين لنصٍّ
-- واحد فيه مسافةٌ غير فاصلة، وهي أشيع ما يُلصَق من صفحةٍ أو مستند.
create or replace function babel.fold_arabic(value text)
returns text
language sql
immutable
strict
parallel safe
as $fold$
select btrim(
    regexp_replace(
        translate(
            regexp_replace(
                normalize(value, nfc),
                '[\u0640\u064B-\u065F\u0670\u06D6-\u06ED\u061C\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]',
                '',
                'g'),
            'أإآٱٲٳىةؤئ٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹ABCDEFGHIJKLMNOPQRSTUVWXYZ',
            'اااااايهوي01234567890123456789abcdefghijklmnopqrstuvwxyz'),
        '[\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+',
        ' ',
        'g'),
    ' ')
$fold$;

comment on function babel.fold_arabic(text) is
    'طيّ اسمٍ عربيّ لمفتاح بحث. لا يُطبَّق على حقلٍ موقَّع ولا يُعرض للمستخدم. '
    'مرآته بلغة C# هي Babel.Ai.Lookup.ArabicNameFold، ويحرس اتّفاقهما إثبات.';

create or replace function babel.fold_arabic_tight(value text)
returns text
language sql
immutable
strict
parallel safe
as $tight$
select replace(babel.fold_arabic(value), ' ', '')
$tight$;

comment on function babel.fold_arabic_tight(text) is
    'الطيّ وقد نُزع منه كل فراغ. مقيس: عبدالله~عبد الله يبقى 0.545 بعد الطيّ الكامل '
    'ويصير تساوياً تامّاً هنا.';
