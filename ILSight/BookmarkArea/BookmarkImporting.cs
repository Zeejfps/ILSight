using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Importing bookmarks from a file, this app's or the real ILSpy's: reads it and finds the place of each
/// bookmark in the open assemblies through <see cref="IPorts"/>, then takes them at once when there are none yet, or
/// waits for the user to say whether they replace or merge in beside those there are. Bookmarks whose place is not
/// found are left out and reported, and so are those marking a line another marks already, when replacing.</summary>
public sealed class BookmarkImporting(BookmarkImporting.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user what was wrong with a file or the bookmarks in it.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The bookmarks in the file at <paramref name="path"/>, each read on its own, as
        /// <see cref="BookmarkFile.Read"/> reads them.</summary>
        BookmarksRead ReadBookmarks(string path);

        /// <summary>The place in the open assemblies of the bookmark each of <paramref name="records"/> describes, in
        /// the same order, null for one not found, looked for without holding up the screen. A lookup that failed
        /// comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<List<NodePath?>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct);
    }

    /// <summary>Reads the bookmarks in the file at <paramref name="path"/> and finds the place of each: takes them
    /// into <paramref name="bookmarks"/> at once when it has none, and otherwise has them wait among
    /// <paramref name="imports"/> for the user to say how (<see cref="FinishImport"/>). Reports what was wrong with
    /// the file, and the bookmarks whose place is not found, which are left out.</summary>
    public async Task ImportAsync(BookmarkList bookmarks, BookmarkImports imports, string path, CancellationToken ct)
    {
        var file = Path.GetFileName(path);
        var read = ports.ReadBookmarks(path);
        var problem = read.Match<AppProblem?>(
            read: r => r.Problems.Count == 0 ? null : new AppProblem.ImportedBookmarksUnreadable
            {
                File = file,
                Problems = r.Problems,
            },
            notBookmarksFile: _ => new AppProblem.NotBookmarksFile(path),
            failed: f => new AppProblem.BookmarksNotImported { File = file, Reason = f.Reason });
        if (problem is not null)
        {
            ports.ReportError(problem);
        }
        if (read is not BookmarksRead.Read bookmarksRead)
        {
            return;
        }

        var records = bookmarksRead.Bookmarks;
        imports.Finding++;
        var found = await ports.LookUpPlacesAsync(records, ct);
        imports.Finding--;
        TakeImported(bookmarks, imports, records, found, file);
    }

    private void TakeImported(BookmarkList bookmarks, BookmarkImports imports, List<BookmarkRecord> records, Outcome<List<NodePath?>> found, string file)
    {
        found.Switch(
            ok => TakeImported(bookmarks, imports, records, ok.Value, file),
            failed => ports.ReportError(new AppProblem.BookmarksNotImported
            {
                File = file,
                Reason = failed.Error.ToString(),
            }));
    }

    private void TakeImported(BookmarkList bookmarks, BookmarkImports imports, List<BookmarkRecord> records, List<NodePath?> places, string file)
    {
        var found = records.Zip(places, static (record, place) => new FoundBookmark
        {
            Record = record,
            Place = place,
        }).ToList();
        ReportLostBookmarks(found, file);
        var imported = found
            .Select(CreateBookmark)
            .OfType<Bookmark>()
            .ToList();
        if (imported.Count == 0)
        {
            return;
        }

        if (bookmarks.Items.Count == 0)
        {
            TakeBookmarks(bookmarks, imported, BookmarkImportMode.Replace);
            return;
        }
        imports.Waiting.Add(new PendingImport(imported));
    }

    private void ReportLostBookmarks(List<FoundBookmark> found, string file)
    {
        var lost = found.FindAll(f => f.Place is null);
        if (lost.Count == 0)
        {
            return;
        }

        var lines = lost.ConvertAll(DescribeLostBookmark);
        ports.ReportError(new AppProblem.ImportedBookmarksLost { File = file, Lost = lines });
    }

    private static string DescribeLostBookmark(FoundBookmark lost)
    {
        var record = lost.Record;
        var location = record.LocationNodeName ?? record.MemberName;
        return $"{record.Name}: {location} ({record.FileName})";
    }

    /// <summary>The bookmark of <paramref name="found"/>, or null when its place wasn't found.</summary>
    private static Bookmark? CreateBookmark(FoundBookmark found)
    {
        if (found.Place is null)
        {
            return null;
        }

        return Bookmarks.CreateFromRecord(found.Record, found.Place);
    }

    /// <summary>Takes the bookmarks <paramref name="import"/> waits with into <paramref name="bookmarks"/> as
    /// <paramref name="mode"/> says, or drops them when null; nothing when it waits among <paramref name="imports"/>
    /// no more.</summary>
    public void FinishImport(BookmarkList bookmarks, BookmarkImports imports, PendingImport import, BookmarkImportMode? mode)
    {
        if (!imports.Waiting.Remove(import))
        {
            return;
        }

        if (mode is null)
        {
            return;
        }

        TakeBookmarks(bookmarks, import.Bookmarks, mode.Value);
    }

    /// <summary>Takes <paramref name="imported"/> in as <paramref name="mode"/> says. Those marking a line a bookmark
    /// marks already are left out: quietly when merging, and reported when replacing.</summary>
    private void TakeBookmarks(BookmarkList bookmarks, IReadOnlyList<Bookmark> imported, BookmarkImportMode mode)
    {
        var leftOut = bookmarks.Import(imported, mode);
        if (mode == BookmarkImportMode.Merge)
        {
            return;
        }

        ReportLeftOutBookmarks(leftOut);
    }

    /// <summary>Says how many bookmarks were left out because another marks the same line.</summary>
    private void ReportLeftOutBookmarks(int leftOut)
    {
        if (leftOut == 0)
        {
            return;
        }

        ports.ReportError(new AppProblem.BookmarksLeftOut(leftOut));
    }

    /// <summary>A bookmark read, and where it was found.</summary>
    private sealed record FoundBookmark
    {
        public required BookmarkRecord Record { get; init; }

        public required NodePath? Place { get; init; }
    }
}
