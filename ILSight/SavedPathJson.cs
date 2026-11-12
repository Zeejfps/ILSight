namespace ILSight;

/// <summary>A place as a settings or bookmarks file can hold it, in the format of <see cref="Version"/>: a
/// step per node down from the root of the listed file. Of another version, or with a step that cannot be
/// read, it is none.</summary>
internal sealed record SavedPathJson
{
    public int Version { get; init; }

    public List<SavedPathStepJson?>? Steps { get; init; }

    public const int CurrentVersion = 1;
}
