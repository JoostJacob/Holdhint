using System.Text.Json;

namespace Holdhint.Core;

/// <summary>
/// Known shortcuts for apps whose menus do not expose AcceleratorKey.
/// Chrome, Electron, and the Office ribbon are the usual cases.
/// </summary>
public sealed class AppProfiles
{
    private readonly Dictionary<string, IReadOnlyList<Shortcut>> _byProcess;
    public IReadOnlyList<string> Warnings { get; }
    public bool ParseFailed { get; }

    private AppProfiles(Dictionary<string, IReadOnlyList<Shortcut>> byProcess, IReadOnlyList<string> warnings, bool parseFailed)
    {
        _byProcess = byProcess;
        Warnings = warnings;
        ParseFailed = parseFailed;
    }

    public IReadOnlyList<Shortcut> ForProcess(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return Array.Empty<Shortcut>();
        var name = FileName(fileName);
        return _byProcess.TryGetValue(name, out var list) ? list : Array.Empty<Shortcut>();
    }

    /// <summary>Last path segment. Slashes are handled here so a Windows path can be tested on macOS.</summary>
    public static string FileName(string path)
    {
        var trimmed = path.Trim().TrimEnd('\\', '/');
        var slash = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }

    public static AppProfiles LoadBundled()
    {
        var json = EmbeddedJson.Read("apps.json");
        if (json == null) return Failed("apps.json was not found in the app.");
        return Load(json);
    }

    public static AppProfiles Load(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, JsonRead.Options);
        }
        catch (JsonException ex)
        {
            return Failed("apps.json is not valid: " + ex.Message);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Failed("apps.json must be a JSON object.");
            if (!JsonRead.TryGet(document.RootElement, "profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Array)
                return Failed("apps.json has no profiles list.");

            var warnings = new List<string>();
            var map = new Dictionary<string, List<Shortcut>>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var profile in profiles.EnumerateArray())
            {
                index++;
                if (profile.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add("Profile " + index + " was skipped because it is not an object.");
                    continue;
                }

                var processes = JsonRead.Strings(profile, "processes")?
                    .Select(FileName)
                    .Where(process => process.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                if (processes.Count == 0)
                {
                    warnings.Add("Profile " + index + " was skipped because it names no program.");
                    continue;
                }

                var entries = JsonRead.TryGet(profile, "shortcuts", out var shortcuts) && shortcuts.ValueKind == JsonValueKind.Array
                    ? JsonRead.Entries(shortcuts)
                    : new List<JsonRead.Entry>();
                var (parsed, entryWarnings) = Catalog.ReadEntries(entries, "Profile " + index + " shortcut");
                warnings.AddRange(entryWarnings);
                foreach (var process in processes)
                {
                    if (!map.TryGetValue(process, out var existing))
                    {
                        map[process] = parsed.ToList();
                        continue;
                    }

                    map[process] = Catalog.Merge(existing, parsed).ToList();
                }
            }

            var frozen = map.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<Shortcut>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
            return new AppProfiles(frozen, warnings, parseFailed: false);
        }
    }

    private static AppProfiles Failed(string warning) =>
        new(new Dictionary<string, IReadOnlyList<Shortcut>>(StringComparer.OrdinalIgnoreCase), new[] { warning }, parseFailed: true);
}
