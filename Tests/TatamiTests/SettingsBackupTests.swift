import Foundation
import Testing
@testable import Tatami

struct SettingsBackupTests {
    @Test func roundTripKeepsEverySetting() throws {
        let grid = Grid(columns: 4, rows: 3)
        let backup = SettingsBackup(
            language: "ja",
            isSnapEnabled: false,
            isAutoUpdateEnabled: true,
            gridColumns: 4,
            gridRows: 3,
            showGridKeyCombo: KeyCombo(keyCode: 5, modifiers: 6144, display: "⌃⌥G"),
            nextScreenKeyCombo: nil,
            shortcuts: [.new(name: "Left", grid: grid)]
        )
        let decoded = try JSONDecoder().decode(SettingsBackup.self, from: JSONEncoder().encode(backup))

        #expect(decoded.app == "Tatami")
        #expect(decoded.formatVersion == SettingsBackup.currentFormatVersion)
        #expect(decoded.platform == "macos")
        #expect(decoded.language == "ja")
        #expect(decoded.isSnapEnabled == false)
        #expect(decoded.gridColumns == 4)
        #expect(decoded.gridRows == 3)
        #expect(decoded.showGridKeyCombo == backup.showGridKeyCombo)
        #expect(decoded.nextScreenKeyCombo == nil)
        #expect(decoded.shortcuts == backup.shortcuts)
    }

    @Test func fileWithOnlyTheHeaderStillImports() throws {
        // 其他版本的檔案可能少了某些欄位，要能讀，缺的欄位是 nil（保留目前設定）
        let json = #"{"app": "Tatami", "formatVersion": 1}"#
        let decoded = try JSONDecoder().decode(SettingsBackup.self, from: Data(json.utf8))
        #expect(decoded.language == nil)
        #expect(decoded.shortcuts == nil)
        #expect(decoded.platform == nil)
    }

    @Test func exportedFileIsMarkedAsMacOS() throws {
        let data = try JSONEncoder().encode(SettingsBackup())
        let json = try #require(String(data: data, encoding: .utf8))
        #expect(json.contains(#""platform":"macos""#))
        #expect(try JSONDecoder().decode(SettingsBackup.self, from: data).isFromOtherPlatform == false)
    }

    @Test func fileWithoutPlatformIsTreatedAsMacOS() throws {
        // Windows 版出現以前匯出的檔案沒有 platform 欄位，按鍵組合照常匯入
        let json = #"{"app": "Tatami", "formatVersion": 1, "showGridKeyCombo": {"keyCode": 5, "modifiers": 6144, "display": "⌃⌥G"}}"#
        let decoded = try JSONDecoder().decode(SettingsBackup.self, from: Data(json.utf8))
        #expect(decoded.platform == nil)
        #expect(decoded.isFromOtherPlatform == false)
        #expect(SettingsBackup.stripForeignKeyCombos(decoded).showGridKeyCombo == decoded.showGridKeyCombo)
    }

    @Test func fileFromWindowsKeepsShortcutsButDropsKeyCombos() throws {
        let json = #"""
        {
          "app": "Tatami",
          "formatVersion": 1,
          "platform": "windows",
          "language": "de",
          "gridColumns": 4,
          "showGridKeyCombo": {"keyCode": 71, "modifiers": 3, "display": "Ctrl+Alt+G"},
          "nextScreenKeyCombo": {"keyCode": 78, "modifiers": 3, "display": "Ctrl+Alt+N"},
          "shortcuts": [{
            "id": "0B1C2D3E-4F50-6172-8394-A5B6C7D8E9F0",
            "name": "Left",
            "keyCombo": {"keyCode": 37, "modifiers": 3, "display": "Ctrl+Alt+Left"},
            "grid": {"columns": 2, "rows": 1},
            "selection": {"column": 0, "row": 0, "columns": 1, "rows": 1}
          }]
        }
        """#
        let decoded = try JSONDecoder().decode(SettingsBackup.self, from: Data(json.utf8))
        #expect(decoded.platform == "windows")
        #expect(decoded.isFromOtherPlatform)

        let stripped = SettingsBackup.stripForeignKeyCombos(decoded)
        // 按鍵組合全部拿掉
        #expect(stripped.showGridKeyCombo == nil)
        #expect(stripped.nextScreenKeyCombo == nil)
        // 其他設定和快捷鍵本身都保留
        #expect(stripped.language == "de")
        #expect(stripped.gridColumns == 4)
        let shortcut = try #require(stripped.shortcuts?.first)
        #expect(stripped.shortcuts?.count == 1)
        #expect(shortcut.name == "Left")
        #expect(shortcut.keyCombo == nil)
        #expect(shortcut.grid == Grid(columns: 2, rows: 1))
        #expect(shortcut.selection == GridSelection(column: 0, row: 0, columns: 1, rows: 1))
        #expect(shortcut.id == decoded.shortcuts?.first?.id)
    }

    @Test func unrelatedJSONIsRejected() {
        let json = #"{"name": "something else"}"#
        #expect(throws: DecodingError.self) {
            try JSONDecoder().decode(SettingsBackup.self, from: Data(json.utf8))
        }
    }

    @Test func newShortcutSelectsTheWholeGrid() {
        let shortcut = WindowShortcut.new(name: "New", grid: Grid(columns: 5, rows: 2))
        #expect(shortcut.selection == GridSelection(column: 0, row: 0, columns: 5, rows: 2))
        #expect(shortcut.keyCombo == nil)
    }
}
