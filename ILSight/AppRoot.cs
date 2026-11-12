using System.Globalization;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.BookmarkArea;
using ILSight.ExportArea;
using ILSight.BuiltInArea;
using ILSight.CodeTooltipArea;
using ILSight.OmnibarArea;
using ILSight.ProcessBrowsing;
using ILSight.FrameworkArea;
using ILSight.UserState;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.DisplayOptionArea;
using ILSight.MetadataGridArea;
using ILSight.NuGetArea;
using ILSight.Searching;
using ILSight.ILSightAppArea;
using ILSight.MainArea;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.SettingsKeeping;
using ILSight.MetadataArea;

namespace ILSight;

/// <summary>The composition root: builds the app state and the screen over it, the use cases on them and the
/// adapters those reach the world through, choosing the desktop app's adapters. The launcher builds the app
/// through it, and so do the tests.</summary>
[Edge]
internal static class AppRoot
{
    private const string LicensesFolder = "licenses";

    /// <summary>The settings in the file at <paramref name="file"/>, which the app starts with, and what was wrong with
    /// it; read from the disk.</summary>
    internal static SettingsLoadResult ReadSettingsFile(FullFilePath file)
    {
        var reading = new SettingsReading(new SettingsReadingAdapter());
        return reading.ReadFile(file);
    }

    /// <summary>The app state to start from: what <paramref name="loadResult"/> describes, over
    /// <paramref name="session"/>, told of what was wrong with the settings, with <paramref name="arguments"/>
    /// followed. The screen is built over this; the files they name open once the app runs.</summary>
    internal static AppState RestoreAppState(SettingsLoadResult loadResult, RunArguments arguments, BrowseSession session)
    {
        var appState = RestoreAppState(loadResult.Settings, session);
        ReportProblem(appState, Settings.FindProblem(appState.SettingsPlace, loadResult.Problem));
        ReportProblem(appState, RunArgumentsActions.FindIgnored(arguments));
        RunArgumentsActions.ApplyStartupOptions(appState, arguments);
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe over <paramref name="session"/>, with the tab
    /// it starts with active, what the settings keep restored into it, and the user's themes and icon themes read.</summary>
    internal static AppState RestoreAppState(AppSettings settings, BrowseSession session)
    {
        var appState = CreateAppState(settings, session);
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
    private static AppState CreateAppState(AppSettings settings, BrowseSession session)
    {
        var assemblies = new AssemblyList { Runtime = new InstalledRuntime { Directories = RuntimeFolders.FindDirectories() } };
        var errors = new ErrorLog();
        var work = new BackgroundWork();
        var opens = new OpenRequests();
        var settingsPlace = new SettingsPlace { FilePath = settings.FilePath, CanSaveOverFile = settings.CanSaveOverFile };
        var themes = Theming.CreateCatalog(BuiltInThemes.Files);
        var iconThemes = IconTheming.CreateCatalog(BuiltInIconThemes.Files);
        var decompileIntoTab = new DecompileIntoTabHandler(new DecompileIntoTabHandlerAdapter(errors, session));
        var navigation = new NavigationInfrastructure(errors, assemblies, session, opens);
        var assemblyList = new AssemblyListInfrastructure(errors, assemblies, session);
        var diagrams = new DiagramInfrastructure(errors, assemblies);
        var bookmarks = new BookmarkInfrastructure(errors, assemblies, session);
        var analysis = new AnalysisInfrastructure(errors, assemblies);
        var exports = new ExportInfrastructure(errors, session);
        var tree = new TreeInfrastructure(session);
        var viewCommands = new Queue<ViewCommand>();
        var appState = new AppState
        {
            ViewCommands = viewCommands,
            ListSession = new OpenListSession
            {
                Assemblies = assemblies,
                Analyzer = new AnalyzerTree(),
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
            OpenAssembliesAsReloadHandler = new OpenAssembliesAsReloadHandler(assemblyList),
            Session = session,
            SettingsPlace = settingsPlace,
            View = Settings.ReadViewSettings(settings),
            Window = settings.Window,
            Errors = errors,
            Tooltips = new CodeTooltips { Assemblies = assemblies, Version = assemblies.Version },
            Themes = themes,
            UserThemes = new UserThemes<ThemeFile, ThemeContents, AppTheme>(new UserColorThemesAdapter(errors)) { Themes = themes, Folder = settingsPlace.ThemesFolder },
            IconThemes = iconThemes,
            UserIconThemes = new UserThemes<IconThemeFile, IconThemeContents, IconTheme>(new UserIconThemesAdapter(errors)) { Themes = iconThemes, Folder = settingsPlace.IconThemesFolder },
            Theme = settings.Theme,
            IconTheme = settings.IconTheme,
            Decompiler = new DecompilerSetup { Language = settings.Language, Options = settings.Decompiler },
            Locale = ChooseLocale(settings),
            ApiVisibility = settings.ApiVisibility,
            Updates = new AppUpdates(new AppUpdatesAdapter()),
            IsUpdateCheckAutomatic = settings.CheckForUpdates,
        };
        AssemblyListing.TryChangeLoadOptions(assemblies, settings.Decompiler);
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
    internal static UiState CreateScreen(AppState appState, HttpMessageHandler? nuGetRequests = null)
    {
        var uiState = CreateUiState(appState.View, appState.Work, problem => Errors.Report(appState, problem), appState.Session, appState.Assemblies.Runtime,
            nuGetRequests);
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
    /// <param name="nuGetRequests">Sends the "open from NuGet" dialog's requests; the app's shared connections when null.</param>
    private static UiState CreateUiState(ViewSettings saved, BackgroundWork work, Action<AppProblem> reportError, BrowseSession session, InstalledRuntime runtime,
        HttpMessageHandler? nuGetRequests)
    {
        var appCommands = new Queue<AppCommand>();
        var uiState = new UiState
        {
            AppCommands = appCommands,
            Tree = new BrowseTree { Session = session },
            FoldedOptionGroups = new FoldedGroups { Folded = new HashSet<string>(saved.FoldedOptionGroups ?? []) },
            Search = new Search(),
            Omnibar = new Omnibar { Search = new Search() },
            MetadataFilters = MetadataGridWidget.CreateFilters(saved.FlagsFilters),
            ShippedLicenses = FindShippedLicenses(),
            Picker = new FrameworkPicker { Browser = new FrameworkBrowser(new FrameworkPickerAdapter(reportError, runtime)) },
            NuGetPicker = new NuGetPicker { Browser = new NuGetBrowser(new NuGetBrowserAdapter(reportError, new NuGetClient(nuGetRequests))) },
            ProcessPicker = new ProcessPicker { Browser = new ProcessBrowser(new ProcessBrowserAdapter(reportError)) },
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
