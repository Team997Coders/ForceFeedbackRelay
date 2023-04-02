// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Windows.Storage;
using ABI.Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NetworkTables;
using WinRT;
using static System.Net.Mime.MediaTypeNames;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ForceFeedbackMonitorWinUI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow
{
    private WheelController wheel;
    private readonly DispatcherQueueController pollDispatcherQueueController;
    private readonly DispatcherQueueTimer pollTimer;
    private readonly DispatcherQueueTimer connectionTimer;
    private readonly DispatcherQueueTimer uiUpdateTimer;
    public MainWindow()
    {
        InitializeComponent();

        // Custom title bar and background
        Title = "Force Feedback Monitor";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        TrySetMicaBackdrop();

        // Setup NetworkTables as client
        NetworkTable.SetClientMode();

        NetworkTable.AddGlobalConnectionListener((remote, connection, b) =>
        {
            Debug.WriteLine("CONNECTED");
        }, true);

        try
        {
            wheel = new WheelController(this);
            // Let's disable centering until set otherwise (via robot code)
            wheel.PlayConstantForce(0);
        }
        catch (JoystickNotConnectedException e)
        {
            Debug.WriteLine(e);
            wheel = null;
        }

        // Start Joystick Loop
        pollDispatcherQueueController = DispatcherQueueController.CreateOnDedicatedThread();
        pollTimer = pollDispatcherQueueController.DispatcherQueue.CreateTimer();
        pollTimer.Interval = TimeSpan.FromMilliseconds(5);
        pollTimer.Tick += Poll;
        pollTimer.Start();

        // Start Joystick Connection Timer
        connectionTimer = DispatcherQueue.CreateTimer();
        connectionTimer.Interval = TimeSpan.FromSeconds(1);
        connectionTimer.Tick += PollConnections;
        connectionTimer.Start();

        // Start UI Loop
        uiUpdateTimer = DispatcherQueue.CreateTimer();
        uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(10);
        uiUpdateTimer.Tick += UiUpdate;
        uiUpdateTimer.Start();

        // Read team number from cache
        ReadTeamNumber();

        Container.Focus(FocusState.Programmatic);
    }

    private async void ReadTeamNumber()
    {
        Debug.WriteLine("Calling this");
        var cache = ApplicationData.Current.LocalCacheFolder;
        Debug.WriteLine("Got Cache");
        var teamNumberFile = await cache.CreateFileAsync("team-number", CreationCollisionOption.OpenIfExists);
        Debug.WriteLine("Created File");
        var teamNumber = await FileIO.ReadTextAsync(teamNumberFile);
        Debug.WriteLine("Got Team Number: "+teamNumber);
        DispatcherQueue.TryEnqueue(() =>
        {
            TeamNumber.Text = teamNumber;
        });
    }

    private bool robotConnected = false;
    private bool forceFeedbackConnected = false;

    // This is called on a non UI thread and reads and sets controller state
    private void Poll(DispatcherQueueTimer timer, object state)
    {
        // Poll network tables
        var liveWindowTable = NetworkTable.GetTable("LiveWindow");
        robotConnected = liveWindowTable.IsConnected;
        var forceFeedbackTable = NetworkTable.GetTable("ForceFeedback");
        forceFeedbackConnected = forceFeedbackTable.ContainsKey("enabled");

        if (wheel != null)
        {
            try
            {
                // Poll wheel
                wheel.Poll();
                if (forceFeedbackConnected)
                {
                    // Set force feedback

                }
            }
            catch (JoystickNotConnectedException e)
            {
                Debug.WriteLine(e);
                wheel.Dispose();
                wheel = null;
            }
        }
    }

    private string lastTeamNumberText = "";
    // This is called on the UI thread (to get team number) and reads and checks for newly plugged in devices and team number
    private void PollConnections(DispatcherQueueTimer timer, object state)
    {
        var teamNumberText = TeamNumber.Text;
        // Launch non UI thread
        pollDispatcherQueueController.DispatcherQueue.TryEnqueue(() =>
        {
            // Check for wheel plugged in
            if (wheel == null)
            {
                try
                {
                    wheel = new WheelController(this);
                    // Let's disable centering until set otherwise (via robot code)
                    wheel.PlayConstantForce(0);
                }
                catch (JoystickNotConnectedException e)
                {
                    Debug.WriteLine(e);
                    wheel = null;
                }
            }

            // Check for team number
            if (teamNumberText != lastTeamNumberText)
            {
                lastTeamNumberText = teamNumberText;
                pollDispatcherQueueController.DispatcherQueue.TryEnqueue(async () =>
                {
                    // Connect to NetworkTables
                    NetworkTable.Shutdown();
                    NetworkTable.SetClientMode();
                    if (int.TryParse(teamNumberText, out var teamNumber))
                        NetworkTable.SetTeam(teamNumber);
                    else
                        NetworkTable.SetIPAddress(teamNumberText);
                    // Start NetworkTables
                    NetworkTable.Initialize();
                    Debug.WriteLine("NetworkTables initialized.");

                    // Save team number
                    var cache = ApplicationData.Current.LocalCacheFolder;
                    var teamNumberFile = await cache.CreateFileAsync("team-number", CreationCollisionOption.OpenIfExists);
                    await FileIO.WriteTextAsync(teamNumberFile, teamNumberText);
                });
            }
        });
    }

    // This is called on the UI thread at a lower interval, and updates the UI
    private void UiUpdate(DispatcherQueueTimer timer, object state)
    {
        // Set connection indicators
        ControllerConnectionIndicator.Fill = new SolidColorBrush(wheel != null ? Colors.GreenYellow : Colors.Red);
        RobotConnectionIndicator.Fill = new SolidColorBrush(robotConnected ? Colors.GreenYellow : Colors.Red);
        ForceFeedbackIndicator.Fill = new SolidColorBrush(forceFeedbackConnected ? Colors.GreenYellow : Colors.Red);
        // Set axis indicators
        if (wheel != null)
        {
            SteeringBar.Value = wheel.SteeringAxis;
            ThrottleBar.Value = -wheel.ThrottleAxis;
            BrakeBar.Value = -wheel.BrakeAxis;
            ClutchBar.Value = -wheel.ClutchAxis;
        }
    }

    #region Mica
    WindowsSystemDispatcherQueueHelper wsdqHelper; // See separate sample below for implementation
    Microsoft.UI.Composition.SystemBackdrops.MicaController micaController;
    Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration configurationSource;
    bool TrySetMicaBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            // Hooking up the policy object
            configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
            this.Activated += Window_Activated;
            this.Closed += Window_Closed;

            // Initial configuration state.
            configurationSource.IsInputActive = true;
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }

            micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

            // Enable the system backdrop.
            // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
            micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(configurationSource);
            return true; // succeeded
        }

        return false; // Mica is not supported on this system
    }
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
    }
    private void Window_Closed(object sender, WindowEventArgs args)
    {
        // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
        // use this closed window.
        if (micaController != null)
        {
            micaController.Dispose();
            micaController = null;
        }
        this.Activated -= Window_Activated;
        configurationSource = null;
    }
    #endregion
}
