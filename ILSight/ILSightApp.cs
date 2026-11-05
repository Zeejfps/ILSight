using System.Threading.Channels;
using ILSight.Core;
using ZGF.Desktop;
using static ILSight.CommandLines;
using static ILSight.Settings;

namespace ILSight;

/// <summary>The app, from before there is anything to show until its window closes: it takes the command
/// lines of later runs from the first, and opens the window that follows them once it is ready.</summary>
public sealed class ILSightApp : ICommandLineHandler
{
    private readonly CommandLine _startupCommand;
    private readonly Channel<CommandLine> _pendingCommands = Channel.CreateUnbounded<CommandLine>();

    public ILSightApp(CommandLine startupCommand)
    {
        _startupCommand = startupCommand;
    }

    /// <summary>Queues <paramref name="command"/> for the window, from any thread; false once the window
    /// has closed.</summary>
    public bool TryHandle(CommandLine command)
    {
        return _pendingCommands.Writer.TryWrite(command);
    }

    /// <summary>Opens the window and runs until it closes.</summary>
    public void Run()
    {
        var loadResult = AppSettings.Load(_startupCommand.SettingsPath);
        var appState = LoadAppState(loadResult, _startupCommand);
        var uiState = ILSightWindow.CreateUiState(appState);
        var window = new ILSightWindow(appState, uiState, _startupCommand.Files, _pendingCommands);
        window.Run(CreateWindowConfig(appState.Window), _startupCommand.RenderBackend);
    }

    /// <summary>The app state to start from: what <paramref name="loadSettingsResult"/> describes, told of what was
    /// wrong with the settings, with <paramref name="commandLine"/> followed. The screen is built over
    /// this; the files the command line names open once the app runs.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadSettingsResult, CommandLine commandLine)
    {
        return LoadAppState(loadSettingsResult, commandLine, new BrowseSession());
    }

    /// <summary>The app state to start from over <paramref name="session"/>, which the tests share.</summary>
    internal static AppState LoadAppState(SettingsLoadResult loadSettingsResult, CommandLine commandLine, BrowseSession session)
    {
        var appState = AppState.Load(loadSettingsResult.Settings, session);
        ReportSettingsProblem(appState, loadSettingsResult.Problem);
        ApplyStartupOptions(appState, commandLine);
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
