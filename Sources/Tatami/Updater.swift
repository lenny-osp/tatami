import AppKit
import Sparkle

/// 用 Sparkle 自動更新：從 GitHub Releases 上的 appcast.xml 查詢新版本，
/// 驗證 EdDSA 簽章後下載、安裝並重新啟動。
///
/// Sparkle 的對話框使用 macOS 系統語言，不受 Tatami 設定裡的語言影響。
@MainActor
final class Updater {
    private let controller = SPUStandardUpdaterController(
        startingUpdater: true,
        updaterDelegate: nil,
        userDriverDelegate: nil
    )

    init() {
        // 不用 Sparkle 內建的「每天檢查一次」，改成每次啟動時依設定檢查
        controller.updater.automaticallyChecksForUpdates = false
    }

    /// 從選單手動檢查：已是最新或檢查失敗也會告訴使用者
    func checkForUpdates() {
        NSApp.activate()
        controller.checkForUpdates(nil)
    }

    /// 啟動時的自動檢查：只有找到新版本才跳出提示
    func checkInBackground() {
        controller.updater.checkForUpdatesInBackground()
    }
}
