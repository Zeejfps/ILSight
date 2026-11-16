using ILSight.Core;
using ILSight.Core.Processes;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.ProcessArea;
using ILSight.NuGetArea;
using ILSight.AssemblyArea;
using ILSight.MetadataGridArea;
using ILSight.SettingArea;

namespace ILSight.ILSightAppArea;

/// <summary>What the app does around each frame, keeping the app state and the screen in step.</summary>
[Edge]
internal static class AppFrames
{
    /// <summary>Has the screen, just created over <paramref name="appState"/>, follow what the app state was
    /// restored with: the tabs reopened from the open list's documents docked where they were, any other tab in
    /// the place of the documents, and the active tab it starts with. It takes the theme and strings with its first
    /// frame.</summary>
    internal static void FollowRestoredAppState(UiState uiState, AppState appState)
    {
        uiState.Documents.SkipPanels(appState.Lists.ListDocumentPanels());
        uiState.TabFocus.Follow(appState.ListSession.Navigation.ActiveTab);
        QueuedCommands.RunViewCommands(uiState, appState);
        uiState.Panels.ShowInRestoredDocuments(appState.ListSession.Navigation);
    }

    /// <summary>Does what comes before drawing a frame: the screen takes the theme, icons and strings the
    /// preferences pick, follows the open assemblies and analyses in the trees, what the open tabs show and
    /// the active tab; then the commands the features queued for the view run, the screen follows the tabs
    /// opened and closed in the layout and focuses another tab when none is active; then the open
    /// assemblies are read again when the options that apply as they are read changed, the tabs in
    /// front decompile again when the language or the options changed, and the disk is looked at for the
    /// export dialog's folder and key file when they changed.</summary>
    internal static void StartFrame(UiState uiState, AppState appState)
    {
        uiState.Appearance.ApplyThemeAndLanguage(Theming.ResolveTheme(appState), appState.Locale);
        uiState.Appearance.ApplyIconTheme(IconTheming.ResolveIconTheme(appState));
        Trees.SyncTrees(uiState.Tree, uiState.Analyzer, uiState.Shown, appState.ListSession.Assemblies, appState.ListSession.Analyzer, appState.ApiVisibility, appState.Reopens.Files);
        appState.ListSession.Navigation.UsePreviewTabs(uiState.Display.Options.PreviewTabs);
        uiState.Documents.FollowViewContent();
        uiState.TabFocus.Follow(appState.ListSession.Navigation.ActiveTab);
        QueuedCommands.RunViewCommands(uiState, appState);
        uiState.Panels.SyncTabViews(appState.ListSession.Navigation);
        ReviveClosedTabs(uiState, appState);
        uiState.Panels.FocusNextTabIfNone(appState.ListSession.Navigation, appState.Decompiler.Setup);
        FollowLoadOptions(uiState, appState);
        FollowDecompilation(uiState, appState);
        FollowExportSetup(uiState.ExportSetup, appState);
    }

    private static void ReviveClosedTabs(UiState uiState, AppState appState)
    {
        // Asked first, so the frames with no closed tab listed again allocate nothing.
        if (!SavedDocuments.HasClosedTabListedAgain(appState.ListSession.Navigation, appState.ListSession.Assemblies))
        {
            return;
        }

        Commands.StartLoads(appState, SavedDocuments.ReviveClosedTabs(appState.ListSession, uiState.Documents));
    }

    private static void FollowExportSetup(ExportSetup setup, AppState appState)
    {
        if (!setup.IsOpen)
        {
            return;
        }

        var check = setup.Files.StartCheck(setup.Folder, setup.StrongNameKeyFile);
        if (check is null)
        {
            return;
        }

        StartCheckingFiles(appState, setup.Files, check);
    }

    // A method of its own, so that the frames that start nothing allocate no closure.
    private static void StartCheckingFiles(AppState appState, ExportFileChecks files, ExportFileCheck check)
    {
        appState.Workflows.StartSettling(nameof(CheckExportFilesHandler), ct => appState.CheckExportFilesHandler.HandleAsync(files, check, ct),
            error => files.Fail(check, error.Message));
    }

    /// <summary>Does what comes before the "open from NuGet" dialog draws: hands the package it downloaded over to
    /// be opened, closing it; and while it is open, starts the search what was typed asks for, so that the dialog
    /// draws it searching, or asks for a frame once what is being typed will have settled; and starts fetching the
    /// icons and details the last frame showed for the first time.</summary>
    internal static void FollowNuGetPicker(IFrameHost frame, IAppCommandWriter commands, NuGetPicker picker, SearchNuGetFeedHandler searchNuGetFeedHandler,
        FetchNuGetIconHandler fetchNuGetIconHandler, ReadNuGetDetailsHandler readNuGetDetailsHandler, AppState appState)
    {
        NuGetPickerView.OpenDownloaded(commands, appState.ListSession.Navigation, picker);
        if (!picker.IsOpen)
        {
            return;
        }

        var now = frame.Now;
        var search = picker.Update(now);
        if (search is not null)
        {
            StartSearching(appState, searchNuGetFeedHandler, picker.Browser, search);
        }

        StartFetching(appState.Workflows, fetchNuGetIconHandler, readNuGetDetailsHandler, picker.Browser);

        if (picker.Browser.IsSettling)
        {
            frame.RequestFrameIn(picker.Browser.CalculateTimeToSettle(now));
        }
    }

    // Its token is cancelled too by a newer search and by closing the dialog. A method of its own, so that the frames
    // that start nothing allocate no closure.
    private static void StartSearching(AppState appState, SearchNuGetFeedHandler searchFeed, NuGetBrowser browser, FeedSearch wanted)
    {
        var search = browser.StartSearch(wanted);
        appState.Workflows.StartSettling(nameof(SearchNuGetFeedHandler), ct => searchFeed.HandleAsync(browser, search, ct),
            error => browser.TryFinishSearch(search, new Outcome<FeedPage>.Failed(error)), search.Work.Token);
    }

    /// <summary>Starts a workflow of its own for each icon and each version's details the dialog asked for since
    /// the last frame, so that one that fails does not stop the others, each on a token that closing the dialog
    /// cancels too. Allocates nothing while nothing is asked for.</summary>
    internal static void StartFetching(WorkflowRunner workflows, FetchNuGetIconHandler fetchIcon, ReadNuGetDetailsHandler readDetails, NuGetBrowser browser)
    {
        if (!browser.HasDownloadsToStart)
        {
            return;
        }

        var closed = browser.UntilClosed;
        foreach (var icon in browser.TakeIconsToFetch())
        {
            StartFetchingIcon(workflows, fetchIcon, icon, closed);
        }

        foreach (var details in browser.TakeDetailsToFetch())
        {
            StartReadingDetails(workflows, readDetails, browser, details, closed);
        }
    }

    // Its own method, so that the closure is made only for an icon asked for.
    private static void StartFetchingIcon(WorkflowRunner workflows, FetchNuGetIconHandler fetchIcon, PackageIconFetch icon, CancellationToken closed)
    {
        workflows.Start(nameof(FetchNuGetIconHandler), ct => fetchIcon.HandleAsync(icon, ct), closed);
    }

    // Its own method, so that the closure is made only for details asked for.
    private static void StartReadingDetails(WorkflowRunner workflows, ReadNuGetDetailsHandler readDetails, NuGetBrowser browser,
        VersionDetailsFetch details, CancellationToken closed)
    {
        workflows.StartSettling(nameof(ReadNuGetDetailsHandler), ct => readDetails.HandleAsync(browser, details, ct),
            error => browser.ShowDetails(details, new Outcome<PackageVersionDetails>.Failed(error)), closed);
    }

    private static void FollowLoadOptions(UiState uiState, AppState appState)
    {
        // Their change ends the list's generation, as a clear does: the opens under way commit nothing after it, and are
        // cancelled.
        if (!AssemblyListing.TryChangeLoadOptions(appState.ListSession.Assemblies, appState.Decompiler.Setup.Options))
        {
            return;
        }

        StartRefreshing(appState, uiState.Tree.ListExpandedPaths());
    }

    // Started before the frame draws, so that it draws them reloading. A method of its own, so that the frames that
    // start nothing allocate no closure.
    private static void StartRefreshing(AppState appState, List<NodePath> expanded)
    {
        Commands.StartReloading(appState, expanded);
    }

    private static void FollowDecompilation(UiState uiState, AppState appState)
    {
        var language = appState.Decompiler.Setup.Language;
        if (!uiState.Shown.TryTakeDecompilation(language, appState.Decompiler.Setup.Options))
        {
            return;
        }

        foreach (var tab in uiState.Panels.ListFrontTabs())
        {
            Commands.StartDecompiling(appState, Decompilation.StartDecompilingAgainIfOutdated(tab, language, appState.Decompiler.Setup.Options));
        }
    }

    /// <summary>Starts the work the screen asked for while it drew, and that listing asked for since the last frame:
    /// the type systems of the modules listed, the children of the tree's nodes it showed loading, the searches of the
    /// queries typed, the tooltips of the references hovered in code, the sorts and filters of the metadata grids and the
    /// pages of their rows they showed loading, and the presets the lists dialog's menu opened to.</summary>
    internal static void StartAskedWork(UiState uiState, AppState appState)
    {
        Commands.StartReadingTypeSystems(appState.Workflows, appState.ListSession.Assemblies);
        Commands.StartBuildingChildren(appState, uiState.Tree);
        Commands.StartSearching(appState, uiState.Search.Results);
        Commands.StartSearching(appState, uiState.Omnibar.Search.Results);
        Commands.StartLookingUpTooltips(appState.Workflows, appState.LookUpTooltipHandler, appState.Tooltips);
        Commands.StartArrangingGrids(appState.Workflows, uiState.ArrangeMetadataGridHandler, uiState.Grids.Views);
        Commands.StartReadingGridPages(appState.Workflows, uiState.ReadMetadataPageHandler, uiState.Grids.Pages);
        Commands.StartScanningPresets(appState.Workflows, uiState.Presets);
    }

    /// <summary>Keeps what the screen changed while it drew: the tabs keep where their views stand, and the
    /// settings keep what changed of the screen.</summary>
    internal static void KeepScreenChanges(UiState uiState, AppState appState)
    {
        KeepTabPositions(uiState, appState.ListSession.Navigation);
        KeepChangedViewSettings(uiState, appState.KeptScreen);
        KeepFirstGridRows(uiState, appState);
    }

    private static void KeepFirstGridRows(UiState uiState, AppState appState)
    {
        var area = uiState.Documents.TabAreaHeight;
        if (area is null)
        {
            return;
        }

        appState.FirstGridRows = MetadataGridFrame.CalculateFirstReadRows(area.Value, uiState.Appearance.Theme.RowHeight);
    }

    private static void KeepTabPositions(UiState uiState, Navigation navigation)
    {
        var open = navigation.OpenTabs;
        for (var i = 0; i < open.Count; i++)
        {
            KeepTabPosition(uiState, navigation, open[i]);
        }
    }

    private static void KeepTabPosition(UiState uiState, Navigation navigation, DocumentTab tab)
    {
        var view = uiState.Documents.FindView(tab);
        if (view is null)
        {
            return;
        }

        var shown = view.Shown;
        if (shown is null)
        {
            return;
        }

        if (shown.IsShownBy(tab))
        {
            tab.Load.KeepPosition(view.GetPosition(tab));
            return;
        }

        // The tab moved on while the frame was drawn: the history entry left from it keeps the position.
        navigation.KeepPositionLeft(shown, view.GetShownPosition());
    }

    private static void KeepChangedViewSettings(UiState uiState, KeptScreen kept)
    {
        var view = ScreenSettings.TakeChangedViewSettings(uiState);
        if (view is null)
        {
            return;
        }

        kept.KeepView(view);
    }

    /// <summary>Runs what the screen asked of the app state, and asks for another frame to show what it changed.</summary>
    internal static void RunAppCommandsAndRedraw(IFrameHost frame, AppCommandQueue commands, AppState appState)
    {
        if (commands.IsEmpty)
        {
            return;
        }

        QueuedCommands.RunAppCommands(commands, appState);
        frame.RequestFrame();
    }

    /// <summary>Asks for another frame while commands the features queued for the screen wait.</summary>
    internal static void RequestUpdateIfViewCommandsPending(IFrameHost frame, AppState appState)
    {
        if (appState.ViewCommands.Count == 0)
        {
            return;
        }

        frame.RequestFrame();
    }

    /// <summary>Opens the active list's assemblies <paramref name="reopening"/> began reopening as a reload, so that the
    /// list is saved whole until they are all open (files that are gone or fail to load stay listed as failed), once the
    /// app is running. Returns the loads that started in the tabs reopened from the list's saved documents.</summary>
    internal static Task<TabLoads> OpenStartupAssembliesAsync(AppState appState, ListReopening reopening, CancellationToken ct)
    {
        return appState.OpenAssembliesAsReloadHandler.HandleAsync(appState.ListSession, reopening.Paths, reopening.Reload, reopening.Work, ct);
    }

    /// <summary>Saves the settings when they changed; returns what it came to.</summary>
    internal static Saving SaveChangedSettings(IFrameHost frame, AppState appState)
    {
        return appState.SettingsSaving.SaveIfChanged(appState, frame.Now);
    }
}
