using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class RunSearchHandlerTests
{
    private static readonly Outcome<IReadOnlyList<SearchResult>> Failure =
        new Outcome<IReadOnlyList<SearchResult>>.Failed(new InvalidDataException("unreadable"));

    /// <summary>A run of <paramref name="search"/> asked for and taken, as the edge takes it to start it.</summary>
    private static SearchRun AskRun(Search search)
    {
        search.Runs.Ask(new SearchRun { Query = SearchQueries.ParseQuery("Greet", SearchKinds.TypesAndMembers), Within = [] });
        return Assert.Single(search.Runs.TakeAsked());
    }

    [Fact]
    public async Task HandleAsync_Ended_LeavesTheSearchIdle()
    {
        var search = new Search();
        var run = AskRun(search);
        var ports = new FakeRunSearchHandlerPorts { Found = [new Outcome<IReadOnlyList<SearchResult>>.Ok([])] };

        await new RunSearchHandler(ports).HandleAsync(search, run, CancellationToken.None);

        Assert.False(search.IsBusy);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_Failed_ReportsItAndEnds()
    {
        var search = new Search();
        var run = AskRun(search);
        var ports = new FakeRunSearchHandlerPorts { Found = [Failure] };

        await new RunSearchHandler(ports).HandleAsync(search, run, CancellationToken.None);

        Assert.IsType<AppProblem.SearchFailed>(Assert.Single(ports.Reported));
        Assert.False(search.IsBusy);
    }

    [Fact]
    public async Task HandleAsync_FailedAfterANewQueryReplacedIt_ReportsNothing()
    {
        var search = new Search();
        var replaced = AskRun(search);
        search.Runs.CancelAll();
        var current = AskRun(search);
        var ports = new FakeRunSearchHandlerPorts { Found = [Failure] };

        await new RunSearchHandler(ports).HandleAsync(search, replaced, CancellationToken.None);

        Assert.Empty(ports.Reported);
        Assert.True(replaced.Work.IsCancellationRequested);
        Assert.True(search.Runs.IsCurrent(current));
    }
}
