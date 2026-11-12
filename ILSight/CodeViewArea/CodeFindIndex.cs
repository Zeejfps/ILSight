using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>The case-insensitive matches of a query in a document, found once per pair and replaced whole,
/// so that the matches can only be those of that pair.</summary>
internal sealed class CodeFindIndex
{
    /// <summary>What a view holds before it has looked for anything.</summary>
    public static readonly CodeFindIndex None = new() { Matches = [], Document = null, Query = "" };

    public required IReadOnlyList<TextPosition> Matches { get; init; }

    public required DecompiledDocument? Document { get; init; }

    public required string Query { get; init; }

    public int Length => Query.Length;
}
