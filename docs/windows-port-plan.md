# Tatami for Windows: porting plan

Status: approved plan, executed in this repository. macOS code stays at the repository root; the Windows app lives in `Windows/`. One repository, one release workflow, two installers.

## 1. Verdict

Tatami can be ported. Every feature has a direct Windows equivalent, and none of them needs a special permission on Windows (there is no Accessibility prompt). The app is small (about 2,000 lines of Swift), and only about 60 lines of it are pure logic (grid math, relative frame, snap zone, backup format); everything else is platform UI and system APIs that must be rewritten anyway.

| macOS feature | macOS API | Windows equivalent | Risk |
|---|---|---|---|
| Menu bar icon + menu | `NSStatusItem` | Notification-area icon (`Shell_NotifyIcon`) + WPF `ContextMenu` | Low |
| Move/resize other apps' windows | Accessibility API (`AXUIElement`) | `GetForegroundWindow`, `DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)`, `SetWindowPos`, `ShowWindow(SW_RESTORE)` | Low. No permission needed. Elevated (admin) windows cannot be moved by a non-elevated process (UIPI). |
| Screens and work area | `NSScreen.visibleFrame` | `EnumDisplayMonitors` / `GetMonitorInfo().rcWork`, `MonitorFromPoint`, `GetDpiForMonitor` | Medium: per-monitor DPI handling |
| Grid panel per screen | `NSPanel` (non-activating, floating) | Borderless topmost WPF window per monitor, positioned with `SetWindowPos` in physical pixels | Low |
| Drag to edge to snap | Global `NSEvent` mouse monitor + frame polling | `SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART/END)` + cursor polling timer | Low. Windows' own Snap conflicts, as macOS tiling does. |
| Global hotkeys | Carbon `RegisterEventHotKey` | Win32 `RegisterHotKey` + `WM_HOTKEY` on a message-only window | Low. Same conflict semantics: registration fails if taken. |
| Shortcut recorder | Local `NSEvent` key monitor | WPF `PreviewKeyDown` on the settings window | Low |
| Settings / Help / About windows | AppKit + SwiftUI | WPF windows, Fluent theme (`ThemeMode=System`, light/dark) | Low |
| In-app language switch, RTL | `.lproj` bundles | Same `.strings` files parsed at runtime; `FlowDirection=RightToLeft` for Arabic | Low |
| Launch at login | `SMAppService` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Low |
| Auto-update | Sparkle | NetSparkleUpdater (MIT, Sparkle-style appcast, Ed25519) | Medium: separate key and feed |
| Settings backup | JSON via `NSSavePanel` | Same JSON format via `SaveFileDialog` | Low |
| Packaging | DMG, ad-hoc or `Tatami Dev` signature | Inno Setup per-user installer, unsigned (SmartScreen warning, documented like Gatekeeper) | Low |

## 2. Decisions

1. **Stack: C# 13 / .NET 10 (LTS) / WPF**, Win32 through P/Invoke (`LibraryImport`). Rejected: Swift on Windows (no usable UI framework, toolchain churn), WinUI 3 (deployment of the Windows App Runtime and packaging complexity for a tray utility), Electron/Tauri (new runtime, heavy for a 2 MB utility).
2. **Repository layout.** macOS stays at the root (`Package.swift`, `Sources/`, `Tests/`, `Resources/`). Windows goes in `Windows/`. Nothing in the macOS build moves.
3. **Shared across platforms:** `Resources/<lang>.lproj/Localizable.strings` (single source of translations, parsed by the Windows app at runtime), the settings backup JSON format, the icon design, `README.md`, `AGENTS.md`, `THIRD_PARTY_NOTICES.md`, the localization check script, and the two GitHub workflows.
4. **No code sharing of Swift.** The shared logic is small; it is reimplemented in C# with the same tests.
5. **Updates:** NetSparkleUpdater with its own Ed25519 key pair and its own feed file `appcast-windows.xml`, published on the same GitHub release as the macOS `appcast.xml`. Sparkle on macOS is not touched, so a mistake on the Windows side can never break macOS updates. The Windows public key is embedded in the app; the private key is the repository secret `NETSPARKLE_PRIVATE_KEY` (plus `NETSPARKLE_PUBLIC_KEY` for the generator).
6. **Installer:** Inno Setup, per-user (`PrivilegesRequired=lowest`, installs to `%LocalAppData%\Programs\Tatami`), so updates never need UAC. Self-contained `win-x64` publish (users need no .NET runtime). Not code-signed for now: SmartScreen shows "Windows protected your PC" on first run, documented like the macOS Gatekeeper dialog.
7. **Dependencies (Windows only):** `NetSparkleUpdater.SparkleUpdater` and `NetSparkleUpdater.UI.WPF` (both MIT), plus `xunit` for tests. Nothing else. Tray icon and Win32 calls are hand-written P/Invoke.
8. **Windows version numbers** come from the same source as macOS: the release tag. Local builds use `git describe --tags`.
9. **Cross-platform backup files.** `SettingsBackup` gets an optional `platform` field (`"macos"` or `"windows"`). Key combos are hardware-specific (Carbon key codes vs. virtual-key codes), so importing a file from the other platform keeps language, snap, grid, and shortcut areas but drops the key combinations. Files without the field are treated as macOS files (they predate Windows). Both apps write the field.

## 3. Architecture of the Windows app

```
Windows/
  Tatami.sln
  Tatami/                      WPF app, net10.0-windows, PerMonitorV2 DPI, single instance
    Tatami.csproj              ApplicationIcon=..\Resources\AppIcon.ico, links ..\..\Resources\*.lproj\Localizable.strings as content
    app.manifest               dpiAware PerMonitorV2, longPathAware
    App.xaml / App.xaml.cs     ThemeMode=System; startup wiring; single-instance mutex
    Core/                      Pure logic, no WPF or Win32 (unit-tested)
      Grid.cs                  Grid, GridSelection, Grid.Frame(selection, area)  (top-left origin: no flip needed)
      WindowShortcut.cs
      KeyCombo.cs              VirtualKey + Modifiers (Win32 MOD_* flags) + Display ("Ctrl+Alt+Shift+W")
      SettingsBackup.cs        Same JSON schema as macOS + "platform"
      WindowGeometry.cs        RelativeFrame(frame, from, to), SnapZone.At(point, screen, margin)
      StringsFile.cs           Parser for Apple .strings format (quotes, \" \n \\ escapes, // and /* */ comments)
      Localizer.cs             Current language, L(key), L(key, args) with %d/%@/%ld, falls back to English then key
    Win32/                     P/Invoke only, no logic
      NativeMethods.cs, Monitors.cs, WindowMover.cs, HotKeyCenter.cs, WinEventHook.cs, TrayIcon.cs, StartupRegistry.cs
    AppSettings.cs             Observable settings, persisted to %AppData%\Tatami\settings.json
    GridController.cs, GridPanelWindow.xaml, GridView.cs (custom drawn control, drag selection, Esc)
    SnapController.cs, SnapPreviewWindow.xaml
    ShortcutController.cs
    SettingsWindow.xaml, HelpWindow.xaml, AboutWindow.xaml, ShortcutRecorder (control), GridPicker (control)
    SettingsTransfer.cs        Export/Import dialogs
    Updater.cs                 NetSparkle wiring (check at launch if enabled, manual check from menu)
    TatamiIcon.cs              Tray icon drawn in code (same five-mat layout), black or white depending on taskbar theme
  Tatami.Tests/                xunit: Grid, WindowGeometry, KeyCombo display, SettingsBackup round trip, StringsFile against the real .lproj files
  Installer/Tatami.iss         Inno Setup script
  Resources/AppIcon.ico        Generated by scripts/make-icon-windows.ps1, committed
```

### Windows-specific rules the implementation must follow

- **Coordinates.** Win32 uses the top-left origin, y down, physical pixels. Grid row 0 is the top row, so `Grid.Frame` maps rows directly (no flip). `SnapZone`: top edge is `minY`. Work area is `MONITORINFO.rcWork`.
- **DPI.** Declare PerMonitorV2 in `app.manifest`. All window moving is done in physical pixels through `SetWindowPos`; never through WPF `Left/Top` for other apps' windows. Panel windows are also placed with `SetWindowPos` (physical pixels, centred on the monitor's work area, width 360 DIPs scaled by that monitor's DPI). The snap edge margin is 5 DIPs scaled by the monitor's DPI.
- **Frame bounds.** `GetWindowRect` includes invisible resize borders on Windows 10/11. Read the visible bounds with `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`, compute the per-side offsets against `GetWindowRect`, and compensate when calling `SetWindowPos` so the visible frame lands exactly on the grid cell.
- **Maximized windows.** `ShowWindow(SW_RESTORE)` before `SetWindowPos`, otherwise the move is ignored.
- **Target window.** `GetForegroundWindow()`, rejected when it belongs to Tatami, is not visible, is cloaked (`DWMWA_CLOAKED`), is the desktop/shell (`Progman`, `WorkerW`, `Shell_TrayWnd`), or has no `WS_CAPTION`/is a tool window. When the hotkey fires, capture the foreground window **before** showing any Tatami window. For the tray menu, capture the foreground window before the menu opens (the menu itself activates Tatami).
- **Elevated windows.** `SetWindowPos` fails silently for admin windows (UIPI). Beep, as macOS does when there is no window; Help explains it.
- **Snap.** `SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`. On start: remember the window and its frame; poll `GetCursorPos` on a 16 ms `DispatcherTimer`. It counts as moving only when the frame's size is unchanged and its position changed (excludes resizing), same heuristic as macOS. Show the preview when the cursor is in a zone of the monitor under the cursor (`MonitorFromPoint`). On end: apply the pending frame after a 50 ms delay (Windows applies its own final position first), then reset. Windows Snap also reacts at the top/left/right edges; the README and Help tell users to turn off "Snap windows" in Settings → System → Multitasking.
- **Hotkeys.** `RegisterHotKey(hwnd, id, MOD_* | MOD_NOREPEAT, vk)` on a message-only window (`HWND_MESSAGE`) created with a WPF `HwndSource`. `MOD_WIN` counts as a modifier. Recording rule mirrors macOS: require Ctrl, Alt, or Win, except F1–F12 which may be bare; Shift alone is not enough. Unregister everything while recording. `RegisterHotKey` returning false marks the shortcut as conflicting, like macOS.
- **Grid panel.** One borderless, topmost, non-resizable WPF window per monitor, `ShowInTaskbar=false`, semi-transparent dark rounded background. The panel on the target window's monitor gets keyboard focus so Esc cancels; clicking outside any panel closes them all (track `Deactivated` across all panels, same as `windowDidResignKey`). Releasing the drag applies the selection on **that panel's** monitor work area.
- **Single instance.** A named mutex; a second launch exits quietly.
- **Localization.** `Localizer` loads `Locales/<code>/Localizable.strings` from the app folder (the csproj copies the shared files). WPF binds through `{Binding [key], Source={x:Static loc:Loc.Instance}}`; on language change `Loc` raises `PropertyChanged("Item[]")` and every binding refreshes without reopening windows. Windows set `FlowDirection` from `AppLanguage.IsRightToLeft`. Tray menu item headers are refreshed every time the menu opens.
- **Settings file.** `%AppData%\Tatami\settings.json`, written atomically (temp file + replace). Same property names as the backup format, plus `launchAtLogin` is never stored (it is read from the registry).
- **Comments** in the C# code are in Traditional Chinese, as in the Swift code. Identifiers in English.

### New localization keys (added to all 10 `.strings` files)

Windows reuses every existing key whose text is platform-neutral. Keys whose macOS text does not apply get a Windows variant with a `.windows` suffix, and the Windows app asks for the `.windows` key first and falls back to the base key:

- `help.snap.body.windows` (Settings → System → Multitasking instead of Desktop & Dock)
- `help.permission.title.windows`, `help.permission.body.windows` (no permission needed; windows of apps running as administrator cannot be moved unless Tatami also runs as administrator)
- `help.updates.body.windows` (installer runs and relaunches)
- `help.backup.body.windows` ("another PC")
- `about.credits.windows` (NetSparkle instead of Sparkle)
- `general.autoUpdate.subtitle` is reused as is.
- `menu.permission.*` is not shown on Windows.
- Tray tooltip: "Tatami" is a proper noun and needs no key.

The localization check scans `Windows/**/*.cs` and `Windows/**/*.xaml` as well as `Sources/`.

## 4. Build, CI, and release

### Scripts

| Script | Platform | Does |
|---|---|---|
| `scripts/build-windows.ps1` | Windows | `dotnet publish` (`win-x64`, self-contained, single file off, ReadyToRun on) into `build/windows/publish/`, version from `VERSION` env or `git describe --tags`; then, if `ISCC.exe` is found (`$env:ISCC`, PATH, or `C:\Program Files (x86)\Inno Setup 6`), compiles `Windows/Installer/Tatami.iss` into `build/windows/Tatami-<version>-Setup.exe`. Prints both paths. |
| `scripts/make-appcast-windows.ps1` | Windows | Signs the installer with `netsparkle-generate-appcast` and writes `appcast-windows.xml` (`sparkle:os="windows"`, `sparkle:version` numeric, `sparkle:shortVersionString`, `edSignature` attribute). Needs `SPARKLE_PRIVATE_KEY`/`SPARKLE_PUBLIC_KEY` env (mapped from the `NETSPARKLE_*` secrets). |
| `scripts/make-icon-windows.ps1` | Windows | Draws the five-mat icon with System.Drawing at 16/24/32/48/64/128/256 px and packs `Windows/Resources/AppIcon.ico`. Run only when the design changes; commit the result. |
| `scripts/check_localizations.py` | Both | The existing check, rewritten without `plutil` so it runs on Windows runners; also scans `Windows/`. `scripts/check-localizations.sh` becomes a one-line wrapper. |

### `ci.yml`

Two jobs run in parallel on every PR and push to `main`:

- `macos` (existing job, unchanged): localization check, `swift test`, app build, bundle verification, DMG.
- `windows` on `windows-latest`: `actions/setup-dotnet` (10.x), localization check, `dotnet test Windows/Tatami.sln`, `scripts/build-windows.ps1` (installs Inno Setup with `choco install innosetup` when `ISCC.exe` is absent from the image), verifies the installer exists, uploads `build/windows/*.exe` as an artifact so a PR can be tried without a release.

### `release.yml`

Two jobs on `release: published`, both reading the version from the tag with the same regex:

- `macos`: the existing job.
- `windows`: version to `$env:VERSION`, test, build installer, `Get-FileHash -Algorithm SHA256` → `Tatami-<version>-Setup.exe.sha256`, `make-appcast-windows.ps1`, `gh release upload` of the installer, its hash, and `appcast-windows.xml` (`--clobber`). Fails if `NETSPARKLE_PRIVATE_KEY` is missing, mirroring the macOS job.

Both jobs upload to the same release, so one tag publishes both platforms. The Windows feed URL compiled into the app is `https://github.com/lenny-osp/tatami/releases/latest/download/appcast-windows.xml`.

### Required new secrets

`NETSPARKLE_PRIVATE_KEY` and `NETSPARKLE_PUBLIC_KEY` from `netsparkle-generate-appcast --generate-keys` followed by `--export`. The public key is also pasted into `Windows/Tatami/Updater.cs`. If the key is ever replaced, installed copies stop accepting updates until users install a build with the new key manually, exactly as on macOS.

## 5. Work packages and file ownership

Two Opus 5.5 agents run in parallel on disjoint files. Neither commits.

**W1 — Windows app** (owns `Windows/**` except `Windows/Installer/`, `scripts/build-windows.ps1`, `scripts/make-icon-windows.ps1`, `Resources/*.lproj/Localizable.strings` additions, `THIRD_PARTY_NOTICES.md` additions, `.gitignore` additions). Done when: `dotnet test` passes, `scripts/build-windows.ps1` publishes, the app runs from `build/windows/publish/Tatami.exe` with a tray icon, hotkeys, grid panel, snap, settings, help, about, backup, and language switching, and the new keys exist in all 10 languages.

**W2 — Pipeline, installer, docs, macOS backup field** (owns `.github/workflows/*.yml`, `Windows/Installer/Tatami.iss`, `scripts/check_localizations.py` + `.sh` wrapper, `scripts/make-appcast-windows.ps1`, `README.md`, `AGENTS.md`, `Sources/Tatami/SettingsBackup.swift`, `Sources/Tatami/AppSettings.swift`, `Tests/TatamiTests/SettingsBackupTests.swift`). Done when: both workflows are valid YAML with the two jobs, the Inno script compiles against the publish layout defined above, the localization check runs with plain Python on Windows, the README has a Windows section (requirements, install, SmartScreen, build from source, troubleshooting, uninstall) and AGENTS.md documents the Windows layout, conventions, and release steps, and the Swift change compiles logically (it is verified by the macOS CI job; there is no Mac available locally).

**Review (Fable):** read every file, run tests and the build on this machine, launch the app, fix or send back findings, then run the localization check end to end.

## 6. Acceptance checklist (status as of 2026-10-22, one 1600×900 display at 100%)

- [x] `dotnet test Windows/Tatami.sln` passes (73 tests).
- [x] `./scripts/build-windows.ps1` produces `build/windows/publish/Tatami.exe` with no warnings. The installer step was skipped locally (no Inno Setup); it is verified by CI.
- [x] `python scripts/check_localizations.py` passes (10 languages × 74 keys, 68 keys used in code).
- [x] App launches, registers the tray icon (in the overflow area), second launch exits.
- [x] Hotkey → custom shortcut moved a Notepad window to the exact left half of the work area, also from a maximized state (borders compensated, window restored first). Show Grid hotkey opened a 360×192 panel with focus; Esc closed it and returned focus. Tested by script, not by hand.
- [x] Drag to the top edge (synthesized mouse input, Windows Snap on) showed the preview and filled the work area without maximizing; a corner drag showed no preview and did not snap.
- [ ] Custom shortcut recorded by hand in the settings window; conflict display. Logic verified by unit tests and a throwaway harness only.
- [ ] Language switch and Arabic RTL in live windows: rendered off-screen to images by the implementing agent, not seen by a person.
- [x] Export/import: macOS file keeps everything but key combos (unit tests on both platforms).
- [x] Launch at login writes and removes the Run value (throwaway harness).
- [x] Background update check with the placeholder key and a 404 feed: no crash, no window.
- [x] `ci.yml` and `release.yml` have both jobs. Not yet run on GitHub: needs a push.
- [ ] Multi-monitor and mixed DPI: not testable on this machine (one display).
- [ ] macOS `platform` change: not compiled locally (no Mac); the `macos` CI job is the verification.

## 7. Out of scope (follow-ups)

- `win-arm64` installer and `sparkle:os="windows-arm64"` item.
- Code signing (Azure Trusted Signing) to remove the SmartScreen warning.
- winget manifest and/or MSIX.
- Acrylic/Mica backdrop for the grid panel.
- Moving the macOS package into `macOS/` (left at the root on purpose to avoid churn in a build that cannot be tested here).
