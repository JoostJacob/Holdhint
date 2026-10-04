using System.Runtime.InteropServices;
using System.Windows.Automation;
using Holdhint.Core;

namespace Holdhint;

/// <summary>
/// Reads AcceleratorKey from the front window. It does not Expand, Invoke, or focus anything.
/// Chrome, Electron, and the Office ribbon often expose nothing. The caller keeps the built-in list.
/// One hung call is abandoned for the rest of the session by the UI thread; this method itself
/// stops walking after a few hundred nodes.
/// </summary>
internal static class MenuReader
{
    public static IReadOnlyList<Shortcut> Read(IntPtr hwnd)
    {
        var found = new List<Shortcut>();
        if (hwnd == IntPtr.Zero) return found;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var started = Environment.TickCount64;
        AutomationElement root;
        try
        {
            root = AutomationElement.FromHandle(hwnd);
        }
        catch
        {
            return found;
        }

        var walker = TreeWalker.ControlViewWalker;
        var nodes = 0;
        var menuBar = FindMenuBar(root, walker);
        if (menuBar != null)
            Walk(menuBar, walker, found, seen, depth: 0, maxDepth: 6, ref nodes, maxNodes: 400, started, budgetMs: 400);

        if (found.Count < 3 && Environment.TickCount64 - started < 250)
        {
            nodes = 0;
            Walk(root, walker, found, seen, depth: 0, maxDepth: 2, ref nodes, maxNodes: 80, started, budgetMs: 250);
        }

        return found;
    }

    static AutomationElement? FindMenuBar(AutomationElement root, TreeWalker walker)
    {
        try
        {
            var child = walker.GetFirstChild(root);
            for (var i = 0; i < 40 && child != null; i++)
            {
                if (IsMenuBar(child)) return child;
                AutomationElement? grandchild = null;
                try { grandchild = walker.GetFirstChild(child); } catch { grandchild = null; }
                for (var j = 0; j < 20 && grandchild != null; j++)
                {
                    if (IsMenuBar(grandchild)) return grandchild;
                    try { grandchild = walker.GetNextSibling(grandchild); } catch { break; }
                }

                try { child = walker.GetNextSibling(child); } catch { break; }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    static bool IsMenuBar(AutomationElement element)
    {
        try { return element.Current.ControlType == ControlType.MenuBar; }
        catch { return false; }
    }

    static void Walk(
        AutomationElement node,
        TreeWalker walker,
        List<Shortcut> into,
        HashSet<string> seen,
        int depth,
        int maxDepth,
        ref int nodes,
        int maxNodes,
        long started,
        int budgetMs)
    {
        if (depth > maxDepth || nodes >= maxNodes) return;
        if (Environment.TickCount64 - started > budgetMs) return;
        nodes++;
        Consider(node, into, seen);

        AutomationElement? child;
        try { child = walker.GetFirstChild(node); }
        catch { return; }

        while (child != null && nodes < maxNodes && Environment.TickCount64 - started <= budgetMs)
        {
            Walk(child, walker, into, seen, depth + 1, maxDepth, ref nodes, maxNodes, started, budgetMs);
            try { child = walker.GetNextSibling(child); }
            catch { break; }
        }
    }

    static void Consider(AutomationElement element, List<Shortcut> into, HashSet<string> seen)
    {
        string? name;
        string? accelerator;
        string? access;
        try
        {
            var current = element.Current;
            name = current.Name;
            accelerator = current.AcceleratorKey;
            access = current.AccessKey;
        }
        catch (ElementNotAvailableException)
        {
            return;
        }
        catch (COMException)
        {
            return;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name)) return;
        var title = name.Trim();
        if (title.Length > 120) title = title[..120].TrimEnd();
        if (!TryChord(accelerator, out var modifiers, out var key) && !TryChord(access, out modifiers, out key))
            return;

        var identity = ((int)modifiers) + "|" + key;
        if (!seen.Add(identity)) return;
        into.Add(new Shortcut(modifiers, key, title));
    }

    static bool TryChord(string? text, out ModifierSet modifiers, out string key)
    {
        modifiers = ModifierSet.None;
        key = "";
        if (string.IsNullOrWhiteSpace(text)) return false;
        return AcceleratorParser.TryParse(text, out modifiers, out key);
    }
}
