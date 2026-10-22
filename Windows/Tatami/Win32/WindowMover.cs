using System.Runtime.InteropServices;
using Tatami.Core;
using static Tatami.Win32.NativeMethods;

namespace Tatami.Win32;

/// <summary>
/// 讀取與設定其他 App 視窗的位置和大小。Windows 不需要特別的權限，
/// 但以系統管理員身分執行的視窗無法被一般權限的程式搬動（UIPI）。
///
/// 座標一律是實體像素（App 宣告了 PerMonitorV2，Win32 API 不會幫我們縮放）。
/// 對外使用「看得到的外框」：Windows 10/11 的 GetWindowRect 包含看不見的調整大小邊框，
/// 直接拿來排列，視窗之間會有縫。
/// </summary>
public static class WindowMover
{
    /// <summary>不能當成排列目標的視窗類別：桌面、工作列、通知區域溢位、開始功能表等。</summary>
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "ForegroundStaging", "MultitaskingViewFrame",
    };

    private static readonly uint CurrentProcessId = (uint)Environment.ProcessId;

    /// <summary>目前在最前面、可以排列的視窗；沒有時是 IntPtr.Zero。</summary>
    public static IntPtr ForegroundWindow()
    {
        var hwnd = GetForegroundWindow();
        return IsArrangeable(hwnd) ? hwnd : IntPtr.Zero;
    }

    /// <summary>
    /// 是否是可以排列的一般視窗：看得到、不是 Tatami 自己的、沒有被隱藏（cloaked，例如其他虛擬桌面的視窗）、
    /// 不是桌面或工作列、有標題列或可以調整大小（排除選單、提示框），也不是工具視窗。
    /// </summary>
    public static bool IsArrangeable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return false;
        if (GetAncestor(hwnd, GA_ROOT) != hwnd) return false;
        GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == CurrentProcessId) return false;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        if (ShellClasses.Contains(ClassName(hwnd))) return false;

        long style = GetWindowLongPtr(hwnd, GWL_STYLE);
        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;
        // 自己畫標題列的 App（Electron 等）沒有 WS_CAPTION，但一定可以調整大小
        return (style & WS_CAPTION) == WS_CAPTION || (style & WS_THICKFRAME) != 0;
    }

    /// <summary>視窗看得到的外框（不含隱形邊框）。</summary>
    public static PixelRect? Frame(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT bounds, Marshal.SizeOf<RECT>()) == 0)
        {
            return bounds.ToPixelRect();
        }
        return GetWindowRect(hwnd, out RECT rect) ? rect.ToPixelRect() : null;
    }

    /// <summary>
    /// 把視窗看得到的外框設成 frame。最大化或最小化的視窗要先還原，否則設定會被忽略。
    /// 失敗時（例如系統管理員的視窗）回傳 false。
    /// </summary>
    public static bool SetFrame(IntPtr hwnd, PixelRect frame)
    {
        if (!IsWindow(hwnd)) return false;
        if (IsZoomed(hwnd) || IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        // 設兩次：跨螢幕移動時，對方 App 可能因為縮放比例改變而自己調整大小，
        // 隱形邊框的寬度也會跟著變，第二次用新的邊框重新計算（和 macOS 版「大小、位置、大小」的作法相同）
        bool moved = Place(hwnd, frame);
        if (moved) Place(hwnd, frame);
        return moved;
    }

    /// <summary>
    /// 把視窗搬到下一個螢幕（由左到右輪流），保持它在螢幕上的相對位置和比例。
    /// 只有一個螢幕或搬移失敗時回傳 false。
    /// </summary>
    public static bool MoveToNextScreen(IntPtr hwnd)
    {
        if (Frame(hwnd) is not { } frame) return false;
        var screens = Monitors.All();
        if (screens.Count < 2) return false;
        var current = Monitors.FromRect(frame);
        int index = screens.ToList().FindIndex(m => m.Handle == current.Handle);
        if (index < 0) return false;
        var next = screens[(index + 1) % screens.Count];

        var target = WindowGeometry.RelativeFrame(frame, current.WorkArea, next.WorkArea);
        return SetFrame(hwnd, target);
    }

    /// <summary>把焦點還給目標視窗（格線面板關閉後）。</summary>
    public static void Activate(IntPtr hwnd)
    {
        if (IsWindow(hwnd)) SetForegroundWindow(hwnd);
    }

    /// <summary>沒有可以排列的視窗或搬移失敗時發出提示音，和 macOS 版相同。</summary>
    public static void Beep() => MessageBeep(MB_OK);

    private static bool Place(IntPtr hwnd, PixelRect frame)
    {
        if (!GetWindowRect(hwnd, out RECT outer)) return false;
        var visible = Frame(hwnd) ?? outer.ToPixelRect();
        // 隱形邊框：外框比看得到的範圍多出來的部分（通常左、右、下各 7 像素）
        int left = visible.Left - outer.Left;
        int top = visible.Top - outer.Top;
        int right = outer.Right - visible.Right;
        int bottom = outer.Bottom - visible.Bottom;

        return SetWindowPos(
            hwnd, IntPtr.Zero,
            frame.Left - left, frame.Top - top,
            frame.Width + left + right, frame.Height + top + bottom,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    private static string ClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        int length = GetClassName(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : "";
    }
}
