using ILSight.Core;

namespace ILSight;

/// <summary>One step of a <see cref="Breadcrumb"/>: a node on the path and its name as the tree labels it.</summary>
public readonly record struct Crumb
{
    public required BrowseNode Node { get; init; }

    public required string Label { get; init; }
}
