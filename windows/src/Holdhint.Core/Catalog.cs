using System.Text.Json;

namespace Holdhint.Core;

public sealed class Catalog
{
    public double DelaySeconds { get; }
    public IReadOnlyList<Shortcut> Shortcuts { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool ParseFailed { get; }

    public Catalog(double delaySeconds, IReadOnlyList<Shortcut> shortcuts, IReadOnlyList<string> warnings, bool parseFailed)
    {
        DelaySeconds = delaySeconds;
        Shortcuts = shortcuts;
        Warnings = warnings;
        ParseFailed = parseFailed;
    }

    public IReadOnlyList<Shortcut> Matching(ModifierSet modifiers)
    {
        return KeySort.Sorted(Shortcuts.Where(shortcut => shortcut.Modifiers == modifiers));
    }

    /// <summary>Later rows replace earlier rows that use the same key. The result is sorted.</summary>
    public static IReadOnlyList<Shortcut> Merge(IReadOnlyList<Shortcut> first, IReadOnlyList<Shortcut> second)
    {
        var byKey = new Dictionary<string, Shortcut>(StringComparer.Ordinal);
        foreach (var shortcut in first) byKey[shortcut.Key] = shortcut;
        foreach (var shortcut in second) byKey[shortcut.Key] = shortcut;
        return KeySort.Sorted(byKey.Values);
    }

    public Catalog WithExtraWarning(string warning)
    {
        var warnings = new List<string> { warning };
        warnings.AddRange(Warnings);
        return new Catalog(DelaySeconds, Shortcuts, warnings, ParseFailed);
    }

    public static string? BundledText() => EmbeddedJson.Read("shortcuts.json");

    public static Catalog LoadBundled()
    {
        var json = BundledText();
        if (json == null) return Failed("shortcuts.json was not found in the app.");
        return Load(json);
    }

    public static Catalog Load(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, JsonRead.Options);
        }
        catch (JsonException ex)
        {
            return Failed("shortcuts.json is not valid: " + ex.Message);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Failed("shortcuts.json must be a JSON object.");

            var root = document.RootElement;
            double? delay = null;
            if (JsonRead.TryGet(root, "delaySeconds", out var delayElement)
                && delayElement.ValueKind == JsonValueKind.Number
                && delayElement.TryGetDouble(out var number))
            {
                delay = number;
            }

            if (!JsonRead.TryGet(root, "shortcuts", out var list) || list.ValueKind != JsonValueKind.Array)
                return Failed("shortcuts.json has no shortcuts list.");

            var (shortcuts, warnings) = ReadEntries(JsonRead.Entries(list), "Shortcut");
            return new Catalog(ClampDelay(delay), shortcuts, warnings, parseFailed: false);
        }
    }

    public static double ClampDelay(double? value)
    {
        if (value == null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return 0.5;
        return Math.Min(3, Math.Max(0.1, value.Value));
    }

    internal static (List<Shortcut> Shortcuts, List<string> Warnings) ReadEntries(IReadOnlyList<JsonRead.Entry> entries, string labelPrefix)
    {
        var shortcuts = new List<Shortcut>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var label = labelPrefix + " " + (index + 1);
            if (!Modifiers.TryParse(entry.Modifiers, out var modifiers, out var error))
            {
                warnings.Add(label + " was skipped because " + (error ?? "a modifier is missing") + ".");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                warnings.Add(label + " was skipped because it has no key.");
                continue;
            }

            var key = KeyDisplay.Normalize(entry.Key);
            if (key.Length == 0)
            {
                warnings.Add(label + " was skipped because its key is not recognized.");
                continue;
            }

            var title = entry.Title?.Trim() ?? "";
            if (title.Length == 0)
            {
                warnings.Add(label + " (" + key + ") was skipped because it has no title.");
                continue;
            }

            var identity = ((int)modifiers) + "|" + key;
            if (!seen.Add(identity))
            {
                warnings.Add(label + " repeats " + KeyDisplay.Label(key) + " and was skipped.");
                continue;
            }

            var note = entry.Note?.Trim();
            shortcuts.Add(new Shortcut(modifiers, key, title, string.IsNullOrEmpty(note) ? null : note));
        }

        return (shortcuts, warnings);
    }

    private static Catalog Failed(string warning) =>
        new(0.5, Array.Empty<Shortcut>(), new[] { warning }, parseFailed: true);
}

internal static class JsonRead
{
    public static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public sealed class Entry
    {
        public List<string>? Modifiers { get; init; }
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Note { get; init; }
    }

    public static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public static List<Entry> Entries(JsonElement array)
    {
        var list = new List<Entry>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                list.Add(new Entry());
                continue;
            }

            list.Add(new Entry
            {
                Modifiers = Strings(item, "modifiers"),
                Key = Text(item, "key"),
                Title = Text(item, "title"),
                Note = Text(item, "note"),
            });
        }

        return list;
    }

    public static string? Text(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public static List<string>? Strings(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is string text)
                list.Add(text);
        }

        return list;
    }
}

internal static class EmbeddedJson
{
    public static string? Read(string fileName)
    {
        var assembly = typeof(Catalog).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(fileName, StringComparison.Ordinal));
        if (name == null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return text.Length == 0 ? null : text;
    }
}
