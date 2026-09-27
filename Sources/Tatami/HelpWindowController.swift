import AppKit
import SwiftUI

/// 「說明」視窗：簡單介紹各項功能的操作方式，跟隨設定裡選的語言。
@MainActor
final class HelpWindowController {
    private let settings: AppSettings
    private var window: NSWindow?

    init(settings: AppSettings) {
        self.settings = settings
        settings.observeLanguage { [weak self] in self?.window?.title = L("help.title") }
    }

    func show() {
        if window == nil {
            let controller = NSHostingController(rootView: HelpView(settings: settings).localized(settings))
            let window = NSWindow(contentViewController: controller)
            window.title = L("help.title")
            window.styleMask = [.titled, .closable]
            window.isReleasedWhenClosed = false
            window.center()
            self.window = window
        }
        // 和設定視窗一樣：等選單關閉後再帶到前景
        DispatchQueue.main.async { [weak self] in
            guard let window = self?.window else { return }
            NSApp.activate()
            window.makeKeyAndOrderFront(nil)
            window.orderFrontRegardless()
        }
    }
}

struct HelpView: View {
    @ObservedObject var settings: AppSettings

    /// 每一段說明：圖示、標題 key、內文 key
    private let topics: [(symbol: String, title: String, body: String)] = [
        ("square.grid.3x3", "help.grid.title", "help.grid.body"),
        ("rectangle.split.2x1", "help.snap.title", "help.snap.body"),
        ("keyboard", "help.shortcuts.title", "help.shortcuts.body"),
        ("display.2", "help.screens.title", "help.screens.body"),
        ("lock.shield", "help.permission.title", "help.permission.body"),
        ("arrow.down.circle", "help.updates.title", "help.updates.body"),
    ]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                Text(L("help.intro"))
                    .font(.title3)

                ForEach(topics, id: \.title) { topic in
                    HStack(alignment: .top, spacing: 12) {
                        Image(systemName: topic.symbol)
                            .font(.title2)
                            .foregroundStyle(.tint)
                            .frame(width: 28)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(L(topic.title))
                                .font(.headline)
                            Text(L(topic.body))
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                    }
                }
            }
            .padding(24)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(width: 480, height: 520)
    }
}
