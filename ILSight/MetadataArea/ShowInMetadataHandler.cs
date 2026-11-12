using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataArea;

/// <summary>Shows the metadata table row that defines what a reference points at.</summary>
public sealed class ShowInMetadataHandler(ShowInMetadataHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The metadata table row that defines what <paramref name="reference"/> points at, null when it
        /// leads nowhere, looked for without holding up the screen (it may read an assembly from disk); a lookup
        /// that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<MetadataTarget?>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background for the request <paramref name="ticket"/>
    /// began, as <see cref="NavigateHandler"/> does, then shows the metadata table row that defines it in the tab the
    /// ticket leads to and records it in the history. Returns the load that started in the tab, for the edge that
    /// started this to start under the tab's work; null when none started. Reports a reference that leads nowhere;
    /// drops it when something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, CodeReference.Navigable reference, ResolveTicket ticket, CancellationToken ct)
    {
        var found = await ports.LookUpMetadataRowAsync(reference, ct);
        return ShowResolvedRow(appState, reference, appState.Navigation.EndResolving(ticket.Intent, found), ticket.Destination);
    }

    /// <summary>Shows the row <paramref name="resolved"/> found, or reports it unresolved. Returns the load it
    /// started in the tab; null when it started none.</summary>
    private DecompileTicket? ShowResolvedRow(AppState appState, CodeReference reference, Resolution<MetadataTarget> resolved, TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => appState.Navigation.ShowRowIn(destination, found.Value, appState.Decompiler),
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

    private void ReportUnresolved(CodeReference reference, Exception? error)
    {
        ports.ReportError(new AppProblem.Unresolved(reference, error));
    }
}
