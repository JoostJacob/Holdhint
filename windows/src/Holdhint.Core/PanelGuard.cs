namespace Holdhint.Core;

/// <summary>
/// When the hint panel must close.
///
/// Hiding only on a key-up misses the case where that event never arrives
/// (a lost hook message, a swallowed Windows-key release, AltGr). The panel
/// ignores clicks and does not take focus, so it would then stay up until
/// the user quits. These rules are the pure half of the watchdog: poll the
/// real modifier state, and hard-stop the panel after <see cref="HardTimeoutSeconds"/>.
/// </summary>
public static class PanelGuard
{
    public const double PollIntervalSeconds = 0.3;
    public const double HardTimeoutSeconds = 30;

    public enum Reason
    {
        ModifiersReleased,
        TimedOut,
        Clicked,
        Escape,
        ApplicationSwitched,
    }

    public abstract record Signal
    {
        private Signal() { }

        /// <summary><paramref name="VisibleFor"/> is how long this chord's panel has been showing.</summary>
        public sealed record Poll(ModifierSet Polled, double VisibleFor) : Signal;
        public sealed record Click(ModifierSet Polled) : Signal;
        public sealed record EscapeKey(ModifierSet Polled) : Signal;
        public sealed record AppSwitch(ModifierSet Polled) : Signal;

        public bool IsUserDismissal => this is Click or EscapeKey or AppSwitch;
    }

    public abstract record Action
    {
        private Action() { }
        public sealed record Nothing : Action;
        public sealed record Sync(ModifierSet Modifiers) : Action;
        public sealed record Suppress(Reason Reason, ModifierSet Holding) : Action;

        public static readonly Action None = new Nothing();
    }

    public static Action Decide(
        HoldSession.Phase phase,
        ModifierSet held,
        Signal signal,
        double hardTimeout = HardTimeoutSeconds)
    {
        switch (signal)
        {
            case Signal.Click click:
                return Dismiss(Reason.Clicked, phase, click.Polled);
            case Signal.EscapeKey escape:
                return Dismiss(Reason.Escape, phase, escape.Polled);
            case Signal.AppSwitch switched:
                return Dismiss(Reason.ApplicationSwitched, phase, switched.Polled);
            case Signal.Poll poll:
                return OnPoll(phase, held, poll.Polled, poll.VisibleFor, hardTimeout);
            default:
                return Action.None;
        }
    }

    private static Action Dismiss(Reason reason, HoldSession.Phase phase, ModifierSet holding)
    {
        switch (phase)
        {
            case HoldSession.Phase.Showing:
            case HoldSession.Phase.Waiting:
                return new Action.Suppress(reason, holding);
            default:
                return Action.None;
        }
    }

    private static Action OnPoll(
        HoldSession.Phase phase,
        ModifierSet held,
        ModifierSet polled,
        double visibleFor,
        double hardTimeout)
    {
        switch (phase)
        {
            case HoldSession.Phase.Idle:
                return Action.None;
            case HoldSession.Phase.Suppressed:
                if (polled == held) return Action.None;
                return new Action.Sync(polled);
            case HoldSession.Phase.Waiting:
                // A missed release during the short delay must cancel the panel.
                // Do not restart that delay when the polled set merely differs,
                // or a flaky reading could postpone the panel forever.
                if (polled == ModifierSet.None)
                    return new Action.Suppress(Reason.ModifiersReleased, ModifierSet.None);
                return Action.None;
            case HoldSession.Phase.Showing:
                if (polled == ModifierSet.None)
                    return new Action.Suppress(Reason.ModifiersReleased, ModifierSet.None);
                if (visibleFor >= hardTimeout)
                    return new Action.Suppress(Reason.TimedOut, polled);
                if (polled != held)
                    return new Action.Sync(polled);
                return Action.None;
            default:
                return Action.None;
        }
    }
}
