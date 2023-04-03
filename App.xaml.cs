using Microsoft.UI.Xaml;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace ForceFeedbackRelay;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App
{
    private Window window;
    private AppWindow appWindow;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();

        // Disable resizing and set default size
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        appWindow = AppWindow.GetFromWindowId(windowId);
        var presenter = appWindow.Presenter as OverlappedPresenter;

        appWindow.Resize(new SizeInt32(880, 260));
        if (presenter != null) presenter.IsResizable = false;

        window.Activate();
    }
}
