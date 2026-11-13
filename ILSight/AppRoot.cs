using System.Globalization;
using System.Threading.Channels;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.BookmarkArea;
using ILSight.ExportArea;
using ILSight.BuiltInArea;
using ILSight.CodeTooltipArea;
using ILSight.OmnibarArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.UserArea;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.MetadataGridArea;
using ILSight.NuGetArea;
using ILSight.SearchArea;
using ILSight.ILSightAppArea;
using ILSight.MainArea;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.SettingArea;
using ILSight.MetadataArea;

namespace ILSight;

/// <summary>The composition root: builds the app state and the screen over it, the use cases on them and the
/// adapters those reach the world through, choosing the desktop app's adapters. The launcher routes a run and builds the
/// app through <see cref="RouteRunAsync"/> and <see cref="BuildLaunchedApp"/>, and the tests that run the whole app
/// build it through <see cref="BuildApp"/>; building starts nothing, every host starting it through
/// <see cref="AppStart.Start"/>.</summary>
[Edge]
internal static class AppRoot
{
    private const string LicensesFolder = "licenses";

    /// <summary>Where this run's <paramref name="arguments"/> go (<see cref="RunRouting"/>), over the role and the
    /// hand-over of the settings at <paramref name="settingsPath"/>: null when they were handed to the window already
    /// open; else the receiver of later runs' arguments this run holds, the role claimed, or standing by to take it, a
    /// <paramref name="newInstance"/> asked for never handing over.</summary>
    internal static async Task<RunArgumentsReceiver?> RouteRunAsync(FullFilePath settingsPath, RunArguments arguments, bool newInstance,
        CancellationToken ct)
    {
        var address = RunArgumentsRouting.GetAddress(settingsPath);
        var routing = new RunRoutingAdapter(address);
        var route = await new RunRouting(routing).RouteAsync(arguments, newInstance, ct);
        if (route == RunRoute.HandedOver)
        {
            return null;
        }

        return routing.Claimed ?? RunArgumentsReceiver.StandBy(address);
    }

    /// <summary>The whole app the launcher opens a window on: the settings at <paramref name="settingsPath"/> read, and
    /// the app built from them over a browse tree of its own, taking each run's arguments, this one's first, from
    /// <paramref name="inbox"/> (<see cref="BuildApp"/>).</summary>
    internal static LaunchedApp BuildLaunchedApp(FullFilePath settingsPath, Channel<RunArguments> inbox)
    {
        var loadResult = ReadSettingsFile(settingsPath);
        var built = BuildApp(loadResult, inbox, new BrowseSession());
        return new LaunchedApp { App = built.App, Window = loadResult.Settings.Window };
    }

    /// <summary>The whole app as it starts, from what the launcher has: the app state restored from
    /// <paramref name="loadResult"/> over <paramref name="session"/>, with its start recorded, the screen over it
    /// (<see cref="RestoreApp"/>), and the app that runs them in a window, taking each run's arguments, this one's
    /// first, from <paramref name="inbox"/>.</summary>
    /// <param name="loadResult">What reading the settings file gave.</param>
    /// <param name="inbox">Where the arguments of this run, put there first, and of the runs handed to this one arrive.</param>
    /// <param name="session">The browse tree the app's trees and searches show nodes of.</param>
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    /// <param name="runtimeScan">Scans the runtime for the "open from framework" dialog; the installed runtime's when null.</param>
    /// <param name="processes">Lists the running processes for the "open from process" dialog; this machine's when null.</param>
    /// <param name="updates">Looks for a newer version of the app; the release feed's when null.</param>
    /// <param name="reopening">Makes the port the reopening of a list lists its files through, from the real adapter;
    /// the real adapter itself when null.</param>
    internal static BuiltApp BuildApp(SettingsLoadResult loadResult, Channel<RunArguments> inbox, BrowseSession session,
        HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null, ProcessBrowser.IPorts? processes = null,
        AppUpdates.IPorts? updates = null, Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? reopening = null)
    {
        var restored = RestoreApp(loadResult, session, nuGetRequests, runtimeScan, processes, updates, reopening);
        var app = new ILSightApp(restored.AppState, restored.Screen, restored.Start, inbox);
        return new BuiltApp { Restored = restored, App = app };
    }

    /// <summary>The app state restored from <paramref name="loadResult"/> over <paramref name="session"/>
    /// (<see cref="RestoreStartingState"/>), the screen over it (<see cref="CreateScreen"/>), and its start recorded on
    /// it (<see cref="RecordStart"/>), with no window: what <see cref="BuildApp"/> runs in one.</summary>
    /// <param name="loadResult">What reading the settings file gave.</param>
    /// <param name="session">The browse tree the app's trees and searches show nodes of.</param>
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    /// <param name="runtimeScan">Scans the runtime for the "open from framework" dialog; the installed runtime's when null.</param>
    /// <param name="processes">Lists the running processes for the "open from process" dialog; this machine's when null.</param>
    /// <param name="updates">Looks for a newer version of the app; the release feed's when null.</param>
    /// <param name="reopening">Makes the port the reopening of a list lists its files through, from the real adapter;
    /// the real adapter itself when null.</param>
    internal static RestoredApp RestoreApp(SettingsLoadResult loadResult, BrowseSession session,
        HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null, ProcessBrowser.IPorts? processes = null,
        AppUpdates.IPorts? updates = null, Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? reopening = null)
    {
        var appState = RestoreStartingState(loadResult, session, updates, reopening);
        var screen = CreateScreen(appState, nuGetRequests, runtimeScan, processes);
        var start = RecordStart(appState);
        return new RestoredApp { AppState = appState, Screen = screen, Start = start };
    }

    /// <summary>The app state restored from <paramref name="loadResult"/> over <paramref name="session"/>
    /// (<see cref="RestoreStartingState"/>) and its start recorded on it, with no screen: what a host that runs the app
    /// with no window starts (<see cref="AppStart.Start"/>).</summary>
    /// <param name="loadResult">What reading the settings file gave.</param>
    /// <param name="session">The browse tree the app's trees and searches show nodes of.</param>
    /// <param name="updates">Looks for a newer version of the app; the release feed's when null.</param>
    internal static RestoredState RestoreWithoutScreen(SettingsLoadResult loadResult, BrowseSession session, AppUpdates.IPorts? updates = null)
    {
        var appState = RestoreStartingState(loadResult, session, updates);
        var start = RecordStart(appState);
        return new RestoredState { AppState = appState, Start = start };
    }

    /// <summary>Records on <paramref name="appState"/> what starting implies, reading nothing: the active list's
    /// reopening begun as a reload, its files on their way (<see cref="OpenListSession.BeginReopening"/>), and the
    /// finding of its saved analyses (<see cref="Analysis.BeginRestoring"/>). The run arguments decide from the list as
    /// on its way, so this is their handler's prerequisite: the app takes those already waiting before it starts the
    /// reading this returns.</summary>
    private static ListStart RecordStart(AppState appState)
    {
        var reopening = appState.ListSession.BeginReopening(appState.Lists);
        var analyses = Analysis.BeginRestoring(appState.Analyzer, appState.Assemblies);
        return new ListStart { Reopening = reopening, Analyses = analyses };
    }

    /// <summary>The settings in the file at <paramref name="file"/>, which the app starts with, and what was wrong with
    /// it; read from the disk.</summary>
    internal static SettingsLoadResult ReadSettingsFile(FullFilePath file)
    {
        var reading = new SettingsReading(new SettingsReadingAdapter());
        return reading.ReadFile(file);
    }

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, over
    /// <paramref name="session"/>, told of what was wrong with the settings. The screen is built over this; the run's
    /// arguments reach it once the app runs, through its inbox.</summary>
    internal static AppState RestoreStartingState(SettingsLoadResult loadResult, BrowseSession session, AppUpdates.IPorts? updates = null,
        Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? reopening = null)
    {
        var appState = RestoreAppState(loadResult.Settings, session, updates, reopening);
        ReportProblem(appState, Settings.FindProblem(appState.SettingsPlace, loadResult.Problem));
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe over <paramref name="session"/>, with the tab
    /// it starts with active, what the settings keep restored into it, and the user's themes and icon themes read; it
    /// looks for a newer version through <paramref name="updates"/>, the release feed when null, and lists a reopened
    /// list's files through the port <paramref name="reopening"/> makes from the real adapter, that adapter when null.</summary>
    internal static AppState RestoreAppState(AppSettings settings, BrowseSession session, AppUpdates.IPorts? updates = null,
        Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? reopening = null)
    {
        var appState = CreateAppState(settings, session, updates ?? new AppUpdatesAdapter(), reopening);
        appState.Navigation.OpenFirstTab();
        ReportProblem(appState, Settings.Restore(appState, settings));
        appState.UserThemes.ReadThemes();
        appState.UserIconThemes.ReadThemes();
        return appState;
    }

    /// <summary>Tells the user <paramref name="problem"/>, if anything is wrong.</summary>
    private static void ReportProblem(AppState appState, AppProblem? problem)
    {
        if (problem is null)
        {
            return;
        }

        Errors.Report(appState, problem);
    }

    /// <summary>The app state <paramref name="settings"/> describe, with its use cases over the adapters they
    /// reach the world through, before anything is restored into it.</summary>
    private static AppState CreateAppState(AppSettings settings, BrowseSession session, AppUpdates.IPorts updates,
        Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? reopening)
    {
        var runtimeDirectories = RuntimeFolders.FindDirectories();
        var runtime = new InstalledRuntime { Directories = runtimeDirectories, FilesByName = RuntimeFolders.IndexFiles(runtimeDirectories) };
        var closable = new ClosableAssemblyList(runtime);
        var assemblies = closable.List;
        var errors = new ErrorLog();
        var refusals = new RefusalLine();
        var work = new BackgroundWork();
        var opens = new OpenRequests();
        var settingsPlace = new SettingsPlace { FilePath = settings.FilePath, CanSaveOverFile = settings.CanSaveOverFile };
        var themes = Theming.CreateCatalog(BuiltInThemes.Files);
        var iconThemes = IconTheming.CreateCatalog(BuiltInIconThemes.Files);
        var decompileIntoTab = new DecompileIntoTabHandler(new DecompileIntoTabHandlerAdapter(errors, session));
        var navigation = new NavigationInfrastructure(errors, assemblies, session, opens);
        var assemblyList = new AssemblyListInfrastructure(errors, refusals, assemblies, session);
        var diagrams = new DiagramInfrastructure(errors, assemblies);
        var bookmarks = new BookmarkInfrastructure(errors, assemblies, session);
        var analysis = new AnalysisInfrastructure(errors, assemblies);
        var exports = new ExportInfrastructure(errors, refusals, session);
        var tree = new TreeInfrastructure(session);
        var viewCommands = new Queue<ViewCommand>();
        var appState = new AppState
        {
            ViewCommands = viewCommands,
            ListSession = new OpenListSession(closable)
            {
                Navigation = new Navigation { ViewCommands = viewCommands, PreviewTabs = (settings.Display ?? DisplayOptions.Default).PreviewTabs },
                Reopened = new ReopenedTabs(),
                Reload = new Reload(),
                Opens = opens,
            },
            Work = work,
            Workflows = new WorkflowRunner(work, errors),
            DecompileIntoTabHandler = decompileIntoTab,
            NavigateHandler = new NavigateHandler(navigation),
            ShowInMetadataHandler = new ShowInMetadataHandler(navigation),
            ShowInApiReferenceHandler = new ShowInApiReferenceHandler(navigation),
            OpenAndShowIdHandler = new OpenAndShowIdHandler(navigation),
            GoToBookmarkHandler = new GoToBookmarkHandler(navigation),
            ShowReopenedNodesHandler = new ShowReopenedNodesHandler(navigation),
            BookmarkImporting = new BookmarkImporting(bookmarks),
            ExportBookmarksHandler = new ExportBookmarksHandler(bookmarks),
            SettingsSaving = new SettingsSaving(new SettingsSavingAdapter(errors)),
            BuildChildrenHandler = new BuildChildrenHandler(tree),
            LookUpTooltipHandler = new LookUpTooltipHandler(new LookUpTooltipHandlerAdapter(assemblies)),
            RunSearchHandler = new RunSearchHandler(new RunSearchHandlerAdapter(errors, session)),
            ExpandNamespacesHandler = new ExpandNamespacesHandler(tree),
            ExportWriting = new ExportWriting(exports),
            SaveResourceHandler = new SaveResourceHandler(exports),
            ExtractPackageFileHandler = new ExtractPackageFileHandler(exports),
            CheckExportFilesHandler = new CheckExportFilesHandler(exports),
            DiagramGrowing = new DiagramGrowing(diagrams),
            ReopenDiagramHandler = new ReopenDiagramHandler(diagrams),
            LayOutDiagramHandler = new LayOutDiagramHandler(diagrams),
            CompareAssembliesHandler = new CompareAssembliesHandler(new CompareAssembliesHandlerAdapter(errors)),
            RunAnalysisHandler = new RunAnalysisHandler(analysis),
            AnalyzeReferenceHandler = new AnalyzeReferenceHandler(navigation),
            RestoreAnalysesHandler = new RestoreAnalysesHandler(analysis),
            GoToHistoryEntryHandler = new GoToHistoryEntryHandler(navigation),
            AssemblyOpening = new AssemblyOpening(assemblyList),
            AssemblyReloading = new AssemblyReloading(assemblyList),
            OpenAssembliesAsReloadHandler = new OpenAssembliesAsReloadHandler(reopening is null ? assemblyList : reopening(assemblyList)),
            Session = session,
            SettingsPlace = settingsPlace,
            View = Settings.ReadViewSettings(settings),
            Window = settings.Window,
            Errors = errors,
            Refusals = refusals,
            Tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version },
            Themes = themes,
            UserThemes = new UserThemes<ThemeFile, ThemeContents, AppTheme>(new UserColorThemesAdapter(errors)) { Themes = themes, Folder = settingsPlace.ThemesFolder },
            IconThemes = iconThemes,
            UserIconThemes = new UserThemes<IconThemeFile, IconThemeContents, IconTheme>(new UserIconThemesAdapter(errors)) { Themes = iconThemes, Folder = settingsPlace.IconThemesFolder },
            Theme = settings.Theme,
            IconTheme = settings.IconTheme,
            Decompiler = new DecompilerChoice { Setup = new DecompilerSetup { Language = settings.Language, Options = settings.Decompiler } },
            Locale = ChooseLocale(settings),
            ApiVisibility = settings.ApiVisibility,
            Updates = new AppUpdates(updates),
            IsUpdateCheckAutomatic = settings.CheckForUpdates,
        };
        AssemblyListing.TryChangeLoadOptions(assemblies, settings.Decompiler);
        appState.FirstGridRows = MetadataGridFrame.CalculateFirstReadRows(settings.Window?.Height ?? 0, Theming.ResolveTheme(appState).Ui.RowHeight);
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
    /// <param name="appState">The app state the screen shows.</param>
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    /// <param name="runtimeScan">Scans the runtime for the "open from framework" dialog; the installed runtime's when null.</param>
    /// <param name="processes">Lists the running processes for the "open from process" dialog; this machine's when null.</param>
    internal static UiState CreateScreen(AppState appState, HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null,
        ProcessBrowser.IPorts? processes = null)
    {
        var reportError = (AppProblem problem) => Errors.Report(appState, problem);
        var uiState = CreateUiState(appState.View, appState.Work, reportError, appState.Session, nuGetRequests,
            runtimeScan ?? new FrameworkPickerAdapter(reportError, appState.Assemblies.Runtime), processes ?? new ProcessBrowserAdapter(reportError));
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
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    /// <param name="runtimeScan">Scans the runtime for the "open from framework" dialog.</param>
    /// <param name="processes">Lists the running processes for the "open from process" dialog.</param>
    private static UiState CreateUiState(ViewSettings saved, BackgroundWork work, Action<AppProblem> reportError, BrowseSession session,
        HttpMessageHandler? nuGetRequests, FrameworkBrowser.IPorts runtimeScan, ProcessBrowser.IPorts processes)
    {
        var appCommands = new Queue<AppCommand>();
        var nuGet = new NuGetClient(nuGetRequests);
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Tree = new BrowseTree { Session = session },
            FoldedOptionGroups = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) },
            Search = new Search(),
            Omnibar = new Omnibar { Search = new Search() },
            MetadataFilters = MetadataGridWidget.CreateFilters(saved.FlagsFilters),
            ArrangeMetadataGridHandler = new ArrangeMetadataGridHandler(new ArrangeMetadataGridHandlerAdapter()),
            ReadMetadataPageHandler = new ReadMetadataPageHandler(new ReadMetadataPageHandlerAdapter()),
            ShippedLicenses = FindShippedLicenses(),
            Picker = new FrameworkPicker { Browser = new FrameworkBrowser(runtimeScan) },
            NuGetPicker = new NuGetPicker { Browser = new NuGetBrowser() },
            ReadNuGetSetupHandler = new ReadNuGetSetupHandler(new ReadNuGetSetupHandlerAdapter(reportError)),
            SearchNuGetFeedHandler = new SearchNuGetFeedHandler(new SearchNuGetFeedHandlerAdapter(reportError, nuGet)),
            DownloadNuGetPackageHandler = new DownloadNuGetPackageHandler(new DownloadNuGetPackageHandlerAdapter(reportError, nuGet)),
            FetchNuGetIconHandler = new FetchNuGetIconHandler(new FetchNuGetIconHandlerAdapter(nuGet)),
            ReadNuGetDetailsHandler = new ReadNuGetDetailsHandler(new ReadNuGetDetailsHandlerAdapter(nuGet)),
            ProcessPicker = new ProcessPicker { Browser = new ProcessBrowser(processes) },
            Display = saved.Display ?? DisplayOptions.Default,
            Analyzer = new AnalyzerRows { AppCommands = appCommands },
            MainLayout = new MainLayout { Dock = DockPanels.Restore(saved.Layout) },
        };
        ScreenLayout.ConnectLayout(uiState);
        return uiState;
    }

    /// <summary>The folder of the licenses the app ships beside it; null when it ships none there.</summary>
    private static string? FindShippedLicenses()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, LicensesFolder);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        return folder;
    }
}
