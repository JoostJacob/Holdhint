import AppKit
import ApplicationServices
import CoreGraphics
import Darwin
import HoldhintCore

final class HintController {
    let overlay = OverlayController()
    private(set) var catalog = Catalog.loadPreferred()
    private(set) var loadWarning: String?
    private(set) var enabled = true
    private var session = HoldSession()
    private var modifiers = ModifierTracker()
    private var pending: DispatchWorkItem?
    private var globalMonitor: Any?
    private var localMonitor: Any?
    private var activity: NSObjectProtocol?
    private var sampleSet: ModifierSet?
    private var sampleToken = 0
    private var cache: AppCache?
    private var watchSource: DispatchSourceFileSystemObject?
    private var reloadWork: DispatchWorkItem?

    struct AppCache {
        var pid: pid_t
        var fetched: Date
        var shortcuts: [Shortcut]
    }

    var statusLine: String {
        let input = CGPreflightListenEventAccess() ? "on" : "off"
        let accessibility = AXIsProcessTrusted() ? "on" : "off"
        var line = "Input Monitoring: \(input)  ·  Accessibility: \(accessibility)"
        if globalMonitor == nil {
            line += "  ·  listener not installed"
        }
        return line
    }

    func start() {
        catalog = Catalog.loadPreferred()
        publishWarnings()
        let mask: NSEvent.EventTypeMask = [.flagsChanged, .keyDown]
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: mask) { [weak self] event in
            self?.handle(event)
        }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: mask) { [weak self] event in
            self?.handle(event)
            return event
        }
        activity = ProcessInfo.processInfo.beginActivity(
            options: [.userInitiatedAllowingIdleSystemSleep],
            reason: "Watching modifier keys to show shortcut hints"
        )
        if FileManager.default.fileExists(atPath: ShortcutLocations.userFile.path) {
            watchUserShortcuts()
        }
        fputs("Holdhint \(catalog.shortcuts.count) shortcuts, delay \(catalog.delaySeconds)s. \(statusLine)\n", stderr)
        for warning in catalog.warnings {
            fputs("Holdhint: \(warning)\n", stderr)
        }
    }

    func setEnabled(_ on: Bool) {
        enabled = on
        guard !on else { return }
        pending?.cancel()
        pending = nil
        session = HoldSession()
        modifiers = ModifierTracker()
        sampleSet = nil
        sampleToken += 1
        overlay.dismiss()
    }

    func showSample(_ set: ModifierSet, sticky: Bool) {
        sampleSet = set
        sampleToken += 1
        let token = sampleToken
        render(set: set)
        guard !sticky else { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 5) { [weak self] in
            guard let self, self.sampleToken == token, self.session.phase != .showing else { return }
            self.sampleSet = nil
            self.overlay.dismiss()
        }
    }

    func reloadFromDisk() {
        let loaded = Catalog.loadPreferred()
        if loaded.parseFailed {
            loadWarning = loaded.warnings.joined(separator: " ")
            fputs("Holdhint kept the previous list. \(loadWarning ?? "")\n", stderr)
            return
        }
        catalog = loaded
        publishWarnings()
        if session.phase == .showing {
            render(set: session.held)
        }
    }

    func revealEditableCopy() {
        let destination = ShortcutLocations.userFile
        let folder = destination.deletingLastPathComponent()
        do {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            if !FileManager.default.fileExists(atPath: destination.path), let data = BundledShortcuts.data() {
                try data.write(to: destination, options: .atomic)
            }
        } catch {
            fputs("Holdhint could not create \(destination.path): \(error)\n", stderr)
        }
        NSWorkspace.shared.open(destination)
        watchUserShortcuts()
    }

    func writeSnapshot(to url: URL) throws {
        try overlay.writePNG(to: url)
    }

    func requestPermissionsOnce() {
        let defaults = UserDefaults.standard
        if !CGPreflightListenEventAccess(), !defaults.bool(forKey: "didRequestInputMonitoring") {
            defaults.set(true, forKey: "didRequestInputMonitoring")
            CGRequestListenEventAccess()
        }
        if !AXIsProcessTrusted(), !defaults.bool(forKey: "didRequestAccessibility") {
            defaults.set(true, forKey: "didRequestAccessibility")
            let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
            let options = [key: true] as CFDictionary
            AXIsProcessTrustedWithOptions(options)
        }
    }

    private func publishWarnings() {
        loadWarning = catalog.warnings.isEmpty ? nil : catalog.warnings.joined(separator: " ")
    }

    private func handle(_ event: NSEvent) {
        guard enabled else { return }
        if event.type == .keyDown {
            // fn/Globe is a modifier. A keyDown for it must not dismiss the panel.
            if event.keyCode == ModifierTracker.functionKeyCode { return }
            apply(session.keyDown())
        } else if event.type == .flagsChanged {
            let flags = event.modifierFlags
            let set = modifiers.flagsChanged(
                keyCode: event.keyCode,
                command: flags.contains(.command),
                option: flags.contains(.option),
                control: flags.contains(.control),
                shift: flags.contains(.shift),
                functionFlag: flags.contains(.function)
            )
            apply(session.modifierChange(set))
        }
    }

    private func apply(_ effects: [HoldSession.Effect]) {
        for effect in effects {
            switch effect {
            case .armTimer:
                armTimer()
            case .disarmTimer:
                pending?.cancel()
                pending = nil
            case .show(let set):
                sampleSet = nil
                sampleToken += 1
                render(set: set)
            case .hide:
                sampleSet = nil
                overlay.dismiss()
            }
        }
    }

    private func armTimer() {
        pending?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.apply(self.session.timerFired())
        }
        pending = work
        DispatchQueue.main.asyncAfter(deadline: .now() + catalog.delaySeconds, execute: work)
    }

    private func render(set: ModifierSet) {
        let application = NSWorkspace.shared.frontmostApplication
        let pid = application?.processIdentifier ?? 0
        let name = application?.localizedName ?? ""
        let ours = pid == ProcessInfo.processInfo.processIdentifier
        let cached = (!ours && cache?.pid == pid) ? cache?.shortcuts ?? [] : []
        paint(set: set, appRows: cached, appName: cached.isEmpty ? "" : name)

        guard !ours, pid > 0, AXIsProcessTrusted() else { return }
        if let cache, cache.pid == pid, Date().timeIntervalSince(cache.fetched) < 3 {
            return
        }
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            let found = MenuReader.shortcuts(pid: pid)
            DispatchQueue.main.async {
                guard let self else { return }
                self.cache = AppCache(pid: pid, fetched: Date(), shortcuts: found)
                let showing = self.session.phase == .showing && self.session.held == set
                let sampling = self.sampleSet == set
                guard showing || sampling else { return }
                self.paint(set: set, appRows: found, appName: name)
            }
        }
    }

    private func paint(set: ModifierSet, appRows: [Shortcut], appName: String) {
        let system = catalog.matching(set)
        let fromApp = appRows.filter { $0.modifiers == set }
        let merged = Catalog.merge(system: system, frontApp: fromApp)
        let subtitle: String
        if merged.isEmpty {
            subtitle = "No shortcuts for these keys"
        } else if !fromApp.isEmpty, !appName.isEmpty {
            subtitle = "\(appName) menus included  ·  release to dismiss"
        } else {
            subtitle = "Release to dismiss"
        }
        let rows = merged.map { shortcut in
            HUDRow(key: KeyDisplay.label(for: shortcut.key), title: shortcut.title, note: shortcut.note)
        }
        overlay.present(HUDContent(symbols: set.symbolList, subtitle: subtitle, rows: rows))
    }

    private func watchUserShortcuts() {
        watchSource?.cancel()
        watchSource = nil
        let folder = ShortcutLocations.userFile.deletingLastPathComponent()
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let fd = open(folder.path, O_EVTONLY)
        guard fd >= 0 else { return }
        let source = DispatchSource.makeFileSystemObjectSource(
            fileDescriptor: fd,
            eventMask: [.write, .rename, .delete],
            queue: .main
        )
        source.setEventHandler { [weak self] in
            self?.scheduleReload()
        }
        source.setCancelHandler {
            close(fd)
        }
        source.resume()
        watchSource = source
    }

    private func scheduleReload() {
        reloadWork?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard FileManager.default.fileExists(atPath: ShortcutLocations.userFile.path) else { return }
            self?.reloadFromDisk()
        }
        reloadWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3, execute: work)
    }
}
