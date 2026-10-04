using Holdhint.Core;

namespace Holdhint;

internal static class ModifierState
{
    public static ModifierSet Read() => ReadReading().ToSet();

    /// <summary>Each modifier, with the two Windows keys separate. Used to resync <see cref="ModifierTracker"/>.</summary>
    public static ModifierReading ReadReading() => new(
        Control: Down(VirtualKeys.Control),
        LeftControl: Down(VirtualKeys.LeftControl),
        RightControl: Down(VirtualKeys.RightControl),
        Alt: Down(VirtualKeys.Menu),
        LeftAlt: Down(VirtualKeys.LeftMenu),
        RightAlt: Down(VirtualKeys.RightMenu),
        Shift: Down(VirtualKeys.Shift),
        LeftShift: Down(VirtualKeys.LeftShift),
        RightShift: Down(VirtualKeys.RightShift),
        LeftWin: Down(VirtualKeys.LeftWin),
        RightWin: Down(VirtualKeys.RightWin));

    static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
}
