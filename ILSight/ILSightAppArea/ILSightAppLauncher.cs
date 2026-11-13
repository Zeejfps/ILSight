using ILSight.Core;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.SettingArea;

namespace ILSight.ILSightAppArea;

[Edge]
public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="arguments"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none can take it; else
    /// opens the app, which takes them from its inbox as it takes those of the runs handed to it.</summary>
    public static void LaunchApp(LaunchConfig config, RunArguments arguments)
    {
        var address = RunArgumentsRouting.GetAddress(config.SettingsPath);
        var routing = new RunRoutingAdapter(address);
#pragma warning disable VSTHRD002 // The entry thread must stay its own for the window loop and has nothing to do until the run's route is known; the routing runs on the thread pool.
        var route = Task.Run(() => new RunRouting(routing).RouteAsync(arguments, config.NewInstance, CancellationToken.None)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        if (route == RunRoute.HandedOver)
        {
            return;
        }

        using var receiver = routing.Claimed ?? RunArgumentsReceiver.StandBy(address);
        // This run's arguments take the path a later run's take: into the inbox, before the window opens. The inbox is
        // unbounded and only the app completes it, as its window closes, so it takes them.
        receiver.Inbox.Writer.TryWrite(arguments);

        var loadResult = AppRoot.ReadSettingsFile(config.SettingsPath);
        var built = AppRoot.BuildApp(loadResult, receiver.Inbox, new BrowseSession());
        OpenWindow(built.App, loadResult, config);
    }

    private static void OpenWindow(ILSightApp app, SettingsLoadResult loadResult, LaunchConfig config)
    {
        var windowConfig = CreateWindowConfig(loadResult.Settings.Window);
        var host = new ImmediateGuiAppHost<UiState>(app, windowConfig, config.RenderBackend);
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
