using ILSight.Core;
using static ILSight.SavedDocuments;

namespace ILSight.AssemblyArea;

/// <summary>Opens files into the list as a reload does, as switching to a list or starting the app does, reaching
/// the files and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class OpenAssembliesAsReloadHandler(OpenAssembliesAsReloadHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, and gives the last one listed, or null for none. Done without holding up the screen, and
        /// stopped between files once <paramref name="stop"/> fires; what that threw comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct);
    }

    /// <summary>Opens <paramref name="paths"/> as the reload <paramref name="ticket"/> started (in the frame it was
    /// asked in), so that another reload or clearing the list stops it, then shows what the tabs reopened from the
    /// saved documents showed. Returns the loads that started in the tabs, for the edge that started this to start.
    /// The frame edge's autosave saves what changed once the reload has ended. A reload stopped before it began reads
    /// none of the files.</summary>
    public async Task<TabLoads> HandleAsync(OpenListSession session, IReadOnlyList<AssemblyPath> paths, ReloadTicket ticket, CancellationToken ct)
    {
        var opened = await OpenUnlessStoppedAsync(paths, ticket, ct);
        return FinishOpeningAsReload(session, opened, ticket);
    }

    /// <summary>What listing <paramref name="paths"/> came to; nothing listed, nothing read, when <paramref name="ticket"/>
    /// was stopped before it began.</summary>
    private Task<Outcome<LoadedAssembly?>> OpenUnlessStoppedAsync(IReadOnlyList<AssemblyPath> paths, ReloadTicket ticket, CancellationToken ct)
    {
        if (ticket.IsStopped)
        {
            return Task.FromResult<Outcome<LoadedAssembly?>>(new Outcome<LoadedAssembly?>.Ok(null));
        }

        return ports.OpenFilesAsync(paths, ticket.Token, ct);
    }

    private TabLoads FinishOpeningAsReload(OpenListSession session, Outcome<LoadedAssembly?> outcome, ReloadTicket ticket)
    {
        if (!session.TryFinishReopening(ticket))
        {
            return TabLoads.None;
        }

        var reopened = ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
        if (outcome is Outcome<LoadedAssembly?>.Failed failed)
        {
            ports.ReportError(new AppProblem.OpenFailed(failed.Error));
        }

        return reopened;
    }
}
