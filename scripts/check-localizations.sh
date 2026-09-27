#!/bin/bash
# 檢查所有語言的 Localizable.strings：格式正確、key 和英文版完全一致，
# 而且程式碼裡用到的每個 key 都有翻譯。
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
# 程式碼裡用到的 key（例如 L("menu.quit")、說明視窗的 "help.grid.title"）都必須存在於英文版
import re, pathlib
prefixes = sorted({k.split(".")[0] for k in base})
pattern = re.compile(r'"((?:%s)\.[A-Za-z0-9_.]+)"' % "|".join(map(re.escape, prefixes)))
used = set()
for source in pathlib.Path("Sources").rglob("*.swift"):
    used.update(pattern.findall(source.read_text(encoding="utf-8")))
undefined = sorted(used - set(base))
if undefined:
    failed = True
    print("✗ 程式碼用到但英文翻譯檔沒有的 key:")
    for k in undefined: print(f"    {k}")
else:
    print(f"✓ 程式碼用到的 {len(used)} 個 key 都有翻譯")
unused = sorted(set(base) - used)
if unused:
    print("  （提醒）翻譯檔裡有、但程式碼沒用到的 key: " + ", ".join(unused))
sys.exit(1 if failed else 0)
PY
