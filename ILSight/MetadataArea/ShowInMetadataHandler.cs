using ILSight.Core;
using ILSight.Core.MetadataArea;

namespace ILSight.MetadataArea;

/// <summary>Shows the metadata table row that defines what a reference points at.</summary>
public sealed class ShowInMetadataHandler(ShowInMetadataHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user looking for the reference failed, or it leads into a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user what they asked for was refused, and why, where they acted.</summary>
        void ShowRefusal(Refusal refusal);


        /// <summary>The metadata table row that defines what <paramref name="reference"/> points at; not found when
        /// it leads nowhere, unreadable when into a module whose type system the library could not read; looked for
        /// without holding up the screen (it may read an assembly from disk); a lookup
        /// that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<MetadataTarget>>> LookUpMetadataRowAsync(Navigable reference, CancellationToken ct);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background for the request <paramref name="ticket"/>
    /// began, as <see cref="NavigateHandler"/> does, then shows the metadata table row that defines it in the tab the
    /// ticket leads to and records it in the history. Returns the load that started in the tab, for the edge that
    /// started this to start under the tab's work; null when none started. Reports a reference that leads nowhere;
    /// drops it when something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(Navigation navigation, DecompilerChoice decompiler, Navigable reference,
        ResolveTicket ticket, CancellationToken ct)
    {
        var found = await ports.LookUpMetadataRowAsync(reference, ct);
        return ShowResolvedRow(navigation, decompiler, reference, navigation.EndResolving(ticket.Intent, found), ticket.Destination);
    }

    private DecompileTicket? ShowResolvedRow(Navigation navigation, DecompilerChoice decompiler, CodeReference reference,
        Resolution<MetadataTarget> resolved, TabDestination destination)
    {
        switch (resolved)
        {
            case Resolution<MetadataTarget>.Found found:
                return navigation.ShowRowIn(destination, found.Value, decompiler.Setup);
            case Resolution<MetadataTarget>.Missing:
                ports.ShowRefusal(new Refusal.Unresolved { Reference = reference });
                break;
            case Resolution<MetadataTarget>.Unreadable unreadable:
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                break;
            case Resolution<MetadataTarget>.Failed failed:
                ports.ReportError(LookupFailures.OfReference(reference, failed.Error));
                break;
            case Resolution<MetadataTarget>.Stale:
                break;
        }

        return null;
    }
}
