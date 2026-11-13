using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.AssemblyArea;

/// <summary>Reaches the app's error reports, and compares assemblies' APIs off the UI thread, for
/// <see cref="CompareAssembliesHandler"/>. The comparison runs on the thread pool and comes back on the UI thread, with
/// what the read failed with (<see cref="UntrustedReads"/>) as <see cref="Outcome{T}.Failed"/>, except the cancellation of the token the call was given, which
/// it lets through.</summary>
public sealed class CompareAssembliesHandlerAdapter(ErrorLog errors) : CompareAssembliesHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Compares on the thread pool once the reads of both assemblies' type systems ended, and comes back on the
    /// UI thread; an assembly the library could not read fails the comparison, saying why; one closed before it began
    /// says so.</summary>
    public async Task<Outcome<ModuleRead<ApiDiff>>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope, CancellationToken ct)
    {
        var compared = await UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => ApiEntries.CompareAsync(left, right, scope, ct), ct)), ct);
        ct.ThrowIfCancellationRequested();
        return compared.Match(
            static ok => ok.Value.Match<Outcome<ModuleRead<ApiDiff>>>(
                static done => new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Read { Value = done.Value }),
                static unreadable => new Outcome<ModuleRead<ApiDiff>>.Failed(unreadable.Module.Why),
                static closed => new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Closed { Module = closed.Module })),
            static failed => new Outcome<ModuleRead<ApiDiff>>.Failed(failed.Error));
    }
}
