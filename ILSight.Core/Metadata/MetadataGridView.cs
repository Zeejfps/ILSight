namespace ILSight.Core.Metadata;

/// <summary>The rows a grid shows, worked out over its cells' values (<see cref="MetadataGrids.ArrangeRows"/>): the
/// order sorted, null for the table's own, and the rows of it that pass the filters.</summary>
public sealed class MetadataGridView
{
    public required int[]? Order { get; init; }

    public required int[] Rows { get; init; }
}
