#!/bin/bash
# 檢查所有語言的 Localizable.strings：格式正確，而且 key 和英文版完全一致。
set -euo pipefail
cd "$(dirname "$0")/.."

python3 - <<'PY'
import glob, json, subprocess, sys

def load(path):
    out = subprocess.run(["plutil", "-convert", "json", "-o", "-", path],
                         capture_output=True, text=True)
    if out.returncode != 0:
        print(f"✗ {path}: 格式錯誤\n{out.stderr}")
        sys.exit(1)
    return json.loads(out.stdout)

base = load("Resources/en.lproj/Localizable.strings")
failed = False
for path in sorted(glob.glob("Resources/*.lproj/Localizable.strings")):
    keys = set(load(path))
    missing = set(base) - keys
    extra = keys - set(base)
    if missing or extra:
        failed = True
        print(f"✗ {path}")
        for k in sorted(missing): print(f"    缺少: {k}")
        for k in sorted(extra): print(f"    多出: {k}")
    else:
        print(f"✓ {path} ({len(keys)} keys)")
sys.exit(1 if failed else 0)
PY
