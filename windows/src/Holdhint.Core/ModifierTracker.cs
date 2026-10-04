namespace Holdhint.Core;

/// <summary>
/// One poll of the OS modifier keys. Left and right Windows keys are separate:
/// there is no combined Windows-key virtual key.
/// </summary>
public readonly record struct ModifierReading(
    bool Control = false,
    bool LeftControl = false,
    bool RightControl = false,
    bool Alt = false,
    bool LeftAlt = false,
    bool RightAlt = false,
    bool Shift = false,
    bool LeftShift = false,
    bool RightShift = false,
    bool LeftWin = false,
    bool RightWin = false)
{
    public bool IsDown(int vk) => vk switch
    {
        VirtualKeys.Control => Control,
        VirtualKeys.LeftControl => LeftControl,
        VirtualKeys.RightControl => RightControl,
        VirtualKeys.Menu => Alt,
        VirtualKeys.LeftMenu => LeftAlt,
        VirtualKeys.RightMenu => RightAlt,
        VirtualKeys.Shift => Shift,
        VirtualKeys.LeftShift => LeftShift,
        VirtualKeys.RightShift => RightShift,
        VirtualKeys.LeftWin => LeftWin,
        VirtualKeys.RightWin => RightWin,
        _ => false,
    };

    public ModifierSet ToSet() => Modifiers.FromState(
        Control || LeftControl || RightControl,
        Alt || LeftAlt || RightAlt,
        Shift || LeftShift || RightShift,
        LeftWin || RightWin);
}

/// <summary>
/// Modifier keys Holdhint believes are held.
///
/// The low-level hook runs before Windows updates <c>GetAsyncKeyState</c>.
/// Eating a Windows-key release (so the Start menu stays closed) therefore
/// leaves that key down in every later poll. A swallowed release is recorded
/// here as up, and a poll that still says down cannot put it back. The next
/// real key-down, or a poll that really says up, clears that override.
/// Left and right Windows keys are tracked apart so one side can be released
/// while the other is still held.
/// </summary>
public sealed class ModifierTracker
{
    static readonly int[] Tracked =
    {
        VirtualKeys.Control, VirtualKeys.LeftControl, VirtualKeys.RightControl,
        VirtualKeys.Menu, VirtualKeys.LeftMenu, VirtualKeys.RightMenu,
        VirtualKeys.Shift, VirtualKeys.LeftShift, VirtualKeys.RightShift,
        VirtualKeys.LeftWin, VirtualKeys.RightWin,
    };

    readonly HashSet<int> _down = new();
    readonly HashSet<int> _freshDown = new();

    // Start by not trusting a Windows key that is already down. A previous run may
    // have swallowed its release, so the OS bit is still set when this process starts.
    // The first poll that sees the key up, or a real key-down, accepts later readings.
    readonly HashSet<int> _ignoreAsyncDown = new()
    {
        VirtualKeys.LeftWin,
        VirtualKeys.RightWin,
    };

    public ModifierSet Current { get; private set; } = ModifierSet.None;

    public ModifierSet Observe(int vk, bool up, bool swallowed)
    {
        if (!VirtualKeys.IsModifier(vk)) return Current;
        if (!up)
        {
            _down.Add(vk);
            _freshDown.Add(vk);
            _ignoreAsyncDown.Remove(vk);
        }
        else
        {
            _down.Remove(vk);
            _freshDown.Remove(vk);
            if (swallowed && VirtualKeys.IsWin(vk))
                _ignoreAsyncDown.Add(vk);
            else
                _ignoreAsyncDown.Remove(vk);
        }

        Current = Compose();
        return Current;
    }

    /// <summary>
    /// A swallowed release that never reached the event queue. The hook counts
    /// these because a full queue must not lose the only proof that the key came up.
    /// </summary>
    public void NoteSwallowedWinUps(int left, int right)
    {
        for (var i = 0; i < left; i++) Observe(VirtualKeys.LeftWin, up: true, swallowed: true);
        for (var i = 0; i < right; i++) Observe(VirtualKeys.RightWin, up: true, swallowed: true);
    }

    /// <summary>
    /// Re-read every modifier. A key-down observed since the previous resync is kept
    /// for this one pass: the hook may have run before the OS bit flipped. After that,
    /// the OS reading wins, except a Windows key whose release we swallowed.
    /// </summary>
    public ModifierSet Resync(ModifierReading reading)
    {
        foreach (var vk in Tracked)
        {
            if (_freshDown.Contains(vk))
            {
                _down.Add(vk);
                continue;
            }

            var asyncDown = reading.IsDown(vk);
            if (!asyncDown)
            {
                _down.Remove(vk);
                _ignoreAsyncDown.Remove(vk);
                continue;
            }

            if (_ignoreAsyncDown.Contains(vk))
            {
                _down.Remove(vk);
                continue;
            }

            _down.Add(vk);
        }

        _freshDown.Clear();
        Current = Compose();
        return Current;
    }

    ModifierSet Compose()
    {
        bool Down(int vk) => _down.Contains(vk);
        return Modifiers.FromState(
            Down(VirtualKeys.Control) || Down(VirtualKeys.LeftControl) || Down(VirtualKeys.RightControl),
            Down(VirtualKeys.Menu) || Down(VirtualKeys.LeftMenu) || Down(VirtualKeys.RightMenu),
            Down(VirtualKeys.Shift) || Down(VirtualKeys.LeftShift) || Down(VirtualKeys.RightShift),
            Down(VirtualKeys.LeftWin) || Down(VirtualKeys.RightWin));
    }
}

/// <summary>
/// Swallow the Windows-key release after the panel was shown for a chord that
/// includes that key, so Start does not open. Stay armed when a letter hides
/// the panel while the key is still held (Win+E). Clear it when our reconciled
/// state says the key is up. A raw poll that is stuck down must not keep it armed,
/// or every later release is swallowed too and the key can never come up.
/// </summary>
public sealed class WinReleaseGate
{
    public bool Armed { get; private set; }

    public void Note(bool panelShowingWinChord, bool winHeld)
    {
        if (!winHeld) Armed = false;
        else if (panelShowingWinChord) Armed = true;
    }
}
