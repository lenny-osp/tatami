#!/bin/bash
# 把 build/Tatami.app 打包成 DMG：打開後會看到 Tatami 和「應用程式」資料夾的捷徑，
# 把 Tatami 拖進去就完成安裝。請先執行 ./scripts/build-app.sh。
#
# 用法：./scripts/make-dmg.sh [輸出檔名]
#   沒指定檔名時，使用 build/Tatami-<版本>.dmg
set -euo pipefail
cd "$(dirname "$0")/.."

APP=build/Tatami.app
if [ ! -d "$APP" ]; then
    echo "找不到 $APP，請先執行 ./scripts/build-app.sh" >&2
    exit 1
fi

VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$APP/Contents/Info.plist")
DMG="${1:-build/Tatami-$VERSION.dmg}"

STAGING=$(mktemp -d)
trap 'rm -rf "$STAGING"' EXIT

# ditto 會保留 .app 的權限與簽章
ditto "$APP" "$STAGING/Tatami.app"
ln -s /Applications "$STAGING/Applications"

rm -f "$DMG"
hdiutil create \
    -volname "Tatami $VERSION" \
    -srcfolder "$STAGING" \
    -fs HFS+ \
    -format UDZO \
    -imagekey zlib-level=9 \
    -quiet \
    "$DMG"

echo "Built $DMG"
