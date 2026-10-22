using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Tatami.Core;

/// <summary>Win32 RegisterHotKey 用的修飾鍵旗標（MOD_*）。</summary>
public static class KeyModifiers
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;
    public const uint Shift = 0x0004;
    public const uint Win = 0x0008;
    /// <summary>按住不放時不要重複觸發。只在註冊時加上，不存進設定。</summary>
    public const uint NoRepeat = 0x4000;

    public const uint All = Alt | Control | Shift | Win;
}

/// <summary>
/// 一組快捷鍵：虛擬鍵碼（VK_*）+ 修飾鍵（MOD_*，給 RegisterHotKey 用）+ 顯示用文字。
/// JSON 欄位名稱和 macOS 版相同，但鍵碼是 Windows 的虛擬鍵碼，不能跨平台使用。
/// </summary>
public sealed record KeyCombo
{
    [JsonConstructor]
    public KeyCombo() { }

    [SetsRequiredMembers]
    public KeyCombo(uint keyCode, uint modifiers, string display)
    {
        KeyCode = keyCode;
        Modifiers = modifiers;
        Display = display;
    }

    public required uint KeyCode { get; init; }
    public required uint Modifiers { get; init; }
    public required string Display { get; init; }

    /// <summary>
    /// 從按下的按鍵建立。沒有按 Ctrl、Alt、Win 任何一個（F1–F12 除外）時回傳 null，
    /// 避免搶走一般打字用的按鍵；只按 Shift 也不算。
    /// </summary>
    /// <param name="virtualKey">虛擬鍵碼（VK_*）</param>
    /// <param name="modifiers">KeyModifiers 旗標</param>
    /// <param name="keyText">依照目前鍵盤配置，這個鍵會打出的字（標點符號鍵用），沒有就是 null</param>
    public static KeyCombo? Create(uint virtualKey, uint modifiers, string? keyText = null)
    {
        if (IsModifierKey(virtualKey)) return null;
        modifiers &= KeyModifiers.All;
        bool hasRequiredModifier = (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Win)) != 0;
        if (!hasRequiredModifier && !IsFunctionKey(virtualKey)) return null;

        // 顯示順序固定：Ctrl+Alt+Shift+Win+鍵
        var display = "";
        if ((modifiers & KeyModifiers.Control) != 0) display += "Ctrl+";
        if ((modifiers & KeyModifiers.Alt) != 0) display += "Alt+";
        if ((modifiers & KeyModifiers.Shift) != 0) display += "Shift+";
        if ((modifiers & KeyModifiers.Win) != 0) display += "Win+";

        return new KeyCombo(virtualKey, modifiers, display + KeyName(virtualKey, keyText));
    }

    /// <summary>F1–F12 可以不搭配修飾鍵。</summary>
    public static bool IsFunctionKey(uint virtualKey) => virtualKey is >= 0x70 and <= 0x7B;

    /// <summary>修飾鍵本身（只按下 Ctrl 等）不能當成快捷鍵的主鍵。</summary>
    public static bool IsModifierKey(uint virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 // Shift、Ctrl、Alt
        or 0x5B or 0x5C // 左右 Win
        or (>= 0xA0 and <= 0xA5); // 左右 Shift、Ctrl、Alt

    /// <summary>按鍵的顯示名稱。字母、數字固定；標點符號鍵依照鍵盤配置，由呼叫端傳入。</summary>
    public static string KeyName(uint virtualKey, string? keyText = null)
    {
        if (virtualKey is (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A)) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x70 and <= 0x87) return $"F{virtualKey - 0x70 + 1}";
        if (virtualKey is >= 0x60 and <= 0x69) return $"Num {virtualKey - 0x60}";
        if (SpecialKeyNames.TryGetValue(virtualKey, out var name)) return name;
        if (!string.IsNullOrWhiteSpace(keyText)) return keyText.Trim().ToUpperInvariant();
        return "?";
    }

    private static readonly Dictionary<uint, string> SpecialKeyNames = new()
    {
        [0x25] = "←", [0x27] = "→", [0x26] = "↑", [0x28] = "↓",
        [0x0D] = "Enter", [0x09] = "Tab", [0x20] = "Space", [0x08] = "Backspace", [0x2E] = "Delete",
        [0x1B] = "Esc", [0x24] = "Home", [0x23] = "End", [0x21] = "PgUp", [0x22] = "PgDn",
        [0x2D] = "Insert", [0x2C] = "PrtScn", [0x13] = "Pause", [0x91] = "ScrLk",
        [0x6A] = "Num *", [0x6B] = "Num +", [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
    };
}
