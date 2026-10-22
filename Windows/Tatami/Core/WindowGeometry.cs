namespace Tatami.Core;

/// <summary>視窗外框的換算（不碰 Win32，可以單元測試）。</summary>
public static class WindowGeometry
{
    /// <summary>浮點誤差的容許值：避免 400.0000001 被無條件進位成 401</summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    /// 把 frame 從 from 區域等比例換算到 to 區域，並確保整個視窗都在 to 裡面。
    /// 例如在左半邊的視窗，搬過去後還是在左半邊。
    /// </summary>
    public static PixelRect RelativeFrame(PixelRect frame, PixelRect from, PixelRect to)
    {
        double scaleX = (double)to.Width / from.Width;
        double scaleY = (double)to.Height / from.Height;
        double width = Math.Min(frame.Width * scaleX, to.Width);
        double height = Math.Min(frame.Height * scaleY, to.Height);
        double x = to.Left + (frame.Left - from.Left) * scaleX;
        double y = to.Top + (frame.Top - from.Top) * scaleY;

        x = Math.Min(Math.Max(x, to.Left), to.Right - width);
        y = Math.Min(Math.Max(y, to.Top), to.Bottom - height);

        // 和 CGRect.integral 相同：取能包住結果的最小整數矩形
        int left = (int)Math.Floor(x + Epsilon);
        int top = (int)Math.Floor(y + Epsilon);
        int right = (int)Math.Ceiling(x + width - Epsilon);
        int bottom = (int)Math.Ceiling(y + height - Epsilon);
        return PixelRect.FromEdges(left, top, right, bottom);
    }
}

/// <summary>拖曳視窗到螢幕邊緣時要排列成的樣子。</summary>
public sealed record SnapZone
{
    public static SnapZone Maximize { get; } = new("maximize", new GridSelection(0, 0, 6, 6));
    public static SnapZone LeftHalf { get; } = new("leftHalf", new GridSelection(0, 0, 3, 6));
    public static SnapZone RightHalf { get; } = new("rightHalf", new GridSelection(3, 0, 3, 6));

    /// <summary>上面的選取範圍都是以 6 × 6 格線表示</summary>
    public static Grid Grid { get; } = new(6, 6);

    /// <summary>游標離螢幕邊緣多近才觸發（DIP，實際像素要乘上螢幕縮放比例）</summary>
    public const double EdgeMargin = 5;

    private SnapZone(string name, GridSelection selection)
    {
        Name = name;
        Selection = selection;
    }

    public string Name { get; }
    public GridSelection Selection { get; }

    /// <summary>
    /// 游標在螢幕 screen 的哪個邊緣。Win32 座標的頂端是 Top（最小的 y）。
    /// 左右邊緣優先於頂端，這樣在左上、右上角也能排成半邊。
    /// </summary>
    public static SnapZone? At(PixelPoint point, PixelRect screen, double margin = EdgeMargin)
    {
        if (point.X <= screen.Left + margin) return LeftHalf;
        if (point.X >= screen.Right - margin) return RightHalf;
        if (point.Y <= screen.Top + margin) return Maximize;
        return null;
    }

    public override string ToString() => Name;
}
