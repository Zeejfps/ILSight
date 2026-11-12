using ILSight.Core;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Goes back and forward through the history: to the place an entry recorded, or the diagram it showed.</summary>
public sealed class GoToHistoryEntryHandler(GoToHistoryEntryHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the place an entry recorded is gone.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node at <paramref name="place"/>, null when it is gone, looked for without holding up the
        /// screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<BrowseNode?>> LookUpPlaceAsync(NodePath place, CancellationToken ct);

        /// <summary>Brings in what <paramref name="load"/> started loading in its tab and shows it there, commanding
        /// the view through <paramref name="viewCommands"/> where to stand in it.</summary>
        Task DecompileIntoTabAsync(Queue<ViewCommand> viewCommands, DecompileTicket load, CancellationToken ct);
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, however far back or forward, returning once
    /// the node it recorded is decompiled; nothing when there is none there or it is the current one.</summary>
    public async Task HandleAsync(AppState appState, int index, CancellationToken ct)
    {
        if (!CanGoTo(appState, index))
        {
            return;
        }

        LeaveActiveTab(appState);
        var entry = appState.History.MoveToEntry(index);
        switch (entry)
        {
            case HistoryEntry.Node node:
                await RestoreAsync(appState, node, ct);
                break;
            case HistoryEntry.Diagram diagram:
                BringDiagramToFront(appState, diagram.Shown);
                break;
        }
    }

    private static bool CanGoTo(AppState appState, int index)
    {
        if (index < 0)
        {
            return false;
        }

        if (index >= appState.History.Entries.Count)
        {
            return false;
        }

        return index != appState.History.CurrentIndex;
    }

    private async Task RestoreAsync(AppState appState, HistoryEntry.Node entry, CancellationToken ct)
    {
        var load = await ShowEntryAsync(appState, entry, ct);
        if (load is null)
        {
            return;
        }

        await ports.DecompileIntoTabAsync(appState.ViewCommands, load, ct);
    }

    /// <summary>Shows the node <paramref name="entry"/> recorded: the one the tab shows when it is still there, else
    /// the one found at its place, or reports the place gone. Returns the load it started in the tab; null when it
    /// started none.</summary>
    private async Task<DecompileTicket?> ShowEntryAsync(AppState appState, HistoryEntry.Node entry, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab: false);
        var place = entry.Place;
        var position = KeepPosition(entry.Position, entry.Language, appState.Language);
        var shown = FindShownNode(appState, FindTab(appState, destination), place);
        if (shown is not null)
        {
            appState.Intent.Begin();
            return ShowNodeIn(appState, destination, shown, position);
        }

        var ticket = appState.Intent.BeginResolving();
        var found = await ports.LookUpPlaceAsync(place, ct);
        return ShowHistoryPlace(appState, place, position, appState.Intent.EndResolving(ticket, found), destination);
    }

    private static BrowseNode? FindShownNode(AppState appState, DocumentTab? tab, NodePath place)
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

        if (!AssemblyListing.IsListed(appState.Assemblies, node.Root.Assembly))
        {
            return null;
        }

        return node;
    }

    private DecompileTicket? ShowHistoryPlace(AppState appState, NodePath place, ViewPosition? position, Resolution<BrowseNode> resolved,
        TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => ShowNodeIn(appState, destination, found.Value, position),
            _ =>
            {
                ReportHistoryPlaceGone(place, null);
                return null;
            },
            failed =>
            {
                ReportHistoryPlaceGone(place, failed.Error);
                return null;
            },
            static _ => null);
    }

    private void ReportHistoryPlaceGone(NodePath place, Exception? error)
    {
        ports.ReportError(new AppProblem.HistoryPlaceGone(place, error));
    }

    /// <summary>Brings the tab showing <paramref name="diagram"/> to the front, dropping what was being
    /// navigated to meanwhile.</summary>
    private static void BringDiagramToFront(AppState appState, TypeDiagram diagram)
    {
        appState.Intent.Begin();
        var tab = FindTabShowing(appState, diagram);
        if (tab is null)
        {
            return;
        }

        Tabs.MakeActive(appState.Tabs, tab);
        appState.ViewCommands.Enqueue(new ViewCommand.ShowTab(tab, Beside: null));
    }
}
