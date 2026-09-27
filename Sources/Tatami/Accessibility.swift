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

    /// 清除這個 App 在「輔助使用」清單裡的紀錄。
    ///
    /// macOS 用簽章辨識 App；簽章不同的舊版本（例如臨時簽章的版本）留下的紀錄，
    /// 就算開關是打開的也對新版本無效，使用者原本得手動「移除 → 新增 → 打開」。
    /// 先清掉紀錄，之後 requestIfNeeded() 會重新加入，使用者只要打開開關一次。
    /// tccutil 只能重設自己指定的 bundle ID，不需要管理員權限。
    static func resetStaleEntry() {
        guard let bundleID = Bundle.main.bundleIdentifier else { return }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/tccutil")
        process.arguments = ["reset", "Accessibility", bundleID]
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        do {
            try process.run()
            process.waitUntilExit()
        } catch {
            // 失敗也沒關係，使用者還是可以到系統設定手動處理
        }
    }

    /// 打開「系統設定 → 隱私權與安全性 → 輔助使用」。
    static func openSettings() {
        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
        NSWorkspace.shared.open(url)
    }
}
