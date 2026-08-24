#!/usr/bin/env bash
# يطبع كل حزمة تم حلّها فعلياً مع إطار العمل المستخدم منها
# prints every actually-resolved package and the TFM its assets were taken from
set -euo pipefail
cd "$(dirname "$0")"
dotnet restore >/dev/null
python3 - <<'PY'
import json, glob
a = json.load(open(glob.glob('obj/project.assets.json')[0]))
tgt = list(a['targets'].values())[0]
rows = []
for k, v in sorted(tgt.items(), key=lambda kv: kv[0].lower()):
    name, ver = k.split('/')
    paths = [p for p in (v.get('compile') or {}) if p.endswith('.dll')]
    tfms = sorted({p.split('/')[1] for p in paths if p.startswith(('lib/', 'ref/'))})
    rows.append((name, ver, ','.join(tfms) or '(analyzer/meta only)'))
w1 = max(len(r[0]) for r in rows); w2 = max(len(r[1]) for r in rows)
print(f"{'PACKAGE':<{w1}}  {'VERSION':<{w2}}  ASSETS TAKEN FROM")
print('-' * (w1 + w2 + 22))
for r in rows:
    print(f"{r[0]:<{w1}}  {r[1]:<{w2}}  {r[2]}")
print(f"\n{len(rows)} packages resolved")
PY
