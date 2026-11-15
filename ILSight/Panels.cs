using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;
using ILSight.MainArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>The views as the user opens them in the layout: docked where each goes beside the active document and
/// brought to the front, the search panel's box then taking the focus, so what is typed next goes into it; and the
/// layout put back as it starts out around the open documents. It holds the layout privately, as the parts this rule
/// spans: it hands it to the framework's dock space itself (<see cref="DrawDockSpace{TState}"/>, which also docks,
/// floats and closes on the user's drag), the settings a copy of the arrangement, and what else reads it the answers
/// it needs; the tabs' panels are docked, brought to the front and closed through it. What the dock space writes the
/// rule follows: a panel the layout makes for itself opens a tab, and a closed panel's tab closes with it, the search
/// cleared with its panel.</summary>
public sealed class Panels
{
    private readonly MainLayout _layout;
    private readonly DocumentViews _documents;
    private readonly TabFocus _tabFocus;
    private readonly Search _search;
    private readonly IAppCommandWriter _commands;

    public Panels(MainLayout layout, DocumentViews documents, TabFocus tabFocus, Search search, IAppCommandWriter commands)
    {
        _layout = layout;
        _documents = documents;
        _tabFocus = tabFocus;
        _search = search;
        _commands = commands;
        var dock = layout.Dock;
        dock.NewPanel = () => DocumentPanels.OpenTabForPanel(layout, documents, commands);
        dock.PanelClosed += panel => DocumentPanels.OnPanelClosed(layout, documents, search, commands, panel);
    }

    /// <summary>Docks <paramref name="view"/> if it is not, by the active document where it goes there, and
    /// brings it to the front; the search panel as <see cref="OpenSearch"/> does.</summary>
    public void Open(DockView view)
    {
        if (view == DockView.Search)
        {
            OpenSearch();
            return;
        }

        Docking.Open(_layout, view, ActivePanel);
    }

    /// <summary>Docks the search panel under the documents if it is closed, brings it to the front
    /// and puts the caret in its search box.</summary>
    public void OpenSearch()
    {
        Docking.Open(_layout, DockView.Search, ActivePanel);
        _search.AskForFocus();
    }

    /// <summary>The panel of the active tab, which the views dock beside, or null.</summary>
    public string? ActivePanel => DocumentPanels.FindActivePanel(_documents, _tabFocus);

    /// <summary>Whether <paramref name="panel"/> is in the layout, docked or floating.</summary>
    public bool IsOpen(string panel)
    {
        return _layout.Dock.Contains(panel);
    }

    /// <summary>Whether any panel is in the layout.</summary>
    public bool HasPanels => _layout.Dock.HasPanels();

    /// <summary>How many times the layout changed, for the settings to tell when to take its arrangement again.</summary>
    public int LayoutChanges => _layout.Dock.Changes;

    /// <summary>A copy of how the panels are arranged, for the settings to keep.</summary>
    public DockLayout CopyArrangement()
    {
        return _layout.Dock.CopyArrangement();
    }

    /// <summary>The documents' panels in the layout, docked or floating.</summary>
    public List<string> ListDocumentPanels()
    {
        return Docking.ListDocumentPanels(_layout);
    }

    /// <summary>Draws the framework's dock space over the layout, which docks, floats and closes panels itself on the
    /// user's drag or a close button; the caller draws each panel it hands out.</summary>
    public DockSpace DrawDockSpace<TState>(Ui ui, string id, TState state, Func<TState, string, DockTab> tab, string tabListTooltip)
    {
        return ui.DockSpace(id, _layout.Dock, state, tab, tabListTooltip: tabListTooltip);
    }

    /// <summary>Puts the layout back as it starts out: the assemblies left of every open document,
    /// tabbed together in one group, an empty tab asked for when there is none. Floating windows and the other views
    /// close; the documents stay open.</summary>
    public void Reset()
    {
        var shown = Docking.ListDocumentPanels(_layout);
        if (shown.Count == 0)
        {
            shown.Add(OpenActiveTabPanel());
        }

        Docking.ResetLayout(_layout, shown);
        DocumentPanels.ActivateActivePanel(_documents, _tabFocus, _layout);
    }

    private string OpenActiveTabPanel()
    {
        var panel = _documents.CreatePanel();
        Commands.Later(_commands, (layout: _layout, documents: _documents, panel),
            static (a, s) => QueuedCommands.OpenTabIn(s.layout, s.documents, a, s.panel, focus: true));
        return panel;
    }

    public void ShowInRestoredDocuments(Navigation navigation)
    {
        DocumentPanels.ShowInRestoredDocuments(_layout, _documents, navigation);
    }

    public void ShowTab(DocumentTab tab, DocumentTab? beside)
    {
        DocumentPanels.ShowTab(_layout, _documents, tab, beside);
    }

    public void ShowReopenedTabs(IReadOnlyList<ReopenedTab> tabs, DockLayout? saved)
    {
        DocumentPanels.ShowReopenedTabs(_layout, _documents, _tabFocus, tabs, saved);
    }

    public void SyncTabViews(Navigation navigation)
    {
        DocumentPanels.SyncTabViews(_layout, _documents, navigation);
    }

    public void FocusNextTabIfNone(Navigation navigation, DecompilerSetup setup)
    {
        DocumentPanels.FocusNextTabIfNone(_layout, _documents, _tabFocus, _commands, navigation, setup);
    }

    public void SyncActiveTab(Navigation navigation, DecompilerSetup setup)
    {
        DocumentPanels.SyncActiveTab(_layout, _documents, _tabFocus, _commands, navigation, setup);
    }

    public void CycleTab(Navigation navigation, DecompilerSetup setup, int by)
    {
        DocumentPanels.CycleTab(_layout, _documents, _tabFocus, _commands, navigation, setup, by);
    }

    /// <summary>The tab in front of each group that has one.</summary>
    public List<DocumentTab> ListFrontTabs()
    {
        return DocumentPanels.ListFrontTabs(_layout, _documents);
    }

    public void ShowInLanguage(DecompilerChoice decompiler, DecompilationLanguage language)
    {
        DocumentPanels.ShowInLanguage(_layout, _documents, _commands, decompiler, language);
    }

    public List<DocumentTab> ListTabsInGroup(DocumentTab tab)
    {
        return DocumentPanels.ListTabsInGroup(_layout, _documents, tab);
    }

    public void CloseActiveTab()
    {
        DocumentPanels.CloseActiveTab(_layout, _documents, _tabFocus);
    }

    public void CloseTab(DocumentTab tab)
    {
        DocumentPanels.CloseTab(_layout, _documents, tab);
    }

    public void CloseOtherTabs(DocumentTab tab)
    {
        DocumentPanels.CloseOtherTabs(_layout, _documents, tab);
    }

    public void CloseTabsInGroup(DocumentTab tab)
    {
        DocumentPanels.CloseTabsInGroup(_layout, _documents, tab);
    }

    public void CloseTabsLeftOf(DocumentTab tab)
    {
        DocumentPanels.CloseTabsLeftOf(_layout, _documents, tab);
    }

    public void CloseTabsRightOf(DocumentTab tab)
    {
        DocumentPanels.CloseTabsRightOf(_layout, _documents, tab);
    }

    public void CloseAllTabs()
    {
        DocumentPanels.CloseAllTabs(_layout, _documents);
    }
}
