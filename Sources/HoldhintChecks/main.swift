import Foundation
import HoldhintCore

/// Logic checks that run with the Command Line Tools alone. This Mac has no
/// XCTest and no usable Swift Testing plugin, so `swift test` is not the runner.
var failures = 0

@MainActor
func check(_ condition: Bool, _ message: String) {
    if !condition {
        failures += 1
        fputs("FAIL \(message)\n", stderr)
    }
}

let catalog = Catalog.loadBundled()
check(!catalog.parseFailed, "bundled file did not parse: \(catalog.warnings)")
check(catalog.warnings.isEmpty, catalog.warnings.joined(separator: " "))
check(catalog.shortcuts.count > 40, "count \(catalog.shortcuts.count)")
check(catalog.delaySeconds == 0.5, "delay \(catalog.delaySeconds)")

let clipboard = catalog.matching([.command, .control, .shift])
let three = clipboard.first { $0.key == "3" }
let four = clipboard.first { $0.key == "4" }
check(three?.title.localizedCaseInsensitiveContains("full screen") == true, "3 title")
check(three?.title.localizedCaseInsensitiveContains("clipboard") == true, "3 clipboard")
check(four?.title.localizedCaseInsensitiveContains("selection") == true, "4 title")
check(four?.title.localizedCaseInsensitiveContains("clipboard") == true, "4 clipboard")
check(four?.note?.localizedCaseInsensitiveContains("Space") == true, "4 note")
check(!clipboard.contains { $0.key == "5" }, "5 is not part of this chord")

let commandOnly = catalog.matching([.command])
check(commandOnly.allSatisfy { $0.modifiers == [.command] }, "command rows must be exact")
check(commandOnly.contains { $0.key == "space" }, "spotlight")
check(!commandOnly.contains { $0.key == "3" }, "screenshot 3 is not command alone")

let saved = catalog.matching([.command, .shift]).first { $0.key == "3" }
check(saved?.title.localizedCaseInsensitiveContains("clipboard") == false, "file shot")
check(saved?.note?.localizedCaseInsensitiveContains("Desktop") == true, "desktop note")

var seen = Set<String>()
for shortcut in catalog.shortcuts {
    let identity = "\(shortcut.modifiers.rawValue)|\(shortcut.key)"
    check(!seen.contains(identity), "duplicate \(shortcut.title)")
    seen.insert(identity)
}

let broken = """
{"delaySeconds": 9, "shortcuts": [
  {"modifiers": ["command", "nope"], "key": "a", "title": "Bad"},
  {"modifiers": ["command", "shift"], "key": "Slash", "title": "Help"},
  {"modifiers": [], "key": "b", "title": "Empty"}
]}
""".data(using: .utf8)!
let parsed = Catalog.load(from: broken)
check(!parsed.parseFailed, "partial file should parse")
check(parsed.shortcuts.count == 1, "one valid row")
check(parsed.shortcuts.first?.key == "/", "slash alias")
check(parsed.delaySeconds == 3, "delay clamp")
check(parsed.warnings.count == 2, "two warnings")
check(Catalog.load(from: Data("[]".utf8)).parseFailed, "array is not a catalog")

let merged = Catalog.merge(
    system: [Shortcut(modifiers: [.command], key: "c", title: "Copy", note: "keep")],
    frontApp: [
        Shortcut(modifiers: [.command], key: "c", title: "Copy Image"),
        Shortcut(modifiers: [.command], key: "k", title: "Clear")
    ]
)
check(merged.first { $0.key == "c" }?.title == "Copy Image", "app title wins")
check(merged.contains { $0.key == "k" }, "app-only key")

check(KeyDisplay.normalize("ESC") == "escape", "esc")
check(KeyDisplay.label(for: "escape") == "Esc", "esc label")
check(KeyDisplay.label(for: "c") == "C", "letter label")
check(KeyDisplay.normalize("spacebar") == "space", "spacebar")

var session = HoldSession()
check(session.modifierChange([.command]) == [.armTimer], "arm")
check(session.phase == .waiting, "waiting")
check(session.modifierChange([.command]) == [], "duplicate flags")
check(session.timerFired() == [.show([.command])], "show")
check(session.modifierChange([.command, .shift]) == [.show([.command, .shift])], "update")
check(session.modifierChange([]) == [.hide], "hide")
check(session.phase == .idle, "idle")

session = HoldSession()
_ = session.modifierChange([.command, .control, .shift])
check(session.keyDown() == [.disarmTimer], "cancel wait")
check(session.timerFired() == [], "suppressed timer")
check(session.modifierChange([]) == [], "release after suppress")
_ = session.modifierChange([.command])
_ = session.timerFired()
check(session.keyDown() == [.hide], "hide on key")
check(session.modifierChange([.option]) == [.armTimer], "new chord after a key")

session = HoldSession()
_ = session.modifierChange([.command])
check(session.modifierChange([.command, .shift]) == [.armTimer], "restart wait")
check(session.modifierChange([]) == [.disarmTimer], "cancel on release")

session = HoldSession()
_ = session.modifierChange([.command])
_ = session.timerFired()
check(session.suppress(holding: []) == [.hide], "release hides")
check(session.phase == .idle, "release is idle")
check(session.held.isEmpty, "release clears keys")

session = HoldSession()
_ = session.modifierChange([.function])
_ = session.timerFired()
check(session.suppress(holding: [.function]) == [.hide], "timeout hides")
check(session.phase == .suppressed, "timeout stays quiet")
check(session.modifierChange([.function]) == [], "same chord stays quiet")
check(session.modifierChange([]) == [], "release after timeout")
check(session.phase == .idle, "idle after a quiet release")

session = HoldSession()
_ = session.modifierChange([.command])
check(session.suppress(holding: [.command]) == [.disarmTimer], "click while waiting")
check(session.phase == .suppressed, "waiting click does not show")
check(session.timerFired() == [], "timer after a click does nothing")

let symbols = ModifierSet([.command, .shift, .control])
check(symbols.symbols == "⌃ ⇧ ⌘", "symbol order")
check(ModifierSet(names: ["shift", "ctrl", "cmd"]) == symbols, "aliases")
check(ModifierSet(names: ["command", "super"]) == nil, "unknown modifier")
check(ModifierSet(names: ["fn"]) == [.function], "fn name")
check(ModifierSet(names: ["globe", "ctrl"]) == [.function, .control], "globe alias")
let fnSymbols = ModifierSet([.function, .command, .shift, .control, .option])
check(fnSymbols.symbols == "fn ⌃ ⌥ ⇧ ⌘", "fn symbol order")

let globe = catalog.matching([.function])
check(globe.contains { $0.key == "a" && $0.title.localizedCaseInsensitiveContains("Dock") }, "fn dock")
check(globe.contains { $0.key == "c" && $0.title.localizedCaseInsensitiveContains("Control Center") }, "fn control center")
check(globe.contains { $0.key == "n" && $0.title.localizedCaseInsensitiveContains("Notification") }, "fn notifications")
check(globe.contains { $0.key == "e" && $0.title.localizedCaseInsensitiveContains("Emoji") }, "fn emoji")
check(globe.contains { $0.key == "q" && $0.title.localizedCaseInsensitiveContains("Quick Note") }, "fn quick note")
check(globe.contains { $0.key == "d" && $0.title.localizedCaseInsensitiveContains("dictation") }, "fn dictation")
check(globe.contains { $0.key == "h" && $0.title.localizedCaseInsensitiveContains("desktop") }, "fn desktop")
check(globe.contains { $0.key == "delete" }, "fn forward delete")
check(globe.contains { $0.key == "left" && $0.title.localizedCaseInsensitiveContains("beginning") }, "fn home")
check(globe.contains { $0.key == "up" && $0.title.localizedCaseInsensitiveContains("page") }, "fn page up")
check(globe.contains { $0.key == "f1" }, "fn f1")
check(globe.contains { $0.key == "f12" }, "fn f12")
check(globe.first { $0.key == "f11" }?.title.localizedCaseInsensitiveContains("desktop") == true, "fn f11 desktop")
check(!globe.contains { $0.key == "f" }, "fn alone is not fullscreen")
check(!globe.contains { $0.key == "m" }, "fn-m is not a documented shortcut")
let apps = catalog.matching([.function, .shift]).first { $0.key == "a" }
check(apps?.title.localizedCaseInsensitiveContains("Apps") == true, "fn shift a")
let fill = catalog.matching([.function, .control]).first { $0.key == "f" }
check(fill?.title.localizedCaseInsensitiveContains("Fill") == true, "tile fill")
check(fill?.title.localizedCaseInsensitiveContains("full screen") == false, "fill is not full screen")
check(catalog.matching([.function, .control]).contains { $0.key == "left" }, "tile left")
check(catalog.matching([.function, .control, .shift]).contains { $0.key == "right" }, "tile pair")
check(catalog.matching([.function, .control, .option, .shift]).contains { $0.key == "up" }, "tile quarters")

var tracker = ModifierTracker()
let arrow = tracker.flagsChanged(keyCode: 126, command: false, option: false, control: false, shift: false, functionFlag: true)
check(arrow == [], "arrow function flag is not fn")
check(!tracker.functionKeyDown, "arrow does not latch fn")
let fKey = tracker.flagsChanged(keyCode: 122, command: true, option: false, control: false, shift: false, functionFlag: true)
check(fKey == [.command], "F-key function flag is not fn")
let held = tracker.flagsChanged(keyCode: 63, command: false, option: false, control: false, shift: false, functionFlag: true)
check(held == [.function], "physical fn down")
let withShift = tracker.flagsChanged(keyCode: 56, command: false, option: false, control: false, shift: true, functionFlag: true)
check(withShift == [.function, .shift], "shift while fn is held")
let arrowWhileHeld = tracker.flagsChanged(keyCode: 123, command: false, option: false, control: false, shift: true, functionFlag: false)
check(arrowWhileHeld == [.function, .shift], "arrow must not clear a real fn hold")
let released = tracker.flagsChanged(keyCode: 63, command: false, option: false, control: false, shift: true, functionFlag: false)
check(released == [.shift], "physical fn up")
check(tracker.functionKeyDown == false, "fn latch cleared")

var stuck = ModifierTracker()
_ = stuck.flagsChanged(keyCode: 63, command: true, option: false, control: false, shift: false, functionFlag: true)
check(stuck.functionKeyDown, "fn latched")
stuck.adopt([])
check(!stuck.functionKeyDown, "adopt clears a missed fn release")
let arrowAfter = stuck.flagsChanged(keyCode: 126, command: false, option: false, control: false, shift: false, functionFlag: true)
check(arrowAfter.isEmpty, "arrow after adopt is not fn")
stuck.adopt([.function])
check(stuck.functionKeyDown, "adopt can set fn")

check(ModifierSet.polled(command: false, option: false, control: false, shift: false, functionKeyDown: false).isEmpty, "poll empty")
check(ModifierSet.polled(command: true, option: false, control: false, shift: true, functionKeyDown: false) == [.command, .shift], "poll ignores the function flag")
check(ModifierSet.polled(command: false, option: false, control: false, shift: false, functionKeyDown: true) == [.function], "poll uses the physical fn key")

let command: ModifierSet = [.command]
let showing = HoldSession.Phase.showing
check(
    PanelGuard.action(phase: showing, held: command, signal: .poll(polled: [], visibleFor: 0.2))
        == .suppress(.modifiersReleased, holding: []),
    "poll hides when nothing is held"
)
check(
    PanelGuard.action(phase: showing, held: command, signal: .poll(polled: command, visibleFor: 1))
        == .none,
    "poll keeps a held chord"
)
check(
    PanelGuard.action(phase: showing, held: command, signal: .poll(polled: command, visibleFor: 29.9))
        == .none,
    "just under the timeout"
)
check(
    PanelGuard.action(phase: showing, held: command, signal: .poll(polled: command, visibleFor: 30))
        == .suppress(.timedOut, holding: command),
    "hard timeout"
)
check(
    PanelGuard.action(phase: showing, held: [.function, .command], signal: .poll(polled: command, visibleFor: 1))
        == .sync(command),
    "stuck fn follows the real keys"
)
check(
    PanelGuard.action(phase: .waiting, held: command, signal: .poll(polled: [], visibleFor: 0))
        == .suppress(.modifiersReleased, holding: []),
    "missed release before the panel shows"
)
check(
    PanelGuard.action(phase: .waiting, held: [.function], signal: .poll(polled: command, visibleFor: 0))
        == .none,
    "waiting does not restart on a different poll"
)
check(
    PanelGuard.action(phase: .idle, held: [], signal: .poll(polled: [], visibleFor: 40)) == .none,
    "idle poll"
)
check(
    PanelGuard.action(phase: showing, held: command, signal: .click(polled: command))
        == .suppress(.clicked, holding: command),
    "click"
)
check(
    PanelGuard.action(phase: showing, held: command, signal: .escape(polled: []))
        == .suppress(.escape, holding: []),
    "escape"
)
check(
    PanelGuard.action(phase: .waiting, held: command, signal: .applicationSwitched(polled: command))
        == .suppress(.applicationSwitched, holding: command),
    "switch app"
)
check(
    PanelGuard.action(phase: .idle, held: [], signal: .click(polled: [])) == .none,
    "click while idle leaves the session"
)
check(PanelGuard.Signal.click(polled: []).isUserDismissal, "click closes a sample")
check(!PanelGuard.Signal.poll(polled: [], visibleFor: 0).isUserDismissal, "poll leaves a sample")
check(
    PanelGuard.action(phase: .suppressed, held: command, signal: .poll(polled: [], visibleFor: 5))
        == .sync([]),
    "quiet chord notices the release"
)
check(PanelGuard.escapeKeyCode == 53, "escape key code")

if failures == 0 {
    fputs("CHECKS OK\n", stderr)
    exit(0)
}
fputs("\(failures) check(s) failed\n", stderr)
exit(1)
