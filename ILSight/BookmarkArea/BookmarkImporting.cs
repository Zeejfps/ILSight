using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Importing bookmarks from a file, this app's or the real ILSpy's: reads it and finds the place of each
/// bookmark in the open assemblies through <see cref="IPorts"/>, then takes them at once when there are none yet, or
/// waits for the user to say whether they replace or merge in beside those there are. Bookmarks whose place is not
/// found are left out and said on the refusal line, with each module that could not be read they are in reported, and so
/// are those marking a line another marks already, when replacing.</summary>
public sealed class BookmarkImporting(BookmarkImporting.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user reading a file or looking its bookmarks up failed, or what in it could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Says on the refusal line what the import answered: a file that holds no bookmarks, the bookmarks it
        /// left out.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>The bookmarks in the file at <paramref name="path"/>, each read on its own, as
        /// <see cref="BookmarkFile.Read"/> reads them.</summary>
        BookmarksRead ReadBookmarks(string path);

        /// <summary>The place in the open assemblies of the bookmark each of <paramref name="records"/> describes, in
        /// the same order, each as its own lookup came out: not found for one not there, unreadable for one in a module
        /// whose type system the library could not read, <see cref="Outcome{T}.Failed"/> for one whose lookup failed;
        /// looked for without holding up the screen.</summary>
        Task<List<Outcome<Lookup<NodePath>>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct);
    }

    /// <summary>Reads the bookmarks in the file at <paramref name="path"/> and finds the place of each: takes them
    /// into <paramref name="bookmarks"/> at once when it has none, and otherwise has them wait among
    /// <paramref name="imports"/> for the user to say how (<see cref="FinishImport"/>). Reports what was wrong with
    /// the file, and the bookmarks whose place is not found, which are left out.</summary>
    public async Task ImportAsync(BookmarkList bookmarks, BookmarkImports imports, string path, CancellationToken ct)
    {
        var file = Path.GetFileName(path);
        var read = ports.ReadBookmarks(path);
        if (read is BookmarksRead.NotBookmarksFile)
        {
            ports.ShowRefusal(new Refusal.NotBookmarksFile { Path = path });
            return;
        }

        var problem = read.Match<AppProblem?>(
            read: r => r.Problems.Count == 0 ? null : new AppProblem.ImportedBookmarksUnreadable
            {
                File = file,
                Problems = r.Problems,
            },
            notBookmarksFile: static _ => null,
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
        var found = await ports.LookUpPlacesAsync(records, ct);
        TakeImported(bookmarks, imports, records, found, file);
    }

    private void TakeImported(BookmarkList bookmarks, BookmarkImports imports, List<BookmarkRecord> records, List<Outcome<Lookup<NodePath>>> places,
        string file)
    {
        var found = records.Zip(places, static (record, place) => new FoundBookmark
        {
            Record = record,
            Place = place,
        }).ToList();
        ReportFailedBookmarks(found, file);
        ReportLostBookmarks(found, file);
        var imported = found
            .Select(f => CreateBookmark(bookmarks, f))
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
        var lost = found.FindAll(static f => f.Place is Outcome<Lookup<NodePath>>.Ok { Value: not Lookup<NodePath>.Found });
        if (lost.Count == 0)
        {
            return;
        }

        var lines = lost.ConvertAll(DescribeLostBookmark);
        ports.ShowRefusal(new Refusal.ImportedBookmarksLost { File = file, Lost = lines });
        foreach (var unreadable in Lookup.ListUnreadable(lost.Select(static f => ((Outcome<Lookup<NodePath>>.Ok)f.Place).Value)))
        {
            ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable));
        }
    }

    // Each bookmark whose lookup failed, left out, reported once with why: the others settle as their own lookups came out.
    private void ReportFailedBookmarks(List<FoundBookmark> found, string file)
    {
        var failed = new List<(string Bookmark, Exception Error)>();
        foreach (var bookmark in found)
        {
            if (bookmark.Place is Outcome<Lookup<NodePath>>.Failed failure)
            {
                failed.Add((DescribeLostBookmark(bookmark), failure.Error));
            }
        }

        if (failed.Count == 0)
        {
            return;
        }

        ports.ReportError(new AppProblem.ImportedBookmarksNotPlaced { File = file, Failed = failed });
    }

    private static string DescribeLostBookmark(FoundBookmark lost)
    {
        var record = lost.Record;
        var location = record.LocationNodeName ?? record.MemberName;
        return $"{record.Name}: {location} ({record.FileName})";
    }

    private static Bookmark? CreateBookmark(BookmarkList bookmarks, FoundBookmark found)
    {
        if (found.Place is not Outcome<Lookup<NodePath>>.Ok { Value: Lookup<NodePath>.Found place })
        {
            return null;
        }

        return bookmarks.CreateFromRecord(found.Record, place.Value);
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

    private void TakeBookmarks(BookmarkList bookmarks, IReadOnlyList<Bookmark> imported, BookmarkImportMode mode)
    {
        var leftOut = bookmarks.Import(imported, mode);
        if (mode == BookmarkImportMode.Merge)
        {
            return;
        }

        ReportLeftOutBookmarks(leftOut);
    }

    private void ReportLeftOutBookmarks(int leftOut)
    {
        if (leftOut == 0)
        {
            return;
        }

        ports.ShowRefusal(new Refusal.BookmarksLeftOut { Count = leftOut });
    }

    private sealed record FoundBookmark
    {
        public required BookmarkRecord Record { get; init; }

        public required Outcome<Lookup<NodePath>> Place { get; init; }
    }
}
