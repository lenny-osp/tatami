import AppKit

/// 畫出 6×6 格線，讓使用者用滑鼠拖曳選取範圍。
final class GridView: NSView {
    var grid = Grid() {
        didSet { needsDisplay = true }
    }
    /// 固定顯示的選取範圍（設定畫面的區塊挑選器用），格線面板不需要設定
    var selection: GridSelection? {
        didSet { needsDisplay = true }
    }
    var onSelect: ((GridSelection) -> Void)?
    var onCancel: (() -> Void)?

    var padding: CGFloat = 12
    var gap: CGFloat = 4
    var cellCornerRadius: CGFloat = 4
    private var dragStart: (column: Int, row: Int)?
    private var dragCurrent: (column: Int, row: Int)?
    private var hovered: (column: Int, row: Int)?

    // 讓 y 從上往下，和 GridSelection 的 row 方向一致
    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(
            rect: bounds,
            options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways],
            owner: self
        ))
    }

    // MARK: - 繪製

    override func draw(_ dirtyRect: NSRect) {
        let area = bounds.insetBy(dx: padding, dy: padding)
        let cellWidth = area.width / CGFloat(grid.columns)
        let cellHeight = area.height / CGFloat(grid.rows)
        let selection = currentSelection

        for row in 0..<grid.rows {
            for column in 0..<grid.columns {
                let rect = CGRect(
                    x: area.minX + CGFloat(column) * cellWidth,
                    y: area.minY + CGFloat(row) * cellHeight,
                    width: cellWidth,
                    height: cellHeight
                ).insetBy(dx: gap / 2, dy: gap / 2)

                let isSelected = selection.map { contains($0, column: column, row: row) } ?? false
                let color = isSelected
                    ? NSColor.controlAccentColor
                    : NSColor.labelColor.withAlphaComponent(0.12)
                color.setFill()
                NSBezierPath(roundedRect: rect, xRadius: cellCornerRadius, yRadius: cellCornerRadius).fill()
            }
        }
    }

    // MARK: - 滑鼠與鍵盤

    override func mouseMoved(with event: NSEvent) {
        hovered = cell(at: event)
        needsDisplay = true
    }

    override func mouseExited(with event: NSEvent) {
        hovered = nil
        needsDisplay = true
    }

    override func mouseDown(with event: NSEvent) {
        dragStart = cell(at: event)
        dragCurrent = dragStart
        needsDisplay = true
    }

    override func mouseDragged(with event: NSEvent) {
        dragCurrent = cell(at: event)
        needsDisplay = true
    }

    override func mouseUp(with event: NSEvent) {
        let selection = currentSelection
        dragStart = nil
        dragCurrent = nil
        needsDisplay = true
        if let selection { onSelect?(selection) }
    }

    override func keyDown(with event: NSEvent) {
        if event.keyCode == 53 { // Esc
            onCancel?()
        } else {
            super.keyDown(with: event)
        }
    }

    // MARK: - 工具

    /// 優先順序：拖曳中的範圍 → 固定的選取範圍 → 滑鼠所在的那一格。
    private var currentSelection: GridSelection? {
        if let start = dragStart, let end = dragCurrent {
            return GridSelection(
                column: min(start.column, end.column),
                row: min(start.row, end.row),
                columns: abs(start.column - end.column) + 1,
                rows: abs(start.row - end.row) + 1
            )
        }
        if let selection {
            return selection
        }
        if let hovered {
            return GridSelection(column: hovered.column, row: hovered.row, columns: 1, rows: 1)
        }
        return nil
    }

    /// 滑鼠位置對應到哪一格（超出範圍時夾在邊界內，拖到外面也能正常選取）。
    private func cell(at event: NSEvent) -> (column: Int, row: Int) {
        let point = convert(event.locationInWindow, from: nil)
        let area = bounds.insetBy(dx: padding, dy: padding)
        let column = Int((point.x - area.minX) / (area.width / CGFloat(grid.columns)))
        let row = Int((point.y - area.minY) / (area.height / CGFloat(grid.rows)))
        return (
            column: min(max(column, 0), grid.columns - 1),
            row: min(max(row, 0), grid.rows - 1)
        )
    }

    private func contains(_ selection: GridSelection, column: Int, row: Int) -> Bool {
        column >= selection.column && column < selection.column + selection.columns
            && row >= selection.row && row < selection.row + selection.rows
    }
}
