using System.Diagnostics;
using ILSight.Core;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>Marking lines with bookmarks, going to them, and exporting and importing them.</summary>
public static class Bookmarks
{
    /// <summary>Adds <paramref name="bookmark"/>, unless a bookmark marks its line already; returns
    /// whether it was added.</summary>
    public static bool Add(BookmarkList list, Bookmark bookmark)
    {
        if (IsMarked(list, bookmark))
            return false;
        list.Items.Add(bookmark);
        list.Revision++;
        return true;
    }

    private static bool IsMarked(BookmarkList list, Bookmark bookmark)
    {
        return list.Items.Exists(bookmark.MarksSameLine);
    }

    /// <summary>Removes <paramref name="bookmark"/>; returns the row it was in, or -1 when it was not listed.</summary>
    public static int Remove(BookmarkList list, Bookmark bookmark)
    {
        var index = list.Items.IndexOf(bookmark);
        if (index < 0)
            return -1;
        list.Items.RemoveAt(index);
        list.Revision++;
        return index;
    }

    public static void SetEnabled(BookmarkList list, Bookmark bookmark, bool enabled)
    {
        if (bookmark.Enabled == enabled) return;
        bookmark.Enabled = enabled;
        list.Revision++;
    }

    /// <summary>Gives <paramref name="bookmark"/> the name <paramref name="name"/>, trimmed, unless it is blank.</summary>
    public static void Rename(BookmarkList list, Bookmark bookmark, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return;
        if (trimmed == bookmark.Name)
            return;
        bookmark.Name = trimmed;
        list.Revision++;
    }

    /// <summary>The next enabled bookmark after <paramref name="from"/>, or the previous when
    /// <paramref name="direction"/> is negative, wrapping around; null when none is enabled.</summary>
    public static Bookmark? FindNext(BookmarkList list, Bookmark? from, int direction)
    {
        var count = list.Items.Count;
        var start = FindStepStart(list, from, direction);
        for (var step = 1; step <= count; step++)
        {
            var index = ((start + Math.Sign(direction) * step) % count + count) % count;
            if (!list.Items[index].Enabled) continue;
            return list.Items[index];
        }
        return null;
    }

    /// <summary>The row stepping starts from: <paramref name="from"/>'s, or just before the first or
    /// after the last.</summary>
    private static int FindStepStart(BookmarkList list, Bookmark? from, int direction)
    {
        var index = FindRow(list, from);
        if (index >= 0)
            return index;
        if (direction > 0)
            return -1;
        return list.Items.Count;
    }

    private static int FindRow(BookmarkList list, Bookmark? bookmark)
    {
        if (bookmark == null)
            return -1;
        return list.Items.IndexOf(bookmark);
    }

    /// <summary>The first bookmark marking <paramref name="line"/> of <paramref name="document"/>,
    /// shown for <paramref name="node"/>.</summary>
    public static Bookmark? FindAt(BookmarkList list, DecompiledDocument document, BrowseNode node, int line)
    {
        foreach (var bookmark in list.Items)
        {
            if (bookmark.FindLine(document, node) == line)
                return bookmark;
        }
        return null;
    }

    /// <summary>The lines of <paramref name="document"/>, shown for <paramref name="node"/>, that
    /// bookmarks mark, each with whether one marking it is enabled. Runs every frame a document shows.</summary>
    public static IReadOnlyDictionary<int, bool> ListMarkedLines(BookmarkList list, DecompiledDocument document, BrowseNode node)
    {
        var marks = list.MarksByDocument.GetOrCreateValue(document);
        if (marks.Revision == list.Revision && Equals(marks.Place, node.Path)) return marks.Lines;
        marks.Revision = list.Revision;
        marks.Place = node.Path;
        marks.Lines.Clear();
        foreach (var bookmark in list.Items)
        {
            var line = bookmark.FindLine(document, node);
            if (line >= 0)
                marks.Lines[line] = marks.Lines.GetValueOrDefault(line) || bookmark.Enabled;
        }
        return marks.Lines;
    }

    /// <summary>The first of <paramref name="format"/>(1), (2), … that no bookmark is called.</summary>
    public static string CreateName(BookmarkList list, Func<int, string> format)
    {
        for (var n = 1; ; n++)
        {
            var name = format(n);
            if (!list.Items.Exists(b => b.Name == name))
                return name;
        }
    }

    public static List<BookmarkRecord> ListRecords(BookmarkList list)
    {
        return list.Items.ConvertAll(static b => b.ToRecord());
    }

    /// <summary>Replaces the list with the bookmarks in <paramref name="saved"/> that know their place.
    /// Returns how many were left out because one before them marks the same line.</summary>
    public static int LoadBookmarks(BookmarkList list, IEnumerable<BookmarkRecord>? saved)
    {
        var bookmarks = (saved ?? []).Select(static r => r.Location != null ? Bookmark.FromRecord(r, r.Location) : null)
            .OfType<Bookmark>();
        return Import(list, bookmarks, BookmarkImportMode.Replace);
    }

    /// <summary>Takes <paramref name="node"/>, found where <paramref name="bookmark"/> was made, as its place,
    /// so that it is saved with the labels the node shows now.</summary>
    public static void Resolve(BookmarkList list, Bookmark bookmark, BrowseNode node)
    {
        bookmark.Place = node.Path;
        list.Revision++;
    }

    /// <summary>Adds <paramref name="bookmarks"/>, after dropping every bookmark first when replacing,
    /// leaving out the ones that mark a line already marked. Returns how many were left out.</summary>
    public static int Import(BookmarkList list, IEnumerable<Bookmark> bookmarks, BookmarkImportMode mode)
    {
        if (mode == BookmarkImportMode.Replace)
            list.Items.Clear();
        var leftOut = 0;
        foreach (var bookmark in bookmarks)
        {
            if (IsMarked(list, bookmark))
            {
                leftOut++;
                continue;
            }
            list.Items.Add(bookmark);
        }
        list.Revision++;
        return leftOut;
    }

    /// <summary>Selects the bookmark a toggle added, or passes the selection of the one it removed to
    /// the bookmark now in its row.</summary>
    public static void TakeToggle(BookmarksPanel panel, BookmarkToggle toggle, IReadOnlyList<Bookmark> bookmarks)
    {
        switch (toggle)
        {
            case BookmarkToggle.Added added:
                panel.Selection.Select(added.Bookmark, bookmarks);
                break;
            case BookmarkToggle.Removed removed:
                TakeRemoved(panel, removed.Bookmark, removed.Row, bookmarks);
                break;
        }
    }

    /// <summary>Passes the selection of <paramref name="bookmark"/>, removed from <paramref name="row"/>, to the
    /// bookmark now in its row, and ends its rename.</summary>
    public static void TakeRemoved(BookmarksPanel panel, Bookmark bookmark, int row, IReadOnlyList<Bookmark> bookmarks)
    {
        if (panel.Renaming == bookmark)
            panel.Renaming = null;
        if (panel.Selection.Item != bookmark)
            return;
        SelectInRow(panel, row, bookmarks);
    }

    /// <summary>Selects the bookmark in <paramref name="row"/>, or the last when there are fewer.</summary>
    private static void SelectInRow(BookmarksPanel panel, int row, IReadOnlyList<Bookmark> bookmarks)
    {
        var kept = Math.Min(row, bookmarks.Count - 1);
        if (kept < 0)
        {
            panel.Selection.Clear();
            return;
        }
        panel.Selection.Select(bookmarks[kept], bookmarks, reveal: false);
    }

    /// <summary>Forgets the rename of a bookmark that is gone, as importing in its place drops it.</summary>
    public static void FollowList(BookmarksPanel panel, IReadOnlyList<Bookmark> bookmarks)
    {
        if (panel.Renaming == null)
            return;
        if (bookmarks.Contains(panel.Renaming))
            return;
        panel.Renaming = null;
    }

    public static void StartRename(BookmarksPanel panel, Bookmark bookmark)
    {
        panel.Renaming = bookmark;
        panel.RenameText = bookmark.Name;
        panel.FocusRename = true;
    }

    /// <summary>Ends the rename, returning the bookmark that was being renamed, or null.</summary>
    public static Bookmark? EndRename(BookmarksPanel panel)
    {
        var bookmark = panel.Renaming;
        panel.Renaming = null;
        return bookmark;
    }

    /// <summary>Removes the bookmark marking <paramref name="line"/> of <paramref name="tab"/>'s
    /// document, or bookmarks the line, naming the bookmark by the statement or definition on it
    /// when it has one. Returns which it did. <paramref name="nameOf"/> names a new bookmark by a number,
    /// counted from 1 until no bookmark has the name.</summary>
    public static BookmarkToggle ToggleBookmark(AppState appState, DocumentTab tab, int line, Func<int, string> nameOf)
    {
        var node = tab.Node;
        if (node == null)
            return new BookmarkToggle.Unchanged();
        if (!CanBookmarkLine(tab, line))
            return new BookmarkToggle.Unchanged();
        var existing = FindAt(appState.Bookmarks, tab.Document, node, line);
        if (existing != null)
            return RemoveBookmark(appState, existing);
        return AddBookmark(appState, tab, node, line, nameOf);
    }

    private static BookmarkToggle RemoveBookmark(AppState appState, Bookmark bookmark)
    {
        var row = Remove(appState.Bookmarks, bookmark);
        return new BookmarkToggle.Removed(bookmark, row);
    }

    private static bool CanBookmarkLine(DocumentTab tab, int line)
    {
        if (tab.IsBusy)
            return false;
        if (tab.Document.Error != null)
            return false;
        if (line < 0)
            return false;
        return line < tab.Document.Lines.Length;
    }

    private static BookmarkToggle AddBookmark(AppState appState, DocumentTab tab, BrowseNode node, int line, Func<int, string> nameOf)
    {
        var anchor = tab.Document.AnchorAt(line);
        var name = CreateName(appState.Bookmarks, nameOf);
        var info = ILSpyTree.Describe(node, anchor);
        var bookmark = Bookmark.At(node, tab.Language, line, anchor, name, info);
        Add(appState.Bookmarks, bookmark);
        return new BookmarkToggle.Added(bookmark);
    }

    /// <summary>Shows the line <paramref name="bookmark"/> marks in the active tab, opening its
    /// assembly first if it is not open, and records it in the history.</summary>
    public static void GoToBookmark(AppState appState, Bookmark bookmark) =>
        Background.Start(GoToBookmarkAsync(appState, bookmark), appState.ReportError);

    private static async Task GoToBookmarkAsync(AppState appState, Bookmark bookmark)
    {
        var ticket = appState.Intent.Begin();
        var destination = ChooseDestination(appState, inNewTab: false);
        var place = bookmark.Place;
        appState.Intent.StartResolving(ticket);
        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var found = await Background.Run(() => NodeLookup.Find(assemblies, session, place));
        if (appState.Intent.IsStale(ticket))
            return;
        appState.Intent.StopResolving();
        ShowBookmarkPlace(appState, bookmark, found, destination);
    }

    private static void ShowBookmarkPlace(AppState appState, Bookmark bookmark, Outcome<BrowseNode?> found, TabDestination destination)
    {
        switch (found)
        {
            case Outcome<BrowseNode?>.Ok ok when ok.Value != null:
                Resolve(appState.Bookmarks, bookmark, ok.Value);
                LeaveActiveTab(appState);
                ShowNodeIn(appState, destination, ok.Value, target: bookmark);
                appState.History.Push(ok.Value, appState.Language);
                break;
            case Outcome<BrowseNode?>.Ok:
                ReportBookmarkGone(appState, bookmark, null);
                break;
            case Outcome<BrowseNode?>.Failed failed:
                ReportBookmarkGone(appState, bookmark, failed.Error);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void ReportBookmarkGone(AppState appState, Bookmark bookmark, Exception? error)
    {
        appState.ReportError(new AppProblem.BookmarkGone(bookmark.Name, bookmark.Place, error));
    }

    /// <summary>Goes to the next enabled bookmark after <paramref name="from"/>, or the previous when
    /// <paramref name="direction"/> is negative. Returns the bookmark gone to, or null when none is enabled.</summary>
    public static Bookmark? StepBookmark(AppState appState, Bookmark? from, int direction)
    {
        var bookmark = FindNext(appState.Bookmarks, from, direction);
        if (bookmark == null)
            return null;
        GoToBookmark(appState, bookmark);
        return bookmark;
    }

    public static void ExportBookmarks(AppState appState, string path)
    {
        var records = ListRecords(appState.Bookmarks);
        var error = BookmarkFile.Write(path, records);
        if (error == null)
            return;
        appState.ReportError(new AppProblem.BookmarksNotExported(error));
    }

    /// <summary>Reads the bookmarks in the file at <paramref name="path"/> — this app's or the real
    /// ILSpy's — and finds the place of each in the background: takes them at once when there are
    /// none yet, and otherwise waits in <see cref="AppState.Imports"/> for the user to say how. Bookmarks
    /// whose place is not found are left out and reported.</summary>
    public static void ImportBookmarks(AppState appState, string path) =>
        Background.Start(ImportBookmarksAsync(appState, path), appState.ReportError);

    private static async Task ImportBookmarksAsync(AppState appState, string path)
    {
        var file = Path.GetFileName(path);
        var read = BookmarkFile.Read(path, out var error);
        if (read == null)
        {
            appState.ReportError(new AppProblem.BookmarksNotImported(file, error ?? ""));
            return;
        }
        if (read.Unreadable.Count > 0)
            appState.ReportError(new AppProblem.ImportedBookmarksUnreadable(file, read.Unreadable));
        var records = read.Bookmarks;
        appState.Imports.Finding++;
        var assemblies = appState.Assemblies;
        var session = appState.Session;
        var found = await Background.Run(() => FindPlaces(assemblies, session, records));
        appState.Imports.Finding--;
        TakeImported(appState, found, file);
    }

    private static List<FoundBookmark> FindPlaces(AssemblyList assemblies, BrowseSession session, List<BookmarkRecord> records)
    {
        return records.ConvertAll(r => FindPlace(assemblies, session, r));
    }

    private static FoundBookmark FindPlace(AssemblyList assemblies, BrowseSession session, BookmarkRecord record)
    {
        if (record.Location != null)
            return new FoundBookmark(record, record.Location);
        var treePath = record.ViewState?.SelectedTreeNodePath;
        var node = ILSpyTree.Locate(assemblies, session, record.FileName, treePath, record.Token, record.MemberName);
        if (node == null)
            return new FoundBookmark(record, null);
        return new FoundBookmark(record, node.Path);
    }

    private static void TakeImported(AppState appState, Outcome<List<FoundBookmark>> found, string file)
    {
        switch (found)
        {
            case Outcome<List<FoundBookmark>>.Ok ok:
                TakeImported(appState, ok.Value, file);
                break;
            case Outcome<List<FoundBookmark>>.Failed failed:
                appState.ReportError(new AppProblem.BookmarksNotImported(file, failed.Error.ToString()));
                break;
            default:
                throw new UnreachableException();
        }
    }

    private static void TakeImported(AppState appState, List<FoundBookmark> places, string file)
    {
        ReportLostBookmarks(appState, places, file);
        var bookmarks = places
            .Select(CreateBookmark)
            .OfType<Bookmark>()
            .ToList();
        if (bookmarks.Count == 0)
            return;
        if (appState.Bookmarks.Items.Count == 0)
        {
            TakeBookmarks(appState, bookmarks, BookmarkImportMode.Replace);
            return;
        }
        appState.Imports.Waiting.Add(new PendingImport(bookmarks));
    }

    private static void ReportLostBookmarks(AppState appState, List<FoundBookmark> found, string file)
    {
        var lost = found.FindAll(f => f.Place == null);
        if (lost.Count == 0)
            return;
        var lines = lost.ConvertAll(DescribeLostBookmark);
        appState.ReportError(new AppProblem.ImportedBookmarksLost(file, lines));
    }

    private static string DescribeLostBookmark(FoundBookmark lost)
    {
        var record = lost.Record;
        var location = record.LocationNodeName ?? record.MemberName;
        return $"{record.Name}: {location} ({record.FileName})";
    }

    /// <summary>Takes the bookmarks <paramref name="import"/> waits with as <paramref name="mode"/> says,
    /// or drops them when null.</summary>
    public static void FinishBookmarkImport(AppState appState, PendingImport import, BookmarkImportMode? mode)
    {
        if (!appState.Imports.Waiting.Remove(import))
            return;
        if (mode == null)
            return;
        TakeBookmarks(appState, import.Bookmarks, mode.Value);
    }

    /// <summary>Takes <paramref name="bookmarks"/> in as <paramref name="mode"/> says. Those marking a
    /// line a bookmark marks already are left out: quietly when merging, and reported when replacing.</summary>
    private static void TakeBookmarks(AppState appState, IReadOnlyList<Bookmark> bookmarks, BookmarkImportMode mode)
    {
        var leftOut = Import(appState.Bookmarks, bookmarks, mode);
        if (mode == BookmarkImportMode.Merge)
            return;
        ReportLeftOutBookmarks(appState, leftOut);
    }

    /// <summary>Says how many bookmarks were left out because another marks the same line.</summary>
    internal static void ReportLeftOutBookmarks(AppState appState, int leftOut)
    {
        if (leftOut == 0)
            return;
        appState.ReportError(new AppProblem.BookmarksLeftOut(leftOut));
    }

    /// <summary>A bookmark read, and where it was found.</summary>
    private sealed record FoundBookmark(BookmarkRecord Record, NodePath? Place);

    /// <summary>The bookmark of <paramref name="found"/>, or null when its place wasn't found.</summary>
    private static Bookmark? CreateBookmark(FoundBookmark found)
    {
        if (found.Place == null)
            return null;
        return Bookmark.FromRecord(found.Record, found.Place);
    }
}
