using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.AssemblyArea;
using ILSight.BookmarkArea;
using ILSight.Exporting;
using ILSight.UserState;
using static ILSight.Decompilation;

namespace ILSight;

/// <summary>Moving through the code: the open tabs, the history of what they showed, the newest thing the user
/// asked for and whether tabs preview, which change together and which it holds privately. Showing a node, a table
/// row, a diagram or a comparison in a tab is a new intent (what was still being looked up for an older one is
/// dropped), and a node's, a row's or a diagram's a place in the history, the place the active tab leaves keeping
/// where its view stood; restoring a tab after a reload or a reopen is neither (<see cref="Restore"/>). Every change
/// to the tabs, the history and the intent is one of its methods, and the views that show nodes take it and call them
/// in the frame of their input. It reads the language and the decompiler options as values it is handed.
/// What waits on a lookup is a handler of its own: <see cref="NavigateHandler"/>, <see cref="ShowInMetadataHandler"/>,
/// <see cref="ShowInApiReferenceHandler"/>, <see cref="OpenAndShowIdHandler"/> and
/// <see cref="GoToHistoryEntryHandler"/>.</summary>
public sealed class Navigation
{
    /// <summary>The open document tabs.</summary>
    private readonly DocumentTabs _tabs = new();

    /// <summary>Back and forward over the places and diagrams the tabs showed.</summary>
    private readonly NavigationHistory _history = new();

    /// <summary>What the user last asked for, so that an answer to an older ask is dropped.</summary>
    private readonly UserIntent _intent = new();

    private bool _previewTabs;

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

    /// <summary>The places and diagrams the tabs showed, oldest first.</summary>
    public IReadOnlyList<HistoryEntry> HistoryEntries => _history.Entries;

    /// <summary>Where in <see cref="HistoryEntries"/> the tabs are now.</summary>
    public int HistoryIndex => _history.CurrentIndex;

    /// <summary>The history entry the tabs are at now; null when the history is empty.</summary>
    public HistoryEntry? CurrentEntry => _history.Current;

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
        return Tabs.IsOpen(_tabs, tab);
    }

    /// <summary>Whether <paramref name="place"/> is the place of the history entry the tabs are at.</summary>
    public bool IsCurrentPlace(NodePath? place)
    {
        return _history.IsCurrentPlace(place);
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
    /// <see cref="UserIntent.EndResolving{T}(int, Outcome{T})"/> says.</summary>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<T?> outcome) where T : class
    {
        return _intent.EndResolving(ticket, outcome);
    }

    /// <inheritdoc cref="EndResolving{T}(int, Outcome{T})"/>
    public Resolution<T> EndResolving<T>(int ticket, Outcome<T?> outcome) where T : struct
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
        return StartDecompiling(ViewCommands, tab, node, setup.Language, setup.Options, new Arrival(position));
    }

    /// <summary>Shows <paramref name="node"/>, picked in the tree, as <see cref="ShowNode"/> does, unless
    /// a tab shows it already, which is brought to the front instead. Returns the load it started, as
    /// <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? PreviewNode(BrowseNode node, DecompilerSetup setup)
    {
        var showing = FindTabShowing(node);
        if (showing is null)
        {
            return ShowNode(node, setup);
        }
        Tabs.MakeActive(_tabs, showing);
        ViewCommands.Enqueue(new ViewCommand.ShowTab(showing, Beside: null));
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
        Tabs.Keep(_tabs, tab);
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
            Tabs.Keep(_tabs, preview);
        }
    }

    /// <summary>Makes <paramref name="tab"/>, when it is open, the tab the user works in, pointing it again at what it
    /// shows when it was shown in another language than <paramref name="language"/> or with other options than
    /// <paramref name="options"/>. Returns the load that started in the tab, for <see cref="DecompileIntoTabHandler"/>
    /// to bring in; null when none did.</summary>
    public DecompileTicket? FocusTab(DocumentTab tab, DecompilerSetup setup)
    {
        if (!Tabs.IsOpen(_tabs, tab))
        {
            return null;
        }

        _intent.Begin();
        Tabs.MakeActive(_tabs, tab);
        return StartDecompilingAgainIfOutdated(ViewCommands, tab, setup.Language, setup.Options);
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
        var target = BrowseSessions.FindMetadataRow(session, grid, address);
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
    /// (<see cref="EndResolving{T}(int, Outcome{T})"/>). What it finds shows in a new tab when
    /// <paramref name="inNewTab"/>, else where <see cref="FindTab"/> leads then. Returns the ticket the lookup's answer
    /// is shown by.</summary>
    public ResolveTicket BeginResolving(bool inNewTab)
    {
        var destination = ChooseDestination(inNewTab);
        return new ResolveTicket { Intent = _intent.BeginResolving(), Destination = destination };
    }

    /// <summary>Begins opening the code of a type or member picked on <paramref name="diagram"/> beside it, in a new
    /// tab when <paramref name="inNewTab"/>: records the diagram in the history, so that going back returns to it, and
    /// begins the request whose lookup finds the code (<see cref="BeginResolving"/>). Returns its ticket; null when no
    /// open tab shows the diagram.</summary>
    public ResolveTicket? BeginResolvingFromDiagram(TypeDiagram diagram, bool inNewTab)
    {
        var tab = FindTabShowing(diagram);
        if (tab is null || tab.Content is not TabContent.Diagram shown)
        {
            return null;
        }

        _history.RecordDiagram(shown.Types, shown.Title);
        return BeginResolving(inNewTab);
    }

    /// <summary>Brings <paramref name="tab"/>, which shows a diagram, to the front as the tab the user works in: a new
    /// intent, and the diagram a place in the history.</summary>
    public void ShowDiagramTab(DocumentTab tab, TabContent.Diagram shown)
    {
        _intent.Begin();
        LeaveActiveTab();
        Tabs.MakeActive(_tabs, tab);
        ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
        _history.RecordDiagram(shown.Types, shown.Title);
    }

    /// <summary>Shows <paramref name="diagram"/>, titled <paramref name="title"/>, in the active tab while it shows
    /// nothing, else in a new tab, kept open and made the tab the user works in: a new intent, and the diagram a place
    /// in the history. Returns the tab.</summary>
    public DocumentTab OpenDiagram(TypeDiagram diagram, string title)
    {
        _intent.Begin();
        LeaveActiveTab();
        var tab = GetOrOpenEmptyTab();
        Tabs.ShowDiagram(tab, title, diagram);
        Tabs.Keep(_tabs, tab);
        Tabs.MakeActive(_tabs, tab);
        _history.RecordDiagram(diagram, title);
        return tab;
    }

    /// <summary>Shows <paramref name="diagram"/>, read back from the saved documents, in <paramref name="tab"/>, which
    /// waited for it: no place in the history and no new intent; taken as the diagram the user works in when the tab
    /// is the active one.</summary>
    public void RestoreDiagram(DocumentTab tab, TypeDiagram diagram, string title)
    {
        Tabs.ShowDiagram(tab, title, diagram);
        if (tab != _tabs.Active)
        {
            return;
        }

        Tabs.MakeActive(_tabs, tab);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/>, in the active tab while it shows
    /// nothing, else in a new tab, kept open and made the tab the user works in: a new intent. Returns the tab.</summary>
    public DocumentTab OpenComparison(AssemblyComparison comparison, string title)
    {
        _intent.Begin();
        LeaveActiveTab();
        var tab = GetOrOpenEmptyTab();
        Tabs.ShowComparison(tab, title, comparison);
        Tabs.Keep(_tabs, tab);
        Tabs.MakeActive(_tabs, tab);
        return tab;
    }

    /// <summary>Shows <paramref name="report"/> in a new tab, made the tab the user works in.</summary>
    public void ShowReport(ExportReport report)
    {
        var tab = OpenNewTab();
        Tabs.MakeActive(_tabs, tab);
        Tabs.ShowReport(tab, report);
    }

    /// <summary>Opens the tab the app starts with, empty and the tab the user works in.</summary>
    public void OpenFirstTab()
    {
        var first = _tabs.AddNew();
        Tabs.MakeActive(_tabs, first);
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

    /// <summary>Makes <paramref name="tab"/>, reopened from the saved documents, the tab the user works in, as it was
    /// when they were saved.</summary>
    public void RestoreActiveTab(DocumentTab tab)
    {
        Tabs.MakeActive(_tabs, tab);
    }

    /// <summary>Makes <paramref name="tab"/>, reopened from the saved documents, the preview tab, as it was when they
    /// were saved.</summary>
    public void RestorePreviewTab(DocumentTab tab)
    {
        Tabs.MakePreview(_tabs, tab);
    }

    /// <summary>Closes <paramref name="tab"/>, cancelling what it was doing, unless it is closed already: a new intent,
    /// and the diagram it shows dropped from the history. Returns whether it was the active tab, which leaves none
    /// active until the view focuses another.</summary>
    public ClosedTab CloseTab(DocumentTab tab)
    {
        if (!Tabs.IsOpen(_tabs, tab))
        {
            return new ClosedTab { WasActive = false };
        }

        _intent.Begin();
        var wasActive = tab == _tabs.Active;
        ForgetDiagramOf(tab);
        Tabs.Remove(_tabs, tab);
        return new ClosedTab { WasActive = wasActive };
    }

    /// <summary>Drops the diagram <paramref name="tab"/> shows, which it is about to stop showing, from
    /// the history.</summary>
    private void ForgetDiagramOf(DocumentTab tab)
    {
        var diagram = tab.Diagram;
        if (diagram is null)
        {
            return;
        }

        _history.ForgetDiagram(diagram);
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

        ForgetDiagramOf(kept);
        Tabs.Clear(kept);
        Tabs.MakeActive(_tabs, kept);
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

    /// <summary>Goes to the history entry before the current one, as <see cref="GoToEntry"/> does.</summary>
    public HistoryMove? GoBack(AssemblyList assemblies, DecompilerSetup setup)
    {
        return GoToEntry(_history.CurrentIndex - 1, assemblies, setup);
    }

    /// <summary>Goes to the history entry after the current one, as <see cref="GoToEntry"/> does.</summary>
    public HistoryMove? GoForward(AssemblyList assemblies, DecompilerSetup setup)
    {
        return GoToEntry(_history.CurrentIndex + 1, assemblies, setup);
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, however far back or forward; nothing when there
    /// is none there or it is the current one. The active tab leaves its entry, and a new intent begins: a diagram's
    /// entry brings the tab showing the diagram to the front; a node's entry shows its node in the tab it shows in,
    /// at the place the view stood when it is still good in <paramref name="language"/>, when that tab still shows it
    /// and its assembly in <paramref name="assemblies"/> is listed, else begins looking up the place the entry
    /// recorded, the app saying it is resolving. Returns what is left for the edge to start: the load the tab
    /// started, or the lookup (for <see cref="GoToHistoryEntryHandler"/>); null when nothing is.</summary>
    public HistoryMove? GoToEntry(int index, AssemblyList assemblies, DecompilerSetup setup)
    {
        if (!CanGoTo(index))
        {
            return null;
        }

        LeaveActiveTab();
        var entry = _history.MoveToEntry(index);
        switch (entry)
        {
            case HistoryEntry.Node node:
                return ShowEntry(node, assemblies, setup);
            case HistoryEntry.Diagram diagram:
                BringDiagramToFront(diagram.Shown);
                return null;
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

        if (!AssemblyListing.IsListed(assemblies, node.Root.Assembly))
        {
            return null;
        }

        return node;
    }

    /// <summary>Brings the tab showing <paramref name="diagram"/> to the front, dropping what was being
    /// navigated to meanwhile.</summary>
    private void BringDiagramToFront(TypeDiagram diagram)
    {
        _intent.Begin();
        var tab = FindTabShowing(diagram);
        if (tab is null)
        {
            return;
        }

        Tabs.MakeActive(_tabs, tab);
        ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
    }

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

    /// <summary>Records where the active tab's view is in the history entry it shows, before it shows
    /// something else, with what of the tab the entry was shown as, which a later read-back of the view keeps
    /// its place by.</summary>
    private void LeaveActiveTab()
    {
        var tab = _tabs.Active;
        if (tab is null)
        {
            return;
        }

        var position = tab.Position;
        if (position is null)
        {
            return;
        }

        if (!_history.IsCurrentPlace(tab.Location))
        {
            return;
        }

        _history.LeaveEntry(tab.Language, position.Value, TabVersion.Of(tab));
    }

    /// <summary>Where what is asked for now from the active tab will show, in a new tab when
    /// <paramref name="inNewTab"/>.</summary>
    public TabDestination ChooseDestination(bool inNewTab)
    {
        return new TabDestination(_tabs.Active, inNewTab);
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
        Tabs.MakeActive(_tabs, shownIn);
        var load = StartDecompiling(ViewCommands, shownIn, node, setup.Language, setup.Options, new Arrival(position, target, row));
        ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        return load;
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
            ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
        }

        if (IsPreviewed(destination))
        {
            Tabs.MakePreview(_tabs, tab);
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
        var open = Tabs.KeepIfOpen(_tabs, origin);
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

        ViewCommands.Enqueue(new ViewCommand.ShowTab(empty, Beside: null));
        return empty;
    }

    /// <summary>Opens an empty tab, which the view docks beside the active one.</summary>
    private DocumentTab OpenNewTab()
    {
        var tab = _tabs.AddNew();
        ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, _tabs.Active));
        return tab;
    }
}
