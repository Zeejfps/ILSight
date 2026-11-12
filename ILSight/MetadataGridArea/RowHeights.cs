namespace ILSight.MetadataGridArea;

/// <summary>How tall a row of a metadata grid is: its <paramref name="Cells"/>, and the
/// <paramref name="Details"/> shown under it.</summary>
internal readonly record struct RowHeights(float Cells, float Details);
