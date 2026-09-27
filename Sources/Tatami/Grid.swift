import CoreGraphics

/// 格線上選取的範圍。row 0 是最上面那一列，column 0 是最左邊那一欄。
struct GridSelection: Codable, Equatable {
    var column: Int
    var row: Int
    var columns: Int
    var rows: Int
}

struct Grid: Codable, Equatable {
    var columns = 6
    var rows = 6

    /// 把格線選取範圍換算成實際的矩形（Cocoa 座標：原點在左下，y 往上）。
    func frame(for selection: GridSelection, in area: CGRect) -> CGRect {
        let cellWidth = area.width / CGFloat(columns)
        let cellHeight = area.height / CGFloat(rows)

        let minX = area.minX + CGFloat(selection.column) * cellWidth
        let maxX = area.minX + CGFloat(selection.column + selection.columns) * cellWidth
        // row 從上往下數，但 Cocoa 的 y 從下往上，所以要從 maxY 往下扣
        let maxY = area.maxY - CGFloat(selection.row) * cellHeight
        let minY = area.maxY - CGFloat(selection.row + selection.rows) * cellHeight

        // 先把四條邊各自取整數再算寬高，相鄰兩格的邊界才會完全接上，不會有 1 點的縫
        let left = minX.rounded()
        let right = maxX.rounded()
        let bottom = minY.rounded()
        let top = maxY.rounded()
        return CGRect(x: left, y: bottom, width: right - left, height: top - bottom)
    }
}
