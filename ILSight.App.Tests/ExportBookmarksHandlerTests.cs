using ILSight.Bookmarking;

namespace ILSight.App.Tests;

public sealed class ExportBookmarksHandlerTests
{
    private const string Path = "C:/bookmarks/exported.json";

    private static BookmarkList OutlineBookmarked()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Outline"));
        var tab = d.AppState.Tabs.Active;
        Assert.NotNull(tab);
        Bookmarks.ToggleBookmark(d.AppState, tab, tab.Document.GetPosition("return First();").Line, Names.Bookmark);
        return d.AppState.Bookmarks;
    }

    [Fact]
    public void Handle_Written_WritesEachBookmarkAndReportsNothing()
    {
        var bookmarks = OutlineBookmarked();
        var ports = new FakeExportBookmarksHandlerPorts { Answer = new BookmarksWriteResult.Written() };

        new ExportBookmarksHandler(ports).Handle(bookmarks, Path);

        var written = Assert.Single(ports.Written);
        Assert.Equal(Path, written.Path);
        Assert.Equal(["Bookmark 1"], written.Records.Select(static r => r.Name));
        Assert.Empty(ports.Reported);
    }

    [Fact]
    public void Handle_WriteFailed_ReportsWhy()
    {
        var bookmarks = OutlineBookmarked();
        var ports = new FakeExportBookmarksHandlerPorts { Answer = new BookmarksWriteResult.Failed { Reason = "Disk full." } };

        new ExportBookmarksHandler(ports).Handle(bookmarks, Path);

        Assert.Equal("Disk full.", Assert.IsType<AppProblem.BookmarksNotExported>(Assert.Single(ports.Reported)).Reason);
    }
}
