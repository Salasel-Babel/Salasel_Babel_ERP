#!/usr/bin/env bash
# تشغيل الاختبار الاستكشافي بأمر واحد / run the whole spike with one command
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet run "$@"
