namespace Holdhint.Core;

public static class KeyDisplay
{
    private static readonly Dictionary<string, string> Aliases = BuildAliases();

    /// <summary>Canonical token used for matching. Letters are lowercase. Unknown words become empty.</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var lower = raw.Trim().ToLowerInvariant();
        if (Aliases.TryGetValue(lower, out var mapped)) return mapped;
        var collapsed = lower.Replace(" ", "").Replace("_", "").Replace("-", "");
        if (collapsed != lower && Aliases.TryGetValue(collapsed, out mapped)) return mapped;
        if (lower.Length == 1) return lower;
        if (IsFunctionKey(lower)) return lower;
        return "";
    }

    public static string Label(string token)
    {
        switch (token)
        {
            case "space": return "Space";
            case "tab": return "Tab";
            case "return": return "Enter";
            case "escape": return "Esc";
            case "delete": return "Del";
            case "backspace": return "Backspace";
            case "insert": return "Ins";
            case "up": return "↑";
            case "down": return "↓";
            case "left": return "←";
            case "right": return "→";
            case "pageup": return "PgUp";
            case "pagedown": return "PgDn";
            case "home": return "Home";
            case "end": return "End";
            case "printscreen": return "PrtScn";
            case "pause": return "Pause";
            case "backtick": return "`";
            case "plus":
            case "+": return "+";
            case "minus":
            case "-": return "-";
            default:
                if (token.StartsWith("num", StringComparison.Ordinal) && token.Length > 3)
                    return "Num " + token[3..];
                if (token.Length == 1 && char.IsLetter(token[0]))
                    return token.ToUpperInvariant();
                if (IsFunctionKey(token))
                    return token.ToUpperInvariant();
                return token;
        }
    }

    public static bool Less(Shortcut left, Shortcut right)
    {
        var rank = Rank(left.Key).CompareTo(Rank(right.Key));
        if (rank != 0) return rank < 0;
        var label = string.Compare(Label(left.Key), Label(right.Key), StringComparison.OrdinalIgnoreCase);
        if (label != 0) return label < 0;
        return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int Rank(string key)
    {
        if (key.Length == 1 && char.IsLetter(key[0])) return 0;
        if (IsNavigation(key)) return 1;
        if (key.Length == 1 && char.IsDigit(key[0])) return 2;
        if (IsFunctionKey(key)) return 3;
        return 4;
    }

    private static bool IsNavigation(string key) => key is
        "tab" or "escape" or "return" or "space" or
        "up" or "down" or "left" or "right" or
        "delete" or "backspace" or "insert" or
        "home" or "end" or "pageup" or "pagedown" or "printscreen";

    private static bool IsFunctionKey(string key)
    {
        if (key.Length is < 2 or > 3 || key[0] != 'f') return false;
        return int.TryParse(key.AsSpan(1), out var n) && n is >= 1 and <= 24;
    }

    private static Dictionary<string, string> BuildAliases()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["space"] = "space", ["spacebar"] = "space",
            ["tab"] = "tab",
            ["return"] = "return", ["enter"] = "return", ["eingabe"] = "return",
            ["esc"] = "escape", ["escape"] = "escape",
            ["delete"] = "delete", ["del"] = "delete", ["entf"] = "delete",
            ["backspace"] = "backspace", ["bksp"] = "backspace", ["rücktaste"] = "backspace", ["ruecktaste"] = "backspace",
            ["insert"] = "insert", ["ins"] = "insert", ["einfügen"] = "insert", ["einfg"] = "insert",
            ["up"] = "up", ["uparrow"] = "up", ["arrowup"] = "up",
            ["down"] = "down", ["downarrow"] = "down", ["arrowdown"] = "down",
            ["left"] = "left", ["leftarrow"] = "left", ["arrowleft"] = "left",
            ["right"] = "right", ["rightarrow"] = "right", ["arrowright"] = "right",
            ["pageup"] = "pageup", ["pgup"] = "pageup", ["prior"] = "pageup",
            ["pagedown"] = "pagedown", ["pgdn"] = "pagedown", ["pgdown"] = "pagedown", ["next"] = "pagedown",
            ["home"] = "home", ["pos1"] = "home",
            ["end"] = "end",
            ["printscreen"] = "printscreen", ["prtsc"] = "printscreen", ["prtscn"] = "printscreen", ["prtcsn"] = "printscreen", ["snapshot"] = "printscreen",
            ["pause"] = "pause", ["break"] = "pause",
            ["backtick"] = "backtick", ["grave"] = "backtick", ["`"] = "backtick", ["oem3"] = "backtick",
            ["comma"] = ",", [","] = ",", ["oemcomma"] = ",",
            ["period"] = ".", ["."] = ".", ["dot"] = ".", ["oemperiod"] = ".",
            ["slash"] = "/", ["/"] = "/", ["oem2"] = "/",
            ["backslash"] = "\\", ["\\"] = "\\", ["oem5"] = "\\",
            ["minus"] = "-", ["-"] = "-", ["hyphen"] = "-", ["dash"] = "-", ["subtract"] = "-", ["oemminus"] = "-",
            ["plus"] = "+", ["+"] = "+", ["add"] = "+", ["numpadadd"] = "+",
            // VK_OEM_PLUS is the =/+ key. Without Shift it is "=".
            ["oemplus"] = "=", ["equal"] = "=", ["equals"] = "=", ["="] = "=",
            ["semicolon"] = ";", [";"] = ";", ["oem1"] = ";",
            ["quote"] = "'", ["'"] = "'", ["apostrophe"] = "'", ["oem7"] = "'",
            ["question"] = "?", ["?"] = "?",
            ["leftbracket"] = "[", ["["] = "[", ["oem4"] = "[",
            ["rightbracket"] = "]", ["]"] = "]", ["oem6"] = "]",
        };

        for (var i = 0; i <= 9; i++)
        {
            map["numpad" + i] = "num" + i;
            map["num" + i] = "num" + i;
        }

        return map;
    }
}

public static class KeySort
{
    public static IReadOnlyList<Shortcut> Sorted(IEnumerable<Shortcut> shortcuts)
    {
        var list = shortcuts.ToList();
        list.Sort((a, b) => KeyDisplay.Less(a, b) ? -1 : KeyDisplay.Less(b, a) ? 1 : 0);
        return list;
    }
}
