using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class GoToBookmarkHandlerTests
{
    /// <summary>Looks up whatever place it is asked for when the test says, lets the cancellation of the token it was
    /// asked with through, as the adapter does; keeps what it was asked and the problems reported.</summary>
    private sealed class FakeLookup : GoToBookmarkHandler.IPorts
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

    /// <summary>A driver with no screen that shows the fixture Outline with its "return First();" line bookmarked,
    /// then the fixture Greeter; and the bookmark.</summary>
    private static (AppStateDriver Driver, Bookmark Bookmark) Bookmarked()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Outline"));
        var tab = d.AppState.Tabs.Active;
        Assert.NotNull(tab);
        Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark);
        d.Show(d.Fixture("Greeter"));
        return (d, Assert.Single(d.AppState.Bookmarks.Items));
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItLoadingAndRecordsItInTheHistory()
    {
        var (d, bookmark) = Bookmarked();
        var outline = d.Fixture("Outline");
        var ports = new FakeLookup();
        var history = d.AppState.History.Entries.Count;

        var going = new GoToBookmarkHandler(ports).HandleAsync(d.AppState, bookmark, CancellationToken.None);
        Assert.True(d.AppState.IsResolving);
        Assert.Single(ports.Answers).SetResult(new Outcome<BrowseNode?>.Ok(outline));
        var load = await going;

        Assert.Equal([bookmark.Place], ports.Asked);
        Assert.NotNull(load);
        Assert.Same(outline, load.Node);
        Assert.Same(d.AppState.Tabs.Active, load.Tab);
        Assert.False(d.AppState.IsResolving);
        Assert.Equal(history + 1, d.AppState.History.Entries.Count);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_PlaceGone_ReportsTheBookmarkGone()
    {
        var (d, bookmark) = Bookmarked();
        var ports = new FakeLookup();

        var going = new GoToBookmarkHandler(ports).HandleAsync(d.AppState, bookmark, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<BrowseNode?>.Ok(null));
        var load = await going;

        Assert.Null(load);
        var gone = Assert.IsType<AppProblem.BookmarkGone>(Assert.Single(ports.Reported));
        Assert.Equal(bookmark.Name, gone.Bookmark);
        Assert.Equal(bookmark.Place, gone.Place);
        Assert.Null(gone.Error);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_ShowsAndReportsNothing()
    {
        var (d, bookmark) = Bookmarked();
        var ports = new FakeLookup();
        var shown = d.AppState.Tabs.Active;

        var going = new GoToBookmarkHandler(ports).HandleAsync(d.AppState, bookmark, CancellationToken.None);
        d.AppState.Intent.Begin();
        Assert.Single(ports.Answers).SetResult(new Outcome<BrowseNode?>.Ok(d.Fixture("Outline")));
        var load = await going;

        Assert.Null(load);
        Assert.Same(shown, d.AppState.Tabs.Active);
        Assert.Empty(ports.Reported);
    }
}
