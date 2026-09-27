import AppKit
import UniformTypeIdentifiers

/// 匯出／匯入設定的檔案格式（JSON）。
///
/// 除了 app 和 formatVersion，其他欄位都是 optional：
/// 匯入其他版本的檔案時，少了的欄位就保留目前的設定。
/// 「登入時啟動」是這台 Mac 的系統設定，不包含在內。
struct SettingsBackup: Codable {
    static let appIdentifier = "Tatami"
    static let currentFormatVersion = 1

    var app = appIdentifier
    var formatVersion = currentFormatVersion
    var language: String?
    var isSnapEnabled: Bool?
    var isAutoUpdateEnabled: Bool?
    var gridColumns: Int?
    var gridRows: Int?
    var showGridKeyCombo: KeyCombo?
    var nextScreenKeyCombo: KeyCombo?
    var shortcuts: [WindowShortcut]?
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
