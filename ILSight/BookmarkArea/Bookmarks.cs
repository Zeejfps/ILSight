using System.Diagnostics;
using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Marking lines with bookmarks, and making bookmarks and the records the bookmarks file keeps of them.
/// Going to them is <see cref="GoToBookmarkHandler"/>'s, importing them
/// <see cref="BookmarkImporting"/>'s and exporting them <see cref="ExportBookmarksHandler"/>'s.</summary>
public static class Bookmarks
{
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

    private static BookmarkToggle RemoveBookmark(AppState appState, Bookmark bookmark)
    {
        var row = appState.Bookmarks.Remove(bookmark);
        if (row is null)
        {
            throw new UnreachableException($"Bookmark '{bookmark.Name}', found marking the line, was not listed.");
        }

        return new BookmarkToggle.Removed { Bookmark = bookmark, Row = row.Value };
    }

    private static bool CanBookmarkLine(DocumentTab tab, int line)
    {
        if (tab.Load.IsBusy)
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
        var info = Describe(node, anchor, tab.Document);
        var bookmark = appState.Bookmarks.CreateAt(node, tab.Load.Language, line, anchor, name, info);
        appState.Bookmarks.TryAdd(bookmark);
        return new BookmarkToggle.Added(bookmark);
    }

    /// <summary>What ILSpy records about a bookmark on a line of <paramref name="place"/>'s
    /// <paramref name="document"/> named by <paramref name="anchor"/>, or by none: then the token is that of the
    /// entity the place stands for, as ILSpy falls back to. Reads the values the node and the document kept: the
    /// anchor's entity is named as the decompile found it.</summary>
    internal static ILSpyBookmarkInfo Describe(BrowseNode place, CodeAnchor? anchor, DecompiledDocument document)
    {
        var assembly = place.Assembly;
        var token = place.Token is null ? 0u : (uint)place.Token.Value;
        var entityName = place.EntityFullName;
        if (anchor is not null)
        {
            token = (uint)anchor.Value.Token;
            entityName = FindAnchoredName(document, anchor.Value, assembly.FilePath) ?? entityName;
        }

        var location = place.EntityFullName ?? place.ILSpyName;
        return new ILSpyBookmarkInfo
        {
            AssemblyFullName = assembly.FullName,
            ModuleName = assembly.FilePath.FileName,
            Token = token,
            MemberName = entityName ?? location ?? place.Text,
            LocationNodeName = location,
            TreePath = ListILSpyPath(place),
        };
    }

    private static string? FindAnchoredName(DecompiledDocument document, CodeAnchor anchor, AssemblyPath filePath)
    {
        if (!anchor.FilePath.Equals(filePath))
        {
            return null;
        }

        return document.EntityNames.GetValueOrDefault((anchor.FilePath, anchor.Token));
    }

    // ILSpy's tree finds a node by the names it gives the nodes down from the assembly.
    private static IReadOnlyList<string>? ListILSpyPath(BrowseNode node)
    {
        var path = new List<string>();
        for (var n = node; n is not null; n = n.Parent)
        {
            var name = n.ILSpyName;
            if (name is null)
            {
                return null;
            }

            path.Add(name);
        }
        path.Reverse();
        return path;
    }

    private static Bookmark CreateBookmark(BookmarkState state, DecompilationLanguage language, int line, CodeAnchor? anchor,
        ILSpyBookmarkInfo ilspy, BookmarkViewState? viewState)
    {
        var place = state.Place;
        return new Bookmark(state)
        {
            Language = language,
            Line = line,
            Anchor = anchor,
            ILSpy = ilspy,
            ViewState = viewState,
            Location = $"{place.Label}:{line + 1}",
            Module = place.FilePath.FileName,
        };
    }

    /// <summary>The bookmark made in a document, at <paramref name="state"/>'s place, which the list holding
    /// <paramref name="state"/> makes.</summary>
    public static Bookmark CreateAt(BookmarkState state, DecompilationLanguage language, int line, CodeAnchor? anchor, ILSpyBookmarkInfo ilspy)
    {
        return CreateBookmark(state, language, line, anchor, ilspy, null);
    }

    /// <summary>The bookmark <paramref name="record"/> describes, made in the document of <paramref name="state"/>'s
    /// place, which the list holding <paramref name="state"/> makes.</summary>
    public static Bookmark CreateFromRecord(BookmarkState state, BookmarkRecord record)
    {
        CodeAnchor? anchor = record.Kind switch
        {
            BookmarkKind.Token => new CodeAnchor
            {
                Kind = CodeAnchorKind.Definition,
                FilePath = record.FileName,
                Token = (int)record.Token,
            },
            BookmarkKind.Body => new CodeAnchor
            {
                Kind = CodeAnchorKind.Statement,
                FilePath = record.FileName,
                Token = (int)record.Token,
                ILOffset = record.ILOffset,
            },
            _ => null,
        };
        var ilspy = new ILSpyBookmarkInfo
        {
            AssemblyFullName = record.AssemblyFullName,
            ModuleName = record.ModuleName,
            Token = record.Token,
            MemberName = record.MemberName,
            LocationNodeName = record.LocationNodeName,
            TreePath = record.ViewState?.SelectedTreeNodePath,
        };
        return CreateBookmark(state, record.Language ?? DecompilationLanguage.CSharp, Math.Max(0, record.LineNumber - 1), anchor,
            ilspy, record.ViewState);
    }

    /// <summary>This bookmark as ILSpy's bookmarks file holds it, with this app's place and language.</summary>
    public static BookmarkRecord CreateRecord(Bookmark bookmark)
    {
        return new BookmarkRecord
        {
            Name = bookmark.Name,
            Enabled = bookmark.Enabled,
            FileName = bookmark.Anchor?.FilePath ?? bookmark.Place.FilePath,
            AssemblyFullName = bookmark.ILSpy.AssemblyFullName,
            ModuleName = bookmark.ILSpy.ModuleName,
            Token = bookmark.ILSpy.Token,
            Kind = bookmark.Anchor?.Kind switch
            {
                CodeAnchorKind.Definition => BookmarkKind.Token,
                CodeAnchorKind.Statement => BookmarkKind.Body,
                _ => BookmarkKind.Line,
            },
            ILOffset = bookmark.Anchor is not null && bookmark.Anchor.Value.Kind == CodeAnchorKind.Statement ? bookmark.Anchor.Value.ILOffset : 0,
            LineNumber = bookmark.Line + 1,
            MemberName = bookmark.ILSpy.MemberName,
            LocationNodeName = bookmark.ILSpy.LocationNodeName,
            ViewState = bookmark.ViewState ?? new BookmarkViewState
            {
                Version = 1,
                CaretOffset = 0,
                VerticalOffset = 0d,
                HorizontalOffset = 0d,
                FoldingChecksum = null,
                ExpandedFoldings = null,
                RenderedLayoutSettings = null,
                SelectedTreeNodePath = bookmark.ILSpy.TreePath?.ToList(),
            },
            Location = bookmark.Place,
            Language = bookmark.Language,
        };
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

        var place = node.Path;
        if (!bookmark.Place.Equals(place) || bookmark.Line >= document.Lines.Length)
        {
            return null;
        }

        return bookmark.Line;
    }
}
