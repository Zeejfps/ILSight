using ILSight.Bookmarking;

namespace ILSight.App.Tests;

/// <summary>Writes nothing, answering as the test says; keeps what it was asked to write and the problems
/// reported.</summary>
internal sealed class FakeExportBookmarksHandlerPorts : ExportBookmarksHandler.IPorts
{
    public required BookmarksWriteResult Answer { get; init; }

    public List<(string Path, List<BookmarkRecord> Records)> Written { get; } = [];

    public List<AppProblem> Reported { get; } = [];

    public void ReportError(AppProblem problem)
    {
        Reported.Add(problem);
    }

    public BookmarksWriteResult WriteBookmarks(string path, List<BookmarkRecord> records)
    {
        Written.Add((path, records));
        return Answer;
    }
}
