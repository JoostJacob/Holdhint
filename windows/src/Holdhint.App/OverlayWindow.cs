using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Holdhint;

internal readonly record struct WorkArea(int Left, int Top, int Width, int Height, float Scale);

internal static class ScreenInfo
{
    public static WorkArea UnderCursor()
    {
        var point = new NativeMethods.Point();
        if (!NativeMethods.GetCursorPos(out point))
        {
            point.X = 0;
            point.Y = 0;
        }

        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo { CbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfoW(monitor, ref info))
            return new WorkArea(0, 0, 1280, 800, 1);

        var width = Math.Max(1, info.RcWork.Right - info.RcWork.Left);
        var height = Math.Max(1, info.RcWork.Bottom - info.RcWork.Top);
        return new WorkArea(info.RcWork.Left, info.RcWork.Top, width, height, DpiScale(monitor));
    }

    static float DpiScale(IntPtr monitor)
    {
        try
        {
            if (NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) != 0 || dpiX == 0)
                return 1;
            var scale = dpiX / 96f;
            if (float.IsNaN(scale) || scale < 1f) return 1;
            return scale > 3f ? 3f : scale;
        }
        catch
        {
            return 1;
        }
    }
}

/// <summary>
/// A topmost layered window. Extended styles are set at creation and again on every show,
/// because dropping WS_EX_LAYERED and putting it back makes UpdateLayeredWindow fail.
/// The bitmap is top-down: GDI+ row 0 is the top, and the DIB uses a negative height.
/// </summary>
internal sealed class OverlayWindow : NativeWindow
{
    public bool IsShown { get; private set; }
    public event Action? DisplayChanged;
    public event Action? SessionEnding;

    public void Create()
    {
        if (Handle != IntPtr.Zero) return;
        // .NET 8 NativeWindow has no CreateParams property. The extended style is set
        // here and again before every show, so WS_EX_LAYERED is never dropped.
        CreateHandle(new CreateParams
        {
            Caption = "Holdhint",
            Style = WindowStyles.WsPopup,
            ExStyle = WindowStyles.OverlayExStyle,
            X = -32000,
            Y = -32000,
            Width = 16,
            Height = 16,
        });
        ApplyExStyle();
    }

    public bool Present(Bitmap bitmap, int x, int y)
    {
        if (Handle == IntPtr.Zero) Create();
        ApplyExStyle();
        if (!Blit(bitmap, x, y))
        {
            Log.Warn("The hint panel could not be drawn.");
            IsShown = false;
            return false;
        }

        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HwndTopmost,
            x,
            y,
            bitmap.Width,
            bitmap.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        NativeMethods.ShowWindow(Handle, NativeMethods.SwShowNoActivate);
        IsShown = true;
        return true;
    }

    public void HidePanel()
    {
        if (Handle != IntPtr.Zero) NativeMethods.ShowWindow(Handle, NativeMethods.SwHide);
        IsShown = false;
    }

    public int ExtendedStyle
    {
        get
        {
            if (Handle == IntPtr.Zero) return 0;
            return unchecked((int)NativeMethods.GetWindowLongPtrW(Handle, WindowStyles.GwlExStyle).ToInt64());
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Messages.WmMouseActivate)
        {
            m.Result = (IntPtr)Messages.MaNoActivate;
            return;
        }

        if (m.Msg == Messages.WmQueryEndSession)
        {
            m.Result = (IntPtr)1;
            return;
        }

        if (m.Msg == Messages.WmEndSession && m.WParam != IntPtr.Zero)
        {
            SessionEnding?.Invoke();
            return;
        }

        if (m.Msg == Messages.WmDisplayChange)
            DisplayChanged?.Invoke();

        base.WndProc(ref m);
    }

    void ApplyExStyle()
    {
        if (Handle == IntPtr.Zero) return;
        var current = unchecked((int)NativeMethods.GetWindowLongPtrW(Handle, WindowStyles.GwlExStyle).ToInt64());
        var next = (current | WindowStyles.OverlayExStyle) & ~WindowStyles.WsExAppWindow;
        if (next != current)
            NativeMethods.SetWindowLongPtrW(Handle, WindowStyles.GwlExStyle, (IntPtr)next);
    }

    bool Blit(Bitmap bitmap, int x, int y)
    {
        var screen = NativeMethods.GetDC(IntPtr.Zero);
        var memory = NativeMethods.CreateCompatibleDC(screen);
        var dib = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            dib = CreateTopDownDib(memory, bitmap);
            if (dib == IntPtr.Zero) return false;
            previous = NativeMethods.SelectObject(memory, dib);
            var destination = new NativeMethods.Point { X = x, Y = y };
            var source = new NativeMethods.Point();
            var size = new NativeMethods.Size { Cx = bitmap.Width, Cy = bitmap.Height };
            var blend = new NativeMethods.BlendFunction
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1,
            };
            return NativeMethods.UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref source, 0, ref blend, 2);
        }
        finally
        {
            if (previous != IntPtr.Zero) NativeMethods.SelectObject(memory, previous);
            if (dib != IntPtr.Zero) NativeMethods.DeleteObject(dib);
            if (memory != IntPtr.Zero) NativeMethods.DeleteDC(memory);
            if (screen != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    static IntPtr CreateTopDownDib(IntPtr memory, Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var header = new NativeMethods.BitmapInfoHeader
        {
            BiSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            BiWidth = width,
            BiHeight = -height,
            BiPlanes = 1,
            BiBitCount = 32,
            BiCompression = 0,
        };
        var dib = NativeMethods.CreateDIBSection(memory, ref header, 0, out var bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero || bits == IntPtr.Zero) return IntPtr.Zero;

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var rowBytes = width * 4;
            var row = new byte[rowBytes];
            for (var y = 0; y < height; y++)
            {
                // Positive stride: scan 0 is the top. Negative stride: scan 0 is the bottom.
                var scan = data.Stride >= 0 ? y : height - 1 - y;
                Marshal.Copy(data.Scan0 + scan * data.Stride, row, 0, rowBytes);
                Marshal.Copy(row, 0, bits + y * rowBytes, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return dib;
    }
}
