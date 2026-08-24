#!/usr/bin/env bash
# أمر واحد يُشغّل كل إثباتات مستوى التحكّم ويطبع جدول PASS/FAIL
# one command: runs every control-plane proof and prints the PASS/FAIL table
#
# لا كلمة مرور في المستودع: الاتصال الافتراضي محلّي بلا كلمة مرور،
# ويُضبط بالكامل من متغيّرات البيئة (انظر src/Babel.ControlPlane/README.md).
set -euo pipefail
cd "$(dirname "$0")"
export PATH="$PATH:/usr/lib/dotnet"
exec dotnet run -c Release --project Babel.ControlPlane.Proofs.csproj -- "$@"
