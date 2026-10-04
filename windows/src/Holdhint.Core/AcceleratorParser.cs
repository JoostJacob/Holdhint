namespace Holdhint.Core;

/// <summary>
/// Reads shortcut strings from menus. Windows UI Automation puts them in AcceleratorKey,
/// for example "Ctrl+Shift+N", "Strg+S", or "Ctrl++".
/// A comma means a sequence (Alt, then F), which is not a chord, so those are rejected.
/// A bare access key such as "F" is also rejected: it only works once a menu is already open.
/// </summary>
public static class AcceleratorParser
{
    public static bool TryParse(string? text, out ModifierSet modifiers, out string key)
    {
        modifiers = ModifierSet.None;
        key = "";
        if (string.IsNullOrWhiteSpace(text)) return false;

        var raw = text.Trim();
        // "Alt, F" is a sequence. "Ctrl+," is a chord whose key is the comma.
        if (raw.Contains(','))
        {
            var plus = raw.LastIndexOf('+');
            var keyPart = plus >= 0 ? raw[(plus + 1)..].Trim() : raw;
            if (keyPart != ",") return false;
        }

        if (raw.Contains(" then ", StringComparison.OrdinalIgnoreCase)) return false;

        string? forcedKey = null;
        var body = raw;
        if (body.EndsWith("++", StringComparison.Ordinal))
        {
            forcedKey = "+";
            body = body[..^1].TrimEnd();
            // "Ctrl++" is now "Ctrl+". Trim the separator that the forced key replaced.
            if (body.EndsWith('+')) body = body[..^1];
        }

        var parts = body.Split('+').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        if (forcedKey == null)
        {
            if (parts.Count < 2) return false;
            forcedKey = parts[^1];
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count == 0 || parts.Count > 4) return false;
        if (!Modifiers.TryParse(parts, out modifiers, out _)) return false;

        key = KeyDisplay.Normalize(forcedKey);
        if (key.Length == 0) return false;
        if (IsModifierWord(key)) return false;
        return true;
    }

    private static bool IsModifierWord(string key) => key is "control" or "alt" or "shift" or "win";
}
