using System.Text.Json;
using Tatami.Core;

namespace Tatami.Tests;

public class SettingsBackupTests
{
    [Fact]
    public void RoundTripKeepsEverySetting()
    {
        var grid = new Grid(4, 3);
        var backup = new SettingsBackup
        {
            Platform = SettingsBackup.PlatformWindows,
            Language = "ja",
            IsSnapEnabled = false,
            IsAutoUpdateEnabled = true,
            GridColumns = 4,
            GridRows = 3,
            ShowGridKeyCombo = new KeyCombo(0x47, KeyModifiers.Control | KeyModifiers.Alt, "Ctrl+Alt+G"),
            NextScreenKeyCombo = null,
            Shortcuts = [WindowShortcut.New("Left", grid)],
        };
        var decoded = SettingsBackup.FromJson(backup.ToJson());

        Assert.Equal("Tatami", decoded.App);
        Assert.Equal(SettingsBackup.CurrentFormatVersion, decoded.FormatVersion);
        Assert.Equal("windows", decoded.Platform);
        Assert.Equal("ja", decoded.Language);
        Assert.False(decoded.IsSnapEnabled);
        Assert.True(decoded.IsAutoUpdateEnabled);
        Assert.Equal(4, decoded.GridColumns);
        Assert.Equal(3, decoded.GridRows);
        Assert.Equal(backup.ShowGridKeyCombo, decoded.ShowGridKeyCombo);
        Assert.Null(decoded.NextScreenKeyCombo);
        AssertSameShortcuts(backup.Shortcuts, decoded.Shortcuts);
    }

    [Fact]
    public void FileWithOnlyTheHeaderStillImports()
    {
        // 其他版本的檔案可能少了某些欄位，要能讀，缺的欄位是 null（保留目前設定）
        var decoded = SettingsBackup.FromJson("""{"app": "Tatami", "formatVersion": 1}""");
        Assert.Null(decoded.Language);
        Assert.Null(decoded.Shortcuts);
        Assert.Null(decoded.ShortcutsForThisPlatform());
    }

    [Fact]
    public void UnrelatedJsonIsRejected()
    {
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.FromJson("""{"name": "something else"}"""));
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.FromJson("""{"app": "Other", "formatVersion": 1}"""));
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.FromJson("not json"));
    }

    [Fact]
    public void NewShortcutSelectsTheWholeGrid()
    {
        var shortcut = WindowShortcut.New("New", new Grid(5, 2));
        Assert.Equal(new GridSelection(0, 0, 5, 2), shortcut.Selection);
        Assert.Null(shortcut.KeyCombo);
    }

    [Fact]
    public void WritesTheSameFieldNamesAsMacOS()
    {
        var shortcut = new WindowShortcut(
            Guid.Parse("e621e1f8-c36c-495a-93fc-0c247a3e6e5f"), "Left", null, new Grid(6, 6), new GridSelection(0, 0, 3, 6));
        var json = new SettingsBackup { Platform = "windows", GridColumns = 6, Shortcuts = [shortcut] }.ToJson();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("Tatami", root.GetProperty("app").GetString());
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("windows", root.GetProperty("platform").GetString());
        Assert.Equal(6, root.GetProperty("gridColumns").GetInt32());
        // 沒設定的欄位不寫（和 Swift 的 Codable 一樣）
        Assert.False(root.TryGetProperty("language", out _));
        var item = root.GetProperty("shortcuts")[0];
        Assert.Equal("E621E1F8-C36C-495A-93FC-0C247A3E6E5F", item.GetProperty("id").GetString());
        Assert.Equal(6, item.GetProperty("grid").GetProperty("columns").GetInt32());
        Assert.Equal(3, item.GetProperty("selection").GetProperty("columns").GetInt32());
        Assert.False(item.TryGetProperty("hasConflict", out _));
    }

    [Fact]
    public void MacOSFileKeepsEverythingButKeyCombos()
    {
        // macOS 版匯出的檔案（沒有 platform 欄位，鍵碼是 Carbon 的）
        const string json = """
            {
              "app" : "Tatami",
              "formatVersion" : 1,
              "gridColumns" : 8,
              "gridRows" : 4,
              "isAutoUpdateEnabled" : true,
              "isSnapEnabled" : false,
              "language" : "zh-Hant",
              "showGridKeyCombo" : { "display" : "⌃⌥G", "keyCode" : 5, "modifiers" : 6144 },
              "shortcuts" : [
                {
                  "grid" : { "columns" : 6, "rows" : 6 },
                  "id" : "E621E1F8-C36C-495A-93FC-0C247A3E6E5F",
                  "keyCombo" : { "display" : "⌃⌥←", "keyCode" : 123, "modifiers" : 6144 },
                  "name" : "左半邊",
                  "selection" : { "column" : 0, "columns" : 3, "row" : 0, "rows" : 6 }
                }
              ]
            }
            """;
        var backup = SettingsBackup.FromJson(json);
        Assert.Null(backup.Platform);
        Assert.True(backup.IsFromOtherPlatform);
        Assert.Equal("zh-Hant", backup.Language);
        Assert.Equal(8, backup.GridColumns);

        var shortcuts = backup.ShortcutsForThisPlatform();
        Assert.NotNull(shortcuts);
        var shortcut = Assert.Single(shortcuts);
        Assert.Equal("左半邊", shortcut.Name);
        Assert.Equal(Guid.Parse("E621E1F8-C36C-495A-93FC-0C247A3E6E5F"), shortcut.Id);
        Assert.Equal(new GridSelection(0, 0, 3, 6), shortcut.Selection);
        Assert.Null(shortcut.KeyCombo);
    }

    [Fact]
    public void WindowsFileKeepsKeyCombos()
    {
        var shortcut = WindowShortcut.New("Left", new Grid(6, 6));
        shortcut.KeyCombo = new KeyCombo(0x25, KeyModifiers.Control | KeyModifiers.Alt, "Ctrl+Alt+←");
        var backup = SettingsBackup.FromJson(new SettingsBackup { Platform = "windows", Shortcuts = [shortcut] }.ToJson());
        Assert.False(backup.IsFromOtherPlatform);
        Assert.Equal(shortcut.KeyCombo, backup.ShortcutsForThisPlatform()?.Single().KeyCombo);
    }

    [Fact]
    public void ShortcutWithoutRequiredFieldsIsRejected()
    {
        // 和 Swift 一樣：快捷鍵少了 grid 就不是有效的檔案
        const string json = """{"app": "Tatami", "formatVersion": 1, "shortcuts": [{"id": "E621E1F8-C36C-495A-93FC-0C247A3E6E5F", "name": "x"}]}""";
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.FromJson(json));
    }

    private static void AssertSameShortcuts(List<WindowShortcut>? expected, List<WindowShortcut>? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Id, actual[i].Id);
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].KeyCombo, actual[i].KeyCombo);
            Assert.Equal(expected[i].Grid, actual[i].Grid);
            Assert.Equal(expected[i].Selection, actual[i].Selection);
        }
    }
}
