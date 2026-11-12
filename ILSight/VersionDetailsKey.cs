using ILSight.Core;

namespace ILSight;

/// <summary>Which version of which package of which feed details are of.</summary>
internal readonly record struct VersionDetailsKey(FeedUrl Feed, PackageId Id, PackageVersion Version);
