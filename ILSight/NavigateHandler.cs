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
    /// history. Reports a reference that leads nowhere; drops it when something else was asked for meanwhile.</summary>
    public async Task HandleAsync(AppState appState, CodeReference.Navigable reference, bool inNewTab, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab);
        var ticket = appState.Intent.BeginResolving();
        var found = await ports.LookUpReferenceAsync(reference, ct);
        ShowResolved(appState, reference, appState.Intent.EndResolving(ticket, found), destination);
    }

    private void ShowResolved(AppState appState, CodeReference reference, Resolution<BrowseNode> resolved, TabDestination destination)
    {
        resolved.Switch((appState, reference, destination, handler: this),
            static (s, found) =>
            {
                LeaveActiveTab(s.appState);
                ShowNodeIn(s.appState, s.destination, found.Value);
                s.appState.History.RecordPlace(found.Value, s.appState.Language);
            },
            static (s, _) => s.handler.ReportUnresolved(s.reference, null),
            static (s, failed) => s.handler.ReportUnresolved(s.reference, failed.Error),
            static (_, _) => { });
    }

    private void ReportUnresolved(CodeReference reference, Exception? error)
    {
        ports.ReportError(new AppProblem.Unresolved(reference, error));
    }
}
