using Microsoft.Win32;

namespace Tatami.Win32;

/// <summary>「登入時啟動」：HKCU\Software\Microsoft\Windows\CurrentVersion\Run 裡的 Tatami 值。</summary>
public static class StartupRegistry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Tatami";

    /// <summary>使用者可能在工作管理員的「啟動應用程式」關掉，或手動刪掉，所以每次都重新讀取。</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    /// <summary>開啟時寫入目前執行檔的路徑（加引號，路徑裡可能有空白）。失敗時丟出例外。</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown executable path");
            key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
