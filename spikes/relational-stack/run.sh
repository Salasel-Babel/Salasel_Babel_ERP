#!/usr/bin/env bash
# أمر واحد يُشغّل كل الإثباتات ويطبع جدول PASS/FAIL
# one command: runs every proof and prints the PASS/FAIL table
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet run -c Release --project RelationalSpike.csproj -- "$@"
