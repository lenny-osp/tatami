import AppKit
import SwiftUI

/// 設定畫面的視窗：上方是 macOS 標準的圖示分頁，每個分頁的內容用 SwiftUI 寫。
@MainActor
final class SettingsWindowController {
    private let settings: AppSettings
    private var window: NSWindow?
    private var tabs: NSTabViewController?
    /// 每個分頁對應的翻譯 key，語言切換時用來更新分頁名稱
    private let tabLabelKeys = ["tab.general", "tab.arrangement"]

    init(settings: AppSettings) {
        self.settings = settings
        settings.observeLanguage { [weak self] in self?.updateTabLabels() }
    }

    func show() {
        settings.refresh()
        if window == nil {
            window = makeWindow()
        }
        // 從選單列點選時，選單還在關閉中，這時啟用 App 常常會被系統忽略，
        // 視窗就會開在其他 App 後面。所以等選單關閉後再啟用。
        DispatchQueue.main.async { [weak self] in
            guard let window = self?.window else { return }
            NSApp.activate()
            window.makeKeyAndOrderFront(nil)
            // 就算啟用失敗，也保證視窗出現在最前面
            window.orderFrontRegardless()
        }
    }

    private func makeWindow() -> NSWindow {
        let tabs = NSTabViewController()
        tabs.tabStyle = .toolbar
        tabs.addTabViewItem(tab(symbol: "gearshape", view: GeneralSettingsView(settings: settings)))
        tabs.addTabViewItem(tab(symbol: "rectangle.split.2x1", view: ArrangementSettingsView(settings: settings)))
        self.tabs = tabs

        let window = NSWindow(contentViewController: tabs)
        window.styleMask = [.titled, .closable]
        window.toolbarStyle = .preference
        window.isReleasedWhenClosed = false
        window.center()
        updateTabLabels()
        return window
    }

    private func tab(symbol: String, view: some View) -> NSTabViewItem {
        let controller = NSHostingController(rootView: view.localized(settings))
        // 讓視窗依照分頁內容的大小調整
        controller.sizingOptions = .preferredContentSize
        let item = NSTabViewItem(viewController: controller)
        item.image = NSImage(systemSymbolName: symbol, accessibilityDescription: nil)
        return item
    }

    private func updateTabLabels() {
        guard let tabs else { return }
        for (item, key) in zip(tabs.tabViewItems, tabLabelKeys) {
            item.label = L(key)
            // 切換分頁時，NSTabViewController 會把分頁內容的 title 當成視窗標題；
            // 沒設定的話視窗會顯示「Untitled」
            item.viewController?.title = L(key)
        }
        // 視窗標題顯示目前分頁的名稱
        tabs.view.window?.title = tabs.tabViewItems[tabs.selectedTabViewItemIndex].label
    }
}

// MARK: - 分頁內容

private struct LocalizedModifier: ViewModifier {
    @ObservedObject var settings: AppSettings

    func body(content: Content) -> some View {
        content
            .environment(\.locale, Locale(identifier: settings.language.rawValue))
            // 阿拉伯文由右到左排列
            .environment(\.layoutDirection, settings.language.isRightToLeft ? .rightToLeft : .leftToRight)
    }
}

extension View {
    /// 套用設定裡選的語言（語系與文字方向）
    func localized(_ settings: AppSettings) -> some View {
        modifier(LocalizedModifier(settings: settings))
    }
}

/// 各分頁統一的寬度，切換時視窗才不會忽寬忽窄。
private let settingsWidth: CGFloat = 420

struct GeneralSettingsView: View {
    @ObservedObject var settings: AppSettings

    var body: some View {
        Form {
            Picker(L("general.language"), selection: $settings.language) {
                ForEach(AppLanguage.allCases) { language in
                    Text(language.nativeName).tag(language)
                }
            }
            Toggle(L("general.launchAtLogin"), isOn: Binding(
                get: { settings.isLaunchAtLoginEnabled },
                set: { settings.setLaunchAtLogin($0) }
            ))
            Toggle(isOn: $settings.isAutoUpdateEnabled) {
                Text(L("general.autoUpdate"))
                Text(L("general.autoUpdate.subtitle"))
            }

            Section {
                HStack {
                    Button(L("general.export")) {
                        SettingsTransfer.exportSettings(from: settings)
                    }
                    Button(L("general.import")) {
                        SettingsTransfer.importSettings(into: settings)
                    }
                }
            } header: {
                Text(L("general.backup.header"))
            } footer: {
                Text(L("general.backup.footer"))
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        .frame(width: settingsWidth)
        .fixedSize()
    }
}

struct ArrangementSettingsView: View {
    @ObservedObject var settings: AppSettings

    var body: some View {
        Form {
            Toggle(isOn: Binding(get: { settings.isSnapEnabled }, set: { settings.setSnapEnabled($0) })) {
                Text(L("snap.title"))
                Text(L("snap.subtitle"))
            }

            Section {
                Stepper(value: $settings.gridColumns, in: AppSettings.gridSizeRange) {
                    LabeledContent(L("grid.columns"), value: "\(settings.gridColumns)")
                }
                Stepper(value: $settings.gridRows, in: AppSettings.gridSizeRange) {
                    LabeledContent(L("grid.rows"), value: "\(settings.gridRows)")
                }
            } header: {
                Text(L("grid.header"))
            } footer: {
                Text(L("grid.footer", settings.gridColumns, settings.gridRows))
                    .foregroundStyle(.secondary)
            }

            Section {
                FixedShortcutRow(
                    title: L("fixed.showGrid"),
                    combo: $settings.showGridKeyCombo,
                    settings: settings,
                    hasConflict: settings.conflictingFixedShortcuts.contains(.showGrid)
                )
                FixedShortcutRow(
                    title: L("fixed.nextScreen"),
                    combo: $settings.nextScreenKeyCombo,
                    settings: settings,
                    hasConflict: settings.conflictingFixedShortcuts.contains(.nextScreen)
                )
            } header: {
                Text(L("fixed.header"))
            } footer: {
                Text(L("fixed.footer"))
                    .foregroundStyle(.secondary)
            }

            Section {
                ForEach($settings.shortcuts) { $shortcut in
                    ShortcutRow(
                        shortcut: $shortcut,
                        settings: settings,
                        hasConflict: settings.conflictingShortcutIDs.contains(shortcut.id),
                        onDelete: { settings.deleteShortcut(shortcut.id) }
                    )
                }
                Button(L("shortcuts.add"), systemImage: "plus") {
                    settings.addShortcut()
                }
            } header: {
                Text(L("shortcuts.header"))
            } footer: {
                Text(L("shortcuts.footer"))
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        // 快捷鍵可能很多，固定高度、內容可以捲動
        .frame(width: settingsWidth, height: 560)
    }
}
