using Holdhint.Core;

namespace Holdhint;

/// <summary>
/// Low-level keyboard and mouse hooks. The procedures only enqueue and return.
/// A throw here makes Windows remove the hook, which is how the panel got stuck on the Mac
/// when a key-up never arrived. The UI thread polls the real key state as a backup.
/// </summary>
internal static class InputHooks
{
    const int QueueCap = 256;
    static readonly object Gate = new();
    static readonly Queue<(int Vk, bool Up)> Queue = new();

    static IntPtr _keyboard;
    static IntPtr _mouse;
    static IntPtr _winEvent;
    static IntPtr _hwnd;
    static int _swallowWinUp;
    static HookProc? _keyboardProc;
    static HookProc? _mouseProc;
    static WinEventProc? _winProc;

    public static bool KeyboardInstalled { get; private set; }
    public static bool MouseInstalled { get; private set; }
    public static bool FrontInstalled { get; private set; }

    public static void SetSwallowWinUp(bool swallow) => Volatile.Write(ref _swallowWinUp, swallow ? 1 : 0);

    public static bool Install(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _winProc = FrontProc;
        var module = NativeMethods.GetModuleHandleW(null);

        _keyboard = NativeMethods.SetWindowsHookExW(NativeMethods.WhKeyboardLl, _keyboardProc, module, 0);
        if (_keyboard == IntPtr.Zero)
            _keyboard = NativeMethods.SetWindowsHookExW(NativeMethods.WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
        KeyboardInstalled = _keyboard != IntPtr.Zero;

        _mouse = NativeMethods.SetWindowsHookExW(NativeMethods.WhMouseLl, _mouseProc, module, 0);
        if (_mouse == IntPtr.Zero)
            _mouse = NativeMethods.SetWindowsHookExW(NativeMethods.WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        MouseInstalled = _mouse != IntPtr.Zero;

        _winEvent = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _winProc,
            0,
            0,
            NativeMethods.WinEventSkipOwnProcess);
        FrontInstalled = _winEvent != IntPtr.Zero;
        return KeyboardInstalled;
    }

    public static void Uninstall()
    {
        SetSwallowWinUp(false);
        if (_keyboard != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboard);
            _keyboard = IntPtr.Zero;
        }

        if (_mouse != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouse);
            _mouse = IntPtr.Zero;
        }

        if (_winEvent != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEvent);
            _winEvent = IntPtr.Zero;
        }

        KeyboardInstalled = false;
        MouseInstalled = false;
        FrontInstalled = false;
        _keyboardProc = null;
        _mouseProc = null;
        _winProc = null;
        lock (Gate) Queue.Clear();
    }

    public static List<(int Vk, bool Up)> Drain()
    {
        lock (Gate)
        {
            var copy = Queue.ToList();
            Queue.Clear();
            return copy;
        }
    }

    static IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                var message = unchecked((int)wParam.ToInt64());
                var up = message is 0x0101 or 0x0105;
                var down = message is 0x0100 or 0x0104;
                if (up || down)
                {
                    var vk = MarshalReadVk(lParam);
                    var swallow = up && VirtualKeys.IsWin(vk) && Volatile.Read(ref _swallowWinUp) != 0;
                    Enqueue(vk, up);
                    // Eat the Windows-key release only. A tap still opens Start, because the
                    // panel (and this flag) never turned on. Alt-up is never eaten: the menu
                    // bar would stay highlighted.
                    if (swallow) return (IntPtr)1;
                }
            }
        }
        catch
        {
            // Never throw out of a hook procedure.
        }

        return NativeMethods.CallNextHookEx(_keyboard, nCode, wParam, lParam);
    }

    static IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = unchecked((int)wParam.ToInt64());
                if (message is 0x0201 or 0x0204 or 0x0207 or 0x020B)
                    NativeMethods.PostMessageW(_hwnd, Messages.Mouse, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch
        {
            // Never throw out of a hook procedure.
        }

        return NativeMethods.CallNextHookEx(_mouse, nCode, wParam, lParam);
    }

    static void FrontProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (hwnd != IntPtr.Zero && _hwnd != IntPtr.Zero)
                NativeMethods.PostMessageW(_hwnd, Messages.Front, hwnd, IntPtr.Zero);
        }
        catch
        {
            // The front-app hook is a convenience. The timeout still closes the panel.
        }
    }

    static int MarshalReadVk(IntPtr lParam)
    {
        // KBDLLHOOKSTRUCT.vkCode is the first 32-bit field.
        return System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
    }

    static void Enqueue(int vk, bool up)
    {
        bool post;
        lock (Gate)
        {
            if (Queue.Count >= QueueCap) Queue.Dequeue();
            post = Queue.Count == 0;
            Queue.Enqueue((vk, up));
        }

        if (post && _hwnd != IntPtr.Zero)
            NativeMethods.PostMessageW(_hwnd, Messages.Key, IntPtr.Zero, IntPtr.Zero);
    }
}
