using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

/// <summary>Reads whatever it is asked to when the test says, lets the cancellation of the token it was asked
/// with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeReopenDiagramHandlerPorts : ReopenDiagramHandler.IPorts
{
    public List<TaskCompletionSource<Outcome<List<Lookup<TypeCard>>>>> Answers { get; } = [];

    public List<IReadOnlyList<TypeAnchor>> Asked { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<List<Lookup<TypeCard>>>> ReadCardsAsync(IReadOnlyList<TypeAnchor> types, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<List<Lookup<TypeCard>>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Asked.Add(types);
        Answers.Add(answer);
        return answer.Task;
    }
}
