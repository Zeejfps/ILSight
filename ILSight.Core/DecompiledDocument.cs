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

    public required ImmutableArray<DocumentLine> Lines { get; init; }

    /// <summary>The foldable regions, ordered by <see cref="FoldRegion.StartLine"/>, each starting on
    /// a line of its own and nested whole inside any region it starts in.</summary>
    public required ImmutableArray<FoldRegion> Folds { get; init; }

    public override string ToString()
    {
        return Title;
    }
}
