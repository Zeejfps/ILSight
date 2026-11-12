using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.AssemblyArea;

/// <summary>Reaches the app's error reports, and compares assemblies' APIs off the UI thread, for
/// <see cref="CompareAssembliesHandler"/>. The comparison runs on the thread pool and comes back on the UI thread, with
/// whatever it threw as <see cref="Outcome{T}.Failed"/>, except the cancellation of the token the call was given, which
/// it lets through.</summary>
public sealed class CompareAssembliesHandlerAdapter(ErrorLog errors) : CompareAssembliesHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Compares on the thread pool once the reads of both assemblies' type systems ended, and comes back on the
    /// UI thread; an assembly the library could not read fails the comparison, saying why.</summary>
    public async Task<Outcome<ApiDiff>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope, CancellationToken ct)
    {
        var compared = await Background.Wait(Task.Run(() => ApiEntries.CompareAsync(left, right, scope, ct), ct));
        ct.ThrowIfCancellationRequested();
        return Outcome<ApiDiff>.FromTypeSystemWork(compared);
    }
}
