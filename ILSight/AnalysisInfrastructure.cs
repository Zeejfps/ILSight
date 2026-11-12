using System.Runtime.CompilerServices;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Reaches the app's error reports, and runs analyses and finds saved ones in the open assemblies off the UI
/// thread, for <see cref="RunAnalysisHandler"/> and <see cref="RestoreAnalysesHandler"/>. What the work threw comes
/// back as <see cref="Outcome{T}.Failed"/>, except the cancellation of the token the call was given, which it lets
/// through.</summary>
public sealed class AnalysisInfrastructure(ErrorLog errors, AssemblyList assemblies) : RunAnalysisHandler.IPorts, RestoreAnalysesHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Runs the analysis on the thread pool, each batch read there and brought back on the UI thread; the
    /// first read that throws ends the sequence with what it threw.</summary>
    public async IAsyncEnumerable<Outcome<IReadOnlyList<AnalysisSymbol>>> RunAsync(AnalysisSymbol subject, AnalysisKind kind,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var batches = Analyzers.RunAsync(assemblies, subject, kind, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            var next = await ReadNextAsync(batches, ct);
            if (next is null)
            {
                yield break;
            }

            yield return next;
            if (next is Outcome<IReadOnlyList<AnalysisSymbol>>.Failed)
            {
                yield break;
            }
        }
    }

    /// <summary>The next batch of <paramref name="batches"/>, or what reading it threw; null at the end.</summary>
    private static async Task<Outcome<IReadOnlyList<AnalysisSymbol>>?> ReadNextAsync(IAsyncEnumerator<IReadOnlyList<AnalysisSymbol>> batches,
        CancellationToken ct)
    {
        var moved = await Background.Wait(batches.MoveNextAsync().AsTask());
        ct.ThrowIfCancellationRequested();
        return moved.Match<Outcome<IReadOnlyList<AnalysisSymbol>>?>(
            ok => ok.Value ? new Outcome<IReadOnlyList<AnalysisSymbol>>.Ok(batches.Current) : null,
            static failed => new Outcome<IReadOnlyList<AnalysisSymbol>>.Failed(failed.Error));
    }

    /// <summary>Finds them on the thread pool and comes back on the UI thread.</summary>
    public async Task<Outcome<List<AnalysisSymbol?>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        return await Background.Wait(Task.Run(() => FindAnalyzed(analyzed), ct));
    }

    private List<AnalysisSymbol?> FindAnalyzed(IReadOnlyList<CodeAnchor> analyzed)
    {
        return analyzed
            .Select(a => Analyzers.Find(assemblies, a))
            .ToList();
    }
}
