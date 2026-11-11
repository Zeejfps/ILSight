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
    }

    /// <summary>Resolves <paramref name="reference"/> in the background (it may load an assembly), then shows
    /// it in the tab it was asked from, or a new one when <paramref name="inNewTab"/>, and records it in the
    /// history. Reports a reference that leads nowhere; drops it when something else was asked for meanwhile.</summary>
    public async Task HandleAsync(AppState appState, CodeReference.Navigable reference, bool inNewTab, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab);
        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var resolved = await appState.Intent.ResolveAsync(() => NodeLookup.Find(assemblies, session, reference), ct);
        ShowResolved(appState, reference, resolved, destination);
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
