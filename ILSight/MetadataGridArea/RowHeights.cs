namespace ILSight.MetadataGridArea;

/// <summary>How tall a row of a metadata grid is: its <see cref="Cells"/>, and the
/// <see cref="Details"/> shown under it.</summary>
internal readonly record struct RowHeights
{
    public required float Cells { get; init; }

    public required float Details { get; init; }
}
