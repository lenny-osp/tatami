#!/bin/bash
# 產生 Sparkle 的 appcast.xml（更新資訊）。每個 release 都會附上這個檔案，
# App 透過 https://github.com/lenny-osp/tatami/releases/latest/download/appcast.xml 查詢最新版本。
#
# 用法：./scripts/make-appcast.sh <DMG 路徑> <DMG 下載網址> <release 頁面網址> <輸出路徑>
# 需要環境變數 SPARKLE_PRIVATE_KEY（generate_keys -x 匯出的私鑰內容）。
set -euo pipefail
cd "$(dirname "$0")/.."

DMG="$1"
DOWNLOAD_URL="$2"
RELEASE_URL="$3"
OUTPUT="$4"

PLIST=build/Tatami.app/Contents/Info.plist
VERSION=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$PLIST")
BUILD=$(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" "$PLIST")
MIN_SYSTEM=$(/usr/libexec/PlistBuddy -c "Print :LSMinimumSystemVersion" "$PLIST")

if [ -z "${SPARKLE_PRIVATE_KEY:-}" ]; then
    echo "SPARKLE_PRIVATE_KEY is not set" >&2
    exit 1
fi

# 輸出例如：sparkle:edSignature="..." length="123456"
SIGNATURE=$(echo "$SPARKLE_PRIVATE_KEY" | .build/artifacts/sparkle/Sparkle/bin/sign_update --ed-key-file - "$DMG")

cat > "$OUTPUT" <<XML
<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>Tatami</title>
    <link>https://github.com/lenny-osp/tatami</link>
    <item>
      <title>Tatami $VERSION</title>
      <link>$RELEASE_URL</link>
      <pubDate>$(LC_ALL=C date -u "+%a, %d %b %Y %H:%M:%S +0000")</pubDate>
      <sparkle:version>$BUILD</sparkle:version>
      <sparkle:shortVersionString>$VERSION</sparkle:shortVersionString>
      <sparkle:minimumSystemVersion>$MIN_SYSTEM</sparkle:minimumSystemVersion>
      <enclosure url="$DOWNLOAD_URL" type="application/octet-stream" $SIGNATURE />
    </item>
  </channel>
</rss>
XML

echo "Wrote $OUTPUT (version $VERSION, build $BUILD)"
