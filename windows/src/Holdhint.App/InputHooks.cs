using Holdhint.Core;

namespace Holdhint;

/// <summary>
/// Low-level keyboard and mouse hooks. The procedures only enqueue and return.
/// A throw here makes Windows remove the hook, which is how the panel got stuck on the Mac
/// when a key-up never arrived. The UI thread polls the real key state as a backup.
///
/// Eating a Windows-key release leaves GetAsyncKeyState down, because this hook runs
/// before Windows updates that bit and a swallowed event never updates it. The release
/// is therefore also counted in <see cref="TakeSwallowedWinUps"/>, which survives a full queue.
/// </summary>
internal readonly record struct HookKey(int Vk, bool Up, bool Swallowed);

internal static class InputHooks
{
    const int QueueCap = 256;
    static readonly object Gate = new();
    static readonly Queue<HookKey> Queue = new();

    static IntPtr _keyboard;
    static IntPtr _mouse;
    static IntPtr _winEvent;
    static IntPtr _hwnd;
    static int _swallowWinUp;
    static int _swallowedLeft;
    static int _swallowedRight;
    static HookProc? _keyboardProc;
    static HookProc? _mouseProc;
    static WinEventProc? _winProc;

    public static bool KeyboardInstalled { get; private set; }
    public static bool MouseInstalled { get; private set; }
    public static bool FrontInstalled { get; private set; }

    public static void SetSwallowWinUp(bool swallow) => Volatile.Write(ref _swallowWinUp, swallow ? 1 : 0);

    public static bool SwallowedWinPending =>
        Volatile.Read(ref _swallowedLeft) != 0 || Volatile.Read(ref _swallowedRight) != 0;

    /// <summary>How many left and right Windows-key releases were eaten since the last take.</summary>
    public static (int Left, int Right) TakeSwallowedWinUps() => (
        Interlocked.Exchange(ref _swallowedLeft, 0),
        Interlocked.Exchange(ref _swallowedRight, 0));

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
        Interlocked.Exchange(ref _swallowedLeft, 0);
        Interlocked.Exchange(ref _swallowedRight, 0);
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

    public static List<HookKey> Drain()
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
                    if (swallow)
                    {
                        // Count before the queue: a dropped queue slot must not lose the release.
                        if (vk == VirtualKeys.LeftWin) Interlocked.Increment(ref _swallowedLeft);
                        else Interlocked.Increment(ref _swallowedRight);
                    }

                    Enqueue(vk, up, swallow);
                    // Eat the Windows-key release only. A tap still opens Start, because the
                    // panel (and this flag) never turned on. Alt-up is never eaten: the menu
                    // bar would stay highlighted. No dummy key is injected; that would show
                    // up as Ctrl, Alt, or Shift in the modifier state.
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

    static void Enqueue(int vk, bool up, bool swallowed)
    {
        bool post;
        lock (Gate)
        {
            if (Queue.Count >= QueueCap) DropOne();
            post = Queue.Count == 0;
            Queue.Enqueue(new HookKey(vk, up, swallowed));
        }

        if (post && _hwnd != IntPtr.Zero)
            NativeMethods.PostMessageW(_hwnd, Messages.Key, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Make one free slot. A swallowed Windows-key release proves the key came up,
    /// so a key-repeat is dropped before that release is.
    /// </summary>
    static void DropOne()
    {
        var kept = new Queue<HookKey>(Queue.Count);
        var dropped = false;
        while (Queue.Count > 0)
        {
            var item = Queue.Dequeue();
            if (!dropped && !item.Swallowed)
            {
                dropped = true;
                continue;
            }

            kept.Enqueue(item);
        }

        if (!dropped && kept.Count > 0) kept.Dequeue();
        while (kept.Count > 0) Queue.Enqueue(kept.Dequeue());
    }
}
