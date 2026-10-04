using System.Windows.Forms;
using Holdhint.Core;

namespace Holdhint;

internal static class Program
{
    static Mutex? _mutex;

    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
            Log.Error("Error: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Error("Stop: " + ex.GetType().Name + ": " + ex.Message);
        };

        if (args.Any(arg => arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            return SelfTest.Run();

        if (!TryParsePreview(args, out var preview, out var error))
        {
            MessageBox.Show(error, "Holdhint", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        if (!AcquireSingleInstance())
        {
            MessageBox.Show(
                "Holdhint is already running. Look for its icon near the clock.",
                "Holdhint",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        try
        {
            Application.Run(new AppHost(preview));
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed: " + ex.GetType().Name + ": " + ex.Message);
            MessageBox.Show(
                "Holdhint could not start.\n\n" + ex.Message,
                "Holdhint",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            ReleaseSingleInstance();
        }
    }

    static bool TryParsePreview(string[] args, out ModifierSet? set, out string? error)
    {
        set = null;
        error = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--preview", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            {
                set = ModifierSet.Win;
                return true;
            }

            var names = args[i + 1].Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!Modifiers.TryParse(names, out var parsed, out error))
            {
                error ??= "Those modifier names are not recognized.";
                return false;
            }

            set = parsed;
            return true;
        }

        return true;
    }

    static bool AcquireSingleInstance()
    {
        _mutex = new Mutex(false, @"Local\Holdhint");
        try
        {
            return _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    static void ReleaseSingleInstance()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* already released, or we never owned it */ }
        _mutex?.Dispose();
        _mutex = null;
    }
}
