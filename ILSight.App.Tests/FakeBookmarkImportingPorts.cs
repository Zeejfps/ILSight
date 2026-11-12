using ILSight.Core;
using ILSight.Bookmarking;

namespace ILSight.App.Tests;

/// <summary>Reads the file the test gave it, looks up places when the test says, lets the cancellation of the
/// token it was asked with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
internal sealed class FakeBookmarkImportingPorts : BookmarkImporting.IPorts
{
    public required BookmarksRead File { get; init; }

    public List<string> Read { get; } = [];

    public List<TaskCompletionSource<Outcome<List<NodePath?>>>> Answers { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public BookmarksRead ReadBookmarks(string path)
    {
        Read.Add(path);
        return File;
    }

    public Task<Outcome<List<NodePath?>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var answer = new TaskCompletionSource<Outcome<List<NodePath?>>>();
        ct.Register(() => answer.TrySetCanceled(ct));
        Answers.Add(answer);
        return answer.Task;
    }
}
