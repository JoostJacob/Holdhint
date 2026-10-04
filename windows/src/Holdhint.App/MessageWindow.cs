using System.Windows.Forms;

namespace Holdhint;

internal sealed class MessageWindow : NativeWindow
{
    public event Action? Keys;
    public event Action? Mouse;
    public event Action<IntPtr>? Front;
    public event Action? Reload;
    public event Action? Uia;
    public event Action? QuitRequested;

    public void Create()
    {
        if (Handle != IntPtr.Zero) return;
        CreateHandle(new CreateParams
        {
            Parent = NativeMethods.HwndMessage,
            Caption = "Holdhint.Message",
        });
    }

    protected override void WndProc(ref Message m)
    {
        try
        {
            switch (m.Msg)
            {
                case Messages.Key:
                    Keys?.Invoke();
                    break;
                case Messages.Mouse:
                    Mouse?.Invoke();
                    break;
                case Messages.Front:
                    Front?.Invoke(m.WParam);
                    break;
                case Messages.Reload:
                    Reload?.Invoke();
                    break;
                case Messages.Uia:
                    Uia?.Invoke();
                    break;
                case Messages.Quit:
                    QuitRequested?.Invoke();
                    break;
                case Messages.WmQueryEndSession:
                    m.Result = (IntPtr)1;
                    return;
                case Messages.WmEndSession when m.WParam != IntPtr.Zero:
                    QuitRequested?.Invoke();
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Message failed: " + ex.GetType().Name);
        }

        base.WndProc(ref m);
    }
}
