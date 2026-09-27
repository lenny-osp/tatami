import AppKit
import ApplicationServices

/// 用 Accessibility API 讀取與設定其他 App 視窗的位置和大小。
///
/// 座標注意：AX API 的原點在「主螢幕左上角」、y 往下；
/// NSScreen 的原點在「主螢幕左下角」、y 往上。這裡對外一律使用 Cocoa 座標。
@MainActor
enum WindowMover {
    /// 目前最前面 App 的焦點視窗。
    static func frontmostWindow() -> AXUIElement? {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.processIdentifier != ProcessInfo.processInfo.processIdentifier
        else { return nil }

        let axApp = AXUIElementCreateApplication(app.processIdentifier)
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(axApp, kAXFocusedWindowAttribute as CFString, &value) == .success,
              let value
        else { return nil }
        return (value as! AXUIElement)
    }

    /// 指定位置（Cocoa 座標）底下的視窗，自己的視窗不算。
    static func window(at point: CGPoint) -> AXUIElement? {
        let systemWide = AXUIElementCreateSystemWide()
        // 對方 App 沒回應時不要卡住太久
        AXUIElementSetMessagingTimeout(systemWide, 0.1)

        let axPoint = flip(CGRect(origin: point, size: .zero)).origin
        var element: AXUIElement?
        guard AXUIElementCopyElementAtPosition(systemWide, Float(axPoint.x), Float(axPoint.y), &element) == .success,
              let element
        else { return nil }

        var pid: pid_t = 0
        AXUIElementGetPid(element, &pid)
        guard pid != ProcessInfo.processInfo.processIdentifier else { return nil }

        // 點到的可能是視窗本身，也可能是標題列、按鈕等子元件
        var role: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXRoleAttribute as CFString, &role)
        if role as? String == kAXWindowRole { return element }

        var window: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, kAXWindowAttribute as CFString, &window) == .success,
              let window
        else { return nil }
        return (window as! AXUIElement)
    }

    /// 視窗目前的外框（Cocoa 座標）。
    static func frame(of window: AXUIElement) -> CGRect? {
        guard let origin: CGPoint = attribute(kAXPositionAttribute, of: window, type: .cgPoint),
              let size: CGSize = attribute(kAXSizeAttribute, of: window, type: .cgSize)
        else { return nil }
        return flip(CGRect(origin: origin, size: size))
    }

    /// 設定視窗外框（Cocoa 座標）。
    static func setFrame(_ frame: CGRect, of window: AXUIElement) {
        let axFrame = flip(frame)
        // 先設大小、再移位置、再設一次大小：
        // 跨螢幕移動時，系統可能因為舊位置而限制大小，第二次設定可以修正。
        setAttribute(kAXSizeAttribute, of: window, value: axFrame.size, type: .cgSize)
        setAttribute(kAXPositionAttribute, of: window, value: axFrame.origin, type: .cgPoint)
        setAttribute(kAXSizeAttribute, of: window, value: axFrame.size, type: .cgSize)
    }

    /// 視窗所在的螢幕（重疊面積最大的那個）。
    static func screen(containing frame: CGRect) -> NSScreen? {
        NSScreen.screens.max { a, b in
            area(a.frame.intersection(frame)) < area(b.frame.intersection(frame))
        } ?? NSScreen.main
    }

    /// 把視窗搬到下一個螢幕（由左到右輪流），保持它在螢幕上的相對位置和比例：
    /// 例如在左半邊的視窗，搬過去後還是在左半邊。只有一個螢幕時回傳 false。
    static func moveToNextScreen(_ window: AXUIElement) -> Bool {
        guard let frame = frame(of: window),
              let current = screen(containing: frame)
        else { return false }

        let screens = NSScreen.screens.sorted {
            ($0.frame.minX, $0.frame.minY) < ($1.frame.minX, $1.frame.minY)
        }
        guard screens.count > 1, let index = screens.firstIndex(of: current) else { return false }
        let next = screens[(index + 1) % screens.count]

        let from = current.visibleFrame
        let to = next.visibleFrame
        let scaleX = to.width / from.width
        let scaleY = to.height / from.height
        var target = CGRect(
            x: to.minX + (frame.minX - from.minX) * scaleX,
            y: to.minY + (frame.minY - from.minY) * scaleY,
            width: min(frame.width * scaleX, to.width),
            height: min(frame.height * scaleY, to.height)
        )
        // 確保整個視窗都在新螢幕的可用範圍內
        target.origin.x = min(max(target.minX, to.minX), to.maxX - target.width)
        target.origin.y = min(max(target.minY, to.minY), to.maxY - target.height)

        setFrame(target.integral, of: window)
        return true
    }

    // MARK: - 私有工具

    /// Cocoa 座標 ⇄ AX 座標（公式對稱，兩個方向都用它）。
    private static func flip(_ rect: CGRect) -> CGRect {
        let primaryHeight = NSScreen.screens.first?.frame.height ?? 0
        return CGRect(
            x: rect.minX,
            y: primaryHeight - rect.maxY,
            width: rect.width,
            height: rect.height
        )
    }

    private static func area(_ rect: CGRect) -> CGFloat {
        rect.isNull ? 0 : rect.width * rect.height
    }

    private static func attribute<T: BitwiseCopyable>(_ name: String, of element: AXUIElement, type: AXValueType) -> T? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success,
              let value, CFGetTypeID(value) == AXValueGetTypeID()
        else { return nil }

        let pointer = UnsafeMutablePointer<T>.allocate(capacity: 1)
        defer { pointer.deallocate() }
        guard AXValueGetValue(value as! AXValue, type, pointer) else { return nil }
        return pointer.pointee
    }

    private static func setAttribute<T: BitwiseCopyable>(_ name: String, of element: AXUIElement, value: T, type: AXValueType) {
        var value = value
        guard let axValue = AXValueCreate(type, &value) else { return }
        AXUIElementSetAttributeValue(element, name as CFString, axValue)
    }
}
