using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>Opens files and shows the namespace, type or member an id names, as a command line asks.</summary>
public sealed class OpenAndShowIdHandler(OpenAndShowIdHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the files could not be opened, or what the id names is not in them, or may be in a
        /// module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Opens the files at <paramref name="paths"/>, then finds the namespace, type or member
        /// <paramref name="idString"/> names among the assemblies read from them, or among every open one when there
        /// are none; not found when none has it, unreadable when none read has it and one could not be read. Done
        /// without holding up the screen, cancelled with the opens; files that
        /// could not be opened come back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct);
    }

    /// <summary>Opens the files at <paramref name="paths"/>, then shows the namespace, type or member
    /// <paramref name="idString"/> names, written as XML documentation ids are, looked for in the assemblies read
    /// from them, or in every open one when there are none. Returns the load that started in the tab, for the edge
    /// that started this to start under the tab's work; null when none started. Reports when none has it, unless
    /// something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        var ticket = appState.Navigation.BeginRequest();
        var found = await ports.OpenAndFindIdAsync(paths, idString, ct);
        return ShowFound(appState, idString, found, ticket);
    }

    /// <summary>Shows what the id names, when it was found and nothing else was asked for meanwhile, else reports
    /// why not. Returns the load it started in the tab; null when it started none.</summary>
    private DecompileTicket? ShowFound(AppState appState, string idString, Outcome<Lookup<BrowseNode>> found, int ticket)
    {
        return found.Match(
            ok => ShowFoundNode(appState, idString, ok.Value, ticket),
            failed =>
            {
                ReportOpenFailure(failed.Error);
                return null;
            });
    }

    /// <summary>Shows the node <paramref name="lookup"/> found, what the id names, unless something else was asked for
    /// meanwhile; reports when none has it, or the module it may be in could not be read. Returns the load it started
    /// in the tab; null when it started none.</summary>
    private DecompileTicket? ShowFoundNode(AppState appState, string idString, Lookup<BrowseNode> lookup, int ticket)
    {
        if (appState.Navigation.IsStale(ticket))
        {
            return null;
        }

        return lookup.Match<DecompileTicket?>(
            found => appState.Navigation.ShowNode(found.Value, appState.Decompiler.Setup),
            _ =>
            {
                ports.ReportError(new AppProblem.IdNotFound(idString));
                return null;
            },
            unreadable =>
            {
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                return null;
            });
    }

    private void ReportOpenFailure(Exception error)
    {
        var problem = Assemblies.FindOpenProblem(error);
        if (problem is null)
        {
            return;
        }

        ports.ReportError(problem);
    }
}
