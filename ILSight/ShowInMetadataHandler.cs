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

        /// <summary>The metadata table row that defines what <paramref name="reference"/> points at, null when it
        /// leads nowhere, looked for without holding up the screen (it may read an assembly from disk); a lookup
        /// that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<MetadataTarget?>> LookUpMetadataRowAsync(CodeReference.Navigable reference, CancellationToken ct);

        /// <summary>Brings in what <paramref name="load"/> started loading in its tab and shows it there, commanding
        /// the view through <paramref name="viewCommands"/> where to stand in it.</summary>
        Task DecompileIntoTabAsync(Queue<ViewCommand> viewCommands, DecompileTicket load, CancellationToken ct);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background, as <see cref="NavigateHandler"/> does,
    /// then shows the metadata table row that defines it in the tab it was asked from, or a new one when
    /// <paramref name="inNewTab"/>, and records it in the history, returning once its table is read there. Reports a
    /// reference that leads nowhere; drops it when something else was asked for meanwhile.</summary>
    public async Task HandleAsync(AppState appState, CodeReference.Navigable reference, bool inNewTab, CancellationToken ct)
    {
        var destination = ChooseDestination(appState, inNewTab);
        var ticket = appState.Intent.BeginResolving();
        var found = await ports.LookUpMetadataRowAsync(reference, ct);
        var load = ShowResolvedRow(appState, reference, appState.Intent.EndResolving(ticket, found), destination);
        if (load is null)
        {
            return;
        }

        await ports.DecompileIntoTabAsync(appState.ViewCommands, load, ct);
    }

    /// <summary>Shows the row <paramref name="resolved"/> found, or reports it unresolved. Returns the load it
    /// started in the tab; null when it started none.</summary>
    private DecompileTicket? ShowResolvedRow(AppState appState, CodeReference reference, Resolution<MetadataTarget> resolved, TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => ShowRowIn(appState, destination, found.Value),
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
