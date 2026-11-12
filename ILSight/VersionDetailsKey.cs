using ILSight.Core;

namespace ILSight;

/// <summary>Which version of which package of which feed details are of.</summary>
internal readonly record struct VersionDetailsKey
{
    public required FeedUrl Feed { get; init; }

    public required PackageId Id { get; init; }

    public required PackageVersion Version { get; init; }
}
