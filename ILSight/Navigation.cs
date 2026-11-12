using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Bookmarking;
using static ILSight.Decompilation;

namespace ILSight;

/// <summary>Moving through the code: the open tabs, the history of what they showed, and the newest thing the user
/// asked for, which change together. Showing a node or a table row in a tab is a new intent (what was still being
/// looked up for an older one is dropped) and a place in the history, the place the active tab leaves keeping where
/// its view stood; this type's methods keep that, and the views that show nodes take it and call them in the frame
/// of their input. It reads the language, the decompiler options and whether tabs preview as values it is handed.
/// What waits on a lookup is a handler of its own: <see cref="NavigateHandler"/>, <see cref="ShowInMetadataHandler"/>,
/// <see cref="ShowInApiReferenceHandler"/>, <see cref="OpenAndShowIdHandler"/> and
/// <see cref="GoToHistoryEntryHandler"/>.</summary>
public sealed class Navigation
{
    /// <summary>The open document tabs.</summary>
    public DocumentTabs Tabs { get; } = new();

    /// <summary>Back and forward over the places and diagrams the tabs showed.</summary>
    public NavigationHistory History { get; } = new();

    /// <summary>What the user last asked for, so that an answer to an older ask is dropped.</summary>
    public UserIntent Intent { get; } = new();

    /// <summary>Where what the view is to do about a change (bring a tab to the front, reveal a node in the tree)
    /// is queued, for the screen to run before it draws next.</summary>
    public required Queue<ViewCommand> ViewCommands { get; init; }

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

    /// <summary>Shows <paramref name="node"/> in the preview tab while <paramref name="previewTabs"/>, or the active
    /// tab while tabs do not preview, or else a new tab kept open, in <paramref name="language"/> with
    /// <paramref name="options"/>; reveals it in the tree and records it in the history. Returns the load it started
    /// in the tab, for <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows the node
    /// already.</summary>
    public DecompileTicket? ShowNode(BrowseNode node, DecompilationLanguage language, DecompilerOptions options, bool previewTabs, bool inNewTab = false)
    {
        Intent.Begin();
        LeaveActiveTab();
        var load = ShowNodeIn(ChooseDestination(inNewTab), node, language, options, previewTabs);
        History.RecordPlace(node, language);
        return load;
    }

    /// <summary>Shows <paramref name="node"/>, picked in the tree, as <see cref="ShowNode"/> does, unless
    /// a tab shows it already, which is brought to the front instead. Returns the load it started, as
    /// <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? PreviewNode(BrowseNode node, DecompilationLanguage language, DecompilerOptions options, bool previewTabs)
    {
        var showing = FindTabShowing(node);
        if (showing is null)
        {
            return ShowNode(node, language, options, previewTabs);
        }
        ILSight.Tabs.MakeActive(Tabs, showing);
        ViewCommands.Enqueue(new ViewCommand.ShowTab(showing, Beside: null));
        return null;
    }

    /// <summary>The tab showing <paramref name="node"/>, the active one first; null when none does.</summary>
    private DocumentTab? FindTabShowing(BrowseNode node)
    {
        var active = Tabs.Active;
        if (active is not null && active.Node == node)
        {
            return active;
        }

        foreach (var tab in Tabs.Open)
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
    public DecompileTicket? OpenNode(BrowseNode node, DecompilationLanguage language, DecompilerOptions options, bool previewTabs)
    {
        if (!previewTabs)
        {
            return ShowNode(node, language, options, previewTabs);
        }
        var load = PreviewNode(node, language, options, previewTabs);
        KeepActiveTabOpen();
        return load;
    }

    private void KeepActiveTabOpen()
    {
        var active = Tabs.Active;
        if (active is null)
        {
            return;
        }

        KeepTabOpen(active);
    }

    /// <summary>Keeps <paramref name="tab"/> open, so that what is picked next shows in another tab.</summary>
    public void KeepTabOpen(DocumentTab tab)
    {
        ILSight.Tabs.Keep(Tabs, tab);
    }

    /// <summary>Has what is picked and followed shown in the preview tab, or, when <paramref name="on"/>
    /// is false, in the active tab, the preview tab then kept open as any other.</summary>
    public static void UsePreviewTabs(AppState appState, bool on)
    {
        appState.PreviewTabs = on;
        if (on)
        {
            return;
        }

        var preview = appState.Tabs.Preview;
        if (preview is not null)
        {
            ILSight.Tabs.Keep(appState.Tabs, preview);
        }
    }

    /// <summary>Makes <paramref name="tab"/>, when it is open, the tab the user works in, pointing it again at what it
    /// shows when it was shown in another language than <paramref name="language"/> or with other options than
    /// <paramref name="options"/>. Returns the load that started in the tab, for <see cref="DecompileIntoTabHandler"/>
    /// to bring in; null when none did.</summary>
    public DecompileTicket? FocusTab(DocumentTab tab, DecompilationLanguage language, DecompilerOptions options)
    {
        if (!ILSight.Tabs.IsOpen(Tabs, tab))
        {
            return null;
        }

        Intent.Begin();
        ILSight.Tabs.MakeActive(Tabs, tab);
        return StartDecompilingAgainIfOutdated(ViewCommands, tab, language, options);
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
    public DecompileTicket? ShowMetadataTarget(MetadataTarget target, bool inNewTab, DecompilationLanguage language, DecompilerOptions options, bool previewTabs)
    {
        Intent.Begin();
        return ShowRowIn(ChooseDestination(inNewTab), target, language, options, previewTabs);
    }

    /// <summary>Shows the table row <paramref name="address"/>, a link of the grid <paramref name="grid"/>
    /// shows, leads to among the nodes of <paramref name="session"/>, as <see cref="ShowMetadataTarget"/> does;
    /// nothing when it leads nowhere. Returns the load it started, as <see cref="ShowNode"/> does.</summary>
    public DecompileTicket? ShowMetadataLink(BrowseSession session, BrowseNode grid, MetadataAddress address, bool inNewTab, DecompilationLanguage language,
        DecompilerOptions options, bool previewTabs)
    {
        var target = BrowseSessions.FindMetadataRow(session, grid, address);
        if (target is null)
        {
            return null;
        }

        return ShowMetadataTarget(target.Value, inNewTab, language, options, previewTabs);
    }

    /// <summary>Shows the table row <paramref name="target"/> leads to in the tab <paramref name="destination"/>
    /// leads to now, and records it in the history. Returns the load it started, as <see cref="ShowNode"/>
    /// does.</summary>
    internal DecompileTicket? ShowRowIn(TabDestination destination, MetadataTarget target, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs)
    {
        LeaveActiveTab();
        var load = ShowNodeIn(destination, target.Node, language, options, previewTabs, row: target.Row);
        History.RecordPlace(target.Node, language);
        return load;
    }

    /// <summary>Goes to the history entry before the current one, as <see cref="GoToHistoryEntryHandler"/> does.</summary>
    public static Task<DecompileTicket?> GoBackAsync(AppState appState, CancellationToken ct)
    {
        return appState.GoToHistoryEntryHandler.HandleAsync(appState, appState.History.CurrentIndex - 1, ct);
    }

    /// <summary>Goes to the history entry after the current one, as <see cref="GoToHistoryEntryHandler"/> does.</summary>
    public static Task<DecompileTicket?> GoForwardAsync(AppState appState, CancellationToken ct)
    {
        return appState.GoToHistoryEntryHandler.HandleAsync(appState, appState.History.CurrentIndex + 1, ct);
    }

    internal DocumentTab? FindTabShowing(TypeDiagram diagram)
    {
        foreach (var tab in Tabs.Open)
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
    internal void LeaveActiveTab()
    {
        var tab = Tabs.Active;
        if (tab is null)
        {
            return;
        }

        var position = tab.Position;
        if (position is null)
        {
            return;
        }

        if (!History.IsCurrentPlace(tab.Location))
        {
            return;
        }

        History.LeaveEntry(tab.Language, position.Value, TabVersion.Of(tab));
    }

    /// <summary>Where what is asked for now from the active tab will show, in a new tab when
    /// <paramref name="inNewTab"/>.</summary>
    internal TabDestination ChooseDestination(bool inNewTab)
    {
        return new TabDestination(Tabs.Active, inNewTab);
    }

    /// <summary>Shows <paramref name="node"/> in the tab <paramref name="destination"/> leads to now, in
    /// <paramref name="language"/> with <paramref name="options"/>, makes that tab active, and commands the view to
    /// bring it to the front and reveal the node in the tree. Returns the load it started in the tab, for
    /// <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows the node already.</summary>
    internal DecompileTicket? ShowNodeIn(
        TabDestination destination,
        BrowseNode node,
        DecompilationLanguage language,
        DecompilerOptions options,
        bool previewTabs,
        ViewPosition? position = null,
        Bookmark? target = null,
        int? row = null)
    {
        var shownIn = GetOrOpenTab(destination, previewTabs);
        ILSight.Tabs.MakeActive(Tabs, shownIn);
        var load = StartDecompiling(ViewCommands, shownIn, node, language, options, new Arrival(position, target, row));
        ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        return load;
    }

    /// <summary>The tab <paramref name="destination"/> leads to now, brought to the front, else a new one
    /// opened; made the preview tab when it shows what tabs preview.</summary>
    private DocumentTab GetOrOpenTab(TabDestination destination, bool previewTabs)
    {
        var tab = FindTab(destination, previewTabs);
        if (tab is null)
        {
            tab = OpenNewTab();
        }
        else
        {
            ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
        }

        if (IsPreviewed(destination, previewTabs))
        {
            ILSight.Tabs.MakePreview(Tabs, tab);
        }

        return tab;
    }

    /// <summary>The open tab <paramref name="destination"/> leads to now, the preview tab while
    /// <paramref name="previewTabs"/>; null when it calls for a new one.</summary>
    internal DocumentTab? FindTab(TabDestination destination, bool previewTabs)
    {
        if (destination.InNewTab)
        {
            return null;
        }

        if (previewTabs)
        {
            return FindPreviewTab();
        }

        return FindOriginToReplace(destination.Origin);
    }

    /// <summary><paramref name="origin"/> while it is open, unless it shows a diagram or a comparison,
    /// which nothing replaces; else null.</summary>
    private DocumentTab? FindOriginToReplace(DocumentTab? origin)
    {
        var open = ILSight.Tabs.KeepIfOpen(Tabs, origin);
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

        return Tabs.Preview;
    }

    /// <summary>The active tab while it shows nothing, as a tab just opened; else null.</summary>
    internal DocumentTab? FindEmptyActiveTab()
    {
        var active = Tabs.Active;
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

    private static bool IsPreviewed(TabDestination destination, bool previewTabs)
    {
        if (destination.InNewTab)
        {
            return false;
        }

        return previewTabs;
    }

    /// <summary>The active tab while it shows nothing, brought to the front, else a new tab.</summary>
    internal DocumentTab GetOrOpenEmptyTab()
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
    internal DocumentTab OpenNewTab()
    {
        var tab = Tabs.AddNew();
        ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Tabs.Active));
        return tab;
    }
}
