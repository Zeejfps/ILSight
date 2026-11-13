using ILSight.Core;

namespace ILSight;

/// <summary>Goes back and forward through the history to a place the tab no longer shows: looks the place up and
/// shows what is there.</summary>
public sealed class GoToHistoryEntryHandler(GoToHistoryEntryHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user looking for the place an entry recorded failed, or it is in a module that could not
        /// be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user what they asked for was refused, and why, where they acted.</summary>
        void ShowRefusal(Refusal refusal);


        /// <summary>The node at <paramref name="place"/> among the listed assemblies; not found when it is gone,
        /// unreadable when in a module whose type system the library could not read; looked for without holding up the
        /// screen (it may build children); opens and lists nothing. A lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpListedPlaceAsync(NodePath place, CancellationToken ct);
    }

    /// <summary>Looks up the place <paramref name="lookup"/> is for, which going to a history entry began
    /// (<see cref="Navigation.GoToEntry"/>, in the frame it was asked in) since the tab no longer shows it, then shows
    /// the node found there at the place the view stood. Returns the load that started in the tab, for the edge that
    /// started this to start under the tab's work; null when none started. Reports the place gone; drops it when
    /// something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, HistoryMove.LookUp lookup, CancellationToken ct)
    {
        var found = await ports.LookUpListedPlaceAsync(lookup.Place, ct);
        return ShowHistoryPlace(appState, lookup.Place, lookup.Position, appState.Navigation.EndResolving(lookup.Ticket.Intent, found), lookup.Ticket.Destination);
    }

    private DecompileTicket? ShowHistoryPlace(AppState appState, NodePath place, ViewPosition? position, Resolution<BrowseNode> resolved,
        TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => appState.Navigation.ShowHistoryPlace(destination, found.Value, appState.Decompiler.Setup, position),
            _ =>
            {
                ports.ShowRefusal(new Refusal.HistoryPlaceGone { Place = place });
                return null;
            },
            unreadable =>
            {
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                return null;
            },
            failed =>
            {
                ports.ReportError(new AppProblem.HistoryPlaceGone { Place = place, Error = failed.Error });
                return null;
            },
            static _ => null);
    }
}
