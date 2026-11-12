namespace ILSight.Core.Metadata;

/// <summary>What a cell's read came to when the library threw reading it, as on a corrupt row: the cell shows "?"
/// and, hovered, <see cref="Reason"/>.</summary>
public sealed class MetadataCellFailure
{
    public required string Reason { get; init; }
}
