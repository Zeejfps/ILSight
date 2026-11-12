using System.Diagnostics;
using ILSight.Core.Processes;

namespace ILSight.ProcessArea;

/// <summary>Reaches the running processes off the UI thread, and the app's error reports, for
/// <see cref="ProcessBrowser"/>. What a listing throws comes back as <see cref="Outcome{T}.Failed"/>, except the
/// cancellation of the token the call was given.</summary>
public sealed class ProcessBrowserAdapter(Action<AppProblem> reportError) : ProcessBrowser.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    public async Task<Outcome<IReadOnlyList<RunningProcess>>> ListProcessesAsync(CancellationToken ct)
    {
        var listed = await Background.Wait(Task.Run(() => RunningProcesses.FindProcessesAsync(ct), ct));
        ct.ThrowIfCancellationRequested();
        return listed;
    }

    /// <summary>Lists on the thread pool, as finding the entry assembly looks for files.</summary>
    public async Task<Outcome<ModuleScan>> ListModulesAsync(RunningProcess process, CancellationToken ct)
    {
        var listed = await Background.Wait(Task.Run(() => ScanModulesAsync(process, ct), ct));
        ct.ThrowIfCancellationRequested();
        return listed;
    }

    private static async Task<ModuleScan> ScanModulesAsync(RunningProcess process, CancellationToken ct)
    {
        var listing = await RunningProcesses.GetModulesAsync(process.Id, ct);
        return listing.Match<ModuleScan>(
            listed => new ModuleScan.Loaded
            {
                Process = process,
                Modules = listed.Modules,
                EntryAssembly = RunningProcesses.FindEntryAssembly(process, listed.Modules),
            },
            unreachable => new ModuleScan.Unreachable
            {
                Process = process,
                Detail = unreachable.Detail,
                EntryAssembly = RunningProcesses.FindEntryAssembly(process, []),
            },
            unreadable => new ModuleScan.Unreadable
            {
                Process = process,
                Detail = unreadable.Detail,
                EntryAssembly = RunningProcesses.FindEntryAssembly(process, []),
            });
    }
}
