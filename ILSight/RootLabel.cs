namespace ILSight;

/// <summary>How a root reads: as tabs show it, and as its row does.</summary>
internal readonly record struct RootLabel
{
    public required string Title { get; init; }

    public required string Row { get; init; }
}
