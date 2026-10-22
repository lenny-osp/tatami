using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tatami.Core;

/// <summary>
/// 使用者自訂的快捷鍵：按下後把目前的視窗搬到指定區塊。
/// 設定畫面直接編輯這個物件，所以會通知屬性變動（INotifyPropertyChanged 不屬於 WPF）。
/// </summary>
public sealed class WindowShortcut : INotifyPropertyChanged
{
    private string name = "";
    private KeyCombo? keyCombo;
    private GridSelection selection = new(0, 0, 1, 1);
    private bool hasConflict;

    [JsonConstructor]
    public WindowShortcut() { }

    [SetsRequiredMembers]
    public WindowShortcut(Guid id, string name, KeyCombo? keyCombo, Grid grid, GridSelection selection)
    {
        Id = id;
        Name = name;
        KeyCombo = keyCombo;
        Grid = grid;
        Selection = selection;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonConverter(typeof(UppercaseGuidConverter))]
    public required Guid Id { get; init; }

    public required string Name
    {
        get => name;
        set => Set(ref name, value);
    }

    public KeyCombo? KeyCombo
    {
        get => keyCombo;
        set => Set(ref keyCombo, value);
    }

    /// <summary>建立時的格線。之後改了格線設定，已經建立的快捷鍵位置也不會跑掉。</summary>
    public required Grid Grid { get; init; }

    public required GridSelection Selection
    {
        get => selection;
        set => Set(ref selection, value);
    }

    /// <summary>快捷鍵無法註冊（已被其他程式使用）。只用在畫面上，不存檔。</summary>
    [JsonIgnore]
    public bool HasConflict
    {
        get => hasConflict;
        set => Set(ref hasConflict, value);
    }

    /// <summary>新快捷鍵：預設選取整個畫面。</summary>
    public static WindowShortcut New(string name, Grid grid) =>
        new(Guid.NewGuid(), name, null, grid, new GridSelection(0, 0, grid.Columns, grid.Rows));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}

/// <summary>Swift 的 UUID 編碼成大寫字串，這裡也寫成大寫，兩邊的檔案看起來一樣。讀取時大小寫都接受。</summary>
public sealed class UppercaseGuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Guid.Parse(reader.GetString() ?? throw new JsonException("id is null"));

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("D").ToUpperInvariant());
}
