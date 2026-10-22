using System.Windows;

namespace Tatami;

/// <summary>說明的一段：圖示、標題、內文（已翻譯好）。</summary>
public sealed record HelpTopic(string Glyph, string Title, string Body);

/// <summary>
/// 「說明」視窗：簡單介紹各項功能的操作方式，跟隨設定裡選的語言。
/// 和 macOS 版的段落相同；說明 macOS 操作的內文，翻譯檔裡有 Windows 版本（.windows），會自動優先使用。
/// </summary>
public partial class HelpWindow : Window
{
    /// <summary>每一段說明：圖示（Segoe Fluent Icons）、標題 key、內文 key</summary>
    private static readonly (string Glyph, string Title, string Body)[] TopicKeys =
    [
        ("", "help.grid.title", "help.grid.body"),
        ("", "help.snap.title", "help.snap.body"),
        ("", "help.shortcuts.title", "help.shortcuts.body"),
        ("", "help.screens.title", "help.screens.body"),
        ("", "help.permission.title", "help.permission.body"),
        ("", "help.updates.title", "help.updates.body"),
        ("", "help.backup.title", "help.backup.body"),
    ];

    public HelpWindow()
    {
        InitializeComponent();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= OnLanguageChanged;
        UpdateTopics();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateTopics();

    private void UpdateTopics() =>
        Topics.ItemsSource = TopicKeys.Select(t => new HelpTopic(t.Glyph, Loc.L(t.Title), Loc.L(t.Body))).ToList();
}
