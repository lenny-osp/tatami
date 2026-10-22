using Tatami.Core;

namespace Tatami.Tests;

public class NextScreenTests
{
    private static readonly PixelRect Small = new(0, 0, 1000, 800);
    private static readonly PixelRect Large = new(1000, 0, 2000, 1600);

    [Fact]
    public void LeftHalfStaysTheLeftHalf()
    {
        var leftHalf = new PixelRect(0, 0, 500, 800);
        var moved = WindowGeometry.RelativeFrame(leftHalf, Small, Large);
        Assert.Equal(new PixelRect(1000, 0, 1000, 1600), moved);
    }

    [Fact]
    public void MovingBackRestoresTheOriginalFrame()
    {
        var window = new PixelRect(100, 200, 300, 400);
        var there = WindowGeometry.RelativeFrame(window, Small, Large);
        var back = WindowGeometry.RelativeFrame(there, Large, Small);
        Assert.Equal(window, back);
    }

    [Fact]
    public void WindowIsKeptInsideTheTargetScreen()
    {
        // 目標螢幕比較矮，而且換算後會超出下緣
        var shortScreen = new PixelRect(1000, 0, 1000, 400);
        var tall = new PixelRect(0, 700, 1000, 800);
        var moved = WindowGeometry.RelativeFrame(tall, new PixelRect(0, 0, 1000, 1500), shortScreen);
        Assert.True(shortScreen.Contains(moved), $"{moved} is not inside {shortScreen}");
    }

    [Fact]
    public void ScreenWithNegativeCoordinates()
    {
        // 主螢幕左邊的螢幕，座標是負的
        var left = new PixelRect(-1920, 0, 1920, 1040);
        var primary = new PixelRect(0, 0, 2560, 1400);
        var rightHalf = new PixelRect(-960, 0, 960, 1040);
        var moved = WindowGeometry.RelativeFrame(rightHalf, left, primary);
        Assert.Equal(new PixelRect(1280, 0, 1280, 1400), moved);
    }
}

public class SnapZoneTests
{
    private static readonly PixelRect Screen = new(0, 0, 1440, 900);

    [Fact]
    public void LeftEdgeSnapsToLeftHalf() =>
        Assert.Equal(SnapZone.LeftHalf, SnapZone.At(new PixelPoint(2, 400), Screen));

    [Fact]
    public void RightEdgeSnapsToRightHalf() =>
        Assert.Equal(SnapZone.RightHalf, SnapZone.At(new PixelPoint(1438, 400), Screen));

    [Fact]
    public void TopEdgeMaximizes() =>
        // Win32 座標：頂端是最小的 y
        Assert.Equal(SnapZone.Maximize, SnapZone.At(new PixelPoint(700, 2), Screen));

    [Fact]
    public void MiddleOfTheScreenDoesNothing() =>
        Assert.Null(SnapZone.At(new PixelPoint(700, 400), Screen));

    [Fact]
    public void BottomEdgeDoesNothing() =>
        Assert.Null(SnapZone.At(new PixelPoint(700, 898), Screen));

    [Fact]
    public void TopCornerPrefersTheSideEdge() =>
        Assert.Equal(SnapZone.LeftHalf, SnapZone.At(new PixelPoint(1, 0), Screen));

    [Fact]
    public void WorksOnASecondaryScreen()
    {
        // 主螢幕右邊的第二個螢幕，座標不是從 0 開始
        var secondary = new PixelRect(1440, -200, 1920, 1080);
        Assert.Equal(SnapZone.LeftHalf, SnapZone.At(new PixelPoint(1442, 300), secondary));
        Assert.Equal(SnapZone.Maximize, SnapZone.At(new PixelPoint(2000, -198), secondary));
    }

    [Fact]
    public void MarginScalesWithDpi()
    {
        // 150% 縮放時邊緣是 7.5 像素
        Assert.Equal(SnapZone.LeftHalf, SnapZone.At(new PixelPoint(7, 400), Screen, SnapZone.EdgeMargin * 1.5));
        Assert.Null(SnapZone.At(new PixelPoint(7, 400), Screen));
    }

    [Fact]
    public void ZonesFillTheSixBySixGrid()
    {
        var area = new PixelRect(0, 0, 1200, 600);
        Assert.Equal(area, SnapZone.Grid.Frame(SnapZone.Maximize.Selection, area));
        Assert.Equal(new PixelRect(0, 0, 600, 600), SnapZone.Grid.Frame(SnapZone.LeftHalf.Selection, area));
        Assert.Equal(new PixelRect(600, 0, 600, 600), SnapZone.Grid.Frame(SnapZone.RightHalf.Selection, area));
    }
}
