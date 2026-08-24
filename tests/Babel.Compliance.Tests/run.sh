#!/usr/bin/env bash
# تشغيل مجموعة اختبارات حدّ الالتزام وحدها — اختصار أثناء التطوير لا بوابة.
# البوابة هي: dotnet test --solution Babel.slnx (وهو ما يشغّله التكامل المستمر).
#
# بلا قاعدة بيانات: اختبارات المخزن العلائقي تُتخطّى من نفسها.
# مع قاعدة بيانات: صدّر BABEL_COMPLIANCE_TEST_DB فتعمل الدورة الكاملة.
#   export BABEL_COMPLIANCE_TEST_DB="Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres"
# لا كلمة مرور في هذا المستودع، ولا اعتمادات، ولا مفاتيح على القرص.
set -euo pipefail

export PATH="$PATH:/usr/lib/dotnet"
cd "$(dirname "$0")/../.."

exec dotnet test --project tests/Babel.Compliance.Tests/Babel.Compliance.Tests.csproj "$@"
