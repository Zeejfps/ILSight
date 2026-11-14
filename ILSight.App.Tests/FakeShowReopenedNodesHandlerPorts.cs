using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Looks up whatever places it is asked for when the test says, lets the cancellation of the token it
/// was asked with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeShowReopenedNodesHandlerPorts : ShowReopenedNodesHandler.IPorts
{
    public List<IReadOnlyList<NodePath?>> Asked { get; } = [];

    public List<TaskCompletionSource<List<Outcome<Lookup<BrowseNode>>>>> Answers { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<List<Outcome<Lookup<BrowseNode>>>> LookUpListedPlacesAsync(IReadOnlyList<NodePath?> places, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<List<Outcome<Lookup<BrowseNode>>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Asked.Add(places);
        Answers.Add(answer);
        return answer.Task;
    }
}
