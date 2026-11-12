namespace ILSight;

/// <summary>A step of a place as a settings or bookmarks file can hold it. Without a segment that reads,
/// or a label, it is none.</summary>
internal sealed record SavedPathStepJson
{
    public string? Step { get; init; }

    public string? Label { get; init; }
}
