using ILSight.Core;
using ILSight.Bookmarking;

namespace ILSight.App.Tests;

public sealed class BookmarkImportingTests
{
    private const string Path = "C:/bookmarks/exported.json";

    /// <summary>Two bookmarks, as a file keeps them, of the fixture Outline's "return First();" and "foreach"
    /// lines.</summary>
    private static List<Bookmark> TwoBookmarks()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Outline"));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark);
        Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("foreach").Line, Names.Bookmark);
        return d.AppState.Bookmarks.Items.ToList();
    }

    private static BookmarksRead FileOf(IEnumerable<Bookmark> bookmarks)
    {
        var records = bookmarks
            .Select(Bookmarks.CreateRecord)
            .ToList();
        return new BookmarksRead.Read { Bookmarks = records, Problems = [] };
    }

    private static List<NodePath?> PlacesOf(IEnumerable<Bookmark> bookmarks)
    {
        return bookmarks
            .Select(static NodePath? (b) => b.Place)
            .ToList();
    }

    [Fact]
    public async Task ImportAsync_NoBookmarksYet_TakesThoseFoundAtOnceAndReportsTheLost()
    {
        var file = TwoBookmarks();
        var ports = new FakeBookmarkImportingPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.True(imports.IsFinding);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<NodePath?>>.Ok([file[0].Place, null]));
        await importing;

        Assert.Equal([Path], ports.Read);
        Assert.Equal(["Bookmark 1"], bookmarks.Items.Select(static b => b.Name));
        Assert.False(imports.IsFinding);
        Assert.Null(imports.Asking);
        var lost = Assert.IsType<AppProblem.ImportedBookmarksLost>(Assert.Single(ports.Reported));
        Assert.Equal("exported.json", lost.File);
        Assert.Single(lost.Lost);
    }

    [Fact]
    public async Task ImportAsync_BookmarksThere_WaitsForTheUserToSayHow()
    {
        var file = TwoBookmarks();
        var ports = new FakeBookmarkImportingPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(file[0]);
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<NodePath?>>.Ok(PlacesOf(file)));
        await importing;

        Assert.NotNull(imports.Asking);
        Assert.Equal(2, imports.Asking.Bookmarks.Count);
        Assert.Single(bookmarks.Items);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task FinishImport_Replace_TakesThemAndReportsThoseLeftOut()
    {
        var file = TwoBookmarks();
        var ports = new FakeBookmarkImportingPorts { File = FileOf([file[0], file[0], file[1]]) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(file[1]);
        var imports = new BookmarkImports();
        var importing = new BookmarkImporting(ports);
        var reading = importing.ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<NodePath?>>.Ok(PlacesOf([file[0], file[0], file[1]])));
        await reading;
        var asked = imports.Asking;
        Assert.NotNull(asked);

        importing.FinishImport(bookmarks, imports, asked, BookmarkImportMode.Replace);

        Assert.Equal(2, bookmarks.Items.Count);
        Assert.Null(imports.Asking);
        Assert.Equal(1, Assert.IsType<AppProblem.BookmarksLeftOut>(Assert.Single(ports.Reported)).Count);
    }

    [Fact]
    public async Task ImportAsync_NotBookmarksFile_ReportsItAndLooksUpNothing()
    {
        var ports = new FakeBookmarkImportingPorts { File = new BookmarksRead.NotBookmarksFile() };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        await new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);

        Assert.Equal(Path, Assert.IsType<AppProblem.NotBookmarksFile>(Assert.Single(ports.Reported)).Path);
        Assert.Empty(ports.Answers);
        Assert.Empty(bookmarks.Items);
        Assert.False(imports.IsFinding);
    }

    [Fact]
    public async Task ImportAsync_LookupFailed_ReportsTheFileNotImported()
    {
        var ports = new FakeBookmarkImportingPorts { File = FileOf(TwoBookmarks()) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        var importing = new BookmarkImporting(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(new Outcome<List<NodePath?>>.Failed(new InvalidOperationException("Unreadable.")));
        await importing;

        Assert.Equal("exported.json", Assert.IsType<AppProblem.BookmarksNotImported>(Assert.Single(ports.Reported)).File);
        Assert.Empty(bookmarks.Items);
        Assert.False(imports.IsFinding);
    }
}
