#!/bin/bash
# 編譯並打包成 build/Tatami.app
#
# 簽章：鑰匙圈裡有名為 "Tatami Dev" 的程式碼簽署憑證就用它（簽章固定，重新編譯不會讓輔助使用權限失效），
# 沒有就改用臨時簽章（ad-hoc），任何人 clone 下來都能編譯。
# 也可以用環境變數指定其他憑證，例如：SIGN_IDENTITY="Developer ID Application: ..." ./scripts/build-app.sh
set -euo pipefail
cd "$(dirname "$0")/.."

SIGN_IDENTITY="${SIGN_IDENTITY:-Tatami Dev}"

swift build -c release

APP=build/Tatami.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp .build/release/Tatami "$APP/Contents/MacOS/Tatami"
cp Resources/Info.plist "$APP/Contents/Info.plist"
mkdir -p "$APP/Contents/Resources"
cp -R Resources/*.lproj "$APP/Contents/Resources/"

if security find-identity -p codesigning | grep -q "\"$SIGN_IDENTITY\""; then
    echo "Signing with \"$SIGN_IDENTITY\""
    codesign --force --sign "$SIGN_IDENTITY" "$APP"
else
    echo "Certificate \"$SIGN_IDENTITY\" not found, using ad-hoc signature"
    codesign --force --sign - "$APP"
fi

echo "Built $APP"
