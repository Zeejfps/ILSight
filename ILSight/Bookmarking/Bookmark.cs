using ILSight.Core;

namespace ILSight.Bookmarking;

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
