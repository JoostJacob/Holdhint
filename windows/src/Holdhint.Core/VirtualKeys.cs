namespace Holdhint.Core;

/// <summary>Virtual-key codes the hook and the poller share. Values are from Winuser.h.</summary>
public static class VirtualKeys
{
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Escape = 0x1B;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftMenu = 0xA4;
    public const int RightMenu = 0xA5;
    public const int LeftWin = 0x5B;
    public const int RightWin = 0x5C;

    public static bool IsWin(int vk) => vk == LeftWin || vk == RightWin;

    public static bool IsModifier(int vk) => vk switch
    {
        Shift or Control or Menu or
        LeftShift or RightShift or
        LeftControl or RightControl or
        LeftMenu or RightMenu or
        LeftWin or RightWin => true,
        _ => false,
    };

    /// <summary>
    /// Swallow a Windows-key release only while the panel is up for a chord that includes that key.
    /// A quick tap still opens the Start menu, because the panel never appeared.
    /// Alt is not swallowed: eating Alt-up while the app saw Alt-down leaves the menu bar stuck.
    /// </summary>
    public static bool ShouldSwallowWinKeyUp(bool keyIsUp, int vk, bool panelVisible, ModifierSet held)
    {
        return keyIsUp
            && IsWin(vk)
            && panelVisible
            && (held & ModifierSet.Win) != 0;
    }
}
