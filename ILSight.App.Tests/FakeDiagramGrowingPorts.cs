using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

/// <summary>Reads whatever it is asked to when the test says, lets the cancellation of the token it was asked
/// with through, as the adapter does; keeps what it was asked and the problems reported. The cards the test answers
/// with were all read: none is in a module that could not be read.</summary>
internal sealed class FakeDiagramGrowingPorts : DiagramGrowing.IPorts
{
    public List<CardReadRequest> Requests { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public async Task<Outcome<TypeSystemWork<List<TypeCard>>>> ReadWithNeighboursAsync(CodeReference.Entity type, CancellationToken ct)
    {
        var read = await AskAsync(nameof(ReadWithNeighboursAsync), [type], ct);
        return read.Match<Outcome<TypeSystemWork<List<TypeCard>>>>(
            static ok => new Outcome<TypeSystemWork<List<TypeCard>>>.Ok(new TypeSystemWork<List<TypeCard>>.Done { Value = ok.Value }),
            static failed => new Outcome<TypeSystemWork<List<TypeCard>>>.Failed(failed.Error));
    }

    public async Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<CodeReference.Entity> types, CancellationToken ct)
    {
        var read = await AskAsync(nameof(ReadCardsAsync), types, ct);
        return read.Match<Outcome<List<Lookup<TypeCard>>>>(
            static ok => new Outcome<List<Lookup<TypeCard>>>.Ok(ok.Value.ConvertAll(static c => (Lookup<TypeCard>)new Lookup<TypeCard>.Found { Value = c })),
            static failed => new Outcome<List<Lookup<TypeCard>>>.Failed(failed.Error));
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
