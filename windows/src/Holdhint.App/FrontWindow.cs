using System.Diagnostics;
using System.Text;
using Holdhint.Core;

namespace Holdhint;

internal readonly record struct FrontApp(int ProcessId, string FileName, string DisplayName);

internal static class FrontWindow
{
    const uint ProcessQueryLimitedInformation = 0x1000;

    static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["explorer.exe"] = "File Explorer",
        ["notepad.exe"] = "Notepad",
        ["chrome.exe"] = "Google Chrome",
        ["msedge.exe"] = "Microsoft Edge",
        ["brave.exe"] = "Brave",
        ["opera.exe"] = "Opera",
        ["vivaldi.exe"] = "Vivaldi",
        ["firefox.exe"] = "Firefox",
        ["Code.exe"] = "Visual Studio Code",
        ["Code - Insiders.exe"] = "Visual Studio Code",
        ["WINWORD.EXE"] = "Microsoft Word",
        ["EXCEL.EXE"] = "Microsoft Excel",
        ["POWERPNT.EXE"] = "PowerPoint",
        ["OUTLOOK.EXE"] = "Outlook",
        ["WindowsTerminal.exe"] = "Windows Terminal",
        ["wt.exe"] = "Windows Terminal",
        ["cmd.exe"] = "Command Prompt",
        ["powershell.exe"] = "PowerShell",
        ["pwsh.exe"] = "PowerShell",
    };

    static readonly Dictionary<int, FrontApp> Cache = new();

    public static FrontApp Current() => Describe(NativeMethods.GetForegroundWindow());

    public static FrontApp Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return default;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pidValue);
        var pid = unchecked((int)pidValue);
        if (pid == 0) return default;
        if (Cache.TryGetValue(pid, out var cached)) return cached;

        var path = QueryPath(pidValue);
        if (path.Length == 0) return new FrontApp(pid, "", "");

        var file = AppProfiles.FileName(path);
        var app = new FrontApp(pid, file, Friendly(file, path));
        if (Cache.Count > 64) Cache.Clear();
        Cache[pid] = app;
        return app;
    }

    static string QueryPath(uint pid)
    {
        var handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size)) return "";
            return buffer.ToString();
        }
        catch
        {
            return "";
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    static string Friendly(string file, string path)
    {
        if (file.Length > 0 && FriendlyNames.TryGetValue(file, out var known)) return known;
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            if (!string.IsNullOrEmpty(description) && description.Length <= 48)
                return description;
        }
        catch
        {
            // A path we cannot read still gets a name from the file.
        }

        if (file.Length == 0) return "";
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
    }
}
