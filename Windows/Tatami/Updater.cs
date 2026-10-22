using System.Windows;
using System.Windows.Media.Imaging;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;

namespace Tatami;

/// <summary>
/// 用 NetSparkle 自動更新：從 GitHub Releases 上的 appcast-windows.xml 查詢新版本，
/// 驗證 Ed25519 簽章後下載安裝程式，關閉 Tatami、安裝並重新啟動。
///
/// NetSparkle 的對話框使用它自己的英文文字，不受 Tatami 設定裡的語言影響（和 macOS 的 Sparkle 一樣）。
/// 查詢失敗（沒有網路、還沒有發佈 feed）只會顯示錯誤訊息或什麼都不做，不會讓 App 當掉。
/// </summary>
public sealed class Updater : IDisposable
{
    public const string AppcastUrl = "https://github.com/lenny-osp/tatami/releases/latest/download/appcast-windows.xml";

    /// <summary>
    /// ⚠️ 預留位置，發佈前必須替換！
    /// 換成 `netsparkle-generate-appcast --generate-keys` 再 `--export` 輸出的 Ed25519 公鑰（Base64），
    /// 也就是 NETSPARKLE_PUBLIC_KEY secret 的內容。私鑰是 NETSPARKLE_PRIVATE_KEY，絕對不要 commit。
    /// 這個值不是有效的公鑰時（例如現在），Strict 模式會拒絕所有更新，不會安裝未簽章的檔案。
    /// </summary>
    public const string PublicKey = "REPLACE_WITH_NETSPARKLE_PUBLIC_KEY";

    /// <summary>傳給 Inno Setup 安裝程式的參數：不顯示精靈，只顯示進度。</summary>
    private const string InstallerArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-";

    private readonly SparkleUpdater? sparkle;

    public Updater()
    {
        try
        {
            var icon = LoadIcon();
            sparkle = new SparkleUpdater(AppcastUrl, new Ed25519Checker(SecurityMode.Strict, PublicKey))
            {
                UIFactory = icon is null ? new NetSparkleUpdater.UI.WPF.UIFactory() : new NetSparkleUpdater.UI.WPF.UIFactory(icon),
                RelaunchAfterUpdate = true,
                CustomInstallerArguments = InstallerArguments,
            };
            // 安裝前關閉 Tatami：用 WPF 的正常結束流程，通知區域圖示才會被移除
            sparkle.CloseApplication += () => Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tatami: updater unavailable: {exception.Message}");
            sparkle = null;
        }
    }

    /// <summary>從選單手動檢查：已是最新或檢查失敗也會告訴使用者。</summary>
    public async void CheckForUpdates()
    {
        if (sparkle is null) return;
        try
        {
            await sparkle.CheckForUpdatesAtUserRequest();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tatami: update check failed: {exception.Message}");
        }
    }

    /// <summary>啟動時的自動檢查：只有找到新版本才跳出提示。不用 NetSparkle 的定期檢查，和 macOS 版相同。</summary>
    public async void CheckInBackground()
    {
        if (sparkle is null) return;
        try
        {
            await sparkle.CheckForUpdatesQuietly();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Tatami: background update check failed: {exception.Message}");
        }
    }

    public void Dispose() => sparkle?.Dispose();

    private static BitmapFrame? LoadIcon()
    {
        try
        {
            return AboutWindow.LoadAppIcon(64);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return null;
        }
    }
}
