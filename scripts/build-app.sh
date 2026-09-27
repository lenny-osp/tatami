#!/bin/bash
# 編譯並打包成 build/Tatami.app
#
# 簽章：鑰匙圈裡有名為 "Tatami Dev" 的程式碼簽署憑證就用它（簽章固定，重新編譯不會讓輔助使用權限失效），
# 沒有就改用臨時簽章（ad-hoc），任何人 clone 下來都能編譯。
# 也可以用環境變數指定其他憑證，例如：SIGN_IDENTITY="Developer ID Application: ..." ./scripts/build-app.sh
#
# 版本號（顯示在「關於」視窗）：
#   VERSION       優先使用；GitHub Actions 發佈時會設成 release 的 tag（去掉開頭的 v）
#   沒設定時      使用 git describe --tags，例如 1.2.0 或 1.2.0-3-gabc1234（tag 之後又多了 3 個 commit）
#   都沒有時      保留 Resources/Info.plist 裡的預設值
#   BUILD_NUMBER  寫入 CFBundleVersion，沒設定時用 commit 數量
set -euo pipefail
cd "$(dirname "$0")/.."

SIGN_IDENTITY="${SIGN_IDENTITY:-Tatami Dev}"

swift build -c release

APP=build/Tatami.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp .build/release/Tatami "$APP/Contents/MacOS/Tatami"
cp Resources/Info.plist "$APP/Contents/Info.plist"

VERSION="${VERSION:-$(git describe --tags 2>/dev/null | sed 's/^v//' || true)}"
BUILD_NUMBER="${BUILD_NUMBER:-$(git rev-list --count HEAD 2>/dev/null || true)}"
if [ -n "$VERSION" ]; then
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP/Contents/Info.plist"
fi
if [ -n "$BUILD_NUMBER" ]; then
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $BUILD_NUMBER" "$APP/Contents/Info.plist"
fi
echo "Version $(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$APP/Contents/Info.plist")" \
    "($(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" "$APP/Contents/Info.plist"))"
mkdir -p "$APP/Contents/Resources"
cp -R Resources/*.lproj "$APP/Contents/Resources/"
cp Resources/AppIcon.icns "$APP/Contents/Resources/"

if security find-identity -p codesigning | grep -q "\"$SIGN_IDENTITY\""; then
    echo "Signing with \"$SIGN_IDENTITY\""
    codesign --force --sign "$SIGN_IDENTITY" "$APP"
else
    echo "Certificate \"$SIGN_IDENTITY\" not found, using ad-hoc signature"
    codesign --force --sign - "$APP"
fi

echo "Built $APP"
