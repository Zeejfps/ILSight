using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

public sealed class BookmarkImportingTests
{
    private const string Path = "C:/bookmarks/exported.json";

    private static async Task<List<Bookmark>> TwoBookmarksAsync()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(await d.FixtureAsync("Outline"));
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Assert.IsType<BookmarkToggle.Added>(Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark));
        Assert.IsType<BookmarkToggle.Added>(Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("foreach").Line, Names.Bookmark));
        return d.AppState.Bookmarks.Items.ToList();
    }

    // A list holds only the bookmarks it made.
    private static Bookmark MadeBy(BookmarkList list, Bookmark bookmark)
    {
        return list.CreateFromRecord(Bookmarks.CreateRecord(bookmark), bookmark.Place);
    }

    private static BookmarksRead FileOf(IEnumerable<Bookmark> bookmarks)
    {
        var records = bookmarks
            .Select(Bookmarks.CreateRecord)
            .ToList();
        return new BookmarksRead.Read { Bookmarks = records, Problems = [] };
    }

    private static List<Lookup<NodePath>> PlacesOf(IEnumerable<Bookmark> bookmarks)
    {
        return bookmarks
            .Select(static Lookup<NodePath> (b) => new Lookup<NodePath>.Found { Value = b.Place })
            .ToList();
    }

    [Fact]
    public async Task ImportAsync_NoBookmarksYet_TakesThoseFoundAtOnceAndReportsTheLost()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImportingPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<NodePath>>>.Ok([new Lookup<NodePath>.Found { Value = file[0].Place }, new Lookup<NodePath>.NotFound()]));
        await importing.WithinPatienceAsync();

        Assert.Equal([Path], ports.Read);
        Assert.Equal(["Bookmark 1"], bookmarks.Items.Select(static b => b.Name));
        Assert.Null(imports.Asking);
        var lost = Assert.IsType<Refusal.ImportedBookmarksLost>(Assert.Single(ports.Refused));
        Assert.Equal("exported.json", lost.File);
        Assert.Single(lost.Lost);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ImportAsync_BookmarksThere_WaitsForTheUserToSayHow()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImportingPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(MadeBy(bookmarks, file[0]));
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<NodePath>>>.Ok(PlacesOf(file)));
        await importing.WithinPatienceAsync();

        Assert.NotNull(imports.Asking);
        Assert.Equal(2, imports.Asking.Bookmarks.Count);
        Assert.Single(bookmarks.Items);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task FinishImport_Replace_TakesThemAndReportsThoseLeftOut()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImportingPorts { File = FileOf([file[0], file[0], file[1]]) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(MadeBy(bookmarks, file[1]));
        var imports = new BookmarkImports();
        var importing = new BookmarkImporting(ports);
        var reading = importing.ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<NodePath>>>.Ok(PlacesOf([file[0], file[0], file[1]])));
        await reading.WithinPatienceAsync();
        var asked = imports.Asking;
        Assert.NotNull(asked);

        importing.FinishImport(bookmarks, imports, asked, BookmarkImportMode.Replace);

        Assert.Equal(2, bookmarks.Items.Count);
        Assert.Null(imports.Asking);
        Assert.Equal(1, Assert.IsType<Refusal.BookmarksLeftOut>(Assert.Single(ports.Refused)).Count);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ImportAsync_NotBookmarksFile_RefusesItAndLooksUpNothing()
    {
        var ports = new FakeBookmarkImportingPorts { File = new BookmarksRead.NotBookmarksFile() };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        await new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);

        Assert.Equal(Path, Assert.IsType<Refusal.NotBookmarksFile>(Assert.Single(ports.Refused)).Path);
        Assert.Empty(ports.Reported);
        Assert.Empty(ports.Answers);
        Assert.Empty(bookmarks.Items);
    }

    [Fact]
    public async Task ImportAsync_LookupFailed_ReportsTheFileNotImported()
    {
        var ports = new FakeBookmarkImportingPorts { File = FileOf(await TwoBookmarksAsync()) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<Lookup<NodePath>>>.Failed(new InvalidOperationException("Unreadable.")));
        await importing.WithinPatienceAsync();

        Assert.Equal("exported.json", Assert.IsType<AppProblem.BookmarksNotImported>(Assert.Single(ports.Reported)).File);
        Assert.Empty(bookmarks.Items);
    }
}
