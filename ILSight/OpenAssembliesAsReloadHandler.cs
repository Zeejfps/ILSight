using System.Diagnostics;
using ILSight.Core;
using static ILSight.SavedDocuments;
using static ILSight.Settings;

namespace ILSight;

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

    /// <summary>Opens <paramref name="paths"/> as a reload does, so that another reload or clearing the list
    /// stops it, and the settings are saved once it is done.</summary>
    public async Task HandleAsync(AppState appState, IReadOnlyList<AssemblyPath> paths, CancellationToken ct)
    {
        var ticket = appState.Reload.Start();
        var opened = await ports.OpenFilesAsync(paths, ticket.Token, ct);
        FinishOpeningAsReload(appState, opened, ticket);
    }

    private void FinishOpeningAsReload(AppState appState, Outcome<LoadedAssembly?> outcome, ReloadTicket ticket)
    {
        if (!appState.Reload.TryFinish(ticket))
        {
            return;
        }

        ShowReopenedDocuments(appState);
        switch (outcome)
        {
            case Outcome<LoadedAssembly?>.Ok:
                SaveSettings(appState);
                break;
            case Outcome<LoadedAssembly?>.Failed failed:
                ports.ReportError(new AppProblem.OpenFailed(failed.Error));
                break;
            default:
                throw new UnreachableException();
        }
    }
}
