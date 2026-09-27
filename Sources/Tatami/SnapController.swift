import AppKit

/// 拖曳視窗到螢幕邊緣時要排列成的樣子。
private enum SnapZone {
    case maximize, leftHalf, rightHalf

    var selection: GridSelection {
        switch self {
        case .maximize: GridSelection(column: 0, row: 0, columns: 6, rows: 6)
        case .leftHalf: GridSelection(column: 0, row: 0, columns: 3, rows: 6)
        case .rightHalf: GridSelection(column: 3, row: 0, columns: 3, rows: 6)
        }
    }
}

/// 監聽全域滑鼠事件：使用者拖曳視窗到螢幕邊緣並放開時，自動排列視窗。
///
/// 流程：
/// 1. 按下滑鼠：記住游標底下的視窗和它的外框
/// 2. 拖曳中：外框位置變了但大小沒變，才算「正在移動視窗」（排除選取文字、調整大小）
///    接著看游標在哪個螢幕的哪個邊緣，顯示預覽
/// 3. 放開滑鼠：如果在邊緣上，把視窗排列到游標所在的螢幕
@MainActor
final class SnapController {
    private let grid = Grid()
    /// 游標離螢幕邊緣多近才觸發（點）
    private let edgeMargin: CGFloat = 5
    private let preview = SnapPreviewWindow()

    private var monitor: Any?
    private var draggedWindow: AXUIElement?
    private var initialFrame: CGRect?
    private var isMovingWindow = false
    private var pendingFrame: CGRect?

    var isRunning: Bool { monitor != nil }

    func start() {
        guard monitor == nil else { return }
        monitor = NSEvent.addGlobalMonitorForEvents(
            matching: [.leftMouseDown, .leftMouseDragged, .leftMouseUp]
        ) { [weak self] event in
            MainActor.assumeIsolated { self?.handle(event) }
        }
    }

    func stop() {
        if let monitor { NSEvent.removeMonitor(monitor) }
        monitor = nil
        reset()
    }

    private func handle(_ event: NSEvent) {
        switch event.type {
        case .leftMouseDown:
            reset()
            guard let window = WindowMover.window(at: NSEvent.mouseLocation) else { return }
            draggedWindow = window
            initialFrame = WindowMover.frame(of: window)

        case .leftMouseDragged:
            guard let window = draggedWindow, let initialFrame else { return }
            if !isMovingWindow {
                guard let frame = WindowMover.frame(of: window),
                      frame.size == initialFrame.size,
                      frame.origin != initialFrame.origin
                else { return }
                isMovingWindow = true
            }
            updatePreview(at: NSEvent.mouseLocation)

        case .leftMouseUp:
            if isMovingWindow, let window = draggedWindow, let target = pendingFrame {
                // 稍等一下，讓系統先處理完視窗拖曳結束，才不會被蓋掉
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) {
                    WindowMover.setFrame(target, of: window)
                }
            }
            reset()

        default:
            break
        }
    }

    private func updatePreview(at point: CGPoint) {
        guard let screen = NSScreen.screens.first(where: { NSMouseInRect(point, $0.frame, false) }),
              let zone = zone(at: point, in: screen.frame)
        else {
            pendingFrame = nil
            preview.hide()
            return
        }

        // 排列到「游標所在」的螢幕，而不是視窗原本的螢幕
        let target = grid.frame(for: zone.selection, in: screen.visibleFrame)
        if target != pendingFrame {
            pendingFrame = target
            preview.show(target)
        }
    }

    /// 左右邊緣優先於頂端，這樣在左上、右上角也能排成半邊。
    private func zone(at point: CGPoint, in frame: CGRect) -> SnapZone? {
        if point.x <= frame.minX + edgeMargin { return .leftHalf }
        if point.x >= frame.maxX - edgeMargin { return .rightHalf }
        if point.y >= frame.maxY - edgeMargin { return .maximize }
        return nil
    }

    private func reset() {
        draggedWindow = nil
        initialFrame = nil
        isMovingWindow = false
        pendingFrame = nil
        preview.hide()
    }
}

/// 拖曳時顯示「放開後視窗會在哪裡」的半透明方框。
private final class SnapPreviewWindow: NSWindow {
    init() {
        super.init(contentRect: .zero, styleMask: .borderless, backing: .buffered, defer: true)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        ignoresMouseEvents = true
        isReleasedWhenClosed = false
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .transient]

        let box = NSView()
        box.wantsLayer = true
        box.layer?.backgroundColor = NSColor.controlAccentColor.withAlphaComponent(0.2).cgColor
        box.layer?.borderColor = NSColor.controlAccentColor.withAlphaComponent(0.8).cgColor
        box.layer?.borderWidth = 2
        box.layer?.cornerRadius = 10
        contentView = box
    }

    func show(_ frame: CGRect) {
        setFrame(frame.insetBy(dx: 4, dy: 4), display: true)
        orderFrontRegardless()
    }

    func hide() {
        orderOut(nil)
    }
}
