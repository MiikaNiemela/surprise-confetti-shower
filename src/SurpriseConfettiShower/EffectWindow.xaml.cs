using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;

namespace SurpriseConfettiShower;

/// <summary>
/// A transparent, click-through overlay covering one monitor. It never activates and never
/// takes a mouse click, so an effect cannot interrupt whatever you are doing.
/// </summary>
public partial class EffectWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;     // keeps the overlay out of Alt+Tab
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly Drawing.Rectangle _bounds;

    public EffectWindow(Drawing.Rectangle bounds, EffectKind kind)
    {
        InitializeComponent();
        _bounds = bounds;

        EffectCanvas canvas = kind switch
        {
            EffectKind.Fireworks => new FireworksEffect(),
            _ => new ConfettiEffect(),
        };
        // Finished is raised from a render-loop callback, so let the frame end before tearing
        // the window (and its composition target) down.
        canvas.Finished += (_, _) => Dispatcher.InvokeAsync(Close);
        Content = canvas;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // These extended styles are what actually guarantee click-through and non-activation; the
        // matching XAML attributes are belt-and-braces at the WPF level. Do not drop either half.
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(
            hwnd,
            GwlExStyle,
            GetWindowLong(hwnd, GwlExStyle) | WsExTransparent | WsExNoActivate | WsExToolWindow);

        // The monitor bounds are device pixels, which is what SetWindowPos wants. Going through
        // WPF's Left/Top/Width/Height instead would land in the wrong place on a multi-monitor
        // desktop with mixed DPI or a negative origin.
        SetWindowPos(
            hwnd, HwndTopmost, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height,
            SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
