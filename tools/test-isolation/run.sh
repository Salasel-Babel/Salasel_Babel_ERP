#!/usr/bin/env bash
# ‏**مسح العزل** — يُشغّل كل وحدة اختبار **وحدها في عمليتها**، ويقارن بنتيجة التشغيل الكامل.
# isolation sweep: every test unit alone, in its own process
#
# لماذا يوجد هذا الملف:
#   «‏687 اختباراً · 0 فشل» في تشغيل كامل ليست جملةً عن صحّة المجموعة، بل عن صحّتها
#   **بترتيب تنفيذ واحد**. اختبارٌ يقرأ حالةً كتبها اختبار آخر يمرّ ما دام الآخر سبقه،
#   ويسقط عند أوّل إعادة تسمية، أو إضافة اختبار، أو تقسيم للمجموعة على عدّة عاملين.
#   وقع هذا فعلاً في tests/Babel.Api.Tests/MoneyOnTheWireTests.cs
#   (‏docs/evidence/traps.md#fakh-green-by-ordering-not-by-construction).
#
# الاستعمال:
#   tools/test-isolation/run.sh [--grain method|class] [--configuration Release] [--jobs N]
#   tools/test-isolation/run.sh --assembly Babel.Api.Tests      # أثناء التطوير فقط
#
# ‏⚠️ **‏`--jobs` أكبر من 1 غير آمن اليوم**، ولا يُستعمل في التكامل المستمرّ: مجموعتان
#    ما زالتا تعملان على قواعد بأسماء ثابتة وتُسقطانها عند التهيئة
#    (‏`Babel.Canonicalization.Tests` و`Babel.Compliance.Tests`)، فعمليتان متزامنتان من
#    أيّهما تُدمّر إحداهما الأخرى فيظهر **فشل كاذب**. مقيس: 5 د 9 ث بأربع عمليات مقابل
#    16 د 52 ث تسلسلياً، وفشل كاذب واحد.
#    (‏docs/evidence/traps.md#fakh-test-databases-share-a-fixed-name-across-processes)
#
# الخروج: 0 إن مرّت كل وحدة وحدها · 1 إن سقطت واحدة · 2 إن كان **المسح نفسه** ضامراً.
set -uo pipefail

# ── عاملٌ يُشغّل وحدة واحدة؛ يُنادى من الملف نفسه ───────────────────────────────
if [ "${1:-}" = "--run-one" ]; then
    pair="$2"; flag="$3"; out="$4"
    exe="${pair%%$'\t'*}"; unit="${pair#*$'\t'}"
    log="$out/logs/$(printf '%s' "$exe|$unit" | md5sum | cut -c1-16).log"
    {
        printf '### %s %s %s\n' "$(basename "$exe")" "$flag" "$unit"
        "$exe" "$flag" "$unit" --no-ansi --progress off 2>&1
    } > "$log"
    total=$(grep -oE '^[[:space:]]*total:[[:space:]]*[0-9]+' "$log" | grep -oE '[0-9]+$' | tail -1)
    failed=$(grep -oE '^[[:space:]]*failed:[[:space:]]*[0-9]+' "$log" | grep -oE '[0-9]+$' | tail -1)
    printf '%s\t%s\t%s\t%s\t%s\n' "$(basename "$exe")" "$unit" "${total:-0}" "${failed:-1}" "$log" >> "$out/results.tsv"
    if [ "${failed:-1}" != "0" ]; then
        printf '  ✗ %s · %s  (اختبارات: %s · فشل: %s) → %s\n' \
            "$(basename "$exe")" "$unit" "${total:-؟}" "${failed:-؟}" "$log"
    fi
    exit 0
fi

set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
GRAIN=method
CONFIGURATION=Release
JOBS=1
TFM=net10.0
declare -a ONLY=()

while [ $# -gt 0 ]; do
    case "$1" in
        --grain) GRAIN="$2"; shift 2 ;;
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --jobs) JOBS="$2"; shift 2 ;;
        --assembly) ONLY+=("$2"); shift 2 ;;
        *) echo "وسيط غير معروف: $1" >&2; exit 2 ;;
    esac
done

case "$GRAIN" in
    method) FLAG=--filter-method ;;
    class)  FLAG=--filter-class ;;
    *) echo "‏--grain يقبل method أو class فقط" >&2; exit 2 ;;
esac

command -v jq > /dev/null || { echo "‏jq مطلوب لقراءة قائمة الاختبارات" >&2; exit 2; }

OUT="${BABEL_ISOLATION_OUT:-$ROOT/artifacts/test-isolation}"
rm -rf "$OUT"; mkdir -p "$OUT/logs"
: > "$OUT/results.tsv"

# ── ما الذي يُمسح: من الحلّ نفسه، لا من نمط مسارات ─────────────────────────────
# نمطُ مسارات (‏*.Tests) كان قد أسقط Babel.ArchitectureTests كاملاً — ستّاً وتسعين
# اختباراً — لأن اسمه لا يحمل نقطةً قبل Tests، فمرّ المسح «أخضر» وهو لا يرى سُبع
# المجموعة. المصدر الوحيد للحقيقة هو Babel.slnx، ومنه اسم التجميعة من كل ملف مشروع.
declare -a ASSEMBLIES=()
while read -r proj; do
    [ -z "$proj" ] && continue
    csproj="$ROOT/$proj"
    [ -f "$csproj" ] || { echo "::error::مشروع مذكور في Babel.slnx وغير موجود: $proj"; exit 2; }
    # ‏sed لا grep: ‏grep يخرج بـ1 حين لا يجد، و`set -e` مع `pipefail` يجعل ذلك
    # **إنهاءً صامتاً للسكربت** عند أوّل مشروع بلا <AssemblyName> — أي مسحٌ يتوقّف
    # في منتصفه ويُبلّغ لا شيء. وقع فعلاً أثناء كتابة هذا الملف.
    name=$(sed -nE 's#.*<AssemblyName>([^<]+)</AssemblyName>.*#\1#p' "$csproj" | head -1)
    [ -n "$name" ] || name=$(basename "$csproj" .csproj)
    case "$name" in *Tests) ASSEMBLIES+=("$(dirname "$proj")|$name") ;; esac
done < <(grep -oE 'Path="[^"]+\.csproj"' "$ROOT/Babel.slnx" | sed -E 's/Path="//; s/"//')

[ "${#ASSEMBLIES[@]}" -ge 10 ] \
    || { echo "::error::قراءة Babel.slnx أعطت ${#ASSEMBLIES[@]} تجميعة اختبار فقط — المسح ضامر"; exit 2; }

DECLARED=${#ASSEMBLIES[@]}

if [ "${#ONLY[@]}" -gt 0 ]; then
    declare -a PICKED=()
    for entry in "${ASSEMBLIES[@]}"; do
        for want in "${ONLY[@]}"; do
            [ "${entry##*|}" = "$want" ] && PICKED+=("$entry")
        done
    done
    [ "${#PICKED[@]}" -gt 0 ] || { echo "::error::‏--assembly لم يطابق تجميعة واحدة"; exit 2; }
    ASSEMBLIES=("${PICKED[@]}")
    cat >&2 <<'WARN'
╔══════════════════════════════════════════════════════════════════════════╗
║  ⚠️  مسحٌ مُرشَّح — أداة تطوير، وليس حكماً على المجموعة.                  ║
║  «أخضر» هنا لا يعني شيئاً عن التجميعات التي لم تُمسح. لا يُستشهد به في    ║
║  تقرير ولا يُبنى عليه قرار دمج: تقريرٌ أعلن ملفّاً أخضر من تشغيل مُرشَّح  ║
║  بينما التشغيل الكامل أحمر كلّف هذا المستودع أياماً من كسرٍ غير مرئي.     ║
╚══════════════════════════════════════════════════════════════════════════╝
WARN
fi

echo "تجميعات الاختبار في Babel.slnx : $DECLARED · تُمسَح الآن: ${#ASSEMBLIES[@]}"
echo "الحبيبة $GRAIN · التهيئة $CONFIGURATION · التوازي $JOBS"
echo

# ── قائمة الوحدات ─────────────────────────────────────────────────────────────
: > "$OUT/units.tsv"
DISCOVERED=0
for entry in "${ASSEMBLIES[@]}"; do
    dir="${entry%%|*}"; name="${entry##*|}"
    exe="$ROOT/$dir/bin/$CONFIGURATION/$TFM/$name"
    [ -x "$exe" ] || { echo "::error::ثنائي الاختبار غير موجود: $exe — ابنِ الحلّ أولاً"; exit 2; }

    listing="$OUT/$name.tests.json"
    "$exe" --list-tests json > "$listing"
    count=$(jq '.tests | length' "$listing")
    [ "$count" -ge 1 ] || { echo "::error::$name لم يُعلن اختباراً واحداً — الاكتشاف معطوب"; exit 2; }
    DISCOVERED=$((DISCOVERED + count))

    if [ "$GRAIN" = method ]; then
        jq -r --arg e "$exe" '.tests[] | [$e, (.type.namespace + "." + .type.typeName + "." + .type.methodName)] | @tsv' "$listing"
    else
        jq -r --arg e "$exe" '.tests[] | [$e, (.type.namespace + "." + .type.typeName)] | @tsv' "$listing"
    fi | sort -u >> "$OUT/units.tsv"
done

UNITS=$(wc -l < "$OUT/units.tsv")
echo "وحدات تُشغَّل وحدها : $UNITS"
echo "اختبارات مُكتشَفة   : $DISCOVERED"
echo

# ── التشغيل ───────────────────────────────────────────────────────────────────
if [ "$JOBS" -le 1 ]; then
    while IFS= read -r pair; do
        "$0" --run-one "$pair" "$FLAG" "$OUT"
    done < "$OUT/units.tsv"
else
    xargs -d '\n' -P "$JOBS" -I{} "$0" --run-one {} "$FLAG" "$OUT" < "$OUT/units.tsv"
fi

# ── الحكم ─────────────────────────────────────────────────────────────────────
RAN=$(wc -l < "$OUT/results.tsv")
SEEN=$(awk -F'\t' '{s+=$3} END {print s+0}' "$OUT/results.tsv")
FAILED=$(awk -F'\t' '$4 != "0"' "$OUT/results.tsv" | wc -l)

echo
echo "────────────────────────────────────────────────"
echo "وحدات شُغِّلت وحدها : $RAN من $UNITS"
echo "اختبارات نُفِّذت    : $SEEN من $DISCOVERED"
echo "وحدات ساقطة       : $FAILED"
echo "────────────────────────────────────────────────"

# حارس اللافراغ: مسحٌ لا يُشغّل ما اكتشفه «يمرّ» دائماً — وهو عطل فخ-43 بعينه.
if [ "$RAN" != "$UNITS" ] || [ "$SEEN" != "$DISCOVERED" ]; then
    echo "::error::المسح لم يُنفّذ كل ما اكتشفه — نتيجته لا تعني شيئاً، ولا تُقرأ نجاحاً."
    exit 2
fi

if [ "$FAILED" != "0" ]; then
    cat <<'MSG'

::error::اختبارات تمرّ مع الجماعة وتسقط وحدها — أي أنها تقرأ حالةً كتبها اختبار آخر.
المجموعة خضراء **بترتيب تنفيذ واحد** لا ببنائها: أوّل إعادة تسمية أو إضافة اختبار أو
تقسيم للمجموعة على عدّة عاملين يقلبها حمراء. كل اختبار يُنشئ الحالة التي يفحصها.
راجع docs/evidence/traps.md#fakh-green-by-ordering-not-by-construction
MSG
    exit 1
fi

if [ "${#ONLY[@]}" -gt 0 ]; then
    echo "كل وحدة **في التجميعات المُرشَّحة** تمرّ وحدها. وهذا ليس حكماً على المجموعة."
else
    echo "كل وحدة تمرّ وحدها كما تمرّ مع الجماعة."
fi
