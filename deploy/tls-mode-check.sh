#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# جدول حالات `tls-mode.sh` — **يُشغَّل، لا يُقال**.
#
# ولماذا جدولٌ في ملفّ لا خطواتٌ في YAML: الجدول الذي لا يُشغَّل إلا على عدّاء
# بناء لا يُجرَّب قبل الدفع، فيُدفَع معطوباً. هذا يعمل بأمر واحد على أي جهاز:
#
#   deploy/tls-mode-check.sh
#
# وكل سطر هنا **حالة رفض أو قبول بعينها**، ولا واحدة منها تُجرَّب على الخادم:
# قيمة الحارس كلّها في أن الرفض يقع **قبل** أن تُوقَف الحزمة القائمة.
# ═══════════════════════════════════════════════════════════════════════════
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
subject="$here/tls-mode.sh"

pass=0
failed=0

# case <وصف> <الخروج المتوقّع> <المُخرَج المتوقّع أو -> <مقتطف من الرسالة أو ->  الوضع الموقع البريد
case_is() {
    local desc="$1" want_code="$2" want_out="$3" want_msg="$4" mode="$5" site="$6" email="$7"
    local out code
    out="$(BABEL_TLS_MODE="$mode" BABEL_SITE="$site" BABEL_TLS_EMAIL="$email" "$subject" 2>&1)"
    code=$?

    local problem=""
    [ "$code" = "$want_code" ] || problem="الخروج $code والمتوقّع $want_code"
    if [ -z "$problem" ] && [ "$want_out" != "-" ]; then
        [ "$out" = "$want_out" ] || problem="المُخرَج «$out» والمتوقّع «$want_out»"
    fi
    if [ -z "$problem" ] && [ "$want_msg" != "-" ]; then
        case "$out" in
            *"$want_msg"*) ;;
            *) problem="الرسالة لا تسمّي «$want_msg»: $out" ;;
        esac
    fi

    if [ -n "$problem" ]; then
        failed=$((failed + 1))
        printf '✗ %s — %s\n' "$desc" "$problem"
    else
        pass=$((pass + 1))
        printf '✔ %s\n' "$desc"
    fi
}

echo "── الأوضاع الثلاثة السليمة، ولا يُشتقّ عنوان إلا في واحد منها"
case_is "auto بنطاق وبريد"            0 ""            -                          auto     "demo.example.invalid"      "ops@example.invalid"
case_is "auto على :80"                0 ""            -                          auto     ":80"                       ""
case_is "internal بعنوان"             0 ""            -                          internal "https://203.0.113.10"      ""
case_is "ip بعنوان IPv4 وبريد"        0 "203.0.113.10" -                          ip       "https://203.0.113.10"      "ops@example.invalid"
case_is "ip بعنوان IPv6 وبريد"        0 "2001:db8::1"  -                          ip       "https://[2001:db8::1]"     "ops@example.invalid"

echo
echo "── الهبوط الصامت: هذا هو سبب وجود الحارس"
case_is "auto بعنوان IPv4 يُرفض"      1 -             "غير موثوقة"                auto     "https://203.0.113.10"      "ops@example.invalid"
case_is "auto بعنوان IPv6 يُرفض"      1 -             "غير موثوقة"                auto     "https://[2001:db8::1]"     "ops@example.invalid"

echo
echo "── شروط وضع ip الأربعة، وكلٌّ منها يُرفض باسمه"
case_is "ip بنطاق يُرفض"              1 -             "عنواناً عارياً"            ip       "demo.example.invalid"      "ops@example.invalid"
case_is "ip بلا مخطّط يُرفض"           1 -             "https://"                 ip       "203.0.113.10"              "ops@example.invalid"
case_is "ip على :80 يُرفض"            1 -             "عنواناً عارياً"            ip       ":80"                       "ops@example.invalid"
case_is "ip بلا بريد يُرفض"           1 -             "BABEL_TLS_EMAIL"           ip       "https://203.0.113.10"      ""
case_is "ip على 10.0.0.5 يُرفض"       1 -             "غير عامّ"                  ip       "https://10.0.0.5"          "ops@example.invalid"
case_is "ip على 192.168.1.9 يُرفض"    1 -             "غير عامّ"                  ip       "https://192.168.1.9"       "ops@example.invalid"
case_is "ip على 172.20.0.1 يُرفض"     1 -             "غير عامّ"                  ip       "https://172.20.0.1"        "ops@example.invalid"
case_is "ip على 127.0.0.1 يُرفض"      1 -             "غير عامّ"                  ip       "https://127.0.0.1"         "ops@example.invalid"
case_is "ip على fd00::1 يُرفض"        1 -             "غير عامّ"                  ip       "https://[fd00::1]"         "ops@example.invalid"
case_is "ip على 172.15.0.1 يمرّ"      0 "172.15.0.1"  -                           ip       "https://172.15.0.1"        "ops@example.invalid"

echo
echo "── ما بقي من الحراسة القائمة"
case_is "auto بنطاق بلا بريد يُرفض"   1 -             "BABEL_TLS_EMAIL"           auto     "demo.example.invalid"      ""
case_is "وضع مجهول يُرفض"             1 -             "ليس وضعاً معروفاً"          selfsign "https://203.0.113.10"      "ops@example.invalid"
case_is "موقع فارغ يُرفض"             1 -             "BABEL_SITE"                auto     ""                          "ops@example.invalid"

echo
printf '══ نجح %d · سقط %d\n' "$pass" "$failed"

# حارس لافراغ: جدولٌ ذبلت حالاته يخرج بـ«نجح 0 · سقط 0» — وهي خُضرة لا تعني شيئاً.
# العدد مقيس (عشرون حالة)، والرسالة تسمّي العددين معاً.
minimum_cases=20
run=$((pass + failed))
if [ "$run" -lt "$minimum_cases" ]; then
    printf '::error::الجدول شغّل %d حالة والحدّ الأدنى %d — الجدول ضامر، وخُضرته لا تعني شيئاً.\n' "$run" "$minimum_cases"
    exit 1
fi

[ "$failed" -eq 0 ] || exit 1
