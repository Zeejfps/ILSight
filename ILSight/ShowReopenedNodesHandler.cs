using ILSight.Core;
using static ILSight.Decompilation;

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
    public async Task<List<DecompileTicket>> HandleAsync(AppState appState, ReopenedNodesLookup lookup, CancellationToken ct)
    {
        var nodes = lookup.Nodes;
        if (nodes.Count == 0)
        {
            return [];
        }

        var places = nodes
            .Select(static n => n.Place)
            .ToList();
        appState.Reopened.Finding++;
        var found = await ports.LookUpListedPlacesAsync(places, ct);
        appState.Reopened.Finding--;
        var shown = found.Match(static ok => ok.Value, _ => places.ConvertAll(static _ => (Lookup<BrowseNode>)new Lookup<BrowseNode>.NotFound()));
        return ShowReopenedNodes(appState, nodes, shown);
    }

    /// <summary>Shows each of <paramref name="nodes"/> as <paramref name="shown"/>, in the same order, found it.
    /// Returns the loads that started in the tabs.</summary>
    private static List<DecompileTicket> ShowReopenedNodes(AppState appState, IReadOnlyList<WaitingNode> nodes, List<Lookup<BrowseNode>> shown)
    {
        var loads = new List<DecompileTicket>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var load = ShowReopenedNode(appState, nodes[i], shown[i]);
            if (load is not null)
            {
                loads.Add(load);
            }
        }

        return loads;
    }

    /// <summary>Shows the node <paramref name="lookup"/> found where the waiting tab's node was, back where its view
    /// stood when it was shown in the language the app shows now, or that it is gone, or in a module that could not be
    /// read; unless the tab was pointed elsewhere or closed meanwhile. Returns the load it started in the tab; null
    /// when it started none.</summary>
    private static DecompileTicket? ShowReopenedNode(AppState appState, WaitingNode waiting, Lookup<BrowseNode> lookup)
    {
        if (!SavedDocuments.TryFinishWaiting(appState.Reopened, waiting.Reopened))
        {
            return null;
        }

        var tab = waiting.Reopened.Tab;
        if (!appState.Navigation.IsOpen(tab))
        {
            return null;
        }

        return lookup.Match<DecompileTicket?>(
            found => Restore(appState, waiting, tab, found.Value),
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

    /// <summary>Shows <paramref name="node"/> in <paramref name="tab"/>, back where its view stood. Returns the load it
    /// started in the tab; null when it started none.</summary>
    private static DecompileTicket? Restore(AppState appState, WaitingNode waiting, DocumentTab tab, BrowseNode node)
    {
        var position = Navigation.KeepPosition(waiting.Record.View, waiting.Record.Language, appState.Decompiler.Setup.Language);
        var load = appState.Navigation.Restore(tab, node, appState.Decompiler.Setup, position);
        RevealIfActive(appState, tab, node);
        return load;
    }

    private static void RevealIfActive(AppState appState, DocumentTab tab, BrowseNode node)
    {
        if (tab != appState.Navigation.ActiveTab)
        {
            return;
        }

        appState.ViewCommands.Enqueue(new ViewCommand.RevealNode(node));
    }
}
