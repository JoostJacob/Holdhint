namespace Holdhint;

internal static class AppPaths
{
    public static string RoamingDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Holdhint");

    public static string LocalDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Holdhint");

    public static string ShortcutsPath => Path.Combine(RoamingDirectory, "shortcuts.json");

    public static string SettingsPath => Path.Combine(RoamingDirectory, "settings.json");

    public static string LogPath => Path.Combine(LocalDirectory, "holdhint.log");

    public static string SelfTestPath => Path.Combine(LocalDirectory, "self-test.txt");
}

internal static class AppVersion
{
    public static string Text
    {
        get
        {
            var version = typeof(AppVersion).Assembly.GetName().Version;
            return version == null ? "1.2.1" : version.Major + "." + version.Minor + "." + version.Build;
        }
    }
}
