using ILSight.Core;
using ZGF.Desktop;
using static ILSight.CommandLines;
using static ILSight.Settings;

namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="commandLine"/> to the window already open with the same settings
    /// and leaves, unless it asks for a new window or none is open; else opens the app.</summary>
    public static void LaunchApp(CommandLine commandLine)
    {
        using var instanceLock = SingleInstanceLock.Request(commandLine.SettingsPath);
        if (instanceLock.IsGranted)
        {
            using var pipe = CommandLinePipe.Listen(instanceLock.Name);
            RunNewAppInstance(commandLine, pipe);
        }
        else if (commandLine.NewInstance)
        {
            RunNewAppInstance(commandLine);
        }
        else
        {
            var handedOver = CommandLinePipe.TryHandOver(instanceLock.Name, commandLine);
            if (!handedOver)
            {
                RunNewAppInstance(commandLine);
            }
        }
    }

    private static void RunNewAppInstance(CommandLine commandLine, CommandLinePipe? pipe = null)
    {
        var loadResult = LoadSettings(commandLine);
        var appState = LoadAppState(loadResult, commandLine);
        var guiState = CreateUiState(appState);
        var app = CreateApp(appState, guiState, commandLine, pipe);
        OpenWindow(app, appState, commandLine);
    }

    private static SettingsLoadResult LoadSettings(CommandLine commandLine)
    {
        return AppSettings.Load(commandLine.SettingsPath);
    }

    /// <summary>The app state to start from: what <paramref name="loadSettingsResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="commandLine"/> followed. The screen is built over
    /// this; the files the command line names open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadSettingsResult, CommandLine commandLine)
    {
        var session = new BrowseSession();
        var appState = AppState.Load(loadSettingsResult.Settings, session);
        ReportSettingsProblem(appState, loadSettingsResult.Problem);
        ApplyStartupOptions(appState, commandLine);
        return appState;
    }

    private static UiState CreateUiState(AppState appState)
    {
        return ILSightApp.CreateUiState(appState);
    }

    private static ILSightApp CreateApp(AppState appState, UiState uiState, CommandLine commandLine, CommandLinePipe? pipe)
    {
        return new ILSightApp(appState, uiState, commandLine.Files, pipe);
    }

    private static void OpenWindow(ILSightApp app, AppState appState, CommandLine commandLine)
    {
        app.Run(CreateWindowConfig(appState.Window), commandLine.RenderBackend);
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
