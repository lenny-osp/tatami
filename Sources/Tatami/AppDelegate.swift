import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem!
    private let showGridItem = NSMenuItem()
    private let permissionItem = NSMenuItem()
    private let settingsItem = NSMenuItem()
    private let helpItem = NSMenuItem()
    private let aboutItem = NSMenuItem()
    private let quitItem = NSMenuItem()
    private let snapController = SnapController()
    private lazy var settings = AppSettings(snapController: snapController)
    private lazy var gridController = GridController(settings: settings)
    private lazy var shortcutController = ShortcutController(settings: settings, gridController: gridController)
    private lazy var settingsWindowController = SettingsWindowController(settings: settings)
    private lazy var helpWindowController = HelpWindowController(settings: settings)

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem.button?.image = TatamiIcon.menuBarImage()

        let menu = NSMenu()
        menu.delegate = self
        showGridItem.action = #selector(showGrid)
        showGridItem.target = self
        menu.addItem(showGridItem)
        menu.addItem(.separator())
        permissionItem.target = self
        permissionItem.action = #selector(openAccessibilitySettings)
        menu.addItem(permissionItem)
        menu.addItem(.separator())
        settingsItem.action = #selector(showSettings)
        settingsItem.keyEquivalent = ","
        settingsItem.target = self
        menu.addItem(settingsItem)
        helpItem.action = #selector(showHelp)
        helpItem.target = self
        menu.addItem(helpItem)
        aboutItem.action = #selector(showAbout)
        aboutItem.target = self
        menu.addItem(aboutItem)
        menu.addItem(.separator())
        quitItem.action = #selector(NSApplication.terminate(_:))
        quitItem.keyEquivalent = "q"
        menu.addItem(quitItem)
        statusItem.menu = menu

        if !Accessibility.isTrusted {
            Accessibility.requestIfNeeded()
        }

        // 讀取設定並套用（啟動 Snap、註冊快捷鍵）
        _ = shortcutController
    }

    // 每次打開選單時更新文字：重新檢查權限狀態，並套用目前的語言
    func menuNeedsUpdate(_ menu: NSMenu) {
        showGridItem.title = L("menu.showGrid")
        permissionItem.title = Accessibility.isTrusted
            ? L("menu.permission.granted")
            : L("menu.permission.missing")
        settingsItem.title = L("menu.settings")
        helpItem.title = L("menu.help")
        aboutItem.title = L("menu.about")
        quitItem.title = L("menu.quit")
    }

    @objc private func showGrid() {
        gridController.show()
    }

    @objc private func showAbout() {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? ""
        let credits = NSAttributedString(
            string: L("about.credits"),
            attributes: [
                .font: NSFont.systemFont(ofSize: NSFont.smallSystemFontSize),
                .foregroundColor: NSColor.secondaryLabelColor,
            ]
        )
        // 和其他視窗一樣：等選單關閉後再帶到前景
        DispatchQueue.main.async {
            NSApp.activate()
            NSApp.orderFrontStandardAboutPanel(options: [
                .applicationName: "Tatami",
                .applicationIcon: NSApp.applicationIconImage as Any,
                .applicationVersion: version,
                .version: "",
                .credits: credits,
                NSApplication.AboutPanelOptionKey(rawValue: "Copyright"):
                    "Copyright © 2026 Chihling Wang <chihlingw@gmail.com>",
            ])
        }
    }

    @objc private func showHelp() {
        helpWindowController.show()
    }

    @objc private func showSettings() {
        settingsWindowController.show()
    }

    @objc private func openAccessibilitySettings() {
        Accessibility.openSettings()
    }
}
