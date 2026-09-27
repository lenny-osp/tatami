import AppKit
import ApplicationServices

/// 搬移其他 App 的視窗需要「輔助使用」權限。
enum Accessibility {
    static var isTrusted: Bool {
        AXIsProcessTrusted()
    }

    /// 沒有權限時，跳出系統對話框請使用者開啟。
    static func requestIfNeeded() {
        // 等同 kAXTrustedCheckOptionPrompt，直接寫字串以避開 Swift 6 的全域變數並行檢查
        let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }

    /// 打開「系統設定 → 隱私權與安全性 → 輔助使用」。
    static func openSettings() {
        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
        NSWorkspace.shared.open(url)
    }
}
