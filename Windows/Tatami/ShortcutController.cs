using Tatami.Win32;

namespace Tatami;

/// <summary>依照設定註冊全域快捷鍵，按下時搬移目前的視窗。</summary>
public sealed class ShortcutController : IDisposable
{
    private readonly AppSettings settings;
    private readonly GridController gridController;
    private readonly HotKeyCenter hotKeys = new();

    public ShortcutController(AppSettings settings, GridController gridController)
    {
        this.settings = settings;
        this.gridController = gridController;
        settings.ShortcutsChanged += Reload;
        Reload();
    }

    public void Dispose()
    {
        settings.ShortcutsChanged -= Reload;
        hotKeys.Dispose();
    }

    private void Reload()
    {
        hotKeys.UnregisterAll();
        // 錄製快捷鍵時先全部停用，否則按下已存在的組合會直接觸發，錄不到
        if (settings.IsRecordingShortcut) return;

        settings.IsShowGridConflicting = settings.ShowGridKeyCombo is { } showGrid
            // 先取得前景視窗，再顯示格線面板
            && !hotKeys.Register(showGrid, () => gridController.Show(WindowMover.ForegroundWindow()));
        settings.IsNextScreenConflicting = settings.NextScreenKeyCombo is { } nextScreen
            && !hotKeys.Register(nextScreen, MoveToNextScreen);

        foreach (var shortcut in settings.Shortcuts)
        {
            shortcut.HasConflict = shortcut.KeyCombo is { } combo
                && !hotKeys.Register(combo, () => Apply(shortcut));
        }
    }

    private static void Apply(Core.WindowShortcut shortcut)
    {
        var window = WindowMover.ForegroundWindow();
        if (window == IntPtr.Zero || WindowMover.Frame(window) is not { } frame)
        {
            WindowMover.Beep();
            return;
        }
        var monitor = Monitors.FromRect(frame);
        var target = shortcut.Grid.Frame(shortcut.Selection, monitor.WorkArea);
        if (!WindowMover.SetFrame(window, target)) WindowMover.Beep();
    }

    private static void MoveToNextScreen()
    {
        var window = WindowMover.ForegroundWindow();
        if (window == IntPtr.Zero || !WindowMover.MoveToNextScreen(window)) WindowMover.Beep();
    }
}
