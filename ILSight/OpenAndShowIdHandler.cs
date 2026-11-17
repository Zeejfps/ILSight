using ILSight.Core;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>Opens files and shows the namespace, type or member an id names, as a command line asks.</summary>
public sealed class OpenAndShowIdHandler(OpenAndShowIdHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the files could not be opened, or what the id names may be in a module that could
        /// not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user what they asked for was refused, and why, where they acted.</summary>
        void ShowRefusal(Refusal refusal);


        /// <summary>Opens the files at <paramref name="paths"/>, then finds the namespace, type or member
        /// <paramref name="idString"/> names among the assemblies read from them, or among every open one when there
        /// are none; not found when none has it, unreadable when none read has it and one could not be read, and
        /// what the library threw looking as <see cref="Outcome{T}.Failed"/>. Opens as <paramref name="work"/> of the
        /// list's generation it began in. Done without holding up the screen; cancelled with the opens, whose canceller
        /// the edge links into <paramref name="ct"/>, that cancellation let through.</summary>
        Task<Outcome<Lookup<BrowseNode>>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, GenerationWork work, CancellationToken ct);
    }

    /// <summary>Opens the files at <paramref name="paths"/>, then shows the namespace, type or member
    /// <paramref name="idString"/> names, written as XML documentation ids are, looked for in the assemblies read
    /// from them, or in every open one when there are none, opening as <paramref name="work"/> of the list's generation
    /// the edge started it in. Returns the load that started in the tab, for the edge
    /// that started this to start under the tab's work; null when none started. Reports when none has it, unless
    /// something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(Navigation navigation, DecompilerChoice decompiler, IReadOnlyList<AssemblyPath> paths,
        string idString, GenerationWork work, CancellationToken ct)
    {
        var ticket = navigation.BeginRequest();
        var found = await ports.OpenAndFindIdAsync(paths, idString, work, ct);
        return ShowFound(navigation, decompiler, idString, found, ticket);
    }

    private DecompileTicket? ShowFound(Navigation navigation, DecompilerChoice decompiler, string idString, Outcome<Lookup<BrowseNode>> found, int ticket)
    {
        return found switch
        {
            Outcome<Lookup<BrowseNode>>.Ok ok => ShowFoundNode(navigation, decompiler, idString, ok.Value, ticket),
            Outcome<Lookup<BrowseNode>>.Failed failed => ReportOpenFailed(failed.Error),
        };
    }

    private DecompileTicket? ReportOpenFailed(Exception error)
    {
        ports.ReportError(new AppProblem.OpenFailed(error));
        return null;
    }

    private DecompileTicket? ShowFoundNode(Navigation navigation, DecompilerChoice decompiler, string idString, Lookup<BrowseNode> lookup, int ticket)
    {
        if (navigation.IsStale(ticket))
        {
            return null;
        }

        switch (lookup)
        {
            case Lookup<BrowseNode>.Found found:
                return navigation.ShowNode(found.Value, decompiler.Setup);
            case Lookup<BrowseNode>.NotFound:
                ports.ShowRefusal(new Refusal.IdNotFound { Id = idString });
                break;
            case Lookup<BrowseNode>.Unreadable unreadable:
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                break;
        }

        return null;
    }
}
