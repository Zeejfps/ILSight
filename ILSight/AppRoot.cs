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
using ILSight.DocumentViewArea;

namespace ILSight;

/// <summary>The composition root: builds the app state and the screen over it, the use cases on them and the
/// adapters those reach the world through, choosing the desktop app's adapters. The launcher routes a run and builds the
/// app through <see cref="RouteRunAsync"/> and <see cref="BuildLaunchedApp"/>, and the tests that run the whole app
/// build it through <see cref="BuildApp"/>; building starts nothing, every host starting it through its loop's
/// <see cref="AppLoop.Start"/>.</summary>
[Edge]
internal static class AppRoot
{
    private const string LicensesFolder = "licenses";

    /// <summary>Where this run's <paramref name="arguments"/> go (<see cref="RunRouter"/>), over the role and the
    /// hand-over of the settings at <paramref name="settingsPath"/>, with no receiver when they were handed to the window
    /// already open, or the hand-over stayed unconfirmed (said on <paramref name="notice"/>, this run's channel when
    /// null, in the language those settings choose), the inbox dropped unread; else the receiver of later runs'
    /// arguments this run holds, the role claimed, or standing by to take it, a <paramref name="newInstance"/> asked for
    /// never handing over. Its inbox holds this run's arguments first, put there before the role is claimed, so a later
    /// run's, sent as soon as it is, come after them.</summary>
    internal static async Task<RoutedRun> RouteRunAsync(FullFilePath settingsPath, RunArguments arguments, bool newInstance,
        CancellationToken ct, RunNotice? notice = null)
    {
        var address = RunArgumentsRouting.GetAddress(settingsPath);
        var inbox = Channel.CreateUnbounded<RunArguments>();
        // Unbounded, and completed only by the app as its window closes: it takes them.
        inbox.Writer.TryWrite(arguments);
        var routing = new RunRouterAdapter(address, inbox, () => ReadRunStrings(settingsPath), notice ?? RunNotice.ForThisRun());
        var route = await new RunRouter(routing).RouteAsync(arguments, newInstance, ct);
        if (route is RunRoute.HandedOver or RunRoute.Unconfirmed)
        {
            return new RoutedRun { Route = route, Receiver = null };
        }

        return new RoutedRun { Route = route, Receiver = routing.Claimed ?? RunArgumentsReceiver.StandBy(address, inbox) };
    }

    /// <summary>The strings of the language the settings at <paramref name="settingsPath"/> choose; the system's when
    /// they choose none or can't be read.</summary>
    internal static Strings ReadRunStrings(FullFilePath settingsPath)
    {
        return Strings.For(ChooseLocale(ReadSettingsFile(settingsPath).Settings));
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
    /// <param name="hooks">What a test host makes of some of the adapters the root builds; the real ones when null.</param>
    internal static BuiltApp BuildApp(SettingsLoadResult loadResult, Channel<RunArguments> inbox, BrowseSession session,
        HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null, ProcessBrowser.IPorts? processes = null,
        AppUpdates.IPorts? updates = null, AdapterHooks? hooks = null)
    {
        var restored = RestoreApp(loadResult, session, nuGetRequests, runtimeScan, processes, updates, hooks);
        var loop = BuildLoop(restored.AppState, restored.Start, inbox);
        var app = new ILSightApp(restored.AppState, restored.Screen, loop, inbox, MakePort<IWindowFront>(new WindowFrontAdapter(), hooks?.BringingToFront));
        return new BuiltApp { Restored = restored, Loop = loop, App = app };
    }

    /// <summary>The app's loop over <paramref name="appState"/>, which every host that runs the whole app runs: the
    /// window, and the test hosts with or without a screen. It starts the reading recorded as <paramref name="start"/>
    /// once it has taken the arguments waiting in <paramref name="inbox"/>. Building it starts nothing.</summary>
    internal static AppLoop BuildLoop(AppState appState, ListStart start, Channel<RunArguments> inbox)
    {
        return new AppLoop(appState, start, new RunArgumentsInbox(appState, inbox), new AssemblyListLoader(appState),
            new UserThemeWatcher(appState.Workflows, appState.UserThemes, appState.UserIconThemes),
            new UpdateChecker(appState.Workflows, appState.Updates), new TabDecompiler(appState),
            new ExportFileChecker(appState.Workflows, appState.CheckExportFilesHandler));
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
    /// <param name="hooks">What a test host makes of some of the adapters the root builds; the real ones when null.</param>
    internal static RestoredApp RestoreApp(SettingsLoadResult loadResult, BrowseSession session,
        HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null, ProcessBrowser.IPorts? processes = null,
        AppUpdates.IPorts? updates = null, AdapterHooks? hooks = null)
    {
        var appState = RestoreStartingState(loadResult, session, updates, hooks);
        var screen = CreateScreen(appState, nuGetRequests, runtimeScan, processes, hooks);
        var start = RecordStart(appState);
        return new RestoredApp { AppState = appState, Screen = screen, Start = start };
    }

    /// <summary>The app state restored from <paramref name="loadResult"/> over <paramref name="session"/>
    /// (<see cref="RestoreStartingState"/>) and its start recorded on it, with no screen: what a host that runs the app
    /// with no window starts (<see cref="AppLoop.Start"/>).</summary>
    /// <param name="loadResult">What reading the settings file gave.</param>
    /// <param name="session">The browse tree the app's trees and searches show nodes of.</param>
    /// <param name="updates">Looks for a newer version of the app; the release feed's when null.</param>
    internal static RestoredState RestoreWithoutScreen(SettingsLoadResult loadResult, BrowseSession session, AppUpdates.IPorts? updates = null)
    {
        var appState = RestoreStartingState(loadResult, session, updates);
        var start = RecordStart(appState);
        return new RestoredState { AppState = appState, Start = start };
    }

    // Reads nothing. The run arguments decide from the list as on its way, so this is their handler's prerequisite: the
    // app takes those already waiting before it starts the reading this returns.
    private static ListStart RecordStart(AppState appState)
    {
        var reopening = appState.ListSession.BeginReopening(appState.Lists);
        var analyses = Analysis.BeginRestoring(appState.ListSession.Analyzer, appState.ListSession.Assemblies);
        return new ListStart { Reopening = reopening, Analyses = analyses };
    }

    /// <summary>The settings in the file at <paramref name="file"/>, which the app starts with, and what was wrong with
    /// it; read from the disk.</summary>
    internal static SettingsLoadResult ReadSettingsFile(FullFilePath file)
    {
        var reading = new SettingsFileReader(new SettingsFileReaderAdapter());
        return reading.ReadFile(file);
    }

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, over
    /// <paramref name="session"/>, told of what was wrong with the settings. The screen is built over this; the run's
    /// arguments reach it once the app runs, through its inbox.</summary>
    internal static AppState RestoreStartingState(SettingsLoadResult loadResult, BrowseSession session, AppUpdates.IPorts? updates = null,
        AdapterHooks? hooks = null)
    {
        var appState = RestoreAppState(loadResult.Settings, session, updates, hooks);
        ReportProblem(appState, Settings.FindProblem(appState.SettingsPlace, loadResult.Problem));
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe over <paramref name="session"/>, with the tab
    /// it starts with active, what the settings keep restored into it, and the user's themes and icon themes read; it
    /// looks for a newer version through <paramref name="updates"/>, the release feed when null, and makes of its adapters what
    /// <paramref name="hooks"/> says, keeping the real ones when null.</summary>
    internal static AppState RestoreAppState(AppSettings settings, BrowseSession session, AppUpdates.IPorts? updates = null,
        AdapterHooks? hooks = null)
    {
        var appState = CreateAppState(settings, session, updates ?? new AppUpdatesAdapter(), hooks);
        appState.ListSession.Navigation.OpenFirstTab();
        var restored = Settings.Restore(appState, settings);
        ShowRefusal(appState, restored);
        appState.UserThemes.ReadThemes();
        appState.UserIconThemes.ReadThemes();
        return appState;
    }

    private static void ReportProblem(AppState appState, AppProblem? problem)
    {
        if (problem is null)
        {
            return;
        }

        Errors.Report(appState, problem);
    }

    private static void ShowRefusal(AppState appState, Refusal? refusal)
    {
        if (refusal is null)
        {
            return;
        }

        appState.Refusals.Show(refusal);
    }

    private static T MakePort<T>(T real, Func<T, T>? hook)
    {
        if (hook is null)
        {
            return real;
        }

        return hook(real);
    }

    private static AppState CreateAppState(AppSettings settings, BrowseSession session, AppUpdates.IPorts updates,
        AdapterHooks? hooks)
    {
        var runtimeDirectories = RuntimeFolders.FindDirectories();
        var runtime = new InstalledRuntime { Directories = runtimeDirectories, FilesByName = RuntimeFolders.IndexFiles(runtimeDirectories) };
        var closable = new ClosableAssemblyList(runtime);
        var assemblies = closable.List;
        var errors = new ErrorLog();
        var refusals = new RefusalLine();
        var work = new BackgroundWork();
        var settingsPlace = new SettingsPlace { FilePath = settings.FilePath, CanSaveOverFile = settings.CanSaveOverFile };
        var themes = Theming.CreateCatalog(BuiltInThemes.Files);
        var iconThemes = IconTheming.CreateCatalog(BuiltInIconThemes.Files);
        var decompileIntoTab = new DecompileIntoTabHandler(new DecompileIntoTabHandlerAdapter(errors, session));
        var navigation = new NavigationInfrastructure(errors, refusals, assemblies, session);
        var assemblyList = new AssemblyListInfrastructure(errors, refusals, assemblies, session);
        var diagrams = new DiagramInfrastructure(errors, refusals, assemblies);
        var bookmarks = new BookmarkInfrastructure(errors, refusals, assemblies, session);
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
            },
            Work = work,
            Workflows = new WorkflowRunner(work, errors),
            DecompileIntoTabHandler = decompileIntoTab,
            NavigateHandler = new NavigateHandler(navigation),
            ShowInMetadataHandler = new ShowInMetadataHandler(navigation),
            ShowInApiReferenceHandler = new ShowInApiReferenceHandler(navigation),
            OpenAndShowIdHandler = new OpenAndShowIdHandler(navigation),
            GoToBookmarkHandler = new GoToBookmarkHandler(MakePort<GoToBookmarkHandler.IPorts>(navigation, hooks?.GoingToBookmarks)),
            ShowReopenedNodesHandler = new ShowReopenedNodesHandler(navigation),
            BookmarkImporter = new BookmarkImporter(bookmarks),
            ExportBookmarksHandler = new ExportBookmarksHandler(bookmarks),
            SettingsSaver = new SettingsSaver(new SettingsSaverAdapter(errors)),
            BuildChildrenHandler = new BuildChildrenHandler(tree),
            LookUpTooltipHandler = new LookUpTooltipHandler(new LookUpTooltipHandlerAdapter(errors, assemblies)),
            RunSearchHandler = new RunSearchHandler(MakePort<RunSearchHandler.IPorts>(new RunSearchHandlerAdapter(session), hooks?.Searching)),
            ExpandNamespacesHandler = new ExpandNamespacesHandler(tree),
            ExportWriter = new ExportWriter(MakePort<ExportWriter.IPorts>(exports, hooks?.Exporting)),
            SaveResourceHandler = new SaveResourceHandler(exports),
            ExtractPackageFileHandler = new ExtractPackageFileHandler(exports),
            CheckExportFilesHandler = new CheckExportFilesHandler(exports),
            DiagramGrower = new DiagramGrower(MakePort<DiagramGrower.IPorts>(diagrams, hooks?.Grower)),
            ReopenDiagramHandler = new ReopenDiagramHandler(diagrams),
            LayOutDiagramHandler = new LayOutDiagramHandler(diagrams),
            CompareAssembliesHandler = new CompareAssembliesHandler(MakePort<CompareAssembliesHandler.IPorts>(new CompareAssembliesHandlerAdapter(errors), hooks?.Comparing)),
            RunAnalysisHandler = new RunAnalysisHandler(analysis),
            AnalyzeReferenceHandler = new AnalyzeReferenceHandler(navigation),
            RestoreAnalysesHandler = new RestoreAnalysesHandler(analysis),
            GoToHistoryEntryHandler = new GoToHistoryEntryHandler(navigation),
            AssemblyOpener = new AssemblyOpener(assemblyList),
            AssemblyReloader = new AssemblyReloader(MakePort<AssemblyReloader.IPorts>(assemblyList, hooks?.Reloader)),
            OpenAssembliesAsReloadHandler = new OpenAssembliesAsReloadHandler(MakePort<OpenAssembliesAsReloadHandler.IPorts>(assemblyList, hooks?.Reopening)),
            Session = session,
            SettingsPlace = settingsPlace,
            KeptScreen = new KeptScreen { View = Settings.ReadViewSettings(settings), Window = settings.Window },
            Errors = errors,
            Refusals = refusals,
            Tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version },
            Themes = themes,
            UserThemes = new UserThemes<ThemeFile, ThemeContents, AppTheme>(new UserColorThemesAdapter(errors)) { Themes = themes, Folder = settingsPlace.ThemesFolder },
            IconThemes = iconThemes,
            UserIconThemes = new UserThemes<IconThemeFile, IconThemeContents, IconTheme>(new UserIconThemesAdapter(errors)) { Themes = iconThemes, Folder = settingsPlace.IconThemesFolder },
            ChosenThemes = new ChosenThemes { Theme = settings.Theme, IconTheme = settings.IconTheme },
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
    /// <param name="hooks">What a test host makes of some of the parts the root builds; the real ones when null.</param>
    internal static UiState CreateScreen(AppState appState, HttpMessageHandler? nuGetRequests = null, FrameworkBrowser.IPorts? runtimeScan = null,
        ProcessBrowser.IPorts? processes = null, AdapterHooks? hooks = null)
    {
        var reportError = (AppProblem problem) => Errors.Report(appState, problem);
        var uiState = CreateUiState(appState.KeptScreen.View, appState.Workflows, reportError, appState.Session, nuGetRequests,
            runtimeScan ?? new FrameworkPickerAdapter(reportError, appState.ListSession.Assemblies.Runtime), processes ?? new ProcessBrowserAdapter(reportError), hooks);
        ScreenSettings.RestoreScreen(uiState, appState.KeptScreen.View);
        OpenRestoredTabs(uiState, appState);
        return uiState;
    }

    // The tabs reopened from the open list's documents docked where they were, any other tab in the place of the
    // documents, and the active tab the screen starts with.
    private static void OpenRestoredTabs(UiState uiState, AppState appState)
    {
        uiState.Documents.SkipPanels(appState.Lists.ListDocumentPanels());
        uiState.TabFocus.RecordActiveTab(appState.ListSession.Navigation.ActiveTab);
        QueuedCommands.RunViewCommands(uiState, appState);
        uiState.Panels.ShowInRestoredDocuments(appState.ListSession.Navigation);
    }

    // Restores, of what the settings kept of the screen, only its layout, display options, folded groups and filters;
    // the rest is restored after.
    private static UiState CreateUiState(ViewSettings saved, WorkflowRunner workflows, Action<AppProblem> reportError, BrowseSession session,
        HttpMessageHandler? nuGetRequests, FrameworkBrowser.IPorts runtimeScan, ProcessBrowser.IPorts processes, AdapterHooks? hooks)
    {
        var appCommands = new AppCommandQueue();
        // A null nuGetRequests uses the app's shared connections.
        var nuGet = new NuGetClient(nuGetRequests);
        var layout = new MainLayout { Dock = DockPanels.Restore(saved.Layout) };
        var documents = new DocumentViews();
        var tabFocus = new TabFocus();
        var search = new Search();
        var nuGetPicker = new NuGetPicker { Browser = new NuGetBrowser() };
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Documents = documents,
            TabFocus = tabFocus,
            Panels = new Panels(layout, MakePort<IDockWidget>(new FrameworkDockWidget(), hooks?.Docking), documents, tabFocus, search, appCommands),
            Tree = new BrowseTree { Session = session },
            DecompilerOptionsPanel = new DecompilerOptionsPanel { Folded = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) } },
            Search = search,
            Omnibar = new Omnibar { Search = new Search() },
            Grids = new MetadataGridScreenState { Filters = MetadataFilters.From(saved.FlagsFilters) },
            ArrangeMetadataGridHandler = new ArrangeMetadataGridHandler(new ArrangeMetadataGridHandlerAdapter()),
            ReadMetadataPageHandler = new ReadMetadataPageHandler(new ReadMetadataPageHandlerAdapter()),
            ShippedLicenses = FindShippedLicenses(),
            Picker = new FrameworkPicker { Browser = new FrameworkBrowser(runtimeScan) },
            NuGetPicker = nuGetPicker,
            ReadNuGetSetupHandler = new ReadNuGetSetupHandler(new ReadNuGetSetupHandlerAdapter(reportError)),
            DownloadNuGetPackageHandler = new DownloadNuGetPackageHandler(new DownloadNuGetPackageHandlerAdapter(reportError, nuGet)),
            NuGetFetcher = new NuGetPickerFetcher(workflows, nuGetPicker, new SearchNuGetFeedHandler(new SearchNuGetFeedHandlerAdapter(reportError, nuGet)),
                new FetchNuGetIconHandler(new FetchNuGetIconHandlerAdapter(nuGet)), new ReadNuGetDetailsHandler(new ReadNuGetDetailsHandlerAdapter(nuGet))),
            ProcessPicker = new ProcessPicker { Browser = new ProcessBrowser(processes) },
            Display = new ScreenDisplay { Options = saved.Display ?? DisplayOptions.Default },
            Analyzer = new AnalyzerRows { AppCommands = appCommands },
        };
        return uiState;
    }

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
