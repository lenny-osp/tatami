using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Tatami.Core;

namespace Tatami;

/// <summary>
/// 介面翻譯給 WPF 用的入口。XAML 這樣綁定：
/// <code>{Binding [menu.quit], Source={x:Static local:Loc.Instance}}</code>
/// 切換語言時發出 PropertyChanged("Item[]")，所有綁定立刻更新，不用重新開視窗。
/// 程式碼裡用 <c>Loc.L("menu.quit")</c>。
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Localizer localizer = new(_ => new Dictionary<string, string>());

    private Loc() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>App 啟動時呼叫一次，從執行檔旁邊的 Locales 資料夾讀取翻譯。</summary>
    public void Initialize(string localesDirectory, AppLanguage language)
    {
        localizer.LanguageChanged -= OnLanguageChanged;
        localizer = new Localizer(localesDirectory) { Language = language };
        localizer.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged(this, EventArgs.Empty);
    }

    public AppLanguage Language
    {
        get => localizer.Language;
        set => localizer.Language = value;
    }

    public string this[string key] => localizer.Get(key);

    /// <summary>阿拉伯文由右到左排列</summary>
    public FlowDirection FlowDirection => Language.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>讓 WPF 依語言選字型（例如中文、日文的漢字字形）</summary>
    public XmlLanguage XmlLanguage => XmlLanguage.GetLanguage(Language.Code);

    /// <summary>語言切換後觸發（給不是用綁定更新的地方，例如選單、視窗標題）。</summary>
    public event EventHandler? LanguageChanged;

    public static string L(string key) => Instance[key];

    public static string L(string key, params object?[] arguments) => Instance.localizer.Get(key, arguments);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XmlLanguage)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 含參數的翻譯：第一個值是翻譯好的格式字串（綁定 [key]），其餘是參數。
/// 語言或參數任何一個改變都會重新計算。
/// </summary>
public sealed class LocFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not string format) return "";
        return Localizer.Format(format, values.Skip(1).ToArray());
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
