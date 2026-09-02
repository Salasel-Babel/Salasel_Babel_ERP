#!/usr/bin/env bash
# ‏**الحصيلة** — تُسأل عن مُخرَج التشغيل لا عن نصّ الأمر الذي أطلقه.
#
# الاستعمال:
#   tools/test-tally/run.sh --begin                       # يُفرّغ مجلّد التقارير ويختمه
#   tools/test-tally/run.sh --job ci.yml:build-and-enforce # يُحصي أسطح تلك الوظيفة
#   tools/test-tally/run.sh --surface web-unit             # سطحٌ بعينه
#
# ولماذا سكربتٌ رقيق فوق Node: التقارير XML وJSON، وNode موجود على مُشغّلات
# التكامل المستمر وفي بوّابة هذا المستودع أصلاً (‏tools/gate/run.sh §٦).
#
# الخروج: 0 إن أنتج كل سطحٍ مطلوب تقريراً عند أرضيته أو فوقها بصفر إخفاق · 1 وإلا.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"

if ! command -v node >/dev/null 2>&1; then
    printf '✗ node غير موجود، والحصيلة تحتاجه. وغيابُه نقصُ تغطية لا نجاح.\n' >&2
    printf '  · node is required to read the run reports; its absence is missing coverage, not a pass.\n' >&2
    exit 1
fi

exec node "$root/tools/test-tally/tally.mjs" "$@"
