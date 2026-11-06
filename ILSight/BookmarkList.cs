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
    public Bookmark(NodePath place, DecompilationLanguage language, int line, CodeAnchor? anchor, string name,
        ILSpyBookmarkInfo ilspy, BookmarkViewState? viewState = null, bool enabled = true)
    {
        Place = place;
        Language = language;
        Line = line;
        Anchor = anchor;
        Name = name;
        ILSpy = ilspy;
        ViewState = viewState;
        Enabled = enabled;
        Location = $"{place.Label}:{line + 1}";
        Module = place.FilePath.FileName;
    }

    /// <summary>The bookmark made in the document of <paramref name="node"/>.</summary>
    public static Bookmark At(BrowseNode node, DecompilationLanguage language, int line, CodeAnchor? anchor, string name, ILSpyBookmarkInfo ilspy) =>
        new(node.Path, language, line, anchor, name, ilspy);

    /// <summary>The bookmark <paramref name="record"/> describes, made in the document of <paramref name="place"/>.</summary>
    public static Bookmark FromRecord(BookmarkRecord record, NodePath place)
    {
        CodeAnchor? anchor = record.Kind switch
        {
            BookmarkKind.Token => new CodeAnchor(CodeAnchorKind.Definition, record.FileName, (int)record.Token),
            BookmarkKind.Body => new CodeAnchor(CodeAnchorKind.Statement, record.FileName, (int)record.Token, record.ILOffset),
            _ => null,
        };
        var ilspy = new ILSpyBookmarkInfo(record.AssemblyFullName, record.ModuleName, record.Token,
            record.MemberName, record.LocationNodeName, record.ViewState?.SelectedTreeNodePath);
        return new Bookmark(place, record.Language ?? DecompilationLanguage.CSharp, Math.Max(0, record.LineNumber - 1), anchor,
            record.Name, ilspy, record.ViewState, record.Enabled);
    }

    /// <summary>This bookmark as ILSpy's bookmarks file holds it, with this app's place and language.</summary>
    public BookmarkRecord ToRecord() => new(
        Name,
        Enabled,
        Anchor?.FilePath ?? Place.FilePath,
        ILSpy.AssemblyFullName,
        ILSpy.ModuleName,
        ILSpy.Token,
        Anchor?.Kind switch
        {
            CodeAnchorKind.Definition => BookmarkKind.Token,
            CodeAnchorKind.Statement => BookmarkKind.Body,
            _ => BookmarkKind.Line,
        },
        Anchor is { Kind: CodeAnchorKind.Statement } statement ? statement.ILOffset : 0,
        Line + 1,
        ILSpy.MemberName,
        ILSpy.LocationNodeName,
        ViewState ?? new BookmarkViewState(1, 0, 0d, 0d, null, null, null, ILSpy.TreePath?.ToList()),
        Place,
        Language);

    /// <summary>Where the document it was made in is: as it was read until the bookmark is gone to, then
    /// where its node was found.</summary>
    public NodePath Place { get; internal set; }

    /// <summary>The language of the document it was made in.</summary>
    public DecompilationLanguage Language { get; }

    /// <summary>The line it was made on, from 0.</summary>
    public int Line { get; }

    public CodeAnchor? Anchor { get; }

    /// <summary>What the real ILSpy records about it besides its line.</summary>
    public ILSpyBookmarkInfo ILSpy { get; }

    /// <summary>ILSpy's view state, kept as it came from an ILSpy file to give back to one; null for a
    /// bookmark made here, which writes one holding only the tree path.</summary>
    public BookmarkViewState? ViewState { get; }

    public string Name { get; internal set; }

    /// <summary>A disabled bookmark stays listed and marked, dimmed, but next and previous skip it.</summary>
    public bool Enabled { get; internal set; }

    /// <summary>The place and line it was made on.</summary>
    public string Location { get; }

    /// <summary>The file name of the assembly it is in.</summary>
    public string Module { get; }

    /// <summary>Whether <paramref name="other"/> marks the same line: the same anchor, or for one
    /// without, the same line of the same place in the same language.</summary>
    public bool MarksSameLine(Bookmark other) =>
        Anchor != null ? other.Anchor == Anchor
        : other.Anchor == null && Language == other.Language && Line == other.Line && Place.Equals(other.Place);

    /// <summary>The line it marks in <paramref name="document"/>, shown for <paramref name="node"/>, or -1.</summary>
    public int FindLine(DecompiledDocument document, BrowseNode node) =>
        Anchor != null ? document.FindLine(Anchor.Value)
        : Language == document.Language && Place.Equals(node.Path) && Line < document.Lines.Length ? Line
        : -1;
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
