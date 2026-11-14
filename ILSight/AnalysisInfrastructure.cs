using System.Runtime.CompilerServices;
using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Reaches the app's error reports, and runs analyses and finds saved ones in the open assemblies off the UI
/// thread, for <see cref="RunAnalysisHandler"/> and <see cref="RestoreAnalysesHandler"/>. What the library failed with
/// comes back as <see cref="Outcome{T}.Failed"/>, as the core answers it; the cancellation of the token the call was
/// given is let through.</summary>
public sealed class AnalysisInfrastructure(ErrorLog errors, AssemblyList assemblies) : RunAnalysisHandler.IPorts, RestoreAnalysesHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Runs the analysis on the thread pool, each batch read there and brought back on the UI thread; a read
    /// the library failed ends the sequence with <see cref="Outcome{T}.Failed"/>, as the core answers it.</summary>
    public async IAsyncEnumerable<Outcome<IReadOnlyList<AnalysisFind>>> RunAsync(AnalysisSymbol subject, AnalysisKind kind,
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
            if (next is Outcome<IReadOnlyList<AnalysisFind>>.Failed)
            {
                yield break;
            }
        }
    }

    private static async Task<Outcome<IReadOnlyList<AnalysisFind>>?> ReadNextAsync(IAsyncEnumerator<Outcome<IReadOnlyList<AnalysisFind>>> batches,
        CancellationToken ct)
    {
        var moved = await Background.Wait(batches.MoveNextAsync().AsTask());
        ct.ThrowIfCancellationRequested();
        return moved ? batches.Current : null;
    }

    /// <summary>Finds them on the thread pool, each once the read of its assembly's type system ended, and comes back
    /// on the UI thread; the first the library failed to find is the answer's <see cref="Outcome{T}.Failed"/>, as the
    /// core answers it.</summary>
    public async Task<Outcome<List<Lookup<AnalysisSymbol>>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => FindAnalyzedAsync(analyzed, ct), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private async Task<Outcome<List<Lookup<AnalysisSymbol>>>> FindAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        var found = new List<Lookup<AnalysisSymbol>>(analyzed.Count);
        foreach (var anchor in analyzed)
        {
            var lookup = await Analyzers.FindAsync(assemblies, anchor, ct);
            if (lookup is Outcome<Lookup<AnalysisSymbol>>.Failed failed)
            {
                return new Outcome<List<Lookup<AnalysisSymbol>>>.Failed(failed.Error);
            }

            found.Add(((Outcome<Lookup<AnalysisSymbol>>.Ok)lookup).Value);
        }
        return new Outcome<List<Lookup<AnalysisSymbol>>>.Ok(found);
    }
}
