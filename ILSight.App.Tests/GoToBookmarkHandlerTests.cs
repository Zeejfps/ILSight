using ILSight.Core;
using ILSight.Bookmarking;

namespace ILSight.App.Tests;

public sealed class GoToBookmarkHandlerTests
{
    /// <summary>A driver with no screen that shows the fixture Outline with its "return First();" line bookmarked,
    /// then the fixture Greeter; and the bookmark.</summary>
    private static (AppStateDriver Driver, Bookmark Bookmark) Bookmarked()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Outline"));
        var tab = d.AppState.Navigation.ActiveTab;
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
        var ports = new FakeGoToBookmarkHandlerPorts();
        var history = d.AppState.Navigation.HistoryEntries.Count;

        var going = new GoToBookmarkHandler(ports).HandleAsync(d.AppState, bookmark, CancellationToken.None);
        Assert.True(d.AppState.IsResolving);
        Assert.Single(ports.Answers).SetResult(new Outcome<BrowseNode?>.Ok(outline));
        var load = await going;

        Assert.Equal([bookmark.Place], ports.Asked);
        Assert.NotNull(load);
        Assert.Same(outline, load.Node);
        Assert.Same(d.AppState.Navigation.ActiveTab, load.Tab);
        Assert.False(d.AppState.IsResolving);
        Assert.Equal(history + 1, d.AppState.Navigation.HistoryEntries.Count);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_PlaceGone_ReportsTheBookmarkGone()
    {
        var (d, bookmark) = Bookmarked();
        var ports = new FakeGoToBookmarkHandlerPorts();

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
        var ports = new FakeGoToBookmarkHandlerPorts();
        var shown = d.AppState.Navigation.ActiveTab;

        var going = new GoToBookmarkHandler(ports).HandleAsync(d.AppState, bookmark, CancellationToken.None);
        d.AppState.Navigation.BeginRequest();
        Assert.Single(ports.Answers).SetResult(new Outcome<BrowseNode?>.Ok(d.Fixture("Outline")));
        var load = await going;

        Assert.Null(load);
        Assert.Same(shown, d.AppState.Navigation.ActiveTab);
        Assert.Empty(ports.Reported);
    }
}
