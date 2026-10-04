using System.Runtime.InteropServices;
using System.Windows.Forms;
using Holdhint.Core;

namespace Holdhint;

internal static class SelfTest
{
    public static int Run()
    {
        var lines = new List<string> { "Holdhint " + AppVersion.Text + " self-test" };
        var failed = false;

        void Check(string name, bool ok, string? detail = null)
        {
            if (!ok) failed = true;
            var line = (ok ? "PASS  " : "FAIL  ") + name;
            if (!string.IsNullOrEmpty(detail)) line += " — " + detail;
            lines.Add(line);
        }

        try
        {
            CheckCatalog(Check);
            CheckParser(Check);
            CheckPainter(Check);
            CheckDpi(Check);
            CheckOverlayAndHook(Check);
        }
        catch (Exception ex)
        {
            failed = true;
            lines.Add("FAIL  unexpected " + ex.GetType().Name + ": " + ex.Message);
        }

        lines.Add(failed ? "Result: FAILED" : "Result: passed");
        try
        {
            var directory = Path.GetDirectoryName(AppPaths.SelfTestPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllLines(AppPaths.SelfTestPath, lines);
            lines.Add("Wrote " + AppPaths.SelfTestPath);
        }
        catch (Exception ex)
        {
            lines.Add("Could not write the result file (" + ex.GetType().Name + ").");
        }

        TryWriteConsole(lines);
        var summary = failed
            ? "Holdhint self-test failed.\n\n" + string.Join("\n", lines.Where(line => line.StartsWith("FAIL")))
            : "Holdhint self-test passed.";
        MessageBox.Show(summary, "Holdhint", MessageBoxButtons.OK, failed ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        return failed ? 1 : 0;
    }

    static void CheckCatalog(Action<string, bool, string?> check)
    {
        var catalog = Catalog.LoadBundled();
        check("Built-in list loads", !catalog.ParseFailed && catalog.Warnings.Count == 0 && catalog.Shortcuts.Count > 70,
            catalog.Shortcuts.Count + " shortcuts");
        check("Win+E opens Explorer", catalog.Matching(ModifierSet.Win).Any(row => row.Key == "e" && row.Title.Contains("Explorer", StringComparison.OrdinalIgnoreCase)), null);
        check("Win+Shift+S snips to the clipboard",
            catalog.Matching(ModifierSet.Win | ModifierSet.Shift).Any(row => row.Key == "s" && row.Title.Contains("clipboard", StringComparison.OrdinalIgnoreCase)), null);
        check("Ctrl+Shift+Esc opens Task Manager",
            catalog.Matching(ModifierSet.Control | ModifierSet.Shift).Any(row => row.Key == "escape" && row.Title.Contains("Task Manager", StringComparison.OrdinalIgnoreCase)), null);
        check("Alt+F4 closes the window",
            catalog.Matching(ModifierSet.Alt).Any(row => row.Key == "f4" && row.Title.Contains("Close", StringComparison.OrdinalIgnoreCase)), null);

        var profiles = AppProfiles.LoadBundled();
        check("Chrome profile has New Tab",
            profiles.ForProcess("chrome.exe").Any(row => row.Modifiers == ModifierSet.Control && row.Key == "t" && row.Title.Contains("tab", StringComparison.OrdinalIgnoreCase)), null);
        var firefox = profiles.ForProcess("firefox.exe");
        check("Firefox private window is Ctrl+Shift+P",
            firefox.Any(row => row.Modifiers == (ModifierSet.Control | ModifierSet.Shift) && row.Key == "p" && row.Title.Contains("private", StringComparison.OrdinalIgnoreCase))
            && !firefox.Any(row => row.Modifiers == (ModifierSet.Control | ModifierSet.Shift) && row.Key == "n"), null);
    }

    static void CheckParser(Action<string, bool, string?> check)
    {
        check("Ctrl+S parses", AcceleratorParser.TryParse("Ctrl+S", out var modifiers, out var key) && modifiers == ModifierSet.Control && key == "s", null);
        check("Ctrl++ parses", AcceleratorParser.TryParse("Ctrl++", out modifiers, out key) && modifiers == ModifierSet.Control && key == "+", null);
        check("Ctrl+, parses", AcceleratorParser.TryParse("Ctrl+,", out modifiers, out key) && modifiers == ModifierSet.Control && key == ",", null);
        check("Alt, F is rejected", !AcceleratorParser.TryParse("Alt, F", out _, out _), null);
        check("Command+S is rejected", !AcceleratorParser.TryParse("Command+S", out _, out _), null);
    }

    static void CheckPainter(Action<string, bool, string?> check)
    {
        using var bitmap = HudPainter.Paint(
            new[] { "Win", "Shift" },
            "Release to dismiss",
            new[]
            {
                new HudRow("S", "Snip the screen to the clipboard", "Drag a region."),
                new HudRow("M", "Restore minimized windows", null),
            },
            1,
            900,
            700);
        check("Panel bitmap has a size", bitmap.Width > 200 && bitmap.Height > 80, bitmap.Width + "×" + bitmap.Height);
        check("Panel bitmap is not invisible", HudPainter.HasOpaquePixel(bitmap), null);
    }

    static void CheckDpi(Action<string, bool, string?> check)
    {
        var awareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetThreadDpiAwarenessContext());
        // 0 is unaware. A blurry or wrongly sized panel follows from that.
        check("DPI awareness", awareness is 1 or 2, "value " + awareness);
    }

    static void CheckOverlayAndHook(Action<string, bool, string?> check)
    {
        var message = new MessageWindow();
        var overlay = new OverlayWindow();
        var hooksInstalled = false;
        try
        {
            message.Create();
            overlay.Create();
            var style = overlay.ExtendedStyle;
            var required = WindowStyles.OverlayExStyle;
            check("Overlay is layered and click-through", (style & required) == required, "style " + style.ToString("X8"));
            check("Overlay stays off the taskbar", (style & WindowStyles.WsExAppWindow) == 0, null);

            using var bitmap = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                graphics.Clear(System.Drawing.Color.FromArgb(255, 18, 20, 26));
            var drawn = overlay.Present(bitmap, -32000, -32000);
            check("Overlay accepts a bitmap", drawn, null);
            style = overlay.ExtendedStyle;
            check("Overlay keeps its extended style", (style & required) == required, "style " + style.ToString("X8"));
            overlay.HidePanel();

            hooksInstalled = InputHooks.Install(message.Handle);
            check("Keyboard hook installs", hooksInstalled, null);
        }
        finally
        {
            if (hooksInstalled || InputHooks.KeyboardInstalled) InputHooks.Uninstall();
            overlay.HidePanel();
        }
    }

    static void TryWriteConsole(IReadOnlyList<string> lines)
    {
        try
        {
            if (!NativeMethods.AttachConsole(NativeMethods.AttachParentProcess)) return;
            var raw = GetStdHandle(-11);
            if (raw == IntPtr.Zero || raw == new IntPtr(-1)) return;
            using var handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(raw, ownsHandle: false);
            using var stream = new FileStream(handle, FileAccess.Write);
            using var writer = new StreamWriter(stream) { AutoFlush = true };
            foreach (var line in lines) writer.WriteLine(line);
        }
        catch
        {
            // The message box and the result file are the outputs that always work.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);
}
