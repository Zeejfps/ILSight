using System.Diagnostics;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Processes;
using ILSight.Exporting;
using ILSight.Commanding;
using ILSight.ProcessBrowsing;
using ILSight.NuGet;
using ILSight.AssemblyHandling;
using static ILSight.AssemblyListKeeping.AssemblyLists;
using static ILSight.CodeDisplay;
using static ILSight.Decompilation;
using static ILSight.SettingsKeeping.Settings;
using static ILSight.AppHosting.Appearance;
using static ILSight.DocumentPanels;

using static ILSight.AppHosting.RunArgumentsActions;
using static ILSight.IconTheming;
using static ILSight.AppHosting.ScreenSettings;
using static ILSight.Theming;
using static ILSight.Trees;

namespace ILSight.AppHosting;

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
        DocumentPanels.SkipPanels(uiState.Documents, appState.Lists.ListDocumentPanels());
        FollowActiveTab(uiState, appState.Navigation.ActiveTab);
        QueuedCommands.RunViewCommands(uiState, appState);
        ShowInRestoredDocuments(uiState, appState.Navigation);
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
        ApplyThemeAndLanguage(uiState, ResolveTheme(appState), appState.Locale);
        ApplyIconTheme(uiState, ResolveIconTheme(appState));
        SyncTrees(uiState, appState.Assemblies, appState.Analyzer, appState.ApiVisibility, appState.Reopens.Files);
        appState.Navigation.UsePreviewTabs(uiState.Display.PreviewTabs);
        FollowTabContent(uiState);
        FollowActiveTab(uiState, appState.Navigation.ActiveTab);
        QueuedCommands.RunViewCommands(uiState, appState);
        SyncTabViews(uiState, appState.Navigation);
        FocusNextTabIfNone(uiState, appState.Navigation, appState.Decompiler);
        FollowLoadOptions(uiState, appState);
        FollowDecompilation(uiState, appState);
        FollowExportSetup(uiState.ExportSetup, appState);
        ShowNewErrors(uiState, appState.Errors);
    }

    /// <summary>While the export dialog is open, starts looking at what it names on the disk now, before the frame
    /// draws, when the names changed since the last look (<see cref="CheckExportFilesHandler"/>), counted in the app's
    /// background work until it ends.</summary>
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

    /// <summary>A method of its own, so that the frames that start nothing allocate no closure.</summary>
    private static void StartCheckingFiles(AppState appState, ExportFileChecks files, ExportFileCheck check)
    {
        appState.Workflows.Start(nameof(CheckExportFilesHandler), ct => appState.CheckExportFilesHandler.HandleAsync(files, check, ct));
    }

    /// <summary>Does what comes before the "open from NuGet" dialog draws: hands the package it downloaded over to
    /// be opened, closing it; and while it is open, starts the search what was typed asks for, so that the dialog
    /// draws it searching, or asks for a frame once what is being typed will have settled; and starts fetching the
    /// icons and details the last frame showed for the first time.</summary>
    internal static void FollowNuGetPicker(Ui<UiState> ui, AppState appState)
    {
        var picker = ui.State.NuGetPicker;
        NuGetPickerView.OpenDownloaded(ui, appState.Navigation, picker);
        if (!picker.IsOpen)
        {
            return;
        }

        var now = ui.RealTime;
        var search = picker.Update(now);
        if (search is not null)
        {
            StartSearching(appState, picker.Browser, search);
        }

        StartFetching(appState.Workflows, picker.Browser);

        if (picker.Browser.IsSettling)
        {
            ui.RequestUpdateIn(picker.Browser.CalculateSecondsToSettle(now));
        }
    }

    /// <summary>Does what comes before the "open from running process" dialog draws: while it is open, takes in
    /// the filter typed, and starts listing the modules of the process selected once it is another than the one
    /// whose modules are shown, so that the dialog draws them loading.</summary>
    internal static void FollowProcessPicker(UiState uiState, AppState appState)
    {
        var picker = uiState.ProcessPicker;
        if (!picker.IsOpen)
        {
            return;
        }

        var process = picker.Update();
        if (process is not null)
        {
            StartListingModules(appState, picker.Browser, process);
        }
    }

    /// <summary>Starts listing the modules of <paramref name="process"/> now, before the frame draws, on a token
    /// that picking another process and closing the dialog cancel too. A method of its own, so that the frames
    /// that start nothing allocate no closure.</summary>
    private static void StartListingModules(AppState appState, ProcessBrowser browser, RunningProcess process)
    {
        var listing = browser.StartListing(process);
        appState.Workflows.Start(nameof(ProcessBrowser.ListModulesAsync), ct => browser.ListModulesAsync(listing, ct), listing.Work.Token);
    }

    /// <summary>Starts <paramref name="wanted"/> now, before the frame draws, on a token that a newer search and
    /// closing the dialog cancel too. A method of its own, so that the frames that start nothing allocate no
    /// closure.</summary>
    private static void StartSearching(AppState appState, NuGetBrowser browser, FeedSearch wanted)
    {
        var search = browser.StartSearch(wanted);
        appState.Workflows.Start(nameof(NuGetBrowser.SearchAsync), ct => browser.SearchAsync(search, ct), search.Work.Token);
    }

    /// <summary>Starts a workflow of its own for each icon and each version's details the dialog asked for since
    /// the last frame, so that one that fails does not stop the others, each on a token that closing the dialog
    /// cancels too. Allocates nothing while nothing is asked for.</summary>
    internal static void StartFetching(WorkflowRunner workflows, NuGetBrowser browser)
    {
        if (!browser.HasDownloadsToStart)
        {
            return;
        }

        var closed = browser.UntilClosed;
        foreach (var icon in browser.TakeIconsToFetch())
        {
            workflows.Start(nameof(NuGetBrowser.DownloadIconAsync), ct => browser.DownloadIconAsync(icon, ct), closed);
        }

        foreach (var details in browser.TakeDetailsToFetch())
        {
            workflows.Start(nameof(NuGetBrowser.ReadDetailsAsync), ct => browser.ReadDetailsAsync(details, ct), closed);
        }
    }

    /// <summary>Reads the open assemblies again, as F5 does, when the options that apply as they are read changed.</summary>
    private static void FollowLoadOptions(UiState uiState, AppState appState)
    {
        if (!AssemblyListing.TryChangeLoadOptions(appState.Assemblies, appState.DecompilerOptions))
        {
            return;
        }

        StartRefreshing(appState, Trees.ListExpandedPaths(uiState.Tree));
    }

    /// <summary>Starts reading the open assemblies again now, before the frame draws, so that it draws them
    /// reloading. A method of its own, so that the frames that start nothing allocate no closure.</summary>
    private static void StartRefreshing(AppState appState, List<NodePath> expanded)
    {
        Commands.StartShowing(appState, nameof(AssemblyReloading.RefreshAssembliesAsync), ct => appState.AssemblyReloading.RefreshAssembliesAsync(appState, expanded, ct));
    }

    /// <summary>Opens the error dialog on the newest error whenever one is reported.</summary>
    private static void ShowNewErrors(UiState uiState, ErrorLog errors)
    {
        ErrorDialogView.ShowNewErrors(uiState.ErrorDialog, errors.Revision, errors.Entries);
    }

    private static void FollowDecompilation(UiState uiState, AppState appState)
    {
        var language = appState.Language;
        if (!ShownChanges.TryTakeDecompilation(uiState, language, appState.DecompilerOptions))
        {
            return;
        }

        foreach (var tab in ListFrontTabs(uiState))
        {
            Commands.StartDecompiling(appState, StartDecompilingAgainIfOutdated(appState.ViewCommands, tab, language, appState.DecompilerOptions));
        }
    }

    /// <summary>Starts the work the screen asked for while it drew: the children of the tree's nodes it showed
    /// loading, the searches of the queries typed, and the tooltips of the references hovered in code.</summary>
    internal static void StartAskedWork(UiState uiState, AppState appState)
    {
        Commands.StartBuildingChildren(appState, uiState.Tree);
        Commands.StartSearching(appState, uiState.Search);
        Commands.StartSearching(appState, uiState.Omnibar.Search);
        Commands.StartLookingUpTooltips(appState.Workflows, appState.LookUpTooltipHandler, appState.Tooltips);
    }

    /// <summary>Keeps what the screen changed while it drew: the tabs keep where their views stand, and the
    /// settings keep what changed of the screen.</summary>
    internal static void KeepScreenChanges(UiState uiState, AppState appState)
    {
        KeepTabPositions(uiState, appState.Navigation);
        KeepChangedViewSettings(uiState, appState);
    }

    /// <summary>Keeps where each tab's view stands with what it drew: on the tab while it still shows that, else on
    /// the history entry left from it, as when the tab moved on while the frame was drawn. Allocates nothing while
    /// each tab shows what its view drew.</summary>
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
        var view = DocumentPanels.FindView(uiState.Documents, tab);
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
            Tabs.KeepPosition(tab, DocumentPanels.GetPosition(view, tab));
            return;
        }

        navigation.KeepPositionLeft(shown, DocumentPanels.GetShownPosition(view));
    }

    private static void KeepChangedViewSettings(UiState uiState, AppState appState)
    {
        var view = TakeChangedViewSettings(uiState);
        if (view is null)
        {
            return;
        }

        KeepViewSettings(appState, view);
    }

    /// <summary>Does what comes after drawing a frame: keeps what the screen changed, runs what it asked of
    /// the app state, asks for another frame when there is more to show, saves the changed settings, has
    /// the window draw at the chosen scale, code in the chosen font and icons in their fonts from the next
    /// frame, and opens the web pages and files asked for.</summary>
    internal static void FinishFrame(Ui<UiState> ui, AppState appState, CodeFontFaces codeFonts)
    {
        KeepScreenChanges(ui.State, appState);
        StartAskedWork(ui.State, appState);
        RunAppCommandsAndRedraw(ui, appState);
        RequestUpdateIfViewCommandsPending(ui, appState);
        SaveChangedSettings(ui, appState);
        DrawAtChosenScale(ui);
        DrawCodeInChosenFont(ui, codeFonts);
        FollowIconTheme(ui);
        OpenAskedWebPages(ui);
        OpenAskedFiles(ui);
        OpenAskedFolders(ui);
        BringToFrontIfAsked(ui);
        QuitIfAsked(ui);
    }

    private static void BringToFrontIfAsked(Ui<UiState> ui)
    {
        if (!ui.State.FrontAsked)
        {
            return;
        }

        ui.State.FrontAsked = false;
        ui.BringWindowToFront();
    }

    private static void QuitIfAsked(Ui<UiState> ui)
    {
        if (ui.State.QuitAsked)
        {
            ui.Quit();
        }
    }

    private static void OpenAskedWebPages(Ui<UiState> ui)
    {
        var pages = ui.State.WebPagesToOpen;
        while (pages.TryDequeue(out var url))
        {
            ui.OpenUrl(url);
        }
    }

    private static void OpenAskedFolders(Ui<UiState> ui)
    {
        var folders = ui.State.FoldersToOpen;
        while (folders.TryDequeue(out var path))
        {
            ui.OpenFolder(path);
        }
    }

    private static void OpenAskedFiles(Ui<UiState> ui)
    {
        var files = ui.State.FilesToOpen;
        while (files.TryDequeue(out var path))
        {
            ui.OpenFile(path);
        }
    }

    /// <summary>When the icons changed, lets go of the fonts they no longer draw in, registers those they
    /// are drawn in, and lets go of the pictures of the icons they replace.</summary>
    private static void FollowIconTheme(Ui<UiState> ui)
    {
        var before = ui.State.ShownIcons?.Fonts ?? [];
        if (!ShownChanges.TryTakeIcons(ui.State, ui.State.Icons, ui.State.IconFonts))
        {
            return;
        }

        foreach (var family in ShownChanges.ListDroppedFamilies(before, ui.State.IconFonts))
        {
            ui.UnregisterFont(family);
        }

        foreach (var font in ui.State.IconFonts)
        {
            ui.RegisterFont(font);
        }

        ui.ReleaseUndrawnIconImages();
    }

    private static void DrawAtChosenScale(Ui<UiState> ui)
    {
        ui.Scale = ui.State.Display.UiScale;
    }

    private static void DrawCodeInChosenFont(Ui<UiState> ui, CodeFontFaces codeFonts)
    {
        var face = TakeCodeFontFace(ui.State, codeFonts);
        if (face == null)
        {
            return;
        }

        ui.RegisterFont(face);
    }

    /// <summary>The face to draw code with when the chosen font changed since code was last drawn, else
    /// null. A chosen font whose file cannot be read is given up for the bundled one.</summary>
    internal static FontFace? TakeCodeFontFace(UiState uiState, CodeFontFaces codeFonts)
    {
        var chosen = uiState.Display.Font;
        if (!ShownChanges.TryTakeCodeFont(uiState, chosen))
        {
            return null;
        }

        var face = EnsureFace(codeFonts, chosen);
        if (face != null)
        {
            return face;
        }

        SetCodeFont(uiState, null);
        ShownChanges.TryTakeCodeFont(uiState, null);
        return codeFonts.Bundled;
    }

    /// <summary>The face of <paramref name="font"/>, the bundled one when it is null; null when its file
    /// cannot be read.</summary>
    private static FontFace? EnsureFace(CodeFontFaces faces, CodeFont? font)
    {
        if (font is null)
        {
            return faces.Bundled;
        }

        if (faces.ReadFaces.TryGetValue(font, out var face))
        {
            return face;
        }

        var data = ReadFontFile(font.Path);
        if (data is null)
        {
            return null;
        }

        face = new FontFace(MonoFonts.Code, data, FaceIndex: font.FaceIndex);
        faces.ReadFaces[font] = face;
        return face;
    }

    private static byte[]? ReadFontFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Runs what the screen asked of the app state while it drew, and draws again to show it.</summary>
    private static void RunAppCommandsAndRedraw(Ui<UiState> ui, AppState appState)
    {
        if (ui.State.AppCommands.Count == 0)
        {
            return;
        }

        QueuedCommands.RunAppCommands(ui.State, appState);
        ui.RequestUpdate();
    }

    private static void RequestUpdateIfViewCommandsPending(Ui<UiState> ui, AppState appState)
    {
        if (appState.ViewCommands.Count == 0)
        {
            return;
        }

        ui.RequestUpdate();
    }

    /// <summary>Looks for a newer version as the app starts, unless the user turned that off.</summary>
    internal static void CheckForUpdatesOnStartup(AppState appState)
    {
        if (!appState.IsUpdateCheckAutomatic)
        {
            return;
        }

        var updates = appState.Updates;
        var check = updates.StartCheck(asked: false);
        if (check is null)
        {
            return;
        }

        appState.Workflows.Start(nameof(AppUpdates.CheckAsync), ct => updates.CheckAsync(check, ct));
    }

    /// <summary>Reads the user's themes and icon themes again whenever their folders change, from the app's start
    /// until it shuts down.</summary>
    internal static void WatchUserThemesOnStartup(AppState appState)
    {
        appState.Workflows.StartUntilShutdown(appState.UserThemes.WatchAsync);
        appState.Workflows.StartUntilShutdown(appState.UserIconThemes.WatchAsync);
    }

    /// <summary>Opens the active list, then <paramref name="files"/> while it opens, once the app is running. Returns
    /// the loads that started in the tabs: those reopened from the list's saved documents, and the one showing what
    /// the files ask for.</summary>
    internal static async Task<TabLoads> OpenStartupAssembliesAsync(AppState appState, FilesToOpen files, CancellationToken ct)
    {
        var restoring = RestoreActiveAssemblyListAsync(appState, ct);
        var opening = OpenRequestedFilesAsync(appState, files, ct);
        await Task.WhenAll(restoring, opening);
        return (await restoring).With(await opening);
    }

    private static void SaveChangedSettings(Ui<UiState> ui, AppState appState)
    {
        switch (appState.SettingsSaving.SaveIfChanged(appState, ui.RealTime))
        {
            case Saving.Unchanged:
            case Saving.Saved:
                break;
            case Saving.Waiting waiting:
                ui.RequestUpdateIn(waiting.RetryIn);
                break;
            case Saving.Failed failed:
                ui.RequestUpdateIn(failed.RetryIn);
                break;
            default:
                throw new UnreachableException();
        }
    }
}
