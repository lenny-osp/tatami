import AppKit

/// 選單列圖示：四疊半榻榻米的風車排列（App 圖示由 scripts/make-icon.swift 產生，排列方式相同），四塊整疊繞著中間的半疊。
enum TatamiIcon {
    enum Style {
        /// 實心塊狀，塊與塊之間留空隙
        case filled
        /// 黑邊框線，像榻榻米的布邊
        case outlined
    }

    /// 以 3×3 單位表示每一塊榻榻米（原點在左上）
    private static let mats: [CGRect] = [
        CGRect(x: 0, y: 0, width: 2, height: 1), // 上方，橫放
        CGRect(x: 2, y: 0, width: 1, height: 2), // 右方，直放
        CGRect(x: 1, y: 2, width: 2, height: 1), // 下方，橫放
        CGRect(x: 0, y: 1, width: 1, height: 2), // 左方，直放
        CGRect(x: 1, y: 1, width: 1, height: 1), // 中間的半疊
    ]

    static func menuBarImage(style: Style = .filled, size: CGFloat = 18) -> NSImage {
        let image = drawImage(style: style, size: size, color: .black)
        // Template：系統會依照淺色／深色選單列自動換顏色
        image.isTemplate = true
        image.accessibilityDescription = "Tatami"
        return image
    }

    private static func drawImage(style: Style, size: CGFloat, color: NSColor) -> NSImage {
        NSImage(size: NSSize(width: size, height: size), flipped: true) { _ in
            // 框線版要多留一點邊，外框線才不會被裁掉
            // 以 18pt 為基準等比例放大，大圖示的比例才會和選單列圖示一樣
            let scale = size / 18
            let inset: CGFloat = (style == .filled ? 1.5 : 2) * scale
            let unit = (size - inset * 2) / 3
            color.set()
            for mat in mats {
                let rect = CGRect(
                    x: inset + mat.minX * unit,
                    y: inset + mat.minY * unit,
                    width: mat.width * unit,
                    height: mat.height * unit
                )
                switch style {
                case .filled:
                    NSBezierPath(
                        roundedRect: rect.insetBy(dx: 0.7 * scale, dy: 0.7 * scale),
                        xRadius: 0.8 * scale,
                        yRadius: 0.8 * scale
                    ).fill()
                case .outlined:
                    // 相鄰的榻榻米共用同一條邊，線條會重疊成一條，不會變粗
                    let path = NSBezierPath(rect: rect)
                    path.lineWidth = 1.2 * scale
                    path.stroke()
                }
            }
            return true
        }
    }
}
