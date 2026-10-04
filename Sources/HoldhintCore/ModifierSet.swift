import Foundation

/// Command, Option, Control, and Shift. Caps Lock and the Fn key are ignored.
public struct ModifierSet: OptionSet, Hashable, Sendable {
    public let rawValue: Int

    public init(rawValue: Int) {
        self.rawValue = rawValue
    }

    public static let command = ModifierSet(rawValue: 1 << 0)
    public static let option = ModifierSet(rawValue: 1 << 1)
    public static let control = ModifierSet(rawValue: 1 << 2)
    public static let shift = ModifierSet(rawValue: 1 << 3)

    /// Apple’s usual order: Control, Option, Shift, Command.
    public var symbolList: [String] {
        var parts: [String] = []
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
            default:
                return nil
            }
        }
        if set.isEmpty { return nil }
        self = set
    }
}
