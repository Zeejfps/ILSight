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

    /// <summary>The next batch of <paramref name="batches"/>, or what reading it threw; null at the end.</summary>
    private static async Task<Outcome<IReadOnlyList<AnalysisFind>>?> ReadNextAsync(IAsyncEnumerator<IReadOnlyList<AnalysisFind>> batches,
        CancellationToken ct)
    {
        var moved = await UntrustedReads.CatchAsync(Background.Wait(batches.MoveNextAsync().AsTask()), ct);
        ct.ThrowIfCancellationRequested();
        return moved.Match<Outcome<IReadOnlyList<AnalysisFind>>?>(
            ok => ok.Value ? new Outcome<IReadOnlyList<AnalysisFind>>.Ok(batches.Current) : null,
            static failed => new Outcome<IReadOnlyList<AnalysisFind>>.Failed(failed.Error));
    }

    /// <summary>Finds them on the thread pool, each once the read of its assembly's type system ended, and comes back
    /// on the UI thread.</summary>
    public Task<Outcome<List<Lookup<AnalysisSymbol>>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => FindAnalyzedAsync(analyzed, ct), ct)), ct);
    }

    private async Task<List<Lookup<AnalysisSymbol>>> FindAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct)
    {
        var found = new List<Lookup<AnalysisSymbol>>(analyzed.Count);
        foreach (var anchor in analyzed)
        {
            found.Add(await Analyzers.FindAsync(assemblies, anchor, ct));
        }
        return found;
    }
}
