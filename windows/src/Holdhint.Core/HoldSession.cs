namespace Holdhint.Core;

/// <summary>When the overlay appears. Arming the timer replaces any timer already running.</summary>
public sealed class HoldSession
{
    public enum Phase
    {
        Idle,
        Waiting,
        Showing,
        Suppressed,
    }

    public abstract record Effect
    {
        private Effect() { }
        public sealed record ArmTimer : Effect;
        public sealed record DisarmTimer : Effect;
        public sealed record ShowPanel(ModifierSet Modifiers) : Effect;
        public sealed record HidePanel : Effect;

        public static readonly Effect Arm = new ArmTimer();
        public static readonly Effect Disarm = new DisarmTimer();
        public static readonly Effect Hide = new HidePanel();
        public static Effect Show(ModifierSet modifiers) => new ShowPanel(modifiers);
    }

    private static readonly Effect[] None = Array.Empty<Effect>();

    public ModifierSet Held { get; private set; } = ModifierSet.None;
    public Phase Current { get; private set; } = Phase.Idle;

    public IReadOnlyList<Effect> ModifierChange(ModifierSet set)
    {
        if (set == Held) return None;
        var previous = Current;
        Held = set;
        if (set == ModifierSet.None)
        {
            Current = Phase.Idle;
            var effects = new List<Effect>(2);
            if (previous == Phase.Waiting) effects.Add(Effect.Disarm);
            if (previous == Phase.Showing) effects.Add(Effect.Hide);
            return effects;
        }

        switch (previous)
        {
            case Phase.Showing:
                Current = Phase.Showing;
                return new[] { Effect.Show(set) };
            default:
                Current = Phase.Waiting;
                return new[] { Effect.Arm };
        }
    }

    public IReadOnlyList<Effect> KeyDown()
    {
        switch (Current)
        {
            case Phase.Idle:
            case Phase.Suppressed:
                return None;
            case Phase.Waiting:
                Current = Phase.Suppressed;
                return new[] { Effect.Disarm };
            case Phase.Showing:
                Current = Phase.Suppressed;
                return new[] { Effect.Hide };
            default:
                return None;
        }
    }

    public IReadOnlyList<Effect> TimerFired()
    {
        if (Current != Phase.Waiting || Held == ModifierSet.None) return None;
        Current = Phase.Showing;
        return new[] { Effect.Show(Held) };
    }

    /// <summary>
    /// Hide the panel, if it is up, and ignore this chord until it changes.
    /// An empty set returns to idle so the next press can show the panel.
    /// </summary>
    public IReadOnlyList<Effect> Suppress(ModifierSet holding)
    {
        var previous = Current;
        Held = holding;
        Current = holding == ModifierSet.None ? Phase.Idle : Phase.Suppressed;
        var effects = new List<Effect>(2);
        if (previous == Phase.Waiting) effects.Add(Effect.Disarm);
        if (previous == Phase.Showing) effects.Add(Effect.Hide);
        return effects;
    }
}
