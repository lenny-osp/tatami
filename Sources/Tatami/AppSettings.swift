import Foundation

/// 內建功能的快捷鍵（使用者設定了按鍵才會啟用）。
enum FixedShortcut: Hashable {
    case showGrid, nextScreen
}

/// 設定畫面用的狀態，改動時同步套用到實際功能。
@MainActor
final class AppSettings: ObservableObject {
    private static let snapEnabledKey = "snapEnabled"
    private static let gridColumnsKey = "gridColumns"
    private static let gridRowsKey = "gridRows"
    private static let shortcutsKey = "shortcuts"
    private static let showGridKeyComboKey = "showGridKeyCombo"
    private static let nextScreenKeyComboKey = "nextScreenKeyCombo"
    private static let languageKey = "language"

    /// 格線欄數、列數的允許範圍
    static let gridSizeRange = 1...10

    private let snapController: SnapController

    @Published private(set) var isSnapEnabled: Bool
    @Published private(set) var isLaunchAtLoginEnabled: Bool

    @Published var gridColumns: Int {
        didSet { UserDefaults.standard.set(gridColumns, forKey: Self.gridColumnsKey) }
    }
    @Published var gridRows: Int {
        didSet { UserDefaults.standard.set(gridRows, forKey: Self.gridRowsKey) }
    }

    @Published var shortcuts: [WindowShortcut] {
        didSet {
            if let data = try? JSONEncoder().encode(shortcuts) {
                UserDefaults.standard.set(data, forKey: Self.shortcutsKey)
            }
            onShortcutsChanged?()
        }
    }
    /// 介面語言，預設英文
    @Published var language: AppLanguage {
        didSet {
            UserDefaults.standard.set(language.rawValue, forKey: Self.languageKey)
            Localizer.language = language
            languageObservers.forEach { $0() }
        }
    }
    /// 語言切換時要通知的 AppKit 部分（例如視窗標題、分頁名稱）
    private var languageObservers: [() -> Void] = []

    func observeLanguage(_ observer: @escaping () -> Void) {
        languageObservers.append(observer)
    }

    /// 「顯示格線」快捷鍵，nil 表示不啟用
    @Published var showGridKeyCombo: KeyCombo? {
        didSet {
            Self.save(showGridKeyCombo, forKey: Self.showGridKeyComboKey)
            onShortcutsChanged?()
        }
    }
    /// 「切換螢幕」快捷鍵，nil 表示不啟用
    @Published var nextScreenKeyCombo: KeyCombo? {
        didSet {
            Self.save(nextScreenKeyCombo, forKey: Self.nextScreenKeyComboKey)
            onShortcutsChanged?()
        }
    }
    /// 無法註冊（已被其他程式使用）的固定功能快捷鍵
    @Published var conflictingFixedShortcuts: Set<FixedShortcut> = []

    /// 正在錄製快捷鍵（這段期間要暫停所有全域快捷鍵）
    @Published var isRecordingShortcut = false {
        didSet { onShortcutsChanged?() }
    }
    /// 無法註冊（已被其他程式使用）的快捷鍵
    @Published var conflictingShortcutIDs: Set<UUID> = []

    /// 快捷鍵設定變動時通知 ShortcutController 重新註冊
    var onShortcutsChanged: (() -> Void)?

    /// 格線面板目前要用的格線
    var grid: Grid {
        Grid(columns: gridColumns, rows: gridRows)
    }

    init(snapController: SnapController) {
        self.snapController = snapController
        UserDefaults.standard.register(defaults: [
            Self.snapEnabledKey: true,
            Self.gridColumnsKey: 6,
            Self.gridRowsKey: 6,
        ])
        isSnapEnabled = UserDefaults.standard.bool(forKey: Self.snapEnabledKey)
        let language = UserDefaults.standard.string(forKey: Self.languageKey).flatMap(AppLanguage.init) ?? .english
        self.language = language
        // init 裡不會觸發 didSet，要自己設定
        Localizer.language = language
        gridColumns = Self.clamp(UserDefaults.standard.integer(forKey: Self.gridColumnsKey))
        gridRows = Self.clamp(UserDefaults.standard.integer(forKey: Self.gridRowsKey))
        isLaunchAtLoginEnabled = LaunchAtLogin.isEnabled
        shortcuts = UserDefaults.standard.data(forKey: Self.shortcutsKey)
            .flatMap { try? JSONDecoder().decode([WindowShortcut].self, from: $0) } ?? []
        showGridKeyCombo = Self.load(forKey: Self.showGridKeyComboKey)
        nextScreenKeyCombo = Self.load(forKey: Self.nextScreenKeyComboKey)

        if isSnapEnabled { snapController.start() }
    }

    func setSnapEnabled(_ enabled: Bool) {
        if enabled {
            snapController.start()
        } else {
            snapController.stop()
        }
        isSnapEnabled = snapController.isRunning
        UserDefaults.standard.set(isSnapEnabled, forKey: Self.snapEnabledKey)
    }

    func addShortcut() {
        shortcuts.append(.new(name: L("shortcut.defaultName"), grid: grid))
    }

    func deleteShortcut(_ id: UUID) {
        shortcuts.removeAll { $0.id == id }
    }

    func setLaunchAtLogin(_ enabled: Bool) {
        LaunchAtLogin.setEnabled(enabled)
        isLaunchAtLoginEnabled = LaunchAtLogin.isEnabled
    }

    private static func save(_ combo: KeyCombo?, forKey key: String) {
        if let combo, let data = try? JSONEncoder().encode(combo) {
            UserDefaults.standard.set(data, forKey: key)
        } else {
            UserDefaults.standard.removeObject(forKey: key)
        }
    }

    private static func load(forKey key: String) -> KeyCombo? {
        UserDefaults.standard.data(forKey: key).flatMap { try? JSONDecoder().decode(KeyCombo.self, from: $0) }
    }

    private static func clamp(_ value: Int) -> Int {
        min(max(value, gridSizeRange.lowerBound), gridSizeRange.upperBound)
    }

    /// 使用者可能在系統設定裡改過「登入項目」，打開設定畫面時重新讀取。
    func refresh() {
        isLaunchAtLoginEnabled = LaunchAtLogin.isEnabled
    }
}
