using ILSight.Core;

namespace ILSight;

/// <summary>Shows again the nodes the tabs reopened from the open list's saved documents wait for, once its
/// assemblies are listed: finds them through <see cref="IPorts"/>, without holding up the screen, and points each tab
/// still waiting at its node, or shows that it is gone, or that finding it failed. Returns the loads it started in the
/// tabs for the edge to start.</summary>
public sealed class ShowReopenedNodesHandler(ShowReopenedNodesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user finding a tab's place failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What looking for the listed node at each of <paramref name="places"/> came to, in the same order:
        /// not found for one that is gone or null itself, unreadable for one in a module whose type system the library
        /// could not read, <see cref="Outcome{T}.Failed"/> for one whose lookup failed; looked for without holding up the
        /// screen. The cancellation of <paramref name="ct"/> is let through.</summary>
        Task<List<Outcome<Lookup<BrowseNode>>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct);
    }

    /// <summary>Finds the nodes <paramref name="lookup"/>'s tabs wait for and shows each as its own lookup came out:
    /// back where its view stood when it was shown in the language the app shows now, or that it is gone, or, when
    /// finding it failed, saying so, and why, where its document was, reported once. A tab pointed elsewhere or closed
    /// meanwhile is left alone. Returns the loads that started in the tabs, for the edge that started this to start
    /// under each tab's work.</summary>
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
        return ShowReopenedNodes(reopened, navigation, decompiler, viewCommands, nodes, found);
    }

    /// <summary>Has each of <paramref name="nodes"/> still waiting, in a tab still open, say that finding its place
    /// failed with <paramref name="error"/>, where its document was; the edge's settle when the workflow that finds them
    /// fails.</summary>
    internal static void ShowLookupFailed(ReopenedTabs reopened, Navigation navigation, IReadOnlyList<WaitingNode> nodes, Exception error)
    {
        foreach (var waiting in nodes)
        {
            if (!SavedDocuments.TryFinishWaiting(reopened, waiting.Reopened))
            {
                continue;
            }

            var tab = waiting.Reopened.Tab;
            if (navigation.IsOpen(tab))
            {
                tab.Load.ShowLookupFailed(waiting.Record.Location, error);
            }
        }
    }

    // shown holds what looking for each of nodes came to, in the same order.
    private List<DecompileTicket> ShowReopenedNodes(ReopenedTabs reopened, Navigation navigation, DecompilerChoice decompiler,
        Queue<ViewCommand> viewCommands, IReadOnlyList<WaitingNode> nodes, List<Outcome<Lookup<BrowseNode>>> shown)
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

    private DecompileTicket? ShowReopenedNode(ReopenedTabs reopened, Navigation navigation, DecompilerChoice decompiler,
        Queue<ViewCommand> viewCommands, WaitingNode waiting, Outcome<Lookup<BrowseNode>> outcome)
    {
        if (!SavedDocuments.TryFinishWaiting(reopened, waiting.Reopened))
        {
            return null;
        }

        if (outcome is Outcome<Lookup<BrowseNode>>.Failed failed)
        {
            ports.ReportError(LookupFailures.OfPlace(waiting.Record.Location, new PlaceLookupStep.ReopeningTabs(), failed.Error));
        }

        var tab = waiting.Reopened.Tab;
        if (!navigation.IsOpen(tab))
        {
            return null;
        }

        return outcome switch
        {
            Outcome<Lookup<BrowseNode>>.Ok ok => ShowFound(navigation, decompiler, viewCommands, waiting, tab, ok.Value),
            Outcome<Lookup<BrowseNode>>.Failed lookupFailed => ShowLookupFailed(waiting, tab, lookupFailed.Error),
        };
    }

    private static DecompileTicket? ShowLookupFailed(WaitingNode waiting, DocumentTab tab, Exception error)
    {
        tab.Load.ShowLookupFailed(waiting.Record.Location, error);
        return null;
    }

    private static DecompileTicket? ShowFound(Navigation navigation, DecompilerChoice decompiler, Queue<ViewCommand> viewCommands, WaitingNode waiting,
        DocumentTab tab, Lookup<BrowseNode> lookup)
    {
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
