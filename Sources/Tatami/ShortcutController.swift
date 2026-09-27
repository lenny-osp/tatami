import AppKit

/// 依照設定註冊全域快捷鍵，按下時搬移目前的視窗。
@MainActor
final class ShortcutController {
    private let settings: AppSettings
    private let gridController: GridController
    private let hotKeys = HotKeyCenter()

    init(settings: AppSettings, gridController: GridController) {
        self.settings = settings
        self.gridController = gridController
        settings.onShortcutsChanged = { [weak self] in self?.reload() }
        reload()
    }

    private func reload() {
        hotKeys.unregisterAll()
        // 錄製快捷鍵時先全部停用，否則按下已存在的組合會直接觸發，錄不到
        guard !settings.isRecordingShortcut else { return }

        var fixedConflicts = Set<FixedShortcut>()
        if let combo = settings.showGridKeyCombo,
           !hotKeys.register(combo, action: { [weak self] in self?.gridController.show() }) {
            fixedConflicts.insert(.showGrid)
        }
        if let combo = settings.nextScreenKeyCombo,
           !hotKeys.register(combo, action: { [weak self] in self?.moveToNextScreen() }) {
            fixedConflicts.insert(.nextScreen)
        }
        if settings.conflictingFixedShortcuts != fixedConflicts {
            settings.conflictingFixedShortcuts = fixedConflicts
        }

        var conflicts = Set<UUID>()
        for shortcut in settings.shortcuts {
            guard let combo = shortcut.keyCombo else { continue }
            let registered = hotKeys.register(combo) { [weak self] in self?.apply(shortcut) }
            if !registered { conflicts.insert(shortcut.id) }
        }
        if settings.conflictingShortcutIDs != conflicts {
            settings.conflictingShortcutIDs = conflicts
        }
    }

    private func apply(_ shortcut: WindowShortcut) {
        guard let window = WindowMover.frontmostWindow(),
              let frame = WindowMover.frame(of: window),
              let screen = WindowMover.screen(containing: frame)
        else {
            NSSound.beep()
            return
        }
        let target = shortcut.grid.frame(for: shortcut.selection, in: screen.visibleFrame)
        WindowMover.setFrame(target, of: window)
    }

    private func moveToNextScreen() {
        guard let window = WindowMover.frontmostWindow(),
              WindowMover.moveToNextScreen(window)
        else {
            NSSound.beep()
            return
        }
    }
}
