namespace Tatami.Core;

/// <summary>
/// 實體像素的矩形（Win32 座標：原點在主螢幕左上角，y 往下）。
/// Right、Bottom 不包含在矩形內，和 Win32 的 RECT 相同。
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    /// <summary>other 是否完全在這個矩形裡面。</summary>
    public bool Contains(PixelRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Contains(PixelPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>和 other 重疊的面積，沒有重疊時是 0。</summary>
    public long IntersectionArea(PixelRect other)
    {
        long width = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
        long height = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }
}

/// <summary>實體像素的座標點。</summary>
public readonly record struct PixelPoint(int X, int Y);
