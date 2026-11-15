using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>
/// The immutable result of decompiling a node: lines of classified, referencing text. Safe to read
/// from any thread while other documents are being produced.
/// </summary>
public sealed class DecompiledDocument
{
    public required IReadOnlyDictionary<CodeReference.Navigable, int> Definitions { get; init; }

    public required IReadOnlyDictionary<int, CodeAnchor> Statements { get; init; }

    /// <summary>The ILSpy full name of the entity each anchor of this document names (a statement's method, a
    /// definition's type or member), by its assembly's path and token, as the decompile found it; none for an anchor
    /// whose entity it could not resolve.</summary>
    public required IReadOnlyDictionary<(AssemblyPath FilePath, int Token), string> EntityNames { get; init; }

    /// <summary>The line of each definition by its anchor, filled by <see cref="Documents.FindLine"/> the first time it is asked for.</summary>
    internal Dictionary<CodeAnchor, int>? DefinitionLines;

    /// <summary>The statements of each method by its anchor, filled by <see cref="Documents.FindLine"/> the first time it is asked for.</summary>
    public Dictionary<CodeAnchor, (int Offset, int Line)[]>? StatementLines;

    /// <summary>A document with no lines.</summary>
    public static DecompiledDocument Empty { get; } = Documents.CreateFromText("", "");

    /// <summary>A short name for the document, e.g. for a tab.</summary>
    public required string Title { get; init; }

    public required DecompilationLanguage Language { get; init; }

    /// <summary>What decompiling failed with, which the lines describe; null when it did not fail.</summary>
    public required Exception? Error { get; init; }

    /// <summary>The references of the module whose file was found but could not be read, which the code says it read by
    /// name only; empty for most.</summary>
    public IReadOnlyList<UnreadReference> UnreadReferences { get; init; } = [];

    /// <summary>What writing parts of the code threw where the rest was written, each said in its place in the text (a
    /// method whose C# could not be written above its IL): the exceptions, never in the text, for the app to report;
    /// empty for most.</summary>
    public IReadOnlyList<Exception> PartFailures { get; init; } = [];

    public required ImmutableArray<DocumentLine> Lines { get; init; }

    /// <summary>The app's notes the lines hold beside the code, each kept as its cause and where it stands, ordered by
    /// line; empty for most.</summary>
    public ImmutableArray<PlacedNote> Notes { get; init; } = [];

    private readonly DecompiledDocument? _asMade;

    /// <summary>The document as it was made: this one, or the one whose notes it says in another language
    /// (<see cref="Documents.Reword"/>), whose code, lines, folds and anchors it shares.</summary>
    public DecompiledDocument AsMade
    {
        get => _asMade ?? this;
        init => _asMade = value;
    }

    /// <summary>The foldable regions, ordered by <see cref="FoldRegion.StartLine"/>, each starting on
    /// a line of its own and nested whole inside any region it starts in.</summary>
    public required ImmutableArray<FoldRegion> Folds { get; init; }

    public override string ToString()
    {
        return Title;
    }
}
