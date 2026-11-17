using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight.App.Tests;

public sealed class BookmarkImporterTests
{
    private const string Path = "C:/bookmarks/exported.json";

    private static async Task<List<Bookmark>> TwoBookmarksAsync()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var outline = await d.FixtureAsync("Outline");
        d.Show(outline);
        var tab = d.AppState.ListSession.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var firstToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(firstToggle);
        var secondToggle = Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("foreach").Line, Names.Bookmark);
        UnionAsserts.GetCase<BookmarkToggle.Added>(secondToggle);
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

    private static List<Outcome<Lookup<NodePath>>> PlacesOf(IEnumerable<Bookmark> bookmarks)
    {
        return bookmarks
            .Select(static b => Found(b.Place))
            .ToList();
    }

    private static Outcome<Lookup<NodePath>> Found(NodePath place)
    {
        return new Outcome<Lookup<NodePath>>.Ok(new Lookup<NodePath>.Found { Value = place });
    }

    [Fact]
    public async Task ImportAsync_NoBookmarksYet_TakesThoseFoundAtOnceAndReportsTheLost()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImporterPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        var importing = new BookmarkImporter(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult([Found(file[0].Place), new Outcome<Lookup<NodePath>>.Ok(new Lookup<NodePath>.NotFound())]);
        await importing.WithinPatienceAsync();

        Assert.Equal([Path], ports.Read);
        Assert.Equal(["Bookmark 1"], bookmarks.Items.Select(static b => b.Name));
        Assert.Null(imports.Asking);
        var lost = UnionAsserts.GetCase<Refusal.ImportedBookmarksLost>(Assert.Single(ports.Refused));
        Assert.Equal("exported.json", lost.File);
        Assert.Single(lost.Lost);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ImportAsync_BookmarksThere_WaitsForTheUserToSayHow()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImporterPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(MadeBy(bookmarks, file[0]));
        var imports = new BookmarkImports();

        var importing = new BookmarkImporter(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(PlacesOf(file));
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
        var ports = new FakeBookmarkImporterPorts { File = FileOf([file[0], file[0], file[1]]) };
        var bookmarks = new BookmarkList();
        bookmarks.TryAdd(MadeBy(bookmarks, file[1]));
        var imports = new BookmarkImports();
        var importing = new BookmarkImporter(ports);
        var reading = importing.ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult(PlacesOf([file[0], file[0], file[1]]));
        await reading.WithinPatienceAsync();
        var asked = imports.Asking;
        Assert.NotNull(asked);

        importing.FinishImport(bookmarks, imports, asked, BookmarkImportMode.Replace);

        Assert.Equal(2, bookmarks.Items.Count);
        Assert.Null(imports.Asking);
        Assert.Equal(1, UnionAsserts.GetCase<Refusal.BookmarksLeftOut>(Assert.Single(ports.Refused)).Count);
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public async Task ImportAsync_NotBookmarksFile_RefusesItAndLooksUpNothing()
    {
        var ports = new FakeBookmarkImporterPorts { File = new BookmarksRead.NotBookmarksFile() };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();

        await new BookmarkImporter(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);

        Assert.Equal(Path, UnionAsserts.GetCase<Refusal.NotBookmarksFile>(Assert.Single(ports.Refused)).Path);
        Assert.Empty(ports.Reported);
        Assert.Empty(ports.Answers);
        Assert.Empty(bookmarks.Items);
    }

    [Fact]
    public async Task ImportAsync_OneLookupFailed_TakesTheOtherAndReportsTheFailedOneOnceWithWhy()
    {
        var file = await TwoBookmarksAsync();
        var ports = new FakeBookmarkImporterPorts { File = FileOf(file) };
        var bookmarks = new BookmarkList();
        var imports = new BookmarkImports();
        var error = new InvalidOperationException("Unreadable.");

        var importing = new BookmarkImporter(ports).ImportAsync(bookmarks, imports, Path, CancellationToken.None);
        Assert.Single(ports.Answers).SetResult([new Outcome<Lookup<NodePath>>.Failed(error), Found(file[1].Place)]);
        await importing.WithinPatienceAsync();

        Assert.Equal(["Bookmark 2"], bookmarks.Items.Select(static b => b.Name));
        var reported = UnionAsserts.GetCase<AppProblem.ImportedBookmarksNotPlaced>(Assert.Single(ports.Reported));
        Assert.Equal("exported.json", reported.File);
        var failed = Assert.Single(reported.Failed);
        Assert.Equal("Bookmark 1", failed.Bookmark.Name);
        Assert.Same(error, failed.Error);
        Assert.Empty(ports.Refused);
    }

    [Fact]
    public void Lost_bookmarks_are_joined_by_each_languages_template_and_separator_the_values_as_they_are()
    {
        Refusal lost = new Refusal.ImportedBookmarksLost
        {
            File = "exported.json",
            Lost =
            [
                new LostBookmark { Name = "Start", Location = "Shop.Cart.Sum()", File = "Shop.dll" },
                new LostBookmark { Name = "End", Location = "Shop.Order", File = "Shop.dll" },
            ],
        };

        Assert.Equal("Start: Shop.Cart.Sum() (Shop.dll); End: Shop.Order (Shop.dll)", lost.Words(Strings.For(Locale.En)).Why);
        Assert.Equal("Start : Shop.Cart.Sum() (Shop.dll) ; End : Shop.Order (Shop.dll)", lost.Words(Strings.For(Locale.Fr)).Why);
        Assert.Equal("Start：Shop.Cart.Sum()（Shop.dll）、End：Shop.Order（Shop.dll）", lost.Words(Strings.For(Locale.Ja)).Why);
        Assert.Equal(["Start：Shop.Cart.Sum()（Shop.dll）", "End：Shop.Order（Shop.dll）"], lost.ListItems(Strings.For(Locale.Ja)));
    }

    [Fact]
    public void A_failed_lookup_and_an_entry_left_out_are_said_by_each_languages_template_the_failure_by_its_message()
    {
        var bookmark = new LostBookmark { Name = "Start", Location = "Shop.Cart.Sum()", File = "Shop.dll" };
        var failed = new AppProblem.ImportedBookmarksNotPlaced { File = "exported.json", Failed = [(bookmark, new InvalidOperationException("Unreadable."))] };
        var unread = new AppProblem.ImportedBookmarksUnreadable
        {
            File = "exported.json",
            Problems = [new BookmarkEntryProblem.NotABookmark { Entry = 2, Detail = "No name." }],
        };

        Assert.Equal("Start: Shop.Cart.Sum() (Shop.dll): Unreadable.", ProblemTexts.Describe(Strings.For(Locale.En), failed).Details);
        Assert.Equal("Start : Shop.Cart.Sum() (Shop.dll) : Unreadable.", ProblemTexts.Describe(Strings.For(Locale.Fr), failed).Details);
        Assert.Equal("Start：Shop.Cart.Sum()（Shop.dll）：Unreadable.", ProblemTexts.Describe(Strings.For(Locale.Ja), failed).Details);
        Assert.Equal("#2: No name.", ProblemTexts.Describe(Strings.For(Locale.En), unread).Details);
        Assert.Equal("#2 : No name.", ProblemTexts.Describe(Strings.For(Locale.Fr), unread).Details);
        Assert.Equal("#2：No name.", ProblemTexts.Describe(Strings.For(Locale.Ja), unread).Details);
    }
}
