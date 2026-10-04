import Foundation

/// When the hint panel must close.
///
/// The panel used to hide only after a `flagsChanged` event with no modifiers
/// left. macOS can deliver the press and drop the release (this happens when
/// Input Monitoring is off, and also when the fn/Globe key-up is not key code
/// 63). The panel ignores clicks and cannot take focus, so it then stays up
/// until the user quits. These rules are the pure half of the watchdog.
public enum PanelGuard {
    /// How often to read the real modifier state while a chord is active.
    public static let pollInterval: TimeInterval = 0.3
    /// Backstop when the system still reports a key down after the user let go.
    public static let hardTimeout: TimeInterval = 30
    /// kVK_Escape. Same on every Mac keyboard.
    public static let escapeKeyCode: UInt16 = 53

    public enum Reason: Equatable, Sendable {
        case modifiersReleased
        case timedOut
        case clicked
        case escape
        case applicationSwitched
    }

    public enum Signal: Equatable, Sendable {
        /// `visibleFor` is how long this chord’s panel has been showing.
        case poll(polled: ModifierSet, visibleFor: TimeInterval)
        case click(polled: ModifierSet)
        case escape(polled: ModifierSet)
        case applicationSwitched(polled: ModifierSet)

        /// Clicks, Escape, and switching apps also close a sample preview,
        /// which is not a held chord.
        public var isUserDismissal: Bool {
            switch self {
            case .click, .escape, .applicationSwitched:
                return true
            case .poll:
                return false
            }
        }
    }

    public enum Action: Equatable, Sendable {
        case none
        /// Hide and remember `holding` so a chord that is still down does not
        /// open the panel again until it changes. Empty means idle.
        case suppress(Reason, holding: ModifierSet)
        /// The keys that are actually down changed. Follow them.
        case sync(ModifierSet)
    }

    public static func action(
        phase: HoldSession.Phase,
        held: ModifierSet,
        signal: Signal,
        hardTimeout: TimeInterval = PanelGuard.hardTimeout
    ) -> Action {
        switch signal {
        case .click(let polled):
            return dismiss(.clicked, phase: phase, holding: polled)
        case .escape(let polled):
            return dismiss(.escape, phase: phase, holding: polled)
        case .applicationSwitched(let polled):
            return dismiss(.applicationSwitched, phase: phase, holding: polled)
        case .poll(let polled, let visibleFor):
            return poll(
                phase: phase,
                held: held,
                polled: polled,
                visibleFor: visibleFor,
                hardTimeout: hardTimeout
            )
        }
    }

    private static func dismiss(_ reason: Reason, phase: HoldSession.Phase, holding: ModifierSet) -> Action {
        switch phase {
        case .showing, .waiting:
            return .suppress(reason, holding: holding)
        case .idle, .suppressed:
            return .none
        }
    }

    private static func poll(
        phase: HoldSession.Phase,
        held: ModifierSet,
        polled: ModifierSet,
        visibleFor: TimeInterval,
        hardTimeout: TimeInterval
    ) -> Action {
        switch phase {
        case .idle:
            return .none
        case .suppressed:
            if polled == held { return .none }
            return .sync(polled)
        case .waiting:
            // A missed release during the short delay must cancel the panel.
            // Do not restart that delay when the polled set merely differs.
            if polled.isEmpty { return .suppress(.modifiersReleased, holding: []) }
            return .none
        case .showing:
            if polled.isEmpty { return .suppress(.modifiersReleased, holding: []) }
            if visibleFor >= hardTimeout { return .suppress(.timedOut, holding: polled) }
            if polled != held { return .sync(polled) }
            return .none
        }
    }
}
