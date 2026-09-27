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
