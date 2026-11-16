using System.Runtime.CompilerServices;
using ILSight.Core;
using ILSight.Core.AnalysisArea;

namespace ILSight.App.Tests;

/// <summary>Finds <see cref="Found"/>, one entry at a time, for any analysis; keeps the problems reported.</summary>
internal sealed class FakeRunAnalysisHandlerPorts : RunAnalysisHandler.IPorts
{
    public required IReadOnlyList<Outcome<IReadOnlyList<AnalysisFind>>> Found { get; init; }

    /// <summary>What the analysis throws after what it found, as a bug none of its answers covers; none when
    /// null.</summary>
    public Exception? Throws { get; init; }

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public async IAsyncEnumerable<Outcome<IReadOnlyList<AnalysisFind>>> RunAsync(AnalysisSymbol subject, AnalysisKind kind, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var found in Found)
        {
            await Task.CompletedTask;
            yield return found;
        }

        var throws = Throws;
        if (throws is not null)
        {
            throw throws;
        }
    }
}
