namespace Holdhint.Core;

/// <summary>Applies <see cref="PanelGuard"/> decisions to a <see cref="HoldSession"/>.</summary>
public sealed class HintEngine
{
    private HoldSession _session = new();

    public HoldSession.Phase Phase => _session.Current;
    public ModifierSet Held => _session.Held;

    public IReadOnlyList<HoldSession.Effect> ModifiersChanged(ModifierSet set) => _session.ModifierChange(set);

    public IReadOnlyList<HoldSession.Effect> NonModifierDown() => _session.KeyDown();

    public IReadOnlyList<HoldSession.Effect> DelayElapsed() => _session.TimerFired();

    public IReadOnlyList<HoldSession.Effect> Guard(PanelGuard.Signal signal)
    {
        return PanelGuard.Decide(_session.Current, _session.Held, signal) switch
        {
            PanelGuard.Action.Sync sync => _session.ModifierChange(sync.Modifiers),
            PanelGuard.Action.Suppress suppress => _session.Suppress(suppress.Holding),
            _ => Array.Empty<HoldSession.Effect>(),
        };
    }

    public void Reset() => _session = new HoldSession();
}
