using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>Marking lines with bookmarks, the bookmarks panel's selection and rename, and making bookmarks and the
/// records the bookmarks file keeps of them. Going to them is <see cref="GoToBookmarkHandler"/>'s, importing them
/// <see cref="BookmarkImporting"/>'s and exporting them <see cref="ExportBookmarksHandler"/>'s.</summary>
public static class Bookmarks
{
    /// <summary>Selects the bookmark a toggle added, or passes the selection of the one it removed to
    /// the bookmark now in its row.</summary>
    public static void TakeToggle(BookmarksPanel panel, BookmarkToggle toggle, IReadOnlyList<Bookmark> bookmarks)
    {
        switch (toggle)
        {
            case BookmarkToggle.Added added:
                ItemSelections.Select(panel.Selection, added.Bookmark, bookmarks);
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
        {
            panel.Renaming = null;
        }

        if (panel.Selection.Item != bookmark)
        {
            return;
        }

        SelectInRow(panel, row, bookmarks);
    }

    /// <summary>Selects the bookmark in <paramref name="row"/>, or the last when there are fewer.</summary>
    private static void SelectInRow(BookmarksPanel panel, int row, IReadOnlyList<Bookmark> bookmarks)
    {
        var kept = Math.Min(row, bookmarks.Count - 1);
        if (kept < 0)
        {
            ItemSelections.Clear(panel.Selection);
            return;
        }
        ItemSelections.Select(panel.Selection, bookmarks[kept], bookmarks, reveal: false);
    }

    /// <summary>Forgets the rename of a bookmark that is gone, as importing in its place drops it.</summary>
    public static void FollowList(BookmarksPanel panel, IReadOnlyList<Bookmark> bookmarks)
    {
        if (panel.Renaming is null)
        {
            return;
        }

        if (bookmarks.Contains(panel.Renaming))
        {
            return;
        }

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
        if (node is null)
        {
            return new BookmarkToggle.Unchanged();
        }

        if (!CanBookmarkLine(tab, line))
        {
            return new BookmarkToggle.Unchanged();
        }

        var existing = appState.Bookmarks.FindAt(tab.Document, node, line);
        if (existing is not null)
        {
            return RemoveBookmark(appState, existing);
        }

        return AddBookmark(appState, tab, node, line, nameOf);
    }

    /// <summary>Removes <paramref name="bookmark"/>, which marks the line, so is listed.</summary>
    private static BookmarkToggle RemoveBookmark(AppState appState, Bookmark bookmark)
    {
        var row = appState.Bookmarks.Remove(bookmark);
        if (row is null)
        {
            throw new UnreachableException($"Bookmark '{bookmark.Name}', found marking the line, was not listed.");
        }

        return new BookmarkToggle.Removed(bookmark, row.Value);
    }

    private static bool CanBookmarkLine(DocumentTab tab, int line)
    {
        if (tab.IsBusy)
        {
            return false;
        }

        if (tab.Document.Error is not null)
        {
            return false;
        }

        if (line < 0)
        {
            return false;
        }

        return line < tab.Document.Lines.Length;
    }

    private static BookmarkToggle AddBookmark(AppState appState, DocumentTab tab, BrowseNode node, int line, Func<int, string> nameOf)
    {
        var anchor = Documents.FindAnchorAt(tab.Document, line);
        var name = appState.Bookmarks.CreateName(nameOf);
        var info = ILSpyTree.Describe(node, anchor);
        var bookmark = Bookmarks.CreateAt(node, tab.Language, line, anchor, name, info);
        appState.Bookmarks.TryAdd(bookmark);
        return new BookmarkToggle.Added(bookmark);
    }

    private static Bookmark CreateBookmark(NodePath place, DecompilationLanguage language, int line, CodeAnchor? anchor, string name,
        ILSpyBookmarkInfo ilspy, BookmarkViewState? viewState, bool enabled)
    {
        return new Bookmark
        {
            Place = place,
            Language = language,
            Line = line,
            Anchor = anchor,
            Name = name,
            ILSpy = ilspy,
            ViewState = viewState,
            Enabled = enabled,
            Location = $"{place.Label}:{line + 1}",
            Module = place.FilePath.FileName,
        };
    }

    /// <summary>The bookmark made in the document of <paramref name="node"/>.</summary>
    public static Bookmark CreateAt(BrowseNode node, DecompilationLanguage language, int line, CodeAnchor? anchor, string name, ILSpyBookmarkInfo ilspy)
    {
        return CreateBookmark(BrowseNodes.GetPath(node), language, line, anchor, name, ilspy, null, true);
    }

    /// <summary>The bookmark <paramref name="record"/> describes, made in the document of <paramref name="place"/>.</summary>
    public static Bookmark CreateFromRecord(BookmarkRecord record, NodePath place)
    {
        CodeAnchor? anchor = record.Kind switch
        {
            BookmarkKind.Token => new CodeAnchor(CodeAnchorKind.Definition, record.FileName, (int)record.Token),
            BookmarkKind.Body => new CodeAnchor(CodeAnchorKind.Statement, record.FileName, (int)record.Token, record.ILOffset),
            _ => null,
        };
        var ilspy = new ILSpyBookmarkInfo(record.AssemblyFullName, record.ModuleName, record.Token,
            record.MemberName, record.LocationNodeName, record.ViewState?.SelectedTreeNodePath);
        return CreateBookmark(place, record.Language ?? DecompilationLanguage.CSharp, Math.Max(0, record.LineNumber - 1), anchor,
            record.Name, ilspy, record.ViewState, record.Enabled);
    }

    /// <summary>This bookmark as ILSpy's bookmarks file holds it, with this app's place and language.</summary>
    public static BookmarkRecord CreateRecord(Bookmark bookmark)
    {
        return new BookmarkRecord(
        bookmark.Name,
        bookmark.Enabled,
        bookmark.Anchor?.FilePath ?? bookmark.Place.FilePath,
        bookmark.ILSpy.AssemblyFullName,
        bookmark.ILSpy.ModuleName,
        bookmark.ILSpy.Token,
        bookmark.Anchor?.Kind switch
        {
            CodeAnchorKind.Definition => BookmarkKind.Token,
            CodeAnchorKind.Statement => BookmarkKind.Body,
            _ => BookmarkKind.Line,
        },
        bookmark.Anchor is not null && bookmark.Anchor.Value.Kind == CodeAnchorKind.Statement ? bookmark.Anchor.Value.ILOffset : 0,
        bookmark.Line + 1,
        bookmark.ILSpy.MemberName,
        bookmark.ILSpy.LocationNodeName,
        bookmark.ViewState ?? new BookmarkViewState(1, 0, 0d, 0d, null, null, null, bookmark.ILSpy.TreePath?.ToList()),
        bookmark.Place,
        bookmark.Language);
    }

    /// <summary>Whether <paramref name="other"/> marks the same line: the same anchor, or for one
    /// without, the same line of the same place in the same language.</summary>
    public static bool MarksSameLine(Bookmark bookmark, Bookmark other)
    {
        return bookmark.Anchor is not null ? other.Anchor == bookmark.Anchor
        : other.Anchor is null && bookmark.Language == other.Language && bookmark.Line == other.Line && bookmark.Place.Equals(other.Place);
    }

    /// <summary>The line it marks in <paramref name="document"/>, shown for <paramref name="node"/>; null
    /// when it marks none there.</summary>
    public static int? FindLine(Bookmark bookmark, DecompiledDocument document, BrowseNode node)
    {
        if (bookmark.Anchor is not null)
        {
            return Documents.FindLine(document, bookmark.Anchor.Value);
        }

        if (bookmark.Language != document.Language)
        {
            return null;
        }

        var place = BrowseNodes.GetPath(node);
        if (!bookmark.Place.Equals(place) || bookmark.Line >= document.Lines.Length)
        {
            return null;
        }

        return bookmark.Line;
    }
}
