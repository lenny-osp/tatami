# 產生 Windows 版 NetSparkle 的 appcast-windows.xml（更新資訊）和它的簽章檔 appcast-windows.xml.signature。
# 每個 release 都會附上這兩個檔案，App 透過
# https://github.com/lenny-osp/tatami/releases/latest/download/appcast-windows.xml 查詢最新版本。
#
# 用法：pwsh scripts/make-appcast-windows.ps1 <安裝程式路徑> <安裝程式下載網址> <release 頁面網址> <輸出路徑>
# 需要環境變數：
#   VERSION              版本號，例如 1.2.3（GitHub Actions 由 release 的 tag 設定）
#   SPARKLE_PRIVATE_KEY  NetSparkle 私鑰（base64），workflow 從 secret NETSPARKLE_PRIVATE_KEY 帶入
#   SPARKLE_PUBLIC_KEY   NetSparkle 公鑰（base64），workflow 從 secret NETSPARKLE_PUBLIC_KEY 帶入
# 變數名稱是 netsparkle-generate-appcast 自己讀的名稱，和 macOS 的 Sparkle 金鑰無關。
param(
    [string]$Installer,
    [string]$DownloadUrl,
    [string]$ReleaseUrl,
    [string]$Output
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# 產生器版本固定，避免新版改了輸出格式
$GeneratorVersion = '2.9.0'

if (-not $Installer -or -not $DownloadUrl -or -not $ReleaseUrl -or -not $Output) {
    Write-Error 'Usage: make-appcast-windows.ps1 <installer path> <download URL> <release page URL> <output path>'
    exit 1
}
if (-not (Test-Path -LiteralPath $Installer -PathType Leaf)) {
    Write-Error "Installer not found: $Installer"
    exit 1
}
foreach ($name in 'VERSION', 'SPARKLE_PRIVATE_KEY', 'SPARKLE_PUBLIC_KEY') {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        Write-Error "$name is not set"
        exit 1
    }
}

$Version = $env:VERSION.Trim() -replace '^v', ''
# sparkle:version 只放數字部分（1.2.3-beta.1 → 1.2.3），完整版本號放 shortVersionString
if ($Version -notmatch '^(\d+(\.\d+){1,3})') {
    Write-Error "VERSION '$Version' does not start with a version number like 1.2.3"
    exit 1
}
$NumericVersion = $Matches[1]

# 沒有產生器就安裝（dotnet global tool）
if (-not (Get-Command netsparkle-generate-appcast -ErrorAction SilentlyContinue)) {
    dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator --version $GeneratorVersion
    if ($LASTEXITCODE -ne 0) { throw 'Could not install NetSparkleUpdater.Tools.AppCastGenerator' }
    # 剛安裝的 global tool 不一定已經在 PATH 裡
    $env:PATH = (Join-Path $HOME '.dotnet/tools') + [IO.Path]::PathSeparator + $env:PATH
}

# 用 SPARKLE_PRIVATE_KEY 簽署檔案。產生器輸出格式為「Signature: <base64>」
function Get-Signature([string]$Path) {
    $text = & netsparkle-generate-appcast --generate-signature $Path 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $text -notmatch 'Signature:\s*([A-Za-z0-9+/=]+)') {
        throw "Could not sign ${Path}:`n$text"
    }
    $signature = $Matches[1]
    # 用 SPARKLE_PUBLIC_KEY 驗證一次，確定兩把金鑰是同一組（不同組的話 App 會拒絕每一次更新）
    $check = & netsparkle-generate-appcast --verify $Path --signature $signature 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $check -notmatch 'Signature valid') {
        throw "Signature of $Path does not verify with SPARKLE_PUBLIC_KEY:`n$check"
    }
    return $signature
}

function Escape([string]$Value) { [Security.SecurityElement]::Escape($Value) }

$length = (Get-Item -LiteralPath $Installer).Length
$signature = Get-Signature $Installer
$pubDate = [DateTime]::UtcNow.ToString('ddd, dd MMM yyyy HH:mm:ss +0000', [Globalization.CultureInfo]::InvariantCulture)

# 格式和 macOS 的 appcast.xml 一樣；NetSparkle 讀 <sparkle:version> 等子元素，也讀 enclosure 上的 sparkle:os 和簽章
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>Tatami</title>
    <link>https://github.com/lenny-osp/tatami</link>
    <item>
      <title>Tatami $(Escape $Version)</title>
      <link>$(Escape $ReleaseUrl)</link>
      <pubDate>$pubDate</pubDate>
      <sparkle:version>$NumericVersion</sparkle:version>
      <sparkle:shortVersionString>$(Escape $Version)</sparkle:shortVersionString>
      <enclosure url="$(Escape $DownloadUrl)" type="application/octet-stream" length="$length" sparkle:os="windows" sparkle:edSignature="$signature" />
    </item>
  </channel>
</rss>
"@ -replace "`r`n", "`n"

# UTF-8 不加 BOM：App 下載後會用 UTF-8 重新編碼再驗證簽章，內容必須逐位元組相同
$OutputPath = [IO.Path]::GetFullPath($Output, (Get-Location).Path)
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($OutputPath, $xml + "`n", $utf8)

# NetSparkle 預設也驗證 appcast 本身：簽章放在 <appcast 網址>.signature
$appcastSignature = Get-Signature $OutputPath
[IO.File]::WriteAllText("$OutputPath.signature", $appcastSignature, $utf8)

Write-Host "Wrote $Output and $Output.signature (version $Version, numeric $NumericVersion, $length bytes)"
