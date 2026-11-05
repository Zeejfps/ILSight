using System.Threading.Channels;
using ILSight.Core;
using ZGF.Desktop;
using static ILSight.ExternalCommands;
using static ILSight.Settings;

namespace ILSight;

/// <summary>The app, from before there is anything to show until its window closes: it takes the commands
/// of later runs from the first, and opens the window that follows them once it is ready.</summary>
public sealed class ILSightApp : IExternalCommandHandler
{
    private readonly Channel<ExternalCommand> _pendingCommands = Channel.CreateUnbounded<ExternalCommand>();

    /// <summary>Queues <paramref name="command"/> for the window, from any thread; false once the window
    /// has closed.</summary>
    public bool TryHandle(ExternalCommand command)
    {
        return _pendingCommands.Writer.TryWrite(command);
    }

    /// <summary>Opens the window as <paramref name="config"/> says, follows <paramref name="command"/>,
    /// and runs until the window closes.</summary>
    public void Run(LaunchConfig config, ExternalCommand command)
    {
        var loadResult = AppSettings.Load(config.SettingsPath);
        var appState = LoadAppState(loadResult, command);
        var uiState = ILSightWindow.CreateUiState(appState);
        var window = new ILSightWindow(appState, uiState, command.Files, _pendingCommands);
        window.Run(CreateWindowConfig(appState.Window), config.RenderBackend);
    }

    /// <summary>The app state to start from: what <paramref name="loadSettingsResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="command"/> followed. The screen is built over
    /// this; the files the command names open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadSettingsResult, ExternalCommand command)
    {
        return LoadAppState(loadSettingsResult, command, new BrowseSession());
    }

    /// <summary>The app state to start from over <paramref name="session"/>, which the tests share.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadSettingsResult, ExternalCommand command, BrowseSession session)
    {
        var appState = AppState.Load(loadSettingsResult.Settings, session);
        ReportSettingsProblem(appState, loadSettingsResult.Problem);
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
