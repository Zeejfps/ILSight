using ILSight.Core;

namespace ILSight;

/// <summary>What a search asked of which feed.</summary>
public sealed record FeedSearch(NuGetSource Feed, string Query, bool IncludePrerelease, int Page);
