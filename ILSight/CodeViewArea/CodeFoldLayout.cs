using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>Which of a document's folds are collapsed and the rows that leaves, immutable and replaced whole by
/// <see cref="CodeFolding"/>, so that the rows can't fall out of step with the folds.</summary>
internal sealed class CodeFoldLayout
{
    /// <summary>What a view holds before it shows a document.</summary>
    public static readonly CodeFoldLayout None = new()
    {
        Document = DecompiledDocument.Empty,
        Collapsed = [],
        Rows = [],
        IsIdentity = true,
        RowCount = 0,
    };

    public required DecompiledDocument Document { get; init; }

    /// <summary>Whether each of the document's folds is collapsed.</summary>
    public required ImmutableArray<bool> Collapsed { get; init; }

    /// <summary>The line each row shows, while <see cref="IsIdentity"/> is false.</summary>
    public required ImmutableArray<int> Rows { get; init; }

    /// <summary>Whether no fold is collapsed, so that every row shows the line of its own number.</summary>
    public required bool IsIdentity { get; init; }

    public required int RowCount { get; init; }
}
