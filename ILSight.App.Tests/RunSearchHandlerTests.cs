using ILSight.Core;
using ILSight.SearchArea;

namespace ILSight.App.Tests;

public sealed class RunSearchHandlerTests
{
    private static readonly Outcome<IReadOnlyList<SearchFind>> Failure =
        new Outcome<IReadOnlyList<SearchFind>>.Failed(new InvalidDataException("unreadable"));

    private static readonly AssemblyList NoAssemblies = new() { Runtime = TestRuntime.Installed };

    private static SearchRun AskRun(SearchResults results, string query = "Greet", double typedAt = 0)
    {
        results.Update(query, SearchKinds.TypesAndMembers, NoAssemblies, typedAt, shown: true);
        // A second later, what was typed has settled.
        results.Update(query, SearchKinds.TypesAndMembers, NoAssemblies, typedAt + 1, shown: true);
        return Assert.Single(results.TakeAskedRuns());
    }

    [Fact]
    public async Task HandleAsync_Ended_LeavesTheSearchIdle()
    {
        var results = new SearchResults();
        var run = AskRun(results);
        var ports = new FakeRunSearchHandlerPorts { Found = [new Outcome<IReadOnlyList<SearchFind>>.Ok([])] };

        await new RunSearchHandler(ports).HandleAsync(results, run, CancellationToken.None);

        Assert.False(results.IsBusy);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_Failed_ReportsItAndEnds()
    {
        var results = new SearchResults();
        var run = AskRun(results);
        var ports = new FakeRunSearchHandlerPorts { Found = [Failure] };

        await new RunSearchHandler(ports).HandleAsync(results, run, CancellationToken.None);

        Assert.IsType<AppProblem.SearchFailed>(Assert.Single(ports.Reported));
        Assert.False(results.IsBusy);
    }

    [Fact]
    public async Task HandleAsync_FailedAfterANewQueryReplacedIt_ReportsNothing()
    {
        var results = new SearchResults();
        var replaced = AskRun(results);
        var current = AskRun(results, "Hello", typedAt: 2);
        var ports = new FakeRunSearchHandlerPorts { Found = [Failure] };

        await new RunSearchHandler(ports).HandleAsync(results, replaced, CancellationToken.None);

        Assert.Empty(ports.Reported);
        Assert.True(replaced.Work.IsCancellationRequested);
        Assert.True(results.IsCurrent(current));
    }
}
