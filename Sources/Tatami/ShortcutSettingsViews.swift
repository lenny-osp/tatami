import AppKit
import SwiftUI

/// 設定畫面中的一個快捷鍵：左邊挑區塊，右邊填名稱、錄快捷鍵。
struct ShortcutRow: View {
    @Binding var shortcut: WindowShortcut
    @ObservedObject var settings: AppSettings
    var hasConflict: Bool
    var onDelete: () -> Void

    private let pickerWidth: CGFloat = 120

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            GridPicker(grid: shortcut.grid, selection: $shortcut.selection)
                .frame(width: pickerWidth, height: pickerWidth * screenAspectRatio)

            VStack(alignment: .leading, spacing: 6) {
                TextField(L("shortcut.namePlaceholder"), text: $shortcut.name)
                    .labelsHidden()
                ShortcutRecorder(combo: $shortcut.keyCombo, settings: settings)
                if hasConflict {
                    Text(L("shortcut.conflict"))
                        .font(.caption)
                        .foregroundStyle(.orange)
                }
                Text(L("shortcut.gridSize", shortcut.grid.columns, shortcut.grid.rows))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Button(role: .destructive, action: onDelete) {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .help(L("shortcut.delete"))
        }
        .padding(.vertical, 4)
    }

    /// 小格線的長寬比和主螢幕一樣
    private var screenAspectRatio: CGFloat {
        guard let frame = NSScreen.main?.visibleFrame, frame.width > 0 else { return 0.625 }
        return frame.height / frame.width
    }
}

/// 內建功能的快捷鍵：名稱固定，只能設定按鍵。
struct FixedShortcutRow: View {
    var title: String
    @Binding var combo: KeyCombo?
    @ObservedObject var settings: AppSettings
    var hasConflict: Bool

    var body: some View {
        LabeledContent {
            ShortcutRecorder(combo: $combo, settings: settings)
        } label: {
            Text(title)
            if hasConflict {
                Text(L("shortcut.conflict"))
                    .foregroundStyle(.orange)
            }
        }
    }
}

/// 把格線面板用的 GridView 縮小，放進設定畫面當區塊挑選器。
struct GridPicker: NSViewRepresentable {
    var grid: Grid
    @Binding var selection: GridSelection

    func makeNSView(context: Context) -> GridView {
        let view = GridView()
        view.padding = 0
        view.gap = 2
        view.cellCornerRadius = 2
        return view
    }

    func updateNSView(_ view: GridView, context: Context) {
        view.grid = grid
        view.selection = selection
        view.onSelect = { selection = $0 }
    }
}

/// 點一下開始錄製，接著按下想要的組合。Esc 取消，⌫ 清除。
struct ShortcutRecorder: View {
    @Binding var combo: KeyCombo?
    @ObservedObject var settings: AppSettings
    @StateObject private var recorder = ShortcutRecorderModel()

    var body: some View {
        HStack(spacing: 4) {
            Button {
                if recorder.isRecording {
                    recorder.stop()
                } else {
                    recorder.start(settings: settings) { combo = $0 }
                }
            } label: {
                Text(recorder.isRecording ? L("recorder.prompt") : (combo?.display ?? L("recorder.record")))
                    .frame(minWidth: 110)
            }

            if combo != nil, !recorder.isRecording {
                Button {
                    combo = nil
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.borderless)
                .help(L("recorder.clear"))
            }
        }
        .onDisappear { recorder.stop() }
    }
}

@MainActor
final class ShortcutRecorderModel: ObservableObject {
    @Published private(set) var isRecording = false
    private var monitor: Any?
    private weak var settings: AppSettings?
    private var onRecord: ((KeyCombo?) -> Void)?

    func start(settings: AppSettings, onRecord: @escaping (KeyCombo?) -> Void) {
        guard !isRecording else { return }
        isRecording = true
        self.settings = settings
        self.onRecord = onRecord
        settings.isRecordingShortcut = true

        // 設定視窗在前景，用 local monitor 攔截按鍵；回傳 nil 表示不再往下傳
        monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            MainActor.assumeIsolated { self?.handle(event) }
            return nil
        }
    }

    func stop() {
        guard isRecording else { return }
        if let monitor { NSEvent.removeMonitor(monitor) }
        monitor = nil
        isRecording = false
        onRecord = nil
        settings?.isRecordingShortcut = false
    }

    private func handle(_ event: NSEvent) {
        let hasModifiers = !event.modifierFlags.isDisjoint(with: [.control, .option, .shift, .command])
        switch event.keyCode {
        case 53 where !hasModifiers: // Esc：取消
            stop()
        case 51 where !hasModifiers, 117 where !hasModifiers: // ⌫ ⌦：清除
            onRecord?(nil)
            stop()
        default:
            if let combo = KeyCombo(event: event) {
                onRecord?(combo)
                stop()
            } else {
                NSSound.beep() // 需要搭配 ⌃⌥⌘
            }
        }
    }
}
