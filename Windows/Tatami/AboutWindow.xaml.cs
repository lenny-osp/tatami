using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Tatami;

/// <summary>「關於 Tatami」：版本、簡介、專案和贊助連結。</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        AppIconImage.Source = LoadAppIcon(256);
        VersionText.Text = Version;
    }

    /// <summary>顯示用的版本號，例如 1.2.3（發佈時由 build-windows.ps1 依 tag 設定）。</summary>
    public static string Version =>
        typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)
        ?? "";

    /// <summary>從 App 圖示（AppIcon.ico）取出最接近指定大小的圖。</summary>
    public static BitmapFrame LoadAppIcon(int size)
    {
        var decoder = BitmapDecoder.Create(
            new Uri("pack://application:,,,/Tatami;component/AppIcon.ico"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return decoder.Frames
            .OrderBy(frame => Math.Abs(frame.PixelWidth - size))
            .First();
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Tatami: cannot open {e.Uri}: {exception.Message}");
        }
        e.Handled = true;
    }
}
