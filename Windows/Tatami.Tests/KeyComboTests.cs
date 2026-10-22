using Tatami.Core;

namespace Tatami.Tests;

public class KeyComboTests
{
    private const uint VkW = 0x57;
    private const uint VkLeft = 0x25;
    private const uint VkF5 = 0x74;
    private const uint VkF13 = 0x7C;
    private const uint VkLeftControl = 0xA2;
    private const uint VkOemComma = 0xBC;

    [Fact]
    public void PlainLetterIsNotAllowed()
    {
        // 沒有 Ctrl、Alt、Win 的組合會搶走一般打字用的按鍵
        Assert.Null(KeyCombo.Create(VkW, 0));
        Assert.Null(KeyCombo.Create(VkW, KeyModifiers.Shift));
    }

    [Fact]
    public void ModifiersAreDisplayedInStandardOrder()
    {
        var combo = KeyCombo.Create(VkW, KeyModifiers.Win | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control);
        Assert.NotNull(combo);
        Assert.Equal("Ctrl+Alt+Shift+Win+W", combo.Display);
        Assert.Equal(VkW, combo.KeyCode);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Win, combo.Modifiers);
    }

    [Fact]
    public void ArrowKeysUseSymbols()
    {
        var combo = KeyCombo.Create(VkLeft, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.NotNull(combo);
        Assert.Equal("Ctrl+Alt+←", combo.Display);
    }

    [Fact]
    public void FunctionKeysWorkWithoutModifiers()
    {
        var combo = KeyCombo.Create(VkF5, 0);
        Assert.NotNull(combo);
        Assert.Equal("F5", combo.Display);
    }

    [Fact]
    public void OnlyF1ToF12MayBeBare()
    {
        Assert.Null(KeyCombo.Create(VkF13, 0));
        Assert.Equal("Ctrl+F13", KeyCombo.Create(VkF13, KeyModifiers.Control)?.Display);
    }

    [Fact]
    public void WinCountsAsAModifier()
    {
        Assert.Equal("Win+W", KeyCombo.Create(VkW, KeyModifiers.Win)?.Display);
    }

    [Fact]
    public void ModifierKeyAloneIsNotACombo()
    {
        Assert.True(KeyCombo.IsModifierKey(VkLeftControl));
        Assert.Null(KeyCombo.Create(VkLeftControl, KeyModifiers.Control));
    }

    [Fact]
    public void NoRepeatFlagIsNotStored()
    {
        var combo = KeyCombo.Create(VkW, KeyModifiers.Control | KeyModifiers.NoRepeat);
        Assert.Equal(KeyModifiers.Control, combo?.Modifiers);
    }

    [Fact]
    public void PunctuationUsesTheKeyboardLayoutText()
    {
        Assert.Equal("Ctrl+,", KeyCombo.Create(VkOemComma, KeyModifiers.Control, ",")?.Display);
        Assert.Equal("Ctrl+?", KeyCombo.Create(VkOemComma, KeyModifiers.Control)?.Display);
    }
}
