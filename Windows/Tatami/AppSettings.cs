using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Tatami.Core;
using Tatami.Win32;

namespace Tatami;

/// <summary>
/// 所有使用者設定，改動時同步套用到實際功能，並存到 %AppData%\Tatami\settings.json。
/// 檔案格式和匯出的備份檔相同（SettingsBackup）；「登入時啟動」不存檔，每次從登錄檔讀取。
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private readonly SnapController snapController;
    private readonly string path;
    private bool isLoading;

    private AppLanguage language = AppLanguage.English;
    private bool isSnapEnabled = true;
    private bool isLaunchAtLoginEnabled;
    private bool isAutoUpdateEnabled = true;
    private int gridColumns = 6;
    private int gridRows = 6;
    private KeyCombo? showGridKeyCombo;
    private KeyCombo? nextScreenKeyCombo;
    private bool isShowGridConflicting;
    private bool isNextScreenConflicting;
    private bool isRecordingShortcut;

    public AppSettings(SnapController snapController, string path)
    {
        this.snapController = snapController;
        this.path = path;
        Shortcuts.CollectionChanged += OnShortcutsCollectionChanged;
        Load();
        isLaunchAtLoginEnabled = SafeIsLaunchAtLoginEnabled();
        if (isSnapEnabled) snapController.Start();
        isSnapEnabled = snapController.IsRunning;
    }

    /// <summary>%AppData%\Tatami\settings.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tatami", "settings.json");

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>快捷鍵設定變動時通知 ShortcutController 重新註冊</summary>
    public event Action? ShortcutsChanged;

    public static IReadOnlyList<AppLanguage> Languages => AppLanguage.All;

    /// <summary>介面語言，預設英文</summary>
    public AppLanguage Language
    {
        get => language;
        set
        {
            if (value is null || !Set(ref language, value)) return;
            Loc.Instance.Language = value;
            Save();
        }
    }

    public bool IsSnapEnabled
    {
        get => isSnapEnabled;
        set
        {
            if (value) snapController.Start(); else snapController.Stop();
            Set(ref isSnapEnabled, snapController.IsRunning);
            Save();
        }
    }

    /// <summary>登入時啟動（寫在登錄檔，不存進設定檔，也不在備份裡）</summary>
    public bool IsLaunchAtLoginEnabled
    {
        get => isLaunchAtLoginEnabled;
        set
        {
            try
            {
                StartupRegistry.SetEnabled(value);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException
                                                  or System.Security.SecurityException or InvalidOperationException)
            {
                Alert.Show(null, Loc.L("launchAtLogin.error"), exception.Message);
            }
            isLaunchAtLoginEnabled = SafeIsLaunchAtLoginEnabled();
            OnPropertyChanged(nameof(IsLaunchAtLoginEnabled));
        }
    }

    /// <summary>每次啟動時自動檢查更新，預設開啟</summary>
    public bool IsAutoUpdateEnabled
    {
        get => isAutoUpdateEnabled;
        set
        {
            if (Set(ref isAutoUpdateEnabled, value)) Save();
        }
    }

    public int GridColumns
    {
        get => gridColumns;
        set
        {
            if (Set(ref gridColumns, Grid.ClampSize(value))) Save();
        }
    }

    public int GridRows
    {
        get => gridRows;
        set
        {
            if (Set(ref gridRows, Grid.ClampSize(value))) Save();
        }
    }

    /// <summary>格線面板目前要用的格線</summary>
    public Grid Grid => new(gridColumns, gridRows);

    public ObservableCollection<WindowShortcut> Shortcuts { get; } = [];

    /// <summary>「顯示格線」快捷鍵，null 表示不啟用</summary>
    public KeyCombo? ShowGridKeyCombo
    {
        get => showGridKeyCombo;
        set
        {
            if (!Set(ref showGridKeyCombo, value)) return;
            Save();
            ShortcutsChanged?.Invoke();
        }
    }

    /// <summary>「搬到下一個螢幕」快捷鍵，null 表示不啟用</summary>
    public KeyCombo? NextScreenKeyCombo
    {
        get => nextScreenKeyCombo;
        set
        {
            if (!Set(ref nextScreenKeyCombo, value)) return;
            Save();
            ShortcutsChanged?.Invoke();
        }
    }

    /// <summary>無法註冊（已被其他程式使用）的固定功能快捷鍵</summary>
    public bool IsShowGridConflicting
    {
        get => isShowGridConflicting;
        set => Set(ref isShowGridConflicting, value);
    }

    public bool IsNextScreenConflicting
    {
        get => isNextScreenConflicting;
        set => Set(ref isNextScreenConflicting, value);
    }

    /// <summary>正在錄製快捷鍵（這段期間要暫停所有全域快捷鍵）</summary>
    public bool IsRecordingShortcut
    {
        get => isRecordingShortcut;
        set
        {
            if (Set(ref isRecordingShortcut, value)) ShortcutsChanged?.Invoke();
        }
    }

    public void AddShortcut() => Shortcuts.Add(WindowShortcut.New(Loc.L("shortcut.defaultName"), Grid));

    public void DeleteShortcut(WindowShortcut shortcut) => Shortcuts.Remove(shortcut);

    /// <summary>使用者可能在工作管理員裡改過「啟動應用程式」，打開設定畫面時重新讀取。</summary>
    public void Refresh()
    {
        isLaunchAtLoginEnabled = SafeIsLaunchAtLoginEnabled();
        OnPropertyChanged(nameof(IsLaunchAtLoginEnabled));
    }

    // MARK: - 匯出／匯入

    public SettingsBackup MakeBackup() => new()
    {
        Platform = SettingsBackup.PlatformWindows,
        Language = language.Code,
        IsSnapEnabled = isSnapEnabled,
        IsAutoUpdateEnabled = isAutoUpdateEnabled,
        GridColumns = gridColumns,
        GridRows = gridRows,
        ShowGridKeyCombo = showGridKeyCombo,
        NextScreenKeyCombo = nextScreenKeyCombo,
        Shortcuts = Shortcuts.ToList(),
    };

    /// <summary>
    /// 套用匯入的設定。檔案裡沒有的欄位保留目前的值；格線大小會限制在允許範圍內。
    /// 另一個平台（macOS）的檔案不套用按鍵組合，其他設定照常匯入。
    /// </summary>
    public void Apply(SettingsBackup backup)
    {
        if (AppLanguage.FromCode(backup.Language) is { } newLanguage) Language = newLanguage;
        if (backup.IsSnapEnabled is { } snap) IsSnapEnabled = snap;
        if (backup.IsAutoUpdateEnabled is { } autoUpdate) IsAutoUpdateEnabled = autoUpdate;
        if (backup.GridColumns is { } columns) GridColumns = columns;
        if (backup.GridRows is { } rows) GridRows = rows;
        if (!backup.IsFromOtherPlatform)
        {
            // 快捷鍵沒設定時匯出檔裡不會有這個欄位，所以直接套用（null 代表清除）
            ShowGridKeyCombo = backup.ShowGridKeyCombo;
            NextScreenKeyCombo = backup.NextScreenKeyCombo;
        }
        if (backup.ShortcutsForThisPlatform() is { } shortcuts) ReplaceShortcuts(shortcuts);
        Save();
    }

    // MARK: - 存檔

    private void Load()
    {
        SettingsBackup? stored = null;
        try
        {
            if (File.Exists(path)) stored = SettingsBackup.FromJson(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // 檔案壞掉時用預設值，下次存檔會覆蓋
            System.Diagnostics.Debug.WriteLine($"Tatami: cannot read {path}: {exception.Message}");
        }

        isLoading = true;
        try
        {
            if (stored is null) return;
            language = AppLanguage.FromCode(stored.Language) ?? AppLanguage.English;
            isSnapEnabled = stored.IsSnapEnabled ?? true;
            isAutoUpdateEnabled = stored.IsAutoUpdateEnabled ?? true;
            gridColumns = Grid.ClampSize(stored.GridColumns ?? 6);
            gridRows = Grid.ClampSize(stored.GridRows ?? 6);
            if (!stored.IsFromOtherPlatform)
            {
                showGridKeyCombo = stored.ShowGridKeyCombo;
                nextScreenKeyCombo = stored.NextScreenKeyCombo;
            }
            ReplaceShortcuts(stored.ShortcutsForThisPlatform() ?? []);
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>先寫到暫存檔再取代，寫到一半當機也不會留下壞掉的設定檔。</summary>
    private void Save()
    {
        if (isLoading) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, MakeBackup().ToJson());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Tatami: cannot save {path}: {exception.Message}");
        }
    }

    private void ReplaceShortcuts(IEnumerable<WindowShortcut> shortcuts)
    {
        bool wasLoading = isLoading;
        isLoading = true;
        try
        {
            Shortcuts.Clear();
            foreach (var shortcut in shortcuts) Shortcuts.Add(shortcut);
        }
        finally
        {
            isLoading = wasLoading;
        }
        Save();
        ShortcutsChanged?.Invoke();
    }

    private void OnShortcutsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (WindowShortcut item in e.OldItems ?? Array.Empty<WindowShortcut>()) item.PropertyChanged -= OnShortcutChanged;
        foreach (WindowShortcut item in e.NewItems ?? Array.Empty<WindowShortcut>()) item.PropertyChanged += OnShortcutChanged;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in Shortcuts)
            {
                item.PropertyChanged -= OnShortcutChanged;
                item.PropertyChanged += OnShortcutChanged;
            }
        }
        if (isLoading) return;
        Save();
        ShortcutsChanged?.Invoke();
    }

    private void OnShortcutChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 衝突狀態是 ShortcutController 設的，不用存檔
        if (e.PropertyName == nameof(WindowShortcut.HasConflict)) return;
        Save();
        if (e.PropertyName == nameof(WindowShortcut.KeyCombo)) ShortcutsChanged?.Invoke();
    }

    private static bool SafeIsLaunchAtLoginEnabled()
    {
        try
        {
            return StartupRegistry.IsEnabled;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(property);
        return true;
    }

    private void OnPropertyChanged(string? property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
