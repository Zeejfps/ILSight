using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.AssemblyArea;
using ILSight.BookmarkArea;
using ILSight.ExportArea;
using ILSight.DiagramArea;
using ILSight.UserArea;
using static ILSight.Decompilation;

namespace ILSight;

/// <summary>Moving through the code: the open tabs, the history of what they showed, the newest thing the user
/// asked for and whether tabs preview, which change together and which it holds privately. Showing what the user asks
/// for in a tab (a node, a table row, a diagram, a comparison or an export's report) is a new intent (what was still
/// being looked up for an older one is dropped) and a place in the history, the place the active tab leaves keeping
/// where its view stood; restoring a tab after a reload or a reopen is neither (<see cref="Restore"/>), and nor is
/// the tab the screen opens for a panel of its own (<see cref="OpenEmptyTab"/>). Every change
/// to the tabs, the history and the intent is one of its methods, and the views that show nodes take it and call them
/// in the frame of their input. It reads the language and the decompiler options as values it is handed.
/// What waits on a lookup is a handler of its own: <see cref="NavigateHandler"/>, <see cref="ShowInMetadataHandler"/>,
/// <see cref="ShowInApiReferenceHandler"/>, <see cref="OpenAndShowIdHandler"/> and
/// <see cref="GoToHistoryEntryHandler"/>.</summary>
public sealed class Navigation
{
    /// <summary>The open document tabs.</summary>
    private readonly DocumentTabs _tabs = new();

    /// <summary>Back and forward over the places, diagrams, comparisons and reports the tabs showed.</summary>
    private readonly NavigationHistory _history = new();

    /// <summary>What the user last asked for, so that an answer to an older ask is dropped.</summary>
    private readonly UserIntent _intent = new();

    private bool _previewTabs;

    /// <summary>The last id given to a diagram or a comparison shown in a tab; ids are never reused.</summary>
    private int _documents;

    /// <summary>Where what the view is to do about a change (bring a tab to the front, reveal a node in the tree)
    /// is queued, for the screen to run before it draws next.</summary>
    public required Queue<ViewCommand> ViewCommands { get; init; }

    /// <summary>Whether what is picked and followed shows in the preview tab, else in the active tab; changed by
    /// <see cref="UsePreviewTabs"/>.</summary>
    public required bool PreviewTabs
    {
        get => _previewTabs;
        init => _previewTabs = value;
    }

    /// <summary>The tab the user last worked in, where the tree and navigation show what they pick.</summary>
    public DocumentTab? ActiveTab => _tabs.Active;

    /// <summary>The tab that shows what is picked until it is kept open; null when no tab is one.</summary>
    public DocumentTab? PreviewTab => _tabs.Preview;

    /// <summary>The tab of the diagram the user last worked in, which types shown in a diagram are added to; null
    /// when no open tab shows a diagram the user worked in.</summary>
    public DocumentTab? DiagramTab => _tabs.Diagram;

    /// <summary>The tabs open, in order.</summary>
    public IReadOnlyList<DocumentTab> OpenTabs => _tabs.Open;

    /// <summary>Counts up whenever the tabs change, as <see cref="DocumentTabs.Changes"/> counts. Allocates
    /// nothing.</summary>
    public int TabChanges => _tabs.Changes;

    /// <summary>The places, diagrams, comparisons and reports the tabs showed, oldest first.</summary>
    public IReadOnlyList<HistoryEntry> HistoryEntries => _history.Entries;

    /// <summary>Where in <see cref="HistoryEntries"/> the tabs are now.</summary>
    public int HistoryIndex => _history.CurrentIndex;

    /// <summary>Counts up whenever the history changes.</summary>
    public int HistoryRevision => _history.Revision;

    public bool CanGoBack => _history.CanGoBack;

    public bool CanGoForward => _history.CanGoForward;

    /// <summary>True while the newest request is finding a place in the background.</summary>
    public bool IsResolving => _intent.IsResolving;

    /// <summary>True while the newest request is opening the files it named.</summary>
    public bool IsOpening => _intent.IsOpening;

    public bool IsOpen(DocumentTab tab)
    {
        return _tabs.IsOpen(tab);
    }

    /// <summary>Whether <paramref name="ticket"/> is the newest request's.</summary>
    public bool IsCurrent(int ticket)
    {
        return _intent.IsCurrent(ticket);
    }

    /// <summary>Whether a request has begun since the one <paramref name="ticket"/> is.</summary>
    public bool IsStale(int ticket)
    {
        return _intent.IsStale(ticket);
    }

    /// <summary><paramref name="position"/>, taken while the node was shown in <paramref name="shownIn"/>, when it
    /// still holds once the node is shown in <paramref name="language"/>: a grid's in any language, a code view's
    /// only in the same; else null.</summary>
    public static ViewPosition? KeepPosition(ViewPosition? position, DecompilationLanguage shownIn, DecompilationLanguage language)
    {
        if (position is null)
        {
            return null;
        }

        if (position.Value.Grid is not null)
        {
            return position;
        }

        if (shownIn != language)
        {
            return null;
        }

        return position;
    }

    /// <summary>Begins a request whose answer comes later: what was still being looked up for an older one is
    /// dropped. Returns its ticket, which <see cref="IsStale"/> tells apart once another request begins.</summary>
    public int BeginRequest()
    {
        return _intent.Begin();
    }

    /// <summary>Begins a request to open <paramref name="paths"/>, as <see cref="UserIntent.BeginOpening"/>
    /// does: in the frame it is asked in, the app says it is opening them (<see cref="IsOpening"/>).</summary>
    public FileOpening? BeginOpening(IEnumerable<AssemblyPath> paths)
    {
        return _intent.BeginOpening(paths);
    }

    /// <summary>Takes what opening the files of <paramref name="ticket"/> came to, as
    /// <see cref="UserIntent.EndOpening"/> does.</summary>
    public void EndOpening(int ticket)
    {
        _intent.EndOpening(ticket);
    }

    /// <summary>What finding a place for <paramref name="ticket"/> came to, as
    /// <see cref="UserIntent.EndResolving{T}(int, Outcome{Lookup{T}})"/> says.</summary>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<Lookup<T>> outcome) where T : notnull
    {
        return _intent.EndResolving(ticket, outcome);
    }

    /// <summary>Shows <paramref name="node"/> in the preview tab while <see cref="PreviewTabs"/>, or the active
    /// tab while tabs do not preview, or else a new tab kept open, in <paramref name="language"/> with
    /// <paramref name="options"/>; reveals it in the tree and records it in the history. Returns the load it started
    /// in the tab, for <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows the node
    /// already.</summary>
    public DecompileTicket? ShowNode(BrowseNode node, DecompilerSetup setup, bool inNewTab = false)
    {
        _intent.Begin();
        LeaveActiveTab();
        var load = ShowNodeIn(ChooseDestination(inNewTab), node, setup);
        _history.RecordPlace(node, setup.Language);
        return load;
    }

    /// <summary>Shows <paramref name="node"/>, a lookup's answer, in the tab <paramref name="destination"/> leads to
    /// now, at <paramref name="target"/> when that is a bookmark's place, and records it in the history. Returns the
    /// load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? ShowFoundNode(TabDestination destination, BrowseNode node, DecompilerSetup setup,
        Bookmark? target = null)
    {
        LeaveActiveTab();
        var load = ShowNodeIn(destination, node, setup, target: target);
        _history.RecordPlace(node, setup.Language);
        return load;
    }

    /// <summary>Shows <paramref name="node"/>, found again at the place of the history entry gone to, in the tab
    /// <paramref name="destination"/> leads to now, at <paramref name="position"/>; the history stays at that entry.
    /// Returns the load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? ShowHistoryPlace(TabDestination destination, BrowseNode node, DecompilerSetup setup,
        ViewPosition? position)
    {
        return ShowNodeIn(destination, node, setup, position);
    }

    /// <summary>Shows <paramref name="node"/> again in <paramref name="tab"/>, which showed it before a reload or a
    /// reopen, at <paramref name="position"/>: no place in the history and no new intent, as the user asked for
    /// nothing. Returns the load it started in the tab, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? Restore(DocumentTab tab, BrowseNode node, DecompilerSetup setup, ViewPosition? position)
    {
        return StartDecompiling(tab, node, setup, new Arrival { Position = position });
    }

    /// <summary>Shows <paramref name="node"/>, picked in the tree, as <see cref="ShowNode"/> does, unless
    /// a tab shows it already, which is brought to the front instead, its place recorded in the history as where the
    /// user is. Returns the load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? PreviewNode(BrowseNode node, DecompilerSetup setup)
    {
        var showing = FindTabShowing(node);
        if (showing is null)
        {
            return ShowNode(node, setup);
        }
        _tabs.MakeActive(showing);
        ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = showing, Beside = null });
        RecordShownPlace(showing, setup.Language);
        return null;
    }

    /// <summary>The tab showing <paramref name="node"/>, the active one first; null when none does.</summary>
    private DocumentTab? FindTabShowing(BrowseNode node)
    {
        var active = _tabs.Active;
        if (active is not null && active.Node == node)
        {
            return active;
        }

        foreach (var tab in _tabs.Open)
        {
            if (tab.Node == node)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>Shows <paramref name="node"/>, opened from the tree, as <see cref="PreviewNode"/> does and
    /// keeps the tab showing it open; while tabs do not preview, as <see cref="ShowNode"/> does. Returns the load
    /// it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? OpenNode(BrowseNode node, DecompilerSetup setup)
    {
        if (!_previewTabs)
        {
            return ShowNode(node, setup);
        }
        var load = PreviewNode(node, setup);
        KeepActiveTabOpen();
        return load;
    }

    private void KeepActiveTabOpen()
    {
        var active = _tabs.Active;
        if (active is null)
        {
            return;
        }

        KeepTabOpen(active);
    }

    /// <summary>Keeps <paramref name="tab"/> open, so that what is picked next shows in another tab.</summary>
    public void KeepTabOpen(DocumentTab tab)
    {
        _tabs.Keep(tab);
    }

    /// <summary>Has what is picked and followed shown in the preview tab, or, when <paramref name="on"/>
    /// is false, in the active tab, the preview tab then kept open as any other.</summary>
    public void UsePreviewTabs(bool on)
    {
        _previewTabs = on;
        if (on)
        {
            return;
        }

        var preview = _tabs.Preview;
        if (preview is not null)
        {
            _tabs.Keep(preview);
        }
    }

    /// <summary>Makes <paramref name="tab"/>, when it is open, the tab the user works in (clicked, docked, brought
    /// forward, or focused after the active one closed), its place recorded in the history as where the user is, and
    /// points it again at what it shows when it was shown in another language than <paramref name="language"/> or with
    /// other options than <paramref name="options"/>. Returns the load that started in the tab, for
    /// <see cref="DecompileIntoTabHandler"/> to bring in; null when none did.</summary>
    public DecompileTicket? FocusTab(DocumentTab tab, DecompilerSetup setup)
    {
        if (!_tabs.IsOpen(tab))
        {
            return null;
        }

        _intent.Begin();
        _tabs.MakeActive(tab);
        RecordShownPlace(tab, setup.Language);
        return StartDecompilingAgainIfOutdated(tab, setup.Language, setup.Options);
    }

    /// <summary>Records the place <paramref name="tab"/> shows as where the user is now, as showing it records it: its
    /// diagram, comparison or report, else its node, in <paramref name="language"/>; nothing for a tab showing
    /// nothing.</summary>
    private void RecordShownPlace(DocumentTab tab, DecompilationLanguage language)
    {
        var content = tab.Content;
        if (content is TabContent.Diagram diagram)
        {
            _history.RecordDiagram(diagram.Id, diagram.Types, diagram.Title);
            return;
        }

        if (content is TabContent.Comparison comparison)
        {
            _history.RecordComparison(comparison.Id, comparison.Assemblies, comparison.Title);
            return;
        }

        if (content is TabContent.Report report)
        {
            _history.RecordReport(report.Export);
            return;
        }

        var node = tab.Node;
        if (node is null)
        {
            return;
        }

        _history.RecordPlace(node, language);
    }

    /// <summary>The assemblies an id is looked for in: those read from the files opened, a package's
    /// among them, else every open one.</summary>
    internal static List<LoadedModule> ListIdScope(AssemblyList assemblies, List<LoadedAssembly> opened)
    {
        var open = assemblies.Assemblies;
        if (opened.Count == 0)
        {
            return open.ToList();
        }

        return open
            .Where(a => opened.Contains(a.ListedItem))
            .ToList();
    }

    /// <summary>Shows the table row a metadata token leads to where <see cref="ShowNode"/> shows a
    /// node, and records it in the history. Returns the load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? ShowMetadataTarget(MetadataTarget target, bool inNewTab, DecompilerSetup setup)
    {
        _intent.Begin();
        return ShowRowIn(ChooseDestination(inNewTab), target, setup);
    }

    /// <summary>Shows the table row <paramref name="address"/>, a link of the grid <paramref name="grid"/>
    /// shows, leads to among the nodes of <paramref name="session"/>, as <see cref="ShowMetadataTarget"/> does;
    /// nothing when it leads nowhere. Returns the load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? ShowMetadataLink(BrowseSession session, BrowseNode grid, MetadataAddress address, bool inNewTab, DecompilerSetup setup)
    {
        var target = session.FindMetadataRow(grid, address);
        if (target is null)
        {
            return null;
        }

        return ShowMetadataTarget(target.Value, inNewTab, setup);
    }

    /// <summary>Shows the table row <paramref name="target"/> leads to in the tab <paramref name="destination"/>
    /// leads to now, and records it in the history. Returns the load it started, as <see cref="ShowNode"/>
    /// does.</summary>
    public DecompileTicket? ShowRowIn(TabDestination destination, MetadataTarget target, DecompilerSetup setup)
    {
        LeaveActiveTab();
        var load = ShowNodeIn(destination, target.Node, setup, row: target.Row);
        _history.RecordPlace(target.Node, setup.Language);
        return load;
    }

    /// <summary>Begins a request whose place a lookup finds, asked for from the active tab: what was still being
    /// looked up for an older request is dropped, and the app says it is resolving until the lookup's answer is taken
    /// (<see cref="EndResolving{T}(int, Outcome{Lookup{T}})"/>). What it finds shows in a new tab when
    /// <paramref name="inNewTab"/>, else where <see cref="FindTab"/> leads then. Returns the ticket the lookup's answer
    /// is shown by.</summary>
    public ResolveTicket BeginResolving(bool inNewTab)
    {
        var destination = ChooseDestination(inNewTab);
        return new ResolveTicket { Intent = _intent.BeginResolving(), Destination = destination };
    }

    /// <summary>Begins opening the code of a type or member picked on <paramref name="diagram"/> beside it, in a new
    /// tab when <paramref name="inNewTab"/>: begins the request whose lookup finds the code
    /// (<see cref="BeginResolving"/>); going back returns to the diagram, recorded when its tab was brought to the
    /// front. Returns its ticket; null when no open tab shows the diagram.</summary>
    public ResolveTicket? BeginResolvingFromDiagram(TypeDiagram diagram, bool inNewTab)
    {
        var tab = FindTabShowing(diagram);
        if (tab is null || tab.Content is not TabContent.Diagram)
        {
            return null;
        }

        return BeginResolving(inNewTab);
    }

    /// <summary>Brings <paramref name="tab"/>, which shows a diagram, to the front as the tab the user works in: a new
    /// intent, and the diagram a place in the history.</summary>
    public void ShowDiagramTab(DocumentTab tab)
    {
        _intent.Begin();
        LeaveActiveTab();
        _tabs.MakeActive(tab);
        ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = tab, Beside = null });
        RecordShownPlace(tab, tab.Load.Language);
    }

    /// <summary>Shows <paramref name="diagram"/>, titled <paramref name="title"/>, in the active tab while it shows
    /// nothing, else in a new tab, kept open and made the tab the user works in: a new intent, and the diagram a place
    /// in the history. Returns the tab.</summary>
    public DocumentTab OpenDiagram(TypeDiagram diagram, string title)
    {
        _intent.Begin();
        LeaveActiveTab();
        var tab = GetOrOpenEmptyTab();
        var id = ++_documents;
        _tabs.GetDocument(tab).ShowDiagram(diagram, title, id);
        _tabs.Keep(tab);
        _tabs.MakeActive(tab);
        _history.RecordDiagram(id, diagram, title);
        return tab;
    }

    /// <summary>Shows <paramref name="diagram"/>, read back from the saved documents, in <paramref name="tab"/>, which
    /// waited for it: no place in the history and no new intent; taken as the diagram the user works in when the tab
    /// is the active one.</summary>
    public void RestoreDiagram(DocumentTab tab, TypeDiagram diagram, string title)
    {
        _tabs.GetDocument(tab).ShowDiagram(diagram, title, ++_documents);
        if (tab != _tabs.Active)
        {
            return;
        }

        _tabs.MakeActive(tab);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/>, its saved one compared again, in
    /// <paramref name="tab"/>, which waited for it: no place in the history and no new intent.</summary>
    public void RestoreComparison(DocumentTab tab, AssemblyComparison comparison, string title)
    {
        _tabs.GetDocument(tab).ShowComparison(comparison, title, ++_documents);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/>, in the active tab while it shows
    /// nothing, else in a new tab, kept open and made the tab the user works in: a new intent, and the comparison a
    /// place in the history. Returns the tab.</summary>
    public DocumentTab OpenComparison(AssemblyComparison comparison, string title)
    {
        _intent.Begin();
        LeaveActiveTab();
        var tab = GetOrOpenEmptyTab();
        var id = ++_documents;
        _tabs.GetDocument(tab).ShowComparison(comparison, title, id);
        _tabs.Keep(tab);
        _tabs.MakeActive(tab);
        _history.RecordComparison(id, comparison, title);
        return tab;
    }

    /// <summary>Shows <paramref name="report"/> in a new tab, made the tab the user works in: a new intent, and the
    /// report a place in the history.</summary>
    public void ShowReport(ExportReport report)
    {
        _intent.Begin();
        LeaveActiveTab();
        var tab = OpenNewTab();
        _tabs.GetDocument(tab).ShowReport(report);
        _tabs.MakeActive(tab);
        _history.RecordReport(report);
    }

    /// <summary>Opens the tab the app starts with, empty and the tab the user works in.</summary>
    public void OpenFirstTab()
    {
        var first = _tabs.AddNew();
        _tabs.MakeActive(first);
    }

    /// <summary>Opens an empty tab for a panel the screen made for itself.</summary>
    public DocumentTab OpenEmptyTab()
    {
        return _tabs.AddNew();
    }

    /// <summary>Opens a tab for a document reopened from the saved documents, to wait for what it showed: no place
    /// in the history and no new intent.</summary>
    public DocumentTab OpenReopenedTab()
    {
        return _tabs.AddNew();
    }

    /// <summary>Has <paramref name="tab"/>, reopened from the saved documents, wait, titled <paramref name="title"/>,
    /// for the node at <paramref name="location"/>, which it showed in <paramref name="language"/> when they were saved,
    /// to be found once the assemblies are listed: no place in the history and no new intent.</summary>
    public void WaitForNode(DocumentTab tab, NodePath location, string? title, DecompilationLanguage language)
    {
        _tabs.GetDocument(tab).WaitForNode(location, title, language);
    }

    /// <summary>Has <paramref name="tab"/>, reopened from the saved documents, wait, titled <paramref name="title"/>,
    /// for the diagram it showed when they were saved to be read once the assemblies are listed: no place in the
    /// history and no new intent.</summary>
    public void WaitForDiagram(DocumentTab tab, string? title)
    {
        _tabs.GetDocument(tab).WaitForDocument(title);
    }

    /// <summary>Has <paramref name="tab"/>, reopened from the saved documents, wait, titled <paramref name="title"/>,
    /// for the assemblies the comparison it showed when they were saved compared to be listed: no place in the history
    /// and no new intent.</summary>
    public void WaitForComparison(DocumentTab tab, string? title)
    {
        _tabs.GetDocument(tab).WaitForDocument(title);
    }

    /// <summary>Makes <paramref name="tab"/>, reopened from the saved documents, the tab the user works in, as it was
    /// when they were saved.</summary>
    public void RestoreActiveTab(DocumentTab tab)
    {
        _tabs.MakeActive(tab);
    }

    /// <summary>Makes <paramref name="tab"/>, reopened from the saved documents, the preview tab, as it was when they
    /// were saved.</summary>
    public void RestorePreviewTab(DocumentTab tab)
    {
        _tabs.MakePreview(tab);
    }

    /// <summary>Closes <paramref name="tab"/>, cancelling what it was doing, unless it is closed already: a new intent.
    /// The history keeps the diagram or comparison it shows as it is left, and going back to it shows it again.
    /// Returns whether it was the active tab, which leaves none active until the view focuses another.</summary>
    public ClosedTab CloseTab(DocumentTab tab)
    {
        if (!_tabs.IsOpen(tab))
        {
            return new ClosedTab { WasActive = false };
        }

        _intent.Begin();
        var wasActive = tab == _tabs.Active;
        KeepDocumentOf(tab);
        _tabs.Remove(tab);
        return new ClosedTab { WasActive = wasActive };
    }

    /// <summary>Keeps the diagram or comparison <paramref name="tab"/> shows, as it stands as the tab leaves it, on its
    /// history entries.</summary>
    private void KeepDocumentOf(DocumentTab tab)
    {
        var content = tab.Content;
        if (content is TabContent.Diagram diagram)
        {
            _history.KeepDiagram(diagram.Id, diagram.Types);
        }

        if (content is TabContent.Comparison comparison)
        {
            _history.KeepComparison(comparison.Id, comparison.Assemblies);
        }
    }

    /// <summary>Closes every document tab but the active one, and empties that one; with no tab left
    /// open, opens an empty one.</summary>
    public void CloseDocuments()
    {
        var kept = GetTabToKeep();
        var others = _tabs
            .Open.Where(t => t != kept)
            .ToList();
        foreach (var tab in others)
        {
            CloseTab(tab);
        }

        KeepDocumentOf(kept);
        _tabs.GetDocument(kept).Clear();
        _tabs.MakeActive(kept);
    }

    private DocumentTab GetTabToKeep()
    {
        var active = _tabs.Active;
        if (active is not null)
        {
            return active;
        }

        if (_tabs.Open.Count > 0)
        {
            return _tabs.Open[0];
        }

        return OpenNewTab();
    }

    /// <summary>Closes every document tab, leaving none open.</summary>
    public void CloseAllTabs()
    {
        var all = _tabs.Open.ToList();
        foreach (var tab in all)
        {
            CloseTab(tab);
        }
    }

    /// <summary>Forgets every place and diagram the tabs showed, as switching to another list does.</summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    /// <summary>Keeps where the view stood in what a tab showed as <paramref name="shownAs"/>, which it shows no more,
    /// on the history entry left with it; read back from the screen after it drew.</summary>
    public void KeepPositionLeft(TabVersion shownAs, ViewPosition position)
    {
        _history.KeepPosition(shownAs, position);
    }

    /// <summary>Goes to the nearest history entry before the current one that can be shown (<see cref="CanShow"/>),
    /// passing over those between that can't, and shows it as <see cref="GoToEntry"/> does. Nothing when there is
    /// none.</summary>
    public HistoryMove? GoBack(AssemblyList assemblies, IReadOnlyList<ExportReport> finished, DecompilerSetup setup)
    {
        return GoOn(_history.CurrentIndex - 1, assemblies, finished, setup);
    }

    /// <summary>Goes to the nearest history entry after the current one that can be shown, as <see cref="GoBack"/>
    /// does.</summary>
    public HistoryMove? GoForward(AssemblyList assemblies, IReadOnlyList<ExportReport> finished, DecompilerSetup setup)
    {
        return GoOn(_history.CurrentIndex + 1, assemblies, finished, setup);
    }

    private HistoryMove? GoOn(int index, AssemblyList assemblies, IReadOnlyList<ExportReport> finished, DecompilerSetup setup)
    {
        var found = FindEntryToShow(index, assemblies, finished);
        if (found is null)
        {
            return null;
        }

        return ShowEntryAt(found.Value, assemblies, finished, setup);
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, however far back or forward, as one picked by name
    /// (the history menu) is gone to: that entry only. Nothing when there is none to go to, it is the current one, or it
    /// can't be shown (<see cref="CanShow"/>: the menu offers it to no click). The active tab leaves its entry, and a new
    /// intent begins: a diagram's, comparison's or report's entry brings the tab showing it to the
    /// front, else shows it again in a tab of its own, made again from the entry; a node's entry shows its node in the
    /// tab it shows in, at the place the view stood when it is still good in the language of <paramref name="setup"/>,
    /// when that tab still shows it and its assembly is listed, else begins looking up the place the entry recorded among
    /// the listed assemblies, the app saying it is resolving. Returns what is left for the edge to start: the load the tab started, the lookup
    /// (for <see cref="GoToHistoryEntryHandler"/>), the read of a diagram's cards or the run of a comparison; null when
    /// nothing is.</summary>
    public HistoryMove? GoToEntry(int index, AssemblyList assemblies, IReadOnlyList<ExportReport> finished, DecompilerSetup setup)
    {
        if (!CanGoTo(index))
        {
            return null;
        }

        if (!CanShow(_history.Entries[index], assemblies, finished))
        {
            return null;
        }

        return ShowEntryAt(index, assemblies, finished, setup);
    }

    private HistoryMove? ShowEntryAt(int index, AssemblyList assemblies, IReadOnlyList<ExportReport> finished, DecompilerSetup setup)
    {
        LeaveActiveTab();
        var entry = _history.MoveToEntry(index);
        if (entry is null)
        {
            return null;
        }

        return entry.Match(
            node => ShowEntry(node, assemblies, setup),
            diagram => ShowDiagramPlace(diagram),
            comparison => ShowComparisonPlace(comparison, assemblies),
            report =>
            {
                ShowReportPlace(report, finished);
                return null;
            });
    }

    /// <summary>The index of the entry at <paramref name="index"/>, or of the first after it on in the same direction,
    /// that can be shown; null when none can or <paramref name="index"/> is none to go to.</summary>
    private int? FindEntryToShow(int index, AssemblyList assemblies, IReadOnlyList<ExportReport> finished)
    {
        if (!CanGoTo(index))
        {
            return null;
        }

        var step = index < _history.CurrentIndex ? -1 : 1;
        var entries = _history.Entries;
        for (var i = index; i >= 0 && i < entries.Count; i += step)
        {
            if (CanShow(entries[i], assemblies, finished))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>Whether going to <paramref name="entry"/> can show something, decided without a wait from what it names,
    /// the same way for every kind: a node's when a tab is at its place or its place's assembly is listed in
    /// <paramref name="assemblies"/> (a place gone from a listed assembly is the lookup's answer); a diagram or
    /// comparison when a tab shows it or one of its assemblies is listed; a report when a tab shows it or its export is
    /// among <paramref name="finished"/>. Back and Forward pass over an entry that can't; the history menu marks it.</summary>
    public bool CanShow(HistoryEntry entry, AssemblyList assemblies, IReadOnlyList<ExportReport> finished)
    {
        return entry.Match(
            node => FindTabAt(node.Place) is not null || assemblies.IsListed(node.Place.FilePath),
            diagram => FindDiagramTab(diagram.Id) is not null || IsAnyListed(assemblies, diagram.HomeAssemblies) || IsAnyTypeListed(assemblies, diagram.Types),
            comparison => FindComparisonTab(comparison.Id) is not null || assemblies.IsListed(comparison.LeftPath)
                || assemblies.IsListed(comparison.RightPath),
            report => FindReportTab(report.Run) is not null || FindFinished(finished, report.Run) is not null);
    }

    private static bool IsAnyListed(AssemblyList assemblies, IReadOnlyList<AssemblyPath> paths)
    {
        foreach (var path in paths)
        {
            if (assemblies.IsListed(path))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAnyTypeListed(AssemblyList assemblies, IReadOnlyList<TypeAnchor> types)
    {
        foreach (var type in types)
        {
            if (assemblies.IsListed(type.AssemblyPath))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The finished export's report of <paramref name="run"/> among <paramref name="finished"/>; null when the
    /// exports panel no longer has it.</summary>
    private static ExportReport? FindFinished(IReadOnlyList<ExportReport> finished, ExportRun run)
    {
        foreach (var report in finished)
        {
            if (report.Run == run)
            {
                return report;
            }
        }
        return null;
    }

    private bool CanGoTo(int index)
    {
        if (index < 0)
        {
            return false;
        }

        if (index >= _history.Entries.Count)
        {
            return false;
        }

        return index != _history.CurrentIndex;
    }

    /// <summary>Shows the node <paramref name="entry"/> recorded when the tab it shows in still shows it, else begins
    /// looking up the place it recorded. Returns the load or the lookup it started; null when neither did.</summary>
    private HistoryMove? ShowEntry(HistoryEntry.Node entry, AssemblyList assemblies, DecompilerSetup setup)
    {
        var destination = ChooseDestination(inNewTab: false);
        var place = entry.Place;
        var position = KeepPosition(entry.Position, entry.Language, setup.Language);
        var shown = FindShownNode(assemblies, FindTab(destination), place);
        if (shown is null)
        {
            return new HistoryMove.LookUp { Place = place, Position = position, Ticket = BeginResolving(inNewTab: false) };
        }

        _intent.Begin();
        var load = ShowNodeIn(destination, shown, setup, position);
        if (load is null)
        {
            return null;
        }

        return new HistoryMove.Decompile { Load = load };
    }

    /// <summary>The node <paramref name="tab"/> shows when it is at <paramref name="place"/> and its assembly is
    /// listed in <paramref name="assemblies"/>; else null.</summary>
    private static BrowseNode? FindShownNode(AssemblyList assemblies, DocumentTab? tab, NodePath place)
    {
        if (tab is null)
        {
            return null;
        }

        var node = tab.Node;
        if (node is null)
        {
            return null;
        }

        if (!place.Equals(tab.Location))
        {
            return null;
        }

        if (!assemblies.IsListed(node.Root.Assembly))
        {
            return null;
        }

        return node;
    }

    /// <summary>Brings the tab showing the diagram <paramref name="place"/> recorded to the front, else makes the
    /// diagram again from it, as it was left, in the active tab while it shows nothing, else in a new tab, kept open and
    /// made active, the entry then naming it. Returns the read of its cards, for the edge to start; null when a tab
    /// showed it.</summary>
    private HistoryMove? ShowDiagramPlace(HistoryEntry.Diagram place)
    {
        var shown = FindDiagramTab(place.Id);
        if (shown is not null)
        {
            BringToFront(shown);
            return null;
        }

        _intent.Begin();
        var diagram = new TypeDiagram();
        diagram.TakeHomes(place.HomeAssemblies);
        diagram.Keep(place.Types);
        var id = ++_documents;
        var tab = GetOrOpenEmptyTab();
        _tabs.GetDocument(tab).ShowDiagram(diagram, place.Title, id);
        _tabs.Keep(tab);
        _tabs.MakeActive(tab);
        _history.ReplaceCurrent(place with { Id = id });
        diagram.StartReading();
        return new HistoryMove.ReadCards { Read = new DiagramPlaceRead { Diagram = diagram, Types = place.Types, Title = place.Title } };
    }

    /// <summary>Brings the tab showing the comparison <paramref name="place"/> recorded to the front, else makes the
    /// comparison again from it and compares its assemblies again among <paramref name="assemblies"/>, shown as
    /// <see cref="ShowDiagramPlace"/> shows a diagram. Returns the comparison's run, for the edge to start; null when a
    /// tab showed it or it shows why it can't run.</summary>
    private HistoryMove? ShowComparisonPlace(HistoryEntry.Comparison place, AssemblyList assemblies)
    {
        var shown = FindComparisonTab(place.Id);
        if (shown is not null)
        {
            BringToFront(shown);
            return null;
        }

        _intent.Begin();
        var comparison = new AssemblyComparison { LeftPath = place.LeftPath, RightPath = place.RightPath };
        comparison.TakeInScope(place.Scope);
        var id = ++_documents;
        var tab = GetOrOpenEmptyTab();
        _tabs.GetDocument(tab).ShowComparison(comparison, place.Title, id);
        _tabs.Keep(tab);
        _tabs.MakeActive(tab);
        _history.ReplaceCurrent(place with { Id = id });
        var run = Comparisons.StartComparing(assemblies, comparison);
        if (run is null)
        {
            return null;
        }

        return new HistoryMove.Compare { Run = run };
    }

    /// <summary>Brings the tab showing the report <paramref name="place"/> recorded to the front, else shows the
    /// report again, found among <paramref name="finished"/>, in a new tab, made active.</summary>
    private void ShowReportPlace(HistoryEntry.Report place, IReadOnlyList<ExportReport> finished)
    {
        var shown = FindReportTab(place.Run);
        if (shown is not null)
        {
            BringToFront(shown);
            return;
        }

        var report = FindFinished(finished, place.Run);
        if (report is null)
        {
            return;
        }

        _intent.Begin();
        var tab = OpenNewTab();
        _tabs.GetDocument(tab).ShowReport(report);
        _tabs.MakeActive(tab);
    }

    /// <summary>Brings <paramref name="tab"/>, showing the diagram, comparison or report of the history entry gone to,
    /// to the front, dropping what was being navigated to meanwhile.</summary>
    private void BringToFront(DocumentTab tab)
    {
        _intent.Begin();
        _tabs.MakeActive(tab);
        ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = tab, Beside = null });
    }

    /// <summary>The open tab showing <paramref name="diagram"/>, the very one; null when none does.</summary>
    private DocumentTab? FindTabShowing(TypeDiagram diagram)
    {
        foreach (var tab in _tabs.Open)
        {
            if (tab.Diagram == diagram)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>The open tab at <paramref name="place"/>; null when none is.</summary>
    private DocumentTab? FindTabAt(NodePath place)
    {
        foreach (var tab in _tabs.Open)
        {
            if (place.Equals(tab.Location))
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>The open tab showing the diagram given <paramref name="id"/>; null when none does.</summary>
    private DocumentTab? FindDiagramTab(int id)
    {
        foreach (var tab in _tabs.Open)
        {
            if (tab.Content is TabContent.Diagram shown && shown.Id == id)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>The open tab showing the comparison given <paramref name="id"/>; null when none does.</summary>
    private DocumentTab? FindComparisonTab(int id)
    {
        foreach (var tab in _tabs.Open)
        {
            if (tab.Content is TabContent.Comparison shown && shown.Id == id)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>The open tab showing the report of the export <paramref name="run"/>; null when none does.</summary>
    private DocumentTab? FindReportTab(ExportRun run)
    {
        foreach (var tab in _tabs.Open)
        {
            var report = tab.Report;
            if (report is not null && report.Run == run)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>Records where the active tab's view is in the history entry it shows, before it shows
    /// something else, with what of the tab the entry was shown as, which a later read-back of the view keeps
    /// its place by; and the diagram or comparison it shows as it is left.</summary>
    private void LeaveActiveTab()
    {
        var tab = _tabs.Active;
        if (tab is null)
        {
            return;
        }

        KeepDocumentOf(tab);
        var position = tab.Load.Position;
        if (position is null)
        {
            return;
        }

        if (!_history.IsCurrentPlace(tab.Location))
        {
            return;
        }

        _history.LeaveEntry(tab.Load.Language, position.Value, TabVersion.Of(tab));
    }

    /// <summary>Where what is asked for now from the active tab will show, in a new tab when
    /// <paramref name="inNewTab"/>.</summary>
    public TabDestination ChooseDestination(bool inNewTab)
    {
        return new TabDestination { Origin = _tabs.Active, InNewTab = inNewTab };
    }

    /// <summary>Shows <paramref name="node"/> in the tab <paramref name="destination"/> leads to now, in
    /// <paramref name="setup"/>, makes that tab active, and commands the view to
    /// bring it to the front and reveal the node in the tree. Returns the load it started in the tab, for
    /// <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows the node already.</summary>
    private DecompileTicket? ShowNodeIn(
        TabDestination destination,
        BrowseNode node,
        DecompilerSetup setup,
        ViewPosition? position = null,
        Bookmark? target = null,
        int? row = null)
    {
        var shownIn = GetOrOpenTab(destination);
        _tabs.MakeActive(shownIn);
        var load = StartDecompiling(shownIn, node, setup, new Arrival
        {
            Position = position,
            Target = target,
            Row = row,
        });
        ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        return load;
    }

    /// <summary>Points <paramref name="tab"/> at <paramref name="node"/>, shown loading from now on as
    /// <paramref name="setup"/> says, cancelling what was in flight, and returns what
    /// <see cref="DecompileIntoTabHandler"/> needs to bring it in, after which the view stands where
    /// <paramref name="arrival"/> says. When the tab already shows <paramref name="node"/> as the setup would, only
    /// commands the view to stand there, and returns null.</summary>
    private DecompileTicket? StartDecompiling(DocumentTab tab, BrowseNode node, DecompilerSetup setup, Arrival arrival)
    {
        if (tab.Load.IsShowing(node, setup.Language, setup.Options))
        {
            Arrive(ViewCommands, tab, arrival);
            return null;
        }

        var work = _tabs.GetDocument(tab).PointAt(node, setup.Language, setup.Options, arrival);
        return new DecompileTicket { Tab = tab, Load = tab.Load, Node = node, Language = setup.Language, Options = setup.Options, Work = work };
    }

    /// <summary>The tab <paramref name="destination"/> leads to now, brought to the front, else a new one
    /// opened; made the preview tab when it shows what tabs preview.</summary>
    private DocumentTab GetOrOpenTab(TabDestination destination)
    {
        var tab = FindTab(destination);
        if (tab is null)
        {
            tab = OpenNewTab();
        }
        else
        {
            ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = tab, Beside = null });
        }

        if (IsPreviewed(destination))
        {
            _tabs.MakePreview(tab);
        }

        return tab;
    }

    /// <summary>The open tab <paramref name="destination"/> leads to now, the preview tab while
    /// <see cref="PreviewTabs"/>; null when it calls for a new one.</summary>
    private DocumentTab? FindTab(TabDestination destination)
    {
        if (destination.InNewTab)
        {
            return null;
        }

        if (_previewTabs)
        {
            return FindPreviewTab();
        }

        return FindOriginToReplace(destination.Origin);
    }

    /// <summary><paramref name="origin"/> while it is open, unless it shows a diagram or a comparison,
    /// which nothing replaces; else null.</summary>
    private DocumentTab? FindOriginToReplace(DocumentTab? origin)
    {
        var open = _tabs.KeepIfOpen(origin);
        if (open is null)
        {
            return null;
        }

        if (!open.IsReplaceable)
        {
            return null;
        }

        return open;
    }

    /// <summary>The active tab while it shows nothing, as a tab just opened, else the preview tab; null
    /// when neither is.</summary>
    private DocumentTab? FindPreviewTab()
    {
        var empty = FindEmptyActiveTab();
        if (empty is not null)
        {
            return empty;
        }

        return _tabs.Preview;
    }

    /// <summary>The active tab while it shows nothing, as a tab just opened; else null.</summary>
    private DocumentTab? FindEmptyActiveTab()
    {
        var active = _tabs.Active;
        if (active is null)
        {
            return null;
        }

        if (active.Content is not TabContent.Empty)
        {
            return null;
        }

        return active;
    }

    private bool IsPreviewed(TabDestination destination)
    {
        if (destination.InNewTab)
        {
            return false;
        }

        return _previewTabs;
    }

    /// <summary>The active tab while it shows nothing, brought to the front, else a new tab.</summary>
    private DocumentTab GetOrOpenEmptyTab()
    {
        var empty = FindEmptyActiveTab();
        if (empty is null)
        {
            return OpenNewTab();
        }

        ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = empty, Beside = null });
        return empty;
    }

    /// <summary>Opens an empty tab, which the view docks beside the active one.</summary>
    private DocumentTab OpenNewTab()
    {
        var tab = _tabs.AddNew();
        ViewCommands.Enqueue(new ViewCommand.ShowTab { Tab = tab, Beside = _tabs.Active });
        return tab;
    }
}
