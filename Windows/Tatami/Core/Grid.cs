using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Tatami.Core;

/// <summary>格線上選取的範圍。row 0 是最上面那一列，column 0 是最左邊那一欄。</summary>
public sealed record GridSelection
{
    [JsonConstructor]
    public GridSelection() { }

    [SetsRequiredMembers]
    public GridSelection(int column, int row, int columns, int rows)
    {
        Column = column;
        Row = row;
        Columns = columns;
        Rows = rows;
    }

    public required int Column { get; init; }
    public required int Row { get; init; }
    public required int Columns { get; init; }
    public required int Rows { get; init; }

    /// <summary>這一格是否在選取範圍內。</summary>
    public bool Contains(int column, int row) =>
        column >= Column && column < Column + Columns && row >= Row && row < Row + Rows;
}

/// <summary>把畫面切成 Columns × Rows 格的格線。</summary>
public sealed record Grid
{
    /// <summary>格線欄數、列數的允許範圍</summary>
    public const int MinSize = 1;
    public const int MaxSize = 10;

    /// <summary>預設 6 × 6</summary>
    public static Grid Default { get; } = new(6, 6);

    [JsonConstructor]
    public Grid() { }

    [SetsRequiredMembers]
    public Grid(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public required int Columns { get; init; }
    public required int Rows { get; init; }

    public static int ClampSize(int value) => Math.Clamp(value, MinSize, MaxSize);

    /// <summary>
    /// 把格線選取範圍換算成實際的矩形。Win32 座標的 y 往下，row 0 就在最上面，不需要翻轉。
    /// </summary>
    public PixelRect Frame(GridSelection selection, PixelRect area)
    {
        double cellWidth = (double)area.Width / Columns;
        double cellHeight = (double)area.Height / Rows;

        double minX = area.Left + selection.Column * cellWidth;
        double maxX = area.Left + (selection.Column + selection.Columns) * cellWidth;
        double minY = area.Top + selection.Row * cellHeight;
        double maxY = area.Top + (selection.Row + selection.Rows) * cellHeight;

        // 先把四條邊各自取整數再算寬高，相鄰兩格的邊界才會完全接上，不會有 1 像素的縫
        return PixelRect.FromEdges(Round(minX), Round(minY), Round(maxX), Round(maxY));
    }

    /// <summary>和 Swift 的 rounded() 一樣：0.5 一律進位（遠離 0），不用銀行家捨入。</summary>
    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
