using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

/// <summary>Looks up whatever place it is asked for when the test says, lets the cancellation of the token it was
/// asked with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeGoToBookmarkHandlerPorts : GoToBookmarkHandler.IPorts
{
    public List<NodePath> Asked { get; } = [];

    public List<TaskCompletionSource<Outcome<BrowseNode?>>> Answers { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public Task<Outcome<BrowseNode?>> LookUpPlaceAsync(NodePath place, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<BrowseNode?>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Asked.Add(place);
        Answers.Add(answer);
        return answer.Task;
    }
}
