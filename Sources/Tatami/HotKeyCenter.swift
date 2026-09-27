import Carbon.HIToolbox

/// 註冊全域快捷鍵。使用 Carbon 的 RegisterEventHotKey：不需要額外權限，在任何 App 裡都能觸發。
@MainActor
final class HotKeyCenter {
    private var hotKeys: [EventHotKeyRef] = []
    private var actions: [UInt32: () -> Void] = [:]
    private var nextID: UInt32 = 0
    private var handler: EventHandlerRef?

    init() {
        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )
        // C 回呼不能捕捉變數，所以把 self 當成 userData 傳進去
        InstallEventHandler(
            GetApplicationEventTarget(),
            { _, event, userData in
                guard let event, let userData else { return OSStatus(eventNotHandledErr) }
                var hotKeyID = EventHotKeyID()
                GetEventParameter(
                    event,
                    EventParamName(kEventParamDirectObject),
                    EventParamType(typeEventHotKeyID),
                    nil,
                    MemoryLayout<EventHotKeyID>.size,
                    nil,
                    &hotKeyID
                )
                let center = Unmanaged<HotKeyCenter>.fromOpaque(userData).takeUnretainedValue()
                MainActor.assumeIsolated { center.fire(hotKeyID.id) }
                return noErr
            },
            1,
            &eventType,
            Unmanaged.passUnretained(self).toOpaque(),
            &handler
        )
    }

    /// 註冊一組快捷鍵。組合已被其他程式（或自己）用掉時回傳 false。
    @discardableResult
    func register(_ combo: KeyCombo, action: @escaping () -> Void) -> Bool {
        nextID += 1
        let id = EventHotKeyID(signature: OSType(0x5441_544D), id: nextID) // 'TATM'
        var ref: EventHotKeyRef?
        let status = RegisterEventHotKey(combo.keyCode, combo.modifiers, id, GetApplicationEventTarget(), 0, &ref)
        guard status == noErr, let ref else { return false }
        hotKeys.append(ref)
        actions[nextID] = action
        return true
    }

    func unregisterAll() {
        hotKeys.forEach { UnregisterEventHotKey($0) }
        hotKeys = []
        actions = [:]
    }

    private func fire(_ id: UInt32) {
        actions[id]?()
    }
}
