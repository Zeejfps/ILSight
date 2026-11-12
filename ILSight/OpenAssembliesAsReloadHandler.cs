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
    /// stops it, then shows what the tabs reopened from the saved documents showed, and the settings are saved once
    /// the files are open. Returns the loads that started in the tabs, for the edge that started this to start.</summary>
    public async Task<TabLoads> HandleAsync(AppState appState, IReadOnlyList<AssemblyPath> paths, CancellationToken ct)
    {
        var ticket = appState.Reload.Start();
        var opened = await ports.OpenFilesAsync(paths, ticket.Token, ct);
        return FinishOpeningAsReload(appState, opened, ticket);
    }

    private TabLoads FinishOpeningAsReload(AppState appState, Outcome<LoadedAssembly?> outcome, ReloadTicket ticket)
    {
        if (!appState.Reload.TryFinish(ticket))
        {
            return TabLoads.None;
        }

        var reopened = ShowReopenedDocuments(appState.Tabs, appState.Reopened, appState.Assemblies);
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

        return reopened;
    }
}
