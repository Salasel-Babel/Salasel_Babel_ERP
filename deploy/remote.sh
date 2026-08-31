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
#   remote.sh logs [خدمة]    يطبع السجلّ
#
# ولا اعتماد واحد في هذا الملف: كلّها في ‎.env‎ بجانبه، بصلاحية 600.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
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

health() {
  # الفحص من **داخل** الخادم: حزمةٌ خلف جدار أو على نطاق داخلي لا يبلغها
  # عدّاء البناء، وفحصٌ يُنفَّذ حيث لا يستطيع الوصول يُبلّغ عن الشبكة لا عن النشر.
  local attempt
  for attempt in $(seq 1 30); do
    if curl --fail --silent --show-error --max-time 5 http://127.0.0.1/health; then
      echo
      echo "✔ الخدمة تُجيب على /health"
      return 0
    fi
    sleep 3
  done
  echo "✘ لم تُجب /health بعد 30 محاولة" >&2
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
      "$0" rollback || true
      exit 1
    fi

    if ! health; then
      echo "✘ الحزمة أُقيمت ولا تُجيب — رجوع تلقائي" >&2
      compose logs --no-color --tail 200 api >&2 || true
      "$0" rollback || true
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
  # يُشغَّل — إمّا بيد إنسان وإمّا من `cron`. صيغته في README §3.
  #
  # وهو يقرأ الشهادة **من المصافحة لا من القرص**: ما يُقاس هو ما يراه الضيف.
  # وبلا SNI عمداً، لأن المتصفّح الذي يفتح عنواناً حرفياً لا يرسل SNI أصلاً
  # (‏RFC 6066) — فهذا الأمر يفحص المسار نفسه الذي يفشل بلا `default_sni`.
  certs)
    threshold_hours="${2:-40}"

    if ! command -v openssl >/dev/null 2>&1; then
      echo "✘ openssl غير موجود على الخادم — لا سبيل إلى قراءة الشهادة من هنا." >&2
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
    not_after="$(printf '%s' "$pem" | openssl x509 -noout -enddate | sed 's/^notAfter=//')"
    subject_alt="$(printf '%s' "$pem" | openssl x509 -noout -ext subjectAltName 2>/dev/null | tail -n +2 | tr -s ' ')"

    expires_at="$(date -u -d "$not_after" +%s 2>/dev/null || echo '')"
    if [ -z "$expires_at" ]; then
      echo "✘ تعذّرت قراءة تاريخ الانتهاء «$not_after»." >&2
      exit 2
    fi
    remaining=$(( (expires_at - $(date -u +%s)) / 3600 ))

    echo "  المُصدِر     : $issuer"
    echo "  الأسماء     :$subject_alt"
    echo "  ينتهي في    : $not_after"
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

  logs)
    # الخدمة اختيارية: وسيطٌ فارغ يُمرَّر إلى compose يُعدّ اسم خدمة لا وجود لها.
    if [ -n "${2:-}" ]; then
      compose logs --no-color --tail "${3:-200}" "$2"
    else
      compose logs --no-color --tail "${3:-200}"
    fi
    ;;

  *)
    echo "استعمال: remote.sh deploy <وسم> | rollback | health | certs [ساعات] | logs [خدمة]" >&2
    exit 2
    ;;
esac
