using System.ComponentModel;
using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Processes;

namespace ILSight.ProcessArea;

/// <summary>Reaches the running processes off the UI thread, and the app's error reports, for
/// <see cref="ProcessBrowser"/>. A listing the system refused or that lost its process comes back as
/// <see cref="Outcome{T}.Failed"/>; the cancellation of the token the call was given, and anything else, are let
/// through.</summary>
public sealed class ProcessBrowserAdapter(Action<AppProblem> reportError) : ProcessBrowser.IPorts
{
    public void ReportError(AppProblem problem)
    {
        reportError(problem);
    }

    public Task<Outcome<IReadOnlyList<RunningProcess>>> ListProcessesAsync(CancellationToken ct)
    {
        return CatchAsync(Background.Wait(Task.Run(() => RunningProcesses.FindProcessesAsync(ct), ct)), ct);
    }

    /// <summary>Lists on the thread pool, as finding the entry assembly looks for files.</summary>
    public Task<Outcome<ModuleScan>> ListModulesAsync(RunningProcess process, CancellationToken ct)
    {
        return CatchAsync(Background.Wait(Task.Run(() => ScanModulesAsync(process, ct), ct)), ct);
    }

    // The system refused the listing (Win32Exception, UnauthorizedAccessException), a file it read could not be read
    // (IOException), or its process went meanwhile (InvalidOperationException).
    private static async Task<Outcome<T>> CatchAsync<T>(BackgroundWork<T> listing, CancellationToken ct)
    {
        try
        {
            var listed = await listing;
            ct.ThrowIfCancellationRequested();
            return new Outcome<T>.Ok(listed);
        }
        catch (Win32Exception e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (IOException e)
        {
            return new Outcome<T>.Failed(e);
        }
        catch (InvalidOperationException e)
        {
            return new Outcome<T>.Failed(e);
        }
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
