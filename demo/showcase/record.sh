#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════════════
# تصوير فيلم العرض — أمر واحد، وقابل لإعادة التشغيل.
#
#   deploy/up.sh --native        # أولاً: حزمة حقيقية بقاعدة مبذورة
#   demo/showcase/record.sh      # ثم: التصوير
#
# المُخرَج: demo/showcase/out/salasel-babel-demo.webm (وmp4 إن توفّر ffmpeg).
# ولا سرّ في المُخرَج: الرمز يُمرَّر بيئةً ويُخفى على الشاشة.
# ═══════════════════════════════════════════════════════════════════════════
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cd "$root"

[ -f deploy/.env.local ] || { echo "✗ لا ملفّ deploy/.env.local — شغّل deploy/up.sh أولاً." >&2; exit 1; }
# shellcheck disable=SC1091
. deploy/.env.local
export BABEL_DEMO_TOKEN BABEL_DEMO_COMPANY_ID

if ! command -v dotnet >/dev/null 2>&1; then
  for candidate in /usr/lib/dotnet /usr/share/dotnet "$HOME/.dotnet"; do
    if [ -x "$candidate/dotnet" ]; then export PATH="$PATH:$candidate"; break; fi
  done
fi

: "${BABEL_WEB_URL:=http://127.0.0.1:5173}"
: "${BABEL_API_URL:=http://127.0.0.1:5080}"
export BABEL_WEB_URL BABEL_API_URL

curl --fail --silent -o /dev/null "$BABEL_WEB_URL/" || { echo "✗ الواجهة لا تُجيب على $BABEL_WEB_URL" >&2; exit 1; }
curl --fail --silent -o /dev/null "$BABEL_API_URL/health" || { echo "✗ الخادم لا يُجيب" >&2; exit 1; }

mkdir -p "$here/out"

echo "── تسخين قارئ الرمز (بناء أوّل يستغرق دقيقة)"
dotnet run demo/showcase/read-qr.cs -- "AS8=" >/dev/null 2>&1 || true

echo "── التصوير"
rm -rf web/test-results
(cd web && npx playwright test demo-film.spec.ts)

raw="$(find web/test-results -name "*.webm" | head -1)"
[ -n "$raw" ] || { echo "✗ لم يُنتَج ملفّ فيديو." >&2; exit 1; }

webm="$here/out/salasel-babel-demo.webm"
cp "$raw" "$webm"
echo "── webm: $webm"

# ‏mp4 أوسع توافقاً عند من يشاهد. ffmpeg المرفق مع Playwright بلا h264،
# فيُجرَّب ffmpeg النظام ثم ثنائيّ npm — وإلّا يبقى webm وحده.
ff=""
if command -v ffmpeg >/dev/null 2>&1; then ff="ffmpeg";
elif [ -x "$here/.tools/ffmpeg" ]; then ff="$here/.tools/ffmpeg";
else
  mkdir -p "$here/.tools"
  if (cd "$here/.tools" && npm pack @ffmpeg-installer/linux-x64 >/dev/null 2>&1 && tar xzf ./*.tgz >/dev/null 2>&1); then
    cp "$here/.tools/package/ffmpeg" "$here/.tools/ffmpeg" && chmod +x "$here/.tools/ffmpeg" && ff="$here/.tools/ffmpeg"
  fi
fi

if [ -n "$ff" ]; then
  mp4="$here/out/salasel-babel-demo.mp4"
  "$ff" -y -loglevel error -i "$webm" -vf "fps=30,scale=1920:1080:flags=lanczos" \
        -c:v libx264 -preset slow -crf 20 -pix_fmt yuv420p -movflags +faststart "$mp4"
  echo "── mp4 : $mp4"
  ls -lh "$mp4"
else
  echo "── لا ffmpeg بـh264 — بقي webm وحده."
fi
