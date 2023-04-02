using Microsoft.UI.Xaml;
using Windows.Graphics;
using Microsoft.UI.Windowing;

namespace ForceFeedbackRelay;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window window;
    private AppWindow appWindow;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        window = new MainWindow();

        // Disable resizing and set default size
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        appWindow = AppWindow.GetFromWindowId(windowId);
        var presenter = appWindow.Presenter as OverlappedPresenter;

        appWindow.Resize(new SizeInt32(780, 300));
        if (presenter != null) presenter.IsResizable = false;

        window.Activate();
    }
}
