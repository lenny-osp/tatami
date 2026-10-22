using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Tatami.Core;
using static Tatami.Win32.NativeMethods;

namespace Tatami;

/// <summary>拖曳時顯示的預覽方框。不搶焦點、滑鼠可以穿透，位置用實體像素設定。</summary>
public partial class SnapPreviewWindow : Window
{
    public SnapPreviewWindow()
    {
        InitializeComponent();
        var accent = SystemColors.AccentColor;
        Box.Background = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
        Box.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, accent.R, accent.G, accent.B));
        SourceInitialized += (_, _) =>
        {
            // 滑鼠穿透、不會被啟用、不出現在 Alt+Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        };
    }

    /// <summary>在 frame（實體像素）內縮 4 DIP 的位置顯示。</summary>
    public void ShowAt(PixelRect frame, double scale)
    {
        int inset = (int)Math.Round(4 * scale);
        var rect = PixelRect.FromEdges(frame.Left + inset, frame.Top + inset, frame.Right - inset, frame.Bottom - inset);
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        if (!IsVisible) Show();
        // 設兩次：移到縮放比例不同的螢幕時，WPF 會先依新的 DPI 調整大小，第二次修正回正確的像素大小
        for (int i = 0; i < 2; i++)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, rect.Left, rect.Top, rect.Width, rect.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
}
