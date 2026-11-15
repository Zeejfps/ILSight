using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>The views as the user opens them in the layout: docked where each goes beside the active document and
/// brought to the front, the search panel's box then taking the focus, so what is typed next goes into it; and the
/// layout put back as it starts out around the open documents. It holds the parts this rule spans; the screen hands
/// the layout and the documents to what reads them (the dock space, which also docks on a drag, and the settings).</summary>
public sealed class Panels(MainLayout layout, DocumentViews documents, TabFocus tabFocus, Search search)
{
    /// <summary>Docks <paramref name="view"/> if it is not, by the active document where it goes there, and
    /// brings it to the front; the search panel as <see cref="OpenSearch"/> does.</summary>
    public void Open(DockView view)
    {
        if (view == DockView.Search)
        {
            OpenSearch();
            return;
        }

        Docking.Open(layout, view, ActivePanel);
    }

    /// <summary>Docks the search panel under the documents if it is closed, brings it to the front
    /// and puts the caret in its search box.</summary>
    public void OpenSearch()
    {
        Docking.Open(layout, DockView.Search, ActivePanel);
        search.AskForFocus();
    }

    /// <summary>The panel of the active tab, which the views dock beside, or null.</summary>
    public string? ActivePanel => DocumentPanels.FindActivePanel(documents, tabFocus);

    /// <summary>Puts the layout back as it starts out: the assemblies left of every open document,
    /// tabbed together in one group, an empty tab asked for when there is none. Floating windows and the other views
    /// close; the documents stay open.</summary>
    public void Reset(IAppCommandWriter commands)
    {
        var shown = Docking.ListDocumentPanels(layout);
        if (shown.Count == 0)
        {
            shown.Add(OpenActiveTabPanel(commands));
        }

        Docking.ResetLayout(layout, shown);
        DocumentPanels.ActivateActivePanel(documents, tabFocus, layout);
    }

    private string OpenActiveTabPanel(IAppCommandWriter commands)
    {
        var panel = documents.CreatePanel();
        Commands.Later(commands, (layout, documents, panel),
            static (a, s) => QueuedCommands.OpenTabIn(s.layout, s.documents, a, s.panel, focus: true));
        return panel;
    }
}
