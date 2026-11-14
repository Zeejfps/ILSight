using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

public sealed class GoToBookmarkHandlerTests
{
    private static async Task<(AppStateDriver Driver, Bookmark Bookmark)> BookmarkedAsync()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(await d.FixtureAsync("Outline"));
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<BookmarkToggle.Added>(Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark));
        d.Show(await d.FixtureAsync("Greeter"));
        return (d, Assert.Single(d.AppState.Bookmarks.Items));
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItLoadingAndRecordsItInTheHistory()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var outline = await d.FixtureAsync("Outline");
        var ports = new FakeGoToBookmarkHandlerPorts();
        var history = d.AppState.ListSession.Navigation.HistoryEntries.Count;

        var going = d.BeginAsync(() => new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        Assert.True(d.AppState.ListSession.Navigation.IsResolving);
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(outline)));
        var load = await d.EndAsync(going);

        Assert.Equal([bookmark.Place], ports.Asked);
        Assert.NotNull(load);
        Assert.Same(outline, load.Node);
        Assert.Same(d.AppState.ListSession.Navigation.ActiveTab, load.Tab);
        Assert.False(d.AppState.ListSession.Navigation.IsResolving);
        Assert.Equal(history + 1, d.AppState.ListSession.Navigation.HistoryEntries.Count);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_PlaceGone_SaysTheBookmarkGoneOnTheRefusalLine()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var ports = new FakeGoToBookmarkHandlerPorts();

        var going = d.BeginAsync(() => new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)));
        var load = await d.EndAsync(going);

        Assert.Null(load);
        var gone = Assert.IsType<Refusal.BookmarkGone>(Assert.Single(ports.Refused));
        Assert.Equal(bookmark.Name, gone.Bookmark);
        Assert.Equal(bookmark.Place, gone.Place);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsTheLookupsFailureAsEveryPlaceLookupDoes()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var ports = new FakeGoToBookmarkHandlerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var going = d.BeginAsync(() => new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Failed(error));
        var load = await d.EndAsync(going);
        Assert.Null(load);

        var reported = Assert.IsType<AppProblem.PlaceLookupFailed>(Assert.Single(ports.Reported));
        Assert.Same(error, reported.Error);
        Assert.Equal(bookmark.Place, reported.Place);
        Assert.Equal(Strings.En.ErrorLookUpPlace(bookmark.Place.Label), ProblemTexts.Describe(Strings.En, reported).Title);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_ShowsAndReportsNothing()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var ports = new FakeGoToBookmarkHandlerPorts();
        var shown = d.AppState.ListSession.Navigation.ActiveTab;

        var going = d.BeginAsync(() => new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false), CancellationToken.None));
        d.AppState.ListSession.Navigation.BeginRequest();
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(await d.FixtureAsync("Outline"))));
        var load = await d.EndAsync(going);

        Assert.Null(load);
        Assert.Same(shown, d.AppState.ListSession.Navigation.ActiveTab);
        Assert.Empty(ports.Reported);
    }
}
