using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>Something the user asked for that the app refused, and why: said where they acted, in the status bar
/// (<see cref="RefusalLine"/>), worded in the language the screen shows (<see cref="Words"/>). A refusal is no failure:
/// it isn't added to the errors. What the app tells unasked, with no place the user acted (arguments a later run
/// handed over), is said there too.</summary>
public abstract record Refusal
{
    private Refusal()
    {
    }

    /// <summary>The resource <see cref="Resource"/> was not saved: its assembly, the file <see cref="Assembly"/>, was
    /// closed before it was read.</summary>
    public sealed record ResourceNotSaved : Refusal
    {
        public required string Resource { get; init; }

        public required string Assembly { get; init; }
    }

    /// <summary>The PDB <see cref="Pdb"/> was not read for the assembly, the file <see cref="Assembly"/>: it was closed
    /// before it was read.</summary>
    public sealed record PdbNotRead : Refusal
    {
        public required string Pdb { get; init; }

        public required string Assembly { get; init; }
    }

    /// <summary>The file at <see cref="Path"/>, dropped on the window, was not opened: it is not a full path.</summary>
    public sealed record NotFullPath : Refusal
    {
        public required string Path { get; init; }
    }

    /// <summary>What was asked would read <see cref="Assembly"/>, the file a tab shows closed
    /// (<see cref="TabLoad.ShowClosed"/>): a view of a closed assembly asks for nothing more.</summary>
    public sealed record AssemblyClosed : Refusal
    {
        public required string Assembly { get; init; }
    }

    /// <summary>Analyzing <see cref="Symbol"/> was asked for, but its assembly isn't on the list and nothing names its
    /// definition to list it by.</summary>
    public sealed record NotAnalyzable : Refusal
    {
        public required string Symbol { get; init; }
    }

    /// <summary>No open assembly has what the documentation id <see cref="Id"/> names.</summary>
    public sealed record IdNotFound : Refusal
    {
        public required string Id { get; init; }
    }

    /// <summary>The command line's <see cref="Arguments"/> asked for nothing the app can do.</summary>
    public sealed record ArgumentsIgnored : Refusal
    {
        public required IReadOnlyList<string> Arguments { get; init; }
    }

    /// <summary>The type or member <see cref="Name"/> has no page in the .NET API reference.</summary>
    public sealed record NoApiReferencePage : Refusal
    {
        public required string Name { get; init; }
    }

    /// <summary>What <see cref="Reference"/> points at was not found.</summary>
    public sealed record Unresolved : Refusal
    {
        public required CodeReference Reference { get; init; }
    }

    /// <summary>The place a history entry shows, <see cref="Place"/>, was not found.</summary>
    public sealed record HistoryPlaceGone : Refusal
    {
        public required NodePath Place { get; init; }
    }

    /// <summary>The place the bookmark <see cref="Bookmark"/> marks, <see cref="Place"/>, was not found.</summary>
    public sealed record BookmarkGone : Refusal
    {
        public required string Bookmark { get; init; }

        public required NodePath Place { get; init; }
    }

    /// <summary>A diagram was asked for <see cref="Type"/>, which is no type that can be read.</summary>
    public sealed record TypeNotDiagrammed : Refusal
    {
        public required CodeReference.Entity Type { get; init; }
    }

    /// <summary>The PDB at <see cref="PdbPath"/>, picked for the assembly <see cref="Assembly"/>, was read and doesn't
    /// match it (<see cref="Check"/>: another build's, or a Windows PDB), so it wasn't used.</summary>
    public sealed record PdbUnusable : Refusal
    {
        public required string PdbPath { get; init; }

        public required string Assembly { get; init; }

        public required PdbCheck Check { get; init; }
    }

    /// <summary>The file at <see cref="Path"/>, given to import bookmarks from, holds no bookmarks list.</summary>
    public sealed record NotBookmarksFile : Refusal
    {
        public required string Path { get; init; }
    }

    /// <summary>The places of the bookmarks <see cref="Lost"/> describes, read from <see cref="File"/>, were not found, so
    /// the import left them out.</summary>
    public sealed record ImportedBookmarksLost : Refusal
    {
        public required string File { get; init; }

        public required IReadOnlyList<BookmarkArea.LostBookmark> Lost { get; init; }
    }

    /// <summary><see cref="Count"/> bookmarks, imported or restored, were left out because others mark their lines.</summary>
    public sealed record BookmarksLeftOut : Refusal
    {
        public required int Count { get; init; }
    }

    /// <summary>The items of the list the refusal tells of, whole, for the note's details, where its line may show only
    /// part of them in the room it is drawn in: every ignored argument, every bookmark an import lost; null where it
    /// tells of no list. In <paramref name="t"/>'s language where an item is the app's words.</summary>
    public IReadOnlyList<string>? ListItems(Strings t)
    {
        return this switch
        {
            ArgumentsIgnored r => r.Arguments,
            ImportedBookmarksLost r => r.Lost.Select(b => b.Words(t)).ToArray(),
            _ => null,
        };
    }

    /// <summary>What was refused and why, in <paramref name="t"/>'s language.</summary>
    public (string What, string Why) Words(Strings t)
    {
        return this switch
        {
            ResourceNotSaved r => (t.ErrorSaveResource(r.Resource), t.DocumentClosed(r.Assembly)),
            PdbNotRead r => (t.ErrorSelectPdb(r.Pdb, r.Assembly), t.DocumentClosed(r.Assembly)),
            NotFullPath r => (t.ErrorOpenFile(r.Path), t.ErrorNotFullPath),
            AssemblyClosed r => (t.DocumentClosed(r.Assembly), ""),
            NotAnalyzable r => (t.ErrorAnalyzeSymbol(r.Symbol), t.ErrorNotOpenable),
            IdNotFound r => (t.ErrorNavigateTo(r.Id), ""),
            ArgumentsIgnored r => (t.ErrorArgumentsIgnored(r.Arguments.Count), string.Join(" ", r.Arguments)),
            NoApiReferencePage r => (t.ErrorNoApiReferencePage(r.Name), t.ErrorNoApiReferencePageDetails),
            Unresolved r => (t.ErrorResolve(r.Reference.Name), t.ErrorNoDefinition(r.Reference)),
            HistoryPlaceGone r => (t.ErrorGoBack(r.Place.Label), t.ErrorNoLongerThere(r.Place)),
            BookmarkGone r => (t.ErrorGoToBookmark(r.Bookmark), t.ErrorNoLongerThere(r.Place)),
            TypeNotDiagrammed r => (t.ErrorShowInDiagram(r.Type.Name), t.ErrorNoDefinition(r.Type)),
            PdbUnusable r => (t.ErrorSelectPdb(System.IO.Path.GetFileName(r.PdbPath), r.Assembly), ProblemTexts.DescribePdbCheck(t, r.Check, r.Assembly)),
            NotBookmarksFile r => (t.ErrorImportBookmarks(System.IO.Path.GetFileName(r.Path)), t.ErrorNotBookmarksFile),
            ImportedBookmarksLost r => (t.ErrorBookmarksNotFound(r.File), BookmarkArea.LostBookmark.JoinWords(t, r.Lost)),
            BookmarksLeftOut r => (t.ErrorDuplicateBookmarks(r.Count), ""),
            _ => throw new UnreachableException($"Refusal.Words does not handle {GetType().Name}."),
        };
    }
}
