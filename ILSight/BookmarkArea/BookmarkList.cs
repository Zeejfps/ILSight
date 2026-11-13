using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>The user's bookmarks in the order they were made. No two mark the same line, and
/// <see cref="Revision"/> changes whenever the list or a bookmark in it does: the list is changed only
/// through its methods, which keep both. It makes the bookmarks it holds (<see cref="CreateAt"/>,
/// <see cref="CreateFromRecord"/>) and holds what changes in each (<see cref="BookmarkState"/>), which the bookmark
/// handed out only reads.</summary>
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

    /// <summary>What changes in each bookmark made here, kept as long as the bookmark is.</summary>
    private readonly ConditionalWeakTable<Bookmark, BookmarkState> _states = new();

    /// <summary>The bookmarks, in the order they were made.</summary>
    public IReadOnlyList<Bookmark> Items => _items;

    /// <summary>Changes whenever the list or a bookmark in it does.</summary>
    public int Revision => _items.Revision;

    /// <summary>The bookmark called <paramref name="name"/> made in the document of <paramref name="node"/>, to
    /// add here.</summary>
    public Bookmark CreateAt(BrowseNode node, DecompilationLanguage language, int line, CodeAnchor? anchor, string name, ILSpyBookmarkInfo ilspy)
    {
        var state = new BookmarkState { Place = node.Path, Name = name, Enabled = true };
        return Hold(state, Bookmarks.CreateAt(state, language, line, anchor, ilspy));
    }

    /// <summary>The bookmark <paramref name="record"/> describes, made in the document of <paramref name="place"/>,
    /// to add here.</summary>
    public Bookmark CreateFromRecord(BookmarkRecord record, NodePath place)
    {
        var state = new BookmarkState { Place = place, Name = record.Name, Enabled = record.Enabled };
        return Hold(state, Bookmarks.CreateFromRecord(state, record));
    }

    private Bookmark Hold(BookmarkState state, Bookmark bookmark)
    {
        _states.Add(bookmark, state);
        return bookmark;
    }

    /// <summary>What changes in <paramref name="bookmark"/>, made here.</summary>
    private BookmarkState GetState(Bookmark bookmark)
    {
        if (!_states.TryGetValue(bookmark, out var state))
        {
            throw new InvalidOperationException($"Bookmark '{bookmark.Name}' was not made by this list.");
        }

        return state;
    }

    /// <summary>Throws unless <paramref name="bookmark"/> was made here, so that the list holds what changes in each
    /// bookmark it lists.</summary>
    private void ThrowIfNotMadeHere(Bookmark bookmark)
    {
        if (!_states.TryGetValue(bookmark, out _))
        {
            throw new InvalidOperationException($"Bookmark '{bookmark.Name}' was not made by this list.");
        }
    }

    /// <summary>Adds <paramref name="bookmark"/>, made here, unless a bookmark marks its line already; returns
    /// whether it was added.</summary>
    public bool TryAdd(Bookmark bookmark)
    {
        ThrowIfNotMadeHere(bookmark);
        if (IsMarked(bookmark))
        {
            return false;
        }

        _items.Add(bookmark);
        return true;
    }

    private bool IsMarked(Bookmark bookmark)
    {
        return _items.Any(other => Bookmarks.MarksSameLine(bookmark, other));
    }

    /// <summary>Removes <paramref name="bookmark"/>; returns the row it was in, or null when it was not listed.</summary>
    public int? Remove(Bookmark bookmark)
    {
        var index = FindRow(bookmark);
        if (index is null)
        {
            return null;
        }

        _items.RemoveAt(index.Value);
        return index;
    }

    public void SetEnabled(Bookmark bookmark, bool enabled)
    {
        var state = GetState(bookmark);
        if (state.Enabled == enabled)
        {
            return;
        }

        state.Enabled = enabled;
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

        GetState(bookmark).Name = trimmed;
        _items.NoteChange();
    }

    /// <summary>Takes <paramref name="node"/>, found where <paramref name="bookmark"/> was made, as its place,
    /// so that it is saved with the labels the node shows now.</summary>
    public void Resolve(Bookmark bookmark, BrowseNode node)
    {
        GetState(bookmark).Place = node.Path;
        _items.NoteChange();
    }

    /// <summary>Adds <paramref name="bookmarks"/>, made here, after dropping every bookmark first when replacing,
    /// leaving out the ones that mark a line already marked. Returns how many were left out.</summary>
    public int Import(IEnumerable<Bookmark> bookmarks, BookmarkImportMode mode)
    {
        var adding = bookmarks.ToList();
        foreach (var bookmark in adding)
        {
            ThrowIfNotMadeHere(bookmark);
        }

        if (mode == BookmarkImportMode.Replace)
        {
            _items.Clear();
        }

        var leftOut = 0;
        foreach (var bookmark in adding)
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
        var bookmarks = (saved ?? []).Select(r => r.Location is not null ? CreateFromRecord(r, r.Location) : null)
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
            var bookmarkLine = Bookmarks.FindLine(bookmark, document, node);
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
        var place = node.Path;
        if (marks.Revision == Revision && Equals(marks.Place, place))
        {
            return marks.Lines;
        }

        marks.Revision = Revision;
        marks.Place = place;
        marks.Lines.Clear();
        foreach (var bookmark in _items)
        {
            var line = Bookmarks.FindLine(bookmark, document, node);
            if (line is not null)
            {
                marks.Lines[line.Value] = marks.Lines.GetValueOrDefault(line.Value) || bookmark.Enabled;
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
