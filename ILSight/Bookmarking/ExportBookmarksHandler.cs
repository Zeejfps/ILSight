
namespace ILSight.Bookmarking;

/// <summary>Exports the bookmarks to a file the real ILSpy reads, through <see cref="IPorts"/>, and reports a file
/// that could not be written.</summary>
public sealed class ExportBookmarksHandler(ExportBookmarksHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the bookmarks could not be exported.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Writes <paramref name="records"/> to the file at <paramref name="path"/>, replacing it whole, as
        /// <see cref="BookmarkFile.Write"/> writes them.</summary>
        BookmarksWriteResult WriteBookmarks(string path, List<BookmarkRecord> records);
    }

    /// <summary>Writes <paramref name="bookmarks"/> to the file at <paramref name="path"/>; reports it when that
    /// failed.</summary>
    public void Handle(BookmarkList bookmarks, string path)
    {
        var written = ports.WriteBookmarks(path, bookmarks.ListRecords());
        if (written is BookmarksWriteResult.Failed failed)
        {
            ports.ReportError(new AppProblem.BookmarksNotExported(failed.Reason));
        }
    }
}
