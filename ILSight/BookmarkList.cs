using System.Runtime.CompilerServices;
using ILSight.Core;

namespace ILSight;

/// <summary>A marked line of code: the place whose document it was made in, what names the line
/// there, and what the user calls it. With an <see cref="Anchor"/> it finds its line in any document
/// showing it, in either language; without one, only where it was made. <see cref="ILSpy"/> and
/// <see cref="ViewState"/> are what the real ILSpy keeps besides, so it can be written to its
/// bookmarks file and read back from one.</summary>
public sealed class Bookmark
{
    /// <summary>Where the document it was made in is: as it was read until the bookmark is gone to, then
    /// where its node was found.</summary>
    public required NodePath Place { get; set; }

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

    public required string Name { get; set; }

    /// <summary>A disabled bookmark stays listed and marked, dimmed, but next and previous skip it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The place and line it was made on.</summary>
    public required string Location { get; init; }

    /// <summary>The file name of the assembly it is in.</summary>
    public required string Module { get; init; }
}

/// <summary>What importing bookmarks does with the ones there are.</summary>
public enum BookmarkImportMode
{
    /// <summary>Drops them for the imported ones.</summary>
    Replace,

    /// <summary>Keeps them, adding the imported ones that mark a line none of them does.</summary>
    Merge,
}

/// <summary>The user's bookmarks in the order they were made. No two mark the same line, and every change
/// to the list or a bookmark bumps <see cref="Revision"/>: both are up to the functions on
/// <see cref="Bookmarks"/>, which are the only things to change it.</summary>
public sealed class BookmarkList
{
    /// <summary>The lines of a document that bookmarks mark, as of <see cref="Revision"/>.</summary>
    public sealed class Marks
    {
        public int Revision = -1;
        public NodePath? Place;
        public readonly Dictionary<int, bool> Lines = new();
    }

    public readonly List<Bookmark> Items = new();

    public readonly ConditionalWeakTable<DecompiledDocument, Marks> MarksByDocument = new();

    public int Revision;
}
