using ILSight.Core;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Follows a reference: what a reference in code, a forwarded type, an imported method, or a base or
/// derived type leads to.</summary>
public sealed class NavigateHandler(NavigateHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node <paramref name="reference"/> leads to, null when it leads nowhere, looked for without
        /// holding up the screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<BrowseNode?>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background (it may load an assembly), then shows
    /// it in the tab it was asked from, or a new one when <paramref name="inNewTab"/>, and records it in the
    /// history. Returns the load that started in the tab, for the edge that started this to start under the tab's
    /// work; null when none started. Reports a reference that leads nowhere; drops it when something else was asked
    /// for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, CodeReference.Navigable reference, bool inNewTab, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab);
        var ticket = appState.Intent.BeginResolving();
        var found = await ports.LookUpReferenceAsync(reference, ct);
        return ShowResolved(appState, reference, appState.Intent.EndResolving(ticket, found), destination);
    }

    /// <summary>Shows what <paramref name="resolved"/> found, or reports it unresolved. Returns the load it started
    /// in the tab; null when it started none.</summary>
    private DecompileTicket? ShowResolved(AppState appState, CodeReference reference, Resolution<BrowseNode> resolved, TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => ShowFound(appState, found.Value, destination),
            _ =>
            {
                ReportUnresolved(reference, null);
                return null;
            },
            failed =>
            {
                ReportUnresolved(reference, failed.Error);
                return null;
            },
            static _ => null);
    }

    private static DecompileTicket? ShowFound(AppState appState, BrowseNode found, TabDestination destination)
    {
        LeaveActiveTab(appState);
        var load = ShowNodeIn(appState, destination, found);
        appState.History.RecordPlace(found, appState.Language);
        return load;
    }

    private void ReportUnresolved(CodeReference reference, Exception? error)
    {
        ports.ReportError(new AppProblem.Unresolved(reference, error));
    }
}
