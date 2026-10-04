import Foundation

public enum KeyDisplay {
    /// Canonical token used for matching. Letters are lowercase.
    public static func normalize(_ raw: String) -> String {
        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.isEmpty { return "" }
        let lower = trimmed.lowercased()
        let aliases: [String: String] = [
            "space": "space", "spacebar": "space",
            "tab": "tab",
            "return": "return", "enter": "return",
            "esc": "escape", "escape": "escape",
            "delete": "delete", "backspace": "delete",
            "up": "up", "uparrow": "up", "arrowup": "up",
            "down": "down", "downarrow": "down", "arrowdown": "down",
            "left": "left", "leftarrow": "left", "arrowleft": "left",
            "right": "right", "rightarrow": "right", "arrowright": "right",
            "backtick": "backtick", "grave": "backtick", "`": "backtick",
            "comma": ",", ",": ",",
            "period": ".", ".": ".", "dot": ".",
            "slash": "/", "/": "/",
            "backslash": "\\", "\\": "\\",
            "minus": "-", "-": "-", "hyphen": "-",
            "equal": "=", "=": "=", "equals": "=", "plus": "=",
            "leftbracket": "[", "[": "[",
            "rightbracket": "]", "]": "]",
            "semicolon": ";", ";": ";",
            "quote": "'", "'": "'", "apostrophe": "'",
            "question": "?", "?": "?"
        ]
        if let mapped = aliases[lower] { return mapped }
        if lower.count == 1 { return lower }
        if lower.first == "f", lower.dropFirst().allSatisfy(\.isNumber) {
            return lower
        }
        return lower
    }

    public static func label(for token: String) -> String {
        switch token {
        case "space": return "Space"
        case "tab": return "Tab"
        case "return": return "Return"
        case "escape": return "Esc"
        case "delete": return "Delete"
        case "up": return "↑"
        case "down": return "↓"
        case "left": return "←"
        case "right": return "→"
        case "backtick": return "`"
        default:
            if token.count == 1, let character = token.first, character.isLetter {
                return String(character).uppercased()
            }
            if token.count > 1, token.first == "f", token.dropFirst().allSatisfy(\.isNumber) {
                return token.uppercased()
            }
            return token
        }
    }
}

public enum KeySort {
    public static func less(_ lhs: Shortcut, _ rhs: Shortcut) -> Bool {
        let left = rank(lhs.key)
        let right = rank(rhs.key)
        if left != right { return left < right }
        let labelOrder = KeyDisplay.label(for: lhs.key).localizedStandardCompare(KeyDisplay.label(for: rhs.key))
        if labelOrder != .orderedSame { return labelOrder == .orderedAscending }
        return lhs.title.localizedStandardCompare(rhs.title) == .orderedAscending
    }

    private static func rank(_ key: String) -> Int {
        guard let character = key.first, key.count == 1 else { return 3 }
        if character.isLetter { return 0 }
        if character.isNumber { return 1 }
        return 2
    }
}
