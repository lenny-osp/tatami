using System.Windows;
using System.Windows.Interop;
using Tatami.Core;
using Tatami.Win32;
using static Tatami.Win32.NativeMethods;

namespace Tatami;

/// <summary>一個螢幕上的格線面板：無邊框、在最上層、不出現在工作列。</summary>
public partial class GridPanelWindow : Window
{
    /// <summary>面板寬度（DIP），實際像素依該螢幕的縮放比例計算</summary>
    private const double PanelWidth = 360;

    public GridPanelWindow(MonitorInfo monitor, Grid grid)
    {
        InitializeComponent();
        Monitor = monitor;
        GridView.Grid = grid;
        SourceInitialized += (_, _) =>
        {
            // 不出現在 Alt+Tab
            var hwnd = new WindowInteropHelper(this).Handle;
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_TOOLWINDOW));
        };
    }

    public MonitorInfo Monitor { get; }

    /// <summary>
    /// 顯示在螢幕工作區的正中央。面板的長寬比和工作區一樣，格子看起來才像縮小版的螢幕。
    /// 位置用實體像素設定，各螢幕的縮放比例不同也不會跑掉。
    /// </summary>
    public void ShowCentered()
    {
        var work = Monitor.WorkArea;
        int width = (int)Math.Round(PanelWidth * Monitor.Scale);
        int height = work.Width > 0 ? (int)Math.Round((double)width * work.Height / work.Width) : width;
        int left = work.Left + (work.Width - width) / 2;
        int top = work.Top + (work.Height - height) / 2;

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        Show();
        // 設兩次：移到縮放比例不同的螢幕時，WPF 會先依新的 DPI 調整大小，第二次修正回正確的像素大小
        for (int i = 0; i < 2; i++)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, left, top, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
}
