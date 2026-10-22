using System.Runtime.InteropServices;
using Tatami.Core;
using static Tatami.Win32.NativeMethods;

namespace Tatami.Win32;

/// <summary>一個螢幕：整個範圍、工作區（扣掉工作列）和縮放比例，全部是實體像素。</summary>
public sealed record MonitorInfo(IntPtr Handle, PixelRect Bounds, PixelRect WorkArea, uint Dpi, bool IsPrimary)
{
    /// <summary>1.0 = 100%（96 DPI）</summary>
    public double Scale => Dpi / 96.0;
}

/// <summary>列出螢幕、找出某個點或矩形所在的螢幕。</summary>
public static class Monitors
{
    /// <summary>所有螢幕，依照由左到右（同一欄由上到下）排序，「搬到下一個螢幕」用這個順序。</summary>
    public static IReadOnlyList<MonitorInfo> All()
    {
        var handles = new List<IntPtr>();
        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            handles.Add(hMonitor);
            return 1;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero);
        GC.KeepAlive(callback);

        return handles
            .Select(Describe)
            .OfType<MonitorInfo>()
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();
    }

    public static MonitorInfo Primary() =>
        Describe(MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY)) ?? Fallback();

    /// <summary>某個點所在的螢幕；在所有螢幕外面時是 null。</summary>
    public static MonitorInfo? FromPoint(PixelPoint point)
    {
        var handle = MonitorFromPoint(new POINT { X = point.X, Y = point.Y }, MONITOR_DEFAULTTONULL);
        return handle == IntPtr.Zero ? null : Describe(handle);
    }

    /// <summary>和矩形重疊面積最大的螢幕（都沒有重疊時是最近的螢幕）。</summary>
    public static MonitorInfo FromRect(PixelRect rect)
    {
        var native = new RECT { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
        return Describe(MonitorFromRect(native, MONITOR_DEFAULTTONEAREST)) ?? Primary();
    }

    private static MonitorInfo? Describe(IntPtr handle)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (handle == IntPtr.Zero || !GetMonitorInfo(handle, ref info)) return null;
        uint dpi = GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX : 96;
        return new MonitorInfo(
            handle,
            info.rcMonitor.ToPixelRect(),
            info.rcWork.ToPixelRect(),
            dpi,
            (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }

    private static MonitorInfo Fallback()
    {
        var area = new PixelRect(0, 0, 1920, 1080);
        return new MonitorInfo(IntPtr.Zero, area, area, 96, true);
    }
}
