import Foundation

public struct Shortcut: Equatable, Sendable {
    public var modifiers: ModifierSet
    public var key: String
    public var title: String
    public var note: String?

    public init(modifiers: ModifierSet, key: String, title: String, note: String? = nil) {
        self.modifiers = modifiers
        self.key = key
        self.title = title
        self.note = note
    }
}

public struct Catalog: Sendable {
    public var delaySeconds: Double
    public var shortcuts: [Shortcut]
    public var warnings: [String]
    /// True when the file itself could not be read as the expected JSON object.
    public var parseFailed: Bool

    public init(delaySeconds: Double, shortcuts: [Shortcut], warnings: [String], parseFailed: Bool) {
        self.delaySeconds = delaySeconds
        self.shortcuts = shortcuts
        self.warnings = warnings
        self.parseFailed = parseFailed
    }

    public func matching(_ modifiers: ModifierSet) -> [Shortcut] {
        shortcuts
            .filter { $0.modifiers == modifiers }
            .sorted(by: KeySort.less)
    }

    /// Front-app rows replace bundled rows that use the same key.
    public static func merge(system: [Shortcut], frontApp: [Shortcut]) -> [Shortcut] {
        var byKey: [String: Shortcut] = [:]
        var order: [String] = []
        func insert(_ shortcut: Shortcut) {
            if byKey[shortcut.key] == nil {
                order.append(shortcut.key)
            }
            byKey[shortcut.key] = shortcut
        }
        for shortcut in system { insert(shortcut) }
        for shortcut in frontApp { insert(shortcut) }
        return order.compactMap { byKey[$0] }.sorted(by: KeySort.less)
    }

    public static func loadBundled() -> Catalog {
        guard let data = BundledShortcuts.data() else {
            return Catalog(
                delaySeconds: 0.5,
                shortcuts: [],
                warnings: ["shortcuts.json was not found in the app bundle."],
                parseFailed: true
            )
        }
        return load(from: data)
    }

    public static func loadPreferred() -> Catalog {
        let userFile = ShortcutLocations.userFile
        if FileManager.default.fileExists(atPath: userFile.path) {
            do {
                return load(from: try Data(contentsOf: userFile))
            } catch {
                return Catalog(
                    delaySeconds: 0.5,
                    shortcuts: [],
                    warnings: ["Could not read \(userFile.path): \(error.localizedDescription)"],
                    parseFailed: true
                )
            }
        }
        return loadBundled()
    }

    public static func load(from data: Data) -> Catalog {
        let decoded: File
        do {
            decoded = try JSONDecoder().decode(File.self, from: data)
        } catch {
            return Catalog(
                delaySeconds: 0.5,
                shortcuts: [],
                warnings: ["shortcuts.json is not valid: \(error.localizedDescription)"],
                parseFailed: true
            )
        }

        var shortcuts: [Shortcut] = []
        var warnings: [String] = []
        var seen = Set<String>()
        for (index, entry) in decoded.shortcuts.enumerated() {
            let label = "Shortcut \(index + 1)"
            guard let names = entry.modifiers, let modifiers = ModifierSet(names: names) else {
                warnings.append("\(label) was skipped because a modifier is missing or unknown.")
                continue
            }
            guard let rawKey = entry.key else {
                warnings.append("\(label) was skipped because it has no key.")
                continue
            }
            let key = KeyDisplay.normalize(rawKey)
            guard !key.isEmpty else {
                warnings.append("\(label) was skipped because its key is empty.")
                continue
            }
            let title = entry.title?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            guard !title.isEmpty else {
                warnings.append("\(label) (\(key)) was skipped because it has no title.")
                continue
            }
            let identity = "\(modifiers.rawValue)|\(key)"
            if seen.contains(identity) {
                warnings.append("\(label) repeats \(KeyDisplay.label(for: key)) and was skipped.")
                continue
            }
            seen.insert(identity)
            let note = entry.note?.trimmingCharacters(in: .whitespacesAndNewlines)
            shortcuts.append(Shortcut(
                modifiers: modifiers,
                key: key,
                title: title,
                note: (note?.isEmpty == false) ? note : nil
            ))
        }

        return Catalog(
            delaySeconds: clampDelay(decoded.delaySeconds),
            shortcuts: shortcuts,
            warnings: warnings,
            parseFailed: false
        )
    }

    public static func clampDelay(_ value: Double?) -> Double {
        guard let value, value.isFinite else { return 0.5 }
        return min(3, max(0.1, value))
    }
}

public enum ShortcutLocations {
    public static var userFile: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSHomeDirectory()).appendingPathComponent("Library/Application Support")
        return base.appendingPathComponent("Holdhint/shortcuts.json")
    }
}

public enum BundledShortcuts {
    public static func data() -> Data? {
        let looseNames = ["shortcuts.json"]
        var candidates: [URL] = []
        if let resources = Bundle.main.resourceURL {
            candidates.append(contentsOf: looseNames.map { resources.appendingPathComponent($0) })
            candidates.append(resources.appendingPathComponent("Holdhint_HoldhintCore.bundle/Contents/Resources/shortcuts.json"))
        }
        if let executable = Bundle.main.executableURL?.deletingLastPathComponent() {
            candidates.append(executable.appendingPathComponent("shortcuts.json"))
            candidates.append(executable.appendingPathComponent("../Resources/shortcuts.json"))
            candidates.append(executable.appendingPathComponent("Holdhint_HoldhintCore.bundle/Contents/Resources/shortcuts.json"))
        }
        for url in candidates {
            let standardized = url.standardizedFileURL
            if let data = try? Data(contentsOf: standardized), !data.isEmpty {
                return data
            }
        }
        if let url = Bundle.module.url(forResource: "shortcuts", withExtension: "json"),
           let data = try? Data(contentsOf: url) {
            return data
        }
        return nil
    }
}

private struct File: Decodable {
    var delaySeconds: Double?
    var shortcuts: [Entry]
}

private struct Entry: Decodable {
    var modifiers: [String]?
    var key: String?
    var title: String?
    var note: String?
}
