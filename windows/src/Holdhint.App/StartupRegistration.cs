using Microsoft.Win32;

namespace Holdhint;

internal static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Holdhint";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            Log.Warn("Startup setting was not read (" + ex.GetType().Name + ").");
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return;
            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) return;
            key.SetValue(ValueName, "\"" + path + "\"");
        }
        catch (Exception ex)
        {
            Log.Warn("Startup setting was not saved (" + ex.GetType().Name + ").");
        }
    }
}
