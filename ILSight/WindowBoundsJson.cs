namespace ILSight;

/// <summary>Where a window sat and how big it was, as a settings file can hold it.</summary>
internal sealed record WindowBoundsJson
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public bool Maximized { get; init; }
}
