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

while [ $# -gt 0 ]; do
    case "$1" in
        --configuration) configuration="${2:?--configuration يحتاج قيمة}"; shift 2 ;;
        --no-isolation)  isolation="no"; shift ;;
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

# ── ١ · البناء أولاً، وعلى **الحلّ كلّه** ──────────────────────────────────────
# ‏`Babel.slnx` لا مشروعاً ولا نمط مسارات: القاعدة 9 تضمن أن كل *.csproj على القرص
# فيه (عدا spikes/)، فالبناء عليه هو الجملة الوحيدة التي تعني «المستودع يُترجم».
step "١ · بناء الحلّ كلّه — $configuration · التحذير خطأ"
dotnet build Babel.slnx -c "$configuration" --nologo || fail "dotnet build Babel.slnx -c $configuration"

# ── ٢ · الحدود المعمارية منفصلةً ──────────────────────────────────────────────
# أولاً ووحدها كما في ci.yml: إن انكسر حدّ، تُقرأ رسالته فوراً لا بين مئات الأسطر.
step "٢ · الحدود المعمارية"
dotnet test --project tests/Babel.ArchitectureTests/Babel.ArchitectureTests.csproj \
    -c "$configuration" --no-build || fail "اختبارات المعمارية"

# ── ٣ · كل الاختبارات ─────────────────────────────────────────────────────────
# ‏`--no-build` ليس تسريعاً فحسب: الخطوة ١ هي التي بنت، وبدونها كانت هذه الخطوة
# ستمرّ على مشاريع لا تشير إليها الاختبارات **دون أن تبنيها** — وهو العطل نفسه.
step "٣ · كل الاختبارات"
dotnet test --solution Babel.slnx -c "$configuration" --no-build || fail "مجموعة الاختبارات"

# ── ٤ · مسح العزل ─────────────────────────────────────────────────────────────
if [ "$isolation" = "yes" ]; then
    step "٤ · مسح العزل — كل دالّة وحدها"
    tools/test-isolation/run.sh --grain method --configuration "$configuration" --jobs 4 \
        || fail "مسح العزل"
else
    printf '\n── مسح العزل مُتخطّى بـ--no-isolation. لا يُتخطّى قبل الدمج.\n'
fi

printf '\n✔ البوّابة المحلية خضراء: بناء الحلّ كلّه + الحدود + الاختبارات'
[ "$isolation" = "yes" ] && printf ' + العزل'
printf '\n'
