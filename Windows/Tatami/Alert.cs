using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Tatami;

/// <summary>
/// 簡單的訊息對話框。不用系統的 MessageBox，因為它的按鈕文字跟隨 Windows 的語言，
/// 而 Tatami 的介面語言是在 App 裡選的；這個對話框也會套用 Fluent 主題和由右到左排列。
/// </summary>
public static class Alert
{
    /// <summary>顯示訊息。有 confirmButton 時多一個取消按鈕，按下確認回傳 true。</summary>
    public static bool Show(Window? owner, string title, string message, string? confirmButton = null)
    {
        // 沒有指定時，顯示在目前使用中的 Tatami 視窗（例如設定視窗）前面
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var window = new Window
        {
            Title = "Tatami",
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = owner is null,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            MinWidth = 340,
            MaxWidth = 480,
        };
        if (owner is { IsVisible: true }) window.Owner = owner;
        window.SetBinding(FrameworkElement.FlowDirectionProperty, new Binding(nameof(Loc.FlowDirection)) { Source = Loc.Instance });
        window.SetBinding(FrameworkElement.LanguageProperty, new Binding(nameof(Loc.XmlLanguage)) { Source = Loc.Instance });

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrEmpty(message))
        {
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };
        bool result = false;
        var ok = new Button
        {
            Content = confirmButton ?? Loc.L("backup.ok"),
            MinWidth = 90,
            IsDefault = true,
        };
        ok.Click += (_, _) =>
        {
            result = true;
            window.Close();
        };
        buttons.Children.Add(ok);
        if (confirmButton is not null)
        {
            ok.Style = Application.Current?.TryFindResource("AccentButtonStyle") as Style;
            var cancel = new Button
            {
                Content = Loc.L("backup.cancel"),
                MinWidth = 90,
                IsCancel = true,
                Margin = new Thickness(8, 0, 0, 0),
            };
            buttons.Children.Add(cancel);
        }
        else
        {
            ok.IsCancel = true;
        }
        panel.Children.Add(buttons);
        window.Content = panel;

        window.ShowDialog();
        return result;
    }
}
