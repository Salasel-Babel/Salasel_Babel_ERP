#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# ما يعمل **على الخادم** — وهو الملف الوحيد الذي يعرف شكل النشر هناك.
#
# ولماذا ملفٌّ يُنسَخ لا سطورٌ داخل سير العمل: منطقُ نشرٍ مكتوبٌ داخل YAML لا
# يُشغَّل إلا بدفعة كاملة، فلا يُجرَّب ولا يُصلَح يدوياً عند الثانية صباحاً.
# وهذا الملف يعمل بيد إنسان على الخادم بالضبط كما يعمل بيد سير العمل.
#
#   remote.sh deploy <وسم>   يبدّل الوسم، يسحب، يقيم، يفحص، ويرجع عند الفشل
#   remote.sh rollback       يعود إلى الوسم السابق المحفوظ
#   remote.sh health         يفحص وحده
#   remote.sh certs [ساعات]  يقرأ الشهادة **كما تُقدَّم** ويسقط قبل انتهائها
#   remote.sh certs-run      ما يُشغّله المؤقّت: يفحص، ويكتب الحالة، ويُسمع الإخفاق
#   remote.sh rotation       يركّب المناوبة الدورية — **مُمَكِّنٌ مرّتين**، ويتحقّق أنها سُجّلت
#   remote.sh rotation-status يقرأ التسجيل وآخر نتيجة، ويسقط إن اختلّ أيّهما
#   remote.sh logs [خدمة]    يطبع السجلّ
#
# ولا اعتماد واحد في هذا الملف: كلّها في ‎.env‎ بجانبه، بصلاحية 600.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# ‏**المسار المطلق للسكربت نفسه، لا `$0`.** الرجوع التلقائي يستدعي هذا الملفّ ثانيةً،
# و`$0` هو المسار **كما كُتب في الاستدعاء**؛ والسكربت قد بدّل مجلّده بـ`cd` قبله. فمن
# ناداه بمسار نسبي — وهكذا يناديه حارس التكامل: `deploy/remote.sh deploy ci` — يحصل
# على «‏No such file or directory» بدل الرجوع، **في اللحظة التي يقع فيها الفشل بالضبط**.
self="$here/$(basename "${BASH_SOURCE[0]}")"
cd "$here"

compose() { docker compose --env-file "$here/.env" -f "$here/compose.yml" "$@"; }

# السحب يُتخطّى في موضع واحد فقط: التكامل المستمر، حيث الصور مبنيّة محلياً ولا
# سجلّ لسحبها منه. والمتغيّر معلن هنا لا مخبوء، وسببه أن **هذا السكربت نفسه** هو
# ما يُختبَر هناك — لا نسخة ثانية منه تنحرف عنه بصمت.
pull_if_possible() {
  if [ "${BABEL_SKIP_PULL:-0}" = "1" ]; then
    echo "── السحب متخطّى (BABEL_SKIP_PULL=1) — الصور محلية"
    return 0
  fi
  compose pull
}

set_tag() {
  local tag="$1"
  # الوسم يُكتب في .env بلا مسّ بقية السطور: البقية أسرار لا يجوز أن يعيد
  # هذا السكربت توليدها ولا أن يقرأها.
  if grep -q '^BABEL_IMAGE_TAG=' "$here/.env"; then
    sed -i "s|^BABEL_IMAGE_TAG=.*|BABEL_IMAGE_TAG=${tag}|" "$here/.env"
  else
    printf 'BABEL_IMAGE_TAG=%s\n' "$tag" >> "$here/.env"
  fi
}

current_tag() {
  sed -n 's|^BABEL_IMAGE_TAG=\(.*\)$|\1|p' "$here/.env" | head -1
}

# ‏`.env` يُقرأ بمفتاح واحد في كل مرّة، بالطريقة نفسها التي يُقرأ بها الوسم: هذا
# السكربت **لا يُصدِر** ملفّ الأسرار ولا يُسرّب قيمه إلى بيئة عملية ابنة.
env_value() {
  [ -f "$here/.env" ] || return 0
  sed -n "s|^$1=\(.*\)\$|\1|p" "$here/.env" | head -1
}

# جزء المضيف من BABEL_SITE، وفارغٌ إن كان الموقع عنوان استماع بلا مضيف (`:80`).
# و`:80` يُميَّز أولاً كي لا يُقرأ عنواناً — وهو التمييز نفسه في `tls-mode.sh`.
site_host() {
  local site="$1"
  [ -n "$site" ] || return 0
  [ "${site#:}" = "$site" ] || return 0
  site="${site#https://}"
  site="${site#http://}"
  printf '%s' "${site%%/*}"
}

# ═══ المناوبة الدورية ═══════════════════════════════════════════════════════
#
# **لماذا هي هنا لا في README:** شهادة عمرها 160 ساعة بلا بريد إنذار (‏Let's
# Encrypt أوقفت رسائل الانتهاء في 2025-06-04) تجعل المناوبة **شرط بقاء**، لا
# صيانةً. وخطوةٌ يدوية في وثيقة تُنسى مرّة واحدة فيسقط الموقع بعد ستّة أيّام
# ونصف بلا أن يعلم أحد. فالتركيب يقع **مع النشر** وبيد السكربت نفسه.
rotation_unit="babel-certs"
rotation_every_hours=4
rotation_cron_schedule="7 */4 * * *"
rotation_systemd_calendar="*-*-* 00/4:07:00"
# العلامة هي ما يجعل تركيب `cron` **مُمَكِّناً مرّتين**: كل سطر يحملها يُحذف قبل
# أن يُكتب سطرٌ واحد. وبدونها يتضاعف السطر مع كل نشرة — وهو العطل المعتاد.
rotation_marker="# babel-certs-rotation — يكتبه deploy/remote.sh rotation، لا تُحرَّره بيد"
rotation_state="$here/certs.state"
# مسار وحدات systemd. **المتغيّر موجود لجدول الحالات في `rotation-check.sh` وحده**
# كي يُشغَّل المنطق نفسه في صندوق رمل بلا لمس `/etc` — ولا يُضبط على خادم أبداً،
# ولذلك يُعلن عن نفسه بصوت حين يكون مضبوطاً.
rotation_unit_dir="${BABEL_ROTATION_UNIT_DIR:-/etc/systemd/system}"

# ── ما يُتحقَّق منه قبل أن يُعتمَد عليه ────────────────────────────────────────
# **وجود الأمر ليس دليل صلاحيته.** `systemctl` موجود في صور كثيرة لم تُقلَع
# بـsystemd أصلاً، و`crontab` يقبل الكتابة على خادم بلا عفريت cron يعمل — فيُقرأ
# التركيب ناجحاً ولا يعمل شيء أبداً. فيُجرَّب كلٌّ منهما **بعملية حقيقية**.
systemd_usable() { systemctl list-timers --no-pager >/dev/null 2>&1; }

systemd_privileged() {
  [ "$(id -u)" = "0" ] && return 0
  command -v sudo >/dev/null 2>&1 || return 1
  sudo -n systemctl --version >/dev/null 2>&1
}

sctl() {
  if [ "$(id -u)" = "0" ]; then systemctl "$@"; else sudo -n systemctl "$@"; fi
}

write_privileged_file() {
  if [ "$(id -u)" = "0" ]; then cat > "$1"; else sudo -n sh -c "cat > '$1'"; fi
}

cron_daemon_running() {
  if command -v pgrep >/dev/null 2>&1; then
    pgrep -x cron  >/dev/null 2>&1 && return 0
    pgrep -x crond >/dev/null 2>&1 && return 0
    return 1
  fi
  ps -eo comm= 2>/dev/null | grep -qxE 'cron|crond'
}

cron_usable() {
  command -v crontab >/dev/null 2>&1 || return 1
  cron_daemon_running
}

# هل تلزم المناوبة أصلاً؟ الوضعان اللذان لا شهادة عامّة فيهما لا يُركَّب لهما
# مؤقّت: مؤقّتٌ يسقط كل أربع ساعات لسببٍ لا علاقة له بما يحرسه **يدرّب الناس
# على تجاهله**، وذلك أسوأ من غيابه.
rotation_reason_to_skip() {
  local mode site
  mode="$(env_value BABEL_TLS_MODE)"
  mode="${mode:-auto}"
  site="$(env_value BABEL_SITE)"

  if [ "$mode" = "internal" ]; then
    printf '%s' "الوضع internal: الشهادة من سلطة Caddy الداخلية وتُجدَّد محلياً بلا سلطة خارجية"
    return 0
  fi
  if [ -z "$site" ]; then
    printf '%s' "‏BABEL_SITE غير مضبوط في .env — لا موقع تُقرأ شهادته"
    return 0
  fi
  if [ -z "$(site_host "$site")" ]; then
    printf '%s' "الموقع «$site» عنوان استماع بلا مضيف (‏HTTP عارٍ): لا شهادة تُجدَّد"
    return 0
  fi
  return 1
}

rotation_flavour() {
  if [ -f "$rotation_unit_dir/$rotation_unit.timer" ]; then
    printf 'systemd'
  elif command -v crontab >/dev/null 2>&1 && crontab -l 2>/dev/null | grep -qF -- "$rotation_marker"; then
    printf 'cron'
  else
    return 1
  fi
}

# **التحقّق بعد التركيب لا قبله.** الفرق بين «كتبتُ الملفّ» و«النظام سجّل المهمّة»
# هو الفرق بين حارسٍ يحرس وحارسٍ يُطمئن.
rotation_verify() {
  local flavour
  flavour="$(rotation_flavour)" || {
    echo "✘ لا مناوبة مسجَّلة: لا وحدة systemd ولا سطر cron يحمل العلامة." >&2
    return 1
  }

  case "$flavour" in
    systemd)
      if ! sctl is-enabled "$rotation_unit.timer" >/dev/null 2>&1; then
        echo "✘ المؤقّت $rotation_unit.timer موجود على القرص و**غير مُمكَّن** — لن يعمل بعد إعادة الإقلاع." >&2
        return 1
      fi
      if ! sctl is-active "$rotation_unit.timer" >/dev/null 2>&1; then
        echo "✘ المؤقّت $rotation_unit.timer مُمكَّن و**غير نشط الآن**." >&2
        return 1
      fi
      if ! sctl list-timers --all --no-pager 2>/dev/null | grep -qF "$rotation_unit.timer"; then
        echo "✘ المؤقّت $rotation_unit.timer لا يظهر في list-timers — أي أن systemd لا يعرف له موعداً." >&2
        return 1
      fi
      echo "✔ المناوبة مسجَّلة: مؤقّت systemd «$rotation_unit.timer» كل ${rotation_every_hours} ساعات"
      ;;
    cron)
      local count
      count="$(crontab -l 2>/dev/null | grep -cF -- "$rotation_marker" || true)"
      if [ "${count:-0}" != "1" ]; then
        echo "✘ سطور cron التي تحمل العلامة عددها ${count:-0} لا واحداً — التركيب تضاعف أو ضاع." >&2
        return 1
      fi
      if ! cron_daemon_running; then
        echo "✘ سطر cron مكتوب و**لا عفريت cron يعمل** — مكتوبٌ ولا يُشغَّل شيء أبداً." >&2
        return 1
      fi
      echo "✔ المناوبة مسجَّلة: سطر cron «$rotation_cron_schedule» وعفريت cron يعمل"
      ;;
  esac
  return 0
}

rotation_install() {
  local skip
  if skip="$(rotation_reason_to_skip)"; then
    echo "── المناوبة غير لازمة هنا: $skip"
    return 0
  fi

  # مسارٌ فيه فراغ يُنتج وحدة systemd أو سطر cron معطوباً **بصمت**. يُقال الآن.
  case "$here" in
    *[[:space:]]*)
      echo "✘ مسار النشر «$here» يحمل فراغاً، ولا تُركَّب عليه وحدة systemd ولا سطر cron سليم." >&2
      echo "  انقل الحزمة إلى مسار بلا فراغات (‏DEPLOY_PATH)." >&2
      return 1 ;;
  esac

  if [ "$rotation_unit_dir" != "/etc/systemd/system" ]; then
    echo "⚠ ‏BABEL_ROTATION_UNIT_DIR مضبوط على «$rotation_unit_dir» — هذا لجدول الحالات لا للخادم."
  fi

  # ── الاختيار: systemd أولاً، وسببه مكتوب في ADR-جديد (مناوبة الشهادة) ──────
  #   ١ · التركيب مُمَكِّن مرّتين ببنيته: ملفّان يُكتبان فوق نفسيهما وكيانٌ واحد
  #       يُمكَّن — لا سطرٌ يُلحَق فيتضاعف.
  #   ٢ · `Persistent=true` يُشغّل الموعد الفائت بعد انقطاع، وcron يتخطّاه صمتاً.
  #   ٣ · الإخفاق يصير **حالةً يقرؤها الآلة**: `is-failed` و`list-units --failed`،
  #       ومُخرَجه في اليوميّة باسم الوحدة. وcron يُرسل مُخرَجه بريداً محلياً —
  #       وعلى خادم بلا وكيل بريد **يُرمى المُخرَج كلّه**، وهو أسوأ سجلّ ممكن.
  # وcron يبقى بديلاً حقيقياً لا زينة: مستخدم النشر غير جذر بحكم README §2،
  # فإن لم يكن له `sudo -n` فلا سبيل إلى وحدة نظامية، وسطر cron يعمل بلا صلاحية.
  if systemd_usable && systemd_privileged; then
    echo "── تركيب المناوبة: مؤقّت systemd (نظامي)"
    write_privileged_file "$rotation_unit_dir/$rotation_unit.service" <<UNIT
[Unit]
Description=فحص شهادة عرض بابل — يسقط قبل انتهائها لا بعده
# ولا سطر Documentation: ‏deploy/README.md لا يُنسَخ إلى الخادم، وإشارةٌ إلى
# ملفّ غير موجود أسوأ من غيابها.

[Service]
Type=oneshot
User=$(id -un)
WorkingDirectory=$here
ExecStart=$here/remote.sh certs-run
UNIT

    write_privileged_file "$rotation_unit_dir/$rotation_unit.timer" <<UNIT
[Unit]
Description=مناوبة فحص شهادة عرض بابل كل ${rotation_every_hours} ساعات

[Timer]
OnCalendar=$rotation_systemd_calendar
Persistent=true
AccuracySec=1min
Unit=$rotation_unit.service

[Install]
WantedBy=timers.target
UNIT

    sctl daemon-reload
    sctl enable --now "$rotation_unit.timer" >/dev/null

    # وإن كان سطر cron من تركيبٍ أقدم باقياً، يُنزع: مصدران للحقيقة ينحرفان.
    if command -v crontab >/dev/null 2>&1 && crontab -l 2>/dev/null | grep -qF -- "$rotation_marker"; then
      echo "── نزع سطر cron القديم: المؤقّت النظامي هو المصدر الآن"
      crontab -l 2>/dev/null | grep -vF -- "$rotation_marker" | crontab - || true
    fi

  elif cron_usable; then
    if systemd_usable; then
      echo "── ‏systemd يعمل هنا ولا صلاحية لهذا المستخدم عليه (لا جذر ولا sudo بلا كلمة مرور)."
    fi
    echo "── تركيب المناوبة: سطر cron لمستخدم $(id -un)"
    local tmp
    tmp="$(mktemp)"
    { crontab -l 2>/dev/null | grep -vF -- "$rotation_marker" || true; } > "$tmp"
    printf '%s cd %s && ./remote.sh certs-run  %s\n' \
      "$rotation_cron_schedule" "$here" "$rotation_marker" >> "$tmp"
    crontab "$tmp"
    rm -f "$tmp"

  else
    # **لا هبوط صامت.** نشرةٌ تترك شهادة 160 ساعة بلا مناوبة ليست نشرةً ناجحة.
    echo "✘ لا سبيل إلى تركيب المناوبة على هذا الخادم: لا مؤقّت systemd ولا cron." >&2
    echo "  والشهادة عمرها 160 ساعة ولا بريد إنذار من Let's Encrypt — بلا مناوبة يسقط الموقع خلال أسبوع بلا إنذار." >&2
    echo "  المخرج أحدهما:" >&2
    echo "    · اجعل لمستخدم النشر «sudo -n systemctl» (وحدة نظامية، وهو المفضّل)، أو" >&2
    echo "    · ركّب عفريت cron وشغّله (‏apt install cron · systemctl enable --now cron)." >&2
    echo "  التفصيل في deploy/README.md §3.4." >&2
    return 1
  fi

  rotation_verify || return 1

  # خطُّ أساسٍ للحالة كي يعرف `rotation-status` متى رُكّبت، فلا يُقرأ ملفٌّ غائب
  # «المؤقّت لم يعمل» بينما لم يحن موعده الأول بعد.
  rotation_write_state 0 "رُكّبت المناوبة، ولم يحن موعد أول فحص بعد"
  return 0
}

rotation_write_state() {
  local code="$1" summary="$2"
  # ‏`umask` في قشرة فرعية: ضبطه في القشرة الأمّ يسري على كل ما بعده في السكربت.
  (
    umask 077
    {
      printf 'ts=%s\n' "$(date -u +%s)"
      printf 'code=%s\n' "$code"
      printf 'every_hours=%s\n' "$rotation_every_hours"
      printf 'summary=%s\n' "$(printf '%s' "$summary" | tr '\n' ' ')"
    } > "$rotation_state"
  )
}

state_value() {
  [ -f "$rotation_state" ] || return 0
  sed -n "s|^$1=\(.*\)\$|\1|p" "$rotation_state" | head -1
}

# **الإخفاق يجب أن يُسمَع.** ثلاث قنوات، ولا واحدة منها تُغني عن الأخرى:
#   ١ · المُخرَج نفسه — يلتقطه systemd في اليوميّة باسم الوحدة، وتصير الوحدة
#       `failed` فتظهر في `systemctl list-units --failed`.
#   ٢ · ‏syslog بوسم الوحدة ومستوى `daemon.err` — وهو ما يلتقطه أي ناقل سجلّات.
#   ٣ · صنبورٌ اختياري: ملفّ `certs-alert` تنفيذي بجانب الحزمة، يُعطى الرسالة
#       ويذهب بها حيث يقرأ إنسان (‏webhook، رسالة، أيّاً كان). ولا سرّ منه في
#       المستودع: الملفّ يعيش على الخادم وحده.
# وفوق الثلاثة: `.github/workflows/certs-watch.yml` يسأل الخادم على جدول من
# **خارجه**، وإخفاقه بريدٌ من GitHub إلى صاحب المستودع — وهي القناة الوحيدة
# التي تصل إنساناً بلا أن يفتح هو شيئاً.
sound_the_alarm() {
  local msg="$1"
  printf '%s\n' "$msg" >&2
  if command -v logger >/dev/null 2>&1; then
    logger -t "$rotation_unit" -p daemon.err -- "$msg" || true
  fi
  if [ -x "$here/certs-alert" ]; then
    "$here/certs-alert" "$msg" || echo "⚠ صنبور الإنذار $here/certs-alert سقط هو نفسه" >&2
  fi
}

# ═══ الفحص — وهو آخر بوّابة في «النشر أو الرجوع»، فلا يجوز أن يكون خاوياً ═══
#
# **العطل الذي أُصلح هنا، ومقيسٌ لا مُستنتَج:** كان الفحص
# `curl --fail http://127.0.0.1/health` وحده. وفي وضعَي `ip` و`auto` **لا يخدم
# المنفذ 80 التطبيق أصلاً**: Caddy يقيم عليه خادم تحويل إلى HTTPS، فيردّ
# `308` بجسمٍ فارغ. و`--fail` لا يسقط على 3xx، و`-L` غير ممرَّرة — فيخرج curl
# بصفر، ويطبع السكربت «✔ الخدمة تُجيب» **ولا خدمة خلف الحافة إطلاقاً**.
# مقيسٌ على caddy 2.11.4 وحده بلا أي حاوية أخرى: الأمر نفسه خرج بصفر.
# (‏docs/evidence/measurements.md §3.‏N · مناوبة الشهادة والحافة)
#
# فالفحص الآن يفعل شيئين لم يكن يفعلهما:
#   ١ · يذهب إلى **المسار الذي يسلكه الضيف** — 443 في أوضاع الشهادة، و80 في
#       وضع HTTP العاري وحده — بالاسم الذي يفتحه الضيف لا بـ`127.0.0.1`.
#   ٢ · **يقرأ الجسم**: لا يكفي رمز 200، بل يجب أن يُجيب التطبيق نفسه.
# والتمييز في الإخفاق مقصود: «الحافة لا تُقدّم» و«التطبيق لا يُجيب» عطلان
# مختلفان، والرجوع إلى وسمٍ سابق **لا يُصدر شهادة** — فلا يُطلَب على الأول.
health_body='"status":"ok"'

health_probe() {
  local site host
  site="$(env_value BABEL_SITE)"
  host="$(site_host "$site")"

  # ‏`--noproxy '*'` ليس زينة: بيئةٌ فيها `https_proxy` تجعل curl يمرّ بالوسيط
  # حتى إلى 127.0.0.1، فيُقاس الوسيط لا الحزمة — والفشل يُقرأ «الخدمة لا تُجيب».
  if [ -z "$host" ]; then
    # وضع HTTP العاري: الحافة تخدم على 80 مباشرة، وهو أيضاً وضع حارس التكامل.
    curl --fail --silent --show-error --noproxy '*' --max-time 5 http://127.0.0.1/health
    return
  fi

  # ‏`--resolve` يجعل الاسم يُحلّ إلى الحلقة المحلية: الطلب يحمل الترويسة
  # `Host` التي يحملها طلب الضيف، و**بلا SNI** إن كان الاسم عنواناً حرفياً —
  # وهو بالضبط ما يفعله المتصفّح (‏RFC 6066). و`--insecure` مقصودة: الثقة
  # شأن `certs`، وهذا الفحص عن التطبيق لا عن الشهادة.
  curl --fail --silent --show-error --insecure --noproxy '*' --max-time 5 \
       --resolve "$host:443:127.0.0.1" "https://$host/health"
}

# الأصل: الواجهة نفسها داخل الشبكة، بلا حافة ولا TLS. صورة الواجهة `nginx:alpine`
# وفيها `wget` من busybox — لا يُضاف شيء إلى الصورة من أجل الفحص.
health_origin() {
  compose exec -T web wget -q -O - http://127.0.0.1:8080/health 2>/dev/null
}

health() {
  local attempt body
  for attempt in $(seq 1 30); do
    body="$(health_probe 2>/dev/null || true)"
    case "$body" in
      *"$health_body"*)
        printf '%s\n' "$body"
        echo "✔ التطبيق يُجيب على /health من خلف الحافة، على المسار الذي يسلكه الضيف"
        return 0 ;;
    esac
    sleep 3
  done

  # سقط. أيّ الطرفين؟
  body="$(health_origin || true)"
  case "$body" in
    *"$health_body"*)
      echo "✘ التطبيق يُجيب على الأصل، و**الحافة لا تُقدّمه**." >&2
      echo "  والأرجح أن الشهادة لم تُصدَر بعد أو أن إصدارها أخفق — والرجوع إلى وسم سابق لا يُصدر شهادة." >&2
      echo "  اقرأ: ./remote.sh certs   ثم   ./remote.sh logs edge 200" >&2
      return 2 ;;
  esac

  echo "✘ لم يُجب /health بجسمٍ يحمل $health_body بعد 30 محاولة" >&2
  return 1
}

case "${1:-}" in
  deploy)
    tag="${2:?استعمال: remote.sh deploy <وسم>}"
    previous="$(current_tag || true)"

    if [ -n "$previous" ] && [ "$previous" != "$tag" ]; then
      printf '%s\n' "$previous" > "$here/previous.tag"
      echo "── الوسم السابق محفوظ للرجوع: $previous"
    fi

    set_tag "$tag"
    echo "── سحب الصور بالوسم $tag"
    pull_if_possible

    echo "── إقامة الحزمة"
    # ‏--wait ينتظر أن تصير كل خدمة **مُسمّاة** سليمة، فلا ينتهي النشر «بنجاح»
    # وحاويةٌ في دورة إعادة تشغيل.
    #
    # والخدمات مُسمّاة هنا عمداً، و migrator ليس منها: هو خدمة **تخرج**، وانتظارُ
    # «السلامة» من حاوية خرجت هو انتظارُ ما لا يقع. وترتيبه محفوظ رغم ذلك، لأن
    # api يعتمد عليه بـservice_completed_successfully — فإن فشل الترحيل لم يبدأ
    # الخادم أصلاً وفشل هذا الأمر.
    if ! compose up -d --wait --wait-timeout 300 --remove-orphans api web edge; then
      echo "✘ فشلت الإقامة — سجلّ الترحيل:" >&2
      compose logs --no-color --tail 200 migrator >&2 || true
      "$self" rollback || true
      exit 1
    fi

    set +e
    health
    health_code=$?
    set -e
    if [ "$health_code" = "2" ]; then
      # الحافة وحدها هي المعطوبة: الرجوع يعيد وسماً ولا يُصدر شهادة، فلا يقع.
      echo "✘ النشرة قائمة والتطبيق حيّ، والحافة لا تُقدّمه — لا رجوع تلقائي هنا." >&2
      compose logs --no-color --tail 200 edge >&2 || true
      exit 1
    fi
    if [ "$health_code" != "0" ]; then
      echo "✘ الحزمة أُقيمت ولا تُجيب — رجوع تلقائي" >&2
      compose logs --no-color --tail 200 api >&2 || true
      "$self" rollback || true
      exit 1
    fi

    # ── المناوبة تُركَّب بالنشر، لا بيد إنسان يتذكّر ────────────────────────────
    # وهي **بعد** الفحص عمداً: مؤقّتٌ يُركَّب على حزمة لم تقم بعدُ يقيس العدم.
    # وإخفاق التركيب **يُفشل النشرة** ولا يُرجعها: الحزمة تعمل، والناقص إنذارٌ
    # لا يستطيع أحد أن يعيش بدونه مع شهادة عمرها 160 ساعة. فيُقال بصوت.
    if ! rotation_install; then
      echo "✘ النشرة قائمة وتعمل، و**المناوبة لم تُركَّب** — deploy/README.md §3.4." >&2
      exit 1
    fi
    ;;

  rollback)
    if [ ! -f "$here/previous.tag" ]; then
      echo "✘ لا وسم سابق محفوظ — لا رجوع ممكن. أعِد النشر بوسم معلوم." >&2
      exit 1
    fi
    previous="$(cat "$here/previous.tag")"
    echo "── رجوع إلى $previous"
    set_tag "$previous"
    pull_if_possible
    compose up -d --wait --wait-timeout 300 --remove-orphans api web edge
    health
    ;;

  health)
    health
    ;;

  # ── الشهادة: مصدرها وعمرها الباقي ─────────────────────────────────────────
  #
  # **لماذا هذا الأمر موجود:** شهادة بعمر 160 ساعة تُجدَّد آلياً ما دام كل شيء
  # سليماً، و**تعطُّلُ التجديد لا يُصدر إشعاراً واحداً**: Let's Encrypt أوقفت
  # رسائل الانتهاء في 2025-06-04، وCaddy يكتب الإخفاق في سجلّه ولا أحد يقرؤه.
  # فالفارق بين «انتبهنا قبل يومين» و«العرض غداً والموقع لا يُفتح» هو أمرٌ
  # يُشغَّل على مناوبة — و**المناوبة تُركَّب بالنشر لا بيد إنسان**: `rotation`
  # أدناه، ويناديه `deploy`. لا سطر يدوي في README بعد اليوم.
  #
  # وهو يقرأ الشهادة **من المصافحة لا من القرص**: ما يُقاس هو ما يراه الضيف.
  # وبلا SNI عمداً، لأن المتصفّح الذي يفتح عنواناً حرفياً لا يرسل SNI أصلاً
  # (‏RFC 6066) — فهذا الأمر يفحص المسار نفسه الذي يفشل بلا `default_sni`.
  certs)
    # الحدّ يُشتقّ من **عمر الشهادة نفسها** حين لا يُمرَّر، ولا يُكتب رقماً ثابتاً:
    # الوضعان اللذان يُصدران شهادة عامّة يختلف عمرهما بأربعة عشر ضعفاً (160 ساعة
    # في وضع ip، ونحو 2160 في وضع auto)، ورقمٌ واحد يصلح لأحدهما يكون في الآخر
    # إمّا إنذاراً دائماً أو إنذاراً يصل بعد الموت. والاشتقاق **ربعُ العمر**،
    # وحسابه في ADR-جديد (عتبة الإنذار) وفي deploy/README.md §3.4.
    threshold_hours="${2:-}"

    if ! command -v openssl >/dev/null 2>&1; then
      echo "✘ openssl غير موجود على الخادم — لا سبيل إلى قراءة الشهادة من هنا." >&2
      exit 2
    fi

    # ‏`-noservername` هو بيت القصيد: بدونه يرسل openssl اسم المضيف في SNI،
    # فيُقاس مسارٌ لا يسلكه أي متصفّح يفتح عنواناً حرفياً. وغيابُ الخيار من
    # نسخة openssl قديمة يُقال صراحةً، ولا يُقرأ «لا شهادة».
    if ! openssl s_client -help 2>&1 | grep -q -- '-noservername'; then
      echo "✘ نسخة openssl هنا لا تعرف -noservername، ولا سبيل إلى قياس المسار بلا SNI." >&2
      echo "  وهو المسار الوحيد الذي يهمّ في وضع ip. حدِّث openssl أو افحص من جهاز آخر." >&2
      exit 2
    fi

    pem="$(echo | openssl s_client -connect 127.0.0.1:443 -noservername 2>/dev/null \
             | sed -n '/BEGIN CERTIFICATE/,/END CERTIFICATE/p')"
    if [ -z "$pem" ]; then
      echo "✘ لا شهادة تُقدَّم على 443. إمّا أن الحافة على HTTP عارٍ، أو أن الإصدار لم ينجح." >&2
      echo "  اقرأ السبب: ./remote.sh logs edge 200" >&2
      exit 1
    fi

    issuer="$(printf '%s' "$pem" | openssl x509 -noout -issuer | sed 's/^issuer=//')"
    not_before="$(printf '%s' "$pem" | openssl x509 -noout -startdate | sed 's/^notBefore=//')"
    not_after="$(printf '%s' "$pem" | openssl x509 -noout -enddate | sed 's/^notAfter=//')"
    subject_alt="$(printf '%s' "$pem" | openssl x509 -noout -ext subjectAltName 2>/dev/null | tail -n +2 | tr -s ' ')"

    expires_at="$(date -u -d "$not_after" +%s 2>/dev/null || echo '')"
    if [ -z "$expires_at" ]; then
      echo "✘ تعذّرت قراءة تاريخ الانتهاء «$not_after»." >&2
      exit 2
    fi
    remaining=$(( (expires_at - $(date -u +%s)) / 3600 ))

    starts_at="$(date -u -d "$not_before" +%s 2>/dev/null || echo '')"
    lifetime_hours=0
    if [ -n "$starts_at" ] && [ "$expires_at" -gt "$starts_at" ]; then
      lifetime_hours=$(( (expires_at - starts_at) / 3600 ))
    fi

    if [ -z "$threshold_hours" ]; then
      # ‏**ربع العمر.** الحسّاب كاملاً: CertMagic يجدّد عند بقاء ثلث العمر
      # (`DefaultRenewalWindowRatio = 1/3`)، فالشهادة السليمة لا تهبط تحت L/3
      # أبداً. والحدّ L/4 يبقى **دونه دائماً**، وفرقُهما L/12 — وهو لشهادة
      # 160 ساعة **13.3 ساعة**، أي أطول من أوسع فجوة في سلّم إعادة المحاولة
      # عند CertMagic (‏6 ساعات). فلا يُبلَغ الحدّ بتجديدٍ متعثّر ثم ناجح،
      # ولا يُبلَغ إلا بإخفاقٍ مستمرّ. ويبقى بعده L/4 = 40 ساعة مهلةً لإنسان.
      if [ "$lifetime_hours" -gt 0 ]; then
        threshold_hours=$(( lifetime_hours / 4 ))
      else
        threshold_hours=40
      fi
    fi

    echo "  المُصدِر     : $issuer"
    echo "  الأسماء     :$subject_alt"
    echo "  يبدأ في     : $not_before"
    echo "  ينتهي في    : $not_after"
    if [ "$lifetime_hours" -gt 0 ]; then
      echo "  العمر كاملاً: ${lifetime_hours} ساعة  ⇒ الحدّ ربعُه = ${threshold_hours} ساعة"
    fi
    echo "  الباقي      : ${remaining} ساعة"

    # **المُصدِر أوّلاً لا العمر:** سلطة Caddy الداخلية تعني شهادة غير موثوقة —
    # قفلٌ مكسور أمام كل ضيف — مهما كان عمرها الباقي طويلاً.
    case "$issuer" in
      *"Caddy Local Authority"*)
        echo "✘ الشهادة من سلطة Caddy الداخلية: **غير موثوقة على أي جهاز لم يُثبَّت جذرها عليه**." >&2
        echo "  إن كان المقصود شهادة عامّة على عنوان، فالوضع هو BABEL_TLS_MODE=ip — deploy/README.md §3." >&2
        exit 1 ;;
    esac

    if [ "$remaining" -lt "$threshold_hours" ]; then
      echo "✘ الباقي ${remaining} ساعة، والحدّ ${threshold_hours}. التجديد التلقائي كان يجب أن يقع قبل الآن." >&2
      echo "  اقرأ السبب: ./remote.sh logs edge 200" >&2
      exit 1
    fi

    echo "✔ الشهادة موثوقة وعمرها فوق الحدّ (${threshold_hours} ساعة)"
    ;;

  # ── ما يُشغّله المؤقّت ────────────────────────────────────────────────────
  #
  # وهو ليس `certs` نفسه: `certs` يفحص ويطبع، وهذا **يُبقي أثراً ويُسمع**.
  # والأثر شرطٌ لأن `rotation-status` — ومعه المراقب الخارجي — يحتاج أن يعرف
  # ‏**متى** عمل الفحص آخر مرّة، لا نتيجته وحدها: مؤقّتٌ توقّف يبدو من مُخرَجه
  # الأخير سليماً إلى الأبد.
  certs-run)
    set +e
    out="$("$self" certs ${2:+"$2"} 2>&1)"
    code=$?
    set -e

    printf '%s\n' "$out"
    summary="$(printf '%s' "$out" | grep -E '^[✘✔]' | tail -1)"
    [ -n "$summary" ] || summary="$(printf '%s' "$out" | tail -1)"
    rotation_write_state "$code" "$summary"

    if [ "$code" != "0" ]; then
      sound_the_alarm "بابل · شهادة العرض: $summary"
      exit "$code"
    fi
    ;;

  # ── تركيب المناوبة — مُمَكِّنٌ مرّتين، ويتحقّق أنها سُجّلت فعلاً ─────────────
  rotation)
    rotation_install
    ;;

  # ── حالة المناوبة — وهي ما يسأله المراقب الخارجي ──────────────────────────
  #
  # ثلاثة أسئلة، وسقوطُ أيّها سقوط: **هل هي مسجَّلة؟** و**هل عملت مؤخّراً؟**
  # و**ماذا قالت آخر مرّة؟** والثاني هو الذي لا يستطيع الفحص نفسه أن يجيب عنه
  # عن نفسه — ولهذا يُقرأ ختم الوقت لا النتيجة وحدها.
  rotation-status)
    skip="$(rotation_reason_to_skip)" && {
      echo "── المناوبة غير لازمة هنا: $skip"
      exit 0
    }

    bad=0
    rotation_verify || bad=1

    if [ ! -f "$rotation_state" ]; then
      echo "✘ لا ملفّ حالة ($rotation_state): المناوبة لم تُركَّب أو لم تعمل قطّ." >&2
      bad=1
    else
      ts="$(state_value ts)"
      code="$(state_value code)"
      summary="$(state_value summary)"
      every="$(state_value every_hours)"
      every="${every:-$rotation_every_hours}"

      age=$(( ( $(date -u +%s) - ${ts:-0} ) / 60 ))
      stale_after=$(( every * 3 * 60 ))
      echo "  آخر فحص    : قبل ${age} دقيقة"
      echo "  رمز الخروج : ${code:-?}"
      echo "  الخلاصة    : ${summary:-—}"

      # ثلاثة أضعاف الدورة: موعدٌ فائت واحد لا يُنذر، وثلاثة يعني أن المؤقّت
      # نفسه توقّف — وهو عطلٌ لا يُبلّغ عن نفسه أبداً.
      if [ "$age" -gt "$stale_after" ]; then
        echo "✘ آخر فحص قبل ${age} دقيقة، والدورة ${every} ساعات — **المؤقّت لا يعمل**." >&2
        bad=1
      fi
      if [ "${code:-1}" != "0" ]; then
        echo "✘ آخر فحص سقط: ${summary:-بلا خلاصة}" >&2
        bad=1
      fi
    fi

    [ "$bad" = "0" ] || exit 1
    echo "✔ المناوبة مسجَّلة، وتعمل، وآخر فحص مرّ"
    ;;

  logs)
    # الخدمة اختيارية: وسيطٌ فارغ يُمرَّر إلى compose يُعدّ اسم خدمة لا وجود لها.
    if [ -n "${2:-}" ]; then
      compose logs --no-color --tail "${3:-200}" "$2"
    else
      compose logs --no-color --tail "${3:-200}"
    fi
    ;;

  *)
    echo "استعمال: remote.sh deploy <وسم> | rollback | health | certs [ساعات] | certs-run [ساعات] | rotation | rotation-status | logs [خدمة]" >&2
    exit 2
    ;;
esac
