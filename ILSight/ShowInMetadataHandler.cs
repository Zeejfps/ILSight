using ILSight.Core;
using ILSight.Core.Metadata;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Shows the metadata table row that defines what a reference points at.</summary>
public sealed class ShowInMetadataHandler(ShowInMetadataHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere.</summary>
        void ReportError(AppProblem problem);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background, as <see cref="NavigateHandler"/> does,
    /// then shows the metadata table row that defines it in the tab it was asked from, or a new one when
    /// <paramref name="inNewTab"/>, and records it in the history. Reports a reference that leads nowhere; drops it
    /// when something else was asked for meanwhile.</summary>
    public async Task HandleAsync(AppState appState, CodeReference.Navigable reference, bool inNewTab, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab);
        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var resolved = await appState.Intent.ResolveAsync(() => NodeLookup.FindMetadataRow(assemblies, session, reference), ct);
        ShowResolvedRow(appState, reference, resolved, destination);
    }

    private void ShowResolvedRow(AppState appState, CodeReference reference, Resolution<MetadataTarget> resolved, TabDestination destination)
    {
        resolved.Switch((appState, reference, destination, handler: this),
            static (s, found) => ShowRowIn(s.appState, s.destination, found.Value),
            static (s, _) => s.handler.ReportUnresolved(s.reference, null),
            static (s, failed) => s.handler.ReportUnresolved(s.reference, failed.Error),
            static (_, _) => { });
    }

    private void ReportUnresolved(CodeReference reference, Exception? error)
    {
        ports.ReportError(new AppProblem.Unresolved(reference, error));
    }
}
