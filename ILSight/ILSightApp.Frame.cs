using ILSight.Core;
using static ILSight.Assemblies;
using static ILSight.Decompilation;
using static ILSight.Settings;
using static ILSight.Appearance;
using static ILSight.DocumentPanels;
using static ILSight.Navigation;
using static ILSight.IconTheming;
using static ILSight.ScreenSettings;
using static ILSight.Theming;
using static ILSight.Trees;

namespace ILSight;

public sealed partial class ILSightApp
{
    /// <summary>The screen over <paramref name="appState"/>, restored from what its settings kept, with the
    /// active tab it starts with, the tabs reopened from the open list's documents docked where they were,
    /// and any other tab in the place of the documents. It takes the theme and strings with its first frame.</summary>
    internal static UiState CreateUiState(AppState appState)
    {
        var uiState = CreateScreen(appState.View, problem => Errors.Report(appState, problem), appState.Session, appState.Assemblies.Runtime);
        DocumentPanels.SkipPanels(uiState.Documents, AssemblyLists.ListDocumentPanels(appState.Lists));
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
            return;
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
            return;
        foreach (var tab in ListFrontTabs(uiState))
            DecompileAgainIfOutdated(appState, tab);
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
            KeepTabPosition(uiState, tabs.Open[i]);
    }

    private static void KeepTabPosition(UiState uiState, DocumentTab tab)
    {
        var position = DocumentPanels.FindViewPosition(uiState.Documents, tab);
        if (position == null)
            return;
        Tabs.KeepPosition(tab, position.Value);
    }

    private static void KeepChangedViewSettings(UiState uiState, AppState appState)
    {
        var view = TakeChangedViewSettings(uiState);
        if (view == null)
            return;
        KeepViewSettings(appState, view);
    }
}
