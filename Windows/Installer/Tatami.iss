; Tatami 的 Windows 安裝程式（Inno Setup 6）。
;
; 平常由 scripts/build-windows.ps1 編譯，它會傳入：
;   ISCC.exe /DAppVersion=1.2.3 /DPublishDir=<publish 資料夾的絕對路徑> /DOutputDir=<輸出資料夾的絕對路徑> Tatami.iss
; 沒有傳入時使用下面的預設值（相對於這個檔案），方便直接在 Inno Setup 裡開啟編譯。
;
; 安裝給目前的使用者（不需要系統管理員權限），所以自動更新時不會跳出 UAC。
; NetSparkle 更新時以 /SILENT 執行，這裡不能有任何需要使用者回應的步驟。

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\..\build\windows\publish"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\..\build\windows"
#endif

#if !FileExists(AddBackslash(PublishDir) + "Tatami.exe")
  #error Tatami.exe not found in PublishDir. Run scripts/build-windows.ps1 first.
#endif

[Setup]
; AppId 固定不變：Windows 靠它認出這是同一個程式，更新時覆蓋舊版而不是另外安裝一份
AppId={{32F560B2-4F57-473F-9B64-9F465714BE8C}
AppName=Tatami
AppVersion={#AppVersion}
AppPublisher=Chihling Wang
AppPublisherURL=https://github.com/lenny-osp/tatami
AppSupportURL=https://github.com/lenny-osp/tatami/issues
AppUpdatesURL=https://github.com/lenny-osp/tatami/releases
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Tatami
DisableProgramGroupPage=yes
DisableStartupPrompt=yes
; 先找和 Windows 介面語言相符的語言，找不到才讓使用者選
ShowLanguageDialog=auto
OutputDir={#OutputDir}
OutputBaseFilename=Tatami-{#AppVersion}-Setup
SetupIconFile=..\Resources\AppIcon.ico
UninstallDisplayIcon={app}\Tatami.exe
UninstallDisplayName=Tatami
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; x64 程式；x64compatible 也包含可以模擬 x64 的 Arm64 Windows 11
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 更新時 Tatami 可能還在執行：用 Restart Manager 關掉它（/SILENT 時自動關閉）。
; 不由安裝程式重新開啟，交給 NetSparkle 或使用者在完成頁勾選「啟動 Tatami」。
CloseApplications=yes
RestartApplications=no

; Inno Setup 官方附帶的翻譯。簡體中文、繁體中文、阿拉伯文、韓文只在較新的版本才有，
; 編譯環境有這些檔案才加入；印地文沒有官方翻譯，使用英文。
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
#if FileExists(AddBackslash(CompilerPath) + "Languages\Korean.isl")
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
#endif
#if FileExists(AddBackslash(CompilerPath) + "Languages\ChineseTraditional.isl")
Name: "zhhant"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"
#endif
#if FileExists(AddBackslash(CompilerPath) + "Languages\ChineseSimplified.isl")
Name: "zhhans"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif
#if FileExists(AddBackslash(CompilerPath) + "Languages\Arabic.isl")
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
#endif

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Tatami"; Filename: "{app}\Tatami.exe"

[Registry]
; 「登入時啟動」的值是 App 自己寫的。這裡不建立任何東西（ValueType: none + dontcreatekey），
; 只是讓解除安裝時把它刪掉，免得留下指向已刪除程式的開機項目。
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Tatami"; Flags: dontcreatekey uninsdeletevalue
; NetSparkle 把更新狀態（上次檢查時間、略過的版本）存在 HKCU\Software\Tatami\Tatami\NetSparkleUpdater，解除安裝時一併刪掉
Root: HKCU; Subkey: "Software\Tatami"; ValueType: none; Flags: dontcreatekey uninsdeletekey

[Run]
; 完成頁的「啟動 Tatami」勾選框；/SILENT 更新時略過
Filename: "{app}\Tatami.exe"; Description: "{cm:LaunchProgram,Tatami}"; Flags: nowait postinstall skipifsilent
