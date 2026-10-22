using System.Windows.Threading;
using Tatami.Core;
using Tatami.Win32;
using static Tatami.Win32.NativeMethods;

namespace Tatami;

/// <summary>
/// 使用者拖曳視窗到螢幕邊緣並放開時，自動排列視窗。
///
/// 流程：
/// 1. 開始移動或調整大小（EVENT_SYSTEM_MOVESIZESTART）：記住視窗和它的外框，開始每 16 ms 看一次游標
/// 2. 拖曳中：外框位置變了但大小沒變，才算「正在移動視窗」（排除調整大小）；
///    接著看游標在哪個螢幕的哪個邊緣，顯示預覽
/// 3. 放開（EVENT_SYSTEM_MOVESIZEEND）：如果在邊緣上，把視窗排列到游標所在的螢幕
/// </summary>
public sealed class SnapController : IDisposable
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly SnapPreviewWindow preview = new();
    private WinEventHook? hook;

    private IntPtr draggedWindow;
    private PixelRect? initialFrame;
    private bool wasMaximized;
    private bool isMovingWindow;
    private PixelRect? pendingFrame;

    public SnapController()
    {
        timer.Tick += (_, _) => Poll();
    }

    public bool IsRunning => hook?.IsActive == true;

    public void Start()
    {
        if (hook is not null) return;
        hook = new WinEventHook(WinEventHook.MoveSizeStart, WinEventHook.MoveSizeEnd, OnEvent);
        if (!hook.IsActive) Stop();
    }

    public void Stop()
    {
        hook?.Dispose();
        hook = null;
        Reset();
    }

    public void Dispose()
    {
        Stop();
        preview.Close();
    }

    private void OnEvent(uint eventType, IntPtr hwnd)
    {
        if (eventType == WinEventHook.MoveSizeStart)
        {
            Reset();
            if (!WindowMover.IsArrangeable(hwnd)) return;
            draggedWindow = hwnd;
            initialFrame = WindowMover.Frame(hwnd);
            wasMaximized = IsZoomed(hwnd);
            timer.Start();
        }
        else if (eventType == WinEventHook.MoveSizeEnd)
        {
            if (isMovingWindow && hwnd == draggedWindow && pendingFrame is { } target)
            {
                // 稍等一下，讓 Windows 先處理完拖曳結束（放好它自己的位置），才不會被蓋掉
                var window = hwnd;
                var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                delay.Tick += (_, _) =>
                {
                    delay.Stop();
                    if (!WindowMover.SetFrame(window, target)) WindowMover.Beep();
                };
                delay.Start();
            }
            Reset();
        }
    }

    private void Poll()
    {
        if (draggedWindow == IntPtr.Zero || initialFrame is not { } initial) return;
        if (!isMovingWindow)
        {
            if (WindowMover.Frame(draggedWindow) is not { } frame) return;
            if (wasMaximized && (frame.Width != initial.Width || frame.Height != initial.Height))
            {
                // 拖曳最大化的視窗時，Windows 會先把它還原成原本的大小：以還原後的外框重新比較
                initialFrame = frame;
                wasMaximized = false;
                return;
            }
            bool sameSize = frame.Width == initial.Width && frame.Height == initial.Height;
            bool moved = frame.X != initial.X || frame.Y != initial.Y;
            if (!sameSize || !moved) return;
            isMovingWindow = true;
        }
        if (GetCursorPos(out POINT cursor)) UpdatePreview(new PixelPoint(cursor.X, cursor.Y));
    }

    private void UpdatePreview(PixelPoint point)
    {
        var monitor = Monitors.FromPoint(point);
        var zone = monitor is null ? null : SnapZone.At(point, monitor.Bounds, SnapZone.EdgeMargin * monitor.Scale);
        if (monitor is null || zone is null)
        {
            pendingFrame = null;
            preview.Hide();
            return;
        }

        // 排列到「游標所在」的螢幕，而不是視窗原本的螢幕
        var target = SnapZone.Grid.Frame(zone.Selection, monitor.WorkArea);
        if (target != pendingFrame)
        {
            pendingFrame = target;
            preview.ShowAt(target, monitor.Scale);
        }
    }

    private void Reset()
    {
        timer.Stop();
        draggedWindow = IntPtr.Zero;
        initialFrame = null;
        wasMaximized = false;
        isMovingWindow = false;
        pendingFrame = null;
        preview.Hide();
    }
}
