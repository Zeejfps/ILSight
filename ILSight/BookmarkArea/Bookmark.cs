using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>A marked line of code: the place whose document it was made in, what names the line
/// there, and what the user calls it. With an <see cref="Anchor"/> it finds its line in any document
/// showing it, in either language; without one, only where it was made. <see cref="ILSpy"/> and
/// <see cref="ViewState"/> are what the real ILSpy keeps besides, so it can be written to its
/// bookmarks file and read back from one. What changes in it after it is made (<see cref="BookmarkState"/>) is
/// changed only by the <see cref="BookmarkList"/> that made it, which holds that state and counts the change; the
/// bookmark only reads it.</summary>
public sealed class Bookmark
{
    private readonly BookmarkState _state;

    /// <param name="state">What changes in it after it is made, which only the list that made it holds.</param>
    public Bookmark(BookmarkState state)
    {
        _state = state;
    }

    /// <summary>Where the document it was made in is: as it was read until the bookmark is gone to, then
    /// where its node was found.</summary>
    public NodePath Place => _state.Place;

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

    public string Name => _state.Name;

    /// <summary>A disabled bookmark stays listed and marked, dimmed, but next and previous skip it.
    /// Changed only by <see cref="BookmarkList.SetEnabled"/>.</summary>
    public bool Enabled => _state.Enabled;

    /// <summary>The place and line it was made on.</summary>
    public required string Location { get; init; }

    /// <summary>The file name of the assembly it is in.</summary>
    public required string Module { get; init; }
}
