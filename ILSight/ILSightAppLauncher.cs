using System.Threading.Channels;
using ILSight.Core;
using ZGF.Desktop;
using ZGF.Gui.Immediate;

namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="arguments"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none can take it; else
    /// opens the app.</summary>
    public static void LaunchApp(LaunchConfig config, RunArguments arguments)
    {
        var address = RunArgumentsRouting.GetAddress(config.SettingsPath);

        using var receiver = RunArgumentsRouting.ForwardOrReceive(address, arguments, config.NewInstance);
        if (receiver is null)
        {
            return;
        }

        var loadResult = SettingsJson.ReadSettingsFile(config.SettingsPath);
        var app = CreateApp(loadResult, arguments, receiver.Inbox);
        OpenWindow(app, loadResult, config);
    }

    private static ILSightApp CreateApp(SettingsLoadResult loadResult, RunArguments arguments, Channel<RunArguments> inbox)
    {
        var appState = AppRoot.RestoreAppState(loadResult, arguments, new BrowseSession());
        var uiState = AppRoot.CreateScreen(appState);
        return new ILSightApp(appState, uiState, arguments.Files, inbox);
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
