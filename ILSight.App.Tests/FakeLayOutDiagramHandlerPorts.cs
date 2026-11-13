using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.DiagramArea;

namespace ILSight.App.Tests;

/// <summary>Lays out whatever it is asked to when the test says, lets the cancellation of the token it was asked
/// with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeLayOutDiagramHandlerPorts : LayOutDiagramHandler.IPorts
{
    public List<TaskCompletionSource<Outcome<TypeLayout>>> Answers { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<TypeLayout>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Answers.Add(answer);
        return answer.Task;
    }
}
