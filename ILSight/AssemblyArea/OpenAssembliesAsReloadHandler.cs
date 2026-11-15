using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>Opens files into the list as a reload does, as switching to a list or starting the app does, reaching
/// the files through <see cref="IPorts"/>.</summary>
public sealed class OpenAssembliesAsReloadHandler(OpenAssembliesAsReloadHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Lists the files at <paramref name="paths"/> in the open list, as work of the list's generation
        /// <paramref name="work"/>, in order, those that fail to load as failed, and gives the last one listed, or null
        /// for none. Done without holding up the screen, and stopped between files once <paramref name="ct"/> fires
        /// (the edge links the reload's canceller and the generation's ending into it); a file met once the generation
        /// ended throws the cancellation of its ending; each let through.</summary>
        Task<LoadedAssembly?> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct);
    }

    /// <summary>Opens <paramref name="paths"/> as the reload <paramref name="ticket"/> started (in the frame it was
    /// asked in), as work of <paramref name="work"/>'s generation, the list's when the reopening began, so that another
    /// reload, clearing the list or any end of that generation stops it (the edge links the ticket's canceller and the
    /// generation's ending into <paramref name="ct"/>, asking each), then shows what the tabs reopened from the saved
    /// documents showed. Returns the loads that started in the tabs, for the edge that started this to start.
    /// The frame edge's autosave saves what changed once the reload has ended. A reload stopped before it began reads
    /// none of the files.</summary>
    public async Task<TabLoads> HandleAsync(OpenListSession session, IReadOnlyList<AssemblyPath> paths, ReloadTicket ticket, GenerationWork work,
        CancellationToken ct)
    {
        if (!ticket.IsStopped)
        {
            await ports.OpenFilesAsync(paths, work, ct);
        }

        return FinishOpeningAsReload(session, ticket);
    }

    /// <summary>Ends the reopening <paramref name="ticket"/> was given for when its workflow failed (the edge's settle,
    /// which reports why), unless another reload started since, as a reopening that failed ends. Returns the loads the
    /// tabs reopened from the open list's saved documents started.</summary>
    public static TabLoads FailOpeningAsReload(OpenListSession session, ReloadTicket ticket)
    {
        if (!session.TryFinishReopening(ticket))
        {
            return TabLoads.None;
        }

        return SavedDocuments.ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
    }

    private static TabLoads FinishOpeningAsReload(OpenListSession session, ReloadTicket ticket)
    {
        if (!session.TryFinishReopening(ticket))
        {
            return TabLoads.None;
        }

        return SavedDocuments.ShowReopenedDocuments(session.Navigation, session.Reopened, session.Assemblies);
    }
}
