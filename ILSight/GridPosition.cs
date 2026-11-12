namespace ILSight;

/// <summary>Where a metadata grid stood: the row at its top, its sideways scroll, and the row selected and
/// the column of its current cell, each null when there is none.</summary>
public readonly record struct GridPosition
{
    public required int TopRow { get; init; }

    public required float ScrollX { get; init; }

    public required int? Row { get; init; }

    public required int? Column { get; init; }
}
