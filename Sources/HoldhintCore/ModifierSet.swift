import Foundation

/// Fn (Globe), Command, Option, Control, and Shift. Caps Lock is ignored.
public struct ModifierSet: OptionSet, Hashable, Sendable {
    public let rawValue: Int

    public init(rawValue: Int) {
        self.rawValue = rawValue
    }

    public static let command = ModifierSet(rawValue: 1 << 0)
    public static let option = ModifierSet(rawValue: 1 << 1)
    public static let control = ModifierSet(rawValue: 1 << 2)
    public static let shift = ModifierSet(rawValue: 1 << 3)
    /// The physical fn/Globe key. Not the `.function` flag on arrow keys or F-keys.
    public static let function = ModifierSet(rawValue: 1 << 4)

    /// Globe first, then Apple’s usual order: Control, Option, Shift, Command.
    public var symbolList: [String] {
        var parts: [String] = []
        if contains(.function) { parts.append("fn") }
        if contains(.control) { parts.append("⌃") }
        if contains(.option) { parts.append("⌥") }
        if contains(.shift) { parts.append("⇧") }
        if contains(.command) { parts.append("⌘") }
        return parts
    }

    public var symbols: String {
        symbolList.joined(separator: " ")
    }

    /// Returns nil when a name is unknown or the list is empty.
    public init?(names: [String]) {
        if names.isEmpty { return nil }
        var set = ModifierSet()
        for name in names {
            switch name.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
            case "command", "cmd", "⌘":
                set.insert(.command)
            case "option", "opt", "alt", "⌥":
                set.insert(.option)
            case "control", "ctrl", "ctl", "⌃":
                set.insert(.control)
            case "shift", "⇧":
                set.insert(.shift)
            case "fn", "function", "globe", "🌐":
                set.insert(.function)
            default:
                return nil
            }
        }
        if set.isEmpty { return nil }
        self = set
    }
}

/// Which modifiers are held, without treating arrow keys or F-keys as Fn.
///
/// `NSEvent.ModifierFlags.function` is set for the fn/Globe key and also for
/// arrow keys and F-keys. A flagsChanged event is the physical fn/Globe key
/// only when its key code is 63 (`kVK_Function`). Other events leave the
/// tracked fn state as it was.
public struct ModifierTracker: Equatable, Sendable {
    public static let functionKeyCode: UInt16 = 63

    public private(set) var functionKeyDown = false

    public init() {}

    public mutating func flagsChanged(
        keyCode: UInt16,
        command: Bool,
        option: Bool,
        control: Bool,
        shift: Bool,
        functionFlag: Bool
    ) -> ModifierSet {
        if keyCode == Self.functionKeyCode {
            functionKeyDown = functionFlag
        }
        var set = ModifierSet()
        if command { set.insert(.command) }
        if option { set.insert(.option) }
        if control { set.insert(.control) }
        if shift { set.insert(.shift) }
        if functionKeyDown { set.insert(.function) }
        return set
    }
}
