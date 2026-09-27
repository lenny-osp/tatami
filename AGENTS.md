# AGENTS.md

Guidance for AI agents (and humans) working on **Tatami**, a macOS menu bar app that arranges windows on a grid (similar to Divvy).

## Build and run

- Toolchain: Swift Package Manager + Command Line Tools. **Xcode is not required** and there is no `.xcodeproj`.
- Build and package: `./scripts/build-app.sh` → produces `build/Tatami.app`.
- Run: `pkill -x Tatami; open build/Tatami.app`
- Always build with the script, not just `swift build`. The script copies `Info.plist` and the `.lproj` folders into the bundle and signs it. A bare executable has no translations and no stable permissions.
- Signing: the script uses the keychain certificate named `Tatami Dev` if it exists, otherwise an ad-hoc signature. Override with `SIGN_IDENTITY="..."`. Never commit certificates or private keys (`*.p12`, `*.cer`).
- Version numbers: don't edit `CFBundleShortVersionString` by hand. The build script sets it from `VERSION`, or from `git describe --tags`. Releases are built by `.github/workflows/release.yml` when a GitHub release is published; the tag (for example `v1.2.3`) becomes the version shown in the About panel.
- The two `ld: warning: search path ... not found` lines come from Command Line Tools and can be ignored. Any other warning should be fixed.

## Project layout

All source is in `Sources/Tatami/`:

| File | Responsibility |
|---|---|
| `main.swift`, `AppDelegate.swift` | App entry, menu bar item and menu |
| `AppSettings.swift` | All user settings (`ObservableObject`), persisted in `UserDefaults`; notifies observers on changes |
| `Localization.swift` | `AppLanguage`, `Localizer`, and the `L(...)` helper |
| `Grid.swift` | `Grid` / `GridSelection` and the grid → screen rect math |
| `WindowMover.swift` | Accessibility (AX) API: find, read, and set window frames; move to next screen |
| `GridController.swift`, `GridView.swift` | The grid panel (one per screen) and drag selection |
| `SnapController.swift` | Drag-to-screen-edge snapping using a global mouse monitor |
| `HotKeyCenter.swift`, `KeyCombo.swift`, `ShortcutController.swift`, `WindowShortcut.swift` | Global hotkeys (Carbon `RegisterEventHotKey`) and user shortcuts |
| `SettingsWindowController.swift`, `ShortcutSettingsViews.swift` | Settings window (toolbar tabs, SwiftUI content) |
| `HelpWindowController.swift` | Help window |
| `LaunchAtLogin.swift`, `Accessibility.swift` | `SMAppService` login item; Accessibility permission |
| `TatamiIcon.swift` | Menu bar icon and About icon, drawn in code |

Other files: `Resources/Info.plist`, `Resources/<lang>.lproj/Localizable.strings`, `scripts/`.

## Conventions

- **Swift 6 strict concurrency.** UI and AX code is `@MainActor`. C and AppKit callbacks (Carbon handlers, `NSEvent` monitors) hop back with `MainActor.assumeIsolated`.
- **Don't pass `@MainActor` method references as closures to SwiftUI**, for example `Binding(get: ..., set: settings.setFoo)`. Write `set: { settings.setFoo($0) }`. The method-reference form crashes the Swift 6.3 compiler used on GitHub Actions (signal 6, "SmallVector unable to grow"), even though newer local toolchains compile it.
- **Coordinates.** Code works in Cocoa coordinates: origin at the bottom-left of the primary screen, y pointing up. The AX API uses the top-left origin with y pointing down. Convert only inside `WindowMover` (`flip`). Use `screen.visibleFrame`, which excludes the menu bar and the Dock, as the target area.
- **Grid rows** count from the top: `row 0` is the top row.
- **Opening windows from the menu bar.** Call `NSApp.activate()` and `makeKeyAndOrderFront` inside `DispatchQueue.main.async`, then call `orderFrontRegardless()`. If you activate while the menu is still closing, macOS may ignore it and the window opens behind other apps.
- **Code comments are in Traditional Chinese.** Match the existing style. Identifiers are in English.
- Keep changes small and match the surrounding code. Don't add dependencies without asking.

## Localization (required for every user-visible change)

The UI language is chosen **inside the app** (Settings → General → Language). It does not follow the system language, and it defaults to English. Supported languages: `en`, `zh-Hant`, `zh-Hans`, `fr`, `de`, `es`, `hi`, `ja`, `ko`, `ar`.

### Rules

1. **Never hard-code user-visible text.** Use `L("key")`, or `L("key", arg1, arg2)` for `%d`/`%@` formats. This applies everywhere users can see text:
   - Menu bar menu items (`AppDelegate`)
   - Settings window: tab labels, section headers and footers, toggles, pickers, placeholders, tooltips (`.help`)
   - Help window topics (`HelpWindowController`)
   - About panel credits (`about.credits`) and any license or copyright text shown in the UI
   - Alerts and error messages, such as in `LaunchAtLogin`
   - Default values that users see, such as the default name for a new shortcut
2. **Add every new key to all 10 `Localizable.strings` files** in the same change. English (`en.lproj`) is the reference. A missing translation falls back to English, but don't ship missing keys.
3. **Run `./scripts/check-localizations.sh`** after editing translations. It validates the file format and checks that every language has the same keys as English.
4. Name keys by area, for example `menu.*`, `tab.*`, `general.*`, `grid.*`, `snap.*`, `fixed.*`, `shortcuts.*`, `shortcut.*`, `recorder.*`, `help.*`, `about.*`.
5. Escape `"` as `\"` and write newlines as `\n` inside `.strings` values.

### Making text update when the language changes

Switching language takes effect immediately. There is no relaunch.

- **SwiftUI views** re-render automatically if they observe `AppSettings`. Wrap new root views with `.localized(settings)`, which sets the locale and switches to right-to-left layout for Arabic.
- **Menu items** get their titles set in `menuNeedsUpdate(_:)`. Add new items there, not only when the item is created.
- **AppKit text outside SwiftUI**, such as window titles and tab labels, must register with `settings.observeLanguage { ... }` and update itself.
- The standard About panel reads its content when it opens, so it only updates the next time it is opened. That is acceptable.

### Adding a new language

1. Add a case to `AppLanguage` in `Localization.swift`. The raw value is the `.lproj` name; `nativeName` is written in that language.
2. Create `Resources/<code>.lproj/Localizable.strings` with every key from English.
3. Add the code to `CFBundleLocalizations` in `Resources/Info.plist`.
4. If the language is written right to left, update `isRightToLeft`.
5. Run `./scripts/check-localizations.sh`.

Hindi and Arabic translations were machine-written and should be reviewed by native speakers when possible.

## Permissions and system behavior

- Moving other apps' windows requires **Accessibility** permission. If the app is ad-hoc signed, macOS treats each rebuild as a new app and the permission stops working. Remove Tatami in System Settings and add it again. The `Tatami Dev` certificate avoids this.
- Global hotkeys use Carbon `RegisterEventHotKey`, which needs no extra permission. Tools that intercept keys at a lower level, such as BetterTouchTool, can swallow a combination without making registration fail. When that happens, Tatami cannot detect the conflict.
- The built-in macOS "drag windows to screen edges to tile" feature conflicts with Snap. Users should turn it off in System Settings → Desktop & Dock.

## Before finishing a change

1. `./scripts/build-app.sh` succeeds with no new warnings.
2. `./scripts/check-localizations.sh` passes, if any UI text changed.
3. Relaunch the app and check the feature. Also check it in at least one non-English language, and in Arabic if the layout changed.
4. Multi-screen behavior can't be tested with one display. Say so explicitly instead of claiming it works.
5. Update the Help window topics if the change affects how users operate the app.
