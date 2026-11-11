using System.Diagnostics;
using ZGF.Gui.Immediate;
using ILSight.Core;
using static ILSight.Assemblies;
using static ILSight.AssemblyLists;
using static ILSight.CodeDisplay;
using static ILSight.Decompilation;
using static ILSight.Settings;
using static ILSight.Appearance;
using static ILSight.DocumentPanels;
using static ILSight.Navigation;
using static ILSight.RunArgumentsActions;
using static ILSight.IconTheming;
using static ILSight.ScreenSettings;
using static ILSight.Theming;
using static ILSight.Trees;

namespace ILSight;

/// <summary>What the app does around each frame, keeping the app state and the screen in step.</summary>
internal static partial class AppFrames
{
    /// <summary>The screen over <paramref name="appState"/>, restored from what its settings kept, with the
    /// active tab it starts with, the tabs reopened from the open list's documents docked where they were,
    /// and any other tab in the place of the documents. It takes the theme and strings with its first frame.</summary>
    internal static UiState CreateUiState(AppState appState)
    {
        var uiState = CreateScreen(appState.View, appState.Work, problem => Errors.Report(appState, problem), appState.Session, appState.Assemblies.Runtime);
        DocumentPanels.SkipPanels(uiState.Documents, appState.Lists.ListDocumentPanels());
        FollowActiveTab(uiState, appState.Tabs.Active);
        RunViewCommands(uiState, appState);
        ShowInRestoredDocuments(uiState, appState.Tabs);
        return uiState;
    }

    /// <summary>Does what comes before drawing a frame: the screen takes the theme, icons and strings the
    /// preferences pick, follows the open assemblies and analyses in the trees, what the open tabs show and
    /// the active tab; then the commands the features queued for the view run, the screen follows the tabs
    /// opened and closed in the layout and focuses another tab when none is active; then the open
    /// assemblies are read again when the options that apply as they are read changed, and the tabs in
    /// front decompile again when the language or the options changed.</summary>
    internal static void StartFrame(UiState uiState, AppState appState)
    {
        ApplyThemeAndLanguage(uiState, ResolveTheme(appState), appState.Locale);
        ApplyIconTheme(uiState, ResolveIconTheme(appState));
        SyncTrees(uiState, appState.Assemblies, appState.Analyzer, appState.ApiVisibility, appState.Reopens.Files);
        UsePreviewTabs(appState, uiState.Display.PreviewTabs);
        FollowTabContent(uiState);
        FollowActiveTab(uiState, appState.Tabs.Active);
        RunViewCommands(uiState, appState);
        SyncTabViews(uiState, appState.Tabs);
        FocusNextTabIfNone(uiState, appState.Tabs);
        FollowLoadOptions(uiState, appState);
        FollowDecompilation(uiState, appState);
        ShowNewErrors(uiState, appState.Errors);
    }

    /// <summary>Reads the open assemblies again, as F5 does, when the options that apply as they are read changed.</summary>
    private static void FollowLoadOptions(UiState uiState, AppState appState)
    {
        if (!AssemblyListing.UseLoadOptions(appState.Assemblies, appState.DecompilerOptions))
        {
            return;
        }

        RefreshAssemblies(appState, Trees.ListExpandedPaths(uiState.Tree));
    }

    /// <summary>Opens the error dialog on the newest error whenever one is reported.</summary>
    private static void ShowNewErrors(UiState uiState, ErrorLog errors)
    {
        ErrorDialogView.ShowNewErrors(uiState.ErrorDialog, errors.Revision, errors.Entries);
    }

    private static void FollowDecompilation(UiState uiState, AppState appState)
    {
        var language = appState.Language;
        if (!ShownChanges.TakeDecompilation(uiState, language, appState.DecompilerOptions))
        {
            return;
        }

        foreach (var tab in ListFrontTabs(uiState))
        {
            DecompileAgainIfOutdated(appState, tab);
        }
    }

    /// <summary>Keeps what the screen changed while it drew: the tabs keep where their views stand, and the
    /// settings keep what changed of the screen.</summary>
    internal static void KeepScreenChanges(UiState uiState, AppState appState)
    {
        KeepTabPositions(uiState, appState.Tabs);
        KeepChangedViewSettings(uiState, appState);
    }

    /// <summary>Allocates nothing.</summary>
    private static void KeepTabPositions(UiState uiState, DocumentTabs tabs)
    {
        for (var i = 0; i < tabs.Open.Count; i++)
        {
            KeepTabPosition(uiState, tabs.Open[i]);
        }
    }

    private static void KeepTabPosition(UiState uiState, DocumentTab tab)
    {
        var position = DocumentPanels.FindViewPosition(uiState.Documents, tab);
        if (position is null)
        {
            return;
        }

        Tabs.KeepPosition(tab, position.Value);
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
        if (!ShownChanges.TakeIcons(ui.State, ui.State.Icons, ui.State.IconFonts))
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
        if (!ShownChanges.TakeCodeFont(uiState, chosen))
        {
            return null;
        }

        var face = CodeDisplay.EnsureFace(codeFonts, chosen);
        if (face != null)
        {
            return face;
        }

        SetCodeFont(uiState, null);
        ShownChanges.TakeCodeFont(uiState, null);
        return codeFonts.Bundled;
    }

    /// <summary>Runs what the screen asked of the app state while it drew, and draws again to show it.</summary>
    private static void RunAppCommandsAndRedraw(Ui<UiState> ui, AppState appState)
    {
        if (ui.State.AppCommands.Count == 0)
        {
            return;
        }

        RunAppCommands(ui.State, appState);
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

    /// <summary>Opens the active list, then <paramref name="files"/>, once the app is running.</summary>
    internal static Task OpenStartupAssembliesAsync(AppState appState, FilesToOpen files, CancellationToken ct)
    {
        RestoreActiveAssemblyList(appState);
        return OpenRequestedFilesAsync(appState, files, ct);
    }

    private static void SaveChangedSettings(Ui<UiState> ui, AppState appState)
    {
        switch (SaveSettingsIfChanged(appState, ui.RealTime))
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
