using System.Threading.Channels;
using ILSight.Core;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using static ILSight.RunArgumentsActions;
using static ILSight.Settings;

namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="arguments"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none can take it; else
    /// opens the app.</summary>
    public static void LaunchApp(LaunchConfig config, RunArguments arguments)
    {
        var address = RunArgumentsRouting.FindAddress(config.SettingsPath);

        using var receiver = RunArgumentsRouting.ForwardOrReceive(address, arguments, config.NewInstance);
        if (receiver == null)
        {
            return;
        }

        var loadResult = SettingsJson.LoadSettings(config.SettingsPath);
        var app = CreateApp(loadResult, arguments, receiver.Inbox);
        OpenWindow(app, loadResult, config);
    }

    private static ILSightApp CreateApp(SettingsLoadResult loadResult, RunArguments arguments, Channel<RunArguments> inbox)
    {
        var appState = LoadAppState(loadResult, arguments);
        var uiState = AppFrames.CreateUiState(appState);
        return new ILSightApp(appState, uiState, arguments.Files, inbox);
    }

    private static void OpenWindow(ILSightApp app, SettingsLoadResult loadResult, LaunchConfig config)
    {
        var windowConfig = CreateWindowConfig(loadResult.Settings.Window);
        var host = new ImmediateGuiAppHost<UiState>(app, windowConfig, config.RenderBackend);
        host.Run();
    }

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="arguments"/> followed. The screen is built over
    /// this; the files they name open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadResult, RunArguments arguments)
    {
        return LoadAppState(loadResult, arguments, new BrowseSession());
    }

    /// <summary>The app state to start from over <paramref name="session"/>, which the tests share.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadResult, RunArguments arguments, BrowseSession session)
    {
        var appState = Settings.LoadAppState(loadResult.Settings, session);
        ReportSettingsProblem(appState, loadResult.Problem);
        ApplyStartupOptions(appState, arguments);
        return appState;
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
        if (window == null)
        {
            return config;
        }

        return window.Value.ApplyTo(config);
    }
}
