using Tatami.Win32;

namespace Tatami;

/// <summary>
/// 記住最後一個在前景、可以排列的視窗。
///
/// 點通知區域的圖示時，工作列會先變成前景視窗，這時 GetForegroundWindow 拿到的是工作列，
/// 所以要記住點之前的那一個視窗，「顯示格線」才知道要搬哪個視窗。
/// </summary>
public sealed class ForegroundTracker : IDisposable
{
    private readonly WinEventHook hook;
    private IntPtr lastWindow;

    public ForegroundTracker()
    {
        lastWindow = WindowMover.ForegroundWindow();
        hook = new WinEventHook(WinEventHook.ForegroundChanged, WinEventHook.ForegroundChanged, (_, hwnd) =>
        {
            if (WindowMover.IsArrangeable(hwnd)) lastWindow = hwnd;
        });
    }

    /// <summary>目前的前景視窗；前景是工作列、桌面或 Tatami 自己時，用最後一個一般視窗。</summary>
    public IntPtr TargetWindow()
    {
        var current = WindowMover.ForegroundWindow();
        if (current != IntPtr.Zero) return current;
        return WindowMover.IsArrangeable(lastWindow) ? lastWindow : IntPtr.Zero;
    }

    public void Dispose() => hook.Dispose();
}
