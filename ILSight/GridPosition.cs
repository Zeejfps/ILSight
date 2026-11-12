namespace ILSight;

/// <summary>Where a metadata grid stood: the row at its top, its sideways scroll, and the row selected and
/// the column of its current cell, each null when there is none.</summary>
public readonly record struct GridPosition(int TopRow, float ScrollX, int? Row, int? Column);
