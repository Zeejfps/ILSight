using System.Diagnostics;
using ILSight.Core;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Opening assemblies into the list: files the user picked or dropped, and the assemblies one references.
/// The workflows return once what they started is done, and reach the files and the user's error reports through
/// <see cref="IPorts"/>.</summary>
public sealed class AssemblyOpening(AssemblyOpening.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user something went wrong.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Whether there is a file, rather than a folder or nothing, at <paramref name="path"/>.</summary>
        bool IsFile(string path);

        /// <summary>Lists the files at <paramref name="paths"/> in the open list, in order, those that fail to load
        /// as failed, and gives the last one listed, or null for none. Done without holding up the screen, and
        /// stopped between files once <paramref name="stop"/> fires; what that threw comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<LoadedAssembly?>> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, CancellationToken stop, CancellationToken ct);

        /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found in the open list,
        /// as <see cref="OpenFilesAsync"/> lists files.</summary>
        Task<Outcome<List<LoadedModule>>> OpenReferencesAsync(LoadedModule assembly, CancellationToken stop, CancellationToken ct);
    }

    /// <summary>Opens the files, then shows the last one opened, unless something else was asked for
    /// meanwhile. Reports why opening failed, unless the opens were cancelled.</summary>
    public async Task OpenAssembliesAsync(AppState appState, IEnumerable<AssemblyPath> paths, CancellationToken ct)
    {
        var files = paths.ToArray();
        if (files.Length == 0)
        {
            return;
        }

        var ticket = appState.Intent.Begin();
        var opened = await ports.OpenFilesAsync(files, appState.Opens.Token, ct);
        ShowOpened(appState, opened, ticket);
    }

    private void ShowOpened(AppState appState, Outcome<LoadedAssembly?> outcome, int ticket)
    {
        switch (outcome)
        {
            case Outcome<LoadedAssembly?>.Ok ok when ok.Value is not null:
                if (!appState.Intent.IsStale(ticket))
                {
                    ShowNode(appState, BrowseSessions.EnsureRoot(appState.Session, ok.Value));
                }

                break;
            case Outcome<LoadedAssembly?>.Ok:
                break;
            case Outcome<LoadedAssembly?>.Failed failed:
                ReportOpenFailure(failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Reports why opening failed, unless the opening was cancelled.</summary>
    private void ReportOpenFailure(Exception error)
    {
        var problem = Assemblies.FindOpenProblem(error);
        if (problem is null)
        {
            return;
        }

        ports.ReportError(problem);
    }

    /// <summary>Opens the file at <paramref name="path"/>, as a file dialog or a download names it; nothing
    /// when it is no absolute path.</summary>
    public Task OpenAssemblyAsync(AppState appState, string path, CancellationToken ct)
    {
        return OpenFilesAtAsync(appState, [path], ct);
    }

    /// <summary>Opens the files among <paramref name="paths"/>, dropped on the window, as
    /// <see cref="OpenAssembliesAsync"/> does; folders are left out.</summary>
    public Task OpenDroppedFilesAsync(AppState appState, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var files = paths
            .Where(ports.IsFile)
            .ToList();
        return OpenFilesAtAsync(appState, files, ct);
    }

    private Task OpenFilesAtAsync(AppState appState, IEnumerable<string> paths, CancellationToken ct)
    {
        var files = paths
            .Select(AssemblyPath.Parse)
            .OfType<AssemblyPath>();
        return OpenAssembliesAsync(appState, files, ct);
    }

    /// <summary>Opens the assemblies <paramref name="assembly"/> references that can be found, cancelled with
    /// <paramref name="opens"/> under way now. Reports why opening failed, unless they were cancelled.</summary>
    public async Task OpenDependenciesAsync(OpenRequests opens, LoadedModule assembly, CancellationToken ct)
    {
        var opened = await ports.OpenReferencesAsync(assembly, opens.Token, ct);
        if (opened is Outcome<List<LoadedModule>>.Failed failed)
        {
            ReportOpenFailure(failed.Error);
        }
    }
}
