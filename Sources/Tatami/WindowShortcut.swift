import Foundation

/// 使用者自訂的快捷鍵：按下後把目前的視窗搬到指定區塊。
struct WindowShortcut: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    var keyCombo: KeyCombo?
    /// 建立時的格線。之後改了格線設定，已經建立的快捷鍵位置也不會跑掉。
    var grid: Grid
    var selection: GridSelection

    /// 新快捷鍵：預設選取整個畫面。
    static func new(name: String, grid: Grid) -> WindowShortcut {
        WindowShortcut(
            name: name,
            grid: grid,
            selection: GridSelection(column: 0, row: 0, columns: grid.columns, rows: grid.rows)
        )
    }
}
