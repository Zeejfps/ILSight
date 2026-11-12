using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.Diagramming;

namespace ILSight.App.Tests;

/// <summary>Reads whatever it is asked to when the test says, lets the cancellation of the token it was asked
/// with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeDiagramGrowingPorts : DiagramGrowing.IPorts
{
    public List<CardReadRequest> Requests { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<List<TypeCard>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct)
    {
        return AskAsync(nameof(ReadWithNeighboursAsync), [type], ct);
    }

    public Task<Outcome<List<TypeCard>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        return AskAsync(nameof(ReadCardsAsync), types, ct);
    }

    public Task<Outcome<List<TypeCard>>> ReadDerivedAsync(CodeReference.Entity type, CancellationToken ct)
    {
        return AskAsync(nameof(ReadDerivedAsync), [type], ct);
    }

    private Task<Outcome<List<TypeCard>>> AskAsync(string read, IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<List<TypeCard>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Requests.Add(new CardReadRequest { Read = read, Types = types.ToList(), Answer = answer });
        return answer.Task;
    }
}
