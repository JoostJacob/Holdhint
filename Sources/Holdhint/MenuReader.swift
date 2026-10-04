import AppKit
import ApplicationServices
import HoldhintCore

enum MenuReader {
    /// Reads keyboard shortcuts from another app’s menu bar. Returns an empty list
    /// when Accessibility permission is missing or the app does not answer in time.
    static func shortcuts(pid: pid_t) -> [Shortcut] {
        guard pid > 0, AXIsProcessTrusted() else { return [] }
        let system = AXUIElementCreateSystemWide()
        AXUIElementSetMessagingTimeout(system, 0.4)
        defer { AXUIElementSetMessagingTimeout(system, 0) }

        let application = AXUIElementCreateApplication(pid)
        guard let menuBar = element(application, kAXMenuBarAttribute) else { return [] }

        var found: [Shortcut] = []
        var seen = Set<String>()
        var visited = 0
        walk(menuBar, into: &found, seen: &seen, visited: &visited, depth: 0)
        return found
    }

    private static func walk(
        _ element: AXUIElement,
        into found: inout [Shortcut],
        seen: inout Set<String>,
        visited: inout Int,
        depth: Int
    ) {
        if depth > 8 || visited > 700 || found.count > 400 { return }
        visited += 1
        if let shortcut = shortcut(from: element) {
            let identity = "\(shortcut.modifiers.rawValue)|\(shortcut.key)"
            if !seen.contains(identity) {
                seen.insert(identity)
                found.append(shortcut)
            }
        }
        for child in children(of: element) {
            walk(child, into: &found, seen: &seen, visited: &visited, depth: depth + 1)
        }
    }

    private static func shortcut(from element: AXUIElement) -> Shortcut? {
        let role = string(element, kAXRoleAttribute) ?? ""
        guard role == (kAXMenuItemRole as String) else { return nil }
        let title = (string(element, kAXTitleAttribute) ?? "")
            .replacingOccurrences(of: "\n", with: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        guard !title.isEmpty else { return nil }

        let command = (string(element, kAXMenuItemCmdCharAttribute) ?? "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let virtual = number(element, kAXMenuItemCmdVirtualKeyAttribute)
        let key: String
        if command.count == 1 {
            key = KeyDisplay.normalize(command)
        } else if let virtual, let token = token(forVirtualKey: virtual) {
            key = token
        } else {
            return nil
        }
        guard !key.isEmpty else { return nil }

        let modifiers: ModifierSet
        if let bits = number(element, kAXMenuItemCmdModifiersAttribute) {
            modifiers = ModifierSet(axMenuBits: bits)
        } else {
            modifiers = .command
        }
        guard !modifiers.isEmpty else { return nil }
        let clipped = title.count > 120 ? String(title.prefix(117)) + "…" : title
        return Shortcut(modifiers: modifiers, key: key, title: clipped)
    }

    private static func element(_ parent: AXUIElement, _ attribute: String) -> AXUIElement? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(parent, attribute as CFString, &value) == .success,
              let value else { return nil }
        return (value as! AXUIElement)
    }

    private static func children(of element: AXUIElement) -> [AXUIElement] {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, kAXChildrenAttribute as CFString, &value) == .success,
              let value else { return [] }
        return (value as? [AXUIElement]) ?? []
    }

    private static func string(_ element: AXUIElement, _ attribute: String) -> String? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, attribute as CFString, &value) == .success else { return nil }
        return value as? String
    }

    private static func number(_ element: AXUIElement, _ attribute: String) -> Int? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, attribute as CFString, &value) == .success else { return nil }
        return (value as? NSNumber)?.intValue
    }

    /// US virtual key codes from HIToolbox/Events.h. Menu items usually set a
    /// command character instead, and that path is preferred.
    private static func token(forVirtualKey code: Int) -> String? {
        let ansi: [Int: String] = [
            0x00: "a", 0x01: "s", 0x02: "d", 0x03: "f", 0x04: "h", 0x05: "g",
            0x06: "z", 0x07: "x", 0x08: "c", 0x09: "v", 0x0B: "b", 0x0C: "q",
            0x0D: "w", 0x0E: "e", 0x0F: "r", 0x10: "y", 0x11: "t", 0x12: "1",
            0x13: "2", 0x14: "3", 0x15: "4", 0x16: "6", 0x17: "5", 0x18: "=",
            0x19: "9", 0x1A: "7", 0x1B: "-", 0x1C: "8", 0x1D: "0", 0x1E: "]",
            0x1F: "o", 0x20: "u", 0x21: "[", 0x22: "i", 0x23: "p", 0x25: "l",
            0x26: "j", 0x27: "'", 0x28: "k", 0x29: ";", 0x2A: "\\", 0x2B: ",",
            0x2C: "/", 0x2D: "n", 0x2E: "m", 0x2F: ".", 0x32: "backtick"
        ]
        if let letter = ansi[code] { return letter }
        switch code {
        case 0x24, 0x4C: return "return"
        case 0x30: return "tab"
        case 0x31: return "space"
        case 0x33: return "delete"
        case 0x35: return "escape"
        case 0x7B: return "left"
        case 0x7C: return "right"
        case 0x7D: return "down"
        case 0x7E: return "up"
        case 0x7A: return "f1"
        case 0x78: return "f2"
        case 0x63: return "f3"
        case 0x76: return "f4"
        case 0x60: return "f5"
        case 0x61: return "f6"
        case 0x62: return "f7"
        case 0x64: return "f8"
        case 0x65: return "f9"
        case 0x6D: return "f10"
        case 0x67: return "f11"
        case 0x6F: return "f12"
        default: return nil
        }
    }
}

extension ModifierSet {
    /// Menu-item modifier bits. Command is present unless the “no command” bit is set.
    init(axMenuBits bits: Int) {
        var set = ModifierSet()
        if bits & (1 << 3) == 0 { set.insert(.command) }
        if bits & (1 << 0) != 0 { set.insert(.shift) }
        if bits & (1 << 1) != 0 { set.insert(.option) }
        if bits & (1 << 2) != 0 { set.insert(.control) }
        self = set
    }
}
