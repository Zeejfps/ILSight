using ILSight.Core;

namespace ILSight;

/// <summary>Shows again the nodes the tabs reopened from the open list's saved documents wait for, once its
/// assemblies are listed: finds them through <see cref="IPorts"/>, without holding up the screen, and points each tab
/// still waiting at its node, or shows that it is gone. Returns the loads it started in the tabs for the edge to
/// start.</summary>
public sealed class ShowReopenedNodesHandler(ShowReopenedNodesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The listed node at each of <paramref name="places"/>, in the same order: not found for one that is
        /// gone or null itself, unreadable for one in a module whose type system the library could not read; looked
        /// for without holding up the screen. A lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<List<Lookup<BrowseNode>>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct);
    }

    /// <summary>Finds the nodes <paramref name="lookup"/>'s tabs wait for and shows each, back where its view stood
    /// when it was shown in the language the app shows now, or that it is gone; all of them gone when finding them
    /// failed. A tab pointed elsewhere or closed meanwhile is left alone. Returns the loads that started in the tabs,
    /// for the edge that started this to start under each tab's work.</summary>
    public async Task<List<DecompileTicket>> HandleAsync(ReopenedTabs reopened, Navigation navigation, DecompilerChoice decompiler,
        Queue<ViewCommand> viewCommands, ReopenedNodesLookup lookup, CancellationToken ct)
    {
        var nodes = lookup.Nodes;
        if (nodes.Count == 0)
        {
            return [];
        }

        var places = nodes
            .Select(static n => n.Place)
            .ToList();
        var found = await ports.LookUpListedPlacesAsync(places, ct);
        var shown = found.Match(static ok => ok.Value, _ => places.ConvertAll(static _ => (Lookup<BrowseNode>)new Lookup<BrowseNode>.NotFound()));
        return ShowReopenedNodes(reopened, navigation, decompiler, viewCommands, nodes, shown);
    }

    // shown holds what was found for each of nodes, in the same order.
    private static List<DecompileTicket> ShowReopenedNodes(ReopenedTabs reopened, Navigation navigation, DecompilerChoice decompiler,
        Queue<ViewCommand> viewCommands, IReadOnlyList<WaitingNode> nodes, List<Lookup<BrowseNode>> shown)
    {
        var loads = new List<DecompileTicket>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var load = ShowReopenedNode(reopened, navigation, decompiler, viewCommands, nodes[i], shown[i]);
            if (load is not null)
            {
                loads.Add(load);
            }
        }

        return loads;
    }

    private static DecompileTicket? ShowReopenedNode(ReopenedTabs reopened, Navigation navigation, DecompilerChoice decompiler,
        Queue<ViewCommand> viewCommands, WaitingNode waiting, Lookup<BrowseNode> lookup)
    {
        if (!SavedDocuments.TryFinishWaiting(reopened, waiting.Reopened))
        {
            return null;
        }

        var tab = waiting.Reopened.Tab;
        if (!navigation.IsOpen(tab))
        {
            return null;
        }

        return lookup.Match<DecompileTicket?>(
            found => Restore(navigation, decompiler, viewCommands, waiting, tab, found.Value),
            _ =>
            {
                tab.Load.ShowGone(waiting.Record.Location);
                return null;
            },
            unreadable =>
            {
                tab.Load.ShowUnreadable(waiting.Record.Location, unreadable.Module);
                return null;
            });
    }

    private static DecompileTicket? Restore(Navigation navigation, DecompilerChoice decompiler, Queue<ViewCommand> viewCommands, WaitingNode waiting,
        DocumentTab tab, BrowseNode node)
    {
        var position = Navigation.KeepPosition(waiting.Record.View, waiting.Record.Language, decompiler.Setup.Language);
        var load = navigation.Restore(tab, node, decompiler.Setup, position);
        RevealIfActive(navigation, viewCommands, tab, node);
        return load;
    }

    private static void RevealIfActive(Navigation navigation, Queue<ViewCommand> viewCommands, DocumentTab tab, BrowseNode node)
    {
        if (tab != navigation.ActiveTab)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.RevealNode(node));
    }
}
