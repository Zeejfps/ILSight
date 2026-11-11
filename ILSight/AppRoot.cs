using System.Globalization;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Processes;
using ILSight.Localization;

namespace ILSight;

/// <summary>The composition root: builds the app state and the screen over it, the use cases on them and the
/// adapters those reach the world through, choosing the desktop app's adapters. The launcher builds the app
/// through it, and so do the tests.</summary>
internal static class AppRoot
{
    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, over
    /// <paramref name="session"/>, told of what was wrong with the settings, with <paramref name="arguments"/>
    /// followed. The screen is built over this; the files they name open once the app runs.</summary>
    internal static AppState RestoreAppState(SettingsLoadResult loadResult, RunArguments arguments, BrowseSession session)
    {
        var appState = RestoreAppState(loadResult.Settings, session);
        Settings.ReportSettingsProblem(appState, loadResult.Problem);
        RunArgumentsActions.ApplyStartupOptions(appState, arguments);
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe over <paramref name="session"/>, with the tab
    /// it starts with active and what the settings keep restored into it.</summary>
    internal static AppState RestoreAppState(AppSettings settings, BrowseSession session)
    {
        var appState = CreateAppState(settings, session);
        var first = Tabs.AddNew(appState.Tabs);
        Tabs.MakeActive(appState.Tabs, first);
        Settings.Restore(appState, settings);
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe, with its use cases over the adapters they
    /// reach the world through, before anything is restored into it. The tooltips report to the error log rather
    /// than to the app state, which they are part of.</summary>
    private static AppState CreateAppState(AppSettings settings, BrowseSession session)
    {
        var assemblies = new AssemblyList();
        var errors = new ErrorLog();
        var work = new BackgroundWork();
        var opens = new OpenRequests();
        var navigation = new NavigationInfrastructure(errors, assemblies, session, opens);
        var assemblyPorts = new AssembliesAdapter(errors, assemblies, session);
        var appState = new AppState
        {
            Work = work,
            Workflows = new WorkflowRunner(work, errors),
            NavigateHandler = new NavigateHandler(navigation),
            ShowInMetadataHandler = new ShowInMetadataHandler(navigation),
            ShowInApiReferenceHandler = new ShowInApiReferenceHandler(navigation),
            OpenAndShowIdHandler = new OpenAndShowIdHandler(navigation),
            GoToHistoryEntryHandler = new GoToHistoryEntryHandler(navigation),
            AssemblyPorts = assemblyPorts,
            Opens = opens,
            Session = session,
            SettingsPlace = new SettingsPlace { FilePath = settings.FilePath, CanSaveOverFile = settings.CanSaveOverFile },
            View = Settings.ReadViewSettings(settings),
            Window = settings.Window,
            Assemblies = assemblies,
            Errors = errors,
            Tooltips = new CodeTooltips { Assemblies = assemblies, Work = work, ReportError = problem => Errors.Report(errors, problem), Version = assemblies.Version },
            Theme = settings.Theme,
            IconTheme = settings.IconTheme,
            Language = settings.Language,
            DecompilerOptions = settings.Decompiler,
            Locale = ChooseLocale(settings),
            ApiVisibility = settings.ApiVisibility,
            Updates = new AppUpdates { Feed = GitHubUpdates.CreateFeed(), CheckAutomatically = settings.CheckForUpdates },
        };
        AssemblyListing.UseLoadOptions(assemblies, settings.Decompiler);
        return appState;
    }

    /// <summary>The interface language the settings name, else the one the user's culture speaks.</summary>
    private static Locale ChooseLocale(AppSettings settings)
    {
        if (settings.Locale is not null)
        {
            return settings.Locale.Value;
        }

        return Locales.For(CultureInfo.CurrentUICulture);
    }

    /// <summary>The screen over <paramref name="appState"/>, restored from what its settings kept, with the
    /// active tab it starts with, the tabs reopened from the open list's documents docked where they were,
    /// and any other tab in the place of the documents. It takes the theme and strings with its first frame.</summary>
    internal static UiState CreateScreen(AppState appState)
    {
        var uiState = CreateUiState(appState.View, appState.Work, problem => Errors.Report(appState, problem), appState.Session, appState.Assemblies.Runtime);
        ScreenSettings.RestoreScreen(uiState, appState.View);
        AppFrames.FollowRestoredAppState(uiState, appState);
        return uiState;
    }

    /// <summary>The screen, with its use cases over the adapters they reach the world through, before what the
    /// settings kept of it besides its layout, display options, folded groups and filters is restored.</summary>
    /// <param name="saved">What the settings kept of the screen when they were last saved.</param>
    /// <param name="work">Where the work the screen starts in the background is counted.</param>
    /// <param name="reportError">Tells the user that something done in the background failed.</param>
    /// <param name="session">The browse tree the tree and searches show nodes of.</param>
    /// <param name="runtime">What this machine has installed.</param>
    private static UiState CreateUiState(ViewSettings saved, BackgroundWork work, Action<AppProblem> reportError, BrowseSession session, InstalledRuntime runtime)
    {
        var appCommands = new Queue<AppCommand>();
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Tree = new BrowseTree { Session = session, Work = work, ReportError = reportError },
            FoldedOptionGroups = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) },
            Search = Searches.CreateSearch(work, reportError, session),
            Omnibar = new Omnibar { Search = Searches.CreateSearch(work, reportError, session) },
            MetadataFilters = MetadataGridWidget.CreateFilters(saved.FlagsFilters),
            Runtime = runtime,
            Picker = new FrameworkPicker(new FrameworkPickerAdapter(reportError, runtime)),
            NuGetPicker = new NuGetPicker { Browser = new NuGetBrowser(new NuGetBrowserAdapter(reportError)) },
            ProcessPicker = new ProcessPicker
            {
                Work = work,
                ReportError = reportError,
                FindProcesses = RunningProcesses.FindProcessesAsync,
                FindModules = RunningProcesses.GetModulesAsync,
            },
            Display = saved.Display ?? DisplayOptions.Default,
            Analyzer = new AnalyzerRows { AppCommands = appCommands },
            MainLayout = new MainLayout { Dock = DockPanels.Restore(saved.Layout) },
        };
        ScreenLayout.ConnectLayout(uiState);
        return uiState;
    }
}
