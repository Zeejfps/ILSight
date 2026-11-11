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
    }

    /// <summary>Goes to the history entry at <paramref name="index"/>, however far back or forward; nothing when
    /// there is none there or it is the current one.</summary>
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
        var destination = ChooseDestination(appState, inNewTab: false);
        var place = entry.Place;
        var position = KeepPosition(entry.Position, entry.Language, appState.Language);
        var shown = FindShownNode(appState, FindTab(appState, destination), place);
        if (shown is not null)
        {
            appState.Intent.Begin();
            ShowNodeIn(appState, destination, shown, position);
            return;
        }

        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var resolved = await appState.Intent.ResolveAsync(() => NodeLookup.Find(assemblies, session, place), ct);
        ShowHistoryPlace(appState, place, position, resolved, destination);
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

    private void ShowHistoryPlace(AppState appState, NodePath place, ViewPosition? position, Resolution<BrowseNode> resolved,
        TabDestination destination)
    {
        resolved.Switch((appState, place, position, destination, handler: this),
            static (s, found) => ShowNodeIn(s.appState, s.destination, found.Value, s.position),
            static (s, _) => s.handler.ReportHistoryPlaceGone(s.place, null),
            static (s, failed) => s.handler.ReportHistoryPlaceGone(s.place, failed.Error),
            static (_, _) => { });
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
