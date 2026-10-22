import AppKit
import UniformTypeIdentifiers

/// 匯出／匯入設定的檔案格式（JSON）。
///
/// 除了 app 和 formatVersion，其他欄位都是 optional：
/// 匯入其他版本的檔案時，少了的欄位就保留目前的設定。
/// 「登入時啟動」是這台 Mac 的系統設定，不包含在內。
///
/// Windows 版使用相同的格式。platform 記錄檔案是哪個平台匯出的（"macos" 或 "windows"），
/// 沒有這個欄位的檔案是 Windows 版出現以前的 macOS 檔案。
struct SettingsBackup: Codable {
    static let appIdentifier = "Tatami"
    static let currentFormatVersion = 1
    static let currentPlatform = "macos"

    var app = appIdentifier
    var formatVersion = currentFormatVersion
    var platform: String? = currentPlatform
    var language: String?
    var isSnapEnabled: Bool?
    var isAutoUpdateEnabled: Bool?
    var gridColumns: Int?
    var gridRows: Int?
    var showGridKeyCombo: KeyCombo?
    var nextScreenKeyCombo: KeyCombo?
    var shortcuts: [WindowShortcut]?
}

extension SettingsBackup {
    /// 檔案是不是其他平台（例如 Windows）匯出的
    var isFromOtherPlatform: Bool {
        platform.map { $0 != Self.currentPlatform } ?? false
    }

    /// 其他平台的按鍵代碼和 Mac 不相容（Windows 用 virtual-key code），匯入時拿掉所有按鍵組合。
    /// 快捷鍵本身（名稱、格線、區塊）保留，使用者再重新錄製按鍵即可；這台 Mac 的檔案原樣回傳。
    static func stripForeignKeyCombos(_ backup: SettingsBackup) -> SettingsBackup {
        guard backup.isFromOtherPlatform else { return backup }
        var stripped = backup
        stripped.showGridKeyCombo = nil
        stripped.nextScreenKeyCombo = nil
        stripped.shortcuts = backup.shortcuts?.map { shortcut in
            var shortcut = shortcut
            shortcut.keyCombo = nil
            return shortcut
        }
        return stripped
    }
}

/// 設定畫面「匯出設定…」「匯入設定…」按鈕的流程：選檔案、確認、顯示結果。
@MainActor
enum SettingsTransfer {
    static func exportSettings(from settings: AppSettings) {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.json]
        panel.nameFieldStringValue = L("backup.defaultFileName") + ".json"
        guard panel.runModal() == .OK, let url = panel.url else { return }

        do {
            let encoder = JSONEncoder()
            // 排版整齊，使用者也能用文字編輯器看懂
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            try encoder.encode(settings.makeBackup()).write(to: url, options: .atomic)
        } catch {
            showAlert(title: L("backup.exportError.title"), message: error.localizedDescription, style: .warning)
        }
    }

    static func importSettings(into settings: AppSettings) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.json]
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let fileName = url.lastPathComponent

        guard let data = try? Data(contentsOf: url),
              let backup = try? JSONDecoder().decode(SettingsBackup.self, from: data),
              backup.app == SettingsBackup.appIdentifier
        else {
            showAlert(
                title: L("backup.error.title"),
                message: L("backup.error.message", fileName),
                style: .warning
            )
            return
        }

        // 匯入會蓋掉目前的設定，先確認
        let confirm = NSAlert()
        confirm.messageText = L("backup.import.confirm.title")
        confirm.informativeText = L("backup.import.confirm.message", fileName)
        confirm.addButton(withTitle: L("backup.import.confirm.button"))
        confirm.addButton(withTitle: L("backup.cancel"))
        guard confirm.runModal() == .alertFirstButtonReturn else { return }

        settings.apply(backup)
        // 語言可能已經換了，成功訊息用新的語言顯示
        showAlert(title: L("backup.import.success"), message: "", style: .informational)
    }

    private static func showAlert(title: String, message: String, style: NSAlert.Style) {
        let alert = NSAlert()
        alert.alertStyle = style
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: L("backup.ok"))
        alert.runModal()
    }
}
