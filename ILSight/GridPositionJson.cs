namespace ILSight;

/// <summary>Where a metadata grid stood, as a settings file can hold it.</summary>
internal readonly record struct GridPositionJson
{
    public int TopRow { get; init; }

    public float ScrollX { get; init; }

    public int? Row { get; init; }

    public int? Column { get; init; }
}
