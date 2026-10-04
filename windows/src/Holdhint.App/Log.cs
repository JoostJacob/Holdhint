namespace Holdhint;

/// <summary>
/// A small diagnostic log. It records startup, hook, and menu-scan failures.
/// It never records which keys were pressed.
/// </summary>
internal static class Log
{
    const int RotateAtBytes = 200_000;
    static readonly object Gate = new();

    public static void Info(string message) => Write("info", message);

    public static void Warn(string message) => Write("warn", message);

    public static void Error(string message) => Write("error", message);

    static void Write(string level, string message)
    {
        try
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message.Replace('\r', ' ').Replace('\n', ' ');
            lock (Gate)
            {
                var path = AppPaths.LogPath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                if (File.Exists(path) && new FileInfo(path).Length > RotateAtBytes)
                {
                    var previous = path + ".old";
                    File.Delete(previous);
                    File.Move(path, previous);
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must not take the hook or the tray down.
        }
    }
}
