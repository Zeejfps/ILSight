using ILSight.Core;
using ILSight.Core.ComparisonArea;

namespace ILSight.AssemblyArea;

/// <summary>Reaches the app's error reports, and compares assemblies' APIs off the UI thread, for
/// <see cref="CompareAssembliesHandler"/>. The comparison runs on the thread pool and comes back on the UI thread, a
/// read the library failed as <see cref="Outcome{T}.Failed"/>, as the core answers it; the cancellation of the token the
/// call was given is let through.</summary>
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
        var compared = await Background.Wait(Task.Run(() => ApiEntries.CompareAsync(left, right, scope, ct), ct));
        ct.ThrowIfCancellationRequested();
        return compared switch
        {
            Outcome<TypeSystemWork<ApiDiff>>.Ok ok => ok.Value.Match<Outcome<ModuleRead<ApiDiff>>>(
                static done => new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Read { Value = done.Value }),
                static unreadable => new Outcome<ModuleRead<ApiDiff>>.Failed(unreadable.Module.Why),
                static closed => new Outcome<ModuleRead<ApiDiff>>.Ok(new ModuleRead<ApiDiff>.Closed { Module = closed.Module })),
            Outcome<TypeSystemWork<ApiDiff>>.Failed failed => new Outcome<ModuleRead<ApiDiff>>.Failed(failed.Error),
        };
    }
}
