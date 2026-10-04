import Foundation

/// When the overlay appears. `armTimer` replaces any timer already running.
public struct HoldSession: Sendable {
    public enum Phase: Equatable, Sendable {
        case idle
        case waiting
        case showing
        case suppressed
    }

    public enum Effect: Equatable, Sendable {
        case armTimer
        case disarmTimer
        case show(ModifierSet)
        case hide
    }

    public private(set) var held: ModifierSet = []
    public private(set) var phase: Phase = .idle

    public init() {}

    public mutating func modifierChange(_ set: ModifierSet) -> [Effect] {
        if set == held { return [] }
        let previous = phase
        held = set
        if set.isEmpty {
            phase = .idle
            var effects: [Effect] = []
            if previous == .waiting { effects.append(.disarmTimer) }
            if previous == .showing { effects.append(.hide) }
            return effects
        }
        switch previous {
        case .showing:
            phase = .showing
            return [.show(set)]
        case .waiting, .idle, .suppressed:
            phase = .waiting
            return [.armTimer]
        }
    }

    public mutating func keyDown() -> [Effect] {
        switch phase {
        case .idle, .suppressed:
            return []
        case .waiting:
            phase = .suppressed
            return [.disarmTimer]
        case .showing:
            phase = .suppressed
            return [.hide]
        }
    }

    public mutating func timerFired() -> [Effect] {
        guard phase == .waiting, !held.isEmpty else { return [] }
        phase = .showing
        return [.show(held)]
    }

    /// Hide the panel, if it is up, and ignore `set` until the chord changes.
    /// An empty set returns to idle so the next press can show the panel.
    public mutating func suppress(holding set: ModifierSet) -> [Effect] {
        let previous = phase
        held = set
        phase = set.isEmpty ? .idle : .suppressed
        var effects: [Effect] = []
        if previous == .waiting { effects.append(.disarmTimer) }
        if previous == .showing { effects.append(.hide) }
        return effects
    }
}
