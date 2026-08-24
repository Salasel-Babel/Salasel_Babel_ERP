#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# أمر واحد: يهيّئ قاعدة البيانات ويبذرها ويشغّل التطبيق، ثم يطبع الرابط.
# التشغيل مرّتين يعطي النتيجة نفسها: المخطط يُحذف ويُعاد إنشاؤه في كل مرة.
#
# One command: sets up the database, seeds it, starts the app, prints the URL.
# Idempotent — the schema is dropped and recreated on every run.
# ---------------------------------------------------------------------------
set -euo pipefail
cd "$(dirname "$0")"

# .NET 10 غير موجود في PATH افتراضياً على بعض التوزيعات
if ! command -v dotnet >/dev/null 2>&1; then
  for d in /usr/lib/dotnet /usr/share/dotnet "$HOME/.dotnet"; do
    [ -x "$d/dotnet" ] && export PATH="$PATH:$d" && break
  done
fi
command -v dotnet >/dev/null 2>&1 || { echo "✗ لم يُعثر على dotnet في PATH. ثبّت .NET SDK 10 أو أضِفه إلى PATH." >&2; exit 1; }

PORT="${BABEL_DEMO_PORT:-5099}"
export BABEL_DEMO_PORT="$PORT"

echo "── نظام سلاسل بابل ERP · عرض الشريحة الرأسية ─────────────────"
echo "  dotnet:     $(dotnet --version)"
echo "  المنفذ:      $PORT"
echo "  دور التطبيق: ${BABEL_DEMO_APP_DB:-Host=127.0.0.1;Port=5432;Database=babel_demo;Username=babel_demo_app (افتراضي بلا كلمة مرور)}"
echo

# التحقّق من أن المنفذ حر قبل البناء
if command -v ss >/dev/null 2>&1 && ss -ltn "sport = :$PORT" 2>/dev/null | grep -q LISTEN; then
  echo "✗ المنفذ $PORT مشغول. أوقف العملية السابقة أو شغّل: BABEL_DEMO_PORT=5100 ./demo.sh" >&2
  exit 1
fi

echo "• بناء التطبيق / building"
dotnet build -c Release --nologo -v q

if [ "${1:-}" = "--setup-only" ]; then
  exec dotnet run -c Release --no-build --project BabelDemo.csproj -- --setup-only
fi

echo
echo "• تهيئة قاعدة البيانات ثم تشغيل الخادم / setting up the database, then serving"
echo "  الرابط بعد بدء التشغيل: http://localhost:$PORT/"
echo
exec dotnet run -c Release --no-build --project BabelDemo.csproj
