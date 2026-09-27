import AppKit
import Carbon.HIToolbox
import Testing
@testable import Tatami

struct KeyComboTests {
    private func keyEvent(_ keyCode: Int, _ characters: String, _ flags: NSEvent.ModifierFlags) -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: flags, timestamp: 0,
            windowNumber: 0, context: nil, characters: characters,
            charactersIgnoringModifiers: characters, isARepeat: false, keyCode: UInt16(keyCode)
        )!
    }

    @Test func plainLetterIsNotAllowed() {
        // 沒有 ⌃⌥⌘ 的組合會搶走一般打字用的按鍵
        #expect(KeyCombo(event: keyEvent(kVK_ANSI_W, "w", [])) == nil)
        #expect(KeyCombo(event: keyEvent(kVK_ANSI_W, "W", [.shift])) == nil)
    }

    @Test func modifiersAreDisplayedInStandardOrder() throws {
        let combo = try #require(KeyCombo(event: keyEvent(kVK_ANSI_W, "w", [.command, .shift, .option, .control])))
        #expect(combo.display == "⌃⌥⇧⌘W")
        #expect(combo.keyCode == UInt32(kVK_ANSI_W))
        #expect(combo.modifiers == UInt32(controlKey | optionKey | shiftKey | cmdKey))
    }

    @Test func arrowKeysUseSymbols() throws {
        let combo = try #require(KeyCombo(event: keyEvent(kVK_LeftArrow, "", [.control, .option])))
        #expect(combo.display == "⌃⌥←")
    }

    @Test func functionKeysWorkWithoutModifiers() throws {
        let combo = try #require(KeyCombo(event: keyEvent(kVK_F5, "", [])))
        #expect(combo.display == "F5")
    }
}
