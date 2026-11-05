using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ILSight.Core;
using static ILSight.CommandLines;
using static ILSight.Settings;

namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="commandLine"/> to the window already open with the same settings
    /// and leaves, unless it asks for a new window or none is open; else opens the app.</summary>
    public static void LaunchApp(CommandLine commandLine)
    {
        using var instance = AppInstance.Claim(commandLine.SettingsPath);
        if (ShouldHandOver(instance, commandLine) && instance.TryHandOver(commandLine))
            return;
        instance.Listen();
        RunApp(commandLine, instance);
    }

    private static bool ShouldHandOver(AppInstance instance, CommandLine commandLine)
    {
        if (instance.IsFirst)
            return false;
        return !commandLine.NewInstance;
    }

    private static void RunApp(CommandLine commandLine, AppInstance instance)
    {
        var loadResult = LoadSettings(commandLine);
        var appState = LoadAppState(loadResult, commandLine, new BrowseSession());
        var uiState = BuildScreen(appState);
        var app = CreateApp(appState, uiState, commandLine, instance);
        OpenWindow(app, appState, commandLine);
    }

    private static SettingsLoadResult LoadSettings(CommandLine commandLine) =>
        AppSettings.Load(commandLine.SettingsPath);

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="commandLine"/> followed. The screen is built over
    /// this; the files the command line names open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadResult, CommandLine commandLine, BrowseSession session)
    {
        var appState = AppState.Load(loadResult.Settings, session);
        ReportSettingsProblem(appState, loadResult.Problem);
        FollowStartupCommandLine(appState, commandLine);
        return appState;
    }

    private static UiState BuildScreen(AppState appState) =>
        ILSightApp.CreateUiState(appState);

    private static ILSightApp CreateApp(AppState appState, UiState uiState, CommandLine commandLine, AppInstance instance) =>
        new(appState, uiState, commandLine.Files, instance);

    private static void OpenWindow(ILSightApp app, AppState appState, CommandLine commandLine) =>
        app.Run(CreateWindowConfig(appState.Window), commandLine.RenderBackend);

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
