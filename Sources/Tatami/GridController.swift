import AppKit

/// 無邊框、不會搶走其他 App 焦點的浮動面板。每個螢幕各一個。
private final class GridPanel: NSPanel {
    var targetScreen: NSScreen?
    override var canBecomeKey: Bool { true }
}

/// 負責在每個螢幕顯示格線面板，並在使用者選好範圍後搬移視窗。
/// 在哪個螢幕的面板上選取，視窗就會搬到那個螢幕。
@MainActor
final class GridController: NSObject, NSWindowDelegate {
    private let settings: AppSettings
    private var grid = Grid()
    private var panels: [GridPanel] = []
    private var targetWindow: AXUIElement?

    private let panelWidth: CGFloat = 360

    init(settings: AppSettings) {
        self.settings = settings
    }

    func show() {
        close()
        // 每次打開都讀取最新設定，改了欄數列數馬上生效
        grid = settings.grid

        // 先記住要搬的視窗，再顯示面板
        guard let window = WindowMover.frontmostWindow(),
              let frame = WindowMover.frame(of: window),
              let currentScreen = WindowMover.screen(containing: frame)
        else {
            NSSound.beep()
            return
        }
        targetWindow = window

        panels = NSScreen.screens.map(makePanel)
        panels.forEach { $0.orderFront(nil) }

        // 視窗目前所在螢幕的面板取得焦點，讓 Esc 可以直接關閉
        let keyPanel = panels.first { $0.targetScreen == currentScreen } ?? panels.first
        keyPanel?.makeKeyAndOrderFront(nil)
        if let gridView = keyPanel?.contentView?.subviews.first {
            keyPanel?.makeFirstResponder(gridView)
        }
    }

    func close() {
        let closing = panels
        panels = []
        targetWindow = nil
        closing.forEach { $0.orderOut(nil) }
    }

    // 點到面板外面時自動關閉；在面板之間切換則不關閉
    func windowDidResignKey(_ notification: Notification) {
        DispatchQueue.main.async { [weak self] in
            guard let self, !self.panels.contains(where: \.isKeyWindow) else { return }
            self.close()
        }
    }

    private func makePanel(for screen: NSScreen) -> GridPanel {
        // 面板的長寬比和螢幕一樣，格子看起來才像縮小版的螢幕
        let visible = screen.visibleFrame
        let size = CGSize(width: panelWidth, height: panelWidth * visible.height / visible.width)
        let origin = CGPoint(x: visible.midX - size.width / 2, y: visible.midY - size.height / 2)

        let panel = GridPanel(
            contentRect: CGRect(origin: origin, size: size),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.targetScreen = screen
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.isReleasedWhenClosed = false
        panel.delegate = self

        let background = NSVisualEffectView(frame: CGRect(origin: .zero, size: size))
        background.material = .hudWindow
        background.state = .active
        background.wantsLayer = true
        background.layer?.cornerRadius = 12
        background.layer?.masksToBounds = true

        let gridView = GridView(frame: background.bounds)
        gridView.grid = grid
        gridView.autoresizingMask = [.width, .height]
        gridView.onSelect = { [weak self] selection in self?.apply(selection, on: screen) }
        gridView.onCancel = { [weak self] in self?.close() }
        background.addSubview(gridView)

        panel.contentView = background
        return panel
    }

    private func apply(_ selection: GridSelection, on screen: NSScreen) {
        guard let window = targetWindow else { return }
        let target = grid.frame(for: selection, in: screen.visibleFrame)
        close()
        WindowMover.setFrame(target, of: window)
    }
}
