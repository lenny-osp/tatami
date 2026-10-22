using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Tatami.Win32;
using static Tatami.Win32.NativeMethods;

namespace Tatami;

/// <summary>
/// 通知區域圖示：四疊半榻榻米的風車排列（App 圖示由 scripts/make-icon-windows.ps1 產生，排列方式相同），
/// 四塊整疊繞著中間的半疊。在程式裡畫，工作列是淺色時畫黑色，深色時畫白色。
/// </summary>
public static class TatamiIcon
{
    /// <summary>以 3×3 單位表示每一塊榻榻米（原點在左上），和 macOS 版 TatamiIcon.swift 相同</summary>
    private static readonly Rect[] Mats =
    [
        new(0, 0, 2, 1), // 上方，橫放
        new(2, 0, 1, 2), // 右方，直放
        new(1, 2, 2, 1), // 下方，橫放
        new(0, 1, 1, 2), // 左方，直放
        new(1, 1, 1, 1), // 中間的半疊
    ];

    /// <summary>工作列（系統）是否使用淺色主題。讀不到時當成深色（Windows 11 預設）。</summary>
    public static bool TaskbarUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>通知區域圖示的大小（實體像素），依主螢幕的縮放比例：100% 是 16，150% 是 24。</summary>
    public static int TraySize()
    {
        int size = GetSystemMetricsForDpi(SM_CXSMICON, Monitors.Primary().Dpi);
        return size > 0 ? size : 16;
    }

    /// <summary>產生通知區域用的 HICON。呼叫端用完要 DestroyIcon。</summary>
    public static IntPtr CreateTrayIcon()
    {
        int size = TraySize();
        var color = TaskbarUsesLightTheme() ? Colors.Black : Colors.White;
        var png = EncodePng(Render(size, color));
        // CreateIconFromResourceEx 可以直接讀 PNG（Windows Vista 以後）
        return CreateIconFromResourceEx(png, (uint)png.Length, true, 0x00030000, size, size, 0);
    }

    /// <summary>畫出 size × size 像素的圖示（實心塊狀，塊與塊之間留空隙）。</summary>
    public static BitmapSource Render(int size, Color color)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // 以 18 為基準等比例放大，大圖示的比例才會和小圖示一樣
            double scale = size / 18.0;
            double inset = 1.5 * scale;
            double unit = (size - inset * 2) / 3;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            foreach (var mat in Mats)
            {
                var rect = new Rect(inset + mat.X * unit, inset + mat.Y * unit, mat.Width * unit, mat.Height * unit);
                rect.Inflate(-0.7 * scale, -0.7 * scale);
                context.DrawRoundedRectangle(brush, null, rect, 0.8 * scale, 0.8 * scale);
            }
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
