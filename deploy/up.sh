#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# أمر واحد يُقيم الحزمة كاملةً **على جهاز بلا خادم**، ثم يطبع الرابط والرمز.
#
#   deploy/up.sh              يختار الوضع تلقائياً: حاويات إن وُجد عفريتها، وإلّا محلياً
#   deploy/up.sh --native     يفرض الوضع المحلي (‏PostgreSQL على الجهاز + dotnet + node)
#   deploy/up.sh --containers يفرض وضع الحاويات (‏docker compose)
#   deploy/up.sh --down       يوقف ما أقامه
#
# **ولا سرّ في هذا الملف ولا في مخرجاته المُودَعة:** رمز العرض يُولَّد عشوائياً
# عند كل تشغيل ويُكتب في deploy/.env.local — وهو مُستثنى بـ.gitignore (`.env.*`).
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"
cd "$root"

mode="auto"
case "${1:-}" in
  --native)     mode="native" ;;
  --containers) mode="containers" ;;
  --down)       mode="down" ;;
  "")           ;;
  *) echo "وسيط غير معروف: $1" >&2; exit 2 ;;
esac

# ‏.NET 10 ليس في PATH افتراضياً على بعض التوزيعات.
if ! command -v dotnet >/dev/null 2>&1; then
  for candidate in /usr/lib/dotnet /usr/share/dotnet "$HOME/.dotnet"; do
    if [ -x "$candidate/dotnet" ]; then export PATH="$PATH:$candidate"; break; fi
  done
fi

env_file="$here/.env.local"
api_port="${BABEL_API_PORT:-5080}"
web_port="${BABEL_WEB_PORT:-5173}"
company="${BABEL_DEMO_COMPANY_ID:-d3305e1e-0000-4000-8000-000000000001}"
user_id="${BABEL_DEMO_USER_ID:-d3305e1e-0000-4000-8000-0000000000a1}"

have_docker() { docker info >/dev/null 2>&1; }

if [ "$mode" = "auto" ]; then
  if have_docker; then mode="containers"; else mode="native"; fi
  echo "── الوضع المُختار تلقائياً: $mode"
fi

# ── الرمز: يُولَّد مرّة ويُعاد استعماله، فلا يتغيّر تحت يد من يعرض ──────────
if [ -f "$env_file" ]; then
  # shellcheck disable=SC1090
  . "$env_file"
fi

if [ -z "${BABEL_DEMO_TOKEN:-}" ]; then
  BABEL_DEMO_TOKEN="demo-$(head -c 18 /dev/urandom | od -An -tx1 | tr -d ' \n')"
  BABEL_DEMO_TOKEN_SHA256="$(printf '%s' "$BABEL_DEMO_TOKEN" | sha256sum | cut -d' ' -f1)"
  BABEL_LEDGER_APP_PASSWORD="$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')"
  POSTGRES_PASSWORD="$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')"
  # مفتاح توقيع تذاكر تنزيل المرفقات — 32 بايتاً، **يُولَّد هنا ويُكتب في ملفّ
  # غير مُودَع**. ولا افتراضَ له في الشيفرة ولا في أي إعداد مُودَع: مفتاحٌ في
  # المستودع مفتاحٌ عامّ، ومفتاحٌ يولّده الخادم لنفسه عند كل إقلاع يجعل كل تذكرة
  # صالحةً قبل إعادة التشغيل ومرفوضةً بعدها — والفشل يُقرأ «انتهت الصلاحية»
  # لا «لا مفتاح» (ADR-0046). وثباتُه هنا عبر التشغيلات هو ما يجعله صالحاً.
  BABEL_STORAGE_TICKET_KEY="$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')"
  umask 077
  cat > "$env_file" <<EOF
# مُولَّد محلياً بـdeploy/up.sh — لا يُودَع في git (.gitignore: .env.*)
BABEL_DEMO_TOKEN=$BABEL_DEMO_TOKEN
BABEL_DEMO_TOKEN_SHA256=$BABEL_DEMO_TOKEN_SHA256
BABEL_LEDGER_APP_PASSWORD=$BABEL_LEDGER_APP_PASSWORD
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
BABEL_STORAGE_TICKET_KEY=$BABEL_STORAGE_TICKET_KEY
BABEL_DEMO_COMPANY_ID=$company
BABEL_DEMO_USER_ID=$user_id
EOF
  echo "── وُلِّد رمز عرض محلي جديد في $env_file"
fi

export BABEL_DEMO_TOKEN BABEL_DEMO_TOKEN_SHA256 BABEL_LEDGER_APP_PASSWORD POSTGRES_PASSWORD
export BABEL_STORAGE_TICKET_KEY

banner() {
  echo
  echo "════════════════════════════════════════════════════════════════"
  echo "  الواجهة : http://127.0.0.1:$1/?token=$BABEL_DEMO_TOKEN&companyId=$company&book=MAIN"
  echo "  الخادم  : http://127.0.0.1:$2/health"
  echo "  الرمز   : $BABEL_DEMO_TOKEN"
  echo "════════════════════════════════════════════════════════════════"
}

# ── وضع الحاويات ────────────────────────────────────────────────────────────
if [ "$mode" = "containers" ] || [ "$mode" = "down" ]; then
  if ! have_docker; then
    echo "✗ لا عفريت حاويات يستجيب. استعمل: deploy/up.sh --native" >&2
    exit 1
  fi

  compose_env="$here/.env"
  umask 077
  cat > "$compose_env" <<EOF
BABEL_REGISTRY=babel-local
BABEL_IMAGE_TAG=local
BABEL_SITE=:80
BABEL_TLS_MODE=auto
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
BABEL_LEDGER_APP_PASSWORD=$BABEL_LEDGER_APP_PASSWORD
BABEL_STORAGE_TICKET_KEY=$BABEL_STORAGE_TICKET_KEY
BABEL_DEMO_TOKEN_SHA256=$BABEL_DEMO_TOKEN_SHA256
BABEL_DEMO_COMPANY_ID=$company
BABEL_DEMO_USER_ID=$user_id
EOF

  if [ "$mode" = "down" ]; then
    docker compose -f "$here/compose.yml" --env-file "$compose_env" down
    exit 0
  fi

  echo "── بناء الصور محلياً"
  docker build -f "$here/Dockerfile.api"      -t babel-local/babel-api:local      "$root"
  docker build -f "$here/Dockerfile.migrator" -t babel-local/babel-migrator:local "$root"
  docker build -f "$here/Dockerfile.web"      -t babel-local/babel-web:local      "$root"

  echo "── إقامة الحزمة"
  docker compose -f "$here/compose.yml" --env-file "$compose_env" up -d --wait --wait-timeout 300 api web edge

  echo "── فحص الحياة عبر الحافة"
  curl --fail --silent --show-error http://127.0.0.1/health && echo

  echo
  echo "════════════════════════════════════════════════════════════════"
  echo "  الواجهة : http://127.0.0.1/?token=$BABEL_DEMO_TOKEN&companyId=$company&book=MAIN"
  echo "════════════════════════════════════════════════════════════════"
  exit 0
fi

# ── الوضع المحلي ────────────────────────────────────────────────────────────
# كلمة مرور دور التطبيق **لا تُسنَد محلياً**: الافتراض المحلي هو `pg_hba: trust`
# على 127.0.0.1، وإسنادُ كلمة مرور هناك يقطع الاتصال بلا فائدة. وهي على الخادم
# إلزامية، ويمرّرها compose إلى حاوية الترحيل وحدها.
unset BABEL_LEDGER_APP_PASSWORD

command -v dotnet >/dev/null 2>&1 || { echo "✗ dotnet غير موجود في PATH." >&2; exit 1; }
command -v node   >/dev/null 2>&1 || { echo "✗ node غير موجود في PATH." >&2; exit 1; }

: "${BABEL_ADMIN_DB:=Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres}"
: "${BABEL_LEDGER_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_ledger;Username=postgres;Include Error Detail=true}"
: "${BABEL_LEDGER_APP_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_ledger;Username=babel_ledger_app;Include Error Detail=true}"
: "${BABEL_SALES_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_sales;Username=postgres;Include Error Detail=true}"
: "${BABEL_PURCHASING_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_purchasing;Username=postgres;Include Error Detail=true}"
# المخزون: التقييم وتكلفة المبيعات (‏ADR-0039). واسمه يتبع بقيّة قواعد العرض —
# غيابه من هنا كان سيُشغّل العرض على `babel_inventory` بينما بقيّته على `babel_demo_*`.
: "${BABEL_INVENTORY_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_inventory;Username=postgres;Include Error Detail=true}"

# النواة: تأسيس المنشأة ومراكز تكلفتها. المالك للترحيل، والتطبيق للخادم — والخادم
# لا يرى اتصال المالك أبداً (ADR-0003).
: "${BABEL_CORE_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_core;Username=postgres;Include Error Detail=true}"
: "${BABEL_CORE_APP_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_core;Username=babel_ledger_app;Include Error Detail=true}"
export BABEL_ADMIN_DB BABEL_LEDGER_OWNER_DB BABEL_LEDGER_APP_DB BABEL_SALES_OWNER_DB BABEL_PURCHASING_OWNER_DB
# ── العقارات: قاعدةٌ **مزوَّدة الآن**، بالشكل نفسه الذي تُزوَّد به بقيّة القواعد ──
# سطح العقارات منشورٌ في العقد (عشرون باباً)، ومخطّطه `realestate` **يُنشر بدور
# المالك** لأنه يركّب امتداد `btree_gist` ويبني عليه قيد الاستبعاد الزمني الذي يمنع
# تأجير الوحدة الواحدة بعقدين متداخلين. وكان هذا النصّ لا ينشئ له قاعدة، فكانت
# أبوابه العشرون **غير قابلة للبلوغ في أي عرض ولا أي نشرة** — وحدةٌ كاملة البناء
# والاختبار ولا تُزوَّد قطّ
# (docs/evidence/traps.md#fakh-a-module-fully-built-fully-tested-and-never-provisioned).
#
# والتزويد ثلاثة أسطر لا أكثر، وهي بالضبط ما تفعله بقيّة القواعد:
#   ١ · اتصال **المالك** للمُنشئ  ← BABEL_REALESTATE_OWNER_DB
#   ٢ · اتصال **التطبيق** للخادم ← BABEL_REALESTATE_DB (أدناه، عند إقلاع الخادم)
#   ٣ · شراء الوحدة للمنشأة      ← Babel__Entitlements__<الشركة>__RealEstate
# ولا يرى الخادم اتصال المالك أبداً، هنا كما في compose.yml (ADR-0003).
: "${BABEL_REALESTATE_OWNER_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_realestate;Username=postgres;Include Error Detail=true}"
: "${BABEL_REALESTATE_APP_DB:=Host=127.0.0.1;Port=5432;Database=babel_demo_realestate;Username=babel_ledger_app;Include Error Detail=true}"
export BABEL_REALESTATE_OWNER_DB

# مخزن المرفقات: جذرٌ على القرص يملكه مستخدم الخدمة. **ولا اتصال مالك هنا**،
# ولا قاعدةَ مرفقات تُنشأ في هذا النصّ: نشرُ مخطّط `storage` عملُ مالك لم يُزوَّد
# بعد، فأبواب المرفقات **غير قابلة للبلوغ في العرض المحلي** حتى يُزوَّد. وهذا
# نقصٌ مُعلَن لا صمت — والخادم يقلع ويعمل بقيّة سطحه كما هو.
: "${BABEL_STORAGE_ROOT:=$here/.attachments}"
mkdir -p "$BABEL_STORAGE_ROOT"
chmod 700 "$BABEL_STORAGE_ROOT"
export BABEL_STORAGE_ROOT

export BABEL_CORE_OWNER_DB BABEL_CORE_APP_DB BABEL_INVENTORY_OWNER_DB
export BABEL_DEMO_COMPANY_ID="$company"

echo "── بناء الخادم والمُنشئ"
dotnet build src/Babel.Api/Babel.Api.csproj        -c Release --nologo -v quiet
dotnet build demo/company/BabelDemoCompany.csproj  -c Release --nologo -v quiet

echo "── تهيئة القاعدة والبذر والإثبات"
dotnet run -c Release --no-build --project demo/company/BabelDemoCompany.csproj -- all

echo "── بناء الواجهة"
[ -d web/node_modules ] || (cd web && npm ci)
(cd web && npm run build)

echo "── إقلاع الخادم على المنفذ $api_port"
# ‏`env` لا بادئةُ إسناد: اسم متغيّر الاستحقاق يحمل معرّف المنشأة بشرطاته، وbash
# لا يقبل شرطةً في اسم متغيّر — فبادئة الإسناد كانت ستُقرأ اسمَ أمر لا إسناداً.
env "Babel__Entitlements__${company}__RealEstate=Entitled" \
  ASPNETCORE_URLS="http://127.0.0.1:$api_port" \
  Babel__Core__AppConnectionString="$BABEL_CORE_APP_DB" \
  Babel__RealEstate__ConnectionString="$BABEL_REALESTATE_APP_DB" \
  Babel__Api__Tokens__0__Sha256="$BABEL_DEMO_TOKEN_SHA256" \
  Babel__Api__Tokens__0__Tenant="$company" \
  Babel__Api__Tokens__0__User="$user_id" \
  Babel__Api__Tokens__0__Companies__0="$company" \
  dotnet src/Babel.Api/bin/Release/net10.0/Babel.Api.dll > "$here/.api.log" 2>&1 &
api_pid=$!
echo "$api_pid" > "$here/.api.pid"

for _ in $(seq 1 40); do
  if curl --fail --silent "http://127.0.0.1:$api_port/health" >/dev/null 2>&1; then break; fi
  sleep 1
done

curl --fail --silent --show-error "http://127.0.0.1:$api_port/health" && echo

echo "── إقلاع الواجهة على المنفذ $web_port"
BABEL_API="http://127.0.0.1:$api_port" BABEL_WEB_PORT="$web_port" \
  node deploy/local/serve.mjs > "$here/.web.log" 2>&1 &
echo "$!" > "$here/.web.pid"
sleep 2

curl --fail --silent --show-error -o /dev/null "http://127.0.0.1:$web_port/"
banner "$web_port" "$api_port"
echo "  للإيقاف: kill \$(cat deploy/.api.pid) \$(cat deploy/.web.pid)"
