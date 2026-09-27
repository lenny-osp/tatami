#!/bin/bash
# 用 scripts/make-icon.swift 畫出圖示，轉成 macOS 需要的各種尺寸，輸出 Resources/AppIcon.icns。
# 只有修改圖示設計時才需要執行；產生的 AppIcon.icns 會 commit 進 repo。
set -euo pipefail
cd "$(dirname "$0")/.."

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

swift scripts/make-icon.swift "$WORK/icon_1024.png"

ICONSET="$WORK/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z $size $size "$WORK/icon_1024.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z $double $double "$WORK/icon_1024.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done

iconutil -c icns "$ICONSET" -o Resources/AppIcon.icns
echo "Wrote Resources/AppIcon.icns"
