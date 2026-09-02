#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# جدول حالات **مناوبة الشهادة** — يُشغَّل، لا يُقال.
#
# وهو على شكل `tls-mode-check.sh` بجانبه وللسبب نفسه: جدولٌ لا يُشغَّل إلا على
# عدّاء بناء لا يُجرَّب قبل الدفع، فيُدفَع معطوباً. هذا يعمل بأمر واحد على أي جهاز:
#
#   deploy/rotation-check.sh
#
# **وما يُثبته ليس أن سطر التركيب موجود، بل أن منطق التركيب يعمل:** يُشغَّل
# `remote.sh rotation` **نفسه** — لا نسخة ثانية منه — داخل صندوق رمل: نسخةٌ من
# السكربت في مجلّد مؤقّت، و`.env` مصنوع، ومسار وحدات systemd مُحوَّل إلى الصندوق
# بـ`BABEL_ROTATION_UNIT_DIR`، وبدائل تنفيذية لـ`systemctl` و`crontab` و`pgrep`
# و`id` و`sudo` على رأس `PATH` تُسجّل ما نُودي به وتُجيب كما يُطلب منها.
#
# ولا حالة واحدة هنا تلمس `/etc` ولا cron الحقيقي ولا خادماً.
#
# **وكل حالة قادرة على السقوط.** ثلاث منها موجودة تحديداً لأن نجاح الأمر لا
# يعني نجاح المهمّة:
#   · `enable` نجح و`list-timers` لا يعرف الوحدة  ⇒ يجب أن يسقط.
#   · سطر cron مكتوب ولا عفريت cron يعمل          ⇒ يجب أن يسقط.
#   · تركيبٌ ثانٍ يُضاعف السطر                     ⇒ يجب أن يسقط.
#
# **وملاحظة على الفحص بالوجود:** `systemd_usable` و`cron_usable` في `remote.sh`
# يفحصان **بعملية** لا بـ`command -v` — ولهذا يتساوى هنا «الأمر غير موجود» مع
# «الأمر موجود ولا يعمل»: كلاهما فرعٌ واحد، وهو المقصود. صورةٌ فيها `systemctl`
# ولم تُقلَع بـsystemd هي الحالة التي تهزم الفحص بالوجود.
# ═══════════════════════════════════════════════════════════════════════════
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pass=0
failed=0
box=""
out=""
code=0

note() { printf '%s\n' "$*"; }

ok()   { pass=$((pass + 1));     printf '✔ %s\n' "$1"; }
bad()  { failed=$((failed + 1)); printf '✗ %s — %s\n' "$1" "$2"; }

# ── صندوق الرمل ─────────────────────────────────────────────────────────────
box_new() { # box_new <وضع> <موقع> [اسم مجلّد]
    local mode="$1" site="$2" dirname="${3:-pkg}"
    box="$(mktemp -d)"
    mkdir -p "$box/$dirname" "$box/bin" "$box/units" "$box/state"
    boxpkg="$box/$dirname"
    cp "$here/remote.sh" "$boxpkg/remote.sh"
    chmod +x "$boxpkg/remote.sh"
    printf 'BABEL_IMAGE_TAG=ci\nBABEL_TLS_MODE=%s\nBABEL_SITE=%s\n' "$mode" "$site" > "$boxpkg/.env"

    cat > "$box/bin/systemctl" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_STATE/systemctl.log"
[ "${STUB_SYSTEMD:-ok}" = "ok" ] || exit 1
case "${1:-}" in
  list-timers)
    if [ -f "$STUB_STATE/enabled" ] && [ "${STUB_FORGET:-0}" != "1" ]; then
      printf 'NEXT LEFT LAST PASSED UNIT ACTIVATES\n'
      printf '— — — — babel-certs.timer babel-certs.service\n'
    fi
    exit 0 ;;
  daemon-reload|--version) exit 0 ;;
  enable)     : > "$STUB_STATE/enabled" ; exit 0 ;;
  disable)    rm -f "$STUB_STATE/enabled" ; exit 0 ;;
  is-enabled) [ -f "$STUB_STATE/enabled" ] && exit 0 ; exit 1 ;;
  is-active)
    [ -f "$STUB_STATE/enabled" ] || exit 1
    [ "${STUB_INACTIVE:-0}" = "1" ] && exit 1
    exit 0 ;;
esac
exit 0
STUB

    cat > "$box/bin/crontab" <<'STUB'
#!/usr/bin/env bash
[ "${STUB_CRON_BIN:-yes}" = "yes" ] || { echo "crontab: not found" >&2; exit 127; }
f="$STUB_STATE/crontab"
case "${1:-}" in
  -l) [ -f "$f" ] || exit 1; cat "$f" ;;
  -r) rm -f "$f" ;;
  -)  cat > "$f" ;;
  "") exit 1 ;;
  *)  cat "$1" > "$f" ;;
esac
STUB

    cat > "$box/bin/pgrep" <<'STUB'
#!/usr/bin/env bash
[ "${STUB_CRON_DAEMON:-0}" = "1" ] || exit 1
case "${2:-}" in cron|crond) exit 0 ;; esac
exit 1
STUB

    cat > "$box/bin/id" <<'STUB'
#!/usr/bin/env bash
case "${1:-}" in
  -u)  printf '%s\n' "${STUB_UID:-0}" ;;
  -un) printf '%s\n' "${STUB_USER:-babel}" ;;
  *)   exit 2 ;;
esac
STUB

    cat > "$box/bin/sudo" <<'STUB'
#!/usr/bin/env bash
[ "${STUB_SUDO:-no}" = "yes" ] || exit 1
[ "${1:-}" = "-n" ] && shift
exec "$@"
STUB

    chmod +x "$box/bin"/*
}

box_run() { # box_run <أمر remote.sh> …
    out="$(
        cd "$boxpkg" && \
        PATH="$box/bin:$PATH" \
        STUB_STATE="$box/state" \
        BABEL_ROTATION_UNIT_DIR="$box/units" \
        STUB_SYSTEMD="${SYSTEMD:-ok}" \
        STUB_FORGET="${FORGET:-0}" \
        STUB_INACTIVE="${INACTIVE:-0}" \
        STUB_CRON_BIN="${CRON_BIN:-yes}" \
        STUB_CRON_DAEMON="${CRON_DAEMON:-0}" \
        STUB_UID="${UID_:-0}" \
        STUB_SUDO="${SUDO_:-no}" \
        ./remote.sh "$@" 2>&1
    )"
    code=$?
}

box_drop() { [ -n "$box" ] && rm -rf "$box"; box=""; }

units_count()  { ls -1 "$box/units" 2>/dev/null | wc -l | tr -d ' '; }
# ‏`grep -c` يطبع «0» **ويخرج بـ1** حين لا يطابق شيئاً، فـ`|| echo 0` يطبع صفراً ثانياً
# ويصير الناتج سطرين. تُقرأ القيمة من أول سطر، ويُعوَّض الفراغ (ملفّ غير موجود).
cron_markers() {
    local n
    n="$(grep -c 'babel-certs-rotation' "$box/state/crontab" 2>/dev/null | head -1)"
    printf '%s' "${n:-0}"
}

# ‏**كل مقبض يُعاد إلى وضعه قبل كل حالة.** إسنادٌ في سطر مستقلّ يبقى في القشرة،
# فحالةٌ تُطفئ cron تُسقط الحالات بعدها لسببٍ لا يخصّها — وحارسٌ يسقط لسبب لا
# يخصّه يُدرَّب الناس على تجاهله.
knobs() {
    SYSTEMD=ok; FORGET=0; INACTIVE=0
    CRON_BIN=yes; CRON_DAEMON=0
    UID_=0; SUDO_=no
}
knobs

# ── مُدقِّقات مركّبة ─────────────────────────────────────────────────────────
want_code() { # want_code <وصف> <المتوقّع>
    [ "$code" = "$2" ] && return 0
    bad "$1" "الخروج $code والمتوقّع $2 — المُخرَج: $out"
    return 1
}
want_says() { # want_says <وصف> <مقتطف>
    case "$out" in *"$2"*) return 0 ;; esac
    bad "$1" "الرسالة لا تسمّي «$2» — المُخرَج: $out"
    return 1
}
want_eq() { # want_eq <وصف> <ما هو> <القيمة> <المتوقّع>
    [ "$3" = "$4" ] && return 0
    bad "$1" "$2 = $3 والمتوقّع $4"
    return 1
}

# ═══════════════════════════════════════════════════════════════════════════
note "── التركيب يقع، وبأي آلة ولماذا"

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=0
box_new ip "https://203.0.113.10"; box_run rotation
d="ip بـsystemd وصلاحية جذر ⇒ مؤقّت نظامي"
want_code "$d" 0 && want_says "$d" "مؤقّت systemd" \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 2 \
  && want_says "$d" "✔ المناوبة مسجَّلة" && ok "$d"
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=1
box_new ip "https://203.0.113.10"; box_run rotation
d="‏systemctl موجود ولم يُقلَع النظام به ⇒ سطر cron"
want_code "$d" 0 && want_says "$d" "سطر cron" \
  && want_eq "$d" "سطور العلامة" "$(cron_markers)" 1 \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 0 && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=1000 SUDO_=no CRON_DAEMON=1
box_new ip "https://203.0.113.10"; box_run rotation
d="مستخدم نشر غير جذر بلا sudo ⇒ سطر cron، والسبب مُعلَن"
want_code "$d" 0 && want_says "$d" "لا صلاحية لهذا المستخدم" \
  && want_says "$d" "سطر cron" && want_eq "$d" "سطور العلامة" "$(cron_markers)" 1 && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=1000 SUDO_=yes CRON_DAEMON=0
box_new ip "https://203.0.113.10"; box_run rotation
d="غير جذر ومعه sudo -n ⇒ مؤقّت نظامي لا cron"
want_code "$d" 0 && want_says "$d" "مؤقّت systemd" \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 2 && ok "$d"
box_drop

# ═══════════════════════════════════════════════════════════════════════════
note ""
note "── مُمَكِّنٌ مرّتين: التشغيل الثاني لا يُضاعف ولا يُنشئ ثانيةً"

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=0
box_new ip "https://203.0.113.10"; box_run rotation; box_run rotation
d="تركيب systemd مرّتين ⇒ وحدتان لا أربع"
want_code "$d" 0 && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 2 && ok "$d"
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=1
box_new ip "https://203.0.113.10"; box_run rotation; box_run rotation; box_run rotation
d="تركيب cron ثلاث مرّات ⇒ سطرٌ واحد بالعلامة"
want_code "$d" 0 && want_eq "$d" "سطور العلامة" "$(cron_markers)" 1 && ok "$d"
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=1
box_new ip "https://203.0.113.10"; box_run rotation
crontab_before="$(cat "$box/state/crontab")"
knobs; SYSTEMD=ok
box_run rotation
d="الانتقال من cron إلى systemd ينزع سطر cron — مصدران للحقيقة ينحرفان"
want_code "$d" 0 && want_eq "$d" "سطور العلامة بعد الانتقال" "$(cron_markers)" 0 \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 2 && ok "$d"
unset crontab_before
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=1
box_new ip "https://203.0.113.10"
printf '%s\n' "0 3 * * * /usr/local/bin/نسخة-احتياطية" > "$box/state/crontab"
box_run rotation
d="سطور المستخدم القائمة في crontab لا تُمسّ"
want_code "$d" 0 && want_eq "$d" "سطور المستخدم الباقية" \
  "$(grep -c 'نسخة-احتياطية' "$box/state/crontab")" 1 && ok "$d"
box_drop

# ═══════════════════════════════════════════════════════════════════════════
note ""
note "── الإخفاق يُقال بصوت، ولا يُهبَط إليه بصمت"

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=0 CRON_BIN=yes
box_new ip "https://203.0.113.10"; box_run rotation
d="لا systemd ولا عفريت cron ⇒ **إخفاق** يسمّي المخرجين"
want_code "$d" 1 && want_says "$d" "لا سبيل إلى تركيب المناوبة" \
  && want_says "$d" "sudo -n systemctl" && want_says "$d" "cron" \
  && want_says "$d" "160 ساعة" && ok "$d"
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=0 CRON_BIN=no
box_new ip "https://203.0.113.10"; box_run rotation
d="لا systemd ولا أمر crontab أصلاً ⇒ إخفاق لا صمت"
want_code "$d" 1 && want_says "$d" "لا سبيل إلى تركيب المناوبة" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0 FORGET=1
box_new ip "https://203.0.113.10"; box_run rotation
d="‏enable قال «تمّ» وlist-timers لا يعرف الوحدة ⇒ **يسقط**"
want_code "$d" 1 && want_says "$d" "لا يظهر في list-timers" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0 INACTIVE=1
box_new ip "https://203.0.113.10"; box_run rotation
d="المؤقّت مُمكَّن وغير نشط ⇒ يسقط"
want_code "$d" 1 && want_says "$d" "غير نشط" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=0
box_new ip "https://203.0.113.10" "مجلّد فيه فراغ"
box_run rotation
d="مسار نشر فيه فراغ ⇒ يُرفض بدل وحدة معطوبة بصمت"
want_code "$d" 1 && want_says "$d" "يحمل فراغاً" && ok "$d"
box_drop

# ═══════════════════════════════════════════════════════════════════════════
note ""
note "── الأوضاع التي لا شهادة عامّة فيها: لا يُركَّب مؤقّت يسقط أبداً بلا سبب"

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=1
box_new auto ":80"; box_run rotation
d="‏:80 (‏HTTP عارٍ) ⇒ تخطٍّ مُعلَّل، ولا مؤقّت ولا cron"
want_code "$d" 0 && want_says "$d" "غير لازمة" \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 0 \
  && want_eq "$d" "سطور العلامة" "$(cron_markers)" 0 && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=1
box_new internal "https://203.0.113.10"; box_run rotation
d="‏internal ⇒ تخطٍّ مُعلَّل، ولا مؤقّت"
want_code "$d" 0 && want_says "$d" "internal" \
  && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 0 && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0 CRON_DAEMON=0
box_new auto "demo.example.invalid"; box_run rotation
d="‏auto بنطاق ⇒ تُركَّب المناوبة (‏90 يوماً تحتاج مناوبة أيضاً)"
want_code "$d" 0 && want_eq "$d" "ملفّات الوحدات" "$(units_count)" 2 && ok "$d"
box_drop

# ═══════════════════════════════════════════════════════════════════════════
note ""
note "── الحالة: مسجَّلة؟ عملت مؤخّراً؟ وماذا قالت؟ — وسقوطُ أيّها سقوط"

knobs; SYSTEMD=ok UID_=0
box_new ip "https://203.0.113.10"; box_run rotation; box_run rotation-status
d="بعد تركيبٍ للتوّ ⇒ الحالة تمرّ"
want_code "$d" 0 && want_says "$d" "✔ المناوبة مسجَّلة، وتعمل" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0
box_new ip "https://203.0.113.10"; box_run rotation
sed -i "s|^ts=.*|ts=$(( $(date -u +%s) - 60*60*24 ))|" "$boxpkg/certs.state" 2>/dev/null
box_run rotation-status
d="آخر فحص قبل يوم والدورة 4 ساعات ⇒ **المؤقّت لا يعمل**، ويسقط"
want_code "$d" 1 && want_says "$d" "المؤقّت لا يعمل" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0
box_new ip "https://203.0.113.10"; box_run rotation
sed -i 's|^code=.*|code=1|; s|^summary=.*|summary=الباقي 31 ساعة، والحدّ 40.|' "$boxpkg/certs.state"
box_run rotation-status
d="آخر فحص سقط ⇒ الحالة تسقط وتنقل الخلاصة"
want_code "$d" 1 && want_says "$d" "آخر فحص سقط" && want_says "$d" "الباقي 31 ساعة" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0
box_new ip "https://203.0.113.10"; box_run rotation
rm -f "$box/units"/*; rm -f "$box/state/enabled"
box_run rotation-status
d="نُزعت الوحدة من تحت المناوبة ⇒ الحالة تسقط «لا مناوبة مسجَّلة»"
want_code "$d" 1 && want_says "$d" "لا مناوبة مسجَّلة" && ok "$d"
box_drop

knobs; SYSTEMD=unusable UID_=0 CRON_DAEMON=1
box_new ip "https://203.0.113.10"; box_run rotation
CRON_DAEMON=0
box_run rotation-status
d="سطر cron باقٍ والعفريت توقّف ⇒ الحالة تسقط «لا عفريت cron يعمل»"
want_code "$d" 1 && want_says "$d" "لا عفريت cron يعمل" && ok "$d"
box_drop

knobs; SYSTEMD=ok UID_=0
box_new auto ":80"; box_run rotation-status
d="‏:80 ⇒ الحالة تمرّ بتخطٍّ مُعلَّل ولا تطلب مؤقّتاً"
want_code "$d" 0 && want_says "$d" "غير لازمة" && ok "$d"
box_drop

# ═══════════════════════════════════════════════════════════════════════════
note ""
printf '══ نجح %d · سقط %d\n' "$pass" "$failed"
[ "$failed" -eq 0 ] || exit 1
