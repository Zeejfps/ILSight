using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

public sealed class GoToBookmarkHandlerTests
{
    private static async Task<(AppStateDriver Driver, Bookmark Bookmark)> BookmarkedAsync()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var outline = await d.FixtureAsync("Outline");
        d.Show(outline);
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var toggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(toggle);
        var greeter = await d.FixtureAsync("Greeter");
        d.Show(greeter);
        return (d, Assert.Single(d.AppState.Bookmarks.Items));
    }

    [Fact]
    public async Task HandleAsync_PlaceFound_ShowsItLoadingAndRecordsItInTheHistory()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var outline = await d.FixtureAsync("Outline");
        var ports = new FakeGoToBookmarkHandlerPorts();
        var history = d.AppState.ListSession.Navigation.HistoryEntries.Count;

        var going = d.BeginAsync(() =>
        {
            var resolveTicket = d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false);
            return new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, resolveTicket, CancellationToken.None);
        });
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

        var going = d.BeginAsync(() =>
        {
            var resolveTicket = d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false);
            return new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, resolveTicket, CancellationToken.None);
        });
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(null)));
        var load = await d.EndAsync(going);

        Assert.Null(load);
        var gone = UnionAsserts.GetCase<Refusal.BookmarkGone>(Assert.Single(ports.Refused));
        Assert.Equal(bookmark.Name, gone.Bookmark);
        Assert.Equal(bookmark.Place, gone.Place);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_LookupFailed_ReportsTheLookupsFailureAsEveryPlaceLookupDoesNamingTheBookmarkBesideIt()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var ports = new FakeGoToBookmarkHandlerPorts();
        var error = new InvalidOperationException("Unreadable.");

        var going = d.BeginAsync(() =>
        {
            var resolveTicket = d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false);
            return new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, resolveTicket, CancellationToken.None);
        });
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Failed(error));
        var load = await d.EndAsync(going);
        Assert.Null(load);

        var reported = UnionAsserts.GetCase<AppProblem.PlaceLookupFailed>(Assert.Single(ports.Reported));
        Assert.Same(error, reported.Error);
        Assert.Equal(bookmark.Place, reported.Place);
        Assert.Equal(new PlaceLookupStep.GoingToBookmark(bookmark.Name), reported.Step);
        var text = ProblemTexts.Describe(Strings.En, reported);
        Assert.Equal(Strings.En.ErrorLookUpPlace(bookmark.Place.Label), text.Title);
        Assert.StartsWith(Strings.En.ErrorStepBookmark(bookmark.Name), text.Details);
    }

    [Fact]
    public async Task HandleAsync_LookupCouldNotOpenAFileThatIsThere_ReportsTheLookupWithWhatTheUserCanDoForEachCause()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var file = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Locked", "Locked.dll"));
        var denied = new AssemblyLoadException(file, new AssemblyLoadFault.CannotOpen(), "Cannot open.", new UnauthorizedAccessException("Access denied."));
        var damaged = new AssemblyLoadException(file, new AssemblyLoadFault.CannotOpen(), "Cannot open.", new BadImageFormatException("Bad image."));

        var deniedReport = await ReportedAsync(d, bookmark, denied);
        var damagedReport = await ReportedAsync(d, bookmark, damaged);

        var deniedLookup = UnionAsserts.GetCase<AppProblem.PlaceLookupFileDenied>(deniedReport);
        Assert.Equal(file, deniedLookup.File);
        var deniedText = ProblemTexts.Describe(Strings.En, deniedReport);
        Assert.Equal(Strings.En.ErrorLookUpPlace(bookmark.Place.Label), deniedText.Title);
        Assert.Equal(Strings.En.ErrorStepBookmark(bookmark.Name) + Environment.NewLine + Environment.NewLine + "Locked.dll couldn't be opened: access to it was denied, or another program has it locked. Make it readable, then try again.",
            deniedText.Details);
        Assert.Equal(denied.ToString(), deniedText.TechnicalDetails);
        var damagedLookup = UnionAsserts.GetCase<AppProblem.PlaceLookupFileDamaged>(damagedReport);
        Assert.Same(damaged, damagedLookup.Error);
        var damagedText = ProblemTexts.Describe(Strings.En, damagedReport);
        Assert.Equal(Strings.En.ErrorLookUpPlace(bookmark.Place.Label), damagedText.Title);
        Assert.Equal(Strings.En.ErrorStepBookmark(bookmark.Name) + Environment.NewLine + Environment.NewLine + Strings.En.ErrorFileDamaged("Locked.dll") + " " + Strings.En.ErrorRecoveryReplaceFile,
            damagedText.Details);
        Assert.Equal(damaged.ToString(), damagedText.TechnicalDetails);
    }

    private static async Task<AppProblem> ReportedAsync(AppStateDriver d, Bookmark bookmark, Exception error)
    {
        var ports = new FakeGoToBookmarkHandlerPorts();
        var going = d.BeginAsync(() =>
        {
            var resolveTicket = d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false);
            return new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, resolveTicket, CancellationToken.None);
        });
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Failed(error));
        Assert.Null(await d.EndAsync(going));
        return Assert.Single(ports.Reported);
    }

    [Fact]
    public async Task HandleAsync_SomethingElseAskedForMeanwhile_ShowsAndReportsNothing()
    {
        var (d, bookmark) = await BookmarkedAsync();
        var ports = new FakeGoToBookmarkHandlerPorts();
        var shown = d.AppState.ListSession.Navigation.ActiveTab;

        var going = d.BeginAsync(() =>
        {
            var resolveTicket = d.AppState.ListSession.Navigation.BeginResolving(inNewTab: false);
            return new GoToBookmarkHandler(ports).HandleAsync(d.AppState.ListSession.Navigation, d.AppState.Bookmarks, d.AppState.Decompiler, bookmark, resolveTicket, CancellationToken.None);
        });
        d.AppState.ListSession.Navigation.BeginRequest();
        var outline = await d.FixtureAsync("Outline");
        Assert.Single(ports.Answers).SetResult(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable<BrowseNode>(outline)));
        var load = await d.EndAsync(going);

        Assert.Null(load);
        Assert.Same(shown, d.AppState.ListSession.Navigation.ActiveTab);
        Assert.Empty(ports.Reported);
    }
}
