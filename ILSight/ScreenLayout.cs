using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using static ILSight.DocumentPanels;

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
        dock.NewPanel = () => OpenTabForPanel(uiState);
        dock.PanelClosed += panel => OnPanelClosed(uiState, panel);
    }

    public static void OpenAnalyzer(UiState uiState)
    {
        Docking.OpenAnalyzer(uiState.MainLayout, FindActivePanel(uiState));
    }

    public static void OpenDecompilerOptions(UiState uiState)
    {
        Docking.OpenDecompilerOptions(uiState.MainLayout, FindActivePanel(uiState));
    }

    public static void OpenDisplayOptions(UiState uiState)
    {
        Docking.OpenDisplayOptions(uiState.MainLayout, FindActivePanel(uiState));
    }

    public static void OpenBookmarks(UiState uiState)
    {
        Docking.OpenBookmarks(uiState.MainLayout, FindActivePanel(uiState));
    }

    public static void OpenExports(UiState uiState)
    {
        Docking.OpenExports(uiState.MainLayout, FindActivePanel(uiState));
    }

    public static void OpenAssembliesPanel(UiState uiState)
    {
        Docking.OpenAssemblies(uiState.MainLayout);
    }

    /// <summary>Docks <paramref name="view"/> if it is not, and brings it to the front.</summary>
    public static void OpenPanel(UiState uiState, DockView view)
    {
        switch (view)
        {
            case DockView.Assemblies:
                OpenAssembliesPanel(uiState);
                break;
            case DockView.Search:
                OpenSearch(uiState);
                break;
            case DockView.Bookmarks:
                OpenBookmarks(uiState);
                break;
            case DockView.Analyzer:
                OpenAnalyzer(uiState);
                break;
            case DockView.DecompilerOptions:
                OpenDecompilerOptions(uiState);
                break;
            case DockView.DisplayOptions:
                OpenDisplayOptions(uiState);
                break;
            case DockView.Exports:
                OpenExports(uiState);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Docks the search panel under the documents if it is closed, brings it to the front
    /// and puts the caret in its search box.</summary>
    public static void OpenSearch(UiState uiState)
    {
        Docking.OpenSearch(uiState.MainLayout, FindActivePanel(uiState));
        uiState.Search.IsFocusPending = true;
    }

    /// <summary>Opens the search panel with its query kept to <paramref name="node"/>'s assembly, or to
    /// the namespace it is.</summary>
    public static void ScopeSearch(UiState uiState, BrowseNode node)
    {
        Searches.ScopeTo(uiState.Search, node);
        OpenSearch(uiState);
    }

    /// <summary>Has the active document's omnibar search, its box taking focus; opens the search panel
    /// instead while no document is active or the omnibar is hidden.</summary>
    public static void SearchInOmnibar(UiState uiState)
    {
        var panel = FindActivePanel(uiState);
        if (panel == null)
        {
            OpenSearch(uiState);
            return;
        }
        if (!uiState.Display.ShowOmnibar)
        {
            OpenSearch(uiState);
            return;
        }
        OmnibarView.StartSearch(uiState.Omnibar, panel);
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
            ResetLayout(uiState);
    }

    /// <summary>Puts the layout back as it starts out: the assemblies left of every open document,
    /// tabbed together in one group. Floating windows and the other views close; the documents stay open.</summary>
    public static void ResetLayout(UiState uiState)
    {
        var documents = Docking.ListDocumentPanels(uiState.MainLayout);
        if (documents.Count == 0)
            documents.Add(OpenActiveTabPanel(uiState));
        Docking.ResetLayout(uiState.MainLayout, documents);
        ActivateActivePanel(uiState);
    }

    /// <summary>A new document panel, with an empty tab asked for to be opened in it as the active tab.</summary>
    private static string OpenActiveTabPanel(UiState uiState)
    {
        var panel = DocumentPanels.CreatePanel(uiState.Documents);
        Commands.Later(uiState, (uiState, panel), static (a, s) => AppFrames.OpenTabIn(s.uiState, a, s.panel, focus: true));
        return panel;
    }
}
