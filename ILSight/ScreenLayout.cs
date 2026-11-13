using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>The layout of the screen: restoring it, opening the views in it beside the active tab, and
/// putting it back as it starts out.</summary>
public static class ScreenLayout
{
    /// <summary>Has a panel the layout of <paramref name="uiState"/> makes for itself open as a new tab, and
    /// a closed panel's tab close with it. The layout's callbacks need the screen the layout is part of, so
    /// these are assigned after the screen is made.</summary>
    internal static void ConnectLayout(UiState uiState)
    {
        var dock = uiState.MainLayout.Dock;
        dock.NewPanel = () => DocumentPanels.OpenTabForPanel(uiState);
        dock.PanelClosed += panel => DocumentPanels.OnPanelClosed(uiState, panel);
    }

    /// <summary>Docks <paramref name="view"/> if it is not, by the active document where it goes there, and
    /// brings it to the front; the search panel as <see cref="OpenSearch"/> does.</summary>
    public static void OpenPanel(UiState uiState, DockView view)
    {
        if (view == DockView.Search)
        {
            OpenSearch(uiState);
            return;
        }

        Docking.Open(uiState.MainLayout, view, DocumentPanels.FindActivePanel(uiState));
    }

    /// <summary>Docks the search panel under the documents if it is closed, brings it to the front
    /// and puts the caret in its search box.</summary>
    public static void OpenSearch(UiState uiState)
    {
        Docking.Open(uiState.MainLayout, DockView.Search, DocumentPanels.FindActivePanel(uiState));
        uiState.Search.AskForFocus();
    }

    /// <summary>Opens the search panel with its query kept to <paramref name="node"/>'s assembly, or to
    /// the namespace it is.</summary>
    public static void ScopeSearch(UiState uiState, BrowseNode node)
    {
        uiState.Search.ScopeTo(node);
        OpenSearch(uiState);
    }

    /// <summary>Has the active document's omnibar search, its box taking focus; opens the search panel
    /// instead while no document is active or the omnibar is hidden.</summary>
    public static void SearchInOmnibar(UiState uiState)
    {
        var panel = DocumentPanels.FindActivePanel(uiState);
        if (panel is null)
        {
            OpenSearch(uiState);
            return;
        }
        if (!uiState.Display.ShowOmnibar)
        {
            OpenSearch(uiState);
            return;
        }
        uiState.Omnibar.StartSearch(panel);
    }

    /// <summary>Asks the user whether to reset the layout before <see cref="ResetLayout"/> does.</summary>
    public static void AskToResetLayout(UiState uiState)
    {
        uiState.ConfirmingLayoutReset = true;
    }

    /// <summary>Stops asking, resetting the layout when <paramref name="confirmed"/>.</summary>
    public static void FinishLayoutReset(UiState uiState, bool confirmed)
    {
        uiState.ConfirmingLayoutReset = false;
        if (confirmed)
        {
            ResetLayout(uiState);
        }
    }

    /// <summary>Puts the layout back as it starts out: the assemblies left of every open document,
    /// tabbed together in one group. Floating windows and the other views close; the documents stay open.</summary>
    public static void ResetLayout(UiState uiState)
    {
        var documents = Docking.ListDocumentPanels(uiState.MainLayout);
        if (documents.Count == 0)
        {
            documents.Add(OpenActiveTabPanel(uiState));
        }

        Docking.ResetLayout(uiState.MainLayout, documents);
        DocumentPanels.ActivateActivePanel(uiState);
    }

    private static string OpenActiveTabPanel(UiState uiState)
    {
        var panel = uiState.Documents.CreatePanel();
        Commands.Later(uiState, (uiState, panel), static (a, s) => QueuedCommands.OpenTabIn(s.uiState, a, s.panel, focus: true));
        return panel;
    }
}
