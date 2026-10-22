#Requires -Version 7
# 編譯 Windows 版並打包成安裝程式：
#   build/windows/publish/                     dotnet publish 的結果（win-x64、自帶 .NET 執行環境、ReadyToRun）
#   build/windows/Tatami-<版本>-Setup.exe      Inno Setup 安裝程式（找得到 ISCC.exe 才會產生）
#
# 版本號（顯示在「關於」視窗，也是安裝程式檔名）：
#   VERSION       優先使用；GitHub Actions 發佈時會設成 release 的 tag（去掉開頭的 v）
#   沒設定時      使用 git describe --tags，例如 1.2.0 或 1.2.0-3-gabc1234（tag 之後又多了 3 個 commit）
#   都沒有時      0.1.0
#
# Inno Setup 的位置：ISCC 環境變數、PATH，或預設的安裝資料夾。找不到時跳過安裝程式，只做 publish。
#
# 用法：pwsh scripts/build-windows.ps1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $true

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'Windows' 'Tatami' 'Tatami.csproj'
$installerScript = Join-Path $root 'Windows' 'Installer' 'Tatami.iss'
$outputDir = Join-Path $root 'build' 'windows'
$publishDir = Join-Path $outputDir 'publish'

# MARK: - 版本號

$version = $env:VERSION
if ([string]::IsNullOrWhiteSpace($version)) {
    try {
        $described = git -C $root describe --tags 2>$null
        if ($LASTEXITCODE -eq 0 -and $described) { $version = ($described | Select-Object -First 1).Trim() }
    } catch {
        $version = $null
    }
    $global:LASTEXITCODE = 0
}
if ([string]::IsNullOrWhiteSpace($version)) { $version = '0.1.0' }
$version = $version.Trim() -replace '^v', ''

# AssemblyVersion 和 FileVersion 只能是數字（最多四段），預發佈的後綴只放進 InformationalVersion
if ($version -notmatch '^(\d+)(\.\d+){0,3}') {
    throw "Version '$version' does not start with a number (expected something like 1.2.3 or 1.2.3-beta.1)"
}
$numericVersion = $Matches[0]
Write-Host "Version $version (numeric $numericVersion)"

# MARK: - dotnet publish

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=false `
    "-p:Version=$numericVersion" `
    "-p:AssemblyVersion=$numericVersion" `
    "-p:FileVersion=$numericVersion" `
    "-p:InformationalVersion=$version" `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $publishDir 'Tatami.exe'
if (-not (Test-Path $exe)) { throw "Tatami.exe not found in $publishDir" }
Write-Host "Published: $publishDir"

# MARK: - Inno Setup

function Find-Iscc {
    if ($env:ISCC -and (Test-Path $env:ISCC)) { return (Resolve-Path $env:ISCC).Path }
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($folder in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
        if (-not $folder) { continue }
        $candidate = Join-Path $folder 'Inno Setup 6' 'ISCC.exe'
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host 'Inno Setup not found, skipping installer (install Inno Setup 6 or set ISCC to the path of ISCC.exe).'
    exit 0
}

& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" $installerScript
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }

$installer = Join-Path $outputDir "Tatami-$version-Setup.exe"
if (-not (Test-Path $installer)) { throw "Installer not found: $installer" }
Write-Host "Installer: $installer"
