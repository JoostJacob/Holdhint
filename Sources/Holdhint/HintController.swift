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
    private var frontObserver: NSObjectProtocol?
    private var frontPID: pid_t = 0
    private var pollTimer: DispatchSourceTimer?
    private var sessionShownAt: Date?
    private var overlayShownAt: Date?
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
        let mask: NSEvent.EventTypeMask = [
            .flagsChanged, .keyDown, .leftMouseDown, .rightMouseDown, .otherMouseDown
        ]
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: mask) { [weak self] event in
            self?.handle(event)
        }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: mask) { [weak self] event in
            self?.handle(event)
            return event
        }
        frontPID = NSWorkspace.shared.frontmostApplication?.processIdentifier ?? 0
        frontObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            self?.noteFrontApplication(notification)
        }
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + PanelGuard.pollInterval, repeating: PanelGuard.pollInterval)
        timer.setEventHandler { [weak self] in
            self?.pollModifiers()
        }
        timer.resume()
        pollTimer = timer
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
        sessionShownAt = nil
        overlayShownAt = nil
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

    /// Command, Option, Control, Shift, and the physical fn key.
    /// `NSEvent.modifierFlags` can stay stale in a menu-bar app after a dropped
    /// key-up, so this reads the session state instead. The function flag is
    /// ignored: arrow keys and F-keys set it without key code 63 being down.
    static func currentModifiers() -> ModifierSet {
        let flags = CGEventSource.flagsState(.combinedSessionState)
        let functionKeyDown = CGEventSource.keyState(
            .combinedSessionState,
            key: ModifierTracker.functionKeyCode
        )
        return ModifierSet.polled(
            command: flags.contains(.maskCommand),
            option: flags.contains(.maskAlternate),
            control: flags.contains(.maskControl),
            shift: flags.contains(.maskShift),
            functionKeyDown: functionKeyDown
        )
    }

    private func publishWarnings() {
        loadWarning = catalog.warnings.isEmpty ? nil : catalog.warnings.joined(separator: " ")
    }

    private func handle(_ event: NSEvent) {
        guard enabled else { return }
        let polled = Self.currentModifiers()
        switch event.type {
        case .leftMouseDown, .rightMouseDown, .otherMouseDown:
            handleGuard(.click(polled: polled))
            return
        case .keyDown where event.keyCode == PanelGuard.escapeKeyCode:
            handleGuard(.escape(polled: polled))
            return
        default:
            break
        }
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

    private func noteFrontApplication(_ notification: Notification) {
        let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication
        let pid = application?.processIdentifier ?? 0
        if pid == 0 || pid == frontPID { return }
        frontPID = pid
        handleGuard(.applicationSwitched(polled: Self.currentModifiers()))
    }

    private func pollModifiers() {
        guard enabled else { return }
        if overlay.isShowing {
            if overlayShownAt == nil { overlayShownAt = Date() }
        } else {
            overlayShownAt = nil
        }
        let polled = Self.currentModifiers()
        let visibleFor = sessionShownAt.map { Date().timeIntervalSince($0) } ?? 0
        handleGuard(.poll(polled: polled, visibleFor: visibleFor))
        // Samples and a missed session update share this cap, so nothing stays up.
        if let overlayShownAt, overlay.isShowing, Date().timeIntervalSince(overlayShownAt) >= PanelGuard.hardTimeout {
            forceClose(holding: polled)
        }
    }

    private func handleGuard(_ signal: PanelGuard.Signal) {
        guard enabled else { return }
        let decision = PanelGuard.action(phase: session.phase, held: session.held, signal: signal)
        switch decision {
        case .none:
            break
        case .sync(let set):
            modifiers.adopt(set)
            apply(session.modifierChange(set))
        case .suppress(_, let holding):
            modifiers.adopt(holding)
            apply(session.suppress(holding: holding))
        }
        if signal.isUserDismissal {
            dismissSample()
        }
    }

    private func forceClose(holding set: ModifierSet) {
        modifiers.adopt(set)
        apply(session.suppress(holding: set))
        dismissSample()
    }

    private func dismissSample() {
        sampleSet = nil
        sampleToken += 1
        overlay.dismiss()
        if !overlay.isShowing {
            overlayShownAt = nil
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
                if sessionShownAt == nil { sessionShownAt = Date() }
                render(set: set)
            case .hide:
                sessionShownAt = nil
                sampleSet = nil
                overlay.dismiss()
                overlayShownAt = nil
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
