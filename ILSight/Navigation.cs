using ILSight.Core;
using ILSight.Core.Metadata;
using static ILSight.Decompilation;

namespace ILSight;

/// <summary>Moving through the code: showing nodes and table rows in the tabs, and going back and forward through
/// the history. What waits on a lookup is a handler of its own: <see cref="NavigateHandler"/>,
/// <see cref="ShowInMetadataHandler"/>, <see cref="ShowInApiReferenceHandler"/>, <see cref="OpenAndShowIdHandler"/>
/// and <see cref="GoToHistoryEntryHandler"/>.</summary>
public static class Navigation
{
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

    /// <summary>Shows <paramref name="node"/> in the preview tab, or the active tab while tabs do not
    /// preview, or else a new tab kept open, reveals it in the tree and records it in the history. Returns the
    /// load it started in the tab, for <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows
    /// the node already.</summary>
    public static DecompileTicket? ShowNode(AppState appState, BrowseNode node, bool inNewTab = false)
    {
        appState.Intent.Begin();
        LeaveActiveTab(appState);
        var load = ShowNodeIn(appState, ChooseDestination(appState, inNewTab), node);
        appState.History.RecordPlace(node, appState.Language);
        return load;
    }

    /// <summary>Shows <paramref name="node"/>, picked in the tree, as <see cref="ShowNode"/> does, unless
    /// a tab shows it already, which is brought to the front instead. Returns the load it started, as
    /// <see cref="ShowNode"/> does.</summary>
    public static DecompileTicket? PreviewNode(AppState appState, BrowseNode node)
    {
        var showing = FindTabShowing(appState, node);
        if (showing is null)
        {
            return ShowNode(appState, node);
        }
        Tabs.MakeActive(appState.Tabs, showing);
        appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(showing, Beside: null));
        return null;
    }

    /// <summary>The tab showing <paramref name="node"/>, the active one first; null when none does.</summary>
    private static DocumentTab? FindTabShowing(AppState appState, BrowseNode node)
    {
        var active = appState.Tabs.Active;
        if (active is not null && active.Node == node)
        {
            return active;
        }

        foreach (var tab in appState.Tabs.Open)
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
    public static DecompileTicket? OpenNode(AppState appState, BrowseNode node)
    {
        if (!appState.PreviewTabs)
        {
            return ShowNode(appState, node);
        }
        var load = PreviewNode(appState, node);
        KeepActiveTabOpen(appState);
        return load;
    }

    private static void KeepActiveTabOpen(AppState appState)
    {
        var active = appState.Tabs.Active;
        if (active is null)
        {
            return;
        }

        KeepTabOpen(appState, active);
    }

    /// <summary>Keeps <paramref name="tab"/> open, so that what is picked next shows in another tab.</summary>
    public static void KeepTabOpen(AppState appState, DocumentTab tab)
    {
        Tabs.Keep(appState.Tabs, tab);
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
            Tabs.Keep(appState.Tabs, preview);
        }
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
    public static DecompileTicket? ShowMetadataTarget(AppState appState, MetadataTarget target, bool inNewTab)
    {
        appState.Intent.Begin();
        return ShowRowIn(appState, ChooseDestination(appState, inNewTab), target);
    }

    /// <summary>Shows the table row <paramref name="address"/>, a link of the grid <paramref name="grid"/>
    /// shows, leads to, as <see cref="ShowMetadataTarget"/> does; nothing when it leads nowhere. Returns the load
    /// it started, as <see cref="ShowNode"/> does.</summary>
    public static DecompileTicket? ShowMetadataLink(AppState appState, BrowseNode grid, MetadataAddress address, bool inNewTab)
    {
        var target = BrowseSessions.FindMetadataRow(appState.Session, grid, address);
        if (target is null)
        {
            return null;
        }

        return ShowMetadataTarget(appState, target.Value, inNewTab);
    }

    /// <summary>Shows the table row <paramref name="target"/> leads to in the tab <paramref name="destination"/>
    /// leads to now, and records it in the history. Returns the load it started, as <see cref="ShowNode"/>
    /// does.</summary>
    internal static DecompileTicket? ShowRowIn(AppState appState, TabDestination destination, MetadataTarget target)
    {
        LeaveActiveTab(appState);
        var load = ShowNodeIn(appState, destination, target.Node, row: target.Row);
        appState.History.RecordPlace(target.Node, appState.Language);
        return load;
    }

    /// <summary>Goes to the history entry before the current one, as <see cref="GoToHistoryEntryHandler"/> does.</summary>
    public static Task GoBackAsync(AppState appState, CancellationToken ct)
    {
        return appState.GoToHistoryEntryHandler.HandleAsync(appState, appState.History.CurrentIndex - 1, ct);
    }

    /// <summary>Goes to the history entry after the current one, as <see cref="GoToHistoryEntryHandler"/> does.</summary>
    public static Task GoForwardAsync(AppState appState, CancellationToken ct)
    {
        return appState.GoToHistoryEntryHandler.HandleAsync(appState, appState.History.CurrentIndex + 1, ct);
    }

    internal static DocumentTab? FindTabShowing(AppState appState, TypeDiagram diagram)
    {
        foreach (var tab in appState.Tabs.Open)
        {
            if (tab.Diagram == diagram)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>Records where the active tab's view is in the history entry it shows, before it shows
    /// something else.</summary>
    internal static void LeaveActiveTab(AppState appState)
    {
        var tab = appState.Tabs.Active;
        if (tab is null)
        {
            return;
        }

        var position = tab.Position;
        if (position is null)
        {
            return;
        }

        if (!appState.History.IsCurrentPlace(tab.Location))
        {
            return;
        }

        appState.History.LeaveEntry(tab.Language, position.Value);
    }

    /// <summary>Where what is asked for now from the active tab will show, in a new tab when
    /// <paramref name="inNewTab"/>.</summary>
    internal static TabDestination ChooseDestination(AppState appState, bool inNewTab)
    {
        return new TabDestination(appState.Tabs.Active, inNewTab);
    }

    /// <summary>Shows <paramref name="node"/> in the tab <paramref name="destination"/> leads to now, makes
    /// that tab active, and commands the view to bring it to the front and reveal the node in the tree. Returns the
    /// load it started in the tab, for <see cref="DecompileIntoTabHandler"/> to bring in; null when the tab shows
    /// the node already.</summary>
    internal static DecompileTicket? ShowNodeIn(
        AppState appState,
        TabDestination destination,
        BrowseNode node,
        ViewPosition? position = null,
        Bookmark? target = null,
        int? row = null)
    {
        var shownIn = GetOrOpenTab(appState, destination);
        Tabs.MakeActive(appState.Tabs, shownIn);
        var load = StartDecompiling(appState.ViewCommands, shownIn, node, appState.Language, appState.DecompilerOptions, new Arrival(position, target, row));
        appState.ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
        return load;
    }

    /// <summary>The tab <paramref name="destination"/> leads to now, brought to the front, else a new one
    /// opened; made the preview tab when it shows what tabs preview.</summary>
    private static DocumentTab GetOrOpenTab(AppState appState, TabDestination destination)
    {
        var tab = FindTab(appState, destination);
        if (tab is null)
        {
            tab = OpenNewTab(appState);
        }
        else
        {
            appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
        }

        if (IsPreviewed(appState, destination))
        {
            Tabs.MakePreview(appState.Tabs, tab);
        }

        return tab;
    }

    /// <summary>The open tab <paramref name="destination"/> leads to now; null when it calls for a new one.</summary>
    internal static DocumentTab? FindTab(AppState appState, TabDestination destination)
    {
        if (destination.InNewTab)
        {
            return null;
        }

        if (appState.PreviewTabs)
        {
            return FindPreviewTab(appState);
        }

        return FindOriginToReplace(appState, destination.Origin);
    }

    /// <summary><paramref name="origin"/> while it is open, unless it shows a diagram or a comparison,
    /// which nothing replaces; else null.</summary>
    private static DocumentTab? FindOriginToReplace(AppState appState, DocumentTab? origin)
    {
        var open = Tabs.KeepIfOpen(appState.Tabs, origin);
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
    private static DocumentTab? FindPreviewTab(AppState appState)
    {
        var empty = FindEmptyActiveTab(appState);
        if (empty is not null)
        {
            return empty;
        }

        return appState.Tabs.Preview;
    }

    /// <summary>The active tab while it shows nothing, as a tab just opened; else null.</summary>
    internal static DocumentTab? FindEmptyActiveTab(AppState appState)
    {
        var active = appState.Tabs.Active;
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

    private static bool IsPreviewed(AppState appState, TabDestination destination)
    {
        if (destination.InNewTab)
        {
            return false;
        }

        return appState.PreviewTabs;
    }

    /// <summary>The active tab while it shows nothing, brought to the front, else a new tab.</summary>
    internal static DocumentTab GetOrOpenEmptyTab(AppState appState)
    {
        var empty = FindEmptyActiveTab(appState);
        if (empty is null)
        {
            return OpenNewTab(appState);
        }

        appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(empty, Beside: null));
        return empty;
    }

    /// <summary>Opens an empty tab, which the view docks beside the active one.</summary>
    internal static DocumentTab OpenNewTab(AppState appState)
    {
        var tab = Tabs.AddNew(appState.Tabs);
        appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, appState.Tabs.Active));
        return tab;
    }
}
