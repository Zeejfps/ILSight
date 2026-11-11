using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight;

/// <summary>A marked line of code: the place whose document it was made in, what names the line
/// there, and what the user calls it. With an <see cref="Anchor"/> it finds its line in any document
/// showing it, in either language; without one, only where it was made. <see cref="ILSpy"/> and
/// <see cref="ViewState"/> are what the real ILSpy keeps besides, so it can be written to its
/// bookmarks file and read back from one. What changes in it after it is made is changed by the
/// <see cref="BookmarkList"/> holding it, which counts the change.</summary>
public sealed class Bookmark
{
    private NodePath? _place;
    private string? _name;

    /// <summary>Where the document it was made in is: as it was read until the bookmark is gone to, then
    /// where its node was found.</summary>
    public required NodePath Place
    {
        get => _place ?? throw new InvalidOperationException("A bookmark was made without a place.");
        init => _place = value;
    }

    /// <summary>The language of the document it was made in.</summary>
    public required DecompilationLanguage Language { get; init; }

    /// <summary>The line it was made on, from 0.</summary>
    public required int Line { get; init; }

    public required CodeAnchor? Anchor { get; init; }

    /// <summary>What the real ILSpy records about it besides its line.</summary>
    public required ILSpyBookmarkInfo ILSpy { get; init; }

    /// <summary>ILSpy's view state, kept as it came from an ILSpy file to give back to one; null for a
    /// bookmark made here, which writes one holding only the tree path.</summary>
    public required BookmarkViewState? ViewState { get; init; }

    public required string Name
    {
        get => _name ?? throw new InvalidOperationException("A bookmark was made without a name.");
        init => _name = value;
    }

    /// <summary>A disabled bookmark stays listed and marked, dimmed, but next and previous skip it.
    /// Changed only by <see cref="BookmarkList.SetEnabled"/>.</summary>
    public bool Enabled { get; internal set; } = true;

    /// <summary>The place and line it was made on.</summary>
    public required string Location { get; init; }

    /// <summary>The file name of the assembly it is in.</summary>
    public required string Module { get; init; }

    /// <summary>Called only by <see cref="BookmarkList.Rename"/>, which counts the change.</summary>
    internal void Rename(string name)
    {
        _name = name;
    }

    /// <summary>Called only by <see cref="BookmarkList.Resolve"/>, which counts the change.</summary>
    internal void MoveTo(NodePath place)
    {
        _place = place;
    }
}

/// <summary>What importing bookmarks does with the ones there are.</summary>
public enum BookmarkImportMode
{
    /// <summary>Drops them for the imported ones.</summary>
    Replace,

    /// <summary>Keeps them, adding the imported ones that mark a line none of them does.</summary>
    Merge,
}

/// <summary>The user's bookmarks in the order they were made. No two mark the same line, and
/// <see cref="Revision"/> changes whenever the list or a bookmark in it does: the list is changed only
/// through its methods, which keep both.</summary>
public sealed class BookmarkList
{
    /// <summary>The lines of a document that bookmarks mark, as of <see cref="Revision"/>.</summary>
    private sealed class Marks
    {
        public int Revision = -1;
        public NodePath? Place;
        public readonly Dictionary<int, bool> Lines = new();
    }

    private readonly TrackedList<Bookmark> _items = new();

    private readonly ConditionalWeakTable<DecompiledDocument, Marks> _marksByDocument = new();

    /// <summary>The bookmarks, in the order they were made.</summary>
    public IReadOnlyList<Bookmark> Items => _items;

    /// <summary>Changes whenever the list or a bookmark in it does.</summary>
    public int Revision => _items.Revision;

    /// <summary>Adds <paramref name="bookmark"/>, unless a bookmark marks its line already; returns
    /// whether it was added.</summary>
    public bool Add(Bookmark bookmark)
    {
        if (IsMarked(bookmark))
        {
            return false;
        }

        _items.Add(bookmark);
        return true;
    }

    private bool IsMarked(Bookmark bookmark)
    {
        return _items.Any(other => Bookmarks.MarkSameLine(bookmark, other));
    }

    /// <summary>Removes <paramref name="bookmark"/>; returns the row it was in, or -1 when it was not listed.</summary>
    public int Remove(Bookmark bookmark)
    {
        var index = _items.IndexOf(bookmark);
        if (index < 0)
        {
            return -1;
        }

        _items.RemoveAt(index);
        return index;
    }

    public void SetEnabled(Bookmark bookmark, bool enabled)
    {
        if (bookmark.Enabled == enabled)
        {
            return;
        }

        bookmark.Enabled = enabled;
        _items.NoteChange();
    }

    /// <summary>Gives <paramref name="bookmark"/> the name <paramref name="name"/>, trimmed, unless it is blank.</summary>
    public void Rename(Bookmark bookmark, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (trimmed == bookmark.Name)
        {
            return;
        }

        bookmark.Rename(trimmed);
        _items.NoteChange();
    }

    /// <summary>Takes <paramref name="node"/>, found where <paramref name="bookmark"/> was made, as its place,
    /// so that it is saved with the labels the node shows now.</summary>
    public void Resolve(Bookmark bookmark, BrowseNode node)
    {
        bookmark.MoveTo(BrowseNodes.GetPath(node));
        _items.NoteChange();
    }

    /// <summary>Adds <paramref name="bookmarks"/>, after dropping every bookmark first when replacing,
    /// leaving out the ones that mark a line already marked. Returns how many were left out.</summary>
    public int Import(IEnumerable<Bookmark> bookmarks, BookmarkImportMode mode)
    {
        if (mode == BookmarkImportMode.Replace)
        {
            _items.Clear();
        }

        var leftOut = 0;
        foreach (var bookmark in bookmarks)
        {
            if (IsMarked(bookmark))
            {
                leftOut++;
                continue;
            }
            _items.Add(bookmark);
        }
        return leftOut;
    }

    /// <summary>Replaces the list with the bookmarks in <paramref name="saved"/> that know their place.
    /// Returns how many were left out because one before them marks the same line.</summary>
    public int Restore(IEnumerable<BookmarkRecord>? saved)
    {
        var bookmarks = (saved ?? []).Select(static r => r.Location is not null ? Bookmarks.CreateFromRecord(r, r.Location) : null)
            .OfType<Bookmark>();
        return Import(bookmarks, BookmarkImportMode.Replace);
    }

    /// <summary>The next enabled bookmark after <paramref name="from"/>, or the previous when
    /// <paramref name="direction"/> is negative, wrapping around; null when none is enabled.</summary>
    public Bookmark? FindNext(Bookmark? from, int direction)
    {
        var count = _items.Count;
        var start = GetStepStart(from, direction);
        for (var step = 1; step <= count; step++)
        {
            var index = ((start + Math.Sign(direction) * step) % count + count) % count;
            if (!_items[index].Enabled)
            {
                continue;
            }

            return _items[index];
        }
        return null;
    }

    /// <summary>The row stepping starts from: <paramref name="from"/>'s, or just before the first or
    /// after the last.</summary>
    private int GetStepStart(Bookmark? from, int direction)
    {
        var index = FindRow(from);
        if (index is not null)
        {
            return index.Value;
        }

        if (direction > 0)
        {
            return -1;
        }

        return _items.Count;
    }

    private int? FindRow(Bookmark? bookmark)
    {
        if (bookmark is null)
        {
            return null;
        }

        var index = _items.IndexOf(bookmark);
        if (index < 0)
        {
            return null;
        }

        return index;
    }

    /// <summary>Whether a bookmark is enabled. Runs every frame the panel shows, so it goes through the
    /// bookmarks itself: LINQ over the list would allocate its enumerator.</summary>
    public bool HasEnabled()
    {
        foreach (var bookmark in _items)
        {
            if (bookmark.Enabled)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The first bookmark marking <paramref name="line"/> of <paramref name="document"/>,
    /// shown for <paramref name="node"/>.</summary>
    public Bookmark? FindAt(DecompiledDocument document, BrowseNode node, int line)
    {
        foreach (var bookmark in _items)
        {
            var bookmarkLine = Bookmarks.GetLine(bookmark, document, node);
            if (bookmarkLine == line)
            {
                return bookmark;
            }
        }
        return null;
    }

    /// <summary>The lines of <paramref name="document"/>, shown for <paramref name="node"/>, that
    /// bookmarks mark, each with whether one marking it is enabled. Runs every frame a document shows.</summary>
    public IReadOnlyDictionary<int, bool> ListMarkedLines(DecompiledDocument document, BrowseNode node)
    {
        var marks = _marksByDocument.GetOrCreateValue(document);
        var place = BrowseNodes.GetPath(node);
        if (marks.Revision == Revision && Equals(marks.Place, place))
        {
            return marks.Lines;
        }

        marks.Revision = Revision;
        marks.Place = place;
        marks.Lines.Clear();
        foreach (var bookmark in _items)
        {
            var line = Bookmarks.GetLine(bookmark, document, node);
            if (line >= 0)
            {
                marks.Lines[line] = marks.Lines.GetValueOrDefault(line) || bookmark.Enabled;
            }
        }
        return marks.Lines;
    }

    /// <summary>The first of <paramref name="format"/>(1), (2), … that no bookmark is called.</summary>
    public string CreateName(Func<int, string> format)
    {
        for (var n = 1; ; n++)
        {
            var name = format(n);
            if (!_items.Any(b => b.Name == name))
            {
                return name;
            }
        }
    }

    public List<BookmarkRecord> ListRecords()
    {
        return _items.Select(static b => Bookmarks.CreateRecord(b)).ToList();
    }
}
