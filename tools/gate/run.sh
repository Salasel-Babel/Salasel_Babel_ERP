#!/usr/bin/env bash
# ‏**البوّابة المحلية** — أمرٌ واحد يبني الحلّ كلّه ثم يُشغّل الاختبارات، بهذا الترتيب.
# the local gate: build the whole solution, then run the tests — in that order
#
# لماذا يوجد هذا الملف:
#   «‏871 اختباراً · 0 فشل» ليست جملةً عن صحّة المستودع، بل عن صحّة **ما بُني**.
#   و‏`dotnet test --solution Babel.slnx` **لا يبني** مشروعاً لا يشير إليه أي مشروع
#   اختبار: يبني مشاريع الاختبار ومراجعها المتعدّية وحدها. فمشروعٌ في ملف الحلّ،
#   خاضعٌ للقاعدة 9، مبنيٌّ في التكامل المستمر — يبقى مع ذلك **غير مبنيّ محلياً**،
#   وعطلُ ترجمةٍ فيه يعيش على فرعٍ تُعلن كل اختباراته أنه سليم.
#   وقع هذا فعلاً على develop عند ed02df2: `demo/company/Verify.cs` لم يُترجم،
#   و‏`dotnet test --solution` أعطى 871/0 بينما `dotnet build` أعطى خطأين.
#   (‏docs/evidence/traps.md#fakh-dotnet-test-does-not-build-what-no-test-references)
#
# الاستعمال:
#   tools/gate/run.sh                     # ‏Release — وهو ما يُشغَّل قبل الدفع
#   tools/gate/run.sh --configuration Debug
#   tools/gate/run.sh --no-isolation      # يتخطّى مسح العزل الطويل (لا يُتخطّى قبل الدمج)
#   tools/gate/run.sh --with-frontend     # يضيف فحوص الواجهة التي تحتاج npm ci
#   tools/gate/run.sh --with-demo         # يبني الشركة التجريبية **من الصفر** ويُشغّلها
#
# وهذه البوّابة **ليست** كل ما يفعله ci.yml: العزل ومتّجهات الشكل القانوني وأداة
# مصفوفة الترحيل وحرّاس المعرّفات هناك أيضاً. هي **الحدّ الأدنى الذي لا يُدفَع فرعٌ بدونه**.
#
# الخروج: 0 إن نجح كل ما شُغّل · 1 عند أول سقوط، ومعه اسم الخطوة التي سقطت.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cd "$root"

configuration="Release"
isolation="yes"
frontend="static"
demo="no"

while [ $# -gt 0 ]; do
    case "$1" in
        --configuration) configuration="${2:?--configuration يحتاج قيمة}"; shift 2 ;;
        --no-isolation)  isolation="no"; shift ;;
    --with-frontend) frontend="full"; shift ;;
    --with-demo)     demo="yes"; shift ;;
        -h|--help)       sed -n '2,24p' "$0"; exit 0 ;;
        *) printf '✗ وسيط غير معروف: %s\n' "$1" >&2; exit 2 ;;
    esac
done

# ‏.NET 10 ليس في PATH افتراضياً على بعض التوزيعات — نفس ارتداد deploy/up.sh.
if ! command -v dotnet >/dev/null 2>&1; then
    for candidate in /usr/lib/dotnet /usr/share/dotnet "$HOME/.dotnet"; do
        if [ -x "$candidate/dotnet" ]; then export PATH="$PATH:$candidate"; break; fi
    done
fi
command -v dotnet >/dev/null 2>&1 || { printf '✗ dotnet غير موجود في PATH.\n' >&2; exit 1; }

step() { printf '\n══ %s\n' "$1"; }
fail() { printf '\n✗ سقطت البوّابة عند: %s\n' "$1" >&2; exit 1; }

# ‏**بأي حزمة تطوير قِيست هذه الخُضرة؟** `global.json` يطلب 10.0.100 بـ`rollForward:
# latestFeature`، وهي صيغة تقبل **أي نطاق ميزات أعلى** — فالعدّاء وجهاز التطوير قد
# يشغّلان نطاقين مختلفين بمحلّلَين مختلفين، ومع TreatWarningsAsErrors يصير ذلك فرقاً
# بين بناءٍ ناجح وبناءٍ ساقط **على البايتات نفسها**. قِيس: `IDE0005` في مسبار العلاقيّة
# خطأٌ على 10.0.400 وصفرُ تحذير على 10.0.111. فيُطبع الرقم هنا وفي ci.yml معاً، كي لا
# تكون «البوّابة خضراء» جملةً عن آلةٍ مجهولة.
# (‏docs/evidence/traps.md#fakh-the-gate-and-ci-run-different-sdk-feature-bands)
printf 'حزمة التطوير المُستعملة: %s\n' "$(dotnet --version)"

# ── ١ · البناء أولاً، وعلى **الحلّ كلّه** ──────────────────────────────────────
# ‏`Babel.slnx` لا مشروعاً ولا نمط مسارات: القاعدة 9 تضمن أن كل *.csproj على القرص
# فيه (عدا spikes/)، فالبناء عليه هو الجملة الوحيدة التي تعني «المستودع يُترجم».
step "١ · بناء الحلّ كلّه — $configuration · التحذير خطأ"
dotnet build Babel.slnx -c "$configuration" --nologo || fail "dotnet build Babel.slnx -c $configuration"

# ── ٢ · المسابر — تُبنى وحدها لأنها خارج ملف الحلّ عمداً ──────────────────────
# ‏spikes/ ليست في `Babel.slnx` بقرارٍ مقصود (القاعدة 9 تُعفيها بالاسم، والقاعدة 8
# تُخرجها من نطاقها لأن إحداها تستعمل WolverineFx.RuntimeCompilation فعلاً). وثمنُ
# ذلك أنّ **لا شيء كان يبنيها**: قِيس على develop أنّ ثلاثة من أربعة لا تستعيد أصلاً
# (‏NU1008)، وخلف جدار الاستعادة 353 تشخيصاً لم يرها أحد لأن المصرّف لم يبلغها قط.
# المسبار دليل، والدليل الذي لا يُبنى توقّف عن كونه دليلاً وهو ما يزال يبدو دليلاً.
# الحارس على وجود هذه الخطوة نفسها: القاعدة 16.
step "٢ · المسابر — كلٌّ وحده · التحذير خطأ"
for spike in \
    spikes/culture-calendar/CultureCalendarSpike.csproj \
    spikes/dotnet-stack/BabelSpike.csproj \
    spikes/pos-offline/PosOfflineSpike.csproj \
    spikes/relational-stack/RelationalSpike.csproj
do
    dotnet build "$spike" -c "$configuration" --nologo || fail "بناء المسبار $spike"
done

# ── ٣ · الحدود المعمارية منفصلةً ──────────────────────────────────────────────
# أولاً ووحدها كما في ci.yml: إن انكسر حدّ، تُقرأ رسالته فوراً لا بين مئات الأسطر.
step "٣ · الحدود المعمارية"
dotnet test --project tests/Babel.ArchitectureTests/Babel.ArchitectureTests.csproj \
    -c "$configuration" --no-build || fail "اختبارات المعمارية"

# ── ٤ · كل الاختبارات ─────────────────────────────────────────────────────────
# ‏`--no-build` ليس تسريعاً فحسب: الخطوة ١ هي التي بنت، وبدونها كانت هذه الخطوة
# ستمرّ على مشاريع لا تشير إليها الاختبارات **دون أن تبنيها** — وهو العطل نفسه.
step "٤ · كل الاختبارات"
dotnet test --solution Babel.slnx -c "$configuration" --no-build || fail "مجموعة الاختبارات"

# ── ٥ · مسح العزل ─────────────────────────────────────────────────────────────
if [ "$isolation" = "yes" ]; then
    step "٥ · مسح العزل — كل دالّة وحدها"
    tools/test-isolation/run.sh --grain method --configuration "$configuration" --jobs 4 \
        || fail "مسح العزل"
else
    printf '\n── مسح العزل مُتخطّى بـ--no-isolation. لا يُتخطّى قبل الدمج.\n'
fi

# ── ٦ · فحوص الواجهة ──────────────────────────────────────────────────────────
# ‏**لماذا هنا:** العقد له طرفان. غيّر إيداعٌ العقد المنشور، ومرّت حرّاس .NET خضراء،
# فقُرئ التغيير نازلاً كاملاً — وهو نصفه: العميل المُولَّد في `web/src/api/generated/`
# بقي على العقد القديم. البوّابة التي لا تشغّل شيئاً من `web/` كانت تُعلن خُضرةً عن
# نصف المستودع. (‏traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only)
#
# ‏**ولماذا هذه الفحوص بالذات افتراضياً:** `generate-client.mjs` و`audit.mjs`
# و`contrast.mjs` لا تستورد إلا وحدات Node المدمجة — **مقيس أنها تنجح بلا
# `node_modules` إطلاقاً**. و`contrast.mjs` يقيس عتبة WCAG AA على ملفّات السمة
# نفسها (‏78 زوجاً × لوحتين × سمتين)، فالحدّ الأدنى للتباين **مُنفَّذ لا موصى به**
# (‏ADR-0059 · contrast-floor-is-enforced).
# فثمنهما ثوانٍ ولا يحتاجان `npm ci`. وما عداهما يحتاج التثبيت، فهو خلف `--with-frontend`
# لئلّا تدفع كل بوّابة ثمن دقيقتين. وما لا يُشغَّل هنا **مُصرَّح به** في القاعدة 19،
# لا متروكاً ليفترض القارئ أنه مُغطّى.
step "٦ · الواجهة — فحوص لا تحتاج تثبيتاً"
if command -v node >/dev/null 2>&1; then
    ( cd web && node scripts/generate-client.mjs --check ) || fail "العميل المُولَّد يخالف العقد المنشور (web: gen:check)"
    ( cd web && node scripts/audit.mjs )                   || fail "فحص التدويل والاتجاه (web: audit:i18n)"
    ( cd web && node scripts/contrast.mjs --quiet )        || fail "عتبة التباين WCAG AA (web: contrast)"
else
    printf '── node غير موجود: فحوص الواجهة الساكنة لم تُشغَّل. وهذا نقصُ تغطية لا نجاح.\n'
fi

if [ "$frontend" = "full" ]; then
    step "٧ · الواجهة — الفحوص التي تحتاج npm ci"
    command -v npm >/dev/null 2>&1 || fail "npm غير موجود و--with-frontend يطلبه"
    ( cd web && npm ci )        || fail "npm ci"
    ( cd web && npm run build ) || fail "بناء الواجهة"
    ( cd web && npm run lint )  || fail "قواعد الحدّ (ESLint)"
    ( cd web && npm test )      || fail "اختبارات وحدة الواجهة"
else
    printf '\n── فحوص الواجهة التي تحتاج npm ci مُتخطّاة. أضف --with-frontend لتشغيلها.\n'
fi

# ── ٨ · الشركة التجريبية — بناءٌ من الصفر وتشغيل ────────────────────────────────
# ‏**لماذا خلف راية:** هذه الخطوة **تُسقط قواعد العرض وتُعيد بناءها**، فهي مُدمِّرة
# بطبيعتها ولا تصلح افتراضاً على آلة يتقاسمها وكلاء يشغّلون مسوحاً. وثمنها دقائق.
#
# ‏**ولماذا وُجدت أصلاً:** ‏`demo/company` كان **يُبنى ولا يُشغَّل**. فحين صارت
# ‏`SalesInvoiceService` تطلب `IInventoryValuation` (‏ADR-0039) ولم يسجّلها `Seed.cs`،
# بقي العطل غير مرئي: الحلّ يُترجَم، والاختبارات خضراء، والقاعدة 15 راضية لأن
# المشروع **مبنيّ**. وأول تشغيل حقيقي رمى عند حقن الاعتماديات. ثم — وهذا الأخبث —
# تشغيلٌ لاحق **تخطّى البذر** لأن البيانات مبذورة سلفاً، فقرأ نتائج قديمة وأعلن
# نجاحاً. ‏**ثلاث طبقات خضراء فوق عطلٍ يمنع كل عميل جديد من الإقلاع.**
# والقاعدة 15 تضمن أن ما يُدَّعى تغطيته **يُبنى**؛ وهذه تضمن أن ما لا يكفيه البناء
# **يُشغَّل**. والقاعدة 19 تحرس التصريح بأنها ليست افتراضية.
if [ "$demo" = "yes" ]; then
    step "٨ · الشركة التجريبية — من الصفر"
    command -v psql >/dev/null 2>&1 || fail "psql غير موجود و--with-demo يطلبه"
    pg_isready >/dev/null 2>&1 || fail "PostgreSQL متوقّف — شغّله: pg_ctlcluster 16 main start"

    # الأسماء تُملى هنا ولا تُخمَّن: هذه الخطوة تُسقط قواعد، و`DROP … IF EXISTS`
    # على اسمٍ خاطئ **ينجح** فيُثبت لا شيء ويبدو أنه أثبت. فتُصدَّر الأسماء نفسها
    # التي سيقرأها البنّاء، ثم تُسقط بها، ثم يُشغَّل عليها.
    #
    # ‏**والقائمة تسعٌ لا خمس — والنقص كان يجعل «من الصفر» جملةً كاذبة.** الشركة
    # التجريبية تزوّد **تسع** قواعد (‏ADR-0060 · `demo/company/ModuleDatabases.cs`)، وكانت
    # هذه الخطوة تُصدّر خمساً وتُسقط خمساً. فالأربع الباقيات — العقارات والمشاريع والموارد
    # البشرية والمرفقات — كنّ يرتددن إلى أسمائهنّ **الافتراضية غير المخصَّصة للعرض**
    # (‏`babel_realestate` …) فلا تُسقطن ولا يُعزلن عن بقية ما على الجهاز. وأثرُ ذلك
    # **مقيس**: تشغيلتان متتاليتان ⇒ الثانية تسقط، لأن العقارات تبقى مبذورةً من الأولى
    # فيُتخطّى بذرُها بينما الدفتر يُعاد بناؤه فارغاً — فتقول المطابقة «نقطة الضبط=0.0000
    # والدفتر المساعد=60,100.0000». **والرسالة تتّهم المطابقة، والجاني قائمةُ الإسقاط.**
    # وبعد إتمام القائمة: تشغيلتان متتاليتان خضراوان بانحراف 0.0000 في كلتيهما (مقيس).
    # (‏docs/evidence/traps.md#fakh-150)
    export BABEL_LEDGER_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_ledger;Username=postgres;Include Error Detail=true"
    export BABEL_LEDGER_APP_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_ledger;Username=babel_ledger_app;Include Error Detail=true"
    export BABEL_SALES_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_sales;Username=postgres;Include Error Detail=true"
    export BABEL_PURCHASING_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_purchasing;Username=postgres;Include Error Detail=true"
    export BABEL_INVENTORY_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_inventory;Username=postgres;Include Error Detail=true"
    export BABEL_CORE_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_core;Username=postgres;Include Error Detail=true"
    export BABEL_CORE_APP_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_core;Username=babel_ledger_app;Include Error Detail=true"
    export BABEL_REALESTATE_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_realestate;Username=postgres;Include Error Detail=true"
    export BABEL_PROJECTS_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_projects;Username=postgres;Include Error Detail=true"
    export BABEL_HR_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_hr;Username=postgres;Include Error Detail=true"
    export BABEL_STORAGE_OWNER_DB="Host=127.0.0.1;Port=5432;Database=babel_demo_storage;Username=postgres;Include Error Detail=true"

    for database in babel_demo_ledger babel_demo_sales babel_demo_purchasing \
                    babel_demo_core babel_demo_inventory babel_demo_realestate \
                    babel_demo_projects babel_demo_hr babel_demo_storage
    do
        psql "host=127.0.0.1 port=5432 dbname=postgres user=postgres" \
             -Atc "DROP DATABASE IF EXISTS \"$database\" WITH (FORCE)" >/dev/null \
            || fail "إسقاط قاعدة العرض $database"
    done
    printf '   · أُسقطت قواعد العرض التسع — البناء من الصفر لا من بقايا\n'

    dotnet demo/company/bin/"$configuration"/net10.0/BabelDemoCompany.dll all \
        || fail "بناء الشركة التجريبية من الصفر"
else
    printf '\n── الشركة التجريبية لم تُبنَ من الصفر. أضف --with-demo (وهي تُسقط قواعد العرض).\n'
fi

printf '\n✔ البوّابة المحلية خضراء: بناء الحلّ كلّه + المسابر + الحدود + الاختبارات + الواجهة الساكنة'
[ "$isolation" = "yes" ] && printf ' + العزل'
printf '\n'
