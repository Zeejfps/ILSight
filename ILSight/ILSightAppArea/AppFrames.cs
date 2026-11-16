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


    /// <summary>Saves the settings when they changed; returns what it came to.</summary>
    internal static Saving SaveChangedSettings(IFrameHost frame, AppState appState)
    {
        return appState.SettingsSaving.SaveIfChanged(appState, frame.Now);
    }
}
