using ILSight.Core;

namespace ILSight.SearchArea;

/// <summary>The query last searched for, what it looked for, and the assembly list's version it searched.</summary>
public sealed record SearchedQuery
{
    public required string Query { get; init; }

    public required SearchKinds Kinds { get; init; }

    public required int Version { get; init; }
}
