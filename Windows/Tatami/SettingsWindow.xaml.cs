using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tatami.Core;

namespace Tatami;

/// <summary>設定畫面：「一般」和「視窗排列」兩個分頁，內容綁定到 AppSettings。</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;

    /// <summary>每個分頁對應的翻譯 key，視窗標題顯示目前分頁的名稱（和 macOS 版相同）</summary>
    private static readonly string[] TabTitleKeys = ["tab.general", "tab.arrangement"];

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        DataContext = settings;
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= OnLanguageChanged;
        UpdateTitle();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateTitle();

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Tabs) UpdateTitle();
    }

    private void UpdateTitle()
    {
        int index = Math.Clamp(Tabs.SelectedIndex, 0, TabTitleKeys.Length - 1);
        Title = Loc.L(TabTitleKeys[index]);
    }

    private void OnExportClick(object sender, RoutedEventArgs e) => SettingsTransfer.Export(settings, this);

    private void OnImportClick(object sender, RoutedEventArgs e) => SettingsTransfer.Import(settings, this);

    private void OnColumnsDown(object sender, RoutedEventArgs e) => settings.GridColumns--;

    private void OnColumnsUp(object sender, RoutedEventArgs e) => settings.GridColumns++;

    private void OnRowsDown(object sender, RoutedEventArgs e) => settings.GridRows--;

    private void OnRowsUp(object sender, RoutedEventArgs e) => settings.GridRows++;

    private void OnAddShortcutClick(object sender, RoutedEventArgs e) => settings.AddShortcut();

    private void OnDeleteShortcutClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowShortcut shortcut }) settings.DeleteShortcut(shortcut);
    }
}

/// <summary>文字是空的時候顯示（輸入框的提示文字用）。</summary>
public sealed class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
