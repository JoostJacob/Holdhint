using Holdhint.Core;

namespace Holdhint;

internal static class ModifierState
{
    public static ModifierSet Read()
    {
        return Modifiers.FromState(
            Down(VirtualKeys.Control) || Down(VirtualKeys.LeftControl) || Down(VirtualKeys.RightControl),
            Down(VirtualKeys.Menu) || Down(VirtualKeys.LeftMenu) || Down(VirtualKeys.RightMenu),
            Down(VirtualKeys.Shift) || Down(VirtualKeys.LeftShift) || Down(VirtualKeys.RightShift),
            Down(VirtualKeys.LeftWin) || Down(VirtualKeys.RightWin));
    }

    static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
}
