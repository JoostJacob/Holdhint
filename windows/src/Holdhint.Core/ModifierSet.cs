namespace Holdhint.Core;

/// <summary>
/// Ctrl, Alt, Shift, and the Windows key. Caps Lock, Num Lock, and the Menu key are not modifiers.
/// Fn never reaches Windows as a normal key, so it is not tracked.
/// </summary>
[Flags]
public enum ModifierSet
{
    None = 0,
    Control = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3,
}

public static class Modifiers
{
    /// <summary>Ctrl, Alt, Shift, Win. Same reading order as a shortcut written in a menu.</summary>
    public static IReadOnlyList<string> Labels(ModifierSet set)
    {
        var parts = new List<string>(4);
        if ((set & ModifierSet.Control) != 0) parts.Add("Ctrl");
        if ((set & ModifierSet.Alt) != 0) parts.Add("Alt");
        if ((set & ModifierSet.Shift) != 0) parts.Add("Shift");
        if ((set & ModifierSet.Win) != 0) parts.Add("Win");
        return parts;
    }

    public static string Symbols(ModifierSet set) => string.Join(" ", Labels(set));

    public static ModifierSet FromState(bool control, bool alt, bool shift, bool win)
    {
        var set = ModifierSet.None;
        if (control) set |= ModifierSet.Control;
        if (alt) set |= ModifierSet.Alt;
        if (shift) set |= ModifierSet.Shift;
        if (win) set |= ModifierSet.Win;
        return set;
    }

    /// <summary>
    /// Names accepted in shortcuts.json. <c>fn</c> and <c>command</c> are rejected on purpose:
    /// they are Mac keys, and treating them as Win would list the wrong shortcuts.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string>? names, out ModifierSet set, out string? error)
    {
        set = ModifierSet.None;
        error = null;
        if (names == null || names.Count == 0)
        {
            error = "a modifier is missing";
            return false;
        }

        foreach (var raw in names)
        {
            var name = raw.Trim().ToLowerInvariant();
            switch (name)
            {
                case "control":
                case "ctrl":
                case "ctl":
                case "strg":
                case "steuerung":
                    set |= ModifierSet.Control;
                    break;
                case "alt":
                case "opt":
                case "option":
                    set |= ModifierSet.Alt;
                    break;
                case "shift":
                case "umschalt":
                case "maj":
                case "mayus":
                case "mayús":
                case "maiusc":
                    set |= ModifierSet.Shift;
                    break;
                case "win":
                case "windows":
                case "winkey":
                case "super":
                case "meta":
                case "windowskey":
                case "windowstoets":
                    set |= ModifierSet.Win;
                    break;
                case "altgr":
                    // Menus name the key AltGr. Windows reports it as Ctrl+Alt.
                    set |= ModifierSet.Control | ModifierSet.Alt;
                    break;
                case "fn":
                case "function":
                case "globe":
                    error = "fn is not a Windows modifier";
                    return false;
                case "command":
                case "cmd":
                case "⌘":
                    error = "command is not a Windows modifier";
                    return false;
                default:
                    error = "unknown modifier \"" + raw.Trim() + "\"";
                    return false;
            }
        }

        if (set == ModifierSet.None)
        {
            error = "a modifier is missing";
            return false;
        }

        return true;
    }
}
