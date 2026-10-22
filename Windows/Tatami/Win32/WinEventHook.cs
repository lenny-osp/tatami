using System.Runtime.InteropServices;
using static Tatami.Win32.NativeMethods;

namespace Tatami.Win32;

/// <summary>
/// 監聽其他程式的視窗事件（SetWinEventHook，WINEVENT_OUTOFCONTEXT）。
/// 回呼在建立它的執行緒（UI 執行緒）的訊息迴圈裡執行，不需要切換執行緒。
/// 只回報頂層視窗本身的事件（OBJID_WINDOW、CHILDID_SELF）。
/// </summary>
public sealed class WinEventHook : IDisposable
{
    private readonly WinEventProc callback;
    private readonly Action<uint, IntPtr> handler;
    private IntPtr hook;

    /// <param name="eventMin">第一個事件，例如 EVENT_SYSTEM_MOVESIZESTART</param>
    /// <param name="eventMax">最後一個事件</param>
    /// <param name="handler">事件代碼和視窗</param>
    public WinEventHook(uint eventMin, uint eventMax, Action<uint, IntPtr> handler)
    {
        this.handler = handler;
        // 委派要一直留著，否則被回收後系統呼叫回呼會當掉
        callback = OnEvent;
        hook = SetWinEventHook(
            eventMin, eventMax, IntPtr.Zero, Marshal.GetFunctionPointerForDelegate(callback), 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    public const uint ForegroundChanged = EVENT_SYSTEM_FOREGROUND;
    public const uint MoveSizeStart = EVENT_SYSTEM_MOVESIZESTART;
    public const uint MoveSizeEnd = EVENT_SYSTEM_MOVESIZEEND;

    public bool IsActive => hook != IntPtr.Zero;

    public void Dispose()
    {
        if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        hook = IntPtr.Zero;
    }

    private void OnEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF || hwnd == IntPtr.Zero) return;
        handler(eventType, hwnd);
    }
}
