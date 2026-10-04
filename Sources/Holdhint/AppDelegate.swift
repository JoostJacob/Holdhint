import AppKit
import HoldhintCore

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    let controller = HintController()
    private var statusItem: NSStatusItem?
    private let permissionItem = NSMenuItem()
    private let warningItem = NSMenuItem()
    private let toggleItem = NSMenuItem(title: "Show Hints", action: #selector(toggleHints(_:)), keyEquivalent: "")
    private var enabled = true

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        if CommandLine.arguments.contains("--self-test") {
            let passed = SelfTest.run(controller: controller)
            exit(passed ? 0 : 1)
        }
        enabled = storedEnabled
        controller.setEnabled(enabled)
        installStatusItem()
        controller.start()
        if let preview = previewModifiers() {
            controller.showSample(preview, sticky: true)
            if let snapshot = snapshotURL() {
                do {
                    try controller.writeSnapshot(to: snapshot)
                    fputs("Wrote \(snapshot.path)\n", stderr)
                } catch {
                    fputs("Could not write \(snapshot.path): \(error)\n", stderr)
                }
            }
            let frame = controller.overlay.panel.frame
            let panel = controller.overlay.panel
            fputs(String(format: "OVERLAY id=%d x=%.0f y=%.0f w=%.0f h=%.0f clickThrough=%@ canKey=%@\n",
                         panel.windowNumber, frame.origin.x, frame.origin.y, frame.width, frame.height,
                         panel.ignoresMouseEvents ? "yes" : "no",
                         panel.canBecomeKey ? "yes" : "no"), stderr)
            return
        }
        controller.requestPermissionsOnce()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    func menuWillOpen(_ menu: NSMenu) {
        permissionItem.title = controller.statusLine
        if let warning = controller.loadWarning, !warning.isEmpty {
            warningItem.title = warning
            warningItem.isHidden = false
        } else {
            warningItem.isHidden = true
        }
        toggleItem.state = enabled ? .on : .off
    }

    private var storedEnabled: Bool {
        if UserDefaults.standard.object(forKey: "hintsEnabled") == nil { return true }
        return UserDefaults.standard.bool(forKey: "hintsEnabled")
    }

    private func installStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let image = NSImage(systemSymbolName: "command", accessibilityDescription: "Holdhint")?
            .withSymbolConfiguration(.init(pointSize: 14, weight: .semibold)) {
            image.isTemplate = true
            item.button?.image = image
        } else {
            item.button?.title = "⌘"
        }
        item.button?.toolTip = "Holdhint"

        let menu = NSMenu()
        menu.delegate = self
        permissionItem.isEnabled = false
        warningItem.isEnabled = false
        warningItem.isHidden = true
        toggleItem.target = self
        toggleItem.state = enabled ? .on : .off

        menu.addItem(permissionItem)
        menu.addItem(warningItem)
        menu.addItem(.separator())
        menu.addItem(toggleItem)
        menu.addItem(menuItem("Show Sample", #selector(showSample(_:))))
        menu.addItem(.separator())
        menu.addItem(menuItem("Edit Shortcuts…", #selector(editShortcuts(_:))))
        menu.addItem(menuItem("Reload Shortcuts", #selector(reloadShortcuts(_:))))
        menu.addItem(.separator())
        menu.addItem(menuItem("Accessibility Settings…", #selector(openAccessibility(_:))))
        menu.addItem(menuItem("Input Monitoring Settings…", #selector(openInputMonitoring(_:))))
        menu.addItem(.separator())
        menu.addItem(menuItem("Quit Holdhint", #selector(quit(_:))))
        item.menu = menu
        statusItem = item
    }

    private func menuItem(_ title: String, _ action: Selector) -> NSMenuItem {
        let entry = NSMenuItem(title: title, action: action, keyEquivalent: "")
        entry.target = self
        return entry
    }

    @objc private func toggleHints(_ sender: NSMenuItem) {
        enabled.toggle()
        UserDefaults.standard.set(enabled, forKey: "hintsEnabled")
        controller.setEnabled(enabled)
        sender.state = enabled ? .on : .off
    }

    @objc private func showSample(_ sender: NSMenuItem) {
        controller.showSample([.command, .control, .shift], sticky: false)
    }

    @objc private func editShortcuts(_ sender: NSMenuItem) {
        controller.revealEditableCopy()
    }

    @objc private func reloadShortcuts(_ sender: NSMenuItem) {
        controller.reloadFromDisk()
    }

    @objc private func openAccessibility(_ sender: NSMenuItem) {
        openPrivacy("Privacy_Accessibility")
    }

    @objc private func openInputMonitoring(_ sender: NSMenuItem) {
        openPrivacy("Privacy_ListenEvent")
    }

    @objc private func quit(_ sender: NSMenuItem) {
        NSApp.terminate(nil)
    }

    private func openPrivacy(_ anchor: String) {
        let urls = [
            "x-apple.systempreferences:com.apple.settings.PrivacySecurity.extension?\(anchor)",
            "x-apple.systempreferences:com.apple.preference.security?\(anchor)"
        ]
        for text in urls {
            if let url = URL(string: text), NSWorkspace.shared.open(url) {
                return
            }
        }
    }

    private func previewModifiers() -> ModifierSet? {
        let args = CommandLine.arguments
        guard let index = args.firstIndex(of: "--preview") else { return nil }
        let next = index + 1
        guard next < args.count, !args[next].hasPrefix("-") else {
            return [.command, .control, .shift]
        }
        let names = args[next].split(separator: ",").map(String.init)
        return ModifierSet(names: names) ?? [.command, .control, .shift]
    }

    private func snapshotURL() -> URL? {
        let args = CommandLine.arguments
        guard let index = args.firstIndex(of: "--snapshot"), index + 1 < args.count else { return nil }
        return URL(fileURLWithPath: args[index + 1])
    }
}

enum SelfTest {
    static func run(controller: HintController) -> Bool {
        var ok = true
        func check(_ condition: Bool, _ message: String) {
            if !condition {
                fputs("FAIL \(message)\n", stderr)
                ok = false
            }
        }

        let catalog = Catalog.loadBundled()
        check(!catalog.parseFailed, "bundled file did not parse: \(catalog.warnings)")
        check(catalog.shortcuts.count > 40, "expected more shortcuts, got \(catalog.shortcuts.count)")
        check(abs(catalog.delaySeconds - 0.5) < 0.001, "delay \(catalog.delaySeconds)")

        let clipboard: ModifierSet = [.command, .control, .shift]
        let rows = catalog.matching(clipboard)
        let three = rows.first { $0.key == "3" }
        let four = rows.first { $0.key == "4" }
        check(three?.title.localizedCaseInsensitiveContains("full screen") == true, "3 title \(three?.title ?? "missing")")
        check(three?.title.localizedCaseInsensitiveContains("clipboard") == true, "3 clipboard")
        check(four?.title.localizedCaseInsensitiveContains("selection") == true, "4 title")
        check(four?.title.localizedCaseInsensitiveContains("clipboard") == true, "4 clipboard")
        check(rows.contains { $0.key == "5" } == false, "5 should not be in the clipboard chord")

        let savedToDisk = catalog.matching([.command, .shift]).first { $0.key == "3" }
        check(savedToDisk?.title.localizedCaseInsensitiveContains("clipboard") == false, "file screenshot must not say clipboard")
        check(savedToDisk?.note?.localizedCaseInsensitiveContains("Desktop") == true, "file screenshot should mention the Desktop")

        var seen = Set<String>()
        for shortcut in catalog.shortcuts {
            let identity = "\(shortcut.modifiers.rawValue)|\(shortcut.key)"
            check(!seen.contains(identity), "duplicate \(identity)")
            seen.insert(identity)
        }

        controller.showSample(clipboard, sticky: true)
        let panel = controller.overlay.panel
        check(panel.ignoresMouseEvents, "clicks should pass through")
        check(panel.canBecomeKey == false, "overlay must not become key")
        check(panel.canBecomeMain == false, "overlay must not become main")
        check(panel.styleMask.contains(.nonactivatingPanel), "panel should be nonactivating")
        check(panel.level == .popUpMenu, "level \(panel.level.rawValue)")
        check(panel.isVisible, "overlay should be visible")
        if let screen = OverlayController.screenUnderMouse() {
            check(panel.frame.intersects(screen.frame), "overlay should sit on the screen under the pointer")
        } else {
            check(false, "no screen")
        }

        let url = URL(fileURLWithPath: "/tmp/holdhint-selftest.png")
        do {
            try controller.writeSnapshot(to: url)
            let bytes = (try? Data(contentsOf: url))?.count ?? 0
            check(bytes > 2000, "snapshot was \(bytes) bytes")
        } catch {
            check(false, "snapshot failed: \(error)")
        }

        if ok { fputs("SELFTEST OK\n", stderr) }
        return ok
    }
}
