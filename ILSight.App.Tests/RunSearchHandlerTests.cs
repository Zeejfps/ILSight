using ILSight.Core;
using ILSight.SearchArea;

namespace ILSight.App.Tests;

public sealed class RunSearchHandlerTests
{
    private static readonly AssemblyList NoAssemblies = new() { Runtime = TestRuntime.Installed };

    private static SearchRun AskRun(SearchResults results, string query = "Greet", TimeSpan typedAt = default)
    {
        results.Update(query, SearchKinds.TypesAndMembers, NoAssemblies, typedAt, shown: true);
        // A second later, what was typed has settled.
        results.Update(query, SearchKinds.TypesAndMembers, NoAssemblies, typedAt + TimeSpan.FromSeconds(1), shown: true);
        return Assert.Single(results.TakeAskedRuns());
    }

    [Fact]
    public async Task HandleAsync_Ended_LeavesTheSearchIdle()
    {
        var results = new SearchResults();
        var run = AskRun(results);
        var ports = new FakeRunSearchHandlerPorts { Found = [[]] };

        await new RunSearchHandler(ports).HandleAsync(results, run, CancellationToken.None);

        Assert.False(results.IsBusy);
        Assert.Null(results.RunFailure);
    }

    [Fact]
    public async Task HandleAsync_SearchThrows_LetsItThroughToTheEdge()
    {
        var results = new SearchResults();
        var run = AskRun(results);
        var bug = new InvalidOperationException("A bug in the search.");
        var ports = new FakeRunSearchHandlerPorts { Found = [], Throws = bug };

        Assert.Same(bug, await Assert.ThrowsAsync<InvalidOperationException>(() => new RunSearchHandler(ports).HandleAsync(results, run, CancellationToken.None)));
    }

    [Fact]
    public void FailRun_ShowsTheSearchFailedSayingWhyUntilANewQueryReplacesIt()
    {
        var results = new SearchResults();
        var run = AskRun(results);

        results.FailRun(run, new InvalidDataException("unreadable"));
        var failed = (results.IsBusy, results.RunFailure);
        var current = AskRun(results, "Hello", typedAt: TimeSpan.FromSeconds(2));

        Assert.Equal((false, "unreadable"), failed);
        Assert.Null(results.RunFailure);
        Assert.True(results.IsCurrent(current));
    }

    [Fact]
    public void FailRun_AfterANewQueryReplacedIt_ShowsNothing()
    {
        var results = new SearchResults();
        var replaced = AskRun(results);
        var current = AskRun(results, "Hello", typedAt: TimeSpan.FromSeconds(2));

        results.FailRun(replaced, new InvalidDataException("unreadable"));

        Assert.Null(results.RunFailure);
        Assert.True(results.IsCurrent(current));
    }
}
