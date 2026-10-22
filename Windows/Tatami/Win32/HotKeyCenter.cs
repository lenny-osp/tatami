using System.Windows.Interop;
using Tatami.Core;
using static Tatami.Win32.NativeMethods;

namespace Tatami.Win32;

/// <summary>
/// 註冊全域快捷鍵。使用 Win32 的 RegisterHotKey：不需要額外權限，在任何 App 裡都能觸發。
/// WM_HOTKEY 送到一個看不見的訊息專用視窗（HWND_MESSAGE）。
/// </summary>
public sealed class HotKeyCenter : IDisposable
{
    private readonly HwndSource window;
    private readonly Dictionary<int, Action> actions = new();
    private int nextId;

    public HotKeyCenter()
    {
        window = new HwndSource(new HwndSourceParameters("TatamiHotKeys")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0,
        });
        window.AddHook(WndProc);
    }

    /// <summary>
    /// 註冊一組快捷鍵。組合已被其他程式（或自己）用掉時回傳 false。
    /// 按住不放不會重複觸發（MOD_NOREPEAT）。
    /// </summary>
    public bool Register(KeyCombo combo, Action action)
    {
        int id = ++nextId;
        if (!RegisterHotKey(window.Handle, id, (combo.Modifiers & KeyModifiers.All) | KeyModifiers.NoRepeat, combo.KeyCode))
        {
            return false;
        }
        actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (int id in actions.Keys) UnregisterHotKey(window.Handle, id);
        actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        window.RemoveHook(WndProc);
        window.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WM_HOTKEY && actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }
}
