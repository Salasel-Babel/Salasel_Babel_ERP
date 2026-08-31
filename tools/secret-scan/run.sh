#!/usr/bin/env bash
# فحص الأسرار — ومعه شاهده الموجب
#
# ‏**لماذا شاهد موجب.** هذا المستودع سرّب فعلاً رمز وصول شخصياً من GitHub فوجب إبطاله،
# وكشف كلمة مرور جذرِ خادمٍ في لقطة شاشة فوجب تدويرها. والحارس الذي وُضع بعدها كان
# — إلى ما قبل هذا الملف — **يجد صفراً على كل تشغيل**، ولم يُثبَت قطّ أنه ينطق. وحارسٌ
# لا يجد شيئاً ولم يُثبَت أنه قادر على أن يجد **لا يُفرَّق عن حارس معطَّل**: «صفر» عنده
# جوابان لا واحد — «لا سرّ» و«لا فحص» — ولا شيء في المخرَج يميّزهما.
#
# فصار الفحص شقّين:
#   ‏١ · **اختبار ذاتي** على `positive-control.txt` — كل نمط في `patterns.txt` **يجب**
#        أن يطابق فيه. سقوط نمط واحد، أو حذف الملف، **يُفشل البناء**.
#   ‏٢ · **المسح الحقيقي** على كل ما يتعقّبه git، مستثنياً **مسار الشاهد وحده**.
#
# ‏**ولا إعفاء دليل هنا.** كان الحارس يستثني `docs/**` و`spikes/**` و`**/ci.yml`، فكان
# مفتاحٌ خاصّ يُودَع تحت `docs/` **غير مرئي له**. والاستثناء الباقي الوحيد سطرٌ واحد
# بمسار واحد كامل — لا نمط دليل — ومعه مُميِّز على مستوى السطر (`NOT-A-SECRET`) يُعلنه
# كاتبه صراحةً في الفرق. (‏docs/evidence/traps.md#fakh-a-guard-that-never-fires-cannot-be-told-from-a-broken-one)

set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

PATTERNS_FILE="tools/secret-scan/patterns.txt"
FIXTURE="tools/secret-scan/positive-control.txt"
MARKER="NOT-A-SECRET"

# الحدّ الأدنى لعدد الأنماط — حارس لافراغ. ملفُّ أنماط صار فارغاً يجعل كل ما تحته
# يمرّ خضراء بلا أن يفحص شيئاً، وهو عين العطل الذي يمنعه هذا الملف.
MINIMUM_PATTERNS=8

names=()
regexes=()
last_name=""
while IFS= read -r line; do
  case "$line" in
    # أول سطر تعليق في الفقرة هو الاسم؛ ما بعده شرحٌ لا اسم.
    '#'*) if [ -z "$last_name" ]; then last_name="${line###}"; last_name="${last_name# }"; fi ;;
    '') last_name="" ;;
    *) names+=("$last_name"); regexes+=("$line"); last_name="" ;;
  esac
done < "$PATTERNS_FILE"

if [ "${#regexes[@]}" -lt "$MINIMUM_PATTERNS" ]; then
  echo "::error::‏$PATTERNS_FILE يحمل ${#regexes[@]} نمطاً فقط والحدّ الأدنى $MINIMUM_PATTERNS — المسح ضامر، وخُضرته لا تعني شيئاً."
  exit 1
fi

failed=0

# ————————————————————————————————————————————————————————————————
# ‏١ · الاختبار الذاتي: الحارس يُثبت أنه ينطق قبل أن يُصدّق صمتُه
# ————————————————————————————————————————————————————————————————

if ! git ls-files --error-unmatch "$FIXTURE" >/dev/null 2>&1; then
  echo "::error::‏الشاهد الموجب $FIXTURE ليس متعقَّباً في git — لا شاهد، فلا شهادة. أعِده أو أعِد إيداعه."
  exit 1
fi

if grep -qF "$MARKER" "$FIXTURE"; then
  echo "::error::‏الشاهد الموجب $FIXTURE يحمل وسم الاستثناء «$MARKER» — فهو يُخرج نفسه من المسح، ويصير شاهداً لا يشهد."
  exit 1
fi

echo "‏— الاختبار الذاتي على $FIXTURE —"
for i in "${!regexes[@]}"; do
  hits="$(grep -cE -- "${regexes[$i]}" "$FIXTURE" || true)"
  if [ "${hits:-0}" -eq 0 ]; then
    echo "::error::‏النمط «${names[$i]}» لم يطابق شيئاً في الشاهد الموجب — الحارس عاجز عن التقاط ما يدّعي التقاطه."
    failed=1
  else
    printf '  ✓ %-32s %s مطابقة\n' "${names[$i]}" "$hits"
  fi
done

if [ "$failed" -ne 0 ]; then
  echo "::error::‏سقط الاختبار الذاتي لفحص الأسرار. لا يُقرأ «لم يُعثر على شيء» بعده على أنه براءة."
  exit 1
fi

# ————————————————————————————————————————————————————————————————
# ‏٢ · المسح الحقيقي: كل ما يتعقّبه git، إلا مسار الشاهد وحده
# ————————————————————————————————————————————————————————————————

echo "‏— المسح الحقيقي —"
found=""
for i in "${!regexes[@]}"; do
  hits="$(git grep -nIE -- "${regexes[$i]}" -- ":!$FIXTURE" | grep -vF "$MARKER" || true)"
  if [ -n "$hits" ]; then
    found="${found}[${names[$i]}]
${hits}
"
  fi
done

if [ -n "$found" ]; then
  printf '%s\n' "$found"
  echo "::error::‏يبدو أن هناك سرّاً في المستودع. CONTRIBUTING §3 بند 7."
  echo "::error::‏إن كان مثالاً توضيحياً لا قيمةً حقيقية، فضَع الوسم «$MARKER» على السطر نفسه — ولا تُعفِ دليلاً كاملاً."
  exit 1
fi

echo "‏لا سرّ في المحتوى المتعقَّب — والحارس أثبت في الشقّ الأول أنه ينطق، فهذا الصمت نتيجة لا عطل."
