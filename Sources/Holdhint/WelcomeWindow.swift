import AppKit
import CoreGraphics

enum PrivacySettings {
    static func open(_ anchor: String) {
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
}

/// Reads Input Monitoring from a new process.
///
/// `CGPreflightListenEventAccess` in the process that is already running often
/// stays false after the user turns the switch on. A child started from the
/// same binary reads the current answer. `--preflight-listen` exits before
/// any window is created.
enum InputMonitoringProbe {
    static func grantedInProcess() -> Bool {
        CGPreflightListenEventAccess()
    }

    static func grantedFresh() -> Bool {
        if CGPreflightListenEventAccess() { return true }
        guard let executable = Bundle.main.executableURL else { return false }
        let task = Process()
        task.executableURL = executable
        task.arguments = ["--preflight-listen"]
        let pipe = Pipe()
        task.standardOutput = pipe
        task.standardError = FileHandle.nullDevice
        do {
            try task.run()
        } catch {
            return false
        }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        task.waitUntilExit()
        let text = String(data: data, encoding: .utf8) ?? ""
        return text.contains("yes")
    }
}

final class WelcomeController: NSObject, NSWindowDelegate {
    private let window: NSWindow
    private let statusLabel: NSTextField
    private let openButton: NSButton
    private let laterButton: NSButton
    private var timer: Timer?
    private var probing = false
    private var relaunching = false

    private static let relaunchKey = "inputMonitoringRelaunchAt"

    override init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 420, height: 460),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = "Welcome to Holdhint"
        window.isReleasedWhenClosed = false
        window.level = .floating
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.isMovableByWindowBackground = true

        let title = Self.wrapping("Allow Input Monitoring", size: 20, weight: .semibold, color: .labelColor)
        let intro = Self.wrapping(
            "Holdhint shows shortcuts while you hold Fn (Globe), Command, Control, Option, or Shift. macOS sends those keys only after Input Monitoring is on.",
            size: 13,
            weight: .regular,
            color: .labelColor
        )
        let steps = Self.wrapping(
            "1. Click Open Input Monitoring Settings. If macOS also asks, click Open System Settings in that dialog.\n2. Turn Holdhint on. If it is missing, click +, choose this copy of Holdhint, and turn it on.\n3. Leave this window open. Holdhint restarts itself when the switch is on. Quit it from the ⌘ menu and open it again if it does not.",
            size: 13,
            weight: .regular,
            color: .labelColor
        )
        let optional = Self.wrapping(
            "Accessibility is optional. It adds shortcuts from the app you are using. The built-in list needs only Input Monitoring.",
            size: 12,
            weight: .regular,
            color: .secondaryLabelColor
        )
        let path = Self.wrapping(
            "This copy: \(Self.displayPath(Bundle.main.bundleURL.path))",
            size: 11,
            weight: .regular,
            color: .secondaryLabelColor
        )
        let status = Self.wrapping("Waiting for Input Monitoring…", size: 13, weight: .medium, color: .labelColor)

        let open = NSButton(title: "Open Input Monitoring Settings", target: nil, action: #selector(openSettings))
        open.bezelStyle = .rounded
        open.keyEquivalent = "\r"
        let later = NSButton(title: "Not Now", target: nil, action: #selector(closeWindow))
        later.bezelStyle = .rounded
        later.keyEquivalent = "\u{1b}"

        let buttons = NSStackView(views: [open, later])
        buttons.orientation = .horizontal
        buttons.spacing = 12
        buttons.alignment = .centerY

        let stack = NSStackView(views: [title, intro, steps, optional, path, buttons, status])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 12
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.setCustomSpacing(16, after: title)
        stack.setCustomSpacing(16, after: path)
        stack.setCustomSpacing(14, after: buttons)

        let content = NSView()
        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 20),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -20),
            stack.topAnchor.constraint(equalTo: content.topAnchor, constant: 16),
            stack.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -18),
            stack.widthAnchor.constraint(equalToConstant: 380)
        ])
        window.contentView = content
        content.layoutSubtreeIfNeeded()
        let height = stack.fittingSize.height + 34
        window.setContentSize(NSSize(width: 420, height: max(280, height)))

        self.window = window
        self.statusLabel = status
        self.openButton = open
        self.laterButton = later
        super.init()
        open.target = self
        later.target = self
        window.delegate = self
    }

    func show() {
        placeAtTopRight()
        NSApp.activate()
        window.makeKeyAndOrderFront(nil)
        let defaults = UserDefaults.standard
        if !InputMonitoringProbe.grantedInProcess(), !defaults.bool(forKey: "didRequestInputMonitoring") {
            defaults.set(true, forKey: "didRequestInputMonitoring")
            DispatchQueue.main.async {
                CGRequestListenEventAccess()
            }
        }
        let timer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            self?.poll()
        }
        self.timer = timer
        poll()
    }

    func windowWillClose(_ notification: Notification) {
        timer?.invalidate()
        timer = nil
    }

    @objc private func openSettings() {
        PrivacySettings.open("Privacy_ListenEvent")
    }

    @objc private func closeWindow() {
        window.close()
    }

    private func placeAtTopRight() {
        let screen = window.screen ?? NSScreen.main ?? NSScreen.screens.first
        guard let visible = screen?.visibleFrame else { return }
        let frame = window.frame
        window.setFrameOrigin(NSPoint(
            x: visible.maxX - frame.width - 16,
            y: visible.maxY - frame.height - 16
        ))
    }

    private func poll() {
        guard !relaunching, !probing else { return }
        probing = true
        DispatchQueue.global(qos: .utility).async { [weak self] in
            let granted = InputMonitoringProbe.grantedFresh()
            DispatchQueue.main.async {
                guard let self else { return }
                self.probing = false
                guard granted, !self.relaunching, self.window.isVisible else { return }
                self.beginRestart()
            }
        }
    }

    private func beginRestart() {
        relaunching = true
        timer?.invalidate()
        timer = nil
        openButton.isEnabled = false
        laterButton.isEnabled = false
        statusLabel.stringValue = "Input Monitoring is on. Restarting Holdhint…"
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { [weak self] in
            self?.relaunch()
        }
    }

    private func relaunch() {
        let url = Bundle.main.bundleURL
        guard url.pathExtension == "app" else {
            showManualRestart()
            return
        }
        let now = Date().timeIntervalSince1970
        let last = UserDefaults.standard.double(forKey: Self.relaunchKey)
        if now - last < 15 {
            showManualRestart()
            return
        }
        UserDefaults.standard.set(now, forKey: Self.relaunchKey)
        let config = NSWorkspace.OpenConfiguration()
        config.createsNewApplicationInstance = true
        config.activates = true
        NSWorkspace.shared.openApplication(at: url, configuration: config) { [weak self] _, error in
            DispatchQueue.main.async {
                if error != nil {
                    self?.showManualRestart()
                    return
                }
                NSApp.terminate(nil)
            }
        }
    }

    private func showManualRestart() {
        relaunching = true
        statusLabel.stringValue = "Input Monitoring is on. Quit Holdhint from the ⌘ menu and open it again."
        openButton.isEnabled = false
        laterButton.isEnabled = true
    }

    private static func displayPath(_ path: String) -> String {
        let home = FileManager.default.homeDirectoryForCurrentUser.path
        if path == home { return "~" }
        if path.hasPrefix(home + "/") {
            return "~" + path.dropFirst(home.count)
        }
        return path
    }

    private static func wrapping(_ text: String, size: CGFloat, weight: NSFont.Weight, color: NSColor) -> NSTextField {
        let field = NSTextField(wrappingLabelWithString: text)
        field.font = .systemFont(ofSize: size, weight: weight)
        field.textColor = color
        field.preferredMaxLayoutWidth = 380
        field.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        return field
    }
}
