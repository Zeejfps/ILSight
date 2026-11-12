using ILSight.Core;

namespace ILSight;

/// <summary>Details asked for, still to be read, and the loading they take the place of.</summary>
internal sealed record VersionDetailsFetch
{
    public required VersionDetailsKey Key { get; init; }

    public required NuGetSource Feed { get; init; }

    public required FeedVersion Version { get; init; }

    public required VersionDetails.Loading Loading { get; init; }
}
