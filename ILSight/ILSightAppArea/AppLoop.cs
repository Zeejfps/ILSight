using ILSight.AssemblyArea;
using ILSight.CommandArea;
using ILSight.MetadataGridArea;
using ILSight.ExportArea;
using ILSight.NuGetArea;
using ILSight.UserArea;

namespace ILSight.ILSightAppArea;

/// <summary>The app's loop, the one sequence every host that runs the whole app runs: its start, and the calls before
/// and after each frame draws. The composition root builds it (<see cref="AppRoot.BuildLoop"/>); the window
/// (<see cref="ILSightApp"/>) owns one, its startup hook calling <see cref="Start"/> after its own setup and each
/// frame calling <see cref="BeforeDraw"/>, the draw, <see cref="AfterDraw"/>, then its own chores; the test hosts build
/// one and run the same. Building starts nothing.</summary>
[Edge]
public sealed class AppLoop
{
    private readonly AppState _appState;
    private readonly ListStart _start;
    private readonly RunArgumentsInbox _runArgumentsInbox;
    private readonly AssemblyListLoader _listLoader;
    private readonly UserThemeWatcher _themeWatcher;
    private readonly UpdateChecker _updateChecker;
    private readonly TabDecompiler _tabDecompiler;
    private readonly ExportFileChecker _exportFileChecker;

    /// <param name="appState">The app state the root built, its start recorded on it.</param>
    /// <param name="start">The start the root recorded on <paramref name="appState"/>: the active list's reopening
    /// begun, nothing read.</param>
    /// <param name="runArgumentsInbox">Holds the arguments of this run and of later ones.</param>
    /// <param name="listLoader">Loads the open list's assemblies and the tabs showing them.</param>
    /// <param name="themeWatcher">Watches the user's theme folders.</param>
    /// <param name="updateChecker">Looks for a newer version as the app starts.</param>
    /// <param name="tabDecompiler">Decompiles the open tabs again when the language or the options change.</param>
    /// <param name="exportFileChecker">Looks at the disk for the export dialog's folder and key file.</param>
    internal AppLoop(AppState appState, ListStart start, RunArgumentsInbox runArgumentsInbox, AssemblyListLoader listLoader,
        UserThemeWatcher themeWatcher, UpdateChecker updateChecker, TabDecompiler tabDecompiler, ExportFileChecker exportFileChecker)
    {
        _appState = appState;
        _start = start;
        _runArgumentsInbox = runArgumentsInbox;
        _listLoader = listLoader;
        _themeWatcher = themeWatcher;
        _updateChecker = updateChecker;
        _tabDecompiler = tabDecompiler;
        _exportFileChecker = exportFileChecker;
    }

    /// <summary>Starts what the app runs from its start until it shuts down: takes the run arguments already waiting,
    /// this run's first, then starts the reading of the list it starts with, the taking of later runs' arguments, the
    /// watch of the user's theme folders, and the look for a newer version, unless the user turned it off.</summary>
    public void Start()
    {
        // The arguments before the list's reading: they decide from the list as on its way (a switch remembers it, a
        // clear drops its files on their way), so their prerequisite is the start recorded, the reopening begun and
        // nothing read; taken first, a switch or a clear stops that reading before it reads a file of the list it leaves.
        _runArgumentsInbox.ApplyWaiting();
        _listLoader.StartOpeningStartupList(_start);
        _runArgumentsInbox.StartApplyingLaterRuns();
        _themeWatcher.StartWatching();
        _updateChecker.CheckIfAutomatic(_appState.IsUpdateCheckAutomatic);
    }

    /// <summary>What comes before <paramref name="screen"/> draws a frame in <paramref name="frame"/>: the screen takes
    /// the theme, icons and strings the preferences pick, rebuilds the trees when the open assemblies or analyses
    /// changed, takes what the open tabs show and the active tab; the commands the features queued for the screen run,
    /// it adds and drops the views of the tabs opened and closed, revives the tabs listed again, and focuses another tab
    /// when none is active; then the open assemblies are read again when the options that apply as they are read
    /// changed, the tabs in front decompile again when the language or the options changed, the disk is looked at for
    /// the export dialog's folder and key file when they changed, and the "open from NuGet" dialog hands the package it
    /// downloaded over to be opened and starts what it asks for.</summary>
    public void BeforeDraw(UiState screen, IFrameHost frame)
    {
        var appState = _appState;
        var navigation = appState.ListSession.Navigation;
        screen.Appearance.ApplyThemeAndLanguage(Theming.ResolveTheme(appState), appState.Locale);
        screen.Appearance.ApplyIconTheme(IconTheming.ResolveIconTheme(appState));
        Trees.SyncTrees(screen.Tree, screen.Analyzer, screen.Shown, appState.ListSession.Assemblies, appState.ListSession.Analyzer, appState.ApiVisibility, appState.Reopens.Files);
        navigation.UsePreviewTabs(screen.Display.Options.PreviewTabs);
        screen.Documents.FollowViewContent();
        screen.TabFocus.Follow(navigation.ActiveTab);
        QueuedCommands.RunViewCommands(screen, appState);
        screen.Panels.SyncTabViews(navigation);
        _listLoader.ReviveClosedTabsListedAgain(screen.Documents);
        screen.Panels.FocusNextTabIfNone(navigation, appState.Decompiler.Setup);
        _listLoader.ReloadListWhenLoadOptionsChange(screen.Tree);
        _tabDecompiler.DecompileOpenTabsAgainWhenSettingsChange(screen.Shown, screen.Panels);
        _exportFileChecker.CheckExportFilesWhenFolderChanges(screen.ExportSetup);
        NuGetPickerView.OpenDownloaded(screen.AppCommands, navigation, screen.NuGetPicker);
        screen.NuGetFetcher.StartAskedNuGetWork(frame);
    }

    /// <summary>What comes after <paramref name="screen"/> drew a frame in <paramref name="frame"/>, the draw failing
    /// or not: the tabs keep where their views stand, the settings what changed of the screen, and the metadata grids
    /// how many rows they read first; the work the screen asked for while it drew starts, and that listing asked for
    /// since the last frame (the type systems of the modules listed, the children of the tree's nodes shown loading, the
    /// searches of the queries typed, the tooltips of the references hovered in code, the sorts, filters and pages of
    /// the metadata grids, the presets the lists dialog's menu opened to); the app runs what the screen asked of it,
    /// asking for another frame while there is more to show; and the changed settings are saved. Returns what the
    /// autosave came to, for a host with frames to come back when a write waits or failed.</summary>
    public Saving AfterDraw(UiState screen, IFrameHost frame)
    {
        var appState = _appState;
        var workflows = appState.Workflows;
        screen.Documents.KeepTabPositions(appState.ListSession.Navigation);
        ScreenSettings.KeepChangedViewSettings(screen, appState.KeptScreen);
        appState.FirstGridRows = MetadataGridFrame.CalculateFirstReadRowsInTabArea(screen.Documents.TabAreaHeight, screen.Appearance.Theme.RowHeight, appState.FirstGridRows);
        Commands.StartReadingTypeSystems(workflows, appState.ListSession.Assemblies);
        Commands.StartBuildingChildren(appState, screen.Tree);
        Commands.StartSearching(appState, screen.Search.Results);
        Commands.StartSearching(appState, screen.Omnibar.Search.Results);
        Commands.StartLookingUpTooltips(workflows, appState.LookUpTooltipHandler, appState.Tooltips);
        Commands.StartArrangingGrids(workflows, screen.ArrangeMetadataGridHandler, screen.Grids.Views);
        Commands.StartReadingGridPages(workflows, screen.ReadMetadataPageHandler, screen.Grids.Pages);
        Commands.StartScanningPresets(workflows, screen.Presets);
        QueuedCommands.RunAppCommandsAndRedraw(frame, screen.AppCommands, appState);
        QueuedCommands.RequestFrameWhileViewCommandsWait(frame, appState.ViewCommands);
        return appState.SettingsSaving.SaveIfChanged(appState, frame.Now);
    }
}
