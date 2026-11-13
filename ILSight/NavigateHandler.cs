using ILSight.Core;

namespace ILSight;

/// <summary>Follows a reference: what a reference in code, a forwarded type, an imported method, or a base or
/// derived type leads to.</summary>
public sealed class NavigateHandler(NavigateHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere, or into a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node <paramref name="reference"/> leads to; not found when it leads nowhere, unreadable when
        /// into a module whose type system the library could not read; looked for without
        /// holding up the screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct);
    }

    /// <summary>Resolves <paramref name="reference"/> in the background (it may load an assembly) for the request
    /// <paramref name="ticket"/> began (<see cref="Navigation.BeginResolving"/>, in the frame it was asked in), then
    /// shows it in the tab the ticket leads to, in <paramref name="navigation"/>, decompiled as
    /// <paramref name="decompiler"/> has it once found, and records it in the history. Returns the load that started in
    /// the tab, for the edge that started this to start under the tab's work; null when none started. Reports a
    /// reference that leads nowhere; drops it when something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(Navigation navigation, DecompilerChoice decompiler, CodeReference.Navigable reference, ResolveTicket ticket,
        CancellationToken ct)
    {
        var found = await ports.LookUpReferenceAsync(reference, ct);
        return ShowResolved(navigation, decompiler, reference, navigation.EndResolving(ticket.Intent, found), ticket.Destination);
    }

    /// <summary>Shows what <paramref name="resolved"/> found, or reports it unresolved. Returns the load it started
    /// in the tab; null when it started none.</summary>
    private DecompileTicket? ShowResolved(Navigation navigation, DecompilerChoice decompiler, CodeReference reference, Resolution<BrowseNode> resolved,
        TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => navigation.ShowFoundNode(destination, found.Value, decompiler.Setup),
            _ =>
            {
                ReportUnresolved(reference, null);
                return null;
            },
            unreadable =>
            {
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
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
        ports.ReportError(new AppProblem.Unresolved { Reference = reference, Error = error });
    }
}
