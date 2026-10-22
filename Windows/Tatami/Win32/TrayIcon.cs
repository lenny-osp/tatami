using System.Runtime.InteropServices;
using System.Windows.Interop;
using static Tatami.Win32.NativeMethods;

namespace Tatami.Win32;

/// <summary>
/// 通知區域（系統匣）的圖示，用 Shell_NotifyIcon。
///
/// 圖示的訊息送到一個看不見的頂層視窗。不用訊息專用視窗（HWND_MESSAGE），因為：
/// 1. 打開選單前要對它呼叫 SetForegroundWindow，點選單外面時選單才會關閉；
/// 2. 檔案總管重新啟動時廣播的 TaskbarCreated、主題變更的 WM_SETTINGCHANGE 只會送到頂層視窗。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const int CallbackMessage = WM_APP + 1;

    private readonly HwndSource window;
    private readonly uint taskbarCreatedMessage;
    private readonly string tooltip;
    private IntPtr icon;
    private bool added;

    public TrayIcon(string tooltip, IntPtr icon)
    {
        this.tooltip = tooltip;
        this.icon = icon;
        window = new HwndSource(new HwndSourceParameters("TatamiTray", 0, 0)
        {
            WindowStyle = 0,
            ExtendedWindowStyle = (int)WS_EX_TOOLWINDOW,
        });
        window.AddHook(WndProc);
        taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        Add();
    }

    /// <summary>點一下圖示（左鍵、右鍵或鍵盤）時觸發，用來打開選單。</summary>
    public event Action? Invoked;

    /// <summary>淺色／深色主題、縮放比例或螢幕設定變了，圖示可能要重畫。</summary>
    public event Action? AppearanceChanged;

    public void SetIcon(IntPtr newIcon)
    {
        icon = newIcon;
        var data = NewData(NIF_ICON);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    /// <summary>讓 Tatami 成為前景程式，選單才會在點到外面時關閉。</summary>
    public void BringToForeground() => SetForegroundWindow(window.Handle);

    public void Dispose()
    {
        if (added)
        {
            var data = NewData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            added = false;
        }
        window.RemoveHook(WndProc);
        window.Dispose();
    }

    private void Add()
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (!added) return;
        // 版本 4：點一下送 NIN_SELECT，右鍵送 WM_CONTEXTMENU，鍵盤送 NIN_KEYSELECT
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private unsafe NOTIFYICONDATAW NewData(uint flags)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = window.Handle,
            uID = IconId,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = icon,
        };
        var tip = tooltip.AsSpan(0, Math.Min(tooltip.Length, 127));
        tip.CopyTo(new Span<char>(data.szTip, 128));
        return data;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == CallbackMessage)
        {
            int trayEvent = (int)(lParam.ToInt64() & 0xFFFF);
            if (trayEvent is NIN_SELECT or NIN_KEYSELECT or WM_CONTEXTMENU)
            {
                handled = true;
                Invoked?.Invoke();
            }
        }
        else if (message == (int)taskbarCreatedMessage)
        {
            // 檔案總管重新啟動，通知區域是新的，要重新加入圖示
            Add();
            AppearanceChanged?.Invoke();
        }
        else if (message is WM_SETTINGCHANGE or WM_DISPLAYCHANGE or WM_DPICHANGED)
        {
            AppearanceChanged?.Invoke();
        }
        return IntPtr.Zero;
    }
}
