using System.Windows.Threading;
using Tatami.Core;
using Tatami.Win32;

namespace Tatami;

/// <summary>
/// 負責在每個螢幕顯示格線面板，並在使用者選好範圍後搬移視窗。
/// 在哪個螢幕的面板上選取，視窗就會搬到那個螢幕。
/// </summary>
public sealed class GridController
{
    private readonly AppSettings settings;
    private readonly List<GridPanelWindow> panels = [];
    private IntPtr targetWindow;
    private Grid grid = Grid.Default;

    public GridController(AppSettings settings)
    {
        this.settings = settings;
    }

    /// <summary>
    /// 顯示格線。target 是要搬的視窗，呼叫端要在 Tatami 的任何視窗出現之前先取得
    /// （快捷鍵：當下的前景視窗；通知區域選單：打開選單前的前景視窗）。
    /// </summary>
    public void Show(IntPtr target)
    {
        Close();
        // 每次打開都讀取最新設定，改了欄數列數馬上生效
        grid = settings.Grid;

        if (target == IntPtr.Zero || WindowMover.Frame(target) is not { } frame)
        {
            WindowMover.Beep();
            return;
        }
        targetWindow = target;
        var currentMonitor = Monitors.FromRect(frame);

        foreach (var monitor in Monitors.All())
        {
            var panel = new GridPanelWindow(monitor, grid);
            panel.GridView.Selected += selection => Apply(selection, monitor);
            panel.GridView.Cancelled += Cancel;
            panel.Deactivated += OnPanelDeactivated;
            // 焦點不在格線上時（例如剛點過面板邊緣），Esc 也要能取消
            panel.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Escape) return;
                e.Handled = true;
                Cancel();
            };
            panels.Add(panel);
            panel.ShowCentered();
        }

        // 視窗目前所在螢幕的面板取得焦點，讓 Esc 可以直接關閉
        var keyPanel = panels.FirstOrDefault(p => p.Monitor.Handle == currentMonitor.Handle) ?? panels.FirstOrDefault();
        if (keyPanel is null) return;
        keyPanel.Activate();
        keyPanel.GridView.Focus();
    }

    public void Close()
    {
        var closing = panels.ToList();
        panels.Clear();
        targetWindow = IntPtr.Zero;
        foreach (var panel in closing)
        {
            panel.Deactivated -= OnPanelDeactivated;
            panel.Close();
        }
    }

    /// <summary>按 Esc：關閉面板，把焦點還給原本的視窗。</summary>
    private void Cancel()
    {
        var target = targetWindow;
        Close();
        WindowMover.Activate(target);
    }

    /// <summary>點到面板外面時自動關閉；在面板之間切換則不關閉。</summary>
    private void OnPanelDeactivated(object? sender, EventArgs e)
    {
        // 等另一個面板的啟用事件處理完再檢查
        System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (panels.Count > 0 && !panels.Any(p => p.IsActive)) Close();
        });
    }

    private void Apply(GridSelection selection, MonitorInfo monitor)
    {
        var target = targetWindow;
        if (target == IntPtr.Zero) return;
        var frame = grid.Frame(selection, monitor.WorkArea);
        Close();
        if (WindowMover.SetFrame(target, frame))
        {
            WindowMover.Activate(target);
        }
        else
        {
            // 例如以系統管理員身分執行的視窗
            WindowMover.Beep();
        }
    }
}
