using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tatami.Core;

namespace Tatami;

/// <summary>畫出格線，讓使用者用滑鼠拖曳選取範圍。格線面板和設定畫面的區塊挑選器共用。</summary>
public class GridView : FrameworkElement
{
    public static readonly DependencyProperty GridProperty = DependencyProperty.Register(
        nameof(Grid), typeof(Grid), typeof(GridView),
        new FrameworkPropertyMetadata(Grid.Default, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>固定顯示的選取範圍（設定畫面的區塊挑選器用），格線面板不需要設定</summary>
    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection), typeof(GridSelection), typeof(GridView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CellPaddingProperty = DependencyProperty.Register(
        nameof(CellPadding), typeof(double), typeof(GridView),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(GridView),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CellCornerRadiusProperty = DependencyProperty.Register(
        nameof(CellCornerRadius), typeof(double), typeof(GridView),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>未選取格子的顏色（畫成 12% 不透明度）</summary>
    public static readonly DependencyProperty CellBrushProperty = DependencyProperty.Register(
        nameof(CellBrush), typeof(Brush), typeof(GridView),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    private (int Column, int Row)? dragStart;
    private (int Column, int Row)? dragCurrent;
    private (int Column, int Row)? hovered;

    public GridView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        // 格線代表實際的螢幕：阿拉伯文由右到左排列時也不能左右顛倒
        FlowDirection = FlowDirection.LeftToRight;
    }

    /// <summary>放開滑鼠時選好的範圍</summary>
    public event Action<GridSelection>? Selected;

    /// <summary>按下 Esc</summary>
    public event Action? Cancelled;

    public Grid Grid
    {
        get => (Grid)GetValue(GridProperty);
        set => SetValue(GridProperty, value);
    }

    public GridSelection? Selection
    {
        get => (GridSelection?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public double CellPadding
    {
        get => (double)GetValue(CellPaddingProperty);
        set => SetValue(CellPaddingProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double CellCornerRadius
    {
        get => (double)GetValue(CellCornerRadiusProperty);
        set => SetValue(CellCornerRadiusProperty, value);
    }

    public Brush CellBrush
    {
        get => (Brush)GetValue(CellBrushProperty);
        set => SetValue(CellBrushProperty, value);
    }

    // MARK: - 繪製

    protected override void OnRender(DrawingContext drawingContext)
    {
        // 透明底色：格子之間的縫也要能接收滑鼠事件
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var grid = Grid;
        if (grid.Columns < 1 || grid.Rows < 1) return;
        var area = Area();
        if (area.Width <= 0 || area.Height <= 0) return;

        double cellWidth = area.Width / grid.Columns;
        double cellHeight = area.Height / grid.Rows;
        var selection = CurrentSelection();
        var selectedBrush = new SolidColorBrush(SystemColors.AccentColor);
        var cellBrush = CellBrush;

        for (int row = 0; row < grid.Rows; row++)
        {
            for (int column = 0; column < grid.Columns; column++)
            {
                var rect = new Rect(area.X + column * cellWidth, area.Y + row * cellHeight, cellWidth, cellHeight);
                rect.Inflate(-Gap / 2, -Gap / 2);
                if (rect.Width <= 0 || rect.Height <= 0) continue;

                bool isSelected = selection?.Contains(column, row) == true;
                if (isSelected)
                {
                    drawingContext.DrawRoundedRectangle(selectedBrush, null, rect, CellCornerRadius, CellCornerRadius);
                }
                else
                {
                    drawingContext.PushOpacity(0.12);
                    drawingContext.DrawRoundedRectangle(cellBrush, null, rect, CellCornerRadius, CellCornerRadius);
                    drawingContext.Pop();
                }
            }
        }
    }

    // MARK: - 滑鼠與鍵盤

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart is not null)
        {
            dragCurrent = Cell(e.GetPosition(this));
        }
        else
        {
            hovered = Cell(e.GetPosition(this));
        }
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        dragStart = Cell(e.GetPosition(this));
        dragCurrent = dragStart;
        CaptureMouse();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (dragStart is null) return;
        var selection = CurrentSelection();
        dragStart = null;
        dragCurrent = null;
        ReleaseMouseCapture();
        e.Handled = true;
        InvalidateVisual();
        if (selection is not null) Selected?.Invoke(selection);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // 拖曳中被其他視窗搶走滑鼠（例如按了 Alt+Tab）：放棄這次選取
        if (dragStart is null) return;
        dragStart = null;
        dragCurrent = null;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Cancelled is not null)
        {
            e.Handled = true;
            Cancelled();
        }
    }

    // MARK: - 工具

    private Rect Area() => new(
        CellPadding, CellPadding,
        Math.Max(0, ActualWidth - CellPadding * 2), Math.Max(0, ActualHeight - CellPadding * 2));

    /// <summary>優先順序：拖曳中的範圍 → 固定的選取範圍 → 滑鼠所在的那一格。</summary>
    private GridSelection? CurrentSelection()
    {
        if (dragStart is { } start && dragCurrent is { } end)
        {
            return new GridSelection(
                Math.Min(start.Column, end.Column),
                Math.Min(start.Row, end.Row),
                Math.Abs(start.Column - end.Column) + 1,
                Math.Abs(start.Row - end.Row) + 1);
        }
        if (Selection is { } selection) return selection;
        if (hovered is { } cell) return new GridSelection(cell.Column, cell.Row, 1, 1);
        return null;
    }

    /// <summary>滑鼠位置對應到哪一格（超出範圍時夾在邊界內，拖到外面也能正常選取）。</summary>
    private (int Column, int Row) Cell(Point point)
    {
        var grid = Grid;
        var area = Area();
        int columns = Math.Max(grid.Columns, 1);
        int rows = Math.Max(grid.Rows, 1);
        int column = area.Width > 0 ? (int)Math.Floor((point.X - area.X) / (area.Width / columns)) : 0;
        int row = area.Height > 0 ? (int)Math.Floor((point.Y - area.Y) / (area.Height / rows)) : 0;
        return (Math.Clamp(column, 0, columns - 1), Math.Clamp(row, 0, rows - 1));
    }
}

/// <summary>
/// 設定畫面的區塊挑選器：縮小的格線，拖曳後更新 Selection（可以雙向綁定）。
/// 長寬比和主螢幕的工作區一樣。
/// </summary>
public sealed class GridPicker : GridView
{
    static GridPicker()
    {
        SelectionProperty.OverrideMetadata(typeof(GridPicker), new FrameworkPropertyMetadata(
            null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    }

    public GridPicker()
    {
        CellPadding = 0;
        Gap = 2;
        CellCornerRadius = 2;
        Width = 120;
        var work = Win32.Monitors.Primary().WorkArea;
        Height = work.Width > 0 ? Math.Round(Width * work.Height / work.Width) : 75;
        Selected += selection => Selection = selection;
    }
}
