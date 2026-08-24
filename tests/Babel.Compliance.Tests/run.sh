#!/usr/bin/env bash
# تشغيل مجموعة اختبارات حدّ الالتزام.
# لا قاعدة بيانات، ولا اعتمادات، ولا شبكة، ولا مفاتيح على القرص.
#
# Runs the compliance-boundary test suite: no database, no credentials,
# no network, no key material on disk.
set -euo pipefail

export PATH="$PATH:/usr/lib/dotnet"
cd "$(dirname "$0")"

# ملاحظة: على .NET 10 لم يعد مسار VSTest مدعوماً مع Microsoft.Testing.Platform،
# فالتشغيل عبر dotnet run مباشرةً (مشروع الاختبار قابل للتنفيذ).
exec dotnet run "$@"
