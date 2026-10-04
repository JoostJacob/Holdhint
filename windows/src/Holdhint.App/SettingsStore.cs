using System.Text.Json;

namespace Holdhint;

internal sealed class SettingsStore
{
    public bool HintsEnabled { get; set; } = true;

    public static SettingsStore Load()
    {
        var store = new SettingsStore();
        try
        {
            var path = AppPaths.SettingsPath;
            if (!File.Exists(path)) return store;
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return store;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("hintsEnabled", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    store.HintsEnabled = property.Value.GetBoolean();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Settings were not read (" + ex.GetType().Name + "). Hints stay on.");
        }

        return store;
    }

    public void Save()
    {
        try
        {
            var directory = AppPaths.RoamingDirectory;
            Directory.CreateDirectory(directory);
            var json = HintsEnabled
                ? "{\n  \"hintsEnabled\": true\n}\n"
                : "{\n  \"hintsEnabled\": false\n}\n";
            File.WriteAllText(AppPaths.SettingsPath, json);
        }
        catch (Exception ex)
        {
            Log.Warn("Settings were not saved (" + ex.GetType().Name + ").");
        }
    }
}
