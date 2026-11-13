using ZGF.Desktop;
using ZGF.Gui.Immediate;

namespace ILSight.ILSightAppArea;

[Edge]
public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="arguments"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none can take it; else
    /// opens the app, which takes them from its inbox as it takes those of the runs handed to it. What it routes and
    /// opens, the root builds (<see cref="AppRoot.RouteRunAsync"/>, <see cref="AppRoot.BuildLaunchedApp"/>).</summary>
    public static void LaunchApp(LaunchConfig config, RunArguments arguments)
    {
#pragma warning disable VSTHRD002 // The entry thread must stay its own for the window loop and has nothing to do until the run's route is known; the routing runs on the thread pool.
        var receiving = Task.Run(() => AppRoot.RouteRunAsync(config.SettingsPath, arguments, config.NewInstance, CancellationToken.None)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        if (receiving is null)
        {
            return;
        }

        // This run's arguments wait in the inbox first, put there before the role was claimed: they take the path a
        // later run's take.
        using var receiver = receiving;
        var launched = AppRoot.BuildLaunchedApp(config.SettingsPath, receiver.Inbox);
        OpenWindow(launched, config);
    }

    private static void OpenWindow(LaunchedApp launched, LaunchConfig config)
    {
        var windowConfig = CreateWindowConfig(launched.Window);
        var host = new ImmediateGuiAppHost<UiState>(launched.App, windowConfig, config.RenderBackend);
        host.Run();
    }

    /// <summary>The main window's config: <paramref name="window"/> as it was left, or the default size,
    /// centered, when there is none.</summary>
    private static StartupConfig CreateWindowConfig(WindowBounds? window)
    {
        var config = new StartupConfig
        {
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowTitle = "ILSight",
        };
        if (window is null)
        {
            return config;
        }

        return window.Value.ApplyTo(config);
    }
}
