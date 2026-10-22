using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tatami.Core;
using Tatami.Win32;
using static Tatami.Win32.NativeMethods;

namespace Tatami;

/// <summary>錄製快捷鍵的按鈕。KeyCombo 可以雙向綁定。</summary>
public partial class ShortcutRecorder : UserControl
{
    public static readonly DependencyProperty KeyComboProperty = DependencyProperty.Register(
        nameof(KeyCombo), typeof(KeyCombo), typeof(ShortcutRecorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((ShortcutRecorder)d).UpdateLabel()));

    /// <summary>同一時間只有一個錄製中的按鈕</summary>
    private static ShortcutRecorder? active;

    private bool isRecording;

    public ShortcutRecorder()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            UpdateLabel();
        };
        Unloaded += (_, _) =>
        {
            Loc.Instance.LanguageChanged -= OnLanguageChanged;
            Stop();
        };
        UpdateLabel();
    }

    public KeyCombo? KeyCombo
    {
        get => (KeyCombo?)GetValue(KeyComboProperty);
        set => SetValue(KeyComboProperty, value);
    }

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (isRecording) Stop(); else Start();
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => KeyCombo = null;

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Stop();

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLabel();

    private void Start()
    {
        if (isRecording) return;
        active?.Stop();
        active = this;
        isRecording = true;
        // 錄製時暫停所有全域快捷鍵，否則按下已存在的組合會直接觸發，錄不到
        App.Settings.IsRecordingShortcut = true;
        RecordButton.Focus();
        UpdateLabel();
    }

    private void Stop()
    {
        if (!isRecording) return;
        isRecording = false;
        if (active == this) active = null;
        App.Settings.IsRecordingShortcut = false;
        UpdateLabel();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!isRecording) return;
        // 錄製中攔下所有按鍵（包括 Tab、Space、Alt），不讓它們操作介面
        e.Handled = true;

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint modifiers = Modifiers(Keyboard.Modifiers);
        if (KeyCombo.IsModifierKey(virtualKey)) return; // 只按下修飾鍵：繼續等

        bool hasModifiers = modifiers != 0;
        if (key == Key.Escape && !hasModifiers)
        {
            Stop(); // 取消
            return;
        }
        if (key is Key.Back or Key.Delete && !hasModifiers)
        {
            KeyCombo = null; // 清除
            Stop();
            return;
        }

        if (Core.KeyCombo.Create(virtualKey, modifiers, KeyText(virtualKey)) is { } combo)
        {
            KeyCombo = combo;
            Stop();
        }
        else
        {
            WindowMover.Beep(); // 需要搭配 Ctrl、Alt 或 Win
        }
    }

    private void UpdateLabel()
    {
        bool showsCombo = !isRecording && KeyCombo is not null;
        Label.Text = isRecording
            ? Loc.L("recorder.prompt")
            : KeyCombo?.Display ?? Loc.L("recorder.record");
        // 按鍵組合（Ctrl+Alt+←）一律由左到右顯示，阿拉伯文介面也不會被重新排序；其他文字跟隨介面方向
        if (showsCombo) Label.FlowDirection = FlowDirection.LeftToRight;
        else Label.ClearValue(FlowDirectionProperty);
        ClearButton.Visibility = KeyCombo is not null && !isRecording ? Visibility.Visible : Visibility.Collapsed;
    }

    private static uint Modifiers(ModifierKeys keys)
    {
        uint result = 0;
        if (keys.HasFlag(ModifierKeys.Control)) result |= KeyModifiers.Control;
        if (keys.HasFlag(ModifierKeys.Alt)) result |= KeyModifiers.Alt;
        if (keys.HasFlag(ModifierKeys.Shift)) result |= KeyModifiers.Shift;
        if (keys.HasFlag(ModifierKeys.Windows)) result |= KeyModifiers.Win;
        return result;
    }

    /// <summary>依照目前的鍵盤配置，這個鍵會打出的字（標點符號鍵的顯示名稱）。</summary>
    private static string? KeyText(uint virtualKey)
    {
        uint character = MapVirtualKey(virtualKey, MAPVK_VK_TO_CHAR) & 0x7FFF;
        return character > 0x20 ? ((char)character).ToString() : null;
    }
}
