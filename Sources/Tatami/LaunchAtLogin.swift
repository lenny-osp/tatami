import AppKit
import ServiceManagement

/// 「登入時啟動」開關，使用 macOS 13 以後的 SMAppService。
@MainActor
enum LaunchAtLogin {
    static var isEnabled: Bool {
        SMAppService.mainApp.status == .enabled
    }

    static func setEnabled(_ enabled: Bool) {
        let service = SMAppService.mainApp
        guard enabled != isEnabled else { return }
        do {
            if enabled {
                try service.register()
            } else {
                try service.unregister()
            }
        } catch {
            let alert = NSAlert(error: error)
            alert.messageText = L("launchAtLogin.error")
            alert.runModal()
            return
        }

        // 使用者曾在系統設定裡關掉過，需要他手動重新允許
        if service.status == .requiresApproval {
            SMAppService.openSystemSettingsLoginItems()
        }
    }
}
