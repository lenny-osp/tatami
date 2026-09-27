import AppKit
import Carbon.HIToolbox

/// 一組快捷鍵：按鍵代碼 + 修飾鍵（Carbon 格式，給 RegisterEventHotKey 用）+ 顯示用文字。
struct KeyCombo: Codable, Equatable {
    var keyCode: UInt32
    var modifiers: UInt32
    var display: String
}

extension KeyCombo {
    /// 從鍵盤事件建立。沒有按 ⌃⌥⌘ 任何一個（F1–F12 除外）時回傳 nil，避免搶走一般打字用的按鍵。
    init?(event: NSEvent) {
        let flags = event.modifierFlags.intersection([.control, .option, .shift, .command])
        let isFunctionKey = Self.functionKeyNames[event.keyCode] != nil
        guard isFunctionKey || !flags.isDisjoint(with: [.control, .option, .command]) else { return nil }

        var modifiers: UInt32 = 0
        var display = ""
        if flags.contains(.control) { modifiers |= UInt32(controlKey); display += "⌃" }
        if flags.contains(.option) { modifiers |= UInt32(optionKey); display += "⌥" }
        if flags.contains(.shift) { modifiers |= UInt32(shiftKey); display += "⇧" }
        if flags.contains(.command) { modifiers |= UInt32(cmdKey); display += "⌘" }

        let keyName = Self.functionKeyNames[event.keyCode]
            ?? Self.specialKeyNames[event.keyCode]
            ?? event.charactersIgnoringModifiers?.uppercased()
            ?? "?"

        self.init(keyCode: UInt32(event.keyCode), modifiers: modifiers, display: display + keyName)
    }

    private static let specialKeyNames: [UInt16: String] = [
        123: "←", 124: "→", 125: "↓", 126: "↑",
        36: "↩", 48: "⇥", 49: "Space", 51: "⌫", 117: "⌦", 53: "⎋",
        115: "↖", 119: "↘", 116: "⇞", 121: "⇟",
    ]

    private static let functionKeyNames: [UInt16: String] = [
        122: "F1", 120: "F2", 99: "F3", 118: "F4", 96: "F5", 97: "F6",
        98: "F7", 100: "F8", 101: "F9", 109: "F10", 103: "F11", 111: "F12",
    ]
}
