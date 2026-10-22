using Tatami.Core;

namespace Tatami.Tests;

public class GridTests
{
    /// <summary>左上原點、400×300 的區域，方便心算</summary>
    private static readonly PixelRect Area = new(100, 50, 400, 300);

    [Fact]
    public void FullSelectionFillsTheWholeArea()
    {
        var grid = new Grid(6, 6);
        var selection = new GridSelection(0, 0, 6, 6);
        Assert.Equal(Area, grid.Frame(selection, Area));
    }

    [Fact]
    public void RowZeroIsTheTopRow()
    {
        // Win32 座標的 y 往下，第 0 列就在區域的最上面（不用像 macOS 那樣翻轉）
        var grid = new Grid(2, 2);
        var topLeft = grid.Frame(new GridSelection(0, 0, 1, 1), Area);
        Assert.Equal(new PixelRect(100, 50, 200, 150), topLeft);
    }

    [Fact]
    public void BottomRightCell()
    {
        var grid = new Grid(2, 2);
        var bottomRight = grid.Frame(new GridSelection(1, 1, 1, 1), Area);
        Assert.Equal(new PixelRect(300, 200, 200, 150), bottomRight);
    }

    [Fact]
    public void AdjacentCellsShareAnEdgeWithoutGapsWhenRounding()
    {
        // 400 / 3 不是整數；相鄰兩格的邊界要完全接上，不能有縫或重疊
        var grid = new Grid(3, 1);
        var left = grid.Frame(new GridSelection(0, 0, 1, 1), Area);
        var middle = grid.Frame(new GridSelection(1, 0, 1, 1), Area);
        var right = grid.Frame(new GridSelection(2, 0, 1, 1), Area);
        Assert.Equal(left.Right, middle.Left);
        Assert.Equal(middle.Right, right.Left);
        Assert.Equal(Area.Right, right.Right);
    }

    [Fact]
    public void HalfPixelsRoundAwayFromZeroLikeSwift()
    {
        // 101 / 2 = 50.5：Swift 的 rounded() 進位成 51，不能用銀行家捨入變成 50
        var grid = new Grid(2, 1);
        var left = grid.Frame(new GridSelection(0, 0, 1, 1), new PixelRect(0, 0, 101, 10));
        Assert.Equal(51, left.Width);
    }

    [Theory]
    [MemberData(nameof(AllowedSizes))]
    public void EveryAllowedGridSizeCoversTheArea(int size)
    {
        var grid = new Grid(size, size);
        var selection = new GridSelection(0, 0, size, size);
        Assert.Equal(Area, grid.Frame(selection, Area));
    }

    public static TheoryData<int> AllowedSizes() => new(Enumerable.Range(Grid.MinSize, Grid.MaxSize));

    [Fact]
    public void GridSizeIsClampedToTheAllowedRange()
    {
        Assert.Equal(1, Grid.ClampSize(0));
        Assert.Equal(10, Grid.ClampSize(11));
        Assert.Equal(6, Grid.ClampSize(6));
    }
}
