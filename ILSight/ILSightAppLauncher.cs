using ILSight.Core;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using static ILSight.ExternalCommands;
using static ILSight.Settings;

namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="command"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none can take it; else
    /// opens the app.</summary>
    public static void LaunchApp(LaunchConfig config, ExternalCommand command)
    {
        var address = ExternalCommandAddress.ForSettingsFile(config.SettingsPath);

        using var receiver = ExternalCommandRouting.ForwardOrReceive(address, command, config.NewInstance);
        if (receiver == null)
            return;

        var loadResult = AppSettings.Load(config.SettingsPath);
        var app = CreateApp(loadResult, command, receiver.Inbox);
        OpenWindow(app, loadResult, config);
    }

    private static ILSightApp CreateApp(SettingsLoadResult loadResult, ExternalCommand command, ExternalCommandInbox inbox)
    {
        var appState = LoadAppState(loadResult, command);
        var uiState = ILSightApp.CreateUiState(appState);
        return new ILSightApp(appState, uiState, command.Files, inbox);
    }

    private static void OpenWindow(ILSightApp app, SettingsLoadResult loadResult, LaunchConfig config)
    {
        var windowConfig = CreateWindowConfig(loadResult.Settings.Window);
        var host = new ImmediateGuiAppHost<UiState>(app, windowConfig, config.RenderBackend);
        host.Run();
    }

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="command"/> followed. The screen is built over
    /// this; the files the command names open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadResult, ExternalCommand command)
    {
        return LoadAppState(loadResult, command, new BrowseSession());
    }

    /// <summary>The app state to start from over <paramref name="session"/>, which the tests share.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadResult, ExternalCommand command, BrowseSession session)
    {
        var appState = AppState.Load(loadResult.Settings, session);
        ReportSettingsProblem(appState, loadResult.Problem);
        ApplyStartupOptions(appState, command);
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
            return config;
        return window.Value.ApplyTo(config);
    }
}
