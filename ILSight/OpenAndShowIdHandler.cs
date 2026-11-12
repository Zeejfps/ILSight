using System.Diagnostics;
using ILSight.Core;
using ILSight.AssemblyHandling;

namespace ILSight;

/// <summary>Opens files and shows the namespace, type or member an id names, as a command line asks.</summary>
public sealed class OpenAndShowIdHandler(OpenAndShowIdHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the files could not be opened, or what the id names is not in them.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Opens the files at <paramref name="paths"/>, then finds the namespace, type or member
        /// <paramref name="idString"/> names among the assemblies read from them, or among every open one when there
        /// are none; null when none has it. Done without holding up the screen, cancelled with the opens; files that
        /// could not be opened come back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<BrowseNode?>> OpenAndFindIdAsync(IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct);
    }

    /// <summary>Opens the files at <paramref name="paths"/>, then shows the namespace, type or member
    /// <paramref name="idString"/> names, written as XML documentation ids are, looked for in the assemblies read
    /// from them, or in every open one when there are none. Returns the load that started in the tab, for the edge
    /// that started this to start under the tab's work; null when none started. Reports when none has it, unless
    /// something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, IReadOnlyList<AssemblyPath> paths, string idString, CancellationToken ct)
    {
        var ticket = appState.Intent.Begin();
        var found = await ports.OpenAndFindIdAsync(paths, idString, ct);
        return ShowFound(appState, idString, found, ticket);
    }

    /// <summary>Shows what the id names, when it was found and nothing else was asked for meanwhile, else reports
    /// why not. Returns the load it started in the tab; null when it started none.</summary>
    private DecompileTicket? ShowFound(AppState appState, string idString, Outcome<BrowseNode?> found, int ticket)
    {
        switch (found)
        {
            case Outcome<BrowseNode?>.Ok ok when ok.Value is not null:
                if (!appState.Intent.IsStale(ticket))
                {
                    return appState.Navigation.ShowNode(ok.Value, appState.Language, appState.DecompilerOptions, appState.PreviewTabs);
                }

                break;
            case Outcome<BrowseNode?>.Ok:
                if (!appState.Intent.IsStale(ticket))
                {
                    ports.ReportError(new AppProblem.IdNotFound(idString));
                }

                break;
            case Outcome<BrowseNode?>.Failed failed:
                ReportOpenFailure(failed.Error);
                break;
            default:
                throw new UnreachableException();
        }

        return null;
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
