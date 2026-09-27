import CoreGraphics
import Testing
@testable import Tatami

struct GridTests {
    /// 左下原點、400×300 的區域，方便心算
    let area = CGRect(x: 100, y: 50, width: 400, height: 300)

    @Test func fullSelectionFillsTheWholeArea() {
        let grid = Grid(columns: 6, rows: 6)
        let selection = GridSelection(column: 0, row: 0, columns: 6, rows: 6)
        #expect(grid.frame(for: selection, in: area) == area)
    }

    @Test func rowZeroIsTheTopRow() {
        // Cocoa 座標的 y 往上，所以第 0 列要在區域的上半部
        let grid = Grid(columns: 2, rows: 2)
        let topLeft = grid.frame(for: GridSelection(column: 0, row: 0, columns: 1, rows: 1), in: area)
        #expect(topLeft == CGRect(x: 100, y: 200, width: 200, height: 150))
    }

    @Test func bottomRightCell() {
        let grid = Grid(columns: 2, rows: 2)
        let bottomRight = grid.frame(for: GridSelection(column: 1, row: 1, columns: 1, rows: 1), in: area)
        #expect(bottomRight == CGRect(x: 300, y: 50, width: 200, height: 150))
    }

    @Test func adjacentCellsShareAnEdgeWithoutGapsWhenRounding() {
        // 400 / 3 不是整數；相鄰兩格的邊界要完全接上，不能有縫或重疊
        let grid = Grid(columns: 3, rows: 1)
        let left = grid.frame(for: GridSelection(column: 0, row: 0, columns: 1, rows: 1), in: area)
        let middle = grid.frame(for: GridSelection(column: 1, row: 0, columns: 1, rows: 1), in: area)
        let right = grid.frame(for: GridSelection(column: 2, row: 0, columns: 1, rows: 1), in: area)
        #expect(left.maxX == middle.minX)
        #expect(middle.maxX == right.minX)
        #expect(right.maxX == area.maxX)
    }

    @Test(arguments: 1...10)
    func everyAllowedGridSizeCoversTheArea(size: Int) {
        let grid = Grid(columns: size, rows: size)
        let selection = GridSelection(column: 0, row: 0, columns: size, rows: size)
        #expect(grid.frame(for: selection, in: area) == area)
    }
}
