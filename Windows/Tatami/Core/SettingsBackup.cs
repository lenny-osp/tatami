using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tatami.Core;

/// <summary>
/// 匯出／匯入設定的檔案格式（JSON），和 macOS 版相同，多了 platform 欄位。
/// Windows 版的 settings.json 也用這個格式存。
///
/// 除了 app 和 formatVersion，其他欄位都是 optional：
/// 匯入其他版本的檔案時，少了的欄位就保留目前的設定。
/// 「登入時啟動」是這台電腦的系統設定，不包含在內。
/// </summary>
public sealed class SettingsBackup
{
    public const string AppIdentifier = "Tatami";
    public const int CurrentFormatVersion = 1;
    public const string PlatformMacOS = "macos";
    public const string PlatformWindows = "windows";

    [JsonRequired]
    public string App { get; set; } = AppIdentifier;

    [JsonRequired]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// 寫入檔案的平台（Windows 版一律寫 windows）。檔案裡沒有這個欄位時是 null，
    /// 代表 macOS 的舊檔案（Windows 版出現之前就有了）。
    /// </summary>
    public string? Platform { get; set; }

    public string? Language { get; set; }
    public bool? IsSnapEnabled { get; set; }
    public bool? IsAutoUpdateEnabled { get; set; }
    public int? GridColumns { get; set; }
    public int? GridRows { get; set; }
    public KeyCombo? ShowGridKeyCombo { get; set; }
    public KeyCombo? NextScreenKeyCombo { get; set; }
    public List<WindowShortcut>? Shortcuts { get; set; }

    /// <summary>
    /// 檔案來自另一個平台。快捷鍵的鍵碼和硬體有關（Carbon 鍵碼 ≠ 虛擬鍵碼），所以不能沿用。
    /// </summary>
    [JsonIgnore]
    public bool IsFromOtherPlatform => Platform != PlatformWindows;

    /// <summary>
    /// 要套用的自訂快捷鍵：另一個平台的檔案保留名稱和區塊，但拿掉按鍵組合。
    /// 檔案裡沒有這個欄位時是 null（保留目前設定）。
    /// </summary>
    public List<WindowShortcut>? ShortcutsForThisPlatform()
    {
        if (Shortcuts is null || !IsFromOtherPlatform) return Shortcuts;
        return Shortcuts
            .Select(s => new WindowShortcut(s.Id, s.Name, null, s.Grid, s.Selection))
            .ToList();
    }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 排版整齊，使用者也能用文字編輯器看懂；中文名稱不要變成 \uXXXX
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>讀取設定檔。不是 Tatami 的設定檔或內容損壞時丟出 JsonException。</summary>
    public static SettingsBackup FromJson(string json)
    {
        var backup = JsonSerializer.Deserialize<SettingsBackup>(json, JsonOptions)
            ?? throw new JsonException("Empty settings file");
        if (backup.App != AppIdentifier) throw new JsonException("Not a Tatami settings file");
        return backup;
    }
}
